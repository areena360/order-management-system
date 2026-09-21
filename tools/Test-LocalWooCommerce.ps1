$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$demo = Get-Content -LiteralPath (Join-Path $repo 'artifacts/local-demo.json') -Raw | ConvertFrom-Json
$base = $demo.apiUrl
$login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -ContentType 'application/json' -Body (@{email=$demo.adminEmail;password=$demo.password} | ConvertTo-Json)
$headers = @{Authorization="Bearer $($login.token)"}
$checks = [Collections.Generic.List[string]]::new()
function Check($condition, $name) { if (-not $condition) { throw "FAIL: $name" }; $checks.Add($name); Write-Output "PASS: $name" }
$stores = Invoke-RestMethod -Uri "$base/integrations/woocommerce/connections" -Headers $headers
Check ($stores.Count -eq 1 -and $stores[0].orderCount -ge 10) 'WordPress authorization and ten real order imports'
$imported = Invoke-RestMethod -Uri "$base/orders?source=WooCommerce" -Headers $headers
Check ($imported.totalCount -ge 10 -and $imported.items[0].source -eq 'WooCommerce') 'Manage Orders WooCommerce source filter'
$orderId = $imported.items[0].id
$details = Invoke-RestMethod -Uri "$base/orders/$orderId" -Headers $headers
$snapshot = Invoke-RestMethod -Uri "$base/integrations/woocommerce/orders/$orderId" -Headers $headers
Check ($snapshot.total -eq 246.90 -and $snapshot.currency -eq 'USD') 'Retail decimal total and currency survive real payload'
Check (($snapshot.itemsJson | ConvertFrom-Json)[0].Sku -eq 'OMS-DEMO-001') 'SKU and line items survive real payload'
$details.trackingNumber = 'OMS-LOCAL-TRACK-001'
Invoke-RestMethod -Method Put -Uri "$base/orders/$orderId" -Headers $headers -ContentType 'application/json' -Body ($details | ConvertTo-Json -Depth 20) | Out-Null
$statuses = Invoke-RestMethod -Uri "$base/lookups/by-type/1" -Headers $headers
$shipped = ($statuses | Where-Object name -eq 'Shipped').id
$store = $stores[0]
$mapping = @{}; $mapping[[string]$shipped] = 'completed'
$settings = @{defaultGenderId=$store.defaultGenderId;defaultMaterialId=$store.defaultMaterialId;defaultStatusId=$store.defaultStatusId;statusMappings=$mapping}
Invoke-RestMethod -Method Put -Uri "$base/integrations/woocommerce/connections/$($store.id)/settings" -Headers $headers -ContentType 'application/json' -Body ($settings | ConvertTo-Json -Depth 10) | Out-Null
Invoke-RestMethod -Method Patch -Uri "$base/orders/$orderId/status" -Headers $headers -ContentType 'application/json' -Body (@{statusId=$shipped}|ConvertTo-Json) | Out-Null
& (Join-Path $repo 'artifacts/php/php.exe') (Join-Path $PSScriptRoot 'local-wordpress.php') tick
if ($LASTEXITCODE -ne 0) { throw 'WordPress queue processing failed.' }
$verified = & (Join-Path $repo 'artifacts/php/php.exe') (Join-Path $PSScriptRoot 'local-wordpress.php') verify
if ($LASTEXITCODE -ne 0) { throw 'WordPress verification failed.' }
Check ([string]::Join("`n",$verified).Contains('completed | tracking OMS-LOCAL-TRACK-001')) 'OMS status and tracking applied to real WooCommerce order'
$manualOrders = Invoke-RestMethod -Uri "$base/orders?source=Manual" -Headers $headers
if ($manualOrders.totalCount -eq 0) {
    $manual = @{customerProductTitle='Manual regression check';customerOrderNumber='DEMO-MANUAL';customerId=$demo.customerId;genderId=5;customerMaterialId=12;manufacturerMaterialId=12;isCustomSize=$true;sizeDetails='Demo sizing';daysForMaking=2;consigneeName='Demo Manual';consigneeAddress='Demo local address'}
    $created = Invoke-RestMethod -Method Post -Uri "$base/orders" -Headers $headers -ContentType 'application/json' -Body ($manual|ConvertTo-Json)
    Check ($created.manufacturerOrderNumber -eq 'AD1001') 'Existing manual AD order sequence unaffected by WooCommerce imports'
}
$manualOrders = Invoke-RestMethod -Uri "$base/orders?source=Manual" -Headers $headers
Check ($manualOrders.totalCount -ge 1 -and $manualOrders.items[0].source -eq 'Manual') 'Manual orders remain available with their existing workflow'
try {
    Invoke-RestMethod -Uri "$base/integrations/woocommerce/updates" -Headers @{Authorization='Bearer invalid';'X-OMS-Connection'=[string]$store.id} | Out-Null
    throw 'Invalid token unexpectedly accepted.'
} catch {
    Check ($_.Exception.Response.StatusCode.value__ -eq 401) 'Real HTTP endpoint rejects invalid installation token'
}
$before = (Invoke-RestMethod -Uri "$base/orders?source=WooCommerce" -Headers $headers).totalCount
& (Join-Path $repo 'artifacts/php/php.exe') (Join-Path $PSScriptRoot 'local-wordpress.php') seed
if ($LASTEXITCODE -ne 0) { throw 'WordPress duplicate delivery failed.' }
$after = (Invoke-RestMethod -Uri "$base/orders?source=WooCommerce" -Headers $headers).totalCount
Check ($before -eq $after) 'Repeated real WooCommerce sync does not create duplicate OMS orders'
@{completedAt=(Get-Date).ToUniversalTime().ToString('o'); checks=$checks; wordpress=$demo.wordpressUrl; api=$base} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $repo 'artifacts/local-e2e-results.json')
Write-Output "All $($checks.Count) local end-to-end checks passed."
