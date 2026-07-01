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
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=techyouth-bpm.db"
  }
}
```

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

API ayaga kalkinca Swagger acilir:

```bash
http://localhost:5291/swagger
```

Ilk calistirmada API, secili veritabani uzerinde demo kullanicilari ve mock BPM verisini seed eder. SQLite dosyasi localde olusur; PostgreSQL/Neon modunda tablolar secili uzak veritabaninda olusturulur.

SQLite ile local demo veritabanini sifirlamak icin:

```powershell
./scripts/run-api-local.ps1 -ResetDb
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

Local SQLite demo DB varsayilan olarak iki form, sekiz surec, acik onay tasklari ve audit log ornekleriyle gelir. Detaylar icin `docs/08-local-database.md` dosyasina bak.

## Current Demo Flow

1. Login ol.
2. Role gore menu ve dashboard'u gor.
3. Admin kullanicisiyle seeded formlari ve dashboard metriklerini incele.
4. Form runner ekraninda seeded bir form secip yeni surec baslat.
5. Approver kullanicisiyla `Islerim` ekranindan task approve/reject akisini dene.
6. Surec detayinda JSON veri ve audit log mantigini incele.

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
