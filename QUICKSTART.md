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

Neon, Mailtrap ve Docker secenekleri icin [README.md](README.md) dosyasina bakin. Secret degerlerini repoya eklemeyin.
