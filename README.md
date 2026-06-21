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

## Run Locally

Iki ayri terminal kullanmak en temiz yoldur.

Terminal 1 - API:

```bash
cd apps/api
dotnet run --project src/TechYouthBpm.Api --urls http://localhost:5291
```

API ayaga kalkinca Swagger acilir:

```bash
http://localhost:5291/swagger
```

Terminal 2 - Web:

```bash
cd apps/web
npm run dev -- --hostname 127.0.0.1 --port 3000
```

Web uygulamasi:

```bash
http://127.0.0.1:3000
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

## Current Demo Flow

1. Login ol.
2. Role gore menu ve dashboard'u gor.
3. Admin kullanicisiyle form designer taslagini incele.
4. Form runner taslaginda validation davranisini dene.
5. Process board uzerinden task approve/reject akisini gor.
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
