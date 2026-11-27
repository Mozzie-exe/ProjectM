# Level tasarımı notları (giriş ve oyun sonu sinematikleri)

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
  tabelası, tahta çit (x 150–214, z ≈ 6.4). "Miller Rd" uydurma bir isim, istersen değiştiririz.
- Tabela (`Sign_SaintPablo`) sahnede elle (301, 7.06) noktasına taşınıp 2.15 kat büyütülmüş. Build artık tabelanın
  sahnedeki yerini, dönüşünü ve ölçeğini koruyor; tabelayı elle taşıyabilirsin. Yazısı artık arabanın içinden
  görünmüyor, arkası koyu metal (önceden arkadan bakınca parlayan beyaz bir dikdörtgen görünüyordu).
- `IntroCinematic` altındaki araba, eller ve trafik araçları.

## Sokak lambaları

`StreetLamps` altında iki lamba var; `Tools` menüsündeki `StreetLampBuilder` ile kuruldular (Blender'da yapılmış eski,
yıpranmış köy lambası modeli).

- `StreetLamp_Kavsak` (16, 10.9, -23): toprak yolun ağzının karşısında, ana yolun uzak kenarında.
- `StreetLamp_ToprakYol` (18, 9.8, 56): toprak yol boyunca, yolun nereye gittiğini gösteriyor.
- Her lambanın uzaktan görünen bir parlaması (`Glare`) var. Bu parlama artık uzakta en fazla 2.4 m büyüyor ve
  300 m'de sönükleşiyor; önceden sürüşte uzaktan güneş gibi büyük bir top olarak görünüyordu.
- Lambanın yerini değiştirirsen sinematiğin son karesi kayar (bkz. aşağıdaki noktalar).

## Tünel (yeni)

Yol x 540–622 arasında, kuzeydeki terrain tepesiyle güneydeki `Yol_Guney_Arazi` tepesinin arasındaki yarmadan
geçiyordu; yamaçlar yolun 7–8 m üstünde kalıyordu. Buraya bir tünel eklendi: `Tunel` objesi, `Tools > Level > Build Tunnel`
(`Assets/Scripts/Editor/TunnelBuilder.cs`) ile kuruluyor.

- **Terrain'e ve `Yol_Guney_Arazi`'ye dokunulmadı.** Tünel ayrı bir obje; iki yamacın yüksekliğini okuyup üstünü
  toprak bir örtüyle birleştiriyor. Yamaçları değiştirirsen Build'i yeniden çalıştır, örtü yeni yüksekliklere oturur.
- Ölçüler: duvarlar z −21.5 ve +1.0'da (yolun iki yanında), dikey duvar 3.5 m, kemerle birlikte tavan yoldan 6 m
  yukarıda (y 17.1). Ağızlarda 0.6 m kalınlığında beton duvar var.
- Malzemeler: `Tunel_Beton` (iç yüz ve ağızlar), örtüde `Toprak_YolKenari`, tavanda 12 m arayla küçük sodyum lambaları
  (`Tunel_Lamba`, sadece parlıyor, ışık kaynağı yok).
- MeshCollider var, obje static.
- Giriş sinematiğinde araba tünelden 15–22. saniyeler arasında, siyah ekranın açıldığı sırada geçiyor; içerisi
  farlarla aydınlanıyor. Tüneli kaydırırsan bu görüntü de değişir.
- Diğer tepe (x −275…−150) için tünel yapılmadı; sinematik oradan geçmiyor.

## Ağaçlar ve FPS (değişti)

FPS düşüklüğünün ana sebebi terrain ağaçlarıydı: 25 425 ağaç var ve kamera her karede 500 m'ye kadar (far clip)
~15 bin ağaç çiziyordu, karede 26 milyon üçgen. Ağaç prefab'larında LODGroup olduğu için terrain'in **Tree Distance
ayarı bu ağaçlarda işe yaramıyor**, ağaçları sadece LODGroup'un son eşiği kesiyor; o eşik de çok düşüktü.

- `Tree1/2/3.prefab` LOD eşikleri değişti, ağaçlar artık ~220 m'de kayboluyor (geceden ve sisten dolayı bu mesafe
  ekranda seçilmiyor). Eşikler (LOD0/1/2/cull): Tree1 0.346/0.225/0.138/0.086, Tree2 0.283/0.184/0.113/0.071,
  Tree3 0.590/0.383/0.236/0.147. Uzaklık eşiği her ağacın boyuna göre hesaplandı, Quality'deki LOD Bias 2'ye göre.
- LOD2 ve LOD3 gölge düşürmüyor (LOD0/1 düşürüyor).
- Sonuç (Play, editör içinde): 26.2M → 16.2M üçgen, ana thread 14.9 → 8.7 ms, GPU ~4 ms.
- Ağaçlar daha uzakta görünsün istersen son eşiği küçült, ama her 100 m binlerce ağaç ve milyonlarca üçgen ekliyor.
- Terrain'in Tree Distance'ı (5000) olduğu gibi bırakıldı.
- Sokak lambası gölgelerini kapatmak ölçülebilir kazanç vermedi, dokunulmadı. Ev içindeki ağır proplar (mumluk 213k,
  saksı bitkisi 107k, karton kutular 17k×37 üçgen...) GPU'ya ~7.5M üçgen yüklüyor ama GPU darboğaz değil; şimdilik
  dokunulmadı.
- **Static / occlusion culling senin işin**, bu FPS işinde hiçbir şey static yapılmadı, bake edilmedi.

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

- Arabanın park yeri: (32, 11.1, 2), yolun kuzey kenarına yanaşık (sağ yanı kenardan ~0.3 m içeride)
- Toprak yolun ana yoldan ayrıldığı yer: (5, 11.1, 10); toprak yol kuzeye gidiyor
- `StreetLamp_ToprakYol` (18, 9.8, 56): sinematiğin son karesi bu lambaya ve toprak yola bakıyor
- `StreetLamp_Kavsak` (16, 10.9, -23): sürüşte ileride görünen varış ışığı
- `MainHouse_V2`: merkez yaklaşık (-103, 20, 220); sinematikte araba dururken ve son karede kadrajda
- Oyuncu sinematik bitince yaklaşık (21.1, 12.6, 4.5) noktasında, asfaltın hemen kenarında, kuzeye (toprak yola)
  bakarak başlıyor

## Oyun sonu sinematiği (bodrum, taş oda)

Sinematik `Tools > End Cinematic > Build` (`Assets/Scripts/Editor/EndCinematicBuilder.cs`) ile kuruluyor. Akış:
`Kapi_TasOda` ilk açılınca kısa bir bakış, ceset odası müziği başlıyor, koşu kapanıyor. Örtülü
cesede E ile 21.5 sn'lik sinematik oynuyor ve oyun dolap oyuncunun üstüne devrilirken siyah ekranla bitiyor.

- Builder şu objeleri **adıyla** buluyor: `Kapi_TasOda` (Door bileşeni), `Bodrum_OrtuluSekil`, `Bodrum_Ortu`,
  `Bodrum_Yeni/Bodrum_DevrilenDolap`, `BodrumOda_TopraKZemin` (zemin yüksekliği) ve `Bodrum_Blood_Drips` (duvardaki
  kana bakılıyor). Adlarını değiştirirsen sinematik kurulamaz. Not: `MainHouse_V2` altında kapalı duran ikinci bir
  `Bodrum_DevrilenDolap` var; kullanılan `Bodrum_Yeni` altındaki.
- Kameranın yolu, eller ve dolabın devrilme yönü bu objelerin sınırlarından hesaplanıyor. Ceseti, örtüyü veya dolabı
  taşırsan Build'i yeniden çalıştırman yeterli; yine de aşağıdaki boşlukları koru.
- `Bodrum_Ortu` objesine bir **BoxCollider** ve **CorpseInspect** bileşeni eklendi (E etkileşimi). Bunları silme.
- Dolap gıcırtıyla birlikte (18 sn) yavaşça 5° öne eğiliyor, son 0.8 saniyede devriliyor; bu dönüş oyunda
  yapılıyor (sahnede yeri değişmiyor). Alt kenarı
  (-87.4, 1.55, 190.25) çevresinde, **kuzeybatıya, cesedin baş ucuna doğru** 35° devriliyor. Dolabın önü ve cesetle
  arası boş kalmalı.
- Kameranın yürüdüğü yol örtünün **güney yanı**: çizmelerin ~2 m batısından (-93, 192.4) başlıyor, örtünün
  0.4–0.8 m güneyinden geçiyor ve baş ucunda (-87.9, 191.3) çömeliyor. Bu şeride eşya koyma; kamera içinden geçer.
- Odada ışık yok. Sinematikte fenerin yanında, sinematiğe ait sönük, soğuk bir spot ışık var (`EndCinematic/BackLight`,
  dolabın arkasında tavana yakın, ~(-86.7, 8.9, 188.9)). Yalnızca sinematik sırasında yanıyor; dolap eğilince gölgesi
  örtünün baş ucuna ve duvara kayıyor. Dolabın üstüyle tavan arası boş kalmalı, yoksa ışık kapanır.
  Odaya ayrıca ışık eklersen sinematiğin havası değişir.
- Canvas'a `EndBlack` adında tam ekran siyah bir görüntü eklendi (başta kapalı).
