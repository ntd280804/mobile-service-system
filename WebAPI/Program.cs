using Microsoft.EntityFrameworkCore;
using WebAPI.Data;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // DbContext Oracle
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseOracle(builder.Configuration.GetConnectionString("OracleDb"))
            );

            // --- Session ---
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); // session tồn tại 30 phút
                options.Cookie.HttpOnly = true;                  // JS không truy cập được
                options.Cookie.IsEssential = true;              // luôn gửi cookie
                options.Cookie.Name = ".MyApp.Session";         // đặt tên cookie riêng
            });

            // HttpContextAccessor để Helper truy cập session
            builder.Services.AddHttpContextAccessor();

            // Helper service
            builder.Services.AddScoped<Helper>();

            var app = builder.Build();

            // Configure pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // --- Session trước Authorization ---
            app.UseSession();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
