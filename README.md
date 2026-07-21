# TechYouth BPM Wizard

TechYouth School 2. donem proje gereksinimleri icin hazirlanan full-stack BPM wizard uygulamasi.

Bu repo iki ana uygulamadan olusur:

- `apps/web`: Next.js + TypeScript frontend
- `apps/api`: .NET 8 Web API backend

Form field/page ordering uses `@dnd-kit`; the visual workflow canvas uses `@xyflow/react`. The API runs a custom typed .NET workflow runtime and persists versioned definitions with EF Core. Camunda/Kissflow are product references, not runtime dependencies.

Dokumantasyon `docs/` altindadir. Tekrarsiz konu sahipligi ve onerilen okuma sirasi [docs/README.md](docs/README.md) dosyasinda tanimlidir.

Hizli kurulum icin [QUICKSTART.md](QUICKSTART.md), sunuma hazirlanmak icin [docs/23-presentation-study-guide.md](docs/23-presentation-study-guide.md) dosyasini kullanin.

## Requirements

- Git
- Node.js 20 veya ustu
- npm 10 veya ustu
- .NET SDK 8 veya ustu
- PostgreSQL opsiyoneldir; ortak takim veritabani icin Neon gibi hosted PostgreSQL kullanilabilir.

Kontrol komutlari:

```bash
node --version
npm --version
dotnet --version
git --version
```

## Install

Repository klonlandiktan sonra frontend paketleri kurulmalidir:

```bash
cd apps/web
npm install
```

Backend paketleri .NET restore ile yuklenir:

```bash
cd apps/api
dotnet restore TechYouthBpm.slnx
```

## Environment

Frontend varsayilan olarak API'yi su adreste bekler:

```bash
http://localhost:5291
```

Farkli API adresi kullanmak icin `apps/web/.env.local` dosyasi olusturulabilir:

```bash
NEXT_PUBLIC_API_BASE_URL=http://localhost:5291
```

`.env.local` dosyalari git'e eklenmez.

Backend varsayilan olarak SQLite kullanir:

```json
{
  "Database": {
    "Provider": "Sqlite"
  },
  "Auth": {
    "SessionDurationMinutes": 120,
    "SessionCacheSeconds": 15,
    "RememberMeDurationMinutes": 43200,
    "RefreshTokenDurationMinutes": 43200,
    "PasswordResetMinutes": 30,
    "MaxFailedLoginAttempts": 5,
    "LockoutMinutes": 10,
    "EmailVerificationMinutes": 1440,
    "EmailVerificationResendCooldownMinutes": 5,
    "RateLimitPermitLimit": 10,
    "RateLimitWindowMinutes": 1
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=techyouth-bpm.db"
  }
}
```

`Auth:SessionDurationMinutes` normal oturum suresini dakika cinsinden belirler ve su anda 120 dakikadir. `Auth:SessionCacheSeconds` opaque session icin cozulmus kullanici/yetki DTO'sunun kisa omurlu memory-cache suresidir; `0` cache'i kapatir, desteklenen guvenlik ve uyelik mutasyonlari ilgili kaydi aninda gecersiz kilar. `Auth:RememberMeDurationMinutes` ve `Auth:RefreshTokenDurationMinutes` beni-hatirla/refresh-token akisi icin kullanilir ve su anda 30 gunluk sureye ayarlidir. `Auth:PasswordResetMinutes` sifre sifirlama token gecerliligini belirler. `Auth:MaxFailedLoginAttempts` ve `Auth:LockoutMinutes` yanlis giris denemelerinden sonra gecici hesap kilitlemeyi belirler. `Auth:EmailVerificationMinutes` e-posta dogrulama kodu gecerliligini, `Auth:EmailVerificationResendCooldownMinutes` yeniden kod gonderme bekleme suresini belirler. `Auth:RateLimitPermitLimit` ve `Auth:RateLimitWindowMinutes` login/register/verification/reset endpointlerini sinirlar.

Auth modeli JWT degildir; backend opaque session token uretir ve yalniz hash'ini veritabaninda saklar. Normal web istemcisi token dondurmeyen `/api/auth/browser-login` endpointini kullanir: access/refresh token JSON body'ye veya Zustand/localStorage'a girmez, HttpOnly cookie'de kalir. Mutation istekleri okunabilir ancak kimlik dogrulama yetkisi tasimayan CSRF cookie'sini `X-CSRF-Token` header'i olarak geri yollar. Sayfa yenilenince oturum `/api/auth/me` ile toparlanir; access suresi dolmus ve beni-hatirla aktifse `/api/auth/refresh` cookie'leri sessizce rotate eder ve response body'de sir dondurmez. Swagger ve acik API istemcileri `/api/auth/login` ile Bearer token response'u almaya devam eder.

`Beni hatirla` secilirse hashed rotating refresh token uretilir; refresh reuse tespitinde aktif oturumlar revoke edilir. Kullanici sifreleri PBKDF2 hash olarak tutulur; logout ve oturum kapatma islemleri session'i veritabaninda revoke eder. Register olan hesaplar `PendingApproval` baslar, Admin onayi olmadan login olamaz.

Sifre sifirlama e-postalarindaki link `Frontend:BaseUrl` ayarindan uretilir. Local varsayilan `http://localhost:3000` degeridir; farkli web portu kullanilirsa scriptlerde `-FrontendBaseUrl` verilebilir.

Email verification varsayilan olarak `Demo` provider ile calisir. Bu modda OTP hashlenerek veritabanina yazilir ve demo kod UI'da gorunur. Kodlar varsayilan olarak 24 saat gecerlidir ve yeniden kod gonderme icin 5 dakikalik cooldown uygulanir. `Routing` provider kullanildiginda once guvenli allowlist'e bagli canli SMTP denenir; allowlist disindaki kullanicilar Mailtrap Sandbox'a yonlendirilir. Sandbox mail gercek Gmail/Outlook inbox'ina degil, Mailtrap Sandbox inbox'ina gider.

Mailtrap kurulumunda takip edilecek ayarlar:

```bash
cd apps/api/src/TechYouthBpm.Api
dotnet user-secrets set "Email:Provider" "Routing"
dotnet user-secrets set "Email:FromAddress" "no-reply@techyouth.local"
dotnet user-secrets set "Email:FromName" "TechYouth BPM"
dotnet user-secrets set "Email:Smtp:Host" "live.smtp.mailtrap.io"
dotnet user-secrets set "Email:Smtp:Port" "587"
dotnet user-secrets set "Email:Smtp:Username" "api"
dotnet user-secrets set "Email:Smtp:Password" "your-mailtrap-live-token"
dotnet user-secrets set "Email:Smtp:EnableSsl" "true"
dotnet user-secrets set "Email:AllowedRecipients" "your-test-email@example.com"
dotnet user-secrets set "Email:AllowedUsernames" "your-test-username"
dotnet user-secrets set "Email:Sandbox:FromAddress" "sandbox@techyouth.local"
dotnet user-secrets set "Email:Sandbox:FromName" "TechYouth BPM Sandbox"
dotnet user-secrets set "Email:Sandbox:Smtp:Host" "sandbox.smtp.mailtrap.io"
dotnet user-secrets set "Email:Sandbox:Smtp:Port" "2525"
dotnet user-secrets set "Email:Sandbox:Smtp:Username" "your-mailtrap-sandbox-username"
dotnet user-secrets set "Email:Sandbox:Smtp:Password" "your-mailtrap-sandbox-password"
dotnet user-secrets set "Email:Sandbox:Smtp:EnableSsl" "true"
```

Mailtrap Sandbox kullaniliyorsa `Email:Sandbox:*` degerleri Mailtrap projesindeki Sandbox inbox `SMTP` sekmesinden kopyalanir. Email Sending kullaniliyorsa canli SMTP icin Mailtrap'in verdigi host/token `Email:Smtp:*` key'lerine yazilir. Gercek username/password/token degerleri repo'ya commit edilmez.

Gercek inbox'a test maili gondermek icin Mailtrap Email Sending tarafinda sending domain dogrulanmis olmalidir. Mailtrap'in SMTP orneklerinde gercek gonderim icin host genellikle `live.smtp.mailtrap.io`, port `587`, username `api`, password ise API token degeridir. Gercek teslim testinde guvenlik icin allowlist kullan:

```bash
dotnet user-secrets set "Email:Smtp:Host" "live.smtp.mailtrap.io"
dotnet user-secrets set "Email:Smtp:Port" "587"
dotnet user-secrets set "Email:Smtp:Username" "api"
dotnet user-secrets set "Email:Smtp:Password" "your-mailtrap-api-token"
dotnet user-secrets set "Email:AllowedRecipients" "your-email@example.com"
dotnet user-secrets set "Email:AllowedUsernames" "your-username"
```

Bu allowlist doluyken SMTP sender sadece belirtilen kullanici ve alici icin mail gonderir. Diger kullanicilar icin backend SMTP gonderimini reddeder.

Takimla ortak PostgreSQL/Neon veritabani kullanmak icin provider ve connection string gizli olarak verilmelidir. Gercek connection string repo'ya commit edilmez.

PowerShell ile gecici environment variable:

```powershell
$env:Database__Provider="PostgreSql"
$env:ConnectionStrings__DefaultConnection="Host=your-neon-host;Port=5432;Database=your-database;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
```

.NET user secrets ile kalici lokal ayar:

```bash
cd apps/api/src/TechYouthBpm.Api
dotnet user-secrets set "Database:Provider" "PostgreSql"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=your-neon-host;Port=5432;Database=your-database;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
```

Ornek format icin `apps/api/src/TechYouthBpm.Api/appsettings.example.json` dosyasi incelenebilir.

Neon icin repo kokunde gitignored `.env.neon.local` dosyasi olusturulabilir:

```text
Database__Provider=PostgreSql
ConnectionStrings__DefaultConnection=Host=your-neon-host;Port=5432;Database=your-database;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true;Channel Binding=Require
Seed__MockData=true
```

Neon baglantisi ayri bir portta denenmek istenirse:

```powershell
./scripts/run-api-neon.ps1 -Url http://localhost:5292
```

Script API'yi calistirdigin terminalde foreground olarak baslatir. API'yi durdurmak icin ayni terminalde `Ctrl+C` kullan.

SQLite API ayni anda aciksa build dosyalari kilitlenebilir. Bu durumda mevcut build ile Neon'u baslatmak icin:

```powershell
./scripts/run-api-neon.ps1 -Url http://localhost:5292 -NoBuild
```

## Run Locally

Iki ayri terminal kullanmak en temiz yoldur.

Terminal 1 - API:

```powershell
./scripts/run-api-local.ps1
```

Script API'yi calistirdigin terminalde foreground olarak baslatir. API'yi durdurmak icin ayni terminalde `Ctrl+C` kullan.

Script varsayilan olarak 120 dakikalik normal session kullanir. Timeout testini hizlandirmak icin:

```powershell
./scripts/run-api-local.ps1 -SessionDurationMinutes 1
```

API ayaga kalkinca Swagger acilir:

```bash
http://localhost:5291/swagger
```

Ilk calistirmada API, secili veritabani uzerinde EF Core migration'larini uygular, sonra demo kullanicilari ve mock BPM verisini seed eder. SQLite dosyasi localde olusur; PostgreSQL/Neon modunda tablolar secili uzak veritabaninda migration ile olusturulur.

SQLite ile local demo veritabanini sifirlamak icin:

```powershell
./scripts/run-api-local.ps1 -ResetDb
```

Migration oncesi `EnsureCreated` ile olusmus eski SQLite dosyalari migration history icermeyebilir. Boyle durumlarda local test icin reset onerilir:

```powershell
./scripts/run-api-local.ps1 -ResetDb -Force
```

Sadece kullanicilarla baslamak ve mock surec/form verisini kapatmak icin:

```powershell
./scripts/run-api-local.ps1 -ResetDb -Force -SkipMockData
```

Local veritabani akisi ve sema ozeti icin `docs/08-local-database.md` dosyasina bak.

Terminal 2 - Web:

```powershell
./scripts/run-web-local.ps1
```

Web uygulamasi:

```bash
http://localhost:3000
```

Ana workspace route'lari:

- `http://localhost:3000/dashboard`
- `http://localhost:3000/forms`
- `http://localhost:3000/runner`
- `http://localhost:3000/workflows`
- `http://localhost:3000/processes`
- `http://localhost:3000/tasks`
- `http://localhost:3000/inbox`
- `http://localhost:3000/management`
- `http://localhost:3000/management/teams`
- `http://localhost:3000/teams` (kullanicinin kendi takimlari ve salt-okunur takim arkadaslari)
- `http://localhost:3000/logs`
- `http://localhost:3000/settings`

EF Core migration komutlari:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project apps/api/src/TechYouthBpm.Infrastructure/TechYouthBpm.Infrastructure.csproj --startup-project apps/api/src/TechYouthBpm.Api/TechYouthBpm.Api.csproj
```

## Docker

Docker Desktop'ta iki ayri Compose uygulamasi kullanilir. `eczacibasi-local`, SQLite ile hizli local demo ortamidir. `eczacibasi-cloud`, gitignored `.env.neon.local` dosyasindaki Neon ayarlariyla uzak PostgreSQL'e baglanir. Ikisi ayni `3000` web ve `5291` API portlarini kullandigi icin ayni anda acilmamalidir.

```powershell
# SQLite local stack: cloud stack aciksa once kapatilir
docker compose -f compose.cloud.yaml down
docker compose up -d --build

# Neon cloud stack: local stack aciksa once kapatilir
docker compose down
docker compose -f compose.cloud.yaml up -d --build
```

- Web: `http://localhost:3000`
- API / Swagger: `http://localhost:5291/swagger`
- Local stack'te DB, `sqlite-data` volume icinde tutulur.
- Cloud stack'te schema ve mock veri Neon uzerinde API baslangicinda EF Core migration + seed ile olusur.

Yalnizca containerlari olusturup Docker Desktop'tan baslatmak icin localde `docker compose create --build --force-recreate`, cloudda `docker compose -f compose.cloud.yaml create --build --force-recreate` kullanilir. Containerlari kapatmak icin local stack'te `docker compose down`, cloud stack'te `docker compose -f compose.cloud.yaml down` kullanilir. Local SQLite volume'unu sifirlamak icin `docker compose down -v` kullanilir. Neon veya SMTP secret'lari compose dosyasina yazilmaz. Ayrintili akis [docs/17-docker-and-deployment.md](docs/17-docker-and-deployment.md) dosyasindadir.

## Stop Local Servers

Normal terminalde calistirildiysa ilgili terminalde `Ctrl + C` yeterlidir.

Port uzerinden kapatmak gerekirse PowerShell:

```powershell
Get-NetTCPConnection -LocalPort 5291,3000 | ForEach-Object {
  Stop-Process -Id $_.OwningProcess -Force
}
```

## Validation

Backend testleri:

```bash
dotnet test apps/api/TechYouthBpm.slnx
```

Test paketi servis testlerini SQLite uzerinde, HTTP guvenlik ve yetki senaryolarini ise gecici SQLite dosyalari kullanan `WebApplicationFactory` hostu uzerinde calistirir. Cookie/CSRF, Bearer, refresh rotation/reuse, rate limit, personal/community/global workflow scope, Swagger, workflow publish, version-pinned process start, task formu, SLA/deadline ve server-side process/task pagination davranislari dogrulanir. Claim concurrency testi iki stale DbContext snapshot'inin ayni gorevi alamadigini kanitlar. Gercek transfer demo zinciri; bagimli form validasyonu, dosya metadata'si, takim+rol adayligi, claim/release, eksik task formu reddi, approve/reject, step output, bildirim ve iki seviyeli audit kaydini ayni HTTP senaryosunda kontrol eder. Mevcut bir session ile rol veya takim uyeligi degistiginde sonraki istegin yeni yetkiyi DB'den yeniden hesapladigi da entegrasyon testiyle guvence altindadir.

Neon/PostgreSQL migration smoke testi varsayilan kosuda dis servise baglanmaz. Opt-in calistirmak icin baglanti bilgisini yalniz mevcut terminal oturumunda tanimla:

```powershell
$env:TECHYOUTH_TEST_POSTGRES_CONNECTION = "<postgresql-connection-string>"
dotnet test apps/api/tests/TechYouthBpm.Tests/TechYouthBpm.Tests.csproj --filter "FullyQualifiedName~PostgreSql_Startup_Applies_Migrations"
Remove-Item Env:TECHYOUTH_TEST_POSTGRES_CONNECTION
```

Test benzersiz gecici bir PostgreSQL schema olusturur, migrations + seed + login/form smoke akisini calistirir ve schema'yi sonunda siler. Paylasilan demo tablolarina dokunmaz.

Frontend store testleri, lint ve production build:

```bash
cd apps/web
npm run test
npm run lint
npm run build
npm run test:e2e
```

Playwright komutu izole SQLite veritabanı hazırlayıp API ve web sunucularını
kendisi başlatır. Cookie session, route koruması, form/workflow yayınlama, süreç
başlatma ve takım+rol claim sınırını gerçek tarayıcıda doğrular.

API çalışırken operasyon kontrolleri:

- `http://localhost:5291/health/live`
- `http://localhost:5291/health/ready`

Her push için temel; `master`/manuel koşu için Playwright, PostgreSQL ve Docker
kalite kapıları GitHub Actions altında tanımlıdır. CI deployment yapmaz. Güncel
test sayıları ve kapsamın tek kaynağı
[docs/24-testing-and-quality-gates.md](docs/24-testing-and-quality-gates.md)
dosyasıdır.

## Demo Users

| Username | Password | Platform role | Community role |
| --- | --- | --- | --- |
| `admin` | `admin123` | SuperAdmin | Global |
| `user` | `user123` | User | Surec Baslatici |
| `approver` | `approver123` | User | Onay Sorumlusu |

Frontend once gercek API'ye login istegi atar. API calismiyorsa ayni demo kullanicilarla local fallback devreye girer; boylece UI gelistirmesi backend olmadan da devam edebilir.

Yeni kullanici kaydi login ekranindaki `Kaydol` modundan yapilir. Kayit `PendingApproval` durumunda olusur. SuperAdmin veya yetkili Topluluk Admin, `Yonetim` ekranindan kendi kapsamina uygun kullaniciyi `Active` yapabilir, community role atayabilir, gecici sifreyle yeni kullanici olusturabilir ve izinli oturumlari gorebilir/kapatabilir. Admin-created kullanicilar `MustChangePassword=true` baslar; normal workspace'e girmeden once zorunlu sifre degistirme ekranindan gecmek zorundadir. Manuel sifre secilmezse backend guclu bir gecici sifre uretir ve mail provider `Mailtrap`/`Smtp` ise kullaniciya e-posta ile gonderir. SuperAdmin, is akisi gecmisi olmayan test kullanicilarini silebilir; process/form/task/audit gecmisi olan kullanicilar icin backend silmeyi reddeder. `Ayarlar` ekraninda profil guncelleme, sifre degistirme, email verification OTP akisi, aktif oturumlar, tek oturum kapatma ve tum cihazlardan cikis akisi denenebilir.

Admin kullanicisi `Loglar` ekraninda sistem gecmisini arayabilir. Loglar varsayilan olarak toplu dokulmez; kisi, surec, entity veya aksiyon aramasi ile server-side paginated sonuc ve ilgili kronolojik gecmis gorulur. Bu liste register, login/logout, rol/status degisikligi, form create/update, process start ve task approve/reject gibi kritik aksiyonlari kullanici, entity ve zaman bilgisiyle takip eder. Surec detay ekranindaki audit timeline ise ilgili surecin state history bilgisini gosterir; sureci baslatan kullanici kendi surec gecmisini, Admin/Approver ise gorebildigi sureclerin gecmisini inceleyebilir.

Local SQLite demo DB; bes toplulukta yayinlanmis workflow'lar, bagli start/task formlari ve topluluk basina bes graph-uyumlu surec senaryosuyla gelir. Acik tasklarda gecikmis, yaklasan ve ileri tarihli deadline ornekleri; tamamlanan/reddedilen/geri gonderilen akislarda form ciktisi, step execution, audit ve bildirim zinciri bulunur. Eski `Legacy Basic Approval` uyumlulugu ve dort swimlane'li kosullu `Transfer Talep Akisi` korunur. Takim seed'i bes topluluga dagilmis 16 takim, lider, coklu takim uyesi ve sanal `Takimsiz` sorgusunda gorunecek kullanicilari birlikte icerir. Detaylar icin `docs/08-local-database.md` dosyasina bak.

## Current Demo Flow

1. Login ol.
2. Role gore menu ve dashboard'u gor.
3. Admin kullanicisiyle seeded formlari ve dashboard metriklerini incele.
4. Form tasarimi ekraninda kayitli bir formu sec, alan modelini duzenle ve guncelle.
5. Form surumunu taslak olarak kaydet, yayinla, arsivle ve eski surumun degismedigini incele.
6. `/workflows` ekraninda Start, User Task, Gateway, End ve Team Swimlane dugumleriyle bir akis ciz; form/takim/rol baglayip yayinla.
7. Form runner'da yayinlanmis formu ve uyumlu workflow'u secip version-pinned surec baslat.
8. `Islerim` ekraninda aday havuzundaki task'i uzerine al; task formunu doldurup approve/reject/complete/send-back akisini dene.
9. Surec detayinda node, attempt, tamamlayan kullanici, task form ciktisi ve audit zincirini incele.
10. Bildirim popover'inda son bes kaydi, `Gelen Kutusu` ekraninda arama/filtre/pagination ve okundu durumunu dene.
11. `Yonetim > Takimlar` ekraninda topluluk takimlarini, uyeleri, adaylari, lider degisimini ve sanal `Takimsiz` listesini dene.
12. Normal bir takim uyesiyle `/teams` ekranini ac; yalnizca kendi takimlarini ve e-posta icermeyen takim arkadasi listesini gorebildigini dogrula.

## Troubleshooting

- API login calismiyorsa once `http://localhost:5291/swagger` adresini kontrol et.
- Web login API'ye ulasamiyorsa `NEXT_PUBLIC_API_BASE_URL` degerinin API portuyla ayni oldugunu kontrol et.
- Port doluysa ilgili process'i kapat veya farkli port kullan.
- Frontend paket hatalarinda `apps/web/node_modules` silinip `npm install` tekrar calistirilabilir.

## Documentation

- [Dokumantasyon rehberi](docs/README.md): konu sahipligi ve okuma sirasi.
- [PDF gereksinim matrisi](docs/00-requirements-from-pdf.md): zorunlu ve bonus kapsam.
- [Mimari](docs/02-architecture.md): katmanlar ve genisleme sinirlari.
- [API ve servisler](docs/04-api-and-services.md): HTTP ve servis sozlesmeleri.
- [Test ve kalite kapilari](docs/24-testing-and-quality-gates.md): kapsam, komutlar ve guncel kanit.
- [Sunum calisma rehberi](docs/23-presentation-study-guide.md): teknoloji, karar ve savunma Q&A.
