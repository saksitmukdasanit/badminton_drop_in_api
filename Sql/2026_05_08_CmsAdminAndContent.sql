-- Migration: CMS หลังบ้าน — แอดมิน + เนื้อหา splash/banner/popup + audit log
-- รัน: psql ... -f 2026_05_08_CmsAdminAndContent.sql

CREATE TABLE IF NOT EXISTS "CmsAdminUsers" (
    "CmsAdminUserId"   SERIAL PRIMARY KEY,
    "Email"            VARCHAR(255) NOT NULL,
    "PasswordHash"     VARCHAR(256) NOT NULL,
    "DisplayName"      VARCHAR(200) NULL,
    "IsActive"         BOOLEAN NOT NULL DEFAULT TRUE,
    "RefreshToken"     VARCHAR(256) NULL,
    "RefreshTokenExpiryUtc" TIMESTAMPTZ NULL,
    "CreatedAtUtc"     TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    "UpdatedAtUtc"     TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "UQ_CmsAdminUsers_Email" UNIQUE ("Email")
);
CREATE INDEX IF NOT EXISTS "IX_CmsAdminUsers_Email" ON "CmsAdminUsers" ("Email");

COMMENT ON TABLE "CmsAdminUsers" IS 'บัญชีเข้า CMS หลังบ้าน (ไม่ใช่ User ของแอปมือถือ)';

CREATE TABLE IF NOT EXISTS "CmsContentItems" (
    "CmsContentItemId" SERIAL PRIMARY KEY,
    "ContentType"      SMALLINT NOT NULL,
    "Title"            VARCHAR(200) NULL,
    "ImageUrl"         VARCHAR(500) NOT NULL,
    "LinkUrl"          VARCHAR(500) NULL,
    "SortOrder"        INT NOT NULL DEFAULT 0,
    "IsActive"         BOOLEAN NOT NULL DEFAULT TRUE,
    "ValidFromUtc"     TIMESTAMPTZ NULL,
    "ValidToUtc"       TIMESTAMPTZ NULL,
    "Platform"         SMALLINT NOT NULL DEFAULT 0,
    "ExtraJson"        TEXT NULL,
    "CreatedAtUtc"     TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    "UpdatedAtUtc"     TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    "CreatedByCmsAdminUserId" INT NULL REFERENCES "CmsAdminUsers"("CmsAdminUserId") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_CmsContentItems_Type_Active" ON "CmsContentItems" ("ContentType", "IsActive");
CREATE INDEX IF NOT EXISTS "IX_CmsContentItems_Sort" ON "CmsContentItems" ("ContentType", "SortOrder");

COMMENT ON TABLE "CmsContentItems" IS 'รูป splash / แบนเนอร์ / main-popup — แอปมือถือยังไม่ต้อง consume';
COMMENT ON COLUMN "CmsContentItems"."ContentType" IS '1=SplashScreen 2=Banner 3=MainPopup';
COMMENT ON COLUMN "CmsContentItems"."Platform" IS '0=All 1=iOS 2=Android';

CREATE TABLE IF NOT EXISTS "AdminAuditLogs" (
    "AdminAuditLogId"  BIGSERIAL PRIMARY KEY,
    "CmsAdminUserId"   INT NOT NULL REFERENCES "CmsAdminUsers"("CmsAdminUserId") ON DELETE CASCADE,
    "Action"           VARCHAR(80) NOT NULL,
    "EntityType"       VARCHAR(80) NULL,
    "EntityId"         VARCHAR(64) NULL,
    "DetailsJson"      TEXT NULL,
    "IpAddress"        VARCHAR(45) NULL,
    "CreatedAtUtc"     TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
);
CREATE INDEX IF NOT EXISTS "IX_AdminAuditLogs_Admin_Time" ON "AdminAuditLogs" ("CmsAdminUserId", "CreatedAtUtc" DESC);

COMMENT ON TABLE "AdminAuditLogs" IS 'Log การกระทำใน CMS';
