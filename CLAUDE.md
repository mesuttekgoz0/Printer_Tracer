# Yazıcı Takip Sistemi (YaziciTakip)

## Proje Amacı
Staj yapılan şirkette, ağa doğrudan bağlı (print server üzerinden değil) yazıcıların kullanımını izleyen bir sistem geliştirmek. Yönetici, yazıcıların hangi saatte ne kadar çıktı verdiğini görebileceği bir rapor/arayüz istiyor.

## Kapsam ve Kısıtlar
- Yazıcılar **doğrudan ağa bağlı**, print server yok → kullanıcı/IP bazlı print job bilgisi (kim gönderdi) SNMP ile alınamaz, sadece **sayfa sayısı (page counter)** takip edilecek.
- Veri toplama yöntemi: **SNMP** (Printer-MIB standart OID: `1.3.6.1.2.1.43.10.2.1.4.1.1` - toplam sayfa sayacı).
- SNMP versiyonu henüz bilinmiyor — önce **v2c** ("public" community string) ile denenecek, çalışmazsa v1'e düşülecek.
- Yazıcı IP adresleri henüz belirlenmedi, sonradan eklenip test edilecek (config üzerinden kolayca eklenebilir olmalı).
- Okuma sıklığı: test amaçlı **15 dakika**.
- Veri saklama: **SQLite** (EF Core ile).
- **Arayüz ve rapor isteniyor** — sadece arka planda veri toplayan bir servis değil, bu veriyi gösterecek bir web arayüzü de olacak.

## Teknoloji
- .NET 8, C#
- ASP.NET Core (Web App + BackgroundService bir arada) — Visual Studio üzerinden proje açıldı
- SNMP için `Lextm.SharpSnmpLib` NuGet paketi
- EF Core + SQLite

## Planlanan Dosya Yapısı
```
YaziciTakip/
├── Program.cs
├── appsettings.json                # yazıcı IP listesi, okuma aralığı, community string
├── Models/
│   ├── Printer.cs                  # Id, Name, IpAddress
│   └── PrintReading.cs             # PrinterId, PageCount, Timestamp
├── Data/
│   └── AppDbContext.cs             # EF Core DbContext (SQLite)
├── Services/
│   ├── ISnmpService.cs
│   ├── SnmpService.cs              # SNMP GET işlemleri
│   └── PrinterMonitorWorker.cs     # BackgroundService - periyodik SNMP okuma
├── Controllers/
│   └── ReportController.cs         # rapor sayfalarını besleyen controller
└── Views/ (veya Pages/)
    └── Report/
        └── Index.cshtml            # tablo/grafik ile rapor arayüzü
```

## Akış
`PrinterMonitorWorker` arka planda periyodik olarak `SnmpService` üzerinden her yazıcının sayaç değerini okur → `AppDbContext` ile SQLite'a zaman damgasıyla kaydeder. İki ardışık okuma arasındaki fark, o aralıkta basılan sayfa sayısını verir. Web arayüzü bu veriyi saatlik/günlük tablo ve grafik halinde gösterir.

## Yapılacaklar (Sırayla)
1. [x] `Models/Printer.cs` ve `Models/PrintReading.cs` oluştur
2. [x] `Data/AppDbContext.cs` ile SQLite bağlantısını kur, migration oluştur (`Data/Migrations/InitialCreate`; worker açılışta `MigrateAsync` çağırıyor)
3. [x] `Services/SnmpService.cs` — SharpSnmpLib ile SNMP GET işlemi (v2c, public community; config'e göre v1 fallback)
4. [x] `Services/PrinterMonitorWorker.cs` — BackgroundService, 15 dk'da bir tüm yazıcıları döngüyle oku ve kaydet
5. [x] `appsettings.json` içine yazıcı listesi ve ayarları ekle (`PrinterMonitoring` bölümü, `PrinterMonitoringOptions`)
6. [x] Web tarafı: `ReportController` — `Summary` action + `Views/Report/Summary.cshtml`: tarih aralığı (varsayılan son 30 gün), gün×yazıcı tablo + günlük toplam bar grafik + özet kartları (dönem toplamı / günlük ort. / en yoğun gün); Son 7/30/90 gün kısayolları, hafta sonu vurgulu. Nav: "Günlük Özet".
   - **Saatlik rapor (`Report/Index`) kaldırıldı (2026-09-03)** — bkz. aşağıdaki not, artık günlük okumaya geçildiği için anlamsızdı.
7. [ ] Test: en az 1 gerçek yazıcı IP'si ile SNMP bağlantısını doğrula, gerekirse v1'e düş
8. [ ] (Sonraki aşama) Yöneticiden gelecek ek detayları netleştir ve entegre et

### Not (2026-09-02)
- Proje `net9.0` hedefliyor (CLAUDE.md'de net8 yazıyordu). Paketler: `Lextm.SharpSnmpLib` 12.5.7, `Microsoft.EntityFrameworkCore.Sqlite` + `.Design` 9.0.4.
- DB dosyası: `yazicitakip.db` (çalışma dizininde, gitignore önerilir).
- Yeni yazıcı eklemek için sadece `appsettings.json` → `PrinterMonitoring:Printers` dizisine `{ "Name", "IpAddress" }` eklemek yeterli; worker açılışta DB ile IP bazlı senkronize ediyor (config'den silinen yazıcı DB'de kalır, geçmiş veri korunur).
- Sayfa sayacı OID'i config'den değiştirilebilir (`PrinterMonitoring:Snmp:PageCountOid`). Bazı yazıcılarda instance farklı olabilir.
- EF migration komutları için `Data/AppDbContextFactory.cs` (design-time factory) eklendi.
- Örnek/demo veri: `Services/SampleDataSeeder.cs`. `PrinterMonitoring:SeedSampleReadings=true` (sadece `appsettings.Development.json`'da açık) VE `PrintReadings` tablosu boşsa açılışta ~3 günlük sahte okuma üretir (ofis kullanım profili). Gerçek veriyi asla ezmez. Prod'da kapalı.
- `appsettings.json`'da 3 örnek yazıcı var (192.0.2.50-52, RFC 5737 belge aralığı — ağda karşılığı yok). Gerçek yazıcı IP'leri hâlâ bekleniyor.
- **SNMP Tanılama sayfası** eklendi: `/Diagnostics?ip=<ip>` (`DiagnosticsController` + `ISnmpService.DiagnoseAsync`). Yazıcıya SNMP GET/WALK yapıp sysDescr, sysName, bilinen sayfa-sayacı OID'leri ve `prtMarkerLifeCount` alt ağacını gösterir; doğru `PageCountOid`'i önerir. Gerçek yazıcı IP'si gelince adım 7 bununla yapılacak.
- Not: kullanıcı test sırasında 192.168.1.51'i denedi → cihaz `HP V1810-48G (J9660A)` **switch** çıktı, yazıcı değil. Placeholder IP'ler artık 192.0.2.x.

### Not (2026-09-03) — "Yazıcının önceki basım verisi de vardır" talebi
- Yönetici, sistemin sadece "şu andan itibaren" değişimi gösterdiğini düşünüp izlemeye başlamadan ÖNCE yazıcının bastığı toplam sayfanın da bulunmasını istedi.
- Çözüm: Printer-MIB sayacı (`prtMarkerLifeCount`) zaten yazıcının **dahili ömür boyu kümülatif sayacı** — üretildiğinden/sıfırlandığından beri bastığı TÜM sayfaları içerir, sadece bizim izlememize başladığımızdan beri basılanları değil. Yani bu veri zaten her okumada (`PrintReading.PageCount`) mevcuttu, sadece ayrı gösterilmiyordu.
- Eklenen: `ReportController.Lifetime()` + `Views/Report/Lifetime.cshtml` (`/Report/Lifetime`, nav'da "Ömür Boyu Sayaç"), `Models/LifetimeViewModel.cs`. Her yazıcı için: ilk okuma tarihi + ilk okumadaki ham sayaç (= izleme öncesi geçmiş dahil, ömür boyu toplam), son okuma tarihi + güncel ham sayaç (= bugüne kadarki ömür boyu toplam), ve ikisi arası fark (izleme süresince basılan).
- Sınır: eğer yazıcı sayacı daha önce (bizim ilk okumamızdan önce) manuel sıfırlanmışsa, "ilk okumadaki sayaç" o sıfırlamadan sonraki toplamı gösterir — yazıcının TÜM ömrü boyunca bastığı mutlak sayı garanti edilemez, sadece cihazın kendi dahili sayacının o anki değeri.

### Not (2026-09-03) — Marka bazlı tanılama + gerçek yazıcı marka testleri
- Kullanıcı ofisteki markaları verdi: "IT" (.205) = **Olivetti**, ayrıca **Samsung SL-M4075FX** (.206) test edildi.
- `SnmpService.DiagnoseAsync`'e marka tespiti eklendi: `sysDescr`/`sysName` içinde anahtar kelime arayan `DetectBrand`. Marka-özel OID adayları (şu an sadece HP'ninkiler kaldı) SADECE o marka tespit edildiğinde denenir/gösterilir; başka markanın OID'leri hiç görünmez (kullanıcı: "Olivetti markasında HP verisi görmek istemiyorum").
- Gerçekte test edildi: hem Olivetti (.205, sysDescr boş döndüğü için marka tespit edilemedi ama sorun değil) hem Samsung (.206, marka doğru tespit edildi) için **standart `prtMarkerLifeCount` zaten doğru sonucu verdi** (sırasıyla 6865, 43212). Tahmini/doğrulanmamış Samsung private OID'i (`1.3.6.1.4.1.236...`) gerçek cihazda boş döndüğü için koddan kaldırıldı. Olivetti için zaten baştan private OID eklenmemişti (bilinen/doğrulanmış yok, Kyocera tabanlı motorlar standart MIB'i tam destekliyor).
- Sonuç: artık marka-özel OID listesi sadece HP içeriyor, o da yalnızca HP tespit edildiğinde gösteriliyor — pratikte şu ana kadarki tüm gerçek yazıcılarda standart OID tek başına yeterli.

### Not (2026-09-03) — Gerçek ihtiyaç: aylık rapor, saatlik değil
- Kullanıcı gerçek iş akışını netleştirdi: firmaya ayda bir (her ayın 1'i civarı) o ay ne kadar basım yapıldığının raporu atılıyor — saatlik/anlık izlemeye hiç gerek yok.
- `PollingIntervalMinutes` test değeri olan 1'den **1440'a (günde 1 kez)** çekildi (`appsettings.json`). Aylık toplam için fazlasıyla yeterli.
- Aylık rapor için yeni bir sayfa gerekmiyor: **`/Report/Summary`** zaten tarih aralığı alıp "Dönem toplamı" (yazıcı bazlı + genel) gösteriyor — ayın 1'i → ay sonu (ya da bugün) aralığı girilerek doğrudan kullanılabilir.
- **Saatlik rapor sayfası kaldırıldı**: `ReportController.Index()`, `Views/Report/Index.cshtml`, `Models/ReportViewModel.cs` (`ReportViewModel`, `PrinterDayReport`) silindi. Nav'dan "Saatlik Rapor" linki çıkarıldı; diğer sayfalardaki "Saatlik rapora dön" linkleri `Report/Summary`'ye yönlendirildi. `/Report` artık sadece `Summary` ve `Lifetime` action'larını içeriyor.

### Not (2026-09-03) — GitHub'a atmadan önce IP gizliliği
- Repo henüz git değildi; `.gitignore` eklendi (`bin/`, `obj/`, `.vs/`, `yazicitakip.db*`, `appsettings.*.Local.json`, `appsettings.Production.json`).
- `appsettings.json`'daki gerçek yazıcı IP'si (`192.168.150.205`, "IT") **kaldırıldı**, `Printers` dizisi tekrar boş — bu dosya git'e commit edilecek, şirket içi IP burada durmamalı.
- Gerçek yazıcılar artık config'e hiç ihtiyaç duymadan `/Yazıcılar` sayfasından doğrudan DB'ye ekleniyor (bkz. Yapılacaklar madde 6 altındaki Printers UI notu) — DB dosyası `.gitignore`'da olduğu için bu IP'ler repoya hiç girmiyor. "IT" (.205) DB'de kalmaya devam ediyor (35+ okuma), sadece appsettings.json'dan çıkarıldı; veri kaybı yok (worker config'den silineni DB'den silmiyor).
- Henüz `git init`/ilk commit yapılmadı — kullanıcı sadece ignore dosyasını istedi, push adımı onayı bekleniyor.

### Not (2026-09-03) — Git init + GitHub push + README
- `git init` yapıldı, ilk commit atıldı, `main` dalı `https://github.com/mesuttekgoz0/Printer_Tracer` reposuna push edildi (remote'ta hazır "Initial commit"/README.md üzerine rebase edildi).
- `README.md` yeniden yazıldı: özellikler, teknoloji tablosu, `dotnet run` ile çalıştırma, `appsettings.json` yapılandırması, yazıcı ekleme, sınırlar. Şirket/staj bağlamı README'de geçmiyor (kullanıcı isteği).

### Not (2026-09-03) — Ömür Boyu Sayaç sayfası kaldırıldı
- Kullanıcı `/Report/Lifetime` sayfasını sildirdi. Silinenler: `Views/Report/Lifetime.cshtml`, `Models/LifetimeViewModel.cs` (`LifetimeViewModel`, `PrinterLifetimeRow`), `ReportController.Lifetime()` action'ı.
- Linkler kaldırıldı: `_Layout.cshtml` nav'daki "Ömür Boyu Sayaç", `Views/Report/Summary.cshtml`'deki "Ömür boyu sayaç" butonu, README'deki madde.
- `/Report` artık sadece `Summary` action'ını içeriyor. Ömür boyu sayaç bilgisi hâlâ `PrintReading.PageCount` ham değerinde mevcut, sadece ayrı sayfası yok.

### Not (2026-09-03) — Yeni yazıcıda anında ilk okuma
- `PrintersController.Create`, yazıcıyı DB'ye kaydettikten hemen sonra `ISnmpService.GetPageCountAsync` ile bir kez SNMP okuması yapıp başarılıysa ilk `PrintReading` kaydını oluşturuyor. Böylece kullanıcı 1440 dk'lık döngüyü beklemeden yazıcının başlangıç sayacını görüyor.
- Ulaşılamazsa yazıcı yine eklenir, TempData mesajı "ilk okuma yapılamadı" der; bir sonraki döngüde tekrar denenir. SNMP çağrısı POST içinde senkron (timeout ~5 sn, v1 fallback ile ~10 sn) — kabul edilebilir.
- `ISnmpService` singleton olduğu için controller'a doğrudan enjekte edildi.

### Not (2026-09-03) — Görünüm yenilendi (panel + koyu tema)
- Hazır ASP.NET şablon görünümü değiştirildi. Koyu lacivert/slate navbar (`site.css`'te CSS değişkenleri: `--app-navy`, `--app-accent` vb.), "YT" marka rozeti + "Yazıcı Takip" adı, aktif sekme vurgusu (`_Layout.cshtml` içinde `NavActive` helper'ı route'a bakıyor).
- Nav'dan "Home"/"Privacy" kaldırıldı; sekmeler: Panel · Yazıcılar · Günlük Özet · SNMP Tanılama. Gizlilik linki sadece footer'da. `<html lang="tr">`, başlık `… · Yazıcı Takip`.
- Ana sayfa artık dashboard: `HomeController.Index` `AppDbContext` alıp `DashboardViewModel` dolduruyor (kayıtlı yazıcı sayısı, bu ay / bugün basılan tahmini sayfa = ardışık okuma pozitif deltalarının toplamı, en son okuma zamanı, yazıcı bazlı kısa tablo). `Models/DashboardViewModel.cs` eklendi.
- `site.css` tamamen elden geçti (metric-card, kart/tablo/buton override'ları). Bootstrap korunuyor, sadece üzerine yazılıyor.
- Tarayıcıda 3 sayfa (Panel, Günlük Özet, Yazıcılar) kontrol edildi, tema tutarlı.

### Not (2026-09-04) — Periyodik okuma kapatıldı, elle "Sayaç Oku" eklendi
- Yönetici otomatik/periyodik SNMP isteği fikrini istemedi: "sisteme girerim, tek butonla tüm yazıcılara bir istek atarım, o anki sayacı alırım, önceki basımdan farkını görürüm" dedi.
- `PrinterMonitoringOptions.PollingEnabled` (bool, **varsayılan false**) eklendi; `appsettings.json`'a `"PollingEnabled": false`. `PrinterMonitorWorker` açılışta DB migration + config→DB yazıcı eşitlemesi + (Development) örnek veri seed'ini hâlâ yapıyor; ama `PollingEnabled=false` ise `PeriodicTimer` döngüsüne hiç girmeden `return` ediyor. `worker`'dan `ISnmpService` bağımlılığı kaldırıldı.
- Okuma mantığı `Services/PrinterReadingService.cs`'e taşındı (`ReadAllAsync` → tüm yazıcıları paralel oku, ulaşılabilenlere `PrintReading` ekle, yazıcı başına önceki sayaç/yeni sayaç/fark döndür = `Models/ManualReadResult.cs`). Hem worker hem yeni controller bunu kullanıyor. DI: `AddScoped<PrinterReadingService>()`.
- Yeni sayfa: `ReadingsController` (`/Readings`, nav'da "Sayaç Oku"). GET = yazıcı sayısı + son okuma zamanı + "Tüm yazıcıları şimdi oku" düğmesi. POST `ReadNow` = okumayı yapıp aynı view'da yazıcı bazlı fark tablosu gösterir (PRG değil, POST'tan doğrudan View — intranet aracı, kabul edilebilir). `Views/Readings/Index.cshtml`, `Models/ReadingsIndexViewModel.cs`.
- Panel başlığına "Sayaç oku" butonu eklendi ("Yazıcı ekle" outline'a çekildi).
- `/Report/Summary` ve Panel dashboard'u değişmeden çalışıyor — zaten ardışık okuma deltalarından hesaplıyorlar, okumaların elle olması fark etmiyor.
- Worker kodu tamamen silinmedi; ileride `PollingEnabled=true` ile tekrar açılabilir. Kullanıcı "sonra sileriz" dedi.
- Smoke test (2026-09-04): dev makinede gerçek 4 yazıcıya `ReadNow` çalıştı, 4/4 okundu, biri +18 sayfa farkla göründü. Bu test dev `yazicitakip.db`'sine 4 gerçek okuma kaydı yazdı (normal kullanımdakiyle aynı, zararsız).

### Not (2026-09-04) — Sayaç Oku ve Günlük Özet tabloları elden geçti
- **Sayaç Oku (`/Readings`)**: "Son okumalar" tablosu artık düğmeye basılmasa da sürekli duruyor (DB'den her yazıcının son 2 okuması → önceki sayaç / son sayaç / fark / zamanlar). Tab değiştirip dönünce kaybolmuyor. `ReadNow` artık **PRG**: POST → okur → TempData mesajı (`Success`/`Warning`/`Error`) → `RedirectToAction(Index)`. F5 tekrar okuma yapmıyor. `ReadingsIndexViewModel` yeniden yazıldı (`PrinterReadingStatus` satırları); `ManualReadResult` yalnızca serviste/worker'da ve flash mesajı için kullanılıyor.
- **Günlük Özet (`/Report/Summary`)**: `Summary(from, to, bool all=false)`. Yeni "Okumalar" kartı = tek tek SNMP okumaları (en yeni önce), her satırda bir önceki okumaya göre fark (`+N` / `0` / `–`). Varsayılan son 30 okuma; "Tümünü göster" ↔ "Sadece son okumalar" linki (`?all=true`, from/to korunur). Özet kartları `metric-card` stiline geçti; "Yazıcı sayısı" kartı "Okuma sayısı"na dönüştü. Gün×yazıcı grid'i sadeleşti (border kaldırıldı, sticky thead/tfoot, tema renkleri: `var(--app-accent)` / `var(--app-border)`). Boş-durum kontrolü `AnyData` yerine `ReadingsTotal == 0` (okuma var ama delta yoksa da sayfa doluyor). `DailySummaryViewModel`'e `Readings` (`ReadingLogRow`), `ReadingsTotal`, `ShowAllReadings` eklendi.
- Test: 4 gerçek yazıcıya `ReadNow` PRG akışı (302→flash), Summary `?all=true` (30↔179 okuma), fark sütunu `+18/+20/0/–` doğrulandı.

### Not (2026-09-04) — Açılışta/otomatik hiç okuma yok, tamamen elle
- Kullanıcı: "program her açıldığında otomatik bir sayaç okuma varsa sil; okuma manuel olacak, program çalışır çalışmaz otomatik atmasın." Artık `PrintReading` yaratan **tek yol** `/Readings` sayfasındaki "Tüm/Seçili yazıcıları oku" düğmeleri.
- **`PrintersController.Create` içindeki anında ilk-okuma kaldırıldı** (bkz. eski not "Yeni yazıcıda anında ilk okuma" — artık geçersiz). Yazıcı eklemek yalnızca DB insert; `ISnmpService` bağımlılığı controller'dan çıkarıldı. Başarı mesajı ve `Views/Printers/Index.cshtml` yardım metni "ilk sayaç için Sayaç Oku'ya gidin" diyor.
- **Worker açılışta okuma yapmıyordu zaten** (`PollingEnabled=false`); ek olarak polling AÇIK olsa bile artık açılışta okumuyor: `do/while` → `while` yapıldı ve 3 sn'lik "ilk okuma" gecikmesi silindi, yani ilk periyodik okuma bir tam `PollingIntervalMinutes` sonra. "her açıldığında yeni okuma" davranışı bu yüzden hiçbir senaryoda oluşmuyor.
- Test: art arda 2 kez restart → `PrintReadings` sayısı 191'de sabit, `max(TimestampUtc)` değişmedi. Yazıcı ekleme (`192.0.2.77`) → 0 okuma yaratıldı, sonra silindi.

### Not (2026-09-04) — Seçili yazıcı okuma + Günlük Özet'ten günlük hesaplar kaldırıldı
- **Sayaç Oku (`/Readings`)**: satır başına checkbox + başlıkta "tümünü seç" (`chkAll`, küçük inline JS, `@@section Scripts`). Yeni **"Seçili yazıcıları oku"** düğmesi (`form="selReadForm"` ile tablo formuna bağlı, `mode=selected` hidden). Hiç seçim yokken bu düğme → TempData Error "Önce ... seçin". "Tüm yazıcıları şimdi oku" düğmesi eskisi gibi üstteki kartta, ayrı formda (checkbox yok → hepsi). `PrinterReadingService.ReadAllAsync(IReadOnlyCollection<int>? printerIds = null, CancellationToken)` — id listesi verilirse `Where(p => printerIds.Contains(p.Id))`, boş/null ise hepsi. Worker çağrısı `ReadAllAsync(printerIds: null, ct)`. `ReadNow(int[] printerIds, string? mode)`. Flash mesajında "seçili yazıcı" / "yazıcı" ayrımı.
- Tablolar: "Son okumalar" ve Günlük Özet "Okumalar" tabloları `table-bordered` + iç scroll (`max-height:60vh` / `34rem`, `overflow-y:auto`, sticky thead).
- **Günlük Özet'ten günlük agregasyonlar TAMAMEN kaldırıldı** (kullanıcı isteği: "fark tablosu güzel, günlük hesaplamaları kaldır"). Silinenler: gün×yazıcı grid tablosu, günlük toplam bar grafiği, "Günlük ortalama" + "En yoğun gün" kartları. `DailySummaryViewModel`'den `Days`, `DailyAverage`, `BusiestDay`, `AnyData` ve `DailySummaryRow` sınıfı kaldırıldı. `ReportController.Summary` artık sadece okuma logunu + `GrandTotal` (pozitif farkların toplamı) + `PrinterTotals` üretiyor.
- Günlük Özet'te kalanlar: tarih aralığı filtresi + 7/30/90 gün kısayolları, 2 kart (Dönem toplamı / Okuma sayısı), "Yazıcı bazında dönem toplamı" tablosu, "Okumalar" logu (fark sütunlu, Tümünü göster/Sadece son toggle). Sayfa/route adı hâlâ "Günlük Özet" / `Report/Summary` (yeniden adlandırılmadı).
- Test: seçili okuma (`mode=selected`+`printerIds` → "1/1 seçili yazıcı okundu"), boş seçim guard'ı, Summary'de günlük öğelerin yokluğu + fark logunun (`+20/+18/+40/0`) varlığı doğrulandı.

### Not (2026-09-04) — Panel (dashboard) kaldırıldı, "Günlük Özet" → "Rapor"
- **Dashboard/Panel sayfası silindi.** İçeriği (yazıcı sayısı, bu ay/bugün basılan, son okuma, yazıcı tablosu) artık Sayaç Oku (güncel durum) + Rapor (toplamlar) ile tamamen karşılanıyordu; "bugün basılan" metriği elle okumayla anlamsızdı. Silinenler: `HomeController.Index` action'ı (+ `AppDbContext`/`ILogger` bağımlılıkları), `Views/Home/Index.cshtml`, `Models/DashboardViewModel.cs`. `HomeController`'da yalnızca `Privacy` + `Error` kaldı (`/Home/Error` exception handler'da hâlâ kullanılıyor).
- **Açılış sayfası artık Sayaç Oku.** `Program.cs` default route `{controller=Readings}/{action=Index}`. Navbar markası da Readings/Index'e gidiyor.
- **"Günlük Özet" → "Rapor" olarak yeniden adlandırıldı** (kullanıcı: "ismi saçma, karar sana"). Sadece görünen isim değişti: nav etiketi, sayfa `<h1>`/`<title>`, çapraz link metinleri, doc comment'ler. **Route/controller/action aynı**: `ReportController.Summary`, `/Report/Summary`. `DailySummaryViewModel` adı da korundu (yeniden adlandırma churn'ü yok).
- Rapor varsayılan tarih aralığı "son 30 gün" yerine **içinde bulunulan ayın 1'inden bugüne** (`fromDay = from ?? new DateOnly(toDay.Year, toDay.Month, 1)`) — aylık rapor için direkt doğru görünüm.
- Nav sırası: **Sayaç Oku · Rapor · Yazıcılar · SNMP Tanılama**. (Önceki notlardaki "Panel · … · Günlük Özet" sıralaması geçersiz.)
- Test: `/` → Sayaç Oku (200), `/Home/Index` → 404, `/Home/Error` + `/Home/Privacy` → 200, Rapor varsayılan aralık `2026-09-01 → bugün`, build temiz.

### Not (2026-09-04) — Sayaç Oku tablosuna Marka / Model sütunu
- `Printer.Model` (string?, MaxLength 250) eklendi. Migration: `Data/Migrations/20260904084545_AddPrinterModel` (worker açılışta `MigrateAsync` ile uygular).
- `ISnmpService.GetModelAsync(ip)` → `sysDescr` (OID `1.3.6.1.2.1.1.1.0`) tek GET, config sürümü + diğerine fallback, çok satır/boşluk tek satıra indirilip 250 karaktere kısaltılır.
- `PrinterReadingService.ReadAllAsync`: yazıcılar artık **tracked** çekiliyor; `Printer.Model` boşsa okuma sırasında paralel olarak model de sorgulanıyor, değiştiyse set edilip aynı `SaveChangesAsync` ile yazılıyor (`ChangeTracker.HasChanges()` ile gate). Model dolu olan yazıcıda tekrar sorgulanmaz (SNMP trafiğini ikiye katlamamak için).
- `PrinterReadingStatus.Model` + `ReadingsController.BuildAsync` → `Views/Readings/Index.cshtml`'de "Yazıcı"dan sonra "Marka / Model" sütunu (uzun değer `text-truncate` + `title` tooltip). Model yoksa "–".
- Gerçek testte alınan değerler: Olivetti'ler `"OLIVETTI Printing System"`, Samsung `"Samsung SL-M4075FX; V4.00.01.47 …"`, Kyocera `"KYOCERA Document Solutions Printing System"`. Olivetti sysDescr'ı model numarası vermiyor (sadece marka) — vendor-özel OID olmadan bundan fazlası alınamıyor, kabul edilebilir.

### Not (2026-09-04) — Hakediş (sayfa-başı fatura belgesi) modülü
- İhtiyaç: yönetici yazıcı markasının servis/bayi firmasına dönemsel "önceki sayaç / şimdiki sayaç / fark" belgesi (hakediş) gönderip sayfa-başı faturalanıyor. Sayaç Oku'da seçilen yazıcılar için tek tıkla belge üretilmeli.
- **Çift-tıklama sorununun çözümü (kullanıcı kararsızdı, karar bize bırakıldı):** hakediş "önceki sayaç"ı bir önceki OKUMADAN değil, bu yazıcının **bir önceki HAKEDİŞ satırındaki `CurrentCounter`**'dan alınır. Dönem sınırını hakediş anları belirler; fazladan/yanlış okuma hakedişi bozmaz. İlk hakedişte önceki sayaç = yazıcının ilk okuması (create ekranında elle düzeltilebilir — "ilk hakediş" uyarısı gösterilir).
- Yeni tablolar: `Hakedis` (Id, Number "YYYY-NNNN", CreatedUtc, PeriodStart/End DateOnly, Note) + `HakedisLine` (HakedisId, PrinterId nullable, PrinterName/Model **kopya**, PreviousCounter, CurrentCounter, Pages nullable). Migration `20260904090414_AddHakedis`. `AppDbContext`: `Hakedisler`, `HakedisLines` DbSet'leri + Number unique + PrinterId index.
- `HakedisController`: `Index` (liste), `Create` [POST, Sayaç Oku'dan seçili `printerIds`] → inceleme/düzeltme ekranı, `Save` [POST] → numara ver + satırları dondur + Details'e git, `Details` (yazdırılabilir belge, `@@media print` ile nav/footer/butonlar gizli), `Csv` (UTF-8 BOM + `;` ayraç, TR Excel uyumlu). `Save` ad/model'i formdan değil güncel `Printers` kaydından alır (HTML-encode round-trip ve güven sorunu yok).
- Numara: `{PeriodEnd.Year}-{o yıl içindeki hakediş sayısı + 1:D4}`. Tek kullanıcılı intranet için yeterince güvenli.
- `Configuration/HakedisOptions.cs` (`appsettings.json` → `Hakedis:FromCompany` / `ToCompany`, boş = belgede gösterilmez). `Program.cs`'te `Configure<HakedisOptions>`.
- UI: Sayaç Oku "Son okumalar" kart başlığına "Hakediş oluştur" butonu (`form="selReadForm" formaction="/Hakedis/Create"` — checkbox'ları aynı forma bağlı). Nav: **Sayaç Oku · Rapor · Hakedişler · Yazıcılar · SNMP Tanılama**. Views: `Hakedis/{Index,Create,Details}.cshtml`.
- Test (gerçek 4 yazıcı): seçili → Create (4 satır, hepsi "ilk hakediş") → Save (`2026-0001`, dönem 01–04.09, toplam 266) → Details + `/Hakedis/Csv/2` doğrulandı; TR karakterler (`İ`) DB'de doğru (UTF-8), CSV'de `;` içeren model alanı tırnaklanıyor. Test kayıtları sonra temizlendi.
- Henüz yok: hakediş silme/düzenleme action'ı (liste sadece Aç), PDF'i tarayıcının "PDF kaydet"ine bırakıyoruz.

### Not (2026-09-07) — Ölü kod / şablon artığı temizliği
- Silinen dosyalar: `Views/Shared/_ValidationScriptsPartial.cshtml` (hiçbir view render etmiyordu), `wwwroot/lib/jquery-validation/` + `wwwroot/lib/jquery-validation-unobtrusive/` (yalnız o partial kullanıyordu; formların hepsi ya `method="get"` ya da unobtrusive validation'sız düz POST), `Views/Shared/_Layout.cshtml.css` (ASP.NET şablonundan kalan `.box-shadow`/`.nav-pills`/`accept-policy` vb. stiller — gerçek tema `wwwroot/css/site.css`'te), `Views/Home/Privacy.cshtml` (şablon "gizlilik politikası" placeholder sayfası; intranet aracına gereksiz).
- `HomeController`: `Privacy()` action'ı kaldırıldı; sadece `Error()` kaldı (`UseExceptionHandler("/Home/Error")` hâlâ kullanıyor).
- `_Layout.cshtml`: footer'daki "Gizlilik" linki ve `~/YaziciTakip.styles.css` `<link>`'i (artık hiç `*.cshtml.css` yok) kaldırıldı.
- Kullanılmayan view-model üyeleri silindi: `ManualReadRow.IsFirstReading`, `DailySummaryViewModel.DayCount`, `ReadingsIndexViewModel.HasAnyReading` (satır tipindeki `HasReading` kullanılıyor, o kaldı).
- Build sonrası: 0 uyarı / 0 hata. `SampleDataSeeder` ve `PrinterMonitorWorker` (açılış migration'ı yaptığı için) bilerek bırakıldı.

### Not (2026-09-09) — Yazıcı türü (siyah-beyaz / renkli) alanı
- `Printer.ColorType` (`PrinterColorType` enum: `Unspecified=0` / `Monochrome=1` / `Color=2`, `Models/Printer.cs` içinde + `ToDisplayText()` uzantısı → "Belirtilmemiş" / "Siyah-Beyaz" / "Renkli"). **Elle** belirlenir, SNMP'den okunmaz (kullanıcı kararı: renk tespiti markaya göre tutarsız).
- Migration: `Data/Migrations/20260909055839_AddPrinterColorType` — `ColorType INTEGER NOT NULL DEFAULT 0`. Mevcut 4 yazıcı 0 (Belirtilmemiş) oldu. Worker açılışta `MigrateAsync` ile uygular; dev DB'ye `dotnet ef database update` ile de uygulandı.
- `PrintersController`: `Create(... PrinterColorType colorType)` ekleme formundan alır; yeni `SetType(int id, PrinterColorType colorType)` [POST, PRG] satır içi `<select onchange="this.form.submit()">` ile türü değiştirir. `PrinterListRow.ColorType` eklendi.
- `Views/Printers/Index.cshtml`: "Yeni yazıcı ekle" formuna Tür `<select>`'i, tabloya "Tür" sütunu (satır içi auto-submit select). Boş-durum `colspan` 6→7.
- `ReadingsController.BuildAsync` → `PrinterReadingStatus.ColorType`; `Views/Readings/Index.cshtml` "Son okumalar" tablosunda "Marka / Model"den sonra "Tür" sütunu (badge; Unspecified'da "–").
- Smoke test (dev, 4 gerçek yazıcı): `/Printers` + `/Readings` 200; `SetType/1 → colorType=2` → 302, DB'de `ColorType=2`, Readings'te "Renkli" badge.
- (2026-09-09) Kullanıcı isteğiyle mevcut 4 yazıcıya rastgele tür atandı: IT=Renkli, Elektrik Sİstemleri=Siyah-Beyaz, İK=Renkli, Muhasebe=Siyah-Beyaz.

### Not (2026-09-09) — Tedarikçi + Fiyat Listesi + tedarikçi-bazlı hakediş
- **Yeni tablolar** (`Data/Migrations/20260909062532_AddTedarikciAndPricing`):
  - `Tedarikci` (Id, Ad, Not). `Models/Tedarikci.cs`.
  - `FiyatListesi` (Id, ListeAdi, Tarih `DateOnly`, TedarikciId) + `FiyatSatiri` (Id, FiyatListesiId, ColorType, SayfaBasiFiyat `decimal(18,4)`). Header + tür başına bir satır. `Models/FiyatListesi.cs`; `FiyatListesi.FiyatBul(tur)` yardımcı.
  - `Printer.TedarikciId` (nullable FK, `OnDelete SetNull`). `Hakedis.TedarikciId` + `TedarikciAd` (donmuş kopya). `HakedisLine.ColorType` + `UnitPrice decimal(18,4)` + `Amount decimal(18,2)` (hepsi donmuş).
  - `AppDbContext`: `Tedarikciler`, `FiyatListeleri`, `FiyatSatirlari` DbSet'leri; FiyatListesi→Tedarikci ve FiyatSatiri→FiyatListesi cascade; `IX_FiyatListeleri (TedarikciId, Tarih)`.
- **Fiyat listesi seçimi**: tedarikçinin `Tarih <= dönem bitişi` olan en güncel listesi; öyle liste yoksa en erken listesi (`HakedisController.ResolvePriceListAsync`). Eski hakedişler kendi fiyatını korur (satıra kopya).
- **`TedarikciController`** (yeni, nav'da "Tedarikçiler"): `Index` (liste + ekle), `Details` (bilgi düzenle + bağlı yazıcılar + fiyat listeleri + "yeni fiyat listesi" formu: Siyah-Beyaz/Renkli ₺/sayfa), `Create`/`Edit`/`Delete`, `AddPriceList`/`DeletePriceList`. Fiyat metni kültürden bağımsız çözülür (`ParsePrice`: "0,15" ve "0.15" kabul, `internal static` — `HakedisController` de kullanıyor).
- **Yazıcı–tedarikçi**: `PrintersController.SetSupplier(id, tedarikciId?)` [POST, PRG]; Yazıcılar tablosunda "Tür"den sonra "Tedarikçi" auto-submit `<select>` sütunu + ekleme formunda. `PrintersController.Index` artık `PrintersIndexViewModel { Printers, Suppliers }` döndürüyor (view `@model` değişti). `Create` opsiyonel `tedarikciId` alıyor.
- **Hakediş akışı değişti** (yazıcı seçimi kalktı, tedarikçi seçimi geldi):
  - `HakedisController.Create(int[] printerIds)` → **`Create(int tedarikciId)`**. Önce `New()` [GET] = tedarikçi seçme ekranı (`Views/Hakedis/New.cshtml`, `TedarikciSecRow`), yazıcısı olmayan tedarikçi seçilemez.
  - `Create`: tedarikçinin TÜM yazıcıları için satır üretir; okuması olmayan → `Skipped`; tür `Unspecified` ya da fiyat 0 → `Warnings`. `HakedisCreateViewModel`'e `TedarikciId/Ad`, `FiyatListesiBilgi`, `Warnings`, `TotalAmount`; `HakedisCreateRow`'a `ColorType`, `UnitPrice`, `Amount`, `UnitPriceInvariant` (gizli alan için nokta-ondalık).
  - `Views/Hakedis/Create.cshtml`: başlıkta tedarikçi + fiyat listesi; tabloya Tür / Sayfa başı ₺ / Tutar sütunları; JS canlı hesap tutarı da günceller (`data-unit` satır özniteliğinden). Gizli `TedarikciId`, satır başına `ColorType` + `UnitPrice` (invariant).
  - `Save`: `HakedisSaveModel`'e `TedarikciId/Ad`, `HakedisSaveRow`'a `ColorType` + `UnitPrice` (string, `ParsePrice`). Tutar sunucuda yeniden hesaplanır (`Round(pages*unit, 2)`), tedarikçi adı güncel kayıttan dondurulur.
  - `Details.cshtml`: üstte "Tedarikçi" bloğu; tabloya Tür / Sayfa Başı ₺ / Tutar + TOPLAM tutar. `Csv`: Tedarikçi satırı + Tür/Fiyat/Tutar sütunları + toplam tutar.
  - `Index.cshtml`: "Sayaç Oku"dan gelen "Hakediş oluştur" düğmesi **kaldırıldı**; yerine Hakedişler sayfasında "+ Yeni hakediş" (→ `New`). Listeye Tedarikçi + Toplam tutar sütunları.
- **`Views/Readings/Index.cshtml`**: "Hakediş oluştur" düğmesi kaldırıldı (checkbox'lar "Seçili yazıcıları oku" için duruyor).
- Para birimi: ₺; birim fiyat 4 haneye kadar, tutar 2 hane, tr-TR. Ondalık bind sorununu önlemek için fiyatlar POST'ta string alınıp invariant çözülüyor.
- Smoke test (dev): tedarikçi + fiyat listesi (Mono 0,15 / Renkli 0,75) oluştur → 2 yazıcı (IT, İK; ikisi Renkli) ata → `New` → `Create` (2 satır, birim 0,75) → `Save` (`2026-0003`, IT 499×0,75=374,25 + İK 60×0,75=45,00 = 419,25 ₺) → Details + CSV doğrulandı; TR karakter (`İ`) UTF-8 doğru. Tüm test kayıtları sonra silindi (tedarikçi/liste/hakediş), yazıcı tür atamaları korundu.
### Not (2026-09-09) — Eksik giderme (fiyat listesi düzenleme + hakediş silme + belge sadeleştirme)
- **Fiyat listesi düzenleme**: `TedarikciController.EditPriceList(id, listeAdi, tarih?, fiyatMono?, fiyatColor?)` [POST]. Satır yoksa oluşturur, varsa günceller (`SetPrice` yerel fonksiyonu). `Views/Tedarikci/Details.cshtml`'deki fiyat listesi tablosu artık satır içi düzenlenebilir: her satır boş `<form id="pl-{id}">` + `form="pl-{id}"` ile bağlı input'lar (Printers rename / Readings selReadForm ile aynı desen). Input değerleri invariant (`0.15`), `ParsePrice` virgül/nokta kabul ediyor.
- **Hakediş silme**: `HakedisController.Delete(id)` [POST] — `Hakedis` sil (satırlar cascade). Silinince o yazıcıların bir sonraki hakedişi "önceki sayaç"ı bir önceki hakedişten alır (silme temiz geri alınır). "Sil" düğmesi: `Views/Hakedis/Index.cshtml` satır sonu + `Details.cshtml` üst araç çubuğu, ikisi de `confirm()`.
- **Belge sadeleştirme**: `Views/Hakedis/Details.cshtml` — hakedişin `TedarikciAd`'ı varsa `appsettings.json`'daki `Hakedis:ToCompany` ("Gönderilen" firma) satırı gösterilmez (tedarikçi zaten alıcı). `FromCompany` ("Düzenleyen") aynen duruyor.
- Kullanıcı isteğiyle **bırakılanlar**: dev DB'deki eski 2 tedarikçisiz/0-tutar hakediş kaydı (takım liderine gösterilecek).
- Smoke test (dev): EditPriceList (ad+tarih+iki fiyat, "0.12" ve "0,66" kabul), Tedarikci Delete (fiyat listeleri cascade), Hakedis Delete (satırlar cascade, 302→Index), Details'te "Gönderilen" gizli + tek "Tedarikçi" bloğu doğrulandı. Test kayıtları silindi, orijinal 2 hakediş korundu.
- Henüz yok: hakediş **düzenleme** (tasarım gereği donmuş; sadece sil), tedarikçi bazlı rapor. Arayüz güzelleştirmesi kullanıcıyla sonraki adımda yapılacak.

### Not (2026-09-09) — Tür artık ayrı tablo (`Tur`) + FK; ID'ler yazıcı listesinde görünüyor
- **`PrinterColorType` enum kaldırıldı**, yerine **`Tur` tablosu** (`Models/Tur.cs`): `Id`, `Ad`. Açılışta `HasData` ile seed: `1 = Siyah-Beyaz`, `2 = Renkli` (eski enum değerleriyle birebir aynı → veri korundu). CRUD yok (sabit lookup); `Tur.Belirtilmemis` sabiti null tür için gösterim metni.
- FK'ler: `Printer.TurId` (nullable, `SetNull`), `FiyatSatiri.TurId` (zorunlu, `Restrict`). `Printer.TedarikciId` FK zaten vardı (`SetNull`) — değişmedi. `HakedisLine`: `ColorType` → `TurId` (nullable, FK **değil** — donmuş snapshot) + `TurAd` (donmuş string, belgede bu gösterilir).
- Migration `20260909081205_AddTurTable` — **elle düzenlendi**: `AddColumn TurId` → `Sql("UPDATE Printers SET TurId = ColorType WHERE ColorType IN (1,2)")` → `DropColumn ColorType` sırası korunarak yazıcı türleri kaybolmadan taşındı. HakedisLines için de ColorType→TurId/TurAd backfill Sql'i eklendi. (EF "table rebuild pending" uyarısı verdi ama sonuç doğru: `dotnet ef database update` sonrası IT=2, İK=2, Elektrik=1, Muhasebe=1.)
- `FiyatListesi.FiyatBul(PrinterColorType)` → `FiyatBul(int turId)`. `TedarikciController.AddPriceList`/`EditPriceList` artık sabit `fiyatMono`/`fiyatColor` yerine paralel `int[] turId` + `string[] fiyat` dizileri alıyor; `Views/Tedarikci/Details.cshtml` `@model` → `TedarikciDetailsViewModel { Tedarikci, List<Tur> Turler }`, fiyat form/tablo sütunları `Turler` üzerinde döngüyle üretiliyor.
- `Views/Printers/Index.cshtml`: tür `<select>`'i `Model.Types` (Tur listesi) üzerinden. `SetType(int id, int? turId)`, `Create(... int? turId, int? tedarikciId)`. (Not: kısa süre "TurId: N" / "TedarikciId: N" metni eklenmişti, kullanıcı istemedi — FK sadece DB tarafında, arayüzde id gösterilmiyor; geri alındı.)
- Diğer view/VM: `PrinterReadingStatus`/`HakedisCreateRow`/`HakedisSaveRow` `ColorType` → `TurId` (+ `TurAd`). `Views/{Readings,Hakedis/Create,Hakedis/Details}` güncellendi.
- Smoke test (dev): migration veri koruması doğrulandı; Printers/Readings/Tedarikci/Hakedis sayfaları 200; dinamik fiyat formu (`turId=1 fiyat=0,20`, `turId=2 fiyat=1,00`) → FiyatSatiri FK'li kaydedildi; hakediş Create satır başına doğru `TurId`/`UnitPrice` + toplam. FK integrity check temiz.

### Not (2026-09-09) — "Classical" görünüme geçiş (tasarım referansı: masaüstü "Printer Tracing Website")
- Kullanıcı masaüstündeki `Printer Tracing Website/` klasörünü (Claude Design export: `classical.css` + `.dc.html`'ler) referans verdi; "şu anki halini bozma, sadece tasarımı ona benzet, tedarikçiler/hakediş gibi eksik sekmeleri sen uydur" dedi.
- **Sadece CSS + layout `<head>` değişti; controller/model/view mantığı ELLENMEDİ.** Bootstrap korunuyor (grid + collapse navbar için); `wwwroot/css/site.css` tamamen yeniden yazıldı: Classical token'ları (`--color-bg #f3f2f2`, `--color-text #201f1d`, tek altın vurgu `--color-accent #b68235`, `--font-heading` Cormorant Garamond, `--font-body` Lora, `--space-*`, `--radius-*`) + Bootstrap görsel katmanını ezme.
- `_Layout.cshtml`: Google Fonts `<link>` (Cormorant Garamond + Lora) eklendi. Markup değişmedi.
- Uygulanan Classical kuralları: koyu lacivert tema → sıcak beyaz zemin; dolu butonlar → **altın konturlu** (`.btn-primary` artık outline); kartlar şeffaf + ince çizgi kenar, gölge yok; tablo başlıkları küçük harf-aralıklı uppercase, sadece satır çizgileri; navbar açık zemin + alt hairline, aktif sekmede altın alt-çizgi; `.alert` kutuları dolu değil kontur+hafif ton; `.badge` küçük tag; footer artık `static` (eski `position:absolute` + `body margin-bottom` kaldırıldı).
- Eksik sekmeler (Tedarikçiler, Hakedişler, Rapor, SNMP Tanılama) referansta yoktu → aynı token/sınıflarla otomatik aynı görünüme oturuyorlar (hepsi Bootstrap `.card`/`.table`/`.btn` kullanıyor).
- Build temiz. Tarayıcı eklentisi bağlı olmadığı için görsel doğrulama yapılamadı; tüm sayfalar 200 dönüyor, `site.css` + fontlar servis ediliyor. Kullanıcı çalıştırıp bakacak, ince ayar sonra.

### Not (2026-09-09) — Koyu tema + navbar'da tema düğmesi
- `site.css`: `:root[data-theme="dark"]` altında tüm Classical token'ları koyu palete (sıcak siyah `#1a1815` zemin, `#ece7df` metin, altın vurgu `#d1a05c`) yeniden tanımlandı. JS'siz/ilk boya için `@media (prefers-color-scheme: dark)` fallback'i (`:root:not([data-theme])`) aynı değerlerle. `color-scheme` de set ediliyor (native form/date/scrollbar).
- Koyu temada `.form-select` chevron'u ve `.navbar-toggler-icon` açık renkli data-uri ile override. Satır içi `#f8fafc` thead/tfoot'lar zaten `.table thead[style*="background"] { background: var(--color-neutral-100) !important }` ile temaya uyuyor.
- `_Layout.cshtml` `<head>`: FOUC önleyen inline script — `localStorage['yt-theme']` yoksa `matchMedia('(prefers-color-scheme: dark)')`, sonra `<html data-theme="...">`.
- Navbar'da `#themeToggle` düğmesi (brand'den sonra, `ms-auto` ile sağa; ≥768px'de `order:5` ile en sağa). İçinde ay/güneş SVG'leri; `:root[data-theme="dark"] .theme-toggle .icon-sun{display:block}` ile ikon değişiyor.
- `wwwroot/js/site.js`: düğmeye tıkla → `data-theme` flip + `localStorage['yt-theme']` yaz. (site.js body sonunda yükleniyor, DOM hazır.)
- Yalnızca CSS + layout + site.js; view/controller/model değişmedi. Build temiz, sayfalar 200. Görsel doğrulama kullanıcıda.

### Not (2026-09-09) — Font değişti: serif başlık + sans metin
- Kullanıcı Cormorant Garamond + Lora (ikisi de serif) ikilisini beğenmedi, "serif başlık + sans metin" seçti.
- `--font-heading: "Newsreader"` (editoryal serif, ekran için), `--font-body: "Inter"` (sans). `_Layout.cshtml` Google Fonts linki güncellendi (`Newsreader` opsz 6..72 + `Inter` 400-700).
- Serif kalanlar: `h1-h6`, `.card-header`, `.card-title`, `.metric-value`, `.navbar-brand`. Sans'a geçenler: `.btn` (weight 500), `.app-navbar .nav-link` (0.92rem, weight 500), body/tablo/form (zaten `--font-body`).
- Boyutlar değişmedi.

### ⚠️ Veri kaybı (2026-09-09) — test temizliğim kullanıcı verisini sildi
- Bu oturumda test sırasında `DELETE FROM Tedarikciler` / `DELETE FROM Hakedisler WHERE Id=<MAX>` gibi **kapsamı geniş temizlik** komutları çalıştırdım. Sonuç:
  1. Kullanıcının eklediği bir **tedarikçi** silindi (kullanıcı "en son eklediğim tedarikçi gözükmüyor" dedi).
  2. Eski 2 hakedişten **`2026-0002`** silindi (kullanıcı "tedarikçisiz olanları bırak, takım liderine göstereceğim" demişti). Geri getirilemedi (WAL yok). Sadece `2026-0001` kaldı.
- **Bundan sonra:** test verisi temizliği yalnızca bu oturumda YARATTIĞIM spesifik id'lerle yapılacak; tablo bazlı toplu DELETE yok.

### Not (2026-09-09) — Next.js frontend'e geçiş başladı (Faz 1)
- **İstek**: firma frontend'i Next.js istiyor. Onların stack'i de backend .NET + frontend Next.js; "bağlantı API'sini" kendileri yazacak. Yani benim işim: .NET backend'e temiz JSON API uçları + bunları tüketen Next.js uygulaması. Giriş yok, SSO yok, kendi tasarım sistemleri sorulmadı (kullanıcı "boş ver" dedi).
- **Mimari**: mevcut Razor MVC bozulmadan duruyor; API onun **yanına** eklendi. Next.js ayrı origin (`web/`, port 3000), .NET API'yi (`:5239/api/**`) CORS ile çağırıyor.
- **.NET tarafı**:
  - `Program.cs`: `AddCors("frontend")` (origin `appsettings → Cors:Origins`, yoksa `http://localhost:3000`), `AddSwaggerGen` + dev'de `/swagger`. `app.MapControllers()` eklendi (Razor route'u da duruyor). `Swashbuckle.AspNetCore` 7.2.0 paketi.
  - `Controllers/Api/PrintersApiController.cs` (`[ApiController]`, `api/printers`): `GET` (liste, tür/tedarikçi/okuma özetiyle), `POST` (ekle, 400/409 `ProblemDetails`), `PATCH {id}/name|type|supplier`, `DELETE {id}`. DTO'lar dosya sonunda `record`.
  - `Controllers/Api/LookupsApiController.cs` (`api/lookups`): `{ turler:[{id,ad}], tedarikciler:[{id,ad}] }` — form açılır listeleri için.
  - İş mantığı mevcut Razor controller'larıyla aynı; kod paylaşılmadı (kopya), ileride servise çekilebilir.
- **Next.js `web/`** (create-next-app, Next 16 App Router, TS, Tailwind YOK, `@/*` alias):
  - `app/globals.css`: Razor'daki Classical temanın bağımsız kopyası (Bootstrap yok, düz sınıflar: `.btn`, `.card`, `.table`, `.input/.select`, `.tag`, `.alert`, `.nav`). Açık+koyu tema token'ları, `@media prefers-color-scheme` fallback.
  - `app/layout.tsx`: `next/font/google` ile Newsreader (`--font-heading`) + Inter (`--font-body`); FOUC önleyen inline tema script'i; `<Nav/>` + `.container` + footer.
  - `components/Nav.tsx` (client): nav linkleri + `usePathname` aktif + tema düğmesi (localStorage `yt-theme` + `data-theme`).
  - `lib/api.ts`: tek `fetch` sarmalayıcı; taban `NEXT_PUBLIC_API_BASE` (`.env.local` → `http://localhost:5239`); `ApiError` + `ProblemDetails.detail` mesajı.
  - `lib/types.ts`: `Printer`, `Option`, `Lookups`.
  - `app/yazicilar/page.tsx` + `components/PrintersClient.tsx`: Yazıcılar sayfası uçtan uca — liste + ekle formu + satır içi ad/tür/tedarikçi düzenleme + sil, hepsi API'ye. "Test et" şimdilik Razor `/Diagnostics`'e link. **Bu sayfa diğerleri için kalıp.**
  - `app/page.tsx` → `/yazicilar`'a redirect.
- **Test**: `next build` temiz (0 TS hatası); CORS preflight 204 + `Access-Control-Allow-Origin` doğru; API CRUD curl ile uçtan uca (400 bad-IP, 201 create, PATCH type→null / supplier→9, 204 delete) doğrulandı. Test yazıcısı silindi. Görsel doğrulama yok (tarayıcı eklentisi bağlı değil).
- **Çalıştırma**: bir terminalde `dotnet run` (kökte, :5239), başka terminalde `cd web && npm run dev` (:3000). `web/` ilk sefer `npm install`. `npm`'i kökte DEĞİL `web/` içinde çalıştır (kökte `package.json` yok).

### Not (2026-09-09) — Next.js Faz 2: tüm sayfalar taşındı
- **Yeni API controller'ları** (`Controllers/Api/`): `ReadingsApiController` (`GET /api/readings`, `POST /api/readings/read`), `ReportApiController` (`GET /api/report/summary?from&to&all`), `TedarikcilerApiController` (`GET/POST/PUT/DELETE /api/tedarikciler[/{id}]`, `POST /api/tedarikciler/{id}/fiyat-listeleri`) + `FiyatListeleriApiController` (`PUT/DELETE /api/fiyat-listeleri/{id}`), `HakedislerApiController` (`GET /api/hakedisler`, `.../tedarikci-secenekleri`, `POST .../taslak`, `POST /api/hakedisler`, `GET /api/hakedisler/{id}`, `GET .../{id}/csv`, `DELETE`), `DiagnosticsApiController` (`GET /api/diagnostics?ip&oid`). Hepsi mevcut Razor controller'larıyla aynı iş mantığı (kopya; ileride servise çekilebilir). Fiyatlar JSON `number` (invariant), kültür sorunu yok.
- **Next.js sayfaları** (`web/app/*`): `/yazicilar`, `/sayac-oku`, `/rapor`, `/tedarikciler`, `/tedarikciler/[id]`, `/hakedisler`, `/hakedisler/yeni` (tedarikçi seç → inceleme → kaydet, canlı tutar hesabı client'ta), `/hakedisler/[id]` (yazdırılabilir, `@media print`), `/snmp-tanilama` (`?ip=` ön-dolgu). `/` → `/yazicilar` redirect.
- `web/lib/`: `api.ts` (get/post/put/patch/del + `ApiError`), `types.ts` (tüm DTO'lar), `format.ts` (tr-TR sayı/para/tarih + `parseFiyat`).
- Her sayfa client component + `useEffect` fetch + mutasyon sonrası re-fetch deseni. Ortak stiller `app/globals.css`.
- **Test**: `next build` temiz (0 TS hatası, 10 route); tüm `/api/*` uçları curl ile doğrulandı (readings/report/tedarikciler/hakedisler/taslak/csv/diagnostics), dinamik route'lar (`/hakedisler/{id}`, `/tedarikciler/{id}`) 200. Görsel doğrulama yok (tarayıcı eklentisi yok).
- Kök dizindeki hatalı `package-lock.json` (kullanıcının kökte `npm install` denemesinden) silindi.
- **Kalan**: görsel gözden geçirme; sonra Razor MVC (`Views/`, eski `Controllers/*Controller.cs`) kaldırılabilir — API + Next tüm işlevi karşılıyor. `DiagnosticsController` Razor'u hâlâ duruyor (silinebilir).

### Not (2026-09-10) — Next.js görsel doğrulama (Claude in Chrome)
- Kullanıcı Chrome eklentisini bağladı; 8 route tarayıcıda gezildi: `/yazicilar`, `/sayac-oku`, `/rapor`, `/tedarikciler`, `/hakedisler`, `/hakedisler/yeni`, `/hakedisler/8` (detay), `/snmp-tanilama`. Hepsi Classical temaya oturmuş, veri API'den yükleniyor, tablolar dolu.
- Tema düğmesi çalışıyor: koyu ↔ açık geçiş + `localStorage` kalıcılığı + sayfalar arası korunuyor. İkon ay/güneş değişiyor.
- **Düzeltilen tek sorun**: Next dev overlay "1 Issue" = hydration mismatch (`<html data-theme>` FOUC script'ten geliyor, SSR'da yok → client'ta var; ayrıca bir tarayıcı eklentisinin `<body cz-shortcut-listen>` eklemesi). `app/layout.tsx`'te `<html suppressHydrationWarning>` + `<body suppressHydrationWarning>` eklendi — bu pattern için standart çözüm. Sonrası: konsol temiz, overlay yok, `next build` temiz.
- Diğer konsol mesajları (`message channel closed`) tarayıcı eklentisinden, uygulama değil.

### Not (2026-09-10) — Razor MVC kaldırıldı; backend saf JSON API
- **Silindi**: `Views/` (tamamı), `Controllers/{Readings,Report,Hakedis,Tedarikci,Printers,Diagnostics,Home}Controller.cs`, `wwwroot/` (css/js/lib/bootstrap/jquery/favicon), yalnız Razor'un kullandığı view-model'ler `Models/{DailySummaryViewModel,ErrorViewModel,HakedisCreateViewModel,ReadingsIndexViewModel}.cs`.
- **Korundu**: `Controllers/Api/*` (7 controller), `Models/` entity'leri + `ManualReadResult` (PrinterReadingService kullanıyor) + `SnmpDiagnosticResult` (SnmpService + API). `Services/`, `Data/`, `Configuration/`, `appsettings.json` aynen.
- `Program.cs`: `AddControllersWithViews()` → `AddControllers()` + `AddProblemDetails()`; `UseExceptionHandler("/Home/Error")` → `UseExceptionHandler()` (ProblemDetails); `MapStaticAssets()` ve `MapControllerRoute(...)` kaldırıldı, sadece `MapControllers()`.
- `YaziciTakip.csproj`: `<StaticWebAssetsEnabled>false</StaticWebAssetsEnabled>` — `wwwroot` silinince `WebApplication.CreateBuilder` `UseStaticWebAssets()`'te `DirectoryNotFoundException` atıyordu; bu bayrak çözüyor. (SDK hâlâ `Microsoft.NET.Sdk.Web` — Kestrel/DI/hosting için gerekli.)
- `Properties/launchSettings.json`: `launchUrl: "swagger"` (kök artık 404).
- `README.md` iki parçalı yapıya göre yeniden yazıldı (backend API + `web/` frontend, çalıştırma iki terminal).
- **Test**: `dotnet build` 0/0; API tüm uçlar 200, `/swagger` 200, eski Razor route'ları (`/`, `/Readings`, `/Report/Summary`, `/Printers`, `/Hakedis`) 404, CORS preflight 204. Next.js frontend trimmed backend'e karşı tarayıcıda doğrulandı (Sayaç Oku + Hakedişler veri yüklüyor, konsol temiz).
- Artık: kök = saf JSON API (`dotnet run`, :5239), `web/` = tek arayüz (`npm run dev`, :3000).

### Not (2026-09-10) — Fiyatlandırma master/detay'a geçti
- Kullanıcı isteği: fiyat listesi (tek tarih + tür başına 2 kutu) yerine → **tür seç + başlangıç/bitiş tarihi + tek fiyat kutusu**. Model master/detay:
  - **`Fiyat`** (`Models/Fiyat.cs`): `Id`, `TedarikciId` (FK cascade), `TurId` (FK restrict). **Unique (TedarikciId, TurId)** — her çift için tek master.
  - **`FiyatDetay`**: `Id`, `FiyatId` (FK cascade), `BaslangicTarihi`, `BitisTarihi` (DateOnly), `TurId` (FK restrict — master'la aynı, istenerek), `SayfaBasiFiyat` decimal(18,4). `Kapsar(tarih)` yardımcı.
  - `Tedarikci.FiyatListeleri` → `Tedarikci.Fiyatlar`. `AppDbContext`: `Fiyatlar` + `FiyatDetaylari` DbSet'leri; eski `FiyatListeleri`/`FiyatSatirlari` kaldırıldı. `FiyatListesi.cs` silindi.
- Migration `20260910061916_FiyatMasterDetay` — **elle düzenlendi**: önce yeni tablolar + index, sonra `Sql()` ile eski veri taşınır (FiyatSatirlari+FiyatListeleri → Fiyatlar+FiyatDetaylari; eski tek `Tarih` → `BaslangicTarihi`, `date(Tarih,'+1 year')` → `BitisTarihi` placeholder), en son eski tablolar `DropTable`. Dev'de 1 liste/2 satır sorunsuz taşındı.
- **API**: `TedarikcilerApiController` — `GET /{id}` artık `fiyatlar: [{id, turId, turAd, detaylar:[{id, baslangicTarihi, bitisTarihi, sayfaBasiFiyat, isCurrent}]}]`. `POST /{id}/fiyatlar` `{turId, baslangicTarihi, bitisTarihi, sayfaBasiFiyat}` → master find-or-create + detay ekle. Yeni `FiyatDetaylariApiController` (`PUT/DELETE /api/fiyat-detaylari/{id}`) — detay güncelle/sil; master'ın son detayı silinince master da silinir. `FiyatListeleriApiController` kaldırıldı. `TedarikciListDto`: `priceListCount/latestPriceListDate` → `fiyatSayisi` + `detaySayisi`.
- `HakedislerApiController`: `ResolvePriceListAsync` → **`ResolveUnitPriceAsync(tedarikciId, turId, asOf)`** — `Fiyat` master'ı bul → `FiyatDetay`'da `asOf` tarihini kapsayan (`Baslangic ≤ asOf ≤ Bitis`), en yeni başlangıçlı satırın fiyatı; yoksa 0. `FiyatListesiBilgi` artık hep null (alan duruyor, frontend guard'lı). Uyarı: "X: '<tür>' için <tarih> tarihinde geçerli fiyat yok".
- **Frontend** (`web/`): `lib/types.ts` — `FiyatListesi/FiyatSatiri` → `Fiyat/FiyatDetay`. `components/TedarikciDetailClient.tsx` yeniden yazıldı: "Yeni fiyat" formu (tür select + iki tarih + tek fiyat) + tür başına "Fiyat — X" kartı, her karttaki detay satırları inline düzenlenebilir/silinebilir. `TedarikcilerClient.tsx` liste sütunu "Fiyat kaydı" = detay sayısı.
- Test: `next build` + `dotnet build` 0/0. Migration veri taşıma doğrulandı. API: ekle (mevcut master'a 2. detay), PUT, DELETE, hakediş taslak fiyat çözümü (bugünü kapsayan detay → doğru fiyat; kapsayan yok → 0 + uyarı). Tarayıcıda tedarikçi detay + hakediş yeni sayfaları render doğrulandı (bir kez `.next` cache bayatlığından ChunkLoadError geldi, `.next` silinip restart edilince düzeldi). Tüm test verileri temizlendi.

### Not (2026-09-10) — Ağ tarama (yazıcı keşfi)
- İhtiyaç: IP bulmakla uğraşmak istemeyen kullanıcı için Yazıcılar sayfasına "Ağı Tara" düğmesi → yerel ağdaki SNMP açık yazıcıları bulup listelesin, tek tıkla eklesin.
- **`Services/PrinterDiscoveryService.cs`**: `GetLocalSubnets()` (aktif IPv4 arayüzlerinden CIDR türetir; `HasGateway` olanı önce sıralar — VirtualBox host-only gibi geçitsiz ağ önerilmez; APIPA 169.254 atlanır; /22'den geniş maske reddedilir). `ScanAsync(cidr, ct)`: CIDR'deki tüm host'lara paralel (`SemaphoreSlim(64)`) `QuickProbeAsync` (700ms timeout). Yazıcı ölçütü: standart sayfa sayacına yanıt (kesin) **VEYA** (marka/`print` eşleşmesi **VE** sysDescr'da switch/router/camera vb. yoksa). /24 gerçek ofis ağında ~6 sn.
- **`ISnmpService.QuickProbeAsync(ip, timeoutMs, ct)`** eklendi (`SnmpQuickProbe` modeli): kısa timeout'la sysDescr → yanıt varsa sysName + sayfa-sayacı OID. `GetSingleAsync`'e `timeoutMs` overload'u eklendi.
- **`Controllers/Api/DiscoveryApiController.cs`**: `GET /api/discovery/subnets` (`{subnets:[{cidr,interfaceName,serverIp,hostCount,scannable,hasGateway}], suggested}`), `POST /api/discovery/scan` `{cidr?}` → `[{ipAddress, sysName, sysDescr, snmpVersion, hasPageCounter, detectedBrand, alreadyRegistered}]` (kayıtlı IP'ler `alreadyRegistered=true`). 45sn linked-token cap. `Program.cs`: `AddScoped<PrinterDiscoveryService>()`.
- **Frontend**: `web/lib/types.ts` (`ScanSubnet`, `SubnetsResponse`, `DiscoveredPrinter`). `web/app/globals.css`: `.modal-overlay/.modal/.modal-head/.modal-body` (kütüphanesiz). `web/components/PrintersClient.tsx`: "Yeni yazıcı ekle" kart başlığında **"Ağı Tara"** düğmesi → `ScanModal` (CIDR input + datalist önerileri, "Tara", sonuç tablosu, satır başına "Ekle"/"Ekli"). "Ekle" → `POST /api/printers {name: sysName||ip, ipAddress}`; modal kapanınca ana liste yenilenir.
- Test (gerçek ofis Wi-Fi 192.168.150.0/24): 9 yazıcı bulundu (Canon MF450, Olivetti'ler, Samsung, Kyocera), **HP J9850A switch elendi** (sayaç yok + "Switch" kelimesi). Tarayıcıda modal + "Ekle" (sysName ile eklendi, "Ekli"ye döndü) doğrulandı; eklenen test yazıcısı silindi. `next build` + `dotnet build` 0/0.
- Sınır: sadece sunucuyla aynı LAN'daki, SNMP açık cihazlar. Farklı VLAN görünmez.

### Not (2026-09-10) — SNMP kararsızlığı (model bazen gelmiyor, 2. denemede geliyor)
- **Kök neden**: SNMP UDP üzerinden çalışır, `Messenger.GetAsync` yeniden gönderim yapmaz. Tek pakedin düşmesi (Wi-Fi/switch buffer) → "yanıt yok". Ayrıca bazı Olivetti'ler sysDescr'ı **ara sıra boş** döndürüyor (geçerli SNMP yanıtı ama işe yaramaz). İkisi birlikte: Sayaç Oku'da "Marka/Model" boş, SNMP Tanılama'da model+marka boş → yeniden tıklayınca geliyor.
- **Fix 1 — retry**: `SnmpOptions.Retries` (varsayılan 2, `appsettings.json`'a eklendi). `GetSingleAsync` yeniden yazıldı: geçici hatada (timeout/socket/exception) `retries` kez daha dener (150·(n+1) ms backoff); cihaz `NoSuchObject/NoSuchInstance/EndOfMibView` derse **kesin cevap** sayıp tekrar denemez; istek iptalinde `throw`. `TryGetAsync` (sayaç okuma) artık kendi bespoke GET'i yerine `GetSingleAsync` üzerinden geçiyor (retry'yi otomatik alıyor).
- **Fix 2 — retryOnEmpty**: `GetIdentityAsync` helper'ı (sysDescr/sysName için) — dönen değer boş/whitespace ise de tekrar dener. `GetModelAsync` ve `DiagnoseAsync`'in kimlik sorguları bunu kullanıyor. Ağ tarama (`QuickProbeAsync`) retry KULLANMAZ (retries=0) — ölü host'lar hızlı elensin, /24 taraması ~6 sn kalsın.
- **Fix 3 — fazladan marka OID satırı**: `DiagnoseAsync`'te "Denenen bilinen OID'ler" listesinde marka-özel OID (ör. Olivetti/HP) **boş dönüyorsa artık eklenmiyor** (`if (brand is not null && v?.Value is null) continue`). Evrensel `prtMarkerLifeCount` her zaman gösteriliyor. "Çıktısı yok" gürültü satırı kalktı.
- Test: .204 (Olivetti, ara sıra boş sysDescr) 5/5 tutarlı `OLIVETTI Printing System` + brand; `/api/readings` 5 yazıcının hepsinde model dolu; diagnostics probes = sadece 1 (standart OID). `dotnet build` 0/0.

### Not (2026-09-10) — Hakedişte fiyatların 0 gelmesi (tarih aralığı sorunu) + uyarı iyileştirmesi
- **Kullanıcı**: "tedarikçiye fiyat eklememe rağmen hakedişte fiyatlar gözükmüyor."
- **Kök neden**: `FiyatMasterDetay` migration'ı eski `FiyatListesi.Tarih` değerini yeni `FiyatDetay.BaslangicTarihi` yaptı. Kullanıcının eski listesindeki tek tarih **gelecek bir tarihti (09.12.2026)** → taşınan detaylar `09.12.2026 – 09.12.2027` aralığında. `ResolveUnitPriceAsync` dönem bitiş tarihini (bugün, 10.09.2026) kapsayan detay arıyor; hiçbiri kapsamıyor → 0. Yani kod doğru çalışıyor, veri (tarih aralığı) yanlış.
- **Çözüm (kullanıcı tarafında)**: Tedarikçi detay sayfasında "Fiyat — Siyah-Beyaz" / "Fiyat — Renkli" kartlarındaki detay satırının **başlangıç tarihini** faturalanan dönemin başına ya da öncesine çek (ör. 01.01.2026). Kod/migration değişmedi.
- **Kod iyileştirmesi** (`HakedislerApiController`): `ResolveUnitPriceAsync` artık `Task<(decimal Price, string? Note)>` döndürüyor. `Note` fiyat 0 ise nedeni açıklıyor: "bu tür için tanımlı fiyat yok" / "fiyat tanımlı ama tarih aralığı (09.12.2026–09.12.2027) 10.09.2026 tarihini kapsamıyor" / "bu tür için fiyat detayı girilmemiş". `Taslak` uyarısı bu notu gösteriyor — kullanıcı hakediş taslağında tam nedeni görüyor.
- Test: `dotnet build` 0/0; `POST /api/hakedisler/taslak {tedarikciId:9}` → 3 satır unit 0 + her biri için "tarih aralığı (09.12.2026–09.12.2027) 10.09.2026 tarihini kapsamıyor (0)." uyarısı.

### Not (2026-09-10) — Hakediş kaydet 500: numara çakışması
- **Kullanıcı**: "hakediş kaydet tuşuna bastığımda 'An error occurred while processing your request.'"
- **Kök neden** (`HakedislerApiController.Create`): numara `COUNT(o yıla ait hakediş) + 1` ile üretiliyordu. Dev DB'de tek hakediş `2026-0002` var (`2026-0001` veri kaybı olayında silindi → numarada boşluk). `COUNT` = 1 → yeni numara `2026-0002` → `UNIQUE constraint failed: Hakedisler.Number` → `SaveChangesAsync` patlıyor → 500.
- **Fix**: numara artık adet değil, o yıl içindeki **en yüksek mevcut sıra + 1**'den üretiliyor (prefix'li numaralar çekilir, sonek `int.TryParse` ile ayrıştırılır, `Max()+1`). Silinmiş kayıt/boşluk olsa da var olan numara tekrar üretilmez.
- Test: `dotnet build` 0/0; `POST /api/hakedisler` (tedarikçi 9, 3 satır) → `2026-0003` oluştu (eskiden 500). Test kaydı `DELETE /api/hakedisler/9` ile silindi, DB yine tek `2026-0002`.
- Not: bu boşluk yüzünden kullanıcının bir sonraki gerçek hakedişi `2026-0002` değil `2026-0003` olur (numara atlar) — kabul edilebilir, benzersizlik korunur.

### Not (2026-09-10) — "güncel" etiketi taşındı + tedarikçi detay genişletildi
- `web/components/TedarikciDetailClient.tsx` `FiyatKarti`/`DetayRow`: "güncel" etiketi bitiş tarihi input'unun **altında** duruyordu (kötü konum). Tabloya en başa dar bir durum sütunu eklendi; "güncel" pill'i artık satırın solunda, dikey ortalı — hangi tarih aralığının aktif olduğu net.
- Kullanıcı fiyat tablosunda "Sil" için yatay kaydırmayı sevmedi → **çerçeveler büyütüldü**: `web/app/globals.css` `.container` max-width 1040 → **1180px** (site geneli); tedarikçi detay 2 sütun grid'i `minmax(260px,1fr) minmax(340px,1.5fr)` → `minmax(240px,0.9fr) minmax(460px,1.7fr)` (sağ/fiyat sütunu daha geniş). Fiyat tablosu satırı (güncel · başlangıç · bitiş · ₺/sayfa · Kaydet/Sil) artık kaydırmasız sığıyor. `tsc --noEmit` temiz.

### Not (2026-09-10) — Logo: nav "YT" kutusu → PrinTrack yazıcı markası
- Kullanıcı masaüstünden bir logo handoff klasörü koydu (`Yazıcı takip sistemi logosu/design_handoff_printrack_logo/`, "PrinTrack" kimliği, seçilen mark **2a — yazıcı gövdesi**: üst tepsi + gövde + çıkış sayfası + altın basım çizgileri + durum LED'i). İş bitince klasör **silindi** (kullanıcı isteği).
- `web/components/Nav.tsx`: `<span className="brand-mark">YT</span>` → handoff'un 2a mark'ı inline SVG (`viewBox 0 0 58 58`, `stroke=currentColor`, nav ölçüsü için `stroke-width 2.3`). Çıkış sayfası knockout `fill: var(--color-bg)` (temaya uyar), basım çizgileri `stroke: var(--brand-accent, var(--color-accent))`, LED `fill: currentColor`. Marka adı metni **"Yazıcı Takip" kaldı** (uygulamanın yerleşik adı; handoff'un "PrinTrack" wordmark'ı benimsenmedi — istenirse değiştirilir).
- `web/app/globals.css` `.nav .brand-mark`: bordür/kutu stili kaldırıldı, sadece 1.95rem SVG. `.nav .brand:hover` → `--brand-accent: var(--color-accent-700)` + mark rengi accent-700 (handoff'un hover davranışı).
- `web/app/icon.svg` (yeni) — favicon: handoff'un sadeleştirilmiş favicon varyantı (silüet + tek altın çizgi, `stroke-width 3`) + `#f3f2f2` zemin karesi (koyu sekme çubuğunda da okunur). Next 16 App Router bunu otomatik `<link rel=icon>` yapıyor (`/icon.svg` route'u build'de görünüyor). c2pa metadata blob'ları export'lardan çıkarıldı.
- Test: `tsc --noEmit` + `next build` temiz; Chrome'da nav marka açık+koyu temada doğrulandı (knockout ve accent iki temada da doğru).

### Not (2026-09-10) — Ölü kod temizliği: Worker + SampleDataSeeder + config-polling kaldırıldı
- Graph (codebase-memory) + manuel tarama: arayüz/servis katmanı zaten temizdi (10 web bileşeni de kullanımda, `format.ts`/`types.ts` ölü export yok, SNMP metotları hep çağrılıyor). Bulunan ölü kod:
- **Silinen dosyalar**: `Services/PrinterMonitorWorker.cs`, `Services/SampleDataSeeder.cs`, `Configuration/PrinterMonitoringOptions.cs`. `Models/FiyatListesi.cs` zaten diskte yoktu (master/detay geçişi) — silme commit'e girdi.
- **Neden**: `PollingEnabled` her yerde `false` (kullanıcı "sonra sileriz" demişti), `PrinterMonitoring:Printers` hep boş (config-tabanlı yazıcı ekleme kullanılmıyor), `SeedSampleReadings` hep `false`. Worker'ın fiilen yaptığı tek iş açılışta `db.Database.MigrateAsync()`'ti.
- **Açılış migration'ı** artık `Program.cs`'te: `builder.Build()` sonrası `using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();`. `AddHostedService`/`AddScoped<SampleDataSeeder>` kaldırıldı. `Program` zaten top-level `await` kullanıyor.
- **`SnmpOptions`** artık kendi dosyasında (`Configuration/SnmpOptions.cs`), top-level, `SectionName = "Snmp"`. `PrinterMonitoringOptions` wrapper + `PrinterConfig` gitti. `SnmpService` ctor: `IOptions<SnmpOptions>` → `_options = options.Value`.
- **appsettings.json**: `"PrinterMonitoring": { PollingEnabled, PollingIntervalMinutes, Snmp{...}, SeedSampleReadings, SampleDataDays, _PrintersNote, Printers }` → düz `"Snmp": { Community, Version, FallbackToV1, Port, TimeoutSeconds, Retries, PageCountOid }`. `appsettings.Development.json`: `PrinterMonitoring` bloğu tamamen çıktı (sadece `Logging` kaldı).
- **Tier 1 küçük ölü alan**: `HakedisTaslakDto.FiyatListesiBilgi` (master/detay geçişinden beri hep `null`) — `HakedislerApiController` + `web/lib/types.ts` (`HakedisTaslak.fiyatListesiBilgi`) + `web/components/HakedisYeniClient.tsx` (fiyat listesi satırı) kaldırıldı.
- **README.md** güncellendi (Worker/polling/SeedSampleReadings/örnek-veri bölümü çıktı, config bloğu sadeleşti, `Ağı Tara` + retry eklendi, `FiyatListesi`→`Fiyat/FiyatDetay`, `DiscoveryApi` eklendi).
- Test: `dotnet build` 0/0, `next build` temiz; açılışta migration logu ("No migrations were applied. The database is already up to date."), tüm `/api/*` uçları 200, `taslak` yanıtında `fiyatListesiBilgi` alanı yok. Graph: 993→944 node, 2410→2277 edge.

## Notlar
- Henüz yazıcı IP'leri ve SNMP versiyonu netleşmedi — bu bilgiler geldikçe `appsettings.json` ve bağlantı testleri güncellenecek.
- Kullanıcı/IP bazlı "hangi istekler gönderildi" bilgisi bu mimaride mevcut değil; bu sınırlama yöneticiyle paylaşılmalı.
