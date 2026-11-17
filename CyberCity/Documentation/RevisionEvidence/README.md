# CyberCity — NPC interiors & Virus Sweep

## Perubahan
- Checksum bundled campaign menormalkan CRLF ke LF sebelum SHA-256; konten yang berubah tetap ditolak. Hash optional scenario pack tetap byte-exact.
- Tujuh scene interior berbeda. Posisi distrik tidak berubah: Professor Byte di Academy; Alex, Maya, Detective Byte di Cafe; Tux di Linux Lab; Red dan Blue di Web Security Lab.
- NPC tidak tampil di luar. Pintu bangunan membuka pilihan ruangan; akses mentor tetap mengikuti progres campaign.
- Transisi fade mengunci gerak/input. Keluar lewat pintu selatan mengembalikan pemain ke posisi kota sebelum masuk. Save di dalam ruangan menyimpan koordinat kota, bukan koordinat interior.
- Dialog bergantian NPC/MC dengan nama pemain, pilihan tugas utama, dan quest khusus Red. Dialog baru tersedia dalam bahasa Indonesia/Inggris; pilihan bahasa lain memakai Inggris.
- Virus Sweep: tiga gelombang, 24 virus, tiga HP, pause, retry, hasil menang/kalah, dan lencana VIRUS DEFENDER setelah melapor ke Red. Tidak mengubah XP atau urutan sebelas misi utama.
- Save v2 lama tetap terbaca; quest opsional menggunakan field tambahan dengan nilai default.

## Cara mencoba
1. Buka `Assets/CyberCity/Scenes/CyberCity.unity`, lalu Play dan mulai/lanjutkan patroli.
2. Ikuti tanda emas ke pintu bangunan. Tekan **E**, pilih ruangan, dekati NPC lalu **E** lagi.
3. Pilih **Tugas utama** setelah percakapan. Keluar lewat pintu selatan dengan **E**.
4. Setelah Web Security Lab terbuka, masuk ke ruangan **Red** dan pilih **Virus Sweep**.
5. Arena: **WASD/panah** bergerak, **mouse + klik kiri** membidik/menembak, atau tahan **Space** untuk membidik virus terdekat. **Esc** menjeda.
6. Setelah menang, bicara lagi dengan Red dan pilih **Lapor Virus Sweep**. Statusnya tercatat di **J / Journal**.

## Pengujian ulang di Unity
- Tools → CyberCity → Verify core systems
- Tools → CyberCity → Verify interior and arcade core
- Dalam Play Mode, di kota: Tools → CyberCity → Verify interior play mode
- Dalam Play Mode: Tools → CyberCity → Verify complete playthrough
- Tools → CyberCity → Build Windows player

Scene interior dapat dibuat ulang melalui **Build NPC interiors**. Builder utama juga memanggilnya. Build Windows memakai semua scene yang aktif dalam EditorBuildSettings.

## Berkas bukti
`VERIFICATION.txt` mencatat hasil aktual, perintah checksum baseline/modified/rollback, dan status build. `DIFF_FILE.patch` memuat perubahan; `MODIFIED_FILE.zip` berisi berkas implementasi yang berubah. `original-hashes.json` mempertahankan SHA-256 awal.

Rollback dijalankan setelah Unity ditutup, memakai Git Bash:

```bash
bash D:/CyberCity/CyberCity/Documentation/RevisionEvidence/ROLLBACK.sh D:/CyberCity/CyberCity
```

Rollback memeriksa hash sebelum mengubah berkas dan menyimpan versi yang diganti di `Temp/RevisionRollback-*`. Progres pemain di persistentDataPath tidak disentuh. Rollback mengembalikan kode lama, termasuk perilaku checksum lama; gunakan hanya jika memang ingin membatalkan revisi.
