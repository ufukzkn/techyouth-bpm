# Docker Ve PostgreSQL Ortami

## Amac

Local varsayilan gelistirme akisi SQLite ile hizli kalir. Docker Desktop'ta iki ayri Compose uygulamasi gorunur: `eczacibasi-local` ve `eczacibasi-cloud`. Ilki SQLite volume kullanir; ikincisi yerel PostgreSQL container'i kurmadan Neon'a baglanir.

## Compose Akisi

```powershell
# eczacibasi-local: SQLite API + Web
docker compose -f compose.cloud.yaml down
docker compose up -d --build

# eczacibasi-cloud: Neon API + Web
docker compose down
docker compose -f compose.cloud.yaml up -d --build
```

Iki Compose uygulamasi da ayni host portlarini kullanir: web `3000`, API `5291`. Bu nedenle bir uygulamayi kapatmadan digerini baslatma. Docker Desktop'ta local stack `eczacibasi-local-api-1` ve `eczacibasi-local-web-1`; cloud stack ise `eczacibasi-cloud-api-1` ve `eczacibasi-cloud-web-1` olarak gorunur.

Her iki API de baslangicta `Database.MigrateAsync()` ile semayi uygular ve `Seed__MockData=true` ile deterministic demo verisini ekler. Cloud API, gitignored `.env.neon.local` dosyasindan provider, Neon connection string ve mail ayarlarini alir. Web build asamasinda tarayicinin erisecegi `NEXT_PUBLIC_API_BASE_URL=http://localhost:5291` degerini alir.

Local Compose'ta API her zaman non-root `app` kullanicisiyla calisir. Eski bir image tarafindan root sahipligiyle olusturulmus `sqlite-data` volume'u varsa, `sqlite-volume-init` ayni API image'ini yalniz bir kez root olarak calistirir ve `/data` sahipligini uygulama UID/GID `1654` icin duzeltir. API bu init adimi basariyla bitmeden acilmaz. Web de API'nin `/health/ready` endpointi saglikli olana kadar bekler. Bu nedenle eski volume'lerde gorulen `attempt to write a readonly database` hatasi volume silmeden giderilir.

Adresler:

- Web: `http://localhost:3000`
- API / Swagger: `http://localhost:5291/swagger`

Kapatma ve reset:

```powershell
# local SQLite stack
docker compose down
docker compose down -v # SQLite volume ve demo verisini de siler

# Mevcut local stack'in API readiness durumunu kontrol et
powershell -ExecutionPolicy Bypass -File scripts/smoke-local-compose.ps1

# Neon cloud stack
docker compose -f compose.cloud.yaml down
```

Docker Desktop uzerinden baslatmak icin iki stack'i de once durmus halde olusturabilirsin:

```powershell
docker compose create --build --force-recreate
docker compose -f compose.cloud.yaml create --build --force-recreate
```

Iki stack ayni host portlarini paylastigi icin Docker Desktop'tan ayni anda yalnizca birini baslat.

## Neon

Neon remote PostgreSQL icindir; secret connection string hicbir zaman tracked compose veya appsettings dosyasina yazilmaz. `compose.cloud.yaml` gitignored `.env.neon.local` dosyasini sadece container runtime'inda okur. .NET user-secrets de terminal gelistirme akisi icin alternatiftir. Npgsql anahtar-deger bicimi tercih edilir:

```text
Database__Provider=PostgreSql
ConnectionStrings__DefaultConnection=Host=your-neon-host;Port=5432;Database=your-database;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true
```

Varsayilan `public` semasi disinda ozel bir PostgreSQL semasi kullaniliyorsa
connection string icindeki `Search Path=your_schema` degerine ek olarak
`Database__Schema=your_schema` verilmelidir. Bu ayar EF migration history
tablosunu da ayni semaya sabitler. Normal Neon/public kurulumunda bu ayar
gerekmez.

Migration ve smoke test tamamlandiginda Neon, ortak ekip testi / production-ready veritabani anlatisi icin kullanilabilir. Local SQLite ise hizli resetlenebilir demo ortami olarak korunur.

## Guvenlik Notu

SMTP, Mailtrap, Neon ve benzeri secret'lar `.env.*`, user-secrets veya CI secret store icinde tutulur; git'e eklenmez. Cloud Compose dosyasi secret degerini degil, yalnizca `.env.neon.local` dosyasinin yolunu tasir.

API runtime image'i root olarak çalışmaz. .NET image'indeki `app` kullanıcısı
uygulamayı çalıştırır; yalnız SQLite için gereken `/data` dizini bu kullanıcıya
yazılabilir bırakılır. `sqlite-volume-init` yalnız legacy local volume sahipligini
düzeltmek icin root kullanir ve uygulama prosesi olarak calismaz. Web runtime da
ayrı `nextjs` kullanıcısıyla çalışır.
Production ortamında CORS origin'leri `Cors__AllowedOrigins__0` benzeri
konfigürasyonla açıkça verilmelidir; credentials kullanılırken wildcard kabul
edilmez. HTTPS terminasyonu arkasında HSTS ve `Secure` cookie davranışı
korunmalıdır.
