# Etamid API - local demo run (in-memory database, no SQL Server needed)
# LOCAL DEMO ONLY: the DataKey below is a placeholder of 32 zero bytes.
# Never use it in production and never commit a real key to git.
$env:Security__DataKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
$env:Persistence__UseInMemory = "true"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project backend/src/Etamid.Api --no-launch-profile --urls http://localhost:5080
