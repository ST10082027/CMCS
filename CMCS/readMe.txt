Current seeded values:

IC: ic@example.com / P@ssw0rd! → should land on /ICDashboard
MR: mr@example.com / P@ssw0rd! → should land on /MRDashboard
CO: co@example.com / P@ssw0rd! → should land on /CODashboard

dotnet ef migrations add InitIdentity
dotnet ef database update
