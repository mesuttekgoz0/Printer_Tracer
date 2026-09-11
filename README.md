# Printer_Tracer (Yazıcı Takip Sistemi)

Ağa **doğrudan bağlı** (print server olmadan) yazıcıların sayfa sayacını SNMP ile
okuyup Microsoft SQL Server'a kaydeden, bu veriyi rapor ve sayfa-başı hakediş
belgesi olarak sunan bir uygulama.

İki parçadan oluşur:

| Parça | Ne | Klasör | Port |
|-------|-----|--------|------|
| **Backend** | ASP.NET Core JSON API + SNMP + MSSQL (saklı yordamlar) | proje kökü | `5239` |
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
- **Ağı Tara** – yerel alt ağı SNMP ile tarayıp bulunan yazıcıları listeler,
  tek tıkla ekler (Yazıcılar sayfası). "Erişilebilir tüm ağları tara" ile
  varsayılan ağ geçidinin yönlendirdiği diğer /24 ağlar da sırayla taranır.
- **SNMP v2c / v1** – önce v2c (`public`), başarısızsa v1. Geçici UDP paket
  kaybına karşı yeniden deneme (`Snmp:Retries`).
- Okumalar **yalnızca elle** yapılır (arka planda periyodik toplama yoktur);
  `dotnet run` açılışta sadece bekleyen EF migration'larını uygular.

## Teknoloji

| Alan | Kullanılan |
|------|------------|
| Backend | .NET 9, ASP.NET Core Web API |
| SNMP | [`Lextm.SharpSnmpLib`](https://www.nuget.org/packages/Lextm.SharpSnmpLib) 12.5.7 |
| Veritabanı | Microsoft SQL Server — şema EF Core 9 migration'larıyla, **tüm CRUD/listeleme saklı yordamlarla (stored procedure)** |
| API dokümantasyonu | Swagger / OpenAPI (`/swagger`, yalnız Development) |
| Frontend | Next.js 16 (App Router), TypeScript, düz CSS |

### Veri erişim mimarisi

EF Core burada **yalnızca şema** (migration'lar) ve DbContext'in bağlantısı için
kullanılır — LINQ ile sorgu/kayıt yapılmaz. `Data/Repositories/` altındaki
sınıflar `AppDbContext.Database.GetDbConnection()`'dan aldıkları bağlantı
üzerinden doğrudan `SqlCommand` + `CommandType.StoredProcedure` ile 46 saklı
yordamı çağırır. Controller'lar repository arayüzlerine bağımlıdır, `AppDbContext`'i
hiç görmez.

Prosedürlerin SQL kaynağı **`Data/StoredProcedures/*.sql`** — her prosedür kendi
düz T-SQL dosyasında (SSMS/Azure Data Studio'da doğrudan açılıp okunur/düzenlenir,
EF'e özgü hiçbir şey yok). `AddStoredProcedures` migration'ı bu dosyaları derlenmiş
assembly'den (embedded resource) okuyup `dotnet ef database update` sırasında
veritabanına yükler — yani tek kaynak `.sql` dosyaları, migration sadece taşıyıcı.

## Proje Yapısı

```
YaziciTakip/
├── Program.cs                  # DI, CORS, Swagger, açılışta EF migration
├── appsettings.json            # Bağlantı dizesi, SNMP ayarları, CORS origin'leri
├── Configuration/              # SnmpOptions, HakedisOptions
├── Models/                     # Entity'ler: Printer, PrintReading, Tur, Tedarikci,
│                               #   Fiyat/FiyatDetay, Hakedis (+ ManualReadResult, SnmpDiagnosticResult)
├── Data/
│   ├── AppDbContext.cs         # yalnızca şema/migration — LINQ sorgusu yok
│   ├── AppDbContextFactory.cs  # design-time factory (dotnet ef ...)
│   ├── StoredProcedures/       # her saklı yordamın kaynağı — düz .sql, 46 dosya
│   ├── Migrations/             # InitialCreate (tablolar) + AddStoredProcedures (.sql'leri yükler)
│   └── Repositories/           # ADO.NET + saklı yordam çağıran veri erişim katmanı
├── Services/                   # SnmpService, PrinterReadingService, PrinterDiscoveryService
├── Controllers/Api/            # PrintersApi, ReadingsApi, ReportApi, TedarikcilerApi,
│                               #   HakedislerApi, DiagnosticsApi, DiscoveryApi, LookupsApi
└── web/                        # Next.js frontend
    ├── app/                    # sayfalar: yazicilar, sayac-oku, rapor, tedarikciler[/id],
    │                           #   hakedisler[/yeni,/id], snmp-tanilama
    ├── components/             # her sayfanın client bileşeni + Nav
    └── lib/                    # api.ts (fetch sarmalayıcı), types.ts, format.ts
```

## Çalıştırma

Gereksinim: [.NET 9 SDK](https://dotnet.microsoft.com/download) + [Node.js 20+](https://nodejs.org)
+ **SQL Server**. Geliştirmede iki seçenek var:
- Kurulu bir SQL Server'ın (Developer/Express, varsayılan instance) `localhost`'ta
  Windows Authentication ile erişilebilir olması — bu makinede kullanılan yol.
- Ya da hiç kurulum yapmadan **LocalDB** (Visual Studio ile gelir; `sqllocaldb info`
  ile kontrol edilir, bağlantı dizesini `(localdb)\MSSQLLocalDB` yapmak yeterli).

```bash
git clone https://github.com/mesuttekgoz0/Printer_Tracer.git
cd Printer_Tracer

# 1. Backend (terminal 1) — ilk seferinde şema + saklı yordamları oluşturur
dotnet ef database update
dotnet run
#   -> http://localhost:5239 , Swagger: http://localhost:5239/swagger

# 2. Frontend (terminal 2)
cd web
npm install          # ilk sefer
npm run dev
#   -> http://localhost:3000
```

`dotnet run` açılışta yalnızca **bekleyen EF migration'larını** uygular (tablo/SP
yoksa oluşturur); **sayaç okuması yapmaz**. İlk kurulumda `dotnet ef database
update`'i elle çalıştırmak (migration kilidi / build sırası netliği için) önerilir,
ama `dotnet run` da aynısını açılışta zaten yapar.

Frontend'in backend adresi `web/.env.local` içindeki `NEXT_PUBLIC_API_BASE`
(varsayılan `http://localhost:5239`). Backend'in kabul ettiği frontend origin'i
`appsettings.json` → `Cors:Origins` (varsayılan `http://localhost:3000`).

## Yapılandırma (`appsettings.json`)

```jsonc
"ConnectionStrings": {
  // localhost'taki SQL Server'a Windows Authentication ile bağlanır (varsayılan instance).
  // Firmanın gerçek/uzak SQL Server'ına geçerken bu satırı appsettings.Production.json /
  // appsettings.*.Local.json'da (git dışı) override edin. LocalDB'ye dönmek için
  // "Server=(localdb)\\MSSQLLocalDB;..." yeterli.
  "AppDb": "Server=localhost;Database=YaziciTakip;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
},
"Cors": { "Origins": [ "http://localhost:3000" ] },   // frontend origin(ler)i
"Hakedis": { "FromCompany": "", "ToCompany": "" },     // belgede gösterilen firma bilgisi (boş = gizli)
"Snmp": {
  "Community": "public",
  "Version": "V2c",                     // V2c | V1
  "FallbackToV1": true,
  "Port": 161,
  "TimeoutSeconds": 5,
  "Retries": 2,                         // geçici UDP hatasında ek deneme
  "PageCountOid": "1.3.6.1.2.1.43.10.2.1.4.1.1"
}
```

### Yazıcı ekleme

Frontend'deki **Yazıcılar** sayfasından ad + IP girilir (ya da **Ağı Tara** ile
bulunur); kayıt doğrudan veritabanına yazılır.

Yeni bir yazıcının hangi OID'de sayaç tuttuğundan emin değilsen **SNMP Tanılama**
sayfasını (ya da `GET /api/diagnostics?ip=<ip>`) kullan.

## Notlar ve Sınırlar

- Sayfa sayacı yazıcının **dahili ömür boyu kümülatif sayacıdır**; izlemeye
  başlamadan önce basılanları da içerir. Sayaç manuel sıfırlanmışsa, gösterilen
  değer o sıfırlamadan sonraki toplamdır.
- `yazicitakip.db*` (eski SQLite dönemi dosyaları, artık kullanılmıyor — arşiv),
  `web/node_modules` ve `web/.next` `.gitignore` içindedir.
- Şirket içi/gerçek IP'ler ve gerçek SQL Server bağlantı dizesi
  `appsettings.Local.json` / `appsettings.*.Local.json` / `appsettings.Production.json`
  dosyalarında tutulabilir (git dışı).
- Giriş / kimlik doğrulama yoktur — iç ağ aracıdır.
