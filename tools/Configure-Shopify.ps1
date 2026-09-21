[CmdletBinding()]
param([Parameter(Mandatory)][string]$ClientId,[Parameter(Mandatory)][string]$PublicUrl)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$PublicUrl=$PublicUrl.TrimEnd('/')
$parsed=[uri]$PublicUrl
if($parsed.Scheme -ne 'https' -or $parsed.UserInfo -or $parsed.Query -or $parsed.Fragment -or $parsed.AbsolutePath -ne '/') {throw 'Use the public HTTPS origin only, without a path, query or credentials.'}
if($ClientId -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid Shopify client ID.'}
$secure=Read-Host 'Shopify Client secret (hidden; never paste in chat)' -AsSecureString
$ptr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {$secret=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)} finally {[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)}
if([string]::IsNullOrWhiteSpace($secret)){throw 'Client secret is required.'}
$settings=Join-Path $repo 'OMS_Backend/OMS_Backend/appsettings.Local.json'
$config=@{}
if(Test-Path $settings){
    # Windows PowerShell 5.1 does not support ConvertFrom-Json -AsHashtable.
    # Preserve existing top-level settings while remaining compatible with it.
    (Get-Content $settings -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object {
        $config[$_.Name]=$_.Value
    }
}
$config['Shopify']=@{Enabled=$true;ClientId=$ClientId;ClientSecret=$secret;PublicBaseUrl=$PublicUrl;FrontendUrl=$PublicUrl}
$json=$config|ConvertTo-Json -Depth 30
Set-Content -LiteralPath $settings -Value $json -Encoding utf8
# Restrict this private configuration to its current owner and SYSTEM.
$acl=Get-Acl -LiteralPath $settings
$acl.SetAccessRuleProtection($true,$false)
$identity=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$acl.SetAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($identity,'FullControl','Allow'))
$acl.SetAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new('NT AUTHORITY\SYSTEM','FullControl','Allow'))
try {
    Set-Acl -LiteralPath $settings -AclObject $acl
} catch {
    # The file may already be protected, while a non-elevated shell lacks the
    # privilege required to rewrite its ACL. Keep the private configuration
    # usable and report the condition instead of discarding the new settings.
    Write-Warning 'Private settings were saved, but Windows could not rewrite their ACL from this shell. Verify file permissions before production deployment.'
}
$secret=$null;$json=$null;$config=$null
$out=Join-Path $repo 'artifacts/shopify-app'
New-Item -ItemType Directory -Path $out -Force|Out-Null
$toml=Get-Content (Join-Path $repo 'Shopify/shopify.app.toml') -Raw
$toml=$toml.Replace('REPLACE_WITH_DEV_DASHBOARD_CLIENT_ID',$ClientId).Replace('https://REPLACE_WITH_PUBLIC_HOST',$PublicUrl)
Set-Content (Join-Path $out 'shopify.app.toml') $toml -Encoding utf8
Write-Output "Private settings saved. App configuration: $out/shopify.app.toml"
Write-Output 'Deploy the Shopify app version, then restart the OMS backend. Client secret was not printed.'
