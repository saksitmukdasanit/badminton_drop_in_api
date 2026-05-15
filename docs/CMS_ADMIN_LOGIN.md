# CMS — สร้างบัญชีแอดมินและเข้าสู่ระบบ

บัญชี CMS อยู่ในตาราง **`CmsAdminUsers`** แยกจากผู้ใช้แอปมือถือ (`Users` / `UserLogins`)

รหัสผ่านเก็บเป็น **BCrypt** (work factor 11) ค่าเดียวกับ `PasswordHasher` ของ API

- อีเมลที่ใช้ login จะถูกแปลงเป็น **ตัวพิมพ์เล็ก** ก่อนเทียบ — แต่แนะนำเก็บในฐานข้อมูลเป็นตัวพิมพ์เล็กทั้งหมด
- ถ้าแก้ hash ใน SQL ต้องเก็บ **ครบ 60 ตัวอักษร** (รูปแบบ `$2a$11$...`) ห้ามตัดหรือมีช่องว่างหัวท้าย

## 1) รัน migration (ครั้งแรก)

รันสคริปต์สร้างตารางก่อน:

```bash
psql "Host=...;Port=5432;Database=...;Username=...;Password=..." -f Sql/2026_05_08_CmsAdminAndContent.sql
```

หรือใช้ connection string / GUI ของคุณแล้วรันไฟล์ `Sql/2026_05_08_CmsAdminAndContent.sql`

## 2) วิธีสร้างแอดมิน (เลือกอย่างใดอย่างหนึ่ง)

### A. Seed ตอนสตาร์ต API (สะดวกในช่วง dev)

ใน `appsettings.json` หรือ `appsettings.Development.json` ตั้ง:

```json
"CmsAuth": {
  "SlidingRefreshDays": 7,
  "AccessTokenMinutes": 60,
  "SeedAdminEmail": "admin@yourcompany.com",
  "SeedAdminPassword": "รหัสชั่วคราวที่แรงพอ"
}
```

จากนั้นสตาร์ต API — ถ้ายัง **ไม่มีแอดมินในตาราง** ระบบจะสร้างให้ครั้งเดียว

**แนะนำ:** หลัง login สำเร็จให้ **ล้าง** `SeedAdminPassword` (และถ้าต้องการให้ล้าง `SeedAdminEmail` ด้วย) เพื่อไม่เก็บรหัสในไฟล์

### B. แทรกด้วย SQL (ใช้ hash สำเร็จรูป)

รหัสตัวอย่าง **`Admin123!`** ตรงกับ hash ด้านล่าง (สร้างด้วย `BCrypt.Net` work factor 11 เหมือนใน API):

```sql
INSERT INTO "CmsAdminUsers" ("Email", "PasswordHash", "DisplayName", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
VALUES (
  'admin@dropinbad.local',
  '$2a$11$9f8gXsFx1sPWqtUIfUgqSepqzcIpnvQOHiCP/vugF0fM5H4XxYtLu',
  'Admin',
  TRUE,
  (now() AT TIME ZONE 'utc'),
  (now() AT TIME ZONE 'utc')
)
ON CONFLICT ("Email") DO NOTHING;
```

- อีเมล: `admin@dropinbad.local`
- รหัส: **`Admin123!`** (ควรเปลี่ยนหลังใช้งานจริง หรือสร้าง hash ใหม่ด้วยวิธี C)

ถ้าแถวนี้มีอยู่แล้ว `ON CONFLICT` จะไม่ error

Actually `ON CONFLICT DO NOTHING` without specification needs unique constraint on Email - in PostgreSQL it's:

```sql
ON CONFLICT ("Email") DO NOTHING;
```

I'll fix the doc.

### C. สร้าง hash เองจากรหัสที่ต้องการ

จากโฟลเดอร์ API:

```bash
cd DropInBadAPI/Tools/HashCmsPassword
dotnet run -- "รหัสของคุณ"
```

คัดลัง output (บรรทัด `$2a$11$...`) ไปใส่ใน `INSERT` ช่อง `"PasswordHash"`.

## 3) Login จากหน้าเว็บ CMS

- **Development:** `environment.ts` → `apiBaseUrl: 'http://localhost:5185'` แล้วยิงไปที่ `${apiBaseUrl}/api/admin/...`
- **Production (มี path `/drop-in-api` เหมือน Flutter):** ตั้ง `apiBaseUrl` เป็น **`https?://host/drop-in-api` อย่าต่อ `/api`** เพราะโค้ดต่อ `/api/admin/...` ให้แล้ว — ถ้าใส่เป็น `.../drop-in-api/api` จะกลายเป็น `.../api/api/admin/...` → **HTTP 404**
- หลัง build CMS สำหรับ production ใช้ `ng build --configuration=production` (หรือ config ที่ชี้ `environment.prod.ts`)

## 4) Endpoint ที่เกี่ยวข้อง (อ้างอิง)

| Method | Path | หมายเหตุ |
|--------|------|----------|
| POST | `/api/admin/auth/login` | ได้ `accessToken` + `refreshToken` (sliding ต่อเมื่อ refresh) |
| POST | `/api/admin/auth/refresh` | ต่ออายุ session (sliding) |
| POST | `/api/admin/auth/logout` | ต้องส่ง Bearer |

CRUD เนื้อหา (Splash / Banner / Popup): `/api/admin/cms/content` — ต้อง Bearer ของแอดมิน

## 5) CORS / ไม่ต่อ API (Safari แจ้ง *access control checks*)

- เปิดแอป CMS ที่ **`http://localhost:4200`** หรือ **`http://127.0.0.1:4200`** ได้ — ทั้งคู่เป็นคนละ **origin** ฝั่ง API ต้องอนุญาตทั้งสอง (ใน `appsettings.json` → `Cors:AllowedOrigins`; ว่างหรือไม่มี key ให้โค้ด fallback เป็นค่า dev เดิม)
- **ยิง API บน server จริงจาก `ng serve` (localhost):** ต้อง **deploy API เวอร์ชันล่าสุด** ที่มี CORS อนุญาต `http://localhost:4200` และ `http://127.0.0.1:4200` — ถ้า server ยังเป็น build เก่า CORS อาจไม่อนุญาต
- **CMS deploy เป็น HTTPS (`https://...`) แต่ `environment.prod.ts` ใช้ `http://...`:** เบราว์เซอร์จะ **บล็อก mixed content** — ให้ใส่ `apiBaseUrl` เป็น **`https://` ให้ตรงกับ public URL** ของ API reverse proxy เช่น `https://line-ddpm.we-builds.com/drop-in-api`
- **ถ้ามีข้อความประมาณ `No 'Access-Control-Allow-Origin'`:** ต้องเพิ่ม **scheme + host + port ตรงๆ** ของหน้า CMS เข้า `Cors:AllowedOrigins` (ระวังว่า **`https://`** กับ **`http://`** เป็นคนละ origin เช่น `https://line-ddpm.we-builds.com` ≠ `http://...`)
- สำหรับทดสอบกับ **API บนเครื่องตัวเอง:** ต้องรัน API ก่อน (`http://localhost:5185` หรือ `http://127.0.0.1:5185`)
- ถ้าใช้พอร์ต `ng serve` ไม่ใช่ 4200 ต้องเพิ่ม origin ใน CORS ให้ตรงพอร์ตนั้น

## 6) ข้อควรระวัง

- อย่า commit รหัสจริงลง git — ใช้ seed เฉพาะเครื่อง dev หรือ user-secrets / env
- เปลี่ยนรหัสตัวอย่าง **`Admin123!`** ทันทีถ้าใช้ในสภาพแวดล้อมที่เข้าถึงได้จากภายนอก
