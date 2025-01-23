using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WSJTX_Controller
{
    public partial class HelpDlg : Form
    {
        private Controller ctrl;

        public HelpDlg(Controller co, string c, string t)
        {
            InitializeComponent();

            ctrl = co;
            Text = c;
            helpLabel.Text = t;
        }

        private void HelpDlg_Load(object sender, EventArgs e)
        {
            //center on current screen
            Screen screen = Screen.FromControl(ctrl);
            Location = new Point(screen.Bounds.X + ((screen.Bounds.Width - Width) / 2) - 50, screen.Bounds.Y + ((screen.Bounds.Height - Height) / 2) - 200);

            int y = helpLabel.Size.Height;

            Height = helpLabel.Location.Y + y + 85;
            closeButton.Location = new Point(closeButton.Location.X, Height - 70);
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void HelpDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            ctrl.HelpClosed();
        }
    }
}
