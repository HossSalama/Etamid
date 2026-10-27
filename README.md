# Etamid (اعتمد)

Escrow platform for buyers and sellers (individuals and companies).

```
backend/    .NET 10 — Clean Architecture, Modular Monolith
website/    Angular SSR (later)
admin/      Angular admin panel (later)
docs/       OpenAPI contract, specs, DB design, Postman collection
```

## Run the API locally with Docker

Install Docker Desktop on Windows, copy `.env.example` to `.env`, generate the local secrets, then run:

```bash
docker compose up --build -d
```

Swagger: http://localhost:5080/swagger. Health: `/health` and `/health/ready`. See [`docs/docker-mobile-ios.md`](docs/docker-mobile-ios.md) for Windows setup, team onboarding, and iOS/device networking. Never commit or share `.env`.

## EF Core migrations

The Docker Compose Development setup applies existing migrations when the API starts. To generate a new migration as a maintainer, install the .NET 10 SDK and run:

```bash
cd backend
dotnet tool install --global dotnet-ef
dotnet ef migrations add MigrationName -p src/Etamid.Infrastructure -s src/Etamid.Api -o Migrations
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
