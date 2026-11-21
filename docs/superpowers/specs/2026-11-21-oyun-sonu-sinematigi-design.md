# Oyun sonu sinematiği: tasarım

Tarih: 21 Kasım 2026. Durum: onay bekliyor.

## Amaç ve neden

Oyun, bodrumdaki taş odada bulunan cesetle bitiyor. Oyuncu kanlı izi taş odanın kapısına kadar takip ediyor. Kapıyı açınca
oyun ona kısa bir uyarı veriyor: oda görünüyor, müzik giriyor, kalp çarpıyor ve koşamıyor. Cesetle etkileşime girince asıl
sinematik başlıyor: cesedi baştan ayağa inceliyor, örtüye uzanıyor, her şey birden susuyor, arkasından bir dolap sesi
geliyor. Oyuncu dönüyor, dolap üstüne devriliyor ve oyun bitiyor. Ardından credits geliyor (ayrı iş).

Hedeflenen his: yavaş, giderek ağırlaşan bir gerilim; tek, ani bir patlama; ve hemen ardından bitiş. Korkunun gücü,
sessizliğin beklenmedik anda gelmesinde ve dolabın görüldüğü anda her şeyin kesilmesinde.

## Hedefler

- Taş oda kapısı ilk kez açılınca kısa bir bakış (~3 sn), ceset odası müziği, kalp çarpıntısı ve koşmanın kapanması.
- Cesetle E ile etkileşimde ~19 sn'lik asıl sinematik; oyuncu sinematik bitene kadar kontrol edemez.
- Sinematik bitince ekran siyah kalır ve oyun biter; credits ekranı için bir bağlantı noktası bırakılır.
- Giriş sinematiğiyle aynı his ve araçlar: sinematik kamera, eller ve fener, Timeline, `Tools` menüsünden Build.

## Kapsam dışı

- Credits ekranı ve credits müziğinin çalınması: sonraki iş. Bu iş yalnızca sinematik bitince çağrılacak yeri hazırlar.
- Örtünün kalkması ya da cesedin yüzünün görünmesi (karar: el uzanır ve örtüye değmeden durur).
- Level düzeninde değişiklik (oda, ceset ve dolabın yeri arkadaşın işi). Builder bunları adıyla bulur, yerlerini değiştirmez.

## İstenmeyenler (çalışsa bile başarısız sayılır)

- E'ye basıldığı anda kameranın oyuncunun bakışından sinematiğin başlangıcına zıplaması.
- Dolap sesinden önce herhangi bir şeyin korkuyu ele vermesi (müzik değişimi, ışık titremesi, ipucu yazısı).
- Kalp çarpıntısının oyuncu odada dolaşırken dakikalarca aynı şiddette çalıp sıkması.
- Dolabın oyuncunun üstüne düştüğünün açıkça gösterilmesi: düşmeye başladığı an kesilir.
- Sinematik sırasında oyuncunun hareket edip sahneyi bozabilmesi ya da atlayabilmesi (atlama yok).

## Sahnede hazır olanlar

| Ne | Obje | Konum (dünya) |
|---|---|---|
| Taş oda kapısı | `Kapilar/Kapi_TasOda` (`Door`, kilitsiz) | batı duvarı, x -104.5, z 184–186 |
| Ceset (örtülü) | `Bodrum_OrtuluSekil` (`Bodrum_Ortu` örtü, iki çizme) | x -90.6…-86.6, z 191.4–193.7; çizmeler batıda, baş doğu duvarında |
| Devrilecek dolap | `Bodrum_Yeni/Bodrum_DevrilenDolap` | x -87.5…-86.2, z 188.3–190.2, y 1.6–6.3 (cesedin başının güneyinde) |
| Kan damlaları | `Bodrum_Blood_Drips` | doğu duvarı, başın üstü |
| Kanlı iz | `Bodrum_Blood_Smear`, `Bodrum_Blood_Pool` | bodrum holünden kapıya |
| Müzikler | `Assets/Sfx/ceset_room_extended.wav`, `credits_extended.wav` | |
| Oda zemini | `BodrumOda_TopraKZemin` | y ≈ 1.6 |

Pixabay'den indirilecek sesler: kalp çarpıntısı (döngü), dolabın arkadan gelen gıcırtısı, ağır ahşap dolabın devrilip
çarpması.

## Yaklaşımlar

1. **Timeline ve builder (seçilen).** `Tools > End Cinematic > Build` sahneye Timeline'ı ve objeleri kurar; giriş
   sinematiğinin yapısını izler. Artısı: editörde ileri geri sarıp kare kare bakılır, zamanlama sabitlerden ayarlanır.
   Eksisi: kurulumu daha uzun sürer.
2. **Sadece script.** Bir bileşen kamerayı ve sesleri kodla sırayla oynatır. Daha hızlı kurulur ama önizleme yok; her
   ayar için Play'e basıp bodruma kadar gitmek gerekir.
3. **Elle Timeline.** Animasyonun tamamı elle yapılır; sonradan düzenli biçimde ayarlanamaz.

Kullanıcı 1'i seçti.

## Akış

### 1. Kapı bakışı (oyun içi, ~3 sn)

Tetik: `Kapi_TasOda` ilk kez açıldığında (yalnızca bir kez).

- Oyuncunun kontrolü kilitlenir (`PlayerControlLock`). Kamera oyuncu kamerası olarak kalır; bakış kodla döndürülür.
- Bakış: kapıdan odaya doğru yavaşça kayar, örtülü şeklin üstünden geçer, oyuncunun eski bakış yönüne döner. Ceset
  bakışın ortasında, ~0.6 sn kadrajda kalır. Bakış sonunda kontrol geri gelir.
- `ceset_room_extended` Music kanalında 2 sn içinde açılır ve oyun sonuna kadar çalar.
- Kalp çarpıntısı başlar (SFX kanalı, 2D, döngü). Şiddeti cesede uzaklığa bağlıdır: 14 m ve ötesinde hafif ve yavaş,
  2 m'de güçlü ve hızlı (ses ve perde birlikte artar).
- Koşma kapanır ve oyun sonuna kadar kapalı kalır.

### 2. Asıl sinematik (~19 sn)

Tetik: örtüye bakarken E ("İncele"). Oyuncunun kontrolü kalıcı olarak kapanır, oyuncu kamerası yerine sinematik kamera açılır
(giriş sinematiğindeki gibi oyuncu kamerasından kopyalanır).

Süreler önerilen başlangıç değerleridir; Build'den sonra kare kare bakılarak ayarlanır.

| Sinematik zamanı (sn) | Olan |
|---|---|
| 0.0 – 1.2 | Kamera oyuncunun o anki bakışından başlangıç pozuna yumuşakça geçer (çizmelerin ~1.5 m batısında ayakta, ayaklara bakar). |
| 1.2 – 6.5 | Bakış çizmelerden örtünün üstünden başa doğru yavaşça kayar; bir iki küçük adım. Fener elde, ışık bakışla birlikte gezer. |
| 6.5 – 10.0 | Cesedin güney kenarı boyunca başa doğru yürür (toprak zeminde adım sesleri). Baş tarafında durur; duvardaki kan damlalarına kısa bir bakış. |
| 10.0 – 13.0 | Hafifçe çömelir. Sol el örtünün baş ucuna doğru uzanır. Çarpıntı en hızlı hâlinde. |
| 13.0 | El örtüye birkaç santim kala durur. Aynı karede bütün sesler kesilir (müzik, çarpıntı, ortam, adımlar). |
| 13.0 – 15.5 | Tam sessizlik. Kamera neredeyse kıpırdamaz, yalnızca nefes kadar bir salınım. |
| 15.5 | Arkadan dolap sesi: ahşabın gıcırdaması. |
| 16.2 – 16.6 | Ani dönüş (~160°, 0.4 sn). Fener ışığı dolaba düşer. |
| 16.6 – 17.4 | Dolap oyuncuya doğru devrilmeye başlar; tepesi kadraja büyür. |
| 17.4 | Çarpma sesinin ilk vuruşuyla ekran kesin siyah olur; ses de birkaç kare içinde kesilir. |
| 17.4 – 19.0 | Siyah ve sessiz. Ardından "bitti" olayı tetiklenir (credits buraya bağlanacak). |

Geometri: oyuncu baş tarafında, cesedin güney kenarında durduğunda (≈ x -88.8, z 190.6) dolap sağ arkasında kalır. Dolap
kendi alt kenarı üstünden oyuncuya doğru (batı ya da kuzeybatı) devrilir.

### 3. Bitiş

Sinematik bittiğinde ekran siyah kalır, imleç gizli kalır, oyun girdisi kapalıdır. `EndCinematic` bir `Ended` olayı yayar; credits
işi buna bağlanacak. Şimdilik olayı dinleyen yoktur ve oyun siyah ekranda bekler.

## Nasıl kurulacak

### Yeni dosyalar

- `Assets/Scripts/Cinematic/EndCinematic.cs`: çalışma zamanı bileşeni.
  - Kapı bakışı: kapının `IsOpen` değeri ilk kez true olunca başlar (`Door.cs` değişmez).
  - Müzik, çarpıntı ve koşu kapatma.
  - Örtüdeki `IInteractable` ("İncele"); E'ye basılınca Timeline'ı oynatır.
  - Başlangıç geçişi: ilk 1.2 sn'de kamera pozu, oyuncu kamerasının pozundan Timeline'daki pozla harmanlanır.
  - Sessizlik: mixer'da Music, SFX, Ambience ve UI kanallarının açık parametreleri -80 dB'e çekilir. Cinematic kanalı açık
    kalır; dolap sesleri o kanaldan çalar.
  - Bitişte siyah ekran ve `Ended` olayı.
- `Assets/Scripts/Editor/EndCinematicBuilder.cs`: `Tools > End Cinematic > Build`.
  - Objeleri adıyla bulur. Örtüye collider yoksa ekler.
  - Sinematik kafayı, kamerayı, elleri ve feneri (`IC_Arms`, `IC_Props`) kurar.
  - Timeline'ı kurar: kafa ve el animasyonu, dolap devrilme animasyonu, ses parçaları (adımlar, gıcırtı, çarpma).
  - Kalp çarpıntısını, gıcırtıyı ve çarpmayı içe aktarır.
  - Tekrar çalıştırmak güvenlidir: eski kurulum silinip yeniden kurulur.
- `Assets/Sfx/Ending/`: Pixabay'den kalp çarpıntısı, dolap gıcırtısı, dolap çarpması.

### Değişecek dosya

- `Assets/Scripts/Player/PlayerMovement.cs`: `public bool sprintAllowed = true;` eklenir ve koşma koşuluna bağlanır.
  Başka bir davranış değişmez.

### Giriş sinematiğinden yeniden kullanılacaklar

Poz yardımcıları (`P`, anahtar örnekleme, kol IK'sı) şu an `IntroCinematicBuilder` içinde private. Giriş builder'ını
bozmamak için bu iş onlara dokunmaz. Yeni builder ihtiyaç duyduğu küçük kısmı kendi içinde kurar. Ortak bir yardımcıya
taşımak sonraki bir temizlik işi olabilir.

## Level'a etkisi (level notuna eklenecek)

- `Kapi_TasOda`, `Bodrum_OrtuluSekil`, `Bodrum_Ortu` ve `Bodrum_DevrilenDolap` adlarıyla bulunuyor. Adları ve yerleri
  değişirse sinematik bozulur.
- Builder örtüye bir collider ekleyebilir ve dolabı sinematik sırasında döndürür.
- Dolabın arkası ve devrilme yolu (batıya ~4.7 m) boş kalmalı.

## Benim verdiğim kararlar (kontrol et)

- Fener sinematikte her zaman açık. Taş odada ışık yok; oyuncunun pili bitmiş ya da feneri kapalı olsa bile karakter
  feneri yakmış kabul edilir. Yoksa dolap karanlıkta görünmez.
- Sinematik başlarken oyuncunun nerede durduğu önemli değil. İlk 1.2 sn'lik geçiş onu başlangıç noktasına taşır.
- Kalp çarpıntısı SFX kanalında, dolap sesleri Cinematic kanalında. Böylece sessizlik anında çarpıntı da kesilir, dolap
  sesi ise duyulur.

## Builder'a kalanlar

- Kamera yolunun ve bakışların tam noktaları; süreleri kare kare bakarak ince ayar.
- Ses seviyeleri; giriş sinematiğindeki gibi ölçülerek dengelenir.
- Dolabın devrilme eğrisi (yavaş başlayıp hızlanan).
- Kapı bakışında, oyuncu kapıyı açtığı anda hangi yöne baktığına göre bakış yolunun hesaplanması.

## Açık konular

- Credits ekranı ve credits müziği: ayrı iş, sonra.
- Yazı tipi: henüz seçilmedi (credits'te gerekecek).
