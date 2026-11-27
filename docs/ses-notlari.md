# Ses efektleri notları (giriş ve oyun sonu sinematikleri)

Sinematiklerde hangi sesin nerede, ne zaman ve hangi seviyede çaldığını anlatıyor. Bir sesi değiştirmek ya da
yenisini eklemek istediğinde nereye dokunman gerektiği burada.

## Sesler oyuna nasıl bağlı

- Sinematik sesleri Unity Timeline üzerinde duruyor. Timeline'ları elle değil, builder script'leri kuruyor:
  - Giriş: `Tools > Intro Cinematic > Build` (`Assets/Scripts/Editor/IntroCinematicBuilder.cs`)
  - Oyun sonu: `Tools > End Cinematic > Build` (`Assets/Scripts/Editor/EndCinematicBuilder.cs`)
- Timeline'daki bir klibi elle taşırsan bir sonraki Build'de geri gelir. Zamanlamayı değiştirmek istersen bize söyle,
  builder'da değiştirelim.
- **Bir sesi değiştirmenin en kolay yolu:** aynı adla, aynı klasöre yeni dosyayı koy (ör. `Assets/Sfx/Ending/Cabinet_Fall.mp3`
  üstüne yaz). Dosyanın `.meta`'sı aynı kalırsa sahnedeki bağlantılar bozulmaz. Sonra ilgili Build'i çalıştır.
- Dosya adını değiştirirsen builder dosyayı bulamaz. Yeni bir ses eklemek istersen adını ve nerede çalacağını söyle.

### Mikser kanalları

`AudioMixer` içinde 6 kanal var. Ayarlar menüsündeki ses kaydırıcıları bunlara bağlı. Varsayılan seviyeler:

| Kanal | Varsayılan | Ne çalıyor |
|---|---|---|
| Master | 1.00 | hepsi |
| Music | 1.00 | menü müziği, ceset odası müziği, credits müziği |
| SFX | 0.30 | oyun içi efektler (adım, kapı vb.) |
| Cinematic | 0.40 | iki sinematiğin Timeline sesleri |
| Ambience | 0.40 | rüzgâr, cırcır böceği, kargalar |
| UI | 1.00 | menü sesleri |

- Sinematik sesleri **Cinematic** kanalından çıkıyor. Duyulan ses = dosyanın kendi seviyesi × klibin volume değeri ×
  kanal seviyesi.
- Oyun sonu sinematiğinde 13. saniyede Music, SFX, Ambience ve UI kanalları tamamen susturuluyor (−80 dB). Yalnızca
  Cinematic açık kalıyor. Sinematik bitince (21.5 sn) o da susturuluyor.

## Oyun sonu sinematiği (bodrum, 21.5 sn)

Oyuncu örtülü cesede E ile basınca başlıyor. Akış: cesedi boydan inceleme → yanına yürüme → çömelip örtüye uzanma →
**her şey susuyor (5 sn sessizlik)** → dolap gıcırdıyor → kamera ani dönüyor → dolap devriliyor → siyah ekran → çınlama.

Dosyalar: `Assets/Sfx/Ending/` (adım sesi `Footsteps_Gravel` ise `Assets/Sfx/Intro/` içinde)

| Zaman (sn) | Dosya | Süre | Volume | Not |
|---|---|---|---|---|
| 3.72 | Footsteps_Gravel | 0.4 | 0.9 | adım (dosyanın 3.59. sn'sinden) |
| 6.92 | Footsteps_Gravel | 0.4 | 0.9 | adım (4.14) |
| 7.57 | Footsteps_Gravel | 0.4 | 0.9 | adım (5.16) |
| 8.22 | Footsteps_Gravel | 0.4 | 0.9 | adım (6.89) |
| 8.87 | Footsteps_Gravel | 0.4 | 0.9 | adım (8.88) |
| 9.47 | Footsteps_Gravel | 0.4 | 0.9 | adım (3.59) |
| 18.00 | Cabinet_Creak | 1.72 | 1.0 | dolap yavaşça 5° eğiliyor |
| 19.38 | Cabinet_Fall | 2.12 | 1.0 | çarpma anı dosyanın 0.52. sn'si = 19.9 (ekran kararıyor) |
| 19.95 | Ear_Ring | 1.42 | 1.0 | siyah ekranda kulak çınlaması |

Önemli anlar:

| Zaman (sn) | Olay |
|---|---|
| 0 | kapı açılınca başlayan ceset odası müziği (`ceset_room_extended.wav`, Music, 0.8, döngü) zaten çalıyor |
| 13.0 | sessizlik: müzik ve tüm kanallar susuyor |
| 18.0 | gıcırtı |
| 18.7–19.1 | kamera dolaba dönüyor |
| 19.1 | dolap devrilmeye başlıyor (0.8 sn, hızlanarak) |
| 19.9 | çarpma, siyah ekran |
| 21.5 | sinematik bitiyor (ileride credits gelecek) |

## Giriş sinematiği (araba, 129.5 sn)

Dosyalar: `Assets/Sfx/Intro/`. Zamanlar sinematiğin başından. "Başlangıç" dosyanın kaçıncı saniyesinden çaldığı.
İlk 12 sn ekran simsiyah, sonra 6 sn'de açılıyor. İlk 3.7 sn sadece yol ve radyo; telefon konuşması karanlıkta
başlıyor. 72.2–95.5 arası kontak kapandıktan sonraki duraklama: motor sustu, sadece soğuma tıkırtısı ve telefonun
son kısmı var.

| Zaman (sn) | Dosya | Süre | Başlangıç | Volume |
|---|---|---|---|---|
| 0 | Drive_Interior | 58.5 | — | 1.0 |
| 0 | Radio_Car | 70.95 | — | 0.07 |
| 48.6 | Car_Passing (pikap) | 6.86 | — | 0.25 |
| 53.5 | Car_ArriveStopDoor (rölanti) | 6 | 3.5 | 0.18 |
| 54.0 | Car_ArriveAndStop | 9.6 | 4.5 | 0.22 |
| 58.9 | Car_ArriveStopDoor (rölanti) | 6 | 3.5 | 0.18 |
| 59.34 | GearShift | 0.7 | 36.22 | 0.7 |
| 60.57 | Handbrake | 0.65 | 0.37 | 0.8 |
| 64.3 | Car_ArriveStopDoor (rölanti) | 6 | 3.5 | 0.28 |
| 64.9 | Paper_Grab | 0.9 | 0.8 | 0.6 |
| 66.8 | Paper_Rustle | 0.9 | 0.2 | 0.9 |
| 68.3 | Flashlight_Pickup | 0.8 | 8.2 | 0.65 |
| 69.7 | Car_ArriveStopDoor (rölanti) | 1.55 | 3.5 | 0.28 |
| 70.3 | EngineOff_Key | 3.1 | 2.4 | 0.8 |
| 71.7 | Paper_Rustle | 0.9 | 0.2 | 0.8 |
| 72.2 | Car_CoolingTicks | 54.3 | — | 0.15 |
| 95.55 | Door_Open | 1.1 | — | 0.9 |
| 97.6 | Cloth_Rustle | 3.0 | 2.5 | 1.0 |
| 98.3 | Footsteps_AsphaltBoost | 2.6 | 4.0 | 0.55 |
| 100.27 | Flashlight_Click | 0.8 | — | 1.0 |
| 101.7 | Footsteps_AsphaltBoost | 1.2 | 6.2 | 0.70 |
| 102.5 | Door_Close | 2.0 | 0.4 | 0.45 |
| 104.54–108.09 | Footsteps_AsphaltBoost (6 adım) | 0.42 | — | 0.85 |
| 113.9 | Car_Passing (wagon) | 6.86 | — | 0.95 |
| 121.3 | Dog_DistantBark | 6.3 | — | 0.35 |
| 125.4–128.01 | Footsteps_AsphaltBoost (5 adım) | 0.42 | — | 0.90 |

**Otomatik kısma:** Biri konuşurken sürüş, rölanti, varış, soğuma tıkırtısı ve geçen arabalar yarıya (−6 dB) iner,
konuşma bitince 0.8 sn'de geri gelir. Radyo ve diğer efektler kısılmaz. Yeni bir motor/yol sesi verirsen bu kısmayı
hesaba kat: konuşmalı yerlerde zaten iniyor. Karışımda bütün replikler arka planın en az 7.5 dB üstünde.

### Telefon konuşması

**Hikâye:** Richard ve Wayne eski ortak. Üç yıl önce bir yol ayrımında Richard, Wayne'i sattı; Wayne arşiv odasına
sürüldü. Konuşma soğuk başlıyor, Wayne'in öfkesiyle patlıyor ("Sen de beni gömdün"), uzun bir sessizlikten sonra
Richard konuyu kapatıyor. Park ettikten sonra Wayne, bütün kırgınlığına rağmen doğruyu soruyor; Richard'ın bildiği
tek şey işin ona verilmesi.

Placeholder seslendirme (ElevenLabs), `Assets/Sfx/Intro/Dialogue/`, mono wav. Ham (efektsiz) hâlleri bizde yedekli;
efekt zincirini değiştirmek istersen söyle, yeniden üretiriz.

- **Richard (telefon):** 300 Hz–3.4 kHz bant, gerçek cep telefonu kodeğinden geçirildi (AMR-NB, 8 kHz), altında
  hafif hat cızırtısı (konuşma aralarında yaklaşık −46 dB). Tartışma boyunca hat temiz (12.2 kbit/s). Wayne köye
  yaklaştıkça çekim zayıflıyor: kodek kalitesi düşüyor (12.2 → 10.2 → 7.95 kbit/s), cızırtı artıyor ve 65 ms'lik
  kopmalar başlıyor (Locals'ta 1, Miller ve AskedForYou'da 2'şer, park sonrası her replikte 2). Kopma anında kısa,
  kısık bir dijital çıtırtı var.
- **Wayne (arabada):** küçük kabin yankısı (4–23 ms erken yansımalar), hafif gövde. Sürüşteki replikler yol
  gürültüsü yüzünden biraz yüksek (öfkeli replikte −17.5 LUFS), "...Miller." fısıltısı ve park sonrası alçak (−22…−24).
- **Nefes:** Wayne park sonrası soruyu sormadan önce nefes alıyor (`Ending/Breath_Hold`, 71.95 sn, volume 0.5).
- **İç çekişler** konuşmanın içinde: "Sen de beni gömdün"den sonraki sessizliğin sonunda Richard, "Peki."den sonra Wayne
  iç çekiyor. "Tabelayı yeni geçtim" araba tabelayı geçtiği anda (38.7 sn) geliyor.

| Zaman (sn) | Dosya | Kim | Altyazı |
|---|---|---|---|
| 3.67 | Dlg_00a_Richard_BeenAWhile | Richard | Uzun zaman oldu. |
| 5.93 | Dlg_00b_Wayne_ThreeYears | Wayne | Üç yıl. Saydığını bilmiyordum. |
| 9.53 | Dlg_00c_Richard_Counted | Richard | Saydım. Her gününü. |
| 12.58 | Dlg_00d_Wayne_RecordsRoom | Wayne | Komik. Ben de saydım. Arşiv odasında. |
| 17.38 | Dlg_00e_Richard_WayneCut | Richard | Wayne— |
| 17.98 | Dlg_00f_Wayne_MyCall | Wayne | O odada durup kararın benim olduğunu söyledin. Benim kararım, Richard. |
| 23.33 | Dlg_00g_Richard_Family | Richard | Bir ailem vardı. İkimizi birden gömerlerdi. |
| 26.98 | Dlg_00h_Wayne_Buried | Wayne | Sen de beni gömdün. |
| 30.98 | Dlg_00i_Richard_NotTheTime | Richard | (iç çeker) Burası tartışmanın ne yeri ne zamanı, Wayne. Çiftliğe vardın mı? |
| 36.05 | Dlg_00j_Wayne_Fine | Wayne | Peki. (iç çeker) |
| 38.7 | Dlg_02_Wayne_PassedSign | Wayne | Tabelayı yeni geçtim. |
| 40.6 | Dlg_03_Richard_Locals | Richard | Köylüler o çiftlik evinden sesler duyuyormuş… |
| 46.75 | Dlg_03b_Richard_Miller | Richard | Eski Miller evi. Aile '89'da gitti… |
| 51.95 | Dlg_03c_Wayne_Miller | Wayne | ...Miller. |
| 53.3 | Dlg_04_Wayne_Touching | Wayne | Ve aklına ben geldim. Ne incelik. |
| 55.85 | Dlg_05_Richard_AskedForYou | Richard | Gelmedin. İhbar senin adınla geldi. Seni istediler. |
| 67.4 | Dlg_07_Wayne_Note | Wayne | "Işıklar sönmeden gel." (notu okur) |
| 72.9 | Dlg_08b_Wayne_Truth1 | Wayne | Richard. Onca yılın hatrına... beni nasıl bir konumda bıraktığını bir kenara bırak ve bana doğruyu söyle. |
| 80.35 | Dlg_08c_Wayne_Truth2 | Wayne | Beni kim istedi? Ve neden? |
| 84.55 | Dlg_09b_Richard_AllIKnow | Richard | Tek bildiğim, eski dostum, bu işi sana vermeleri. |
| 88.5 | Dlg_09c_Richard_CallMe | Richard | İşin bitince beni mutlaka ara. Karakolu değil. Beni. |
| 93.0 | Dlg_10_Wayne_OldTimes | Wayne | Tıpkı eski günlerdeki gibi. |

**Senden istenen telefon sesleri** (ayrı dosyalar, biz yerleştiririz):

1. **Telefon titreşimi/zili** (siyah ekranda, 3.6 sn'den önce biten): eski bir cep telefonu ya da araç telefonu,
   1–2 çalış, sonra açılma tıkı. Kısa ve kısık olsun, karanlıkta ilk duyulan şey bu.
2. **Hat sesi**: Richard konuşurken çok hafif bir telefon hattı cızırtısı/uğultusu (−45 LUFS civarı, döngü olabilir).
3. **Hattın kapanması** (94.8 sn civarı, Wayne'in "Tıpkı eski günlerdeki gibi" repliğinden hemen sonra): kapanma tıkı
   ve ardından kısa, boğuk bir meşgul sinyali ya da sessizleşen cızırtı (en fazla 0.7 sn). Sonra 95.55'te kapı açılıyor.

## Ortam sesleri ve müzikler

- `NightAmbience/Wind` → `Ambience_Wind`, `NightAmbience/Crickets` → `Ambience_Crickets`,
  `Field/AmbienceZone_Tarla` → `Ambience_Ravens`. Hepsi Ambience kanalında, 2D, döngü. Her objede iki AudioSource var
  (döngü noktası çapraz geçişle gizleniyor), ikisini de bırak.
- Müzikler: `MainMenuTheme_creepy_subtle_lesshiss_long70_varied.mp3` (menü), `ceset_room_extended.wav` (bodrum),
  `credits_extended.wav` (credits; dolabın çarpmasından yaklaşık 2 sn sonra başlıyor, 2 sn'de açılıyor, sonunda 2.5 sn'de kısılıyor. Müziğin tamamı, 57.6 sn, çalıyor).

## Dosya seviyeleri

Ölçüm: integrated loudness (LUFS) ve true peak (dBTP). Kaba hedef: efektler −20…−30 LUFS, true peak en fazla
**−1 dBTP**. 0 ve üstü, dosyanın kendisinde kırpılma riski demek.

| Dosya | LUFS | True peak | Süre |
|---|---|---|---|
| Ending/Cabinet_Creak | −22.0 | −5.8 | 1.65 sn |
| Ending/Cabinet_Fall | −7.9 | **+0.7** | 3.00 sn |
| Ending/Ear_Ring | −28.0 | −23.4 | 1.35 sn |
| Footsteps_Gravel | −32.3 | | |
| Car_ArriveAndStop | −18.6 | | |
| Car_ArriveStopDoor | −19.7 | −1.0 | |
| Car_CoolingTicks | −24.2 | | |
| Car_Passing | −15.8 | | |
| Cloth_Rustle | −42.2 | | |
| Dog_DistantBark | −24.5 | | |
| Door_Close | −14.5 | **+0.2** | |
| Door_Open | −24.7 | | |
| Drive_Interior | −29.8 | | |
| EngineOff_Key | −42.6 | | |
| Flashlight_Click | −36.7 | | |
| Flashlight_Pickup | −30.2 | **+1.9** | |
| Footsteps_Asphalt | −36.2 | | |
| Footsteps_AsphaltBoost | −27.4 | **0.0** | |
| GearShift | −22.2 | **+1.4** | |
| Handbrake | −22.5 | **+1.4** | |
| Paper_Grab | −22.2 | **+0.5** | |
| Paper_Rustle | −28.0 | | |
| Radio_Car | −11.9 | **0.0** | |
| Ambience_Crickets | −38.0 | | |
| Ambience_Wind | −31.5 | | |
| Ambience_Ravens | −29.5 | | |
| MainMenuTheme | −18.4 | | |
| ceset_room_extended | −14.1 | −2.6 | |
| credits_extended | −27.4 | | |

## Bilinen sorunlar (senden istenenler)

Oyun sonu sinematiğini kaydedip dinledik. Bulduklarımız, önem sırasıyla:

1. **Devrilme sesi kırpılıyor.** `Cabinet_Fall` çok yüksek (−7.9 LUFS, +0.7 dBTP). Gıcırtının kuyruğu ve çınlamayla
   üst üste binince 19.6. saniyeden sonra kırpılma var. Dosyayı −1 dBTP'ye indirip seviyesini biraz düşürmek yeterli.
2. **Devrilme sesinin sonunda tık var** (21.48 sn). Dosya sessizliğe inmeden kesiliyor. Sona kısa bir fade-out
   (0.2–0.3 sn) koy, ya da 2.1 sn içinde doğal olarak sönen bir ses seç.
3. **Çınlama duyulmuyor.** `Ear_Ring` (sentetik 3.7 kHz ton + hafif gürültü) devrilmenin altında kayboluyor. Gerçek bir
   kulak çınlaması (tinnitus) sesi ya da çarpmadan sonra yükselen daha belirgin bir ton işe yarar.
4. **Bodrum adımları müziğin altında kalıyor.** `Footsteps_Gravel` müziğin bas sesleriyle maskeleniyor. Daha net
   toprak/taş zemin adımları (−25 LUFS civarı) daha iyi olur.
5. **13–18 sn arası tam dijital sessizlik.** Bilerek sessiz ama tamamen sıfır olunca "ses bozuldu" hissi verebilir.
   Çok kısık bir oda tonu (bodrum uğultusu, −45…−50 LUFS, 5 sn) önerilir. Eklersen ayrı bir dosya olarak ver, biz
   13. saniyeye bağlarız.
6. **Gıcırtı hâlâ kapı gibi duyuluyor.** Ağır, tahta bir dolabın yavaşça eğilmesi gibi olmalı: alçak, uzun, gergin bir
   ahşap gıcırtısı (1.7 sn).
7. **Giriş seslerinde kırpılma riski.** Yukarıdaki tabloda true peak'i 0 ve üstü olan 7 dosya (Door_Close,
   Flashlight_Pickup, GearShift, Handbrake, Paper_Grab, Footsteps_AsphaltBoost, Radio_Car) −1 dBTP'ye indirilmeli.

## Kullanılmayan dosyalar

`Assets/Sfx/Ending` içinde `Breath_Nervous`, `Breath_Gasp` ve `Heartbeat_Loop` var ama sinematikte çalmıyor (nefes ve
kalp atışı kaldırıldı). `Breath_Hold` artık giriş sinematiğinde kullanılıyor, silme. `Assets/Sfx/Intro/Dialogue/` içinde `Dlg_01_Richard_ThereYet`,
`Dlg_06_Wayne_WhoDid`, `Dlg_08_Wayne_WhoAskedForMe` ve `Dlg_09_Richard_TakeALook` artık kullanılmıyor (konuşma
yeniden yazıldı). Silmek istersen önce bize haber ver.

## İleride gerekecekler

- Fikir aşamasında: siyah ekrandan sonra kâğıt/örtü hışırtısı, fenerin elden düşme sesi, fenerin titreme/cızırtı sesi.
  Bunlardan hangileri yapılacak henüz belli değil.
- Ses kaynakları: şimdiye kadar Pixabay kullanıldı. Başka bir kaynak kullanırsan lisansını kontrol et.
