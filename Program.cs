using System;
using System.Windows.Forms;

namespace VectorCanTest
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Forms.TestForm());
        }
    }
}
