# Ses Mixeri — Tasarım (kısa)

## Amaç
Oyundaki tüm sesleri tek bir AudioMixer üzerinden yönetmek. Şimdilik seviyeler editörden ayarlanacak;
ileride kurulacak ayarlar sekmesi aynı API'yi çağıracak. Ana menü ve credits bu işin kapsamında değil.

## Hedefler / hedef olmayanlar
- Hedef: Master / Müzik / Efekt (+Sinematik alt grubu) / Ortam / Arayüz grupları.
- Hedef: Inspector'da 0–1 slider'larla ayar; Play modunda da anında etki ve kalıcı değer.
- Hedef: Mevcut tüm sesler doğru gruba gider (kapı, fener, cam, intro Timeline, NightAmbience).
- Hedef değil: ana menü, credits sahnesi, müzik çalar, ayarlar arayüzü, PlayerPrefs kaydı.
- Kaçınılacak: oyuncunun duyduğu dengeyi değiştirmek (varsayılan seviyeler 1 = 0 dB, mevcut mix aynen kalır).

## Yaklaşımlar
1. Sadece AudioMixer, ayar Audio Mixer penceresinden (dB). Sade ama ayarlar menüsü için ayrıca dB çevirisi
   yazmak gerekir; Play modundaki değişiklikler kolay kaybolur.
2. **Seçilen:** AudioMixer + ayar asset'i (ScriptableObject, 0–1 slider). Editörde şimdi kullanılan şey ile
   ileride oyuncunun kullanacağı şey aynı olur.
3. Mixer'sız, script ile ses seviyesi yönetimi — alt grup, filtre, snapshot imkânı olmadığı için elendi.

## Kararlar
- `Assets/Audio/MainMixer.mixer`: Master → Music, SFX → Cinematic, Ambience, UI. Her grubun Volume'u
  expose edilir (`MasterVol`, `MusicVol`, `SFXVol`, `CinematicVol`, `AmbienceVol`, `UIVol`).
- `Assets/Resources/GameAudioSettings.asset` (sınıf adı `GameAudioSettings`; Unity'nin `AudioSettings`
  sınıfıyla çakışmasın diye). Alanlar: mixer, her kanal için grup referansı ve 0–1 seviye.
  `SetVolume(kanal, 0–1)` → dB'ye çevirip mixer'a yazar (0 → -80 dB, log ölçek). OnValidate ile Play
  modunda anında uygulanır; oyun başında otomatik uygulanır.
- Statik yardımcı `GameAudio`: `Group(kanal)`, `PlayAt(clip, pos, vol, kanal)` — runtime'da oluşturulan
  AudioSource'lar (Door, Flashlight, BreakableGlass) bunu kullanır.
- Intro Timeline ses izleri IntroCinematic altında Cinematic grubuna bağlı AudioSource'lara bağlanır
  (IntroCinematicBuilder üzerinden, tekrar kurulabilir).
- NightAmbience kaynakları Ambience grubuna gider.

## Uygulayıcıya kalan
- dB eğrisi ayrıntısı, asset bulunamazsa sessizce mixer'sız çalma (oyun bozulmasın).
