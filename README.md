# Etamid (اعتمد)

Escrow platform for buyers and sellers (individuals and companies).

```
backend/    .NET 10 — Clean Architecture, Modular Monolith
website/    Angular SSR (later)
admin/      Angular admin panel (later)
docs/       OpenAPI contract, specs, DB design, Postman collection
```

## Run locally (3 commands)
```bash
docker compose up -d                                   # SQL Server, Redis, Seq
cd backend
dotnet run --project src/Etamid.Api                    # http://localhost:5080/swagger
```
Seq (logs): http://localhost:5341 — Health: `/health` and `/health/ready`.

## First migration
```bash
cd backend
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial -p src/Etamid.Infrastructure -s src/Etamid.Api -o Persistence/Migrations
dotnet ef database update -p src/Etamid.Infrastructure -s src/Etamid.Api
```

## Tests
```bash
cd backend && dotnet test Etamid.slnx
```

## Layers
`Api → Application → Domain` and `Infrastructure → Application → Domain`; SharedKernel is used by all.
- **Domain**: entities, value objects, domain services (pure rules), stable error codes.
- **Application**: MediatR commands/queries/handlers, validators, application services, interfaces.
- **Infrastructure**: EF Core, JWT, hashing, SMS, Redis, jobs — implements Application interfaces.
- **Api**: thin controllers, middleware, envelope, localization.

Response envelope: `{ success, message, data }` + `errors` (failures only) + `meta` (paginated lists only).
Language: `Accept-Language: ar | en`. Every response has `X-Correlation-Id`.
