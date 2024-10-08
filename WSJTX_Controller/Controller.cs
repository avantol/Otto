﻿using System;
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
        public bool alwaysOnTop = false;
        public bool firstRun = true;        //first run for each user level
        public bool skipLevelPrompt = false;
        public bool offsetTune = false;
        public int helpDialogsPending = 0;
        public float dispFactor = 1.0F;

        private bool formLoaded = false;
        private SetupDlg setupDlg = null;
        private IniFile iniFile = null;
        private int minSkipCount = 1;
        private int maxSkipCount = 20;
        private const string separateBySpaces = "(separate by spaces)";
        private List<Control> ctrls = new List<Control>();
        private int windowSizePctIncr = 0;
        bool confirmWindowSize = false;
        private bool showCloseMsgs = true;
        public string friendlyName = "";
        private MouseEventArgs mouseEventArgs;
        private int listBoxClickCount;

        private System.Windows.Forms.Timer mainLoopTimer;

        public System.Windows.Forms.Timer statusMsgTimer;
        public System.Windows.Forms.Timer initialConnFaultTimer;
        public System.Windows.Forms.Timer debugHighlightTimer;
        public System.Windows.Forms.Timer confirmTimer;
        public System.Windows.Forms.Timer setupTimer;
        public System.Windows.Forms.Timer callListBoxClickTimer;

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
            confirmTimer = new System.Windows.Forms.Timer();
            confirmTimer.Interval = 2000;
            confirmTimer.Tick += new System.EventHandler(confirmTimer_Tick);
            setupTimer = new System.Windows.Forms.Timer();
            setupTimer.Interval = 20;
            setupTimer.Tick += new System.EventHandler(setupTimer_Tick);
            callListBoxClickTimer = new System.Windows.Forms.Timer();
            callListBoxClickTimer.Interval = 250;
            callListBoxClickTimer.Tick += new System.EventHandler(callListBoxClickTimer_Tick);

            SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(SystemEvents_UserPreferenceChanged);
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
            SuspendLayout();

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
                MessageBox.Show("Unable to create settings file: " + pathFileNameExt + "\n\nContinuing with default settings...", friendlyName, MessageBoxButtons.OK);
            }

            string ipAddress = null;            //flag as invalid
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
            modeComboBox.SelectedIndex = 0;
            int rankMethodIdx = 0;

            freqCheckBox.Checked = false;
            timedCheckBox.Checked = false;           //not saved

            if (iniFile == null || !iniFile.KeyExists("advanced"))     //.ini file not written yet, read properties (possibly set defaults)
            {
                firstRun = Properties.Settings.Default.firstRun;
                debug = Properties.Settings.Default.debug;
                if (Properties.Settings.Default.windowPos != new Point(0, 0)) this.Location = Properties.Settings.Default.windowPos;
                if (Properties.Settings.Default.windowHt != 0) this.Height = Properties.Settings.Default.windowHt;
                ipAddress = Properties.Settings.Default.ipAddress;
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

                int x = Math.Max(Convert.ToInt32(iniFile.Read("windowPosX")), 0);
                int y = Math.Max(Convert.ToInt32(iniFile.Read("windowPosY")), 0);
                //check all screens, extended screen may not be present
                var screens = System.Windows.Forms.Screen.AllScreens;
                bool found = false;
                for (int scnIdx = 0; scnIdx < screens.Length; scnIdx++)
                {
                    if (screens[scnIdx].Bounds.Contains(new Point(x + (Bounds.Width / 2), y + (Bounds.Height / 2))))
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
                this.Height = Convert.ToInt32(iniFile.Read("windowHt"));

                ipAddress = iniFile.Read("ipAddress");
                port = Convert.ToInt32(iniFile.Read("port"));
                multicast = iniFile.Read("multicast") == "True";
                timeoutNumUpDown.Value = Convert.ToInt32(iniFile.Read("timeout"));
                directedTextBox.Text = iniFile.Read("directeds");
                callDirCqCheckBox.Checked = iniFile.Read("useDirected") == "True";
                mycallCheckBox.Checked = iniFile.Read("playMyCall") == "True";
                loggedCheckBox.Checked = iniFile.Read("playLogged") == "True";
                callAddedCheckBox.Checked = iniFile.Read("playCallAdded") == "True";
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
                if (iniFile.KeyExists("timedOperationIdx")) modeComboBox.SelectedIndex = Convert.ToInt32(iniFile.Read("timedOperationIdx"));
                if (iniFile.KeyExists("offsetHiLimit")) offsetHiLimit = Convert.ToInt32(iniFile.Read("offsetHiLimit"));
                if (iniFile.KeyExists("offsetLoLimit")) offsetLoLimit = Convert.ToInt32(iniFile.Read("offsetLoLimit"));
                replyLocalCheckBox.Checked = iniFile.Read("enableReplyLocal") == "True";
                optimizeCheckBox.Checked = iniFile.Read("optimizeTx") == "True";
                exceptTextBox.Text = iniFile.Read("exceptCalls");
                replyNewOnlyCheckBox.Checked = iniFile.Read("replyOnlyDxcc") == "True";
                callCqDxCheckBox.Checked = iniFile.Read("callCqDx") == "True";
                ignoreNonDxCheckBox.Checked = iniFile.Read("ignoreNonDx") == "True";
                callNonDirCqCheckBox.Checked = iniFile.Read("callNonDirCq") == "True";
                overrideUdpDetect = iniFile.Read("overrideUdpDetect") == "True";
                skipLevelPrompt = iniFile.Read("skipLevelPrompt") == "True";
                if (iniFile.KeyExists("windowSizePctIncr")) windowSizePctIncr = Convert.ToInt32(iniFile.Read("windowSizePctIncr"));
                confirmWindowSize = iniFile.Read("confirmWindowSize") == "True";
                cqOnlyRadioButton.Checked = iniFile.Read("cqOnly") != "False";              //default: true
                bool newOnBand = iniFile.Read("newOnBand") != "False";      //default: true
                bandComboBox.SelectedIndex = newOnBand ? 1 : 0;
                if (iniFile.KeyExists("myContinent")) myContinent = iniFile.Read("myContinent");    //required to be null if not set
                if (iniFile.KeyExists("rankMethod")) rankMethodIdx = Convert.ToInt32(iniFile.Read("rankMethod"));
                cqGridRadioButton.Checked = iniFile.Read("cqGrid") == "True";
                anyMsgRadioButton.Checked = iniFile.Read("anyMsg") == "True";

                replyRR73CheckBox.Checked = iniFile.Read("replyRR73") == "True";
                //read-only
                offsetTune = iniFile.Read("offsetTune") == "True";
            }

            txMode = mode ? WsjtxClient.TxModes.LISTEN : WsjtxClient.TxModes.CALL_CQ;

            if (!advanced)
            {
                showTxModes = false;
                freqCheckBox.Checked = false;
                optimizeCheckBox.Checked = false;
                holdCheckBox.Checked = false;
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

            exceptTextBox.Enabled = replyNewDxccCheckBox.Checked;
            if (!exceptTextBox.Enabled && exceptTextBox.Text == "")
            {
                exceptTextBox.Text = separateBySpaces;
            }

            UpdateTxLabel();

            callCqDxCheckBox_CheckedChanged(null, null);
            callNonDirCqCheckBox_CheckedChanged(null, null);
            directedTextBox_Leave(null, null);
            UpdateCqNewOnBand();

#if DEBUG
            AllocConsole();

            if (!debug)
            {
                ShowWindow(GetConsoleWindow(), 0);
            }
#endif

            //start the UDP message server
            wsjtxClient = new WsjtxClient(this, IPAddress.Parse(ipAddress), port, multicast, overrideUdpDetect, debug, diagLog);
            wsjtxClient.advanced = advanced;
            wsjtxClient.txMode = txMode;
            wsjtxClient.showTxModes = showTxModes;
            wsjtxClient.myContinent = myContinent;
            if (myContinent != null) replyLocalCheckBox.Text = myContinent;
            UpdateMinSkipCount();
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

            if (confirmWindowSize) confirmTimer.Start();

            UpdateDebug();

            //save font details because setting form size also resets fonts for all controls
            foreach (Control control in Controls)
            {
                ctrls.Add(control);
            }
            RescaleForm();

            wsjtxClient.UpdateModeSelection();

            ResumeLayout();
            formLoaded = true;
            updateReplyNewOnlyCheckBoxEnabled();
        }

        private void Controller_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (CheckHelpDlgOpen())
            {
                e.Cancel = true;
                return;
            }

            if (showCloseMsgs)                 //not closing for immediate restart
            {
                firstRun = false;

                if (!skipLevelPrompt)
                {
                    if (!wsjtxClient.advanced && wsjtxClient.ConnectedToWsjtx())
                    {
                        if (MessageBox.Show($"If you're familiar with the basic operation of this program now, you'll probably be interested in more options.{Environment.NewLine}{Environment.NewLine}Do you want to see all options the next time you run this program?{Environment.NewLine}{Environment.NewLine}(You can make this choice later)", wsjtxClient.pgmName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            wsjtxClient.advanced = true;
                            firstRun = true;
                        }
                    }
                    else
                    {
                        if (!wsjtxClient.showTxModes && wsjtxClient.ConnectedToWsjtx())
                        {
                            if (MessageBox.Show($"If you're familiar with using some of the options in this program now, you'll probably be interested in the 'Listen for calls' option.{Environment.NewLine}{Environment.NewLine}This causes much less traffic on the band than CQing, by waiting to reply until the calls you want are detected.{Environment.NewLine}{Environment.NewLine}Do you want to see this useful option the next time you run this program?{Environment.NewLine}{Environment.NewLine}(You can make this choice later)", wsjtxClient.pgmName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
                iniFile.Write("windowPosX", (Math.Max(this.Location.X, 0)).ToString());
                iniFile.Write("windowPosY", (Math.Max(this.Location.Y, 0)).ToString());
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
                iniFile.Write("windowSizePctIncr", windowSizePctIncr.ToString());
                iniFile.Write("confirmWindowSize", confirmWindowSize.ToString());
                iniFile.Write("cqOnly", cqOnlyRadioButton.Checked.ToString());
                iniFile.Write("newOnBand", (bandComboBox.SelectedIndex == 1).ToString());
                iniFile.Write("myContinent", wsjtxClient.myContinent);
                iniFile.Write("rankMethod", wsjtxClient.rankMethodIdx.ToString());
                iniFile.Write("replyRR73", replyRR73CheckBox.Checked.ToString());
                iniFile.Write("cqGrid", cqGridRadioButton.Checked.ToString());
                iniFile.Write("anyMsg", anyMsgRadioButton.Checked.ToString());
            }

            CloseComm();

            SystemEvents.UserPreferenceChanged -= new UserPreferenceChangedEventHandler(SystemEvents_UserPreferenceChanged);
        }

        public void CloseComm()
        {
            if (mainLoopTimer != null) mainLoopTimer.Stop();
            mainLoopTimer = null;
            statusMsgTimer.Stop();
            initialConnFaultTimer.Stop();
            confirmTimer.Stop();
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
                Console.Beep();
                timeoutNumUpDown.Value = minSkipCount;
            }

            if (timeoutNumUpDown.Value > maxSkipCount)
            {
                Console.Beep();
                timeoutNumUpDown.Value = maxSkipCount;

            }
            UpdateTxLabel();

            wsjtxClient.TxRepeatChanged();
        }

        public void UpdateMinSkipCount()
        {
            if (wsjtxClient != null && wsjtxClient.showTxModes && cqModeButton.Checked)
            {
                minSkipCount = 1;
            }
            else
            {
                minSkipCount = 2;
            }

            if (timeoutNumUpDown.Value < minSkipCount)
            {
                timeoutNumUpDown.Value = minSkipCount;
            }
        }

        private void UpdateTxLabel()
        {
            if (timeoutNumUpDown.Value == 1)
            {
                repeatLabel.Text = "Tx per msg";
            }
            else
            {
                repeatLabel.Text = "repeat Tx";
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
        }

        private void callDirCqCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            directedTextBox.Enabled = callDirCqCheckBox.Checked;
            if (callDirCqCheckBox.Checked && directedTextBox.Text == separateBySpaces)
            {
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
        }

        private void replyNewDxccCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            exceptTextBox.Enabled = replyNewDxccCheckBox.Checked;
            updateReplyNewOnlyCheckBoxEnabled();

            if (replyNewDxccCheckBox.Checked && (replyDxCheckBox.Checked || replyLocalCheckBox.Checked || replyDirCqCheckBox.Checked)) replyNewOnlyCheckBox.Checked = false;

            if (!formLoaded) return;

            if (replyNewDxccCheckBox.Checked && exceptTextBox.Text == separateBySpaces)
            {
                exceptTextBox.Clear();
                exceptTextBox.ForeColor = System.Drawing.Color.Black;
            }
            if (!replyNewDxccCheckBox.Checked && exceptTextBox.Text == "") exceptTextBox.Text = separateBySpaces;

            CheckManualSelection();
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

            if (!wsjtxClient.advanced) return;
            wsjtxClient.debug = !wsjtxClient.debug;
            UpdateDebug();
            if (formLoaded) wsjtxClient.DebugChanged();
        }

        private void UpdateDebug()
        {
            if (wsjtxClient.debug)
            {
#if DEBUG
                AllocConsole();
                ShowWindow(GetConsoleWindow(), 5);
#endif
                Height = this.MaximumSize.Height;
                FormBorderStyle = FormBorderStyle.Fixed3D;
                wsjtxClient.UpdateDebug();
                BringToFront();
            }
            else
            {
                if (wsjtxClient.advanced)
                {
                    Height = (int)(this.MaximumSize.Height * 0.886);
                }
                else
                {
                    statusText.Location = new Point(statusText.Location.X, 279);
                    setupButton.Location = new Point(setupButton.Location.X, 308);
                    verLabel.Location = new Point(verLabel.Location.X, 309);
                    verLabel2.Location = new Point(verLabel2.Location.X, 323);
                    verLabel3.Location = new Point(verLabel3.Location.X, 323);
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

                    Height = (int)(this.MaximumSize.Height * 0.46);
                }
                FormBorderStyle = FormBorderStyle.FixedSingle;
#if DEBUG
                ShowWindow(GetConsoleWindow(), 0);
#endif
            }
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
            replyNormCqLabel.Visible = true;
            replyDxCheckBox.Visible = true;
            replyLocalCheckBox.Visible = true;
            ExcludeHelpLabel.Visible = true;
            AutoFreqHelpLabel.Visible = true;
            ReplyNewHelpLabel.Visible = true;
            StartHelpLabel.Visible = true;
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
            confirmTimer.Stop();

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
            setupDlg.pct = windowSizePctIncr;
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

        private void addCallLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"Calls are replied to in order of importance:{Environment.NewLine}- New countries on any band*{Environment.NewLine}- New countries on current band*{Environment.NewLine}- Calls directed to {MyCall()}*{Environment.NewLine}- Calls you select manually*{Environment.NewLine}- CQs matching 'Reply to directed CQs'*{Environment.NewLine}- Calls matching 'Reply to new calls', ranked by the selected 'Reply priority'.{Environment.NewLine}{Environment.NewLine}(*ranked in the order received, within each type){Environment.NewLine}{Environment.NewLine}To manually add more call signs to the reply list:{Environment.NewLine}- Press and hold the 'Alt' key, then{Environment.NewLine}- Double-click on the line containing the desired 'from' call sign in the WSJT-X 'Band Activity' list.{Environment.NewLine}{Environment.NewLine}To remove a call sign from the reply list:{Environment.NewLine}- Right-click on the call, then confirm.{Environment.NewLine}{Environment.NewLine}To reply to any call from the reply list:{Environment.NewLine}- Double-click on the call.{Environment.NewLine}{Environment.NewLine}To cancel the current call when the reply list is empty:{Environment.NewLine}- Double-click on the reply list box.{Environment.NewLine}{Environment.NewLine}When you double-click on a call in the WSJT-X 'Band Activity list *without* using the 'Alt' key:{Environment.NewLine}- This causes an immediate reply, instead of placing the call on a list of calls to reply to.{Environment.NewLine}- Automatic operation continues after this call is processed.{Environment.NewLine}{Environment.NewLine}Note:{Environment.NewLine}- Unless 'Reply priority' is set to 'order received', lower-priority calls on the reply list are continuously replaced by higher-priority calls.{Environment.NewLine}- If 'Reply priority' is set to 'Best for ... beam', calls that are off the nominal azimuth by more than {wsjtxClient.beamWidth / 2} degrees are not added to the reply list.{Environment.NewLine}- '*' denotes a call from a new country.{Environment.NewLine}{Environment.NewLine}You can leave this dialog open while you try out these hints.");
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
            ShowHelp($"This section allows you to choose which messages from new callers you want to add to the reply list.{Environment.NewLine}{Environment.NewLine}- Select 'CQ' if you want to reply only to CQ messages.{Environment.NewLine}- Select 'CQ/grid' if you want to reply only to messages with grid information, allowing you to prioritize calls based on distance or azimuth.{Environment.NewLine}- Select 'any' to reply to any message.{Environment.NewLine}{Environment.NewLine}Note: The selections here don't affect replies to 'new countries' or 'new countries on band', which are enabled when 'Reply longer to new rare DX calls' is selected.");
        }

        private void IgnoreNonDxHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"When calling 'CQ DX', select 'Ignore non-DX reply' to disable replying to calls to {MyCall()} from continents other than your continent.{Environment.NewLine}{Environment.NewLine}This also disables replies to calls not directed to {MyCall()}.");
        }

        private void UseDirectedHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"To send directed CQs:{Environment.NewLine}- Enter the code(s) for the directed CQs you want to transmit (2 to 4 letters each), separated by spaces.{Environment.NewLine}- Don't enter 'DX' here.{Environment.NewLine}{Environment.NewLine}The directed CQs will be used in random order.{Environment.NewLine}{Environment.NewLine}Example: EU SA OC");
        }

        private void AlertDirectedHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"To reply to specific directed CQs from callers you haven't worked yet:{Environment.NewLine}- Enter the code(s) for the directed CQs (2 to 4 letters each), separated by spaces.{Environment.NewLine}{Environment.NewLine}Example: DX POTA NA USA WY{Environment.NewLine}{Environment.NewLine}If you specify 'DX', there will be no reply if the caller is on your continent.{Environment.NewLine}{Environment.NewLine}(Note: 'CQ POTA' or 'CQ SOTA' is an exception to the 'already worked' rule, these calls will cause an auto-reply if you haven't already logged that call in the current mode/band in the current day).");
        }

        private void LogEarlyHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"To maximize the chance of completed QSOs, consider 'early logging':{Environment.NewLine}{Environment.NewLine}The defining requirement for any QSO is the exchange of call signs and signal reports.{Environment.NewLine}Once either party sends an 'RRR' message (and reports have been exchanged), those requirements have been met... a '73' is not necessary for logging the QSO.{Environment.NewLine}{Environment.NewLine}Note that the QSO will continue after early logging, completing when 'RR73' or '73' is sent, or '73' is received.{Environment.NewLine}{Environment.NewLine}New countries are an exception to early logging. In this case, logging is only after confirmation with a '73' or 'RR73'.");
        }

        private void verLabel2_Click(object sender, EventArgs e)
        {
            string command = "mailto:more.avantol@xoxy.net?subject=Otto";
            System.Diagnostics.Process.Start(command);
        }

        private void ExcludeHelpLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.UpdateMaxAutoGenEnqueue();
            string myContinent = wsjtxClient.myContinent == null ? "" : $" '{wsjtxClient.myContinent}'";
            string onBand = bandComboBox.Items[1].ToString();
            ShowHelp($"{friendlyName} will add up to {wsjtxClient.maxAutoGenEnqueue} calls to the reply list that meet these conditions:{Environment.NewLine}{Environment.NewLine}- The call has not been worked before on 'any band' or '{onBand}', as specified{Environment.NewLine}- The call is 'DX' or originated in your continent{myContinent}, as specified{Environment.NewLine}- The message can be a CQ, a call with grid information, or any type, as specified{Environment.NewLine}- The caller is on your Rx time slot (if in 'Call CQ' mode){Environment.NewLine}- The caller hasn't been replied to more than {wsjtxClient.maxPrevCqs} times during this mode / band session.{Environment.NewLine}{Environment.NewLine}If you select 'DX', {friendlyName} will reply to calls from continents other than yours.{Environment.NewLine}{Environment.NewLine}For example, this is useful in case you've already worked all states/entities on your continent, and only want to reply to calls you haven't worked yet from other continents.{Environment.NewLine}{Environment.NewLine}- If you select your continent{myContinent}, {friendlyName} will reply only to those calls.{Environment.NewLine}{Environment.NewLine}For example, this is useful in case you're running QRP, and expect you can't be heard on other continents, and only want to reply to calls from your continent.{Environment.NewLine}{Environment.NewLine}Select '{onBand}' to also add calls to the reply list that you haven't worked before on the current band.{Environment.NewLine}{Environment.NewLine}Note: If you have entered 'directed CQs' to reply to, those CQs will be replied to regardless of the 'DX',{myContinent}, 'include msg', or new 'any band' or '{onBand}' settings here.");
        }

        private void modeHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"Choose what you want this progam to do after replying to all queued calls:{Environment.NewLine}{Environment.NewLine}- Call CQ until there is a reply, and automatically complete each contact,{Environment.NewLine}or{Environment.NewLine}- Listen for CQs or other interesting calls, and automatically reply to them.{Environment.NewLine}{Environment.NewLine}The advantage to listening is that you can monitor both odd and even Rx time slots. This is helpful for maximizing POTA or new country QSOs, for example.{Environment.NewLine}{Environment.NewLine}(Note: If you choose 'Listen for calls', be sure to select a large enough number of retries in 'Limit to [ ] repeat Tx' so that the stations you call have a chance to reply before any automatic switch to the opposite time slot).{Environment.NewLine}{Environment.NewLine}Shortcut keys:{Environment.NewLine}    Ctrl+C  Call CQ{Environment.NewLine}    Ctrl+L  Listen for calls{Environment.NewLine}    Esc  Stop transmit immediately{Environment.NewLine}    Ctrl+Q  Clear reply list{Environment.NewLine}    Ctrl+N  Skip to next call (cancel if none){Environment.NewLine}    Alt+S  Setup{Environment.NewLine}");
        }

        public void cqModeButton_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.TxModeChanged(WsjtxClient.TxModes.CALL_CQ);
        }

        public void listenModeButton_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.TxModeChanged(WsjtxClient.TxModes.LISTEN);
        }

        private void freqCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            wsjtxClient.WsjtxSettingChanged();
            wsjtxClient.AutoFreqChanged(freqCheckBox.Checked, false);
        }

        private void LimitTxHelpLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            string adv = wsjtxClient != null && wsjtxClient.advanced ? $"{Environment.NewLine}{Environment.NewLine}If 'Optimize' is selected, the maximum number of replies and CQs for the current call is automatically adjusted lower than the specified limit, to help process the call queue faster.{Environment.NewLine}{Environment.NewLine}If 'Hold' is selected, the repeat Tx limit is ignored, and replies to the current call sign are transmitted a maximum of {wsjtxClient.holdMaxTxRepeat} times. 'Hold' is automatically enabled when processing a 'new country', deselect 'Reply longre to new rare DX calls' to prevent this action." : "";
            ShowHelp($"This will limit the number of times the same message is transmitted.{Environment.NewLine}{Environment.NewLine}For example, it will limit the number of repeated transmitted replies or CQs for the current call. If there is no response to your reply messages when the limit is reached, the next call in the queue is processed (or if the call queue is empty, CQing will resume).{Environment.NewLine}{Environment.NewLine}As the repeat limit is reduced, the number of times a call can be automatically re-added to the call queue is increased, to compensate.{adv}");
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
            DeleteTextBoxSelection(startTextBox);
            char c = e.KeyChar;
            if (c == (char)Keys.Back || ((c >= '0' && c <= '9') && startTextBox.Text.Length < 4)) return;
            Console.Beep();
            e.Handled = true;
        }

        private void stopTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            DeleteTextBoxSelection(stopTextBox);
            char c = e.KeyChar;
            if (c == (char)Keys.Back || ((c >= '0' && c <= '9') && stopTextBox.Text.Length < 4)) return;
            Console.Beep();
            e.Handled = true;
        }

        private void ReplyNewLabel_Click(object sender, EventArgs e)
        {
            if (!formLoaded) return;

            string s = "";
            if (wsjtxClient.showTxModes) s = $"{Environment.NewLine}{Environment.NewLine}If you select 'Exclusively', only new countries will be replied to, and all other calls will be ignored. (This option is only available when using the 'Listen for calls' operating mode).";
            ShowHelp($"Select 'Reply longer to new rare DX calls' to retry replies many more times for any call (not only CQs) from *very* unusual countries or DXpeditions.{Environment.NewLine}{Environment.NewLine}This option is intended ONLY when replying to difficult stations that are likely to have many competing callers. It's not suitable at all for working common DX entities!{Environment.NewLine}{Environment.NewLine}If a call sign is from a country never worked before on any band, {friendlyName} will sound an audio notification and 'hold' (repeat) transmissions to that call sign for a maximum of {wsjtxClient.holdMaxTxRepeat} times.{Environment.NewLine}{Environment.NewLine}If a call sign is from a country not worked before on the current band, {friendlyName} will sound an audio notification and 'hold' (repeat) transmissions to that call sign for a maximum of {wsjtxClient.holdMaxTxRepeatNewOnBand} times.{Environment.NewLine}{Environment.NewLine}If a station never replies or won't confirm QSOs conveniently, you can add that call sign to the 'Except' list, and it will be ignored.{s}{Environment.NewLine}{Environment.NewLine}If you don't select 'Reply longer to new rare DX calls', you will still be able to reply to new DX stations, just with far fewer Tx retries.");
        }

        private void callAddedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoaded && callAddedCheckBox.Checked) wsjtxClient.Play("blip.wav");
        }

        private void AutoFreqHelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"The Tx audio frequency is automatically set to an unused part of the audio spectrum.{Environment.NewLine}{Environment.NewLine}After a period of no replies being received, transmitting is temporarily suspended for one Tx cycle, the received audio is re-sampled, and the best Tx frequency is re-calculated.");
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

            if (!callCqDxCheckBox.Checked && !callDirCqCheckBox.Checked)
            {
                callNonDirCqCheckBox.Checked = true;
            }

        }

        private void directedTextBox_Leave(object sender, EventArgs e)
        {
            if (directedTextBox.Text == separateBySpaces) return;

            string text = directedTextBox.Text.Replace("DX", "");       //not allowed
            text = text.Replace("*", "");        //obsoleted
            var dirArray = text.Trim().ToUpper().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string corrText = "";
            string delim = "";
            foreach (string dir in dirArray)
            {
                if (dir.Length >= 2 && dir.Length <= 4) corrText = corrText + delim + dir;
                delim = " ";
            }
            directedTextBox.Text = corrText;
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

        private void callNonDirCqCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (callNonDirCqCheckBox.Checked)
            {
                if (callCqDxCheckBox.Checked) ignoreNonDxCheckBox.Checked = false;
            }
            else
            {
                if (!callCqDxCheckBox.Checked && (!callDirCqCheckBox.Checked || directedTextBox.Text == ""))
                {
                    callNonDirCqCheckBox.Checked = true;
                    if (formLoaded)
                    {
                        new Thread(new ThreadStart(delegate
                        {
                            helpDialogsPending++;
                            MessageBox.Show($"Because at least one type of CQ must be specified, 'Call CQ (non-directed)' can only be unselected if:{Environment.NewLine}{Environment.NewLine}'Call CQ DX' is selected{Environment.NewLine}or{Environment.NewLine}'Call CQ directed to' is selected and direction text is entered.", wsjtxClient.pgmName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            helpDialogsPending--;
                        })).Start();
                        return;
                    }
                }
            }
            if (formLoaded) wsjtxClient.WsjtxSettingChanged();              //resets CQ to non-directed
        }

        private void alertTextBox_Leave(object sender, EventArgs e)
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
        }

        private void CheckManualSelection()
        {
            if (formLoaded && wsjtxClient.showTxModes && listenModeButton.Checked && !replyDxCheckBox.Checked && !replyLocalCheckBox.Checked && !replyDirCqCheckBox.Checked && !replyNewDxccCheckBox.Checked && !replyDxCheckBox.Checked && !replyLocalCheckBox.Checked)
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
        }

        void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Window)
            {
                RescaleForm();
            }
        }

        private void RescaleForm()
        {
            if (windowSizePctIncr == 0) return;

            dispFactor = 1.0F + (float)(windowSizePctIncr / 100.0F);
            Font = new Font(SystemFonts.DefaultFont.Name,
                SystemFonts.DefaultFont.SizeInPoints * dispFactor, GraphicsUnit.Point);

            float fontAdjPts = 0.0F;
            foreach (Control control in ctrls)
            {
                control.Font = new Font(control.Font.Name, (control.Font.SizeInPoints * dispFactor) + fontAdjPts, control.Font.Style, GraphicsUnit.Point);
            }
        }

        public void ResizeForm(int newPct)
        {
            windowSizePctIncr = newPct;
            confirmWindowSize = windowSizePctIncr != 0;
            showCloseMsgs = false;
            Application.Restart();
        }

        public void confirmTimer_Tick(object sender, EventArgs e)
        {
            confirmTimer.Stop();
            confirmWindowSize = false;
            if (MessageBox.Show($"Do you want to keep the new window size?", wsjtxClient.pgmName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                windowSizePctIncr = 0;
                showCloseMsgs = false;
                Application.Restart();
            }
        }

        private void Controller_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.S)
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

            if (e.Control && e.KeyCode == Keys.Q)       //clear call queue
            {
                if (wsjtxClient.ConnectedToWsjtx()) wsjtxClient.ClearCallQueue();
            }
        }

        private bool CheckHelpDlgOpen()     //true if dlg open
        {
            if (helpDialogsPending != 0)
            {
                ShowMsg("Close any open dialogs first", true);
                return true;
            }
            return false;
        }

        private void ShowHelp(string s)
        {
            if (CheckHelpDlgOpen()) return;

            //help for setting directed CQs
            new Thread(new ThreadStart(delegate
            {
                helpDialogsPending++;
                MessageBox.Show(s, wsjtxClient.pgmName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                helpDialogsPending--;
            })).Start();
        }

        private void cqModeButton_CheckedChanged(object sender, EventArgs e)
        {
            updateReplyNewOnlyCheckBoxEnabled();
            UpdateMinSkipCount();
        }

        private void listenModeButton_CheckedChanged(object sender, EventArgs e)
        {
            updateReplyNewOnlyCheckBoxEnabled();
            UpdateMinSkipCount();
        }

        private void DeleteTextBoxSelection(TextBox textBox)
        {
            if (textBox.SelectionLength > 0)
            {
                int start = textBox.SelectionStart;
                string sel = textBox.Text.Substring(textBox.SelectionStart, textBox.SelectionLength);
                textBox.Text = textBox.Text.Replace(sel, "");
                textBox.SelectionLength = 0;
                textBox.SelectionStart = start;
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
                        //available for ctrl/left-click action
                    }
                }
            }
        }

        private string MyCall()
        {
            return (wsjtxClient == null || wsjtxClient.myCall == null) ? "my call" : wsjtxClient.myCall;
        }

        private void ReplyRR73HelpLabel_Click(object sender, EventArgs e)
        {
            ShowHelp($"Select 'Reply to RR73 msg' if you want to reply '73' to an RR73 message received at the end of a QSO.{Environment.NewLine}{Environment.NewLine}'RR73' means:{Environment.NewLine}- 'Signal report received', and{Environment.NewLine}- 'Best regards', and{Environment.NewLine}- 'I'm confident you will see this', so{Environment.NewLine}- 'No further reply requested'.{Environment.NewLine}{Environment.NewLine}You can safely skip replying to 'RR73' to speed up the QSO cycle, if conditions allow.{Environment.NewLine}{Environment.NewLine}Exceptions:{Environment.NewLine}- If from a new country, RR73 is always replied to with a '73'.{Environment.NewLine}- If a Fox/Hound-style (multi-stream) 'RR73' message, no '73' is expected by the caller, so it's not sent.");
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
    }
}


