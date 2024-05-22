using System; // Menggunakan fitur dasar dari bahasa C#
using System.Drawing; // Untuk manipulasi gambar seperti Bitmap, Color, dll.
using System.Windows.Forms; // Untuk komponen User Interface (UI) seperti Form, Button, PictureBox, dll.
using MPI; // Untuk pemrosesan paralel dengan MPI.NET (Message Passing Interface)
using System.IO; // Untuk operasi input/output dengan file
using System.Drawing.Imaging; // Untuk mengatur format gambar seperti JPEG, PNG, BMP, dll.
using System.ComponentModel; // Untuk menggunakan komponen seperti BackgroundWorker

namespace MPI_CG
{
    public partial class Form1 : Form
    {
        private Intracommunicator comm; // Deklarasi objek untuk komunikasi MPI
        private Bitmap originalImage; // Menyimpan gambar asli yang dimuat
        private BackgroundWorker progressWorker; // BackgroundWorker untuk menangani operasi latar belakang dan pelaporan kemajuan

        public Form1()
        {
            InitializeComponent(); // Inisialisasi komponen UI
            comm = MPI.Communicator.world; // Inisialisasi komunikasi MPI dengan dunia MPI

            // Inisialisasi BackgroundWorker
            progressWorker = new BackgroundWorker();
            progressWorker.WorkerReportsProgress = true; // Mengizinkan BackgroundWorker melaporkan kemajuan
            progressWorker.DoWork += ProgressWorker_DoWork; // Event handler ketika pekerjaan dijalankan di latar belakang
            progressWorker.ProgressChanged += ProgressWorker_ProgressChanged; // Event handler ketika kemajuan pekerjaan berubah
            progressWorker.RunWorkerCompleted += progressWorker_RunWorkerCompleted; // Event handler ketika pekerjaan selesai

        }

        private void btnLoadImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog(); // Membuat instance dari OpenFileDialog untuk memilih file gambar
            openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"; // Menetapkan filter untuk hanya menampilkan file gambar dengan ekstensi tertentu
            if (openFileDialog.ShowDialog() == DialogResult.OK) // Memeriksa apakah pengguna telah memilih file dan menekan tombol OK
            {
                originalImage = new Bitmap(openFileDialog.FileName); // Memuat gambar yang dipilih ke dalam objek Bitmap
                PictureBox1.Image = originalImage; // Menampilkan gambar yang dipilih di PictureBox pada form
            }
        }

        private void btnGrayscale_Click(object sender, EventArgs e)
        {
            if (originalImage != null) // Memeriksa apakah ada gambar yang dimuat
            {
                ProcessImageWithMPI("grayscale"); // Memproses gambar dengan mode "grayscale" menggunakan MPI
            }
        }

        private void btnBinarize_Click(object sender, EventArgs e)
        {
            if (originalImage != null) // Memeriksa apakah ada gambar yang dimuat
            {
                ProcessImageWithMPI("binarize"); // Memproses gambar dengan mode "binarize" menggunakan MPI
            }
        }

        private void ClearPictureBox()
        {
            PictureBox1.Image = null; // Menghapus gambar yang ditampilkan di PictureBox
            ProgressBar1.Value = 0; // Mengatur ulang nilai ProgressBar menjadi 0
            originalImage = null; // Menghapus referensi ke gambar asli untuk membersihkan memori
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (originalImage != null) // Memeriksa apakah ada gambar yang dimuat
            {
                // Menampilkan gambar asli kembali
                PictureBox1.Image = originalImage; // Mengatur PictureBox untuk menampilkan gambar asli
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            ClearPictureBox(); // Memanggil metode ClearPictureBox untuk menghapus gambar dari PictureBox dan mengatur ulang ProgressBar
        }

        private Bitmap ResizeImage(Bitmap originalImage, Size newSize)
        {
            // Membuat gambar baru dengan ukuran yang ditentukan
            Bitmap resizedImage = new Bitmap(newSize.Width, newSize.Height);

            // Menggunakan Graphics untuk menggambar gambar asli dengan ukuran baru
            using (Graphics graphics = Graphics.FromImage(resizedImage))
            {
                // Mengatur mode interpolasi untuk mendapatkan kualitas gambar yang tinggi saat gambar diubah ukurannya
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                // Menggambar gambar asli pada gambar yang diubah ukurannya
                graphics.DrawImage(originalImage, new Rectangle(Point.Empty, newSize));
            }
            
            return resizedImage;  // Mengembalikan gambar yang sudah diubah ukurannya
        } 

        private void ProgressWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            int totalParts = (int)e.Argument;

            // Melakukan perulangan untuk mengupdate progress
            for (int i = 0; i < totalParts; i++)
            {
                // Menghitung nilai progress
                int progressPercentage = (i + 1) * 100 / totalParts;

                // Melaporkan progress ke UI thread
                worker.ReportProgress(progressPercentage);

                // Menunggu sejenak sebelum mengupdate progress berikutnya
                System.Threading.Thread.Sleep(100);
            }
        }

        private void ProgressWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Mengupdate nilai progress bar dengan persentase progress yang dilaporkan
            ProgressBar1.Value = e.ProgressPercentage;
        }

        private void ProcessImageWithMPI(string mode)
        {
            // Membuat objek Bitmap dari gambar yang ditampilkan di PictureBox1
            Bitmap original = new Bitmap(PictureBox1.Image);

            // Membuat Varibal untuk memproses pengolahan gambar dan MPI
            // Inisialisasi Varibal untuk mentimpan lebar dan tinggi picturebox
            int pictureBoxWidth = PictureBox1.Width;
            int pictureBoxHeight = PictureBox1.Height;

            // Inisialisasi Varibal untuk mentimpan lebar dan tinggi gambar asli
            int originalWidth = original.Width;
            int originalHeight = original.Height;

            // Mengatur ukuran gambar hasil
            Size newSize = new Size(pictureBoxWidth, pictureBoxHeight);

            // Menyesuaikan ukuran gambar asli dengan ukuran gambar hasil
            Bitmap resizedOriginal = ResizeImage(original, newSize);

            // Menghitung ukuran potongan gambar untuk masing-masing proses
            int chunkSize = originalHeight / comm.Size;
            int totalParts = comm.Size;

            // Membuat Variabel untuk menyimpan data gambar yang diterima dari semua proses
            byte[][] receivedData = null;

            if (comm.Rank == 0)
            {
                // Inisialisasi array untuk menyimpan data dari semua proses pada proses root (rank 0)
                receivedData = new byte[comm.Size][];
                for (int i = 0; i < comm.Size; i++)
                {
                    receivedData[i] = new byte[originalHeight * originalWidth * 3];
                }
            }

            // Mengubah bagian dari gambar menjadi array byte
            byte[] partialData = ImageToByteArray(original.Clone(new Rectangle(0, 0, originalWidth, chunkSize), original.PixelFormat), 0, chunkSize);
            
            // Mengumpulkan data gambar dari semua proses
            comm.Gather<byte[]>(partialData, 0, ref receivedData);

            if (comm.Rank == 0)
            {
                // Menggabungkan bagian-bagian kecil gambar menjadi gambar akhir
                Bitmap finalImage = MergeImages(receivedData, originalWidth, originalHeight);

                // Menerapkan efek grayscale atau binary pada gambar hasil
                if (mode == "grayscale")
                {
                    ConvertToGrayscale(finalImage);
                }
                else if (mode == "binarize")
                {
                    ConvertToBinary(finalImage);
                }
                // Menampilkan gambar hasil di PictureBox
                PictureBox1.Image = finalImage;
            }
            // Sinkronisasi semua proses
            comm.Barrier();

            // Menjalankan background worker untuk mengupdate progress bar
            progressWorker.RunWorkerAsync(totalParts);
                       
        }

        private void progressWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Menampilkan notifikasi ketika proses selesai
            ShowConversionCompleteNotification();
        }

        private Bitmap MergeImages(byte[][] imageData, int originalWidth, int originalHeight)
        {
            // Mendapatkan ukuran PictureBox1
            int pictureBoxWidth = PictureBox1.Width;
            int pictureBoxHeight = PictureBox1.Height;

            // Menghitung tinggi gambar yang dihasilkan
            float scaleX = (float)pictureBoxWidth / originalWidth;
            float scaleY = (float)pictureBoxHeight / originalHeight;

            // Menggabungkan semua bagian-bagian kecil dari gambar hasil
            Bitmap finalImage = new Bitmap(pictureBoxWidth, pictureBoxHeight);

            // Menggunakan Graphics untuk menggambar bagian-bagian gambar hasil ke dalam gambar final
            using (Graphics g = Graphics.FromImage(finalImage))
            {
                // Mengatur mode interpolasi gambar untuk hasil berkualitas tinggi
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                // Jumlah bagian gambar hasil
                int totalParts = imageData.Length;

                // Memproses setiap bagian gambar hasil
                for (int i = 0; i < totalParts; i++)
                {
                    // Mengubah bagian gambar hasil dari array byte menjadi objek Bitmap
                    Bitmap partialImage = ByteArrayToImage(imageData[i], originalWidth, originalHeight);

                    // Mengubah ukuran bagian gambar hasil sesuai dengan faktor skalanya
                    partialImage = ResizeImage(partialImage, new Size((int)(originalWidth * scaleX), (int)(originalHeight * scaleY)));

                    // Menggambar bagian gambar hasil ke dalam gambar final
                    g.DrawImage(partialImage, new Point(0, i * (int)(originalHeight * scaleY)));

                    // Memperbarui nilai progress bar
                    float progress = (float)(i + 1) / totalParts * 100;
                    ProgressBar1.Value = (int)progress;
                }
            }
            // Mengembalikan gambar final yang telah digambar
            return finalImage;
        }


        private void ConvertToGrayscale(Bitmap partialImage)
        {
            // Mendapatkan lebar dan tinggi gambar
            int width = partialImage.Width;
            int height = partialImage.Height;
            
            // Melakukan iterasi untuk setiap piksel dalam gambar
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Mendapatkan warna asli dari piksel
                    Color originalColor = partialImage.GetPixel(x, y);

                    // Menghitung nilai grayscale dari warna piksel
                    int grayScale = (int)((originalColor.R * 0.3) + (originalColor.G * 0.59) + (originalColor.B * 0.11));

                    // Membuat warna baru dengan nilai grayscale yang sama untuk setiap komponen RGB
                    Color newColor = Color.FromArgb(grayScale, grayScale, grayScale);

                    // Mengatur piksel ke warna baru
                    partialImage.SetPixel(x, y, newColor);
                }
            }
        }

        private void ConvertToBinary(Bitmap partialImage)
        {
            // Mendapatkan lebar dan tinggi gambar
            int width = partialImage.Width;
            int height = partialImage.Height;

            // Melakukan iterasi untuk setiap piksel dalam gambar
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Mendapatkan warna asli dari piksel
                    Color originalColor = partialImage.GetPixel(x, y);

                    // Menghitung nilai grayscale dari warna piksel
                    int grayScale = (int)((originalColor.R * 0.3) + (originalColor.G * 0.59) + (originalColor.B * 0.11));

                    // Menentukan warna biner berdasarkan nilai grayscale
                    Color newColor = grayScale < 128 ? Color.Black : Color.White;

                    // Mengatur piksel ke warna biner yang baru
                    partialImage.SetPixel(x, y, newColor);
                }
            }
        }

        private byte[] ImageToByteArray(Bitmap image, int startY, int endY)
        {
            // Membuat MemoryStream untuk menampung data gambar
            MemoryStream ms = new MemoryStream();

            // Mengkloning bagian gambar yang diinginkan
            Bitmap cropped = image.Clone(new Rectangle(0, startY, image.Width, endY - startY), image.PixelFormat);

            // Menyimpan gambar yang sudah di-kloning ke MemoryStream dalam format BMP
            cropped.Save(ms, ImageFormat.Bmp);

            // Mengembalikan data dalam bentuk byte array
            return ms.ToArray();
        }

        private Bitmap ByteArrayToImage(byte[] data, int width, int height)
        {
            // Membuat MemoryStream dari data byte array
            MemoryStream ms = new MemoryStream(data);

            // Membuat objek Bitmap dari MemoryStream
            Bitmap bmp = new Bitmap(ms);

            // Mengembalikan gambar hasil dari data byte array
            return bmp;
        }

        private void ShowConversionCompleteNotification()
        {
            // Menampilkan notifikasi bahwa proses konversi telah selesai
            MessageBox.Show("Proses konversi selesai.", "Notifikasi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
