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
using ComboPath         = OsziWaveformAnalyzer.ComboBoxEx.ComboPath;
using DispFactor        = OsziWaveformAnalyzer.PanelOszi.DispFactor;
using ExImportManager   = ExImport.ExImportManager;
using eSaveAs           = ExImport.ExImportManager.eSaveAs;
using OperationManager  = Operations.OperationManager;
using TransferManager   = Transfer.TransferManager;
using PlatformManager   = Platform.PlatformManager;
using AudioFile         = ExImport.AudioFile;
using IAudioInDevice    = Platform.PlatformManager.IAudioInDevice;
using FormRecordAudio   = Transfer.FormRecordAudio;

namespace OsziWaveformAnalyzer
{
    public partial class FormMain : Form
    {
        #region enums

        public enum eTab
        {
            Invalid = -1,
            Oszi = 0,
            XYPlot,
            FFT,
            Decoder,
        }

        #endregion

        #region IPanelAction

        public interface IPanelAction
        {
            // called when the user has switched to another tab
            // called with null when the old capture has been deleted 
            // called with valid Capture when a new capture has been loaded
            void OnUserAction(eTab e_ActiveTab, Capture i_Capture);
        }

        #endregion

        bool             mb_Abort;
        bool             mb_InitDone;
        ExImportManager  mi_ExImport;
        ComboPath        mi_CurInputFile;
        Timer            mi_StatusTimer  = new Timer();
        Timer            mi_CmdLineTimer = new Timer();
        CtrlStartup      mi_CtrlStartup;
        CtrlOszi         mi_CtrlOszi;
        CtrlXY           mi_CtrlXY;
        CtrlFFT          mi_CtrlFFT;
        IPanelAction[]   mi_PanelActions;
        String           ms_DecoderLabel;
        ComboBoxEx       mi_ComboFactor;
        Label            mi_LblFactorHint;
        Label            mi_LblDispSamples;

        public FormMain()
        {
            InitializeComponent();

            pictureLogo.Image    = Utils.LoadResourceImage("Logo.png");
            pictureLogo.SizeMode = PictureBoxSizeMode.StretchImage;
        }

        protected override void OnLoad(EventArgs e)
        {
            try
            {
                base.OnLoad(e);
                Utils.Init(this, panelOszi, panelXY, panelFFT);

                CheckBox i_CheckSepChannels;
                mi_CtrlStartup  = new CtrlStartup();
                mi_CtrlOszi     = new CtrlOszi(toolTip, out i_CheckSepChannels, out mi_ComboFactor, out mi_LblFactorHint, out mi_LblDispSamples);
                mi_CtrlXY       = new CtrlXY  (toolTip, scrollBarXY);
                mi_CtrlFFT      = new CtrlFFT (toolTip);

                mi_PanelActions = new IPanelAction[] { panelOszi, panelXY, panelFFT, panelRtf, 
                                                       mi_CtrlStartup, mi_CtrlOszi, mi_CtrlXY, mi_CtrlFFT };

                mi_ComboFactor.SelectedIndexChanged += new EventHandler(comboFactor_SelectedIndexChanged);

                // ------------------------------

                panelXY  .Init(scrollBarXY);
                panelFFT .Init();
                panelOszi.Init(mi_LblDispSamples, i_CheckSepChannels, mi_ComboFactor);
                panelOszi.KeyDown += new KeyEventHandler(OnOsziPanelKeyDown);

                // ------------------------------

                mi_ExImport = new ExImportManager (textFileName, comboOsziModel);

                statusLabel      .Text = "";
                mi_LblDispSamples.Text = "";
                SetLabel("No samples loaded");
                
                mi_StatusTimer.Tick += new EventHandler(OnStatusTimer);
                mi_StatusTimer.Interval = 7000;

                mi_CmdLineTimer.Tick += new EventHandler(OnCmdLineTimer);
                mi_CmdLineTimer.Interval = 100;

                // Any future oscilloscope models have to be added to the class CaptureManager
                TransferManager.FillComboOsziModel(comboOsziModel);

                // Any future ex/imports have to be added to the class ExImportManager
                ExImportManager.FillComboSaveAs(comboSaveAs);

                SetButtonTooltips();

                Utils.LoadWndPosFromRegistry(this, eRegKey.MainWindow);

                String s_Title = "Oszi Waveform Analyzer — Capture, Display, A/D, Logicanalyzer " + Utils.APP_VERSION + " by ElmüSoft";
                #if DEBUG
                    s_Title += "  [DEBUG VERSION]";
                #endif
                this.Text = s_Title;

                AdjustTabPanels();

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
                if (Utils.CurCapture != null && Utils.CurCapture.mb_Dirty)
                {
                    DialogResult e_Result = MessageBox.Show(this, "You have unsaved changes.\n"
                                          + "Click OK to lose the changes and load the file\n" 
                                          + Path.GetFileName(s_OsziPath)
                                          + "\nClick Cancel to open the file in another process.", 
                                            "Oszi Waveform Analyer", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);

                    if (e_Result == DialogResult.Cancel)
                        return false;

                    Utils.CurCapture.mb_Dirty = false;
                    UpdateDirty();
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
            SetUserDirectory(Path.GetDirectoryName(s_OsziPath));

            // Select the user file in the ComboBox
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

            if (HasUnsavedChanges(true))
            {
                e.Cancel = true;
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

            if (mb_InitDone)
            {
                if (WindowState == FormWindowState.Normal)
                    Utils.RegWriteRectangle(eRegKey.MainWindow, Bounds);

                AdjustTabPanels();
            }

            statusLabel.Width = ClientSize.Width - 20;
        }

        void OnTabControl_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (mb_InitDone)
                AdjustTabPanels();
        }

        /// <summary>
        /// Called from OnInit(), on Tabe change, on resize and after loading a new capture
        /// </summary>
        void AdjustTabPanels()
        {
            eTab e_Tab = (eTab)tabControl.SelectedIndex;
            switch (e_Tab)
            {
                case eTab.Oszi:
                    panelOszi.RecalculateEverything();
                    panelOszi.Visible = (Utils.CurCapture != null);
                    break;

                case eTab.XYPlot:
                    int X  = (tabXY.Width - tabXY.Height) / 2;
                    int s32_Size    = tabXY.Height - scrollBarXY.Height;
                    panelXY.Bounds  = new Rectangle(X, 0, s32_Size, s32_Size);
                    panelXY.Visible = (Utils.CurCapture != null);
                    break;

                case eTab.FFT:
                    panelFFT.Bounds  = new Rectangle(0, 0, tabFFT.Width, tabFFT.Height);
                    panelFFT.Visible = (Utils.CurCapture != null);
                    break;

                case eTab.Decoder:
                    if (ms_DecoderLabel != null) SetLabel(ms_DecoderLabel);
                    else                         SetLabel("No decoder result");
                    panelRtf.Visible = (Utils.CurCapture != null);
                    break;
            }

            lblInfo.Width = ClientSize.Width - lblInfo.Left - 5;

            if (Utils.CurCapture == null) 
            {
                SetLabel("No samples loaded");

                pictureOszi.Parent   = tabControl.SelectedTab;
                pictureOszi.Location = new Point((tabControl.SelectedTab.Width  - pictureOszi.Width)  / 2,
                                                 (tabControl.SelectedTab.Height - pictureOszi.Height) / 2);
                pictureOszi.Visible = true;
            }
            else pictureOszi.Visible = false;

            foreach (Control i_Panel in mi_PanelActions)
            {
                if (i_Panel.Parent == null) // a MainCtrl Panel is shown for the first time
                {
                    i_Panel.Location = new Point(groupSave.Right + 6, groupSave.Top);
                    Controls.Add(i_Panel);
                }
                ((IPanelAction)i_Panel).OnUserAction(e_Tab, Utils.CurCapture);
            }
        }

        private void linkSave_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "SaveOptions");
        }

        // -----------------------------------------------------

        void OnOsziPanelKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                // Arrow Left + Arrow Right are handled in OsziPanel
                case Keys.Up:
                    mi_ComboFactor.SelectedIndex = Math.Max(mi_ComboFactor.SelectedIndex - 1, 0);
                    break;
                case Keys.Down:
                    mi_ComboFactor.SelectedIndex = Math.Min(mi_ComboFactor.SelectedIndex + 1, mi_ComboFactor.Items.Count - 1);
                    break;
            }
        }

        private void comboFactor_SelectedIndexChanged(object sender, EventArgs e)
        {
            DispFactor i_Factor = (DispFactor)mi_ComboFactor.SelectedItem;
            if (i_Factor == null)
                return;

            panelOszi.DispSteps = i_Factor.ms32_DispSteps;
            panelOszi.Zoom      = i_Factor.ms32_Zoom;

            if (panelOszi.DispSteps == 1) // 1:1 or zoom mode
                checkApplyFactor.Checked = false;

            panelOszi.RecalculateEverything(true);

            // if the user has set a cursor make sure it is visible after zooming in
            if (Utils.PanelOszi.CursorSample > 0)
                Utils.PanelOszi.JumpToSample(Utils.PanelOszi.CursorSample);

            panelOszi.Focus();
        }

        private void checkSaveFactor_CheckedChanged(object sender, EventArgs e)
        {
            eSaveAs e_SaveAs = (eSaveAs)comboSaveAs.SelectedIndex;
            if (checkApplyFactor.Checked && panelOszi.Zoom > 1 && 
                (e_SaveAs == eSaveAs.OsziFilePlain || 
                 e_SaveAs == eSaveAs.OsziFileZip))
            {
                MessageBox.Show(Utils.FormMain, "The zoom factor is only used for display.\n"
                                              + "It cannot be applied when saving an OSZI file.\n"
                                              + "You can only select a shrink factor to reduce file size.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                checkApplyFactor.Checked = false;
                return;
            }

            if (checkApplyFactor.Checked)
            {
                mi_LblFactorHint.Text = "Used for display\nand save to OSZI file";
                mi_LblFactorHint.ForeColor = Color.Yellow;
            }
            else
            {
                mi_LblFactorHint.Text = "Use Arrow up/down\n to change Factor";
                mi_LblFactorHint.ForeColor = Color.PeachPuff;
            }
        }

        private void comboSaveAs_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch ((eSaveAs)comboSaveAs.SelectedIndex)
            {
                case eSaveAs.Screenshot:
                case eSaveAs.FullImage:
                case eSaveAs.RtfFile:
                    checkApplyFactor.Visible = false;
                    checkShrink8Bit .Visible = false;
                    break;
                default:
                    if (panelOszi.Zoom > 1)
                        checkApplyFactor.Checked = false;

                    checkApplyFactor.Visible = true;
                    checkShrink8Bit .Visible = true;
                    break;
            }
        }

        // -----------------------------------------------------

        /// <summary>
        /// Reload the combobox with CSV and OSCI files
        /// </summary>
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if ((String)btnRefresh.Tag == "Abort")
            {
                mb_Abort = true; // Abort file loading
                return;
            }

            if (!Utils.StartBusyOperation(this))
                return;

            mi_ExImport.FillComboInput(comboInput);

            Utils.EndBusyOperation(this);
        }

        /// <summary>
        /// Called when the user has deleted all Channels of a Capture
        /// </summary>
        public void ResetInputFile()
        {
            comboInput.SelectedIndex = -1;
            textFileName.Text = "";
        }

        public bool HasUnsavedChanges(bool b_Closing = false)
        {
            if (Utils.CurCapture != null && Utils.CurCapture.mb_Dirty)
            {
                String s_NextAction = b_Closing ? "and close the program?" : "and load another capture?";

                DialogResult e_Result = MessageBox.Show(this, "You have unsaved changes.\n"
                                        + "Do you want to lose them and " + s_NextAction, 
                                          "Oszi Waveform Analyer", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                if (e_Result == DialogResult.No)
                    return true;

                Utils.CurCapture.mb_Dirty = false;
                UpdateDirty();
            }
            return false;
        }

        /// <summary>
        /// Read CSV file (very slow) or OSZI file (binary samples)
        /// </summary>
        private void comboInput_SelectedIndexChanged(object sender, EventArgs e)
        {
            // After transferring a capture from the oscilloscope the filename is removed from the combobox.
            if (comboInput.SelectedIndex < 0)
            {
                mi_CurInputFile = null;
                return;
            }

            // The user has dropped down the combobox and re-selected the same item --> do not load again
            if (mi_CurInputFile != null && mi_CurInputFile == (ComboPath)comboInput.SelectedItem)
                return;
            
            if (HasUnsavedChanges())
            {
                // restore previous combo selection
                comboInput.DroppedDown  = false;
                comboInput.SelectedItem = mi_CurInputFile;
                return;
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
            panelOszi.Focus();
        }

        /// <summary>
        /// Load .OSZI or .CSV file
        /// </summary>
        void ImportFile(String s_Path)
        {
            if (!Utils.StartBusyOperation(this))
                return;

            mb_Abort = false;
            mi_ComboFactor.Items.Clear();
            btnFolder .Enabled = false;
            comboInput.Enabled = false;
            SetButtonTooltips(); // show Abort image
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

            btnFolder .Enabled = true;
            comboInput.Enabled = true;
            SetButtonTooltips(); // Show Refresh image

            if (Utils.CurCapture == null)
            {
                comboInput.SelectedIndex = -1;
                PrintStatus("Aborted", Color.Red);
            }
        }

        /// <summary>
        /// Set tooltips for buttons Refresh, Folder and Save
        /// Set Image for button Refresh
        /// </summary>
        void SetButtonTooltips()
        {
            if (Utils.UserDir.Length == 0)
                toolTip.SetToolTip(btnFolder, "Select a second directory where to load files from.\n"
                                            + "Currently no second directory is selected.");
            else
                toolTip.SetToolTip(btnFolder, "Select a second directory where to load files from.\n"
                                            + "The current second directory is:\n" + Utils.UserDir);

            toolTip.SetToolTip(btnSave, "Save the file into the directory\n" + Utils.SampleDir);

            // The combobox is disabled in ImportFile() while loading --> show abort icon
            if (comboInput.Enabled)
            {
                btnRefresh.Tag   = "Refresh";
                btnRefresh.Image = Utils.LoadResourceImage("Refresh.png");

                if (Utils.UserDir.Length == 0)
                    toolTip.SetToolTip(btnRefresh, "Refresh the list 'Input File' reading the directory\n" + Utils.SampleDir
                                                 + "\nYou can select a second directory with the 'Folder' button");
                else
                    toolTip.SetToolTip(btnRefresh, "Refresh the list 'Input File' reading the directories\n" + Utils.SampleDir
                                                 + "\nand\n" + Utils.UserDir
                                                 + "\nYou can change the second directory with the 'Folder' button");
            }
            else
            {
                btnRefresh.Tag   = "Abort";
                btnRefresh.Image = Utils.LoadResourceImage("Abort.png");
                toolTip.SetToolTip(btnRefresh, "Abort loading");
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

            ms_DecoderLabel = null; // No decoder result

            // Clear the current capture in all panels
            foreach (IPanelAction i_Panel in mi_PanelActions)
            {
                // free memory of previous capture, clear tab content
                // Below AdjustTabPanels() is cxalled which will inform all panels about the new Capture
                i_Panel.OnUserAction((eTab)tabControl.SelectedIndex, null);
            }

            // The tabs Decoder and FFT are empty now --> switch to show the new capture
            if (tabControl.SelectedIndex != (int)eTab.Oszi && 
                tabControl.SelectedIndex != (int)eTab.XYPlot)
                tabControl.SelectedTab = tabOszi;

            Utils.CurCapture = i_Capture;
    
            if (i_Capture != null) 
            {
                mi_ComboFactor.BeginUpdate();
                mi_ComboFactor.Items.Clear();

                // Add fix zoom factors
                mi_ComboFactor.Items.Add(new DispFactor(true, 20));
                mi_ComboFactor.Items.Add(new DispFactor(true, 10));
                mi_ComboFactor.Items.Add(new DispFactor(true,  5));
                mi_ComboFactor.Items.Add(new DispFactor(true,  2));
                mi_ComboFactor.Items.Add(new DispFactor(true,  1));

                // Add dynamic shrink factors
                int[] s32_Steps = new int[] { 2, 5, 10 };                
                bool  b_Abort   = false;
                for (int s32_Multiply=1; !b_Abort; s32_Multiply *= 10)
                {
                    foreach (int s32_Step in s32_Steps)
                    {                   
                        int s32_Factor = s32_Step * s32_Multiply;
                        mi_ComboFactor.Items.Add(new DispFactor(false, s32_Factor));

                        // The minimum display size is 1000 pixel
                        if (i_Capture.ms32_Samples < s32_Factor * 1000)
                        {
                            b_Abort = true;
                            break;
                        }
                    }
                }
                mi_ComboFactor.EndUpdate();
                Utils.ComboAdjustDropDownWidth(mi_ComboFactor);

                // Select a low resolution, so the entire waveform is visible after loading a new capture.
                // The next line will set panelOszi.ms32_DispSteps in the event handler.
                mi_ComboFactor.SelectedIndex = Math.Max(0, mi_ComboFactor.Items.Count - 2);

                if (i_Capture.ms_Path == null) // Capture does not come from a file --> remove file name
                {
                    comboInput.SelectedIndex = -1;
                    i_Capture.mb_Dirty = true;
                }
            }
            
            panelOszi.RecalculateEverything();
            UpdateDirty();

            // avoid displaying a black panel if the new file does not support X/Y plot
            if (tabControl.SelectedIndex == (int)eTab.XYPlot && i_Capture != null && i_Capture.ms32_AnalogCount != 2)
                tabControl.SelectedTab = tabOszi;

            // Call this after RecalculateEverything() has calculated ms32_AnalogCount
            AdjustTabPanels();

            if (i_Capture != null)
            {
                String s_Mesg = String.Format("Loaded {0} channels with {1:N0} samples", i_Capture.mi_Channels.Count, i_Capture.ms32_Samples);
                if (i_Capture.ms_Path != null) s_Mesg += " from " + i_Capture.ms_Path;
                PrintStatus(s_Mesg, Color.Green);
            }
        }

        /// <summary>
        /// s32_SamplesPerBit == 0 --> do not change display factor
        /// </summary>
        public void ShowAnalysisResult(RtfDocument i_Result, int s32_SamplesPerBit, String s_Decoder)
        {
            panelOszi.SetDisplayFactorForBit(s32_SamplesPerBit);

            if (i_Result == null || i_Result.IsEmpty)
            {
                panelRtf.Clear();
                // This must be called also in case if nothing was detected to show any added digital channels.
                panelOszi.RecalculateEverything();
                ms_DecoderLabel = null; // No decoder result
            }
            else
            {
                panelRtf.Rtf = i_Result.BuildRTF();
                tabControl.SelectedTab = tabDecoder;
                ms_DecoderLabel = s_Decoder;
                SetLabel(s_Decoder);
            }
        }

        public String SaveRichText(String s_Path)
        {
            if (ms_DecoderLabel == null)
            {
                MessageBox.Show(this, "No decoder result to be saved.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                tabControl.SelectedTab = tabDecoder; // show the empty tab to the user
                return null;
            }

            try
            {
                File.WriteAllText(s_Path, panelRtf.Rtf, Utils.ENCODING_ANSI); // NOT UTF8 !!!
                return "decoder result";
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
                return null;
            }
        }

        public void SwitchToTab(eTab e_Tab)
        {
            tabControl.SelectedIndex = (int)e_Tab;
        }

        // -----------------------------------------------------

        private void btnFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog i_Dlg = new FolderBrowserDialog();
            i_Dlg.SelectedPath = Utils.UserDir;
            i_Dlg.Description  = "Select the second directory where to load additional Input Files.\n"
                               + "Click 'Cancel' to turn off the second directory.";
            i_Dlg.ShowNewFolderButton = false;
            
            if (i_Dlg.ShowDialog(this) != DialogResult.OK)
            {
                SetUserDirectory("");
                return;
            }
            SetUserDirectory(i_Dlg.SelectedPath);
        }

        /// <summary>
        /// The user has manually selected a new second folder or double clicked an Oszi file in any folder.
        /// </summary>
        private void SetUserDirectory(String s_Path)
        {
            // If the user has double clicked an OSZI file in the sample direcory -> do not add this as a new second directory
            String s_UserDir = s_Path.TrimEnd('\\');
            if (s_Path.Length == 0 || String.Compare(s_UserDir, Utils.SampleDir, true) == 0)
                s_UserDir = "";

            Utils.RegWriteString(eRegKey.UserDirectory, s_UserDir);

            // Change the second directory to the new user directory.
            Utils.UserDir = s_UserDir;

            // Reload combobox with the new files
            btnRefresh_Click(null, null);

            // Add the new user directory to the tooltip
            SetButtonTooltips();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!Utils.CheckMinSamples())
                return;

            eSaveAs e_SaveAs = (eSaveAs)comboSaveAs.SelectedIndex;
            eTab    e_Tab    = (eTab)   tabControl .SelectedIndex;

            // Asks if existing file may be overwritten
            String s_Path = mi_ExImport.GetSavePath(e_SaveAs, e_Tab);
            if (s_Path == null)
                return;

            if (!Utils.StartBusyOperation(this))
                return;

            int s32_SaveSteps = checkApplyFactor.Checked ? panelOszi.DispSteps : 1;
            try
            {
                mi_ExImport.Export(s_Path, e_SaveAs, e_Tab, s32_SaveSteps, checkShrink8Bit.Checked);

                // Always turn this checkbox off, so the user has to set it manually each time again.
                // This avoids that the user saves an OSZI file in low resolution without wanting it explicitely.
                // The default save mode is always to save the higehst resolution that is possible.
                checkApplyFactor.Checked = false;
                checkShrink8Bit .Checked = false;
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

        private void btnRecordAudio_Click(object sender, EventArgs e)
        {
            FormRecordAudio i_Form = new FormRecordAudio();
            i_Form.ShowDialog(this);
        }

        // -----------------------------------------------------

        /// <summary>
        /// Set text of Label above the tab control
        /// </summary>
        public void SetLabel(String s_Text)
        {
            lblInfo.Text = s_Text;
        }

        // -----------------------------------------------------

        /// <summary>
        /// Display "Oszi *" in the tab if the user has unsaved changes
        /// </summary>
        public void UpdateDirty()
        {
            tabOszi.Text = (Utils.CurCapture != null && Utils.CurCapture.mb_Dirty) ? "Oszi *" : "Oszi";
        }

        /// <summary>
        /// Print to status bar
        /// </summary>
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
