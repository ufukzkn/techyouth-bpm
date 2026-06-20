# TechYouth BPM Wizard

TechYouth School 2. donem proje gereksinimleri icin hazirlanan full-stack BPM wizard uygulamasi.

Bu repo iki ana uygulamadan olusur:

- `apps/web`: Next.js + TypeScript frontend
- `apps/api`: .NET 8 Web API backend

Dokumantasyon `docs/` altindadir. Proje ilerledikce mimari kararlar, servis isleyisi, code review notlari ve ekip sunum dagilimi buradan takip edilir.

## Planned Local Commands

```bash
# backend
cd apps/api
dotnet run

# frontend
cd apps/web
npm run dev
```

## Demo Users

| Username | Password | Role |
| --- | --- | --- |
| `admin` | `admin123` | Admin |
| `user` | `user123` | User |
| `approver` | `approver123` | Approver |

## Documentation

- `docs/00-requirements-from-pdf.md`
- `docs/01-agent-notes.md`
- `docs/02-architecture.md`
- `docs/03-bpm-and-state-machine.md`
- `docs/04-api-and-services.md`
- `docs/05-code-review-guide.md`
- `docs/06-team-presentation-split.md`
