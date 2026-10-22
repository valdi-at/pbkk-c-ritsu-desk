# RITSU Desk


NRP: **5025221238**
Nama: **Mohamad Valdi Anadna Tauhid**

## Jawaban ETS

### Pertanyaan 1

> [!NOTE]
> 1. Jelaskan perbedaan aplikasi desktop dengan aplikasi lain? Mengapa masih dibutuhkan

Aplikasi desktop adalah perangkat lunak yang dipasang dan berjalan langsung di sistem operasi komputer (Windows, macOS, Linux) dengan memanfaatkan sumber daya lokal seperti CPU, RAM, dan GPU. Istilah “desktop” berasal dari metafora permukaan meja kerja pada antarmuka grafis komputer pribadi. Berbeda dengan aplikasi web yang cukup dibuka lewat browser dan umumnya butuh internet, aplikasi desktop harus diinstal, bisa berjalan offline, dan punya akses penuh ke perangkat keras serta sistem file.

Aplikasi desktop masih dibutuhkan karena mampu menangani pekerjaan berat (video editing, rendering 3D, game, IDE), tetap berfungsi tanpa internet, memiliki akses sistem yang lebih dalam, menjaga privasi data lokal, terintegrasi baik dengan OS, dan masih dipakai pada banyak sistem lama (legacy). Kini batasnya dengan aplikasi web makin kabur berkat Electron, Tauri, PWA, dan WebAssembly, sehingga pilihan terbaik bergantung pada kebutuhan: web unggul dalam kemudahan akses, desktop unggul dalam performa dan kendali.

### Pertanyaan 2

> [!NOTE]
> 2. Tuliskan problem yang akan diselesaikan oleh aplikasi yang kamu buat? Definisikan fitur aplikasi yang merupakan solusi dari problem tersebut.

**Problem**

RITSU (Rute ITS Utama) adalah aplikasi web ojek kampus ITS yang berfokus pada alur pemesanan berbasis peta. Dari sisi informasi dan pengelolaan data, masih ada beberapa masalah:

1. **Penumpang tidak punya rekap pengeluaran.** Halaman riwayat di web hanya menampilkan pemesanan yang sudah selesai (stage 5), tanpa total pengeluaran.
2. **Driver tidak punya rekap pendapatan pribadi.** Data pendapatan per driver baru muncul di panel "Top Earning Drivers" pada dashboard admin.
3. **Admin kesulitan memantau operasional.** Dashboard web menampilkan banyak angka, tetapi tidak ada daftar pemesanan dengan pencarian dan filter, detail per pengguna, maupun ekspor laporan.
4. **Aturan bisnis admin di web belum aman.** Persetujuan pengajuan driver menjalankan dua UPDATE tanpa transaksi, dan penghapusan pengguna yang punya riwayat pemesanan menghasilkan error yang tidak ditangani.
5. **Aplikasi desktop yang terhubung langsung ke database berisiko.** Kredensial PostgreSQL harus tersimpan di setiap perangkat, sehingga siapa pun yang membacanya bisa melewati semua pembatasan peran.

**Solusi: RITSU Desk**

RITSU Desk adalah aplikasi desktop WPF (C#, .NET 10) untuk melihat dan mengelola data RITSU. Aplikasi ini tidak terhubung langsung ke database, tetapi memanggil RITSU Desktop API (`/api/desktop/v1`) yang dijalankan server web. Dengan begitu, query, pemeriksaan hak akses, dan aturan bisnis tetap berada di satu tempat, dan data yang tampil sama persis dengan web. RITSU Desk tidak menggantikan alur pemesanan, chat, dan pelacakan lokasi, yang tetap di web karena membutuhkan peta, GPS, dan interaksi real time.

| Problem | Fitur solusi |
|---|---|
| Tidak ada rekap pengeluaran penumpang | **Dashboard Saya** dan **Riwayat Perjalanan**: total perjalanan, total pengeluaran, perjalanan belum dibayar, grafik per bulan, serta pencarian, filter (stage, status bayar, periode), dan panel detail |
| Tidak ada rekap pendapatan driver | Dashboard dan riwayat khusus driver: pendapatan dari perjalanan yang sudah dibayar, dan driver bisa beralih ke data sebagai penumpang |
| Mahasiswa tidak tahu hasil pengajuan driver | **Status Driver**: status Menunggu/Disetujui/Ditolak, tanggal peninjauan, dan alasan penolakan |
| Admin sulit memantau operasional | **Dashboard Admin** (statistik pengguna, pendapatan, outstanding, volume pemesanan, top driver, aktivitas terbaru), **Pemesanan** dengan filter dan detail, serta **Laporan Keuangan** dengan tren bulanan dan rincian per metode pembayaran |
| Tidak ada ekspor laporan | **Ekspor CSV** daftar pemesanan (mengikuti filter aktif) dan laporan keuangan |
| Aturan admin tidak aman dan tidak konsisten | **Pengajuan Driver** (setujui secara transaksional, tolak dengan alasan wajib, pratinjau foto SIM) dan **Pengguna** (ubah role, hapus dengan pengecekan relasi, admin tidak bisa mengubah atau menghapus dirinya sendiri), semuanya lewat service bersama di server |
| Risiko kredensial database di perangkat | **Arsitektur berbasis API**: login memakai akun RITSU yang sama, token bearer berumur 12 jam hanya disimpan di memori, role dibaca dari database pada setiap request, dan data penumpang atau driver dibatasi ke data miliknya sendiri di sisi server |
| Pengalaman pengguna dan keandalan | Menu otomatis sesuai role, dark/light mode, notifikasi toast, pesan error berbahasa Indonesia, penanganan sesi kedaluwarsa dan server tidak terjangkau, tombol muat ulang, serta pengecekan kompatibilitas versi API (`/meta`) |

### Pertanyaan 3

> [!NOTE]
> 3. Buat Poster Infografis aplikasi yang telah dibuat dalam 1 halaman.

![Poster Infografis RITSU Desk](./Poster%20Infografis%20RITSU%20Desk.png)
