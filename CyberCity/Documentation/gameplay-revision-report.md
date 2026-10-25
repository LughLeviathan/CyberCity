# Laporan revisi gameplay CyberCity

Branch: `feature/gameplay-revision`. Unity: **6000.6.5f1**. Semua perubahan berada pada branch ini; tidak ada push atau merge ke main.

## Ringkasan per fase

| Fase | Hasil | Commit |
| --- | --- | --- |
| 0 | Jumlah misi, retry, batas XP, segmen HUD, journal dan unlock mengikuti campaign. Save v2 menyimpan identitas rute; v1 direset dengan penjelasan. Fallback InvalidDataException diperbaiki. | `612df77` |
| 1 | Username wajib 3–16 karakter, validasi inline, trim, save, rename dari pause, label karakter dan personalisasi `{player}`. Rich text tetap mati. | `72c0240` |
| 2 | Tiga CTF terpisah: hidden-file, log-hunt, permissions. Command tambahan/pipe, tiga flag, hint berbiaya dan pemulihan progres. | `d81444b` |
| 3 | Fake browser uGUI dengan URL editable, riwayat/reload, form, DevTools, pola payload JSON, source patch dan retest request yang sama. Kuis Web Lab dihapus. | `e7f2bbb` |
| 4 | Blue-SOC menggantikan blue-team: alert/noise, traffic/detail, filter, triase, IP berbukti, klasifikasi, respons fiktif dan laporan terstruktur. | `79c1ef0` |
| 5 | Red-team dihapus; final IDOR → SQLi → analisis SOC; graduation dengan nama, XP dan badge area. Penyelesaian misi memeriksa bukti praktik. | `e3af7c9` |

Rute: orientation → awareness → phishing → fake-site → linux-ctf → idor → xss → sqli → blue-soc → final → graduation.

**11 misi, 6 soal pilihan ganda:** orientation 2, awareness 3, graduation 1. Evidence Analyzer diselesaikan lewat inspeksi/pelaporan; terminal, browser, SOC dan final tidak memakai kuis.

## Hasil verifikasi

- Core systems setelah review: **169 checks passed**.
- Complete playthrough setelah review: **273 checks passed, termasuk 169 core checks**, semua 11 misi lewat UI aktual. Mencakup username kosong/valid, rename salah/benar, Continue, gerakan/collision, akses mentor, token/rich text, tiga flag, buka ulang CTF, patch/retest, SOC sampai laporan, save segera, final dan pencegahan XP ganda.
- Maksimum XP dinamis: **1650**. Satu kesalahan sengaja pada orientation menghasilkan **1635 XP**.
- GUID/meta: **370 file, 0 error, 0 warning**.
- SHA-256 campaign: `f71fb3609a411799f004d5c91602a6399c453389c379a48eeb1f6d1f153464c7`. Diverifikasi terhadap teks yang dimuat Unity dan byte UTF-8 lokal.
- Pack lokal 2 misi yang valid diterima. Checksum rusak/schema salah kembali ke bundled offline; file pack pengguna dipulihkan melalui `finally`. Dua warning penolakan fixture memang diharapkan.
- Screenshot CTF, browser sebelum/sesudah patch, SOC dan graduation diperiksa pada Game View **1920×1080**.
- Pemindaian API berisiko pada file C# baru: **0 temuan**. Terminal in-memory; browser hanya mem-parsing URL/pola. Tidak ada fetch, eksekusi JS/SQL, OS command atau WebView. Firewall/reset hanya mengubah status simulasi.
- Bukti kompilasi ada pada [compilation.txt](compilation.txt); hasil playthrough pada [verification.txt](verification.txt).

## Keputusan implementasi

- Save v1 direset dengan pesan karena urutan/kuis lama tidak membuktikan praktik baru. Nama hilang/invalid pada v2 meminta input ulang sambil mempertahankan progres valid. Save atomik `.tmp`/`.bak` tetap dipakai.
- Reward maksimum 150 XP/misi; retry mengurangi 15 XP; hint mengurangi 5 XP; minimum 75 XP. Tiap challenge CTF dan lab browser punya dua hint. Hint yang sudah dibuka tidak dikenakan biaya ulang.
- Patch mengaktifkan source aman yang disiapkan. Tombol retest mengisi dan menjalankan ulang request tertangkap; request lain tidak dapat memverifikasi perbaikan.
- Laporan memakai lima bidang terstruktur dan istilah deterministik sesuai bukti, tanpa layanan AI eksternal.
- Final memakai browser yang sama dan SOC lebih singkat (3 record), dengan satu penerapan rencana respons: blokir sumber, patch, reset kredensial, pertahankan bukti.
- Tidak ada package/dependency baru. Palette/widget tetap dari GameUI. Struktur Citizen tidak perlu diubah; role dan unlock instance NPC diperbarui melalui Builder/scene dan disinkronkan saat runtime.

## File utama

- Runtime diubah: `Campaign.cs`, `CyberCityGame.cs`, `GameUI.cs`, `ModuleUI.cs`, `ProgressStore.cs`, `VirtualTerminal.cs`.
- Runtime baru: `PlayerIdentity.cs`, `TerminalChallenges.cs`, `WebLabSession.cs`, `FakeBrowser.cs`, `SocSession.cs`, `SocUI.cs`, `MissionRequirements.cs`, beserta `.meta` masing-masing.
- Editor: `CyberCityBuilder.cs`, `CyberCityVerification.cs`.
- Konten/scene: `Resources/campaign.json`, `Resources/campaign_hash.txt`, `Scenes/CyberCity.unity`.
- Dokumentasi: rencana/ledger, verifikasi, kompilasi dan screenshot UI.

Perubahan `ProjectSettings/ProjectSettings.asset` sudah ada sebelum pekerjaan dimulai, dipertahankan persis dan tidak masuk commit revisi ini.

## Tindak lanjut review

Enam temuan P2 direproduksi sebelum perbaikan dan ditutup dengan regresi:

1. Save yang mengklaim misi praktik selesai tanpa flag/hasil lab ditolak dan memulihkan backup.
2. Capture browser diuji ulang terhadap scenario aslinya sebelum save diterima, termasuk nilai yang dapat dipulihkan oleh UI browser. Capture rusak tidak dapat membuat lab macet.
3. Hint berbayar tersimpan dapat dibuka ulang gratis lewat REVIEW HINTS, termasuk setelah navigasi atau membuka ulang browser.
4. Filter SOC yang salah atau menyembunyikan paket membatalkan seleksi; klasifikasi memerlukan bukti yang terlihat dan filter valid.
5. Assignment teks dinamis di HUD, patch label, hint dan pesan laporan memakai helper personalisasi.
6. Antrean SOC memakai viewport/scroll; fixture delapan alert membuktikan alert terakhir dapat digulir ke area yang terlihat.

Keputusan lingkup yang dipertahankan: semantik terminal/web tetap dibatasi sebagai simulasi; SHA-256 mendeteksi korupsi dan bukan autentikasi penulis pack; direktori kerja dan perubahan izin terminal yang belum menghasilkan flag diulang saat dibuka kembali, sedangkan flag/hint tetap tersimpan; perubahan ProjectSettings awal tetap di luar commit. Tidak ada temuan minor tertunda selain koreksi pelaporan bahwa jumlah playthrough mencakup pemeriksaan inti.

## Batas verifikasi

Tidak ada fitur wajib yang tertunda. Build player Windows dan pengujian perangkat/gamepad terpisah tidak dilakukan. Verifikasi utama memakai Unity Editor dan UI aktual. Seluruh konten simulasi tetap fiktif dan lokal.

## Daftar file lengkap pada branch

- `CyberCity/Assets/CyberCity/Editor/CyberCityBuilder.cs`
- `CyberCity/Assets/CyberCity/Editor/CyberCityVerification.cs`
- `CyberCity/Assets/CyberCity/Resources/campaign.json`
- `CyberCity/Assets/CyberCity/Resources/campaign_hash.txt`
- `CyberCity/Assets/CyberCity/Scenes/CyberCity.unity`
- `CyberCity/Assets/CyberCity/Scripts/Campaign.cs`
- `CyberCity/Assets/CyberCity/Scripts/CyberCityGame.cs`
- `CyberCity/Assets/CyberCity/Scripts/FakeBrowser.cs`
- `CyberCity/Assets/CyberCity/Scripts/FakeBrowser.cs.meta`
- `CyberCity/Assets/CyberCity/Scripts/GameUI.cs`
- `CyberCity/Assets/CyberCity/Scripts/MissionRequirements.cs`
- `CyberCity/Assets/CyberCity/Scripts/MissionRequirements.cs.meta`
- `CyberCity/Assets/CyberCity/Scripts/ModuleUI.cs`
- `CyberCity/Assets/CyberCity/Scripts/PlayerIdentity.cs`
- `CyberCity/Assets/CyberCity/Scripts/PlayerIdentity.cs.meta`
- `CyberCity/Assets/CyberCity/Scripts/ProgressStore.cs`
- `CyberCity/Assets/CyberCity/Scripts/SocSession.cs`
- `CyberCity/Assets/CyberCity/Scripts/SocSession.cs.meta`
- `CyberCity/Assets/CyberCity/Scripts/SocUI.cs`
- `CyberCity/Assets/CyberCity/Scripts/SocUI.cs.meta`
- `CyberCity/Assets/CyberCity/Scripts/TerminalChallenges.cs`
- `CyberCity/Assets/CyberCity/Scripts/TerminalChallenges.cs.meta`
- `CyberCity/Assets/CyberCity/Scripts/VirtualTerminal.cs`
- `CyberCity/Assets/CyberCity/Scripts/WebLabSession.cs`
- `CyberCity/Assets/CyberCity/Scripts/WebLabSession.cs.meta`
- `CyberCity/Documentation/Screenshots/02-city.png`
- `CyberCity/Documentation/Screenshots/03-fake-site.png`
- `CyberCity/Documentation/Screenshots/03-phishing.png`
- `CyberCity/Documentation/Screenshots/04-terminal.png`
- `CyberCity/Documentation/Screenshots/05-final-idor-patched.png`
- `CyberCity/Documentation/Screenshots/05-final-idor.png`
- `CyberCity/Documentation/Screenshots/05-final-sqli-patched.png`
- `CyberCity/Documentation/Screenshots/05-final-sqli.png`
- `CyberCity/Documentation/Screenshots/05-idor-idor-patched.png`
- `CyberCity/Documentation/Screenshots/05-idor-idor.png`
- `CyberCity/Documentation/Screenshots/05-idor-patched.png`
- `CyberCity/Documentation/Screenshots/05-idor.png`
- `CyberCity/Documentation/Screenshots/05-sqli-patched.png`
- `CyberCity/Documentation/Screenshots/05-sqli-sqli-patched.png`
- `CyberCity/Documentation/Screenshots/05-sqli-sqli.png`
- `CyberCity/Documentation/Screenshots/05-sqli.png`
- `CyberCity/Documentation/Screenshots/05-xss-patched.png`
- `CyberCity/Documentation/Screenshots/05-xss-xss-patched.png`
- `CyberCity/Documentation/Screenshots/05-xss-xss.png`
- `CyberCity/Documentation/Screenshots/05-xss.png`
- `CyberCity/Documentation/Screenshots/06-feedback.png`
- `CyberCity/Documentation/Screenshots/07-completion.png`
- `CyberCity/Documentation/Screenshots/08-soc-report.png`
- `CyberCity/Documentation/Screenshots/08-soc-traffic.png`
- `CyberCity/Documentation/Screenshots/09-graduation.png`
- `CyberCity/Documentation/compilation.txt`
- `CyberCity/Documentation/gameplay-revision-report.md`
- `CyberCity/Documentation/verification.txt`
- `CyberCity/docs/superpowers/plans/2026-10-25-gameplay-revision.md`
