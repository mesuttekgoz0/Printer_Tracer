using Microsoft.EntityFrameworkCore;
using YaziciTakip.Configuration;
using YaziciTakip.Data;
using YaziciTakip.Data.Repositories;
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

var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? "Server=localhost;Database=YaziciTakip;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

// AppDbContext yalnızca şema (EF Core migration'ları) için kullanılır — uygulama artık
// veriye LINQ ile değil, Data/Repositories altındaki sınıflar üzerinden, doğrudan
// saklı yordamları (stored procedure) çağırarak erişiyor (bkz. CLAUDE.md "MSSQL geçişi").
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.Configure<SnmpOptions>(
    builder.Configuration.GetSection(SnmpOptions.SectionName));

builder.Services.Configure<HakedisOptions>(
    builder.Configuration.GetSection(HakedisOptions.SectionName));

builder.Services.AddSingleton<ISnmpService, SnmpService>();
builder.Services.AddScoped<PrinterReadingService>();
builder.Services.AddScoped<PrinterDiscoveryService>();

// Saklı yordam tabanlı veri erişim katmanı (ADO.NET + SqlCommand, CommandType.StoredProcedure).
builder.Services.AddScoped<ITurRepository, TurRepository>();
builder.Services.AddScoped<ITedarikciRepository, TedarikciRepository>();
builder.Services.AddScoped<IPrinterRepository, PrinterRepository>();
builder.Services.AddScoped<IPrintReadingRepository, PrintReadingRepository>();
builder.Services.AddScoped<IFiyatRepository, FiyatRepository>();
builder.Services.AddScoped<IHakedisRepository, HakedisRepository>();

var app = builder.Build();

// Açılışta bekleyen EF Core migration'larını uygula (DB dosyası yoksa oluşturur).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

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
