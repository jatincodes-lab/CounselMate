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
- Added frontend API client using:
  - `VITE_API_BASE_URL`
  - `VITE_TENANT_SLUG`
- Connected dashboard, leads, pipeline, and follow-up screens to backend API data.
- Added loading, empty, and error states for API-backed views.
- Added production-oriented Add Lead workflow:
  - Backend lookup endpoint for courses, sources, stages, branches, and counsellors
  - Backend POST `/api/leads`
  - Tenant-scoped server validation
  - Normalized phone duplicate protection
  - Database unique index for tenant + normalized phone
  - Frontend modal form with client validation and server error display
- Added lead detail workflow:
  - Click lead from table or pipeline
  - View full profile, timeline, and follow-ups in a side drawer
  - Update stage, status, priority, assigned counsellor, and next follow-up
  - Add activity notes
  - Schedule follow-ups
  - Mark follow-ups complete
- Added required-field red markers in the Add Lead form.
- Added CounselMate-themed scrollbars.
- Added local authentication and role-aware access:
  - Login endpoint with tenant slug, email, and password
  - HMAC-SHA256 bearer token issuance and validation
  - Password hashes stored with PBKDF2
  - Current-user endpoint
  - API tenant scope now resolves from authenticated user
  - Write actions blocked for read-only users
  - Frontend login screen, session restore, and logout
- Added platform tenant onboarding:
  - Owner-only tenant list endpoint
  - Owner-only create tenant endpoint
  - Creates tenant, main branch, tenant admin, default lead stages, default lead sources, and starter courses
  - Frontend owner-only Platform page
  - Frontend create client institute form
- Added tenant user management:
  - Tenant-scoped user list endpoint
  - Admin/owner-only create user endpoint
  - Admin/owner-only update user endpoint
  - Duplicate tenant email protection
  - Role validation and escalation checks
  - Self-deactivation/self-role-change protection
  - Frontend Team Management page under Counsellors
  - Team summary counts and name/email, role, status, and branch filters
  - Accessible create, edit, and reset-password modals with client/server validation
  - Silent directory refresh and success/error feedback after user actions
  - Permission-aware actions that hide unavailable owner and self-management operations
  - Backend guard preventing admins from updating owner accounts
- Added production Master Data management:
  - Tenant-scoped branches, courses, lead sources, and lead stages API
  - Owner/Admin writes with authenticated read-only access for other roles
  - Case-insensitive normalized-name uniqueness per tenant
  - Optimistic concurrency versions returning `409` for stale updates
  - Safe deactivation rules for assigned branches, required lookups, occupied stages, and default/won/lost stages
  - Transactional lead-stage ordering and default-stage selection
  - Responsive Settings workspace with tabs, search, status filters, usage counts, modals, and read-only states
  - Live lookup synchronization for Add Lead, Team Management, Pipeline, and Dashboard
  - IST `CreatedAt` and `UpdatedAt` storage

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
- Applied the initial migration to the Neon database.
- Verified Neon-backed API endpoints with the demo tenant.
- Added and applied the normalized phone migration:
  - `AddLeadNormalizedPhone`
- Added and applied the authentication migration:
  - `AddUserAuthentication`
- Added and applied the owner seed migration:
  - `SeedPlatformOwnerRole`
- Added and applied the master-data management migration:
  - `AddMasterDataManagement`

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
- After applying the Neon migration, API smoke test passed:
  - Health status: `healthy`
  - Database status: `connected`
  - Tenant: `demo-academy`
  - Lead count: `5`
  - Pipeline stages: `7`
- Frontend build passed after API integration.
- Frontend build passed after Add Lead workflow.
- Frontend build passed after lead detail workflow.
- Frontend production build passed after Team Management polish.
- Backend Release build passed after Team Management authorization hardening.
- Frontend production build passed after Master Data management.
- Backend Release build passed after Master Data management.
- Master Data migration applied successfully to Neon.
- Master Data API smoke tests passed for duplicate names, invalid fields, dependency blocking, stale versions, stage invariants, and read-only permissions.
- Lead Management Core migration applied successfully to Neon.
- Lead Management Core is implemented:
  - Paginated lead list with search, filters, sorting, and archive mode.
  - Full lead profile edit with active master-data validation and historical inactive values preserved.
  - Role-scoped lead visibility and mutation rules.
  - Dedicated assign, stage move, archive, and restore APIs.
  - Lead `Version`, `UpdatedAt`, `ArchivedAt`, and user audit fields.
- Lead Management Core smoke tests passed:
  - Login worked.
  - Paginated lead list returned live data.
  - Lead detail loaded.
  - Stale archive request returned `409`.
  - Archive and restore round trip succeeded.
- Frontend production build passed after Lead Management Core.
- Backend Release build passed after Lead Management Core.
- Follow-up lifecycle migration applied successfully to Neon.
- Follow-up reschedule/cancel and activity timeline polish are implemented:
  - Follow-ups now have `Version`, `UpdatedAt`, `CompletedAt`, and `CancelledAt`.
  - Scheduled follow-ups can be rescheduled, cancelled, or completed.
  - Completed/cancelled follow-ups are immutable.
  - Lead `NextFollowUpAt` is recalculated from scheduled follow-ups after every mutation.
  - Lead drawer now has reschedule/cancel/complete controls and grouped activity timeline sections.
- Follow-up lifecycle smoke tests passed:
  - Follow-up create succeeded.
  - Stale reschedule returned `409`.
  - Reschedule advanced follow-up version.
  - Cancel updated status to `Cancelled`.
  - Complete updated status to `Completed`.
  - `NextFollowUpAt` cleared when no scheduled follow-up remained.
- Frontend production build passed after Follow-up lifecycle.
- Backend Release build passed after Follow-up lifecycle.
- Local frontend responded successfully while pointed at the Neon-backed API:
  - `http://127.0.0.1:5173`
- Add Lead API smoke test passed:
  - Options endpoint returned `5` courses
  - Duplicate phone returned `409`
  - Valid lead created as `LD-1006`
- Lead detail API smoke test passed:
  - Health endpoint returned `healthy`
  - `GET /api/leads/LD-1001` returned lead detail
  - No-op `PATCH /api/leads/LD-1004` succeeded
  - Invalid activity and follow-up requests returned `400`
  - Missing lead returned `404`
- Auth smoke test passed:
  - Unauthenticated dashboard request returned `401`
  - Admin login worked for `rahul@demo-academy.test`
  - Authenticated dashboard returned tenant CRM data
  - Read-only write attempt returned `403`
- Platform onboarding smoke test passed:
  - Owner login worked for `rahul@demo-academy.test`
  - Owner tenant list loaded
  - New test tenant was created in Neon
  - New tenant admin login worked
  - New tenant dashboard returned tenant-scoped empty CRM data
  - Read-only user platform access returned `403`
- Tenant user management smoke test passed:
  - Owner/admin user list loaded
  - Tenant user creation succeeded
  - Tenant user update succeeded
  - Read-only user create attempt returned `403`
  - Duplicate email returned `409`
  - Self-deactivation returned `400`

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
- Current frontend uses API data for dashboard, leads, pipeline, and follow-ups.
- Current backend uses EF Core with PostgreSQL configuration and tenant-filtered queries.
- The initial migration has been applied to the Neon database.
- Authentication and role-based access control are implemented for local CRM workflows.
- CRM timestamps are now standardized to Indian Standard Time for persisted business data. Backend code uses `IndianClock`, and EF stores `DateTimeOffset` fields as PostgreSQL `timestamp without time zone` values representing IST clock time.

## Next Development Steps

1. Add activity communication templates.
2. Add tenant profile/settings persistence.
3. Add lead import/export.
4. Add payments and documents later after core CRM workflows are stable.

## Recommended Immediate Priority

The next best step is:

1. Add activity communication templates.
2. Add tenant profile/settings persistence.
3. Add lead import/export.
4. Deploy to Render/Vercel only after core workflows are complete.
