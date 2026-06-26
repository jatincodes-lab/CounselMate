# CounselMate CRM Progress

## Current Status

The project has been started and the first working foundation is in place. The product name is now CounselMate, and the planned architecture is multi-tenant so the CRM can serve multiple client institutes.

## Completed

### Planning

- Reviewed the original project scope document.
- Expanded it into a production-ready project plan.
- Added modules, roles, permissions, validations, lead lifecycle rules, backend scope, QA plan, deployment plan, and production-readiness checklist.
- Updated the plan to use the selected stack:
  - React + Vite frontend
  - ASP.NET Core Web API backend
  - PostgreSQL database
  - Entity Framework Core planned for ORM
  - Hangfire planned for background jobs
- Added the provided UI/UX reference folder into the planning document:
  - `stitch_admission_counselor_crm`

### UI/UX Reference Review

- Reviewed the provided design reference screens:
  - Counselor dashboard
  - Leads management
  - Student profile detail
  - Admissions pipeline
  - Follow-ups management
  - Admissions reports and analytics
  - CRM settings
- Identified the main design direction:
  - Deep-blue fixed sidebar
  - White bordered CRM cards
  - Compact admin dashboard layout
  - Dense tables and filters
  - Strong blue primary actions
  - Status and priority badges
  - Kanban pipeline
  - Lead detail side panels
  - Report widgets and settings sections

### Frontend Foundation

- Created the React frontend project in:
  - `frontend`
- Added Vite + React setup.
- Added the initial CRM UI shell.
- Added app sidebar, top header, search, profile section, and navigation.
- Added these initial frontend screens:
  - Dashboard
  - Leads
  - Pipeline
  - Follow-ups
  - Counsellors
  - Reports
  - Settings
- Added mock CRM data for leads, follow-ups, stages, activities, and counsellors.
- Added CSS matching the provided admission CRM UI reference.
- Added responsive layout support for smaller screens.

### Backend Foundation

- Created the ASP.NET Core Web API project in:
  - `backend`
- Removed unnecessary template weather endpoint.
- Added initial mock CRM API endpoints:
  - `GET /api/health`
  - `GET /api/dashboard`
  - `GET /api/leads`
  - `GET /api/leads/{id}`
  - `GET /api/pipeline`
  - `GET /api/follow-ups`
  - `GET /api/reports/conversion`
- Added CORS support for the React frontend.
- Added initial backend records for leads, follow-ups, dashboard summary, pipeline stages, and report funnel data.

### Multi-Tenant Database Foundation

- Added EF Core and Npgsql PostgreSQL support for Neon.
- Added tenant-aware database models:
  - Tenants
  - Branches
  - Users
  - Courses
  - Lead stages
  - Lead sources
  - Leads
  - Follow-ups
  - Activities
- Added `TenantId` to tenant-owned CRM records.
- Added a temporary development tenant resolver using:
  - `X-Tenant-Slug`
  - `?tenant=`
  - `DefaultTenantSlug`
- Added demo seed data for tenant:
  - `demo-academy`
- Replaced mock backend endpoints with EF-backed tenant-filtered queries.
- Added the initial EF migration:
  - `InitialTenantSchema`
- Added repo-local EF tooling:
  - `.config/dotnet-tools.json`
- Added deployment notes for:
  - Neon PostgreSQL
  - Render backend
  - Vercel frontend

### Documentation

- Created:
  - `README.md`
- Updated:
  - `PROJECT_TIMELINE_AND_SCOPE.md`
- Created this progress tracker:
  - `PROGRESS.md`

## Verification Done

### Frontend

- Installed frontend dependencies successfully.
- Ran production build successfully:
  - `npm.cmd --prefix "E:\ETPL-04\Jatin\Transferdata\CRM\frontend" run build`
- Frontend responded successfully:
  - `http://localhost:5173`
  - HTTP status: `200`

### Backend

- Ran backend build successfully:
  - `dotnet build "E:\ETPL-04\Jatin\Transferdata\CRM\backend\EducationCrm.Api.csproj"`
- Before the database conversion, API health endpoint responded successfully:
  - `http://localhost:5078/api/health`
- Before the database conversion, leads endpoint responded successfully:
  - `http://localhost:5078/api/leads`
- After the database conversion, backend build passes with EF Core models and migrations included.

## Previous Local URLs

- Frontend:
  - `http://localhost:5173`
- Backend API:
  - `http://localhost:5078`
- API health:
  - `http://localhost:5078/api/health`

## Important Notes

- PowerShell blocks direct `npm` usage because script execution is disabled, so commands should use `npm.cmd`.
- The first frontend build had a sandbox path permission issue, but it succeeded when run outside the sandbox.
- The backend initially failed restore because the template OpenAPI package required NuGet access. The external package was removed for the first foundation build.
- Current frontend uses mock data locally.
- Current backend uses EF Core with PostgreSQL configuration and tenant-filtered queries.
- The initial migration has not been applied to a real Neon database yet.
- Authentication, roles, database persistence, and real validations are not implemented yet.

## Next Development Steps

1. Create the Neon database and apply the initial migration.
2. Add the Render backend environment variables.
3. Connect the React frontend to the ASP.NET Core API.
4. Add lead create/edit forms.
5. Add frontend and backend validation.
6. Add authentication and role-based access control.
7. Add real pipeline stage updates.
8. Add follow-up create, complete, and reschedule workflows.
9. Add payments, documents, and import/export later after core CRM workflows are stable.

## Recommended Immediate Priority

The next best step is:

1. Create a Neon database.
2. Set the backend connection string using `DATABASE_URL` or `ConnectionStrings__DefaultConnection`.
3. Run `dotnet tool restore`.
4. Run `dotnet tool run dotnet-ef database update --project backend/EducationCrm.Api.csproj --startup-project backend/EducationCrm.Api.csproj`.
5. Start the backend and verify `/api/health`, `/api/leads`, and `/api/pipeline`.
