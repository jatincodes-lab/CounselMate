START TRANSACTION;
ALTER TABLE follow_ups ADD "CancelledAt" timestamp without time zone;

ALTER TABLE follow_ups ADD "CompletedAt" timestamp without time zone;

ALTER TABLE follow_ups ADD "UpdatedAt" timestamp without time zone NOT NULL DEFAULT TIMESTAMP '-infinity';

ALTER TABLE follow_ups ADD "Version" integer NOT NULL DEFAULT 1;

UPDATE follow_ups SET "CancelledAt" = NULL, "CompletedAt" = NULL, "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
WHERE "Id" = '80000000-0000-0000-0000-000000000001';

UPDATE follow_ups SET "CancelledAt" = NULL, "CompletedAt" = NULL, "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
WHERE "Id" = '80000000-0000-0000-0000-000000000002';

UPDATE follow_ups SET "CancelledAt" = NULL, "CompletedAt" = NULL, "UpdatedAt" = TIMESTAMP '2026-06-25T05:30:00', "Version" = 1
WHERE "Id" = '80000000-0000-0000-0000-000000000003';

UPDATE follow_ups
SET "UpdatedAt" = "CreatedAt",
    "Version" = 1
WHERE "UpdatedAt" <= TIMESTAMP '1900-01-01'
   OR "Version" < 1;

ALTER TABLE follow_ups ALTER COLUMN "UpdatedAt" DROP DEFAULT;
ALTER TABLE follow_ups ALTER COLUMN "Version" SET DEFAULT 1;

CREATE INDEX "IX_follow_ups_TenantId_Status_DueAt" ON follow_ups ("TenantId", "Status", "DueAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260627110009_AddFollowUpLifecycle', '10.0.9');

COMMIT;

