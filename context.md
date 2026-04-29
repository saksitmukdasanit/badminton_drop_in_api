# 🏸 Drop-in Badminton (DropInBad) - Project Context

## 1. Tech Stack
- **Frontend:** Flutter (Dart)
- **Backend:** .NET 8 Web API (C#)
- **Database:** PostgreSQL (ใช้ Entity Framework Core)
- **Real-time Communication:** SignalR
- **Payment Gateway:** Xendit (Dynamic QR Code, Sub-accounts, Payout)
- **SMS & OTP:** SMSMKT (ใช้ยิง OTP ยืนยันเบอร์โทรศัพท์)
- **Map & Places:** Google Maps API (Places Autocomplete สำหรับค้นหาสถานที่ในหน้าสร้างก๊วน)

## 2. User Roles (บทบาทผู้ใช้งาน)
- **Organizer (ผู้จัด):** สร้างก๊วน, จัดแมตช์การแข่งขัน (Auto/Manual), เช็คบิลค่าใช้จ่าย
- **Player (ผู้เล่นตัวจริง/สำรอง):** จองคิวเล่น, ดู Live State กระดาน, จ่ายเงิน
- **Guest (Walk-in):** ผู้เล่นที่ผู้จัดเพิ่มหน้างาน

## 3. Core Flows (กระบวนการหลัก)

### 3.1 Match Management & Live State (การจัดการกระดาน)
- **API Hub:** `ManagementGameHub` (SignalR)
- **Flow:** เมื่อผู้จัดย้ายตัวผู้เล่น, เริ่มเกม (`Status=1`), หรือจบเกม (`Status=2`) API จะคำนวณและยิง SignalR `ReceiveLiveStateUpdate` กลับไปที่แอปทุกเครื่องให้อัปเดตหน้าจอทันที
- **SignalR Events (Client-side Listeners):**
  - `ReceiveLiveStateUpdate`: รับข้อมูลกระดานล่าสุด (ผู้เล่น, สนาม, ทีมสำรอง, เวลาเริ่ม)
  - `PlayerPauseStateChanged`: แจ้งเตือนเมื่อผู้เล่นกดหยุด/กลับสู่เกม
  - `QrPaymentSuccess`: แจ้งเตือนเมื่อสแกนจ่ายเงินผ่าน QR สำเร็จ (เพื่อปิดหน้าต่าง QR อัตโนมัติ)
  - `PlayerCheckedIn`: แจ้งเตือนเมื่อผู้เล่นสแกน QR เช็คอินเข้าสนาม

### 3.2 Billing & Payment (การคิดเงิน)
- **รูปแบบการคิดค่าลูกแบด (`CostingMethod`):** 
  - `1` = คิดตามจำนวนเกมที่เล่น (Per Game)
  - `2` = เหมาจ่าย (Buffet)
- **การคำนวณ (Single Source of Truth):** 
  - ให้ Backend เป็นคนคำนวณเสมอ (ผ่าน API `/checkout` และ `/bill-preview`)
  - Frontend ส่งไปแค่ `CustomLineItems` (รายการที่ผู้จัดเพิ่ม/ลดเอง เช่น ค่าน้ำ)
- **การชำระเงิน (Xendit):** 
  - บันทึกบิลเป็น `Status=1` (ค้างชำระ)
  - API วิ่งไปขอ QR Code จาก Xendit (`CreateQrCodeAsync`)
  - Xendit ส่ง Webhook กลับมาที่ `/api/webhooks/xendit/qr-payment` เมื่อลูกค้าจ่ายสำเร็จ
  - ส่ง SignalR ปิดหน้าจอ QR
- **ช่องทางการชำระเงิน (`PaymentMethod` in DB):**
  - `1` = เงินสด / บัตรเครดิต (Cash/Card)
  - `2` = QR Code
  - `3` = กระเป๋าเงิน (Wallet)

### 3.3 Refund, Wallet & Withdrawal (กระเป๋าเงินและการถอน)
- **ตารางหลัก:** `UserWallets` และ `WalletTransactions`
- **โครงสร้างรายได้ (Money Flow):**
  - **ยอดที่ผู้เล่นจ่าย (Total) = ค่าสนาม + ค่าลูกแบด + ค่าธรรมเนียม (Service Fee)**
  - ผู้จัด (Organizer) จะได้รับเฉพาะ ค่าสนาม + ค่าลูกแบด (เข้า Wallet หรือ Xendit Sub-account)
  - แพลตฟอร์มจะได้รับ ค่าธรรมเนียม (Service Fee)
- **การคืนเงิน (Refund) & หนี้สิน (Negative Balance):** 
  - คืนเงิน **เต็มจำนวน (Total)** เข้า Wallet ผู้เล่นอัตโนมัติเมื่อมีการยกเลิกก๊วน
  - ระบบจะ **ดึงเงินกลับจาก Wallet ผู้จัด** (เฉพาะส่วนค่าสนาม+ลูกแบดที่ผู้จัดได้รับไป)
  - หากผู้จัดมีเงินใน Wallet ไม่พอ **ยอดจะกลายเป็นติดลบ (หนี้สิน)** และจะถูกนำไปหักลบกับรายได้ที่จะเข้ามาใหม่ในรอบถัดไป
- **การใช้เงิน (Pay with Wallet):** ผู้เล่นชำระค่าก๊วนด้วย Wallet ระบบจะหักเงินผู้เล่น (`TransactionType = 2`) และนำยอดสุทธิ (ไม่รวมค่าธรรมเนียม) ไปเพิ่มใน Wallet ผู้จัดอัตโนมัติ (`TransactionType = 1`) ทำหน้าที่เสมือนตัวกลาง (Escrow)
- **การถอนเงิน (Withdrawal & Payout):** 
  - ผู้เล่นและผู้จัดสามารถสั่งถอนเงินคงเหลือในระบบเข้าบัญชีธนาคารที่ผูกไว้
  - ผูกกับระบบ **Xendit Payout API** โดยระบบจะส่งคำสั่งโอนเงินไปยังธนาคารปลายทางอัตโนมัติ (กรณีถอนรายได้ผู้จัด จะดึงเงินจาก Sub-account ของผู้จัดผ่าน `for-user-id`) เมื่อได้รับ ID อ้างอิงกลับมาจึงจะทำการตัดยอด Wallet

### 3.4 Push Notifications (ระบบการแจ้งเตือน)
- ตารางหลัก `Notifications` เชื่อมต่อกับ Firebase Cloud Messaging (FCM) และ Local Notifications
- **Events ที่แจ้งเตือนผู้เล่น:** จองก๊วนสำเร็จ, ก๊วนถูกยกเลิก, เลื่อนเป็นตัวจริง (Promote), แจ้งเตือนลงสนาม (Match Start), ยืนยันการรับเงิน
- **Events ที่แจ้งเตือนผู้จัด:** มีผู้เล่นจองก๊วน/สำรอง, มีผู้เล่นยกเลิก, ได้รับชำระเงิน, ผู้เล่นเช็คอิน (สแกน QR)

### 3.5 Auto Match (การจัดคู่อัตโนมัติ)
- **เงื่อนไขคัดกรองผู้เล่น:** ต้องเช็คอินแล้ว, ยังไม่เช็คเอาท์ (จ่ายเงินออกไป), ไม่อยู่ในสนาม (กำลังเล่น/เตรียมลง), และไม่ถูกผู้จัดระงับ (Pause/End Game)
- **ระบบการให้คะแนน (Scoring System):** คัดเลือกผู้เล่นโดยรวมคะแนนจาก 3 ส่วนคือ
  1. **Queue Score:** ลำดับคิว (รอนานสุด/เกมน้อยสุด จะได้คะแนนดีสุด)
  2. **History Score:** ประวัติการเล่น (ลงโทษคะแนนหากเคยคู่กัน หรือเป็นคู่แข่งกันมาก่อน เพื่อกระจายผู้เล่นไม่ให้ซ้ำหน้า)
  3. **Skill Score:** ระดับฝีมือ (คำนวณระยะห่างฝีมือให้สอดคล้องกับโหมดที่เลือก)
- **โหมดการคัดคน (Selection Mode):**
  - **โหมดผสม (Mixed Mode):** ดึงคนคิวแรกเป็นแกนหลัก หาระดับมือที่ห่างที่สุดมาอยู่ฝั่งตรงข้าม และหาคนที่ระดับมือใกล้เคียงกับแกนหลักมาช่วยประคองทีม
  - **โหมดจัดตามมือ (Skill Mode):** หาระดับมือที่ใกล้เคียงกับแกนหลักมากที่สุด
- **การจัดทีมภายใน (Team Balancing):** นำ 4 คนที่เลือกมาจัดทีม A vs B โดยคำนวณจากรูปแบบที่เป็นไปได้ทั้งหมด เพื่อให้ได้ทีมที่ผลรวมฝีมือสูสีกันที่สุด และหลีกเลี่ยงการจับคู่หรือเจอคู่แข่งที่ซ้ำกับแมตช์ก่อนหน้า
- **ผลลัพธ์:** สร้างเป็นรายการเตรียมลงสนาม (`Staged Match` Status = 4) นำไปลงในคอร์ทที่ว่าง หรือสร้างเป็น "ทีมสำรอง" (-1, -2) กรณีที่คอร์ทเต็ม

### 3.6 Skill Level Management (การจัดการระดับมือ)
- **Default Levels:** หากผู้จัดสมัครใหม่ ระบบจะสร้าง 4 ระดับให้เป็นค่าเริ่มต้นอัตโนมัติ (มือใหม่, มือเบา, มือกลาง, มือหนัก)
- **Soft Delete:** การซ่อน/ลบระดับมือโดยผู้จัด จะแค่ปรับ `IsActive = false` เพื่อไม่ให้กระทบประวัติการเล่นเก่า ส่วน UI ฝั่งแอปจะป้องกันแอปแครชโดยรีเซ็ตค่าเป็นว่างอัตโนมัติหากหาไม่เจอ
- **Global Tracking:** ระดับมือของผู้เล่นจะผูกติดกับผู้จัดแต่ละคน (`UserOrganizerSkills`) เมื่อผู้เล่นจองก๊วนใหม่กับผู้จัดเดิม ระบบจะดึงระดับมือล่าสุดมาแสดงให้อัตโนมัติ

### 3.7 Authentication & Routing (การยืนยันตัวตนและการนำทาง)
- **Apple App Store Guideline Compliance:** อนุญาตให้ผู้ใช้ทั่วไป (Guest) สามารถเข้าดูหน้า Home และหน้ารายละเอียดก๊วนได้โดยไม่ต้อง Login จะถูกบังคับ Login ก็ต่อเมื่อกระทำการสำคัญ (เช่น จองก๊วน, ชำระเงิน)
- **Token Refresh Locking:** มีกลไก Lock ใน `ApiProvider` เพื่อป้องกันการยิง API Refresh Token ซ้ำซ้อนพร้อมกันหลายเส้นเมื่อ Token หมดอายุ (แก้ปัญหาแอปแครชและเซิร์ฟเวอร์ทำงานหนัก)
- **Rolling Refresh Token:** ทุกครั้งที่มีการ Refresh Token ฝั่ง Backend จะยืดอายุ Refresh Token ออกไปอีก 90 วัน เพื่อทำระบบ "Keep Me Logged In" อย่างสมบูรณ์แบบ

## 4. Database Schema Summary (ตารางที่สำคัญ)
- **Users / UserProfiles:** ข้อมูลผู้ใช้งานทั่วไป
- **OrganizerProfiles:** ข้อมูลผู้จัด (มี `XenditAccountId` สำหรับรับเงินแยกกระเป๋า)
- **GameSessions:** ข้อมูลก๊วนแบด (`Status`: 1=เปิดรับ, 2=กำลังเล่น, 3=ยกเลิก, 4=จบแล้ว)
- **SessionParticipants / SessionWalkinGuests:** รายชื่อคนจองและ Walk-in
- **Matches / MatchPlayers:** ข้อมูลการแข่งขันแต่ละรอบ
- **ParticipantBills / BillLineItems / Payments:** บิลและประวัติการจ่ายเงิน

## 5. Database DBML (โครงสร้างตารางหลัก)
```dbml
// =============================================================================
// SECTION 1: MASTER DATA
// =============================================================================
Table "Banks" {
  "BankID" INT [pk, increment]
  "BankName" NVARCHAR(100) [not null, note: 'ชื่อธนาคาร']
  "BankCode" NVARCHAR(10) [note: 'รหัสธนาคาร']
  "IsActive" BIT [default: 1]
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}
Table "Facilities" {
  "FacilityID" INT [pk, increment]
  "FacilityName" NVARCHAR(100) [not null, note: 'ชื่อสิ่งอำนวยความสะดวก']
  "IconName" NVARCHAR(250) [note: 'ชื่อไอคอนสำหรับแสดงผล']
  "IsActive" BIT [default: 1]
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}
Table "GameTypes" {
  "GameTypeID" INT [pk, increment]
  "TypeName" NVARCHAR(100) [not null, note: 'ชื่อประเภทเกม']
  "IsActive" BIT [default: 1]
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}
Table "PairingMethods" {
  "PairingMethodID" INT [pk, increment]
  "MethodName" NVARCHAR(100) [not null, note: 'ชื่อวิธีจัดคู่']
  "IsActive" BIT [default: 1]
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}
Table "ShuttlecockBrands" {
  "BrandID" INT [pk, increment]
  "BrandName" NVARCHAR(100) [not null, note: 'ชื่อยี่ห้อลูกแบด']
  "IsActive" BIT [default: 1]
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}
Table "ShuttlecockModels" {
  "ModelID" INT [pk, increment]
  "ModelName" NVARCHAR(100) [not null, note: 'ชื่อรุ่นลูกแบด']
  "BrandID" INT [not null, note: 'ID ยี่ห้อที่เป็นเจ้าของรุ่นนี้']
  "IsActive" BIT [default: 1]
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}


// =============================================================================
// SECTION 2: USER & AUTHENTICATION
// =============================================================================
Table "Users" {
  "UserID" INT [pk, increment, note: 'ID หลักภายในระบบ (สำหรับ Join)']
  "UserPublicId" UNIQUEIDENTIFIER [unique, not null, default: `NEWID()`, note: 'ID สำหรับใช้อ้างอิงภายนอก (API, URL)']
  "IsActive" BIT [not null, default: 1, note: 'สถานะบัญชี: 1=ใช้งาน, 0=ถูกระงับ']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันที่สร้างบัญชี']
  "CreatedBy" INT [note: 'สร้างโดย UserID ไหน']
  "UpdatedDate" DATETIME2 [note: 'วันที่แก้ไขล่าสุด']
  "UpdatedBy" INT [note: 'แก้ไขโดย UserID ไหน']
}
Table "UserProfiles" {
  "UserID" INT [pk, note: 'ID ของ User ที่เป็นเจ้าของโปรไฟล์นี้ (ข้อมูลผู้เล่น)']
  "ProfilePhotoURL" NVARCHAR(500) [note: 'URL รูปโปรไฟล์ของผู้เล่น']
  "PrimaryContactEmail" NVARCHAR(255) [note: 'อีเมลหลักสำหรับใช้ติดต่อ']
  "Nickname" NVARCHAR(100) [note: 'ชื่อเล่น']
  "FirstName" NVARCHAR(150) [note: 'ชื่อจริง']
  "LastName" NVARCHAR(150) [note: 'นามสกุล']
  "Gender" TINYINT [note: 'เพศ: 1=ชาย, 2=หญิง, 3=อื่นๆ']
  "PhoneNumber" NVARCHAR(20) [note: 'เบอร์โทรศัพท์ส่วนตัว']
  "IsPhoneNumberVerified" BIT [not null, default: 0, note: 'สถานะยืนยันเบอร์โทร: 1=ยืนยันแล้ว']
  "EmergencyContactName" NVARCHAR(200) [note: 'ชื่อผู้ติดต่อฉุกเฉิน']
  "EmergencyContactPhone" NVARCHAR(20) [note: 'เบอร์ผู้ติดต่อฉุกเฉิน']
  "OTPCode" NVARCHAR(6) [note: 'รหัส OTP ชั่วคราว']
  "OTPExpiryDate" DATETIME2 [note: 'เวลาหมดอายุของ OTP']
  "BankID" INT [note: 'ธนาคารสำหรับถอนเงิน']
  "BankAccountNumber" NVARCHAR(50) [note: 'เลขบัญชีธนาคาร']
  "BankAccountName" NVARCHAR(150) [note: 'ชื่อบัญชีธนาคาร']
  "BankAccountPhotoURL" NVARCHAR(500) [note: 'รูปสมุดบัญชีธนาคาร']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}
Table "OrganizerProfiles" {
  "UserID" INT [pk, note: 'ID ของ User ที่เป็นเจ้าของโปรไฟล์นี้ (ข้อมูลผู้จัด)']
  "ProfilePhotoURL" NVARCHAR(500) [note: 'URL รูปโปรไฟล์ของผู้จัด']
  "NationalID" NVARCHAR(255) [note: 'เลขบัตรประชาชน (ควรเข้ารหัส)']
  "XenditAccountID" NVARCHAR(100) [note: 'Line ID']
  "BankID" INT [not null, note: 'ID ธนาคาร']
  "BankAccountNumber" NVARCHAR(50) [not null, note: 'เลขบัญชีธนาคาร']
  "BankAccountPhotoURL" NVARCHAR(500) [note: 'URL รูปสมุดบัญชี']
  "PublicPhoneNumber" NVARCHAR(20) [note: 'เบอร์โทรสำหรับติดต่อสาธารณะ']
  "FacebookLink" NVARCHAR(500) [note: 'ลิงก์ Facebook']
  "LineID" NVARCHAR(100) [note: 'Line ID']
  "PhoneVisibility" TINYINT [not null, default: 0, note: 'การแสดงผลเบอร์โทร: 0=ไม่แสดง, 1=หลังจอง, 2=สาธารณะ']
  "FacebookVisibility" TINYINT [not null, default: 0, note: 'การแสดงผล Facebook: 0=ไม่แสดง, 1=หลังจอง, 2=สาธารณะ']
  "LineVisibility" TINYINT [not null, default: 0, note: 'การแสดงผล Line: 0=ไม่แสดง, 1=หลังจอง, 2=สาธารณะ']
  "Status" TINYINT [not null, default: 0, note: '0=Pending, 1=Approved, 2=Rejected, 3=Inactive']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}
Table "UserLogins" {
  "ProviderName" NVARCHAR(50) [not null, note: 'ชื่อผู้ให้บริการ เช่น Local, Google, Facebook']
  "ProviderKey" NVARCHAR(255) [not null, note: 'ID ที่ได้จากผู้ให้บริการนั้นๆ']
  "UserID" INT [not null, note: 'ID ของ User ที่เป็นเจ้าของ Login นี้']
  "PasswordHash" NVARCHAR(256) [note: 'รหัสผ่านที่เข้ารหัสแล้ว (สำหรับ Local Login)']
  "ProviderEmail" NVARCHAR(255) [note: 'อีเมลที่ได้มาจากผู้ให้บริการ']
  "RefreshToken" NVARCHAR(256) [note: 'Refresh Token ที่ใช้งานได้']
  "RefreshTokenExpiryTime" DATETIME2 [note: 'วันหมดอายุของ Refresh Token']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
  Indexes { ("ProviderName", "ProviderKey") [pk] }
}
// *** ตารางใหม่: สำหรับเก็บชุดระดับฝีมือที่ผู้จัดสร้างเอง ***
Table "OrganizerSkillLevels" {
  "SkillLevelID" INT [pk, increment]
  "OrganizerUserID" INT [not null, note: 'UserID ของผู้จัดที่เป็นเจ้าของระดับมือชุดนี้']
  "LevelRank" TINYINT [not null, note: 'ลำดับของระดับ (1-10)']
  "LevelName" NVARCHAR(50) [not null, note: 'ชื่อระดับที่ผู้จัดตั้งเอง']
  "ColorHexCode" NVARCHAR(7) [not null, note: 'โค้ดสีสำหรับแสดงผล เช่น #FF5733']
  "IsActive" BIT [default: 1]
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
  "UpdatedDate" DATETIME2
  "UpdatedBy" INT
}

Table "UserOrganizerSkills" {
  "UserID" INT [not null, note: 'ID ผู้เล่น']
  "OrganizerUserID" INT [not null, note: 'ID ผู้จัด']
  "SkillLevelID" INT [not null, note: 'ID ระดับมือ (จาก OrganizerSkillLevels)']
  "UpdatedDate" DATETIME2 [default: `GETUTCDATE()`]
  "UpdatedBy" INT
  Indexes { (UserID, OrganizerUserID) [pk] }
}

// =============================================================================
// SECTION 3: GAME & BOOKING
// =============================================================================
Table "Venues" {
  "VenueID" INT [pk, increment, note: 'ID ของสนามภายในระบบ']
  "GooglePlaceId" NVARCHAR(255) [unique, not null, note: 'ID ของสถานที่จาก Google Places API']
  "VenueName" NVARCHAR(255) [not null, note: 'ชื่อสนาม']
  "Address" NVARCHAR(MAX) [note: 'ที่อยู่']
  "Latitude" DECIMAL(9,6) [note: 'ละติจูด']
  "Longitude" DECIMAL(9,6) [note: 'ลองจิจูด']
  "FirstUsedDate" DATETIME2 [default: `GETUTCDATE()`, note: 'วันที่ถูกใช้ในระบบครั้งแรก']
  "FirstUsedByUserID" INT [note: 'UserID ของคนที่เพิ่มสนามนี้เข้ามาคนแรก']
}
Table "GameSessions" {
  "SessionID" INT [pk, increment, note: 'ID ของก๊วน']
  "SessionPublicId" UNIQUEIDENTIFIER [unique, not null, default: `NEWID()`, note: 'ID ก๊วนสำหรับใช้อ้างอิงภายนอก']
  "GroupName" NVARCHAR(255) [not null, note: 'ชื่อทีม/ชื่อก๊วน']
  "VenueID" INT [not null, note: 'ID ของสนามที่จัดก๊วน']
  "SessionDate" DATE [not null, note: 'วันที่จัด']
  "StartTime" TIME [not null, note: 'เวลาเริ่มต้น']
  "EndTime" TIME [not null, note: 'เวลาสิ้นสุด']
  "GameTypeID" INT [note: 'ID ประเภทเกม']
  "PairingMethodID" INT [note: 'ID วิธีจัดคู่']
  "MaxParticipants" INT [not null, note: 'จำนวนที่เปิดรับจองสูงสุด']
  "CostingMethod" TINYINT [note: 'วิธีคิดเงิน: 1=เก็บตามจำนวนลูก, 2=บุฟเฟ่ต์']
  "CourtFeePerPerson" DECIMAL(10,2) [note: 'ราคาค่าคอร์ทต่อคน']
  "ShuttlecockFeePerPerson" DECIMAL(10,2) [note: 'ราคาค่าลูกแบดต่อคน']
  "TotalCourtCost" DECIMAL(10,2) [note: 'ต้นทุนสนามทั้งหมด']
  "ShuttlecockCostPerUnit" DECIMAL(10,2) [note: 'ต้นทุนลูกแบดต่อลูก']
  "ShuttlecockModelID" INT [note: 'ID รุ่นลูกแบดที่ใช้']
  "NumberOfCourts" INT [note: 'จำนวนคอร์ทที่ใช้']
  "CourtNumbers" NVARCHAR(100) [note: 'หมายเลขคอร์ท เช่น "1, 2, 4"']
  "Notes" NVARCHAR(MAX) [note: 'รายละเอียดเพิ่มเติม/Note']
  "Status" TINYINT [default: 1, note: 'สถานะก๊วน: 1=เปิดรับ, 2=เต็ม, 3=ยกเลิก']
  "CreatedByUserID" INT [not null, note: 'UserID ของคนสร้างก๊วน']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "UpdatedDate" DATETIME2
}
Table "SessionParticipants" {
  "ParticipantID" INT [pk, increment, note: 'ID ของการลงทะเบียน']
  "SessionID" INT [not null, note: 'ID ของก๊วนที่ลงทะเบียน']
  "UserID" INT [not null, note: 'ID ของผู้ใช้ที่ลงทะเบียน']
  "SkillLevelID" INT [note: 'ID ระดับฝีมือที่ผู้จัดกำหนดสำหรับก๊วนนี้ (Nullable)']
  "Status" TINYINT [default: 1, note: 'สถานะ: 1=เข้าร่วม, 2=รอคิว (Waitlist), 3=ยกเลิก']
  "JoinedDate" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันเวลาที่ลงทะเบียน']
  "CheckinTime" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันเวลาที่มา']
  "CheckoutTime" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันเวลาที่กลับ']
}
Table "GameSessionFacilities" {
  "SessionID" INT [pk, note: 'ID ของก๊วน']
  "FacilityID" INT [pk, note: 'ID ของสิ่งอำนวยความสะดวก']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันที่เพิ่ม']
  "CreatedBy" INT [note: 'UserID ของคนที่เพิ่ม']
}
Table "GameSessionPhotos" {
  "PhotoID" INT [pk, increment, note: 'ID ของรูปภาพ']
  "SessionID" INT [not null, note: 'ID ของก๊วนที่เป็นเจ้าของรูป']
  "PhotoURL" NVARCHAR(500) [not null, note: 'URL ของไฟล์รูปภาพ']
  "DisplayOrder" TINYINT [note: 'ลำดับการแสดงผลของรูปภาพ (เช่น 1-5)']
  "Caption" NVARCHAR(255) [note: 'คำบรรยายใต้ภาพ (ถ้ามี)']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT [note: 'UserID ของคนที่อัปโหลด']
}
Table "SessionWalkinGuests" {
  "WalkinID" INT [pk, increment, note: 'ID ของรายการ Walk-in']
  "SessionID" INT [not null, note: 'ID ของก๊วนที่เข้าร่วม']
  "GuestName" NVARCHAR(150) [not null, note: 'ชื่อเล่นของผู้เล่น Walk-in']
  "Gender" TINYINT [note: 'เพศ: 1=ชาย, 2=หญิง, 3=อื่นๆ']
  "SkillLevelID" INT [note: 'ID ระดับฝีมือที่ผู้จัดกำหนด']
  "AmountPaid" DECIMAL(10,2) [note: 'จำนวนเงินที่จ่าย']
  "PaymentDate" DATETIME2 [note: 'วันเวลาที่จ่ายเงิน']
  "Status" TINYINT [default: 1, note: 'สถานะ: 1=เข้าร่วม, 3=ยกเลิก']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันที่เพิ่ม']
  "CreatedBy" INT [note: 'UserID ของผู้จัดที่เพิ่มเข้ามา']
  "CheckinTime" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันเวลาที่มา']
  "CheckoutTime" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันเวลาที่กลับ']
}

Table "Matches" {
  "MatchID" INT [pk, increment]
  "SessionID" INT [not null]
  "CourtNumber" INT [not null]
  "StartTime" DATETIME2
  "EndTime" DATETIME2
  "Status" TINYINT [note: 'สถานะ: 1=กำลังเล่น, 2=จบแล้ว']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT
}
Table "MatchPlayers" {
  "MatchPlayerID" INT [pk, increment]
  "MatchID" INT [not null]
  "UserID" INT
  "WalkinID" INT
  "Team" NVARCHAR(1) [not null, note: 'ทีม A หรือ B']
  "Result" TINYINT [note: 'ผลการแข่ง: 1=ชนะ, 2=แพ้, 3=เสมอ']
  "Notes" NVARCHAR(MAX) [note: 'โน้ตจากผู้เล่น']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  "CreatedBy" INT [note: 'ผู้จัดที่สร้างแมตช์']
  "UpdatedDate" DATETIME2 [note: 'วันที่ส่งผล']
  "UpdatedBy" INT [note: 'ผู้เล่นที่ส่งผล']
}
Table "ParticipantBills" {
  "BillID" INT [pk, increment, note: 'ID ของใบแจ้งหนี้']
  "SessionID" INT [not null, note: 'ID ของก๊วนที่สร้างใบแจ้งหนี้นี้']
  "UserID" INT [note: 'ID ของผู้เล่นที่เป็นสมาชิก (Nullable)']
  "WalkinID" INT [note: 'ID ของผู้เล่น Walk-in (Nullable)']
  "TotalAmount" DECIMAL(10,2) [not null, note: 'ยอดรวมที่ต้องชำระ']
  "Status" TINYINT [not null, note: 'สถานะใบแจ้งหนี้: 1=ยังไม่จ่าย, 2=จ่ายแล้ว, 3=ยกเลิก']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันที่สร้างใบแจ้งหนี้']
}

Table "BillLineItems" {
  "LineItemID" INT [pk, increment, note: 'ID ของรายการ']
  "BillID" INT [not null, note: 'ID ของใบแจ้งหนี้ที่เป็นเจ้าของรายการนี้']
  "Description" NVARCHAR(255) [not null, note: 'คำอธิบายรายการ เช่น "ค่าคอร์ท", "ค่าลูก", "ส่วนลดพิเศษ"']
  "Amount" DECIMAL(10,2) [not null, note: 'จำนวนเงิน (สามารถติดลบได้สำหรับส่วนลด)']
}

Table "Payments" {
  "PaymentID" INT [pk, increment, note: 'ID ของการชำระเงิน']
  "BillID" INT [not null, note: 'ID ของใบแจ้งหนี้ที่ชำระ']
  "PaymentMethod" TINYINT [not null, note: 'วิธีชำระเงิน: 1=เงินสด, 2=QR Code']
  "Amount" DECIMAL(10,2) [not null, note: 'จำนวนเงินที่ชำระ']
  "PaymentDate" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'วันเวลาที่ชำระเงิน']
  "ReceivedByUserID" INT [note: 'UserID ของผู้จัดที่รับเงิน']
}

Table "Notifications" {
  "NotificationID" INT [pk, increment, note: 'ID ของการแจ้งเตือน']
  "UserID" INT [not null, note: 'ID ของผู้รับ (ใครคือคนที่จะเห็น Noti นี้)']
  "Title" NVARCHAR(255) [not null, note: 'หัวข้อการแจ้งเตือน']
  "Message" NVARCHAR(MAX) [not null, note: 'รายละเอียดการแจ้งเตือน']
  "Type" NVARCHAR(50) [not null, note: 'ประเภท Noti เช่น "NewGame", "Payment", "GameStarted"']
  "ReferenceID" INT [note: 'ID อ้างอิง เช่น SessionID หรือ BillID เพื่อให้ App รู้ว่ากดแล้วต้องเด้งไปหน้าไหน']
  "IsRead" BIT [not null, default: 0, note: 'สถานะการอ่าน: 0=ยังไม่อ่าน, 1=อ่านแล้ว']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`, note: 'เวลาที่สร้าง Noti']
}

Table "UserBookmarkedSessions" {
  "UserId" INT [not null, note: 'ID ของผู้ใช้ที่ Bookmark']
  "SessionId" INT [not null, note: 'ID ของก๊วนที่ถูก Bookmark']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  
  Indexes {
    ("UserId", "SessionId") [pk]
  }
}

Table "UserFollows" {
  "FollowerId" INT [not null, note: 'ID ของผู้ใช้ที่กดติดตาม']
  "OrganizerId" INT [not null, note: 'ID ของผู้จัดที่ถูกติดตาม']
  "CreatedDate" DATETIME2 [not null, default: `GETUTCDATE()`]
  
  Indexes {
    ("FollowerId", "OrganizerId") [pk]
  }
}

Table "UserWallets" {
  "WalletID" int [pk, increment]
  "UserID" int [unique, not null]
  "Balance" decimal(10,2) [not null, default: 0]
  "CreatedDate" timestamp [not null, default: `now()`]
  "UpdatedDate" timestamp
}

Table "WalletTransactions" {
  "TransactionID" int [pk, increment]
  "WalletID" int [not null]
  "Amount" decimal(10,2) [not null]
  "TransactionType" smallint [not null, note: '1 = IN (Refund), 2 = OUT (Payment)']
  "Description" varchar(255)
  "ReferenceID" int [note: 'SessionID หรือ BillID']
  "CreatedDate" timestamp [not null, default: `now()`]
}

Table "UserFcmTokens" {
  "TokenID" int [pk, increment]
  "UserID" int [not null]
  "Token" text [not null, unique]
  "DeviceName" varchar(255)
  "CreatedDate" timestamp [not null, default: `now()`]
  "UpdatedDate" timestamp [not null, default: `now()`]
  
  Indexes {
    "UserID" [name: "IX_UserFcmTokens_UserID"]
  }
}

// เพิ่มในส่วน Relationships
Ref: "Users"."UserID" < "UserFollows"."FollowerId"
Ref: "Users"."UserID" < "UserFollows"."OrganizerId"



// =============================================================================
// SECTION 4: RELATIONSHIPS
// =============================================================================
// Master Data Relationships
Ref: "ShuttlecockBrands"."BrandID" < "ShuttlecockModels"."BrandID"
Ref: "Banks"."BankID" < "OrganizerProfiles"."BankID"

// User Relationships
Ref: "Users"."UserID" - "UserProfiles"."UserID"
Ref: "Users"."UserID" - "OrganizerProfiles"."UserID"
Ref: "Users"."UserID" < "UserLogins"."UserID"
Ref: "Users"."UserID" < "OrganizerSkillLevels"."OrganizerUserID"

  // User Skill Persistence Relationships
Ref: "Users"."UserID" < "UserOrganizerSkills"."UserID" 
Ref: "Users"."UserID" < "UserOrganizerSkills"."OrganizerUserID" 
Ref: "OrganizerSkillLevels"."SkillLevelID" < "UserOrganizerSkills"."SkillLevelID"

// Game & Booking Relationships
Ref: "Venues"."VenueID" < "GameSessions"."VenueID"
Ref: "Users"."UserID" < "GameSessions"."CreatedByUserID"
Ref: "GameTypes"."GameTypeID" < "GameSessions"."GameTypeID"
Ref: "PairingMethods"."PairingMethodID" < "GameSessions"."PairingMethodID"
Ref: "ShuttlecockModels"."ModelID" < "GameSessions"."ShuttlecockModelID"
Ref: "GameSessions"."SessionID" < "SessionParticipants"."SessionID"
Ref: "Users"."UserID" < "SessionParticipants"."UserID"
Ref: "OrganizerSkillLevels"."SkillLevelID" < "SessionParticipants"."SkillLevelID"
Ref: "GameSessions"."SessionID" < "GameSessionFacilities"."SessionID"
Ref: "Facilities"."FacilityID" < "GameSessionFacilities"."FacilityID"
Ref: "GameSessions"."SessionID" < "GameSessionPhotos"."SessionID"
Ref: "GameSessions"."SessionID" < "SessionWalkinGuests"."SessionID"
Ref: "OrganizerSkillLevels"."SkillLevelID" < "SessionWalkinGuests"."SkillLevelID"
Ref: "GameSessions"."SessionID" < "Matches"."SessionID"
Ref: "Matches"."MatchID" < "MatchPlayers"."MatchID"
Ref: "Users"."UserID" < "MatchPlayers"."UserID"
Ref: "SessionWalkinGuests"."WalkinID" < "MatchPlayers"."WalkinID"
Ref: "GameSessions"."SessionID" < "ParticipantBills"."SessionID"
Ref: "Users"."UserID" < "ParticipantBills"."UserID"
Ref: "SessionWalkinGuests"."WalkinID" < "ParticipantBills"."WalkinID"
Ref: "ParticipantBills"."BillID" < "BillLineItems"."BillID"
Ref: "ParticipantBills"."BillID" < "Payments"."BillID"
Ref: "Users"."UserID" < "Notifications"."UserID"
Ref: "Users"."UserID" < "UserBookmarkedSessions"."UserId"
Ref: "GameSessions"."SessionID" < "UserBookmarkedSessions"."SessionId"

Ref: "UserWallets"."UserID" - "Users"."UserID"
Ref: "WalletTransactions"."WalletID" > "UserWallets"."WalletID"
Ref: "Users"."UserID" < "UserFcmTokens"."UserID"

```

## 6. Coding Standards & Conventions
- **Backend:** 
  - ใช้ Dependency Injection (`builder.Services.AddScoped`)
  - Return ด้วย Record/DTO เสมอ
  - ห่อ Response ด้วยคลาส `Response<T> { Status, Message, Data }` เสมอ
  - เช็ค Concurrency ด้วย `.AsSplitQuery()` เมื่อมีการ Include หลายตาราง
- **Frontend (Flutter):**
  - เชื่อมต่อ API ผ่าน `ApiProvider`
  - ไม่คำนวณตัวเลขทางการเงินเอง (ยึดตามที่ API ส่งมา 100%)
  - ใช้ Optimistic UI (อัปเดตหน้าจอก่อนยิง API) ในหน้า Live State เพื่อให้ลื่นไหล
  - **Responsive Design Strategy:** ใช้วิธีแบบผสมผสาน (Hybrid) 
    1. โครงสร้าง (Layout): ใช้ `LayoutBuilder` แบ่งคอลัมน์สำหรับ Tablet (iPad) และจัดเรียงแนวตั้งสำหรับ Mobile
    2. ขนาดฟอนต์ (Scaling): ใช้ฟังก์ชัน `getResponsiveFontSize` แบบ Native แต่บังคับใช้ `.clamp()` เพื่อจำกัดไม่ให้ฟอนต์ขยายใหญ่เกินขีดจำกัดบนหน้าจอแท็บเล็ต

## 7. Current Progress & Next Steps (สถานะปัจจุบัน)
- **ล่าสุด:** 
  - ปรับปรุง Routing ให้รองรับการทำงานแบบ Guest (สอดคล้องกับกฎของ Apple) และทำระบบ Token Refresh แบบมี Lock
  - อัปเดตหน้า Dashboard ฝั่งผู้เล่น (`HomeUserPage`) และฝั่งผู้จัด (`profile_organizer.dart`) ให้แสดงยอดเงินในกระเป๋า (Wallet), ยอดค้างชำระ, และสถิติที่ครบถ้วน
  - เคลียร์ Mock Data ในหน้า `finance.dart` ฝั่งมือถือ และเชื่อมต่อ API จริงสำหรับดูยอดรายได้/ประวัติการเงินของผู้จัดสำเร็จ 100%
  - เชื่อมโยงระบบ Tap Notification ให้เปิดแอปและ Route ไปยังหน้ารายละเอียดก๊วนได้ถูกต้องไม่ว่าจะเปิดหรือปิดแอปอยู่
  - อุดช่องโหว่บั๊กในระบบ "สร้างก๊วนใหม่" (Validation เวลาเริ่ม-จบ, ซ่อนบั๊ก Google Places, ป้องกัน Foreign Key Error)
  - ปรับปรุง Layout หน้า Profile ให้รองรับมือถือจอเล็กด้วย `SingleChildScrollView` (แก้ปัญหาแถบเหลืองดำล้นจอ)
  - ปรับปรุงระบบแอปบาร์ (AppBar) และปุ่ม Back ให้ตรวจสอบประวัติหน้าจอก่อน (`context.canPop()`) เพื่อป้องกันปัญหาแอปจอดำเมื่อผู้ใช้เผลอกด Back หลังถูก Redirect
  - อัปเดตหน้าชำระเงินก่อนจองก๊วน (`payment.dart`) โดยถอดฟอร์มบัตรเครดิตออก ให้เหลือเฉพาะการสแกน QR Code (PromptPay) และ Wallet เพื่อหลีกเลี่ยงค่าธรรมเนียม Payment Gateway ที่สูงเกินไป
  - แก้ไขบั๊ก GoRouter ทำงานซ้อนทับกัน (Exception `!_debugLocked`) ตอนกดยืนยัน OTP และจังหวะที่ Token หมดอายุ
  - เชื่อมต่อระบบชำระเงิน Xendit แบบ E2E: สร้าง Dynamic QR Code จาก Backend ส่งให้แอปแสดงผลในรูปแบบ Popup Dialog
  - เพิ่มฟีเจอร์บันทึกรูปภาพ QR Code ลงเครื่อง (Gallery) ด้วย `image_gallery_saver`
  - ปรับปรุง Flow การจอง: หากผู้เล่นกดยกเลิกหรือปิดหน้าต่าง QR โดยยังไม่จ่ายเงิน ระบบจะยิง API คืนที่นั่ง (Cancel Booking) อัตโนมัติทันที
  - ปรับปรุง Xendit Webhook ให้ยืดหยุ่นขึ้น รองรับ Payload ทั้งแบบ QR Code และ Invoices ป้องกันปัญหา 400 Bad Request
  - อธิบายและเปิดใช้งาน Xendit xenPlatform (Sub-accounts) แบบ Owned Account เพื่อให้ผู้จัดรับเงินเข้ากระเป๋าตัวเองได้โดยตรง
  - ผูกระบบ API สร้างบัญชีย่อย (Sub-account) บน Xendit อัตโนมัติเมื่อผู้ใช้กดสมัครเป็นผู้จัด (Apply Organizer)
  - ทดสอบระบบ Xendit Simulate Payment (E2E) และตั้งค่า Webhook บนเซิร์ฟเวอร์จริงสำเร็จ
  - แก้ไขบั๊กหน้าต่าง QR Code ไม่ปิดอัตโนมัติ โดยปรับแก้การยิง SignalR ฝั่ง Backend ให้ระบุเป้าหมายรายบุคคล (User/Group) และเพิ่ม Listener `QrPaymentSuccess` ในฝั่งแอป Flutter
  - อัปเดต API `player/gamesessions/my` ฝั่ง Backend ให้เรียงลำดับก๊วน "กำลังมาถึง" แบบ Ascending (ใกล้ถึงที่สุดขึ้นก่อน)
  - วางระบบแจ้งเตือน Firebase Cloud Messaging (FCM) แบบสมบูรณ์ รองรับการทำงานทั้งตอนเปิดแอป (Foreground) และปิดแอป (Background/Terminated)
  - เพิ่มระบบ Notification Badge (ป้ายแจ้งเตือนสีแดง) ผ่าน `NotificationProvider` เพื่อให้แสดงจำนวนที่ยังไม่อ่านบน AppBar แบบ Real-time
  - เพิ่มการยิง Push Notification ฝั่งผู้จัดในเหตุการณ์: สร้างก๊วนใหม่ (แจ้งผู้ติดตาม), เปิดก๊วน/เริ่มการแข่งขัน, และจบการแข่งขัน
  - แก้ไขปัญหา "เริ่มเกมไม่สำเร็จ (Timeout)" ฝั่งผู้จัด โดยขยายเวลา Timeout ใน `api_provider.dart` เป็น 30 วินาที และดักจับ Timeout Error เพื่อทำ Optimistic UI อัปเดตข้อมูลเงียบๆ ไม่ให้รบกวนการจัดทีม
  - ปรับปรุง Backend ระบบ Notification ให้เป็นแบบ Fire-and-forget (ทำงานเบื้องหลังด้วย `Task.Run` และ `IServiceScopeFactory`) เพื่อให้ API ตอบสนองไวปานสายฟ้า ไม่ถูกบล็อกจากการยิงแจ้งเตือนทีละหลายคน
  - แก้ไขบั๊กหน้า "เกมส์ของฉัน" (ฝั่งผู้เล่น): ป้องกันปุ่มคิวการเล่นหายไประหว่างเกมเมื่อผู้จัดเพิ่มค่าใช้จ่าย โดยจะเปลี่ยนสถานะเป็นค้างชำระ (Pending Payment) ก็ต่อเมื่อ Checkout แล้วเท่านั้น
  - ปรับปรุง Logic หน้าจองก๊วน (Booking Confirm): นำการคำนวณเวลาจากฝั่งหน้าบ้าน (Frontend) ออกทั้งหมด เพื่อให้ปุ่มเข้าสู่กระดานและปุ่มชำระเงินทำงานตามสถานะที่ได้รับจาก Backend 100% (Smart Backend, Dumb Frontend)
  - โละ Logic การตัดต่อ String วันที่/เวลา บนแอปทิ้ง (ในหน้า `MyGameUserPage`) และเชื่อถือค่า `SessionStart` ที่ถูกคำนวณมาจาก Backend 100%
  - อุดช่องโหว่เมื่อมีการ "ลบสนาม" ในหน้ากระดานผู้จัด Backend จะเป็นผู้รับผิดชอบเคลียร์คิวที่จัดค้างไว้ในคอร์ทที่โดนลบ (Orphaned Staged Matches) ทิ้ง และปล่อยผู้เล่นกลับสู่คิวรออัตโนมัติ เพื่อป้องกันฐานข้อมูลรกและ UI รวน
  - แก้ไขปัญหา Push Notification ไม่ทำงานบนเครื่องจริง โดยการเพิ่ม Logic การส่ง FCM Token ไปยัง Backend ในจังหวะ Auto-Login (ตอน Refresh Token) เพื่อให้แน่ใจว่า Backend มี "ที่อยู่" สำหรับส่ง Noti เสมอ
  - เพิ่ม Priority ใน Payload ของ FCM ฝั่ง Backend เพื่อบังคับให้ Noti เด้งทันที ทะลุโหมดประหยัดพลังงาน (Doze Mode) ของ Android
  - แก้ไขบั๊กการคำนวณยอดเงินในหน้า "ประวัติการจัดก๊วน" และ "ดูรายงาน" ของผู้จัด โดยป้องกันการนำบิลค้างชำระ (Status = 1) มาบวกซ้ำกับบิลที่จ่ายแล้ว (Status = 2) เพื่อให้ยอดรวมถูกต้อง
  - **[Completed]** แก้ไขโครงสร้างและ Logic ของตาราง `UserLogins` ฝั่ง Backend ที่ใช้เก็บ FCM Token โดยแยกออกมาเป็นตาราง `UserFcmTokens` รองรับ 1-to-N
  - **[Completed]** ปรับปรุงหน้า "จัดการรายชื่อ (Roster)" ของผู้จัด ให้แสดงสถานะผู้เล่นที่ "Check-out (กลับบ้านแล้ว)" ได้อย่างชัดเจน พร้อมย้าย Logic ตรวจสอบเวลาไปยัง Backend
  - **[Completed]** ปิดการทำงานของ Auto Backup ใน Android (`AndroidManifest.xml`) เพื่อแก้ปัญหา Token ล็อกอินเก่าค้างอยู่ในเครื่องหลังจากลบและติดตั้งแอปใหม่
  - **[Completed]** ปรับปรุงหน้า UI การแจ้งเตือน (Notification) ให้แสดง Icon และสีที่แตกต่างกันตามประเภทของ Noti เพื่อความสวยงามและแยกแยะง่ายขึ้น
  - **[Completed]** แก้ไขปัญหา Entity Framework (EF Core) ฝั่ง Backend แครชจากการดึงข้อมูล (Cartesian Explosion) ในหน้า Manage โดยดึงข้อมูลดิบลง Memory ก่อนทำ DTO Projection
  - **[Completed]** แก้ไขบั๊กหน้า "การเงิน (Finance Dashboard)" ฝั่งแอปให้เข้ากันได้กับ `fl_chart` เวอร์ชันใหม่ และปรับปรุงตัวแปร State ป้องกันแอปแครชตอนเปิดดูกราฟ
  - **[Completed]** ปรับปรุง UX หน้าชำระเงิน (QR Code และ Wallet) ให้เด้งเปลี่ยนหน้าอัตโนมัติ (Auto-redirect) เมื่อได้รับ Webhook สำเร็จ ไม่ต้องรอให้ผู้ใช้กดปุ่มยืนยันซ้ำซ้อน
  - **[Completed]** ปรับปรุง Logic การคืนเงิน (Refund) เมื่อผู้เล่นกดยกเลิกก๊วนเอง ระบบจะหักค่าธรรมเนียมแพลตฟอร์ม (Service Fee) ออกก่อนคืนเงินเข้า Wallet ผู้เล่นให้ถูกต้องตามนโยบาย
  - **[Completed]** ย้าย Logic การกรองวันที่ (Filter) ในหน้าการเงิน (Finance) จากหน้าบ้าน (Frontend) ไปจัดการที่หลังบ้าน (Backend) ผ่าน Entity Framework (Smart Backend)
  - **[Completed]** เพิ่มการยิง SignalR `PlayerCheckedOut` จากฝั่งผู้จัดไปยังฝั่งผู้เล่นเมื่อรับเงินสดสำเร็จ เพื่อให้แอปผู้เล่นเตรียมพร้อมเด้งแจ้งเตือนและเปลี่ยนหน้าอัตโนมัติ
  - **[Completed]** ปรับปรุง UX หน้าเช็คบิลผู้เล่น (ExpensePanel) ซิงค์ข้อมูลให้โหลดบิลและยอดรวมใหม่ทันทีเมื่อผู้จัดกดยกเลิกสแกน QR
  - **[Completed]** เพิ่ม `WidgetsBindingObserver` ในหน้ากระดานผู้เล่น เพื่อบังคับให้แอปเชื่อมต่อ SignalR และโหลดข้อมูลใหม่ทันทีเมื่อผู้ใช้ปลุกแอปจากพื้นหลัง (Background/Sleep)
  - **[Completed]** แก้ไขหน้า `ExpensePanel` ในหน้าควบคุมกระดาน (ManageGame) ให้ดึงข้อมูลบิลและยอดค้างชำระใหม่ทันทีเมื่อผู้จัดปิดหน้าต่าง QR Code
  - **[Completed]** แก้ไขบั๊กยอดหนี้ซ้ำซ้อน บังคับยกเลิกบิลค้างชำระใบเก่า (Status = 3) เสมอเมื่อออกบิลใบใหม่ เพื่อไม่ให้หน้าประวัติผู้เล่นแสดงสถานะค้างชำระผิดพลาด
  - **[Completed]** เพิ่ม `WidgetsBindingObserver` ในหน้ากระดานผู้จัด (`manage_game.dart`) เพื่อให้ SignalR ต่ออัตโนมัติและรีเฟรชข้อมูลป้องกัน State ค้าง (Stale Data)
  - **[Completed]** แก้ไขปัญหากดปุ่มย้อนกลับ (Back) แล้วข้อมูลหน้าจอเป็นของเก่า โดยเพิ่มการดักคำสั่ง `.then(...)` เพื่อบังคับโหลดข้อมูลใหม่ให้เป็นปัจจุบัน 100%
  - **[Technical Debt]** `GameSessionService.cs` เป็น God Object (~2,400 บรรทัด) มีการรวม Logic ของฝั่งผู้จัดและผู้เล่นไว้ด้วยกัน (เช่น `JoinSession`, `CancelBooking`) และอาจมี Logic ทับซ้อนกับ `MatchManagementService.cs` ตัดสินใจชะลอการ Refactor ไว้ก่อนเพื่อรักษาความเสถียร
- **สิ่งที่ต้องทำต่อ:**
  1. ทำระบบ Social Login (Google / Apple / LINE) ด้วยการ Verify Token ควบคู่กับการยืนยันเบอร์โทรศัพท์ (OTP)
  2. ทำระบบแชร์ก๊วน (Share / Deep Linking) เพื่อให้ผู้จัดส่งลิงก์ชวนเพื่อนทาง Social Media / LINE ได้
  3. เตรียมความพร้อมแอปพลิเคชันก่อนขึ้น Store (App Icon, Splash Screen, Permissions)
  4. Audit UX/UI และจัดการ Responsive Design (Font Size, Layout) ให้แสดงผลได้สวยงามสม่ำเสมอในทุกขนาดหน้าจอ (Mobile/Tablet)

## 8. Project Directory Structure (กฎการวางไฟล์สำหรับ AI)
เพื่อรักษามาตรฐานสถาปัตยกรรมของโปรเจกต์ ให้ AI อ้างอิงการสร้างหรือแก้ไขไฟล์ตามโครงสร้างนี้:

### 8.1 Backend (.NET 8 API) - Modular Architecture
- `DropInBadAPI/Models/` -> เก็บ Entity Classes ทั้งหมด (Database Schema)
- `DropInBadAPI/Data/` -> เก็บ `BadmintonDbContext.cs` (EF Core)
- `DropInBadAPI/Modules/` -> เก็บ Logic แบ่งตาม Domain (Feature Folders):
  - `/Auth/` -> ระบบ Login, JWT, OTP
  - `/Master/` -> ข้อมูลพื้นฐาน (Banks, Facilities, ShuttlecockBrands)
  - `/MobileOrganizer/` -> API สำหรับแอปฝั่งผู้จัด (แยกย่อยเป็น Game, MatchManagement, Dashboard)
  - `/MobilePlayer/` -> API สำหรับแอปฝั่งผู้เล่น (จองก๊วน, ประวัติ, Wallet)
  - `/Notification/` -> บริการส่ง FCM / Notification
  - `/Shared/` -> DTOs กลางที่ใช้ร่วมกันหลาย Module
  - `/Webhooks/` -> ตัวรับข้อมูลจากภายนอก (Xendit)

### 8.2 Frontend (Flutter) - Feature-based
- `lib/component/` -> Reusable UI Widgets (ปุ่ม, การ์ด, Dialogs)
- `lib/model/` -> Data Models / Classes
- `lib/page/` -> หน้าจอแอปพลิเคชัน แบ่งตาม Role:
  - `/auth/` -> หน้า Login, Register, OTP
  - `/organizer/` -> หน้าสำหรับผู้จัด (สร้างก๊วน, จัดการบอร์ด, ประวัติ, โปรไฟล์)
  - `/user/` -> หน้าสำหรับผู้เล่น (ค้นหาก๊วน, จ่ายเงิน, กระเป๋าเงิน)
- `lib/shared/` -> Core Logic, API Provider, State Management (Providers)
- `lib/widget/` -> Custom Widgets เฉพาะทาง

<!-- ตัวอย่างการแจ้งแก้ UI ให้ผม
เครื่องที่เทส: iPad Mini 5 (หรือ iPhone SE, Galaxy S23) หน้าจอ: จัดการก๊วน manage_game.dart ปัญหาที่เจอ: ในการ์ดสนามตรงปุ่ม Pause ไอคอนมันเล็กเกินไป และชื่อผู้เล่นในช่องมันยาวจนตกบรรทัดไปทับขอบการ์ด -->