using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using WsjtxUdpLib;
using System.Net;
using System.Configuration;
using System.Threading;
using System.Media;
using System.IO;
using System.Reflection;
using Microsoft.Win32;


namespace WSJTX_Controller
{
    public partial class Controller : Form
    {
        public WsjtxClient wsjtxClient;
        public Guide guide;
        public bool alwaysOnTop = false;
        public bool firstRun = true;        //first run for each user level
        public bool skipLevelPrompt = false;
        public bool offsetTune = false;

        private bool formLoaded = false;
        private SetupDlg setupDlg = null;
        private HelpDlg helpDlg = null;
        private IniFile iniFile = null;
        private int minSkipCount = 1;
        private const int maxSkipCount = 20;
        private const string separateBySpaces = "(separate by spaces)";
        private bool showCloseMsgs = true;
        public string friendlyName = "";
        private MouseEventArgs mouseEventArgs;
        private int listBoxClickCount;
        private bool ignoreDirectedChange = false;
        private bool showOptions = false;
        private Control[] movableCtrls;
        private int[] movableCtrlYs;
        private List<Control> hideCtrls;
        private int optionsOffset;
        private string helpSuffix = " Help";
        private bool ignoreExceptChange = false;

    private System.Windows.Forms.Timer mainLoopTimer;

        public System.Windows.Forms.Timer statusMsgTimer;
        public System.Windows.Forms.Timer initialConnFaultTimer;
        public System.Windows.Forms.Timer debugHighlightTimer;
        public System.Windows.Forms.Timer setupTimer;
        public System.Windows.Forms.Timer guideTimer;
        public System.Windows.Forms.Timer callListBoxClickTimer;
        public System.Windows.Forms.Timer helpTimer;

        private string nl = Environment.NewLine;

        public Controller()
        {
            InitializeComponent();
            friendlyName = Text;
            KeyPreview = true;

            //timers
            mainLoopTimer = new System.Windows.Forms.Timer();
            mainLoopTimer.Tick += new System.EventHandler(mainLoopTimer_Tick);
            statusMsgTimer = new System.Windows.Forms.Timer();
            statusMsgTimer.Interval = 5000;
            statusMsgTimer.Tick += new System.EventHandler(statusMsgTimer_Tick);
            initialConnFaultTimer = new System.Windows.Forms.Timer();
            initialConnFaultTimer.Tick += new System.EventHandler(initialConnFaultTimer_Tick);
            debugHighlightTimer = new System.Windows.Forms.Timer();
            debugHighlightTimer.Tick += new System.EventHandler(debugHighlightTimer_Tick);
            setupTimer = new System.Windows.Forms.Timer();
            setupTimer.Interval = 20;
            setupTimer.Tick += new System.EventHandler(setupTimer_Tick);
            guideTimer = new System.Windows.Forms.Timer();
            guideTimer.Interval = 20;
            guideTimer.Tick += new System.EventHandler(guideTimer_Tick);
            callListBoxClickTimer = new System.Windows.Forms.Timer();
            callListBoxClickTimer.Interval = 250;
            callListBoxClickTimer.Tick += new System.EventHandler(callListBoxClickTimer_Tick);
            helpTimer = new System.Windows.Forms.Timer();
            helpTimer.Interval = 20;
            helpTimer.Tick += new System.EventHandler(helpTimer_Tick);

            optionsOffset = modeGroupBox.Location.Y - (callListBox.Location.Y + callListBox.Size.Height) - 10;
            movableCtrls = new Control[9] 
                {optionsButton,
                setupButton,
                modeGroupBox,
                guideLabel,
                msgTextBox,
                statusText,
                verLabel,
                verLabel2,
                modeHelpLabel};

            movableCtrlYs = new int[9];
            for (int i = 0; i < movableCtrlYs.Length; i++)
            {
                movableCtrlYs[i] = movableCtrls[i].Location.Y;
            }


            hideCtrls = new List<Control>();
            Rectangle rect = new Rectangle(
                0,
                callListBox.Location.Y + callListBox.Height + 10,
                Width,
                optionsButton.Location.Y + optionsButton.Height - modeGroupBox.Location.Y + 8);

            foreach (Control control in Controls) { 
                if (control.Bounds.IntersectsWith(rect))
                {
                    hideCtrls.Add(control);
                }
            }
        }

#if DEBUG
        //project type must be Console application for this to work

        [DllImport("Kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
#endif
        private void Form_Load(object sender, EventArgs e)
        {
            //use .ini file for settings (avoid .Net config file mess)
            string pgmName = Assembly.GetExecutingAssembly().GetName().Name.ToString();
            string path = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\{pgmName}";
            string pathFileNameExt = path + "\\" + pgmName + ".ini";
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                iniFile = new IniFile(pathFileNameExt);
            }
            catch
            {
                MessageBox.Show("Unable to create settings file: " + pathFileNameExt + $"{nl}Continuing with default settings...", friendlyName, MessageBoxButtons.OK);
            }

            string ipAddrStr = null;
            IPAddress ipAddress = null;
            int port = 0;
            bool multicast = true;
            bool overrideUdpDetect = false;
            bool advanced = false;
            bool debug = false;
            bool diagLog = false;
            WsjtxClient.TxModes txMode = WsjtxClient.TxModes.CALL_CQ;
            bool showTxModes = false;
            int offsetHiLimit = -1;
            int offsetLoLimit = -1;
            bool useRR73 = false;
            bool mode = false;
            string myContinent = null;

            //control defaults
            modeComboBox.SelectedIndex = 0;
            periodComboBox.SelectedIndex = 2;
            int rankMethodIdx = (int)WsjtxClient.RankMethods.CALL_ORDER;
            freqCheckBox.Checked = false;
            timedCheckBox.Checked = false;           //not saved

            if (iniFile == null || !iniFile.KeyExists("advanced"))     //.ini file not written yet, read properties (possibly set defaults)
            {
                firstRun = Properties.Settings.Default.firstRun;
                debug = Properties.Settings.Default.debug;
                if (Properties.Settings.Default.windowPos != new Point(0, 0)) 
                    this.Location = Properties.Settings.Default.windowPos;
                if (Properties.Settings.Default.windowHt != 0) 
                    this.Height = Properties.Settings.Default.windowHt;
                ipAddrStr = Properties.Settings.Default.ipAddress;
                port = Properties.Settings.Default.port;
                multicast = Properties.Settings.Default.multicast;
                timeoutNumUpDown.Value = Properties.Settings.Default.timeout;
                directedTextBox.Text = Properties.Settings.Default.directeds;
                callDirCqCheckBox.Checked = Properties.Settings.Default.useDirected;
                mycallCheckBox.Checked = Properties.Settings.Default.playMyCall;
                loggedCheckBox.Checked = Properties.Settings.Default.playLogged;
                alertTextBox.Text = Properties.Settings.Default.alertDirecteds;
                replyDirCqCheckBox.Checked = Properties.Settings.Default.useAlertDirected;
                logEarlyCheckBox.Checked = Properties.Settings.Default.logEarly;
                advanced = Properties.Settings.Default.advanced;
                alwaysOnTop = Properties.Settings.Default.alwaysOnTop;
                useRR73 = Properties.Settings.Default.useRR73;
                skipGridCheckBox.Checked = Properties.Settings.Default.skipGrid;
                diagLog = Properties.Settings.Default.diagLog;
            }
            else        //read settings from .ini file (avoid .Net config file mess)
            {
                firstRun = iniFile.Read("firstRun") == "True";
                debug = iniFile.Read("debug") == "True";

                int x;
                int.TryParse(iniFile.Read("windowPosX"), out x);
                int y;
                int.TryParse(iniFile.Read("windowPosY"), out y);
                //check all screens, extended screen may not be present
                var screens = System.Windows.Forms.Screen.AllScreens;
                bool found = false;
                for (int scnIdx = 0; scnIdx < screens.Length; scnIdx++)
                {
                    var screenBounds = screens[scnIdx].Bounds;
                    var centerPt = new Point(x + (this.Width / 2), y + (this.Height / 2));
                    if (screenBounds.Contains(centerPt))
                    {
                        found = true;       //found screen for window posn
                        break;
                    }
                }
                if (!found)     //default window posn
                {
                    x = 0;
                    y = 0;
                }
                this.Location = new Point(x, y);
                int i;
                int.TryParse(iniFile.Read("windowHt"), out i);
                this.Height = i;

                ipAddrStr = iniFile.Read("ipAddress");
                multicast = iniFile.Read("multicast") == "True";
                try
                {
                    ipAddress = IPAddress.Parse(ipAddrStr);
                    port = int.Parse(iniFile.Read("port"));
                }
                catch (Exception err)
                {
                    ipAddrStr = Properties.Settings.Default.ipAddress;
                    port = Properties.Settings.Default.port;
                    multicast = Properties.Settings.Default.multicast;
                }

                int.TryParse(iniFile.Read("timeout"), out i);
                timeoutNumUpDown.Value = i;
                directedTextBox.Text = iniFile.Read("directeds");
                callDirCqCheckBox.Checked = iniFile.Read("useDirected") == "True";
                mycallCheckBox.Checked = iniFile.Read("playMyCall") != "False";
                loggedCheckBox.Checked = iniFile.Read("playLogged") != "False";
                callAddedCheckBox.Checked = iniFile.Read("playCallAdded") != "False";
                alertTextBox.Text = iniFile.Read("alertDirecteds");
                replyDirCqCheckBox.Checked = iniFile.Read("useAlertDirected") == "True";
                logEarlyCheckBox.Checked = iniFile.Read("logEarly") == "True";
                advanced = iniFile.Read("advanced") == "True";
                alwaysOnTop = iniFile.Read("alwaysOnTop") == "True";
                useRR73 = iniFile.Read("useRR73") == "True";
                skipGridCheckBox.Checked = iniFile.Read("skipGrid") == "True";
                replyNewDxccCheckBox.Checked = iniFile.Read("autoReplyNewCq") == "True";
                replyDxCheckBox.Checked = iniFile.Read("enableReplyDx") != "False";     //default: true
                diagLog = iniFile.Read("diagLog") == "True";

                //start of .ini-file-only settings (not in .Net config)
                mode = iniFile.Read("replyAndQuit") == "True";
                showTxModes = iniFile.Read("showTxModes") == "True";
                freqCheckBox.Checked = iniFile.Read("bestOffset") == "True";
                if (iniFile.KeyExists("stopTxTime")) stopTextBox.Text = iniFile.Read("stopTxTime");
                if (iniFile.KeyExists("startTxTime")) startTextBox.Text = iniFile.Read("startTxTime");
                if (iniFile.KeyExists("timedOperationIdx"))
                {
                    int.TryParse(iniFile.Read("timedOperationIdx"), out i);
                    modeComboBox.SelectedIndex = i;
                }
                if (iniFile.KeyExists("offsetHiLimit")) int.TryParse(iniFile.Read("offsetHiLimit"), out offsetHiLimit);
                if (iniFile.KeyExists("offsetLoLimit")) int.TryParse(iniFile.Read("offsetLoLimit"), out offsetLoLimit);
                replyLocalCheckBox.Checked = iniFile.Read("enableReplyLocal") != "False";     //default
                optimizeCheckBox.Checked = iniFile.Read("optimizeTx") == "True";
                exceptTextBox.Text = iniFile.Read("exceptCalls");
                replyNewOnlyCheckBox.Checked = iniFile.Read("replyOnlyDxcc") == "True";
                callCqDxCheckBox.Checked = iniFile.Read("callCqDx") == "True";
                ignoreNonDxCheckBox.Checked = iniFile.Read("ignoreNonDx") == "True";
                callNonDirCqCheckBox.Checked = iniFile.Read("callNonDirCq") == "True";
                overrideUdpDetect = iniFile.Read("overrideUdpDetect") == "True";
                skipLevelPrompt = iniFile.Read("skipLevelPrompt") == "True";
                cqOnlyRadioButton.Checked = iniFile.Read("cqOnly") != "False";              //default: true
                bool newOnBand = iniFile.Read("newOnBand") != "False";      //default: true
                bandComboBox.SelectedIndex = newOnBand ? 1 : 0;
                if (iniFile.KeyExists("myContinent")) myContinent = iniFile.Read("myContinent");    //required to be null if not set
                if (iniFile.KeyExists("rankMethod")) int.TryParse(iniFile.Read("rankMethod"), out rankMethodIdx);
                cqGridRadioButton.Checked = iniFile.Read("cqGrid") == "True";
                anyMsgRadioButton.Checked = iniFile.Read("anyMsg") == "True";
                if (iniFile.KeyExists("txPeriodIdx"))
                {
                    int.TryParse(iniFile.Read("txPeriodIdx"), out i);
                    periodComboBox.SelectedIndex = i;
                }

                replyRR73CheckBox.Checked = iniFile.Read("replyRR73") == "True";
                //read-only
                offsetTune = iniFile.Read("offsetTune") == "True";
                showOptions = iniFile.Read("showOptions") == "True";
            }

            txMode = mode ? WsjtxClient.TxModes.LISTEN : WsjtxClient.TxModes.CALL_CQ;

            if (!advanced)
            {
                showTxModes = false;
                freqCheckBox.Checked = false;
                optimizeCheckBox.Checked = false;
                holdCheckBox.Checked = false;
                minSkipCount = 2;
            }

            if (showTxModes)
            {
                atLabel.Visible = true;
                modeComboBox.Visible = true;
                startLabel.Text = "Start";
            }
            else
            {
                txMode = WsjtxClient.TxModes.CALL_CQ;
                startLabel.Text = "Start calling CQ at:";
            }

            if (directedTextBox.Text == "") callDirCqCheckBox.Checked = false;
            directedTextBox.Enabled = callDirCqCheckBox.Checked;
            if (!directedTextBox.Enabled && directedTextBox.Text == "")
            {
                directedTextBox.Text = separateBySpaces;
            }

            if (alertTextBox.Text == "") replyDirCqCheckBox.Checked = false;
            alertTextBox.Enabled = replyDirCqCheckBox.Checked;
            if (!alertTextBox.Enabled && alertTextBox.Text == "")
            {
                alertTextBox.Text = separateBySpaces;
            }

            if (exceptTextBox.Text == "")
            {
                exceptTextBox.Text = separateBySpaces;
                exceptTextBox.ForeColor = Color.Gray;
            }

            UpdateTxLabel();

            callCqDxCheckBox_CheckedChanged(null, null);
            callNonDirCqCheckBox_CheckedChanged(null, null);
            directedTextBox_Leave(null, null);
            if (!cqOnlyRadioButton.Checked && !cqGridRadioButton.Checked && !anyMsgRadioButton.Checked) cqOnlyRadioButton.Checked = true;
            UpdateCqNewOnBand();

#if DEBUG
            AllocConsole();

            if (!debug)
            {
                ShowWindow(GetConsoleWindow(), 0);
            }
#endif

            //start the UDP message server
            wsjtxClient = new WsjtxClient(this, IPAddress.Parse(ipAddrStr), port, multicast, overrideUdpDetect, debug, diagLog);
            wsjtxClient.advanced = advanced;
            wsjtxClient.txMode = txMode;
            wsjtxClient.showTxModes = showTxModes;
            wsjtxClient.myContinent = myContinent;
            if (myContinent != null) replyLocalCheckBox.Text = myContinent;
            if (offsetLoLimit > 0) wsjtxClient.offsetLoLimit = offsetLoLimit;
            if (offsetHiLimit > 0) wsjtxClient.offsetHiLimit = offsetHiLimit;
            wsjtxClient.useRR73 = useRR73;
            rankComboBox.SelectedIndex = rankMethodIdx;
            wsjtxClient.RankMethodIdxChanged(rankMethodIdx);

            mainLoopTimer.Interval = 10;           //actual is 11-12 msec (due to OS limitations)
            mainLoopTimer.Start();

            if (wsjtxClient.advanced)
            {
                UpdateAdvancedCtrls();
            }
            TopMost = alwaysOnTop;

            UpdateDebug();

            wsjtxClient.UpdateModeSelection();

            formLoaded = true;
            updateReplyNewOnlyCheckBoxEnabled();
        }

        private void Controller_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (showCloseMsgs)                 //not closing for immediate restart
            {
                firstRun = false;

                if (!skipLevelPrompt)
                {
                    if (!wsjtxClient.advanced && wsjtxClient.ConnectedToWsjtx())
                    {
                        if (MessageBox.Show($"If you're familiar with the basic operation of this program now, you'll probably be interested in more options." +
                            $"{nl}{nl}Do you want to see all options the next time you run this program?" +
                            $"{nl}{nl}(You can make this choice later)", wsjtxClient.pgmName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            wsjtxClient.advanced = true;
                            firstRun = true;
                        }
                    }
                    else
                    {
                        if (!wsjtxClient.showTxModes && wsjtxClient.ConnectedToWsjtx())
                        {
                            if (MessageBox.Show($"If you're familiar with using some of the options in this program now, you'll probably be interested in the 'Listen for calls' option." +
                                $"{nl}{nl}This causes much less traffic on the band than CQing, by waiting to reply until the calls you want are detected." +
                                $"{nl}{nl}Do you want to see this useful option the next time you run this program?" +
                                $"{nl}{nl}(You can make this choice later)", wsjtxClient.pgmName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                wsjtxClient.showTxModes = true;
                                firstRun = true;
                            }
                        }
                    }
                }
            }


            if (iniFile != null)
            {
                iniFile.Write("debug", wsjtxClient.debug.ToString());
                iniFile.Write("windowPosX", this.Location.X.ToString());
                iniFile.Write("windowPosY", this.Location.Y.ToString());
                iniFile.Write("windowHt", this.Height.ToString());
                if (wsjtxClient.ipAddress != null) iniFile.Write("ipAddress", wsjtxClient.ipAddress.ToString());   //string
                if (wsjtxClient.port != 0) iniFile.Write("port", wsjtxClient.port.ToString());
                iniFile.Write("multicast", wsjtxClient.multicast.ToString());
                iniFile.Write("timeout", ((int)timeoutNumUpDown.Value).ToString());
                iniFile.Write("useDirected", callDirCqCheckBox.Checked.ToString());
                if (directedTextBox.Text == separateBySpaces) directedTextBox.Clear();
                iniFile.Write("directeds", directedTextBox.Text.Trim());
                iniFile.Write("playMyCall", mycallCheckBox.Checked.ToString());
                iniFile.Write("playLogged", loggedCheckBox.Checked.ToString());
                iniFile.Write("playCallAdded", callAddedCheckBox.Checked.ToString());
                iniFile.Write("useAlertDirected", replyDirCqCheckBox.Checked.ToString());
                if (alertTextBox.Text == separateBySpaces) alertTextBox.Clear();
                iniFile.Write("alertDirecteds", alertTextBox.Text.Trim());
                iniFile.Write("logEarly", logEarlyCheckBox.Checked.ToString());
                iniFile.Write("advanced", wsjtxClient.advanced.ToString());
                iniFile.Write("alwaysOnTop", alwaysOnTop.ToString());
                iniFile.Write("useRR73", wsjtxClient.useRR73.ToString());
                iniFile.Write("skipGrid", skipGridCheckBox.Checked.ToString());
                iniFile.Write("firstRun", firstRun.ToString());
                iniFile.Write("autoReplyNewCq", replyNewDxccCheckBox.Checked.ToString());
                iniFile.Write("enableReplyDx", replyDxCheckBox.Checked.ToString());
                iniFile.Write("enableReplyLocal", replyLocalCheckBox.Checked.ToString());
                iniFile.Write("diagLog", wsjtxClient.diagLog.ToString());
                bool mode = wsjtxClient.txMode == WsjtxClient.TxModes.LISTEN;
                iniFile.Write("replyAndQuit", mode.ToString());
                iniFile.Write("showTxModes", wsjtxClient.showTxModes.ToString());
                iniFile.Write("bestOffset", freqCheckBox.Checked.ToString());
                iniFile.Write("stopTxTime", stopTextBox.Text.Trim());
                iniFile.Write("startTxTime", startTextBox.Text.Trim());
                iniFile.Write("optimizeTx", optimizeCheckBox.Checked.ToString());
                iniFile.Write("timedOperationIdx", modeComboBox.SelectedIndex.ToString());
                if (exceptTextBox.Text == separateBySpaces) exceptTextBox.Clear();
                iniFile.Write("exceptCalls", exceptTextBox.Text.Trim());
                iniFile.Write("replyOnlyDxcc", replyNewOnlyCheckBox.Checked.ToString());
                iniFile.Write("callCqDx", callCqDxCheckBox.Checked.ToString());
                iniFile.Write("ignoreNonDx", ignoreNonDxCheckBox.Checked.ToString());
                iniFile.Write("callNonDirCq", callNonDirCqCheckBox.Checked.ToString());
                iniFile.Write("overrideUdpDetect", wsjtxClient.overrideUdpDetect.ToString());
                iniFile.Write("skipLevelPrompt", skipLevelPrompt.ToString());
                iniFile.Write("cqOnly", cqOnlyRadioButton.Checked.ToString());
                iniFile.Write("newOnBand", (bandComboBox.SelectedIndex == 1).ToString());
                iniFile.Write("myContinent", wsjtxClient.myContinent);
                iniFile.Write("rankMethod", wsjtxClient.rankMethodIdx.ToString());
                iniFile.Write("replyRR73", replyRR73CheckBox.Checked.ToString());
                iniFile.Write("cqGrid", cqGridRadioButton.Checked.ToString());
                iniFile.Write("anyMsg", anyMsgRadioButton.Checked.ToString());
                iniFile.Write("txPeriodIdx", periodComboBox.SelectedIndex.ToString());
                iniFile.Write("showOptions", showOptions.ToString());
            }

            CloseComm();
            if (guide != null) guide.Close();
            if (helpDlg != null) helpDlg.Close();
        }

        public void CloseComm()
        {
            if (mainLoopTimer != null) mainLoopTimer.Stop();
            mainLoopTimer = null;
            statusMsgTimer.Stop();
            initialConnFaultTimer.Stop();
            wsjtxClient.Closing();
        }

        private void Controller_FormClosed(object sender, FormClosedEventArgs e)
        {

        }

#if DEBUG
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
#endif

        private void mainLoopTimer_Tick(object sender, EventArgs e)
        {
            if (mainLoopTimer == null) return;
            wsjtxClient.UdpLoop();
        }

        private void statusMsgTimer_Tick(object sender, EventArgs e)
        {
            statusMsgTimer.Stop();
            if (wsjtxClient.showTxModes)
            {
                wsjtxClient.UpdateCallInProg();
            }
            else
            {
                msgTextBox.Text = "";
            }

        }

        private void initialConnFaultTimer_Tick(object sender, EventArgs e)
        {
            BringToFront();
            wsjtxClient.ConnectionDialog();
        }

        private void debugHighlightTimer_Tick(object sender, EventArgs e)
        {
            debugHighlightTimer.Stop();
            label17.ForeColor = Color.Black;
            label24.ForeColor = Color.Black;
            label25.ForeColor = Color.Black;
            label13.ForeColor = Color.Black;
            label10.ForeColor = Color.Black;
            label20.ForeColor = Color.Black;
            label21.ForeColor = Color.Black;
            label8.ForeColor = Color.Black;
            label19.ForeColor = Color.Black;
            label18.ForeColor = Color.Black;
            label12.ForeColor = Color.Black;
            label4.ForeColor = Color.Black;
            label14.ForeColor = Color.Black;
            label15.ForeColor = Color.Black;
            label16.ForeColor = Color.Black;
            label26.ForeColor = Color.Black;
            label27.ForeColor = Color.Black;
            label3.ForeColor = Color.Black;
            label1.ForeColor = Color.Black;
            label2.ForeColor = Color.Black;
            label28.ForeColor = Color.Black;
            label11.ForeColor = Color.Black;
        }

        private void timeoutNumUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            if (timeoutNumUpDown.Value < minSkipCount)
            {
                timeoutNumUpDown.Value = minSkipCount;
            }

            if (timeoutNumUpDown.Value > maxSkipCount)
            {
                timeoutNumUpDown.Value = maxSkipCount;
            }
            UpdateTxLabel();

            wsjtxClient.TxRepeatChanged();
            if (guide != null) guide.UpdateView();
        }

        private void UpdateTxLabel()
        {
            if (timeoutNumUpDown.Value == 1)
            {
                repeatLabel.Text = "Tx per msg";
            }
            else
            {
                repeatLabel.Text = "repeated Tx";
            }
        }

        private void replyDirCqCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            if (replyDirCqCheckBox.Checked) ignoreNonDxCheckBox.Checked = false;

            CheckManualSelection();

            alertTextBox.Enabled = replyDirCqCheckBox.Checked;
            if (replyDirCqCheckBox.Checked && replyNewDxccCheckBox.Checked) replyNewOnlyCheckBox.Checked = false;

            if (replyDirCqCheckBox.Checked && alertTextBox.Text == separateBySpaces)
            {
                alertTextBox.Clear();
                alertTextBox.ForeColor = System.Drawing.Color.Black;
            }
            if (!replyDirCqCheckBox.Checked && alertTextBox.Text == "") alertTextBox.Text = separateBySpaces;

            if (guide != null) guide.UpdateView();
        }

        private void replyNewDxccCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            updateReplyNewOnlyCheckBoxEnabled();

            if (replyNewDxccCheckBox.Checked && (replyDxCheckBox.Checked || replyLocalCheckBox.Checked || replyDirCqCheckBox.Checked)) replyNewOnlyCheckBox.Checked = false;

            if (!formLoaded) return;

            CheckManualSelection();

            if (guide != null) guide.UpdateView();
        }

        private void loggedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoaded && loggedCheckBox.Checked) wsjtxClient.Play("echo.wav");
        }

        private void mycallCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoaded && mycallCheckBox.Checked) wsjtxClient.Play("trumpet.wav");
        }

        private void verLabel_DoubleClick(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            if (!wsjtxClient.advanced || !showOptions) return;
            wsjtxClient.debug = !wsjtxClient.debug;
            UpdateDebug();
            if (formLoaded) wsjtxClient.DebugChanged();
        }

        private void UpdateDebug()
        {
            SuspendLayout();
            FormBorderStyle = FormBorderStyle.FixedSingle;
            if (wsjtxClient.debug)
            {
#if DEBUG
                AllocConsole();
                ShowWindow(GetConsoleWindow(), 5);
#endif
                Height = this.MaximumSize.Height;
                wsjtxClient.UpdateDebug();
                BringToFront();
            }
            else
            {
                if (wsjtxClient.advanced)
                {
                    if (!showOptions)
                    {
                        UpdateShowOptions();       //otherwise don't move controls
                    }
                    else
                    {
                        Height = setupButton.Location.Y + setupButton.Height + 45;
                    }
                }
                else
                {
                    statusText.Location = new Point(statusText.Location.X, 279);
                    setupButton.Location = new Point(setupButton.Location.X, 308);
                    verLabel.Location = new Point(verLabel.Location.X, 309);
                    verLabel2.Location = new Point(verLabel2.Location.X, 323);
                    msgTextBox.Location = new Point(msgTextBox.Location.X, 260);
                    inProgTextBox.Location = new Point(inProgTextBox.Location.X, 225);
                    inProgLabel.Location = new Point(inProgLabel.Location.X, 207);

                    limitLabel.Location = new Point(limitLabel.Location.X, 158);
                    timeoutNumUpDown.Location = new Point(timeoutNumUpDown.Location.X, 155);
                    repeatLabel.Location = new Point(repeatLabel.Location.X, 158);
                    LimitTxHelpLabel.Location = new Point(LimitTxHelpLabel.Location.X, 157);
                    playSoundLabel.Location = new Point(playSoundLabel.Location.X, 182);
                    callAddedCheckBox.Location = new Point(callAddedCheckBox.Location.X, 182);
                    mycallCheckBox.Location = new Point(mycallCheckBox.Location.X, 182);
                    loggedCheckBox.Location = new Point(loggedCheckBox.Location.X, 182);

                    //Height = (int)(this.MaximumSize.Height * 0.46);
                    Height = setupButton.Location.Y + setupButton.Height + 45;
                }
#if DEBUG
                ShowWindow(GetConsoleWindow(), 0);
#endif
            }
            ResumeLayout();
        }

        private void UpdateAdvancedCtrls()
        {
            replyDirCqCheckBox.Visible = true;
            alertTextBox.Visible = true;
            directedTextBox.Visible = true;
            callDirCqCheckBox.Visible = true;
            logEarlyCheckBox.Visible = true;
            useRR73CheckBox.Visible = true;
            skipGridCheckBox.Visible = true;
            addCallLabel.Visible = false;
            replyNewDxccCheckBox.Visible = true;
            exceptTextBox.Visible = true;
            UseDirectedHelpLabel.Visible = true;
            AlertDirectedHelpLabel.Visible = true;
            LogEarlyHelpLabel.Visible = true;
            PriorityHelpLabel.Visible = true;
            replyNormCqLabel.Visible = true;
            replyDxCheckBox.Visible = true;
            replyLocalCheckBox.Visible = true;
            ExcludeHelpLabel.Visible = true;
            AutoFreqHelpLabel.Visible = true;
            ReplyNewHelpLabel.Visible = true;
            StartHelpLabel.Visible = true;
            LimitTxHelpLabel.Visible = true;
            PeriodHelpLabel.Visible = true;
            freqCheckBox.Visible = true;
            startLabel.Visible = true;
            stopTextBox.Visible = true;
            timedCheckBox.Visible = true;
            startTextBox.Visible = true;
            timeLabel.Visible = true;
            timeLabel2.Visible = true;
            stopLabel.Visible = true;
            optimizeCheckBox.Visible = true;
            holdCheckBox.Visible = true;
            callCqDxCheckBox.Visible = true;
            callNonDirCqCheckBox.Visible = true;
            ignoreNonDxCheckBox.Visible = true;
            exceptLabel.Visible = true;
            callingLabel.Visible = true;
            replyingLabel.Visible = true;
            optionsLabel.Visible = true;
            cqOnlyRadioButton.Visible = true;
            IgnoreNonDxHelpLabel.Visible = true;
            replyRR73CheckBox.Visible = true;
            ReplyRR73HelpLabel.Visible = true;
            anyMsgRadioButton.Visible = true;
            cqGridRadioButton.Visible = true;
            forLabel.Visible = true;
            includeLabel.Visible = true;
            callLabel.Visible = true;
            bandComboBox.Visible = true;
            includeLabel.Visible = true;
            rankComboBox.Visible = true;
            guideLabel.Visible = true;
            blockHelpLabel.Visible = true;

            wsjtxClient.advanced = true;
            wsjtxClient.UpdateModeVisible();
        }

        private void skipGridCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            skipGridCheckBox.Text = "Skip grid (pending)";
            skipGridCheckBox.ForeColor = Color.DarkGreen;
            wsjtxClient.WsjtxSettingChanged();
        }

        private void useRR73CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            useRR73CheckBox.Text = "Use RR73 (pending)";
            useRR73CheckBox.ForeColor = Color.DarkGreen;
            wsjtxClient.WsjtxSettingChanged();
        }

        public void WsjtxSettingConfirmed()
        {
            skipGridCheckBox.Text = "Skip grid msg";
            skipGridCheckBox.ForeColor = Color.Black;
            useRR73CheckBox.Text = "Use RR73 msg";
            useRR73CheckBox.ForeColor = Color.Black;
        }

        public void setupButton_Click(object sender, EventArgs e)
        {
            initialConnFaultTimer.Stop();

            if (setupDlg != null)
            {
                setupDlg.BringToFront();
                return;
            }

            setupTimer.Tag = e == null;
            setupTimer.Start();        //show only UDP setup
        }

        private void setupTimer_Tick(object sender, EventArgs e)
        {
            setupTimer.Stop();
            setupDlg = new SetupDlg();
            setupDlg.wsjtxClient = wsjtxClient;
            setupDlg.ctrl = this;
            if ((bool)setupTimer.Tag) setupDlg.ShowUdpOnly();
            setupDlg.Show();
        }

        public void SetupDlgClosed()
        {
            initialConnFaultTimer.Start();
            TopMost = alwaysOnTop;
            setupDlg = null;
            wsjtxClient.suspendComm = false;
        }


        public void guideLabel_Click(object sender, EventArgs e)
        {
            initialConnFaultTimer.Stop();

            if (guide != null)
            {
                guide.BringToFront();
                return;
            }

            //guideTimer.Tag = e == null;
            guideTimer.Start();
        }

        private void guideTimer_Tick(object sender, EventArgs e)
        {
            guideTimer.Stop();
            guide = new Guide(wsjtxClient, this);
            //if ((bool)guideTimer.Tag) ;
            guide.Show();
            if (wsjtxClient.advanced && !wsjtxClient.showTxModes) firstRun = false;     //prevent showing guide automatically later
        }

        public void GuideClosed()
        {
            initialConnFaultTimer.Start();
            guide = null;
        }
        public void HelpClosed()
        {
            initialConnFaultTimer.Start();
            helpDlg = null;
        }

        private void addCallLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"Calls are replied to in order of importance:" +
                $"{nl}- New countries on any band*" +
                $"{nl}- New countries on current band*" +
                $"{nl}- Calls directed to {MyCall()}*" +
                $"{nl}- Calls you select manually*" +
                $"{nl}- CQs matching 'Reply to directed CQs'*" +
                $"{nl}- Calls matching 'Reply to new calls', ranked by the selected 'Reply priority'." +
                $"{nl}{nl}(*ranked in the order received, within each type)" +
                $"{nl}{nl}To manually add more call signs to the 'Calls waiting reply' list:" +
                $"{nl}- Press and hold the 'Alt' key, then" +
                $"{nl}- Double-click on the line containing the desired 'from' call sign in the WSJT-X 'Band Activity' list." +
                $"{nl}{nl}To remove a call sign from the 'Calls waiting reply' list:" +
                $"{nl}- Right-click on the call, then confirm." +
                $"{nl}{nl}To reply to any call from the 'Calls waiting reply' list:" +
                $"{nl}- Double-click on the call." +
                $"{nl}{nl}To cancel the current call when the 'Calls waiting reply' list is empty:" +
                $"{nl}- Double-click on the reply list box." +
                $"{nl}{nl}When you double-click on a call in the WSJT-X 'Band Activity list *without* using the 'Alt' key:" +
                $"{nl}- This causes an immediate reply, instead of placing the call on the 'Calls waiting reply' list." +
                $"{nl}- Automatic operation continues after this call is processed." +
                $"{nl}{nl}Note:" +
                $"{nl}- If 'Reply priority' is set to 'Best for ... beam', calls that are off the nominal azimuth by more than {WsjtxClient.beamWidth / 2} degrees are not added to the 'Calls waiting reply' list." +
                $"{nl}- The '*' symbol denotes a call from a new country." +
                $"{nl}{nl}You can leave this dialog open while you try out these hints.");
        }

        public void ShowMsg(string text, bool sound)
        {
            if (sound)
            {
                SystemSounds.Beep.Play();
            }

            statusMsgTimer.Stop();
            msgTextBox.Text = text;
            statusMsgTimer.Start();
        }

        private void IncludeHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"The 'Reply to new calls' section allows you to choose which messages from new callers you want to add to the 'Calls waiting reply' list." +
                $"{nl}{nl}- Select 'CQ' if you want to reply only to CQ messages." +
                $"{nl}- Select 'CQ/grid' if you want to reply only to messages with grid information, allowing you to prioritize calls based on distance or azimuth." +
                $"{nl}- Select 'any' to reply to any message." +
                $"{nl}{nl}Note: The selections here don't affect replies to 'new countries' or 'new countries on band', which are enabled when 'Reply to new DXCC' is selected.");
        }

        private void IgnoreNonDxHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"When calling 'CQ DX', select 'Ignore non-DX reply' to disable replying to calls to {MyCall()} from continents other than your continent." +
                $"{nl}{nl}This also disables replies to calls not directed to {MyCall()}.");
        }

        private void UseDirectedHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"To send directed CQs:{nl}" +
                $"- Enter the code(s) for the directed CQs you want to transmit (2 to 4 letters each), separated by spaces." +
                $"{nl}- Don't enter 'DX' here." +
                $"{nl}{nl}The directed CQs will be used in random order." +
                $"{nl}{nl}Example: EU SA OC");
        }

        private void AlertDirectedHelpLabel_Click(object sender, EventArgs e)
        {
            string continent = wsjtxClient.myContinent == null ? "" : $" '{wsjtxClient.myContinent}'";
            ShowHelp($"To reply to specific directed CQs from callers you haven't worked yet:" +
                $"{nl}- Enter the code(s) for the directed CQs (2 to 4 letters each), separated by spaces." +
                $"{nl}{nl}Example: POTA WY" +
                $"{nl}{nl}If you enter 'DX', there will be no reply if the caller is on your continent." +
                $"{nl}{nl}There is no need to enter 'DX' or your continent{continent} if you have selected 'DX' and 'CQ/73' at 'Reply to new calls'." +
                $"{nl}{nl}(Note: 'CQ POTA' is an exception to the 'already worked' rule, these calls will cause an auto-reply if you haven't already logged that call in the current mode/band in the current day).");
        }

        private void LogEarlyHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"To maximize the chance of completed QSOs, consider 'early logging':" +
                $"{nl}{nl}" +
                $"The defining requirement for any QSO is the exchange of call signs and signal reports." +
                $"{nl}Once either party sends an 'RRR' message (and reports have been exchanged), those requirements have been met... a '73' is not necessary for logging the QSO." +
                $"{nl}{nl}Note that the QSO will continue after early logging, completing when 'RR73' or '73' is sent, or '73' is received." +
                $"{nl}{nl}New countries are an exception to early logging. In this case, logging is only after confirmation with a '73' or 'RR73'.");
        }

        private void verLabel2_Click(object sender, EventArgs e)
        {
            string command = "https://github.com/avantol/Otto/releases";
            System.Diagnostics.Process.Start(command);
        }

        private void ExcludeHelpLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.UpdateMaxAutoGenEnqueue();
            string continent = wsjtxClient.myContinent == null ? "" : $" '{wsjtxClient.myContinent}'";
            string onBand = $"{bandComboBox.Items[1]}";
            ShowHelp($"{friendlyName} will add up to {wsjtxClient.maxAutoGenEnqueue} calls to the 'Calls waiting reply' list that meet these conditions:" +
                $"{nl}{nl}- The call has not been worked before 'for 1 band' or '{onBand}'." +
                $"{nl}- The call is 'DX' or originated in your continent{continent}." +
                $"{nl}- The received message can be" +
                $"{nl}     * CQ, 73 or RR73 (the best time to reply), or" +
                $"{nl}     * grid information (for distance calculation), or" +
                $"{nl}     * any type (for maximum number of replies)." +
                $"{nl}- The caller is on your Rx time slot (if in 'Call CQ' mode)." +
                $"{nl}- The caller hasn't been replied to more than {wsjtxClient.maxPrevTo} times during this mode / band session." +
                $"{nl}{nl}If you select 'DX', {friendlyName} will reply to calls from continents other than yours." +
                $"{nl}{nl}For example, this is useful in case you've already worked all states/entities on your continent, and only want to reply to calls you haven't worked yet from other continents." +
                $"{nl}{nl}- If you select your continent{continent}, {friendlyName} will reply only to those calls." +
                $"{nl}{nl}For example, this is useful in case you're running QRP, and expect you can't be heard on other continents, and only want to reply to calls from your continent." +
                $"{nl}{nl}Select 'for 1 band' if you want to reply to calls you haven't worked before, but only need new calls on one band. Select '{onBand}' to also reply to calls that you haven't worked before on the current band." +
                $"{nl}{nl}Note: If you have entered 'directed CQs' to reply to, those CQs will be replied to regardless of the 'DX',{continent}, 'from messages', or new 'for 1 band' or '{onBand}' settings here.");
        }

        private void modeHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"Choose what you want this progam to do after replying to all queued calls:" +
                $"{nl}{nl}- Call CQ until there is a reply, and automatically complete each contact," +
                $"{nl}or" +
                $"{nl}- Listen for CQs or other interesting calls, and automatically reply to them." +
                $"{nl}{nl}The advantage to listening is that you can monitor both odd and even Rx time slots. This is helpful for maximizing POTA or new country QSOs, for example." +
                $"{nl}{nl}(Note: If you choose 'Listen for calls', be sure to select a large enough number of retries in 'Limit to...repeated Tx' so that the stations you call have a chance to reply before any automatic switch to the opposite time slot)." +
                $"{nl}{nl}Shortcut keys:" +
                $"{nl}    Esc:  Stop transmit immediately" +
                $"{nl}    Ctrl+Q:  Show/hide Quick-start setup" +
                $"{nl}    Ctrl+O:  Show/hide complete Options" +
                $"{nl}    Ctrl+C:  Call CQ" +
                $"{nl}    Ctrl+L:  Listen for calls" +
                $"{nl}    Ctrl+D:  Delete 'Calls waiting reply' list" +
                $"{nl}    Ctrl+N:  Skip to next call (cancel if none)" +
                $"{nl}    Alt+C:  Configuration");
        }

        public void cqModeButton_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.TxModeChanged(WsjtxClient.TxModes.CALL_CQ);
            if (guide != null) guide.UpdateView();
        }

        public void listenModeButton_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.TxModeChanged(WsjtxClient.TxModes.LISTEN);
            if (guide != null) guide.UpdateView();
        }

        private void freqCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.WsjtxSettingChanged();
            wsjtxClient.AutoFreqChanged(freqCheckBox.Checked, false);
            if (guide != null) guide.UpdateView();
        }

        private void LimitTxHelpLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            string adv = wsjtxClient != null && wsjtxClient.advanced ? $"{nl}{nl}If 'Optimize' is selected, the maximum number of replies and CQs for the current call is automatically adjusted lower than the specified limit (if possible), to help process the call queue faster." +
                $"{nl}{nl}If 'Hold' is selected, the 'Repeated Tx' limit is ignored, and replies to the current call sign are transmitted a maximum of {wsjtxClient.holdMaxTxRepeat} times. 'Hold' is automatically enabled when processing a 'new DXCC', deselect 'Reply to new DXCC' to prevent this action." : "";
            ShowHelp($"This will limit the number of times the same message is transmitted." +
                $"{nl}{nl}For example, it will limit the number of repeated transmitted replies or CQs for the current call. If there is no response to your reply messages when the limit is reached, the next call in the queue is processed (or if the call queue is empty, CQing (or listening) will resume)." +
                $"{nl}{nl}As the repeat limit is reduced, the number of times a call can be automatically re-added to the call queue is increased, to compensate.{adv}");
        }

        private void optimizeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoaded) wsjtxClient.TxRepeatChanged();
        }

        private void timedCheckBoxChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.timedCheckBoxChanged();
        }

        private void startTextBox_TextChanged(object sender, EventArgs e)
        {
            timedCheckBox.Checked = false;
        }

        private void stopTextBox_TextChanged(object sender, EventArgs e)
        {
            timedCheckBox.Checked = false;
        }

        private void StartHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"Leave 'Start' time blank if you want to start or continue transmitting now, and stop at a specified time.");
        }

        private void holdCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.HoldCheckBoxChanged();
        }

        private void directedTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.KeyChar = char.ToUpper(e.KeyChar);
            char c = e.KeyChar;
            if (c == (char)Keys.Back || c == ' ' || (c >= 'A' && c <= 'Z')) return;
            Console.Beep();
            e.Handled = true;
        }

        private void alertTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.KeyChar = char.ToUpper(e.KeyChar);
            char c = e.KeyChar;
            if (c == (char)Keys.Back || c == ' ' || (c >= 'A' && c <= 'Z')) return;
            Console.Beep();
            e.Handled = true;
        }

        private void startTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (startTextBox.SelectionLength > 0)
            {
                int start = startTextBox.SelectionStart;
                startTextBox.SelectedText = "";
                startTextBox.SelectionStart = start;
                startTextBox.SelectionLength = 0;
            }
            char c = e.KeyChar;
            if (c == (char)Keys.Back || ((c >= '0' && c <= '9') && startTextBox.Text.Length < 4)) return;
            Console.Beep();
            if (startTextBox.Text.Length == 4) ShowMsg("Enter only 4 digits", false);
            e.Handled = true;
        }

        private void stopTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (stopTextBox.SelectionLength > 0)
            {
                int start = stopTextBox.SelectionStart;
                stopTextBox.SelectedText = "";
                stopTextBox.SelectionStart = start;
                stopTextBox.SelectionLength = 0;
            }
            char c = e.KeyChar;
            if (c == (char)Keys.Back || ((c >= '0' && c <= '9') && stopTextBox.Text.Length < 4)) return;
            Console.Beep();
            if (stopTextBox.Text.Length == 4) ShowMsg("Enter only 4 digits", false);
            e.Handled = true;
        }

        private void ReplyNewLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            string s = "";
            if (wsjtxClient.showTxModes) s = $"{nl}{nl}If you select 'Exclusively', only new countries will be replied to, and all other calls will be ignored. (This option is only available when using the 'Listen for calls' operating mode).";
            ShowHelp($"Select 'Reply to new DXCC' to repeat replies more times than normal, for any message type, from new countries." +
                $"{nl}{nl}This option is intended ONLY when replying to difficult stations that are likely to have many competing callers, like DXpeditions. It's NOT suitable at all for working the more common DX entities!" +
                $"{nl}{nl}If a call sign is from a country never worked before on any band, {friendlyName} will sound an audio notification and 'hold' (repeat) transmissions to that call sign for a maximum of {wsjtxClient.holdMaxTxRepeat} times." +
                $"{nl}{nl}If a call sign is from a country not worked before on the current band, {friendlyName} will sound an audio notification and 'hold' (repeat) transmissions to that call sign for a maximum of {wsjtxClient.holdMaxTxRepeatNewOnBand} times." +
                $"{nl}{nl}If a station never replies or won't confirm QSOs conveniently, you can add that call sign to the 'Block any reply' list, and it will be ignored.{s}" +
                $"{nl}{nl}If you don't select 'Reply to new DXCC', you will still be able to reply automatically to messages from new countries, just with the normal number of Tx repeats.");
        }

        private void ReplyRR73HelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"Select 'Reply to RR73 msg' if you want to reply '73' to an RR73 message received at the end of a QSO." +
                $"{nl}{nl}'RR73' means:" +
                $"{nl}- 'Signal report received', and" +
                $"{nl}- 'Best regards', and" +
                $"{nl}- 'I'm confident you will see this', so" +
                $"{nl}- 'No further reply requested'." +
                $"{nl}{nl}You can safely skip replying to 'RR73' to speed up the QSO cycle, if conditions allow." +
                $"{nl}{nl}Exceptions:" +
                $"{nl}- If from a new country, RR73 is always replied to with a '73'." +
                $"{nl}- If a Fox/Hound-style (multi-stream) 'RR73' message, no '73' is expected by the caller, so it's not sent.");
        }

        private void PeriodHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"'Tx period' allows you to select which period you want WSJT-X to use for transmit when in 'Listen for calls' mode." +
                $"{nl}{nl}If you are using multiple transmitters at your station, you may want for all of them to use the same Tx period, to avoid interference." +
                $"{nl}{nl}Otherwise, the normal selection is 'any'.");
        }

        private void PriorityHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"Select the order to reply to messages specified in the 'Reply to new calls' section:" +
                $"{nl}{nl}- 'Most recent' (when combined with 'Repeated Tx' set to '1' allows immediate replies to a different new message in every Rx period... VERY effective! Select 'CQ/73' to reply to a caller the instant the caller is known to be available for a new QSO." +
                $"{nl}{nl}- 'Order received' forms a 'first-in-first-out queue', where the oldest message is replied to first, so wait times for callers are equalized." +
                $"{nl}{nl}- The 'Best for...beam' selections allow messages from callers closest to the specified beam heading to be answered first. Messages from callers to the side and back of your beam heading are ignored. This selection works best when grid information is available from messages, by selecting 'CD/grid'." +
                $"{nl}{nl}Note that the ordering of calls to {MyCall()}, directed CQs, and new DXCCs are not affected by this option; these calls always replied to before the calls specified in this 'Reply to new calls' section.");
        }

        private void AutoFreqHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"The Tx audio frequency is automatically set to an unused part of the audio spectrum." +
                $"{nl}{nl}After a period of no replies being received, transmitting is temporarily suspended for one Tx cycle, the received audio is re-sampled, and the best Tx frequency is re-calculated.");
        }

        private void blockHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"To block any automatic replies to a specific call sign:" +
                $"{nl}{nl}If the call sign is in the 'Calls waiting reply' list:" +
                $"{nl}- Hold the 'Ctrl' key down and click on the call sign." +
                $"{nl}{nl}Otherwise," +
                $"{nl}- Enter the call sign in the 'Block any reply' box, with each call sign separated by a space." +
                $"{nl}{nl}Note: If you manually select a blocked call, it will be unblocked to allow replies.");
        }

        private void callAddedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoaded && callAddedCheckBox.Checked) wsjtxClient.Play("blip.wav");
        }

        private void exceptTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.KeyChar = char.ToUpper(e.KeyChar);
            char c = e.KeyChar;
            if (c == (char)Keys.Back || c == ' ' || c == '/' || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return;
            Console.Beep();
            e.Handled = true;
        }

        private void msgTextBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (!formLoaded || Control.ModifierKeys != Keys.Control) return;

            if (e.Button == MouseButtons.Left)
            {
                //available for ctrl/left-click action
            }
            else
            {
                //available for ctrl/right-click action
            }
        }

        private void callCqDxCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ignoreNonDxCheckBox.Enabled = callCqDxCheckBox.Checked;

            if (callDirCqCheckBox.Checked || callNonDirCqCheckBox.Checked || replyDirCqCheckBox.Checked || replyDxCheckBox.Checked || replyLocalCheckBox.Checked)
            {
                if (callCqDxCheckBox.Checked) ignoreNonDxCheckBox.Checked = false;
            }

            ValidateDirCqTextBox();
            if (!callCqDxCheckBox.Checked && !callDirCqCheckBox.Checked && !callNonDirCqCheckBox.Checked)
            {
                callNonDirCqCheckBox.Checked = true;
            }

            if (guide != null) guide.UpdateView();
        }

        private void directedTextBox_Leave(object sender, EventArgs e)
        {
            if (directedTextBox.Text == separateBySpaces) return;

            ValidateDirCqTextBox();

            if (directedTextBox.Text == "")
            {
                callDirCqCheckBox.Checked = false;
                return;
            }
        }

        private void ignoreNonDxCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ignoreNonDxCheckBox.Checked)
            {
                callDirCqCheckBox.Checked = false;
                callNonDirCqCheckBox.Checked = false;
                replyDirCqCheckBox.Checked = false;
                replyLocalCheckBox.Checked = false;
                replyDxCheckBox.Checked = false;
            }
        }

        private void callDirCqCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            directedTextBox.Enabled = callDirCqCheckBox.Checked;
            if (callDirCqCheckBox.Checked && directedTextBox.Text == separateBySpaces)
            {
                ignoreDirectedChange = true;
                directedTextBox.Clear();
                directedTextBox.ForeColor = System.Drawing.Color.Black;
            }
            if (!callDirCqCheckBox.Checked && directedTextBox.Text == "") directedTextBox.Text = separateBySpaces;

            if (callDirCqCheckBox.Checked)
            {
                if (callCqDxCheckBox.Checked) ignoreNonDxCheckBox.Checked = false;
            }
            else
            {
                if (!callCqDxCheckBox.Checked)
                {
                    callNonDirCqCheckBox.Checked = true;
                }
            }
            wsjtxClient.WsjtxSettingChanged();              //resets CQ to not directed

            if (guide != null) guide.UpdateView();
        }

        private void callNonDirCqCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (callNonDirCqCheckBox.Checked)
            {
                if (callCqDxCheckBox.Checked) ignoreNonDxCheckBox.Checked = false;
            }
            else
            {
                ValidateDirCqTextBox();
                if (!callCqDxCheckBox.Checked && !callDirCqCheckBox.Checked)
                {
                    callNonDirCqCheckBox.Checked = true;
                }
            }
            if (formLoaded) wsjtxClient.WsjtxSettingChanged();              //resets CQ to non-directed

            if (guide != null) guide.UpdateView();
        }

        private void alertTextBox_Leave(object sender, EventArgs e)
        {
            ValidateAlertTextBox();
        }

        private void ValidateAlertTextBox()
        {
            if (alertTextBox.Text == separateBySpaces) return;

            var dirArray = alertTextBox.Text.Trim().ToUpper().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string corrText = "";
            string delim = "";
            foreach (string dir in dirArray)
            {
                if (dir.Length >= 2 && dir.Length <= 4) corrText = corrText + delim + dir;
                delim = " ";
            }
            alertTextBox.Text = corrText;
        }

        private void replyNewOnlyCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (replyNewOnlyCheckBox.Checked && replyNewDxccCheckBox.Checked)
            {
                replyDirCqCheckBox.Checked = false;
                replyDxCheckBox.Checked = false;
                replyLocalCheckBox.Checked = false;
            }

            if (formLoaded) wsjtxClient.ReplyNewOnlyChanged();

            if (guide != null) guide.UpdateView();
        }

        private void ValidateDirCqTextBox()
        {
            if (directedTextBox.Text == separateBySpaces) return;

            string text = directedTextBox.Text.Replace("*", "");        //obsoleted
            var dirArray = text.Trim().ToUpper().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string corrText = "";
            string delim = "";
            foreach (string dir in dirArray)
            {
                if (dir.Length >= 2 && dir.Length <= 4)
                {
                    corrText = corrText + delim + dir;
                    delim = " ";
                }
            }
            directedTextBox.Text = corrText;

            if (corrText == "") callDirCqCheckBox.Checked = false;
        }

        public void updateReplyNewOnlyCheckBoxEnabled()
        {
            if (formLoaded && wsjtxClient.showTxModes)
            {
                replyNewOnlyCheckBox.Visible = true;
            }

            if (!replyNewDxccCheckBox.Checked)
            {
                replyNewOnlyCheckBox.Enabled = false;
                return;
            }

            if (formLoaded) replyNewOnlyCheckBox.Enabled = listenModeButton.Checked;
        }

        private void useRR73CheckBox_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.useRR73 = useRR73CheckBox.Checked;
        }

        private void replyLocalCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (replyLocalCheckBox.Checked) ignoreNonDxCheckBox.Checked = false;
            if (replyLocalCheckBox.Checked && replyNewDxccCheckBox.Checked) replyNewOnlyCheckBox.Checked = false;

            UpdateCqNewOnBand();
            CheckManualSelection();
            if (guide != null) guide.UpdateView();
        }

        private void CheckManualSelection()
        {
            if (formLoaded && wsjtxClient.showTxModes && listenModeButton.Checked && !replyDxCheckBox.Checked && !replyLocalCheckBox.Checked && !replyDirCqCheckBox.Checked && !replyNewDxccCheckBox.Checked && !replyDxCheckBox.Checked && !replyLocalCheckBox.Checked & !replyNewDxccCheckBox.Checked)
            {
                ShowMsg($"Select calls manually in WSJT-X (alt/dbl-click)", true);
            }
        }

        private void replyDxCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (replyDxCheckBox.Checked) ignoreNonDxCheckBox.Checked = false;
            if (replyDxCheckBox.Checked && replyNewDxccCheckBox.Checked) replyNewOnlyCheckBox.Checked = false;

            UpdateCqNewOnBand();
            CheckManualSelection();
            if (guide != null) guide.UpdateView();
        }

        private void Controller_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Q)
            {
                guideLabel_Click(null, null);
            }

            if (e.Control && e.KeyCode == Keys.O)
            {
                optionsButton_Click(null, null);
            }

            if (e.Alt && e.KeyCode == Keys.C)
            {
                setupButton_Click(null, null);
            }

            if (e.Alt && e.KeyCode == Keys.D)           //debug toggle
            {
                verLabel_DoubleClick(null, null);
            }

            if (!formLoaded) return;

            if (e.KeyCode == Keys.Escape)               //halt Tx immediately
            {
                if (wsjtxClient.ConnectedToWsjtx())
                {
                    wsjtxClient.Pause(true);
                    ShowMsg("Tx halted", true);
                }
            }

            if (e.Control && e.KeyCode == Keys.C)
            {
                if (wsjtxClient.ConnectedToWsjtx()) cqModeButton_Click(null, null);
            }

            if (e.Control && e.KeyCode == Keys.L)
            {
                if (wsjtxClient.ConnectedToWsjtx()) listenModeButton_Click(null, null);
            }

            if (e.Control && e.KeyCode == Keys.N)       //next call or cancel current
            {
                if (wsjtxClient.ConnectedToWsjtx()) wsjtxClient.NextCall(false, callListBox.SelectedIndex);
            }

            if (e.Control && e.KeyCode == Keys.D)       //clear call queue
            {
                if (wsjtxClient.ConnectedToWsjtx()) wsjtxClient.ClearCallQueue();
            }
        }

        public void ShowHelp(string s)
        {
            helpTimer.Tag = s;
            helpTimer.Start();
        }

        private void helpTimer_Tick(object sender, EventArgs e)
        {
            helpTimer.Stop();
            if (helpDlg != null) helpDlg.Close();
            helpDlg = new HelpDlg(this, $"{wsjtxClient.pgmName}{helpSuffix}", (string)helpTimer.Tag);
            helpDlg.Show();
        }

        private void cqModeButton_CheckedChanged(object sender, EventArgs e)
        {
            updateReplyNewOnlyCheckBoxEnabled();
        }

        private void listenModeButton_CheckedChanged(object sender, EventArgs e)
        {
            updateReplyNewOnlyCheckBoxEnabled();
        }

        private void DeleteTextBoxSelection(TextBox textBox)
        {
            if (textBox.SelectionLength > 0)
            {
                int start = textBox.SelectionStart;
                string sel = textBox.Text.Substring(textBox.SelectionStart, textBox.SelectionLength);
                textBox.Text = textBox.Text.Replace(sel, "");
                textBox.SelectionLength = 0;
            }
        }

        private void UpdateCqNewOnBand()
        {
            anyMsgRadioButton.Enabled = cqGridRadioButton.Enabled = cqOnlyRadioButton.Enabled = bandComboBox.Enabled = replyDxCheckBox.Checked || replyLocalCheckBox.Checked;
        }

        private void rankComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.RankMethodIdxChanged(rankComboBox.SelectedIndex);
            if (guide != null) guide.UpdateView();
        }

        private void callListBox_MouseDown(object sender, MouseEventArgs e)
        {
            mouseEventArgs = e;
            listBoxClickCount++;
            callListBoxClickTimer.Start();
        }

        private void callListBoxClickTimer_Tick(object sender, EventArgs e)
        {
            callListBoxClickTimer.Stop();
            bool dblClk = listBoxClickCount > 1;
            listBoxClickCount = 0;
            ProcessCallListBoxAnyClick(dblClk);
        }

        private void ProcessCallListBoxAnyClick(bool dblClk)
        {
            if (!formLoaded) return;

            int idx = callListBox.IndexFromPoint(mouseEventArgs.Location);

            if (mouseEventArgs.Button == MouseButtons.Right)
            {
                if (Control.ModifierKeys == Keys.Control)
                {
                    if (idx < 0 || callListBox.SelectionMode == SelectionMode.None) return;
                    //available for ctrl/right-click action
                }
                else   //right-click (no modifier)
                {
                    if (idx >= 0 && idx < callListBox.Items.Count && callListBox.SelectionMode != SelectionMode.None) callListBox.SelectedIndex = idx;
                    wsjtxClient.EditCallQueue(idx);
                }
            }
            else   //left-click
            {
                if (dblClk)   //left-dbl-click (no modifier)
                {
                    wsjtxClient.NextCall(false, idx);
                }
                else
                {
                    if (idx < 0 || callListBox.SelectionMode == SelectionMode.None) return;

                    if (Control.ModifierKeys == Keys.Control)
                    {
                        wsjtxClient.BlockCall(idx);
                    }
                }
            }
        }

        private string MyCall()
        {
            return (wsjtxClient == null || wsjtxClient.myCall == null) ? "my call" : wsjtxClient.myCall;
        }

        private void cqOnlyRadioButton_Click(object sender, EventArgs e)
        {
            anyMsgRadioButton.Checked = cqGridRadioButton.Checked = false;
        }

        private void cqGridRadioButton_Click(object sender, EventArgs e)
        {
            anyMsgRadioButton.Checked = cqOnlyRadioButton.Checked = false;
        }

        private void anyMsgRadioButton_Click(object sender, EventArgs e)
        {
            cqGridRadioButton.Checked = cqOnlyRadioButton.Checked = false;
        }

        private void periodComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.TxPeriodIdxChanged(periodComboBox.SelectedIndex);
            if (guide != null) guide.UpdateView();
        }

        private void directedTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ignoreDirectedChange)
            {
                ignoreDirectedChange = false;
                return;           //was cleared initially
            }
            if (directedTextBox.Text == "") callDirCqCheckBox.Checked = false;
            if (guide != null) guide.UpdateView();
        }

        public void GuideListenMode()
        {
            listenModeButton_Click(null, null);
            periodComboBox.SelectedIndex = (int)WsjtxClient.ListenModeTxPeriods.ANY;
        }

        public void GuideCqMode()
        {
            cqModeButton_Click(null, null);
        }
        public void ToggleDx()
        {
            replyDxCheckBox.Checked = !replyDxCheckBox.Checked;
        }

        public void ToggleLocal()
        {
            replyLocalCheckBox.Checked = !replyLocalCheckBox.Checked;
        }

        public void ToggleActivator()
        {
            ValidateDirCqTextBox();
            if (directedTextBox.Text == separateBySpaces || directedTextBox.Text == "") directedTextBox.Text = " ";
            if (directedTextBox.Text == "POTA" && callDirCqCheckBox.Checked && !callCqDxCheckBox.Checked && !callNonDirCqCheckBox.Checked)
            {
                directedTextBox.Text = directedTextBox.Text = "";
                callDirCqCheckBox.Checked = false;
            }
            else
            {
                directedTextBox.Text = "POTA";
                callDirCqCheckBox.Checked = true;
                callCqDxCheckBox.Checked = callNonDirCqCheckBox.Checked = false;
            }
            ValidateDirCqTextBox();
        }
        public void ToggleHunter()
        {
            bool origState = replyDirCqCheckBox.Checked;
            ValidateAlertTextBox();
            if (alertTextBox.Text == separateBySpaces || alertTextBox.Text == "") alertTextBox.Text = " ";
            if (alertTextBox.Text.Contains("POTA") && replyDirCqCheckBox.Checked)
            {
                alertTextBox.Text = alertTextBox.Text.Replace("POTA", "");
                if (alertTextBox.Text.Length == 0) replyDirCqCheckBox.Checked = false;
            }
            else
            {
                if (!alertTextBox.Text.Contains("POTA")) alertTextBox.Text = $"{alertTextBox.Text} POTA";
                replyDirCqCheckBox.Checked = true;
            }
            ValidateAlertTextBox();
        }

        private void alertTextBox_TextChanged(object sender, EventArgs e)
        {
            if (guide != null) guide.UpdateView();
        }

        private void optionsButton_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            showOptions = !showOptions;
            if (showOptions) Hide();
            SuspendLayout();
            UpdateShowOptions();
            ResumeLayout();
            if (showOptions) Show();
        }

        private void UpdateShowOptions()
        {
            optionsButton.Text = (showOptions ? "Hide options" : "Show all options");

            int offset = optionsOffset;
            if (!showOptions) offset = -offset;

            if (showOptions)
            {
                //restore the movable controls to original location
                for (int i = 0; i < movableCtrlYs.Length; i++)
                {
                    movableCtrls[i].Location = new Point(movableCtrls[i].Location.X, movableCtrlYs[i]);
                }
            }
            else
            {
                //move the movable controls to new location
                for (int i = 0; i < movableCtrls.Length; i++)
                {
                    movableCtrls[i].Location = new Point(movableCtrls[i].Location.X, movableCtrls[i].Location.Y + offset);
                }
            }

            foreach (Control control in hideCtrls)
            {
                control.Visible = showOptions;
            }

            Height = (wsjtxClient.debug && showOptions ? this.MaximumSize.Height : setupButton.Location.Y + setupButton.Height + 45);
        }

        public string[] CallDirCqEntries()
        {
            ValidateDirCqTextBox();
            return directedTextBox.Text.Trim().ToUpper().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public string[] ReplyDirCqEntries()
        {
            ValidateAlertTextBox();
            return alertTextBox.Text.Trim().ToUpper().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void replyRR73CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoaded) wsjtxClient.ReplyRR73Changed(replyRR73CheckBox.Checked);
        }

        private void exceptTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            if (exceptTextBox.Text == separateBySpaces || exceptTextBox.Text.Trim() == "" || ignoreExceptChange) return;
            wsjtxClient.BlockedTextChanged(exceptTextBox.Text);
        }

        public bool ExceptTextBoxRemove(string call)
        {
            if (call == null || !exceptTextBox.Text.Contains(call)) return false;

            exceptTextBox_Enter(null, null);
            exceptTextBox.Text = exceptTextBox.Text.Replace(call, "");      //triggers exceptTextBox_TextChanged()
            exceptTextBox_Leave(null, null);
            return true;
        }

        public void ExceptTextBoxAdd(string call)
        {
            //call known to be non-null
            exceptTextBox_Enter(null, null);
            exceptTextBox.Text = $"{call} {exceptTextBox.Text}";      //triggers exceptTextBox_TextChanged()
            exceptTextBox_Leave(null, null);
        }

        private void exceptTextBox_Enter(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            exceptTextBox.ForeColor = Color.Black;
            if (exceptTextBox.Text == separateBySpaces)
            {
                exceptTextBox.Text = "";
            }
        }

        private void exceptTextBox_Leave(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            exceptTextBox.ForeColor = Color.Black;

            StringBuilder sb = new StringBuilder();
            string sep = "";
            var blockedCalls = exceptTextBox.Text.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
            foreach (string call in blockedCalls)
            {
                sb.Append($"{sep}{call}");
                sep = " ";
            }

            ignoreExceptChange = true;
            exceptTextBox.Text = sb.ToString();
            ignoreExceptChange = false;

            if (exceptTextBox.Text == "")
            {
                exceptTextBox.Text = separateBySpaces;
                exceptTextBox.ForeColor = Color.Gray;
            }
        }
    }
}



