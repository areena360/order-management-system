# Order statuses

All OMS order status selectors use `GET /api/lookups/by-type/1`, which returns
Assign, In Manufacturing, Refund, and Cancel, in that order. Manual orders default
to Assign. The backend rejects retired statuses for updates and integration settings.
User approval statuses and external providers' native status names are separate.

Apply migration `20260924100000_RestrictOrderStatuses` when deploying the backend:

```powershell
dotnet ef database update --project OMS_Backend/OMS_Backend
```

The migration resets existing orders to Assign and records that reset in status
history. Old lookup rows remain inactive so previous history retains its original
meaning; they are absent from all order status dropdowns. Other lookup types are
unchanged. Existing assignment flags/dates remain unchanged.

Integration defaults using retired statuses become Assign. Shopify fulfillment
triggers using retired statuses are disabled rather than remapped to Assign.
Review fulfillment settings before re-enabling them. WooCommerce settings save
only mappings for current OMS statuses; native WooCommerce status values retain
their provider-defined names.

Restart the backend and reload the frontend after deployment. The migration is a
one-time data reset; rolling it back requires restoring a database backup rather
than deleting lookup rows referenced by orders.

Local SQL Server smoke test (all checks run in a rolled-back transaction):

```powershell
dotnet run --project tests/OMS.OrderStatuses.Smoke
```

Add `-- --apply` to apply this migration after verification. The smoke runner
refuses remote servers and refuses to apply unrelated pending migrations.

## Assignment dates and draft visibility

Migration `20260924112459_OrderAssignmentVisibility` permanently removes
`Orders.CreatedDate` and `Orders.UpdatedDate`. History, chat, user and inventory
timestamps remain intact. A schema rollback cannot restore the removed date values.

The Orders table and details use Assign Date and Days Passed. Assignment time is
set by the server on the first customer assignment and survives repeat/concurrent
requests. Date filters and default sorting use assignment time.

`RequiresCustomerAssignment` records order origin independently of later role
changes: customer-created and imported orders remain private until assigned;
staff-created orders are immediately visible. The migration classifies existing
orders from their creator and import link; unknown origins require assignment.
Customer ownership and staff visibility are enforced on list/detail/edit, image,
inventory, snapshot and chat endpoints. Draft chat is unavailable until assignment.

Apply the migration with the updated backend, then restart the backend before
using Orders. Older backend binaries still expect the deleted columns.

Assignment database checks (temporary schema/data changes are rolled back):

```powershell
dotnet run --project tests/OMS.OrderStatuses.Smoke -- --assignment
```

Add `--apply` to that command to apply the verified migration to the local DB.
