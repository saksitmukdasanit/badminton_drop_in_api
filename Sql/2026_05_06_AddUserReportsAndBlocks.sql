-- Migration: เพิ่ม UserReports + UserBlocks สำหรับ Apple Guideline 1.2 / 5.1.1(viii)
-- รัน: psql -h <host> -U <user> -d <db> -f 2026_05_06_AddUserReportsAndBlocks.sql

CREATE TABLE IF NOT EXISTS "UserReports" (
    "ReportId"            SERIAL PRIMARY KEY,
    "ReporterUserId"      integer NOT NULL REFERENCES "Users"("UserID") ON DELETE CASCADE,
    "ReportedUserId"      integer NOT NULL REFERENCES "Users"("UserID") ON DELETE CASCADE,
    "Reason"              varchar(50) NOT NULL,
    "Description"         text NULL,
    "SessionId"           integer NULL REFERENCES "GameSessions"("SessionID") ON DELETE SET NULL,
    "CreatedAt"           timestamp with time zone NOT NULL DEFAULT now(),
    "ResolvedAt"          timestamp with time zone NULL,
    "ResolvedByUserId"    integer NULL REFERENCES "Users"("UserID") ON DELETE SET NULL,
    "AdminNotes"          text NULL,
    CONSTRAINT "CK_UserReports_NoSelfReport" CHECK ("ReporterUserId" != "ReportedUserId")
);
CREATE INDEX IF NOT EXISTS "IX_UserReports_Reported" ON "UserReports" ("ReportedUserId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_UserReports_Pending" ON "UserReports" ("CreatedAt" DESC) WHERE "ResolvedAt" IS NULL;

COMMENT ON TABLE "UserReports" IS 'รายงานพฤติกรรมผู้ใช้ — Apple 5.1.1(viii)';
COMMENT ON COLUMN "UserReports"."Reason" IS 'spam | harassment | fraud | fake_profile | inappropriate_content | other';

CREATE TABLE IF NOT EXISTS "UserBlocks" (
    "BlockId"            SERIAL PRIMARY KEY,
    "BlockerUserId"      integer NOT NULL REFERENCES "Users"("UserID") ON DELETE CASCADE,
    "BlockedUserId"      integer NOT NULL REFERENCES "Users"("UserID") ON DELETE CASCADE,
    "CreatedAt"          timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "UQ_UserBlocks" UNIQUE ("BlockerUserId", "BlockedUserId"),
    CONSTRAINT "CK_UserBlocks_NoSelfBlock" CHECK ("BlockerUserId" != "BlockedUserId")
);
CREATE INDEX IF NOT EXISTS "IX_UserBlocks_Blocker" ON "UserBlocks" ("BlockerUserId");
CREATE INDEX IF NOT EXISTS "IX_UserBlocks_Blocked" ON "UserBlocks" ("BlockedUserId");

COMMENT ON TABLE "UserBlocks" IS 'การ block ผู้ใช้ — Apple 1.2';
