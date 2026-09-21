# Local verification — 18 September 2026

- 65 checks passed in the disposable SQL Server LocalDB regression harness, including existing WooCommerce behavior and Shopify signing, domain validation, encryption, per-unit import, images, idempotency, privacy redaction and fulfillment quantity safeguards.
- Angular production and HTTPS gateway builds pass. The normal build reports the existing initial-bundle warning (about 792 kB vs 500 kB warning threshold; below 1 MB error threshold).
- .NET build passes with seven pre-existing warnings in unrelated models/auth/email code.
- Original database backup verified with CHECKSUM and RESTORE VERIFYONLY:
  `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OMS_before_Shopify_65078DB8388245798B25CD396AEC1FE3.bak`
- Additive migration `20260918111002_AddShopifyIntegration` applied to the original database.
- Before and after: Orders = 16, checksum = -371910498; WooCommerceOrder rows = 10, checksum = 90217274. Existing connection Provider = WooCommerce. No Shopify store or live Shopify order was created.
- Original backend restarted. Shopify and WooCommerce management endpoints reject unauthenticated requests with HTTP 401.

## Still requires the merchant's Shopify account

Dev Dashboard app creation/release, store installation, protected customer data permission, actual OAuth exchange, real signed webhook delivery, token refresh and fulfillment on a development store have not been tested against Shopify. No client credentials or `.myshopify.com` store domain have been supplied. The Shopify page therefore shows **App setup required** and disables connection until configured.

## Operational limits

200 distinct product lines / 1,000 units per imported order; up to 100 image URLs per product line. Automatic fulfillment supports at most 10 fulfillment orders with at most 50 lines each, and merchant-managed locations only; larger fulfillment requests stop for manual handling. Payments, refunds, inventory and financial accounting are not synchronized. Privacy requests involving files, chat, backups or legally retained records require the operator's review.

Local setup is not an always-on hosted service. Backend and public HTTPS gateway must stay running. A changed tunnel URL requires updating app URLs/webhooks and private settings. Shopify app configuration deployment does not deploy the OMS server.

