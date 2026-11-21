# Oyun Sonu Sinematiği Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Taş oda kapısı açılınca kısa bakış, müzik, kalp çarpıntısı ve koşu kilidi; cesede E ile 19 sn'lik sinematik; siyah ekranla oyun sonu.

**Architecture:** Saf hesaplar test edilebilir bir statik sınıfta (`EndCinematicMath`). Oyun içi davranış iki bileşende:
`EndCinematic` (kapı bakışı, sesler, sinematik akışı) ve `CorpseInspect` (E etkileşimi). Timeline'ı ve sinematik objeleri
`EndCinematicBuilder` kurar; yapı `IntroCinematicBuilder` ile aynıdır ama onun koduna dokunmaz.

**Tech Stack:** Unity 6.5, URP, Timeline (`UnityEngine.Timeline`), AudioMixer, NUnit EditMode testleri, unityMCP
(`execute_code`, `refresh_unity`, `read_console`, `run_tests`).

**Spec:** `docs/superpowers/specs/2026-11-21-oyun-sonu-sinematigi-design.md`

## Global Constraints

- Commit yok: kullanıcı commit'leri kendisi yapar. Görevlerde commit adımı bulunmaz.
- Level objelerinin yeri ve adı değişmez; builder onları adıyla bulur: `Kapi_TasOda`, `Bodrum_OrtuluSekil`, `Bodrum_Ortu`,
  `Bodrum_DevrilenDolap` (`Bodrum_Yeni` altındaki).
- `IntroCinematicBuilder.cs`, `IntroCinematic.cs`, `Door.cs` ve `MouseLook.cs` değişmez.
- Sinematik atlanamaz; sinematik sırasında oyuncu kontrolü yoktur.
- Sesler: müzik Music kanalında, kalp çarpıntısı SFX kanalında (2D), dolap sesleri ve sinematik adımları Cinematic kanalında.
- Sessizlik anında Music, SFX, Ambience ve UI mixer parametreleri -80 dB'e çekilir; Cinematic ve Master'a dokunulmaz.
  `GameAudioSettings.SetVolume` kullanılmaz (kullanıcının ses ayarını kaydeder).
- Fener sinematikte her zaman açık.
- C# dosyaları UTF-8, BOM'suz. `using System` olan dosyalarda `UnityEngine.Object` açık yazılır.
- Her kod değişikliğinden sonra: `refresh_unity` (force, all, compile request) ardından `read_console` types ["error"] boş olmalı.

## Review Focus

1. Oyuncu E'ye eğilmişken, bir sandığın üstündeyken ya da cesede yan bakarken basar: başlangıç geçişi her pozdan zıplamadan
   tam başlangıç pozuna varmalı (Görev 2 testi `StartBlend_ReachesOneAt1_2s`, Görev 6 probu).
2. Oyuncu kapıyı açar, geri çıkar, kapıyı kapatıp yeniden açar: kapı bakışı yalnızca bir kez çalışmalı, müzik baştan
   başlamamalı (Görev 4 probu).
3. Kapı açıldığı anda oyuncu koşuyor: koşma o karede bitmeli, kapı bakışı bitince de geri gelmemeli (Görev 4 probu).
4. Fenerin pili bitmiş ya da fener kapalı: sinematikte fener yine yanık olmalı (Görev 6 probu).
5. Oyuncu ayarlardan müziği kısmış: ceset müziği kullanıcının Music seviyesine uymalı (Music grubuna bağlı kaynak;
   Görev 4 probu `outputAudioMixerGroup` kontrolü).

---

### Görev 1: Ses dosyaları

**Files:**
- Create: `Assets/Sfx/Ending/Heartbeat_Loop.mp3`, `Assets/Sfx/Ending/Cabinet_Creak.mp3`, `Assets/Sfx/Ending/Cabinet_Fall.mp3`

**Interfaces:**
- Produces: yukarıdaki üç klip; Görev 4 ve 5 bu yollarla yükler.

- [ ] **Adım 1: Pixabay'den adayları bul ve indir.** Built-in browser'da `javascript_exec` ile arama sayfası (`heartbeat`,
  `wooden cabinet creak`, `furniture fall crash`), her öğe sayfasından `cdn.pixabay.com/download/audio/...mp3` adresi,
  `curl` ile scratchpad'e indirme.
- [ ] **Adım 2: Seç ve kes.** earshot ile dinle. Kalp çarpıntısı: tek başına, müziksiz, temiz dönen 4–10 sn'lik bir bölüm
  (ffmpeg ile kesilir, uçlarına 20 ms fade). Gıcırtı: 0.6–1.2 sn, yavaş başlayan ahşap gıcırtısı. Çarpma: ilk vuruşu ilk
  50 ms içinde olan ağır ahşap çarpması.
- [ ] **Adım 3: `Assets/Sfx/Ending/` altına kopyala ve import et.** `refresh_unity`.
- [ ] **Adım 4: Doğrula.** `execute_code` ile üç klibi `AssetDatabase.LoadAssetAtPath<AudioClip>` ile yükle.
  Beklenen: üçü de null değil; uzunluklar sırasıyla 4–10, 0.6–1.2 ve 0.5–3 sn.

### Görev 2: `EndCinematicMath` ve testleri

**Files:**
- Create: `Assets/Scripts/Cinematic/EndCinematicMath.cs`
- Test: `Assets/Scripts/Editor/Tests/EndCinematicMathTests.cs`

**Interfaces:**
- Produces (hepsi `public static`, sınıf `public static class EndCinematicMath`):
  - `float HeartbeatVolume(float distance)`: 14 m ve ötesi 0.25, 2 m ve berisi 0.9, arası smoothstep.
  - `float HeartbeatPitch(float distance)`: 14 m ve ötesi 0.85, 2 m ve berisi 1.3, arası smoothstep.
  - `float DoorLookWeight(float t)`: 0 sn'de 0, 1.1 sn'de 1 (smoothstep), 1.7 sn'ye kadar 1, 3.0 sn'de 0 (smoothstep).
  - `const float DoorLookDuration = 3f`
  - `float StartBlend(float t)`: smoothstep(t / 1.2).
  - `float CabinetFallAngle(float t)`: t düşme başından beri sn; `35f * u*u`, `u = clamp01(t / 0.8f)`.
  - `AudioChannel[] HushChannels`: `{ Music, SFX, Ambience, UI }`.

- [ ] **Adım 1: Testleri yaz.**

```csharp
public class EndCinematicMathTests
{
    [Test] public void Heartbeat_Far_IsQuietAndSlow()
    { Assert.AreEqual(0.25f, EndCinematicMath.HeartbeatVolume(20f), 1e-4f); Assert.AreEqual(0.85f, EndCinematicMath.HeartbeatPitch(14f), 1e-4f); }
    [Test] public void Heartbeat_Near_IsLoudAndFast()
    { Assert.AreEqual(0.9f, EndCinematicMath.HeartbeatVolume(1f), 1e-4f); Assert.AreEqual(1.3f, EndCinematicMath.HeartbeatPitch(2f), 1e-4f); }
    [Test] public void Heartbeat_RisesAsDistanceFalls()
    { Assert.Less(EndCinematicMath.HeartbeatVolume(10f), EndCinematicMath.HeartbeatVolume(5f)); }
    [Test] public void DoorLook_Profile()
    { Assert.AreEqual(0f, EndCinematicMath.DoorLookWeight(0f), 1e-4f); Assert.AreEqual(1f, EndCinematicMath.DoorLookWeight(1.1f), 1e-4f);
      Assert.AreEqual(1f, EndCinematicMath.DoorLookWeight(1.7f), 1e-4f); Assert.AreEqual(0f, EndCinematicMath.DoorLookWeight(3f), 1e-4f); }
    [Test] public void StartBlend_ReachesOneAt1_2s()
    { Assert.AreEqual(0f, EndCinematicMath.StartBlend(0f), 1e-4f); Assert.AreEqual(1f, EndCinematicMath.StartBlend(1.2f), 1e-4f); Assert.AreEqual(1f, EndCinematicMath.StartBlend(5f), 1e-4f); }
    [Test] public void CabinetFall_StartsStillEndsAt35()
    { Assert.AreEqual(0f, EndCinematicMath.CabinetFallAngle(0f), 1e-4f); Assert.AreEqual(35f, EndCinematicMath.CabinetFallAngle(0.8f), 1e-4f);
      Assert.Less(EndCinematicMath.CabinetFallAngle(0.2f), EndCinematicMath.CabinetFallAngle(0.4f) / 2f); }
    [Test] public void Hush_LeavesCinematicAndMaster()
    { CollectionAssert.DoesNotContain(EndCinematicMath.HushChannels, AudioChannel.Cinematic);
      CollectionAssert.DoesNotContain(EndCinematicMath.HushChannels, AudioChannel.Master);
      CollectionAssert.Contains(EndCinematicMath.HushChannels, AudioChannel.Music); }
}
```

- [ ] **Adım 2: Testi çalıştır, başarısız olduğunu gör.** `run_tests` (EditMode, filtre `EndCinematicMathTests`).
  Beklenen: `EndCinematicMath` bulunamadı derleme hatası.
- [ ] **Adım 3: `EndCinematicMath` yaz.**
- [ ] **Adım 4: Testleri çalıştır.** Beklenen: 7/7 geçer, `GameAudioTests` de geçmeye devam eder.

### Görev 3: Koşu kilidi

**Files:**
- Modify: `Assets/Scripts/Player/PlayerMovement.cs` (alan bloğu ve satır 89)

**Interfaces:**
- Produces: `public bool sprintAllowed = true;` (Görev 4 false yapar).

- [ ] **Adım 1: Alanı ekle ve satır 89'daki koşma koşuluna `sprintAllowed &&` olarak bağla.** Başka satır değişmez.
- [ ] **Adım 2: Derle.** Konsolda hata yok; sahnedeki `PlayerMovement` üzerinde alan true. Davranış Görev 4 probunda.

### Görev 4: `EndCinematic` oyun içi kısım (kapı bakışı, müzik, çarpıntı)

**Files:**
- Create: `Assets/Scripts/Cinematic/EndCinematic.cs`

**Interfaces:**
- Consumes: `EndCinematicMath.*` (Görev 2), `PlayerMovement.sprintAllowed` (Görev 3), `Door.IsOpen`,
  `PlayerControlLock.Push/Pop`, `GameAudio.Group(AudioChannel)`.
- Produces (Görev 5 ve 6 kullanır):
  - Alanlar: `Door door; Transform corpse; Transform player; Camera playerCamera;`
    `AudioClip music, heartbeat; float musicVolume = 0.8f, musicFadeIn = 2f;`
  - `public bool DoorLookDone { get; private set; }`
  - `public bool Started { get; private set; }` (asıl sinematik başladı mı)
  - Kendi oluşturduğu iki `AudioSource`: `musicSource` (Music grubu, loop) ve `heartbeatSource` (SFX grubu, loop, 2D).

- [ ] **Adım 1: Kapı izleme.** `Update`: `door.IsOpen` ilk kez true olunca kapı bakışını başlat (bir kez, `doorTriggered`
  bayrağı).
- [ ] **Adım 2: Kapı bakışı.** `PlayerControlLock.Push()`; oyuncu kamerasının yerel dönüşünü sakla; hedef = kameranın cesedin
  merkezine bakan yerel dönüşü. `DoorLookDuration` boyunca kameranın yerel dönüşü = `Slerp(saklanan, hedef, DoorLookWeight(t))`.
  Bitişte yerel dönüşü saklanan değere tam olarak geri koy (MouseLook'un özel `xRot`'u geçerli kalsın),
  `PlayerControlLock.Pop()`, `DoorLookDone = true`.
- [ ] **Adım 3: Ses ve koşu.** Tetik karesinde: tüm `PlayerMovement.sprintAllowed = false`; `musicSource` 0'dan
  `musicVolume`'a `musicFadeIn` sn'de; `heartbeatSource.Play()`. Her karede (sinematik başlamadıysa) çarpıntı ses ve
  perdesi `HeartbeatVolume/Pitch(oyuncu–ceset yatay mesafesi)`.
- [ ] **Adım 4: Derle.** Konsolda hata yok.
- [ ] **Adım 5: Play probu (Review Focus 2, 3, 5).** `execute_code`: oyuncuyu kapının önüne koy, `door.Interact` çağır,
  0.5/1.5/3.2 sn'de örnek al. Beklenen: 1.5 sn'de kamera yönü cesede 10°'den yakın; 3.2 sn'de `DoorLookDone` true ve
  `PlayerControlLock.IsLocked` false; `sprintAllowed` false; `musicSource.outputAudioMixerGroup` Music grubu. Kapıyı kapat ve
  aç: kapı bakışı tekrar başlamaz, `musicSource.time` sıfırlanmaz. Oyuncuyu cesede 2 m'ye taşı: çarpıntı perdesi ~1.3.

### Görev 5: `EndCinematicBuilder` (Timeline ve sinematik objeler)

**Files:**
- Create: `Assets/Scripts/Editor/EndCinematicBuilder.cs`
- Create (builder üretir): `Assets/Cinematics/EndCinematic.playable`, `Assets/Cinematics/EndCinematic_Anim.anim`

**Interfaces:**
- Consumes: Görev 1 klipleri; `Assets/Models/IntroCinematic/IC_Arms.fbx`, `IC_Props.fbx` (fener); adımlar için
  `Assets/Sfx/Intro` içindeki mevcut toprak/çakıl adım sesi.
- Produces: menü `Tools/End Cinematic/Build` → `EndCinematicBuilder.Build()`; sahnede `EndCinematic` kökü:
  `Head` (sinematik kamera), kollar, fener, `PlayableDirector`. `EndCinematic` bileşenine şu alanları atar (Görev 6 bu alanları
  bileşene ekler; bu görevde builder yalnızca Görev 4 alanlarını bağlar):
  `PlayableDirector director; Transform head; Camera cinematicCamera; Transform cabinet; float hushAt, blackAt, endAt;`
- Zaman sabitleri (sinematik saniyesi, spec tablosundan): `T_BlendEnd 1.2, T_InspectEnd 6.5, T_WalkEnd 10.0, T_Reach 13.0
  (= hushAt), T_Creak 15.5, T_TurnStart 16.2, T_TurnEnd 16.6, T_FallStart 16.6, T_Black 17.4 (= blackAt), T_End 19.0 (= endAt)`.
- Noktalar (dünya): başlangıç pozu çizmelerin ~1.5 m batısı (≈ -92.6, göz yüksekliği, 192.5), ayaklara bakış; baş tarafı
  duruş (≈ -88.8, 190.6); dolap devrilme ekseni dolabın batı alt kenarı, devrilme yönü oyuncuya doğru. Göz yüksekliği,
  oyuncu kamerasının zemine (`BodrumOda_TopraKZemin`, y ≈ 1.6) göre yüksekliğinden okunur.

- [ ] **Adım 1: Builder iskeleti.** Eski `EndCinematic` kökünü sil, yeniden kur; objeleri adıyla bul, bulunamayanı
  `Debug.LogError` ile adıyla bildir ve dur. Örtüde collider yoksa `BoxCollider` ekle. Kamera, kollar ve fener kur.
- [ ] **Adım 2: Kafa anahtarları.** Spec tablosundaki her satır için anahtar; dönüş 0.4 sn, ~160°. Animasyon klibine
  pişirilir (giriş builder'ındaki gibi sık örnekleme).
- [ ] **Adım 3: El anahtarları.** Sağ el feneri tutar (oyun içi fener pozu), sol el 10.0–13.0 arası örtünün baş ucuna uzanır,
  13.0'da örtüden ~5 cm yukarıda durur.
- [ ] **Adım 4: Dolap animasyonu.** Dolap kökünün dönüşü `CabinetFallAngle(t - T_FallStart)` ile animasyon klibinde;
  17.4'ten sonrası önemsiz (ekran siyah).
- [ ] **Adım 5: Ses parçaları.** Cinematic kanalında: adımlar (6.5–10.0 arası 4–5 adım), gıcırtı 15.5, çarpma 17.4.
- [ ] **Adım 6: Derle ve Build.** `execute_code` ile `EndCinematicBuilder.Build()`; konsolda hata yok; sahneyi kaydet.
- [ ] **Adım 7: Kare kontrolü.** Sinematik kamerayı 0.5, 3, 8, 12.9, 16.6, 17.3 sn'de render edip `Temp/ICShots/end_*.png`
  olarak kaydet (`Directory.CreateDirectory` ile) ve bak. Beklenen: 3 sn'de örtü ve çizmeler kadrajda; 12.9'da sol el örtünün
  hemen üstünde; 16.6'da dolap kadrajın ortasında; 17.3'te dolabın tepesi kadrajın üst yarısını kaplıyor.

### Görev 6: Asıl sinematik akışı (`CorpseInspect`, geçiş, sessizlik, bitiş)

**Files:**
- Create: `Assets/Scripts/Cinematic/CorpseInspect.cs`
- Modify: `Assets/Scripts/Cinematic/EndCinematic.cs`
- Modify: `Assets/Scripts/Editor/EndCinematicBuilder.cs` (`CorpseInspect`'i örtüye ekler, Görev 5 alanlarını bağlar)

**Interfaces:**
- Consumes: Görev 4 alanları; Görev 5'teki alan listesi.
- Produces:
  - `CorpseInspect : MonoBehaviour, IInteractable`; `public EndCinematic ending;`
    `GetPrompt()` → `ending.DoorLookDone && !ending.Started ? "İncele" : ""`; `Interact` → `ending.BeginMain()`.
  - `EndCinematic.BeginMain()`; `public event System.Action Ended;`

- [ ] **Adım 1: `BeginMain`.** `Started = true`; `PlayerControlLock.Push()` (bir daha Pop edilmez); imleci gizle ve kilitle;
  oyuncunun o anki kamera dünya pozunu sakla; sinematik kamerayı oyuncu kamerasından kopyala (`CopyFrom`, URP verisi, near clip
  0.05); dinleyiciyi sinematik kameraya geçir; oyuncu kamerasını kapat; fener her zaman açık; `director.time = 0; director.Play()`.
- [ ] **Adım 2: Başlangıç geçişi.** `LateUpdate`: `t < 1.2` iken sinematik kameranın dünya pozu =
  `Lerp/Slerp(saklanan oyuncu pozu, head pozu, StartBlend(t))`; sonrasında kamera head'e yerel sıfırda bağlı.
- [ ] **Adım 3: Sessizlik.** `t >= hushAt` ilk karesinde `HushChannels` içindeki her kanalın `param`'ını
  `GameAudio.Settings.mixer.SetFloat(param, -80f)`; `musicSource` ve `heartbeatSource` durdur.
- [ ] **Adım 4: Siyah ve bitiş.** `t >= blackAt` ilk karesinde tam ekran siyah `Image` (alfa 1, anında); 0.1 sn sonra Cinematic
  kanalı da -80 dB. `t >= endAt` ilk karesinde `director.Pause()` ve `Ended?.Invoke()` (bir kez).
- [ ] **Adım 5: Builder'ı güncelle ve Build et.** Konsolda hata yok; sahneyi kaydet.
- [ ] **Adım 6: Play probu (Review Focus 1, 4).** `execute_code`: kapı bakışını tetikle ve bitir; fenerin pilini bitir ya da
  feneri kapat; oyuncuyu çömelmiş ve cesede yan bakar hâlde koy; `CorpseInspect.Interact` çağır. Örnekler:
  0.0 sn'de kamera oyuncu pozunda (fark < 1 cm, < 1°); 1.25 sn'de head pozunda (fark < 1 cm, < 1°); 5 sn'de fener ışığı açık;
  13.1 sn'de Music, SFX, Ambience, UI parametreleri -80, Cinematic değişmemiş; 17.5 sn'de siyah görüntü alfası 1;
  19.1 sn'de `Ended` bir kez çağrılmış; tüm süre boyunca `PlayerControlLock.IsLocked` true.
- [ ] **Adım 7: Play modundan çık.** Test için değiştirilen pil ve konumlar sahneye kaydedilmez; sahnenin değişmediğini
  `manage_scene` ile kontrol et.

### Görev 7: Ses dengesi ve level notu

**Files:**
- Modify: `Assets/Scripts/Editor/EndCinematicBuilder.cs` (yalnızca ses seviyesi sabitleri)
- Modify: `docs/level-notlari.md`, `docs/level-notlari.pdf`

- [ ] **Adım 1: Seviyeleri ölç.** Giriş sinematiğindeki `levels.py` yöntemiyle Timeline klip seviyelerini ölç. Hedef: gıcırtı
  sessizlikten sonra net ama çarpmadan en az 6 dB kısık; çarpma bütün sinematiğin en yüksek sesi; adımlar müziğin altında.
- [ ] **Adım 2: Sabitleri ayarla, Build et, tekrar ölç.**
- [ ] **Adım 3: Level notuna "Oyun sonu sinematiği" bölümü ekle.** Spec'in "Level'a etkisi" bölümündeki üç madde; ardından
  PDF'i `md2html.py` ve headless Edge ile yeniden üret.
- [ ] **Adım 4: Uçtan uca Play.** Kullanıcıdan sinematiği bir kez Play'de izlemesini iste; sonucu bildir.
