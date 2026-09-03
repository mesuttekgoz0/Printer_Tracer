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

## Notlar
- Henüz yazıcı IP'leri ve SNMP versiyonu netleşmedi — bu bilgiler geldikçe `appsettings.json` ve bağlantı testleri güncellenecek.
- Kullanıcı/IP bazlı "hangi istekler gönderildi" bilgisi bu mimaride mevcut değil; bu sınırlama yöneticiyle paylaşılmalı.
