using Microsoft.EntityFrameworkCore;
using YaziciTakip.Configuration;
using YaziciTakip.Data;
using YaziciTakip.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AppDb")
                      ?? "Data Source=yazicitakip.db"));

builder.Services.Configure<PrinterMonitoringOptions>(
    builder.Configuration.GetSection(PrinterMonitoringOptions.SectionName));

builder.Services.AddSingleton<ISnmpService, SnmpService>();
builder.Services.AddScoped<SampleDataSeeder>();
builder.Services.AddHostedService<PrinterMonitorWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
