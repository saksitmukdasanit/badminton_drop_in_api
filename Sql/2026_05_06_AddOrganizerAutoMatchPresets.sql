-- Migration: AddOrganizerAutoMatchPresets
-- Date: 2026-05-06
-- Purpose: เก็บ preset น้ำหนักของ Auto Match ตามผู้จัดแต่ละคน
-- (ก่อนหน้าเก็บใน SharedPreferences ของเครื่อง — ไม่ sync ข้ามเครื่อง)
--
-- Apply: psql -h <host> -U <user> -d <db> -f 2026_05_06_AddOrganizerAutoMatchPresets.sql

CREATE TABLE IF NOT EXISTS "OrganizerAutoMatchPresets"
(
    "UserID" INTEGER NOT NULL,
    "QueuePositionMultiplier" INTEGER NOT NULL DEFAULT 10,
    "MatchTogetherPenaltyPerOccurrence" INTEGER NOT NULL DEFAULT 40,
    "MixedModeOppositeSkillMultiplier" INTEGER NOT NULL DEFAULT 15,
    "MixedModeTeammateSkillMultiplier" INTEGER NOT NULL DEFAULT 20,
    "SameLevelSkillMultiplier" INTEGER NOT NULL DEFAULT 30,
    "TeamFormationTeammateHistoryMultiplier" INTEGER NOT NULL DEFAULT 2,
    "TeamFormationOpponentHistoryMultiplier" INTEGER NOT NULL DEFAULT 1,
    "CreatedDate" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    "UpdatedDate" TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "OrganizerAutoMatchPresets_pkey" PRIMARY KEY ("UserID"),
    CONSTRAINT "FK_OrganizerAutoMatchPresets_UserID"
        FOREIGN KEY ("UserID")
        REFERENCES "Users" ("UserID")
        ON DELETE CASCADE
);
