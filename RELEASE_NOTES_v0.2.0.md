# PokeyApp v0.2.0 🌐 Network Discovery & Integration

Bu sürüm, **Faz 4** ve **Faz 5** numaralı aşamaların entegre edildiği, ağ ortamındaki uygulamanın kullanılabilir hale geldiği bir pre-release aşamasıdır.

### 🎯 Bu Aşamadaki Gelişmeler
- **UDP Network Discovery (Otomatik Keşif):** Bilgisayarlar arası IP bilme zorunluluğunu ortadan kaldıran UDP Broadcast keşif altyapısı aktifleştirildi. İki bilgisayar ağda birbirini 30 saniyelik broadcast'lerle otomatik bulur.
- **Ayarlar Ekranı:** Kullanıcı adı ekleme ve keşfedilen cihazları/peer'ları seçme arayüzü eklendi.
- **UI Entegrasyonu:** Tüm ağ ve servis layer'ı ViewModels (CommunityToolkit.Mvvm) ile frontend tarafına bağlandı.
- **Tray (Sistem Tepsisi):** Bağlantı durumuna göre otomatik yeşil/gri renk değiştiren durum ikonları `App.xaml` düzeyine dahil edildi.
