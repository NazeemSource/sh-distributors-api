# Distributor API

Central ASP.NET Core API for the distributor admin and sales rep apps. It uses MySQL 8, JWT bearer authentication, EF Core migrations, a stock ledger, atomic order and stock-in processing, payment balances, and cheque reminders.

## Modules

- Authentication and users with `Admin` and company-scoped `Rep` roles
- Companies, shops/customers, and products
- Inventory opening stock, stock-in, orders, free issues, and adjustments
- Order and supplier payments with pending, partially-paid, and paid status
- Shop receivables and company payables
- Cheques with search, filters, reminders, and overdue queries
- Soft deletion on business records and restrictive financial foreign keys

All monetary and quantity values use `decimal(18,2)`. Product availability is calculated from `SUM(quantity_in) - SUM(quantity_out)` in the immutable inventory movement ledger.

## API routes

- `/health`
- `/api/auth/*`, `/api/users/*`
- `/api/companies`, `/api/shops`, `/api/products`
- `/api/products/{id}/stock`, `/stock-history`, `/adjustments`
- `/api/stock-ins`, `/api/stock-ins/{id}/payments`
- `/api/orders`, `/api/orders/{id}`, `/api/orders/{id}/payments`
- `/api/shops/{id}/outstanding`, `/api/companies/{id}/outstanding`
- `/api/cheques`, `/api/cheques/upcoming`, `/api/cheques/overdue`

List routes support the documented search/filter parameters and bounded pagination. Errors use RFC 7807 problem details with a stable `code` and `traceId`.

## Development

Development uses an isolated in-memory database:

```powershell
dotnet run --environment Development
dotnet test tests/API.Tests/API.Tests.csproj
```

Development users are `admin`, `rep01`, `rep02`, and `rep03` with password `1234`. They use generic company and user names only. These users are never inserted in production.

## MySQL

Production requires MySQL 8+. EF migrations run automatically at startup. The generated first-install SQL is available at `database/schema.sql`; normal deployment should use the EF migration rather than applying that script separately.

Required configuration:

```text
ConnectionStrings__Default=Server=...;Port=3306;Database=...;User=...;Password=...;SslMode=Required
Jwt__Key=<at least 32 random bytes>
Jwt__Issuer=Distributor.Api
Jwt__Audience=Distributor.Apps
Cors__Origins__0=https://dist.umigs.com
Cors__Origins__1=https://rep.umigs.com
```

The VPS deployment creates `cwebsite_shdistr`, stores private production configuration under `/home/cwebsite/apps/shdistrapi-shared`, starts the self-contained service on `127.0.0.1:5041`, and proxies `https://shdistrapi.umigs.com` through `/home/cwebsite/public_html/shdistrapi/public/.htaccess`. The first generated admin credential is stored with mode `0600` at `/home/cwebsite/apps/shdistrapi-shared/initial-admin.txt` and should be changed after first login.

## Deployment

Pushes to `main` run build, unit/business tests, an actual MySQL 8 migration/startup check, package audit, Linux publish, SSH upload, installation, and a health check. The GitHub repository needs an `SSH_PRIVATE_KEY` secret authorized for `cwebsite@198.54.121.31`.
