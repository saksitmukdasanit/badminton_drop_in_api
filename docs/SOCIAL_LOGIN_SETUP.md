# Social Login Setup Guide (Phase 1: Google + Apple)

> Phase 1 ของ Social Login ครอบคลุม **Google** และ **Apple** เท่านั้น (Apple บังคับโดย App Store เมื่อมี social login อื่น). Line / Facebook จะตามมาในภายหลัง

## ภาพรวม Flow

```
┌──────────────────┐  1. กดปุ่ม Sign in        ┌────────────┐
│  LoginScreen     │ ────────────────────────▶│  Google /  │
│  (Flutter)       │                          │  Apple SDK │
└──────────────────┘                          └─────┬──────┘
                                                    │ idToken
                                                    ▼
┌──────────────────┐  2. POST idToken         ┌──────────────┐
│  AuthController  │ ◀────────────────────────│ App Frontend │
│  (Backend)       │                          └──────────────┘
└─────┬────────────┘
      │ 3. verify token + match/create user
      ▼
┌──────────────────┐
│  UserLogins +    │  ProviderName=Google/Apple
│  UserProfiles    │  ProviderKey=sub
│  (PostgreSQL)    │
└─────┬────────────┘
      │ 4. return access/refresh token + requiresPhoneVerification
      ▼
   First-time social signup → /social-phone-link → OTP → /personal-info-screen
   Returning social user    → /
```

## ฝั่ง Backend ที่ต้องตั้งค่า

แก้ `appsettings.json` (หรือ `appsettings.Production.json` / environment variables):

```json
{
  "Auth": {
    "Google": {
      "IosClientId": "<iOS Client ID>",
      "AndroidClientId": "<Android Client ID>",
      "WebClientId": "<Web Client ID — ใช้เป็น aud ของ ID token เมื่อยิงจาก backend ของเรา>"
    },
    "Apple": {
      "BundleId": "<Bundle ID ของ iOS app เช่น com.dropinbad.badminton>",
      "ServiceId": "<Service ID สำหรับ Android web flow เช่น com.dropinbad.signin>",
      "AdditionalAudiences": ""
    }
  }
}
```

> ⚠️ **ห้าม commit ค่า production keys เข้า git** ใช้ `dotnet user-secrets` หรือ environment variables แทน

## ฝั่ง Frontend ที่ต้องตั้งค่า

### Google
แก้ `lib/shared/api_provider.dart`:
```dart
final String? googleServerClientId = '<Web Client ID>';
final String? googleIosClientId = '<iOS Client ID>';
```

### Apple
ไม่มี config ใน Flutter — ใช้ Bundle ID ที่ตั้งไว้ใน Xcode capabilities (Sign in with Apple)

## การขอ Keys

### Google Cloud Console (~5 นาที)

1. ไปที่ [Google Cloud Console → APIs & Services → Credentials](https://console.cloud.google.com/apis/credentials)
2. เลือก project ที่เชื่อมอยู่ (ดูใน `android/app/google-services.json` → `project_id`)
3. กด **+ CREATE CREDENTIALS → OAuth client ID** สร้าง 3 ตัว:

#### 3.1 Web Client ID (สำหรับ verify ที่ backend)
- Application type: **Web application**
- Name: `Drop In Bad - Backend Verify`
- Authorized redirect URIs: ไม่ต้องใส่
- กด Create → คัดลอก **Client ID** มาใส่ใน
  - Backend `Auth:Google:WebClientId`
  - Frontend `googleServerClientId`

#### 3.2 iOS Client ID
- Application type: **iOS**
- Name: `Drop In Bad - iOS`
- Bundle ID: ใส่ Bundle ID ของ iOS app (เช่น `com.dropinbad.badminton`)
- กด Create → คัดลอก **Client ID** มาใส่ใน
  - Backend `Auth:Google:IosClientId`
  - Frontend `googleIosClientId`
- ดาวน์โหลด `GoogleService-Info.plist` (จะใช้ตอนเช็คในโปรเจกต์ iOS)

#### 3.3 Android Client ID
- Application type: **Android**
- Name: `Drop In Bad - Android`
- Package name: `dropinbad.badminton` (ตอนนี้) → **เปลี่ยนเป็น Bundle ID จริง** ก่อนปล่อย Play Store
- SHA-1 fingerprint: เอาจาก keystore
  - Debug:
    ```bash
    keytool -list -v -keystore ~/.android/debug.keystore \
      -alias androiddebugkey -storepass android -keypass android
    ```
  - Release: รัน keytool กับ release keystore ของคุณ
- กด Create → คัดลอก **Client ID** มาใส่ใน Backend `Auth:Google:AndroidClientId`

### Apple Developer Portal (~10 นาที)

1. ไปที่ [Apple Developer → Certificates, IDs & Profiles → Identifiers](https://developer.apple.com/account/resources/identifiers/list)
2. **App ID**: เลือก Bundle ID ที่ใช้งาน → เปิด capability **Sign In with Apple** → Save
3. **Service ID** (สำหรับ web/Android flow):
   - กด **+ → Services IDs**
   - Description: `Drop In Bad Sign-In`
   - Identifier: `com.dropinbad.signin` (ขอแนะนำใช้ reverse domain ของแอปคุณ + suffix)
   - กดถัดไป → เปิด **Sign In with Apple** → Configure
     - Primary App ID: เลือก App ID ที่ตั้ง capability ไว้ (ขั้น 2)
     - Domains and Subdomains: domain ของ backend (เช่น `api.dropinbad.com`)
     - Return URLs: `https://api.dropinbad.com/api/auth/apple-callback` (ผมยังไม่ได้สร้าง endpoint นี้ — ขอเตือนเฉพาะตอน Phase ที่ใช้ web flow)
4. นำค่า Bundle ID + Service ID ใส่ใน Backend
   - `Auth:Apple:BundleId` = Bundle ID ของ App ID (สำหรับ iOS native flow)
   - `Auth:Apple:ServiceId` = Service ID ที่สร้างใหม่ (สำหรับ Android web flow)

> หากมีหลาย Bundle ID (dev / prod) ใส่ comma-separated ใน `Auth:Apple:AdditionalAudiences`

5. **Xcode capability**: เปิด Xcode → Runner target → Signing & Capabilities → **+ Capability → Sign in with Apple**

## การทดสอบ

### Backend
```bash
cd DropInBadAPI && dotnet run
```

ลองยิง Google login (ใช้ idToken จริงจาก mobile app):
```bash
curl -X POST http://localhost:5185/api/Auth/login-google \
  -H "Content-Type: application/json" \
  -d '{"idToken":"<ID_TOKEN>"}'
```

### Frontend
1. เปิดแอป → Login screen → กดปุ่ม Google หรือ Apple
2. ครั้งแรก: ระบบ navigate ไปหน้า `/social-phone-link` → กรอกเบอร์ → OTP → personal info
3. ครั้งถัดไป: navigate ไปหน้า `/` ทันที

## Account Linking (Phase 2 ในอนาคต)

ปัจจุบัน Phase 1 **ไม่ auto-link** ด้วย email — ผู้ใช้ที่มีบัญชีเก่า (Local) แล้วอยากใช้ Google/Apple จะได้บัญชีใหม่แยก. Phase 2 จะเพิ่มหน้า Settings → Linked Accounts ให้ผู้ใช้ผูก/ถอน social provider เอง

## Trouble shooting

| ปัญหา | สาเหตุ |
|---|---|
| `Google sign-in rejected: no Auth:Google client IDs configured` | ยังไม่ได้ใส่ `Auth:Google:*` ใน appsettings.json |
| `Apple identity token validation failed: aud invalid` | `Auth:Apple:BundleId` ไม่ตรงกับ Bundle ID จริงของ iOS app |
| `ไม่ได้รับ ID token จาก Google` ในแอป | `googleServerClientId` ใน api_provider.dart ไม่ถูกต้อง |
| Apple login ไม่ขึ้นใน iOS | ลืมเปิด capability Sign in with Apple ใน Xcode |
| Google ขึ้นข้อความ "Developer Error" บน Android | SHA-1 fingerprint ใน Google Cloud ไม่ตรงกับ keystore ที่ build |
