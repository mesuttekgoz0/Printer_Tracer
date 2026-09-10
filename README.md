# Printer_Tracer (Yazıcı Takip Sistemi)

Ağa **doğrudan bağlı** (print server olmadan) yazıcıların sayfa sayacını SNMP ile
okuyup SQLite'a kaydeden, bu veriyi rapor ve sayfa-başı hakediş belgesi olarak
sunan bir uygulama.

İki parçadan oluşur:

| Parça | Ne | Klasör | Port |
|-------|-----|--------|------|
| **Backend** | ASP.NET Core JSON API + SNMP + arka plan toplayıcı + SQLite | proje kökü | `5239` |
| **Frontend** | Next.js (App Router, TypeScript) | `web/` | `3000` |

Frontend, backend'in `/api/**` uçlarını çağırır (CORS ile). Backend hiçbir HTML
sunmaz; sadece JSON.

Print server olmadığı için kullanıcı/IP bazlı "kim ne bastı" bilgisi alınamaz;
takip edilen tek şey her yazıcının **toplam sayfa sayacı** (Printer-MIB
`prtMarkerLifeCount`). İki ardışık okuma arasındaki fark, o aralıkta basılan
sayfa sayısını verir.

## Özellikler

- **Sayaç Oku** – tek düğmeyle tüm ya da seçili yazıcıların o anki sayacı paralel
  okunur. Her yazıcı için marka/model (SNMP `sysDescr`, ilk okumada otomatik),
  önceki/son sayaç ve fark.
- **Rapor** – tarih aralığındaki tek tek okumalar + farkları, dönem toplamı,
  yazıcı bazında toplam. Aylık rapor buradan alınır.
- **Yazıcılar** – ekleme / yeniden adlandırma / silme; tür (siyah-beyaz / renkli)
  ve tedarikçi ataması. Doğrudan DB'ye yazar.
- **Tedarikçiler** – yazıcı servis/bayi firmaları ve tarihli sayfa-başı fiyat
  listeleri (tür başına ₺/sayfa).
- **Hakedişler** – bir tedarikçi seçilir, o tedarikçinin tüm yazıcıları için
  "önceki sayaç / şimdiki sayaç / fark / tutar" belgesi üretilir. Tutar =
  fiyat listesinden çekilen sayfa-başı fiyat × sayfa sayısı. Belge numaralanıp
  dondurulur; yazdırılabilir (PDF) ve CSV indirilir.
- **SNMP Tanılama** – bir IP'ye GET/WALK yapıp `sysDescr`, `sysName`, marka ve
  doğru sayfa-sayacı OID'ini önerir.
- **Arka plan toplayıcı** (`PrinterMonitorWorker`) – `PollingEnabled=true` ise
  yapılandırılan aralıkta otomatik okur. Varsayılan **kapalı**; açılışta yalnızca
  DB migration'ını çalıştırır.
- **SNMP v2c / v1** – önce v2c (`public`), başarısızsa v1.

## Teknoloji

| Alan | Kullanılan |
|------|------------|
| Backend | .NET 9, ASP.NET Core Web API + `BackgroundService` |
| SNMP | [`Lextm.SharpSnmpLib`](https://www.nuget.org/packages/Lextm.SharpSnmpLib) 12.5.7 |
| Veritabanı | SQLite + Entity Framework Core 9 |
| API dokümantasyonu | Swagger / OpenAPI (`/swagger`, yalnız Development) |
| Frontend | Next.js 16 (App Router), TypeScript, düz CSS |

## Proje Yapısı

```
YaziciTakip/
├── Program.cs                  # DI, CORS, Swagger, hosted service
├── appsettings.json            # SNMP ayarları, CORS origin'leri, Hakediş firma bilgisi
├── Configuration/              # PrinterMonitoringOptions, HakedisOptions
├── Models/                     # Entity'ler: Printer, PrintReading, Tur, Tedarikci,
│                               #   FiyatListesi, Hakedis (+ ManualReadResult, SnmpDiagnosticResult)
├── Data/                       # AppDbContext, design-time factory, Migrations
├── Services/                   # SnmpService, PrinterReadingService, PrinterMonitorWorker, SampleDataSeeder
├── Controllers/Api/            # PrintersApi, ReadingsApi, ReportApi, TedarikcilerApi,
│                               #   HakedislerApi, DiagnosticsApi, LookupsApi
└── web/                        # Next.js frontend
    ├── app/                    # sayfalar: yazicilar, sayac-oku, rapor, tedarikciler[/id],
    │                           #   hakedisler[/yeni,/id], snmp-tanilama
    ├── components/             # her sayfanın client bileşeni + Nav
    └── lib/                    # api.ts (fetch sarmalayıcı), types.ts, format.ts
```

## Çalıştırma

Gereksinim: [.NET 9 SDK](https://dotnet.microsoft.com/download) + [Node.js 20+](https://nodejs.org).

```bash
git clone https://github.com/mesuttekgoz0/Printer_Tracer.git
cd Printer_Tracer

# 1. Backend (terminal 1)
dotnet run
#   -> http://localhost:5239 , Swagger: http://localhost:5239/swagger

# 2. Frontend (terminal 2)
cd web
npm install          # ilk sefer
npm run dev
#   -> http://localhost:3000
```

`dotnet run` açılışta DB migration'larını uygular (`yazicitakip.db` çalışma
dizininde oluşur); **sayaç okuması yapmaz**.

Frontend'in backend adresi `web/.env.local` içindeki `NEXT_PUBLIC_API_BASE`
(varsayılan `http://localhost:5239`). Backend'in kabul ettiği frontend origin'i
`appsettings.json` → `Cors:Origins` (varsayılan `http://localhost:3000`).

### Arayüzü örnek veriyle denemek

Gerçek yazıcı olmadan raporları görmek için Development ortamında,
`appsettings.Development.json` içinde:

```json
"PrinterMonitoring": { "SeedSampleReadings": true }
```

ayarlanıp backend çalıştırılır (yalnızca `PrintReadings` tablosu boşken üretir).

## Yapılandırma (`appsettings.json`)

```jsonc
"Cors": { "Origins": [ "http://localhost:3000" ] },   // frontend origin(ler)i
"Hakedis": { "FromCompany": "", "ToCompany": "" },     // belgede gösterilen firma bilgisi (boş = gizli)
"PrinterMonitoring": {
  "PollingEnabled": false,               // true ise arka planda periyodik okuma
  "PollingIntervalMinutes": 1440,
  "Snmp": {
    "Community": "public",
    "Version": "V2c",                    // V2c | V1
    "FallbackToV1": true,
    "Port": 161,
    "TimeoutSeconds": 5,
    "PageCountOid": "1.3.6.1.2.1.43.10.2.1.4.1.1"
  },
  "SeedSampleReadings": false,
  "SampleDataDays": 3,
  "Printers": []                         // istenirse { "Name": "...", "IpAddress": "..." }
}
```

### Yazıcı ekleme

Frontend'deki **Yazıcılar** sayfasından ad + IP girilir; kayıt doğrudan
veritabanına yazılır. Alternatif olarak `appsettings.json` → `Printers` dizisine
eklenebilir; toplayıcı açılışta config ile veritabanını IP bazlı senkronize eder.

Yeni bir yazıcının hangi OID'de sayaç tuttuğundan emin değilsen **SNMP Tanılama**
sayfasını (ya da `GET /api/diagnostics?ip=<ip>`) kullan.

## Notlar ve Sınırlar

- Sayfa sayacı yazıcının **dahili ömür boyu kümülatif sayacıdır**; izlemeye
  başlamadan önce basılanları da içerir. Sayaç manuel sıfırlanmışsa, gösterilen
  değer o sıfırlamadan sonraki toplamdır.
- `yazicitakip.db` ve tüm `*.db*` dosyaları, `web/node_modules` ve `web/.next`
  `.gitignore` içindedir.
- Şirket içi/gerçek IP'ler `appsettings.Local.json` / `appsettings.*.Local.json`
  / `appsettings.Production.json` dosyalarında tutulabilir (git dışı).
- Giriş / kimlik doğrulama yoktur — iç ağ aracıdır.
