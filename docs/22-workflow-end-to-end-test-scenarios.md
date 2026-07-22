# Workflow Uçtan Uca Test Senaryoları

Bu doküman dinamik form, workflow, takım ataması, claim, bildirim ve audit zincirini aynı sırayla doğrulamak için kullanılır. Local demo SQLite ile çalışır; aynı senaryolar Neon/PostgreSQL smoke testinde de uygulanabilir.

## Hazırlık

1. Veritabanını migration ve güncel seed ile oluşturun.
2. API ve web uygulamasını başlatın.
3. `Transfer Teklif ve Onay Akışı` ile `Acil Sevkiyat ve Teslimat Akışı` tanımlarının son sürümünün `Published` olduğunu doğrulayın.
4. Tarayıcı Network panelinde başarısız istek olmadığını kontrol edin.

Kullanılacak demo hesapları:

| Amaç | Kullanıcı | Şifre |
| --- | --- | --- |
| SuperAdmin | `admin` | `admin123` |
| Sportif Faaliyetler Admin | `fatih.terim` | `imparator123` |
| Scout/teknik aday | `quaresma` | `trivela123` |
| Teknik takım sorumlusu | `approver` | `approver123` |
| Finans normal üyesi | `okan.buruk` | `okan123` |
| Lojistik depo sorumlusu | `atiba` | `atiba123` |
| Lojistik teslimat sorumlusu | `sergen.yalcin` | `sergen123` |

Hizli Sportif Faaliyetler demo akislari icin ortak parola `sport123` kullanilir:

| Amaç | Kullanıcı |
| --- | --- |
| Topluluk Admin | `sport.admin` |
| Süreç başlatıcı | `sport.starter` |
| Scout Ekibi sorumlusu, Onay Sorumlusu rolü | `sport.scout` |
| Teknik takım sorumlusu, Onay Sorumlusu rolü | `sport.approver` |
| Mali İşler takım sorumlusu, Onay Sorumlusu rolü | `sport.finance` |
| Transfer Operasyon sorumlusu, Onay Sorumlusu rolü | `sport.operations` |
| Gözlemci | `sport.viewer` |

## Senaryo A: Transfer Teklif ve Onay

1. `fatih.terim` ile giriş yapın ve Form Başlat alanından son published workflow sürümünü seçin.
2. İki form sayfasını doldurun. Teklif tutarını `7.500.000 EUR` girin; PDF metadata alanına geçerli bir `.pdf` seçin.
3. `Acil Değerlendirme` seçildiğinde gerekçenin zorunlu olduğunu, seçilmediğinde opsiyonel kaldığını doğrulayın.
4. Süreci başlatın. Süreç detayında pinned workflow/form version, `start` değişkenleri, ilk step execution, system audit ve Scout bildirimi oluşmalıdır.
5. `quaresma` ile Scout işini claim edin, task formunu doldurun ve onaylayın.
6. `approver` ile Teknik Değerlendirme işini claim edip onaylayın.
7. Tutar 5 milyonun üzerinde olduğu için akış Mali Onay adımına gitmelidir. `steps.scoutReview` ve `steps.technicalReview` verileri korunmalıdır.
8. `fatih.terim` ile Mali Onay task formunda fiyatı güncelleyin, bütçe kararını girin ve onaylayın.
9. `quaresma` ile Transfer Operasyonu işini görüntüleyin. Kullanıcı adaydır fakat takım sorumlusu olmadığı için claim/aksiyon reddedilmeli ve açıklayıcı kilit mesajı görünmelidir.
10. `fatih.terim` ile aynı işi claim edin; sözleşme metadata dosyasını, tarihi ve operasyon notunu girip tamamlayın.
11. Süreç `Completed` olmalı; bütün task, step, form çıktısı, actor, tarih, bildirim ve audit zinciri görüntülenmelidir.

Takım adları operasyon bağlamını, ortak `Onay Sorumlusu` rolü permission paketini belirler. Seed bu dört takım için aynı izinleri taşıyan ayrı rol kopyaları üretmez.

Ek dallar:

- Teklif tutarı 5 milyonun altındaysa Mali Onay atlanıp doğrudan Transfer Operasyonuna geçmelidir.
- Teknik veya mali task `SendBack` ile önceki user task’a dönmeli; yeni task için SLA ve `DueAt` yeniden hesaplanmalıdır.
- Scout/teknik/mali `Reject` aksiyonu süreci `Rejected` end node’una taşımalıdır.

## Senaryo B: Acil Sevkiyat ve Teslimat

1. Lojistik topluluğunda süreç başlatma yetkili bir kullanıcıyla son published sürümü açın.
2. İki sayfalı sevkiyat formunu doldurun; `Acil Sevkiyat` ve `Hassas Yük` seçin.
3. Hassas yük için özel taşıma talimatının zorunlu olduğunu ve sevk belgesinin yalnız metadata olarak saklandığını doğrulayın.
4. Süreci başlatın. Gateway `urgentDispatch` dalını seçmeli; task `Critical` öncelikli ve iki saat SLA’lı olmalıdır.
5. `atiba` ile lider kilitli depo görevini claim edip onaylayın.
6. `sergen.yalcin` ile teslimat task formuna kanıt metadata’sı ve süre girin; `Complete` ile süreci bitirin.
7. Alternatif çalıştırmada `Acil Sevkiyat` seçmeyin. Gateway `standardDispatch` dalını seçmeli ve altı saat SLA uygulamalıdır.
8. Teslimat adımında `SendBack`, işi acil depo düzeltme adımına döndürmelidir.

## Sözleşme ve Negatif Kontroller

- Aynı toplulukta aynı workflow adıyla ikinci tanım oluşturma temiz, lokalize hata döndürmelidir; yarım draft oluşmamalıdır.
- Başka topluluğun form, takım, rol veya kullanıcısı graph’a bağlanamamalıdır.
- Published form ve workflow sürümü değiştirilememeli; düzenleme yeni sürüm üretmelidir.
- Yeni sürüm yayınlandıktan sonra çalışan süreç kendi eski sürümünde devam etmelidir.
- Task formu eksik veya hatalıysa backend aksiyonu reddetmeli ve task açık kalmalıdır.
- İki aday aynı task’ı eş zamanlı claim ederse yalnız biri kazanmalıdır.
- Takım sorumlusu olmayan adayın doğrudan API claim/action çağrısı da reddedilmelidir.
- Claim edilmemiş lider görevinde yetkili lider veya `Tasks.ManageAll` sahibi kullanıcı `CanClaim=true` görmelidir; `CanAct=false` değeri claim öncesinde lider engeli olarak yorumlanmamalıdır.
- `Geçmiş İşlerim` yalnız oturumdaki kullanıcının tamamladığı taskları göstermeli; süresi geçmiş açık task aktif listede kalmalıdır.
- Dosya alanı gerçek binary saklamaz; ad, boyut, MIME, uzantı ve `lastModified` metadata’sını doğrular.

## Audit ve Bildirim Beklentileri

Her mutasyonda actor, community, entity, action ve zaman bilgisi aynı transaction sonucunda görünmelidir. Şunları özellikle kontrol edin:

- workflow/form sürümü yayınlama
- süreç başlatma
- task oluşturma, claim, release ve action
- gateway geçişi, send-back ve end durumu
- takım üyeliği ve sorumlu değişimi
- kullanıcı rol/durum değişimi ve pending kayıt isteği

Bildirim okundu/okunmadı değişikliği audit üretmez. Kaynak iş olayı audit üretir; hedef kullanıcıya oluşan bildirim aynı entity kimliğine bağlanır.

## Otomatik Doğrulama

`WorkflowRoleAndActionIntegrationTests.Transfer_Workflow_Enforces_Form_Team_Role_Claim_Approve_And_Reject_Through_Http` gerçek controller hattında şu zinciri tek veritabanı üzerinde doğrular:

- koşullu başlangıç formu ve dosya metadata validasyonu
- takım + community role kesişimi ve yanlış aday reddi
- claim, eksik task formu reddi, release ve yeniden claim
- Scout onayıyla Teknik Değerlendirme adımına geçiş
- başka kullanıcının claim ettiği task'a müdahale reddi
- teknik ret ile süreç kapanışı
- `start`/`steps.<nodeKey>` çıktıları, process audit, system audit ve bildirimler

`Existing_Session_Reevaluates_Role_And_Team_Membership_On_Every_Request` aynı access token açıkken community role değişiminin görevi görünür yaptığını, takım üyeliği kapatılınca aynı token ile görevin yeniden gizlendiğini kanıtlar. Token yetki snapshot'ı taşımaz; backend aktif rol ve takımları her protected istekte veritabanından çözer.

`Three_sportif_demo_workflows_complete_both_http_outcomes_with_their_real_candidates` üç hızlı Sportif Faaliyetler akışını gerçek login, process start, claim, task formu ve action endpointleri üzerinden hem Onay hem Ret çıkışında tamamlar. Böylece takım, takım+rol ve yalnız takım sorumlusu atamaları aynı tekrarlanabilir demo setinde sınanır.

Güncel doğrulama tabanı 231 backend ve 60 frontend testidir. Servis/unit
testleri ayrıca cookie-only browser transport, bir dakikalık access-session
expiry, remembered refresh recovery, cookie logout, gateway, SendBack, Complete,
Escalate, takım sorumlusu kilidi, eşzamanlı claim, version pinning, transaction
rollback, pagination, OpenAPI sözleşmesi, task özet/detail ayrımı ve büyük fixture
sorgu sınırlarını kapsar.

Sekiz Playwright senaryosu gerçek API ve web sunucularıyla şunları otomatikleştirir:

- cookie session reload ve logout
- normal kullanıcının doğrudan yönetim route'una erişememesi
- form yayınlama, workflow'a bağlama ve süreç başlatma
- takım+rol adaylığında claim zorunluluğu ve task action sınırı
- Aktif/Geçmiş geçişinde slider'ın sabit kalması ve son isteğin state'i kazanması
- Süreç durum filtresinde toolbar sabitken yalnız liste gövdesinin yenilenmesi
- Takım üyesi iş yükünün seçilen kişinin kartının hemen altında açılması
- Form Designer kabuğunun DnD canvas chunk'ından bağımsız yüklenmesi

Bu otomasyon işlevsel zinciri korur; çok kullanıcılı tam Transfer sunumu,
real-device touch/zoom ve görsel yerleşim hâlâ bu belgedeki manuel kabul
adımlarıyla doğrulanır.

```powershell
dotnet test apps/api/TechYouthBpm.slnx
cd apps/web
npm run test
npm run lint
npm run build
npm run test:e2e
```

Opt-in PostgreSQL smoke testi için `TECHYOUTH_TEST_POSTGRES_CONNECTION` tanımlanır. Test geçici schema kullanır ve Neon demo verisini değiştirmez.
