using Microsoft.EntityFrameworkCore;
using YaziciTakip.Configuration;
using YaziciTakip.Data;
using YaziciTakip.Services;

var builder = WebApplication.CreateBuilder(args);

// Saf JSON API — Razor/MVC arayüzü Next.js'e (web/) taşındı.
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Next.js frontend'i (ayrı origin) API'yi çağırabilsin diye CORS.
// İzin verilen origin'ler appsettings.json -> "Cors:Origins" (dizi); yoksa dev varsayılanı.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? new[] { "http://localhost:3000" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AppDb")
                      ?? "Data Source=yazicitakip.db"));

builder.Services.Configure<PrinterMonitoringOptions>(
    builder.Configuration.GetSection(PrinterMonitoringOptions.SectionName));

builder.Services.Configure<HakedisOptions>(
    builder.Configuration.GetSection(HakedisOptions.SectionName));

builder.Services.AddSingleton<ISnmpService, SnmpService>();
builder.Services.AddScoped<SampleDataSeeder>();
builder.Services.AddScoped<PrinterReadingService>();
builder.Services.AddHostedService<PrinterMonitorWorker>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("frontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
