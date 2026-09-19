# Distributor API

Central ASP.NET Core API for the distributor admin and sales rep apps. This first version contains the shared user database, authentication, roles, company assignment, and user administration.

## Current endpoints

- `GET /health`
- `POST /api/auth/login`
- `GET /api/auth/me`
- `POST /api/auth/change-password`
- `GET /api/users` — Admin
- `GET /api/users/{id}` — Admin
- `POST /api/users` — Admin
- `PUT /api/users/{id}` — Admin
- `POST /api/users/{id}/reset-password` — Admin
- `DELETE /api/users/{id}` — Admin; deactivates the account

The development users are `admin`, `rep01`, `rep02`, and `rep03`, all with password `1234`. Demo users are never inserted automatically in production.

## Run locally

Development uses an in-memory database so it starts without a local MySQL installation:

```powershell
dotnet run --environment Development
```

Production uses MySQL. Create an empty database and configure the API host with:

```text
ConnectionStrings__Default=Server=...;Port=3306;Database=distributor_api;User=...;Password=...;SslMode=Required
Jwt__Key=<at least 32 random bytes>
Jwt__Issuer=Distributor.Api
Jwt__Audience=Distributor.Apps
Cors__Origins__0=https://dist.umigs.com
Cors__Origins__1=https://rep.umigs.com
Seed__Enabled=true
Seed__AdminUsername=admin
Seed__AdminName=Administrator - 01
Seed__AdminPassword=<at least 12 characters, used only on the first startup>
```

The API creates the `Users` table on first startup. Production creates the first admin only when seeding is explicitly enabled. Disable `Seed__Enabled` after the first successful startup. The Dockerfile publishes a Linux container on port 8080 for a separate API host.
