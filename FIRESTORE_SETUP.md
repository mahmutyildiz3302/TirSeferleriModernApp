# Firestore Kurulum ve Yetki Kontrol Listesi

Bu belge, uygulaman�n Google Cloud Firestore ile �al��ma �nko�ullar�n� ekip�e kolayca do�rulaman�z i�in haz�rlanm��t�r. A�a��daki ad�mlar� s�rayla uygulay�n ve her bir ad�m� kontrol edin.

## 1) Firestore API etkin mi?
- Google Cloud Console > APIs & Services > Library
- �Cloud Firestore API� arat�n ve �Enable� (Etkinle�tir) butonuna bas�n.
- E�er zaten etkinse �Manage� (Y�net) ekran� g�r�n�r.

Yer Tutucu: Ekran G�r�nt�leri
- [Ekran g�r�nt�s�: API Library arama]
- [Ekran g�r�nt�s�: Cloud Firestore API kart�]
- [Ekran g�r�nt�s�: Enable/Manage butonu]

Not: Bu ad�m programatik olarak uygulama i�inden do�rulanamaz; proje panelinden manuel kontrol gerekir.

## 2) Hizmet hesab� ve yetkiler
- Google Cloud Console > IAM & Admin > Service Accounts
- Kullanaca��n�z hizmet hesab�n� olu�turun veya mevcut birini se�in.
- �Permissions/Roles (Roller)� b�l�m�nde en az �u rol atanm�� olmal�:
  - Cloud Datastore User (roles/datastore.user)
- Sadece okuma gerekiyorsa: Cloud Datastore Viewer (roles/datastore.viewer)
- Geli�mi� i�lemler i�in (ihracat/indeks vb.) ek roller verilebilir, fakat asgari gereksinim �User� rol�d�r.

Yer Tutucu: Ekran G�r�nt�leri
- [Ekran g�r�nt�s�: Service Accounts listesi]
- [Ekran g�r�nt�s�: Role ekleme ekran�]

Not: Roller ve izinler uygulama i�inden programatik olarak de�i�tirilemez; GCP/IAM �zerinden y�netilir.

## 3) Hizmet hesab� JSON dosyas�
- Service Accounts > �lgili hesap > Keys > Add key > Create new key > JSON
- �ndirilen JSON�u g�venli bir klas�re koyun (kaynak kontrol�ne dahil etmeyin!)
- Uygulamadaki AppSettings.json dosyas�nda GoogleApplicationCredentialsPath alan�na tam dosya yolunu yaz�n.

Yer Tutucu: Ekran G�r�nt�leri
- [Ekran g�r�nt�s�: Key ekleme d��mesi]
- [Ekran g�r�nt�s�: JSON anahtar indirildi]

Not: Uygulama a��l���nda bu yolun varl��� kontrol edilir; dosya yoksa uyar� loglan�r.

## 4) Proje kimli�i (FirebaseProjectId) do�rulamas�
- AppSettings.json dosyas�nda FirebaseProjectId alan�ndaki de�er, hizmet hesab�n�n ait oldu�u Google Cloud projesi ile ayn� olmal�d�r.
- Firebase Console > Project Settings > General ekran�ndaki �Project ID� de�eri ile e�le�ti�inden emin olun.
- Uygulama Firestore�a ba�lan�rken do�rudan bu projectId ile FirestoreDb.CreateAsync(projectId) �a�r�s� yapar.

Yer Tutucu: Ekran G�r�nt�leri
- [Ekran g�r�nt�s�: Firebase Project Settings > General]
- [Ekran g�r�nt�s�: GCP �st �ubuk Project ID]

Not: Uygulama, proje kimli�ini AppSettings.json�dan okur ve bo�/yanl��sa uyar� loglar.

## 5) Uygulama yap�land�rmas� (�zet)
- AppSettings.json:
  - FirebaseProjectId: "my-gcp-project-id"
  - GoogleApplicationCredentialsPath: "C:\\GuvenliKlasor\\service-account.json"
- Uygulama a��l���nda:
  - Bu de�erler okunur ve do�rulan�r (bo�/yanl��sa uyar� logu).
  - Kimlik dosyas� mevcutsa GOOGLE_APPLICATION_CREDENTIALS ortam de�i�keni ayarlan�r.
  - Senkron ajan� ve Firestore dinleyicisi ba�lat�l�r.

## 6) H�zl� kontrol listesi
- [ ] Cloud Firestore API etkin
- [ ] Hizmet hesab� mevcut ve en az roles/datastore.user rol� atanm��
- [ ] Hizmet hesab� JSON dosyas� indirildi ve g�venli bir konumda
- [ ] AppSettings.json�da GoogleApplicationCredentialsPath do�ru dosyay� g�steriyor
- [ ] AppSettings.json�daki FirebaseProjectId, GCP/Firebase Project ID ile ayn�
- [ ] Uygulama a��l�� loglar�nda yap�land�rma uyar�s� yok, Firestore dinleyici ve senkron ajan� ba�l�yor

## 7) Sorun giderme ipu�lar�
- Kimlik dosyas� yolu yanl��sa: App a��l���nda uyar� loglan�r. Yolun do�ru ve dosyan�n eri�ilebilir oldu�undan emin olun.
- Yetki hatalar� (permission denied): Hizmet hesab�na roles/datastore.user verildi�ini IAM ekran�ndan do�rulay�n.
- API devre d���: Cloud Firestore API�yi etkinle�tirin ve yeniden deneyin.
- Proje kimli�i uyu�mazl���: FirebaseProjectId de�eri ile GCP/Firebase Project ID de�erini e�itleyin.

## 8) Ekip i�i pratikler
- JSON anahtarlar� depo�ya koymay�n; herkes kendi makinesinde g�venli dizine yerle�tirsin.
- �retim/test i�in ayr� hizmet hesab� kullan�n.
- En az ayr�cal�k ilkesi: yaln�zca gereken rolleri atay�n.
