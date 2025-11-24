using WebApp.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient("WebApiClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WebApi:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler
    {
        // Bỏ qua validation certificate (chỉ dev, LAN)
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });


// --- Thêm session ---
builder.Services.AddDistributedMemoryCache(); // Lưu session trên RAM
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = ".WebAPI.Session"; // single cookie
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(7158, listenOptions =>
    {
        listenOptions.UseHttps(); // Dùng dev certificate
    });
});
builder.Services.AddSingleton<OracleClientHelper>();
builder.Services.AddHttpClient<WebApp.Services.SecurityClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WebApi:BaseUrl"]);
    client.Timeout = TimeSpan.FromMinutes(2);
}).ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// --- Nếu muốn HttpContext trong Helper/Service ---
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Public/Home/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// --- Enable session ---
app.UseSession();

app.UseAuthorization();

// Route cho tất cả Area (Admin, Public,...)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Route default, nếu truy cập / thì tự động vào Area Public
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "Public", controller = "Home", action = "Index" });

app.Run();
