-- Migration: Organizer recurring game templates (ข้อ 7 roadmap)
-- เซิร์ฟเวอร์สร้างก๊วนจาก template อัตโนมัติในช่วง ~14 วันข้างหน้า (ตามวันในสัปดาห์ที่เลือก)
--
-- Apply: psql ... -f 2026_05_06_AddOrganizerRecurringGameTemplates.sql

CREATE TABLE IF NOT EXISTS "OrganizerRecurringGameTemplates"
(
    "RecurringTemplateID" SERIAL PRIMARY KEY,
    "CreatedByUserID" INTEGER NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    -- bit 0=จันทร์ ... bit 6=อาทิตย์ (สอดคล้อง DateTime.weekday ของ Flutter: Mon=1 -> bit0)
    "DaysOfWeekMask" SMALLINT NOT NULL CHECK ("DaysOfWeekMask" BETWEEN 1 AND 127),

    "GroupName" VARCHAR(255) NOT NULL,
    "GooglePlaceID" VARCHAR(128) NOT NULL,
    "VenueNameSnapshot" VARCHAR(255) NOT NULL,
    "AddressSnapshot" VARCHAR(500) NOT NULL,
    "Latitude" NUMERIC(10, 7) NOT NULL,
    "Longitude" NUMERIC(10, 7) NOT NULL,

    "StartTime" TIME NOT NULL,
    "EndTime" TIME NOT NULL,

    "GameTypeID" INTEGER NULL,
    "PairingMethodID" INTEGER NULL,
    "MaxParticipants" INTEGER NOT NULL,
    "CostingMethod" SMALLINT NULL,

    "CourtFeePerPerson" NUMERIC(10, 2) NULL,
    "ShuttlecockFeePerPerson" NUMERIC(10, 2) NULL,
    "TotalCourtCost" NUMERIC(10, 2) NULL,
    "ShuttlecockCostPerUnit" NUMERIC(10, 2) NULL,
    "ShuttlecockModelID" INTEGER NULL,
    "NumberOfCourts" INTEGER NULL,
    "CourtNumbers" VARCHAR(100) NULL,
    "Notes" TEXT NULL,

    "FacilityIdsCsv" VARCHAR(500) NULL,
    "PhotoUrlsJson" TEXT NULL,

    "CreatedDate" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    "UpdatedDate" TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT "FK_OrganizerRecurringGameTemplates_UserID"
        FOREIGN KEY ("CreatedByUserID")
            REFERENCES "Users" ("UserID")
            ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_OrganizerRecurringGameTemplates_CreatedByUserID_Active"
    ON "OrganizerRecurringGameTemplates" ("CreatedByUserID", "IsActive");
