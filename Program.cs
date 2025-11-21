using Microsoft.EntityFrameworkCore;
using DropInBadAPI.Data;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Repositories;
using DropInBadAPI.Services; // ตรวจสอบให้แน่ใจว่า using นี้ถูกต้อง
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DropInBadAPI.Service.Mobile.Organizer;
using DropInBadAPI.Service.Mobile.Game;
using DropInBadAPI.Service.Mobile.Profile;
using DropInBadAPI.Hubs;


var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5185");

var MyAllowSpecificOrigins = "CorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          // แก้ไข: ลบ .AllowAnyOrigin() ที่ขัดแย้งออกไป
                          // และเพิ่ม .AllowCredentials() ที่จำเป็นสำหรับ SignalR
                          policy.WithOrigins(
                                        "http://localhost:5185",         // อนุญาตตัวเอง และ iOS Simulator
                                        "http://10.0.2.2:5185",          // อนุญาต Android Emulator (สำหรับการพัฒนา)
                                        "http://line-ddpm.we-builds.com" // อนุญาต Domain ของคุณ
                                     )
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials(); // **สำคัญมากสำหรับ SignalR**
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
        Description = "กรุณาใส่ Token ของคุณในรูปแบบ 'Bearer {token}'",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
    options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
    {
        Url = "/drop-in-api"
    });
});


// ลงทะเบียน SignalR
builder.Services.AddSignalR();
builder.Services.AddControllers();


builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped(typeof(IGenericService<>), typeof(GenericService<>));
builder.Services.AddScoped<IDropdownService, DropdownService>();


builder.Services.AddScoped<IOrganizerService, OrganizerService>();
builder.Services.AddScoped<IOrganizerSkillLevelService, OrganizerSkillLevelService>();

builder.Services.AddScoped<IGameSessionService, GameSessionService>();
builder.Services.AddScoped<IMatchManagementService, MatchManagementService>();
builder.Services.AddScoped<IMatchRecommenderService, MatchRecommenderService>();
builder.Services.AddScoped<IProfileService, ProfileService>();




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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
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


var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }
app.UseSwagger();
app.UseSwaggerUI();
// app.UseHttpsRedirection();

// **สำคัญมาก:** บอกให้แอปพลิเคชันรู้ว่าทำงานภายใต้ Path Base นี้
app.UsePathBase("/drop-in-api");

app.UseCors(MyAllowSpecificOrigins);


var useAuthentication = true;
if (useAuthentication)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapHub<ManagementGameHub>("/managementGameHub");

app.MapControllers();

app.Run();