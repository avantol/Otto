//NOTE CAREFULLY: Several message classes require the use of a slightly modified WSJT-X program.
//Further information is in the README file.

using System;
//using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WsjtxUdpLib.Messages;
using WsjtxUdpLib.Messages.Out;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Media;

namespace WSJTX_Controller
{
    public class WsjtxClient : IDisposable
    {
        public Controller ctrl;
        public bool altListPaused = false;
        public UdpClient udpClient;
        public int port;
        public IPAddress ipAddress;
        public bool multicast;
        public bool overrideUdpDetect;
        public bool debug;
        public bool advanced;
        public string pgmName;
        public bool diagLog = false;
        public bool paused = true;
        public bool showTxModes = false;
        public int offsetLoLimit = 300;
        public int offsetHiLimit = 2800;
        public bool useRR73 = false;                //applies to non-FT4 modes

        private List<string> acceptableWsjtxVersions = new List<string> { "2.7.0/172" };
        private List<string> supportedModes = new List<string>() { "FT8", "FT4", "JT65", "JT9", "FST4", "MSK144", "Q65" };    //6/7/22

        public int maxPrevCqs = 2;
        public int maxPrevPotaCqs = 4;
        public int maxNewCountryCqs = 16;
        public int maxAutoGenEnqueue = 4;
        public int maxTimeoutCalls = 5;
        public int holdMaxTxRepeat = 50;
        public int holdMaxTxRepeatNewOnBand = 20;
        public bool suspendComm = false;
        public string myCall = null, myGrid = null;

        private StreamWriter logSw = null;
        private StreamWriter potaSw = null;
        private bool settingChanged = false;
        private string cmdCheck = "";
        private bool commConfirmed = false;
        private Dictionary<string, EnqueueDecodeMessage> callDict = new Dictionary<string, EnqueueDecodeMessage>();
        private Queue<string> callQueue = new Queue<string>();
        private List<string> sentReportList = new List<string>();
        private List<string> sentCallList = new List<string>();
        private Dictionary<string, List<EnqueueDecodeMessage>> allCallDict = new Dictionary<string, List<EnqueueDecodeMessage>>();            //all calls to this station plus CQs processed (no 73)
        private Dictionary<string, int> cqCallDict = new Dictionary<string, int>();         //replies to cqs rec'd from specific stations
        private Dictionary<string, int> timeoutCallDict = new Dictionary<string, int>();    //calls sent to myCall immed after timeout
        private bool txEnabled = false;
        private bool txEnabledConf = false;
        private bool wsjtxTxEnableButton = false;
        private bool transmitting = false;
        private bool decoding = false;
        private WsjtxMessage.QsoStates qsoState = WsjtxMessage.QsoStates.CALLING;
        private WsjtxMessage.QsoStates qsoStateConf = WsjtxMessage.QsoStates.CALLING;
        private string mode = "";
        private bool modeSupported = true;
        private string rawMode = "";
        private bool txFirst = false;
        private bool dblClk = false;
        private int? trPeriod = null;       //msec
        private ulong dialFrequency = 0;
        private UInt32 txOffset = 0;
        private string replyCmd = null;     //no "reply to" cmd sent to WSJT-X yet, will not be a CQ
        private string curCmd = null;       //cmd last issed, can be CQ
        private EnqueueDecodeMessage replyDecode = null;
        private string configuration = null;
        private TimeSpan latestDecodeTime;
        private DateTime latestDecodeDate;
        private string callInProg = null;
        private bool restartQueue = false;

        private WsjtxMessage.QsoStates lastQsoState = WsjtxMessage.QsoStates.INVALID;
        private UdpClient udpClient2;
        private IPEndPoint endPoint;
        private bool? lastXmitting = null;
        private bool? lastTxWatchdog = null;
        private string dxCall = null;
        private string lastMode = null;
        private ulong? lastDialFrequency = null;
        private bool? lastTxFirst = null;
        private bool? lastDecoding = null;
        private int? lastSpecOp = null;
        private string lastTxMsg = null;
        private bool? lastTxEnabled = null;
        private string lastCallInProgDebug = null;
        private bool? lastTxTimeoutDebug = null;
        private string lastReplyCmdDebug = null;
        private WsjtxMessage.QsoStates lastQsoStateDebug = WsjtxMessage.QsoStates.INVALID;
        private string lastDxCallDebug = null;
        private string lastTxMsgDebug = null;
        private string lastLastTxMsgDebug = null;
        private bool lastTransmittingDebug = false;
        private bool lastRestartQueueDebug = false;
        private bool lastTxFirstDebug = false;

        private string lastDxCall = null;
        private int xmitCycleCount = 0;
        private bool txTimeout = false;
        private bool newDirCq = false;
        private int specOp = 0;
        private string tCall = null;            //call sign being processed at timeout
        private string txMsg = null;            //msg for the most-recent Tx
        private List<string> logList = new List<string>();      //calls logged for current mode/band for this session
        private Dictionary<string, List<string>> potaLogDict = new Dictionary<string, List<string>>();      //calls logged for any mode/band for this day: "call: date,band,mode"

        private AsyncCallback asyncCallback;
        private UdpState udpSt;
        private static bool messageRecd;
        private static byte[] datagram;
        private static IPEndPoint fromEp = new IPEndPoint(IPAddress.Any, 0);
        private static bool recvStarted;
        private static uint defaultAudioOffset = 1500;
        private string failReason = "Failure reason: Unknown";

        public const int maxQueueLines = 7, maxQueueWidth = 19, maxLogWidth = 9;
        private byte[] ba;
        private EnableTxMessage emsg;
        private WsjtxMessage msg = new UnknownMessage();
        private Random rnd = new Random();
        DateTime firstDecodeTime;
        private const string spacer = "           *";
        private const int freqChangeThreshold = 200;
        private bool firstDecodePass = true;
        private bool skipFirstDecodeSeries = true;
        private System.Windows.Forms.Timer postDecodeTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer processDecodeTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer processDecodeTimer2 = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer reminderTimer = new System.Windows.Forms.Timer();
        public System.Windows.Forms.Timer cmdCheckTimer = new System.Windows.Forms.Timer();
        public System.Windows.Forms.Timer dialogTimer = new System.Windows.Forms.Timer();
        public System.Windows.Forms.Timer dialogTimer2 = new System.Windows.Forms.Timer();
        public System.Windows.Forms.Timer dialogTimer3 = new System.Windows.Forms.Timer();
        string path = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\{Assembly.GetExecutingAssembly().GetName().Name.ToString()}";
        List<int> audioOffsets = new List<int>();
        int oddOffset = 0;
        int lastOddOffsetDebug = 0;
        int evenOffset = 0;
        int lastEvenOffsetDebug = 0;
        const int maxTxTimeHrs = 4;      //hours
        const int maxDecodeAgeMinutes = 30;
        DateTime txStopDateTime = DateTime.MaxValue;
        DateTime txStartDateTime = DateTime.MaxValue;
        public System.Windows.Forms.Timer heartbeatRecdTimer = new System.Windows.Forms.Timer();
        private string cancelledCall = null;
        int maxTxRepeat = 4;
        int holdTxRepeat = 0;
        string curVerBld = null;
        bool disableTxWatchdog = true;
        bool txWarningSound = false;
        bool timedStartInProgress = false;
        int consecCqCount = 0;
        int lastConsecCqCountDebug = 0;
        const int maxConsecCqCount = 8;
        int consecTimeoutCount = 0;
        int lastConsecTimeoutCount = 0;
        const int maxConsecTimeoutCount = 7;
        int consecTxCount = 0;
        int lastConsecTxCountDebug = 0;
        bool lastPausedDebug = true;
        const int maxConsecTxCount = 12;
        const int noSnrAvail = -30;
        const uint tuningAudioOffset = 3200;
        uint prevOffset = defaultAudioOffset;

        private Queue<string> soundQueue = new Queue<string>();
        bool wsjtxClosing = false;
        int heartbeatInterval = 15;           //expected recv interval, sec
        string toCallTxStart = null;
        DateTime txBeginTime = DateTime.MaxValue;
        bool shortTx = false;
        bool txInterrupted = false;

        public TxModes txMode;
        private TxModes lastTxModeDebug;

        private struct UdpState
        {
            public UdpClient u;
            public IPEndPoint e;
        }

        public enum TxModes
        {
            LISTEN,
            CALL_CQ
        }

        private enum OpModes
        {
            IDLE,
            START,
            ACTIVE
        }
        private OpModes opMode;

        public enum CallPriority
        {
            RESERVED,
            NEW_COUNTRY,            //1
            NEW_COUNTRY_ON_BAND,    //2
            TO_MYCALL,              //3
            MANUAL_CQ,              //4
            WANTED_CQ,              //5
            DEFAULT                 //6
        }

        private enum Periods
        {
            UNK,
            ODD,
            EVEN
        }
        private Periods period;

        private enum autoFreqPauseModes
        {
            DISABLED,
            ENABLED,
            ACTIVE
        }
        private autoFreqPauseModes autoFreqPauseMode;
        private autoFreqPauseModes lastAutoFreqPauseModeDebug = autoFreqPauseModes.DISABLED;

        public WsjtxClient(Controller c, IPAddress reqIpAddress, int reqPort, bool reqMulticast, bool reqOverrideUdpDetect, bool reqDebug, bool reqLog)
        {
            ctrl = c;           //used for accessing/updating UI
            ipAddress = reqIpAddress;
            port = reqPort;
            multicast = reqMulticast;
            overrideUdpDetect = reqOverrideUdpDetect;
            //major.minor.build.private
            string allVer = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            Version v;
            Version.TryParse(allVer, out v);
            string fileVer = $"{v.Major}.{v.Minor}.{v.Build}";
            WsjtxMessage.PgmVersion = fileVer;
            debug = reqDebug;
            opMode = OpModes.IDLE;              //wait for WSJT-X running to read its .INI
            WsjtxMessage.NegoState = WsjtxMessage.NegoStates.INITIAL;
            pgmName = ctrl.Text;      //or Assembly.GetExecutingAssembly().GetName().ToString();

            if (reqLog)            //request log file open
            {
                diagLog = SetLogFileState(true);
                if (diagLog)
                {
                    DebugOutput($"\n\n\n{DateTime.UtcNow.ToString("yyyy-MM-dd HHmmss")} UTC ###################### Program starting.... v{fileVer}");
                }
            }

            DebugOutput($"{spacer}dispFactor:{ctrl.dispFactor}");

            ClearAudioOffsets();
            if (ctrl.freqCheckBox.Checked) WsjtxSettingChanged();

            ResetNego();
            UpdateDebug();

            DebugOutput($"{Time()} NegoState:{WsjtxMessage.NegoState}");
            DebugOutput($"{Time()} opMode:{opMode}");
            DebugOutput($"{Time()} Waiting for WSJT-X to run...");

            ShowStatus();
            ShowQueue();
            ShowLogged();
            messageRecd = false;
            recvStarted = false;

            ctrl.verLabel.Text = $"by WM8Q v{fileVer}";
            ctrl.verLabel2.Text = "more.avantol@xoxy.net";
            ctrl.verLabel3.Text = "Comments?";

            ctrl.timeoutLabel.Visible = false;

            UpdateModeSelection();
            UpdateModeVisible();
            UpdateTxTimeEnable();

            emsg = new EnableTxMessage();
            emsg.Id = WsjtxMessage.UniqueId;

            firstDecodeTime = DateTime.MinValue;

            postDecodeTimer.Interval = 4000;
            postDecodeTimer.Tick += new System.EventHandler(ProcessPostDecodeTimerTick);

            processDecodeTimer.Tick += new System.EventHandler(ProcessDecodeTimerTick);

            processDecodeTimer2.Tick += new System.EventHandler(ProcessDecodeTimer2Tick);

            reminderTimer.Interval = 10000;
            reminderTimer.Tick += new System.EventHandler(reminderTimerTick);

            cmdCheckTimer.Tick += new System.EventHandler(cmdCheckTimer_Tick);

            dialogTimer.Tick += new System.EventHandler(dialogTimer_Tick);
            dialogTimer.Interval = 2000;

            dialogTimer2.Tick += new System.EventHandler(dialogTimer2_Tick);
            dialogTimer2.Interval = 20;

            dialogTimer3.Tick += new System.EventHandler(dialogTimer3_Tick);
            dialogTimer3.Interval = 20;

            ReadPotaLogDict();

            heartbeatRecdTimer.Interval = 4 * heartbeatInterval * 1000;            //heartbeats every 15 sec
            heartbeatRecdTimer.Tick += new System.EventHandler(HeartbeatNotRecd);

            Task task = new Task(new Action(ProcSoundQueue));
            task.Start();

            UpdateMaxTxRepeat();

            var dtNow = DateTime.UtcNow;
            latestDecodeDate = dtNow.Date;
            latestDecodeTime = dtNow.TimeOfDay;

            UpdateDebug();          //last before starting loop
        }

        //override auto IP addr, port, and/or mode with new values
        public void UpdateAddrPortMulti(IPAddress reqIpAddress, int reqPort, bool reqMulticast, bool reqOverrideUdpDetect)
        {
            ipAddress = reqIpAddress;
            port = reqPort;
            multicast = reqMulticast;
            overrideUdpDetect = reqOverrideUdpDetect;
            ResetNego();
            CloseAllUdp();
        }

        public void ReceiveCallback(IAsyncResult ar)
        {
            datagram = null;
            messageRecd = true;

            try
            {
                if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.WAIT) return;
                UdpClient u = ((UdpState)(ar.AsyncState)).u;
                if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.WAIT) return;
                fromEp = ((UdpState)(ar.AsyncState)).e;
                if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.WAIT) return;
                datagram = u.EndReceive(ar, ref fromEp);
            }
            catch (Exception err)
            {
#if DEBUG
                Console.WriteLine($"Exception: ReceiveCallback() {err}");
#endif
                return;
            }

            //DebugOutput($"Received: {receiveString}");
        }

        public void UdpLoop()
        {
            if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.WAIT)
            {
                if (!suspendComm) CheckWsjtxRunning();            //re-init if so
                return;
            }
            else
            {
                bool notRunning = !IsWsjtxRunning();
                if (notRunning || wsjtxClosing)
                {
                    DebugOutput($"\n{Time()} WSJT-X notRunning:{notRunning} wsjtxClosing:{wsjtxClosing}");
                    ResetNego();
                    CloseAllUdp();
                    wsjtxClosing = false;
                    ctrl.ShowMsg("WSJT-X closed", true);
                }
            }

            //timer expires at 11-12 msec minimum (due to OS limitations)
            if (messageRecd)
            {
                if (datagram != null) Update();
                messageRecd = false;
                recvStarted = false;
            }
            // Receive a UDP datagram
            if (!recvStarted)
            {
                if (udpClient == null || WsjtxMessage.NegoState == WsjtxMessage.NegoStates.WAIT) return;
                udpClient.BeginReceive(asyncCallback, udpSt);
                recvStarted = true;
            }
        }

        private void CheckWsjtxRunning()
        {
            if (IsWsjtxRunning())
            {
                DebugOutput($"\n{Time()} WSJT-X running");
                ctrl.ShowMsg("WSJT-X detected", false);
                Thread.Sleep(3000);     //wait for WSJT-X to open UDP

                bool retry = true;
                while (retry)
                {
                    if (!overrideUdpDetect)
                    {
                        if (!DetectUdpSettings(out ipAddress, out port, out multicast))
                        {
                            DebugOutput($"{spacer}unable to get IP address from WSJT-X");
                            heartbeatRecdTimer.Stop();
                            suspendComm = true;
                            ctrl.BringToFront();
                            MessageBox.Show($"Unable to auto-detect WSJT-X's UDP IP address and port.\n\nAt 'Setup', select 'Override' and enter these manually.", pgmName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            ctrl.setupButton_Click(null, null);
                            return;                 //suspendComm set to false at Setup close
                        }
                    }


                    DebugOutput($"{spacer}ipAddress:{ipAddress} port:{port} multicast:{multicast}");
                    string modeStr = multicast ? "multicast" : "unicast";
                    try
                    {
                        if (multicast)
                        {
                            udpClient = new UdpClient();
                            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            udpClient.Client.Bind(endPoint = new IPEndPoint(IPAddress.Any, port));
                            udpClient.JoinMulticastGroup(ipAddress);
                        }
                        else
                        {
                            udpClient = new UdpClient(endPoint = new IPEndPoint(ipAddress, port));
                        }
                        DebugOutput($"{spacer}opened udpClient:{udpClient}");
                        retry = false;
                    }
                    catch (Exception e)
                    {
                        e.ToString();
                        DebugOutput($"{spacer}unable to open udpClient:{udpClient}\n{e}");
                        heartbeatRecdTimer.Stop();
                        suspendComm = true;
                        ctrl.BringToFront();
                        if (MessageBox.Show($"Unable to open WSJT-X's specified UDP port,\naddress: {ipAddress}\nport: {port}\nmode: {modeStr}.\n\nIn WSJT-X, select File | Settings | Reporting.\nAt 'UDP Server':\n- Enter '239.255.0.0' for 'UDP Server\n- Enter '2237' for 'UDP Server port number'\n- Select all checkboxes at 'Outgoing interfaces'\n- Click 'Retry' below to try opening the UDP port again.\n\nAlternatively:\n- Click 'Cancel' below for Controller's 'Setup'\n- Enter the UDP address and port as shown in WSJT-X, or\n- Select 'Override' to use auto-detected UDP settings.", pgmName, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) == DialogResult.Cancel)
                        {
                            ctrl.setupButton_Click(null, null);
                            return;                 //suspendComm set to false at Setup close
                        }
                    }
                }
                suspendComm = false;

                udpSt = new UdpState();
                udpSt.e = endPoint;
                udpSt.u = udpClient;
                asyncCallback = new AsyncCallback(ReceiveCallback);

                WsjtxMessage.NegoState = WsjtxMessage.NegoStates.INITIAL;
                DebugOutput($"{spacer}NegoState:{WsjtxMessage.NegoState}");

                if (!suspendComm)
                {
                    ctrl.initialConnFaultTimer.Interval = 3 * heartbeatInterval * 1000;           //pop up dialog showing UDP corrective info at tick
                    ctrl.initialConnFaultTimer.Start();
                }
            }
        }

        public bool ConnectedToWsjtx()
        {
            return opMode == OpModes.ACTIVE;
        }

        public void DebugChanged()
        {
            ShowQueue();
            UpdateCallInProg();
        }

        public void AutoFreqChanged(bool autoFreqEnabled, bool bandOrModeChanged)
        {
            DisableAutoFreqPause();
            if (autoFreqEnabled)
            {
                //if (commConfirmed) EnableMonitoring();       may crash WSJT-X
                if (opMode != OpModes.ACTIVE)
                {
                    ctrl.freqCheckBox.Text = "Select best Tx frequency (pending)";
                    ctrl.freqCheckBox.ForeColor = Color.DarkGreen;
                    return;
                }
                if (oddOffset > 0 && evenOffset > 0)
                {
                    ctrl.freqCheckBox.Text = "Select best Tx frequency";
                    return;
                }

                ctrl.freqCheckBox.Text = "Select best Tx frequency (pending)";
                ctrl.freqCheckBox.ForeColor = Color.DarkGreen;

                paused = true;
                StopDecodeTimers();
                DisableTx(false);
                opMode = OpModes.START;
                if (bandOrModeChanged)
                {
                    txTimeout = false;
                    tCall = null;
                    replyCmd = null;
                    curCmd = null;
                    replyDecode = null;
                    newDirCq = false;
                    dxCall = null;
                    xmitCycleCount = 0;
                    SetCallInProg(null);
                    UpdateCallInProg();
                }
                UpdateModeVisible();
                UpdateModeSelection();
                UpdateTxTimeEnable();
                ShowStatus();
                DebugOutput($"{Time()} AutoFreqChanged enabled:true, bandOrModeChanged:{bandOrModeChanged} evenOffset:{evenOffset} oddOffset:{oddOffset} opMode:{opMode} NegoState:{WsjtxMessage.NegoState}");
            }
            else
            {
                ctrl.freqCheckBox.Text = "Select best Tx frequency";
                ctrl.freqCheckBox.ForeColor = Color.Black;
                DebugOutput($"{Time()} AutoFreqChanged enabled:false");
                CheckActive();
            }
            UpdateDebug();
        }

        public void timedCheckBoxChanged()
        {
            int value24;
            try
            {
                if (ctrl.timedCheckBox.Checked)
                {
                    string stopText = ctrl.stopTextBox.Text.Trim();
                    if (stopText.Length != 4 || !int.TryParse(stopText, out value24) || value24 < 0 || value24 > 2359 || Convert.ToInt32(stopText.Substring(2, 2)) > 59)
                    {
                        ctrl.ShowMsg("Use 24-hour format, 4 digits (ex: 0900)", true);
                        ctrl.stopTextBox.Focus();
                        ctrl.timedCheckBox.Checked = false;
                        return;
                    }

                    string startText = ctrl.startTextBox.Text.Trim();
                    if (startText.Length != 0 && (startText.Length != 4 || !int.TryParse(startText, out value24) || value24 < 0 || value24 > 2359 || Convert.ToInt32(startText.Substring(2, 2)) > 59))
                    {
                        ctrl.ShowMsg("Use 24-hour format, 4 digits (ex: 0900)", true);
                        ctrl.startTextBox.Focus();
                        ctrl.timedCheckBox.Checked = false;
                        return;
                    }

                    DateTime dtNow = DateTime.Now;          //local

                    DateTime dtStart = dtNow;
                    if (startText.Length != 0) dtStart = ScheduledOnDateTime();

                    DateTime dtStop = ScheduledOffDateTime();

                    if (dtStart < dtNow)
                    {
                        dtStart = dtStart.AddHours(24);
                    }

                    if (dtStop <= dtStart)
                    {
                        dtStop = dtStop.AddHours(24);
                    }

                    if (dtStop <= dtStart)
                    {
                        ctrl.ShowMsg($"Start Tx time is after stop time", true);
                        ctrl.timedCheckBox.Checked = false;
                        return;
                    }

                    if (((dtStop - dtStart).TotalMinutes > maxTxTimeHrs * 60))
                    {
                        ctrl.ShowMsg($"Maximum Tx time is {maxTxTimeHrs} hours", true);
                        ctrl.timedCheckBox.Checked = false;
                        return;
                    }

                    if (startText.Length == 0)
                    {
                        txStartDateTime = DateTime.MaxValue;
                        ctrl.ShowMsg($"Transmit stops in {(dtStop - dtStart).Hours}h {(dtStop - dtStart).Minutes}m", false);
                    }
                    else
                    {
                        //timed start/stop enabled
                        txStartDateTime = dtStart;
                        ctrl.ShowMsg($"Transmit starts in {(dtStart - dtNow).Hours}h {(dtStart - dtNow).Minutes}m", false);
                        Play("beepbeep.wav");
                        if (!paused)                //pause until time to start
                        {
                            Pause(true);
                            DebugOutput($"{spacer}tx stopped(3), paused:{paused}");
                        }
                        ClearCalls(false);
                        SetCallInProg(null);
                        restartQueue = false;           //get ready for next decode phase
                        txTimeout = false;              //ready for next timeout
                        tCall = null;
                    }

                    txStopDateTime = dtStop;
                    DebugOutput($"{Time()} Timed operation enabled, txStartDateTime:{txStartDateTime}, txStopDateTime:{txStopDateTime}, Tx duration:{txStopDateTime - txStartDateTime}");
                }
                else   //checkbox not checked
                {
                    txWarningSound = false;
                    timedStartInProgress = false;
                    DebugOutput($"{Time()} Timed operation disabled");
                }
            }
            finally
            {
                ShowStatus();
                UpdateStartStopTime();
                UpdateTxTimeEnable();
            }
        }

        public void HoldCheckBoxChanged()
        {
            DebugOutput($"{Time()} HoldCheckBoxChanged holdCheckBox.Checked:{ctrl.holdCheckBox.Checked} holdCheckBox.Enabled:{ctrl.holdCheckBox.Enabled}");
            if (ctrl.holdCheckBox.Checked /*|| (mode == "MSK144" && modeSupported)*/)
            {
                ctrl.limitLabel.Enabled = false;
                ctrl.repeatLabel.Enabled = false;
                ctrl.timeoutNumUpDown.Enabled = false;
                ctrl.optimizeCheckBox.Enabled = false;
            }
            else
            {
                ctrl.limitLabel.Enabled = true;
                ctrl.repeatLabel.Enabled = true;
                ctrl.timeoutNumUpDown.Enabled = true;
                ctrl.optimizeCheckBox.Enabled = true;
            }
            ShowStatus();
        }

        private void UpdateStartStopTime()
        {
            DateTime dtNow = DateTime.Now;      //local

            if (ctrl.timedCheckBox.Checked)
            {
                ctrl.timeoutLabel.Visible = true;
                ctrl.timeoutLabel.ForeColor = Color.Black;
                ctrl.timeoutLabel.Font = new Font(Label.DefaultFont, FontStyle.Regular);

                if (txStartDateTime == DateTime.MaxValue)
                {
                    ctrl.timeoutLabel.Text = $"(Tx stops {RemainingTimeDesc(txStopDateTime - dtNow)})";
                }
                else
                {
                    if (paused && txStartDateTime > dtNow)
                    {
                        ctrl.timeoutLabel.ForeColor = Color.Red;
                        ctrl.timeoutLabel.Font = new Font(Label.DefaultFont, FontStyle.Bold);
                        string action = ctrl.modeComboBox.SelectedIndex == 0 ? "starts" : "enabled";
                        string prompt = $"Tx {action} {RemainingTimeDesc(txStartDateTime - dtNow)}";
                        ctrl.timeoutLabel.Text = $"({prompt})";
                        if ((txStartDateTime - dtNow).TotalMinutes < 1 && !txWarningSound)
                        {
                            ctrl.ShowMsg(prompt, false);
                            Play("beepbeep.wav");
                            txWarningSound = true;
                        }
                    }
                    else
                    {
                        ctrl.timeoutLabel.Text = $"(Tx stops {RemainingTimeDesc(txStopDateTime - dtNow)})";
                    }
                }
            }
            else
            {
                ctrl.timeoutLabel.Visible = false;
            }
        }

        private string RemainingTimeDesc(TimeSpan ts)
        {
            if (ts.TotalMinutes < 1)
            {
                return "soon";
            }
            else
            {
                int addHrs = 0;
                if (ts.TotalHours > 24) addHrs = 24;
                return $"in {ts.Hours + addHrs}h {ts.Minutes}m";
            }
        }

        //log file mode requested to be (possibly) changed
        public void LogModeChanged(bool enable)
        {
            if (enable == diagLog) return;       //no change requested

            diagLog = SetLogFileState(enable);
        }

        private void TxModeEnabled()        //WSJT-X "Enable Tx" button checked
        {
            //marker1
            DebugOutput($"\n{Time()} TxModeEnabled, txMode:{txMode} paused:{paused} txEnabled:{txEnabled}");

            if (txEnabled) return;

            bool prevTxEnabled = txEnabled;

            if (ctrl.timedCheckBox.Checked)
            {
                if ((txStopDateTime - DateTime.Now).TotalMinutes > maxTxTimeHrs * 60)       //local
                {
                    HaltTx();
                    ctrl.ShowMsg($"Timed operation Tx limited to {maxTxTimeHrs} hour duration", true);
                    return;
                }
            }

            if (txMode == TxModes.LISTEN && !ctrl.replyLocalCheckBox.Checked && !ctrl.replyDxCheckBox.Checked && !ctrl.replyDirCqCheckBox.Checked && !ctrl.replyNewDxccCheckBox.Checked && !ctrl.replyDxCheckBox.Checked && !ctrl.replyLocalCheckBox.Checked)
            {
                reminderTimer.Start();
            }

            paused = false;
            UpdateModeSelection();
            UpdateMaxTxRepeat();
            DisableAutoFreqPause();
            DebugOutput($"{spacer}txMode:{txMode} paused:{paused} callInProg:'{CallPriorityString(callInProg)}' txTimeout:{txTimeout} callQueue.Count:{callQueue.Count} tCall:'{tCall}'");

            if (txMode == TxModes.LISTEN)
            {
                if (callInProg != null && replyDecode != null && replyDecode.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND)
                {
                    ctrl.holdCheckBox.Enabled = true;
                    ctrl.holdCheckBox.Checked = true;
                }
                else
                {
                    ctrl.holdCheckBox.Checked = false;
                }
            }
            else        //CQ mode
            {
                ctrl.holdCheckBox.Checked = false;

                CheckCallQueuePeriod(txFirst);        //remove queued calls from wrong time period
            }

            if (callInProg != null)       //finish call in progress
            {
                EnableTx();       //start replying immediately
            }
            else
            {
                if ((callInProg == null && callQueue.Count == 0) || (callInProg != null && callInProg == tCall))  //no call in progress/queued, or call just timed out so don't continue this call
                {
                    txTimeout = true;       //start CQing immediately
                    DebugOutput($"{spacer}txTimeout:{txTimeout} tCall:{tCall}");
                    if (callInProg != null && callInProg == tCall) LogBeep();
                }

                CheckNextXmit();
            }

            ctrl.ShowMsg($"CAUTION: Automatic transmit!", false);
            Play("beepbeep.wav");

            ShowStatus();
            UpdateMaxTxRepeat();
            UpdateStartStopTime();
            UpdateDebug();
        }

        public void TxModeChanged(TxModes newMode)
        {
            //marker1
            TxModes prevTxMode = txMode;
            txMode = newMode;
            DebugOutput($"\n{Time()} TxModeChanged, txMode:{txMode} paused:{paused} txEnabled:{txEnabled}");
            SetListenMode();
            UpdateModeSelection();

            if (callInProg != null && callInProg == tCall)        //just timed out so don't continue this call
            {
                txTimeout = true;
                DebugOutput($"{spacer}{tCall} timed out, txTimeout:{txTimeout} callInProg:{callInProg} tCall:{tCall}");
                LogBeep();
            }

            if (wsjtxTxEnableButton)
            {
                if (txMode == TxModes.CALL_CQ && prevTxMode == TxModes.LISTEN)        //WSJT-X "Enable Tx" button is checked
                {
                    EnableTx();       //set WSJT-X tx to enabled and set "Enable Tx" button state to checked
                    CheckCallQueuePeriod(txFirst);        //remove queued calls from wrong time period
                    if (callInProg == null && callQueue.Count == 0) txTimeout = true;    //start CQ immediately
                }

                if (txMode == TxModes.LISTEN && prevTxMode == TxModes.CALL_CQ)        //WSJT-X "Enable Tx" button is checked
                {
                    if (callInProg != null || callQueue.Count > 0)      //continue with tx
                    {
                        EnableTx();     //set WSJT-X tx to enabled and set "Enable Tx" button state to checked
                    }
                    else                //no calls to process, prepare for next call
                    {
                        HaltTx();           //stop CQing immediately
                        DisableTx(true);    //set WSJT-X tx to disabled and set "Enable Tx" button state to checked
                    }
                }

                CheckNextXmit();
            }

            ShowStatus();
            UpdateDebug();
        }

        public void TxRepeatChanged()
        {
            UpdateMaxTxRepeat();
            DebugOutput($"{Time()} TxRepeatChanged, optimize queue: {ctrl.optimizeCheckBox.Checked} manual value:{(int)ctrl.timeoutNumUpDown.Value} maxTxRepeat:{maxTxRepeat} maxPrevCqs:{maxPrevCqs} maxAutoGenEnqueue:{maxAutoGenEnqueue}");
            UpdateDebug();
        }

        public void UpdateCallInProg()
        {
            string callWithPriority = callInProg;
            if (debug)
            {
                callWithPriority = CallPriorityString(callInProg);
            }
            else
            {
                if (Priority(callInProg) <= (int)CallPriority.NEW_COUNTRY_ON_BAND) callWithPriority = $"{callInProg}{"*"}";
            }

            if (!showTxModes)
            {
                if (callInProg == null)
                {
                    ctrl.inProgLabel.Visible = false;
                    ctrl.inProgTextBox.Visible = false;
                    ctrl.inProgTextBox.Text = "";
                }
                else
                {
                    ctrl.inProgTextBox.Text = callWithPriority;
                    ctrl.inProgTextBox.Visible = true;
                    ctrl.inProgLabel.Visible = true;
                }
            }
            else
            {
                if (ctrl.statusMsgTimer.Enabled) return;
                if (callInProg == null)
                {
                    ctrl.msgTextBox.Text = "";
                }
                else
                {
                    ctrl.msgTextBox.Text = $"In progress: {callWithPriority}";
                }
            }
        }

        public void WsjtxSettingChanged()
        {
            settingChanged = true;
            newDirCq = true;
        }

        public void Pause(bool haltTx)         //go to pause mode, optionally halt Tx
        {
            if (haltTx) HaltTx();
            paused = true;
            txEnabled = false;
            ctrl.holdCheckBox.Checked = false;
            DebugOutput($"{Time()} Pause paused:{paused}");
            DisableAutoFreqPause();
            StopDecodeTimers();

            ShowStatus();
            UpdateMaxTxRepeat();
            UpdateStartStopTime();
            UpdateDebug();
        }

        public void UpdateModeVisible()
        {
            if (advanced && opMode == OpModes.ACTIVE)
            {
                if (showTxModes)
                {
                    ctrl.listenModeButton.Visible = true;
                    ctrl.cqModeButton.Visible = true;
                    ctrl.modeHelpLabel.Visible = true;
                    ctrl.modeGroupBox.Visible = true;
                }
                else
                {
                    ctrl.inProgTextBox.Visible = true;
                }
            }
            else
            {
                ctrl.listenModeButton.Visible = false;
                ctrl.cqModeButton.Visible = false;
                ctrl.modeHelpLabel.Visible = false;
                ctrl.modeGroupBox.Visible = false;
                ctrl.modeGroupBox.Visible = false;
            }
            DebugOutput($"{spacer}UpdateModeVisible, advanced:{advanced} showTxModes:{showTxModes} txMode:{txMode}");
        }

        private void Update()
        {
            if (suspendComm) return;

            try
            {
                msg = WsjtxMessage.Parse(datagram);
                //DebugOutput($"{Time()} msg:{msg} datagram[{datagram.Length}]:\n{DatagramString(datagram)}");
            }
            catch (ParseFailureException ex)
            {
                //File.WriteAllBytes($"{ex.MessageType}.couldnotparse.bin", ex.Datagram);
                DebugOutput($"{Time()} ERROR: Parse failure {ex.InnerException.Message}");
                DebugOutput($"datagram[{datagram.Length}]: {DatagramString(datagram)}");
                return;
            }

            if (msg == null)
            {
                DebugOutput($"{Time()} ERROR: null message, datagram[{datagram.Length}]: {DatagramString(datagram)}");
                return;
            }

            //rec'd first HeartbeatMessage
            //check version, send requested schema version
            //request a StatusMessage
            //go from INIT to SENT state
            if (msg.GetType().Name == "HeartbeatMessage" && (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.INITIAL || WsjtxMessage.NegoState == WsjtxMessage.NegoStates.FAIL))
            {
                ctrl.initialConnFaultTimer.Stop();             //stop connection fault dialog
                HeartbeatMessage imsg = (HeartbeatMessage)msg;
                DebugOutput($"{Time()}\n{imsg}");
                curVerBld = $"{imsg.Version}/{imsg.Revision}";
                if (!acceptableWsjtxVersions.Contains(curVerBld))
                {
                    heartbeatRecdTimer.Stop();
                    suspendComm = true;
                    ctrl.BringToFront();
                    MessageBox.Show($"WSJT-X v{imsg.Version}/{imsg.Revision} is not supported.\n\nSupported WSJT-X version(s):\n{AcceptableVersionsString()}\n\nYou can check the WSJT-X version/build by selecting 'Help | About' in WSJT-X.\n\n{pgmName} will try again when you close this dialog.", pgmName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ResetOpMode();
                    suspendComm = false;
                    UpdateDebug();
                    return;
                }
                else
                {
                    if (udpClient2 != null)
                    {
                        udpClient2.Close();
                        udpClient2 = null;
                        DebugOutput($"{spacer}closed udpClient2:{udpClient2}");
                    }

                    var tmsg = new HeartbeatMessage();
                    tmsg.SchemaVersion = WsjtxMessage.PgmSchemaVersion;
                    tmsg.MaxSchemaNumber = (uint)WsjtxMessage.PgmSchemaVersion;
                    tmsg.SchemaVersion = WsjtxMessage.PgmSchemaVersion;
                    tmsg.Id = WsjtxMessage.UniqueId;
                    tmsg.Version = WsjtxMessage.PgmVersion;
                    tmsg.Revision = WsjtxMessage.PgmRevision;

                    ba = tmsg.GetBytes();
                    udpClient2 = new UdpClient();
                    udpClient2.Connect(fromEp);
                    udpClient2.Send(ba, ba.Length);
                    WsjtxMessage.NegoState = WsjtxMessage.NegoStates.SENT;
                    UpdateDebug();
                    DebugOutput($"{spacer}NegoState:{WsjtxMessage.NegoState}");
                    DebugOutput($"{Time()} >>>>>Sent'Heartbeat' msg:\n{tmsg}");
                    ShowStatus();
                    ctrl.ShowMsg("WSJT-X responding", false);
                }
                UpdateDebug();
                return;
            }

            //rec'd negotiation HeartbeatMessage
            //send another request for a StatusMessage
            //go from SENT to RECD state
            if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.SENT && msg.GetType().Name == "HeartbeatMessage")
            {
                HeartbeatMessage hmsg = (HeartbeatMessage)msg;
                DebugOutput($"{Time()}\n{hmsg}");
                WsjtxMessage.NegotiatedSchemaVersion = hmsg.SchemaVersion;
                WsjtxMessage.NegoState = WsjtxMessage.NegoStates.RECD;
                UpdateDebug();
                DebugOutput($"{spacer}NegoState:{WsjtxMessage.NegoState}");
                DebugOutput($"{spacer}negotiated schema version:{WsjtxMessage.NegotiatedSchemaVersion}");
                UpdateDebug();

                //send ACK request to WSJT-X, to get 
                //a StatusMessage reply to start normal operation
                Thread.Sleep(250);
                emsg.NewTxMsgIdx = 7;
                emsg.GenMsg = $"";          //no effect
                emsg.ReplyReqd = true;
                emsg.EnableTimeout = !debug;
                emsg.CmdCheck = cmdCheck;
                ba = emsg.GetBytes();
                udpClient2.Send(ba, ba.Length);
                DebugOutput($"{Time()} >>>>>Sent 'Ack Req' cmd:7 cmdCheck:{cmdCheck}\n{emsg}");

                HaltTx();       //sync up WSJT-X button state

                cmdCheckTimer.Interval = 10000;           //set up cmd check timeout
                cmdCheckTimer.Start();
                DebugOutput($"{spacer}check cmd timer started");
                return;
            }

            //while in INIT or SENT state:
            //get minimal info from StatusMessage needed for faster startup
            //and for special case of ack msg returned by WSJT-X after req for StatusMessage
            //check for no call sign or grid, exit if so;
            //calculate best offset frequency;
            //also get decode offset frequencies for best offest calculation
            if (WsjtxMessage.NegoState != WsjtxMessage.NegoStates.RECD)
            {
                if (msg.GetType().Name == "StatusMessage")
                {
                    StatusMessage smsg = (StatusMessage)msg;
                    DebugOutput($"\n{Time()}\n{smsg}");
                    if (smsg.TRPeriod != null) trPeriod = (int)smsg.TRPeriod;

                    if (trPeriod != null)
                    {
                        decoding = smsg.Decoding;
                        DebugOutput($"{spacer}decoding:{decoding} lastDecoding:{lastDecoding} firstDecodePass:{firstDecodePass}");
                        if (decoding != lastDecoding)
                        {
                            if (decoding)
                            {
                                postDecodeTimer.Stop();
                                postDecodeTimer.Start();                    //restart timer at every decode, will time out after last decode
                                DebugOutput($"{spacer}postDecodeTimer.Enabled:{postDecodeTimer.Enabled}");

                                if (firstDecodePass)
                                {
                                    SetPeriodState();
                                }
                            }
                            else
                            {
                                if (lastDecoding != null)           //need to start with decoding = true
                                {
                                    if (firstDecodePass)
                                    {
                                        //first calcluation of best offset
                                        DebugOutput($"{spacer}audioOffsets.Count:{audioOffsets.Count}");
                                        CalcBestOffset(audioOffsets, period, false);
                                        firstDecodePass = false;
                                        DebugOutput($"{spacer}firstDecodePass:{firstDecodePass}");
                                    }
                                }
                            }
                        }
                        lastDecoding = decoding;
                    }

                    txEnabledConf = smsg.TxEnabled;
                    if (txEnabledConf != lastTxEnabled)         //lastTxEnabled can be null
                    {
                        if (txEnabledConf)
                        {
                            ctrl.ShowMsg("Not ready yet... please wait", true);
                        }
                    }
                    lastTxEnabled = txEnabledConf;

                    wsjtxTxEnableButton = smsg.TxEnableButton;          //keep WSJT-X "Enable Tx" button state current

                    mode = smsg.Mode;
                    if (lastMode == null) lastMode = mode;
                    if (mode != lastMode)
                    {
                        DebugOutput($"{Time()}mode changed, firstDecodePass:{firstDecodePass} lastDecoding:{lastDecoding}");
                        ClearAudioOffsets();
                    }
                    lastMode = mode;

                    dialFrequency = smsg.DialFrequency;
                    if (lastDialFrequency == null) lastDialFrequency = dialFrequency;
                    if (lastDialFrequency != null && (Math.Abs((float)lastDialFrequency - (float)dialFrequency) > freqChangeThreshold))
                    {
                        DebugOutput($"{Time()}frequency changed, firstDecodePass:{firstDecodePass} lastDecoding:{lastDecoding}");
                        ClearAudioOffsets();
                    }
                    lastDialFrequency = dialFrequency;

                    UpdateRR73();
                    specOp = (int)smsg.SpecialOperationMode;
                    CheckModeSupported();

                    configuration = smsg.ConfigurationName;
                    if (!CheckMyCall(smsg)) return;
                    DebugOutput($"{Time()}\nStatus     myCall:'{myCall}' myGrid:'{myGrid}' mode:{mode} specOp:{specOp} configuration:{configuration} check:{smsg.Check}");
                    UpdateDebug();
                }

                if (msg.GetType().Name == "EnqueueDecodeMessage")
                {
                    EnqueueDecodeMessage qmsg = (EnqueueDecodeMessage)msg;
                    if (qmsg.DeltaFrequency > offsetLoLimit && qmsg.DeltaFrequency < offsetHiLimit) audioOffsets.Add(qmsg.DeltaFrequency);
                    
                    if (!qmsg.AutoGen)
                        ctrl.ShowMsg("Not ready yet... please wait", true);
                }
            }

            //************
            //CloseMessage
            //************
            if (msg.GetType().Name == "CloseMessage")
            {
                DebugOutput($"\n{Time()} CloseMessage rec'd\n{Time()}\n{msg}");
                if (WsjtxMessage.NegoState != WsjtxMessage.NegoStates.WAIT) wsjtxClosing = true;
                DebugOutput($"{spacer}NegoState:{WsjtxMessage.NegoState} wsjtxClosing:{wsjtxClosing}");
                return;
            }

            //****************
            //HeartbeatMessage
            //****************
            //in case 'Monitor' disabled, get StatusMessages
            if (msg.GetType().Name == "HeartbeatMessage")
            {
                DebugOutput($"\n{Time()} WSJT-X event, heartbeat rec'd:\n{msg}");
                emsg.NewTxMsgIdx = 7;
                emsg.GenMsg = $"";          //no effect
                emsg.ReplyReqd = (opMode != OpModes.ACTIVE);
                emsg.EnableTimeout = !debug;
                if (emsg.ReplyReqd) cmdCheck = RandomCheckString();
                emsg.CmdCheck = cmdCheck;
                ba = emsg.GetBytes();
                udpClient2.Send(ba, ba.Length);
                //DebugOutput($"{Time()} >>>>>Sent 'Ack Req' cmd:7 cmdCheck:{cmdCheck}\n{emsg}");

                heartbeatRecdTimer.Stop();
                if (!debug)
                {
                    heartbeatRecdTimer.Start();
                    //DebugOutput($"{spacer}heartbeatRecdTimer restarted");
                }

                if (disableTxWatchdog)
                {
                    emsg.NewTxMsgIdx = 13;      //reset watchdog timer
                    emsg.GenMsg = $"";          //no effect
                    emsg.ReplyReqd = false;     //no effect
                    emsg.EnableTimeout = true;  //no effect
                    emsg.CmdCheck = "";         //no effect
                    ba = emsg.GetBytes();
                    udpClient2.Send(ba, ba.Length);
                    //DebugOutput($"{Time()} >>>>>Sent 'Reset Tx watchdog' cmd:13");
                }

                //if (ctrl.freqCheckBox.Checked && commConfirmed) EnableMonitoring();   may crash WSJT-X
                CheckTimedStartStop();
            }

            if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.RECD)
            {
                if (modeSupported)
                {
                    //********************
                    //EnqueueDecodeMessage
                    //********************
                    //only resulting action is to add call to callQueue, optionally restart queue
                    if (msg.GetType().Name == "EnqueueDecodeMessage" && myCall != null)
                    {
                        EnqueueDecodeMessage dmsg = (EnqueueDecodeMessage)msg;
                        if (!dmsg.Message.Contains(";"))
                        {
                            //normal (not "special operating activity") message
                            ProcessDecodeMsg(dmsg, false);
                        }
                        else
                        {
                            //fox/hound-style (multi-target) message: process as two separate decodes (note: full f/h mode not supported)
                            // 0    1     2    3   4
                            //W1AW RR73; WM8Q T2C -02
                            string msg = dmsg.Message;
                            DebugOutput($"\n{Time()} F/H msg detected: {msg}");
                            string[] words = msg.Replace(";", "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (words.Length != 5) return;

                            EnqueueDecodeMessage dmsg1 = dmsg;
                            dmsg1.Message = $"{words[0]} {words[3]} {words[1]}";
                            DebugOutput($"{spacer}processing first msg: {dmsg1.Message}");
                            ProcessDecodeMsg(dmsg1, true);

                            EnqueueDecodeMessage dmsg2 = dmsg;
                            dmsg2.Message = $"{words[2]} {words[3]} {words[4]}";
                            DebugOutput($"{spacer}processing second msg: {dmsg2.Message}");
                            ProcessDecodeMsg(dmsg2, true);
                        }
                        return;
                    }
                }


                //*************
                //StatusMessage
                //*************
                if (msg.GetType().Name == "StatusMessage")
                {
                    StatusMessage smsg = (StatusMessage)msg;
                    DateTime dtNow = DateTime.UtcNow;
                    bool modeChanged = false;
                    if (opMode < OpModes.ACTIVE) DebugOutput($"{Time()}\n{msg}");
                    qsoStateConf = smsg.CurQsoState();
                    txEnabledConf = smsg.TxEnabled;
                    dxCall = smsg.DxCall;                               //unreliable info, can be edited manually
                    if (dxCall == "") dxCall = null;
                    mode = smsg.Mode;
                    specOp = (int)smsg.SpecialOperationMode;
                    txMsg = WsjtxMessage.RemoveAngleBrackets(smsg.LastTxMsg);        //msg from last Tx
                    txFirst = smsg.TxFirst;
                    decoding = smsg.Decoding;
                    transmitting = smsg.Transmitting;
                    dialFrequency = smsg.DialFrequency;
                    txOffset = smsg.TxDF;
                    wsjtxTxEnableButton = smsg.TxEnableButton;

                    //*****************************
                    //check for dbl-click on a call
                    //*****************************
                    if (opMode == OpModes.ACTIVE && smsg.DblClk && smsg.Check != null)
                    {
                        dblClk = true;           //event, not state
                        DebugOutput($"\n{Time()} WSJT-X event, dblClk:{dblClk}");
                        var items = smsg.Check.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (items.Count() >= 4)
                        {
                            ProcessDblClick(items[0], items[2] == "1", items[3] == "1");
                        }
                        dblClk = false;
                    }

                    if (lastXmitting == null) lastXmitting = transmitting;     //initialize
                    if (lastQsoState == WsjtxMessage.QsoStates.INVALID) lastQsoState = qsoStateConf;    //initialize WSJT-X user QSO state change detection
                    if (lastDecoding == null) lastDecoding = decoding;     //initialize
                    if (lastTxWatchdog == null) lastTxWatchdog = smsg.TxWatchdog;   //initialize
                    if (lastTxFirst == null) lastTxFirst = txFirst;                     //initialize
                    if (lastDialFrequency == null) lastDialFrequency = smsg.DialFrequency; //initialize
                    if (smsg.TRPeriod != null) trPeriod = (int)smsg.TRPeriod;

                    if (cmdCheckTimer.Enabled && smsg.Check == cmdCheck)             //found the random cmd check string, cmd receive ack'd
                    {
                        cmdCheckTimer.Stop();
                        commConfirmed = true;
                        DebugOutput($"\n{Time()} WSJT-X event, Check cmd rec'd, match");
                    }

                    //***********************
                    //check myCall and myGrid
                    //***********************
                    if (myCall == null || myGrid == null)
                    {
                        CheckMyCall(smsg);
                    }
                    else
                    {
                        if (myCall != smsg.DeCall || myGrid != smsg.DeGrid)
                        {
                            DebugOutput($"\n{Time()} WSJT-X event, Call or grid changed, myCall:{smsg.DeCall} (was {myCall}) myGrid:{smsg.DeGrid} (was {myGrid})");
                            myCall = smsg.DeCall;
                            myGrid = smsg.DeGrid;

                            ResetOpMode();
                            txTimeout = true;       //cancel current calling
                            SetCallInProg(null);    //not calling anyone
                            if (!paused) CheckNextXmit();
                        }
                    }

                    //*********************************
                    //detect WSJT-X xmit start/end ASAP
                    //*********************************
                    if (trPeriod != null && transmitting != lastXmitting)
                    {
                        if (transmitting)
                        {
                            StartProcessDecodeTimer();
                            ProcessTxStart();
                            if (firstDecodeTime == DateTime.MinValue) firstDecodeTime = DateTime.UtcNow;       //start counting until WSJT-X watchdog timer set
                        }
                        else                //end of transmit
                        {
                            ProcessTxEnd();
                        }
                        lastXmitting = transmitting;
                        ShowStatus();
                    }

                    //*******************************
                    //check for WSJT-X dxCall changed
                    //*******************************
                    if (dxCall != lastDxCall)       //occurs after dbl-click reported
                    {
                        DebugOutput($"\n{Time()} WSJT-X event, dxCall changed, dxCall:{dxCall} (was {lastDxCall})");
                        lastDxCall = dxCall;
                    }

                    //****************************
                    //detect WSJT-X Tx mode change
                    //****************************
                    if (mode != lastMode)
                    {
                        DebugOutput($"\n{Time()} WSJT-X event, mode changed, mode:{mode} (was {lastMode})");
                        UpdateRR73();

                        if (opMode > OpModes.IDLE) ClearAudioOffsets();

                        if (opMode == OpModes.ACTIVE)
                        {
                            ctrl.holdCheckBox.Checked = false;
                            DisableAutoFreqPause();
                            ResetOpMode();
                            txTimeout = true;       //cancel current calling
                            SetCallInProg(null);      //not calling anyone
                            if (!paused) Pause(true);
                            ctrl.ShowMsg("Mode changed", false);
                            modeChanged = true;
                        }
                        CheckModeSupported();
                        lastMode = mode;
                    }

                    //*******************************************
                    //detect WSJT-X special operating mode change
                    //*******************************************
                    if (specOp != lastSpecOp)
                    {
                        DebugOutput($"\n{Time()} WSJT-X event, Special operating mode changed, specOp:{specOp} (was {lastSpecOp})");

                        if (opMode > OpModes.IDLE) ClearAudioOffsets();

                        if (opMode == OpModes.ACTIVE)
                        {
                            ctrl.holdCheckBox.Checked = false;
                            DisableAutoFreqPause();
                            ResetOpMode();
                            ClearAudioOffsets();
                            txTimeout = true;       //cancel current calling
                            SetCallInProg(null);      //not calling anyone
                            if (!paused) Pause(true);
                        }
                        CheckModeSupported();
                        lastSpecOp = specOp;
                    }

                    //***************************************
                    //check for transition from IDLE to START
                    //***************************************
                    if (commConfirmed && supportedModes.Contains(mode) && specOp == 0 && opMode == OpModes.IDLE)
                    {
                        if (!(transmitting && txMsg == "TUNE"))         //don't interrupt tuning
                        {
                            DisableTx(false);
                            HaltTx();                //****this syncs txEnable state with WSJT-X****
                        }
                        EnableMonitoring();                 //must do only after DisableTx and HaltTx
                        EnableDebugLog();

                        opMode = OpModes.START;
                        ShowStatus();
                        UpdateModeVisible();
                        DebugOutput($"{Time()} opMode:{opMode}");
                    }

                    //*************************
                    //detect decoding start/end
                    //*************************
                    if (smsg.Decoding != lastDecoding)
                    {
                        if (smsg.Decoding)
                        {
                            postDecodeTimer.Stop();
                            postDecodeTimer.Start();                    //restart timer at every decode, will time out after last decode
                            string newLn = firstDecodePass ? "\n" : "";
                            DebugOutput($"{newLn}{Time()} WSJT-X event, firstDecodePass:{firstDecodePass}, postDecodeTimer.Enabled:{postDecodeTimer.Enabled} processDecodeTimer.Enabled:{processDecodeTimer.Enabled}");
                            if (firstDecodePass)
                            {
                                SetPeriodState();
                                if (!processDecodeTimer.Enabled)           //was not started at end of last xmit, use first decode instead
                                {
                                    int msec = (dtNow.Second * 1000) + dtNow.Millisecond;
                                    int diffMsec = msec % (int)trPeriod;
                                    int cycleTimerAdj = CalcTimerAdj();
                                    int interval = Math.Max(((int)trPeriod) - diffMsec - cycleTimerAdj, 1);
                                    DebugOutput($"{spacer}msec:{msec} diffMsec:{diffMsec} interval:{interval} cycleTimerAdj:{cycleTimerAdj}");
                                    if (interval > 0)
                                    {
                                        processDecodeTimer.Interval = interval;
                                        processDecodeTimer.Start();
                                        DebugOutput($"{spacer}processDecodeTimer start");
                                    }
                                }
                                CheckTimedStartStop();            //may pause or start operation
                            }
                        }
                        else
                        {
                            DebugOutput($"{Time()} WSJT-X event, Decode end");
                            if (firstDecodePass)
                            {
                                //first calculation of best offset
                                if (CalcBestOffset(audioOffsets, period, false))       //calc for period when decodes started
                                {
                                    ctrl.freqCheckBox.Text = "Select best Tx frequency";
                                    ctrl.freqCheckBox.ForeColor = Color.Black;
                                }

                                firstDecodePass = false;
                                DebugOutput($"{spacer}firstDecodePass:{firstDecodePass}");
                            }
                        }
                        lastDecoding = smsg.Decoding;
                    }

                    //*************************************
                    //check for changed QSO state in WSJT-X
                    //*************************************
                    if (lastQsoState != qsoStateConf)
                    {
                        qsoState = qsoStateConf;            //qsoState confirmed
                        DebugOutput($"\n{Time()} WSJT-X event, qsoState changed, qsoState:{qsoState} (was {lastQsoState})");
                        lastQsoState = qsoState;
                        DebugOutputStatus();
                    }
                    
                    //**********************
                    //WSJT-X Tx halt clicked
                    //**********************
                    if (smsg.TxHaltClk)
                    {
                        if (opMode >= OpModes.START)
                        {
                            DebugOutput($"\n{Time()} WSJT-X event, TxHaltClk, paused:{paused} txMode:{txMode} processDecodeTimer.Enabled:{processDecodeTimer.Enabled}");
                            Pause(false);       //WSJT-X already halted Tx
                            txEnabled = false;
                        }
                    }
                    //***********************************************
                    //check for WSJT-X Tx enable button state changed
                    //***********************************************
                    if (smsg.TxEnableClk)           //WSJT-X "Tx Enable" button clicked, and button state updated by WSJT-X
                    {
                        //marker1
                        if (opMode >= OpModes.START)
                        {
                            DebugOutput($"\n{Time()} WSJT-X event, wsjtxTxEnableButton:{wsjtxTxEnableButton}, txEnabled:{txEnabled} paused:{paused} txMode:{txMode} processDecodeTimer.Enabled:{processDecodeTimer.Enabled}");
                            if (!wsjtxTxEnableButton)    //tx button unchecked
                            {
                                Pause(false);
                            }
                            else if (wsjtxTxEnableButton)   //tx button checked
                            {
                                if (opMode == OpModes.START)
                                {
                                    DebugOutput($"{Time()} Halt Tx, during opMode:START");
                                    ctrl.ShowMsg("Not ready yet... please wait", true);
                                    HaltTx();
                                }

                                if (opMode == OpModes.ACTIVE) TxModeEnabled();
                            }
                        }
                    }

                    //***********************************
                    //check for changed WSJT-X Tx enabled
                    //***********************************
                    if (txEnabledConf != lastTxEnabled)
                    {
                        DebugOutput($"\n{Time()} WSJT-X event, Tx enable change confirmed, txEnabled:{txEnabled} (was {lastTxEnabled}) paused:{paused} txMode:{txMode}");
                        lastTxEnabled = txEnabledConf;
                    }

                    //**********************************************
                    //check for WSJT-X watchdog timer status changed
                    //**********************************************
                    if (smsg.TxWatchdog != smsg.TxWatchdog)
                    {
                        DebugOutput($"\n{Time()} WSJT-X event, smsg.TxWatchdog:{smsg.TxWatchdog} (was {lastTxWatchdog})");
                        if (opMode == OpModes.ACTIVE)
                        {
                            ctrl.holdCheckBox.Checked = false;
                        }
                        if (smsg.TxWatchdog && opMode == OpModes.ACTIVE)        //only need this event if in valid mode
                        {
                            if (firstDecodeTime != DateTime.MinValue)
                            {
                                if ((DateTime.UtcNow - firstDecodeTime).TotalMinutes < 15)
                                {
                                    ModelessDialog("Set the 'Tx watchdog' in WSJT-X to 15 minutes or longer.\n\nThis will be the timeout in case the Controller sends the same message repeatedly (for example, calling CQ when the band is closed).\n\nThe WSJT-X 'Tx watchdog' setting is under File | Settings, in the 'General' tab.");
                                }
                                else
                                {
                                    ModelessDialog("The 'Tx watchdog' in WSJT-X has timed out.\n\n(The WSJT-X 'Tx watchdog' setting is under File | Settings, in the 'General' tab).\n\nSelect an 'Operatng Mode' to continue.");
                                }
                                firstDecodeTime = DateTime.MinValue;        //allow timing to restart
                            }
                        }
                        lastTxWatchdog = smsg.TxWatchdog;
                    }

                    //**********************************
                    //check for WSJT-X frequency changed
                    //**********************************
                    if (lastDialFrequency != null && (Math.Abs((float)lastDialFrequency - (float)dialFrequency) > freqChangeThreshold))
                    {
                        DebugOutput($"\n{Time()} WSJT-X event, Freq changed:{dialFrequency / 1e6} (was:{lastDialFrequency / 1e6}) opMode:{opMode}");

                        if (FreqToBand(dialFrequency / 1e6) == FreqToBand(lastDialFrequency / 1e6))      //same band
                        {
                            if (opMode == OpModes.ACTIVE)
                            {
                                ClearAudioOffsets();
                                if (ctrl.freqCheckBox.Checked) AutoFreqChanged(true, false);
                                Pause(true);
                                if (!modeChanged) ctrl.ShowMsg("Frequency changed", false);
                            }
                        }
                        else        //new band
                        {
                            if (opMode > OpModes.IDLE) ClearAudioOffsets();

                            if (opMode == OpModes.ACTIVE)
                            {
                                DebugOutput($"{spacer}band changed:{FreqToBand(dialFrequency / 1e6)} (was:{FreqToBand(lastDialFrequency / 1e6)})");
                                ResetOpMode();
                                ClearCalls(true);
                                logList.Clear();        //can re-log on new mode/band or in new session
                                ShowLogged();
                                txTimeout = true;       //cancel current calling
                                SetCallInProg(null);      //not calling anyone
                                if (!paused) Pause(true); //WSJT-X may have already halted Tx, or done above
                                if (!modeChanged) ctrl.ShowMsg("Band changed", false);
                                DebugOutput($"{spacer}cleared queued calls:DialFrequency, txTimeout:{txTimeout} callInProg:'{CallPriorityString(callInProg)}'");
                            }
                        }
                        lastDialFrequency = smsg.DialFrequency;
                    }

                    //*****************************
                    //detect WSJT-X Tx First change
                    //*****************************
                    if (txFirst != lastTxFirst)
                    {
                        DebugOutput($"\n{Time()} WSJT-X event, Tx first changed, txFirst:{txFirst} txMode:{txMode}");
                        settingChanged = true;
                        DisableAutoFreqPause();

                        if (opMode == OpModes.ACTIVE)
                        {
                            //ctrl.holdCheckBox.Checked = false;   don't do this, will reset hold enabled by new countrey

                            if (txMode == TxModes.CALL_CQ)              //won't be selected in WSHT-X manually when in listen mode (WSJT-X "Tx even/1st" control disabed)
                            {
                                CheckCallQueuePeriod(txFirst);        //remove queued calls with opposite time period
                                if (!paused && replyDecode != null)   //check current call time period
                                {
                                    bool evenCall = IsEvenPeriod((replyDecode.SinceMidnight.Minutes * 60) + replyDecode.SinceMidnight.Seconds);
                                    DebugOutput($"{spacer}txFirst:{txFirst} evenCall:{evenCall}");
                                    if (evenCall == txFirst)            //current call is incorrect time period
                                    {
                                        txTimeout = true;               //get next call in queue, or start CQing
                                        DebugOutput($"{spacer}current call {replyDecode.ToCall()} incorrect period");
                                        CheckNextXmit();
                                    }
                                }
                            }
                        }
                        lastTxFirst = txFirst;
                        DebugOutput($"{spacer}lastTxFirst:{lastTxFirst}");
                    }

                    CheckActive();

                    //*****end of status *****
                    UpdateDebug();
                    return;
                }
            }
        }

        private void ProcessDecodeMsg(EnqueueDecodeMessage dmsg, bool isSpecOp)
        {
            string deCall = dmsg.DeCall();
            if (deCall == null || dmsg.ToCall() == null)            //bad decode
            {
                if (!dmsg.Message.Contains("...")) DebugOutput($"{Time()} invalid decode:'{dmsg.Message}'");
                return;
            }

            latestDecodeTime = dmsg.SinceMidnight;
            latestDecodeDate = dmsg.RxDate;
            bool recdPrevSignoff = false;
            rawMode = dmsg.Mode;    //different from mode string in status msg
            bool toMyCall = dmsg.IsCallTo(myCall);

            if (dmsg.DeltaFrequency > offsetLoLimit && dmsg.DeltaFrequency < offsetHiLimit) audioOffsets.Add(dmsg.DeltaFrequency);

            if (toMyCall && dmsg.AutoGen)
            {

                DebugOutput($"{Time()}");
                DebugOutput($"{dmsg}\n{spacer}msg:'{dmsg.Message}'");
                DebugOutput($"{spacer}deCall:'{deCall}' callInProg:'{CallPriorityString(callInProg)}' txEnabled:{txEnabled} transmitting:{transmitting} restartQueue:{restartQueue} RecdAnyMsg:{RecdAnyMsg(deCall)}");

                if (dmsg.IsContest())
                {
                    if (!paused && deCall != null && (callInProg == null || deCall == callInProg))
                    {
                        restartQueue = true;
                        DebugOutput($"{spacer}contest reply: restartQueue:{restartQueue}");
                    }
                    UpdateDebug();
                    return;
                }

                int callTimeouts = 0;
                timeoutCallDict.TryGetValue(deCall, out callTimeouts);
                DebugOutput($"{spacer}callTimeouts:{callTimeouts} maxTimeoutCalls:{maxTimeoutCalls}");
                if (!dmsg.Is73orRR73() && callTimeouts >= maxTimeoutCalls)
                {
                    ctrl.ShowMsg($"Blocking {deCall} temporarily...", false);
                    DebugOutput($"{spacer}ignoring call, callTimeouts:{callTimeouts} restartQueue:{restartQueue}");
                    UpdateDebug();
                    return;
                }

                //do some processing not directly related to replying immediately
                dmsg.Priority = (int)CallPriority.TO_MYCALL;       //as opposed to a decode from anyone else
                if (advanced)
                {
                    if (dmsg.IsNewCountryOnBand) dmsg.Priority = (int)CallPriority.NEW_COUNTRY_ON_BAND;
                    if (dmsg.IsNewCountry) dmsg.Priority = (int)CallPriority.NEW_COUNTRY;
                }

                CheckLateLog(deCall, dmsg);
                UpdateDebug();

                //detect previous signoff before adding call to allCallDict
                recdPrevSignoff = RecdSignoff(deCall);
                DebugOutput($"{spacer}recdPrevSignoff:{recdPrevSignoff}");

                //if call not logged this band/session: save Report (...+03) and RogerReport (...R-02) decodes for out-of-order call processing
                if (!logList.Contains(deCall))
                {
                    AddAllCallDict(deCall, dmsg);
                }

                DebugOutput($"{spacer}deCall:{deCall} dmsg.Priority:{dmsg.Priority} callQueue.Contains:{callQueue.Contains(deCall)} SentAnyMsg:{SentAnyMsg(deCall)}");
                if (dmsg.Priority > (int)CallPriority.NEW_COUNTRY_ON_BAND
                    && txMode == TxModes.LISTEN
                    && ctrl.replyNewDxccCheckBox.Checked
                    && ctrl.replyNewOnlyCheckBox.Checked
                    && !callQueue.Contains(deCall)
                    && !SentAnyMsg(deCall)
                    )
                {
                    ctrl.ShowMsg($"{deCall} ignored (low priority)", false);
                    DebugOutput($"{spacer}{deCall} ignored, calls from new country[on band] only");
                    return;
                }
                //if calling CQ DX and ignore non-DX replies
                if (!dmsg.IsDx
                    && dmsg.Priority > (int)CallPriority.NEW_COUNTRY_ON_BAND
                    && txMode == TxModes.CALL_CQ
                    && ctrl.callCqDxCheckBox.Checked
                    && ctrl.ignoreNonDxCheckBox.Checked
                    && !callQueue.Contains(deCall)
                    && !SentAnyMsg(deCall)
                    && !dmsg.Is73orRR73()           //tempOnly 2/18/24
                    )
                {
                    ctrl.ShowMsg($"{deCall} ignored (not DX)", false);
                    DebugOutput($"{spacer}{deCall} ignored, DX only");
                    return;
                }

                if (deCall == callInProg)
                {
                    consecTxCount = 0;          //reset Tx hold count since we're being heard
                }

                if (ctrl.mycallCheckBox.Checked) Play("trumpet.wav");   //not the call just logged


                if (!txEnabled && deCall != null && !recdPrevSignoff && !dmsg.Is73orRR73())
                {
                    if (!callQueue.Contains(deCall))
                    {
                        DebugOutput($"{spacer}'{deCall}' not in queue");
                        AddCall(deCall, dmsg);
                        if (ctrl.callAddedCheckBox.Checked) Play("blip.wav");

                        //check for call after decodes "done"
                        if (!processDecodeTimer.Enabled)
                        {
                            DebugOutput($"{spacer}late decode");
                            if (dmsg.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND)
                            {
                                DebugOutput($"{spacer}process high-priority late decode, restartQueue:{restartQueue}");
                                StartProcessDecodeTimer2();
                            }
                        }

                        if (callInProg == null && txMode == TxModes.LISTEN && !paused && callQueue.Count >= 1)       //txEnabled = false
                        {
                            restartQueue = true;
                            DebugOutput($"{spacer}restartQueue:{restartQueue}");
                        }
                    }
                    else
                    {
                        DebugOutput($"{spacer}'{deCall}' already in queue");
                        UpdateCall(deCall, dmsg);
                    }
                    UpdateDebug();
                }

                //decode processing of calls to myCall requires txEnabled
                if (txEnabled && deCall != null)
                {
                    DebugOutput($"{spacer}'{deCall}' is to {myCall}");
                    if ((deCall == callInProg || (txTimeout && deCall == tCall)) && recdPrevSignoff)        //cancel call in progress
                    {
                        restartQueue = true;
                        DebugOutput($"{spacer}already rec'd signoff, restartQueue:{restartQueue} qsoState:{qsoState}");
                    }
                    else
                    {
                        if (!dmsg.Is73orRR73())       //not a 73 or RR73
                        {
                            DebugOutput($"{spacer}not a 73 or RR73");
                            if (deCall != callInProg)
                            {
                                DebugOutput($"{spacer}{deCall} is not callInProg:{CallPriorityString(callInProg)}");
                                if (!callQueue.Contains(deCall))        //call not in queue, enqueue the call data
                                {
                                    DebugOutput($"{spacer}'{deCall}' not already in queue");
                                    AddCall(deCall, dmsg);
                                    if (ctrl.callAddedCheckBox.Checked) Play("blip.wav");

                                    //check for high-priority call after decodes "done"
                                    DebugOutput($"{spacer}transmitting:{transmitting} processDecodeTimer.Enabled:{processDecodeTimer.Enabled} qsoState:{qsoState}");
                                    if (!processDecodeTimer.Enabled)
                                    {
                                        DebugOutput($"{spacer}late decode");
                                        if (dmsg.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND)
                                        {
                                            DebugOutput($"{spacer}process high-priority late decode, restartQueue:{restartQueue}");
                                            StartProcessDecodeTimer2();
                                        }
                                    }

                                }
                                else       //call is already in queue, update the call data
                                {
                                    DebugOutput($"{spacer}'{deCall}' already in queue");
                                    UpdateCall(deCall, dmsg);
                                }

                                if (replyDecode != null)
                                {
                                    string replyDecodeDeCall = replyDecode.DeCall();
                                    bool noMsgsDeCall = !RecdAnyMsg(replyDecodeDeCall);
                                    DebugOutput($"{spacer}replyDecode.Message: {replyDecode.Message} replyDecode.Priority:{replyDecode.Priority} noMsgsDeCall:{noMsgsDeCall}");
                                    if (dmsg.Priority < replyDecode.Priority && noMsgsDeCall)   //currently replying to a CQ or other selected call, but not replying to a call to myCall and no msgs rec'd from this call yet
                                    {
                                        cancelledCall = replyDecodeDeCall;
                                        restartQueue = true;
                                        DebugOutput($"{spacer}replyDecode is not a call to {myCall} and and no msgs rec'd from {replyDecodeDeCall}, restartQueue:{restartQueue} cancelledCall:'{cancelledCall}'");
                                    }
                                }
                            }
                            else        //call is in progress
                            {
                                DebugOutput($"{spacer}{CallPriorityString(deCall)} is callInProg, txTimeout:{txTimeout} cancelledCall:{cancelledCall}");
                                if (deCall == cancelledCall)
                                {
                                    AddCall(deCall, dmsg);
                                    DebugOutput($"{spacer}re-added cancelledCall '{cancelledCall}' to queue");

                                    //check for call after decodes "done"
                                    if (!processDecodeTimer.Enabled)
                                    {
                                        DebugOutput($"{spacer}late decode");
                                        if (dmsg.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND)
                                        {
                                            DebugOutput($"{spacer}process high-priority late decode, restartQueue:{restartQueue}");
                                            StartProcessDecodeTimer2();
                                        }
                                    }
                                }
                                else
                                {
                                    if (isSpecOp)
                                    {
                                        //send Reply message now, WSJT-X does not respond sutomatically to F/H style (multi-target) msg
                                        DebugOutput($"{spacer}special operation");
                                        var rmsg = new ReplyMessage();
                                        rmsg.SchemaVersion = WsjtxMessage.NegotiatedSchemaVersion;
                                        rmsg.Id = WsjtxMessage.UniqueId;
                                        rmsg.SinceMidnight = dmsg.SinceMidnight;
                                        rmsg.Snr = dmsg.Snr;
                                        rmsg.DeltaTime = dmsg.DeltaTime;
                                        rmsg.DeltaFrequency = dmsg.DeltaFrequency;
                                        rmsg.Mode = dmsg.Mode;
                                        rmsg.Message = dmsg.Message;
                                        rmsg.UseStdReply = dmsg.UseStdReply;
                                        ba = rmsg.GetBytes();
                                        udpClient2.Send(ba, ba.Length);
                                        replyCmd = dmsg.Message;            //save the last reply cmd to determine which call is in progress
                                        replyDecode = dmsg;                 //save the decode the reply cmd derived from
                                        curCmd = dmsg.Message;
                                        DebugOutput($"{Time()} >>>>>Sent 'Reply To Msg' cmd:\n{rmsg} lastTxMsg:'{lastTxMsg}'\n{spacer}replyCmd:'{replyCmd}'");
                                    }
                                }
                            }
                        }
                        else        //decode is 73 or RR73 msg
                        {
                            DebugOutput($"{spacer}decode is 73 or RR73");
                            if (deCall == callInProg)
                            {
                                restartQueue = true;
                                DebugOutput($"{spacer}call is in progress, restartQueue:{restartQueue}");
                                if (!processDecodeTimer.Enabled || transmitting)
                                {
                                    //prevent calling CQ when not wanted
                                    DebugOutput($"{spacer}process late decode, restartQueue:{restartQueue}");
                                    StartProcessDecodeTimer2();
                                }
                            }
                            else
                            {
                                RemoveCall(deCall);     //might have been added manually
                            }
                        }
                    }
                    UpdateDebug();
                }
            }
            else    //not toMyCall or is not auto-generated
            {
                //only resulting action is to add call to callQueue, optionally restart queue
                AddSelectedCall(dmsg);              //known to be "new" and not "replay
                UpdateDebug();
            }

            return;
        }

        private bool CheckActive()
        {
            //*****************************************
            //check for transition from START to ACTIVE
            //*****************************************
            if (commConfirmed && myCall != null && supportedModes.Contains(mode) && specOp == 0 && opMode == OpModes.START && (!ctrl.freqCheckBox.Checked || (oddOffset > 0 && evenOffset > 0)))
            {
                opMode = OpModes.ACTIVE;
                SetListenMode();
                if (txMode == TxModes.LISTEN)
                {
                    Pause(true);
                }

                dialogTimer.Start();            //for CheckFirstRun
                UpdateModeVisible();
                UpdateTxTimeEnable();
                DebugOutput($"{spacer}CheckActive, opMode:{opMode}");
                ShowStatus();
                UpdateAddCall();
                UpdateDebug();
                return true;
            }
            return false;
        }

        private void StartProcessDecodeTimer()
        {
            DateTime dtNow = DateTime.UtcNow;
            int diffMsec = ((dtNow.Second * 1000) + dtNow.Millisecond) % (int)trPeriod;
            int cycleTimerAdj = CalcTimerAdj();
            processDecodeTimer.Interval = (2 * (int)trPeriod) - diffMsec - cycleTimerAdj;
            processDecodeTimer.Start();
            DebugOutput($"{Time()} processDecodeTimer start: interval:{processDecodeTimer.Interval} msec");
        }

        private bool CheckMyCall(StatusMessage smsg)
        {
            if (smsg.DeCall == null || smsg.DeGrid == null || smsg.DeGrid.Length < 4)
            {
                heartbeatRecdTimer.Stop();
                suspendComm = true;
                ctrl.BringToFront();
                MessageBox.Show($"Call sign and Grid are not entered in WSJT-X.\n\nEnter these in WSJT-X:\n- Select 'File | Settings' then the 'General' tab.\n\n(Grid must be at least 4 characters)\n\n{pgmName} will try again when you close this dialog.", pgmName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ResetOpMode();
                suspendComm = false;
                return false;
            }

            if (myCall == null)
            {
                myCall = smsg.DeCall;
                myGrid = smsg.DeGrid;
                DebugOutput($"{spacer}CheckMyCall myCall:{myCall} myGrid:{myGrid}");
            }

            UpdateDebug();
            return true;
        }

        private void CheckNextXmit()
        {
            //can be called anytime, but will be called at least once per decode period shortly before the tx period begins;
            //can result in tx enabled (or disabled)
            DebugOutput($"{Time()} CheckNextXmit: txTimeout:{txTimeout} callQueue.Count:{callQueue.Count} qsoState:{qsoState}");
            DebugOutput($"{spacer}tx stop enabled:{ctrl.timedCheckBox.Checked} txStopDateTime(local):{txStopDateTime} now(local):{DateTime.Now}");
            DateTime dtNow = DateTime.UtcNow;      //helps with debugging to do this here

            //*******************
            //Best Tx freq update
            //*******************
            if (autoFreqPauseMode == autoFreqPauseModes.ENABLED)        //auto freq update started
            {
                DebugOutput($"{spacer}CheckNextXmit(4) start");
                autoFreqPauseMode = autoFreqPauseModes.ACTIVE;
                UpdateCallInProg();
                DebugOutput($"{spacer}auto freq update continue");
                DebugOutput($"{spacer}CheckNextXmit(4) end, autoFreqPauseMode:{autoFreqPauseMode}");
            }
            else if (autoFreqPauseMode == autoFreqPauseModes.ACTIVE)        //end auto freq update
            {
                DebugOutput($"{spacer}CheckNextXmit(5) start");
                DebugOutput($"{spacer}auto freq update end");
                DisableAutoFreqPause();
                EnableTx();
                DebugOutput($"{spacer}CheckNextXmit(5) end, autoFreqPauseMode:{autoFreqPauseMode}");
            }
            
            //*************
            //Timed tx stop
            //**************
            if (txTimeout && ctrl.timedCheckBox.Checked && (DateTime.Now >= txStopDateTime))          //local time
            {
                DebugOutput($"{spacer}CheckNextXmit(3) start");
                DebugOutput($"{spacer}tx stopped(2), paused:{paused}");
                Pause(false);
                UpdateModeSelection();
                ctrl.timedCheckBox.Checked = false;     //end timed operation
                SetCallInProg(null);
                restartQueue = false;           //get ready for next decode phase
                txTimeout = false;              //ready for next timeout
                tCall = null;
                DebugOutputStatus();
                DebugOutput($"{spacer}CheckNextXmit(3) end: restartQueue:{restartQueue} txTimeout:{txTimeout}");
                UpdateDebug();      //unconditional
                return;
            }

            //********************
            //Next call processing
            //********************
            //check for time to initiate next xmit from queued calls,
            //or resume CQing mode,
            //or disable tx for listen mode
            if (txTimeout || (callQueue.Count > 0 && callInProg == null))        //important to sync qso logged to end of xmit, and manually-added call(s) to status msgs
            {
                consecTxCount = 0;
                replyCmd = null;        //last reply cmd sent is no longer in effect
                replyDecode = null;
                SetCallInProg(null);    //not calling anyone (set this as late as possible to pick up possible reply to last Tx)
                DebugOutput($"{spacer}CheckNextXmit(1) start");
                DebugOutputStatus();

                //process the next call in the queue, if any present and correct time period
                bool timePeriodOk = true;
                string deCall = "";
                bool toMyCall = false;
                EnqueueDecodeMessage dmsg = new EnqueueDecodeMessage();

                DebugOutput($"{spacer}callQueue.Count:{callQueue.Count} ctrl.freqCheckBox.Checked:{ctrl.freqCheckBox.Checked}");
                DebugOutput($"{spacer}txMode:{txMode} autoFreqPauseMode:{autoFreqPauseMode} toMyCall:{toMyCall}");
                if (callQueue.Count > 0)            //have queued call signs 
                {
                    deCall = PeekNextCall(out dmsg);
                    toMyCall = dmsg.IsCallTo(myCall);
                    bool evenCall = IsEvenPeriod((dmsg.SinceMidnight.Minutes * 60) + dmsg.SinceMidnight.Seconds);
                    bool evenPeriod = txFirst;          //CQ mode can only xmit on txFirst setting
                    //"dtNow" will be shortly before the tx period following the decode period, at least once per cycle;
                    //add 2 seconds to dtNow to assure that decision to reply is based on the tx period's time
                    if (txMode == TxModes.LISTEN) evenPeriod = IsEvenPeriod((dtNow.Minute * 60) + dtNow.Second + 2);       //listen mode can xmit on either period depending on current time
                    if (!dmsg.UseStdReply) timePeriodOk = evenCall != evenPeriod;      //reply is in opposite time period from msg
                    DebugOutput($"{spacer}peek in queue, got '{deCall}', dtNow:{dtNow.ToString("HHmmss.fff")} evenCall:{evenCall} txFirst:{txFirst} timePeriodOk:{timePeriodOk} UseStdReply:{dmsg.UseStdReply}");

                    if (timePeriodOk && (txMode == TxModes.LISTEN || autoFreqPauseMode == autoFreqPauseModes.DISABLED || (autoFreqPauseMode > autoFreqPauseModes.DISABLED && toMyCall)))
                    {
                        string nCall = GetNextCall(out dmsg);
                        DebugOutput($"{spacer}have entries in queue, got '{nCall}'");

                        if (WsjtxMessage.IsCQ(dmsg.Message))                  //save the grid for logging
                        {
                            AddAllCallDict(nCall, dmsg);
                        }

                        //set call options
                        emsg.NewTxMsgIdx = 10;
                        emsg.GenMsg = $"";          //no effect
                        emsg.SkipGrid = (dmsg.UseStdReply ? false : ctrl.skipGridCheckBox.Checked);
                        emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
                        emsg.CmdCheck = "";         //ignored
                        emsg.Offset = AudioOffsetFromMsg(dmsg);
                        ba = emsg.GetBytes();
                        udpClient2.Send(ba, ba.Length);
                        DebugOutput($"{Time()} >>>>>Sent 'Opt Req' cmd:10\n{emsg}");
                        if (settingChanged)
                        {
                            ctrl.WsjtxSettingConfirmed();
                            settingChanged = false;
                        }

                        //send Reply message
                        var rmsg = new ReplyMessage();
                        rmsg.SchemaVersion = WsjtxMessage.NegotiatedSchemaVersion;
                        rmsg.Id = WsjtxMessage.UniqueId;
                        rmsg.SinceMidnight = dmsg.SinceMidnight;
                        rmsg.Snr = dmsg.Snr;
                        rmsg.DeltaTime = dmsg.DeltaTime;
                        rmsg.DeltaFrequency = dmsg.DeltaFrequency;
                        rmsg.Mode = dmsg.Mode;
                        rmsg.Message = dmsg.Message.Replace("RR73", "").Replace(" 73", "").Replace("73 ", "").Replace(" 73 ", "");      //remove these because sending 73 as a reply terminates msg sequence (note: "73" might be part of a call sign)
                        rmsg.UseStdReply = dmsg.UseStdReply;
                        ba = rmsg.GetBytes();
                        udpClient2.Send(ba, ba.Length);
                        replyCmd = dmsg.Message;            //save the last reply cmd to determine which call is in progress
                        replyDecode = dmsg;                 //save the decode the reply cmd derived from
                        curCmd = dmsg.Message;
                        SetCallInProg(nCall);
                        DebugOutput($"{Time()} >>>>>Sent 'Reply To Msg' cmd:\n{rmsg} lastTxMsg:'{lastTxMsg}'\n{spacer}replyCmd:'{replyCmd}'");
                        //ctrl.ShowMsg($"Replying to {nCall}...", false);

                        EnableTx();             //also sets WSJT-X "Tx Enable" button state

                        if (dmsg.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND)
                        {
                            ctrl.holdCheckBox.Checked = true;
                            ctrl.ShowMsg($"Hold enabled for new country", true);
                        }

                        //                  auto freq update interrupted
                        if (txMode == TxModes.LISTEN || autoFreqPauseMode > autoFreqPauseModes.DISABLED)
                        {
                            DebugOutput($"{spacer}disable auto freq update");
                            DisableAutoFreqPause();     //end auto freq update
                        }
                        restartQueue = false;           //get ready for next decode phase
                        txTimeout = false;              //ready for next timeout
                        tCall = null;
                        xmitCycleCount = 0;
                    }
                    else
                    {
                        if (txMode == TxModes.LISTEN)
                        {
                            ctrl.ShowMsg($"Waiting to process {deCall}...", false);
                            DisableTx(true);        //also sets WSJT-X "Enable Tx" button state
                        }
                    }
                }
                else            //no queued call signs, start CQing (or if Listening: prepare for replying) 
                {
                    if (txMode == TxModes.LISTEN)
                    {
                        DisableTx(true);            //also sets WSJT-X "Enable Tx" button state
                    }
                    else        //CQ mode
                    {
                        DebugOutput($"{spacer}no entries in queue, start CQing");
                        SetupCq(true);      //also sets WSJT-X "Tx Enable" button state
                    }
                    restartQueue = false;           //get ready for next decode phase
                    txTimeout = false;              //ready for next timeout
                    tCall = null;
                    xmitCycleCount = 0;
                }
                DebugOutputStatus();
                DebugOutput($"{spacer}CheckNextXmit(1) end: restartQueue:{restartQueue} txTimeout:{txTimeout}");
                UpdateDebug();      //unconditional
                return;             //don't process newDirCq
            }

            //*************************************
            //Directed CQ / new setting / best freq
            //*************************************
            if (txMode == TxModes.CALL_CQ && qsoState == WsjtxMessage.QsoStates.CALLING)      //tempOnly 2/13/24 added "callInProg == null"
            {
                if (callInProg == null)
                {

                    DebugOutput($"{spacer}CheckNextXmit(2) start");
                    if (ctrl.freqCheckBox.Checked && oddOffset > 0 && evenOffset > 0)
                    {
                        //set/show frequency offset for period after decodes started
                        emsg.NewTxMsgIdx = 10;
                        emsg.GenMsg = $"";          //no effect
                        emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
                        emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
                        emsg.CmdCheck = "";         //ignored
                        emsg.Offset = AudioOffsetFromTxPeriod();
                        ba = emsg.GetBytes();
                        udpClient2.Send(ba, ba.Length);
                        DebugOutput($"{Time()} >>>>>Sent 'Opt Req' cmd:10\n{emsg}");
                        if (settingChanged)
                        {
                            ctrl.WsjtxSettingConfirmed();
                            settingChanged = false;
                        }
                    }

                    if (newDirCq)
                    {
                        emsg.NewTxMsgIdx = 6;
                        emsg.GenMsg = $"CQ{NextDirCq()} {myCall} {myGrid}";
                        emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
                        emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
                        emsg.CmdCheck = "";         //ignored
                        ba = emsg.GetBytes();           //set up for CQ, auto, call 1st
                        udpClient2.Send(ba, ba.Length);
                        DebugOutput($"{Time()} >>>>>Sent 'Setup CQ' cmd:6\n{emsg}");
                        qsoState = WsjtxMessage.QsoStates.CALLING;      //in case enqueueing call manually right now
                        replyCmd = null;        //invalidate last reply cmd since not replying
                        replyDecode = null;
                        curCmd = emsg.GenMsg;
                        newDirCq = false;
                        DebugOutput($"{spacer}newDirCq:{newDirCq}");
                        SetCallInProg(null);
                        if (settingChanged)
                        {
                            ctrl.WsjtxSettingConfirmed();
                            settingChanged = false;
                        }
                    }

                    UpdateWsjtxOptions();
                    DebugOutputStatus();
                    DebugOutput($"{spacer}CheckNextXmit(2) end");
                    UpdateDebug();      //unconditional
                    return;
                }
                else
                {
                    LogBeep();
                    DebugOutput($"{spacer}callInProg:{callInProg}");
                }
            }
        }

        private void ProcessDecodes()
        {
            //always called shortly before the tx period begins
            cancelledCall = null;
            DebugOutput($"{Time()} ProcessDecodes: restartQueue:{restartQueue} txTimeout:{txTimeout} txEnabled:{txEnabled}\n{spacer}txMode:{txMode} paused:{paused} txEnabled:{txEnabled} cancelledCall:{cancelledCall} autoFreqPauseMode:{autoFreqPauseMode}");
            DebugOutputStatus();
            if (debug)
            {
                DebugOutput(AllCallDictString());
                DebugOutput(ReportListString());
                DebugOutput(LogListString());
                DebugOutput(PotaLogDictString());
                DebugOutput(TimeoutCallDictString());
            }

            if (restartQueue)           //queue went from empty to having entries, during decode(s) phase: restart queue processing
            {
                txTimeout = true;       //important to only set this now, not during decode phase, since decodes can happen after Tx starts
                SetCallInProg(null);    //not calling anyone (set this as late as possible to pick up possible reply to last Tx)
                DebugOutput($"{spacer}qsoState:{qsoState} txTimeout:{txTimeout} callInProg:'{CallPriorityString(callInProg)}'");
                UpdateDebug();
            }

            //check for call in progress with tx disabled
            if (!paused && !txEnabled && callInProg != null && autoFreqPauseMode == autoFreqPauseModes.DISABLED)
            {
                DebugOutput($"{spacer}call in progress with tx disabled");
                //LogBeep();
                EnableTx();
            }

            //check for auto freq update disabled while CQ mode previously in progress
            if (!paused && !txEnabled && txMode == TxModes.CALL_CQ && autoFreqPauseMode == autoFreqPauseModes.DISABLED)
            {
                DebugOutput($"{spacer}auto freq update disabled while CQ mode previously in progress");
                //LogBeep();
                EnableTx();
            }

            //check for processing next call in queue, 
            //or resume tx in CQ mode, or disable tx for listen mode,
            //or process auto freq update enabled / in progress
            //or check for Tx started manually during Rx
            if (!paused && (txEnabled || txMode == TxModes.LISTEN || autoFreqPauseMode != autoFreqPauseModes.DISABLED))
            {
                DebugOutput($"{spacer}check resume/disable/manual/auto freq");
                CheckNextXmit();        //can result in tx enabled (or disabled)
            }
            else
            {
                UpdateWsjtxOptions();
            }
            DebugOutput($"{Time()} ProcessDecodes done");
        }

        //check for time to log (best done at Tx start to avoid any logging/dequeueing timing problem if done at Tx end)
        private void ProcessTxStart()
        {
            string toCall = WsjtxMessage.ToCall(txMsg);
            string lastToCall = WsjtxMessage.ToCall(lastTxMsg);
            toCallTxStart = toCall;
            DebugOutput($"\n{Time()} WSJT-X event, Tx start: toCall:'{toCall}' lastToCall:'{lastToCall}' processDecodeTimer interval:{processDecodeTimer.Interval} msec");

            if (toCall == null)
            {
                if (txMsg == "TUNE" && ctrl.offsetTune)
                {
                    prevOffset = txOffset;
                    DebugOutput($"{spacer}tuning start");
                    emsg.NewTxMsgIdx = 10;
                    emsg.GenMsg = $"";          //no effect
                    emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
                    emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
                    emsg.CmdCheck = "";         //ignored
                    emsg.Offset = tuningAudioOffset;
                    ba = emsg.GetBytes();
                    udpClient2.Send(ba, ba.Length);
                    DebugOutput($"{Time()} >>>>>Sent 'Opt Req' cmd:10\n{emsg}");
                    if (settingChanged)
                    {
                        ctrl.WsjtxSettingConfirmed();
                        settingChanged = false;
                    }
                }
                return;
            }

            txBeginTime = DateTime.UtcNow;

            DebugOutput($"{Time()} Tx start done: txMsg:'{txMsg}' lastTxMsg:'{lastTxMsg}' toCall:'{toCall}' lastToCall:'{lastToCall}'");
            UpdateDebug();      //unconditional
        }

        //check for QSO end or timeout (and possibly logging (if txMsg changed between Tx start and Tx end)
        private void ProcessTxEnd()
        {
            string toCall = WsjtxMessage.ToCall(txMsg);
            string lastToCall = WsjtxMessage.ToCall(lastTxMsg);
            string deCall = WsjtxMessage.DeCall(replyCmd);
            string cmdToCall = WsjtxMessage.ToCall(curCmd);
            DateTime txEndTime = DateTime.UtcNow;
            shortTx = false;

            DebugOutput($"\n{Time()} WSJT-X event, Tx end: toCall:'{toCall}' lastToCall:'{lastToCall}' deCall:'{deCall}' cmdToCall:'{cmdToCall}' maxTxRepeat:{maxTxRepeat}");

            if (toCall == null)
            {
                if (txMsg == "TUNE")
                {
                    if (ctrl.offsetTune)
                    {
                        DebugOutput($"{spacer}tuning end");
                        emsg.NewTxMsgIdx = 10;
                        emsg.GenMsg = $"";          //no effect
                        emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
                        emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
                        emsg.CmdCheck = "";         //ignored
                        emsg.Offset = CurAudioOffset();
                        ba = emsg.GetBytes();
                        udpClient2.Send(ba, ba.Length);
                        DebugOutput($"{Time()} >>>>>Sent 'Opt Req' cmd:10\n{emsg}");
                        if (settingChanged)
                        {
                            ctrl.WsjtxSettingConfirmed();
                            settingChanged = false;
                        }
                    }
                    DisableTx(false); 
                    HaltTx();          //****this syncs txEnable state with WSJT-X****
                }
                return;
            }

            if (toCall == "CQ")
            {
                SetCallInProg(null);
            }

            DebugOutputStatus();

            //save all call signs a report msg was sent to
            //if this is an interrupting call, or is interrupted, 
            //it still might be rec'd, no harm by recording the attempt
            if ((WsjtxMessage.IsReport(txMsg) || WsjtxMessage.IsRogerReport(txMsg)) && !sentReportList.Contains(toCall)) sentReportList.Add(toCall);

            txInterrupted = (toCall != toCallTxStart);
            int lateTxMsec = ((int)trPeriod / 5);       //how late a tx start can be and still be assumed a valid tx
            shortTx = txBeginTime != DateTime.MaxValue && ((txEndTime - txBeginTime).TotalMilliseconds < trPeriod - lateTxMsec);
            txBeginTime = DateTime.MaxValue;

            if (shortTx || txInterrupted)           //tx was invalid
            {
                DebugOutput($"{spacer}shortTx:{shortTx}");
                DebugOutput($"{spacer}txInterrupted:{txInterrupted} tx originally to '{toCallTxStart}'");
            }
            else
            {
                //check for max Tx count during Tx hold
                if (ctrl.freqCheckBox.Checked && autoFreqPauseMode == autoFreqPauseModes.DISABLED && ctrl.holdCheckBox.Checked)
                {
                    consecTxCount++;
                    if (consecTxCount >= maxConsecTxCount)
                    {
                        if (autoFreqPauseMode == autoFreqPauseModes.DISABLED)
                        {
                            DisableTx(true);
                            autoFreqPauseMode = autoFreqPauseModes.ENABLED;
                            UpdateCallInProg();
                            DebugOutput($"{spacer}auto freq update started (Tx hold)");
                        }
                        else
                        {
                            consecTxCount = 0;
                        }
                    }
                }
                else
                {
                    consecTxCount = 0;
                }
                DebugOutput($"{spacer}autoFreqPauseMode:{autoFreqPauseMode} consecTxCount:{consecTxCount}");

                //could have clicked on "CQ" button in WSJT-X
                if (toCall == "CQ")
                {
                    SetCallInProg(null);
                    DebugOutput($"{spacer}possible CQ button, callInProg:'{CallPriorityString(callInProg)}'");

                    //check for CQ button manually selected, one CQ is allowed if txMode mode
                    if (txMode == TxModes.LISTEN)
                    {
                        txTimeout = true;
                        DebugOutput($"{spacer}txTimeout:{txTimeout} txMode:{txMode}");
                    }

                    //check for consecutive CQs sent
                    if (ctrl.freqCheckBox.Checked && autoFreqPauseMode == autoFreqPauseModes.DISABLED && txMode == TxModes.CALL_CQ)
                    {
                        if (++consecCqCount >= maxConsecCqCount)
                        {
                            if (autoFreqPauseMode == autoFreqPauseModes.DISABLED)
                            {
                                DisableTx(true);
                                autoFreqPauseMode = autoFreqPauseModes.ENABLED;
                                UpdateCallInProg();
                                DebugOutput($"{spacer}auto freq update started (CQs)");
                            }
                            else
                            {
                                consecCqCount = 0;
                            }
                        }
                    }
                    else
                    {
                        consecCqCount = 0;
                    }
                    DebugOutput($"{spacer}toCall:{toCall} autoFreqPauseMode:{autoFreqPauseMode} consecCqCount:{consecCqCount} consecTimeoutCount:{consecTimeoutCount}");
                }
                else
                {
                    consecCqCount = 0;
                    if (!sentCallList.Contains(toCall)) sentCallList.Add(toCall);
                }

                if (debug)
                {
                    DebugOutput($"{spacer}logEarlyCheckBox:{ctrl.logEarlyCheckBox.Checked} IsRogers:{WsjtxMessage.IsRogers(txMsg)} RecdReport:{RecdReport(toCall)} RecdRogerReport:{RecdRogerReport(toCall)}\n{spacer}sentReportList.Contains:{sentReportList.Contains(toCall)} logList.Contains:{logList.Contains(toCall)} sentCallList.Contains:{sentCallList.Contains(toCall)}");
                }

                if (!logList.Contains(toCall))          //toCall not logged yet this mode/band for this session
                {
                    //check for time to log early; NOTE: doing this at Tx end because WSJT-X may have changed Tx msgs (between Tx start and Tx end) due to late-decode for the current call
                    //  option enabled                   just sent RRR                and prev. recd Report  or prev. recd RogerReport   and prev. sent any report
                    if (IsLogEarly(toCall) && WsjtxMessage.IsRogers(txMsg) && (RecdReport(toCall) || RecdRogerReport(toCall)) && sentReportList.Contains(toCall))
                    {
                        DebugOutput($"{spacer}early logging: toCall:'{toCall}'");
                        LogQso(toCall);
                    }
                    //check for QSO completed, trigger next call in the queue
                    if (WsjtxMessage.Is73orRR73(txMsg))
                    {
                        txTimeout = true;      //timeout to Tx the next call in the queue
                        tCall = toCall;
                        xmitCycleCount = 0;
                        SetCallInProg(null);
                        DebugOutput($"{spacer}reset(2): (is 73 or RR73) xmitCycleCount:{xmitCycleCount} txTimeout:{txTimeout}\n           callInProg:'{CallPriorityString(callInProg)}' tCall:'{tCall}'");

                        //NOTE: doing this at Tx end because WSJT-X may have changed Tx msgs (between Tx start and Tx end) due to late-decode for the current call
                        // prev. recd Report    or prev. recd RogerReport   and prev. sent any report
                        if ((RecdReport(toCall) || RecdRogerReport(toCall)) && sentReportList.Contains(toCall))
                        {
                            DebugOutput($"{spacer}normal logging: toCall:'{toCall}'");
                            LogQso(toCall);
                        }
                    }
                }

                //count tx cycles: check for changed Tx call in WSJT-X
                UpdateMaxTxRepeat();
                if (maxTxRepeat > 1 && !IsSameMessage(lastTxMsg, txMsg))
                {
                    if (xmitCycleCount >= 0)
                    {
                        //check  for "to" call changed since last xmit end
                        // !restartQueue = didn't just add this call to queue during late-decode that overlapped Tx start
                        if (!restartQueue && toCall != lastToCall && callQueue.Contains(toCall))
                        {
                            RemoveCall(toCall);         //manually switched to Txing a call that was also in the queue
                        }

                        if (ctrl.holdCheckBox.Checked && toCall == lastToCall)      //overall xmit limit during hold
                        {
                            xmitCycleCount++;
                        }
                        else
                        {
                            xmitCycleCount = 0;
                        }
                        DebugOutput($"{spacer}reset(1) (different msg) xmitCycleCount:{xmitCycleCount} txMsg:'{txMsg}' lastTxMsg:'{lastTxMsg}' holdCheckBox.Checked:{ctrl.holdCheckBox.Checked}");
                    }
                    lastTxMsg = txMsg;
                }
                else        //same "to" call as last xmit or maxTxRepeat = 1, count xmit cycles
                {
                    if (toCall != "CQ")        //don't count CQ (or non-std) calls
                    {
                        xmitCycleCount++;           //count xmits to same call sign at end of xmit cycle
                        UpdateHoldTxRepeat();
                        DebugOutput($"{spacer}(same msg, or maxTxRepeat = 1) xmitCycleCount:{xmitCycleCount} txMsg:'{txMsg}' lastTxMsg:'{lastTxMsg}' holdCheckBox.Checked:{ctrl.holdCheckBox.Checked} holdTxRepeat:{holdTxRepeat}");

                        if ((!ctrl.holdCheckBox.Checked && xmitCycleCount >= maxTxRepeat - 1) || (ctrl.holdCheckBox.Checked && xmitCycleCount >= holdTxRepeat - 1))  //n msgs = n-1 diffs
                        {
                            xmitCycleCount = 0;
                            txTimeout = true;
                            tCall = toCall;        //call to remove from queue, will be null if non-std msg
                            lastTxMsg = null;
                            SetCallInProg(null);
                            ctrl.holdCheckBox.Checked = false;

                            //this caller might call indefinitely, so count call attempts
                            int callTimeouts = 0;
                            if (!timeoutCallDict.TryGetValue(toCall, out callTimeouts) || callTimeouts < maxTimeoutCalls)
                            {
                                if (callTimeouts > 0)
                                {
                                    timeoutCallDict.Remove(toCall);
                                }
                                timeoutCallDict.Add(toCall, ++callTimeouts);
                                DebugOutput($"{spacer}callTimeouts:{callTimeouts}");
                            }

                            DebugOutput($"{spacer}reset(3) (timeout) xmitCycleCount:{xmitCycleCount} txTimeout:{txTimeout} tCall:'{tCall}' callInProg:'{CallPriorityString(callInProg)}' callTimeouts:{callTimeouts}");
                        }
                    }
                    else
                    {
                        //same CQ or non-std call
                        xmitCycleCount = 0;
                        DebugOutput($"{spacer}reset(4) (no action, CQ or non-std) xmitCycleCount:{xmitCycleCount}");
                    }
                }

                if (txTimeout)      //CQ or reply timed out
                {
                    DebugOutput($"{spacer}'{tCall}' timed out or completed");
                    RemoveCall(tCall);

                    if (toCall != "CQ") consecCqCount = 0;
                    //auto freq update when too many timed out replies
                    DebugOutput($"{spacer}ctrl.freqCheckBox.Checked:{ctrl.freqCheckBox.Checked} autoFreqPauseMode:{autoFreqPauseMode} txMode:{txMode} toCall:{toCall}");
                    if (ctrl.freqCheckBox.Checked && autoFreqPauseMode == autoFreqPauseModes.DISABLED && txMode == TxModes.CALL_CQ && toCall != "CQ")
                    {
                        consecTimeoutCount += Math.Max(maxTxRepeat, 2);   //min of 2 in case many stations not hearing calls
                        if (consecTimeoutCount >= maxConsecTimeoutCount)
                        {
                            if (autoFreqPauseMode == autoFreqPauseModes.DISABLED)
                            {
                                DisableTx(true);
                                autoFreqPauseMode = autoFreqPauseModes.ENABLED;
                                UpdateCallInProg();
                                DebugOutput($"{spacer}auto freq update started (no QSOs)");
                            }
                            else
                            {
                                consecTimeoutCount = 0;
                            }
                        }
                    }
                    else
                    {
                        consecTimeoutCount = 0;
                    }
                    DebugOutput($"{spacer}txTimeout:{txTimeout} autoFreqPauseMode:{autoFreqPauseMode} consecTimeoutCount:{consecTimeoutCount} callQueue.Count:{callQueue.Count} consecCqCount:{consecCqCount}");
                }

                //check for time to process new directed CQ
                if (txMode == TxModes.CALL_CQ && (toCall == "CQ" || qsoState == WsjtxMessage.QsoStates.CALLING) && (ctrl.callCqDxCheckBox.Checked || (ctrl.callDirCqCheckBox.Checked && ctrl.directedTextBox.Text.Trim().Length > 0)))
                {
                    xmitCycleCount = 0;
                    newDirCq = true;
                    DebugOutput($"{spacer}reset(5) (new directed CQ) xmitCycleCount:{xmitCycleCount} newDirCq:{newDirCq}");
                }

                if (txMode == TxModes.LISTEN && WsjtxMessage.Is73orRR73(txMsg) && callQueue.Count == 0)
                {
                    DebugOutput($"{spacer}txMode:True Is73orRR73:True callQueue.Count:0");
                    DisableTx(true);
                }
                DebugOutputStatus();
            }

            DebugOutput($"{Time()} Tx end done");
            UpdateDebug();      //unconditional
        }

        private void UpdateWsjtxOptions()
        {
            if (settingChanged)
            {
                emsg.NewTxMsgIdx = 10;
                emsg.GenMsg = $"";          //no effect
                emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
                emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
                emsg.CmdCheck = "";         //ignored
                emsg.Offset = 0;            //ignored
                ba = emsg.GetBytes();
                udpClient2.Send(ba, ba.Length);
                DebugOutput($"{Time()} >>>>>Sent 'Opt Req' cmd:10\n{emsg}");

                ctrl.WsjtxSettingConfirmed();
                settingChanged = false;
            }
        }

        //log a QSO (early or normal timing in QSO progress)
        private void LogQso(string call)
        {
            List<EnqueueDecodeMessage> msgList;
            if (!allCallDict.TryGetValue(call, out msgList)) return;          //no previous call(s) from DX station
            EnqueueDecodeMessage rMsg;
            if ((rMsg = msgList.Find(RogerReport)) == null && (rMsg = msgList.Find(Report)) == null) return;        //the DX station never reported a signal
            if (!sentReportList.Contains(call)) return;         //never reported SNR to the DX station
            RequestLog(call, rMsg, null);
            RemoveAllCall(call);       //prevents duplicate logging, unless caller starts over again
            RemoveCall(call);
        }

        private bool IsEvenPeriod(int secPastHour)          //or seconds since midnight
        {
            if (mode == "FT4")          //irregular
            {
                int sec = secPastHour % 60;     //seconds past the minute
                return (sec >= 0 && sec < 7) || (sec >= 15 && sec < 22) || (sec >= 30 && sec < 37) || (sec >= 45 && sec < 52);
            }

            return (secPastHour / (trPeriod / 1000)) % 2 == 0;
        }

        private string NextDirCq()
        {
            string dirCq = "";

            List<string> dirList = new List<string>();
            if (ctrl.callNonDirCqCheckBox.Checked)
            {
                dirList.Add("");            //note zero length
            }

            if (ctrl.callCqDxCheckBox.Checked)
            {
                dirList.Add("DX");
            }

            if ((ctrl.callDirCqCheckBox.Checked && ctrl.directedTextBox.Text.Trim().Length > 0))
            {
                dirList.AddRange(ctrl.directedTextBox.Text.Trim().ToUpper().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }

            if (dirList.Count > 0)
            {
                string s = dirList[rnd.Next(dirList.Count)];
                if (s.Length <= 4 && s.Length > 0) dirCq = " " + s;          //is directed else non-directed
                DebugOutput($"{spacer}dirCq:'{dirCq}'");
            }

            return dirCq;
        }

        private void ResetNego()
        {
            WsjtxMessage.Reinit();                      //NegoState = WAIT;
            heartbeatRecdTimer.Stop();
            cmdCheckTimer.Stop();
            DebugOutput($"\n{Time()} ResetNego, NegoState:{WsjtxMessage.NegoState}");
            ResetOpMode();
            DebugOutput($"{Time()} Waiting for WSJT-X to run...");
            cmdCheck = RandomCheckString();
            commConfirmed = false;
            mode = "";
            UpdateRR73();
            ShowStatus();
            UpdateDebug();
        }

        private void ResetOpMode()
        {
            StopDecodeTimers();
            postDecodeTimer.Stop();
            firstDecodePass = true;
            DebugOutput($"{Time()} ResetOpMode, postDecodeTimer.Enabled:{postDecodeTimer.Enabled} firstDecodePass:{firstDecodePass}");
            paused = true;
            if (WsjtxMessage.NegoState != WsjtxMessage.NegoStates.WAIT) HaltTx();
            opMode = OpModes.IDLE;
            ShowStatus();
            myCall = null;
            myGrid = null;
            SetCallInProg(null);
            txTimeout = false;
            replyCmd = null;
            curCmd = null;
            replyDecode = null;
            tCall = null;
            newDirCq = false;
            dxCall = null;
            xmitCycleCount = 0;
            logList.Clear();        //can re-log on new mode, band, or session
            ShowLogged();
            ClearCalls(true);
            UpdateModeVisible();
            UpdateModeSelection();
            UpdateTxTimeEnable();
            UpdateDebug();
            UpdateAddCall();
            AutoFreqChanged(ctrl.freqCheckBox.Checked, true);
            ctrl.holdCheckBox.Checked = false;
            ShowStatus();
            DebugOutput($"\n{Time()} ResetOpMode, opMode:{opMode} NegoState:{WsjtxMessage.NegoState}");
        }

        private void ClearCalls(bool clearBandSpecific)             //if only changing Tx period, keep info for the current band, since may return to original Tx period
        {
            callQueue.Clear();
            UpdateMaxTxRepeat();
            callDict.Clear();
            if (clearBandSpecific)
            {
                cqCallDict.Clear();
                timeoutCallDict.Clear();
                allCallDict.Clear();
                sentCallList.Clear();
                sentReportList.Clear();
            }
            ShowQueue();
            xmitCycleCount = 0;
            StopDecodeTimers();
        }

        private void ClearAudioOffsets()
        {
            oddOffset = 0;
            evenOffset = 0;
            DisableAutoFreqPause();
            skipFirstDecodeSeries = true;
            DebugOutput($"{Time()} ClearAudioOffsets, skipFirstDecodeSeries:{skipFirstDecodeSeries}");
        }

        private void UpdateAddCall()
        {
            ctrl.addCallLabel.Visible = (advanced && opMode == OpModes.ACTIVE);
        }

        private bool UpdateCall(string call, EnqueueDecodeMessage msg)
        {
            if (call != null && callDict.ContainsKey(call))
            {
                //check for call saved as a low-priority CQ but now high-priority call to myCall
                EnqueueDecodeMessage dmsg;
                if (callDict.TryGetValue(call, out dmsg) && msg.Priority < dmsg.Priority)
                {
                    DebugOutput($"{spacer}update priority {call}:{CallQueueString()} {CallDictString()}");
                    RemoveCall(call);
                    return AddCall(call, msg);
                }

                callDict.Remove(call);
                callDict.Add(call, msg);
                ShowQueue();
                DebugOutput($"{spacer}updated {call}:{CallQueueString()} {CallDictString()}");
                return true;
            }
            DebugOutput($"{spacer}not updated {call}:{CallQueueString()} {CallDictString()}");
            return false;
        }

        //remove call from queue/dictionary;
        //call not required to be present
        //return false if failure
        private bool RemoveCall(string call)
        {
            EnqueueDecodeMessage msg;
            if (call != null && callDict.TryGetValue(call, out msg))     //dictionary contains call data for this call sign
            {
                callDict.Remove(call);

                string[] qArray = new string[callQueue.Count];
                callQueue.CopyTo(qArray, 0);
                callQueue.Clear();
                for (int i = 0; i < qArray.Length; i++)
                {
                    if (qArray[i] != call) callQueue.Enqueue(qArray[i]);
                }

                if (callDict.Count != callQueue.Count)
                {
                    DebugOutput("ERROR: queueDict and callDict out of sync");
                    UpdateDebug();
                    return false;
                }

                ShowQueue();
                DebugOutput($"{spacer}removed {call}: {CallQueueString()} {CallDictString()}");
                UpdateMaxTxRepeat();
                return true;
            }
            DebugOutput($"{spacer}not removed, not in callQueue '{call}': {CallQueueString()} {CallDictString()}");
            return false;
        }

        //add call/decode to queue/dict;
        //priority decodes (to myCall or wanted directed) move toward the head of the queue
        //because non-priority calls are added manually to queue (i.e., not rec'd, prospective for QSO)
        //but priority calls are decoded calls to myCall (i.e., rec'd and immediately ready for QSO);
        //return false if already added;
        //optionally find a substitute for the specifed decode from a previous signal report from the caller (use earlier qso progress instead of starting over)
        private bool AddCall(string call, EnqueueDecodeMessage msg)
        {
            DebugOutput($"{Time()} AddCall, call:{call}");
            EnqueueDecodeMessage dmsg;
            if (!callDict.TryGetValue(call, out dmsg))     //dictionary does not contain call data for this call sign
            {
                if (msg.Priority < (int)CallPriority.WANTED_CQ)           //may need to insert this priority call ahead of non-priority calls
                {
                    var callArray = callQueue.ToArray();        //make accessible
                    var tmpQueue = new Queue<string>();         //will be the updated queue

                    //go thru calls in reverse time order
                    int i;
                    for (i = 0; i < callArray.Length; i++)
                    {
                        EnqueueDecodeMessage decode;
                        callDict.TryGetValue(callArray[i], out decode);     //get the decode for an existing call in the queue
                        if (decode.Priority > msg.Priority)               //reached the end of priority calls (if any)
                        {
                            break;
                        }
                        else
                        {
                            tmpQueue.Enqueue(callArray[i]); //add the existing priority call 
                        }
                    }
                    tmpQueue.Enqueue(call);         //add the new priority call (before oldest non-priority call, or at end of all-priority-call queue)

                    //fill in the remaining non-priority callls
                    for (int j = i; j < callArray.Length; j++)
                    {
                        tmpQueue.Enqueue(callArray[j]);
                    }
                    callQueue = tmpQueue;
                }
                else            //is a non-priority call, add to end of all calls
                {
                    callQueue.Enqueue(call);
                }

                callDict.Add(call, msg);
                ShowQueue();
                DebugOutput($"{spacer}enqueued {call}: {CallQueueString()} {CallDictString()}");
                UpdateMaxTxRepeat();
                return true;
            }
            DebugOutput($"{spacer}not enqueued {call}: {CallQueueString()} {CallDictString()}");
            return false;
        }

        //return next call/msg in queue;
        //queue not assume to have any entries;
        //return null if failure
        private string PeekNextCall(out EnqueueDecodeMessage dmsg)
        {
            dmsg = null;
            if (callQueue.Count == 0)
            {
                DebugOutput($"{spacer}no peek: {CallQueueString()} {CallDictString()}");
                return null;
            }

            string call = callQueue.Peek();

            if (!callDict.TryGetValue(call, out dmsg))
            {
                DebugOutput("ERROR: '{call}' not found");
                UpdateDebug();
                return null;
            }

            if (WsjtxMessage.Is73(dmsg.Message)) dmsg.Message = dmsg.Message.Replace("73", "");            //important, otherwise WSJT-X will not respond
            DebugOutput($"{spacer}peek {call}: msg:'{dmsg.Message}' {CallQueueString()} {CallDictString()}");
            return call;
        }

        //get next call/msg in queue;
        //queue not assume to have any entries;
        //return null if failure
        private string GetNextCall(out EnqueueDecodeMessage dmsg)
        {
            dmsg = null;
            if (callQueue.Count == 0)
            {
                DebugOutput($"{spacer}not dequeued: {CallQueueString()} {CallDictString()}");
                return null;
            }

            string call = callQueue.Dequeue();

            if (!callDict.TryGetValue(call, out dmsg))
            {
                DebugOutput("ERROR: '{call}' not found");
                UpdateDebug();
                return null;
            }

            if (callDict.ContainsKey(call)) callDict.Remove(call);

            if (callDict.Count != callQueue.Count)
            {
                DebugOutput("ERROR: callDict and queueDict out of sync");
                UpdateDebug();
                return null;
            }

            ShowQueue();
            if (WsjtxMessage.Is73(dmsg.Message)) dmsg.Message = dmsg.Message.Replace("73", "");            //important, otherwise WSJT-X will not respond
            DebugOutput($"{spacer}dequeued {call}: msg:'{dmsg.Message}' {CallQueueString()} {CallDictString()}");
            UpdateMaxTxRepeat();
            return call;
        }

        private string CallQueueString()
        {
            string delim = "";
            StringBuilder sb = new StringBuilder();
            sb.Append("callQueue [");
            foreach (string call in callQueue)
            {
                int pri = 0;
                EnqueueDecodeMessage d;
                if (callDict.TryGetValue(call, out d))
                {
                    pri = d.Priority;
                }
                sb.Append(delim + call + $":{pri}");
                delim = " ";
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string ReportListString()
        {
            string delim = "";
            StringBuilder sb = new StringBuilder();
            sb.Append($"{spacer}sentReportList [");
            foreach (string call in sentReportList)
            {
                sb.Append(delim + call);
                delim = " ";
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string LogListString()
        {
            string delim = "";
            StringBuilder sb = new StringBuilder();
            sb.Append($"{spacer}logList [");
            foreach (string call in logList)
            {
                sb.Append(delim + call);
                delim = " ";
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string CallDictString()
        {
            string delim = "";
            StringBuilder sb = new StringBuilder();
            sb.Append("callDict [");
            foreach (var entry in callDict)
            {
                sb.Append(delim + entry.Key);
                delim = " ";
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string TimeoutCallDictString()
        {
            int count = 0;
            string delim = "";
            StringBuilder sb = new StringBuilder();
            sb.Append($"{spacer}timeoutCallDict [");
            foreach (var entry in timeoutCallDict)
            {
                sb.Append($"{delim}{entry.Key} {entry.Value}");
                delim = ", ";
                if (++count % 10 == 0)
                {
                    sb.Append($"\n{spacer}");
                    delim = "";
                }
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string AllCallDictString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{spacer}allCallDict");
            if (allCallDict.Count == 0)
            {
                sb.Append(" []");
            }
            else
            {
                sb.Append(":");
            }

            foreach (var entry in allCallDict)
            {
                sb.Append($"\n{spacer}{entry.Key} ");
                string delim = "";
                sb.Append("[");
                foreach (EnqueueDecodeMessage msg in entry.Value)
                {
                    sb.Append($"{delim}{msg.Message}:{msg.Priority} @{msg.SinceMidnight}");
                    delim = ", ";
                }
                sb.Append("]");
            }

            return sb.ToString();
        }

        private string PotaLogDictString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{spacer}potaLogDict");
            if (potaLogDict.Count == 0)
            {
                sb.Append(" []");
            }
            else
            {
                sb.Append(":");
            }
            foreach (var entry in potaLogDict)
            {
                string delim = "";
                sb.Append($"\n{spacer}{entry.Key} [");
                foreach (var info in entry.Value)
                {
                    sb.Append($"{delim}{info}");
                    delim = "  ";
                }
                sb.Append("]");
            }

            return sb.ToString();
        }

        private string Time()
        {
            var dt = DateTime.UtcNow;
            return dt.ToString("HHmmss.fff");
        }

        public void Closing()
        {
            DebugOutput($"\n\n{DateTime.UtcNow.ToString("yyyy-MM-dd HHmmss")} UTC ###################### Program closing...");
            if (opMode > OpModes.IDLE) HaltTx();
            ResetOpMode();
            heartbeatRecdTimer.Stop();
            cmdCheckTimer.Stop();
            DebugOutput($"{spacer}heartbeatRecdTimer stop");

            try
            {
                if (emsg != null && udpClient2 != null)
                {
                    //notify WSJT-X
                    emsg.NewTxMsgIdx = 0;           //de-init WSJT-X
                    emsg.GenMsg = $"";         //ignored
                    emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
                    emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
                    emsg.CmdCheck = "";         //ignored
                    ba = emsg.GetBytes();
                    udpClient2.Send(ba, ba.Length);
                    DebugOutput($"{Time()} >>>>>Sent 'De-init Req' cmd:0\n{emsg}");
                    Thread.Sleep(500);
                    udpClient2.Close();
                    udpClient2 = null;
                    DebugOutput($"{spacer}closed udpClient2:{udpClient2}");
                }
            }
            catch (Exception e)         //udpClient might be disposed already
            {
                DebugOutput($"{spacer}error at Closing, error:{e.ToString()}");
            }

            CloseAllUdp();

            if (potaSw != null)
            {
                potaSw.Flush();
                potaSw.Close();
                potaSw = null;
            }

            SetLogFileState(false);         //close log file
        }

        public void Dispose()
        {
        }

        [DllImport("winmm.dll", SetLastError = true)]
        static extern bool PlaySound(string pszSound, UIntPtr hmod, uint fdwSound);

        [Flags]
        private enum SoundFlags
        {
            /// <summary>play synchronously (default)</summary>
            SND_SYNC = 0x0000,
            /// <summary>play asynchronously</summary>
            SND_ASYNC = 0x0001,
            /// <summary>silence (!default) if sound not found</summary>
            SND_NODEFAULT = 0x0002,
            /// <summary>pszSound points to a memory file</summary>
            SND_MEMORY = 0x0004,
            /// <summary>loop the sound until next sndPlaySound</summary>
            SND_LOOP = 0x0008,
            /// <summary>don’t stop any currently playing sound</summary>
            SND_NOSTOP = 0x0010,
            /// <summary>Stop Playing Wave</summary>
            SND_PURGE = 0x40,
            /// <summary>don’t wait if the driver is busy</summary>
            SND_NOWAIT = 0x00002000,
            /// <summary>name is a registry alias</summary>
            SND_ALIAS = 0x00010000,
            /// <summary>alias is a predefined id</summary>
            SND_ALIAS_ID = 0x00110000,
            /// <summary>name is file name</summary>
            SND_FILENAME = 0x00020000,
            /// <summary>name is resource name or atom</summary>
            SND_RESOURCE = 0x00040004
        }

        public void Play(string strFileName)
        {
            soundQueue.Enqueue(strFileName);
            //DebugOutput($"{Time()} Play, enqueued {strFileName}");
        }

        private void ShowQueue()
        {
            ctrl.callListBox.Items.Clear();

            if (callQueue.Count == 0)
            {
                ctrl.callListBox.Font = new Font(ctrl.callListBox.Font.FontFamily, ctrl.callListBox.Font.SizeInPoints, FontStyle.Regular, GraphicsUnit.Point);
                ctrl.callListBox.ForeColor = Color.Gray;
                ctrl.callListBox.SelectionMode = SelectionMode.None;
                ctrl.callListBox.Items.Add("[None]");
                return;
            }

            foreach (string call in callQueue)
            {
                EnqueueDecodeMessage d;
                if (callDict.TryGetValue(call, out d))
                {
                    string callp = d.Message;
                    string oa = d.OffAir ? "!" : "";
                    if (debug)
                    {
                        callp = $"{d.Priority}{oa}:{d.Message}";
                    }
                    else
                    {
                        if (d.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND) callp = $"*{oa}{d.Message}";
                    }
                    ctrl.callListBox.Items.Add(callp);
                }
            }
            ctrl.callListBox.Font = new Font(ctrl.callListBox.Font.FontFamily, ctrl.callListBox.Font.SizeInPoints, FontStyle.Bold, GraphicsUnit.Point);
            ctrl.callListBox.ForeColor = Color.Black;
            ctrl.callListBox.SelectionMode = SelectionMode.One;
        }

        private void ShowStatus()
        {
            string status = "";
            Color foreColor = Color.Black;
            Color backColor = Color.Yellow;     //caution

            try
            {
                if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.WAIT)
                {
                    status = "Waiting for WSJT-X...";
                    foreColor = Color.Black;
                    backColor = Color.Orange;
                    return;
                }

                if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.FAIL || !modeSupported)
                {
                    status = failReason;
                    backColor = Color.Red;
                    return;
                }

                if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.INITIAL)
                {
                    status = "Waiting for WSJT-X to reply...";
                    foreColor = Color.Black;
                    backColor = Color.Orange;
                }
                else
                {
                    switch ((int)opMode)
                    {
                        case (int)OpModes.START:
                            if (ctrl.freqCheckBox.Checked)
                            {
                                status = "Analyzing Rx data, no Tx until ready";
                            }
                            else
                            {
                                status = "Connecting, wait until ready";
                            }
                            foreColor = Color.Black;
                            backColor = Color.Orange;
                            return;
                        case (int)OpModes.IDLE:
                            status = "Connecting, wait until ready";
                            foreColor = Color.Black;
                            backColor = Color.Orange;
                            return;
                        case (int)OpModes.ACTIVE:
                            string hold = ctrl.holdCheckBox.Checked ? "/hold" : "";
                            string mode = txMode == TxModes.LISTEN ? "Listen" : "CQ";
                            string desc = $" ({mode}{hold})";
                            if (paused)
                            {
                                if (WaitingTimedStart())
                                {
                                    status = "Waiting for timed start";
                                }
                                else
                                {
                                    status = "Select 'Enable Tx' in WSJT-X";
                                    foreColor = Color.White;
                                    backColor = Color.Green;
                                }
                            }
                            else    //not paused
                            {
                                if (txMode == TxModes.LISTEN || autoFreqPauseMode > autoFreqPauseModes.DISABLED)
                                {
                                    if (autoFreqPauseMode > autoFreqPauseModes.DISABLED)
                                    {
                                        status = "Updating best Tx frequency...";
                                    }
                                    else
                                    {
                                        status = $"Automatic transmit enabled{desc}";
                                    }

                                    if (!showTxModes)
                                    {
                                        foreColor = Color.White;
                                        backColor = Color.Green;
                                    }
                                }
                                else  //CQ mode and not updatinf best offset
                                {
                                    if (showTxModes)
                                    {
                                        status = $"Automatic transmit enabled{desc}";
                                    }
                                    else
                                    {
                                        status = $"Automatic operation enabled{desc}";
                                        foreColor = Color.White;
                                        backColor = Color.Green;
                                    }
                                }
                            }
                            break;
                    }
                }
            }
            finally
            {
                ctrl.statusText.ForeColor = foreColor;
                ctrl.statusText.BackColor = backColor;
                ctrl.statusText.Text = status;
            }
        }

        private void ShowLogged()
        {
            ctrl.loggedLabel.Text = $"Calls auto-logged ({logList.Count})";
            ctrl.logListBox.Items.Clear();
            if (logList.Count == 0)
            {
                ctrl.logListBox.Font = new Font(ctrl.callListBox.Font.FontFamily, ctrl.callListBox.Font.SizeInPoints, FontStyle.Regular, GraphicsUnit.Point);
                ctrl.logListBox.ForeColor = Color.Gray;
                ctrl.logListBox.Items.Add("[None]");
                return;
            }

            var rList = logList.GetRange(0, logList.Count);
            rList.Reverse();
            ctrl.logListBox.Font = new Font(ctrl.callListBox.Font.FontFamily, ctrl.callListBox.Font.SizeInPoints, FontStyle.Bold, GraphicsUnit.Point);
            ctrl.logListBox.ForeColor = Color.Black;
            foreach (string call in rList)
            {
                ctrl.logListBox.Items.Add(call);
            }
        }

        //process a manually- or automatically-generated request to add a decode to call reply queue
        public void AddSelectedCall(EnqueueDecodeMessage emsg)
        {
            string msg = emsg.Message;
            string deCall = WsjtxMessage.DeCall(msg);
            string toCall = WsjtxMessage.ToCall(msg);
            string directedTo = WsjtxMessage.DirectedTo(msg);
            bool isPota = WsjtxMessage.IsPotaOrSota(msg);
            bool isCq = WsjtxMessage.IsCQ(emsg.Message);                //CQ format check
            bool isContest = WsjtxMessage.IsContest(emsg.Message);
            bool isWantedNewCountryOnBand = ctrl.replyNewDxccCheckBox.Checked && emsg.IsNewCountryOnBand && advanced;
            bool isWantedNewCountry = ctrl.replyNewDxccCheckBox.Checked && emsg.IsNewCountry && advanced;
            bool isWantedNonDirected = ((ctrl.replyDxCheckBox.Checked && emsg.IsDx) || (ctrl.replyLocalCheckBox.Checked && !emsg.IsDx)) && directedTo == null;
            bool isWantedDirected = ctrl.replyDirCqCheckBox.Checked && IsDirectedAlert(directedTo, emsg.IsDx);
            UpdateMaxTxRepeat();

            emsg.Priority = (int)CallPriority.DEFAULT;
            if (isCq && (isWantedNonDirected || isWantedDirected)) emsg.Priority = (int)CallPriority.WANTED_CQ;
            if (!emsg.AutoGen) emsg.Priority = (int)CallPriority.MANUAL_CQ;     //generated by click
            if (toCall == myCall) emsg.Priority = (int)CallPriority.TO_MYCALL;
            if (isWantedNewCountryOnBand) emsg.Priority = (int)CallPriority.NEW_COUNTRY_ON_BAND;
            if (isWantedNewCountry) emsg.Priority = (int)CallPriority.NEW_COUNTRY;

            if (deCall == null) return;

            int replyDecodePriority = (int)CallPriority.NEW_COUNTRY;      //simulate highest priority
            if (replyDecode != null)
            {
                //DebugOutput($"{spacer}replyDecode.Message: {replyDecode.Message} replyDecode.Priority:{replyDecode.Priority}");
                if (!RecdAnyMsg(replyDecode.DeCall())) replyDecodePriority = replyDecode.Priority;
            }

            //*******************
            //Auto-generated call
            //*******************
            //auto-generated notification of a call rec'd by WSJT-X;
            if (emsg.AutoGen)       //automatically-generated queue request, not clicked
            {
                //check for timed start pending
                if (WaitingTimedStart()) return;

                //check for call sign logged already on this band, *except* if POTA/SOTA which can be logged repeatedly
                if (!emsg.IsNewCallOnBand && !isPota) return;

                if ((!isCq && !isWantedNewCountryOnBand) || isContest || !advanced || (isWantedNewCountryOnBand && ctrl.exceptTextBox.Text.Contains(deCall))) return;                //non-std format

                //check for call to be queued
                if (isWantedNonDirected || isWantedDirected || isWantedNewCountryOnBand)
                {
                    DebugOutput($"{Time()}");
                    DebugOutput($"{emsg}\n{spacer}msg:'{emsg.Message}'");
                    DebugOutput($"{spacer}AddSelectedCall, isCq:{isCq} deCall:'{deCall}' emsg.Priority:{emsg.Priority} IsDx:{emsg.IsDx} isWantedNonDirected:{isWantedNonDirected} isWantedDirected:{isWantedDirected}");
                    DebugOutput($"{spacer}isWantedNewCountry:{isWantedNewCountry} isWantedNewCountryOnBand:{isWantedNewCountryOnBand} maxAutoGenEnqueue:{maxAutoGenEnqueue} maxPrevCqs:{maxPrevCqs}");
                    DebugOutput($"{spacer}isNewCountry:{emsg.IsNewCountry} isNewCountryOnBand:{emsg.IsNewCountryOnBand} isContest:{isContest} isPota:{isPota} directedTo:'{directedTo}'");
                    DebugOutput($"{spacer}toCall: '{toCall}' callInProg:'{CallPriorityString(callInProg)}' callQueue.Count:{callQueue.Count} callQueue.Contains:{callQueue.Contains(deCall)} logList.Contains:{logList.Contains(deCall)}");
                    if (myCall == null || opMode != OpModes.ACTIVE
                        || (txMode == TxModes.CALL_CQ && !paused && IsEvenPeriod((emsg.SinceMidnight.Minutes * 60) + emsg.SinceMidnight.Seconds) == txFirst)
                        || msg.Contains("...")) return;

                    if (deCall == null || callQueue.Contains(deCall) || deCall == callInProg) return;

                    if (isPota) DebugOutput($"{PotaLogDictString()}");
                    List<string> list;
                    if (isPota && potaLogDict.TryGetValue(deCall, out list))
                    {
                        string band = FreqToBand(dialFrequency / 1e6);
                        string date = DateTime.Now.ToShortDateString();     //local date/time
                        string potaInfo = $"{date},{band},{mode}";
                        DebugOutput($"{spacer}potaInfo:{potaInfo}");
                        if (list.Contains(potaInfo)) return;         //already logged today (local date/time) on this mode and band
                    }

                    if (callQueue.Count < maxAutoGenEnqueue || isWantedDirected || isWantedNewCountryOnBand)
                    {
                        int prevCqs = 0;
                        int maxCqs = isPota ? maxPrevPotaCqs : (isWantedNewCountryOnBand ? maxNewCountryCqs : maxPrevCqs);
                        DebugOutput($"{spacer}replyDecodePriority:{replyDecodePriority} prevCqs:{prevCqs} maxPrevPotaCqs:{maxPrevPotaCqs} maxCqs:{maxCqs}");
                        if (!cqCallDict.TryGetValue(deCall, out prevCqs) || prevCqs < maxCqs)
                        {
                            //add to call queue;
                            //optionally substitute a previous signal report from caller
                            //for the message to add (reason: don't lose earlier QSO progress)
                            AddCall(deCall, emsg);

                            //                            tempOnly 2/20/24 tx not *temporarily* disabled during auto freq update
                            if ((!paused && !txEnabled && autoFreqPauseMode == autoFreqPauseModes.DISABLED && txMode == TxModes.LISTEN && callQueue.Count == 1) || emsg.Priority < replyDecodePriority)
                            {
                                restartQueue = true;
                                DebugOutput($"{spacer}restartQueue:{restartQueue}");

                                if (emsg.Priority < replyDecodePriority && replyDecodePriority <= (int)CallPriority.TO_MYCALL && !logList.Contains(replyDecode.DeCall()) && RecdAnyMsg(replyDecode.DeCall()))
                                {
                                    AddCall(replyDecode.DeCall(), replyDecode);
                                    DebugOutput($"{spacer}re-added {replyDecode.DeCall()} to queue");
                                }
                            }

                            //check for call after decodes "done"
                            if (!processDecodeTimer.Enabled)
                            {
                                DebugOutput($"{spacer}late decode");
                                if (emsg.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND)
                                {
                                    DebugOutput($"{spacer}process high-priority late decode, restartQueue:{restartQueue}");
                                    StartProcessDecodeTimer2();
                                }
                            }

                            if (prevCqs > 0)                   //track how many times Controlller replied to CQ from this call sign
                            {
                                cqCallDict.Remove(deCall);
                            }
                            cqCallDict.Add(deCall, prevCqs + 1);
                            DebugOutput($"{spacer}CQ added, prevCqs:{prevCqs}");
                            if (toCall != myCall && ctrl.callAddedCheckBox.Checked)
                            {
                                if (isWantedNewCountry)      //new on any band
                                {
                                    Play("dingding.wav");
                                }
                                else
                                {
                                    Play("blip.wav");
                                }
                            }
                        }
                        else
                        {
                            DebugOutput($"{spacer}call not added, prevCqs:{prevCqs}");
                        }
                    }
                    UpdateDebug();
                }
                return;
            }

            //***********************
            //Manually-generated call
            //***********************
            //can be any type of msg

            if (WaitingTimedStart())
            {
                ctrl.ShowMsg("Can't add calls before timed start", true);
                return;
            }

            //can enable Tx
            if (myCall == null || opMode != OpModes.ACTIVE)
            {
                ctrl.ShowMsg("Not ready to add calls yet", true);
                return;
            }

            if (msg.Contains("..."))
            {
                ctrl.ShowMsg("Can't add call from hashed msg", true);
                return;
            }

            if (isContest)
            {
                ctrl.ShowMsg("Can't add contest call", true);
                return;
            }

            if (deCall == null)
            {
                ctrl.ShowMsg("No 'from' call in message", true);
                return;
            }

            //**********************
            //Ctrl + Alt key pressed
            //**********************
            if (emsg.Modifier)
            {
                if (toCall == "CQ")
                {
                    ctrl.ShowMsg("Message is CQ", true);
                    return;
                }

                if (toCall == null)
                {
                    ctrl.ShowMsg("No 'to' call in message", true);
                    return;
                }

                if (toCall == myCall)
                {
                    ctrl.ShowMsg($"Message is to my station", true);
                    return;
                }

                if (paused)
                {
                    ctrl.ShowMsg($"Select 'Enable Tx' in WSJT-X first", true);
                    return;
                }

                if (txMode == TxModes.LISTEN)
                {
                    if (callQueue.Contains(toCall))
                    {
                        ctrl.ShowMsg($"{toCall} already on call list", true);
                        return;
                    }
                    if (txEnabled && toCall == callInProg)
                    {
                        ctrl.ShowMsg($"{toCall} is already in progress", true);
                        return;
                    }
                }

                if (txMode == TxModes.LISTEN && emsg.Priority > (int)CallPriority.NEW_COUNTRY_ON_BAND && ctrl.replyNewDxccCheckBox.Checked && ctrl.replyNewOnlyCheckBox.Checked)
                {
                    ctrl.ShowMsg($"{toCall} is not a new country", true);
                    return;
                }

                DebugOutput($"\n{Time()} ctrl/alt/dbl-click on {toCall}");
                DebugOutput($"{emsg}\n{spacer}msg:'{emsg.Message}'");
                DebugOutput($"{spacer}AddSelectedCall, isCq:{isCq} deCall:'{deCall}' emsg.Priority:{emsg.Priority}isNewCountry:{emsg.IsNewCountry} isNewCountryOnBand:{emsg.IsNewCountryOnBand}");
                DebugOutput($"{spacer}modifier:{emsg.Modifier} AutoGen:{emsg.AutoGen}");

                //build a CQ message to reply to
                EnqueueDecodeMessage nmsg = new EnqueueDecodeMessage();
                nmsg.Mode = rawMode;
                nmsg.SchemaVersion = WsjtxMessage.NegotiatedSchemaVersion;
                nmsg.New = true;
                nmsg.OffAir = false;
                nmsg.UseStdReply = true;   //override skipGrid since no SNR available
                nmsg.Id = WsjtxMessage.UniqueId;
                nmsg.Snr = noSnrAvail;      //used as a flag to prevent signal report, use grid reply instead
                nmsg.DeltaTime = 0.0;       //not used
                nmsg.DeltaFrequency = (int)defaultAudioOffset; //real offset unknown
                nmsg.Message = $"CQ {toCall}";
                nmsg.SinceMidnight = latestDecodeTime + new TimeSpan(0, 0, 0, 0, (int)trPeriod);
                nmsg.RxDate = latestDecodeDate;
                nmsg.Priority = emsg.Priority;
                //DebugOutput($"{nmsg}");

                AddCall(toCall, nmsg);              //add to call queue

                if (txMode == TxModes.CALL_CQ)                  //if CQing, process this call immediately
                {
                    txTimeout = true;                   //switch to other tx period
                    DebugOutput($"{spacer}txTimeout:{txTimeout} callInProg:'{CallPriorityString(callInProg)}' txFirst:{txFirst}");
                    CheckNextXmit();        //results in tx enabled
                    if (ctrl.skipGridCheckBox.Checked) settingChanged = true; //restore skipGrid setting overriden above
                }

                if (nmsg.Priority < replyDecodePriority && replyDecodePriority <= (int)CallPriority.TO_MYCALL && !logList.Contains(replyDecode.DeCall()) && RecdAnyMsg(replyDecode.DeCall()))
                {
                    AddCall(replyDecode.DeCall(), replyDecode);
                    DebugOutput($"{spacer}re-added {replyDecode.DeCall()} to queue");
                }

                if (ctrl.callAddedCheckBox.Checked) Play("blip.wav");
            }
            else
            {
                //***************
                //Alt key pressed
                //***************
                if ((!showTxModes || (txMode == TxModes.CALL_CQ)) && IsEvenPeriod((emsg.SinceMidnight.Minutes * 60) + emsg.SinceMidnight.Seconds) == txFirst)
                {
                    string s = txFirst ? "odd" : "even/1st";
                    ctrl.ShowMsg($"Select in '{s}' period (or 'Listen' mode)", true);
                    return;
                }

                if (callQueue.Contains(deCall))
                {
                    ctrl.ShowMsg($"{deCall} already on call list", true);
                    return;
                }

                if (txEnabled && deCall == callInProg)
                {
                    ctrl.ShowMsg($"{deCall} is already in progress", true);
                    return;
                }

                if (emsg.Snr == noSnrAvail) emsg.UseStdReply = true;            //no SNR available, force standard (grid) reply (as opposed to SNR reply)

                DebugOutput($"\n{Time()} alt/dbl-click on {toCall}");
                DebugOutput($"{emsg}\n{spacer}msg:'{emsg.Message}'");
                DebugOutput($"{spacer}AddSelectedCall, isCq:{isCq} deCall:'{deCall}' emsg.Priority:{emsg.Priority} isNewCountry:{emsg.IsNewCountry} isNewCountryOnBand:{emsg.IsNewCountryOnBand}");
                DebugOutput($"{spacer}emsg.Modifier:{emsg.Modifier} emsg.AutoGen:{emsg.AutoGen} emsg.Snr:{emsg.Snr} emsg:UseStdReply:{emsg:UseStdReply}");

                //message to reply to
                AddCall(deCall, emsg);              //add to call queue

                if (emsg.Priority < replyDecodePriority && replyDecodePriority <= (int)CallPriority.TO_MYCALL && !logList.Contains(replyDecode.DeCall()) && RecdAnyMsg(replyDecode.DeCall()))
                {
                    AddCall(replyDecode.DeCall(), replyDecode);
                    DebugOutput($"{spacer}re-added {replyDecode.DeCall()} to queue");
                }

                if (ctrl.callAddedCheckBox.Checked) Play("blip.wav");
            }
            UpdateDebug();
        }

        public void UpdateDebug()
        {
            if (!debug) return;
            string s;
            bool chg = false;

            try
            {
                ctrl.label5.ForeColor = wsjtxTxEnableButton ? Color.White : Color.Black;
                ctrl.label5.BackColor = wsjtxTxEnableButton ? Color.Red : Color.LightGray;
                ctrl.label5.Text = $"En but: {wsjtxTxEnableButton.ToString().Substring(0, 1)}";

                ctrl.label6.Text = $"{msg.GetType().Name.Substring(0, 6)}";

                ctrl.label7.ForeColor = txEnabled ? Color.White : Color.Black;
                ctrl.label7.BackColor = txEnabled ? Color.Red : Color.LightGray;
                ctrl.label7.Text = $"txEn: {txEnabled.ToString().Substring(0, 1)}";

                ctrl.label23.Text = $"t/c/p/e: {maxTxRepeat}/{maxPrevCqs}/{maxPrevPotaCqs}/{maxAutoGenEnqueue}";

                if (replyCmd != lastReplyCmdDebug)
                {
                    ctrl.label8.ForeColor = Color.Red;
                    ctrl.label21.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label8.Text = $"cmd from: {WsjtxMessage.DeCall(replyCmd)}";
                lastReplyCmdDebug = replyCmd;

                ctrl.label9.Text = $"opMode: {opMode}-{WsjtxMessage.NegoState}";

                string txTo = (txMsg == null ? "" : WsjtxMessage.ToCall(txMsg));
                s = (txTo == "CQ" ? null : txTo);
                ctrl.label12.Text = $"tx to: {s}";

                if (callInProg != lastCallInProgDebug)
                {
                    ctrl.label13.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label13.Text = $"in-prog: {CallPriorityString(callInProg)}";
                lastCallInProgDebug = callInProg;

                if (qsoState != lastQsoStateDebug)
                {
                    ctrl.label14.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label14.Text = $"qso:{qsoState}";
                lastQsoStateDebug = qsoState;

                if (evenOffset != lastEvenOffsetDebug)
                {
                    ctrl.label15.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label15.Text = $"evn:{evenOffset}";
                lastEvenOffsetDebug = evenOffset;

                if (oddOffset != lastOddOffsetDebug)
                {
                    ctrl.label16.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label16.Text = $"odd:{oddOffset}";
                lastOddOffsetDebug = oddOffset;

                if (txTimeout != lastTxTimeoutDebug)
                {
                    ctrl.label10.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label10.Text = $"t/o: {txTimeout.ToString().Substring(0, 1)}";
                lastTxTimeoutDebug = txTimeout;

                if (txFirst != lastTxFirstDebug)
                {
                    ctrl.label11.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label11.Text = $"txFirst: {txFirst.ToString().Substring(0, 1)}";
                lastTxFirstDebug = txFirst;

                if (restartQueue != lastRestartQueueDebug)
                {
                    ctrl.label24.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label24.Text = $"rstQ: {restartQueue.ToString().Substring(0, 1)}";
                lastRestartQueueDebug = restartQueue;

                if (transmitting != lastTransmittingDebug)
                {
                    ctrl.label25.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label25.Text = $"tx: {transmitting.ToString().Substring(0, 1)}";
                lastTransmittingDebug = transmitting;

                if (txMsg != lastTxMsgDebug)
                {
                    ctrl.label19.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label19.Text = $"tx:  {txMsg}";
                lastTxMsgDebug = txMsg;

                if (lastTxMsg != lastLastTxMsgDebug)
                {
                    ctrl.label18.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label18.Text = $"last: {lastTxMsg}";
                lastLastTxMsgDebug = lastTxMsg;

                if (lastDxCallDebug != dxCall)
                {
                    ctrl.label4.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label4.Text = $"dxCall: {dxCall}";
                lastDxCallDebug = dxCall;

                ctrl.label21.Text = $"replyCmd: {replyCmd}";

                if (autoFreqPauseMode != lastAutoFreqPauseModeDebug)
                {
                    ctrl.label17.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label17.Text = $"aFP:{autoFreqPauseMode}";
                lastAutoFreqPauseModeDebug = autoFreqPauseMode;

                if (consecCqCount != lastConsecCqCountDebug)
                {
                    ctrl.label26.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label26.Text = $"cCQ:{consecCqCount}/{maxConsecCqCount}";
                lastConsecCqCountDebug = consecCqCount;

                if (consecTimeoutCount != lastConsecTimeoutCount)
                {
                    ctrl.label27.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label27.Text = $"cTo:{consecTimeoutCount}/{maxConsecTimeoutCount}";
                lastConsecTimeoutCount = consecTimeoutCount;

                ctrl.label20.Text = $"t/o cnt: {xmitCycleCount}";

                if (dblClk)
                {
                    ctrl.label3.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label3.Text = $"dblClk";

                if (consecTxCount != lastConsecTxCountDebug)
                {
                    ctrl.label1.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label1.Text = $"cTx:{consecTxCount}/{maxConsecTxCount}";
                lastConsecTxCountDebug = consecTxCount;

                if (paused != lastPausedDebug)
                {
                    ctrl.label2.ForeColor = Color.Red;
                    chg = true;
                }
                ctrl.label2.Text = $"paused:{paused.ToString().Substring(0, 1)}";
                lastPausedDebug = paused;

                if (txMode != lastTxModeDebug)
                {
                    ctrl.label28.ForeColor = Color.Red;
                    chg = true;
                }
                string m = txMode == TxModes.LISTEN ? "Lis" : "CQ";
                ctrl.label28.Text = $"TxMode:{m}";
                lastTxModeDebug = txMode;

                ctrl.label22.Text = $"tCall: {tCall}";
                ctrl.label29.Text = $"shTx: {shortTx.ToString().Substring(0, 1)}";
                ctrl.label30.Text = $"txInt: {txInterrupted.ToString().Substring(0, 1)}";

                if (chg)
                {
                    ctrl.debugHighlightTimer.Stop();
                    ctrl.debugHighlightTimer.Interval = 4000;
                    ctrl.debugHighlightTimer.Start();
                }
            }
            catch (Exception err)
            {
                DebugOutput($"ERROR: UpdateDebug: err:{err}");
            }
        }

        public void ConnectionDialog()
        {
            ctrl.initialConnFaultTimer.Stop();
            heartbeatRecdTimer.Stop();
            if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.INITIAL)
            {
                heartbeatRecdTimer.Stop();
                suspendComm = true;         //in case udpClient msgs start 
                string s = multicast ? "\n\nIn WSJT-X:\n- Select File | Settings then the 'Reporting' tab.\n\n'- Try different 'Outgoing interface' selection(s), including selecting all of them." : "";
                ctrl.BringToFront();
                MessageBox.Show($"No response from WSJT-X.{s}\n\n{pgmName} will continue waiting for WSJT-X to respond when you close this dialog.\n\nAlternatively, select 'Setup' and override the auto-detected UDP settings.", pgmName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                suspendComm = false;
                ctrl.initialConnFaultTimer.Start();
            }
        }

        public void CmdCheckDialog()
        {
            cmdCheckTimer.Stop();
            if (commConfirmed) return;

            heartbeatRecdTimer.Stop();
            suspendComm = true;
            ctrl.BringToFront();
            MessageBox.Show($"Unable to make a two-way connection with WSJT-X.\n\n{pgmName} will try again when you close this dialog.", pgmName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ResetOpMode();

            emsg.NewTxMsgIdx = 7;
            emsg.GenMsg = $"";          //no effect
            emsg.ReplyReqd = true;
            emsg.EnableTimeout = !debug;
            cmdCheck = RandomCheckString();
            emsg.CmdCheck = cmdCheck;
            ba = emsg.GetBytes();
            udpClient2.Send(ba, ba.Length);
            DebugOutput($"{Time()} >>>>>Sent 'Ack Req' cmd:7 cmdCheck:{cmdCheck}\n{emsg}");

            cmdCheckTimer.Interval = 10000;           //set up cmd check timeout
            cmdCheckTimer.Start();
            DebugOutput($"{Time()} Check cmd timer restarted");

            suspendComm = false;
        }

        private void ModelessDialog(string text)
        {
            new Thread(new ThreadStart(delegate
             {
                 ctrl.helpDialogsPending++;
                 MessageBox.Show
                 (
                   text,
                   pgmName,
                   MessageBoxButtons.OK,
                   MessageBoxIcon.Warning
                 );
                 ctrl.helpDialogsPending--;
             })).Start();
        }

        private string AcceptableVersionsString()
        {
            string delim = "";
            StringBuilder sb = new StringBuilder();

            foreach (string s in acceptableWsjtxVersions)
            {
                sb.Append(delim);
                sb.Append(s);
                delim = "\n";
            }

            return sb.ToString();
        }

        private string RandomCheckString()
        {
            string s = rnd.Next().ToString();
            if (s.Length > 8) s = s.Substring(0, 8);
            return s;
        }

        private void DebugOutput(string s)
        {
            if (diagLog)
            {
                try
                {
                    if (logSw != null) logSw.WriteLine(s);
                }
                catch (Exception e)
                {
#if DEBUG
                    Console.WriteLine(e);
#endif
                }
            }

#if DEBUG
            if (debug)
            {
                Console.WriteLine(s);
            }
#endif
        }

        //during decoding, check for late signoff (73 or RR73) 
        //from a call sign that isn't (or won't be) the call in progress;
        //if reports have been exchanged, log the QSO;
        //logging is done directly via log file, not via WSJT-X
        private void CheckLateLog(string call, EnqueueDecodeMessage msg)
        {
            DebugOutput($"{spacer}CheckLateLog: call:'{call}' callInProg:'{CallPriorityString(callInProg)}' txTimeout:{txTimeout} msg:{msg.Message} Is73orRR73:{WsjtxMessage.Is73orRR73(msg.Message)}");
            if (call == null || !WsjtxMessage.Is73orRR73(msg.Message))
            {
                DebugOutput($"{spacer}no late log: call is in progress (or just timed out), or is not RRR73 or 73");
                return;
            }

            if (logList.Contains(call))         //call already logged for thos mode or band for this session
            {
                DebugOutput($"{spacer}no late log: call is already logged");
                return;
            }

            List<EnqueueDecodeMessage> msgList;
            if (!allCallDict.TryGetValue(call, out msgList))
            {
                DebugOutput($"{spacer}no late log: no previous call(s) rec'd");
                return;          //no previous call(s) from DX station
            }

            EnqueueDecodeMessage rMsg;
            if ((rMsg = msgList.Find(RogerReport)) == null && (rMsg = msgList.Find(Report)) == null)
            {
                DebugOutput($"{spacer}no late log: no previous report(s) rec'd");
                return;        //the DX station never reported a signal
            }

            if (!sentReportList.Contains(call))
            {
                DebugOutput($"{spacer}no late log: no previous report(s) sent");
                return;         //never reported SNR to the DX station
            }

            RequestLog(call, rMsg, msg);              //process a "late" QSO completion
            RemoveAllCall(call);       //prevents duplicate logging, unless caller starts over again
            RemoveCall(call);
        }

        private bool Rogers(EnqueueDecodeMessage msg)
        {
            return WsjtxMessage.IsRogers(msg.Message);
        }

        private bool RogerReport(EnqueueDecodeMessage msg)
        {
            return WsjtxMessage.IsRogerReport(msg.Message);
        }

        private bool Report(EnqueueDecodeMessage msg)
        {
            return WsjtxMessage.IsReport(msg.Message);
        }

        private bool Reply(EnqueueDecodeMessage msg)
        {
            return WsjtxMessage.IsReply(msg.Message);
        }

        private bool Signoff(EnqueueDecodeMessage msg)
        {
            return WsjtxMessage.Is73orRR73(msg.Message);
        }


        private bool CQ(EnqueueDecodeMessage msg)
        {
            return WsjtxMessage.IsCQ(msg.Message);
        }

        //request WSJT-X log a QSO to the WSJT-X .ADI log file and re-broadcast to UDP listeners;
        //logging done only via WSJT-X because WSJT-X keeps track of 'logged-before' status, 
        //which is important to processing CQ notification msgs received from WSJT-X
        //recdMsg null if logging because of a sent msg
        private void RequestLog(string call, EnqueueDecodeMessage reptMsg, EnqueueDecodeMessage recdMsg)
        {
            string qsoDateOff, qsoTimeOff;

            //<call:4>W1AW  <gridsquare:4>EM77 <mode:3>FT8 <rst_sent:3>-10 <rst_rcvd:3>+01 <qso_date:8>20201226 
            //<time_on:6>042215 <qso_date_off:8>20201226 <time_off:6>042300 <band:3>40m <freq:8>7.076439 
            //<station_callsign:4>WM8Q <my_gridsquare:6>DN61OK <eor>

            string rstSent = reptMsg.Snr == 0 ? "+00" : (reptMsg.Snr > 0 ? "+" + reptMsg.Snr.ToString("D2") : reptMsg.Snr.ToString("D2"));
            string rstRecd = WsjtxMessage.RstRecd(reptMsg.Message);
            string qsoDateOn = reptMsg.RxDate.ToString("yyyyMMdd");
            string qsoTimeOn = reptMsg.SinceMidnight.ToString("hhmmss");      //one of the report decodes
            EnqueueDecodeMessage cqMsg = CqMsg(call);
            bool isPota = cqMsg != null && WsjtxMessage.IsPotaOrSota(cqMsg.Message);
            var dtNow = DateTime.UtcNow;

            if (recdMsg == null)            //logging because of xmitted RRR, 73, or RR73 (not because of a rec'd msg)
            {
                qsoDateOff = dtNow.ToString("yyyyMMdd");
                qsoTimeOff = dtNow.TimeOfDay.ToString("hhmmss");
            }
            else
            {
                qsoDateOff = recdMsg.RxDate.ToString("yyyyMMdd");
                qsoTimeOff = recdMsg.SinceMidnight.ToString("hhmmss");
            }
            string qsoMode = mode;
            string grid = "";
            EnqueueDecodeMessage gridMsg = ReplyMsg(call);
            if (gridMsg == null) gridMsg = cqMsg;
            if (gridMsg != null)
            {
                string g = WsjtxMessage.Grid(gridMsg.Message);
                if (g != null) grid = g;                //CQ does have a grid
            }
            string freq = ((dialFrequency + txOffset) / 1e6).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string band = FreqToBand(dialFrequency / 1e6);

            string adifRecord = $"<call:{call.Length}>{call} <gridsquare:{grid.Length}>{grid} <mode:{mode.Length}>{mode} <rst_sent:{rstSent.Length}>{rstSent} <rst_rcvd:{rstRecd.Length}>{rstRecd} <qso_date:{qsoDateOn.Length}>{qsoDateOn} <time_on:{qsoTimeOn.Length}>{qsoTimeOn} <qso_date_off:{qsoDateOff.Length}>{qsoDateOff} <time_off:{qsoTimeOff.Length}>{qsoTimeOff} <band:{band.Length}>{band} <freq:{freq.Length}>{freq} <station_callsign:{myCall.Length}>{myCall} <my_gridsquare:{myGrid.Length}>{myGrid}";

            //request add record to log / worked before (using explicit parameters, unlike typical WSJT-X logging)
            //send ADIF record to WSJT-X for re-broadcast to logging pgms
            emsg.NewTxMsgIdx = 255;     //function code
            emsg.GenMsg = $"{call}${grid}${band}${mode}";
            emsg.Param0 = false;      //no effect
            emsg.Param1 = false;      //no effect
            emsg.CmdCheck = adifRecord;
            ba = emsg.GetBytes();
            udpClient2.Send(ba, ba.Length);
            DebugOutput($"{Time()} >>>>>Sent 'Broadcast' cmd:255");
            emsg.CmdCheck = "";

            if (ctrl.loggedCheckBox.Checked) Play("echo.wav");
            ctrl.ShowMsg($"Logging QSO with {call}", false);
            logList.Add(call);      //even if already logged this mode/band for this session
            if (isPota) AddPotaLogDict(call, DateTime.Now, band, mode);         //local date/time
            StopCqCall(call);     //no more CQ responses to this call
            ShowLogged();
            consecCqCount = 0;
            consecTimeoutCount = 0;
            consecTxCount = 0;
            DebugOutput($"{spacer}QSO logged: call'{call}'");
            UpdateCallInProg();
            UpdateDebug();
        }

        private string FreqToBand(double? freq)
        {
            if (freq == null) return "";
            if (freq >= 0.1357 && freq <= 0.1378) return "2200m";
            if (freq >= 0.472 && freq <= 0.479) return "630m";
            if (freq >= 1.8 && freq <= 2.0) return "160m";
            if (freq >= 3.5 && freq <= 4.0) return "80m";
            if (freq >= 5.35 && freq <= 5.37) return "60m";
            if (freq >= 7.0 && freq <= 7.3) return "40m";
            if (freq >= 10.1 && freq <= 10.15) return "30m";
            if (freq >= 14.0 && freq <= 14.35) return "20m";
            if (freq >= 18.068 && freq <= 18.168) return "17m";
            if (freq >= 21.0 && freq <= 21.45) return "15m";
            if (freq >= 24.89 && freq <= 24.99) return "12m";
            if (freq >= 28.0 && freq <= 29.7) return "10m";
            if (freq >= 50.0 && freq <= 54.0) return "6m";
            if (freq >= 144.0 && freq <= 148.0) return "2m";
            return "";
        }

        private void RemoveAllCall(string call)
        {
            if (call == null) return;
            if (allCallDict.Remove(call)) DebugOutput($"{spacer}removed '{call}' from allCallDict");
            if (sentReportList.Remove(call)) DebugOutput($"{spacer}removed '{call}' from sentReportList");
            if (sentCallList.Remove(call)) DebugOutput($"{spacer}removed '{call}' from sentCallList");
        }

        private string CurrentStatus()
        {
            return $"myCall:'{myCall}' callInProg:'{CallPriorityString(callInProg)}' qsoState:{qsoState} lastQsoState:{lastQsoState} txMsg:'{txMsg}'\n           lastTxMsg:'{lastTxMsg}' curCmd:'{curCmd}' replyCmd:'{replyCmd}'\n           replyDecode:{replyDecode}\n           txTimeout:{txTimeout} xmitCycleCount:{xmitCycleCount} transmitting:{transmitting} mode:{mode} txEnabled:{txEnabled}\n           txFirst:{txFirst} dxCall:'{dxCall}' trPeriod:{trPeriod} settingChanged:{settingChanged}\n           newDirCq:{newDirCq} tCall:'{tCall}' decoding:{decoding} restartQueue:{restartQueue} paused:{paused} txMode:{txMode}\n           autoFreqPauseMode:{autoFreqPauseMode} consecCqCount:{consecCqCount} consecTimeoutCount:{consecTimeoutCount} holdCheckBox.Checked:{ctrl.holdCheckBox.Checked}\n           {CallQueueString()}";
        }

        private void DebugOutputStatus()
        {
            DebugOutput($"(update)   {CurrentStatus()}");
        }

        //detect supported mode
        private void CheckModeSupported()
        {
            string s = "";
            modeSupported = supportedModes.Contains(mode) && specOp == 0;
            DebugOutput($"{Time()} CheckModeSupported, mode:{mode} curVerBld:{curVerBld} modeSupported:{modeSupported}");

            if (!modeSupported)
            {
                if (specOp != 0) s = "Special ";
                DebugOutput($"{spacer}{s}mode:{mode} specOp:{specOp}");
                failReason = $"{s}{mode} mode not supported";
                if (txMode == TxModes.LISTEN)
                { 
                    if (opMode == OpModes.ACTIVE) ctrl.cqModeButton_Click(null, null);       //re-enable WSJT-X "Tx even/1st" control
                }
            }

            if (mode == "MSK144" && modeSupported)
            {
                ctrl.freqCheckBox.Enabled = false;
                ctrl.freqCheckBox.Checked = false;
                ctrl.optimizeCheckBox.Enabled = false;
                ctrl.optimizeCheckBox.Checked = false;
                ctrl.holdCheckBox.Checked = false;
            }
            else
            {
                ctrl.freqCheckBox.Enabled = true;
                ctrl.optimizeCheckBox.Enabled = !ctrl.holdCheckBox.Checked;
            }

            ShowStatus();
        }

        private string DatagramString(byte[] datagram)
        {
            var sb = new StringBuilder();
            string delim = "";
            for (int i = 0; i < datagram.Length; i++)
            {
                sb.Append(delim);
                sb.Append(datagram[i].ToString("X2"));
                delim = " ";
            }
            return sb.ToString();
        }

        public void IncrCallPriority(int idx)
        {
            string call = null;
            EnqueueDecodeMessage msg = null;

            if (idx >= 0)
            {
                var callArray = callQueue.ToArray();
                call = callArray[idx];
                callDict.TryGetValue(call, out msg);
            }
            else
            {
                if (idx < 0 && callInProg != null && replyDecode != null && replyDecode.DeCall() == callInProg)
                {
                    call = callInProg;
                    msg = replyDecode;
                }
                else
                {
                    return;
                }
            }

            if (msg.Priority > (int)CallPriority.NEW_COUNTRY)
            {
                msg.Priority--;
                if (callQueue.Contains(call))
                {
                    RemoveCall(call);
                    AddCall(call, msg);
                }
                if (replyDecode != null && replyDecode.DeCall() == call)
                {
                    replyDecode = msg;
                    ctrl.holdCheckBox.Checked = replyDecode.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND;
                }

                UpdatePriority(call, msg.Priority);

                DebugOutput($"{Time()} IncrCallPriority, call:{call} new priority:{msg.Priority}");
                DebugOutputStatus();
                UpdateCallInProg();
                UpdateDebug();
            }
        }

        public void DecrCallPriority(int idx)
        {
            string call = null;
            EnqueueDecodeMessage msg = null;

            if (idx >= 0)
            {
                var callArray = callQueue.ToArray();
                call = callArray[idx];
                callDict.TryGetValue(call, out msg);
            }
            else
            {
                if (idx < 0 && callInProg != null && replyDecode != null && replyDecode.DeCall() == callInProg)
                {
                    call = callInProg;
                    msg = replyDecode;
                }
                else
                {
                    return;
                }
            }

            if (msg.Priority < (int)CallPriority.DEFAULT)
            {
                msg.Priority++;
                if (callQueue.Contains(call))
                {
                    RemoveCall(call);
                    AddCall(call, msg);
                }
                if (replyDecode != null && replyDecode.DeCall() == call)
                {
                    replyDecode = msg;
                    ctrl.holdCheckBox.Checked = replyDecode.Priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND;
                }

                UpdatePriority(call, msg.Priority);

                DebugOutput($"{Time()} IncrCallPriority, call:{call} new priority:{msg.Priority}");
                DebugOutputStatus();
                UpdateCallInProg();
                UpdateDebug();
            }
        }

        //stop responding to CQs from this call
        private void StopCqCall(string call)
        {
            int prevCqs;
            if (cqCallDict.TryGetValue(call, out prevCqs))
            {
                cqCallDict.Remove(call);
            }
            cqCallDict.Add(call, 9999);
        }

        public void NextCall(bool confirm)
        {
            if (paused)
            {
                ctrl.ShowMsg("'Enable Tx' in WSJT-X first", true);
                return;
            }   

            if (txTimeout)
            {
                ctrl.ShowMsg("Processing next call...", true);
                return;
            }

            DebugOutput($"{Time()} NextCall");
            dialogTimer2.Tag = confirm;
            dialogTimer2.Start();
        }

        private void dialogTimer2_Tick(object sender, EventArgs e)
        {
            dialogTimer2.Stop();
            if (paused) return;
            bool confirm = (bool)dialogTimer2.Tag;

            if (callQueue.Count == 0 && callInProg != null)
            {
                if (!confirm || Confirm($"Cancel current call ({callInProg})?") == DialogResult.Yes)
                {
                    if (paused) return;
                    xmitCycleCount = 0;
                    ctrl.holdCheckBox.Checked = false;
                    DebugOutput($"{Time()} dialogTimer2_Tick, cancel current call {callInProg}, txTimeout:{txTimeout} xmitCycleCount:{xmitCycleCount} holdCheckBox.Checked{ctrl.holdCheckBox.Checked}");

                    if (txMode == TxModes.CALL_CQ)
                    {
                        HaltTx();
                        Thread.Sleep(500);
                        SetupCq(true);
                    }
                    else
                    {
                        if (transmitting) HaltTx();
                        txTimeout = true;
                        CheckNextXmit();
                    }

                    UpdateDebug();
                    if (!confirm) ctrl.ShowMsg($"Cancelled current call {callInProg}", true);
                    return;
                }
                return;
            }

            if (callQueue.Count > 0)
            {
                EnqueueDecodeMessage msg = new EnqueueDecodeMessage();
                string call = PeekNextCall(out msg);
                if (!confirm || Confirm($"Reply to {call}?") == DialogResult.Yes)
                {
                    if (paused) return;
                    if (!callQueue.Contains(call)) return;          //call has already been removed or processed

                    if (transmitting && (txMode == TxModes.LISTEN))
                    {
                        EnqueueDecodeMessage dmsg = new EnqueueDecodeMessage();
                        DateTime dtNow = DateTime.Now;
                        PeekNextCall(out dmsg);
                        bool evenCall = IsEvenPeriod((dmsg.SinceMidnight.Minutes * 60) + dmsg.SinceMidnight.Seconds);
                        bool evenPeriod = IsEvenPeriod((dtNow.Minute * 60) + dtNow.Second);       //listen mode can xmit on either period depending on current time
                        if (evenCall == evenPeriod)     //reply is in same time period from msg
                        {
                            HaltTx();
                        }
                    }

                    txTimeout = true;
                    xmitCycleCount = 0;
                    ctrl.holdCheckBox.Checked = false;
                    DebugOutput($"{Time()} dialogTimer2_Tick, reply next call {call}, txTimeout:{txTimeout} holdCheckBox.Checked{ctrl.holdCheckBox.Checked}");
                    CheckNextXmit();
                    UpdateDebug();
                    if (!confirm) ctrl.ShowMsg($"Replying to next call {call}", true);
                    return;
                }
                return;
            }
        }

        public void EditCallQueue(int idx)
        {
            if (idx < 0) return;
            DebugOutput($"{Time()} EditCallQueue");
            dialogTimer3.Tag = idx;
            dialogTimer3.Start();
        }

        private void dialogTimer3_Tick(object sender, EventArgs e)
        {
            dialogTimer3.Stop();
            int idx = (int)dialogTimer3.Tag;
            var callArray = callQueue.ToArray();
            string call = callArray[idx];

            if (callQueue.Contains(call))
            {
                if (Confirm($"Delete {call}?") == DialogResult.Yes)
                {
                    DebugOutput($"{Time()} dialogTimer3_Tick");
                    RemoveCall(call);
                    DebugOutputStatus();
                    UpdateDebug();
                }
            }
        }

        //must be actual DX (relative to current continent) to match "DX"
        private bool IsDirectedAlert(string dirTo, bool isDx)
        {
            if (dirTo == null) return false;

            string s = ctrl.alertTextBox.Text.ToUpper();
            string[] a = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string elem in a)
            {
                if (elem == dirTo && (elem != "DX" || isDx)) return true;
            }
            return false;
        }

        //return true if received a R-XX or R+XX from the specified call
        private bool RecdRogerReport(string call)
        {
            if (call == null) return false;
            List<EnqueueDecodeMessage> msgList;
            if (!allCallDict.TryGetValue(call, out msgList)) return false;          //no previous call(s) from DX station
            //DebugOutput($"{spacer}recd previous call(s)");
            return msgList.Find(RogerReport) != null;        //the DX station never sent R-XX or R+XX
        }

        //return true if received a -XX or +XX from the specified call
        private bool RecdReport(string call)
        {
            if (call == null) return false;
            List<EnqueueDecodeMessage> msgList;
            if (!allCallDict.TryGetValue(call, out msgList)) return false;          //no previous call(s) from DX station
            //DebugOutput($"{spacer}recd previous call(s)");
            return msgList.Find(Report) != null;        //the DX station never sent -XX or +XX
        }

        //return true if received a grid from specified call
        private bool RecdReply(string call)
        {
            if (call == null) return false;
            List<EnqueueDecodeMessage> msgList;
            if (!allCallDict.TryGetValue(call, out msgList)) return false;          //no previous call(s) from DX station
            //DebugOutput($"{spacer}recd previous call(s)");
            return msgList.Find(Reply) != null;        //the DX station never sent grid
        }

        private bool RecdSignoff(string call)
        {
            if (call == null) return false;
            List<EnqueueDecodeMessage> msgList;
            if (!allCallDict.TryGetValue(call, out msgList)) return false;          //no previous call(s) from DX station
            //DebugOutput($"{spacer}recd previous call(s)");
            return msgList.Find(Signoff) != null;        //the DX station never sent 73 or RR73
        }

        private EnqueueDecodeMessage ReplyMsg(string call)
        {
            if (call == null) return null;
            List<EnqueueDecodeMessage> msgList;
            if (!allCallDict.TryGetValue(call, out msgList)) return null;          //no previous call(s) from DX station
            //DebugOutput($"{spacer}recd previous call(s)");
            return msgList.Find(Reply);
        }

        private EnqueueDecodeMessage CqMsg(string call)
        {
            if (call == null) return null;
            List<EnqueueDecodeMessage> msgList;
            if (!allCallDict.TryGetValue(call, out msgList)) return null;          //no previous call(s) from DX station
            //DebugOutput($"{spacer}recd previous call(s)");
            return msgList.Find(CQ);
        }

        private bool RecdAnyMsg(string call)
        {
            if (call == null) return false;
            return RecdReply(call) || RecdReport(call) || RecdRogerReport(call);
        }

        private bool SentAnyMsg(string call)
        {
            if (call == null) return false;
            return sentCallList.Contains(call);
        }

        //add CQ (for grid info), 
        //or report (only to myCall), or rogerReport (only to myCall)
        //but not 73 or RR73
        private void AddAllCallDict(string call, EnqueueDecodeMessage emsg)
        {
            if ((!emsg.IsCallTo(myCall) && !emsg.IsCQ()) || emsg.Is73orRR73()) return;

            //  is CQ          already added a CQ from call
            if (emsg.IsCQ() && CqMsg(call) != null) return;        //don't duplicate CQs

            List<EnqueueDecodeMessage> vlist;
            //create new List for call if nothing entered yet into the Dictionary
            if (!allCallDict.TryGetValue(call, out vlist)) allCallDict.Add(call, vlist = new List<EnqueueDecodeMessage>());
            vlist.Add(emsg);        //messages from call are in order rec'd, will be duplicate msg types
            DebugOutput($"{Time()} AddAllCallDict, call:{call} msg.Message:{emsg.Message}");
        }

        //remove old rec'd calls
        private bool TrimAllCallDict()
        {
            bool removed = false;
            var keys = new List<string>();
            var dtNow = DateTime.UtcNow;
            var ts = new TimeSpan(0, maxDecodeAgeMinutes, 0);

            foreach (var entry in allCallDict)
            {
                var list = entry.Value;
                if (entry.Key != callInProg && list.Count > 0)
                {
                    var decode = list[0];           //just check the oldest entry
                    //DebugOutput($"{spacer}entry.Key:{entry.Key} dtNow:{dtNow} decode.RxDate:{decode.RxDate} decode.SinceMidnight:{decode.SinceMidnight} sum:{decode.RxDate + decode.SinceMidnight}");
                    if ((dtNow - (decode.RxDate + decode.SinceMidnight)) > ts)  //entry is older than wanted
                    {
                        keys.Add(entry.Key);        //collect keys to delete
                        //DebugOutput($"{spacer}{entry.Key} is expired");
                    }
                }
            }

            //delete keys to old decodes and sent reports
            foreach (string key in keys)
            {
                if (!callQueue.Contains(key))
                {
                    RemoveAllCall(key);
                    removed = true;
                }
            }

            if (removed) DebugOutput($"{spacer}TrimAllCallDict: expired calls removed from allCallDict and/or sentReportList");
            return removed;
        }

        private bool TrimCallQueue()
        {
            bool removed = false;
            var keys = new List<string>();
            var dtNow = DateTime.UtcNow;
            var ts = new TimeSpan(0, maxDecodeAgeMinutes, 0);

            foreach (var entry in callDict)
            {   //                              old call                                                          not a high priority                                             not manually selected
                if (entry.Key != callInProg && (dtNow - (entry.Value.RxDate + entry.Value.SinceMidnight)) > ts && entry.Value.Priority > (int)CallPriority.NEW_COUNTRY_ON_BAND && entry.Value.AutoGen)  //entry is older than wanted
                {
                    keys.Add(entry.Key);        //collect keys to delete
                }
            }

            //delete keys to old decodes
            foreach (string key in keys)
            {
                RemoveCall(key);
                removed = true;
            }

            if (removed) DebugOutput($"{spacer}TrimCallQueue: expired calls removed from callQueue and callDict");
            return removed;
        }


        private void SetCallInProg(string call)
        {
            ctrl.holdCheckBox.Enabled = (call != null);
            DebugOutput($"{spacer}SetCallInProg: callInProg:'{CallPriorityString(call)}' (was '{CallPriorityString(callInProg)}') holdCheckBox.Enabled:{ctrl.holdCheckBox.Enabled}");
            if (call == null || call != callInProg)
            {
                ctrl.holdCheckBox.Checked = false;
            }
            callInProg = call;
            UpdateCallInProg();
        }

        private void EnableTx()
        {
            try
            {
                if (emsg == null || udpClient2 == null)
                {
                    DebugOutput($"{Time()} EnableTx skipped, udpClient2:{udpClient2} emsg:{emsg}");
                    return;
                }

                DebugOutput($"{Time()} EnableTx, txEnabled:{txEnabled} processDecodeTimer.Enabled:{processDecodeTimer.Enabled}");
                emsg.NewTxMsgIdx = 9;
                emsg.Param0 = true;       //WSJT-X Enable Tx button state 
                emsg.GenMsg = $"";         //ignored
                emsg.CmdCheck = "";         //ignored
                ba = emsg.GetBytes();
                udpClient2.Send(ba, ba.Length);
                DebugOutput($"{Time()} >>>>>Sent 'Enable Tx' cmd:9\n{emsg}");
                txEnabled = true;
                wsjtxTxEnableButton = true;
                DebugOutput($"{spacer}txEnabled:{txEnabled}");
            }
            catch
            {
                DebugOutput($"{Time()} 'EnableTx' failed, txEnabled:{txEnabled}");        //only happens during closing
            }
        
            UpdateDebug();
        }

        private void DisableTx(bool buttonState)
        {
            DebugOutput($"{Time()} DisableTx, txEnabled:{txEnabled} processDecodeTimer.Enabled:{processDecodeTimer.Enabled}");
            StopDecodeTimers();

            try
            {
                if (emsg == null || udpClient2 == null)
                {
                    DebugOutput($"{Time()} DisableTx skipped, udpClient2:{udpClient2} emsg:{emsg}");
                    return;
                }

                emsg.NewTxMsgIdx = 8;
                emsg.Param0 = buttonState;    //set WSJT-X Enable Tx button state
                emsg.GenMsg = $"";         //ignored
                emsg.CmdCheck = "";         //ignored
                ba = emsg.GetBytes();
                udpClient2.Send(ba, ba.Length);
                DebugOutput($"{Time()} >>>>>Sent 'Disable Tx' cmd:8\n{emsg}");
                txEnabled = false;
                wsjtxTxEnableButton = buttonState;
                DebugOutput($"{spacer}txEnabled:{txEnabled}");
            }
            catch
            {
                DebugOutput($"{Time()} 'DisableTx' failed, txEnabled:{txEnabled}");        //only happens during closing
            }

            UpdateDebug();
        }

        private void EnableMonitoring()
        {
            emsg.NewTxMsgIdx = 11;
            emsg.GenMsg = $"";         //ignored
            emsg.CmdCheck = "";         //ignored
            ba = emsg.GetBytes();
            udpClient2.Send(ba, ba.Length);
            DebugOutput($"{Time()} >>>>>Sent 'Enable Monitoring' cmd:11\n{emsg}");
        }

        private void SetListenMode()
        {
            if (udpClient2 == null)
            {
                DebugOutput($"{Time()} SetListenMode skipped, udpClient2:{udpClient2}");
                return;
            }

            emsg.NewTxMsgIdx = 14;
            emsg.Param0 = (txMode == TxModes.LISTEN);
            emsg.GenMsg = $"";         //ignored
            emsg.CmdCheck = "";         //ignored
            ba = emsg.GetBytes();
            udpClient2.Send(ba, ba.Length);
            DebugOutput($"{Time()} >>>>>Sent 'Set listen mode' cmd:14\n{emsg}");
        }

        private void EnableDebugLog()
        {
            if (!debug) return;

            emsg.NewTxMsgIdx = 5;
            emsg.GenMsg = $"";         //ignored
            emsg.CmdCheck = "";         //ignored
            ba = emsg.GetBytes();
            udpClient2.Send(ba, ba.Length);
            DebugOutput($"{Time()} >>>>>Sent 'Enable Debug' cmd:5\n{emsg}");
        }

        public void HaltTx()
        {
            StopDecodeTimers();
            if (udpClient2 != null)
            {
                emsg.NewTxMsgIdx = 12;
                emsg.GenMsg = $"";         //ignored
                emsg.CmdCheck = "";         //ignored
                ba = emsg.GetBytes();
                udpClient2.Send(ba, ba.Length);
                DebugOutput($"{Time()} >>>>>Sent 'HaltTx' cmd:12\n{emsg}");
                txEnabled = false;
                wsjtxTxEnableButton = false;
            }
            else
            {
                DebugOutput($"{Time()} HaltTx skipped, udpClient2:{udpClient2}");
                return;
            }
        }

        public void UpdateModeSelection()
        {
            ctrl.cqModeButton.Checked = txMode == TxModes.CALL_CQ;
            ctrl.listenModeButton.Checked = txMode == TxModes.LISTEN;
        }

        private void UpdateRR73()
        {
            if (mode == "FT4")
            {
                ctrl.useRR73CheckBox.Checked = true;
                ctrl.useRR73CheckBox.Enabled = false;
            }
            else
            {
                ctrl.useRR73CheckBox.Checked = useRR73;
                ctrl.useRR73CheckBox.Enabled = true;

                if (mode == "") ctrl.WsjtxSettingConfirmed();
            }
        }

        private void UpdateTxTimeEnable()
        {
            bool enabled = (opMode == OpModes.ACTIVE);
            ctrl.timedCheckBox.Enabled = enabled;
            enabled = enabled && !ctrl.timedCheckBox.Checked;
            ctrl.stopTextBox.Enabled = enabled;
            ctrl.timeLabel.Enabled = enabled;
            ctrl.startTextBox.Enabled = enabled;
            ctrl.timeLabel2.Enabled = enabled;
            ctrl.atLabel.Enabled = enabled;
            ctrl.modeComboBox.Enabled = enabled;
            ctrl.startLabel.Enabled = enabled;
            ctrl.stopLabel.Enabled = enabled;
        }

        private void ProcessPostDecodeTimerTick(object sender, EventArgs e)
        {
            DecodesCompleted();
        }

        private void ProcessDecodeTimerTick(object sender, EventArgs e)
        {
            processDecodeTimer.Stop();
            DebugOutput($"\n{Time()} processDecodeTimer stop");
            ProcessDecodes();
        }
        private void ProcessDecodeTimer2Tick(object sender, EventArgs e)
        {
            processDecodeTimer2.Stop();
            DebugOutput($"\n{Time()} processDecodeTimer2 tick: stop, transmitting:{transmitting}");
            //                    "call CQ" mode and a late 73 may have caused WSJT-X to start calling "CQ" when it should be "CQ DX" or other directed CQ
            if (!transmitting || (txMode == TxModes.CALL_CQ && (qsoState == WsjtxMessage.QsoStates.CALLING || qsoState == WsjtxMessage.QsoStates.SIGNOFF))) ProcessDecodes();
        }

        private void ProcessDblClick(string call, bool isNewCountryOnBand, bool isNewCountry)
        {
            //marker1
            //manual operation started
            DebugOutput($"{Time()} ProcessDblClick, call:{call} isNewCountryOnBand:{isNewCountryOnBand}isNewCountry:{isNewCountry}");

            if (call == null || call == myCall) return;

            DisableAutoFreqPause();
            txTimeout = false;          //cancel timeout or settings change, which would start auto CQing
            tCall = null;
            xmitCycleCount = 0;         //restart timeout for the new call

            int priority = (int)CallPriority.MANUAL_CQ;
            if (advanced)
            {
                if (isNewCountryOnBand) priority = (int)CallPriority.NEW_COUNTRY_ON_BAND;
                if (isNewCountry) priority = (int)CallPriority.NEW_COUNTRY;
            }

            replyCmd = $"CQ {call}";      //fake a new reply cmd, last reply cmd sent is no longer in effect
            replyDecode = new EnqueueDecodeMessage();

            replyDecode.Mode = rawMode;
            replyDecode.SchemaVersion = WsjtxMessage.NegotiatedSchemaVersion;
            replyDecode.New = true;
            replyDecode.OffAir = false;
            replyDecode.UseStdReply = true;   //override skipGrid since no SNR available
            replyDecode.Id = WsjtxMessage.UniqueId;
            replyDecode.Snr = noSnrAvail;      //used as a flag to prevent signal report, use grid reply instead
            replyDecode.DeltaTime = 0.0;       //not used
            replyDecode.DeltaFrequency = (int)defaultAudioOffset; //real offset unknown
            replyDecode.Message = replyCmd; //fake a new reply cmd
            replyDecode.SinceMidnight = latestDecodeTime + new TimeSpan(0, 0, 0, 0, (int)trPeriod);     //dummy value, NOT ACCURATE
            replyDecode.RxDate = latestDecodeDate;
            replyDecode.Priority = priority;

            //if (!txEnabled) EnableTx();         //tx often disabled when in LISTEN mode
            DebugOutput($"{spacer}call:{CallPriorityString(call)} replyDecode:{replyDecode}");
            SetCallInProg(call);
            RemoveCall(call);
            ctrl.ShowMsg("Manual call selection", false);
            UpdateDebug();

            ctrl.holdCheckBox.Checked = priority <= (int)CallPriority.NEW_COUNTRY_ON_BAND;

            DebugOutput($"{spacer}new DX call selected manually, txTimeout:{txTimeout} xmitCycleCount:{xmitCycleCount} replyCmd:'{replyCmd}'");
            ShowStatus();
            DebugOutputStatus();
        }

        //the last decode pass has completed, ready to detect first decode pass
        private void DecodesCompleted()
        {
            postDecodeTimer.Stop();
            DebugOutput($"\n{Time()} Last decode completed, postDecodeTimer.Enabled:{postDecodeTimer.Enabled} firstDecodePass:{firstDecodePass} NegoState:{WsjtxMessage.NegoState}");
            firstDecodePass = true;
            DebugOutput($"{spacer}firstDecodePass:{firstDecodePass}");

            if (skipFirstDecodeSeries)
            {
                skipFirstDecodeSeries = false;
                DebugOutput($"{spacer}skipFirstDecodeSeries:{skipFirstDecodeSeries}");
            }
            else
            {
                //final calculation of best offset
                if (CalcBestOffset(audioOffsets, period, true))       //calc for period when decodes started
                {
                    ctrl.freqCheckBox.Text = "Select best Tx frequency";
                    ctrl.freqCheckBox.ForeColor = Color.Black;
                }
            }

            if (WsjtxMessage.NegoState != WsjtxMessage.NegoStates.RECD) 
                return;

            if (ctrl.freqCheckBox.Checked)
            {
                if (!transmitting)
                {
                    if (!CheckActive())
                    {
                        //set/show frequency offset for Tx period
                        emsg.NewTxMsgIdx = 10;
                        emsg.GenMsg = $"";          //no effect
                        emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
                        emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
                        emsg.CmdCheck = "";         //ignored
                        emsg.Offset = AudioOffsetFromTxPeriod();
                        ba = emsg.GetBytes();
                        udpClient2.Send(ba, ba.Length);
                        DebugOutput($"{Time()} >>>>>Sent 'Opt Req' cmd:10\n{emsg}");
                        if (settingChanged)
                        {
                            ctrl.WsjtxSettingConfirmed();
                            settingChanged = false;
                        }
                    }
                }
            }
            UpdateDebug();

            if (TrimCallQueue())
            {
                DebugOutput(CallQueueString());
            }

            if (TrimAllCallDict())
            {
                DebugOutput(AllCallDictString());
            }
        }

        private void HeartbeatNotRecd(object sender, EventArgs e)
        {
            //no heartbeat from WSJT-X, re-init communication
            heartbeatRecdTimer.Stop();
            DebugOutput($"{Time()} heartbeatRecdTimer timed out");
            if (WsjtxMessage.NegoState == WsjtxMessage.NegoStates.RECD)
            {
                ctrl.ShowMsg("WSJT-X disconnected", false);
                Play("dive.wav");
            }
            else
            {
                ctrl.ShowMsg("WSJT-X not responding", true);
            }
            ResetNego();
            CloseAllUdp();          //usually not needed
        }

        private void cmdCheckTimer_Tick(object sender, EventArgs e)
        {
            CmdCheckDialog();
        }

        private void dialogTimer_Tick(object sender, EventArgs e)
        {
            dialogTimer.Stop();
            CheckFirstRun();
        }

        private void reminderTimerTick(object sender, EventArgs e)
        {
            reminderTimer.Stop();
            if (!paused && txMode == TxModes.LISTEN && !ctrl.replyDxCheckBox.Checked && !ctrl.replyLocalCheckBox.Checked && !ctrl.replyDirCqCheckBox.Checked && !ctrl.replyNewDxccCheckBox.Checked)
            {
                ctrl.ShowMsg($"Select calls manually in WSJT-X (alt/dbl-click)", false);
            }
        }

        private void CheckCallQueuePeriod(bool newTxFirst)
        {
            bool removed = false;
            var calls = new List<string>();

            foreach (var entry in callDict)
            {
                var decode = entry.Value;
                if (IsEvenPeriod((decode.SinceMidnight.Minutes * 60) + decode.SinceMidnight.Seconds) == newTxFirst)  //entry is wrong time period for new txFirst
                {
                    calls.Add(entry.Key);        //collect keys to delete
                }
            }

            //delete from callQueue
            foreach (string call in calls)
            {
                RemoveCall(call);
                removed = true;
            }

            if (removed) DebugOutput($"{spacer}CheckCallQueuePeriod: calls removed: {CallQueueString()} {CallDictString()}");
        }

        private bool IsSameMessage(string tx, string lastTx)
        {
            if (tx == lastTx) return true;
            if (WsjtxMessage.ToCall(tx) != WsjtxMessage.ToCall(lastTx)) return false;
            if (WsjtxMessage.IsReport(tx) && WsjtxMessage.IsReport(lastTx)) return true;
            if (WsjtxMessage.IsRogerReport(tx) && WsjtxMessage.IsRogerReport(lastTx)) return true;
            return false;
        }

        //set log file open/closed state
        //return new diagnostic log file state (true = open)
        private bool SetLogFileState(bool enable)
        {
            if (enable)         //want log file opened for write
            {
                if (logSw == null)     //log not already open
                {
                    try
                    {
                        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                        logSw = File.AppendText($"{path}\\log_{DateTime.Now.Date.ToShortDateString().Replace('/', '-')}.txt");      //local time
                        logSw.AutoFlush = true;
                        logSw.WriteLine($"\n\n{Time()} Opened log");
                    }
                    catch (Exception err)
                    {
                        err.ToString();
                        logSw = null;
                        return false;       //log file state = closed
                    }
                }
                return true;       //log file state = open
            }
            else    //want log file flushed and closed
            {
                if (logSw != null)
                {
                    logSw.WriteLine($"{Time()} Closing log...");
                    logSw.Flush();
                    logSw.Close();
                    logSw = null;
                }
                return false;       //log file state = closed
            }
        }

        private void ReadPotaLogDict()
        {
            List<string> updList = new List<string>();
            string pathFileNameExt = $"{path}\\pota.txt";
            StreamReader potaSr = null;
            potaSw = null;
            potaLogDict.Clear();

            try
            {
                if (File.Exists(pathFileNameExt))
                {
                    string line = null;
                    string today = DateTime.Now.ToShortDateString();        //local time
                    potaSr = File.OpenText(pathFileNameExt);
                    DebugOutput($"{spacer}POTA log opened for read");

                    while ((line = potaSr.ReadLine()) != null)
                    {
                        string[] parts = line.Split(new char[] { ',' });   //call,date,band,mode
                        if (parts.Length == 4 && parts[1] == today)
                        {                       //date     band       mode
                            string potaInfo = $"{parts[1]},{parts[2]},{parts[3]}";
                            List<string> curList;
                            //                          call
                            if (potaLogDict.TryGetValue(parts[0], out curList))
                            {
                                if (!curList.Contains(potaInfo)) curList.Add(potaInfo);
                            }
                            else
                            {
                                List<string> newList = new List<string>();
                                newList.Add(potaInfo);
                                //              call
                                potaLogDict.Add(parts[0], newList);
                            }

                            updList.Add(line);
                        }
                    }
                    potaSr.Close();
                }
            }
            catch (Exception err)
            {
                DebugOutput($"{spacer}POTA log open/read failed: {err.ToString()}");
                if (potaSr != null) potaSr.Close();
                return;
            }

            //open, re-write updated file; leave file open if no error
            try
            {
                if (File.Exists(pathFileNameExt)) File.Delete(pathFileNameExt);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                potaSw = File.AppendText(pathFileNameExt);
                potaSw.AutoFlush = true;
                DebugOutput($"{spacer}POTA log opened for write");

                foreach (string line in updList)
                {
                    potaSw.WriteLine(line);
                }
            }
            catch (Exception err)
            {
                DebugOutput($"{spacer}POTA log open/rewrite failed: {err.ToString()}");
                potaSw = null;
            }
            DebugOutput($"{PotaLogDictString()}");
        }

        private void AddPotaLogDict(string potaCall, DateTime potaDtLocal, string potaBand, string potaMode)     //UTC
        {
            bool updateLog = false;

            string potaInfo = $"{potaDtLocal.Date.ToShortDateString()},{potaBand},{potaMode}";
            DebugOutput($"{spacer}AddPotaLogDict, potaInfo:{potaInfo}");
            DebugOutput($"{PotaLogDictString()}");
            List<string> curList;
            if (potaLogDict.TryGetValue(potaCall, out curList))
            {
                if (!curList.Contains(potaInfo))
                {
                    curList.Add(potaInfo);
                    updateLog = true;
                }
            }
            else
            {
                List<string> newList = new List<string>();
                newList.Add(potaInfo);
                potaLogDict.Add(potaCall, newList);
                updateLog = true;
            }

            if (potaSw != null && updateLog)
            {
                potaSw.WriteLine($"{potaCall},{potaInfo}");
                DebugOutput($"{PotaLogDictString()}");
            }
        }

        private bool CalcBestOffset(List<int> offsetList, Periods decodePeriod, bool clearList)
        {
            DebugOutput($"{Time()} CalcBestOffset, decodePeriod:{decodePeriod} clearList:{clearList} offsetList.Count:{offsetList.Count()} skipFirstDecodeSeries:{skipFirstDecodeSeries}");

            if (period == Periods.UNK)
            {
                oddOffset = 0;
                evenOffset = 0;
                return false;
            }

            if (skipFirstDecodeSeries)
            {
                return false;
            }

            int bestOffset = 0;
            int maxInterval = 0;

            //set limits
            offsetList.Add(offsetLoLimit);
            offsetList.Add(offsetHiLimit);

            offsetList.Sort();
            int[] offsets = offsetList.ToArray();

            for (int i = 0; i < offsets.Length - 1; i++)
            {
                if (offsets[i + 1] - offsets[i] > maxInterval)
                {
                    maxInterval = offsets[i + 1] - offsets[i];
                    bestOffset = (offsets[i + 1] + offsets[i]) / 2;
                }
            }

            if (decodePeriod == Periods.EVEN)
            {
                evenOffset = bestOffset;
            }
            else
            {
                oddOffset = bestOffset;
            }

            if (clearList) offsetList.Clear();

            DebugOutput($"{spacer}evenOffset:{evenOffset} oddOffset:{oddOffset}");

            return oddOffset > 0 && evenOffset > 0;
        }

        private UInt32 AudioOffsetFromMsg(EnqueueDecodeMessage msg)        //msg is a reply msg, so tx msg will be opposite time period
        {
            if (msg == null || !ctrl.freqCheckBox.Checked) return 0;

            if (IsEvenPeriod((msg.SinceMidnight.Minutes * 60) + msg.SinceMidnight.Seconds))
            {
                return (UInt32)oddOffset;
            }
            else
            {
                return (UInt32)evenOffset;
            }
        }

        private UInt32 AudioOffsetFromTxPeriod()
        {
            if (period == Periods.UNK || !ctrl.freqCheckBox.Checked) 
                return 0;

            if (txFirst)
            {
                return (UInt32)evenOffset;
            }
            else
            {
                return (UInt32)oddOffset;
            }
        }

        private UInt32 CurAudioOffset()
        {
            if (period == Periods.UNK || !ctrl.freqCheckBox.Checked) return prevOffset;

            if (txFirst)
            {
                if (evenOffset > 0) return (UInt32)evenOffset;
            }
            else
            {
                if (oddOffset > 0) return (UInt32)oddOffset;
            }
            return prevOffset;
        }

        private DateTime ScheduledOffDateTime()
        {
            DateTime dtNow = DateTime.Now;              //local time
            string stop = ctrl.stopTextBox.Text.Trim();
            DateTime dtStop = new DateTime(
                dtNow.Year, 
                dtNow.Month, 
                dtNow.Day, 
                Convert.ToInt32(stop.Substring(0, 2)), 
                Convert.ToInt32(stop.Substring(2, 2)), 
                0);

            return dtStop;
        }

        private DateTime ScheduledOnDateTime()
        {
            DateTime dtNow = DateTime.Now;              //local time
            string start = ctrl.startTextBox.Text.Trim();
            DateTime dtStart = new DateTime(
                dtNow.Year,
                dtNow.Month,
                dtNow.Day,
                Convert.ToInt32(start.Substring(0, 2)),
                Convert.ToInt32(start.Substring(2, 2)),
                0);

            return dtStart;
        }

        private int CalcTimerAdj()
        {
            return (mode == "FT8" ? 150 /*300*/ : (mode == "FT4" ? 150 /*300*/ : (mode == "FST4" ? 750 : 300)));      //msec
        }

        private void UpdateMaxTxRepeat()
        {
            if (!ctrl.optimizeCheckBox.Checked)
            {
                maxTxRepeat = (int)ctrl.timeoutNumUpDown.Value;
            }
            else
            {
                int max = 1;
                if (callQueue.Count <= 1) max = (int)ctrl.timeoutNumUpDown.Value;
                if (callQueue.Count == 2) max = 4;
                if (callQueue.Count == 3) max = 3;
                if (callQueue.Count >= 4) max = 2;
                if (callQueue.Count >= 6 && txMode == TxModes.CALL_CQ && !paused) max = 1;
                maxTxRepeat = Math.Min(max, (int)ctrl.timeoutNumUpDown.Value);
            }

            UpdateMaxPrevCqs();
            UpdateMaxAutoGenEnqueue();
        }

        //if low number of repeated msgs before timeout
        //allow CQs to be re-queued more often     
        private void UpdateMaxPrevCqs()
        {
            maxPrevCqs = 2;
            if (maxTxRepeat == 3) maxPrevCqs = 3;
            if (maxTxRepeat == 2) maxPrevCqs = 4;
            if (maxTxRepeat == 1) maxPrevCqs = 5;
            maxPrevPotaCqs = Math.Min((int)(maxPrevCqs * 1.5), 8);
        }

        private void UpdateMaxAutoGenEnqueue()
        {
            maxAutoGenEnqueue = 4;
            if (maxTxRepeat == 3) maxAutoGenEnqueue = 5;
            if (maxTxRepeat == 2) maxAutoGenEnqueue = 6;
            if (maxTxRepeat == 1) maxAutoGenEnqueue = 8;
        }

        private void UpdateHoldTxRepeat()
        {
            holdTxRepeat = holdMaxTxRepeat;
            if (replyDecode != null)
            {
                if (replyDecode.Priority == 1)
                {
                    return;
                }
                else if (replyDecode.Priority == 2)
                {
                    holdTxRepeat = holdMaxTxRepeatNewOnBand;
                    if (callQueue.Count >= 12) holdTxRepeat = holdMaxTxRepeatNewOnBand / 4;
                    if (callQueue.Count >= 8) holdTxRepeat = holdMaxTxRepeatNewOnBand / 3;
                    if (callQueue.Count >= 4) holdTxRepeat = holdMaxTxRepeatNewOnBand / 2;
                }
            }
        }

        private void DisableAutoFreqPause()
        {
            DebugOutput($"{Time()} DisableAutoFreqPause autoFreqPauseMode:{autoFreqPauseMode} consecCqCount:{consecCqCount} consecTimeoutCount:{consecTimeoutCount}");
            autoFreqPauseMode = autoFreqPauseModes.DISABLED;
            consecCqCount = 0;
            consecTimeoutCount = 0;
            consecTxCount = 0;
            UpdateCallInProg();
            UpdateDebug();
            DebugOutput($"{spacer}autoFreqPauseMode:{autoFreqPauseMode} consecCqCount:{consecCqCount} consecTimeoutCount:{consecTimeoutCount}");
        }

        private string CallPriorityString(string call)
        {
            if (call == null) return "";

            return $"{call}:{Priority(call)}";
        }

        //for the specified call, return the priority, or default if not found
        //check allCallDict and replyDecode
        private int Priority(string call)
        {
            int priority = (int)CallPriority.DEFAULT;
            if (call == null || call == "CQ") return priority;

            EnqueueDecodeMessage msg = null;
            List<EnqueueDecodeMessage> msgList;
            if (allCallDict.TryGetValue(call, out msgList))
            {
                if (msgList.Count > 0)
                {
                    msg = msgList.Last();       //list is in chronological order, latest at end
                    priority = msg.Priority;
                }
            }
            else
            {
                if (replyDecode != null && callInProg != null && replyDecode.DeCall() == call) priority = replyDecode.Priority;
            }
            return priority;
        }

        private void UpdatePriority(string call, int priority)
        {
            List<EnqueueDecodeMessage> msgList;
            if (allCallDict.TryGetValue(call, out msgList))
            {
                foreach (EnqueueDecodeMessage msg in msgList)
                {
                    msg.Priority = priority;
                }
            }
        }

        private void StartProcessDecodeTimer2()
        {
            if (processDecodeTimer2.Enabled || (mode != "FT8" &&  mode != "FT4")) return;
            processDecodeTimer2.Interval = (mode == "FT8" ? 1500 : 750);
            processDecodeTimer2.Start();
            DebugOutput($"{Time()} processDecodeTimer2 start");
        }

        private void StopDecodeTimers()
        {
            if (processDecodeTimer.Enabled)
            {
                processDecodeTimer.Stop();       //no xmit cycle now
                DebugOutput($"{Time()} processDecodeTimer stop");
            }
            if (processDecodeTimer2.Enabled)
            {
                processDecodeTimer2.Stop();
                DebugOutput($"{Time()} processDecodeTimer2 stop");
            }
        }

        //high priority call should log late (needs definite confirmation, not presumed/implied)
        private bool IsLogEarly(string deCall)
        {
            if (!ctrl.logEarlyCheckBox.Checked) return false;
            return Priority(deCall) > (int)CallPriority.NEW_COUNTRY_ON_BAND;
        }

        /*private bool SendUdp(byte[] ba)
        {
            if (udpClient2 == null) return false;

            try
            {
                udpClient2.Send(ba, ba.Length);
            }
            catch (Exception e)
            {
                DebugOutput($"{Time()} sendUdp error:{e.ToString()}");
                return false;
            }
            return true;
        }*/

        private void ProcSoundQueue()
        {
            while (true)
            {
                if (soundQueue.Count > 0)
                {
                    string waveFileName = soundQueue.Peek();
                    //DebugOutput($"{Time()} ProcSoundQueue, soundQueue.Count:{soundQueue.Count} waveFileName:{waveFileName}");
                    PlaySound(soundQueue.Dequeue(), UIntPtr.Zero, (uint)(SoundFlags.SND_ASYNC));
                    if (waveFileName == "beepbeep.wav" || waveFileName == "blip.wav")
                    {
                        Thread.Sleep(200);
                    }
                    else
                    {
                        Thread.Sleep(650);
                    }
                }

                Thread.Sleep(100);
            }
        }

        private bool DetectUdpSettings(out IPAddress ipa, out int prt, out bool mul)
        {
            //use WSJT-X.ini file for settings
            string pgmNameWsjtx = "WSJT-X";
            string pathWsjtx = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\{pgmNameWsjtx}";
            string pathFileNameExtWsjtx = pathWsjtx + "\\" + pgmNameWsjtx + ".ini";
            ipa = null;
            prt = 0;
            mul = false;
            string ipaString;

            if (!Directory.Exists(pathWsjtx)) return false;
            try
            {
                IniFile iniFile = new IniFile(pathFileNameExtWsjtx);
                ipaString = iniFile.Read("UDPServer", "Configuration");
                prt = Convert.ToInt32(iniFile.Read("UDPServerPort", "Configuration"));
            }
            catch
            {
                //ctrl.BringToFront();
                //MessageBox.Show("Unable to open settings file: " + pathFileNameExt + "\n\nContinuing with default settings...", pgmName, MessageBoxButtons.OK);
                return false;
            }

            if (ipaString == "" || prt == 0)
            {
                ipa = null;
                return false;
            }

            ipa = IPAddress.Parse(ipaString);
            mul = ipaString.Substring(0, 4) != "127.";
            return true;
        }

        private bool IsWsjtxRunning()
        {
            string file = "WSJT-X.lock";
            string pathFileNameExt = $"{Path.GetTempPath()}{file}";
            return File.Exists(pathFileNameExt);
        }

        //must call only when in WAIT state
        //to avoid async cakkback using disposed udpClient
        private void CloseAllUdp()
        {
            DebugOutput($"{Time()} CloseAllUdp");

            try
            {
                if (udpClient != null)
                {
                    udpClient.Close();
                    udpClient = null;
                    DebugOutput($"{spacer}closed udpClient");
                }
                if (udpClient2 != null)
                {
                    udpClient2.Close();
                    udpClient2 = null;
                    DebugOutput($"{spacer}closed udpClient2");
                }
            }
            catch (Exception e)         //udpClient might be disposed already
            {
                DebugOutput($"{spacer}error:{e.ToString()}");
            }
        }
        private void CheckFirstRun()
        {
            DebugOutput($"{spacer}ctrl.firstRun:{ctrl.firstRun} advanced:{advanced} showTxModes:{showTxModes} ");

            if (ctrl.firstRun && !advanced)
            {
                MessageBox.Show($"This program can be completely automatic, you don't need to do anything for continuous CQs and replies after you select 'Enable Tx' in WSJT-X.{Environment.NewLine}{Environment.NewLine}Do that when you're ready to start CQing, then for the next 15 minutes or so, let the Controller run by itself so you can get familiar with how it works.", pgmName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ctrl.firstRun && advanced && !showTxModes)
            {
                MessageBox.Show($"To learn how to select, skip, or cancel specific calls, select 'More info'.", pgmName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ctrl.firstRun && advanced && showTxModes)
            {
                ctrl.BringToFront();
                if (MessageBox.Show($"Please read carefully:{Environment.NewLine}{Environment.NewLine}You can now use a 'Listen for Calls' operating mode.{Environment.NewLine}{Environment.NewLine}Selecting 'Enable Tx' in WSJT-X will KEY YOUR TRANSMITTER PERIODICALLY!{Environment.NewLine}{Environment.NewLine}- By clicking 'OK', you understand and agree to this automatic method of keying your transmitter.{Environment.NewLine}{Environment.NewLine}- If you do not want this option, click 'Cancel', and the option will be removed permanently.", pgmName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                {
                    ctrl.skipLevelPrompt = true;
                    showTxModes = false;
                    Application.Restart();
                    return;
                }
                ctrl.firstRun = false;          //prevent asking again during this run (ex: config chgd, callsign chgd, etc.)
                return;
            }
        }

        private DialogResult Confirm(string s)
        {
            var confDlg = new ConfirmDlg();
            confDlg.text = s;
            confDlg.Owner = ctrl;
            confDlg.ShowDialog();
            return confDlg.DialogResult;
        }

        private bool WaitingTimedStart()
        {
            return ctrl.timedCheckBox.Checked 
                && txStartDateTime != DateTime.MaxValue
                && txStartDateTime > DateTime.Now;      //local
        }

        private void CheckTimedStartStop()
        {
            if (ctrl.timedCheckBox.Checked)
            {
                DateTime dtNow = DateTime.Now;      //local
                TimeSpan ts = new TimeSpan(0, 0, (int)(0.5 * (trPeriod / 1000)));       //decode start time is before top of the minute
                DebugOutput($"{Time()} CheckTimedStartStop, enabled:{ctrl.timedCheckBox.Checked} txStartDateTime(local):{txStartDateTime} ts:{ts} now(local):{dtNow}");
                if (paused && (dtNow >= txStartDateTime - ts) && !timedStartInProgress)          //local time
                {
                    timedStartInProgress = true;              //don't restart until timed operation re-enabled
                    DebugOutput($"{spacer}start Tx time, paused:{paused} txMode:{txMode}");

                    //begin timed operation
                    if (showTxModes)
                    {
                        txMode = (ctrl.modeComboBox.SelectedIndex == 1) ? WsjtxClient.TxModes.LISTEN : WsjtxClient.TxModes.CALL_CQ;
                        if (txMode == TxModes.LISTEN)
                        {
                            ctrl.listenModeButton_Click(null, null);
                        }
                        else
                        {
                            ctrl.cqModeButton_Click(null, null);
                        }
                    }
                    else
                    {
                        ctrl.cqModeButton_Click(null, null);
                    }
                    TxModeEnabled();

                    UpdateModeSelection();
                    DebugOutputStatus();
                    UpdateDebug();
                    return;
                }

                DebugOutput($"{Time()} CheckTimedStartStop, enabled:{ctrl.timedCheckBox.Checked} txStopDateTime(local):{txStopDateTime} ts:{ts} now(local):{dtNow}");
                if (dtNow >= txStopDateTime - ts)          //local time
                {
                    List<EnqueueDecodeMessage> msgList;
                    bool recdAny = false;       //no previous call(s) from DX station
                    if (callInProg != null && allCallDict.TryGetValue(callInProg, out msgList))
                    {
                        EnqueueDecodeMessage rMsg;
                        recdAny = (rMsg = msgList.Find(RogerReport)) != null || (rMsg = msgList.Find(Report)) != null || (rMsg = msgList.Find(Reply)) != null || (rMsg = msgList.Find(Signoff)) != null;
                    }
                    DebugOutput($"{spacer}stop Tx time(1), paused:{paused} callInProg:'{CallPriorityString(callInProg)}' recdAny:{recdAny} qsoState:{qsoState}");
                    if (!recdAny || qsoState == WsjtxMessage.QsoStates.CALLING)
                    {
                        //end timed operation
                        if (!paused)
                        {
                            DebugOutput($"{spacer}tx stopped(1), paused:{paused}");
                            Pause(true);
                        }
                        SetCallInProg(null);
                        restartQueue = false;           //get ready for next decode phase
                        txTimeout = false;              //ready for next timeout
                        tCall = null;
                        ctrl.timedCheckBox.Checked = false;
                    }
                    DebugOutputStatus();
                    UpdateDebug();
                }
            }
            UpdateStartStopTime();
        }

        private void SetupCq(bool enableTx)
        {
            //set/show frequency offset for period after decodes started
            emsg.NewTxMsgIdx = 10;
            emsg.GenMsg = $"";          //no effect
            emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
            emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
            emsg.CmdCheck = "";         //ignored
            emsg.Offset = AudioOffsetFromTxPeriod();
            ba = emsg.GetBytes();
            udpClient2.Send(ba, ba.Length);
            DebugOutput($"{Time()} >>>>>Sent 'Opt Req' cmd:10\n{emsg}");
            if (settingChanged)
            {
                ctrl.WsjtxSettingConfirmed();
                settingChanged = false;
            }

            emsg.NewTxMsgIdx = 6;
            emsg.GenMsg = $"CQ{NextDirCq()} {myCall} {myGrid}";
            emsg.SkipGrid = ctrl.skipGridCheckBox.Checked;
            emsg.UseRR73 = ctrl.useRR73CheckBox.Checked;
            emsg.CmdCheck = "";         //ignored
            ba = emsg.GetBytes();           //set up for CQ, auto, call 1st
            udpClient2.Send(ba, ba.Length);
            DebugOutput($"{Time()} >>>>>Sent 'Setup CQ' cmd:6\n{emsg}");
            qsoState = WsjtxMessage.QsoStates.CALLING;      //in case enqueueing call manually right now
            replyCmd = null;        //invalidate last reply cmd since not replying
            replyDecode = null;
            SetCallInProg(null);
            curCmd = emsg.GenMsg;
            newDirCq = false;           //if set, was processed here
            DebugOutput($"{spacer}qsoState:{qsoState} (was {lastQsoState} replyCmd:'{replyCmd}') newDirCq:{newDirCq}");

            if (enableTx) EnableTx();             //sets WSJT-X "Enable Tx" button state
        }

        private void SetPeriodState()
        {
            DateTime dtNow = DateTime.UtcNow;
            DebugOutput($"{Time()} SetPeriodState, dtNow:{dtNow}");
            period = IsEvenPeriod((dtNow.Minute * 60) + dtNow.Second) ? Periods.EVEN : Periods.ODD;       //determine this period
            DebugOutput($"{spacer}period:{period}");
        }

        private void LogBeep()
        {
            if (!debug) return;
            Console.Beep();
            DebugOutput($"{spacer}BEEP");
        }
    }
}

