-- CMS: เกี่ยวกับเรา (singleton) + เอกสารนโยบาย

CREATE TABLE IF NOT EXISTS "CmsAboutSettings" (
    "CmsAboutSettingsId" SMALLINT NOT NULL PRIMARY KEY DEFAULT 1,
    "Title"              VARCHAR(300) NOT NULL DEFAULT '',
    "Body"               TEXT NOT NULL DEFAULT '',
    "UpdatedAtUtc"       TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "CK_CmsAboutSettings_Singleton" CHECK ("CmsAboutSettingsId" = 1)
);

INSERT INTO "CmsAboutSettings" ("CmsAboutSettingsId", "Title", "Body", "UpdatedAtUtc")
SELECT 1, '', '', (now() AT TIME ZONE 'utc')
WHERE NOT EXISTS (SELECT 1 FROM "CmsAboutSettings" WHERE "CmsAboutSettingsId" = 1);

CREATE TABLE IF NOT EXISTS "CmsPolicyDocuments" (
    "CmsPolicyDocumentId" SERIAL PRIMARY KEY,
    "Title"               VARCHAR(200) NOT NULL,
    "Slug"                VARCHAR(120) NOT NULL,
    "Body"                TEXT NOT NULL,
    "SortOrder"           INT NOT NULL DEFAULT 0,
    "IsActive"            BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAtUtc"        TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    "UpdatedAtUtc"        TIMESTAMPTZ NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT "UQ_CmsPolicyDocuments_Slug" UNIQUE ("Slug")
);

CREATE INDEX IF NOT EXISTS "IX_CmsPolicyDocuments_Active_Sort" ON "CmsPolicyDocuments" ("IsActive", "SortOrder");
