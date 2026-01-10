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
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

using delInvokeBool     = OsziWaveformAnalyzer.Utils.delInvokeBool;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using ComboPath         = OsziWaveformAnalyzer.Utils.ComboPath;
using DispFactor        = OsziWaveformAnalyzer.OsziPanel.DispFactor;
using ExImportManager   = ExImport.ExImportManager;
using eSaveAs           = ExImport.ExImportManager.eSaveAs;
using OperationManager  = Operations.OperationManager;
using TransferManager   = Transfer.TransferManager;
using PlatformManager   = Platform.PlatformManager;

namespace OsziWaveformAnalyzer
{
    public partial class FormMain : Form
    {
        // Used when starting for the first time (no registry entries exist yet)
        const int DEFAULT_ANAL_SEPARATE_HEIGHT = 150; // pixel
        const int DEFAULT_ANAL_COMMON_HEIGHT   = 300; // pixel
        const int DEFAULT_DIGITAL_HEIGHT       =  35; // pixel

        bool             mb_Abort;
        bool             mb_InitDone;
        ExImportManager  mi_ExImport;
        Timer            mi_StatusTimer  = new Timer();
        Timer            mi_CmdLineTimer = new Timer();
        CheckBox[]       mi_CheckBoxes   = new CheckBox[0];
        ComboPath        mi_CurInputFile;

        public FormMain()
        {
            InitializeComponent();

            // This array must contain the checkboxes in the order from left to right
            mi_CheckBoxes = new CheckBox[] { checkSepChannels, checkLegend };

            pictureLogo.Image    = Utils.LoadResourceImage("Logo.png");
            pictureLogo.SizeMode = PictureBoxSizeMode.StretchImage;
        }

        protected override void OnLoad(EventArgs e)
        {
            try
            {
                base.OnLoad(e);

                Utils.Init(this, osziPanel);

                mi_ExImport = new ExImportManager (textFileName, comboOsziModel);

                osziPanel.Init(lblInfo, lblDispSamples, checkSepChannels);
                osziPanel.KeyDown += new KeyEventHandler(OnOsziPanelKeyDown);

                statusLabel   .Text = "";
                lblDispSamples.Text = "";
                lblInfo       .Text = "No samples loaded";

                checkLegend     .Checked = Utils.RegReadBool(eRegKey.ShowLegend,      true);
                checkSepChannels.Checked = Utils.RegReadBool(eRegKey.SeparateChannel, true);

                eRegKey e_KeyAnalog = checkSepChannels.Checked ? eRegKey.AnalogHeightSeparate : eRegKey.AnalogHeightCommon;
                int     s32_Default = checkSepChannels.Checked ? DEFAULT_ANAL_SEPARATE_HEIGHT : DEFAULT_ANAL_COMMON_HEIGHT;
                trackAnalogHeight.Minimum  = OsziPanel.MIN_ANALOG_HEIGHT;
                trackAnalogHeight.Maximum  = OsziPanel.MAX_ANALOG_HEIGHT;
                trackAnalogHeight.Value    = Utils.RegReadInteger(e_KeyAnalog, s32_Default);
                trackAnalogHeight.Visible  = false;

                trackDigitalHeight.Minimum = OsziPanel.MIN_DIGITAL_HEIGHT;
                trackDigitalHeight.Maximum = OsziPanel.MAX_DIGITAL_HEIGHT;
                trackDigitalHeight.Value   = Utils.RegReadInteger(eRegKey.DigitalHeight, DEFAULT_DIGITAL_HEIGHT);
                trackDigitalHeight.Visible = false;

                lblAnalogHeight .Text      = "";
                lblDigitalHeight.Text      = "You can right-click into the graph";
                lblDigitalHeight.ForeColor = Color.PeachPuff;

                mi_StatusTimer.Tick += new EventHandler(OnStatusTimer);
                mi_StatusTimer.Interval = 7000;

                mi_CmdLineTimer.Tick += new EventHandler(OnCmdLineTimer);
                mi_CmdLineTimer.Interval = 100;

                // Any future oscilloscope models have to be added to the class CaptureManager
                TransferManager.FillComboOsziModel(comboOsziModel);

                // Any future ex/imports have to be added to the class ExImportManager
                ExImportManager.FillComboSaveAs(comboSaveAs);

                Utils.LoadWndPosFromRegistry(this, eRegKey.MainWindow);

                String s_Title = "Oszi Waveform Analyzer — Capture, Display, A/D, Logicanalyzer " + Utils.APP_VERSION + " by ElmüSoft";
                #if DEBUG
                    s_Title += "  [DEBUG VERSION]";
                #endif
                this.Text = s_Title;

                btnRefresh_Click(null, null);
                mb_InitDone = true;
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex, "Crash in OnLoad()");
            }
        }

        /// <summary>
        /// Makes the main window visible on the screen
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            mi_CmdLineTimer.Start();
        }

        // --------------------------------------------------

        /// <summary>
        /// IMPORTANT: This must be called from a timer!
        /// Loading an OSZI file with 24 Megasamples may take several seconds.
        /// Meanwhile the user does not even see the main window if OpenCommandlineFile() is called directly from OnLoad().
        /// </summary>
        void OnCmdLineTimer(object sender, EventArgs e)
        {
            mi_CmdLineTimer.Stop();

            if (Program.CmdOpenFile != null)
                OpenCommandlineFile(Program.CmdOpenFile);
        }

        /// <summary>
        /// The pipeserver has received a request from another process to open an Oszi file
        /// </summary>
        public bool RemoteOpenRequest(String s_OsziPath)
        {
            // This is called from the PipeServerThread --> invoke into GUI thread
            return (bool)Invoke((delInvokeBool)delegate() 
            {
                if (OsziPanel.CurCapture != null && OsziPanel.CurCapture.mb_Dirty)
                {
                    String s_Filename = Path.GetFileName(s_OsziPath);
                    DialogResult e_Result = MessageBox.Show(this, "You have unsaved changes.\n"
                                          + "Click OK to lose the changes and load the file\n" + s_Filename
                                          + "\nClick Cancel to open the file in another process.", 
                                            "Oszi Waveform Analyer", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);

                    if (e_Result == DialogResult.Cancel)
                        return false;
                }
                OpenCommandlineFile(s_OsziPath);
                return true;
            });
        }

        /// <summary>
        /// Open the file that has been passed by commandline after a double click in Explorer
        /// </summary>
        void OpenCommandlineFile(String s_OsziPath)
        {
            // Change the working directory with the capture files to the directory that the user has clicked.
            Utils.SampleDir = Path.GetDirectoryName(s_OsziPath);

            // reload combobox with the files from the new directory
            mi_ExImport.LoadComboInput(comboInput);

            // If the user double clicked a file in subfolder "Samples" it is already in the ComboBox --> select it.
            foreach (ComboPath i_Exist in comboInput.Items)
            {
                if (String.Compare(i_Exist.ms_Path, s_OsziPath, true) == 0)
                {
                    mi_CurInputFile = null;            // FIRST
                    comboInput.SelectedItem = i_Exist; // AFTER
                    break;
                }
            }
        }

        // --------------------------------------------------

        protected override void OnClosing(CancelEventArgs e)
        {
 	        base.OnClosing(e);
            
            if (Utils.IsBusy)
            {
                e.Cancel = true;
                MessageBox.Show(this, "An operation is still in progress.\nAbort the operation to close the window.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);

            if (mb_InitDone && WindowState == FormWindowState.Normal)
                Utils.RegWriteRectangle(eRegKey.MainWindow, Bounds);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            if (mb_InitDone && WindowState == FormWindowState.Normal)
                Utils.RegWriteRectangle(eRegKey.MainWindow, Bounds);

            statusLabel.Width = ClientSize.Width - 20;
            osziPanel.RecalculateEverything();

            AdjustLabelInfo();
        }

        /// <summary>
        /// Adjust the with of lblInfo
        /// </summary>
        void AdjustLabelInfo()
        {
            int s32_Width = ClientSize.Width;
            foreach (CheckBox i_Box in mi_CheckBoxes)
            {
                if (i_Box.Visible)
                {
                    s32_Width = i_Box.Left; // left-most visible checkbox
                    break;
                }
            }
            lblInfo.Width = s32_Width - lblInfo.Left - 5;
        }

        private void linkSave_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "SaveOptions");
        }

        private void linkUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowURL(this, "https://netcult.ch/elmue/Oszi-Waveform-Analyzer");
        }

        // --------------------------------------------------

        private void trackAnalogHeight_Scroll(object sender, EventArgs e)
        {
            eRegKey e_KeyAnalog = checkSepChannels.Checked ? eRegKey.AnalogHeightSeparate : eRegKey.AnalogHeightCommon;
            Utils.RegWriteInteger(e_KeyAnalog, trackAnalogHeight.Value);
            UpdateTrackbars();
            osziPanel.RecalculateEverything();
            osziPanel.Focus();
        }

        private void trackDigital_Scroll(object sender, EventArgs e)
        {
            Utils.RegWriteInteger(eRegKey.DigitalHeight, trackDigitalHeight.Value);
            UpdateTrackbars();
            osziPanel.RecalculateEverything();
            osziPanel.Focus();
        }

        private void UpdateTrackbars()
        {
            trackAnalogHeight .Visible = true;
            trackDigitalHeight.Visible = true;

            osziPanel.AnalogHeight  = trackAnalogHeight.Value;
            osziPanel.DigitalHeight = trackDigitalHeight.Value;
            
            lblAnalogHeight .Text = "Analog Height: "   + trackAnalogHeight.Value  + " pixel";
            lblDigitalHeight.Text = "Digital  Height: " + trackDigitalHeight.Value + " pixel";
            lblDigitalHeight.ForeColor = Color.White;
            linkUpdate.Visible = false;
        }

        // --------------------------------------------------

        private void checkSepChannels_CheckedChanged(object sender, EventArgs e)
        {
            if (mb_InitDone)
            {
                Utils.RegWriteBool(eRegKey.SeparateChannel, checkSepChannels.Checked);

                eRegKey e_KeyAnalog = checkSepChannels.Checked ? eRegKey.AnalogHeightSeparate : eRegKey.AnalogHeightCommon;
                int     s32_Default = checkSepChannels.Checked ? DEFAULT_ANAL_SEPARATE_HEIGHT : DEFAULT_ANAL_COMMON_HEIGHT;
                trackAnalogHeight.Value = Utils.RegReadInteger(e_KeyAnalog, s32_Default);
                trackAnalogHeight_Scroll(null, null);
            }

            osziPanel.RecalculateEverything(); // does nothing if no Capture loaded
        }

        private void checkLegend_CheckedChanged(object sender, EventArgs e)
        {
            if (mb_InitDone)
                Utils.RegWriteBool(eRegKey.ShowLegend, checkLegend.Checked);

            osziPanel.ShowLegend = checkLegend.Checked;
            osziPanel.RecalculateEverything(); // does nothing if no Capture loaded
        }

        // -----------------------------------------------------

        void OnOsziPanelKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                // Arrow Left + Arrow Right are handled in OsziPanel
                case Keys.Up:
                    comboFactor.SelectedIndex = Math.Max(comboFactor.SelectedIndex - 1, 0);
                    break;
                case Keys.Down:
                    comboFactor.SelectedIndex = Math.Min(comboFactor.SelectedIndex + 1, comboFactor.Items.Count - 1);
                    break;
            }
        }

        private void comboFactor_SelectedIndexChanged(object sender, EventArgs e)
        {
            DispFactor i_Factor = (DispFactor)comboFactor.SelectedItem;
            if (i_Factor == null)
                return;

            osziPanel.DispSteps = i_Factor.ms32_DispSteps;
            osziPanel.Zoom      = i_Factor.ms32_Zoom;

            if (osziPanel.DispSteps == 1) // 1:1 or zoom mode
                checkSaveFactor.Checked = false;

            mi_ExImport.ms32_SaveSteps = checkSaveFactor.Checked ? i_Factor.ms32_DispSteps : 1;
            tabControl.SelectedTab = tabOszi;
            osziPanel.RecalculateEverything(true);
            osziPanel.Focus();
        }

        private void checkSaveFactor_CheckedChanged(object sender, EventArgs e)
        {
            if (checkSaveFactor.Checked && osziPanel.Zoom > 1 && 
                (mi_ExImport.me_SaveAs == eSaveAs.OsziFilePlain || 
                 mi_ExImport.me_SaveAs == eSaveAs.OsziFileZip))
            {
                MessageBox.Show(Utils.FormMain, "The zoom factor is only used for display.\n"
                                              + "It cannot be applied when saving an OSZI file.\n"
                                              + "You can only select a shrink factor to reduce file size.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                checkSaveFactor.Checked = false;
                return;
            }

            if (checkSaveFactor.Checked)
            {
                lblFactorHint.Text = "Used for display\nand save to OSZI file";
                lblFactorHint.ForeColor = Color.Yellow;
            }
            else
            {
                lblFactorHint.Text = "Press ALT + F\nand arrow up/down";
                lblFactorHint.ForeColor = Color.PeachPuff;
            }

            mi_ExImport.ms32_SaveSteps = checkSaveFactor.Checked ? osziPanel.DispSteps : 1;
        }

        private void comboSaveAs_SelectedIndexChanged(object sender, EventArgs e)
        {
            mi_ExImport.me_SaveAs = (eSaveAs)comboSaveAs.SelectedIndex;
            switch (mi_ExImport.me_SaveAs)
            {
                case eSaveAs.Screenshot:
                case eSaveAs.FullImage:
                case eSaveAs.RtfFile:
                    checkSaveFactor.Visible = false;
                    break;
                default:
                    if (osziPanel.Zoom > 1)
                        checkSaveFactor.Checked = false;

                    checkSaveFactor.Visible = true;
                    break;
            }
        }

        // -----------------------------------------------------

        /// <summary>
        /// Reload the combobox with CSV and SOCI file
        /// </summary>
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (btnRefresh.Text == "Abort")
            {
                mb_Abort = true; // Abort file loading
                return;
            }

            if (!Utils.StartBusyOperation(this))
                return;

            mi_ExImport.LoadComboInput(comboInput);

            Utils.EndBusyOperation(this);
        }

        /// <summary>
        /// Called when the user has delected all Channels of a Capture
        /// </summary>
        public void ResetInputFile()
        {
            comboInput.SelectedIndex = -1;
            textFileName.Text = "";
        }

        /// <summary>
        /// Read CSV file (very slow) or OSZI file (binary samples)
        /// </summary>
        private void comboInput_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ((ComboPath)comboInput.SelectedItem == mi_CurInputFile)
                return;
            
            if (mi_CurInputFile != null && OsziPanel.CurCapture != null && OsziPanel.CurCapture.mb_Dirty)
            {
                DialogResult e_Result = MessageBox.Show(this, "You have unsaved changes.\n"
                                        + "Do you want to lose them and open another file?", 
                                          "Oszi Waveform Analyer", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                if (e_Result == DialogResult.No)
                {
                    comboInput.DroppedDown  = false;
                    comboInput.SelectedItem = mi_CurInputFile;
                    return;
                }
            }

            mi_CurInputFile = (ComboPath)comboInput.SelectedItem;

            if (mi_CurInputFile == null || mi_CurInputFile.ms_Path == null)
            {
                if (comboInput.Items.Count == 0)
                    PrintStatus("No CSV or OSZI files found in " + Utils.SampleDir, Color.Red);

                StoreNewCapture(null);
                return;
            }

            ImportFile(mi_CurInputFile.ms_Path);
            osziPanel.Focus();
        }

        /// <summary>
        /// Load .OSZI or .CSV file
        /// </summary>
        void ImportFile(String s_Path)
        {
            if (!Utils.StartBusyOperation(this))
                return;

            mb_Abort = false;
            comboFactor.Items.Clear();
            comboInput.Enabled = false;
            btnRefresh.Text = "Abort";
            lblInfo   .Text = "";
            Application.DoEvents();

            try
            {
                StoreNewCapture(null); // show black screen

                Capture i_NewCapture = mi_ExImport.Import(s_Path, ref mb_Abort);

                StoreNewCapture(i_NewCapture);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
            }

            Utils.EndBusyOperation(this);
            btnRefresh.Text = "Refresh";

            comboInput.Enabled = true;
            if (OsziPanel.CurCapture == null)
            {
                comboInput.SelectedIndex = -1;
                PrintStatus("Aborted", Color.Red);
            }
        }

        // -----------------------------------------------------

        /// <summary>
        /// Load a completly new Capture class with new Channels coming form a file or from USB.
        /// i_Capture = null --> show black screen
        /// </summary>
        public void StoreNewCapture(Capture i_Capture)
        {
            if (i_Capture != null && i_Capture.ms32_Samples < Utils.MIN_VALID_SAMPLES)
            {
                MessageBox.Show(this, Utils.ERR_MIN_SAMPLES, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            rtfViewer.Clear();
            tabControl.SelectedTab = tabOszi;

            // Only store the Capture, do not yet display it.
            // First osziPanel.ms32_DispSteps must be set
            osziPanel.StoreCapture(i_Capture); 

            if (i_Capture != null) 
            {
                comboFactor.BeginUpdate();
                comboFactor.Items.Clear();

                // Add fix zoom factors
                comboFactor.Items.Add(new DispFactor(true, 20));
                comboFactor.Items.Add(new DispFactor(true, 10));
                comboFactor.Items.Add(new DispFactor(true,  5));
                comboFactor.Items.Add(new DispFactor(true,  2));
                comboFactor.Items.Add(new DispFactor(true,  1));

                // Add dynamic shrink factors
                int[] s32_Steps = new int[] { 2, 5, 10 };                
                bool  b_Abort   = false;
                for (int s32_Multiply=1; !b_Abort; s32_Multiply *= 10)
                {
                    foreach (int s32_Step in s32_Steps)
                    {                   
                        int s32_Factor = s32_Step * s32_Multiply;
                        comboFactor.Items.Add(new DispFactor(false, s32_Factor));

                        // The minimum display size is 1000 pixel
                        if (i_Capture.ms32_Samples < s32_Factor * 1000)
                        {
                            b_Abort = true;
                            break;
                        }
                    }
                }
                comboFactor.EndUpdate();
                Utils.ComboAdjustDropDownWidth(comboFactor);

                // Select a low resolution, so the entire waveform is visible after loading a new capture.
                // The next line will set osziPanel.ms32_DispSteps in the event handler.
                comboFactor.SelectedIndex = Math.Max(0, comboFactor.Items.Count - 2);

                if (i_Capture.ms_Path == null) // Capture does not come from a file --> remove file name
                    comboInput.SelectedIndex = -1;

                // Make trackbars visible after the first loaded samples
                UpdateTrackbars();
            }
            
            osziPanel.RecalculateEverything();
            UpdateDirty();

            // This must be after RecalculateEverything() has calculated ms32_AnalogCount
            checkSepChannels.Visible = i_Capture != null && i_Capture.ms32_AnalogCount > 1;

            AdjustLabelInfo();

            if (i_Capture != null)
            {
                String s_Mesg = String.Format("Loaded {0} channels with {1:N0} samples", i_Capture.mi_Channels.Count, i_Capture.ms32_Samples);
                if (i_Capture.ms_Path != null) s_Mesg += " from " + i_Capture.ms_Path;
                PrintStatus(s_Mesg, Color.Green);
            }
        }

        public void ShowAnalysisResult(RtfDocument i_Result, int s32_SamplesPerBit)
        {
            AdjustDisplayFactor(s32_SamplesPerBit);

            if (i_Result == null || i_Result.IsEmpty)
            {
                rtfViewer.Clear();
                tabControl.SelectedTab = tabOszi;
                return;
            }

            rtfViewer.Rtf = i_Result.BuildRTF();
            tabControl.SelectedTab = tabDecoder;
        }

        /// <summary>
        /// s32_SamplesPerBit:
        /// In case of self sync data (CAN bus, UART, IR, MagStripe) this corresponds to one bit.
        /// In case of synchronous data (SPI, I2C) this corresponds to a clock period.
        /// Tries to auto-select a Display Factor, so that one digital bit corresponds to approx 25 pixels on the screen.
        /// </summary>
        public void AdjustDisplayFactor(int s32_SamplesPerBit)
        {
            if (s32_SamplesPerBit == 0)
                return;

            double d_Steps    = s32_SamplesPerBit / 25.0;
            double d_MinDiff  = double.MaxValue;
            DispFactor i_Best = null;

            foreach (DispFactor i_Factor in comboFactor.Items)
            {
                if (i_Factor.ms32_Zoom > 1)
                    continue;

                double d_Diff = Math.Abs(i_Factor.ms32_DispSteps - d_Steps);
                if (d_Diff < d_MinDiff)
                {
                    d_MinDiff = d_Diff;
                    i_Best = i_Factor;
                }
            }

            if (i_Best != null)
                comboFactor.SelectedItem = i_Best;
        }

        public String SaveRichText(String s_Path)
        {
            String s_RTF = rtfViewer.Rtf;
            if (String.IsNullOrEmpty(s_RTF))
            {
                MessageBox.Show(this, "Nothing to be saved.\nThere is no decoder result.", "Error", 
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                tabControl.SelectedTab = tabDecoder;
                return null;
            }

            try
            {
                File.WriteAllText(s_Path, s_RTF, Utils.ENCODING_ANSI); // NOT UTF8 !!!
                return "decoder result";
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
                return null;
            }
        }

        public void SwitchToTab(int s32_Index)
        {
            tabControl.SelectedIndex = s32_Index;
        }

        // -----------------------------------------------------

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!Utils.CheckMinSamples())
                return;

            // Asks if existing file may be overwritten
            String s_Path = mi_ExImport.GetSavePath();
            if (s_Path == null)
                return;

            if (!Utils.StartBusyOperation(this))
                return;
            try
            {
                mi_ExImport.Export(s_Path);

                // Always turn this checkbox off, so the user has to set it manually each time again.
                // This avoids that the user saves an OSZI file in low resolution without wanting it explicitely.
                // The default save mode is always to save the higehst resolution that is possible.
                checkSaveFactor.Checked = false;
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
            }

            Utils.EndBusyOperation(this);
        }

        // -----------------------------------------------------

        private void btnCapture_Click(object sender, EventArgs e)
        {
            if (!Utils.StartBusyOperation(this))
                return;

            // The Capture Forms have their own Busy management.
            Utils.EndBusyOperation(this);

            textFileName.Text = "";

            try
            {
                // Shows a modal Form
                TransferManager.Transfer(comboOsziModel);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
            }
        }

        // -----------------------------------------------------

        /// <summary>
        /// Display "Oszi *" in the tab if the user has unsaved changes
        /// </summary>
        public void UpdateDirty()
        {
            tabOszi.Text = (OsziPanel.CurCapture != null && OsziPanel.CurCapture.mb_Dirty) ? "Oszi *" : "Oszi";
        }

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
    }
}
