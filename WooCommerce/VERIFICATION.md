# Verification record — 16 September 2026

## Passed

- .NET Release build: zero errors. Seven pre-existing nullable/member-hiding warnings on a full rebuild.
- Angular development and production builds: passed. Production initial bundle 788.91 kB; warning threshold 500 kB, error threshold 1 MB. Budgets were not weakened.
- PHP 8.5.10 syntax checks: all four connector PHP files passed.
- EF pending-model check: no unscaffolded model changes.
- SQL Server smoke suite: **35 checks passed** on a randomly named disposable LocalDB database, cleaned up afterward. Includes applying the actual additive migration to a pre-integration schema with an existing order and verifying its amount, tracking and number remained unchanged.
- Real WordPress 7.1 / WooCommerce 11.1.0 / MariaDB 11.4.12 integration: ten orders imported, decimal totals and SKU preserved, source filtering, manual AD1001 order creation, reverse status/tracking, invalid-token rejection and duplicate delivery verified.
- WooCommerce legacy order storage tested, then existing test orders synchronized to HPOS and verified with HPOS enabled.
- Real API outage simulation: retry remains queued after attempt eight; restored API accepts the order; connector metadata does not cause repeated unchanged sends.
- Automatic event delivery: WooCommerce order 22 was created without calling the send method; the background worker delivered it to OMS order 12.
- Browser checks: customer login, connected-store page, sync logs, manual/imported orders list, retail order details, and WordPress connector administration.
- ZIP entries verified: one `oms-woocommerce/` root containing the bootstrap, three include classes and readme. SHA-256 is alongside the ZIP.
- `git diff --check`: passed.

## State left for use

Update: original OMS activation was subsequently requested and completed. See `ORIGINAL-ACTIVATION.md` for the verified backup, migration, unchanged data checksums and current connection boundary. The original-database statement below records the earlier demo-only verification stage.

- Local demo processes running on localhost ports 4201, 5511, 8088 and 127.0.0.1:3308, plus a local queue worker.
- Newly generated demo credentials: `artifacts/LOCAL-ACCESS.md` (gitignored).
- Plugin: `artifacts/oms-woocommerce-1.0.0.zip`.
- Idempotent integration SQL: `artifacts/woocommerce-migration.sql`.
- Source baseline backup: `artifacts/oms-before-woocommerce.zip`.
- The original SQL Express `OrderManagementSystemDb` was not changed. The new integration remains disabled in the original API configuration until explicitly enabled after migration. Demo SQL data is isolated in `OMS_Woo_Local_<GUID>`.
- No production hosting/deployment was performed. See README for activation, rollback and implementation boundaries.
