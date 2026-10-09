# Exception logging

OMS stores diagnostic events in SQL Server's `ExceptionLogs` table. Deploy the
backend/frontend together, run `dotnet ef database update --project
OMS_Backend/OMS_Backend/OMS_Backend.csproj`, and restart the backend. Rebuild and
install the WooCommerce plugin to enable its new reporting queue.

## Coverage

- API exceptions, including failures after response headers were sent.
- Returned HTTP 4xx/5xx responses: validation, authorization, missing routes and
  rate-limit rejections. These are classified as `HttpResponse`, not invented exceptions.
- Handled activation-email, role-delete, duplicate-webhook and invalid-file-grant exceptions.
- SignalR method, connect and disconnect failures.
- Shopify background job, store synchronization and worker-loop failures.
- Host startup/run failures after configuration loads. EF design-time host shutdown
  is excluded; migration commands use a dedicated DbContext factory.
- Angular ErrorHandler, browser errors/unhandled promises, bootstrap failures,
  network failures and explicitly handled chat/integration/UI failures.
- WooCommerce plugin scheduled callback exceptions and reported sync failures:
  bounded WordPress queue, authenticated delivery on the next scheduled tick.

Expected cancellation during shutdown or a disconnected HTTP request is excluded.
Safe parsing/validation fallbacks (invalid URL, missing cached token, invalid date)
are not operational faults. Shopify signature parsing uses nonthrowing validation.
HTTP failures are recorded server-side instead of reporting them twice from Angular.
Exceptions handled inside third-party libraries without surfacing to these boundaries,
OS/process termination and scripts outside the running application are not captured.

## Stored data and access

Each event has a UUID, UTC time, source, trace ID, optional authenticated user ID,
HTTP status, stable error code, route template/operation, exception type and code
locations including inner exceptions. Raw exception messages, request bodies,
headers, cookies, query strings and actual route values are deliberately excluded:
they can contain credentials, SQL values and personal information. Browser reports
only accept an enumerated category and bundled JS filename/line/column locations.
They do not accept arbitrary messages or stack content. Client reports are marked
as client-reported, not trusted server exceptions.

The browser reporting endpoint has a size limit and per-IP rate limit. WooCommerce
reports require its existing connection credential; replay IDs are bound to that
connection. No public endpoint exposes stored logs. Restrict database read access
to operators; restrict the spool directory to the application service account.

```sql
SELECT TOP (100) OccurredAtUtc, Source, TraceId, UserId, StatusCode,
       ErrorCode, Operation, ExceptionType, Message, StackTrace
FROM dbo.ExceptionLogs
ORDER BY OccurredAtUtc DESC;
```

## Failure behavior

The recorder uses a separate parameterized SQL connection, independent of the
request's EF DbContext/transaction. It cannot commit dirty business entities or
lose a log when the business transaction rolls back. Each SQL attempt has a
3-second deadline; an unavailable database triggers a 15-second retry backoff.

If SQL is unavailable, an atomic JSON file is queued outside `wwwroot` in
`App_Data/exception-spool`. The worker retries up to 100 files every 30 seconds.
UUID primary keys make an ambiguous successful insert safe to replay. A file is
deleted only after SQL accepts its event. Malformed files are preserved as
`.invalid` for operator inspection. Use persistent storage for this directory in
container deployments. Each instance needs its own writable spool directory.

The disk queue is capped at 10,000 entries. If both SQL and disk fail, or the queue
is full, safe structured events are written to standard error; the original
operation/error response is preserved. Collect standard error centrally and alert
on `Exception logging unavailable`, `Exception replay unavailable`, spool growth,
and `.invalid` files. No system can guarantee database delivery during simultaneous
database/storage failure or forced process termination.

Browser reports retry in memory with bounded backoff, up to 50 pending events;
closing/reloading the browser while offline loses pending reports. Repeated pending
events are coalesced. The WordPress plugin retains at most 100 pending reports and
delivers 10 per scheduled tick; keep Action Scheduler/cron running. Existing local
WooCommerce logs remain available when the OMS connection is unavailable.

## Configuration

Optional configuration/environment variables:

- `ExceptionLogging:SpoolDirectory` / `ExceptionLogging__SpoolDirectory`: private,
  writable persistent directory. Default: `<content root>/App_Data/exception-spool`.
- `ExceptionLogging:RetentionDays` / `ExceptionLogging__RetentionDays`: 90 by
  default; 0 disables cleanup. Cleanup deletes up to 1,000 expired database rows
  every 10 minutes. Size retention and monitoring for actual deployment volume.

## Verification

```powershell
dotnet run --project tests/OMS.Exceptions.Smoke
dotnet run --project tests/OMS.Exceptions.Smoke -- --database
```

Focused frontend verification (from `OMS_Frontend`):

```powershell
npm test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.exceptions.spec.json --include=src/app/core/client-error-reporter.spec.ts
```

Connector queue verification: `artifacts/php/php.exe tests/woo-exceptions.php`.

The database check uses the configured local connection and removes only its own
synthetic event. Checks cover privacy, duplicate suppression, cancellation, safe
production responses, handled HTTP errors, SignalR, browser payload sanitation,
disk failure, SQL insert after caller rollback and durable queue replay.
