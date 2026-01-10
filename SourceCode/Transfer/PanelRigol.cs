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
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using Utils             = OsziWaveformAnalyzer.Utils;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eOperation        = Transfer.Rigol.eOperation;
using OsziModel         = Transfer.Rigol.OsziModel;
using OsziConfig        = Transfer.Rigol.OsziConfig;
using ScpiCombo         = Transfer.SCPI.ScpiCombo;
using PlatformManager   = Platform.PlatformManager;

namespace Transfer
{
    public partial class PanelRigol : UserControl, ITransferPanel
    {
        SCPI         mi_Scpi;
        Rigol        mi_Rigol;
        eOsziSerie   me_OsziSerie;
        FormTransfer mi_Form;
        WForms.Timer mi_RefreshTimer;

        // ---------- DC Capture ------------

        WForms.Timer mi_CaptureTimer;
        Capture      mi_DcCapture;
        int          ms32_CaptureIdx;

        public PanelRigol()
        {
            InitializeComponent();
        }

        // This is called when FormTransfer is opened
        public void OnLoad(eOsziSerie e_OsziSerie)
        {
            me_OsziSerie = e_OsziSerie;
            mi_Form      = (FormTransfer)Parent;

            mi_RefreshTimer = new WForms.Timer();
            mi_RefreshTimer.Tick += new EventHandler(OnRefreshTimer);
            mi_RefreshTimer.Interval = 500;

            mi_CaptureTimer = new WForms.Timer();
            mi_CaptureTimer.Tick += new EventHandler(OnCaptureTimer);

            lblSerie.Text = Utils.GetDescriptionAttribute(e_OsziSerie);       
            ClearGroupBox();

            if (Utils.RegReadBool(eRegKey.TransferMemory))
                radioMemory.Checked = true;
            else
                radioScreen.Checked = true;   

            comboInterval .Text = Utils.RegReadString(eRegKey.CaptureInterval);
            comboTotalTime.Text = Utils.RegReadString(eRegKey.CaptureTotTime);

            if (comboInterval .SelectedIndex < 0) comboInterval .SelectedIndex = 0;
            if (comboTotalTime.SelectedIndex < 0) comboTotalTime.SelectedIndex = 0;
        }

        // Open the connection to the oscilloscope
        // May throw
        public void OnOpenDevice(SCPI i_Scpi)
        {
            mi_Scpi  = i_Scpi;
            mi_Rigol = new Rigol(me_OsziSerie);
            mi_Rigol.Connect(mi_Form, i_Scpi); // may throw

            LoadGroupBox();

            OnRefreshTimer(null, null); // starts the refresh timer
        }

        // Close the connection to the oscilloscope
        public void OnCloseDevice()
        {
            // Close the connection
            if (mi_Rigol != null)
            {
                mi_Rigol.Disconnect();
                mi_Rigol = null;
            }

            mi_CaptureTimer.Stop();
            mi_RefreshTimer.Stop();
            ClearGroupBox();
        }

        private void radioMemory_CheckedChanged(object sender, EventArgs e)
        {
            OnRefreshTimer(null, null);
        }

        // This is called every 500 ms to update the oscilloscope settings in the Panel
        void OnRefreshTimer(object sender, EventArgs e)
        {
            mi_RefreshTimer.Stop();

            // The displayed values change when START/STOP is changing or more channels are enabled or the memory is configured in the scope
            try
            {
                OsziConfig i_Config = mi_Rigol.GetOsziConfiguration(radioMemory.Checked);

                SetLabel(lblSampleRate, false, Utils.FormatFrequency(i_Config.md_SampleRate));

                if (i_Config.ms32_SamplePoints == 0)
                    SetLabel(lblSamplePoints, true, "NOT READY");
                else
                    SetLabel(lblSamplePoints, false, i_Config.ms32_SamplePoints.ToString("N0"));
                
                if (i_Config.ms64_Duration == 0)
                    SetLabel(lblTotDuration, true, "NOT READY");
                else
                    SetLabel(lblTotDuration, false, Utils.FormatTimePico(i_Config.ms64_Duration));

                mi_RefreshTimer.Interval = 500;
            }
            catch (Exception Ex)
            {
                SetLabel(lblSampleRate,   true, "ERROR");
                SetLabel(lblSamplePoints, true, "ERROR");
                SetLabel(lblTotDuration,  true, "ERROR");

                // TimeoutException:
                // This may happen when the wrong oscilloscope serie is selected.
                // Try again after a longer interval to avoid sending the same wrong command again and again.
                // This also happens when the scope is busy because the user has moved the signal with the knobs.
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
            if (mi_Rigol.Model != null)
            {
                SetLabel(lblBrand,    false, mi_Rigol.Model.ms_Brand);
                SetLabel(lblModel,    false, mi_Rigol.Model.ms_Model);
                SetLabel(lblSerial,   false, mi_Rigol.Model.ms_Serial);
                SetLabel(lblFirmware, false, mi_Rigol.Model.ms_Firmware);
            }
            else
            {
                SetLabel(lblBrand,    true, "ERROR");
                SetLabel(lblModel,    true, "ERROR");
                SetLabel(lblSerial,   true, "ERROR");
                SetLabel(lblFirmware, true, "ERROR");
            }

            btnReset    .BackColor = Color.BlanchedAlmond;
            btnClear    .BackColor = Color.BlanchedAlmond;
            btnAuto     .BackColor = Color.LightSkyBlue;
            btnRun      .BackColor = Color.PaleGreen;
            btnStop     .BackColor = Color.Salmon;
            btnSingle   .BackColor = Color.BlanchedAlmond;
            btnForceTrig.BackColor = Color.BlanchedAlmond;

            groupTransfer .Enabled = true;
            groupDcCapture.Enabled = true;
        }

        void ClearGroupBox()
        {
            lblBrand       .Text = "";
            lblModel       .Text = "";
            lblSerial      .Text = "";
            lblFirmware    .Text = "";
            lblSampleRate  .Text = "";
            lblSamplePoints.Text = "";
            lblTotDuration .Text = "";
            lblProgress    .Text = "";

            btnReset    .BackColor = SystemColors.Control;
            btnClear    .BackColor = SystemColors.Control;
            btnAuto     .BackColor = SystemColors.Control;
            btnRun      .BackColor = SystemColors.Control;
            btnStop     .BackColor = SystemColors.Control;
            btnSingle   .BackColor = SystemColors.Control;
            btnForceTrig.BackColor = SystemColors.Control;

            groupTransfer .Enabled = false;
            groupDcCapture.Enabled = false;
        }

        // ==========================================================

        private void btnClear_Click(object sender, EventArgs e)
        {
            OnButtonOperation(eOperation.Clear);
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
        private void btnForceTrig_Click(object sender, EventArgs e)
        {
            OnButtonOperation(eOperation.ForceTrigger);
        }
        private void btnReset_Click(object sender, EventArgs e)
        {
            OnButtonOperation(eOperation.Reset);
        }
        void OnButtonOperation(eOperation e_Operation)
        {
            if (!Utils.StartBusyOperation(mi_Form))
                return;

            mi_RefreshTimer.Stop();
            try
            {
                mi_Rigol.ExecuteOperation(e_Operation);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(mi_Form, Ex);
            }
            OnRefreshTimer(null, null);

            Utils.EndBusyOperation(mi_Form);
        }

        // ==========================================================

        private void btnTransfer_Click(object sender, EventArgs e)
        {
            if (btnTransfer.Text == "Abort")
            {
                mi_Rigol.AbortTransfer();
                return;
            }

            if (!Utils.StartBusyOperation(mi_Form))
                return;

            Utils.RegWriteBool(eRegKey.TransferMemory, radioMemory.Checked);

            mi_RefreshTimer.Stop();
            btnTransfer.Text = "Abort";
            mi_Form.PrintStatus("Start Tansfer...", Color.Blue);

            try
            {
                Capture i_Capture = mi_Rigol.TransferAllChannels(radioMemory.Checked);
                if (i_Capture != null)
                {
                    i_Capture.mb_Dirty = true; // The user has an unsaved capture
                    Utils.FormMain.StoreNewCapture(i_Capture);
                    mi_Form.PrintStatus("Ready", Color.Green);
                }
                else
                {
                    mi_Form.PrintStatus("Aborted", Color.Red);
                }
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(mi_Form, Ex);
            }

            btnTransfer.Text = "Transfer";
            OnRefreshTimer(null, null);

            Utils.EndBusyOperation(mi_Form);
        }

        // ==========================================================

        /// <summary>
        /// In case of error an oscilloscope which supports :SYSTEM:ERROR? can display additional information (serie DS1000Z)
        /// FormTransfer calls StartBusyOperation() before calling this function
        /// </summary>
        public void SendManualCommand(String s_Command, TextBox i_TextReponse)
        {
            mi_RefreshTimer.Stop();
            try
            {
                // There may be multiple "last errors" in the queue. Delete them all before sending the command.
                while (mi_Rigol.GetLastError() != null)
                {
                }

                i_TextReponse.Text = mi_Scpi.SendStringCommand(s_Command, 2000);
                i_TextReponse.ForeColor = Color.Black;
            }
            catch (TimeoutException)
            {
                String s_ErrorCause = mi_Rigol.GetLastError();
                if (s_ErrorCause != null)
                {
                    i_TextReponse.Text = s_ErrorCause;
                    i_TextReponse.ForeColor = Color.Red;
                }
                else 
                {
                    // It is not an error if a command like ":STOP" does not send a response
                    i_TextReponse.Text = "No response received.";
                    i_TextReponse.ForeColor = Color.Black;    
                }
            }
            catch (Exception Ex)
            {
                i_TextReponse.Text = Ex.Message;
                i_TextReponse.ForeColor = Color.Red;
            }
            OnRefreshTimer(null, null);
        }

        // ==========================================================================
        //                                  Capture
        // ==========================================================================

        private void btnCapture_Click(object sender, EventArgs e)
        {
            if (btnCapture.Text == "Abort")
            {
                StopCapture();
                mi_Form.PrintStatus("Capture aborted", Color.Black);
                lblProgress.Text = "";
                return;
            }

            if (!Utils.StartBusyOperation(mi_Form))
                return;

            try
            {
                Utils.RegWriteString(eRegKey.CaptureInterval, comboInterval .Text);
                Utils.RegWriteString(eRegKey.CaptureTotTime,  comboTotalTime.Text);

                decimal d_Interval = Utils.ParseInterval(comboInterval .Text); // in seconds
                decimal d_TotTime  = Utils.ParseInterval(comboTotalTime.Text); // in seconds

                // s64_Interval = 1 second   ... 5 minutes
                // s64_TotTime  = 10 minutes ... 10 days
                mi_DcCapture = mi_Rigol.CreateDcCapture(d_Interval, d_TotTime); // throws
                ms32_CaptureIdx = 0;

                Utils.FormMain.StoreNewCapture(mi_DcCapture);

                comboInterval .Enabled = false;
                comboTotalTime.Enabled = false;
                btnCapture.Text = "Abort";
                mi_Form.PrintStatus("Do not touch the oscilloscope while capturing!", Color.Red);

                mi_CaptureTimer.Interval = (int)(d_Interval * 1000);
                mi_CaptureTimer.Start();    // FIRST
                OnCaptureTimer(null, null); // AFTER (stops timer on error)
            }
            catch (Exception Ex)
            {
                StopCapture();                       // FIRST (stop timer)
                Utils.ShowExceptionBox(mi_Form, Ex); // NEXT  (show messagebox)
                lblProgress.Text = "";               // LAST  (remove progress where error occurred)
            }
        }

        void OnCaptureTimer(object sender, EventArgs e)
        {
            try
            {
                // Avoid that the operating system goes into sleep mode while capturing data
                PlatformManager.Instance.PreventSleep();

                mi_Rigol.AddDcSample(mi_DcCapture, ms32_CaptureIdx); // throws
                Utils.OsziPanel.RecalculateEverything();

                // Increment only if AddDcSample() did not throw an exception
                ms32_CaptureIdx ++;

                if (ms32_CaptureIdx == mi_DcCapture.ms32_Samples)
                {
                    StopCapture();   // FIRST
                    mi_Form.Close(); // AFTER
                    return;
                }

                String s_Progess = String.Format("{0}%  ({1} samples of {2})", 
                                                 100 * ms32_CaptureIdx / mi_DcCapture.ms32_Samples,
                                                 ms32_CaptureIdx, mi_DcCapture.ms32_Samples);

                decimal d_ElapsedMilli = (ms32_CaptureIdx - 1) * mi_CaptureTimer.Interval;
                if (d_ElapsedMilli > 0)
                    s_Progess += "  Elapsed: " + Utils.FormatTimePico(d_ElapsedMilli / 1000 * Utils.PICOS_PER_SECOND);

                lblProgress.Text = s_Progess;
            }
            catch (Exception Ex)
            {
                StopCapture();                       // FIRST (stop timer)
                Utils.ShowExceptionBox(mi_Form, Ex); // NEXT  (show messagbox)
                lblProgress.Text = "";               // LAST  (remove progress where error occurred)
            }
        }

        void StopCapture()
        {
            mi_CaptureTimer.Stop();
            btnCapture .Text = "DC Capture";
            comboInterval .Enabled = true;
            comboTotalTime.Enabled = true;

            if (mi_DcCapture != null)
            {
                // If the user aborts capturing --> remove all unused samples that have zero Volt.
                mi_DcCapture.ms32_Samples = Math.Max(Utils.MIN_VALID_SAMPLES, ms32_CaptureIdx);
                Utils.FormMain.StoreNewCapture(mi_DcCapture);
                mi_DcCapture = null;
            }

            OnRefreshTimer(null, null);
            Utils.EndBusyOperation(mi_Form);
        }
    }
}
