# Ahır Gerilim Sahnesi — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ahırın sağ ön kapısı E ile kayarak açılır; içeri girince ~2.4 sn'lik olay: gıcırtı, kamera samanlığa çekilir, 3 balya önüne düşer, samanlıkta siluet kaybolur, kalp atışı, kontrol geri gelir.

**Architecture:** Saf zamanlama/eğri matematiği `BarnScareMath`'te (EditMode testli). `SlidingDoor` yeni bir `IInteractable`. `BarnScare` MonoBehaviour'u trigger + coroutine ile olayı oynatır (Timeline yok). Sahne kurulumu `BarnScareBuilder` editör menüsüyle, mevcut builder'lar gibi.

**Tech Stack:** Unity 6 (6000.5) URP, C#, NUnit EditMode, unityMCP (Play doğrulaması, ekran görüntüsü).

**Spec:** `docs/superpowers/specs/2026-11-27-ahir-gerilim-sahnesi-design.md`

## Global Constraints

- Commit yok; kullanıcı kendisi commit eder.
- Mevcut objeler silinmez/taşınmaz; `Ahir_Kapi_Sag_Aralik`'a yalnızca bileşen eklenir. Static flag/occlusion'a dokunulmaz.
- Level değişiklikleri `docs/level-notlari.md`'ye yazılır, PDF yenilenir; ses istekleri `docs/ses-notlari.md`'ye, PDF yenilenir.
- Sesler şimdilik mevcut kliplerden geçici: `Assets/Sfx/Ending/Cabinet_Creak.mp3` (gıcırtı), `Cabinet_Fall.mp3` (balya düşüşü), `Heartbeat_Loop.mp3`, `Breath_Gasp.mp3`. Kapı kayma ve tahta ayak sesi alanları boş kalabilir (null güvenli); indirme yapılmaz.
- Ses kaynakları `GameAudio.Group(AudioChannel.SFX)`'e bağlanır, 3D olanlar `GameAudio.Apply3D`.
- Build/derleme öncesi `EditorApplication.isPlaying` kontrolü.
- Kod yorumları Türkçe, mevcut dosyaların üslubunda.

## Review Focus

1. Oyuncu tetikten geçerken balyaların collider'ı oyuncuyu itmemeli → balya collider'ları düşüş bitene kadar kapalı (Task 3 Step 5).
2. Olay bitince `MouseLook` eski pitch'e sıçramamalı → `MouseLook.Pitch` senkronu (Task 1 testi + Task 3 Step 5).
3. Olay sırasında imleç görünmemeli (`PlayerControlLock.Push` imleci açıyor) → Push'tan hemen sonra imleç kilitlenir (Task 3 Step 5).
4. Kapı açılmadan ya da ikinci kez tetiklenmemeli → `BarnScareMath.CanTrigger` testi (Task 1) + Play kontrolü.
5. Duraklatma menüsü olay ortasında açılırsa olay donmalı ve devam edebilmeli → coroutine ölçekli zaman kullanır (Task 3 Step 5).

---

### Task 1: BarnScareMath + MouseLook pitch erişimi

**Files:**
- Create: `Assets/Scripts/Cinematic/BarnScareMath.cs`
- Create: `Assets/Scripts/Editor/Tests/BarnScareMathTests.cs`
- Modify: `Assets/Scripts/Player/MouseLook.cs` (özellik ekle)

**Interfaces:**
- Produces (`public static class BarnScareMath`):
  - Sabitler: `T_Creak = 0f`, `T_Pull = 0.3f`, `PullDuration = 0.6f`, `T_Bales = 0.8f`, `BaleStagger = 0.12f`, `T_Shadow = 1.0f`, `ShadowDuration = 1.0f`, `T_Heart = 1.6f`, `T_Release = 2.4f`, `TipDuration = 0.35f`, `Gravity = 9.81f`, `DoorSlideDuration = 1.2f`, `DoorSlideDistance = 2.5f`.
  - `float LookWeight(float t)` — T_Pull öncesi 0, T_Pull..T_Pull+PullDuration smoothstep, sonra T_Release'e kadar 1, T_Release'den sonra 0.
  - `float SlideOffset(float t, float duration, float distance)` — smoothstep(t/duration)·distance, [0, distance] aralığında.
  - `float TipAngle(float tLocal)` — 0..TipDuration'da 0→90 (ease-in, s²), öncesi 0, sonrası 90.
  - `float FallDrop(float tLocal, float height)` — düşüş TipDuration·0.5'te başlar: `0.5·Gravity·(tLocal−TipDuration·0.5)²`, [0, height] aralığında.
  - `bool CanTrigger(bool doorOpen, bool alreadyFired) => doorOpen && !alreadyFired`.
- Produces (`MouseLook`): `public float Pitch { get; set; }` — `xRot`'u okur/yazar, set ederken [−90, 90]'a kırpar.

- [ ] **Step 1: Testleri yaz** (`BarnScareMathTests`, `EndCinematicMathTests` düzeninde)
  - `LookWeight_ZeroBeforePull`: `LookWeight(0.2f) == 0`
  - `LookWeight_FullAfterPullUntilRelease`: `LookWeight(0.9f) == 1`, `LookWeight(2.0f) == 1`, `0 < LookWeight(0.6f) < 1`
  - `LookWeight_ZeroAfterRelease`: `LookWeight(2.5f) == 0`
  - `SlideOffset_Clamped`: `(−1,1.2,2.5)=0`, `(0,1.2,2.5)=0`, `(1.2,1.2,2.5)=2.5`, `(5,1.2,2.5)=2.5`
  - `TipAngle_ZeroToNinety`: `TipAngle(0)=0`, `TipAngle(0.35f)=90`, `TipAngle(1f)=90`
  - `FallDrop_ClampsAtHeight`: `FallDrop(0.1f,4.8f)=0`, `FallDrop(0.675f,4.8f)≈1.226` (tolerans 1e-3), `FallDrop(10f,4.8f)=4.8`
  - `CanTrigger_OnlyOpenAndNotFired`: (true,false)=true; (false,false)=false; (true,true)=false
  - `MouseLookPitch_Clamps` (yeni GameObject + `MouseLook`): `Pitch = 120` → `90`; `Pitch = −30` → `−30`
- [ ] **Step 2: Çalıştır, derleme hatasıyla düştüğünü gör** — `run_tests` EditMode. Beklenen: `BarnScareMath`/`Pitch` bulunamadı.
- [ ] **Step 3: `BarnScareMath` ve `MouseLook.Pitch`'i yaz.**
- [ ] **Step 4: Tüm EditMode testleri** — Beklenen: 50/50 (42 + 8).

### Task 2: SlidingDoor

**Files:**
- Create: `Assets/Scripts/Interaction/SlidingDoor.cs`

**Interfaces:**
- Consumes: `BarnScareMath.SlideOffset`, `DoorSlideDuration`, `DoorSlideDistance`; `IInteractable`; `WindRattle`.
- Produces: `public class SlidingDoor : MonoBehaviour, IInteractable` — alanlar `Vector3 localAxis` (Awake'te aynı objedeki `WindRattle.localAxis`, yoksa `Vector3.right`), `float distance = 2.5f`, `float duration = 1.2f`, `string openText = "Kapıyı Aç"`, `AudioClip slideSound` (null olabilir); `public bool IsOpen { get; }` (Interact anında true); `GetPrompt()` açıldıktan sonra boş; `Interact` tek sefer çalışır, aynı objedeki `WindRattle`'ı kapatır, kapıyı o anki `localPosition`'dan `localAxis.normalized·distance` kaydırır.

- [ ] **Step 1:** `SlidingDoor`'u yaz; kayma `Update`'te `SlideOffset(elapsed, duration, distance)` ile.
- [ ] **Step 2:** Derle, `read_console` hata yok; EditMode 50/50. (Davranış Task 3'te Play'de doğrulanır.)

### Task 3: BarnScare + builder + Play doğrulaması

**Files:**
- Create: `Assets/Scripts/Cinematic/BarnScare.cs`
- Create: `Assets/Scripts/Editor/BarnScareBuilder.cs`
- Modify: `Assets/Scenes/SampleScene.unity` (builder çalıştırılarak)

**Interfaces:**
- Consumes: Task 1 sabit ve fonksiyonları, `MouseLook.Pitch`, `SlidingDoor.IsOpen`, `PlayerControlLock.Push/Pop`, `GameAudio`.
- Produces: `public class BarnScare : MonoBehaviour` — alanlar `SlidingDoor door`, `Transform lookTarget`, `Transform[] bales` (3), `float[] baleHeights`, `Transform shadow`, `Vector3 shadowExit`, `ParticleSystem dust`, `AudioClip creak, baleThud, heartbeat, gasp, woodSteps`; `public bool Fired { get; }`. `OnTriggerEnter` (oyuncunun `CharacterController`'ı) → `CanTrigger(door.IsOpen, Fired)` ise coroutine.
- Builder menüsü `Tools/Level/Build Barn Scare` → kök obje `BarnScare` (varsa yeniden kullanır, çocuklarını yeniden kurar).

- [ ] **Step 1: `BarnScare` coroutine'i yaz** (t ölçekli zaman):
  - 0: `creak` samanlık kenarında 3D.
  - T_Pull: `PlayerControlLock.Push()`; hemen ardından `Cursor.lockState = Locked`, `visible = false`. Başlangıç yaw (player body) ve pitch (`MouseLook.Pitch`) kaydedilir, hedef `lookTarget`'tan; her karede `LookWeight(t)` ile `Mathf.LerpAngle`.
  - T_Bales + i·BaleStagger: balya i devrilir (`TipAngle`, kenara paralel eksende) ve düşer (`FallDrop`, `baleHeights[i]`). İnişte collider açılır (Rigidbody yok), `baleThud` 3D, `dust` emit, kamerada 0.25 sn ≤0.05 m sarsıntı.
  - T_Shadow: `shadow` ShadowDuration'da `shadowExit` kadar kayar, sonra kapanır; `woodSteps` varsa çalar.
  - T_Heart: `heartbeat` 2D, 3 sn'de söner; `gasp` bir kez.
  - T_Release: `MouseLook.Pitch` = o anki kamera pitch'i; `PlayerControlLock.Pop()`.
- [ ] **Step 2: `BarnScareBuilder`'ı yaz:**
  - `Ahir_Kapi_Sag_Aralik`'a `SlidingDoor` (varsa yeniden kullan); BoxCollider kalır.
  - Trigger: BoxCollider (isTrigger) 3×3×4 m, kapının ~3 m içinde, orta koridorda (zemin y 10).
  - Samanlık kenarı: tetikten içeri bakışın önündeki yan samanlığın (y≈14.8, `Barn_Planks`) koridora bakan kenarı, raycast ile bulunur; `lookTarget` kenar ortası +0.5 m.
  - 3 balya: mevcut `HaiSquare` mesh+malzeme kopyası, kenarda yan yana; iniş tetik merkezinden 1.5–2.5 m ileri, koridorun yarısından fazlasını kapatmaz. `baleHeights[i]` = kenar y − zemin y.
  - Siluet: kapsül gövde (1.75 m) + küre baş, unlit siyah malzeme, collider yok; balyaların arkasında samanlıkta, `shadowExit` koridordan 2 m uzağa.
  - Toz: ParticleSystem (kahverengi-gri, 40 parçacık, 1.5 sn ömür, URP Particles Unlit).
  - Klipler Global Constraints'teki geçici yollardan; `woodSteps`, `slideSound` null. Sahneyi kaydet.
- [ ] **Step 3:** Derle (`isPlaying` kontrol), konsol temiz, EditMode 50/50.
- [ ] **Step 4:** Builder'ı çalıştır; kamera görüntüleri: tetikten samanlığa bakış (balyalar + siluet kadrajda), koridor (yol açık).
- [ ] **Step 5: Play doğrulaması** (`Application.runInBackground = true`):
  - Kapı kapalıyken oyuncuyu tetiğe sok → olay olmaz.
  - `SlidingDoor.Interact` → kapı 1.2 sn'de 2.5 m kayar, WindRattle kapalı.
  - Tetiğe sok → T≈0.9 ekran görüntüsü (balyalar havada, siluet kadrajda); T≈2.6: `PlayerControlLock.IsLocked == false`, imleç kilitli/görünmez, `MouseLook.Pitch` kamera pitch'ine eşit, balyalar yerde, oyuncu konumu itilmemiş, `Ahir_Varil`'e yol açık (raycast/kapsül testi).
  - Olay ortasında `Time.timeScale = 0` 1 sn → durur, geri açınca devam eder.
  - İkinci kez tetiğe girme → tekrar etmez.
  - Ekran görüntülerini `Assets/Screenshots`'tan sil.

### Task 4: Notlar

**Files:**
- Modify: `docs/level-notlari.md` (+ PDF), `docs/ses-notlari.md` (+ PDF)

- [ ] **Step 1:** `level-notlari.md`'ye "## Ahır gerilim sahnesi (yeni)": kapı artık açılıyor, eklenen objeler ve yerleri, tetik, balyaların koridorda kalıcı durduğu, `Tools/Level/Build Barn Scare`, samanlık/kapı değişirse builder'ı yeniden çalıştır.
- [ ] **Step 2:** `ses-notlari.md`'ye ahır olayı ses tablosu (zaman, ses, şu anki geçici, istenen): kapı kayma, samanlık gıcırtısı, balya düşüşü ×3, tahtada uzaklaşan ayak sesi, kalp/nefes (mevcut).
- [ ] **Step 3:** İki PDF'i yeniden üret (`md2html2.py` + Edge headless), tarih 27 Kasım 2026.
- [ ] **Step 4:** Son kontrol: EditMode 50/50, sahne kirli değil, konsol temiz.
