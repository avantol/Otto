using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WSJTX_Controller
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            {
                MessageBox.Show("An instance of this application is already running.");
                return;
            }
            if (System.Diagnostics.Process.GetProcessesByName("Semmi").Count() > 0)
            {
                MessageBox.Show("Otto and Semmi can't run at the same time.\n\nClose Semmi before running Otto.");
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Controller());
        }
    }
}
