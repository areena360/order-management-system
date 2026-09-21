=== OMS WooCommerce Connector ===
Requires at least: 6.5
Requires PHP: 8.1
Requires Plugins: woocommerce
Stable tag: 1.0.2
License: GPLv2 or later

Connect your WooCommerce store to your existing OMS customer account.

== Installation ==
1. Install and activate WooCommerce, then upload the oms-woocommerce ZIP in Plugins.
2. Open WooCommerce > OMS Integration.
3. Enter the HTTPS OMS API URL ending /api and the HTTPS OMS web URL.
4. Connect, open OMS authorization, sign in and approve this store with production defaults.
5. Return to WordPress and click Finish connection.
6. Choose an initial import period. New order events enqueue automatically.

== Data and permissions ==
Orders, buyer contact/address data, product references, image URLs and payment method identifiers
are transmitted to the OMS server you configure. Card details and WordPress passwords are not sent.
Only manage_woocommerce users can configure the connector. Requests verify TLS certificates.
Credentials are stored in a non-autoloaded WordPress option. Protect WordPress database/backups.
OMS stores only a hash of each installation credential. Reauthorization rotates the credential.
Deactivation stops tasks but does not revoke the server credential: disconnect first when retiring a store.
Deleting this plugin does not delete WooCommerce or imported OMS orders.

== Synchronization ==
Event jobs retry with bounded exponential backoff. Scheduled reconciliation recovers missed orders.
Initial OMS manufacturing defaults are selected explicitly during approval. Store changes refresh
the retail snapshot, preserving OMS production edits. Outbound status changes require explicit
per-store mapping in OMS; these can trigger WooCommerce emails/stock/extension behavior.
Tracking is stored in _oms_tracking_number and displayed in the WooCommerce order admin screen.
Images are retained as HTTPS references, not downloaded to the OMS server.
Use a server cron for reliable background execution; inspect Scheduled Actions for stuck jobs.
