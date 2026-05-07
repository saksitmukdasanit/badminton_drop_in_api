-- Migration: เพิ่มคอลัมน์ DeletedAt สำหรับระบบ Account Deletion (Apple Guideline 5.1.1(v))
-- ภายใต้นโยบาย soft-delete + 30-day grace period
-- รัน: psql -h <host> -U <user> -d <db> -f 2026_05_06_AddUserDeletedAt.sql

ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;

COMMENT ON COLUMN "Users"."DeletedAt" IS
    'เวลาที่ผู้ใช้ขอลบบัญชี — หลัง 30 วันจะถูก hard-delete โดย background job. NULL = ยังใช้งานปกติ';

-- Index สำหรับ background job ที่จะ hard-delete users ครบกำหนด
CREATE INDEX IF NOT EXISTS "IX_Users_DeletedAt"
    ON "Users" ("DeletedAt")
    WHERE "DeletedAt" IS NOT NULL;
