Current seeded values:

IC: ic@example.com / P@ssw0rd! → should land on /ICDashboard
MR: mr@example.com / P@ssw0rd! → should land on /MRDashboard
CO: co@example.com / P@ssw0rd! → should land on /CODashboard

dotnet ef migrations add InitIdentity
dotnet ef database update


Dev Terminal commands
#1. Stage changes: "git add .".
#2. Commit with a custom message: "git commit -m "custom message"".
#3. Push to main GitHub repo: "git push origin main"