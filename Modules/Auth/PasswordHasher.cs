namespace DropInBadAPI.Modules.Auth;

/// <summary>
/// Password hashing abstraction พร้อมรองรับ legacy hash แบบ "hashed_xxx" ที่อยู่ในฐานข้อมูลเดิม
/// (จะถูก upgrade เป็น BCrypt อัตโนมัติเมื่อผู้ใช้ login สำเร็จครั้งถัดไป)
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hash plaintext password ด้วย BCrypt (work factor default 11)</summary>
    string Hash(string password);

    /// <summary>
    /// ตรวจสอบรหัสผ่านกับ hash ที่เก็บไว้
    /// รองรับทั้ง BCrypt (รูปแบบ <c>$2a$..</c> / <c>$2b$..</c> / <c>$2y$..</c>)
    /// และ legacy <c>hashed_&lt;plaintext&gt;</c>
    /// </summary>
    bool Verify(string password, string storedHash);

    /// <summary>true เมื่อ hash ที่เก็บไว้เป็นรูปแบบเดิม (placeholder) ที่ควร upgrade</summary>
    bool IsLegacyHash(string storedHash);
}

public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;
    private const string LegacyPrefix = "hashed_";

    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        if (IsLegacyHash(storedHash))
        {
            // เปรียบเทียบแบบเดิม (insecure) เพื่อรองรับ user เก่าที่ยังไม่ได้ login หลัง upgrade
            return storedHash == LegacyPrefix + password;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // hash รูปแบบไม่รู้จัก → reject
            return false;
        }
    }

    public bool IsLegacyHash(string storedHash)
    {
        return !string.IsNullOrEmpty(storedHash) && storedHash.StartsWith(LegacyPrefix);
    }
}
