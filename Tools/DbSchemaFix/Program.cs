using Npgsql;

var connStr = args.Length > 0
    ? args[0]
    : "Host=110.78.211.156;Port=5432;Database=DropInBad;Username=postgres;Password=PassW0rd";

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

static async Task<bool> ColumnExists(NpgsqlConnection conn, string table, string column)
{
    await using var cmd = new NpgsqlCommand(
        "SELECT 1 FROM information_schema.columns WHERE table_name = @t AND column_name = @c LIMIT 1",
        conn);
    cmd.Parameters.AddWithValue("t", table);
    cmd.Parameters.AddWithValue("c", column);
    return await cmd.ExecuteScalarAsync() != null;
}

static async Task RunSql(NpgsqlConnection conn, string sql)
{
    await using var cmd = new NpgsqlCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Checking schema...");

// ลบแถว bypass OTP รูปแบบเก่าที่ใช้ ProviderKey ซ้ำทั้งระบบ (ทำให้สมัครคนที่ 2+ ไม่ได้)
await using (var cleanup = new NpgsqlCommand(
    """DELETE FROM "UserLogins" WHERE "ProviderName" = 'SMSMKT' AND "ProviderKey" = '__OTP_BYPASS__';""",
    conn))
{
    var n = await cleanup.ExecuteNonQueryAsync();
    if (n > 0) Console.WriteLine($"Removed {n} legacy OTP bypass UserLogin row(s).");
}

if (!await ColumnExists(conn, "Users", "DeletedAt"))
{
    Console.WriteLine("Adding Users.DeletedAt ...");
    await RunSql(conn, """
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
        CREATE INDEX IF NOT EXISTS "IX_Users_DeletedAt" ON "Users" ("DeletedAt") WHERE "DeletedAt" IS NOT NULL;
        """);
}
else Console.WriteLine("Users.DeletedAt OK");

if (!await ColumnExists(conn, "UserProfiles", "SkillDisplayOrganizerUserID"))
{
    Console.WriteLine("Adding UserProfiles.SkillDisplayOrganizerUserID ...");
    await RunSql(conn, """
        ALTER TABLE "UserProfiles" ADD COLUMN IF NOT EXISTS "SkillDisplayOrganizerUserID" integer NULL;
        """);
    await RunSql(conn, """
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_UserProfiles_SkillDisplayOrganizerUserID') THEN
            ALTER TABLE "UserProfiles"
              ADD CONSTRAINT "FK_UserProfiles_SkillDisplayOrganizerUserID"
              FOREIGN KEY ("SkillDisplayOrganizerUserID") REFERENCES "Users" ("UserID") ON DELETE SET NULL;
          END IF;
        END $$;
        """);
}
else Console.WriteLine("UserProfiles.SkillDisplayOrganizerUserID OK");

// Test insert pattern like RegisterAsync
await using var tx = await conn.BeginTransactionAsync();
try
{
    await using (var cmd = new NpgsqlCommand(
        """INSERT INTO "Users" ("IsActive") VALUES (true) RETURNING "UserID";""", conn, tx))
    {
        var userId = (int)(await cmd.ExecuteScalarAsync())!;
        Console.WriteLine($"Test insert Users OK (UserID={userId})");

        await using var cmd2 = new NpgsqlCommand(
            """INSERT INTO "UserProfiles" ("UserID", "PhoneNumber", "IsPhoneNumberVerified") VALUES (@uid, @phone, false);""",
            conn, tx);
        cmd2.Parameters.AddWithValue("uid", userId);
        cmd2.Parameters.AddWithValue("phone", "__schema_test__");
        await cmd2.ExecuteNonQueryAsync();
        Console.WriteLine("Test insert UserProfiles OK");

        var refreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        await using var cmd3 = new NpgsqlCommand(
            "INSERT INTO \"UserLogins\" (\"ProviderName\", \"ProviderKey\", \"UserID\", \"PasswordHash\", \"RefreshToken\", \"RefreshTokenExpiryTime\") " +
            "VALUES ('Local', @key, @uid, @hash, @rt, @exp);",
            conn, tx);
        cmd3.Parameters.AddWithValue("key", "__schema_test_user__");
        cmd3.Parameters.AddWithValue("uid", userId);
        cmd3.Parameters.AddWithValue("hash", "$2a$11$abcdefghijklmnopqrstuv");
        cmd3.Parameters.AddWithValue("rt", refreshToken);
        cmd3.Parameters.AddWithValue("exp", DateTime.UtcNow.AddDays(90));
        await cmd3.ExecuteNonQueryAsync();
        Console.WriteLine("Test insert UserLogins OK (refresh len=" + refreshToken.Length + ")");
    }
    await tx.RollbackAsync();
    Console.WriteLine("Schema fix complete (test rolled back).");
}
catch (Exception ex)
{
    await tx.RollbackAsync();
    Console.WriteLine("Test insert FAILED: " + ex.Message);
    Environment.ExitCode = 1;
}
