# Ahır gerilim sahnesi — tasarım

Tarih: 27 Kasım 2026 · Durum: onaylandı, uygulandı

> **28 Kasım 2026 değişiklik:** Kullanıcının isteğiyle ("silüete gerek yok ya") siluet kaldırıldı. Aşağıdaki
> siluet geçen maddeler artık geçerli değil; "biri var" hissini balyaların düşüşü ve samanlıktan gelen ayak sesi
> (`BarnScare.woodSteps`, 1.0 sn, ses dosyası gelince) veriyor. Kamera hedefi ortadaki balya.

## Amaç ve neden

Oyunda yeterince gerilim yok. Oyuncu çiftlik evinde **bir şeylerin ters gittiğini, anormal şeyler olduğunu**
anlamalı. Ahırda kısa bir olay: oyuncu kapıyı kendisi açar, içeri adım atar, yukarıdan saman balyaları önüne
dökülür ve samanlıkta bir gölge kaybolur. Oyuncuda kalan his: "burada biri var, balyaları biri itti."

- **Hedef:** anlık korku + kalıcı huzursuzluk; mesaj kaçırılmamalı (oyuncu gölgeyi görmeli).
- **Hedef değil:** kovalamaca, hasar, oyuncunun ölmesi, yeni bir görev.
- **Anti-hedef:** balyaların komik/sebepsiz görünmesi; kontrolün uzun süre elden alınması; oyuncunun ahırda
  mahsur kalması ya da `Ahir_Varil`'e giden yolun kapanması.

## Yaklaşımlar

| | Ne | Neden seçilmedi / seçildi |
|---|---|---|
| A | Tam sinematik, kamera 3–4 sn bizde | 4 sn'lik an için oyuncuyu "izleyiciye" çeviriyor, korku bağı kopuyor |
| B | Tamamen oyun içi, fizikle düşen balyalar | Oyuncu başka yere bakarsa gölgeyi kaçırır; fizik her seferinde farklı, bazen saçma |
| **C** | **Karışık: kısa kamera çekişi + senaryolu düşüş** | **Seçildi.** Mesaj garanti görülür, kontrol hissi korunur |

## Mevcut durum (sahneden)

- Ahır `FarmSet/Barn_Model`, x −161…−95, z 485…539. Ön cephe x ≈ −100'de, iki sürgülü kapı:
  `BarnDoors/Ahir_Kapi_Sol` (−103.4, 499.5) ve `Ahir_Kapi_Sag_Aralik` (−101.5, 506.3). Sağdaki aralık, üstünde
  `WindRattle` var (rüzgârda kayıp takırdıyor, `bangSound` boş). Kapılar 5.1 m eninde, 8.1 m boyunda; sadece
  BoxCollider var, açılmıyorlar.
- İçeride orta koridor çatıya kadar açık; iki yanda (z ≈ 500 ve 524 hizası) **y 14.8'de samanlık katı** var
  (zemin y 10, yani 4.8 m yukarıda).
- `Brief_Gameplay/Ahir_Varil` (−105.7, 501.6) kapının hemen içinde, oyuncu hikâye gereği ahıra giriyor.
- Mevcut balyalar (`Ahir_Inekler/Saman_Balyalari`, 7 adet HaiSquare/HaiCylinder) ahırın dip tarafında (x ≈ −150).
- Kod: `Door` (menteşeli, E ile), `IInteractable`, `PlayerInteractor`, `PlayerControlLock` (Push/Pop; MouseLook,
  PlayerMovement, PlayerInteractor, HeadBob'u kapatır, **imleci açar**), `GameAudio.PlayAt`, `IndoorZone`.

## Tasarım

### 1. Kapı
- `Ahir_Kapi_Sag_Aralik` E ile açılan **sürgülü kapı** olur (yeni `SlidingDoor : IInteractable`; `Door` menteşeli
  olduğu için uymuyor). E → kapı `WindRattle.localAxis` yönünde ~2.5 m kayarak açılır (yumuşak, ~1.2 sn, ağır
  ahşap-ray sesi). Açıldıktan sonra `WindRattle` kapanır. Yazı: "Kapıyı Aç". Kapanma yok (tek yönlü).
- Kapı açılınca içerisi sessiz hissettirmeli; mevcut `IndoorZone_Ahir` ortam sesini zaten kısıyorsa ona dokunulmaz.

### 2. Tetik
- Kapının ~3 m içinde, orta koridorda bir trigger. **Kapı açılmadan tetiklenmez, bir kere çalışır.**

### 3. Olay akışı (~3 sn, t = tetik anı)
- **0.0** Yukarıdan tahta gıcırtısı (samanlık katı, oyuncunun önünde).
- **0.3** Yürüme durur; kamera ~0.6 sn'de yumuşakça samanlık kenarına döner (oyuncunun bakışından en kısa yoldan).
  Fare kontrolü bu sürede bizde, **imleç görünmez** (PlayerControlLock imleci açtığı için bu olayda imleç kilitli
  kalmalı).
- **0.8** Samanlık kenarından 3 balya devrilir, oyuncunun 1.5–2.5 m önüne düşer: senaryolu hareket (devrilme +
  düşüş), yere değince kısa bir fizik oturması. Toz bulutu, ağır "küt" sesleri, hafif kamera sarsıntısı.
- **1.0** Balyaların arkasında, samanlığın içinde karanlık bir insan silueti ~1 sn içinde yana çekilip görünmez olur;
  tahta üstünde birkaç hızlı ayak sesi uzaklaşır.
- **1.6** Kalp atışı başlar (oyun sonundaki kalp sesi varsa o), birkaç saniyede söner.
- **2.4** Kontrol geri gelir; kamera olduğu yerde kalır (geri çevrilmez).
- Balyalar yerde kalır (collider'lı), koridoru **tamamen kapatmaz**, `Ahir_Varil`'e yol açık.
- Siluet bir daha görünmez; samanlığa çıkış yok (bu tasarımın konusu değil).

### 4. Kurulum
- Diğer sinematikler gibi bir editör builder'ı (`Tools/Level/Build Barn Scare`): yeni balyalar (mevcut HaiSquare
  mesh'inden kopya) samanlık kenarına, trigger, siluet, ses kaynakları, kapıya `SlidingDoor`.
- Mevcut objeler silinmez/taşınmaz; sadece kapıya bileşen eklenir. `docs/level-notlari.md`'ye bölüm, PDF yenilenir.
- Ses: kapı, gıcırtı, dökülme, ayak sesi için ses arkadaşına istek `docs/ses-notlari.md`'ye; o gelene kadar
  Pixabay'dan geçici sesler (indirmeden önce izin).

## Builder'a kalanlar
- Kesin balya yerleşimi ve devrilme eğrisi, kamera çekiş eğrisi, siluetin mesh'i (projede insan modeli varsa o,
  yoksa basit koyu siluet; karanlıkta zaten seçilmiyor), toz efekti.
- Saf zamanlama/eğri matematiği EditMode testleriyle (mevcut `*MathTests` düzeni).
