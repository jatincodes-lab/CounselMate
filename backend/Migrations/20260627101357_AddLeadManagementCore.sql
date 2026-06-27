START TRANSACTION;
ALTER TABLE leads ADD "ArchivedAt" timestamp without time zone;

ALTER TABLE leads ADD "ArchivedByUserId" uuid;

ALTER TABLE leads ADD "CreatedByUserId" uuid;

ALTER TABLE leads ADD "UpdatedAt" timestamp without time zone NOT NULL DEFAULT TIMESTAMP '-infinity';

ALTER TABLE leads ADD "UpdatedByUserId" uuid;

ALTER TABLE leads ADD "Version" integer NOT NULL DEFAULT 1;

UPDATE leads SET "ArchivedAt" = NULL, "ArchivedByUserId" = NULL, "CreatedByUserId" = NULL, "UpdatedAt" = TIMESTAMP '0001-01-01T05:30:00', "UpdatedByUserId" = NULL, "Version" = 1
WHERE "Id" = '70000000-0000-0000-0000-000000000001';

UPDATE leads SET "ArchivedAt" = NULL, "ArchivedByUserId" = NULL, "CreatedByUserId" = NULL, "UpdatedAt" = TIMESTAMP '0001-01-01T05:30:00', "UpdatedByUserId" = NULL, "Version" = 1
WHERE "Id" = '70000000-0000-0000-0000-000000000002';

UPDATE leads SET "ArchivedAt" = NULL, "ArchivedByUserId" = NULL, "CreatedByUserId" = NULL, "UpdatedAt" = TIMESTAMP '0001-01-01T05:30:00', "UpdatedByUserId" = NULL, "Version" = 1
WHERE "Id" = '70000000-0000-0000-0000-000000000003';

UPDATE leads SET "ArchivedAt" = NULL, "ArchivedByUserId" = NULL, "CreatedByUserId" = NULL, "UpdatedAt" = TIMESTAMP '0001-01-01T05:30:00', "UpdatedByUserId" = NULL, "Version" = 1
WHERE "Id" = '70000000-0000-0000-0000-000000000004';

UPDATE leads SET "ArchivedAt" = NULL, "ArchivedByUserId" = NULL, "CreatedByUserId" = NULL, "UpdatedAt" = TIMESTAMP '0001-01-01T05:30:00', "UpdatedByUserId" = NULL, "Version" = 1
WHERE "Id" = '70000000-0000-0000-0000-000000000005';

UPDATE leads
SET "UpdatedAt" = "CreatedAt",
    "Version" = 1
WHERE "UpdatedAt" <= TIMESTAMP '1900-01-01'
   OR "Version" < 1;

ALTER TABLE leads ALTER COLUMN "UpdatedAt" DROP DEFAULT;
ALTER TABLE leads ALTER COLUMN "Version" SET DEFAULT 1;

CREATE INDEX "IX_leads_ArchivedByUserId" ON leads ("ArchivedByUserId");

CREATE INDEX "IX_leads_CreatedByUserId" ON leads ("CreatedByUserId");

CREATE INDEX "IX_leads_TenantId_ArchivedAt" ON leads ("TenantId", "ArchivedAt");

CREATE INDEX "IX_leads_TenantId_AssignedUserId" ON leads ("TenantId", "AssignedUserId");

CREATE INDEX "IX_leads_TenantId_BranchId" ON leads ("TenantId", "BranchId");

CREATE INDEX "IX_leads_TenantId_LeadStageId" ON leads ("TenantId", "LeadStageId");

CREATE INDEX "IX_leads_UpdatedByUserId" ON leads ("UpdatedByUserId");

ALTER TABLE leads ADD CONSTRAINT "FK_leads_users_ArchivedByUserId" FOREIGN KEY ("ArchivedByUserId") REFERENCES users ("Id") ON DELETE SET NULL;

ALTER TABLE leads ADD CONSTRAINT "FK_leads_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE SET NULL;

ALTER TABLE leads ADD CONSTRAINT "FK_leads_users_UpdatedByUserId" FOREIGN KEY ("UpdatedByUserId") REFERENCES users ("Id") ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260627101357_AddLeadManagementCore', '10.0.9');

COMMIT;

