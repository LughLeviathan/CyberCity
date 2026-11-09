# Peta fullscreen dan HUD minimal

Implementasi dan uji runtime: 27 Oktober 2026. Pemeriksaan artefak dan penyelesaian laporan: 2 November 2026. Branch: `feature/gameplay-revision`.
Tag cadangan `pre-fullscreen-map` menunjuk commit `cc5e714057d79ec39dfe4886f10e80422b88f58b` sebelum perubahan.

## Kamera

Peta tetap **1024×576 px, 32×18 tile, PPU 32**. Resolusi acuan kamera kini sama dengan peta. PixelPerfectCamera memakai **Upscale Render Texture + Stretch Fill + Point**. Dalam URP yang terpasang, Stretch Fill mempertahankan aspek; Point membesarkan langsung tanpa tahap bilinear RetroAA. Setting filter belum memiliki setter publik, sehingga Builder menggunakan field serialized `m_FilterMode` seperti Inspector.

Kamera berpusat tepat di **(16, −9, −10)**. Offset untuk konsol/header sudah dihapus. Spawn, clamp x **0,5–31,5**, y **−17,5–−0,5**, footprint, air, pagar, serta boundary tetap mengikuti `WorldLayout` dan tidak berubah ukuran.

| Game View | Area peta | Skala piksel | Sisa layar |
|---|---|---:|---|
| 1920×1080 | 1920×1080 | 1,875× | Tidak ada |
| 1366×768 | sekitar 1365,33×768 | 1,3333× | Pembulatan viewport kurang dari 1 px |
| 2560×1440 | 2560×1440 | 2,5× | Tidak ada |
| 1280×720 | 1280×720 | 1,25× | Tidak ada |
| 1920×1200 | 1920×1080 | 1,875× | 60 px atas/bawah |
| 2560×1080 | 1920×1080 | 1,875× | 320 px kiri/kanan |
| 1440×1080 | 1440×810 | 1,40625× | 135 px atas/bawah |

URP membersihkan crop bar dengan hitam. Empat rectangle UI non-interaktif memberi warna `GameUI.Ink` hanya pada area sisa, mengikuti viewport kamera. Canvas memakai Expand agar desain modal 1600×900 muat pada rasio yang lebih lebar maupun lebih tinggi; panel modal dipusatkan dan scrim memenuhi layar.

Skala non-integer dengan Point tetap tajam tetapi lebar satu piksel sumber bergantian antara jumlah piksel layar bulat, misalnya 1–2 px pada 1,875×. Ini konsekuensi nearest-neighbor; tidak ada blur atau distorsi geometris peta.

## HUD

Mission console beserta objective, jumlah/progress, daftar distrik dan tombol M tidak lagi dibuat. XP dan nama pemain diperbarui langsung tanpa dependensi pada `count`. Kiri atas hanya judul kecil dan XP. Kanan atas berisi Journal serta ESC Menu dengan latar tombol transparan; tidak ada panel header/footer solid.

`GameUI.QuestButtonSlot` adalah properti publik `RectTransform`. Sesudah `GameUI.Initialize`, hapus/ganti child Journal dan pasang tombol kustom sebagai child slot. Slot dibuat ulang ketika interface diinisialisasi ulang; integrasi tombol kustom perlu dilakukan kembali setelah inisialisasi itu. Journal masih bisa dibuka dengan J. M, H, ESC dan E tetap memakai jalur input/gameplay yang sama.

Nama NPC/pemain dan penanda emas tetap memakai `WorldToScreenPoint`, lalu dikonversi ke Canvas. Pembaruan label dipindah ke LateUpdate setelah posisi kamera. Prompt E berada pada strip waterfront di tengah bawah, 48 unit UI dari bawah, terpisah dari footer 12 unit; posisi ini menghindari sprite pemain/NPC, globe dan bangunan. Toast berada di tengah atas. Objective tetap tersedia melalui Journal dan Map. Latar Welcome juga memenuhi seluruh layar, dengan konten tetap 1600×900 yang dipusatkan.

## Verifikasi dan bukti

Tes awal menolak scene lama karena reference kamera masih memiliki padding konsol. Setelah rebuild, kontrak scene, importer, dan **169 pemeriksaan core lulus**. Run akhir **Verify complete playthrough lulus 350 pemeriksaan** (termasuk 169 core), seluruh 11 misi, 48 kombinasi akses area, reachability, E, J/M/H/ESC, animasi unlock, XP/nama, framing 7 resolusi, posisi capture yang bebas collider, dan cakupan Welcome. Bukti `verification.txt`: **2026-10-27 11:54:15 UTC**. Pengaturan input kembali ke `PointersAndKeyboardsRespectGameViewFocus` / `ResetAndDisableNonBackgroundDevices`, keyboard verifikasi sudah dihapus, dan Unity keluar dari Play mode. Dua warning optional-pack berasal dari pengujian penolakan paket yang sengaja rusak.

Matriks screenshot: `Documentation/Screenshots/fullscreen-<lebar>x<tinggi>-{start,interact,journal,open}.png` untuk tujuh resolusi pada tabel. `start` memakai progres baru; `open` diambil setelah orientation selesai lewat UI dan Internet Cafe terbuka. Screenshot tambahan `fullscreen-world-*`, terminal, fake browser, SOC, graduation, dan akhir kampanye mendokumentasikan alur lengkap. Capture dan perubahan modal berada di langkah berbeda agar gambar mewakili keadaan yang dinamai.

Tersedia **70 PNG**, termasuk **28 gambar matriks**. Semua berasal dari run akhir. Pemeriksaan berkas memastikan ukuran yang benar, bar gelap pada rasio lain, serta gambar interaksi/peta tidak tertukar dengan Journal; hash tersedia di `fullscreen-screenshot-validation.json`. Hasil utama:

- [Peta awal 1920×1080](Screenshots/fullscreen-1920x1080-start.png)
- [NPC, marker dan prompt E](Screenshots/fullscreen-1920x1080-interact.png)
- [Journal](Screenshots/fullscreen-1920x1080-journal.png)
- [Internet Cafe terbuka dan toast](Screenshots/fullscreen-1920x1080-open.png)
- [16:10 / 1920×1200](Screenshots/fullscreen-1920x1200-start.png)
- [Ultrawide / 2560×1080](Screenshots/fullscreen-2560x1080-interact.png)
- [4:3 / 1440×1080](Screenshots/fullscreen-1440x1080-start.png)

## File perubahan

- `Assets/CyberCity/Scripts/WorldLayout.cs`, `WorldCamera.cs`: acuan dan pemusatan kamera.
- `Assets/CyberCity/Scripts/GameUI.cs`, `CyberCityGame.cs`: overlay, slot quest, label, crop bar, layout modal.
- `Assets/CyberCity/Editor/CyberCityBuilder.cs`: setting PixelPerfectCamera yang dapat dibangun ulang.
- `Assets/CyberCity/Editor/CyberCityWorldVerification.cs`, `CyberCityFullscreenVerification.cs`, `CyberCityVerification.cs`: kontrak framing/UI, matriks resolusi, playthrough dan capture.
- `Assets/CyberCity/Scenes/CyberCity.unity`, `Generated/Citizen.controller`: hasil Builder (ID serialization dapat berubah).
- Laporan ini, `fullscreen-map-plan.md`, `fullscreen-map-review.md`, `fullscreen-screenshot-validation.json`, `verification.txt`, serta PNG `fullscreen-*`.

Tidak ada package baru, perubahan source art, akses jaringan game, push, atau perubahan ukuran peta. `ProjectSettings/ProjectSettings.asset` tetap perubahan milik pengguna dan tidak ikut commit; hash sebelum pekerjaan **4A2747F932499276B052F7718F2771732D7242E0A7C83B94EC836E7B5459D713**.

## Batas verifikasi

Bukti berasal dari Game View Unity 6000.6.5f1, bukan executable standalone. Posisi label diperiksa terhadap koordinat peta independen dengan toleransi 2 px; framing toleransi 1,1 px untuk pembulatan viewport. Tombol quest kustom pengguna belum disediakan; placeholder Journal sudah berfungsi. Seluruh pekerjaan yang diminta selesai; tidak ada temuan review yang ditunda.
