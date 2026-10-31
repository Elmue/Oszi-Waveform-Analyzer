/*
------------------------------------------------------------
Oscilloscope Waveform Analyzer by ElmüSoft (www.netcult.ch/elmue)
This code is released under the terms of the GNU General Public License.
------------------------------------------------------------

NAMING CONVENTIONS which allow to see the type of a variable immediately without having to jump to the variable declaration:
 
     cName  for class    definitions
     tName  for type     definitions
     eName  for enum     definitions
     kName  for "konstruct" (struct) definitions (letter 's' already used for string)
   delName  for delegate definitions

    b_Name  for bool
    c_Name  for Char, also Color
    d_Name  for double
    e_Name  for enum variables
    f_Name  for function delegates, also float
    i_Name  for instances of classes
    k_Name  for "konstructs" (struct) (letter 's' already used for string)
	r_Name  for Rectangle
    s_Name  for strings
    o_Name  for objects
 
   s8_Name  for   signed  8 Bit (sbyte)
  s16_Name  for   signed 16 Bit (short)
  s32_Name  for   signed 32 Bit (int)
  s64_Name  for   signed 64 Bit (long)
   u8_Name  for unsigned  8 Bit (byte)
  u16_Name  for unsigned 16 bit (ushort)
  u32_Name  for unsigned 32 Bit (uint)
  u64_Name  for unsigned 64 Bit (ulong)

  An additional "m" is prefixed for all member variables (e.g. ms_String)
*/ 

#pragma warning disable 0618 // CS0618 warning System.Net.IPAddress.Address'has been deprecated. It is address family dependent.

using System;
using System.Net;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Windows.Forms;

using WForms            = System.Windows.Forms;
using DialogResult      = System.Windows.Forms.DialogResult;
using eOsziSerie        = Transfer.TransferManager.eOsziSerie;
using ITransferPanel    = Transfer.TransferManager.ITransferPanel;
using eRs232Settg       = Transfer.TransferManager.eRs232Settg;
using OsziFeatures      = Transfer.TransferManager.OsziFeatures;
using ScpiCombo         = Transfer.SCPI.ScpiCombo;
using eConnectMode      = Transfer.SCPI.eConnectMode;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using Utils             = OsziWaveformAnalyzer.Utils;
using AbortException    = OsziWaveformAnalyzer.Utils.AbortException;
using PlatformManager   = Platform.PlatformManager;

// This class implements communication with an oscilloscope
// Sadly there is no standard for this communication so each vendor cooks his own soup.
// Therefore the vendor specific code is in a UserControl that implements ITransferPanel.
namespace Transfer
{
    public partial class FormTransfer : Form
    {
        #region class Progress

        public class Progress
        {
            FormTransfer mi_Form;
            String       ms_Operation;
            int          ms32_TotalBytes;
            int          ms32_StartTick;
            int          ms32_LastTick;
            int          ms32_LastTransfer;
            bool         mb_Active;
            bool         mb_Abort;

            /// <summary>
            /// Constructor
            /// </summary>
            public Progress(FormTransfer i_Form)
            {
                mi_Form = i_Form;
            }

            /// <summary>
            /// mb_Abort must be reset once when the user clicks the Transfer button.
            /// mb_Abort is set when the user clicks the Abort button.
            /// </summary>
            public bool AbortTransfer
            {
                set { mb_Abort = value; }
            }

            /// <summary>
            /// Call this each time before starting a transfer on each channel.
            /// s_Operation is displayed before the progress (e.g. "Analog Channel 1")
            /// If the count of bytes to transfer is known in advance, pass it in s32_TotalBytes.
            /// If the count of bytes to transfer is unknown, pass s32_TotalBytes = 0. 
            /// Percent display will not be available or Total Bytes may later be set with SetTotalBytes().
            /// </summary>
            public void Init(String s_Operation, int s32_TotalBytes)
            {
                Debug.Assert(!String.IsNullOrEmpty(s_Operation), "Operation must be specified");

                ms_Operation      = s_Operation;
                ms32_TotalBytes   = s32_TotalBytes;
                ms32_StartTick    = Environment.TickCount;
                ms32_LastTick     = ms32_StartTick;
                ms32_LastTransfer = 0;
                mb_Active         = true;

                mi_Form.PrintStatus(ms_Operation + ":  Please wait...", Color.Black);
            }

            /// <summary>
            /// If the block size is unknown in Init(), but is obtained from the oscilloscope, it can be set here.
            /// </summary>
            public void SetTotalBytes(int s32_Total)
            {
                Debug.Assert(ms32_TotalBytes == 0); // call this only after Init() with unknown length
                ms32_TotalBytes = s32_Total;
                mb_Active       = true;
            }

            /// <summary>
            /// Display into status bar: 
            /// "Analog Channel 1: Transferred 1.289.225 (85%), Speed 122 kB/s"
            /// </summary>
            public void PrintProgress(int s32_Transferred)
            {
                if (mb_Abort)
                    throw new AbortException();

                if (!mb_Active)
                    return;

                if (ms32_TotalBytes > 0 && s32_Transferred >= ms32_TotalBytes)
                {
                    mi_Form.PrintStatus(ms_Operation + ": Finished", Color.Green);
                    return;
                }

                // If the user starts the next transfer without calling Init() --> do not display progress
                if (s32_Transferred < ms32_LastTransfer)
                {
                    mb_Active = false;
                    return;
                }

                int s32_Now = Environment.TickCount;
                if (Math.Abs(s32_Now - ms32_LastTick) > 250)
                {
                    int s32_Interval  = s32_Now - ms32_LastTick;
                    int s32_Added     = s32_Transferred - ms32_LastTransfer;
                    ms32_LastTick     = s32_Now;
                    ms32_LastTransfer = s32_Transferred;
    
                    String s_Msg = String.Format("{0}: Transferred {1:N0}", ms_Operation, s32_Transferred);

                    if (ms32_TotalBytes > 0)
                        s_Msg += String.Format(" ({0}%)", (Int64)s32_Transferred * 100 / ms32_TotalBytes);

                    if (s32_Interval > 200)
                        s_Msg += ", Speed " + Utils.FormatSpeed(s32_Added * 1000 / s32_Interval); // ms --> seconds

                    mi_Form.PrintStatus(s_Msg, Color.Black);
                }
            }
        }

        #endregion

        eConnectMode   me_Mode;
        eOsziSerie     me_OsziSerie;
        SCPI           mi_Scpi;
        ITransferPanel mi_Panel;
        Progress       mi_Progress;
        WForms.Timer   mi_ClearStatusTimer;

        // ---------- DC Capture ------------

        WForms.Timer   mi_CaptureTimer;
        Capture        mi_DcCapture;
        int            ms32_CaptureIdx;

        public eConnectMode ConnectMode
        {
            get { return me_Mode; }
        }

        public SCPI Scpi
        {
            get { return mi_Scpi; }
        }

        public Progress Progr
        {
            get { return mi_Progress; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public FormTransfer(eOsziSerie e_OsziSerie, ITransferPanel i_Panel)
        {
            me_OsziSerie = e_OsziSerie;
            mi_Panel     = i_Panel;
            mi_Progress  = new Progress(this);

            InitializeComponent();

            Control i_Ctrl = (Control)i_Panel;
            i_Ctrl.Top  = radioRS232.Bottom + 3;
            i_Ctrl.Left = 10;
            Controls.Add(i_Ctrl);

            Height += i_Ctrl.Height - 5;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.Text += "  —  " + Utils.GetDescriptionAttribute(me_OsziSerie); // Window Title

            mi_Panel.OnLoad(me_OsziSerie);
            EnableGui(false);

            textCommand.Text = Utils.RegReadString(eRegKey.SendCommand, "*IDN?", me_OsziSerie);
            textCommand.KeyDown += new KeyEventHandler(OnTextCommandKeyDown);

            statusLabel.Text  = "";
            statusLabel.Width = ClientSize.Width - 4;    
       
            // Load RS232 settings (stop bits, handshake)
            foreach (eRs232Settg e_RS232 in Enum.GetValues(typeof(eRs232Settg)))
            {
                comboRS232.Items.Add(e_RS232.ToString().Replace('_', ' ').Trim());                        // FIRST
            }

            int s32_Mode = Utils.RegReadInteger(eRegKey.ConnectMode, (int)eConnectMode.USB, me_OsziSerie); // AFTER
            switch ((eConnectMode)s32_Mode)
            {
                case eConnectMode.VXI:   radioVXI  .Checked = true; break; // fires OnRadioButton_CheckedChanged()
                case eConnectMode.TCP:   radioTCP  .Checked = true; break; // fires OnRadioButton_CheckedChanged()
                case eConnectMode.RS232: radioRS232.Checked = true; break; // fires OnRadioButton_CheckedChanged()
                default:                 radioUSB  .Checked = true; break; // fires OnRadioButton_CheckedChanged()
            }

            mi_ClearStatusTimer = new WForms.Timer();
            mi_ClearStatusTimer.Tick += new EventHandler(OnClearStatusTimer);
            mi_ClearStatusTimer.Interval = 4000;

            // ---------- DC Capture ------------

            mi_CaptureTimer = new WForms.Timer();
            mi_CaptureTimer.Tick += new EventHandler(OnCaptureTimer);

            comboInterval .Text = Utils.RegReadString(eRegKey.CaptureInterval, ""); // not oszi specific
            comboTotalTime.Text = Utils.RegReadString(eRegKey.CaptureTotTime,  ""); // not oszi specific

            if (comboInterval .SelectedIndex < 0) comboInterval .SelectedIndex = 0;
            if (comboTotalTime.SelectedIndex < 0) comboTotalTime.SelectedIndex = 0;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (Utils.IsBusy)
            {
                e.Cancel = true;
                MessageBox.Show(this, "An operation is still in progress.\nAbort the operation to close the window.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Disconnect();
            mi_ClearStatusTimer.Stop();
            mi_CaptureTimer    .Stop();
        }

        // ==========================================================

        public void PrintStatus(String s_Text, Color c_Color)
        {
            statusLabel.Text      = s_Text;
            statusLabel.ForeColor = c_Color;
            Application.DoEvents();

            mi_ClearStatusTimer.Stop();
            mi_ClearStatusTimer.Start();
        }

        void OnClearStatusTimer(object sender, EventArgs e)
        {
            mi_ClearStatusTimer.Stop();
            statusLabel.Text = "";
        }

        // ==========================================================

        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (!Utils.StartBusyOperation(this))
                return;

            comboDevices.Items.Clear();
            comboDevices.Text = "";
            btnSearch.Enabled = false;
            PrintStatus("Please wait ...", Color.Blue);

            try
            {
                switch (me_Mode)
                {
                    case eConnectMode.USB:
                        PlatformManager.Instance.EnumerateUsbDevices(comboDevices); // throws
                        if (comboDevices.Items.Count == 0)
                            throw new Exception("No USB device of type 'Test and Measurement Class' is connected.");

                        PrintStatus("Loaded " + comboDevices.Items.Count + " USB device(s) into Combobox", Color.Green);
                        break;

                    case eConnectMode.VXI:
                        VxiClient i_VxiCLient = new VxiClient();
                        i_VxiCLient.EnumerateVxiDevices(comboDevices); // throws

                        if (comboDevices.Items.Count == 0)
                            throw new Exception("No device has responded to the VXI broadcast request.");

                        PrintStatus("Loaded " + comboDevices.Items.Count + " VXI device(s) into Combobox", Color.Green);
                        break;
                }
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
            }

            Utils.EndBusyOperation(this);
            btnSearch.Enabled = true;
        }

        private void btnInstallDriver_Click(object sender, EventArgs e)
        {
            PlatformManager.Instance.InstallDriver(this);
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "SCPI");
        }

        /// <summary>
        /// Called from all RadioButtons
        /// </summary>
        private void OnRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            // This function is called twice: for the RadioButton that has been unchecked and for the one that has been checked.
            if (!((RadioButton)sender).Checked)
                return;

            lblDevice2 .Text = "";
            lblDevice3 .Text = "";
            textVxiLink.Visible = false;
            comboRS232 .Visible = false;
            comboBaud  .Visible = false;
            btnSearch  .Visible = false;
            comboDevices.Items.Clear();

            me_Mode = eConnectMode.Invalid;
            if (radioUSB  .Checked) me_Mode = eConnectMode.USB;
            if (radioTCP  .Checked) me_Mode = eConnectMode.TCP;
            if (radioVXI  .Checked) me_Mode = eConnectMode.VXI;
            if (radioRS232.Checked) me_Mode = eConnectMode.RS232;

            OsziFeatures i_Feature = mi_Panel.OsziFeatures;
            if (!i_Feature.IsModeSupported(me_Mode))
            {
                String s_Msg = String.Format("The {0}\nsupports only these transfer modes:\n{1}.", 
                                             Utils.GetDescriptionAttribute(me_OsziSerie), i_Feature.AllModesToString());

                // TCP is not supported, but RS232
                if (me_Mode == eConnectMode.TCP && i_Feature.IsModeSupported(eConnectMode.RS232))
                {
                    // Ask the user only once about Ethernet converter, store the response in the registry
                    bool b_AllowRS232OverTCP = Utils.RegReadBool(eRegKey.RS232_OverTCP, false, me_OsziSerie);
                    if (!b_AllowRS232OverTCP)
                    {
                        s_Msg += "\n\nDo you have a RS232 to Ethernet converter\nconnected to the oscilloscope?";
                        if (MessageBox.Show(this, s_Msg, "Unsupported Transfer Mode", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            Utils.RegWriteBool(eRegKey.RS232_OverTCP, true, me_OsziSerie);
                        else
                            me_Mode = eConnectMode.Invalid;
                    }
                }
                else
                {
                    MessageBox.Show(this, s_Msg, "Unsupported Transfer Mode", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    me_Mode = eConnectMode.Invalid;
                }

                // Select the first RadioButton that is supported
                if (me_Mode == eConnectMode.Invalid)
                {
                    switch (i_Feature.GetFirstValidMode())
                    {
                        case eConnectMode.USB:   radioUSB  .Checked = true; break;
                        case eConnectMode.VXI:   radioVXI  .Checked = true; break;
                        case eConnectMode.TCP:   radioTCP  .Checked = true; break;
                        case eConnectMode.RS232: radioRS232.Checked = true; break;
                    }
                    return;
                }
            }

            // Depending on the router the local network my be  192.168.0.X  or  192.168.1.X or anything else.
            // Help the user by getting this right.
            // As default we simply assume that the oscilloscope is configured to the IP address X.X.X.240
            String s_DefaultIp = "192.168.0.240";
            if (Utils.LocalIp4Addr != null)
            {
                int s32_Addr = (int)Utils.LocalIp4Addr.Address;
                s_DefaultIp  = String.Format("{0}.{1}.{2}.240", s32_Addr & 0xFF, (s32_Addr >> 8) & 0xFF, (s32_Addr >> 16) & 0xFF);
            }

            // here me_Mode is valid for the Oszi
            switch (me_Mode)
            {
                case eConnectMode.USB:
                    try { PlatformManager.Instance.EnumerateUsbDevices(comboDevices); } 
                    catch (Exception Ex)
                    { 
                        Utils.ShowExceptionBox(this, Ex, "Error enumerating USB devices"); 
                        return;
                    }
                    comboDevices.DropDownStyle = ComboBoxStyle.DropDownList;
                    comboDevices.Width = btnSearch.Left - comboDevices.Left - 5;
                    comboDevices.Text  = Utils.RegReadString(eRegKey.USB_Device, "", me_OsziSerie);
                    lblDevice1  .Text  = "USB Device";
                    btnSearch.Visible  = true;
                    break;

                case eConnectMode.TCP:
                    String s_AddrPort  = s_DefaultIp + " : " + i_Feature.ms_TcpPort;
                    comboDevices.DropDownStyle = ComboBoxStyle.DropDown;
                    comboDevices.Width = btnSearch.Left - comboDevices.Left - 5;
                    comboDevices.Text  = Utils.RegReadString(eRegKey.TCP_AddrPort, s_AddrPort, me_OsziSerie);
                    lblDevice1  .Text  = "IP Address : Port";
                    break;

                case eConnectMode.VXI:
                    comboDevices.DropDownStyle = ComboBoxStyle.DropDown;
                    comboDevices.Width  = btnSearch.Left - comboDevices.Left - 5;
                    comboDevices.Text   = Utils.RegReadString(eRegKey.VXI_Address, s_DefaultIp,          me_OsziSerie);
                    textVxiLink .Text   = Utils.RegReadString(eRegKey.VXI_Link,    i_Feature.ms_VxiLink, me_OsziSerie);
                    lblDevice1.Text     = "IP Address (:Port)";
                    lblDevice3.Text     = "VXI Link";
                    lblDevice3.Left     = textVxiLink.Left;
                    textVxiLink.Visible = true;
                    btnSearch  .Visible = true;
                    break;

                case eConnectMode.RS232:
                    try { PlatformManager.Instance.EnumerateRs232Ports(comboDevices); }
                    catch (Exception Ex)
                    { 
                        Utils.ShowExceptionBox(this, Ex, "Error enumerating RS232 ports"); 
                        return;
                    }
                    comboDevices.DropDownStyle = ComboBoxStyle.DropDownList;
                    comboDevices.Width  = comboBaud.Left - comboDevices.Left - 5;
                    comboDevices.Text   = Utils.RegReadString(eRegKey.RS232_Port,   "COM1",                  me_OsziSerie);
                    comboBaud   .Text   = Utils.RegReadString(eRegKey.RS232_Baud,   i_Feature.ms_Rs232Baud,  me_OsziSerie);
                    comboRS232  .Text   = Utils.RegReadString(eRegKey.RS232_Config, i_Feature.ms_Rs232Settg, me_OsziSerie);
                    lblDevice1.Text     = "Port";
                    lblDevice2.Text     = "Baudrate";
                    lblDevice3.Text     = "Settings";
                    lblDevice3.Left     = comboRS232.Left;
                    comboBaud .Visible  = true;
                    comboRS232.Visible  = true;
                    break;
            }

            if (comboDevices.SelectedIndex < 0 && comboDevices.Items.Count > 0) 
                comboDevices.SelectedIndex = 0;
        }

        // ==========================================================

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (btnOpen.Text == "Close")
            {
                // Do not allow to close the connection while a Tansfer is running
                if (!Utils.StartBusyOperation(this))
                    return;

                Utils.EndBusyOperation(this);

                Disconnect();
                return;
            }

            if (me_Mode == eConnectMode.Invalid)
            {
                MessageBox.Show(this, "Select a valid tansfer mode!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!Utils.StartBusyOperation(this))
                return;

            Utils.RegWriteInteger(eRegKey.ConnectMode, (int)me_Mode, me_OsziSerie);

            textVxiLink.Text = textVxiLink.Text.Trim();

            btnOpen.Enabled = false;
            PrintStatus("Connecting to oscilloscope...", Color.Blue);
            mi_Scpi = new SCPI();
            try
            {
                switch (me_Mode)
                {
                    case eConnectMode.USB:
                        ScpiCombo i_Combo = (ScpiCombo)comboDevices.SelectedItem;
                        if (i_Combo == null)
                            throw new Exception("Please connect the oscilloscope over USB, turn it on and click 'Search'.\n"
                                              + "If it does not appear, click 'Install Diver'.\n"
                                              + "If it still does not work read the help file.");

                        mi_Scpi.ConnectUsb  (i_Combo); // opens USB device, throws
                        Utils.RegWriteString(eRegKey.USB_Device, comboDevices.Text, me_OsziSerie);
                        break;

                    case eConnectMode.VXI:
                        mi_Scpi.ConnectVxi  (comboDevices.Text,   textVxiLink.Text); // opens network connection, throws
                        Utils.RegWriteString(eRegKey.VXI_Address, comboDevices.Text, me_OsziSerie);
                        Utils.RegWriteString(eRegKey.VXI_Link,    textVxiLink.Text,  me_OsziSerie);
                        break;

                    case eConnectMode.TCP:
                        mi_Scpi.ConnectTcp  (comboDevices.Text); // opens network connection, throws
                        Utils.RegWriteString(eRegKey.TCP_AddrPort, comboDevices.Text, me_OsziSerie);
                        break;

                    case eConnectMode.RS232:
                        mi_Scpi.ConnectRS232(comboDevices.Text,    comboBaud.Text, comboRS232.Text); // opens serial port, throws
                        Utils.RegWriteString(eRegKey.RS232_Port,   comboDevices.Text, me_OsziSerie);
                        Utils.RegWriteString(eRegKey.RS232_Baud,   comboBaud   .Text, me_OsziSerie);
                        Utils.RegWriteString(eRegKey.RS232_Config, comboRS232  .Text, me_OsziSerie);
                        break;
                }
                
                mi_Panel.OpenDevice();
                EnableGui(true);
                PrintStatus("Connected", Color.Black);
            }
            catch (Exception Ex)
            {
                PrintStatus("Connection failed", Color.Red);
                Utils.ShowExceptionBox(this, Ex);
                Disconnect();
            }

            btnOpen.Enabled = true;
            Utils.EndBusyOperation(this);
        }

        void Disconnect()
        {
            mi_Panel.CloseDevice();
            if (mi_Scpi != null)
            {
                mi_Scpi.Dispose(); // close native handles
                mi_Scpi = null;
            }
            EnableGui(false);
        }

        void EnableGui(bool b_Open)
        {
            groupCommand  .Enabled =  b_Open;
            groupDcCapture.Enabled =  b_Open;
            comboDevices  .Enabled = !b_Open;
            btnSearch     .Enabled = !b_Open;
            radioTCP      .Enabled = !b_Open;
            radioUSB      .Enabled = !b_Open;
            radioVXI      .Enabled = !b_Open;
            radioRS232    .Enabled = !b_Open;
            comboRS232    .Enabled = !b_Open;
            lblDevice1    .Enabled = !b_Open;
            lblDevice2    .Enabled = !b_Open;
            lblDevice3    .Enabled = !b_Open;
            textVxiLink   .Enabled = !b_Open;
            textResponse  .Text    = "";
            btnOpen       .Text    = b_Open ? "Close" : "Open";
        }

        // ==========================================================================
        //                                DC Capture
        // ==========================================================================

        private void btnDcCapture_Click(object sender, EventArgs e)
        {
            if (btnDcCapture.Text == "Abort")
            {
                StopCapture();
                PrintStatus("Capture aborted", Color.Black);
                return;
            }

            if (!Utils.StartBusyOperation(this))
                return;

            try
            {
                Utils.RegWriteString(eRegKey.CaptureInterval, comboInterval .Text); // not oszi specific
                Utils.RegWriteString(eRegKey.CaptureTotTime,  comboTotalTime.Text); // not oszi specific

                decimal d_Interval = Utils.ParseInterval(comboInterval .Text); // in seconds
                decimal d_TotTime  = Utils.ParseInterval(comboTotalTime.Text); // in seconds

                // The maximum interval that can be stored in a signed 64 bit variable with pico second resolution is 106 days.
                if (d_TotTime > 100 * 24 * 60 * 60)
                    throw new Exception("Maximum interval (100 days) exceeded");

                // s64_Interval = 1 second   ... 5 minutes
                // s64_TotTime  = 10 minutes ... 10 days
                mi_DcCapture    = mi_Panel.StartDcCapture(d_Interval, d_TotTime); // throws
                ms32_CaptureIdx = 0;

                Utils.FormMain.StoreNewCapture(mi_DcCapture);

                comboInterval .Enabled = false;
                comboTotalTime.Enabled = false;
                btnDcCapture.Text = "Abort";
                PrintStatus("Do not touch the oscilloscope while capturing!", Color.Red);

                mi_CaptureTimer.Interval = (int)(d_Interval * 1000);
                mi_CaptureTimer.Start();    // FIRST
                OnCaptureTimer(null, null); // AFTER (stops timer on error)
            }
            catch (Exception Ex)
            {
                StopCapture();                    // FIRST (stop timer)
                Utils.ShowExceptionBox(this, Ex); // AFTER (show messagebox)
            }
        }

        void OnCaptureTimer(object sender, EventArgs e)
        {
            try
            {
                // Avoid that the operating system goes into sleep mode while capturing data
                PlatformManager.Instance.PreventSleep();

                mi_Panel.CaptureDcSample(mi_DcCapture, ms32_CaptureIdx); // throws
                Utils.PanelOszi.RecalculateEverything();

                // Increment only if AddDcSample() did not throw an exception
                ms32_CaptureIdx ++;

                if (ms32_CaptureIdx == mi_DcCapture.ms32_Samples)
                {
                    StopCapture(); // FIRST
                    Close();       // AFTER
                    return;
                }

                String s_Progess = String.Format("Captured {0} Samples of {1} ({2}%)", 
                                                 ms32_CaptureIdx, mi_DcCapture.ms32_Samples,
                                                 100 * ms32_CaptureIdx / mi_DcCapture.ms32_Samples);

                decimal d_ElapsedMilli = (ms32_CaptureIdx - 1) * mi_CaptureTimer.Interval;
                if (d_ElapsedMilli > 0)
                    s_Progess += "  Elapsed: " + Utils.FormatTimePico(d_ElapsedMilli / 1000 * Utils.PICOS_PER_SECOND);

                PrintStatus(s_Progess, Color.Blue);
            }
            catch (Exception Ex)
            {
                StopCapture();                    // FIRST (stop timer)
                Utils.ShowExceptionBox(this, Ex); // AFTER (show messagbox)
            }
        }

        void StopCapture()
        {
            mi_CaptureTimer.Stop();
            btnDcCapture.Text = "DC Capture";
            comboInterval .Enabled = true;
            comboTotalTime.Enabled = true;

            if (mi_DcCapture != null)
            {
                // If the user aborts capturing --> remove all unused samples that have zero Volt.
                mi_DcCapture.ms32_Samples = Math.Max(Utils.MIN_VALID_SAMPLES, ms32_CaptureIdx);
                Utils.FormMain.StoreNewCapture(mi_DcCapture);
                mi_DcCapture = null;
            }

            Utils.EndBusyOperation(this);
        }

        // ==========================================================================
        //                                Manual Command
        // ==========================================================================

        void OnTextCommandKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                btnSend_Click(null, null);
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (textCommand.Text.Length == 0)
            {
                MessageBox.Show(this, "Enter a command!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Utils.RegWriteString(eRegKey.SendCommand, textCommand.Text, me_OsziSerie);

            if (!Utils.StartBusyOperation(this))
                return;

            btnSend.Enabled = false;
            textResponse.Text      = "Please wait...";
            textResponse.ForeColor = Color.Blue;
            Application.DoEvents();

            mi_Panel.SendManualCommand(textCommand.Text, textResponse);

            btnSend.Enabled = true;
            Utils.EndBusyOperation(this);
        }
    }
}


