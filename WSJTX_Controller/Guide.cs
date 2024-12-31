using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using  WSJTX_Controller;

namespace WSJTX_Controller
{
    public partial class Guide : Form
    {
        private Color normalFore;
        private Color normalBack;
        private Color highlightFore;
        private Color highlightBack;
        private Color highlightBackDisabled;

        private bool cqButtonEnabled = false;
        private bool activatorEnabled = false;
        private bool hunterEnabled = false;
        private bool cqDxButtonEnabled = false;
        private bool nonDxButtonEnabled = false;
        private bool dxButtonEnabled = false;
        private bool freqButtonEnabled = false;
        private bool dxccButtonEnabled = false;
        private List<Button> disableList;

        private WsjtxClient wsjtxClient;
        private Controller ctrl;

        public Guide(WsjtxClient cl, Controller co)
        {
            InitializeComponent();

            wsjtxClient = cl;
            ctrl = co;

            normalFore = closeButton.ForeColor;
            normalBack = closeButton.BackColor;
            highlightFore = Color.White;
            highlightBack = Color.Green;
            highlightBackDisabled = Color.LightGreen;

            disableList = new List<Button>()
            {
                listenButton,
                callCqButton,

                cqButton,
                cqDxButton,

                dxButton,
                nonDxButton,

                potaButton,
                hunterButton,

                allButton,
                recentButton
            };
        }

        public void UpdateView()
        {
            UpdateAllButtons();
        }

        private void Guide_Load(object sender, EventArgs e)
        {
            modeLabel.Visible = modeLabel2.Visible = callCqButton.Visible = listenButton.Visible = label13.Visible = label14.Visible = dxccButton.Visible = dxccHelpLabel.Visible = wsjtxClient.showTxModes;
            UpdateAllButtons();
            dxccButtonEnabled = wsjtxClient.txMode == WsjtxClient.TxModes.LISTEN && ctrl.periodComboBox.SelectedIndex == (int)WsjtxClient.ListenModeTxPeriods.ANY && ctrl.replyNewDxccCheckBox.Checked && ctrl.replyNewOnlyCheckBox.Checked;
            dxccHelpLabel.Visible = dxccButton.Visible && dxccButtonEnabled;
            dxccHelpLabel.Text = "(Unselect to use\r\nother options";
            UpdateAllButtons();

            //center on current screen
            Screen screen = Screen.FromControl(ctrl);
            Location = new Point(screen.Bounds.X + ((screen.Bounds.Width - Width) / 2), screen.Bounds.Y + ((screen.Bounds.Height - Height) / 2));
        }

        private void Guide_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (dxccButton.Visible)
            {
                if (dxccButtonEnabled)
                {
                    ctrl.GuideListenMode();
                    ctrl.replyNewDxccCheckBox.Checked = true;
                    ctrl.replyNewOnlyCheckBox.Checked = true;
                    ctrl.periodComboBox.SelectedIndex = (int)WsjtxClient.ListenModeTxPeriods.ANY;
                }
                else
                {
                    ctrl.replyNewDxccCheckBox.Checked = false;
                    ctrl.replyNewOnlyCheckBox.Checked = false;
                }
            }

            if (dxButtonEnabled || nonDxButtonEnabled)
            {
                ctrl.cqOnlyRadioButton.Checked = true;
                ctrl.bandComboBox.SelectedIndex = (int)WsjtxClient.NewCallBands.ANY;
            }
        }

        private void Guide_FormClosed(object sender, FormClosedEventArgs e)
        {
            ctrl.GuideClosed();
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void UpdateAllButtons()
        {
            foreach (Button b in disableList)
            {
                b.Enabled = !dxccButtonEnabled;
            }

            if (dxccButton.Visible && dxccButtonEnabled)
            {

                foreach (Button b in disableList)
                {
                    b.Enabled = false;
                    SetState(b, b.ForeColor == Color.White, b.ForeColor != Color.White);
                }
            }
            else
            {
                SetState(listenButton, wsjtxClient.txMode == WsjtxClient.TxModes.LISTEN && ctrl.periodComboBox.SelectedIndex == (int)WsjtxClient.ListenModeTxPeriods.ANY, true);
                SetState(callCqButton, wsjtxClient.txMode == WsjtxClient.TxModes.CALL_CQ, true);

                SetState(cqButton, (cqButtonEnabled = ctrl.callNonDirCqCheckBox.Checked && !ctrl.callDirCqCheckBox.Checked), true);
                SetState(cqDxButton, (cqDxButtonEnabled = ctrl.callCqDxCheckBox.Checked && !ctrl.callDirCqCheckBox.Checked), true);

                SetState(dxButton, (dxButtonEnabled = ctrl.replyDxCheckBox.Checked), true);
                SetState(nonDxButton, (nonDxButtonEnabled = ctrl.replyLocalCheckBox.Checked), true);

                SetState(potaButton, (activatorEnabled = wsjtxClient.txMode == WsjtxClient.TxModes.CALL_CQ && ctrl.directedTextBox.Text == "POTA" && ctrl.callDirCqCheckBox.Checked && !ctrl.callCqDxCheckBox.Checked && !ctrl.callNonDirCqCheckBox.Checked), true);
                SetState(hunterButton, (hunterEnabled = wsjtxClient.txMode == WsjtxClient.TxModes.LISTEN && ctrl.alertTextBox.Text.Contains("POTA") && ctrl.replyDirCqCheckBox.Checked), true);

                SetState(allButton, (ctrl.rankComboBox.SelectedIndex == (int)WsjtxClient.RankMethods.CALL_ORDER && ctrl.timeoutNumUpDown.Value == 3), true);
                SetState(recentButton, (ctrl.rankComboBox.SelectedIndex == (int)WsjtxClient.RankMethods.MOST_RECENT && ctrl.timeoutNumUpDown.Value == 1), true);
            }

            SetState(freqButton, (freqButtonEnabled = ctrl.freqCheckBox.Checked), true);
            if (dxccButton.Visible) SetState(dxccButton, dxccButtonEnabled, true);

            if (dxButtonEnabled || nonDxButtonEnabled) dxccHelpLabel.Visible = false;
        }

        private void callCqButton_Click(object sender, EventArgs e)
        {
            ctrl.GuideCqMode();
            UpdateAllButtons();
        }

        private void listenButton_Click(object sender, EventArgs e)
        {
            ctrl.GuideListenMode();
            if (wsjtxClient.txMode == WsjtxClient.TxModes.LISTEN) ctrl.periodComboBox.SelectedIndex = (int)WsjtxClient.ListenModeTxPeriods.ANY;
            UpdateAllButtons();
        }



        private void cqButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            if (cqButtonEnabled)
            {
                ctrl.callNonDirCqCheckBox.Checked = false;
            }
            else
            {
                ctrl.callNonDirCqCheckBox.Checked = true;
                ctrl.callDirCqCheckBox.Checked = false;
            }
            UpdateAllButtons();
        }

        private void cqDxButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            if (cqDxButtonEnabled)
            {
                ctrl.callCqDxCheckBox.Checked = false;
            }
            else
            {
                ctrl.callCqDxCheckBox.Checked = true;
                ctrl.callDirCqCheckBox.Checked = false;
                ctrl.periodComboBox.SelectedIndex = (int)WsjtxClient.ListenModeTxPeriods.ANY;
            }
            UpdateAllButtons();
        }



        private void dxButton_Click(object sender, EventArgs e)
        {

            UpdateAllButtons();
            ctrl.ToggleDx();
            UpdateAllButtons();
        }

        private void nonDxButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            ctrl.ToggleLocal();
            UpdateAllButtons();
        }



        private void potaButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            if (!activatorEnabled && hunterEnabled) ctrl.ToggleHunter();
            ctrl.ToggleActivator();
            ctrl.cqModeButton_Click(null, null);
            UpdateAllButtons();
        }

        private void hunterButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            if (!hunterEnabled && activatorEnabled) ctrl.ToggleActivator();
            ctrl.ToggleHunter();
            ctrl.listenModeButton_Click(null, null);
            ctrl.periodComboBox.SelectedIndex = (int)WsjtxClient.ListenModeTxPeriods.ANY;
            UpdateAllButtons();
        }



        private void allButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            if (ctrl.rankComboBox.SelectedIndex != (int)WsjtxClient.RankMethods.CALL_ORDER || ctrl.timeoutNumUpDown.Value != 3)
            {
                ctrl.rankComboBox.SelectedIndex = (int)WsjtxClient.RankMethods.CALL_ORDER;
                ctrl.timeoutNumUpDown.Value = 3;
            }
            UpdateAllButtons();
        }

        private void recentButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            if (ctrl.rankComboBox.SelectedIndex != (int)WsjtxClient.RankMethods.MOST_RECENT || ctrl.timeoutNumUpDown.Value != 1)
            {
                ctrl.rankComboBox.SelectedIndex = (int)WsjtxClient.RankMethods.MOST_RECENT;
                ctrl.timeoutNumUpDown.Value = 1;
            }
            UpdateAllButtons();
        }



        private void freqButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            ctrl.freqCheckBox.Checked = !ctrl.freqCheckBox.Checked;
            UpdateAllButtons();
        }


        private void dxccButton_Click(object sender, EventArgs e)
        {
            UpdateAllButtons();
            dxccButtonEnabled = !dxccButtonEnabled;
            if (!dxccButtonEnabled)
            {
                if (!dxButtonEnabled && !nonDxButtonEnabled)
                {
                    dxccHelpLabel.Visible = true;
                    dxccHelpLabel.Text = "(Check 'DX' and\r\n'My continent)";
                }
                else
                {
                    dxccHelpLabel.Visible = false;
                }
            }
            else
            {
                dxccHelpLabel.Visible = false;
            }
            UpdateAllButtons();
        }


        private void SetState(Button button, bool selected, bool enabled)
        {
            if (selected)
            {
                HighLight(button, enabled);
            }
            else
            {
                Normal(button, enabled);
            }
        }

        private void HighLight(Button button, bool enabled)
        {
            button.ForeColor = highlightFore;
            button.BackColor = enabled ? highlightBack : highlightBackDisabled;
        }

        private void Normal(Button button, bool enabled)
        {
            button.ForeColor = normalFore;
            button.BackColor = normalBack;
        }

        private void Guide_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Q)
            {
                Close();
            }
        }
    }
}
