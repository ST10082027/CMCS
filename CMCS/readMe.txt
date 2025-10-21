Welcome to CMCS
This is a .NET Core MVC web application for streamlining Independent Contractor claim submissions,
approvals, and payroll processing with secure role-based access.

SETUP
#Trust the server certificate
dotnet dev-certs https --trust

1. Restore dependencies
In the terminal of the project's location run the command:
dotnet restore

2. Apply migrations and create the database
In the terminal of the project's location, run the command:
dotnet ef migrations add InitIdentity

then run the command:
dotnet ef database update

3. Run the application
In the terminal of the project's location, run the command:
dotnet run

4. Open your browser at https://localhost:5266.

5. Use the following seeded values to log in to the role specific dashboards:
================================================================================
Role                    : Username       : Password     : URL
================================================================================
Independent Contractor  : ic@example.com / P@ssw0rd!    : /ICDashboard
Manager                 : mr@example.com / P@ssw0rd!    : /MRDashboard
Corporate               : co@example.com / P@ssw0rd!    : /CODashboard
================================================================================

4. Documentation
ERD and UML diagrams are included (/docs/).

5. License
This project is for educational purposes (POE for PROG6212).





!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
Devs notes

dotnet ef migrations add InitIdentity
dotnet ef database update
dotnet dev-certs https --trust

Dev Terminal commands
#1. Stage changes: "git add .".
#2. Commit with a custom message: "git commit -m "custom message"".
#3. Push to main GitHub repo: "git push origin main"

Dev updates
2025.10.21-Tuesday-14:03
Try the end-to-end flow
1. Login as IC ic@example.com / P@ssw0rd!
2. Go to My Claims → New Claim → Save (Draft) → Submit
3. Logout → Login as MR mr@example.com / P@ssw0rd!
4. Go to Review Queue → Approve or Reject (with remark)
5. Login as CO co@example.com
6. Go to All Claims and see everything, with statuses.
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&
!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&!@#%$^%&^*&*^%$#@!#$#$%^%&^*&&^%$^&*&^%$#@!@#$%^&