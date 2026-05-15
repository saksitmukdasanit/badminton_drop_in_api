if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run -- <plainPassword>");
    Console.WriteLine("Outputs BCrypt hash for CmsAdminUsers.PasswordHash");
    Environment.Exit(1);
}

var pwd = string.Join(" ", args);
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(pwd, 11));
