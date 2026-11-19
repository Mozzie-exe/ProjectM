# Ses Mixeri Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tüm oyun seslerini tek AudioMixer'dan yönetmek; seviyeler Inspector'da 0–1 slider ile, ileride ayarlar menüsünün çağıracağı aynı API üzerinden.

**Architecture:** Editör menüsü (`Tools/Audio/Build Mixer`) mixer'ı ve grupları kurar (Unity'nin dahili `UnityEditor.Audio.AudioMixerController` API'si reflection ile; public API grup oluşturmaya izin vermiyor). `GameAudioSettings` ScriptableObject (Resources içinde) grup referanslarını ve 0–1 seviyeleri tutar, mixer'a dB yazar. Statik `GameAudio` yardımcısı runtime AudioSource'larına grup atar.

**Tech Stack:** Unity 6.5, URP, Timeline 1.8.12, Unity Test Framework 1.7.0 (EditMode).

**Spec:** `docs/superpowers/specs/2026-11-19-audio-mixer-design.md`

## Global Constraints
- Gruplar: Master → Music, SFX → Cinematic, Ambience, UI.
- Exposed parametreler: `MasterVol`, `MusicVol`, `SFXVol`, `CinematicVol`, `AmbienceVol`, `UIVol`.
- Dosyalar: `Assets/Audio/MainMixer.mixer`, `Assets/Resources/GameAudioSettings.asset`.
- Sınıf adı `GameAudioSettings` (UnityEngine.AudioSettings ile çakışmasın).
- Varsayılan seviyeler 1 (= 0 dB); mevcut mix duyulur biçimde değişmez.
- PlayerPrefs, ayarlar arayüzü, ana menü, credits yok.
- Commit yalnızca kullanıcı isteyince.

## Review Focus
- Ayar asset'i yoksa / mixer atanmamışsa: sesler eskisi gibi mixer'sız çalar, hata yok.
- Seviye 0: tam sessiz (-80 dB), NaN/-Infinity değil.
- Play modunda slider oynatma: anında duyulur ve Play'den çıkınca asset değerini korur.
- Builder tekrar çalıştırılınca: mixer/gruplar çoğalmaz, asset referansları kopmaz.
- Intro sinematiği yeniden kurulunca (`Tools/Intro Cinematic/Build`) Timeline izleri yine Cinematic grubuna bağlı.

---

### Task 1: GameAudioSettings + GameAudio (runtime API)

**Files:**
- Create: `Assets/Scripts/Audio/AudioChannel.cs` — `public enum AudioChannel { Master, Music, SFX, Cinematic, Ambience, UI }`
- Create: `Assets/Scripts/Audio/GameAudioSettings.cs`
- Create: `Assets/Scripts/Audio/GameAudio.cs`
- Test: `Assets/Scripts/Editor/Tests/GameAudioTests.cs`

**Interfaces:**
- Produces:
  - `GameAudioSettings : ScriptableObject` — `AudioMixer mixer`; serialized `Channel[] channels` (`AudioChannel id; AudioMixerGroup group; string param; [Range(0,1)] float level = 1`).
  - `static float GameAudioSettings.LinearToDb(float level)` → `level <= 0.0001f ? -80f : Mathf.Log10(level) * 20f`.
  - `float GetVolume(AudioChannel)`, `void SetVolume(AudioChannel, float)` (clamp 0–1, mixer'a yazar), `void ApplyAll()`, `AudioMixerGroup GetGroup(AudioChannel)`.
  - `OnValidate` → Play modundaysa `ApplyAll()`.
  - `static GameAudioSettings GameAudio.Settings` (`Resources.Load<GameAudioSettings>("GameAudioSettings")`, önbellekli, yoksa null).
  - `static AudioMixerGroup GameAudio.Group(AudioChannel)` (null-güvenli).
  - `static AudioSource GameAudio.PlayAt(AudioClip clip, Vector3 pos, float volume, AudioChannel ch)` — geçici GO, `spatialBlend = 1`, grup atanmış, `clip.length` sonra yok edilir.
  - `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` → `Settings?.ApplyAll()`.

- [ ] **Step 1: Testleri yaz** — `LinearToDb(1) == 0`, `LinearToDb(0) == -80`, `LinearToDb(0.5f) ≈ -6.02 (±0.01)`; `SetVolume(SFX, 2f)` sonrası `GetVolume(SFX) == 1`; mixer'sız asset'te `SetVolume` hata atmaz.
- [ ] **Step 2: Test Runner'da (EditMode) çalıştır** — derleme hatası/fail beklenir.
- [ ] **Step 3: Üç dosyayı yaz.**
- [ ] **Step 4: Testleri çalıştır** — hepsi PASS.

### Task 2: Mixer builder

**Files:**
- Create: `Assets/Scripts/Editor/AudioMixerBuilder.cs` — menü `Tools/Audio/Build Mixer`.

**Interfaces:**
- Consumes: Task 1 `GameAudioSettings`, `AudioChannel`.
- Produces: `static void AudioMixerBuilder.Build()`; sonuç `Assets/Audio/MainMixer.mixer` + `Assets/Resources/GameAudioSettings.asset` (mixer ve 6 kanal dolu).

- [ ] **Step 1: Önce reflection API'sini dene** — `AudioMixerController.CreateMixerControllerAtPath`, `CreateNewGroup(name, bool)`, `AddChildToParent`, `masterGroup`, `GetGUIDForVolume`, `AudioParameterPath` ile expose; execute_code ile tek tek doğrula.
- [ ] **Step 2: Builder'ı yaz** — mixer varsa yeniden oluşturmaz, eksik grup/parametreyi ekler; asset varsa seviyeleri korur.
- [ ] **Step 3: İki kez çalıştır, doğrula** — 6 grup, 6 exposed param, `mixer.SetFloat("SFXVol", -10)` true döner; ikinci çalıştırmada sayılar aynı.
- [ ] **Step 4: Test ekle/çalıştır** — `GameAudio.Settings != null`, her kanal için `GetGroup` null değil, `mixer.GetFloat(param, out _)` true.

### Task 3: Seslerin gruplara bağlanması

**Files:**
- Modify: `Assets/Scripts/Interaction/Door.cs:46-48` — `outputAudioMixerGroup = GameAudio.Group(AudioChannel.SFX)`.
- Modify: `Assets/Scripts/Player/Flashlight.cs:55-57` — aynı, SFX.
- Modify: `Assets/Scripts/Interaction/BreakableGlass.cs:88` — `GameAudio.PlayAt(clip, transform.position, volume, AudioChannel.SFX)`.
- Modify: `Assets/Scripts/Cinematic/AmbienceLoop.cs` (Awake) — kaynaklara `GameAudio.Group(AudioChannel.Ambience)`.
- Modify: `Assets/Scripts/Editor/IntroCinematicBuilder.cs` `BuildAudio` — her AudioTrack için IntroCinematic altında `Audio_<iz>` AudioSource (2D, Cinematic grubu) ve `director.SetGenericBinding(track, source)`.

- [ ] **Step 1: Değişiklikleri yap, derle** — konsolda hata yok.
- [ ] **Step 2: `Tools/Intro Cinematic/Build` çalıştır** — 5 audio track'in her biri Cinematic gruplu bir AudioSource'a bağlı (execute_code ile kontrol).
- [ ] **Step 3: Play testi** — intro oynarken `CinematicVol`'u 0 yap → Timeline kaynakları sessiz; `AmbienceVol` 0 → NightAmbience sessiz; handoff sonrası fener tıkı SFX'ten. Konsolda hata yok. Sahneyi kaydet.
