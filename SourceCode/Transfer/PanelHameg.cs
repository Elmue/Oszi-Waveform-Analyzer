/*
------------------------------------------------------------
Oscilloscope Waveform Analyzer by ElmüSoft (www.netcult.ch/elmue)
This code is released under the terms of the GNU General Public License.
This class has been written by Peter Schwendner, integrated into the project by ELmü
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

using WForms            = System.Windows.Forms;
using ITransferPanel    = Transfer.TransferManager.ITransferPanel;
using eOsziSerie        = Transfer.TransferManager.eOsziSerie;
using OsziFeatures      = Transfer.TransferManager.OsziFeatures;
using eRs232Settg       = Transfer.TransferManager.eRs232Settg;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using Utils             = OsziWaveformAnalyzer.Utils;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using eOperation        = Transfer.Hameg.eOperation;
using OsziConfig        = Transfer.Hameg.OsziConfig;
using eConnectMode      = Transfer.SCPI.eConnectMode;
using AbortException    = OsziWaveformAnalyzer.Utils.AbortException;

namespace Transfer
{
    /// <summary>
    /// The controls in FormTransfer for Hameg oscilloscopes HMO1522 (HMO Compact series) and HM2008 (CombiScopes).
    /// These are connected over RS232 (Hameg HO720 interface) or USB virtual COM port or Ethernet (HO730).
    /// </summary>
    public partial class PanelHameg : UserControl, ITransferPanel
    {
        IHamegScope  mi_Hameg;
        eOsziSerie   me_OsziSerie;
        FormTransfer mi_Form;
        WForms.Timer mi_RefreshTimer;

        public OsziFeatures OsziFeatures
        {
            get 
            { 
                OsziFeatures i_Features = new OsziFeatures();
                switch (me_OsziSerie)
                {
                    case eOsziSerie.Hameg_HMO1522:  
                        i_Features.me_SupportedModes = eConnectMode.RS232 | eConnectMode.TCP; // with additional HO720 / HO730 card
                        i_Features.ms_TcpPort        = "5025";
                        i_Features.ms_Rs232Baud      = "115200";
                        i_Features.ms_Rs232Settg     = "8N1 RTS";
                        break;
                    case eOsziSerie.Hameg_HM2008: 
                        i_Features.me_SupportedModes = eConnectMode.RS232 | eConnectMode.TCP; // with additional HO720 / HO730 card
                        i_Features.ms_TcpPort        = "5025";
                        i_Features.ms_Rs232Baud      = "115200";
                        i_Features.ms_Rs232Settg     = "8N2 RTS";
                        break;
                    case eOsziSerie.Hameg_HM507:  
                        i_Features.me_SupportedModes = eConnectMode.RS232; // RS232 to Ethernet converters are handled in FormTransfer!
                        i_Features.ms_Rs232Baud      = "115200";
                        i_Features.ms_Rs232Settg     = "8N2 RTS";
                        break;
                    default: 
                        throw new Exception("Invalid OsziSerie");
                }
                return i_Features;
            }
        }

        public PanelHameg()
        {
            InitializeComponent();
        }

        // This is called when FormTransfer is opened
        public void OnLoad(eOsziSerie e_OsziSerie)
        {
            me_OsziSerie = e_OsziSerie;
            mi_Form      = (FormTransfer)Parent;

            // RS232 is slow. Do not poll the scope too often, otherwise it is busy answering instead of acquiring.
            mi_RefreshTimer = new WForms.Timer();
            mi_RefreshTimer.Tick += new EventHandler(OnRefreshTimer);
            mi_RefreshTimer.Interval = 1500;

            lblSerie.Text = Utils.GetDescriptionAttribute(e_OsziSerie);
            ClearGroupBox();

            if (Utils.RegReadBool(eRegKey.TransferMemory, false, me_OsziSerie))
                radioMemory.Checked = true;
            else
                radioScreen.Checked = true;
        }

        // Open the connection to the oscilloscope
        // May throw
        public void OpenDevice()
        {
            if (me_OsziSerie == eOsziSerie.Hameg_HM507)
                mi_Hameg = new HamegHM507();
            else
                mi_Hameg = new Hameg(me_OsziSerie);

            mi_Hameg.Connect(mi_Form); // may throw

            LoadGroupBox();

            OnRefreshTimer(null, null); // starts the refresh timer
        }

        // Close the connection to the oscilloscope
        public void CloseDevice()
        {
            if (mi_Hameg != null)
            {
                mi_Hameg.Disconnect();
                mi_Hameg = null;
            }

            mi_RefreshTimer.Stop();
            ClearGroupBox();
        }

        // This is called periodically to update the oscilloscope settings in the Panel
        void OnRefreshTimer(object sender, EventArgs e)
        {
            mi_RefreshTimer.Stop();
            if (mi_Hameg == null)
                return;

            try
            {
                OsziConfig i_Config = mi_Hameg.GetOsziConfiguration();

                if (i_Config.md_TimeBase > 0)
                    SetLabel(lblTimeBase, false, Utils.FormatTimePico(i_Config.md_TimeBase * Utils.PICOS_PER_SECOND) + " / div");
                else
                    SetLabel(lblTimeBase, true, "UNKNOWN");

                if (i_Config.md_SampleRate > 0)
                    SetLabel(lblSampleRate, false, Utils.FormatFrequency(i_Config.md_SampleRate));
                else
                    SetLabel(lblSampleRate, false, "not reported");

                if (i_Config.ms_AcqState != null)
                    SetLabel(lblAcqState, i_Config.ms_AcqState == "RUN", i_Config.ms_AcqState);
                else
                    SetLabel(lblAcqState, false, "not reported");

                mi_RefreshTimer.Interval = 1500;
            }
            catch (Exception Ex)
            {
                SetLabel(lblTimeBase,   true, "ERROR");
                SetLabel(lblSampleRate, true, "ERROR");
                SetLabel(lblAcqState,   true, "ERROR");

                // TimeoutException:
                // This may happen when the wrong oscilloscope serie is selected or the baudrate is wrong.
                // Try again after a longer interval to avoid sending the same wrong command again and again.
                if (Ex is TimeoutException)
                    mi_RefreshTimer.Interval = 5000;
            }

            mi_RefreshTimer.Start();
        }

        void SetLabel(Label i_Label, bool b_Error, String s_Text)
        {
            if (b_Error) i_Label.ForeColor = Color.FromArgb(0xFF, 0xA0, 0x80); // Red is too dark
            else         i_Label.ForeColor = Color.Cyan;
            i_Label.Text = s_Text;
        }

        void LoadGroupBox()
        {
            if (mi_Hameg.Model != null)
            {
                SetLabel(lblBrand,    false, mi_Hameg.Model.ms_Brand);
                SetLabel(lblModel,    false, mi_Hameg.Model.ms_Model);
                SetLabel(lblSerial,   false, mi_Hameg.Model.ms_Serial);
                SetLabel(lblFirmware, false, mi_Hameg.Model.ms_Firmware);
            }
            else
            {
                SetLabel(lblBrand,    true, "ERROR");
                SetLabel(lblModel,    true, "ERROR");
                SetLabel(lblSerial,   true, "ERROR");
                SetLabel(lblFirmware, true, "ERROR");
            }

            btnReset .BackColor = Color.BlanchedAlmond;
            btnAuto  .BackColor = Color.LightSkyBlue;
            btnRun   .BackColor = Color.PaleGreen;
            btnStop  .BackColor = Color.Salmon;
            btnSingle.BackColor = Color.BlanchedAlmond;

            btnReset   .Visible = mi_Hameg.CanReset;
            radioMemory.Enabled = mi_Hameg.HasDeepMemory;
            if (!mi_Hameg.HasDeepMemory)
                radioScreen.Checked = true;

            groupTransfer.Enabled = true;
        }

        void ClearGroupBox()
        {
            lblBrand     .Text = "";
            lblModel     .Text = "";
            lblSerial    .Text = "";
            lblFirmware  .Text = "";
            lblTimeBase  .Text = "";
            lblSampleRate.Text = "";
            lblAcqState  .Text = "";

            btnReset .BackColor = SystemColors.Control;
            btnAuto  .BackColor = SystemColors.Control;
            btnRun   .BackColor = SystemColors.Control;
            btnStop  .BackColor = SystemColors.Control;
            btnSingle.BackColor = SystemColors.Control;

            groupTransfer.Enabled = false;
        }

        // ==========================================================================
        //                             Operation Buttons 
        // ==========================================================================

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(mi_Form, "This resets all settings of the oscilloscope to factory defaults.\nContinue?",
                                "Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                OnButtonOperation(eOperation.Reset);
        }
        private void btnAuto_Click(object sender, EventArgs e)
        {
            OnButtonOperation(eOperation.Auto);
        }
        private void btnRun_Click(object sender, EventArgs e)
        {
            OnButtonOperation(eOperation.Run);
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            OnButtonOperation(eOperation.Stop);
        }
        private void btnSingle_Click(object sender, EventArgs e)
        {
            OnButtonOperation(eOperation.Single);
        }
        void OnButtonOperation(eOperation e_Operation)
        {
            if (!Utils.StartBusyOperation(mi_Form))
                return;

            mi_RefreshTimer.Stop();
            try
            {
                mi_Hameg.ExecuteOperation(e_Operation);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(mi_Form, Ex);
            }
            OnRefreshTimer(null, null);

            Utils.EndBusyOperation(mi_Form);
        }

        // ==========================================================================
        //                             Waveform Transfer
        // ==========================================================================

        private void btnTransfer_Click(object sender, EventArgs e)
        {
            if (btnTransfer.Text == "Abort")
            {
                mi_Form.Progr.AbortTransfer = true;
                return;
            }

            if (!Utils.StartBusyOperation(mi_Form)) // FIRST
                return;

            if (Utils.FormMain.HasUnsavedChanges()) // AFTER
                return;

            Utils.RegWriteBool(eRegKey.TransferMemory, radioMemory.Checked, me_OsziSerie);

            mi_RefreshTimer.Stop();
            btnTransfer.Text = "Abort";
            mi_Form.PrintStatus("Start Tansfer...", Color.Blue);

            try
            {
                mi_Form.Progr.AbortTransfer = false;
                Capture i_Capture = mi_Hameg.TransferAllChannels(radioMemory.Checked);
                if (i_Capture != null)
                {
                    Utils.FormMain.StoreNewCapture(i_Capture);
                    mi_Form.PrintStatus("Ready", Color.Green);

                    if (mi_Hameg.TransferWarnings != null)
                        MessageBox.Show(mi_Form, mi_Hameg.TransferWarnings, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    mi_Form.PrintStatus("Aborted", Color.Red);
                }
            }
            catch (AbortException Ex)
            {
                mi_Form.PrintStatus(Ex.Message, Color.Red);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(mi_Form, Ex);
            }

            btnTransfer.Text = "Transfer";
            OnRefreshTimer(null, null);

            Utils.EndBusyOperation(mi_Form);
        }

        // ==========================================================================
        //                                DC Capture
        // ==========================================================================

        // The user has clicked the "DC Capture" button in FormTransfer.
        // d_Interval and d_TotTime are the values that the user has entered in FormTransfer.
        public Capture StartDcCapture(decimal d_Interval, decimal d_TotTime)
        {
            // TODO
            throw new NotImplementedException();
        }

        // The timer has elapsed --> add a new sample to the capture.
        // i_DcCapture is the Capture that was returned from OnStartDcCapture()
        // s32_CaptureIdx is the sample index in i_DcCapture where to store the new analog value.
        public void CaptureDcSample(Capture i_DcCapture, int s32_CaptureIdx)
        {
            // TODO
            throw new NotImplementedException();
        }

        // ==========================================================================
        //                                Manual Command
        // ==========================================================================

        /// <summary>
        /// FormTransfer calls StartBusyOperation() before calling this function
        /// </summary>
        public void SendManualCommand(String s_Command, TextBox i_TextReponse)
        {
            mi_RefreshTimer.Stop();
            try
            {
                i_TextReponse.Text = mi_Hameg.SendManualCommand(s_Command);
                i_TextReponse.ForeColor = Color.Black;
            }
            catch (TimeoutException)
            {
                // It is not an error if a command like ":STOP" does not send a response
                i_TextReponse.Text = "No response received.";
                i_TextReponse.ForeColor = Color.Black;
            }
            catch (Exception Ex)
            {
                i_TextReponse.Text = Ex.Message;
                i_TextReponse.ForeColor = Color.Red;
            }
            OnRefreshTimer(null, null);
        }
    }
}
