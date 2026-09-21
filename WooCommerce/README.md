# OMS WooCommerce integration

This repository now includes the WordPress plugin, .NET integration API, additive database migration, Angular integration screens and an isolated local demonstration.

Quantity import has since been updated to one OMS row per product unit. See [Quantity units and UI update](QUANTITY-UNITS.md) for migration, status aggregation and quantity-change behavior; it supersedes the original single-row mapping below.

## Open the installed local demo

| Application | URL |
| --- | --- |
| OMS demo | http://localhost:4201 |
| WordPress administration | http://localhost:8088/wp-admin/ |
| WordPress connector dashboard | http://localhost:8088/wp-admin/admin.php?page=oms-woocommerce |
| Demo API / Swagger | http://localhost:5511/swagger |

Demo credentials are generated locally, not committed:

- `artifacts/local-demo.json`: OMS `adminEmail`, `customerEmail`, and `password`.
- `artifacts/wordpress-local.json`: WordPress `adminUser` and `adminPassword`.

Open these files locally. Do not share their contents or commit the artifacts directory.

From the repository root, run in PowerShell 7:

```powershell
pwsh -NoProfile -File tools/Start-LocalWooCommerce.ps1
pwsh -NoProfile -File tools/Stop-LocalWooCommerce.ps1
```

The start script runs portable MariaDB (127.0.0.1:3308), WordPress (localhost:8088), the demo API (localhost:5511), Angular (localhost:4201), and a one-minute background queue worker. No Windows services are installed. Check `artifacts/woo-*.log` if a port is already occupied. The stop script only stops its recorded, recognizable demo processes and retains their data.

The demo SQL database is a separately generated `OMS_Woo_Local_<GUID>` on LocalDB. On 16 September 2026, at the user's request, the original `OrderManagementSystemDb` was backed up, the backup verified, and the additive migration applied. Existing orders and users retained identical counts and checksums. Integration is now enabled in the original API's Development configuration. WordPress remains connected to the isolated demo until an original customer account is selected and the store is reauthorized. See `ORIGINAL-ACTIVATION.md`. Original Angular development URLs remain unchanged. The original committed source is archived locally as `artifacts/oms-before-woocommerce.zip`.

The demo has imported sample retail orders, a manual `AD1001` regression order, an automatic event-delivery test order, and one example shipment/tracking update. Both OMS demo accounts use the same generated demo password. WordPress is configured to suppress email and external requests; do not use this local runtime for production.

## Plugin ZIP

Install `artifacts/oms-woocommerce-1.0.0.zip` using WordPress → Plugins → Add New → Upload Plugin, then activate it with WooCommerce active. Rebuild it after editing PHP:

```powershell
pwsh -NoProfile -File tools/Build-WooCommercePlugin.ps1
```

The plugin source is `WooCommerce/oms-woocommerce/`. It is a WordPress plugin, not a Codex plugin.

## Connect another store

1. In WordPress → WooCommerce → OMS Integration, enter the OMS API URL ending `/api` and the OMS frontend URL.
2. Click **Connect to OMS**, then **Open OMS authorization**.
3. Sign into OMS. Verify the exact store address, select the initial gender/material/status defaults, and authorize. An OMS administrator also selects the existing customer account that owns the store.
4. Return to WordPress and click **Finish connection**. No WordPress password or manual integration-token copying is required.
5. Select All / Last 30 / Last 90 / Custom start date and start an import. New orders are queued automatically.
6. In OMS → WooCommerce, optionally map OMS statuses to WooCommerce statuses. Unmapped statuses leave WooCommerce status unchanged.

For this local demo the API URL is `http://localhost:5511/api` and frontend URL is `http://localhost:4201`. HTTP works only with both of these explicit local settings:

```php
// Local WordPress wp-config.php only:
define('WP_ENVIRONMENT_TYPE', 'local');
define('OMS_WOO_ALLOW_LOCAL_HTTP', true);
```

The .NET application must run in Development with `WooCommerce__AllowLocalHttp=true`, and the request must come from a loopback address to a literal loopback hostname. Production and non-loopback requests require HTTPS. HTTPS certificate verification is never disabled. Do not use localhost URLs for a remotely hosted store.

## Original OMS activation / other installations

The original local OMS has now been migrated and enabled in `appsettings.Development.json`. The instructions below are for another installation; do not repeat migration/backup unnecessarily. Outside Development, the feature flag still defaults to **off** unless explicitly configured, so the new tables are not required by existing order APIs while disabled.

1. Back up the original SQL database and verify the backup before applying a schema change.
2. Review the generated additive SQL at `artifacts/woocommerce-migration.sql` and migration `20260916082854_AddWooCommerceIntegration`.
3. Apply that migration to the intended existing database, using its configured connection. Do not point demo initialization at the original database:

   ```powershell
   dotnet ef database update AddWooCommerceIntegration --project OMS_Backend/OMS_Backend/OMS_Backend.csproj --configuration Release
   ```

4. Set `WooCommerce__Enabled=true` in the intended API environment and restart that API after building. For same-machine local testing only, also use the local HTTP settings described above. Deploy/build Angular from the normal profile; the `woocommerce-local` profile is exclusively for the isolated demo.
5. Connect the store to the original OMS URL and original customer account. The demo connection belongs to the demo database and cannot be reused against another API/database. Disconnect/reconnect from WordPress when changing the destination.

The migration creates five integration tables and indexes. It does not rename/drop existing tables or rewrite old orders. Existing order/customer entities are linked through sidecar records, preserving manual order numbering and existing ownership rules.

Rollback: disable `WooCommerce__Enabled` and disconnect/pause the store. Retain integration tables and imported orders. Do not run the migration's `Down` on a populated installation: it deletes integration metadata. Restore a verified database backup if an actual full rollback is necessary.

## Mapping and synchronization rules

- The OMS **Customer** login remains the business/store owner. WooCommerce buyers are separate, store-scoped contacts. Registered buyers map by external ID; guests map by normalized email hash or external order ID when no email exists. The same email in another store stays separate.
- A WooCommerce order becomes one normal OMS production order titled from its line items. The full line items, product/variation IDs, SKU, quantity, attributes, billing/shipping addresses, external customer ID, status, payment method and decimal retail total/currency are preserved in the retail snapshot on Order Details.
- Imported order numbers use `WC<connection>-<external-order>`. Existing manual `AD` numbering remains independent.
- OMS's existing integer Amount is manufacturing pricing. Retail totals are stored separately as `decimal(18,4)`; they are not rounded into the manufacturing field.
- Initial material/gender/status are explicitly chosen per store. Imported sizing is flagged for production review. Product attributes are preserved, not guessed into OMS material/size lookups.
- Incoming edits/cancellations/refunds refresh the retail snapshot, preserving production fields already edited in OMS. Cancellation does **not** silently cancel manufacturing; the store status remains visible for review.
- Outbound status mapping is explicit per store. Mapping to completed/cancelled/refunded may trigger WooCommerce stock/email/extension behavior; it does not initiate a payment refund. Tracking is stored in `_oms_tracking_number` and shown in the WooCommerce order administration page.
- Product images remain HTTPS references with a “View product image” link. Remote files are not automatically downloaded into OMS storage.
- One SQL unique constraint per store/external order and transaction-scoped application locks protect concurrent imports. Repeated or older payloads do not duplicate orders. Soft-deleted OMS orders are not recreated.
- Event deliveries use Action Scheduler with exponential backoff capped at one hour. Failures continue retrying after long outages. Periodic modified-order scans recover missed hooks and preserve the chosen historical import window. **Sync now** in OMS requests a full reconciliation, collected on the next plugin poll.
- Outbound snapshots are paged in batches of 100, with per-order content revisions to avoid status loops. The normal plugin poll is every ten minutes; the local demo worker processes every minute. Schedule a real WP-Cron runner on production/low-traffic stores.
- Disconnection revokes credentials and retains imported orders. Reauthorizing the same installation/owner/store rotates the token and retains duplicate protection. Deactivating the plugin pauses jobs but does not revoke a server credential; disconnect first when retiring a store.

## Security boundaries

Management endpoints use the existing OMS JWT and active user checks. Only customer/store owners and OMS Admin/Super Admin roles manage connections. An installation bearer credential can access only its own import/update/disconnect endpoints; it cannot access regular OMS order APIs. Credentials are generated with 256 bits of randomness; only their hashes are stored in OMS. Approval codes are single-use and expire after ten minutes. WordPress stores its credential in a non-autoloaded option; protect its database/backups.

Requests use TLS, bounded payloads, validation, rate limiting, WordPress capability/nonces, ownership checks, idempotency and stale-event checks. This connector does not implement a separate HMAC signature protocol; TLS plus the scoped credential authenticate traffic. New integration endpoints do not make the existing OMS a fully independent-business SaaS tenant system: existing manufacturer/admin access remains as designed in this OMS.

## Verification

```powershell
# Disposable SQL Server database; never reads original app connection settings:
dotnet run --project tests/OMS.WooCommerce.Smoke -c Smoke

# Requires running local demo:
pwsh -NoProfile -File tools/Test-LocalWooCommerce.ps1
./artifacts/php/php.exe tools/local-wordpress.php failure-test
./artifacts/php/php.exe tools/local-wordpress.php verify

# Frontend:
cd OMS_Frontend
npm run build
```

Smoke tests cover schema/data preservation, authorization and replay, cross-store isolation, 100-order import, eight concurrent deliveries, decimal amounts, stale events, cancellations, soft deletion, token validation/rotation and disconnect/reconnect. The local end-to-end script covers real WordPress authorization/import, line mapping, source filters, manual orders, status/tracking propagation, invalid credentials and repeated sync. The local store was tested with legacy order storage and HPOS.

Angular production build currently emits an initial-bundle warning (~789 kB versus the 500 kB warning budget); it is below the configured 1 MB error budget. Existing backend nullable/member-hiding warnings remain. No live hosting or production deployment has been performed.

References used: [WooCommerce order queries](https://developer.woocommerce.com/docs/features/orders/wc-get-orders/), [Action Scheduler API](https://actionscheduler.org/api/).
