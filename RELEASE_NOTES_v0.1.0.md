# PokeyApp v0.1.0 🏗️ Core Transport & UI Base

Bu sürüm, tasarım (Faz 1) ve iskelet (Faz 2) aşamalarından geçip **Faz 3'ün** (Ağ Katmanı) başarıyla tamamlandığı ilk çalışan yapı taşıdır.

### 🎯 Bu Aşamadaki Gelişmeler
- **Focus Çalmayan Çekirdek UI:** `WS_EX_NOACTIVATE` ile geliştirilen ve kullanıcıya oyun oynarken / iş yaparken bile engel olmayan Native bildirim arayüzü tasarlandı.
- **Ses Altyapısı:** Background thread üzerinde zero-dependency olarak çalışan PCM WAV dürtme (dıt) ses sistemi ayağa kaldırıldı.
- **TCP İskeleti & Framing:** İki bilgisayar arasındaki asıl iletişimin (reliable delivery) geçeceği custom UDP ve TCP alt ağ protokollerinin (length prefiksli) frame'leri eklendi.
- **Reconnect Mekanizması:** Cihaz kapandığında otomatik olarak `2s -> 4s -> 8s -> 30s` şeklinde artan mantıkla kendi kendine yeniden bağlanma servisi yazıldı.
