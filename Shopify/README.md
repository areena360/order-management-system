# Shopify → OMS

This is a standalone Shopify OAuth app hosted by the existing .NET OMS API, with an Angular management page. It is not a WordPress plugin or a ZIP uploaded into Shopify. The code can be installed through a Shopify Dev Dashboard app after store access, app credentials and a public HTTPS host are configured.

## Included behavior

- Owner-bound OAuth with signed callback, expiring offline tokens encrypted with ASP.NET Data Protection, refresh, disconnect and uninstall handling.
- Signed webhooks durably queued and deduplicated. Orders are fetched fresh from Admin GraphQL 2026-07; delayed/repeated events do not create duplicate rows.
- Initial 30-day import, five-minute reconciliation and per-order retry. Backend must remain running. Imports can request 1–60 days; older history needs extra Shopify approval and is not enabled here.
- One OMS production row per current product unit. Quantity 2 creates two rows with the same customer order number. `SH` numbers and Source = Shopify distinguish these from WooCommerce and manual orders.
- Quantity increases add missing rows. Reductions retain previous production work for review. Incoming store changes update snapshots without overwriting existing production edits, pricing or manufacturing status.
- Featured and product gallery images as HTTPS references (up to 100 per line), variants/SKU, billing/shipping, buyer, retail totals and payment gateway. Product-update webhooks refresh already imported orders containing that product.
- Optional fulfillment/tracking back to merchant-managed Shopify fulfillment orders, disabled by default. All current units must reach the configured shipped status. No payment capture, refunds, inventory adjustment or automatic cancellation. Customer emails are not sent by these fulfillment mutations.
- Privacy request export and explicit resolution in job logs; signed redaction requests scrub imported buyer/address/note/attribute data and prevent customer data reimport. Uploaded production files, chat, external backups and legally retained accounting records require the operator's separate retention review.

## Configure and install

1. Use the intended store's `.myshopify.com` domain. Obtain permission to install apps and access the Shopify Dev Dashboard. Create an app named **A360 OMS Connector**. For a single merchant choose custom distribution as appropriate; distribution to unrelated merchants requires a public app and Shopify review. A draft local app is not App Store approved.
2. Keep the original API, frontend gateway and public HTTPS host running. Production needs a stable hostname; temporary tunnels change on restart. For local testing, build with `npm run build -- --configuration woocommerce-tunnel` inside `OMS_Frontend`, then run `./tools/Start-ShopifyTunnel.ps1` from the repository. The Shopify gateway uses loopback port 5523 and its URL appears in `artifacts/shopify-tunnel.stderr.log`; it leaves the existing WooCommerce gateway on 5522 alone. Stop it with `./tools/Stop-ShopifyTunnel.ps1`. These scripts use the previously installed official `artifacts/cloudflared.exe`. They expose only login, necessary lookups/profile and Shopify integration routes; other order/user/file APIs stay blocked. Use the same public origin for the frontend and `/api` during installation because the callback is protected by an HttpOnly browser cookie. Ordinary order management remains available on local port 4200.
3. In PowerShell 7 from the repository run:
   ```powershell
   ./tools/Configure-Shopify.ps1 -ClientId 'YOUR_CLIENT_ID' -PublicUrl 'https://YOUR_PUBLIC_HOST'
   ```
   Enter the Client secret at the hidden prompt. It writes gitignored `OMS_Backend/OMS_Backend/appsettings.Local.json` and `artifacts/shopify-app/shopify.app.toml`. Do not send secrets in chat, commit the local file, or package it for distribution. Restart under the same Windows account so the encrypted token keyring remains accessible. IIS production deployments need a persistent, protected Data Protection keyring; back up it securely with the database or reinstall/reconnect after a machine change.
4. In the Dev Dashboard configure the generated TOML values, or link/deploy with Shopify CLI from `artifacts/shopify-app`: `shopify app config link`, then review the generated file against this template and `shopify app deploy`. Linking can rewrite configuration: preserve the OAuth URLs, requested scopes, legacy installation flow and webhook subscriptions. Deployment creates/releases the Shopify app configuration; it does not host the .NET backend.
5. Request/enable protected customer data access needed for names, email, phone and addresses. Confirm access scopes include `read_orders`, `read_products`, `read_merchant_managed_fulfillment_orders`, `write_merchant_managed_fulfillment_orders`. A store test cannot pass with denied customer-data permissions.
6. Ensure app URL = `https://HOST/dashboard/integrations/shopify`, allowed redirect = `https://HOST/api/integrations/shopify/callback`, all listed webhook topics target `https://HOST/api/integrations/shopify/webhooks`. Release the app version and use its install link for the target store.
7. Sign into the public OMS app URL with an existing OMS Customer, or Admin/Super Admin. Select **Shopify**, enter the store domain, choose owner `a360team2@gmail.com` if appropriate, and gender/material/initial status. Click **Connect with Shopify** and approve permissions in Shopify. The existing customer account is not the Shopify shopper; it is the OMS owner of the store's production orders.
8. Return to local OMS port 4200 → Shopify, confirm Connected. Create a test order in Shopify and wait around 15–60 seconds for a webhook job (API and gateway running). Under Orders select Source Shopify. If no webhook arrives, five-minute reconciliation recovers it; **Import / sync last 30 days** and **Retry order** are also available. Use Shopify's numeric order ID, not the display order number.

## Acceptance checks

Use a development/test store and Shopify test payments. Test quantity 2, two different products, multiple gallery images, product gallery update, repeat sync (no duplicate), quantity increase/decrease, edits retained, other customer's access denied, API downtime followed by retry, token refresh, disconnect/reconnect and uninstall. Only enable fulfillment after checking the shipped-status mapping. Verify tracking in Shopify with a merchant-managed location; third-party fulfillment services are not supported by this flow.

Privacy jobs appear in **View logs**. For `customers/data_request`, download the export, verify the requester and deliver through an approved channel, then explicitly mark resolved. Do not blindly mark privacy requests resolved. Monitor logs and backups as part of operating the service.

## Existing OMS safety

The additive `AddShopifyIntegration` migration adds four Shopify tables and a Provider column defaulting existing connections to WooCommerce. It does not replace Orders, users or WooCommerce rows. Back up and verify the actual database before applying. Do not roll back this migration while running binaries that query Provider. Disable Shopify with `Shopify:Enabled=false` to stop its worker/routes while retaining imported production data.

`tests/OMS.WooCommerce.Smoke` creates a disposable GUID-named LocalDB database, verifies additive migrations, existing WooCommerce behavior, Shopify HMAC, mapping, deduplication, privacy and fulfillment safeguards. It does not use the application's database. Real Shopify OAuth/webhook/API behavior still requires the target app/store acceptance checks.

Official references: [standalone authorization](https://shopify.dev/docs/apps/build/authentication-authorization/authenticate-standalone-apps), [app configuration](https://shopify.dev/docs/apps/build/cli-for-apps/app-configuration), [webhook subscriptions](https://shopify.dev/docs/apps/build/webhooks/subscribe), [distribution](https://shopify.dev/docs/apps/launch/distribution), [protected customer data](https://shopify.dev/docs/apps/launch/protected-customer-data).
