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
using System.Diagnostics;

namespace WSJTX_Controller
{
    public partial class Controller : Form
    {
        private const int WM_SETREDRAW = 0x000B;
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public WsjtxClient wsjtxClient;
        public Guide guide;
        public bool alwaysOnTop = false;
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
        internal bool callingExpanded = true;
        internal bool replyingExpanded = true;
        internal bool sequenceExpanded = true;
        internal bool generalExpanded = true;
        private Control[][] sectionControls;
        private int[][] sectionCtrlYs;
        private Label[] sectionHeaders;
        private int[] sectionHeaderOrigYs;
        private int[] sectionHeights;
        private Control[] bottomCtrls;
        private int[] bottomCtrlOrigYs;
        private const int sectionHeaderHeight = 18;
        private string helpSuffix = " Help";
        private bool ignoreExceptChange = false;
        public bool darkMode = false;

        private System.Windows.Forms.Timer mainLoopTimer;

        public System.Windows.Forms.Timer statusMsgTimer;
        public System.Windows.Forms.Timer initialConnFaultTimer;
        public System.Windows.Forms.Timer debugHighlightTimer;
        public System.Windows.Forms.Timer setupTimer;
        public System.Windows.Forms.Timer guideTimer;
        public System.Windows.Forms.Timer callListBoxClickTimer;
        public System.Windows.Forms.Timer helpTimer;

        private string nl = Environment.NewLine;
        //private static string alphanumericOnly = "[^0-9A-Za-z]";  //match if any non-alphanumeric
        //private static string alphaOnly = "[^A-Za-z]";         //match if any numeric
        //private static string numericOnly = "[^0-9]";          //match if any alpha
        private string fileVer;
        private const string basicOnlyFileVer = "1.2";

        public Controller()
        {
            InitializeComponent();
            friendlyName = Text;
            KeyPreview = true;

            string allVer = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            Version v;
            Version.TryParse(allVer, out v);
            fileVer = $"{v.Major}.{v.Minor}";

            showCloseMsgs = !IsBasicOnly();

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

            // Section headers
            sectionHeaders = new Label[] { callingLabel, replyingLabel, sequenceLabel, optionsLabel };
            sectionHeaderOrigYs = new int[4];
            for (int i = 0; i < 4; i++)
            {
                sectionHeaderOrigYs[i] = sectionHeaders[i].Location.Y;
                sectionHeaders[i].Cursor = Cursors.Hand;
            }
            callingLabel.Text = "\u25b2 Calling options";
            replyingLabel.Text = "\u25b2 Replying options";
            sequenceLabel.Text = "\u25b2 Sequence options";
            optionsLabel.Text = "\u25b2 General options";

            // Section controls (excluding headers)
            sectionControls = new Control[4][];
            sectionControls[0] = new Control[] {
                callCqDxCheckBox, IgnoreNonDxHelpLabel, ignoreNonDxCheckBox,
                callDirCqCheckBox, UseDirectedHelpLabel, directedTextBox,
                callNonDirCqCheckBox
            };
            sectionControls[1] = new Control[] {
                replyDirCqCheckBox, AlertDirectedHelpLabel, alertTextBox,
                replyNormCqLabel, ExcludeHelpLabel, bandComboBox, forLabel, replyDxCheckBox, replyLocalCheckBox,
                cqOnlyRadioButton, cqGridRadioButton, anyMsgRadioButton, includeLabel, IncludeHelpLabel,
                rankComboBox, PriorityHelpLabel, callLabel,
                replyNewDxccCheckBox, replyNewOnlyCheckBox, ReplyNewHelpLabel,
                exceptLabel, exceptTextBox, blockHelpLabel
            };
            sectionControls[2] = new Control[] {
                skipGridCheckBox, useRR73CheckBox,
                logEarlyCheckBox, LogEarlyHelpLabel, replyRR73CheckBox, ReplyRR73HelpLabel,
                limitLabel, timeoutNumUpDown, repeatLabel, optimizeCheckBox, holdCheckBox, LimitTxHelpLabel
            };
            sectionControls[3] = new Control[] {
                freqCheckBox, AutoFreqHelpLabel,
                periodLabel, periodComboBox, PeriodHelpLabel,
                timedCheckBox, timeoutLabel, startLabel, startTextBox, atLabel, timeLabel2, modeComboBox, StartHelpLabel,
                stopLabel, stopTextBox, timeLabel,
                playSoundLabel, callAddedCheckBox, mycallCheckBox, loggedCheckBox
            };

            // Record original Y positions for each section's controls
            sectionCtrlYs = new int[4][];
            sectionHeights = new int[4];
            for (int s = 0; s < 4; s++)
            {
                sectionCtrlYs[s] = new int[sectionControls[s].Length];
                int maxBottom = sectionHeaderOrigYs[s] + sectionHeaderHeight;
                for (int c = 0; c < sectionControls[s].Length; c++)
                {
                    sectionCtrlYs[s][c] = sectionControls[s][c].Location.Y;
                    int bottom = sectionControls[s][c].Location.Y + sectionControls[s][c].Height;
                    if (bottom > maxBottom) maxBottom = bottom;
                }
                sectionHeights[s] = maxBottom - sectionHeaderOrigYs[s];
            }

            // Bottom controls (always visible, repositioned dynamically)
            bottomCtrls = new Control[] {
                setupButton, modeGroupBox, guideLabel, msgTextBox,
                statusText, verLabel, verLabel2, modeHelpLabel,
                label1, label2, label3, label4, label5, label6, label7, label8, label9, label10,
                label11, label12, label13, label14, label15, label16, label17, label18, label19, label20,
                label21, label22, label23, label24, label25, label26, label27, label28, label29, label30,
                label31, label32, label33, label34
            };
            bottomCtrlOrigYs = new int[bottomCtrls.Length];
            for (int i = 0; i < bottomCtrls.Length; i++)
            {
                bottomCtrlOrigYs[i] = bottomCtrls[i].Location.Y;
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
            bool debug = false;
            bool diagLog = false;
            WsjtxClient.TxModes txMode = WsjtxClient.TxModes.CALL_CQ;
            int offsetHiLimit = -1;
            int offsetLoLimit = -1;
            bool useRR73 = false;
            bool mode = false;
            string myContinent = null;

            //control defaults
            modeComboBox.SelectedIndex = 0;
            periodComboBox.SelectedIndex = 2;
            int rankMethodIdx = (int)WsjtxClient.RankMethods.MOST_RECENT;
            freqCheckBox.Checked = false;
            timedCheckBox.Checked = false;           //not saved

            if (iniFile == null)     //.ini file not written yet, read properties (possibly set defaults)
            {
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
                alwaysOnTop = Properties.Settings.Default.alwaysOnTop;
                useRR73 = Properties.Settings.Default.useRR73;
                skipGridCheckBox.Checked = Properties.Settings.Default.skipGrid;
                diagLog = Properties.Settings.Default.diagLog;
                freqCheckBox.Checked = Properties.Settings.Default.bestOffset;
                rankMethodIdx = Properties.Settings.Default.rankMethodIdx;
                callAddedCheckBox.Checked = Properties.Settings.Default.playCallAdded;
            }
            else        //read settings from .ini file (avoid .Net config file mess)
            {
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
                alwaysOnTop = iniFile.Read("alwaysOnTop") == "True";
                useRR73 = iniFile.Read("useRR73") == "True";
                skipGridCheckBox.Checked = iniFile.Read("skipGrid") == "True";
                replyNewDxccCheckBox.Checked = iniFile.Read("autoReplyNewCq") == "True";
                replyDxCheckBox.Checked = iniFile.Read("enableReplyDx") != "False";     //default: true
                diagLog = iniFile.Read("diagLog") == "True";

                //start of .ini-file-only settings (not in .Net config)
                mode = iniFile.Read("replyAndQuit") == "True";
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
                callingExpanded = iniFile.Read("callingExpanded") == "True";
                replyingExpanded = iniFile.Read("replyingExpanded") == "True";
                sequenceExpanded = iniFile.Read("sequenceExpanded") == "True";
                generalExpanded = iniFile.Read("generalExpanded") == "True";
                darkMode = iniFile.Read("darkMode") == "True";
            }

            txMode = mode ? WsjtxClient.TxModes.LISTEN : WsjtxClient.TxModes.CALL_CQ;

            atLabel.Visible = true;
            modeComboBox.Visible = true;
            startLabel.Text = "Start";

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
                exceptTextBox.ForeColor = DarkMode.GrayText;
            }

            UpdateTxLabel();

            timeoutNumUpDown_ValueChanged(null, null);
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
            wsjtxClient.txMode = txMode;
            wsjtxClient.myContinent = myContinent;
            if (myContinent != null) replyLocalCheckBox.Text = myContinent;
            if (offsetLoLimit > 0) wsjtxClient.offsetLoLimit = offsetLoLimit;
            if (offsetHiLimit > 0) wsjtxClient.offsetHiLimit = offsetHiLimit;
            wsjtxClient.useRR73 = useRR73;
            rankComboBox.SelectedIndex = rankMethodIdx;
            wsjtxClient.RankMethodIdxChanged(rankMethodIdx);

            mainLoopTimer.Interval = 10;           //actual is 11-12 msec (due to OS limitations)
            mainLoopTimer.Start();

            UpdateAdvancedCtrls();
            RecalculateLayout();
            TopMost = alwaysOnTop;

            UpdateDebug();

            wsjtxClient.UpdateModeSelection();

            formLoaded = true;
            updateReplyNewOnlyCheckBoxEnabled();

            if (darkMode)
            {
                DarkMode.Enabled = true;
                DarkMode.ApplyToForm(this);
            }
        }

        private void Controller_FormClosing(object sender, FormClosingEventArgs e)
        {
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
                iniFile.Write("alwaysOnTop", alwaysOnTop.ToString());
                iniFile.Write("useRR73", wsjtxClient.useRR73.ToString());
                iniFile.Write("skipGrid", skipGridCheckBox.Checked.ToString());
                iniFile.Write("autoReplyNewCq", replyNewDxccCheckBox.Checked.ToString());
                iniFile.Write("enableReplyDx", replyDxCheckBox.Checked.ToString());
                iniFile.Write("enableReplyLocal", replyLocalCheckBox.Checked.ToString());
                iniFile.Write("diagLog", wsjtxClient.diagLog.ToString());
                bool mode = wsjtxClient.txMode == WsjtxClient.TxModes.LISTEN;
                iniFile.Write("replyAndQuit", mode.ToString());
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
                iniFile.Write("cqOnly", cqOnlyRadioButton.Checked.ToString());
                iniFile.Write("newOnBand", (bandComboBox.SelectedIndex == 1).ToString());
                iniFile.Write("myContinent", wsjtxClient.myContinent);
                iniFile.Write("rankMethod", wsjtxClient.rankMethodIdx.ToString());
                iniFile.Write("replyRR73", replyRR73CheckBox.Checked.ToString());
                iniFile.Write("cqGrid", cqGridRadioButton.Checked.ToString());
                iniFile.Write("anyMsg", anyMsgRadioButton.Checked.ToString());
                iniFile.Write("txPeriodIdx", periodComboBox.SelectedIndex.ToString());
                iniFile.Write("callingExpanded", callingExpanded.ToString());
                iniFile.Write("replyingExpanded", replyingExpanded.ToString());
                iniFile.Write("sequenceExpanded", sequenceExpanded.ToString());
                iniFile.Write("generalExpanded", generalExpanded.ToString());
                iniFile.Write("darkMode", darkMode.ToString());
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
            wsjtxClient.UpdateCallInProg();

        }

        private void initialConnFaultTimer_Tick(object sender, EventArgs e)
        {
            BringToFront();
            wsjtxClient.ConnectionDialog();
        }

        private void debugHighlightTimer_Tick(object sender, EventArgs e)
        {
            debugHighlightTimer.Stop();
            Color normalColor = DarkMode.NormalLabelFore;
            label17.ForeColor = normalColor;
            label24.ForeColor = normalColor;
            label25.ForeColor = normalColor;
            label13.ForeColor = normalColor;
            label10.ForeColor = normalColor;
            label20.ForeColor = normalColor;
            label21.ForeColor = normalColor;
            label8.ForeColor = normalColor;
            label19.ForeColor = normalColor;
            label18.ForeColor = normalColor;
            label12.ForeColor = normalColor;
            label4.ForeColor = normalColor;
            label14.ForeColor = normalColor;
            label15.ForeColor = normalColor;
            label16.ForeColor = normalColor;
            label26.ForeColor = normalColor;
            label27.ForeColor = normalColor;
            label3.ForeColor = normalColor;
            label1.ForeColor = normalColor;
            label2.ForeColor = normalColor;
            label28.ForeColor = normalColor;
            label11.ForeColor = normalColor;
        }

        private void timeoutNumUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (timeoutNumUpDown.Value < minSkipCount)
            {
                timeoutNumUpDown.Value = minSkipCount;
            }

            if (timeoutNumUpDown.Value > maxSkipCount)
            {
                timeoutNumUpDown.Value = maxSkipCount;
            }
            UpdateTxLabel();

            if (formLoaded) wsjtxClient.TxRepeatChanged();
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
                alertTextBox.ForeColor = DarkMode.Foreground;
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

            wsjtxClient.debug = !wsjtxClient.debug;
            UpdateDebug();
            if (formLoaded) wsjtxClient.DebugChanged();
        }

        private void UpdateDebug()
        {
            SuspendLayout();
            FormBorderStyle = FormBorderStyle.FixedSingle;
            label2.Visible = wsjtxClient.debug;
            label5.Visible = wsjtxClient.debug;
            label7.Visible = wsjtxClient.debug;
            label13.Visible = wsjtxClient.debug;
            label28.Visible = wsjtxClient.debug;
            if (wsjtxClient.debug)
            {
#if DEBUG
                AllocConsole();
                ShowWindow(GetConsoleWindow(), 5);
#endif
                RecalculateLayout();
                wsjtxClient.UpdateDebug();
                BringToFront();
            }
            else
            {
                RecalculateLayout();
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
            sequenceLabel.Visible = true;
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

            wsjtxClient.UpdateModeVisible();
        }

        private void skipGridCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            skipGridCheckBox.Text = "Skip grid (pending)";
            skipGridCheckBox.ForeColor = DarkMode.PendingColor;
            wsjtxClient.WsjtxSettingChanged();
        }

        private void useRR73CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            useRR73CheckBox.Text = "Use RR73 (pending)";
            useRR73CheckBox.ForeColor = DarkMode.PendingColor;
            wsjtxClient.WsjtxSettingChanged();
        }

        public void WsjtxSettingConfirmed()
        {
            skipGridCheckBox.Text = "Skip grid msg";
            skipGridCheckBox.ForeColor = DarkMode.ConfirmedCheckboxColor;
            useRR73CheckBox.Text = "Use RR73 msg";
            useRR73CheckBox.ForeColor = DarkMode.ConfirmedCheckboxColor;
        }

        public void setupButton_Click(object sender, EventArgs e)
        {
            initialConnFaultTimer.Stop();
            if (formLoaded) wsjtxClient.RestartAutoCqTimer();

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
            if (DarkMode.Enabled) DarkMode.ApplyToForm(setupDlg);
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
            if (formLoaded) wsjtxClient.RestartAutoCqTimer();

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
            if (DarkMode.Enabled) DarkMode.ApplyToForm(guide);
            guide.Show();
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
            if (!formLoaded) return;

            wsjtxClient.RestartAutoCqTimer();

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
                $"{nl}    Ctrl+K:  Toggle dark mode" +
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

            string adv = $"{nl}{nl}If 'Optimize' is selected, the maximum number of replies and CQs for the current call is automatically adjusted lower than the specified limit (if possible), to help process the call queue faster." +
                $"{nl}{nl}If 'Hold' is selected, the 'Repeated Tx' limit is ignored, and replies to the current call sign are transmitted a maximum of {wsjtxClient.holdMaxTxRepeat} times. 'Hold' is automatically enabled when processing a 'new DXCC', deselect 'Reply to new DXCC' to prevent this action.";
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
            if (c == (char)Keys.Back || c == ' ' || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return;
            Console.Beep();
            e.Handled = true;
        }

        private void alertTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.KeyChar = char.ToUpper(e.KeyChar);
            char c = e.KeyChar;
            if (c == (char)Keys.Back || c == ' ' || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return;
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

            string s = $"{nl}{nl}If you select 'Exclusively', only new countries will be replied to, and all other calls will be ignored. (This option is only available when using the 'Listen for calls' operating mode).";
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
                directedTextBox.ForeColor = DarkMode.Foreground;
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
            if (formLoaded)
            {
                replyNewOnlyCheckBox.Visible = replyingExpanded;
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
            if (formLoaded && listenModeButton.Checked && !replyDxCheckBox.Checked && !replyLocalCheckBox.Checked && !replyDirCqCheckBox.Checked && !replyNewDxccCheckBox.Checked && !replyDxCheckBox.Checked && !replyLocalCheckBox.Checked & !replyNewDxccCheckBox.Checked)
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

            if (e.Alt && e.KeyCode == Keys.C)
            {
                setupButton_Click(null, null);
            }

            if (e.Shift && e.Alt && e.KeyCode == Keys.D)           //debug toggle
            {
                verLabel_DoubleClick(null, null);
            }

            if (!formLoaded) return;

            if (e.KeyCode == Keys.Escape)               //halt Tx immediately
            {
                wsjtxClient.RestartAutoCqTimer();
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
                wsjtxClient.RestartAutoCqTimer();
                if (wsjtxClient.ConnectedToWsjtx()) wsjtxClient.NextCall(false, callListBox.SelectedIndex);
            }

            if (e.Control && e.KeyCode == Keys.D)       //clear call queue
            {
                wsjtxClient.RestartAutoCqTimer();
                if (wsjtxClient.ConnectedToWsjtx()) wsjtxClient.ClearCallQueue();
            }

            if (e.Control && e.KeyCode == Keys.K)       //toggle dark mode
            {
                ToggleDarkMode();
            }
        }

        public void ToggleDarkMode()
        {
            darkMode = !darkMode;
            DarkMode.Enabled = darkMode;
            DarkMode.ApplyToForm(this);
            if (guide != null) DarkMode.ApplyToForm(guide);
            if (setupDlg != null) DarkMode.ApplyToForm(setupDlg);
            if (helpDlg != null) DarkMode.ApplyToForm(helpDlg);
            wsjtxClient.RefreshDisplay();
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
            if (DarkMode.Enabled) DarkMode.ApplyToForm(helpDlg);
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

        private void callingLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;
            callingExpanded = !callingExpanded;
            ToggleSection();
        }

        private void replyingLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;
            replyingExpanded = !replyingExpanded;
            ToggleSection();
        }

        private void sequenceLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;
            sequenceExpanded = !sequenceExpanded;
            ToggleSection();
        }

        private void optionsLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;
            generalExpanded = !generalExpanded;
            ToggleSection();
        }

        private void ToggleSection()
        {
            SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            SuspendLayout();
            RecalculateLayout();
            ResumeLayout();
            SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            Invalidate(true);
            Update();
        }

        private void RecalculateLayout()
        {
            bool[] expanded = { callingExpanded, replyingExpanded, sequenceExpanded, generalExpanded };
            string[] expandedText = { "\u25b2 Calling options", "\u25b2 Replying options", "\u25b2 Sequence options", "\u25b2 General options" };
            string[] collapsedText = { "\u25bc Calling options", "\u25bc Replying options", "\u25bc Sequence options", "\u25bc General options" };

            int currentY = sectionHeaderOrigYs[0];

            for (int s = 0; s < 4; s++)
            {
                // Position section header
                sectionHeaders[s].Location = new Point(sectionHeaders[s].Location.X, currentY);
                sectionHeaders[s].Text = expanded[s] ? expandedText[s] : collapsedText[s];

                if (expanded[s])
                {
                    // Show controls at their offset from this section's header
                    for (int c = 0; c < sectionControls[s].Length; c++)
                    {
                        int relY = sectionCtrlYs[s][c] - sectionHeaderOrigYs[s];
                        sectionControls[s][c].Location = new Point(sectionControls[s][c].Location.X, currentY + relY);
                        sectionControls[s][c].Visible = true;
                    }
                    currentY += sectionHeights[s];
                }
                else
                {
                    // Hide controls
                    for (int c = 0; c < sectionControls[s].Length; c++)
                    {
                        sectionControls[s][c].Visible = false;
                    }
                    currentY += sectionHeaderHeight;
                }
            }

            // Position bottom controls relative to their original offset from the bottom of the last section
            int lastSectionOrigBottom = sectionHeaderOrigYs[3] + sectionHeights[3];
            int delta = currentY - lastSectionOrigBottom;

            for (int i = 0; i < bottomCtrls.Length; i++)
            {
                bottomCtrls[i].Location = new Point(bottomCtrls[i].Location.X, bottomCtrlOrigYs[i] + delta);
            }

            int normalHeight = setupButton.Location.Y + setupButton.Height + 45;
            if (wsjtxClient != null && wsjtxClient.debug)
            {
                int debugBottom = label17.Location.Y + label17.Height + 10;
                Height = Math.Max(normalHeight, debugBottom + (Height - ClientSize.Height));
            }
            else
            {
                Height = normalHeight;
            }
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

            exceptTextBox.ForeColor = DarkMode.Foreground;
            if (exceptTextBox.Text == separateBySpaces)
            {
                exceptTextBox.Text = "";
            }
        }

        private void exceptTextBox_Leave(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            exceptTextBox.ForeColor = DarkMode.Foreground;

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
                exceptTextBox.ForeColor = DarkMode.GrayText;
            }
        }

        public bool IsBasicOnly()
        {
            return fileVer == basicOnlyFileVer;
        }
    }
}



