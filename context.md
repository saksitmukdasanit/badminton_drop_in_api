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
- **Service:** `IAutoMatchService` (แยกออกจาก `GameSessionService`) ครอบ 4 use case คือ `AutoMatchAsync`, `SwapPlayersAsync`, `AssignReserveToCourtAsync`, `MovePlayersAsync` พร้อม `BroadcastLiveStateChange` (SignalR push)
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
- **Per-Organizer Preset (`OrganizerAutoMatchPresets`):** น้ำหนัก 7 ค่าของ Auto Match (Queue / History / Mixed-Opp / Mixed-TM / Same-Level / Team-TM / Team-Opp) เก็บแยกตามผู้จัดบน server ผ่าน `IAutoMatchPresetService` (endpoint `GET/PUT/DELETE /api/organizer/auto-match-preset`); ฝั่งแอปดึง preset เข้ามาเป็นค่าตั้งต้น แล้วเก็บ cache ลง `SharedPreferences` กัน UI กระตุก/รองรับ offline

### 3.6 Skill Level Management (การจัดการระดับมือ)
- **Default Levels:** หากผู้จัดสมัครใหม่ ระบบจะสร้าง 4 ระดับให้เป็นค่าเริ่มต้นอัตโนมัติ (มือใหม่, มือเบา, มือกลาง, มือหนัก)
- **Soft Delete:** การซ่อน/ลบระดับมือโดยผู้จัด จะแค่ปรับ `IsActive = false` เพื่อไม่ให้กระทบประวัติการเล่นเก่า ส่วน UI ฝั่งแอปจะป้องกันแอปแครชโดยรีเซ็ตค่าเป็นว่างอัตโนมัติหากหาไม่เจอ
- **Global Tracking:** ระดับมือของผู้เล่นจะผูกติดกับผู้จัดแต่ละคน (`UserOrganizerSkills`) เมื่อผู้เล่นจองก๊วนใหม่กับผู้จัดเดิม ระบบจะดึงระดับมือล่าสุดมาแสดงให้อัตโนมัติ

### 3.7 Authentication & Routing (การยืนยันตัวตนและการนำทาง)
- **Apple App Store Guideline Compliance:** อนุญาตให้ผู้ใช้ทั่วไป (Guest) สามารถเข้าดูหน้า Home และหน้ารายละเอียดก๊วนได้โดยไม่ต้อง Login จะถูกบังคับ Login ก็ต่อเมื่อกระทำการสำคัญ (เช่น จองก๊วน, ชำระเงิน). หน้า `/privacy-policy`, `/terms`, `/about` เป็น public pages เพื่อให้ผู้ใช้อ่านได้ก่อน sign up
- **Account Deletion (Apple 5.1.1(v)):** Soft-delete + 30-day grace period — `User.DeletedAt` (timestamp), `IsActive=false`, invalidate refresh tokens. ปฏิเสธการลบหากยังมี Wallet balance / active session / upcoming joined session. SQL: `Sql/2026_05_06_AddUserDeletedAt.sql`. Endpoints: `POST /api/auth/request-deletion`, `POST /api/auth/cancel-deletion`. Frontend: `profile_user.dart` แสดง confirm dialog → call API → logout
- **Report User + Block User (Apple 1.2 / 5.1.1(viii)):** ตาราง `UserReports` (reasons: spam/harassment/fraud/fake_profile/inappropriate_content/other) + `UserBlocks` (UNIQUE blocker+blocked, no self-block). SQL: `Sql/2026_05_06_AddUserReportsAndBlocks.sql`. Module: `Modules/UserSafety/`. Endpoints: `/api/user-safety/{report,block,blocks}` — ปุ่ม "More" (PopupMenu) ใน `UserProfileDialog` มีตัวเลือก "รายงาน" + "บล็อก"
- **Privacy Policy / Terms of Service / About:** หน้า `/privacy-policy`, `/terms`, `/about` ใน `lib/page/legal/legal_pages.dart` (เนื้อหาเป็น **TEMPLATE** ที่ต้องตรวจกับ legal team ก่อนปล่อย Store). ลิงก์ถูกฝังใน checkbox "ยอมรับข้อกำหนด" ของ `personal_info_screen.dart` + เมนู profile ของ player/organizer
- **App Tracking Transparency (iOS 14.5+):** `NSUserTrackingUsageDescription` ใส่ใน `Info.plist` แล้ว (scaffold เผื่ออนาคต). ปัจจุบันแอปไม่มี cross-app tracking SDK จึงยังไม่ trigger ATT prompt — เปิดใช้เมื่อมี ad SDK / Firebase Analytics ที่เปิด Personalized Ads
- **Password Hashing:** ใช้ BCrypt (`BCrypt.Net-Next`) ผ่าน `IPasswordHasher` (work factor 11) พร้อม **lazy upgrade** — ถ้าตรวจเจอ legacy hash รูปแบบ `hashed_<password>` ใน DB จะยอมเทียบแบบเดิมเพื่อให้ผู้ใช้เก่า login ผ่าน แล้วทับด้วย BCrypt อัตโนมัติทันที (ไม่ต้องบังคับ reset password ทุกคน)
- **Social Login (Phase 1 — Google + Apple):**
  - DB schema เดิม `UserLogins.ProviderName/ProviderKey` รองรับ multi-provider อยู่แล้ว (Local / Google / Apple / SMSMKT)
  - Backend: `IGoogleTokenVerifier` (verify ผ่าน `Google.Apis.Auth`) + `IAppleTokenVerifier` (verify JWT กับ Apple JWKS endpoint, validate `iss=https://appleid.apple.com` + `aud=BundleId/ServiceId`); `AuthService.LoginWithGoogleAsync` / `LoginWithAppleAsync` — ถ้าเจอ ProviderKey เดิม → ออก token; ถ้าไม่เจอ → สร้าง User+Profile+UserLogin ใหม่และ flag `RequiresPhoneVerification=true`
  - **ไม่ auto-link ด้วย email** เพราะ DB เดิมไม่ verify email — ผู้ใช้ที่อยากผูก social กับบัญชีเก่าจะทำใน Settings → Linked Accounts (Phase 2 ในอนาคต)
  - `POST /api/auth/login-google` + `POST /api/auth/login-apple` (anonymous) คืน `SocialLoginResponseDto { accessToken, refreshToken, requiresPhoneVerification, phoneNumber }`
  - `POST /api/auth/link-phone` (authorized) — สำหรับ user ที่ login ผ่าน social ครั้งแรก: บันทึกเบอร์ลง UserProfile + ส่ง OTP ผ่าน SMSMKT
  - Frontend: `lib/shared/social_login_service.dart` ห่อ `google_sign_in: ^7.2.0` + `sign_in_with_apple: ^7.0.1`; `login_screen.dart` ปุ่ม Google + Apple (Facebook ลบไปก่อน Phase 3); หน้าใหม่ `/social-phone-link` (`SocialPhoneLinkScreen`) สำหรับ flow first-time signup
  - Setup keys: `docs/SOCIAL_LOGIN_SETUP.md`
- **Login Race Fix:** `AuthProvider.login()` เขียน `accessToken`/`refreshToken` ลง `SharedPreferences` ให้เสร็จก่อน `notifyListeners()` และ `LoginScreen` `await login()` ก่อน `context.go('/')` กันอาการ "ติดตั้งใหม่ login แล้วข้อมูลไม่ขึ้น" (Dio interceptor อ่าน token ทัน)
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

// Per-organizer preset สำหรับ Auto Match scoring (ยกจาก SharedPreferences เป็น server-side, sync ข้ามเครื่อง)
Table "OrganizerAutoMatchPresets" {
  "UserID" int [pk, note: 'PK = UserID ของผู้จัด (1-to-1)']
  "QueuePositionMultiplier" int [not null, default: 10]
  "MatchTogetherPenaltyPerOccurrence" int [not null, default: 40]
  "MixedModeOppositeSkillMultiplier" int [not null, default: 15]
  "MixedModeTeammateSkillMultiplier" int [not null, default: 20]
  "SameLevelSkillMultiplier" int [not null, default: 30]
  "TeamFormationTeammateHistoryMultiplier" int [not null, default: 2]
  "TeamFormationOpponentHistoryMultiplier" int [not null, default: 1]
  "CreatedDate" timestamp [not null, default: `now()`]
  "UpdatedDate" timestamp
}

// เพิ่มในส่วน Relationships
Ref: "Users"."UserID" < "UserFollows"."FollowerId"
Ref: "Users"."UserID" < "UserFollows"."OrganizerId"
Ref: "Users"."UserID" - "OrganizerAutoMatchPresets"."UserID"



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

## 7. Project Directory Structure (กฎการวางไฟล์สำหรับ AI)
เพื่อรักษามาตรฐานสถาปัตยกรรมของโปรเจกต์ ให้ AI อ้างอิงการสร้างหรือแก้ไขไฟล์ตามโครงสร้างนี้:

### 7.1 Backend (.NET 8 API) - Modular Architecture
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

### 7.2 Frontend (Flutter) - Feature-based
- `lib/component/` -> Reusable UI Widgets (ปุ่ม, การ์ด, Dialogs)
- `lib/model/` -> Data Models / Classes
- `lib/page/` -> หน้าจอแอปพลิเคชัน แบ่งตาม Role:
  - `/auth/` -> หน้า Login, Register, OTP
  - `/organizer/` -> หน้าสำหรับผู้จัด (สร้างก๊วน, จัดการบอร์ด, ประวัติ, โปรไฟล์)
  - `/user/` -> หน้าสำหรับผู้เล่น (ค้นหาก๊วน, จ่ายเงิน, กระเป๋าเงิน)
- `lib/shared/` -> Core Logic, API Provider, State Management (Providers)
- `lib/widget/` -> Custom Widgets เฉพาะทาง

## 8. System Status & Completed Features (สถานะระบบปัจจุบัน)

**[Authentication & Security]**
- รองรับ Guest Mode (Apple Guideline) และ Token Refresh Locking แบบ Rolling
- แยกตาราง `UserFcmTokens` รองรับ 1-to-N Devices
- Fire-and-forget FCM Token Update ป้องกันหน้า Login หมุนค้าง
- ปิด Auto Backup ใน Android ป้องกัน Token เก่าค้างเมื่อลงแอปใหม่

**[Game & Live State (SignalR)]**
- ระบบกระดาน Live State ลื่นไหลด้วย Optimistic UI ทั้งผู้เล่นและผู้จัด
- จัดการเวลาแบบ Smart Backend (อิงเวลาเซิร์ฟเวอร์เพื่อความแม่นยำ)
- UI รองรับการย่อ Card อัตโนมัติเมื่อลงสนาม และมีสัญลักษณ์ Walk-in ชัดเจน
- มี `ReadOnlyCourtTimerWidget` ในแอปผู้เล่นเพื่อนับเวลาแบบ Real-time
- `WidgetsBindingObserver` บังคับ SignalR ต่อใหม่เสมอเมื่อเปิดแอปจาก Background (ป้องกันข้อมูล Stale)

**[Payment, Wallet & Xendit]**
- เชื่อมต่อ Xendit Dynamic QR Code และ Sub-account แบบ E2E
- ระบบชำระเงินด้วย Wallet และกลไก Negative Balance (ยอดหนี้สินผู้จัด)
- **Race Condition Protection:** ป้องกันลูกค้ากดยกเลิกพร้อมจังหวะ Webhook เข้า; `WalletService.WithdrawAsync` ใช้ atomic `ExecuteUpdate WHERE Balance >= amount` + compensating transaction (คืนยอด) เมื่อ Xendit Payout ล้มเหลว — ไม่ต้องล็อก transaction ยาวระหว่างเรียก external API
- **Financial Leak Protection:** หักค่าธรรมเนียมผู้จัด 10 บาทเป็นหนี้เสมอเมื่อรับเงินสด/โอนตรง/สแกนเข้าบัญชีตัวเอง
- **Refund Calculation Fix:** `CancelSessionByOrganizerAsync` Include `BillLineItems` ครบ ให้แยก service fee จาก court+shuttle fee ตอนคำนวณยอดดึงคืนจาก Wallet ผู้จัด (เดิมไม่ Include → service fee = 0 → ดึงคืนเกินจริง)
- **Webhook Hardening:** `XenditWebhookController` reject ทุก request ทันทีเมื่อ `Xendit:WebhookVerificationToken` ใน config ว่าง (กัน production deploy ลืมตั้งค่า → spoof endpoint)
- นโยบายคืนเงิน: คืนเต็มจำนวน (รวม Service Fee) เมื่อยกเลิกก๊วน แพลตฟอร์มเป็นผู้รับผิดชอบค่าธรรมเนียม
- UI อัปเดตยอดเงินทันทีที่ Webhook ทำงานสำเร็จ (Auto-redirect) โดยไม่ต้องกดยืนยัน
- ผู้จัดกด Checkout เงินสดปุ๊บ ผู้เล่นถูกดึงออกจากบอร์ดกลับไปหน้าประวัติทันทีด้วย SignalR

**[Booking & Concurrency]**
- `PromoteWaitlistedParticipantAsync` ห่อด้วย `Serializable` transaction + re-check capacity → กัน 2 คำขอ promote พร้อมกันเลื่อนคนเกิน `MaxParticipants`; ส่ง notification หลัง commit ป้องกันแจ้งผิดเมื่อ rollback
- `ParticipantDtoMapper` มี fallback `?.LevelName ?? "ยังไม่กำหนด"` + สี `#9E9E9E` กัน NPE ตอนเจอ skill level ที่ถูก soft-delete

**[Responsive UI / UX]**
- `manage_game.dart` ใช้ `LayoutBuilder` แบ่ง 2 คอลัมน์เมื่อกว้าง ≥ 900px (สนาม | คิวผู้เล่น) — ผู้จัดบนแท็บเล็บใช้พื้นที่จอเต็มประสิทธิภาพ
- ปุ่ม play/pause กลางสนาม tap target ≥ 48×48 ตาม Material guideline
- `AutoMatchSettingsSheet` constrain `maxWidth: 600` กัน sheet แผ่เต็มจอบนแท็บเล็บ
- หน้า auth (welcome / login / register / personal_info / otp) + home (player/organizer) ผ่าน `getResponsiveFontSize` ทั้งหมด — ขจัด fontSize hardcoded ที่ใหญ่เกินบนแท็บเล็บ
- Skeleton loaders (`HomeDashboardSkeleton`) ใน home; `SessionCardListSkeleton` / `WalletPageSkeleton` / `MyGamesPageSkeleton` ในหน้า หาก๊วน (`search_user`), ประวัติ (`history_user`), เกมของฉัน (`mygame_user`), กระเป๋าเงิน (`my_wallet_page`) — ลด perceived loading time
- `appRouteObserver` (global `RouteObserver`) ลงทะเบียนกับ `GoRouter`; home pages mixin `RouteAware` + `WidgetsBindingObserver` → refresh ตอน app resume + ตอน popNext กลับมาจากหน้าอื่น
- `CustomElevatedButton` semantics ของ `enabled` ถูกแก้ให้ตรง convention (`enabled=true` = กดได้) + default `enabled = true` (เดิมกลับด้าน → caller บางที่ทำงานด้วย bug บังเอิญ)

**[Push Notifications]**
- แจ้งเตือนครบทุกเหตุการณ์: สร้างก๊วน, ยกเลิก, เริ่มเกม, จบเกม, เช็คอิน, จ่ายเงิน
- ระบบแจ้งเตือนทะลุ Doze Mode (High Priority)
- กด Noti เพื่อ Route ไปหน้าก๊วนถูกต้อง (มีการยิง API เช็คสถานะก่อนเปลี่ยนหน้า ป้องกันเด้งเข้าสนามที่ยังไม่เริ่ม)

## 9. Technical Debt & Known Issues
- ~~`GameSessionService.cs` เป็น God Object (~2,400 บรรทัด)~~ → **แยกครบแล้ว** เป็น `GameSessionBookingService` (เพิ่ม Walk-in / ลบ / เลื่อนคิว), `GameSessionBillingService` (สรุปการเงิน), `IAutoMatchService` (AutoMatch + Swap + AssignReserve + MovePlayers + BroadcastLiveStateChange) และ `ParticipantDtoMapper` (รวม mapping ของ Member/Guest); `GameSessionService` ลดเหลือ ~1,100 บรรทัดและทำหน้าที่เป็น façade ผ่าน `IGameSessionService` (controller ไม่ต้องแก้)
- ~~Auto Match Scoring + Weight Preset~~ → **ครบทั้ง stack** (server-side preset):
  - **Backend:** `AutoMatchScoringWeightsDto` (optional ใน `AutoMatchRequestDto.ScoringWeights`) + `OrganizerAutoMatchPresets` table + `IAutoMatchPresetService` + endpoint `GET/PUT/DELETE /api/organizer/auto-match-preset`. ทุกค่า clamp ≥ 0 และ default = ค่าเดิม (backward-compatible)
  - **Frontend (Flutter):** `lib/model/auto_match_scoring_weights.dart` (model + 3 presets: เน้นคิว / ไม่ซ้ำหน้า / ฝีมือสูสี + `loadFromServer/saveToServer/resetOnServer`) + `lib/component/auto_match_settings_sheet.dart` (bottom-sheet ปรับ slider 7 ค่า, constrain maxWidth 600). เปิดผ่านปุ่มเฟือง (`Icons.tune`) ใน `manage_game.dart`; โหลดจาก server แล้ว cache ลง `SharedPreferences` กัน UI กระตุก/รองรับ offline; ส่ง `scoringWeights` ใน body ของ `/auto-match` เฉพาะตอนผู้จัดปรับเอง (default → ไม่แนบ → backward-compatible)
  - **Migration:** `DropInBadAPI/Sql/2026_05_06_AddOrganizerAutoMatchPresets.sql` — รันก่อน deploy production
- ~~`ParticipantDtoMapper` NPE risk~~ → **ปลอดภัยแล้ว** ใช้ `?.LevelName ?? "ยังไม่กำหนด"` + fallback color `#9E9E9E`
- ~~Login race (ติดตั้งใหม่ → login → ข้อมูลไม่ขึ้น)~~ → **แก้แล้ว** เขียน prefs ก่อน notify + await login() ก่อน navigate
- ~~Refund leak ใน CancelSessionByOrganizerAsync~~ → **แก้แล้ว** ThenInclude `BillLineItems`
- ~~Password placeholder hash (`hashed_xxx`)~~ → **แก้แล้ว** ใช้ BCrypt + lazy upgrade
- ~~Xendit webhook ยอม pass ตอน config ว่าง~~ → **แก้แล้ว** reject ทันที
- ~~`personal_info_screen` ปุ่ม enabled สลับ + `CustomElevatedButton` semantics กลับด้าน~~ → **แก้แล้ว** ทั้งสองที่
- ~~Promote-waitlist ไม่มี transaction~~ → **แก้แล้ว** Serializable TX + re-check capacity
- ~~`participantType` literals~~ → **เรียบร้อย** ใช้ `ParticipantTypes.Member` / `Guest` และ helper `ParticipantTypes.IsMember` / `IsGuest` / `IsMemberOrGuest` ใน controller + billing + participant flows; payload JSON ถึงผู้เล่นยังเป็น `"Member"` / `"Guest"` เหมือนเดิม
- ~~`withOpacity` deprecated warning กระจายในหลายไฟล์ Flutter~~ → **กวาดครบใน `lib/`** แทนด้วย `Color.withValues(alpha:)` แล้ว
- ~~`MatchManagementService` nullable / โครงสร้าง~~ → **partial ครบ + กวาด nullable ในโฟลเดอร์นั้นแล้ว**; **backend `dotnet build` 0 warnings** (DTO หลักใช้ `#nullable disable warnings` เฉพาะไฟล์เพื่อตัด noise CS8618 โดยยังเก็บ `string?` ใน contract ได้)

## 10. Next Steps (Roadmap)
> หมายเหตุ: DB migration ทั้ง 3 ไฟล์ + `Xendit:WebhookVerificationToken` — **ทีมตั้งค่าใน environment แล้ว** (deploy ยังต้อง verify smoke test ตามรายการด้านล่าง)  
> **เพิ่ม:** รัน `Sql/2026_05_06_AddOrganizerRecurringGameTemplates.sql` สำหรับก๊วนประจำสัปดาห์ (ข้อ 7)

1. **Deploy & Migration Validation:**
   - ~~Apply SQL migration บน DB production (`OrganizerAutoMatchPresets`)~~ + `UserDeletedAt` + reports/blocks — **รันแล้วใน env ที่ใช้**
   - ~~ตั้ง `Xendit:WebhookVerificationToken` (กัน webhook spoof)~~ — **ตั้งแล้ว**
   - ทดสอบ flow login user เก่า → BCrypt lazy upgrade ทำงานถูก (smoke test ก่อน release)
2. **Social Login Phase 1 — ใส่ keys**: ดู `docs/SOCIAL_LOGIN_SETUP.md`
   - ✅ **Google:** `Auth:Google:{IosClientId, AndroidClientId, WebClientId}` + Flutter `googleServerClientId` / `googleIosClientId`; Android SHA-1 + `google-services.json` — **ทำแล้ว**
   - ⏳ **Apple:** ต้อง Apple Developer Program (จ่ายรายปี); จากนั้นตั้ง `Auth:Apple:*`, capability Xcode, และ (iOS) URL scheme Google ตามเอกสาร
3. ~~**Tier 1 — App Store Compliance**~~ ✅ **เสร็จแล้ว** (ทำในรอบนี้):
   - ✅ Account Deletion API จริง (Apple 5.1.1(v)) — `User.DeletedAt` + soft-delete + 30-day grace
   - ✅ Privacy Policy + Terms + About pages (เนื้อหาเป็น template ต้องอัปเดตจริงก่อนปล่อย Store)
   - ✅ Report User + Block User (Apple 1.2 / 5.1.1(viii)) — UserReports + UserBlocks tables + UserSafety module
   - ✅ ATT scaffold — `NSUserTrackingUsageDescription` ใน Info.plist
   - ✅ OTP copy fix — `otp_verification_screen.dart` ใช้ภาษาไทยและแสดงเบอร์ที่ mask แล้ว
   - ✅ Filter bug fix — `search_user.dart` ส่ง `daysOfWeek` ไป backend (facilities/brand/walkin รอ backend support)
4. **Social Login Phase 2 — Line + Account Linking:**
   - เพิ่ม `flutter_line_sdk` + `LineTokenVerifier` (verify โดยเรียก `https://api.line.me/v2/profile`)
   - หน้า Settings → Linked Accounts (ผูก/ถอน social provider จากบัญชี Local เดิมได้)
5. **Reviews & Ratings** ผู้จัด↔ผู้เล่นหลังจบก๊วน (สร้าง trust, ให้ผู้จัดดีขึ้นบนสุดในระบบค้นหา)
6. ~~**Deep Linking (Share Link)**~~ ✅ **MVP:**
   - **Backend:** `GET /api/player/gamesessions/share/{sessionPublicId}` `[AllowAnonymous]` → `UpcomingSessionCardDto`; ฟิลด์ `sessionPublicId` บนการ์ด + `ManageGameSessionDto` / `EditGameSessionDto`
   - **Flutter:** scheme `dropinbad://session/{guid}` (Android intent-filter + iOS `CFBundleURLTypes`), `app_links`, เส้นทาง `/share-session/:publicId` →โหลด API → `/booking-confirm`; ผู้จัดแชร์จากหน้าจัดการก๊วน (ปุ่มแชร์)
   - **ถัดไป:** Universal Links (`https://…`) + AASA / Digital Asset Links เมื่อมีโดเมนจริง
7. ~~**Recurring Session Template**~~ ✅ **เสร็จแล้ว** — **Backend:** ตาราง `OrganizerRecurringGameTemplates` + CRUD `/api/organizer/recurring-templates` + PATCH `active`; `RecurringSessionGenerationHostedService` สร้างก๊วนล่วงหน้า ~14 วันทุก 6 ชม. และหลังบันทึก template (reuse `CreateSessionAsync`); **Flutter:** ปุ่ม «ก๊วนประจำสัปดาห์» (`/recurring-templates`, form), mask วัน Mon=บิต0…Sun=บิต6
8. ~~**Map view ใน search**~~ ✅ **เสร็จแล้ว** — Backend ส่ง `latitude`/`longitude` จาก `Venues`; Flutter แท็บรายการ/แผนที่ (`search_sessions_map.dart`)
9. ~~**Backend hygiene (MatchManagement + warnings)**~~ ✅ — partial + `ParticipantTypes` + Player/Organizer services ที่เคยมี CS86xx / CS8629; DTO noisy warnings ถูกกดในไฟล์ด้วย `#nullable disable warnings`
10. **Refactor เชิงลึก (ทางเลือก):** แยก class เล็ก (live-state aggregator, billing orchestrator) เพื่อ unit test — ไม่จำเป็นก่อนปล่อย production
11. **Settings hub** (/settings): notification toggles per category + dark mode + linked accounts
12. ~~**withOpacity → withValues(alpha:) sweep**~~ ✅ **เสร็จแล้ว** (`lib/` ไม่เหลือ `.withOpacity(`)
13. ~~**Skeleton Loaders ขยายไปหน้าอื่น**~~ ✅ **เสร็จแล้ว** — `SessionCardListSkeleton` (ค้นหา + ประวัติ), `MyGamesPageSkeleton`, `WalletPageSkeleton`
14. **UI/UX Polish & Store Preparation:** App Icon, Splash Screen, Permissions, App Store screenshots, Empty states with illustration

<!-- ตัวอย่างการแจ้งแก้ UI ให้ผม
เครื่องที่เทส: iPad Mini 5 (หรือ iPhone SE, Galaxy S23) หน้าจอ: จัดการก๊วน manage_game.dart ปัญหาที่เจอ: ในการ์ดสนามตรงปุ่ม Pause ไอคอนมันเล็กเกินไป และชื่อผู้เล่นในช่องมันยาวจนตกบรรทัดไปทับขอบการ์ด -->