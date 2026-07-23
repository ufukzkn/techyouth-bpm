# TechYouth BPM Wizard

TechYouth School 2. dönem proje gereksinimleri için geliştirilen full-stack,
dinamik form ve BPM iş akışı uygulamasıdır. Platform; topluluk, rol ve takım
sınırları içinde form tasarlamayı, görsel akış yayınlamayı, süreç başlatmayı,
işleri aday havuzundan üstlenmeyi ve bütün hareketleri denetlenebilir biçimde
izlemeyi sağlar.

**Hızlı demo:** En zahmetsiz ve önerilen çalışma yolu **Docker + SQLite**'tır.
Tek komut akışı için [Hızlı Kurulum](#hızlı-kurulum) bölümüne bakın.

**Canlı demo:** [https://techyouth-bpm.vercel.app](https://techyouth-bpm.vercel.app)

## Proje ve Repo Yapısı

| Yol | Sorumluluk |
| --- | --- |
| `apps/web` | Next.js, React ve TypeScript frontend |
| `apps/api/src/TechYouthBpm.Api` | HTTP, cookie, CSRF, middleware ve controller katmanı |
| `apps/api/src/TechYouthBpm.Application` | DTO'lar, servis kontratları ve use-case sınırları |
| `apps/api/src/TechYouthBpm.Domain` | Entity ve enum'lar |
| `apps/api/src/TechYouthBpm.Infrastructure` | EF Core, auth, e-posta, workflow runtime ve seed implementasyonları |
| `apps/api/tests/TechYouthBpm.Tests` | Unit, servis ve HTTP entegrasyon testleri |
| `scripts` | Local, Neon, Docker smoke ve yardımcı çalıştırma scriptleri |
| `docs` | Gereksinim, mimari, API, senaryo, sunum ve kalite belgeleri |

Hızlı başvuru için [QUICKSTART.md](QUICKSTART.md), doküman haritası için
[docs/README.md](docs/README.md), sunuma hazırlanmak için
[docs/23-presentation-study-guide.md](docs/23-presentation-study-guide.md)
kullanılabilir.

## Kullanılan Teknolojiler ve Sürümler

| Alan | Teknoloji | Sürüm |
| --- | --- | --- |
| Frontend | Next.js / React / TypeScript | `16.2.9` / `19.2.4` / `5.9.3` |
| Global state | Zustand | `5.0.14` |
| Form drag/drop | `@dnd-kit/core` / `@dnd-kit/sortable` | `6.3.1` / `10.0.0` |
| Workflow canvas | `@xyflow/react` | `12.11.2` |
| UI ikonları | Lucide React | `1.24.0` |
| Backend | ASP.NET Core / .NET | `8.0` |
| ORM ve sağlayıcılar | EF Core SQLite / Npgsql PostgreSQL | `8.0.11` |
| Veritabanı | SQLite / Neon PostgreSQL | Local dosya / doğrulanmış ekip ortamı `18.4` |
| Backend test | xUnit / WebApplicationFactory | `2.5.3` / `8.0.11` |
| Frontend test | Vitest / Playwright | `3.2.7` / `1.61.1` |
| CI uyumluluk ortamı | Node / .NET / PostgreSQL | `24` / `8` / `16` |

Kesin JavaScript bağımlılıkları
[apps/web/package-lock.json](apps/web/package-lock.json), .NET paketleri ilgili
`.csproj` dosyaları ve CI sürümleri `.github/workflows` altındaki iş akışları
tarafından sabitlenir. Neon `18.4` ekibin doğrulanmış cloud ortamıdır;
PostgreSQL `16` ise CI uyumluluk ortamıdır.

## Kurulması Gereken Araçlar

Seçtiğiniz çalışma yöntemine göre araç ihtiyacı değişir:

| Yöntem | Gerekli araçlar |
| --- | --- |
| Terminal + SQLite | Git `2.x`, .NET SDK `8.x`, Node.js `24.x` (minimum `20.x`), npm `11.x` (minimum `10.x`) |
| Docker + SQLite | Git `2.x`, güncel Docker Desktop ve Docker Compose v2 |
| Docker + PostgreSQL/Neon | Git `2.x`, Docker Desktop, Compose v2 ve erişilebilir bir PostgreSQL veritabanı |
| Native PostgreSQL | Terminal araçlarına ek olarak erişilebilir PostgreSQL/Neon; local PostgreSQL kurulumu zorunlu değildir |

Sürüm kontrolü:

```powershell
git --version
node --version
npm --version
dotnet --version
docker --version
docker compose version
```

## Tamamlanan Gereksinimler

- Next.js tabanlı responsive, authenticated ve rol bazlı workspace.
- Login, kayıt, onay, parola kurtarma, oturum listeleme ve merkezi revoke.
- Global session/user state ve permission tabanlı route/navigation kontrolü.
- Dinamik form tasarlama, çok adımlı form, alan sıralama, yayınlama, arşivleme
  ve immutable form sürümleri.
- Text, textarea, number, email, select, radio, checkbox, date ve dosya
  metadata alanları; zorunlu, tip ve bağımlı validasyon.
- Loading, success ve error durumları ile taşmayan/kopyalanabilen JSON görünümü.
- .NET 8 REST API, Swagger/OpenAPI, EF Core migrations ve deterministic seed.
- SQLite ile PostgreSQL/Neon sağlayıcı desteği.
- Görsel workflow graph, gateway koşulları, form-adım bağlama ve sürüm pinning.
- Kişi, takım, rol ve takım+rol adaylığı; claim/release ve
  Approve/Reject/Complete/SendBack/Escalate aksiyonları.
- Task form doğrulaması, SLA/DueAt, süreç adım geçmişi ve transaction sınırları.
- Sistem audit kayıtları, bildirim merkezi, gelen kutusu ve rol/topluluk scope'u.
- Backend, frontend ve Playwright testleri ile GitHub Actions kalite kapıları.

PDF gereksinimlerinin zorunlu ve bonus karşılıkları ayrıntılı olarak
[docs/00-requirements-from-pdf.md](docs/00-requirements-from-pdf.md) içinde
izlenir. Zorunlu kapsamda bilinen bir açık bulunmamaktadır.

## Tamamlanmayan Gereksinimler ve Bilinçli Sınırlar

Aşağıdakiler zorunlu gereksinim eksiği değil, bilinçli ürün veya production
sınırlarıdır:

- Dosya alanı binary içeriği yüklemez; güvenli dosya metadata'sı saklar. Object
  storage, MIME/içerik taraması ve signed URL sonraki aşamadır.
- Parallel gateway, zamanlayıcıyla otomatik SLA eskalasyonu ve service task
  henüz yoktur. Typed exclusive gateway, `DueAt` ve manuel `Escalate` vardır.
- BPMN XML import/export ve Camunda deployment yapılmaz.
- Harici ERP/kurumsal servis entegrasyonu yapılmaz.
- Sentry/Seq/Datadog entegrasyonu ve yayınlanmış kısa demo videosu henüz
  yoktur.

Bu tercihler proje kapsamı gereği bilinçlidir: hazır bir BPM motorunu veya
kurumsal sistemi bağlamak yerine dinamik form, yetkilendirme ve iş akışı
mantığının ekip tarafından geliştirilip gösterilmesi hedeflendiği için özel
.NET workflow runtime kullanılmış, Camunda ve ERP bağımlılıkları eklenmemiştir.

## Geliştirilen Ek Özellikler

- Topluluk, davet kodu, custom role, işlem bazlı permission ve SuperAdmin
  yönetimi.
- Çoklu takım üyeliği, takım sorumlusu ve sanal `Takımsız` görünümü.
- Camunda/Kissflow esintili sürükle-bırak workflow tasarımcısı ve swimlane.
- Kişisel/topluluk/global dashboard kapsamı ve role göre çalışma özeti.
- Kategorili audit araması, süreç timeline'ı ve yapılandırılmış bildirim akışı.
- TR/EN dil desteği, light/dark tema ve mobil alan paleti.
- RFC 7807 ProblemDetails, correlation ID, health endpointleri ve non-root
  Docker image'ları.
- SQLite ve cloud PostgreSQL için ayrı Docker Compose uygulamaları.

## Mimari ve Yöntemsel Kararlar

### Katmanlı Mimari

- **API:** HTTP sözleşmesi, cookie/CSRF davranışı, middleware ve controller
  sınırıdır. İş kuralını controller içine taşımaz.
- **Application:** DTO'ları, servis kontratlarını ve uygulama use-case
  sınırlarını tanımlar. Infrastructure ayrıntılarına bağımlı değildir.
- **Domain:** Kullanıcı, topluluk, takım, form, workflow, süreç ve task
  entity/enum'larını barındırır.
- **Infrastructure:** EF Core erişimi, kimlik doğrulama, e-posta gönderimi,
  özel workflow runtime ve idempotent seed işlemlerini uygular.

Generic Repository eklenmemiştir. EF Core `DbContext/DbSet` zaten repository ve
unit-of-work sorumluluklarını sağlar; ikinci bir genel katman sorgu gücünü
gizleyip gerçek bir iş sınırı üretmeden kod miktarını artıracaktı. Servisler iş
kurallarını ve transaction sınırlarını, `AppDbContext` ise veri erişimini
yönetir.

### Opaque Session, Cookie, Bearer ve CSRF

Bu uygulama **JWT kullanmaz**. Sunucu tahmin edilemez, anlam taşımayan bir
opaque token üretir ve veritabanında token'ın kendisini değil hash'ini saklar.
`Bearer` burada token biçimini değil, HTTP ile nasıl taşındığını ifade eder.

- Normal tarayıcı girişi `POST /api/auth/browser-login` kullanır. Access ve
  refresh token yalnız `HttpOnly` cookie'de tutulur; JSON response'a,
  Zustand'a veya `localStorage`'a girmez.
- Sayfa yenilendiğinde oturum `GET /api/auth/me` ile toparlanır.
- Swagger veya harici API istemcisi `POST /api/auth/login` üzerinden opaque
  access token alabilir ve `Authorization: Bearer <opaque-token>` gönderir.
- Beni hatırla seçildiğinde hashed refresh token üretilir. Refresh sırasında
  token rotate edilir; eski refresh token tekrar kullanılırsa ilgili session
  zinciri revoke edilir.
- Cookie tabanlı mutation isteklerinde okunabilir CSRF cookie değeri
  `X-CSRF-Token` header'ıyla geri gönderilmelidir. CSRF token kimlik doğrulama
  yetkisi taşımaz.
- Parolalar PBKDF2 hash olarak saklanır. Logout, parola değişimi ve yönetim
  aksiyonları ilgili session'ları revoke edebilir.
- Rol, permission, takım ve topluluk kontrolleri backend'de canlı kullanıcı
  bağlamıyla yeniden değerlendirilir. Kısa session cache'i desteklenen güvenlik
  ve üyelik mutasyonlarında geçersiz kılınır.

Bu tercih, merkezi revoke, refresh reuse tespiti ve rol/yetki değişikliğinin
sonraki isteğe yansıması için bilinçlidir.

### Form ve Workflow Motoru

- Form ve workflow tanımları sürümlenir; yayınlanmış sürümler immutable'dır.
- Process instance başladığı workflow sürümüne, başlangıç ve task adımları da
  ilgili form sürümüne pin edilir.
- Workflow, `schemaVersion` taşıyan typed JSON graph olarak saklanır.
- Assignment hedefi belirli kullanıcı, takım, community role veya takım+rol
  olabilir. Aday kullanıcı görevi claim eder; claim ve aksiyonlar backend
  tarafından tekrar yetkilendirilir.
- Start, task, gateway ve end geçişleri özel .NET runtime tarafından transaction
  içinde yürütülür. Task, step execution, bildirim ve audit kayıtlarından biri
  hata verirse işlem rollback edilir.
- Camunda ve Kissflow modelleme/ürün deneyimi için referanstır; uygulamanın
  gerçek motoru harici Camunda kurulumu değil, bu repodaki .NET runtime'dır.

### Audit ve Teknik Log Ayrımı

- `SystemAuditLog`, login, rol değişimi, takım üyeliği, form/workflow yayınlama,
  process başlatma ve task aksiyonu gibi **iş hareketlerini** saklar.
- `ILogger`, request süresi, correlation ID, operasyonel uyarı ve beklenmeyen
  hata gibi **teknik teşhis** bilgisini üretir.
- Hassas token, cookie, parola ve form payload'u teknik loglara yazılmaz.
- Beklenmeyen API hataları production stack trace'i sızdırmadan RFC 7807
  `ProblemDetails` ve güvenli `traceId` döndürür.

## Hızlı Kurulum

Önce repoyu alın:

```powershell
git clone https://github.com/ufukzkn/techyouth-bpm.git
cd techyouth-bpm
```

> **Önerilen ve en zahmetsiz demo yolu: Docker + SQLite.** Yalnız güncel
> Docker Desktop gerektirir; `.env`, secret, Node.js, .NET SDK veya harici
> veritabanı kurulumu istemeden API, web, migration ve demo seed verisini
> birlikte hazırlar.

### Seçenek A: Docker ile SQLite (Önerilen)

```powershell
# Aynı portları kullanan cloud stack açıksa kapat
docker compose -f compose.cloud.yaml down

docker compose up -d --build
powershell -ExecutionPolicy Bypass -File scripts/smoke-local-compose.ps1
```

Stack'i kapatmak için `docker compose down`, SQLite volume'unu da silerek temiz
başlamak için `docker compose down -v` kullanılır.

### Seçenek B: Terminal ile SQLite

```powershell
dotnet restore apps/api/TechYouthBpm.slnx
npm ci --prefix apps/web
```

İki terminal açın:

```powershell
# Terminal 1
.\scripts\run-api-local.ps1

# Terminal 2
.\scripts\run-web-local.ps1
```

Local SQLite için `.env`, PostgreSQL kurulumu veya secret gerekmez.

### Seçenek C: Docker ile PostgreSQL/Neon

Önce secretsiz şablonu gitignored yerel dosyaya kopyalayın ve kendi bağlantı
bilginizle doldurun:

```powershell
Copy-Item .env.example .env.neon.local
notepad .env.neon.local
```

Ardından local stack'i kapatıp cloud stack'i başlatın:

```powershell
docker compose down
docker compose -f compose.cloud.yaml up -d --build
docker compose -f compose.cloud.yaml ps

Invoke-WebRequest http://localhost:5291/health/ready -UseBasicParsing |
  Select-Object StatusCode
```

Buradaki **cloud**, yalnız veritabanının uzakta olduğunu ifade eder. Web ve API
bilgisayarınızdaki container'larda çalışır. Ayrıntılı bağlantı ve güvenlik
adımları bir sonraki bölümdedir.

## Konfigürasyon Bilgileri ve Kendi Cloud Veritabanınız

### Konfigürasyon Kaynakları

| Dosya / yöntem | Kullanım |
| --- | --- |
| `apps/api/src/TechYouthBpm.Api/appsettings.json` | Secretsiz local varsayılanlar |
| `apps/api/src/TechYouthBpm.Api/appsettings.example.json` | PostgreSQL, auth, CORS ve SMTP örnek şeması |
| `.env.example` | Cloud Compose için secretsiz PostgreSQL şablonu |
| `.env.neon.local` | Gerçek cloud DB bilgileri; gitignored |
| `apps/web/.env.local` | Frontend API adresi override'ı; gitignored |
| `apps/web/vercel.json` | Canlı frontend için Vercel Next.js framework ayarı |
| `render.yaml` | Canlı Render API servisi ve secretsiz runtime ayarları |
| .NET user-secrets | Native API çalıştırmada PostgreSQL/SMTP secret'ları |

Önemli auth varsayılanları:

| Anahtar | Varsayılan | Amaç |
| --- | --- | --- |
| `Auth:SessionDurationMinutes` | `120` | Normal access session süresi |
| `Auth:SessionCacheSeconds` | `15` | Çözülmüş kullanıcı/yetki bağlamının kısa cache süresi; `0` kapatır |
| `Auth:RememberMeDurationMinutes` | `43200` | Beni hatırla süresi |
| `Auth:RefreshTokenDurationMinutes` | `43200` | Refresh token süresi |
| `Auth:PasswordResetMinutes` | `30` | Parola sıfırlama token süresi |
| `Auth:MaxFailedLoginAttempts` | `5` | Geçici kilit öncesi başarısız giriş sayısı |
| `Auth:LockoutMinutes` | `10` | Hesap kilidi süresi |
| `Auth:EmailVerificationMinutes` | `1440` | E-posta doğrulama kodu süresi |
| `Auth:EmailVerificationResendCooldownMinutes` | `5` | Yeniden kod gönderme bekleme süresi |
| `Auth:RateLimitPermitLimit` | `10` | Auth rate-limit kotası |
| `Auth:RateLimitWindowMinutes` | `1` | Auth rate-limit penceresi |

### Kendi PostgreSQL veya Neon Veritabanınızı Bağlama

Cloud akışı ekipteki mevcut Neon secret'ına bağlı değildir. Schema
oluşturma/değiştirme yetkisine sahip istediğiniz PostgreSQL sunucusunu, Neon
projesini veya Neon branch'ini kullanabilirsiniz.

1. `.env.example` dosyasını `.env.neon.local` olarak kopyalayın.
2. `Database__Provider=PostgreSql` değerini koruyun.
3. `ConnectionStrings__DefaultConnection` değerini kendi host, port, database,
   username ve password bilginizle doldurun.
4. SSL gerektiren cloud servislerinde `SSL Mode=Require` kullanın.
5. `docker compose -f compose.cloud.yaml up -d --build` komutunu çalıştırın.

Örnek `.env.neon.local` biçimi:

```text
Database__Provider=PostgreSql
ConnectionStrings__DefaultConnection=Host=your-host;Port=5432;Database=your-database;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true;Channel Binding=Require
Seed__MockData=true
```

> API başlangıçta migration ve idempotent seed işlemini seçtiğiniz veritabanına
> uygular. Bağlantıyı yalnız şema ve demo veri oluşturulmasını kabul ettiğiniz
> bir database/branch için kullanın.

Gerçek connection string hiçbir zaman Git'e eklenmemelidir.
`.env.neon.local`, `.gitignore` tarafından dışlanır.

Native API için geçici PowerShell environment variable alternatifi:

```powershell
$env:Database__Provider = "PostgreSql"
$env:ConnectionStrings__DefaultConnection = "Host=your-host;Port=5432;Database=your-database;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
dotnet run --project apps/api/src/TechYouthBpm.Api --urls http://localhost:5291
```

.NET user-secrets alternatifi:

```powershell
Push-Location apps/api/src/TechYouthBpm.Api
dotnet user-secrets set "Database:Provider" "PostgreSql"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=your-host;Port=5432;Database=your-database;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
Pop-Location
dotnet run --project apps/api/src/TechYouthBpm.Api --urls http://localhost:5291
```

Repo kökündeki `.env.neon.local` dosyasını okuyup aynı ayarları hazırlayan script
alternatifi:

```powershell
.\scripts\run-api-neon.ps1 -Url http://localhost:5291
```

API ve frontend farklı origin'lerde yayınlanacaksa üç ayar birlikte
güncellenmelidir:

- Frontend build-time API adresi: `NEXT_PUBLIC_API_BASE_URL`
- Backend izinli origin listesi: `Cors:AllowedOrigins`
- E-posta doğrulama/parola sıfırlama link tabanı: `Frontend:BaseUrl`

Credentials ile wildcard CORS kabul edilmez. Production connection string ve
SMTP değerleri environment variable veya secret manager üzerinden verilmelidir.

## Veritabanı Migration ve Seed Data

- Migration dosyaları
  `apps/api/src/TechYouthBpm.Infrastructure/Data/Migrations` altındadır.
- API açılışında `Database.MigrateAsync` çalışır; ardından deterministic ve
  idempotent seed uygulanır.
- SQLite ve PostgreSQL aynı `AppDbContext` modelini ve migration geçmişini
  kullanır.
- Seed; örnek kullanıcılar, beş topluluk, roller, takımlar, form/workflow
  sürümleri, süreçler, tasklar, bildirimler ve audit kayıtları oluşturur.
- `Transfer Aksiyon Laboratuvarı` ve `Operasyon Aksiyon Laboratuvarı`, task
  aksiyonları ve farklı assignment biçimleri için tekrarlanabilir demo sağlar.
- Seed yeniden çalıştırıldığında seed-owned veriyi çoğaltmaz ve kullanıcıların
  oluşturduğu kayıtları korur.

Migration'ı elle uygulamak:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project apps/api/src/TechYouthBpm.Infrastructure/TechYouthBpm.Infrastructure.csproj `
  --startup-project apps/api/src/TechYouthBpm.Api/TechYouthBpm.Api.csproj
```

SQLite verisini sıfırlayıp migration ve seed'i yeniden çalıştırmak:

```powershell
.\scripts\run-api-local.ps1 -ResetDb -Force
```

Yalnız temel hesaplarla başlamak için:

```powershell
.\scripts\run-api-local.ps1 -ResetDb -Force -SkipMockData
```

## Erişim Adresleri ve Örnek Hesaplar

| Servis | Adres |
| --- | --- |
| Web uygulaması | `http://localhost:3000` |
| Swagger / OpenAPI | `http://localhost:5291/swagger` |
| API liveness | `http://localhost:5291/health/live` |
| API readiness | `http://localhost:5291/health/ready` |
| Public canlı demo | [https://techyouth-bpm.vercel.app](https://techyouth-bpm.vercel.app) |
| Canlı API readiness | [https://techyouth-bpm-api.onrender.com/health/ready](https://techyouth-bpm-api.onrender.com/health/ready) |

Örnek hesaplar:

| Kullanıcı | Parola | Amaç |
| --- | --- | --- |
| `admin` | `admin123` | Global SuperAdmin |
| `user` | `user123` | Temel süreç başlatıcı |
| `approver` | `approver123` | Temel onay sorumlusu |
| `sport.admin` | `sport123` | Sportif Faaliyetler Topluluk Admin |
| `sport.starter` | `sport123` | Süreç başlatma |
| `sport.scout` | `sport123` | Scout takımı sorumlusu |
| `sport.approver` | `sport123` | Teknik onay |
| `sport.finance` | `sport123` | Mali İşler takım sorumlusu |
| `sport.operations` | `sport123` | Transfer operasyon |
| `sport.viewer` | `sport123` | Salt okunur gözlemci |

Yeni kayıt `PendingApproval` durumunda başlar. Yetkili Topluluk Admin yalnız
kendi topluluğundaki, SuperAdmin ise global kapsamdaki kullanıcıları
onaylayabilir. Admin tarafından geçici parolayla oluşturulan hesap
`MustChangePassword=true` başlar.

## Test ve Kalite Kontrolleri

Backend:

```powershell
dotnet test apps/api/TechYouthBpm.slnx
```

Frontend:

```powershell
npm --prefix apps/web run test
npm --prefix apps/web run lint
npm --prefix apps/web run build
npm --prefix apps/web run test:e2e
```

Playwright izole SQLite veritabanı hazırlayıp API ve web sunucularını kendisi
başlatır. Cookie session, route koruması, form/workflow yayınlama, process
başlatma, claim/action ve community/role sınırlarını gerçek tarayıcıyla
doğrular.

PostgreSQL migration smoke testi normal test koşusunda dış servise bağlanmaz.
İsteğe bağlı çalıştırma:

```powershell
$env:TECHYOUTH_TEST_POSTGRES_CONNECTION = "<postgresql-connection-string>"
dotnet test apps/api/tests/TechYouthBpm.Tests/TechYouthBpm.Tests.csproj `
  --filter "FullyQualifiedName~PostgreSql_Startup_Applies_Migrations"
Remove-Item Env:TECHYOUTH_TEST_POSTGRES_CONNECTION
```

Test benzersiz geçici schema oluşturur, migration/seed/login/form smoke akışını
çalıştırır ve yalnız o schema'yı siler. Paylaşılan demo tablolarına dokunmaz.

GitHub Actions deployment yapmaz. Her push'ta temel build/test/lint kontrolleri;
`master` veya manuel doğrulamada Playwright, PostgreSQL ve Docker kalite
kapıları çalışır. Güncel test sayıları ve kapsamın tek kaynağı
[docs/24-testing-and-quality-gates.md](docs/24-testing-and-quality-gates.md)
dosyasıdır.

## Canlı Demo ve Video Durumu

- Public canlı demo:
  [https://techyouth-bpm.vercel.app](https://techyouth-bpm.vercel.app)
- Canlı mimari: **Vercel Next.js frontend → Render .NET API → Neon
  PostgreSQL**.
- Vercel, `/backend/*` isteklerini Render API'ye yönlendirir; browser cookie ve
  CSRF akışı aynı frontend origin'i üzerinden yürür.
- Render Blueprint yalnız API servisini yönetir; yinelenen ikinci bir frontend
  servisi çalıştırılmaz.
- Kısa demo videosu: **Henüz yok**
- Uygulama terminal veya Docker seçenekleriyle localde tam demo veriyle
  çalıştırılabilir.

## Troubleshooting ve Ayrıntılı Dokümanlar

Yaygın kontroller:

- Login çalışmıyorsa önce `http://localhost:5291/health/ready` ve Swagger'ı
  kontrol edin.
- Web API'ye erişemiyorsa frontend API adresinin API portuyla eşleştiğini
  doğrulayın.
- Docker web ayakta, API kapalıysa `docker compose logs api` çıktısını ve
  readiness durumunu inceleyin.
- Eski SQLite volume'u schema/seed ile uyuşmuyorsa `docker compose down -v`
  sonrasında stack'i yeniden kurun.
- Native SQLite schema'sını temizlemek için
  [Veritabanı Migration ve Seed Data](#veritabanı-migration-ve-seed-data)
  bölümündeki reset akışını kullanın.
- `3000` veya `5291` doluysa ilgili prosesi kapatın ya da scriptlerde farklı
  port seçin.

Doküman haritası:

- [Dokümantasyon rehberi](docs/README.md)
- [PDF gereksinim matrisi](docs/00-requirements-from-pdf.md)
- [Katmanlı mimari](docs/02-architecture.md)
- [API ve servis sözleşmeleri](docs/04-api-and-services.md)
- [Local veritabanı ve seed](docs/08-local-database.md)
- [Docker ve deployment sınırları](docs/17-docker-and-deployment.md)
- [Dinamik workflow ve takım mimarisi](docs/18-dynamic-workflow-and-team-architecture.md)
- [Workflow uçtan uca senaryoları](docs/22-workflow-end-to-end-test-scenarios.md)
- [Sunum çalışma rehberi](docs/23-presentation-study-guide.md)
- [Test ve kalite kapıları](docs/24-testing-and-quality-gates.md)

## Opsiyonel Entegrasyonlar

### E-posta Provider'ları

Varsayılan `Email:Provider=Demo` dış SMTP gerektirmez; doğrulama kodunu local
demo akışında görünür kılar. Gerçek gönderim için `Smtp` veya `Mailtrap`,
allowlist ve sandbox yönlendirmesi için `Routing` kullanılabilir.

Mailtrap Email Sending ve Sandbox örneği:

```powershell
Set-Location apps/api/src/TechYouthBpm.Api
dotnet user-secrets set "Email:Provider" "Routing"
dotnet user-secrets set "Email:FromAddress" "no-reply@your-domain.test"
dotnet user-secrets set "Email:FromName" "TechYouth BPM"
dotnet user-secrets set "Email:Smtp:Host" "live.smtp.mailtrap.io"
dotnet user-secrets set "Email:Smtp:Port" "587"
dotnet user-secrets set "Email:Smtp:Username" "api"
dotnet user-secrets set "Email:Smtp:Password" "your-mailtrap-live-token"
dotnet user-secrets set "Email:Smtp:EnableSsl" "true"
dotnet user-secrets set "Email:AllowedRecipients" "your-test-email@example.com"
dotnet user-secrets set "Email:AllowedUsernames" "your-test-username"
dotnet user-secrets set "Email:Sandbox:FromAddress" "sandbox@your-domain.test"
dotnet user-secrets set "Email:Sandbox:FromName" "TechYouth BPM Sandbox"
dotnet user-secrets set "Email:Sandbox:Smtp:Host" "sandbox.smtp.mailtrap.io"
dotnet user-secrets set "Email:Sandbox:Smtp:Port" "2525"
dotnet user-secrets set "Email:Sandbox:Smtp:Username" "your-sandbox-username"
dotnet user-secrets set "Email:Sandbox:Smtp:Password" "your-sandbox-password"
dotnet user-secrets set "Email:Sandbox:Smtp:EnableSsl" "true"
```

Mailtrap Sandbox bilgileri Sandbox inbox'ın `SMTP` sekmesinden; canlı token ise
Email Sending ayarlarından alınır. Gerçek inbox gönderimi için sending domain
doğrulanmalıdır.

Gmail SMTP alternatifi aynı `Email:Smtp:*` anahtarlarını kullanır:

- Host: `smtp.gmail.com`
- Port: `587`
- Username: Gmail hesabı
- Password: Google uygulama parolası
- SSL: `true`

`Email:AllowedRecipients` ve `Email:AllowedUsernames`, yanlış alıcıya gerçek
e-posta gönderme riskini sınırlar. Doğrulama ve parola sıfırlama linkleri
`Frontend:BaseUrl` üzerinden üretilir. Hiçbir SMTP parolası veya token Git'e
eklenmemelidir.

### Gelişmiş PostgreSQL ve Observability

Opt-in PostgreSQL smoke testi yukarıdaki geçici schema yaklaşımıyla kendi
PostgreSQL/Neon ortamınızda çalıştırılabilir. Production'da migration yetkisi
olmayan ayrı runtime kullanıcısı tercih edilecekse migration deployment
aşamasında ayrıca uygulanmalıdır.

Sentry, Seq, Datadog ve OpenTelemetry şu anda entegre değildir. Uygulama
`ILogger`, JSON production logları, correlation ID, health ve ProblemDetails
altyapısını sağlar; harici telemetry hedefi gerçek hosting ortamı seçildiğinde
eklenmelidir.
