START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    DROP INDEX "IX_lead_stages_TenantId_Name";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    DROP INDEX "IX_lead_sources_TenantId_Name";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    DROP INDEX "IX_courses_TenantId_Name";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    DROP INDEX "IX_branches_TenantId_Name";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE lead_stages ADD "IsActive" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE lead_stages ADD "IsDefaultStage" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE lead_stages ADD "NormalizedName" character varying(120) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE lead_stages ADD "UpdatedAt" timestamp without time zone NOT NULL DEFAULT TIMESTAMP '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE lead_stages ADD "Version" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE lead_sources ADD "NormalizedName" character varying(120) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE lead_sources ADD "UpdatedAt" timestamp without time zone NOT NULL DEFAULT TIMESTAMP '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE lead_sources ADD "Version" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE courses ADD "NormalizedName" character varying(160) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE courses ADD "UpdatedAt" timestamp without time zone NOT NULL DEFAULT TIMESTAMP '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE courses ADD "Version" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE branches ADD "NormalizedName" character varying(160) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE branches ADD "UpdatedAt" timestamp without time zone NOT NULL DEFAULT TIMESTAMP '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    ALTER TABLE branches ADD "Version" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    WITH ranked AS (
        SELECT "Id",
               upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g')) AS normalized,
               row_number() OVER (
                   PARTITION BY "TenantId", upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g'))
                   ORDER BY "CreatedAt", "Id") AS duplicate_number
        FROM branches
    )
    UPDATE branches AS item
    SET "NormalizedName" = CASE
            WHEN ranked.duplicate_number = 1 THEN left(ranked.normalized, 160)
            ELSE left(ranked.normalized, 150) || ' #' || left(item."Id"::text, 8)
        END,
        "UpdatedAt" = item."CreatedAt",
        "Version" = 1
    FROM ranked
    WHERE item."Id" = ranked."Id";

    WITH ranked AS (
        SELECT "Id",
               upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g')) AS normalized,
               row_number() OVER (
                   PARTITION BY "TenantId", upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g'))
                   ORDER BY "CreatedAt", "Id") AS duplicate_number
        FROM courses
    )
    UPDATE courses AS item
    SET "NormalizedName" = CASE
            WHEN ranked.duplicate_number = 1 THEN left(ranked.normalized, 160)
            ELSE left(ranked.normalized, 150) || ' #' || left(item."Id"::text, 8)
        END,
        "UpdatedAt" = item."CreatedAt",
        "Version" = 1
    FROM ranked
    WHERE item."Id" = ranked."Id";

    WITH ranked AS (
        SELECT "Id",
               upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g')) AS normalized,
               row_number() OVER (
                   PARTITION BY "TenantId", upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g'))
                   ORDER BY "CreatedAt", "Id") AS duplicate_number
        FROM lead_sources
    )
    UPDATE lead_sources AS item
    SET "NormalizedName" = CASE
            WHEN ranked.duplicate_number = 1 THEN left(ranked.normalized, 120)
            ELSE left(ranked.normalized, 110) || ' #' || left(item."Id"::text, 8)
        END,
        "UpdatedAt" = item."CreatedAt",
        "Version" = 1
    FROM ranked
    WHERE item."Id" = ranked."Id";

    WITH ranked AS (
        SELECT "Id",
               upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g')) AS normalized,
               row_number() OVER (
                   PARTITION BY "TenantId", upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g'))
                   ORDER BY "CreatedAt", "Id") AS duplicate_number
        FROM lead_stages
    )
    UPDATE lead_stages AS item
    SET "NormalizedName" = CASE
            WHEN ranked.duplicate_number = 1 THEN left(ranked.normalized, 120)
            ELSE left(ranked.normalized, 110) || ' #' || left(item."Id"::text, 8)
        END,
        "IsActive" = TRUE,
        "UpdatedAt" = item."CreatedAt",
        "Version" = 1
    FROM ranked
    WHERE item."Id" = ranked."Id";

    WITH defaults AS (
        SELECT "Id",
               row_number() OVER (
                   PARTITION BY "TenantId"
                   ORDER BY CASE WHEN upper(btrim("Name")) = 'NEW INQUIRY' THEN 0 ELSE 1 END,
                            "SortOrder", "CreatedAt", "Id") AS stage_number
        FROM lead_stages
    )
    UPDATE lead_stages AS item
    SET "IsDefaultStage" = defaults.stage_number = 1
    FROM defaults
    WHERE item."Id" = defaults."Id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE branches SET "NormalizedName" = 'MAIN BRANCH', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '20000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE courses SET "NormalizedName" = 'MBA GLOBAL', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '40000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE courses SET "NormalizedName" = 'DATA SCIENCE', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '40000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE courses SET "NormalizedName" = 'UI/UX DESIGN', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '40000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE courses SET "NormalizedName" = 'FULL STACK DEV', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '40000000-0000-0000-0000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE courses SET "NormalizedName" = 'DIGITAL MARKETING', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '40000000-0000-0000-0000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_sources SET "NormalizedName" = 'GOOGLE ADS', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '60000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_sources SET "NormalizedName" = 'WEBSITE', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '60000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_sources SET "NormalizedName" = 'LINKEDIN', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '60000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_sources SET "NormalizedName" = 'REFERRAL', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '60000000-0000-0000-0000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_sources SET "NormalizedName" = 'OFFLINE EXPO', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '60000000-0000-0000-0000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_stages SET "IsActive" = TRUE, "IsDefaultStage" = TRUE, "NormalizedName" = 'NEW INQUIRY', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '50000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_stages SET "IsActive" = TRUE, "IsDefaultStage" = FALSE, "NormalizedName" = 'CONTACTED', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '50000000-0000-0000-0000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_stages SET "IsActive" = TRUE, "IsDefaultStage" = FALSE, "NormalizedName" = 'INTERESTED', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '50000000-0000-0000-0000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_stages SET "IsActive" = TRUE, "IsDefaultStage" = FALSE, "NormalizedName" = 'DEMO SCHEDULED', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '50000000-0000-0000-0000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_stages SET "IsActive" = TRUE, "IsDefaultStage" = FALSE, "NormalizedName" = 'APPLICATION STARTED', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '50000000-0000-0000-0000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_stages SET "IsActive" = TRUE, "IsDefaultStage" = FALSE, "NormalizedName" = 'ENROLLED', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '50000000-0000-0000-0000-000000000006';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    UPDATE lead_stages SET "IsActive" = TRUE, "IsDefaultStage" = FALSE, "NormalizedName" = 'DROPPED', "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
    WHERE "Id" = '50000000-0000-0000-0000-000000000007';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    CREATE UNIQUE INDEX "IX_lead_stages_TenantId_IsDefaultStage" ON lead_stages ("TenantId", "IsDefaultStage") WHERE "IsDefaultStage" = TRUE AND "IsActive" = TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    CREATE UNIQUE INDEX "IX_lead_stages_TenantId_NormalizedName" ON lead_stages ("TenantId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    CREATE UNIQUE INDEX "IX_lead_sources_TenantId_NormalizedName" ON lead_sources ("TenantId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    CREATE UNIQUE INDEX "IX_courses_TenantId_NormalizedName" ON courses ("TenantId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    CREATE UNIQUE INDEX "IX_branches_TenantId_NormalizedName" ON branches ("TenantId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627085119_AddMasterDataManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260627085119_AddMasterDataManagement', '10.0.9');
    END IF;
END $EF$;
COMMIT;

