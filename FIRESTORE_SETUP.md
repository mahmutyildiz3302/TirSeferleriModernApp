# Firestore Kurulum ve Yetki Kontrol Listesi

Bu belge, uygulamanýn Google Cloud Firestore ile çalýþma önkoþullarýný ekipçe kolayca doðrulamanýz için hazýrlanmýþtýr. Aþaðýdaki adýmlarý sýrayla uygulayýn ve her bir adýmý kontrol edin.

## 1) Firestore API etkin mi?
- Google Cloud Console > APIs & Services > Library
- “Cloud Firestore API” aratýn ve “Enable” (Etkinleþtir) butonuna basýn.
- Eðer zaten etkinse “Manage” (Yönet) ekraný görünür.

Yer Tutucu: Ekran Görüntüleri
- [Ekran görüntüsü: API Library arama]
- [Ekran görüntüsü: Cloud Firestore API kartý]
- [Ekran görüntüsü: Enable/Manage butonu]

Not: Bu adým programatik olarak uygulama içinden doðrulanamaz; proje panelinden manuel kontrol gerekir.

## 2) Hizmet hesabý ve yetkiler
- Google Cloud Console > IAM & Admin > Service Accounts
- Kullanacaðýnýz hizmet hesabýný oluþturun veya mevcut birini seçin.
- “Permissions/Roles (Roller)” bölümünde en az þu rol atanmýþ olmalý:
  - Cloud Datastore User (roles/datastore.user)
- Sadece okuma gerekiyorsa: Cloud Datastore Viewer (roles/datastore.viewer)
- Geliþmiþ iþlemler için (ihracat/indeks vb.) ek roller verilebilir, fakat asgari gereksinim “User” rolüdür.

Yer Tutucu: Ekran Görüntüleri
- [Ekran görüntüsü: Service Accounts listesi]
- [Ekran görüntüsü: Role ekleme ekraný]

Not: Roller ve izinler uygulama içinden programatik olarak deðiþtirilemez; GCP/IAM üzerinden yönetilir.

## 3) Hizmet hesabý JSON dosyasý
- Service Accounts > Ýlgili hesap > Keys > Add key > Create new key > JSON
- Ýndirilen JSON’u güvenli bir klasöre koyun (kaynak kontrolüne dahil etmeyin!)
- Uygulamadaki AppSettings.json dosyasýnda GoogleApplicationCredentialsPath alanýna tam dosya yolunu yazýn.

Yer Tutucu: Ekran Görüntüleri
- [Ekran görüntüsü: Key ekleme düðmesi]
- [Ekran görüntüsü: JSON anahtar indirildi]

Not: Uygulama açýlýþýnda bu yolun varlýðý kontrol edilir; dosya yoksa uyarý loglanýr.

## 4) Proje kimliði (FirebaseProjectId) doðrulamasý
- AppSettings.json dosyasýnda FirebaseProjectId alanýndaki deðer, hizmet hesabýnýn ait olduðu Google Cloud projesi ile ayný olmalýdýr.
- Firebase Console > Project Settings > General ekranýndaki “Project ID” deðeri ile eþleþtiðinden emin olun.
- Uygulama Firestore’a baðlanýrken doðrudan bu projectId ile FirestoreDb.CreateAsync(projectId) çaðrýsý yapar.

Yer Tutucu: Ekran Görüntüleri
- [Ekran görüntüsü: Firebase Project Settings > General]
- [Ekran görüntüsü: GCP üst çubuk Project ID]

Not: Uygulama, proje kimliðini AppSettings.json’dan okur ve boþ/yanlýþsa uyarý loglar.

## 5) Uygulama yapýlandýrmasý (özet)
- AppSettings.json:
  - FirebaseProjectId: "my-gcp-project-id"
  - GoogleApplicationCredentialsPath: "C:\\GuvenliKlasor\\service-account.json"
- Uygulama açýlýþýnda:
  - Bu deðerler okunur ve doðrulanýr (boþ/yanlýþsa uyarý logu).
  - Kimlik dosyasý mevcutsa GOOGLE_APPLICATION_CREDENTIALS ortam deðiþkeni ayarlanýr.
  - Senkron ajaný ve Firestore dinleyicisi baþlatýlýr.

## 6) Hýzlý kontrol listesi
- [ ] Cloud Firestore API etkin
- [ ] Hizmet hesabý mevcut ve en az roles/datastore.user rolü atanmýþ
- [ ] Hizmet hesabý JSON dosyasý indirildi ve güvenli bir konumda
- [ ] AppSettings.json’da GoogleApplicationCredentialsPath doðru dosyayý gösteriyor
- [ ] AppSettings.json’daki FirebaseProjectId, GCP/Firebase Project ID ile ayný
- [ ] Uygulama açýlýþ loglarýnda yapýlandýrma uyarýsý yok, Firestore dinleyici ve senkron ajaný baþlýyor

## 7) Sorun giderme ipuçlarý
- Kimlik dosyasý yolu yanlýþsa: App açýlýþýnda uyarý loglanýr. Yolun doðru ve dosyanýn eriþilebilir olduðundan emin olun.
- Yetki hatalarý (permission denied): Hizmet hesabýna roles/datastore.user verildiðini IAM ekranýndan doðrulayýn.
- API devre dýþý: Cloud Firestore API’yi etkinleþtirin ve yeniden deneyin.
- Proje kimliði uyuþmazlýðý: FirebaseProjectId deðeri ile GCP/Firebase Project ID deðerini eþitleyin.

## 8) Ekip içi pratikler
- JSON anahtarlarý depo’ya koymayýn; herkes kendi makinesinde güvenli dizine yerleþtirsin.
- Üretim/test için ayrý hizmet hesabý kullanýn.
- En az ayrýcalýk ilkesi: yalnýzca gereken rolleri atayýn.
