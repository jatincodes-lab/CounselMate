# CounselMate CRM - Production Project Plan

## 1. Product Vision

Build CounselMate as a production-ready multi-tenant SaaS CRM for coaching institutes, admission teams, counsellors, telecallers, and branch managers in India. The product should help institutes capture leads, assign them to counsellors, track follow-ups, move students through the admission pipeline, measure counsellor productivity, manage payments/admission status, and maintain clear communication history.

The first delivery should start with a strong frontend MVP using mock data, but every screen, form, filter, and workflow must be designed around the future production data model so the backend integration phase does not require major rewrites.

## 2. Target Users

- Institute owner/admin
- Branch manager
- Admission manager
- Counsellor
- Telecaller
- Accountant/payment user
- Read-only/reporting user

## 3. Core Business Goals

- Capture every admission inquiry in one system.
- Reduce missed follow-ups and overdue calls.
- Improve counsellor accountability and conversion tracking.
- Give managers a clear view of lead source quality, branch performance, and admission revenue.
- Support multi-branch institutes and role-based access.
- Prepare the product for real integrations such as WhatsApp, SMS, email, payments, and document uploads.

## 4. MVP Product Scope

## 4A. UI/UX Reference Direction

The CRM UI/UX should follow the reference bundle located at:

`E:\ETPL-04\Jatin\Transferdata\CRM\stitch_admission_counselor_crm`

The implementation should use these files as visual and interaction references, not as copied static HTML. The final app should be rebuilt as reusable React components.

### 4A.1 Reference Screens

- `counselor_dashboard` for dashboard layout, KPI cards, activity feed, schedule panel, and source performance table.
- `leads_management` for lead table, filters, search, pagination, badges, row actions, and add lead action.
- `student_profile_detail` for lead profile layout, tabs, timeline, side action panels, quick update, and assigned team section.
- `admissions_pipeline` for Kanban board, quick filters, lead cards, stage counts, and board/list toggle.
- `follow_ups_management` for daily follow-up queue, date navigator, priority badges, completion actions, and recent notes.
- `admissions_reports_analytics` for conversion funnel, KPI cards, source report, productivity report, and export flow.
- `crm_settings` for institute profile, team management, lead stages, automation toggles, and integrations/API section.

### 4A.2 Visual System

- Fixed deep-blue left sidebar around 240-260px.
- Light gray application background.
- White cards with subtle 1px borders.
- Minimal shadows; use tonal separation more than elevation.
- Compact, dense CRM layout optimized for daily work.
- Primary actions in strong blue.
- Neutral secondary buttons.
- Status colors used sparingly for enrolled, interested, follow-up, dropped, pending, completed, and priority states.
- Inter font for UI text.
- Small labels, compact tables, and clear hierarchy.
- 4px radius for buttons/inputs/small cards.
- 8px radius for larger panels where needed.
- Sidebar active state with highlighted row and left indicator.

### 4A.3 Component Rules

- Build a reusable app shell: sidebar, top header, search, profile menu, notification icon, and page content container.
- Build reusable data components: KPI card, filter bar, CRM table, status badge, priority badge, activity item, empty state, and action menu.
- Build reusable form components: text input, phone input, select, date picker, textarea, file upload, switch, and validation message.
- Build reusable workflow components: lead card, Kanban column, timeline, follow-up item, quick action panel, and report card.
- Every screen must have loading, empty, error, and permission-denied states.
- All primary screens must be responsive. On mobile, sidebar collapses and tables should become horizontally scrollable or card-based where needed.

### 4A.4 UX Rules

- Keep lead and follow-up actions visible without forcing users through many pages.
- Search should remain available in the top header.
- Filters should be grouped at the top of list/report screens.
- Destructive actions require confirmation.
- Stage changes to Enrolled or Dropped require validation and confirmation.
- Important follow-ups, overdue tasks, and high-priority leads must be visually prominent.
- Side panels should be used for quick updates, assigned team, next follow-up, and contextual actions.
- Tables should show only high-value columns by default and move secondary actions into row menus.

### 4.1 App Foundation

- React + Vite app structure
- JavaScript-first implementation so the codebase remains easier to learn and maintain initially
- Tailwind CSS theme
- shadcn/ui or Material UI component system
- lucide-react icons
- App shell with fixed sidebar
- Top header with global search, institute/branch selector, notifications, quick actions, and profile menu
- Responsive layout for desktop, tablet, and mobile
- Shared loading, empty, error, and permission-denied states
- Mock data layer matching the future backend schema
- Route structure for authenticated CRM pages

### 4.2 Dashboard

- Total leads
- New leads today
- Pending follow-ups
- Overdue follow-ups
- Admissions/enrollments
- Conversion rate
- Expected revenue
- Collected revenue
- Source-wise lead summary
- Counsellor performance snapshot
- Recent activity
- Quick actions for add lead, add follow-up, import leads, and create report

### 4.3 Leads Management

- Lead list/table page
- Search by student name, guardian name, phone, alternate phone, email, lead ID, and city
- Filters by stage, status, source, counsellor, branch, course, priority, date range, and follow-up state
- Sort by created date, last activity, next follow-up, priority, and stage
- Add lead form
- Edit lead form
- Lead status and priority badges
- Lead owner/counsellor assignment
- Duplicate lead warning
- Bulk action UI for assignment, stage change, tag update, export, and delete/archive
- Pagination and saved filter views

### 4.4 Lead Detail

- Student profile summary
- Contact and guardian information
- Course interest
- Branch and counsellor assignment
- Lead source and campaign
- Current stage and admission status
- Priority and tags
- Activity timeline
- Notes
- Follow-ups
- Communication history
- Documents
- Payments/admission tab
- Change history/audit events
- Quick actions for call, WhatsApp, email, add note, schedule follow-up, change stage, assign counsellor, and mark admission

### 4.5 Pipeline

- Kanban board
- Default stages: New Inquiry, Contacted, Interested, Demo Scheduled, Demo Done, Application Started, Enrolled, Dropped
- Drag-and-drop stage movement
- Stage count and stage value
- Lead cards with name, course, source, priority, counsellor, last activity, and next follow-up
- Filters by branch, counsellor, course, source, and date range
- Stage movement confirmation for sensitive stages such as Enrolled and Dropped
- Stage change activity log

### 4.6 Follow-ups and Tasks

- Today, overdue, upcoming, and completed follow-up views
- Call, WhatsApp, email, meeting, demo, payment reminder, and document collection follow-up types
- Follow-up status: pending, completed, missed, rescheduled, cancelled
- Priority: low, medium, high, urgent
- Assign follow-up to user
- Reschedule flow with reason
- Reminder UI
- Calendar/list toggle
- Daily task queue for counsellors
- Manager view for team follow-up performance

### 4.7 Counsellors and Users

- User list
- Role badges
- Branch assignment
- Active/inactive status
- Assigned leads
- Open follow-ups
- Conversion summary
- Add/edit user UI
- Password/reset invite placeholder
- Permission overview
- Workload summary

### 4.8 Reports and Analytics

- Lead source report
- Conversion funnel report
- Counsellor productivity report
- Branch performance report
- Follow-up completion report
- Admission and revenue report
- Dropped lead reason report
- Course-wise demand report
- Date range filters
- Branch/course/counsellor/source filters
- Export UI for CSV/XLSX/PDF
- Saved reports placeholder

### 4.9 Settings

- Institute profile
- Branches
- Users and roles
- Lead stages
- Lead sources
- Courses/programs
- Tags
- Custom fields
- Follow-up templates
- Notification preferences
- Communication templates
- Payment settings placeholder
- Integration settings placeholder
- Data import/export settings

## 5. Roles and Permissions

### 5.1 Owner/Admin

- Full access to all branches, users, settings, reports, exports, integrations, and billing.

### 5.2 Branch Manager

- Access only to assigned branch data.
- Can manage counsellors, assign leads, view branch reports, and monitor follow-ups.

### 5.3 Admission Manager

- Can view and manage leads, counsellors, follow-ups, pipeline, and reports for assigned branches.

### 5.4 Counsellor

- Can view assigned leads.
- Can add notes, follow-ups, calls, communication logs, and update allowed lead stages.
- Cannot delete leads or export full datasets unless permission is granted.

### 5.5 Telecaller

- Can create leads, update contact outcomes, schedule follow-ups, and transfer leads.
- Limited access to reports and payment data.

### 5.6 Accountant

- Can view admitted students and payment-related records.
- Limited access to lead communication and counselling notes.

### 5.7 Read-only User

- Can view dashboards and reports based on assigned branch permissions.
- Cannot create, edit, delete, import, or export unless explicitly allowed.

## 6. Lead Lifecycle Rules

### 6.1 Default Stages

1. New Inquiry
2. Contacted
3. Interested
4. Demo Scheduled
5. Demo Done
6. Application Started
7. Enrolled
8. Dropped

### 6.2 Stage Rules

- Every lead must have exactly one current stage.
- Stage changes must be logged in the activity timeline.
- Moving to Enrolled requires course, branch, admission date, and admission owner.
- Moving to Dropped requires a dropped reason.
- Reopening a Dropped lead requires a reopen reason.
- Moving backward in the pipeline should be allowed only for permitted roles.
- Stage order must be configurable from settings.

### 6.3 Admission Status

- Not started
- In discussion
- Demo scheduled
- Application pending
- Payment pending
- Partially paid
- Enrolled
- Dropped
- Deferred

### 6.4 Lead Priority

- Low
- Medium
- High
- Urgent

Urgent and high-priority leads must appear prominently in counsellor dashboards and follow-up queues.

## 7. Core Data Model

### 7.1 Institute

- ID
- Name
- Logo
- Contact email
- Contact phone
- Address
- GST number
- Subscription plan
- Status
- Created date

### 7.2 Branch

- ID
- Institute ID
- Branch name
- City
- State
- Address
- Phone
- Manager
- Status

### 7.3 User

- ID
- Institute ID
- Branch IDs
- Name
- Email
- Phone
- Role
- Status
- Last login
- Created date

### 7.4 Lead/Student

- ID
- Institute ID
- Branch ID
- Student name
- Guardian name
- Phone
- Alternate phone
- Email
- City
- State
- Course interest
- Source
- Campaign
- Stage
- Admission status
- Priority
- Assigned counsellor
- Tags
- Next follow-up date
- Last activity date
- Created by
- Created date
- Updated date

### 7.5 Course

- ID
- Institute ID
- Course name
- Category
- Duration
- Fee
- Status

### 7.6 Follow-up

- ID
- Lead ID
- Assigned user
- Type
- Due date/time
- Status
- Priority
- Notes
- Completion notes
- Reschedule reason
- Created date
- Completed date

### 7.7 Activity

- ID
- Lead ID
- Actor user
- Activity type
- Description
- Metadata
- Created date

### 7.8 Payment/Admission

- ID
- Lead ID
- Course ID
- Admission date
- Total fee
- Amount paid
- Balance
- Payment status
- Payment mode
- Receipt reference
- Created date

### 7.9 Document

- ID
- Lead ID
- Document type
- File name
- File URL
- Uploaded by
- Uploaded date
- Verification status

## 8. Validations and Form Rules

### 8.1 Lead Form Validations

- Student name is required.
- Phone number is required and must be a valid Indian mobile number.
- Email must be valid if provided.
- Course interest is required.
- Lead source is required.
- Branch is required for multi-branch institutes.
- Assigned counsellor is optional on creation but required before active counselling.
- Duplicate warning should trigger on same phone/email within the same institute.
- Priority must default to Medium.
- Created date should be system-generated and not user editable.

### 8.2 Follow-up Validations

- Lead is required.
- Follow-up type is required.
- Due date and time are required.
- Assigned user is required.
- Past due dates should require manager permission or a clear reason.
- Completing a follow-up requires outcome notes.
- Rescheduling requires new due date and reason.

### 8.3 Stage Change Validations

- Enrolled requires course, branch, admission date, and admission owner.
- Dropped requires dropped reason.
- Payment pending requires expected amount or admission package if available.
- Demo scheduled requires demo date/time.
- Stage changes must be blocked if the user lacks permission.

### 8.4 User Form Validations

- Name is required.
- Email is required and unique within the institute.
- Phone must be valid if provided.
- Role is required.
- At least one branch is required for non-owner users.
- Inactive users cannot receive new lead assignments.

### 8.5 Settings Validations

- Stage names must be unique.
- At least one active lead source must exist.
- Course names must be unique within an institute.
- Branch names should be unique within an institute.
- Custom fields must have valid field type and label.
- Required custom fields must be enforced on lead creation/edit.

## 9. Communication Scope

### 9.1 MVP

- Communication history placeholder
- Manual call log
- Manual WhatsApp log
- Manual email log
- Notes and outcomes
- Template UI placeholder

### 9.2 Production Phase

- WhatsApp Business Cloud API
- SMS provider such as MSG91
- Email provider such as AWS SES
- Approved communication templates
- Delivery status tracking
- Failed message tracking
- Consent and opt-out handling
- Communication audit log

## 10. Import, Export, and Data Management

### 10.1 Import

- CSV/XLSX lead import
- Column mapping UI
- Required field validation
- Duplicate detection
- Preview before import
- Import success/failure report
- Partial import support
- Import rollback or correction workflow

### 10.2 Export

- Export leads, follow-ups, counsellor reports, source reports, and admission reports
- Export should respect role and branch permissions
- Export events must be audit logged
- Sensitive fields should be masked for restricted users

### 10.3 Data Retention

- Soft delete/archive for leads
- Restore archived leads for permitted roles
- Audit logs should not be editable by normal users
- Production retention policy should be finalized with legal/business approval

## 11. Security and Compliance Conditions

- Tenant isolation must be enforced at backend query level.
- Every API must check authenticated user, tenant, role, and branch access.
- Passwords must never be stored in plain text.
- Sessions/tokens must expire and support logout.
- Sensitive actions must be audit logged.
- Exports must be permission-controlled.
- File uploads must validate type, size, and ownership.
- Public file URLs should not expose private documents without signed access.
- Rate limiting should be applied to login, OTP, imports, and messaging endpoints.
- PII such as phone, email, and documents must be protected.
- Consent capture should be implemented before automated marketing communication.
- DPDP, TRAI, WhatsApp, SMS, email, and payment compliance should be reviewed before production launch.
- Production launch must include privacy policy, terms, refund/cancellation policy if payments are enabled, and support contact details.

## 12. Backend Scope

### 12.1 Recommended Final Tech Stack

- React + Vite frontend
- ASP.NET Core Web API backend
- PostgreSQL
- Entity Framework Core with Npgsql
- JWT authentication with httpOnly cookies or secure bearer token handling
- ASP.NET Core Identity if full user/account management is needed
- FluentValidation or built-in ASP.NET Core validation
- Swagger/OpenAPI for API documentation
- Hangfire for background jobs
- Redis later for caching, distributed locks, and high-volume queues
- S3-compatible object storage for documents
- Serilog for structured logging
- Docker for production deployment

### 12.2 Backend Modules

- Authentication
- Tenant/institute management
- Branch management
- User and role management
- Lead CRUD
- Lead assignment
- Pipeline stage management
- Follow-ups/tasks
- Activity timeline
- Notes
- Documents
- Reports
- Imports
- Exports
- Notifications
- Audit logs
- Integrations
- Subscription/billing

### 12.3 API Requirements

- Consistent REST API structure
- Pagination on all list APIs
- Server-side filtering and sorting
- Schema-based request validation
- Standard error response format
- Request logging
- Audit logging for sensitive actions
- API documentation

## 13. Frontend Production Conditions

- All forms must use schema validation.
- All server mutations must show loading, success, and error states.
- All tables must support pagination, search, filters, and empty states.
- All destructive actions must require confirmation.
- Role-restricted actions must be hidden or disabled with proper backend enforcement.
- Responsive layout must work on common desktop, tablet, and mobile sizes.
- Common components should be reusable across modules.
- Mock data should follow the production model.
- Accessibility basics must be followed: labels, keyboard navigation, focus states, contrast, and semantic controls.
- UI should avoid copied template assets and use a custom implementation inspired by the references.

## 14. Testing and QA Plan

### 14.1 Frontend Tests

- Form validation tests
- Table filter/search tests
- Kanban stage movement tests
- Permission UI tests
- Lead detail tab rendering tests
- Empty/loading/error state checks
- Responsive visual checks

### 14.2 Backend Tests

- Auth tests
- Role and permission tests
- Tenant isolation tests
- Lead CRUD tests
- Follow-up workflow tests
- Stage transition tests
- Import validation tests
- Export permission tests
- Report accuracy tests

### 14.3 Manual QA Checklist

- Create lead
- Edit lead
- Detect duplicate lead
- Assign counsellor
- Move pipeline stage
- Schedule follow-up
- Complete follow-up
- Reschedule follow-up
- Mark lead enrolled
- Mark lead dropped
- Import leads
- Export report
- Verify role restrictions
- Verify branch restrictions
- Verify mobile layout

## 15. Deployment and Operations

### 15.1 Environments

- Local development
- Staging
- Production

### 15.2 CI/CD

- Lint check
- Type check
- Build check
- Test check
- Migration check
- Deploy to staging
- Manual approval for production

### 15.3 Production Operations

- Domain and SSL
- Environment variable management
- Database backup
- Error monitoring
- Uptime monitoring
- Log monitoring
- Audit log review
- Backup restore test
- Runbook for common incidents

## 16. SaaS and Billing Scope

### 16.1 Later Phase

- Subscription plans
- Trial period
- User limits by plan
- Lead limits by plan
- Branch limits by plan
- Feature limits by plan
- Razorpay subscription/payment integration
- Invoice history
- Payment failed state
- Account suspension/reactivation

## 17. Recommended Timeline

### Phase 1: Product Planning and Frontend Foundation

Estimated duration: 1-2 weeks

Deliverables:

- Finalized data model
- Finalized roles and permissions
- React + Vite project setup
- Theme and layout system
- Sidebar and header
- Shared UI components
- Mock data model
- Dashboard page
- Leads list page

Acceptance criteria:

- App shell is responsive.
- Mock data follows future backend schema.
- Dashboard and leads page render realistic CRM data.
- Shared UI states exist for loading, empty, error, and permission-denied.

### Phase 2: Core CRM Workflows

Estimated duration: 2-3 weeks

Deliverables:

- Add/edit lead forms
- Lead detail page
- Activity timeline
- Notes
- Follow-ups
- Pipeline Kanban board
- Counsellor/users page

Acceptance criteria:

- Leads can be created/edited in frontend state or mock state.
- Lead detail page shows complete student context.
- Pipeline movement logs a mock activity.
- Follow-up creation and completion flows are represented.
- Role-based UI placeholders are visible.

### Phase 3: Reports, Settings, and Admin Controls

Estimated duration: 1-2 weeks

Deliverables:

- Reports dashboard
- Report filters
- Export UI placeholder
- Settings pages
- Branches
- Courses
- Stages
- Sources
- Tags
- Custom fields placeholder

Acceptance criteria:

- Reports have realistic filters and chart/table views.
- Settings match the future configurable CRM model.
- Export actions are present but clearly mocked.

### Phase 4: Backend Foundation

Estimated duration: 3-4 weeks

Deliverables:

- Backend project setup
- PostgreSQL schema
- Authentication
- Tenant model
- Branch model
- User/role model
- Lead APIs
- Follow-up APIs
- Activity APIs
- Basic report APIs

Acceptance criteria:

- APIs enforce tenant isolation.
- APIs validate request payloads.
- Lead, user, branch, follow-up, and activity records persist in database.
- Backend has tests for auth, roles, tenant isolation, and core CRUD.

### Phase 5: API Integration

Estimated duration: 2-3 weeks

Deliverables:

- Replace mock data with API calls
- Authenticated routes
- Form submission flows
- Server-side table pagination/filtering
- Kanban persistence
- Follow-up status updates
- Activity timeline persistence

Acceptance criteria:

- Frontend works with real API data.
- Form errors from backend are shown clearly.
- Pipeline and follow-up actions persist after refresh.
- Branch and role restrictions are enforced by backend.

### Phase 6: Import, Export, Communication, and Payments

Estimated duration: 4-6 weeks

Deliverables:

- CSV/XLSX import
- Export reports
- Manual communication logs
- WhatsApp/SMS/email integration
- Razorpay payment links
- Document uploads
- Audit logs for communication, imports, exports, and payments

Acceptance criteria:

- Imports validate data and report row-level errors.
- Exports respect permissions.
- Communication events are logged.
- Payment events are linked to leads/admissions.
- Documents are securely uploaded and accessed.

### Phase 7: Security, Compliance, Hardening, and Launch

Estimated duration: 3-4 weeks

Deliverables:

- RBAC enforcement
- Tenant isolation audit
- Rate limits
- Export audit logging
- Consent and opt-out workflow
- Production deployment
- Monitoring
- Backups
- Runbook
- Launch QA

Acceptance criteria:

- Critical workflows pass QA.
- Tenant isolation tests pass.
- Role restriction tests pass.
- Production environment has monitoring and backups.
- Legal/compliance documents are reviewed before public launch.

## 18. First Build Route List

- `/dashboard`
- `/leads`
- `/leads/new`
- `/leads/[id]`
- `/pipeline`
- `/follow-ups`
- `/counsellors`
- `/reports`
- `/settings`
- `/settings/branches`
- `/settings/courses`
- `/settings/stages`
- `/settings/sources`
- `/settings/users`

## 19. Out of Scope for First Frontend Build

- Real authentication
- Real database
- Real WhatsApp sending
- Real SMS sending
- Real email sending
- Real payment processing
- Real document uploads
- AWS production deployment
- Native mobile app
- Full compliance automation
- Advanced AI lead scoring
- Marketplace integrations

## 20. Key Risks and Assumptions

- WhatsApp, SMS, email, and payment integrations may require account verification and approval time.
- Compliance requirements must be reviewed before production launch.
- Import/export features can become complex if institutes have inconsistent data formats.
- Reporting accuracy depends on a clean activity and stage-change model.
- Tenant isolation must be designed from the first backend phase, not added at the end.
- Mock frontend data must match backend schema to avoid rework during API integration.

## 21. Definition of Production Ready

The CRM should be considered production ready only when:

- Authentication is implemented.
- Role-based access is enforced in frontend and backend.
- Tenant and branch isolation are tested.
- Core workflows persist to database.
- Forms have frontend and backend validation.
- Critical APIs have tests.
- Imports and exports are permission-controlled.
- Sensitive actions are audit logged.
- Error, empty, loading, and permission states are handled.
- Backups and monitoring are active.
- Deployment process is documented.
- Privacy, consent, and communication rules are reviewed.
- Launch QA checklist is completed.
