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
0. OPEN THE PROJECT IN VS CODE
The project should be opened in VS Code...
...

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
This is optional but allows files such as: PDF, PNG, JPG, DOCX, XLSX, CSV and TXT. 
Uploaded files are stored under:
Academic Managers and Human Resources can view and download these files.

=============================================================================================================
NOTES
=============================================================================================================
If you encounter migration issues, run the setup commands again from the top.
This project was created for educational purposes as part of the PROG6212 Portfolio of Evidence.

=============================================================================================================
LECTURER FEEDBACK |  PART 2 - - - > PART 3
=============================================================================================================

Feedback quote:
"Your program does not actually distinguish the different roles between Coordinator and Manager.
Please read the POE's instructions for Part 3 and adjust accordingly."
xxxxxxx
Improvement made:
The revised system now fully separates the Programme Coordinator and Academic Manager roles, ensuring
each user sees only the functions and data relevant to their responsibilities. There are now four roles
within the CMCS system, namely: Lecturers, Programme Coordinators, Academic Managers and Human Resources. 
Lecturers can only submit and alter previously submitted claims. Coordinators can verify submitted claims,
while Managers can only approve claims that have already been verified. This aligns directly with the POE
workflow that requires a verification–approval hierarchy. Session-based authorisation has also been implemented
instead of .NET Identity.

Feedback quote:
"Program failed to create claims & upload documents, and not user-friendly as separation of concerns is
inefficient. What is a Corporate Officer?"
xxxxxxx
Improvements made: 
The corporate officer was misunderstood. ‘CO’ was the backends’ abbreviation for a ‘Corporate’ user role, 
which has subsequently been changed to a Human Resources / HR role. However, as previously mentioned, the 
CMCS system now has four roles.he redesigned application now follows proper separation of concerns with 
cleaner controllers, services and data-layer organisation, resulting in a smoother, more user-friendly 
workflow for creating claims. Lecturers can submit claims without system breaks, and document uploads are
now stable, validated and linked correctly to claims. The confusing “Corporate Officer” term has been
removed entirely and replaced with the correct POE-mandated roles—Coordinator, Manager, and HR—to ensure
consistency with Part 3's terminology and avoid ambiguity.

Feedback quote:
"Approval workflow not implemented per POE requirements."
xxxxxxx
Improvements made: 
The approval chain now follows the exact four-stage process required by the POE: Submission by Lecturer,
verification by Coordinator, approval by Manager and finalisation by Human Resources. Each stage triggers
a status update that becomes visible to the lecturer and the system prevents steps from being skipped or
repeated incorrectly.

Feedback quote:
"Uploaded documents are stored in wwwroot, unencrypted, and no file type validation."
xxxxxxx
Improvements made: 
Uploaded documents are now stored database. File type restrictions, size limits and server-side validation
ensure only safe formats.

Feedback quote:
"Unit testing not conducted."
xxxxxxx
Improvements made: 
Twenty one unit tests have now been added to cover core features. Features such as claim creation, calculations,
role access restrictions, document validation and workflow transitions. This ensures reliability across all major
components. This directly addresses the earlier feedback noting the absence of any unit testing.

Feedback quote:
"Overall very confusing program."
xxxxxxx
Improvements made: 
The entire application has been reorganised. Everything from the code, folders, filenames, etc.. This was done
to follow a cleaner structure with intuitive navigation based on user roles. Views are grouped logically, 
labels have been refined, front-end reworked, backend organised and the workflow has been simplified. This was
done to support the end user with minimal confusion. Hopefully this improves readability, system wide!

=============================================================================================================
UNIT TESTING
=============================================================================================================
--Step 0--
Open terminal and navigate to the correct location.
Run the following command in the terminal of the project directory '/CMCS':
cd CMCS.Tests

--Step 1--
Restore dependancies of CMCS 
Run the following command in the terminal of the project directory '/CMCS/CMCS.Tests':
dotnet restore

--Step 2--
Remove the output files generated during a previous build.
Run the following command in the terminal of the project directory '/CMCS/CMCS.Tests':
dotnet clean

--Step 3--
Build the solution and all of its dependencies.
Run the following command in the terminal of the project directory '/CMCS/CMCS.Tests':
dotnet build

--Step 4--
Finally run this command to execute unit tests in the .NET test driver.
Run the following command in the terminal of the project directory '/CMCS/CMCS.Tests':
dotnet test

--Step 5-- 
Review output.

=============================================================================================================
END OF FILE
=============================================================================================================