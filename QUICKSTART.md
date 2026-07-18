# Quick Start

Bu rehber, projeyi varsayilan SQLite demo veritabaniyla en kisa yoldan calistirir. Neon, SMTP ve Docker ayarlari zorunlu degildir.

## Gereksinimler

- Git
- Node.js 20+ ve npm 10+
- .NET SDK 8+

## Kurulum

```powershell
git clone https://github.com/ufukzkn/techyouth-bpm.git
cd techyouth-bpm
dotnet restore apps/api/TechYouthBpm.slnx
npm --prefix apps/web install
```

## Calistirma

Iki PowerShell terminali acin.

Terminal 1 - API ve SQLite:

```powershell
.\scripts\run-api-local.ps1
```

Terminal 2 - Next.js web uygulamasi:

```powershell
.\scripts\run-web-local.ps1
```

- Uygulama: `http://localhost:3000`
- Swagger: `http://localhost:5291/swagger`
- SuperAdmin: `admin` / `admin123`
- Kullanici: `user` / `user123`
- Onay sorumlusu: `approver` / `approver123`

Ilk API acilisinda EF Core migrations uygulanir ve gercekci demo form, takim, workflow, process, task, bildirim ve audit kayitlari seed edilir. Eski bir local veritabani sema hatasi verirse:

```powershell
.\scripts\run-api-local.ps1 -ResetDb -Force
```

## Hizli Dogrulama

```powershell
dotnet test apps/api/TechYouthBpm.slnx
npm --prefix apps/web run test
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

Neon ve Mailtrap secenekleri icin [README.md](README.md) dosyasina bakin. Secret degerlerini repoya eklemeyin.

## Docker Ile Hizli Baslatma

Docker Desktop acik olmali. Ilk build, base image ve paketleri indirecegi icin internet baglantisi gerekir.

Local SQLite stack herhangi bir `.env` veya secret istemez:

```powershell
docker compose -f compose.cloud.yaml down
docker compose up -d --build
```

- Uygulama: `http://localhost:3000`
- Swagger: `http://localhost:5291/swagger`

Local stack'i kapatmak veya SQLite demo verisini sifirlamak icin:

```powershell
docker compose down
docker compose down -v
```

Neon cloud stack yalniz repo kokunde gitignored `.env.neon.local` hazirlandiktan sonra calisir:

```powershell
docker compose down
docker compose -f compose.cloud.yaml up -d --build
```

Iki stack de `3000` ve `5291` portlarini kullandigi icin ayni anda acilmamalidir. Cloud stack'i kapatmak icin:

```powershell
docker compose -f compose.cloud.yaml down
```
