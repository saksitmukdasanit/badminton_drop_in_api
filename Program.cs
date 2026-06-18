using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;
using DropInBadAPI.Data;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Repositories;
using DropInBadAPI.Services; // ตรวจสอบให้แน่ใจว่า using นี้ถูกต้อง
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.Linq;
using DropInBadAPI.Service.Mobile.Organizer;
using DropInBadAPI.Service.Mobile.Game;
using DropInBadAPI.Service.Mobile.Profile;
using DropInBadAPI.Hubs;
using DropInBadAPI.Utilities; // เพิ่ม using สำหรับ Combinatorics
using DropInBadAPI.Service.MobilePlayer.Game;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;


var builder = WebApplication.CreateBuilder(args);

// 0.0.0.0 = รับทั้ง localhost / 127.0.0.1 / LAN (mobile ยิงจากเครือข่ายเดียวกันได้)
builder.WebHost.UseUrls("http://0.0.0.0:5185");

var MyAllowSpecificOrigins = "CorsPolicy";

string[] CorsDefaultOrigins()
{
    return new[]
    {
        "http://localhost:5185",
        "http://127.0.0.1:5185",
        "http://10.0.2.2:5185",
        "http://line-ddpm.we-builds.com",
        "https://line-ddpm.we-builds.com",
        "http://localhost:4200",
        "http://127.0.0.1:4200",
        "http://localhost:4201",
        "http://127.0.0.1:4201",
    };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          // ควบคุมด้วย appsettings Cors:AllowedOrigins (เหมาะเมื่อเปลี่ยนโดเมน CMS/production)
                          var configured = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
                          var origins = configured is { Length: > 0 }
                              ? configured.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.TrimEnd('/')).Distinct().ToArray()
                              : CorsDefaultOrigins();
                          policy.WithOrigins(origins)
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials(); // จำเป็นกับ SignalR / cookie-style clients
                      });
});

// ดึง Connection String จาก appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// ลงทะเบียน BadmintonDbContext กับ Dependency Injection
builder.Services.AddDbContext<BadmintonDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    }));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(options =>
{
    // 1. กำหนดรูปแบบของ Security Scheme (JWT Bearer)
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme.",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    // 2. บอกให้ Swagger รู้ว่าต้องใช้ Security Scheme นี้
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[]{}

        }
    });
    // options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
    // {
    //     Url = "/drop-in-api"
    // });
});


// ลงทะเบียน SignalR
builder.Services.AddSignalR();
builder.Services.AddHttpClient(); // เพิ่มบรรทัดนี้เพื่อใช้งาน HttpClientFactory
builder.Services.AddControllers();


builder.Services.AddSingleton<DropInBadAPI.Modules.Auth.IPasswordHasher, DropInBadAPI.Modules.Auth.PasswordHasher>();
builder.Services.AddSingleton<DropInBadAPI.Modules.Auth.IGoogleTokenVerifier, DropInBadAPI.Modules.Auth.GoogleTokenVerifier>();
builder.Services.AddSingleton<DropInBadAPI.Modules.Auth.IAppleTokenVerifier, DropInBadAPI.Modules.Auth.AppleTokenVerifier>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<DropInBadAPI.Modules.UserSafety.IUserSafetyService, DropInBadAPI.Modules.UserSafety.UserSafetyService>();

builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminAuditService, DropInBadAPI.Modules.Admin.AdminAuditService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminAuthService, DropInBadAPI.Modules.Admin.AdminAuthService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminCmsService, DropInBadAPI.Modules.Admin.AdminCmsService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminCmsAdminUsersService, DropInBadAPI.Modules.Admin.AdminCmsAdminUsersService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminAboutSettingsService, DropInBadAPI.Modules.Admin.AdminAboutSettingsService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminPolicyDocumentsService, DropInBadAPI.Modules.Admin.AdminPolicyDocumentsService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminAppUsersService, DropInBadAPI.Modules.Admin.AdminAppUsersService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminOrganizersService, DropInBadAPI.Modules.Admin.AdminOrganizersService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminGameSessionsAdminService, DropInBadAPI.Modules.Admin.AdminGameSessionsAdminService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminNotificationsAdminService, DropInBadAPI.Modules.Admin.AdminNotificationsAdminService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Admin.IAdminDashboardService, DropInBadAPI.Modules.Admin.AdminDashboardService>();
builder.Services.AddScoped<DropInBadAPI.Modules.Public.ICmsPublicService, DropInBadAPI.Modules.Public.CmsPublicService>();

builder.Services.AddScoped(typeof(IGenericService<>), typeof(GenericService<>));
builder.Services.AddScoped<IDropdownService, DropdownService>();


builder.Services.AddScoped<IOrganizerService, OrganizerService>();
builder.Services.AddScoped<IOrganizerSkillLevelService, OrganizerSkillLevelService>();

builder.Services.AddScoped<IGameSessionBillingService, GameSessionBillingService>();
builder.Services.AddScoped<IGameSessionBookingService, GameSessionBookingService>();
builder.Services.AddScoped<IAutoMatchService, AutoMatchService>();
builder.Services.AddScoped<IAutoMatchPresetService, AutoMatchPresetService>();
builder.Services.AddScoped<IGameSessionService, GameSessionService>();
builder.Services.AddScoped<IRecurringGameTemplateService, RecurringGameTemplateService>();
builder.Services.AddHostedService<RecurringSessionGenerationHostedService>();
builder.Services.AddHostedService<ExpiredSessionCancellationHostedService>(); // Auto-Cancel ก๊วนหมดเวลา + คืนเงินผู้เล่น
builder.Services.AddScoped<IMatchManagementService, MatchManagementService>();
builder.Services.AddScoped<IMatchRecommenderService, MatchRecommenderService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

builder.Services.AddScoped<INotificationService, NotificationService>(); // ลงทะเบียน Notification Service

builder.Services.AddScoped<IPlayerGameSessionService, PlayerGameSessionService>();
builder.Services.AddScoped<IFollowService, FollowService>(); // ลงทะเบียน Follow Service
builder.Services.AddScoped<IPlayerDashboardService, PlayerDashboardService>(); // ลงทะเบียน Dashboard Service ของ Player
builder.Services.AddScoped<IOrganizerDashboardService, OrganizerDashboardService>(); // ลงทะเบียน Dashboard Service ของ Organizer

builder.Services.AddHttpClient<IXenditService, XenditService>();
builder.Services.AddScoped<IWalletService, WalletService>(); // ลงทะเบียน Wallet Service



builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            RoleClaimType = ClaimTypes.Role
        };

        // **สำคัญมาก:** เพิ่มส่วนนี้เพื่อให้ SignalR สามารถยืนยันตัวตนผ่าน Query String ได้
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                // ถ้า Request วิ่งมาที่ Hub ของเราและมี access_token ใน Query String
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/managementGameHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();


// --- Firebase Admin SDK Initialization ---
// ตรวจสอบว่ามีไฟล์ Service Account Key ของ Firebase หรือไม่
var firebaseConfigPath = Path.Combine(AppContext.BaseDirectory, "firebase-adminsdk.json");
if (File.Exists(firebaseConfigPath))
{
#pragma warning disable CS0618 // GoogleCredential.FromFile — รอขึ้น credential factory ในทีหลัง
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromFile(firebaseConfigPath),
    });
#pragma warning restore CS0618
    Console.WriteLine("Firebase Admin SDK Initialized.");
}
else
{
    Console.WriteLine($"[WARNING] Firebase Admin SDK Config file not found at: {firebaseConfigPath}");
}
// -----------------------------------------

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// app.UseHttpsRedirection();

// **สำคัญมาก:** บอกให้แอปพลิเคชันรู้ว่าทำงานภายใต้ Path Base นี้
// app.UsePathBase("/drop-in-api");

app.UseCors(MyAllowSpecificOrigins);

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        if (ex != null)
            Console.WriteLine($"[API-UNHANDLED] {ex}");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new DropInBadAPI.Models.Response<object>
        {
            Status = 500,
            Message = "เกิดข้อผิดพลาดภายในระบบ กรุณาลองใหม่อีกครั้ง",
            Data = null
        });
    });
});

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ManagementGameHub>("/managementGameHub");

app.MapControllers();

await SeedCmsAdminIfConfigured(app);

app.Run();

static async Task SeedCmsAdminIfConfigured(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var seedEmail = cfg["CmsAuth:SeedAdminEmail"];
    var seedPwd = cfg["CmsAuth:SeedAdminPassword"];
    if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPwd))
    {
        return;
    }

    var db = scope.ServiceProvider.GetRequiredService<BadmintonDbContext>();
    if (await db.CmsAdminUsers.AnyAsync())
    {
        return;
    }

    var hasher = scope.ServiceProvider.GetRequiredService<DropInBadAPI.Modules.Auth.IPasswordHasher>();
    db.CmsAdminUsers.Add(new CmsAdminUser
    {
        Email = seedEmail.Trim().ToLowerInvariant(),
        PasswordHash = hasher.Hash(seedPwd),
        DisplayName = "Seed Admin",
        IsActive = true,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    Console.WriteLine("[CMS] Seeded first CmsAdminUser (clear CmsAuth:SeedAdmin* after use).");
}