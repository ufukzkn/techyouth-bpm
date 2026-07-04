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
    "MaxFailedLoginAttempts": 5,
    "LockoutMinutes": 10,
    "RateLimitPermitLimit": 10,
    "RateLimitWindowMinutes": 1
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=techyouth-bpm.db"
  }
}
```

`Auth:SessionDurationMinutes` normal oturum suresini dakika cinsinden belirler ve su anda 120 dakikadir. `Auth:RememberMeDurationMinutes` beni-hatirla secenegi icin kullanilir ve su anda 30 gunluk sureye ayarlidir. `Auth:MaxFailedLoginAttempts` ve `Auth:LockoutMinutes` yanlis giris denemelerinden sonra gecici hesap kilitlemeyi belirler. `Auth:RateLimitPermitLimit` ve `Auth:RateLimitWindowMinutes` login/register endpointlerini sinirlar.

Auth modeli JWT degildir; backend opaque bearer session token uretir. Token'in sadece hash'i veritabaninda saklanir. Kullanici sifreleri PBKDF2 hash olarak tutulur; logout ve oturum kapatma islemleri session'i veritabaninda revoke eder. Register olan hesaplar `PendingApproval` baslar, Admin onayi olmadan login olamaz.

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

## Run Locally

Iki ayri terminal kullanmak en temiz yoldur.

Terminal 1 - API:

```powershell
./scripts/run-api-local.ps1
```

Script varsayilan olarak 120 dakikalik normal session kullanir. Timeout testini hizlandirmak icin:

```powershell
./scripts/run-api-local.ps1 -SessionDurationMinutes 1
```

API ayaga kalkinca Swagger acilir:

```bash
http://localhost:5291/swagger
```

Ilk calistirmada API, secili veritabani uzerinde demo kullanicilari ve mock BPM verisini seed eder. SQLite dosyasi localde olusur; PostgreSQL/Neon modunda tablolar secili uzak veritabaninda olusturulur.

SQLite ile local demo veritabanini sifirlamak icin:

```powershell
./scripts/run-api-local.ps1 -ResetDb
```

Identity veya schema alanlari degistiginde mevcut SQLite dosyasi yeni kolonlari otomatik alamayabilir. Boyle durumlarda local test icin reset onerilir:

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
- `http://localhost:3000/users`
- `http://localhost:3000/logs`
- `http://localhost:3000/settings`

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

Yeni kullanici kaydi login ekranindaki `Kaydol` modundan yapilir. Kayit `PendingApproval` durumunda olusur. Admin, `Yetki ve Onay` ekranindan kullaniciyi `Active` yapabilir ve rol atayabilir. `Ayarlar` ekraninda email verification demo kodu, aktif oturumlar, tek oturum kapatma ve tum cihazlardan cikis akisi denenebilir.

Admin kullanicisi `Loglar` ekraninda sistem gecmisini arayabilir. Loglar varsayilan olarak toplu dokulmez; kisi, surec, entity veya aksiyon aramasi ile paginated sonuc ve ilgili kronolojik gecmis gorulur. Bu liste register, login/logout, rol/status degisikligi, form create/update, process start ve task approve/reject gibi kritik aksiyonlari kullanici, entity ve zaman bilgisiyle takip eder. Surec detay ekranindaki audit timeline ise ilgili surecin state history bilgisini gosterir; sureci baslatan kullanici kendi surec gecmisini, Admin/Approver ise gorebildigi sureclerin gecmisini inceleyebilir.

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
