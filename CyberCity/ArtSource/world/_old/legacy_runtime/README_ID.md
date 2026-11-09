# CyberCity World — paket pixel art RPG untuk Unity

Dibuat dengan Aseprite MCP berdasarkan suasana dan delapan warna pada referensi pengguna. Perspektif top-down ¾, grid dasar 32×32 piksel, tanpa anti-alias pada artwork. Aset statis; karakter, animasi, sistem quest, dan logika portal tidak termasuk.

## Pakai di Unity

1. Gunakan project 2D. Pastikan package resmi **2D Sprite** dan **2D Tilemap Editor** tersedia melalui Package Manager.
2. Impor file `CyberCityWorld.unitypackage` yang disertakan terpisah melalui **Assets > Import Package > Custom Package**. Alternatif: salin folder `Assets/CyberCityWorld` dari paket ZIP ke folder Assets project, beserta file `.meta`.
3. Buka `Assets/CyberCityWorld/Scenes/CyberCityLobby.unity` untuk melihat contoh lobby. Prefab tersedia di `Generated/CyberCityLobby.prefab`.
4. Untuk menyusun map sendiri, buat Tile Palette lalu tarik Tile asset dari `Generated/Tiles`. Props dan bangunan sudah dipotong serta diberi nama pada masing-masing texture.
5. Jika texture atau katalog diedit, jalankan **Tools > CyberCity > 1. Import and build assets**. Menu kedua menambahkan lobby ke scene aktif.

Importer mengatur **Point filtering**, **32 Pixels Per Unit**, **No Compression**, **Clamp**, **No Mipmaps**, dan **Full Rect**. Pivot props berada pada titik pijakan, sehingga penempatan dan urutan kedalaman konsisten. Gunakan skala kamera integer / Pixel Perfect Camera untuk menjaga piksel tetap tajam.

## Isi

| File | Isi / ukuran |
|---|---|
| `Textures/terrain.png` | 96 tile, 12 kolom × 8 baris; 384×256 px; slice 32×32 |
| `Textures/props.png` | 24 dekorasi transparan, 8 × 3 sel; 512×192 px; slice 64×64 |
| `Textures/buildings.png` | 6 bangunan transparan, 3 × 2 sel; 384×256 px; slice 128×128 |
| `Scenes/CyberCityLobby.unity` | Scene demo dengan 616 sel ground dan 70 objek |
| `Generated/` | Tile asset, prefab, dan collision air |
| `Data/catalog.json` | Nama sprite, rectangle, pivot, warna, dan saran collision |
| `Data/demo_map.json` | Susunan map 28×22 tile, koordinat objek dalam piksel |
| `Source/` | 4 file `.aseprite` editable; atlas memiliki named slices; demo memiliki layer Ground dan Objects |
| `Previews/` | Contoh lobby 896×704 px dan ekspor layer terpisah |
| `palette/cybercity.gpl` | Palet 8 warna untuk editor |
| `tile_catalog.csv` | Indeks 0–95 dan koordinat masing-masing tile |

Path `Textures`, `Scenes`, `Generated`, dan `Data` pada tabel berada di `Assets/CyberCityWorld`.

Terrain: rumput, tanah, air, paving, aspal, marka jalan, zebra cross, tepi neon, kanal, jembatan, tangga, drainase, panel metal, grating, kabel, hazard stripe, bunga, batu, dan detail plaza. Empat material pertama memiliki susunan tepi 3×3 untuk penempatan manual. Paket ini tidak mencakup RuleTile otomatis lengkap atau semua kombinasi sudut cekung.

Dekorasi: dua pohon, semak, bunga, lampu amber, bangku, pagar dua arah, bollard, peti, terminal, vending machine, papan akademi/cafe, obelisk, fountain hologram, portal, barrel, hatch, recycle bin, batu, palm planter, server, dan penunjuk arah.

Bangunan: Cyber Academy, Player House, Command Lab, Internet Cafe, Security Lab, dan Cyber Corp. Bentuk disederhanakan menjadi pixel art modular dari konsep referensi.

## Palet

| Warna | Hex |
|---|---|
| Deep Navy | `#0B1026` |
| Indigo | `#1F2A68` |
| Electric Cyan | `#00E5FF` |
| Neon Teal | `#00D4B7` |
| Violet | `#8B5CF6` |
| Magenta | `#FF2D9B` |
| Amber | `#FFB347` |
| Soft Cream | `#FFF4D6` |

Transparansi merupakan alpha 0; semua piksel terlihat memakai alpha 255 dan salah satu dari delapan warna di atas. Glow visual dibuat dengan warna dan piksel tegas; tidak membutuhkan bloom atau shader khusus.

## Koordinat dan collision

JSON memakai origin kiri atas: x ke kanan, y ke bawah. Unity memakai y ke atas; importer mengonversinya. Rectangle sprite di JSON juga memakai koordinat kiri atas. Props memakai pivot `[32,56]` dalam sel 64×64; bangunan `[64,120]` dalam sel 128×128. Semua nilai pivot dihitung dari kiri atas sel.

Collision air menggunakan TilemapCollider2D terpisah. Props dan bangunan memiliki BoxCollider2D sederhana pada bagian pijakan. Bentuknya titik awal untuk desain; sesuaikan dengan ukuran karakter dan akses pintu/portal. Objek statis memakai sorting order berdasarkan koordinat pijakan y. Jika menambahkan karakter bergerak, gunakan aturan sorting yang sama: `sortingOrder = Mathf.RoundToInt(-transform.position.y * 32f)` pada sorting layer yang sama.

## Verifikasi

- Berhasil diimpor dan script terkompilasi dengan **Unity 6000.6.5f1**.
- Dibuat melalui Unity: 96 Tile asset, prefab lobby, scene demo, dan `.unitypackage`.
- Aseprite MCP memvalidasi ukuran grid, palet, dan background transparan.
- Pemeriksaan PNG memastikan tepat 8 warna terlihat, alpha biner, batas sprite valid, dan hasil gabungan Ground + Objects identik dengan preview.
- Hasil pemeriksaan terperinci tersedia pada `validation.json` dan `unity-verification.json`.

Importer menggunakan API resmi [Sprite Editor Data Provider Unity](https://docs.unity.cn/Packages/com.unity.2d.sprite@1.0/manual/DataProvider.html). Versi Unity lain belum diuji langsung.
