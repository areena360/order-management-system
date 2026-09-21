# Original local OMS activation — 16 September 2026

Completed at the user's request:

- SQL Express database: `DESKTOP-7NI37TI\SQLEXPRESS` / `OrderManagementSystemDb`.
- Created a COPY_ONLY backup WITH CHECKSUM and successfully ran RESTORE VERIFYONLY WITH CHECKSUM.
- Verified backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OMS_before_WooCommerce_20260916_1B2B94DD96C94E779788D7AF0D0BD6F9.bak`.
- Applied migration `20260916082854_AddWooCommerceIntegration`: five new tables; no existing business records edited.
- Before/after Orders: 6 rows, checksum `-863522007`.
- Before/after Users: 33 rows, checksum `456622221`.
- Enabled WooCommerce and loopback-only local HTTP in `appsettings.Development.json`.
- Built and restarted the original IIS Express site using its existing configuration and bindings. Build passed with seven existing warnings.
- Original frontend: http://localhost:4200; HTTPS API: https://localhost:44370/api; local WordPress-compatible HTTP API: http://localhost:52984/api.
- Confirmed Swagger responds and exposes 14 integration routes. Invalid authorization payload receives the intended 400 validation response, confirming feature and local transport are enabled without creating any business data.

The selected owner is the existing active Customer Naveed Syed, `a360team2@gmail.com` (ID 38). The demo credential has been revoked and WordPress now targets the original API on port 52984. Authorization is prepared but awaits the owner's normal OMS login/approval and WordPress Finish connection. No password was reset and no new account was created. No demo orders have been imported into the original database. Do not use the demo credentials on the original OMS. If the ten-minute approval expires, click Connect to OMS / reauthorize again.

To reconnect through the UI after choosing the owner: WordPress → WooCommerce → OMS Integration → Disconnect, then API `http://localhost:52984/api` and web `http://localhost:4200` → Connect → Open OMS authorization → sign into the original OMS and select owner/defaults → approve → Finish connection in WordPress. Choose the desired import range explicitly.

The API can be started normally through Visual Studio's existing IIS Express profile. `tools/Restart-OriginalOms.ps1` also rebuilds/restarts only this checkout's IIS Express instance. This interrupts an attached debugging session. It does not start/stop the isolated demo. If starting through Visual Studio while the helper-started instance still runs, stop that instance first to avoid a port conflict.

Rollback of the integration feature: set `WooCommerce:Enabled` to false in Development configuration and restart the original API. Retain the new tables; do not run the destructive Down migration on populated integration data. The verified backup is available for a deliberate full restore if needed.
