=============================================================================================================
CMCS - Contract Monthly Claim System
=============================================================================================================
Welcome to CMCS.
This is a .NET Core MVC web application built to help Independent Contractors (ICs) submit monthly claims,
Managers (MRs) review and approve them, and Corporate Officers (COs) manage approvals and users.
Each user type has their own secure dashboard and access level. hehe 

=============================================================================================================
BASIC SETUP
=============================================================================================================
1. TRUST THE LOCAL HTTPS CERTIFICATE
Run this command in the terminal of the project directory:
dotnet dev-certs https --trust

2. RUNNING FOR THE FIRST TIME 
If you are setting up for the first time or starting fresh, proceed with the steps outlined below.
If you have done these steps before, proceed to 'BUILD AND RUN'

-- Step 0 --
Make sure you navigate to '.../CMCS'

-- Step 1 --
Remove previous migrations (if first time...)
Run the following command in the terminal of the project directory '/CMCS':
rm -rf Migrations

-- Step 2 --
Delete any previous database (if first time...)
Run the following command in the terminal of the project directory '/CMCS':
rm -f CMCS.db

-- Step 3 -- 
Create a new Initial Migration (if first time...)
Run the following command in the terminal of the project directory '/CMCS':
dotnet ef migrations add Initial

-- Step 4 --
Update the database to seed users (if first time...)
Run the following command in the terminal of the project directory '/CMCS':
dotnet ef database update

=============================================================================================================
BUILD AND RUN
=============================================================================================================
Run the following commands:
--Step 0--
Restore dependancies.
Run the following command in the terminal of the project directory '/CMCS':
dotnet restore

--Step 1--
Remove the output files generated during a previous build.
Run the following command in the terminal of the project directory '/CMCS':
dotnet clean

--Step 2--
Build the solution and all of its dependencies.
Run the following command in the terminal of the project directory '/CMCS':
dotnet build 

--Step 3--
Run source code without any explicit compile or launch commands.
Run the following command in the terminal of the project directory '/CMCS':
dotnet run

--Step 4--
Once it’s running, open your browser and go to:
   https://localhost:5266
or https://localhost:5001
or http://localhost:5000

=============================================================================================================
SEEDED LOGIN DETAILS
=============================================================================================================
Role: Lecturer
Email: lecturer@example.com 
Password: P@ssw0rd!

Role: Programme Coordinator
Email: coordinator@example.com
Password: P@ssw0rd!

Role: Academic Manager
Email: manager@example.com
Password: P@ssw0rd!

Role: Human Resources
Email: hr@example.com
Password: P@ssw0rd!  
=============================================================================================================
ABOUT FILE UPLOADS
=============================================================================================================
Independent Contractors can attach supporting documents when submitting claims.
This is optional but allows files such as:
PDF, PNG, JPG, DOCX, XLSX, CSV and TXT. 
Uploaded files are stored under: wwwroot/uploads/claims/[ClaimId]
Managers and Corporate Officers can view and download these files.

=============================================================================================================
NOTES
=============================================================================================================
If you encounter migration issues, run the setup commands again from the top.
This project was created for educational purposes as part of the PROG6212 Portfolio of Evidence.

END OF FILE