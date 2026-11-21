# Level tasarımı notları (giriş sinematiğinden)

Giriş sinematiği üzerinde çalışırken level tarafında gördüğümüz şeyler. Sinematik `Tools > Intro Cinematic > Build`
(`Assets/Scripts/Editor/IntroCinematicBuilder.cs`) ile kuruluyor. Aşağıdakiler level'a dokunduğu için sana bırakıldı.

## Önce dikkat: terrain'de yapılmış bir değişiklik

`Assets/New Terrain.asset` içinden **76 ağaç kaldırıldı** (15.405 → 15.329). Sinematikte araba dururken kamera eve
bakıyor ve ev ağaçların arkasında kalıyordu.

- Koridor: ana yolda x ≈ 41–65, z ≈ -5 civarından MainHouse_V2'nin ön cephesine (x ≈ -135…-71, z ≈ 181) uzanan dar bir şerit.
- Terrain'i sen de düzenliyorsan, aynı dosyada çakışma çıkabilir. Commit'leri birleştirirken buna dikkat et.
- Oyuncu yürürken bu koridor ormanda düz bir boşluk gibi görünebilir. Kenarlarına alçak çalı ya da küçük ağaç
  eklemek iyi olabilir. Yalnızca evin çatısı ve ışıklı pencereleri yoldan görünsün yeter.
- Koridor kapanırsa sinematikteki eve bakış yine boşa düşer. Değiştirirsen haber ver, kadrajı yeniden ayarlarız.

## Ana yol çok geniş

`yol` objesi yaklaşık 70 m genişliğinde tek bir düz kalıp (x -295…804, z -66…4.3, üst yüzey y 11.1).
Sürüş boyunca sol camdan ufka kadar dümdüz, karanlık bir zemin görünüyor.

- Öneri: yolu iki şerit genişliğine daraltmak (sinematikte bizim şerit z ≈ -6.7, karşıdan gelen araç z ≈ -16),
  ötesini tarla, çimen ya da toprak yapmak.
- Yolun malzemesi `Black` ve rengi **tam siyah (0, 0, 0)**. Tam siyah yüzey hiç ışık yansıtmaz: sinematikteki farlar,
  sokak lambaları ve oyuncunun feneri yolda hiçbir şey göstermiyor. Koyu gri bir asfalt (ör. 0.12–0.15) ve hafif
  pürüzlü bir doku, farların yolda ışık havuzu yapmasını sağlar.
- Yol kalıbının üst yüzeyi (y 11.1) çevresindeki araziden (y ≈ 10) **1.1 m yüksek**. Yoldan toprağa çıkarken
  bir basamak kadar düşülüyor; toprak yolun ağzı (≈ x 5, z 4–10) da bu yüzden asfalta düz bağlanmıyor.
  Yol araziyle aynı seviyeye inerse sinematiğin son yürüyüşü toprak yola kadar uzatılabilir (şu an asfaltta bitiyor).
- Yolun yerini veya genişliğini değiştirirsen haber ver. Sinematikteki araba rotası, geçen araçlar ve yol kenarı
  objeleri bu koordinatlara göre kurulu.

## Sinematiğin level'a koyduğu objeler

Bunlar builder tarafından her Build'de silinip yeniden kuruluyor. Elle düzenlenirse bir sonraki Build'de kaybolur.
Taşımak istersen bize söyle, builder'da değiştirelim.

- `IC_Roadside`: reflektör direkleri (yolun iki kenarı, x 62–384), posta kutusu (x ≈ 268), "SAINT PABLO 3 / MILLER RD 1"
  tabelası (x ≈ 322), tahta çit (x 150–214, z ≈ 6.4). "Miller Rd" uydurma bir isim, istersen değiştiririz.
- `IntroCinematic` altındaki araba, eller ve trafik araçları.

## Sokak lambaları

`StreetLamps` altında iki lamba var; `Tools` menüsündeki `StreetLampBuilder` ile kuruldular (Blender'da yapılmış eski,
yıpranmış köy lambası modeli).

- `StreetLamp_Kavsak` (16, 10.9, -23): toprak yolun ağzının karşısında, ana yolun uzak kenarında.
- `StreetLamp_ToprakYol` (18, 9.8, 56): toprak yol boyunca, yolun nereye gittiğini gösteriyor.
- Her lambanın uzaktan görünen bir parlaması (`Glare`) var. Bu parlama artık uzakta en fazla 2.4 m büyüyor ve
  300 m'de sönükleşiyor; önceden sürüşte uzaktan güneş gibi büyük bir top olarak görünüyordu.
- Lambanın yerini değiştirirsen sinematiğin son karesi kayar (bkz. aşağıdaki noktalar).

## Evin ışıkları sinematiğin sonunda sönüyor

`MainHouse_V2/HouseLights` (iki pencere parlaması `Glow_Cam_S_14/26`, iki iç ışık ve iki gaz lambası ışığı) sinematiğin
son saniyesinde kapatılıyor ve **oyunda da kapalı kalıyor**. Sinematik bilerek böyle bitiyor: oyuncu kontrolü
"ışıkları kim söndürdü?" sorusuyla alıyor.

- Oyuncu eve girdiğinde içerisi bu ışıklar olmadan karanlık. Evin içini aydınlatmak istersen başka bir obje kullan
  ya da bize söyle.
- Gaz lambalarının alev modelleri (`vintage_oil_lamp_flame`) hâlâ parlıyor ama ışık yaymıyor. Oyunda tuhaf görünürse
  söndürülebilir.
- `HouseLights` objesinin adını veya yerini değiştirirsen sinematik onu bulamaz (builder adıyla arıyor).

## Ortam sesleri

- `NightAmbience/Wind`, `NightAmbience/Crickets` ve `Field/AmbienceZone_Tarla` objelerine ikinci bir AudioSource
  eklendi (sesler iki kaynak arasında çapraz geçişle dönüyor). Oyun başında çıkan "Only custom filters can be played"
  uyarıları bu yüzden gitti. Bu ikinci kaynakları silme.
- `Ambience_Wind`, `Ambience_Crickets` ve `Ambience_Ravens` seslerinde "Preload Audio Data" açıldı.

## Sinematiğin dayandığı level noktaları

Bunların yeri değişirse sinematik kayar:

- Arabanın park yeri: (32, 11.1, -1)
- Toprak yolun ana yoldan ayrıldığı yer: (5, 11.1, 10); toprak yol kuzeye gidiyor
- `StreetLamp_ToprakYol` (18, 9.8, 56): sinematiğin son karesi bu lambaya ve toprak yola bakıyor
- `StreetLamp_Kavsak` (16, 10.9, -23): sürüşte ileride görünen varış ışığı
- `MainHouse_V2`: merkez yaklaşık (-103, 20, 220); sinematikte araba dururken ve son karede kadrajda
- Oyuncu sinematik bitince (21.4, 12.6, 2.0) noktasında, kuzeye (toprak yola) bakarak başlıyor
