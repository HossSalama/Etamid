# Docker for Etamid API and mobile/iOS testing

This Compose setup runs the Etamid .NET API and SQL Server for local development. It is not a production deployment. The API uses the development-only fake phone/Nafath providers; the project must not handle real customer data or real payments in this mode.

## 1. Install Docker Desktop on Windows

Install Docker Desktop using Docker's [official Windows installation guide](https://docs.docker.com/desktop/setup/install/windows-install/). Choose the WSL 2 backend when prompted. If WSL is not installed or needs updating, open PowerShell as Administrator and run `wsl --install`, restart Windows if requested, then run `wsl --update`. Start Docker Desktop and wait until it reports that the engine is running.

Open a new PowerShell window and confirm both commands return versions:

```powershell
docker --version
docker compose version
```

## 2. Prepare the local environment

Open PowerShell in the project folder, for example:

```powershell
cd D:\etamid
Copy-Item .env.example .env
notepad .env
```

Replace the three `REPLACE...` values in `.env`. Generate a different random value for each setting. This PowerShell snippet prints a strong SQL Server password, a JWT signing secret, and a 32-byte base64 encryption key:

```powershell
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$b = New-Object byte[] 32
$rng.GetBytes($b)
"Mssql7!" + [Convert]::ToBase64String($b).TrimEnd('=')
$rng.GetBytes($b)
[Convert]::ToBase64String($b)
$rng.GetBytes($b)
[Convert]::ToBase64String($b)
$rng.Dispose()
```

Copy the printed values in order to `MSSQL_SA_PASSWORD`, `JWT_SECRET`, and `DATA_PROTECTION_KEY`. Keep `.env` private: it is ignored by Git. Do not send it to the mobile team. Each developer should create their own `.env` from `.env.example`.

For a physical iPhone on the same Wi-Fi, replace `localhost` in `API_PUBLIC_BASE_URL` with the Windows PC's LAN IPv4 address, such as `http://192.168.1.25:5080/files`. You can find that address with `ipconfig`. The mobile app's API root on that same Wi-Fi is `http://192.168.1.25:5080/api/v1`. Allow Docker Desktop through Windows Firewall on a trusted private network if prompted. Do not expose port 1433 or forward ports on your router.

## 3. Build and start the API

Run:

```powershell
docker compose config
docker compose up --build -d
docker compose ps
docker compose logs -f api
```

The first startup downloads the .NET and SQL Server images and can take several minutes. Compose waits for SQL Server to become healthy. The API then applies the checked-in EF Core migrations automatically in Development and starts on host port 5080.

Press `Ctrl+C` to stop following logs; this does not stop the containers. Verify these addresses in a browser:

- Swagger: `http://localhost:5080/swagger`
- Liveness: `http://localhost:5080/health`
- Database readiness: `http://localhost:5080/health/ready`

## 4. Share the setup with the mobile/iOS team

Commit and push the Docker-related project files and `.env.example` to the team's private Git repository. Share the repository/branch and this guide. Do not commit or send `.env`, database files, real provider credentials, or copied JWT/data keys. The mobile developer clones the repository, creates their own `.env`, and runs the same `docker compose up --build -d` command.

If the API stays on your Windows PC, `localhost` works only from that PC. A simulator or physical device on the same private Wi-Fi can use the PC's LAN IP, but plain HTTP may be rejected by iOS App Transport Security unless the app's development configuration explicitly permits it. Do not add a broad HTTP exception to a release build. For a distributed team, remote devices, or TestFlight, deploy a separate HTTPS staging API and database; a local Docker container is not a shared internet endpoint. Do not publish this development API directly to the internet.

## 5. Useful commands

```powershell
docker compose logs -f api
docker compose logs -f sqlserver
docker compose restart api
docker compose down
```

`docker compose down` preserves the named SQL and upload volumes. To permanently delete the local development database and uploaded files, use `docker compose down -v` (this is destructive).

## Test-mode authentication

The Compose configuration deliberately selects the development-only fake phone verifier and fake Nafath provider. The OTP service accepts `123456` as a development-only bypass. These options are for integration testing only and are prohibited in Production. No real payment gateway or production escrow custody is configured by this Docker setup.
