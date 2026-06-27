CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE tenants (
        "Id" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Slug" character varying(80) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE branches (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "City" character varying(120) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_branches" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_branches_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE courses (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_courses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_courses_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE lead_sources (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_lead_sources" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_lead_sources_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE lead_stages (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "SortOrder" integer NOT NULL,
        "IsWonStage" boolean NOT NULL,
        "IsLostStage" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_lead_stages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_lead_stages_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "BranchId" uuid,
        "FullName" character varying(160) NOT NULL,
        "Email" character varying(240) NOT NULL,
        "Role" character varying(40) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_users_branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES branches ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_users_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE leads (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "BranchId" uuid,
        "CourseId" uuid NOT NULL,
        "LeadStageId" uuid NOT NULL,
        "LeadSourceId" uuid NOT NULL,
        "AssignedUserId" uuid,
        "LeadNumber" character varying(40) NOT NULL,
        "StudentName" character varying(160) NOT NULL,
        "GuardianName" character varying(160),
        "Email" character varying(240) NOT NULL,
        "Phone" character varying(40) NOT NULL,
        "City" character varying(120),
        "Status" character varying(80) NOT NULL,
        "Priority" character varying(40) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "NextFollowUpAt" timestamp with time zone,
        CONSTRAINT "PK_leads" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_leads_branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES branches ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_leads_courses_CourseId" FOREIGN KEY ("CourseId") REFERENCES courses ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_leads_lead_sources_LeadSourceId" FOREIGN KEY ("LeadSourceId") REFERENCES lead_sources ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_leads_lead_stages_LeadStageId" FOREIGN KEY ("LeadStageId") REFERENCES lead_stages ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_leads_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_leads_users_AssignedUserId" FOREIGN KEY ("AssignedUserId") REFERENCES users ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE activities (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "LeadId" uuid NOT NULL,
        "CreatedByUserId" uuid,
        "Type" character varying(80) NOT NULL,
        "Description" character varying(1000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_activities" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_activities_leads_LeadId" FOREIGN KEY ("LeadId") REFERENCES leads ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_activities_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_activities_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE TABLE follow_ups (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "LeadId" uuid NOT NULL,
        "AssignedUserId" uuid,
        "Type" character varying(80) NOT NULL,
        "Priority" character varying(40) NOT NULL,
        "Status" character varying(60) NOT NULL,
        "DueAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_follow_ups" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_follow_ups_leads_LeadId" FOREIGN KEY ("LeadId") REFERENCES leads ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_follow_ups_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_follow_ups_users_AssignedUserId" FOREIGN KEY ("AssignedUserId") REFERENCES users ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO tenants ("Id", "CreatedAt", "IsActive", "Name", "Slug")
    VALUES ('10000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Demo Academy', 'demo-academy');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO branches ("Id", "City", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('20000000-0000-0000-0000-000000000001', 'New Delhi', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Main Branch', '10000000-0000-0000-0000-000000000001');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO courses ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('40000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'MBA Global', '10000000-0000-0000-0000-000000000001');
    INSERT INTO courses ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('40000000-0000-0000-0000-000000000002', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Data Science', '10000000-0000-0000-0000-000000000001');
    INSERT INTO courses ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('40000000-0000-0000-0000-000000000003', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'UI/UX Design', '10000000-0000-0000-0000-000000000001');
    INSERT INTO courses ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('40000000-0000-0000-0000-000000000004', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Full Stack Dev', '10000000-0000-0000-0000-000000000001');
    INSERT INTO courses ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('40000000-0000-0000-0000-000000000005', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Digital Marketing', '10000000-0000-0000-0000-000000000001');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO lead_sources ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('60000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Google Ads', '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_sources ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('60000000-0000-0000-0000-000000000002', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Website', '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_sources ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('60000000-0000-0000-0000-000000000003', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'LinkedIn', '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_sources ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('60000000-0000-0000-0000-000000000004', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Referral', '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_sources ("Id", "CreatedAt", "IsActive", "Name", "TenantId")
    VALUES ('60000000-0000-0000-0000-000000000005', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, 'Offline Expo', '10000000-0000-0000-0000-000000000001');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO lead_stages ("Id", "CreatedAt", "IsLostStage", "IsWonStage", "Name", "SortOrder", "TenantId")
    VALUES ('50000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', FALSE, FALSE, 'New Inquiry', 10, '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_stages ("Id", "CreatedAt", "IsLostStage", "IsWonStage", "Name", "SortOrder", "TenantId")
    VALUES ('50000000-0000-0000-0000-000000000002', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', FALSE, FALSE, 'Contacted', 20, '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_stages ("Id", "CreatedAt", "IsLostStage", "IsWonStage", "Name", "SortOrder", "TenantId")
    VALUES ('50000000-0000-0000-0000-000000000003', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', FALSE, FALSE, 'Interested', 30, '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_stages ("Id", "CreatedAt", "IsLostStage", "IsWonStage", "Name", "SortOrder", "TenantId")
    VALUES ('50000000-0000-0000-0000-000000000004', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', FALSE, FALSE, 'Demo Scheduled', 40, '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_stages ("Id", "CreatedAt", "IsLostStage", "IsWonStage", "Name", "SortOrder", "TenantId")
    VALUES ('50000000-0000-0000-0000-000000000005', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', FALSE, FALSE, 'Application Started', 50, '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_stages ("Id", "CreatedAt", "IsLostStage", "IsWonStage", "Name", "SortOrder", "TenantId")
    VALUES ('50000000-0000-0000-0000-000000000006', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', FALSE, TRUE, 'Enrolled', 60, '10000000-0000-0000-0000-000000000001');
    INSERT INTO lead_stages ("Id", "CreatedAt", "IsLostStage", "IsWonStage", "Name", "SortOrder", "TenantId")
    VALUES ('50000000-0000-0000-0000-000000000007', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TRUE, FALSE, 'Dropped', 70, '10000000-0000-0000-0000-000000000001');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO users ("Id", "BranchId", "CreatedAt", "Email", "FullName", "IsActive", "Role", "TenantId")
    VALUES ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', 'rahul@demo-academy.test', 'Rahul Sharma', TRUE, 'Counselor', '10000000-0000-0000-0000-000000000001');
    INSERT INTO users ("Id", "BranchId", "CreatedAt", "Email", "FullName", "IsActive", "Role", "TenantId")
    VALUES ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', 'verma@demo-academy.test', 'S. Verma', TRUE, 'Counselor', '10000000-0000-0000-0000-000000000001');
    INSERT INTO users ("Id", "BranchId", "CreatedAt", "Email", "FullName", "IsActive", "Role", "TenantId")
    VALUES ('30000000-0000-0000-0000-000000000003', '20000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', 'khanna@demo-academy.test', 'R. Khanna', TRUE, 'Counselor', '10000000-0000-0000-0000-000000000001');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO leads ("Id", "AssignedUserId", "BranchId", "City", "CourseId", "CreatedAt", "Email", "GuardianName", "LeadNumber", "LeadSourceId", "LeadStageId", "NextFollowUpAt", "Phone", "Priority", "Status", "StudentName", "TenantId")
    VALUES ('70000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', NULL, '40000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-07T00:00:00+00:00', 'arjun.a@email.com', NULL, 'LD-1001', '60000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000006', TIMESTAMPTZ '2026-06-25T04:00:00+00:00', '+91 98765 43210', 'High', 'Enrolled', 'Arjun Adhikari', '10000000-0000-0000-0000-000000000001');
    INSERT INTO leads ("Id", "AssignedUserId", "BranchId", "City", "CourseId", "CreatedAt", "Email", "GuardianName", "LeadNumber", "LeadSourceId", "LeadStageId", "NextFollowUpAt", "Phone", "Priority", "Status", "StudentName", "TenantId")
    VALUES ('70000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000003', '20000000-0000-0000-0000-000000000001', NULL, '40000000-0000-0000-0000-000000000002', TIMESTAMPTZ '2026-06-19T00:00:00+00:00', 'priya.s@outlook.com', NULL, 'LD-1002', '60000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000003', TIMESTAMPTZ '2026-06-26T02:00:00+00:00', '+91 91234 56789', 'Medium', 'Interested', 'Priya Sharma', '10000000-0000-0000-0000-000000000001');
    INSERT INTO leads ("Id", "AssignedUserId", "BranchId", "City", "CourseId", "CreatedAt", "Email", "GuardianName", "LeadNumber", "LeadSourceId", "LeadStageId", "NextFollowUpAt", "Phone", "Priority", "Status", "StudentName", "TenantId")
    VALUES ('70000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', NULL, '40000000-0000-0000-0000-000000000003', TIMESTAMPTZ '2026-06-21T00:00:00+00:00', 'm.jones@gmail.com', NULL, 'LD-1003', '60000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000004', TIMESTAMPTZ '2026-06-25T03:00:00+00:00', '+91 99887 76655', 'High', 'Follow Up', 'Michael Jones', '10000000-0000-0000-0000-000000000001');
    INSERT INTO leads ("Id", "AssignedUserId", "BranchId", "City", "CourseId", "CreatedAt", "Email", "GuardianName", "LeadNumber", "LeadSourceId", "LeadStageId", "NextFollowUpAt", "Phone", "Priority", "Status", "StudentName", "TenantId")
    VALUES ('70000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', NULL, '40000000-0000-0000-0000-000000000004', TIMESTAMPTZ '2026-05-31T00:00:00+00:00', 'd.reddy@tcs.com', NULL, 'LD-1004', '60000000-0000-0000-0000-000000000004', '50000000-0000-0000-0000-000000000007', NULL, '+91 90000 11223', 'Low', 'Dropped', 'Deepak Reddy', '10000000-0000-0000-0000-000000000001');
    INSERT INTO leads ("Id", "AssignedUserId", "BranchId", "City", "CourseId", "CreatedAt", "Email", "GuardianName", "LeadNumber", "LeadSourceId", "LeadStageId", "NextFollowUpAt", "Phone", "Priority", "Status", "StudentName", "TenantId")
    VALUES ('70000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', NULL, '40000000-0000-0000-0000-000000000005', TIMESTAMPTZ '2026-06-24T00:00:00+00:00', 'k.luthra@gmail.com', NULL, 'LD-1005', '60000000-0000-0000-0000-000000000005', '50000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T06:00:00+00:00', '+91 88776 65544', 'Medium', 'New Lead', 'Kriti Luthra', '10000000-0000-0000-0000-000000000001');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO follow_ups ("Id", "AssignedUserId", "CreatedAt", "DueAt", "LeadId", "Priority", "Status", "TenantId", "Type")
    VALUES ('80000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TIMESTAMPTZ '2026-06-25T00:45:00+00:00', '70000000-0000-0000-0000-000000000001', 'High', 'Scheduled', '10000000-0000-0000-0000-000000000001', 'Call');
    INSERT INTO follow_ups ("Id", "AssignedUserId", "CreatedAt", "DueAt", "LeadId", "Priority", "Status", "TenantId", "Type")
    VALUES ('80000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000002', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TIMESTAMPTZ '2026-06-25T02:00:00+00:00', '70000000-0000-0000-0000-000000000002', 'Medium', 'Scheduled', '10000000-0000-0000-0000-000000000001', 'WhatsApp');
    INSERT INTO follow_ups ("Id", "AssignedUserId", "CreatedAt", "DueAt", "LeadId", "Priority", "Status", "TenantId", "Type")
    VALUES ('80000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000003', TIMESTAMPTZ '2026-06-25T00:00:00+00:00', TIMESTAMPTZ '2026-06-25T04:00:00+00:00', '70000000-0000-0000-0000-000000000003', 'Low', 'Scheduled', '10000000-0000-0000-0000-000000000001', 'Email');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_activities_CreatedByUserId" ON activities ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_activities_LeadId" ON activities ("LeadId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_activities_TenantId_CreatedAt" ON activities ("TenantId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_branches_TenantId_Name" ON branches ("TenantId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_courses_TenantId_Name" ON courses ("TenantId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_follow_ups_AssignedUserId" ON follow_ups ("AssignedUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_follow_ups_LeadId" ON follow_ups ("LeadId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_follow_ups_TenantId_DueAt" ON follow_ups ("TenantId", "DueAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_lead_sources_TenantId_Name" ON lead_sources ("TenantId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_lead_stages_TenantId_Name" ON lead_stages ("TenantId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_lead_stages_TenantId_SortOrder" ON lead_stages ("TenantId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_leads_AssignedUserId" ON leads ("AssignedUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_leads_BranchId" ON leads ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_leads_CourseId" ON leads ("CourseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_leads_LeadSourceId" ON leads ("LeadSourceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_leads_LeadStageId" ON leads ("LeadStageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_leads_TenantId" ON leads ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_leads_TenantId_CreatedAt" ON leads ("TenantId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_leads_TenantId_LeadNumber" ON leads ("TenantId", "LeadNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_leads_TenantId_Phone" ON leads ("TenantId", "Phone");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_tenants_Slug" ON tenants ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE INDEX "IX_users_BranchId" ON users ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    CREATE UNIQUE INDEX "IX_users_TenantId_Email" ON users ("TenantId", "Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626085437_InitialTenantSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260626085437_InitialTenantSchema', '10.0.9');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    DROP INDEX "IX_leads_TenantId_Phone";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    ALTER TABLE leads ADD "NormalizedPhone" character varying(32) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    UPDATE leads SET "NormalizedPhone" = '919876543210'
    WHERE "Id" = '70000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    UPDATE leads SET "NormalizedPhone" = '919123456789'
    WHERE "Id" = '70000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    UPDATE leads SET "NormalizedPhone" = '919988776655'
    WHERE "Id" = '70000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    UPDATE leads SET "NormalizedPhone" = '919000011223'
    WHERE "Id" = '70000000-0000-0000-0000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    UPDATE leads SET "NormalizedPhone" = '918877665544'
    WHERE "Id" = '70000000-0000-0000-0000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    CREATE UNIQUE INDEX "IX_leads_TenantId_NormalizedPhone" ON leads ("TenantId", "NormalizedPhone");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626093728_AddLeadNormalizedPhone') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260626093728_AddLeadNormalizedPhone', '10.0.9');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626103038_AddUserAuthentication') THEN
    ALTER TABLE users ADD "FailedLoginAttempts" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626103038_AddUserAuthentication') THEN
    ALTER TABLE users ADD "LastLoginAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626103038_AddUserAuthentication') THEN
    ALTER TABLE users ADD "PasswordHash" character varying(220) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626103038_AddUserAuthentication') THEN
    UPDATE users SET "FailedLoginAttempts" = 0, "LastLoginAt" = NULL, "PasswordHash" = 'v1.100000.Mt8GC3coU3xegrVi+C2aAw==.i10uchOOE1k5pG2zdL/PW+FxQ7wZ9yM+MW/hwgowbPM=', "Role" = 'Admin'
    WHERE "Id" = '30000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626103038_AddUserAuthentication') THEN
    UPDATE users SET "FailedLoginAttempts" = 0, "LastLoginAt" = NULL, "PasswordHash" = 'v1.100000.Mt8GC3coU3xegrVi+C2aAw==.i10uchOOE1k5pG2zdL/PW+FxQ7wZ9yM+MW/hwgowbPM='
    WHERE "Id" = '30000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626103038_AddUserAuthentication') THEN
    UPDATE users SET "FailedLoginAttempts" = 0, "LastLoginAt" = NULL, "PasswordHash" = 'v1.100000.Mt8GC3coU3xegrVi+C2aAw==.i10uchOOE1k5pG2zdL/PW+FxQ7wZ9yM+MW/hwgowbPM=', "Role" = 'ReadOnly'
    WHERE "Id" = '30000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626103038_AddUserAuthentication') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260626103038_AddUserAuthentication', '10.0.9');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626105352_SeedPlatformOwnerRole') THEN
    UPDATE users SET "Role" = 'Owner'
    WHERE "Id" = '30000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626105352_SeedPlatformOwnerRole') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260626105352_SeedPlatformOwnerRole', '10.0.9');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "users"
    ALTER COLUMN "LastLoginAt"
    TYPE timestamp without time zone
    USING ("LastLoginAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "users"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "tenants"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "leads"
    ALTER COLUMN "NextFollowUpAt"
    TYPE timestamp without time zone
    USING ("NextFollowUpAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "leads"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "lead_stages"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "lead_sources"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "follow_ups"
    ALTER COLUMN "DueAt"
    TYPE timestamp without time zone
    USING ("DueAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "follow_ups"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "courses"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "branches"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    ALTER TABLE "activities"
    ALTER COLUMN "CreatedAt"
    TYPE timestamp without time zone
    USING ("CreatedAt" AT TIME ZONE 'Asia/Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE branches SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '20000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE courses SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '40000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE courses SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '40000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE courses SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '40000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE courses SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '40000000-0000-0000-0000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE courses SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '40000000-0000-0000-0000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE follow_ups SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00', "DueAt" = TIMESTAMP '2026-06-25T06:15:00'
    WHERE "Id" = '80000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE follow_ups SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00', "DueAt" = TIMESTAMP '2026-06-25T07:30:00'
    WHERE "Id" = '80000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE follow_ups SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00', "DueAt" = TIMESTAMP '2026-06-25T09:30:00'
    WHERE "Id" = '80000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_sources SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '60000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_sources SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '60000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_sources SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '60000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_sources SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '60000000-0000-0000-0000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_sources SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '60000000-0000-0000-0000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_stages SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '50000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_stages SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '50000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_stages SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '50000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_stages SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '50000000-0000-0000-0000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_stages SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '50000000-0000-0000-0000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_stages SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '50000000-0000-0000-0000-000000000006';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE lead_stages SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '50000000-0000-0000-0000-000000000007';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE leads SET "CreatedAt" = TIMESTAMP '2026-06-07T05:30:00', "NextFollowUpAt" = TIMESTAMP '2026-06-25T09:30:00'
    WHERE "Id" = '70000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE leads SET "CreatedAt" = TIMESTAMP '2026-06-19T05:30:00', "NextFollowUpAt" = TIMESTAMP '2026-06-26T07:30:00'
    WHERE "Id" = '70000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE leads SET "CreatedAt" = TIMESTAMP '2026-06-21T05:30:00', "NextFollowUpAt" = TIMESTAMP '2026-06-25T08:30:00'
    WHERE "Id" = '70000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE leads SET "CreatedAt" = TIMESTAMP '2026-05-31T05:30:00', "NextFollowUpAt" = NULL
    WHERE "Id" = '70000000-0000-0000-0000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE leads SET "CreatedAt" = TIMESTAMP '2026-06-24T05:30:00', "NextFollowUpAt" = TIMESTAMP '2026-06-25T11:30:00'
    WHERE "Id" = '70000000-0000-0000-0000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE tenants SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00'
    WHERE "Id" = '10000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE users SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00', "LastLoginAt" = NULL
    WHERE "Id" = '30000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE users SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00', "LastLoginAt" = NULL
    WHERE "Id" = '30000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    UPDATE users SET "CreatedAt" = TIMESTAMP '2026-06-25T05:30:00', "LastLoginAt" = NULL
    WHERE "Id" = '30000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627051626_StoreIndianStandardTimestamps') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260627051626_StoreIndianStandardTimestamps', '10.0.9');
    END IF;
END $EF$;
COMMIT;

