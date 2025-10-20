using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WebAPI;

using WebAPI.Helpers;
using WebAPI.Hubs;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Add services --- 
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- DbContext Oracle ---
var username = builder.Configuration.GetConnectionString("DefaultUsername");      
var password = builder.Configuration.GetConnectionString("DefaultPassword");

var connectionStringTemplate = builder.Configuration.GetConnectionString("OracleDb");
var connectionString = connectionStringTemplate
    .Replace("{username}", username)
    .Replace("{password}", password);

// --- Session ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = ".WebAPI.Session"; // single cookie
});



// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

// --- JWT Authentication ---
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// --- HttpContextAccessor & Helper ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddSwaggerGen(options =>
{
    // Add JWT auth support in Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your token.\nExample: 'Bearer abc123xyz'",
    });

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
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddSingleton<OracleSessionHelper>();
builder.Services.AddSingleton<OracleConnectionManager>();
builder.Services.AddSingleton<QrGeneratorSingleton>();
// Add SignalR
builder.Services.AddSignalR();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5131, listenOptions =>
    {
        listenOptions.UseHttps(); // Dùng dev certificate tự sinh
    });
});


var app = builder.Build();

// --- Middleware pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection(); 

// --- CORS ---
app.UseCors("CorsPolicy");

// --- Session trước Authentication & Authorization ---
app.UseSession();

// --- Authentication & Authorization ---
app.UseAuthentication();
app.UseAuthorization();

// --- Map Controllers ---
app.MapControllers();

// --- Map SignalR Hubs ---
app.MapHub<NotificationHub>("/Hubs/notification");

app.Run();
