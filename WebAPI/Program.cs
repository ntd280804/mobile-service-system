using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // --- Add services ---
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // --- DbContext Oracle ---
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseOracle(builder.Configuration.GetConnectionString("OracleDb"))
            );

            // --- SignalR ---
            builder.Services.AddSignalR();

            // --- Session ---
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.Name = ".MyApp.Session";
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
                        .SetIsOriginAllowed(_ => true); // cho phép tất cả origin (có thể giới hạn sau)
                });
            });

            // --- HttpContextAccessor & Helper ---
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<Helper>();

            var app = builder.Build();

            // --- Middleware pipeline ---
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Thêm CORS
            app.UseCors("CorsPolicy");

            // Session trước Authorization
            app.UseSession();

            app.UseAuthorization();

            // --- Map routes ---
            app.MapControllers();

            // Map SignalR Hub

            app.Run();
        }
    }
}
