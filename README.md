# CounselMate CRM

Production-ready multi-tenant CRM foundation for admission counsellors and coaching institutes.

## Stack

- Frontend: React + Vite
- Backend: ASP.NET Core Web API
- Database: PostgreSQL
- Future ORM: Entity Framework Core with Npgsql
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

The frontend reads:

- `VITE_API_BASE_URL`
- `VITE_TENANT_SLUG`

Initial API endpoints:

- `GET /api/health`
- `GET /api/dashboard`
- `GET /api/leads`
- `GET /api/leads/{id}`
- `GET /api/pipeline`
- `GET /api/follow-ups`
- `GET /api/reports/conversion`
