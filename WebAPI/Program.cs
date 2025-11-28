using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MobileServiceSystem.Signing;
using QuestPDF.Infrastructure;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Hubs;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF license configuration
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
var keyFolder = Path.Combine(AppContext.BaseDirectory, "DataProtection-Keys");
Directory.CreateDirectory(keyFolder); // đảm bảo folder tồn tại

// --- Add services --- 
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyFolder))
    .SetApplicationName("MyWebAPI"); // giữ ApplicationName cố định


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
    options.Cookie.Name = ".WebAPI.Session.new"; // single cookie
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
builder.Services.AddSingleton<QrLoginStore>();
builder.Services.AddSingleton<WebToMobileQrStore>();
builder.Services.AddSingleton<ProxyLoginService>();
// PDF signing services
builder.Services.AddSingleton<SignatureService>(sp =>
    new SignatureService(
        sp.GetRequiredService<OracleSessionHelper>(),
        sp.GetRequiredService<OracleConnectionManager>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    )
);
builder.Services.AddSingleton<WebAPI.Services.PdfTemplates.IPdfTemplate<WebAPI.Models.Import.ImportStockDto>, WebAPI.Services.PdfTemplates.ImportInvoiceTemplate>();
builder.Services.AddSingleton<WebAPI.Services.PdfTemplates.IPdfTemplate<WebAPI.Models.Export.ExportStockDto>, WebAPI.Services.PdfTemplates.ExportInvoiceTemplate>();
builder.Services.AddSingleton<WebAPI.Services.PdfTemplates.IPdfTemplate<WebAPI.Models.Invoice.InvoiceDto>, WebAPI.Services.PdfTemplates.SalesInvoiceTemplate>();
builder.Services.AddSingleton<PdfService>(sp => new PdfService(
    sp.GetRequiredService<SignatureService>(),
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<IHostEnvironment>(),
    sp.GetRequiredService<WebAPI.Services.PdfTemplates.IPdfTemplate<WebAPI.Models.Import.ImportStockDto>>(),
    sp.GetRequiredService<WebAPI.Services.PdfTemplates.IPdfTemplate<WebAPI.Models.Export.ExportStockDto>>(),
    sp.GetRequiredService<WebAPI.Services.PdfTemplates.IPdfTemplate<WebAPI.Models.Invoice.InvoiceDto>>()
));
builder.Services.AddSingleton<RsaKeyService>();
builder.Services.AddSingleton<EmailService>();
// Controller helper for common patterns
builder.Services.AddSingleton<ControllerHelper>();
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
