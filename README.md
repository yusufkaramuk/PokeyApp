# 👆 PokeyApp

<p align="left">
  <img src="https://img.shields.io/badge/C%23-%23239120.svg?style=for-the-badge&logo=c-sharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/WPF-3C3C44?style=for-the-badge&logo=windows&logoColor=white" alt="WPF" />
</p>

Aynı ağ (LAN/Wi-Fi) üzerindeki iki Windows bilgisayar arasında çalışan, eski MSN Messenger "titreşim/dürtme" mantığını modern ve rahatsız etmeyen bir şekilde sunan açık kaynaklı bir masaüstü uygulamasıdır.

İstediğiniz zaman karşı tarafa tek bir butonla sesli ve görsel bir bildirim gönderebilir, dikkati dağıtmadan haberleşebilirsiniz.

---

## ✨ Öne Çıkan Özellikler

- **⚡ Tek Tuşla Dürtme:** "Dürt!" butonuna tıklayarak karşı bilgisayara anında bildirim gönderin.
- **🛡️ Odak Dostu (Focus-safe):** Gelen bildirim tam ekran oynadığınız bir oyunu veya çalıştığınız bir uygulamayı alta almaz, odak çalmadan çalışır.
- **🔍 Otomatik Keşif (Auto Discovery):** UDP broadcast kullanarak ağdaki diğer bilgisayarı saniyeler içinde otomatik olarak bulur. IP adresi bilmenize gerek yoktur.
- **🔄 Akıllı Yeniden Bağlanma:** Bağlantı koptuğunda (2s → 4s → 8s → 30s) şeklinde katlanarak artan zaman dilimlerinde kendini otomatik toparlar.
- **🎨 Premium Arayüz:** Modern "Dark Mode" barındıran, çerçevesiz ve ekranın sağ altına hizalanan şık tasarım.
- **🔊 Sesli Uyarılar:** Bildirim geldiğinde çalınan özel dürtme sesi (isteğe bağlı kapatılabilir).
- **📂 Arka Plan Çalışması:** System Tray (Sistem Tepsisi) üzerinde sessizce çalışmaya devam eder.

---

## 🚀 Hızlı Kurulum ve Oynatma

Projeyi direkt olarak kullanmak veya geliştirici olarak kendi bilgisayarınıza kurmak için aşağıdaki adımları izleyebilirsiniz.

### 1️⃣ Projeyi İndirin
Projeyi kendi yerel makinenize klonlayın ve klasöre girin:
```bash
git clone https://github.com/yusufkaramuk/PokeyApp.git
cd PokeyApp
```

### 2️⃣ Uygulamayı Derleyin (Build)
Uygulamayı çalıştırılabilir, tek bir "EXE" dosyası haline getirmek için şu komutu çalıştırın:
```powershell
dotnet publish src/PokeyApp/PokeyApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
```

### 3️⃣ Otomatik Kurulum (Yönetici İzniyle)
Projeyi kolayca kurmak, başlat menüsüne eklemek ve Güvenlik Duvarı (Firewall) izinlerini otomatik verdirmek için sağ tıklayıp "PowerShell ile Yönetici olarak Çalıştır" diyerek veya şu komutla kurabilirsiniz:
```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```
*(Kaldırmak isterseniz sonuna `-Uninstall` eklemeniz yeterlidir.)*

---

## 🛠️ Kullanılan Teknolojiler

| Bileşen | Teknoloji | Not |
|---------|-----------|-----|
| **UI Framework** | WPF (.NET 8) | Arayüz için kullanılmıştır (`net8.0-windows`). |
| **Mimari** | MVVM Türü | `CommunityToolkit.Mvvm` aracı ile yönetilmektedir. |
| **Bağlantı Servisi** | TCP & UDP | UDP ile keşif yapıp (Port 14190), TCP ile (Port 14191) haberleşir. |
| **DI / Barındırma**| `Microsoft.Extensions.Hosting` | Servisleri tek bir merkezden ayağa kaldırır. |
| **Loglama Türü** | Serilog | Hataları ve işlem geçmişini `%APPDATA%\PokeyApp\logs\` altına kaydeder. |

---

## ⚙️ Sorun Giderme (Troubleshooting)

- **Bağlantı turuncu (Bağlanıyor) kalıyorsa:** Her iki bilgisayarda da `install.ps1`'i yönetici olarak çalıştırdığınızdan ve ağın (Ortak/Özel) ikisinde de aynı izinlerde olduğundan emin olun.
- **Diğer bilgisayar açılır menüde yoksa:** Ayarlar menüsünden `Manuel IP adresi` kısmını kullanarak direkt karşı bilgisayarın IP'sini girebilirsiniz.
- **Uygulama açılmıyor gibi görünüyorsa:** Uygulama "çerçevesiz" bir yapıda otomatik olarak sağ alt köşeye yerleşmektedir. Görev çubuğunun bildirim tepsisi alanını kontrol edin.

---

## 📄 Lisans
Bu proje, kaynak kodlarını dilediğiniz gibi modifiye etmeniz ve özgürce kullanabilmeniz için **MIT License** ile lisanslanmıştır. Daha fazla bilgi için `LICENSE` dosyasına bakabilirsiniz.
