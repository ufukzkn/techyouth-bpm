# Arayüz Kullanım Rehberi

Bu rehber, TechYouth BPM arayüzündeki menüleri, temel butonları ve kullanıcı
akışlarını açıklar. Ekran görünürlüğü sabit bir unvandan çok kullanıcının
topluluk rolüne ve işlem bazlı izinlerine göre belirlenir. Bu nedenle burada
anlatılan bir sekme hesabınızda görünmüyorsa ilgili yetkiniz bulunmuyor olabilir.

## Üst Bar

- **Oturum kalkanı:** Kullanıcı adı, topluluk, topluluk rolü, takımlar ve oturum
  bitiş zamanını gösterir.
- **İşlerim kısayolu:** İzin varsa doğrudan aktif işlere gider.
- **Bildirim zili:** Son bildirimleri açar. Bildirim tek tek okundu/okunmadı
  yapılabilir; bütün kayıtlar için **Gelen Kutusunu Aç** kullanılır.
- **TR/EN:** Arayüz dilini değiştirir.
- **Tema:** Açık ve koyu tema arasında geçiş yapar.
- **Çıkış:** API oturumunu kapatır ve giriş ekranına yönlendirir.
- **Mobil menü:** Dar ekranlarda sol menüyü açar ve kapatır.

## Sol Menü Haritası

| Menü | Route | Amaç |
| --- | --- | --- |
| Dashboard | `/dashboard` | Kişisel iş ve süreç özeti, dağılım grafiği, öncelikli kayıtlar ve son bildirimler |
| Takım | `/teams` | Üyesi olduğunuz takımları, takım arkadaşlarını ve yetkiniz varsa iş yüklerini görüntüleme |
| Tasarım > Form Tasarımı | `/forms` | Dinamik form oluşturma, alanları sıralama, doğrulama ve sürüm yönetimi |
| Tasarım > Akış Tasarımı | `/workflows` | Süreç adımlarını görsel canvas üzerinde tasarlama ve yayınlama |
| Süreçler > Yeni Talep | `/runner` | Yayınlanmış form ve akışı kullanarak yeni bir süreç örneği başlatma |
| Süreçler > Süreçler | `/processes` | Başlatılmış süreçleri filtreleme ve adım geçmişini inceleme |
| Süreçler > İşlerim | `/tasks` | Kullanıcının yapabileceği aktif işleri ve tamamladığı iş geçmişini yönetme |
| Yönetim > Kullanıcılar | `/management/users` | Kullanıcı, onay, rol, oturum ve erişim yönetimi |
| Yönetim > Topluluklar | `/management/communities` | Topluluk, davet kodu ve topluluğa özel rol/izin yönetimi |
| Yönetim > Takımlar | `/management/teams` | Topluluk içindeki operasyon takımlarını ve üyelikleri yönetme |
| Loglar | `/logs` | Kimlik, erişim, form, süreç ve görev audit kayıtlarını arama |
| Gelen Kutusu | `/inbox` | Bütün bildirimleri filtreleme, sayfalama ve okunma durumunu yönetme |
| Ayarlar | `/settings` | Profil, e-posta doğrulama, parola ve aktif oturum yönetimi |

`Tasarım`, `Süreçler` ve `Yönetim` başlıkları sayfa açmaz; alt menüleri
gösteren açılır gruplardır. Aktif alt sayfanın grubu açık tutulur.

## Dashboard

Dashboard oturum açan kullanıcıya göre hazırlanır. Yetkiye bağlı olarak
**Kişisel**, **Topluluk** veya **Tümü** kapsamı seçilebilir.

- Donut grafik açık işler, devam eden süreçler ve tamamlanan süreçleri gösterir.
  Legend satırları ilgili filtreyle İşlerim veya Süreçler sayfasını açar.
- **Öncelikli İşlerim**, kullanıcının işlem yapabileceği güncel kayıtları
  listeler. Kayıt seçildiğinde ilgili iş açılır.
- **Son Hareketler**, son bildirimlerin kısa görünümüdür.
- **Yeni Talep**, **Akış Tasarımı** ve **Form Tasarımı** aksiyonları yalnız
  gerekli izne sahip kullanıcıda görünür.

## Takım ve Takım Yönetimi

Sol menüdeki **Takım**, kullanıcının kendi üyeliklerini gösterir. Bir takım
seçildiğinde takım arkadaşları, sorumlu kişiler ve aktif iş sayıları görülebilir.
Detaylı iş yükü erişimi takım sorumlusu veya yönetim yetkisiyle sınırlı olabilir.

**Yönetim > Takımlar** ise idari ekrandır. Burada yetkili kullanıcı:

- takım arar ve topluluk/durum filtresi uygular,
- yeni takım oluşturur,
- takım adı, açıklaması ve aktiflik durumunu günceller,
- kullanıcı ekler veya üyelik kaldırır,
- kullanıcıyı **Sorumlu** yapabilir.

Sorumluluk işareti kullanıcıya yeni bir sistem izni vermez; yalnız takım
sorumlusuna ayrılmış işlerin adaylık koşulunu etkiler.

## Form Tasarımı

Form Tasarımı, süreçte doldurulacak veri modelini hazırlar.

1. Yeni form başlatın veya kayıtlı bir formu seçin.
2. Form adı, açıklama ve topluluk bilgisini belirleyin.
3. Alan paletinden metin, sayı, e-posta, seçim, checkbox, tarih veya dosya
   metadata alanı ekleyin.
4. Alan etiketi, teknik anahtar, zorunluluk, seçenekler ve bağımlı doğrulama
   kurallarını düzenleyin.
5. Alanları sürükle-bırak veya yukarı/aşağı düğmeleriyle sıralayın.
6. Hataları giderip formu kaydedin ve uygun sürümü yayınlayın.

Masaüstünde alan paleti sağda sabit kalır. Mobilde yuvarlak palet düğmesi alt
paneli açar; seçilen alan listenin sonuna eklenir. Alan kartları mobil sürükleme
tutamacıyla da sıralanabilir.

- **Yeni form:** Mevcut seçimi bırakıp boş taslak açar.
- **Formu kaydet/güncelle:** Taslağı veritabanına yazar.
- **Taslağı dışa/İçe aktar:** Düzenlenebilir JSON taslağını taşır; mevcut
  yayınlanmış kaydı sessizce ezmez.
- **Yayınla:** Kullanılabilir, değiştirilemez bir form sürümü üretir.
- **Arşivle:** Formu yeni kullanım listesinden kaldırır; geçmiş süreç kayıtlarını
  silmez.

## Akış Tasarımı

Akış Tasarımı, yayınlanmış formların hangi görev ve karar adımlarından geçeceğini
belirler.

- Sol paletten başlangıç, kullanıcı görevi, karar kapısı, tamamlandı/reddedildi
  sonu ve takım swimlane öğeleri canvas'a eklenir.
- Canvas pan, zoom, bağlantı kurma ve sürükle-bırak işlemlerini destekler.
- Sağ özellik panelinde seçili adımın adı, form bağlantısı, takım/rol/kullanıcı
  ataması, aksiyonları ve koşulları düzenlenir.
- **Kaydet**, üzerinde çalışılabilir taslağı saklar.
- **Yayınla**, doğrulama hatası yoksa çalıştırılabilir ve değiştirilemez bir
  sürüm oluşturur.
- **Yeni sürüm**, yayınlanmış akıştan yeni bir düzenlenebilir taslak üretir.
- **Taslağı dışa/İçe aktar**, graph yapısını JSON dosyasıyla taşır. Ortama özel
  form, takım ve rol bağları içe aktarma sonrasında yeniden seçilmelidir.

Doğrulama panelindeki bir hataya tıklamak ilgili node veya alanı açar. Eksik
başlangıç/son, bağlantısız node, belirsiz aksiyon, yayınlanmamış form ya da
eksik atama giderilmeden akış yayınlanamaz.

## Yeni Talep

Bu ekranda başlatılan şey yalnız bir form değil, yeni bir **process instance**
yani süreç örneğidir. Aynı yayınlanmış akış farklı talepler için tekrar tekrar
başlatılabilir.

1. Kayıtlı formu ve varsa yayınlanmış süreç akışını seçin.
2. Çok adımlı form sayfalarını doldurun.
3. Zorunlu ve bağımlı alan hatalarını giderin.
4. **Süreç başlat** ile form verisini backend doğrulamasından geçirin.

**Temizle** girilen değerleri sıfırlar. Sağdaki JSON görünümü API'ye gidecek
payload'u gösterir; kopyalanabilir ve genişletilebilir. Başarılı işlem yeni bir
süreç kimliği üretir.

## Süreçler

Süreçler ekranı, başlatılmış süreç örneklerini gösterir.

- Durum, kapsam, tarih veya son tarihe göre filtreleme ve sıralama yapılabilir.
- **Detay**, aktif adımı, sorumlu takım/rolü, claim sahibini, form verisini ve
  zaman bilgisini gösterir.
- **Adım geçmişi**, işlemi yapan kullanıcıyı, aksiyonu, notu ve zamanı okunabilir
  timeline halinde sunar.
- Teknik JSON yardımcı inceleme içindir; ana işlem geçmişi timeline'dır.

Kişisel kapsam, kullanıcının başlattığı veya görev aldığı süreçleri; topluluk ve
global kapsam ise yalnız yetki verilen daha geniş kayıt kümesini gösterir.

## İşlerim

- **Aktif:** Doğrudan atanmış, kullanıcının üstlendiği veya aday havuzundan
  üstlenebileceği işleri gösterir.
- **Geçmiş:** Kullanıcının tamamladığı işleri, verdiği aksiyonu ve işlem notunu
  gösterir.
- **Üzerime al:** Takım veya rol havuzundaki sahipsiz işi kullanıcıya kilitler.
- **Havuza bırak:** Claim edilen işi yeniden uygun adaylara açar.
- **Onayla, Reddet, Geri Gönder, Tamamla, Eskale Et:** Akış tasarımında o görev
  için izin verilen aksiyonlardır. Her görevde bütün aksiyonlar görünmez.

Kartlarda öncelik, son tarih, takım/rol, aktif adım ve claim bilgileri gösterilir.
Başka bir kullanıcının aldığı havuz işi üzerinde aksiyon yapılamaz.

## Yönetim

### Kullanıcılar

Arama, topluluk, rol ve durum filtreleri server-side pagination ile çalışır.
Kullanıcı seçildiğinde:

- rol ve hesap durumu güncellenebilir,
- pending hesap onaylanabilir veya reddedilebilir,
- aktif oturumlar görüntülenip iptal edilebilir,
- SuperAdmin tarafından parola sıfırlanabilir,
- yetkili SuperAdmin tarafından kullanıcı başka topluluk ve role atomik olarak
  taşınabilir,
- kullanıcının ilgili audit geçmişi açılabilir.

Yeni kullanıcı oluşturma ve kritik erişim değişiklikleri onay penceresinden
geçer. Topluluk Admin yalnız kendi topluluğunun kapsamını yönetir.

### Topluluklar

SuperAdmin topluluk oluşturabilir, davet kodunu ve topluluk bilgilerini
güncelleyebilir. Topluluk rolleri hazır şablondan veya boş özel rolden
oluşturulur. İzin kutuları değiştirilirse şablonun topluluğa özel bir kopyası
üretilir. Özel roller güncellenebilir veya güvenli bir hedef role taşıma
seçilerek silinebilir.

## Loglar ve Gelen Kutusu

**Loglar**, uygulamadaki kalıcı audit izidir. Kategori kartları global kapsam
sayılarını gösterir; arama sonucu bu sayıları değiştirmez. Kayıtlar tarih,
aksiyon veya aktöre göre sıralanabilir. **İlgili geçmiş** görünümünde:

- **Aksiyon bağlamı:** Yapan ve etkilenen tarafın kesiştiği işlem zinciri,
- **Yapan:** Aktörün gerçekleştirdiği işlemler,
- **Etkilenen:** Seçili kullanıcı veya kaydı etkileyen işlemler

incelenir.

**Gelen Kutusu**, kullanıcı bildirimlerini arama, kategori, okunma durumu ve
sayfa bazında listeler. Bildirime tıklamak kaydı okundu yapıp ilgili görev,
süreç, ayar veya yönetim ekranına yönlendirebilir.

## Ayarlar

- **Profil Bilgileri:** Görünen ad ve e-posta adresini günceller. E-posta
  değişirse doğrulama durumu sıfırlanır.
- **E-posta Doğrulama:** Süreli kod ister ve girilen kodu doğrular.
- **Şifre Değiştir:** Mevcut ve yeni parola ile güvenli değişiklik yapar.
- **Aktif Oturumlar:** Cihaz, IP, son görülme ve bitiş zamanını gösterir.
- **Oturumu kapat / Tüm cihazlardan çıkış yap:** Seçili veya bütün session ve
  refresh token zincirlerini iptal eder.

Geçici parolayla oluşturulan kullanıcı, diğer workspace ekranlarına geçmeden
önce zorunlu parola değişikliği ekranını tamamlar.

## Ortak Buton ve Geri Bildirim Kuralları

- **Yenile:** İlgili ekranın cache'ini yeniden doğrular; başarı veya hata bildirimi
  gösterir.
- **Detay / bilgi ikonu:** Seçili kaydın yan panelini açar; kayıt üzerinde
  değişiklik yapmaz.
- **Chevron:** Kart veya bölümün açılıp kapanabildiğini gösterir.
- **Kaydet/Güncelle:** Taslak veya metadata değişikliğini saklar.
- **Yayınla:** Doğrulanmış sürümü kullanıma açar; kaydetmekle aynı işlem değildir.
- **Kırmızı buton:** Silme, iptal veya erişim kapatma gibi riskli işlemdir.
- **Yeşil buton:** Oluşturma, aktifleştirme veya başarılı tamamlama aksiyonudur.
- **Onay penceresi:** Kritik işlemin etkisini gösterir; onay verilmeden API
  mutation isteği gönderilmez.
- **Skeleton/inline loader:** İlk veri veya sayısal özet yüklenirken geçici `0`
  ya da `-` göstermek yerine kullanılır.
- **Kart içi geri bildirim:** Başarı ve hata mesajı mümkün olduğunca işlemi
  başlatan kartın yanında görünür.

## Kısa Demo Zinciri

1. Yetkili kullanıcı **Form Tasarımı** ekranında formu hazırlayıp yayınlar.
2. **Akış Tasarımı** ekranında görevleri, takımları ve karar yollarını bağlayıp
   akışı yayınlar.
3. Talep sahibi **Yeni Talep** ekranından formu doldurup süreci başlatır.
4. Aday kullanıcı **İşlerim** ekranında işi üzerine alır ve aksiyon verir.
5. **Süreçler** ekranında aktif adım ve tamamlanan adımlar izlenir.
6. **Gelen Kutusu** bildirimleri, **Loglar** ise kalıcı işlem zincirini gösterir.
