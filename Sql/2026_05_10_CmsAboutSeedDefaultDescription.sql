-- เนื้อหาค่าเริ่มต้นหน้า «เกี่ยวกับเรา» / เกี่ยวกับในแอป (รูปแบบเดียวกับที่ CMS About ส่งเป็น JSON)
-- ใช้เฉพาะเมื่อแถว singleton ยังไม่ได้กรอก (ไม่ overwrite ของที่แก้ผ่าน CMS แล้ว)

UPDATE "CmsAboutSettings"
SET
    "Title" = 'Drop In Bad',
    "Body" =
        json_build_object(
                'appLogoUrl', '',
                'appName', 'Drop In Bad',
                'appVersion', '1.0.0',
                'supportEmail', 'support@dropinbad.com',
                'policyUrl', '',
                'termsUrl', '',
                'description',
                E'Drop In Bad เป็นแพลตฟอร์มสำหรับจัดและเข้าร่วมแบดมินตันแบบก๊วน (drop-in) โดยเชื่อมผู้เล่นกับผู้จัดไว้ในที่เดียว '
                    E'เราเชื่อว่าการเข้าก๊วนควรสะดวกและโปร่งใส ทั้งคิว การลงคอร์ท การจัดแมตช์ และค่าสนาม\n\n'
                    E'ฟีเจอร์ที่เน้น:\n'
                    E'• ค้นหาและเข้าร่วมก๊วนได้จากมือถือ\n'
                    E'• ดูสถานะคิวและสนามแบบผูกกับก๊วนที่เข้าร่วมอยู่\n'
                    E'• ระบบกระเป๋าเงินและบัญชีรับเงินสำหรับธุรกรรมที่เกี่ยวกับกิจกรรมในแอป\n\n'
                    E'ทีมให้ความช่วยเหลือผ่านอีเมลฝ่ายสนับสนุน และพัฒนาแอปต่อจากข้อเสนอแนะของผู้เล่นและผู้จัด'
            )::text,
    "UpdatedAtUtc" = (now() AT TIME ZONE 'utc')
WHERE "CmsAboutSettingsId" = 1
  AND trim(coalesce("Body", '')) = '';
