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
using eOsziSerie        = Transfer.TransferManager.eOsziSerie;
using ITransferPanel    = Transfer.TransferManager.ITransferPanel;
using ScpiCombo         = Transfer.SCPI.ScpiCombo;
using eConnectMode      = Transfer.SCPI.eConnectMode;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using Utils             = OsziWaveformAnalyzer.Utils;
using PlatformManager   = Platform.PlatformManager;

// This class implements communication with an oscilloscope
// Sadly there is no standard for this communication so each vendor cooks his own soup.
// Therefore the vendor specific code is in a UserControl that implements ITransferPanel.
namespace Transfer
{
    public partial class FormTransfer : Form
    {
        eConnectMode   me_Mode;
        eOsziSerie     me_OsziSerie;
        SCPI           mi_Scpi;
        ITransferPanel mi_Panel;
        WForms.Timer   mi_StatusTimer;

        /// <summary>
        /// Constructor
        /// </summary>
        public FormTransfer(eOsziSerie e_OsziSerie, ITransferPanel i_Panel)
        {
            me_OsziSerie = e_OsziSerie;
            mi_Panel     = i_Panel;

            InitializeComponent();

            Control i_Ctrl = (Control)i_Panel;
            i_Ctrl.Top  = btnInstallDriver.Bottom + 5;
            i_Ctrl.Left = 10;
            Controls.Add(i_Ctrl);

            Height += i_Ctrl.Height - 5;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.Text = "Transfer  —  " + Utils.GetDescriptionAttribute(me_OsziSerie); // Window Title

            mi_Panel.OnLoad(me_OsziSerie);
            EnableGui(false);

            textCommand.Text = Utils.RegReadString(eRegKey.SendCommand, "*IDN?");
            textCommand.KeyDown += new KeyEventHandler(OnTextCommandKeyDown);

            statusLabel .Text  = "";
            statusLabel .Width = ClientSize.Width - 4;           

            // Load Combobox with USB devices
            try { PlatformManager.Instance.EnumerateUsbDevices(comboDevices); }                  // FIRST
            catch {}

            int s32_Mode = Utils.RegReadInteger(eRegKey.ConnectMode, (int)eConnectMode.USB) % 3; // AFTER
            switch ((eConnectMode)s32_Mode)
            {
                case eConnectMode.USB: radioUSB.Checked = true; break; // fires OnRadioButton_CheckedChanged()
                case eConnectMode.VXI: radioVXI.Checked = true; break; // fires OnRadioButton_CheckedChanged()
                case eConnectMode.TCP: radioTCP.Checked = true; break; // fires OnRadioButton_CheckedChanged()
            }

            textVxiLink.Text = Utils.RegReadString(eRegKey.LinkVXI, "inst0");

            mi_StatusTimer = new WForms.Timer();
            mi_StatusTimer.Tick += new EventHandler(OnStatusTimer);
            mi_StatusTimer.Interval = 4000;
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
            mi_StatusTimer.Stop();
        }

        // ==========================================================

        public void PrintStatus(String s_Text, Color c_Color)
        {
            statusLabel.Text      = s_Text;
            statusLabel.ForeColor = c_Color;
            Application.DoEvents();

            mi_StatusTimer.Stop();
            mi_StatusTimer.Start();
        }

        void OnStatusTimer(object sender, EventArgs e)
        {
            mi_StatusTimer.Stop();
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
                if (me_Mode == eConnectMode.USB)
                {
                    PlatformManager.Instance.EnumerateUsbDevices(comboDevices); // throws
                    if (comboDevices.Items.Count == 0)
                        throw new Exception("No USB device of type 'Test and Measurement Class' is connected.");

                    PrintStatus("Loaded " + comboDevices.Items.Count + " USB device(s) into Combobox", Color.Green);
                }
                else // TCP / VXI
                {
                    VxiClient i_VxiCLient = new VxiClient();
                    i_VxiCLient.EnumerateVxiDevices(comboDevices); // throws

                    if (comboDevices.Items.Count == 0)
                        throw new Exception("No device has responded to the VXI broadcast request.");

                    PrintStatus("Loaded " + comboDevices.Items.Count + " VXI device(s) into Combobox", Color.Green);
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
        /// Called from all 3 RadioButtons
        /// </summary>
        private void OnRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (radioUSB.Checked)
            {
                me_Mode = eConnectMode.USB;
                comboDevices.DropDownStyle = ComboBoxStyle.DropDownList;
                comboDevices.Text = Utils.RegReadString(eRegKey.ConnectUSB);
                lblUsbEndp  .Text = "USB Device";
            }
            if (radioTCP.Checked)
            {
                me_Mode = eConnectMode.TCP;
                comboDevices.DropDownStyle = ComboBoxStyle.DropDown;
                comboDevices.Text = Utils.RegReadString(eRegKey.ConnectTCP, "192.168.0.240 : 5555");
                lblUsbEndp  .Text = "IP Address : Port";
            }
            if (radioVXI.Checked)
            {
                me_Mode = eConnectMode.VXI;
                comboDevices.DropDownStyle = ComboBoxStyle.DropDown;
                comboDevices.Text = Utils.RegReadString(eRegKey.ConnectVXI, "192.168.0.240");
                lblUsbEndp  .Text = "IP Address";
            }

            lblVxiLink .Visible = radioVXI.Checked;
            textVxiLink.Visible = radioVXI.Checked;
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

            if (!Utils.StartBusyOperation(this))
                return;

            Utils.RegWriteInteger(eRegKey.ConnectMode,  (int)me_Mode);

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

                        mi_Scpi.ConnectUsb(i_Combo); // opens USB device, throws
                        Utils.RegWriteString(eRegKey.ConnectUSB, comboDevices.Text);
                        break;

                    case eConnectMode.VXI:
                        mi_Scpi.ConnectVxi(comboDevices.Text, textVxiLink.Text); // opens network connection, throws
                        Utils.RegWriteString(eRegKey.ConnectVXI, comboDevices.Text);
                        Utils.RegWriteString(eRegKey.LinkVXI,    textVxiLink.Text);
                        break;

                    case eConnectMode.TCP:
                        mi_Scpi.ConnectTcp(comboDevices.Text); // opens network connection, throws
                        Utils.RegWriteString(eRegKey.ConnectTCP, comboDevices.Text);
                        break;
                }
                
                mi_Panel.OnOpenDevice(mi_Scpi);
                EnableGui(true);
                PrintStatus("Connected", Color.Black);
            }
            catch (Exception Ex)
            {
                PrintStatus("Connect Error", Color.Red);
                Utils.ShowExceptionBox(this, Ex);
                Disconnect();
            }

            btnOpen.Enabled = true;
            Utils.EndBusyOperation(this);
        }

        void Disconnect()
        {
            mi_Panel.OnCloseDevice();
            if (mi_Scpi != null)
            {
                mi_Scpi.Dispose(); // close native handles
                mi_Scpi = null;
            }
            EnableGui(false);
        }

        void EnableGui(bool b_Open)
        {
            groupCommand.Enabled =  b_Open;
            comboDevices.Enabled = !b_Open;
            btnSearch   .Enabled = !b_Open;
            radioTCP    .Enabled = !b_Open;
            radioUSB    .Enabled = !b_Open;
            radioVXI    .Enabled = !b_Open;
            lblUsbEndp  .Enabled = !b_Open;
            textVxiLink .Enabled = !b_Open;
            lblVxiLink  .Enabled = !b_Open;
            textResponse.Text    = "";
            btnOpen     .Text    = b_Open ? "Close" : "Open";
        }

        // ==========================================================

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

            Utils.RegWriteString(eRegKey.SendCommand, textCommand.Text);

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


