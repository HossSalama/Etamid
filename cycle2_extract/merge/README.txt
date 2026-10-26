1) Copy the folder "backend" over D:\etamid\backend (new files + files antigravity did not touch).
2) Apply the 5 merge items in this folder by hand (they are files antigravity also edited, so they are not overwritten):
   - Program.cs.additions.txt
   - Infrastructure.DependencyInjection.additions.txt
   - appsettings.additions.json  (two files)
   - messages.en.additions.json / messages.ar.additions.json  (add the keys to the Resources files)
3) dotnet build ; dotnet test
4) dotnet ef migrations add Cycle2Seeds -p src/Etamid.Infrastructure -s src/Etamid.Api
   dotnet ef database update -p src/Etamid.Infrastructure -s src/Etamid.Api --connection "Server=localhost;Database=EtamidDb;Trusted_Connection=True;TrustServerCertificate=True"
