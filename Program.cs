using Microsoft.EntityFrameworkCore;
using DropInBadAPI.Data;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Repositories;
using DropInBadAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5185");

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          // สำหรับ Production ควรระบุ Domain ของ Frontend โดยตรง
                          // เช่น .WithOrigins("http://example.com", "https://www.example.com")
                          policy.AllowAnyOrigin() 
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

// ดึง Connection String จาก appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// ลงทะเบียน BadmintonDbContext กับ Dependency Injection
builder.Services.AddDbContext<BadmintonDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptionsAction: sqlOptions =>
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
});


builder.Services.AddControllers();

builder.Services.AddScoped<IFacilityService, FacilityService>();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IOrganizerService, OrganizerService>();
builder.Services.AddScoped<IOrganizerSkillLevelService, OrganizerSkillLevelService>();

builder.Services.AddScoped<IGameSessionService, GameSessionService>();
builder.Services.AddScoped<IMatchManagementService, MatchManagementService>();




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

var useAuthentication = true;
if (useAuthentication)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapControllers();

app.Run();