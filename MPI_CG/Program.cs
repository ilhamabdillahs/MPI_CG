using System;
using System.Windows.Forms;

namespace MPI_CG
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Simpan argumen command line dalam variabel lokal
            string[] args = System.Environment.GetCommandLineArgs();

            // Inisialisasi MPI
            using (new MPI.Environment(ref args))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
        }
    }
}

// mpiexec -n 2 MPI_CG.exe
