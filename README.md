# TechYouth BPM Wizard

TechYouth School 2. donem proje gereksinimleri icin hazirlanan full-stack BPM wizard uygulamasi.

Bu repo iki ana uygulamadan olusur:

- `apps/web`: Next.js + TypeScript frontend
- `apps/api`: .NET 8 Web API backend

Dokumantasyon `docs/` altindadir. Proje ilerledikce mimari kararlar, servis isleyisi, code review notlari ve ekip sunum dagilimi buradan takip edilir.

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

`Auth:SessionDurationMinutes` normal oturum suresini dakika cinsinden belirler ve su anda 120 dakikadir. `Auth:RememberMeDurationMinutes` ve `Auth:RefreshTokenDurationMinutes` beni-hatirla/refresh-token akisi icin kullanilir ve su anda 30 gunluk sureye ayarlidir. `Auth:PasswordResetMinutes` sifre sifirlama token gecerliligini belirler. `Auth:MaxFailedLoginAttempts` ve `Auth:LockoutMinutes` yanlis giris denemelerinden sonra gecici hesap kilitlemeyi belirler. `Auth:EmailVerificationMinutes` e-posta dogrulama kodu gecerliligini, `Auth:EmailVerificationResendCooldownMinutes` yeniden kod gonderme bekleme suresini belirler. `Auth:RateLimitPermitLimit` ve `Auth:RateLimitWindowMinutes` login/register/verification/reset endpointlerini sinirlar.

Auth modeli JWT degildir; backend opaque bearer session token uretir. Token'in sadece hash'i veritabaninda saklanir. Browser akisi access token'i HttpOnly cookie olarak da alir, mutating cookie isteklerinde CSRF header kullanir. `Beni hatirla` secilirse hashed rotating refresh token uretilir; refresh reuse tespitinde aktif oturumlar revoke edilir. Kullanici sifreleri PBKDF2 hash olarak tutulur; logout ve oturum kapatma islemleri session'i veritabaninda revoke eder. Register olan hesaplar `PendingApproval` baslar, Admin onayi olmadan login olamaz.

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
- `http://localhost:3000/processes`
- `http://localhost:3000/tasks`
- `http://localhost:3000/management`
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

Test paketi servis testlerini SQLite uzerinde, HTTP guvenlik ve yetki senaryolarini ise gecici SQLite dosyalari kullanan `WebApplicationFactory` hostu uzerinde calistirir. Cookie/CSRF, Bearer, refresh rotation/reuse, rate limit, community scope, Swagger ve formdan surec baslatmaya kadar gercek controller pipeline'i dogrulanir.

Neon/PostgreSQL migration smoke testi varsayilan kosuda dis servise baglanmaz. Opt-in calistirmak icin baglanti bilgisini yalniz mevcut terminal oturumunda tanimla:

```powershell
$env:TECHYOUTH_TEST_POSTGRES_CONNECTION = "<postgresql-connection-string>"
dotnet test apps/api/tests/TechYouthBpm.Tests/TechYouthBpm.Tests.csproj --filter "FullyQualifiedName~PostgreSql_Startup_Applies_Migrations"
Remove-Item Env:TECHYOUTH_TEST_POSTGRES_CONNECTION
```

Test benzersiz gecici bir PostgreSQL schema olusturur, migrations + seed + login/form smoke akisini calistirir ve schema'yi sonunda siler. Paylasilan demo tablolarina dokunmaz.

Frontend lint ve production build:

```bash
cd apps/web
npm run lint
npm run build
```

## Demo Users

| Username | Password | Role |
| --- | --- | --- |
| `admin` | `admin123` | Admin |
| `user` | `user123` | User |
| `approver` | `approver123` | Approver |

Frontend once gercek API'ye login istegi atar. API calismiyorsa ayni demo kullanicilarla local fallback devreye girer; boylece UI gelistirmesi backend olmadan da devam edebilir.

Yeni kullanici kaydi login ekranindaki `Kaydol` modundan yapilir. Kayit `PendingApproval` durumunda olusur. Admin, `Yonetim` ekranindan kullaniciyi `Active` yapabilir, rol atayabilir, gecici sifreyle yeni kullanici olusturabilir ve kullanici oturumlarini gorebilir/kapatabilir. Admin-created kullanicilar `MustChangePassword=true` baslar; normal workspace'e girmeden once zorunlu sifre degistirme ekranindan gecmek zorundadir. Manuel sifre secilmezse backend guclu bir gecici sifre uretir ve mail provider `Mailtrap`/`Smtp` ise kullaniciya e-posta ile gonderir. Admin, is akisi gecmisi olmayan test kullanicilarini silebilir; process/form/task/audit gecmisi olan kullanicilar icin backend silmeyi reddeder. `Ayarlar` ekraninda profil guncelleme, sifre degistirme, email verification OTP akisi, aktif oturumlar, tek oturum kapatma ve tum cihazlardan cikis akisi denenebilir.

Admin kullanicisi `Loglar` ekraninda sistem gecmisini arayabilir. Loglar varsayilan olarak toplu dokulmez; kisi, surec, entity veya aksiyon aramasi ile server-side paginated sonuc ve ilgili kronolojik gecmis gorulur. Bu liste register, login/logout, rol/status degisikligi, form create/update, process start ve task approve/reject gibi kritik aksiyonlari kullanici, entity ve zaman bilgisiyle takip eder. Surec detay ekranindaki audit timeline ise ilgili surecin state history bilgisini gosterir; sureci baslatan kullanici kendi surec gecmisini, Admin/Approver ise gorebildigi sureclerin gecmisini inceleyebilir.

Local SQLite demo DB varsayilan olarak iki form, sekiz surec, acik onay tasklari ve audit log ornekleriyle gelir. Detaylar icin `docs/08-local-database.md` dosyasina bak.

## Current Demo Flow

1. Login ol.
2. Role gore menu ve dashboard'u gor.
3. Admin kullanicisiyle seeded formlari ve dashboard metriklerini incele.
4. Form tasarimi ekraninda kayitli bir formu sec, alan modelini duzenle ve guncelle.
5. Form runner ekraninda seeded veya guncellenmis bir form secip yeni surec baslat.
6. Approver kullanicisiyla `Islerim` ekranindan task approve/reject akisini dene.
7. Surec detayinda JSON veri ve audit log mantigini incele.

## Troubleshooting

- API login calismiyorsa once `http://localhost:5291/swagger` adresini kontrol et.
- Web login API'ye ulasamiyorsa `NEXT_PUBLIC_API_BASE_URL` degerinin API portuyla ayni oldugunu kontrol et.
- Port doluysa ilgili process'i kapat veya farkli port kullan.
- Frontend paket hatalarinda `apps/web/node_modules` silinip `npm install` tekrar calistirilabilir.

## Documentation

- `docs/00-requirements-from-pdf.md`
- `docs/01-agent-notes.md`
- `docs/02-architecture.md`
- `docs/03-bpm-and-state-machine.md`
- `docs/04-api-and-services.md`
- `docs/05-code-review-guide.md`
- `docs/06-team-presentation-split.md`
- `docs/07-product-todo.md`
- `docs/08-local-database.md`
- `docs/09-ozgun-form-flow.md`
- `docs/10-ufuk-access-shell-flow.md`
- `docs/11-i18n-language-support.md`
- `docs/12-cagdas-process-flow.md`
