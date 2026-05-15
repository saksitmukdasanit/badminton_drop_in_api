-- Migration: ผู้เล่นเลือกว่าให้แสดงระดับมือบนหน้าแรกจากประเมินของผู้จัดคนไหน
-- ค่า NULL = ให้ระบบเลือกจากแถวล่าสุดใน UserOrganizerSkills (พฤติกรรมเดิม)
-- รัน: psql -h <host> -U <user> -d <db> -f 2026_05_11_UserProfileSkillDisplayOrganizer.sql

ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "SkillDisplayOrganizerUserID" integer NULL;

COMMENT ON COLUMN "UserProfiles"."SkillDisplayOrganizerUserID" IS
    'ผู้จัดที่ผู้เล่นเลือกให้ใช้ระดับมือบนหน้าแรก — NULL = ใช้อัปเดตล่าสุด';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_UserProfiles_SkillDisplayOrganizerUserID'
    ) THEN
        ALTER TABLE "UserProfiles"
            ADD CONSTRAINT "FK_UserProfiles_SkillDisplayOrganizerUserID"
            FOREIGN KEY ("SkillDisplayOrganizerUserID")
            REFERENCES "Users" ("UserID")
            ON DELETE SET NULL;
    END IF;
END $$;
