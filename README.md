# Printer_Tracer (Yazıcı Takip Sistemi)

Ağa **doğrudan bağlı** (print server olmadan) yazıcıların sayfa sayacını SNMP ile
periyodik okuyup SQLite'a kaydeden ve bu veriyi rapor/grafik olarak sunan bir
ASP.NET Core web uygulaması.

Print server olmadığı için kullanıcı/IP bazlı "kim ne bastı" bilgisi alınamaz;
takip edilen tek şey her yazıcının **toplam sayfa sayacı** (Printer-MIB
`prtMarkerLifeCount`). İki ardışık okuma arasındaki fark, o aralıkta basılan
sayfa sayısını verir.

## Özellikler

- **Arka plan toplayıcı** (`PrinterMonitorWorker`) – yapılandırılan aralıkta tüm
  yazıcıları dolaşıp SNMP GET ile sayaç değerini okur, zaman damgasıyla kaydeder.
- **SNMP v2c / v1** – önce v2c (`public` community) denenir, başarısız olursa
  otomatik v1'e düşer (config'den kapatılabilir).
- **Yazıcı yönetimi arayüzü** (`/Printers`, menüde "Yazıcılar") – yazıcı ekleme / yeniden adlandırma
  / silme; doğrudan veritabanına yazar, `appsettings.json` düzenlemek gerekmez.
- **Günlük Özet raporu** (`/Report/Summary`) – tarih aralığı seçilir
  (varsayılan son 30 gün), gün × yazıcı tablosu, günlük toplam bar grafik, özet
  kartları (dönem toplamı / günlük ortalama / en yoğun gün), 7/30/90 gün
  kısayolları, hafta sonu vurgusu. Aylık rapor için ayın 1'i → ay sonu aralığı
  girilerek kullanılır.
- **Ömür Boyu Sayaç raporu** (`/Report/Lifetime`) – her yazıcı için ilk okumadaki
  ham sayaç, güncel ham sayaç ve izleme süresince basılan fark.
- **SNMP Tanılama sayfası** (`/Diagnostics?ip=<ip>`) – bir yazıcıya GET/WALK yapıp
  `sysDescr`, `sysName`, bilinen sayfa-sayacı OID'leri ve `prtMarkerLifeCount`
  alt ağacını gösterir; marka tespiti yapıp doğru `PageCountOid`'i önerir. Yeni
  bir yazıcı bağlandığında doğru OID'i bulmak için kullanılır.
- **Örnek veri üreteci** (`SampleDataSeeder`) – yalnızca Development ortamında ve
  yalnızca tablo boşsa, arayüzü denemek için ~3 günlük sahte okuma üretir. Gerçek
  veriyi asla ezmez, üretimde kapalıdır.
- Config'den silinen yazıcı veritabanında kalır, geçmiş verisi korunur.

## Teknoloji

| Alan | Kullanılan |
|------|------------|
| Platform | .NET 9, C# |
| Web | ASP.NET Core MVC (Web App + `BackgroundService` bir arada) |
| SNMP | [`Lextm.SharpSnmpLib`](https://www.nuget.org/packages/Lextm.SharpSnmpLib) 12.5.7 |
| Veritabanı | SQLite + Entity Framework Core 9 (`Microsoft.EntityFrameworkCore.Sqlite` / `.Design`) |
| Arayüz | Razor Views, Bootstrap 5, jQuery |

## Proje Yapısı

```
YaziciTakip/
├── Program.cs                       # DI, DbContext, hosted service kaydı
├── appsettings.json                # SNMP ayarları, okuma aralığı, yazıcı listesi
├── Configuration/
│   └── PrinterMonitoringOptions.cs
├── Models/                         # Printer, PrintReading, rapor view-model'leri
├── Data/
│   ├── AppDbContext.cs
│   ├── AppDbContextFactory.cs      # design-time factory (EF komutları için)
│   └── Migrations/                 # InitialCreate
├── Services/
│   ├── ISnmpService.cs / SnmpService.cs      # SNMP GET / WALK / tanılama
│   ├── PrinterMonitorWorker.cs               # periyodik toplayıcı
│   └── SampleDataSeeder.cs
├── Controllers/                    # Home, Printers, Report, Diagnostics
└── Views/
```

## Çalıştırma

Gereksinim: [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/mesuttekgoz0/Printer_Tracer.git
cd Printer_Tracer

dotnet restore
dotnet run
```

Uygulama açıldığında veritabanı migration'ları otomatik uygulanır
(`yazicitakip.db` çalışma dizininde oluşur). Konsolda yazan
`http://localhost:5xxx` adresini tarayıcıda aç.

> Visual Studio ile: `YaziciTakip.csproj` açılıp F5 ile çalıştırılabilir.

### Arayüzü örnek veriyle denemek

Gerçek yazıcı olmadan raporları görmek için Development ortamında örnek veri
üretilebilir. `appsettings.Development.json` içinde:

```json
"PrinterMonitoring": { "SeedSampleReadings": true }
```

ayarlanıp `dotnet run` çalıştırılır (yalnızca `PrintReadings` tablosu boşken
üretir).

## Yapılandırma (`appsettings.json`)

```jsonc
"PrinterMonitoring": {
  "PollingIntervalMinutes": 1440,        // okuma sıklığı (dk)
  "Snmp": {
    "Community": "public",
    "Version": "V2c",                    // V2c | V1
    "FallbackToV1": true,                // v2c başarısızsa v1 dene
    "Port": 161,
    "TimeoutSeconds": 5,
    "PageCountOid": "1.3.6.1.2.1.43.10.2.1.4.1.1"   // toplam sayfa sayacı
  },
  "SeedSampleReadings": false,
  "SampleDataDays": 3,
  "Printers": []                         // istenirse { "Name": "...", "IpAddress": "..." }
}
```

### Yazıcı ekleme

Uygulama açıkken **Yazıcılar** sayfasından ad + IP girilir; kayıt doğrudan
veritabanına yazılır (`appsettings.json` düzenlemek gerekmez, `yazicitakip.db`
sürüm kontrolüne girmez). Alternatif olarak `appsettings.json` → `Printers`
dizisine eklenebilir; toplayıcı açılışta config ile veritabanını IP bazlı
senkronize eder.

Yeni bir yazıcının hangi OID'de sayaç tuttuğundan emin değilsen
`/Diagnostics?ip=<yazici-ip>` sayfasını kullan.

## Notlar ve Sınırlar

- Sayfa sayacı yazıcının **dahili ömür boyu kümülatif sayacıdır**; izlemeye
  başlamadan önce basılan sayfaları da içerir. Ancak sayaç daha önce manuel
  sıfırlanmışsa, gösterilen değer o sıfırlamadan sonraki toplamdır.
- `yazicitakip.db` ve tüm `*.db*` dosyaları `.gitignore` içindedir; okuma verisi
  repoya girmez.
- Şirket içi/gerçek IP'ler `appsettings.Local.json` /
  `appsettings.*.Local.json` / `appsettings.Production.json` dosyalarında
  tutulabilir (bunlar da git dışı).
