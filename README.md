# CounselMate CRM

Production-ready multi-tenant CRM foundation for admission counsellors and coaching institutes.

## Stack

- Frontend: React + Vite
- Backend: ASP.NET Core Web API
- Database: PostgreSQL
- ORM: Entity Framework Core with Npgsql
- Future background jobs: Hangfire

## Multi-Tenant Direction

CounselMate is planned as a SaaS CRM for multiple client institutes. The first production schema should include tenants/institutes, branches, tenant-scoped users, and tenant-owned CRM records. Business tables such as leads, follow-ups, activities, courses, stages, payments, and documents should carry `TenantId` so each client only sees its own data.

## Folders

- `frontend` - React CRM UI for CounselMate, based on the provided admission CRM reference screens.
- `backend` - ASP.NET Core API foundation with initial mock endpoints.
- `stitch_admission_counselor_crm` - UI/UX reference bundle.
- `PROJECT_TIMELINE_AND_SCOPE.md` - production scope and timeline.
- `DEPLOYMENT.md` - Neon, Render, and Vercel deployment notes.

## First Run

Frontend:

```bash
cd frontend
npm install
copy .env.example .env
npm run dev
```

Backend:

```bash
cd backend
dotnet restore
dotnet tool restore
dotnet tool run dotnet-ef database update
dotnet run
```

For Neon or Render, set `DATABASE_URL` or `ConnectionStrings__DefaultConnection` to the PostgreSQL connection string.

The frontend reads `VITE_API_BASE_URL`. Login resolves the tenant from the matching email and password.

Initial API endpoints:

- `GET /api/health`
- `GET /api/dashboard`
- `GET /api/leads` with pagination, search, filters, sort, and archive mode
- `GET /api/leads/{id}`
- `POST /api/leads`
- `PATCH /api/leads/{id}`
- `PATCH /api/leads/{id}/assign`
- `PATCH /api/leads/{id}/stage`
- `PATCH /api/leads/{id}/archive`
- `PATCH /api/leads/{id}/restore`
- `POST /api/leads/{id}/follow-ups`
- `PATCH /api/leads/{leadId}/follow-ups/{followUpId}/reschedule`
- `PATCH /api/leads/{leadId}/follow-ups/{followUpId}/cancel`
- `POST /api/leads/{leadId}/follow-ups/{followUpId}/complete`
- `GET /api/pipeline`
- `GET /api/follow-ups`
- `GET /api/reports/conversion`
- `GET /api/master-data`
- `POST/PATCH /api/master-data/branches`
- `POST/PATCH /api/master-data/courses`
- `POST/PATCH /api/master-data/lead-sources`
- `POST/PATCH /api/master-data/lead-stages`
- `POST /api/master-data/lead-stages/reorder`

Master-data writes require the Owner or Admin role. Records are deactivated rather than deleted so historical leads keep their original branch, course, source, and stage references.

Lead writes use optimistic `Version` checks and archived leads are read-only until restored. Owner/Admin can see all leads, branch managers are scoped to their branch plus assigned leads, and counsellor/telecaller users are scoped to assigned leads.

Follow-up actions also use optimistic `Version` checks. Scheduled follow-ups can be rescheduled, cancelled, or completed; completed/cancelled follow-ups are immutable. Lead `NextFollowUpAt` is recalculated from scheduled follow-ups after every follow-up mutation.
