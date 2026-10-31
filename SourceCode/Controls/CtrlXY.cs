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
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using PanelXY           = OsziWaveformAnalyzer.PanelXY;
using IPanelAction      = OsziWaveformAnalyzer.FormMain.IPanelAction;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Range             = OsziWaveformAnalyzer.Utils.Range;
using PlatformManager   = Platform.PlatformManager;

namespace OsziWaveformAnalyzer
{
    public partial class CtrlXY : UserControl, IPanelAction
    {
        // ============= Static ==============

        // Drawing 20000 samples every 20 ms already consumes some CPU. Do not allow more samples.
        const int MAX_GLOW_COUNT = 20000;
        const int MIN_GLOW_COUNT = 10;
        const int MAX_PLAY_STEP  = 5000;
        const int MIN_PLAY_STEP  = 1;

        const String ms_DemoFile = "XY Plot Lissajous 96 KHz Stereo 16 bit.oszi";

        static CtrlXY()
        {
            Utils.CheckDemoFileExists(ms_DemoFile);
        }

        // ============= Member ==============

        HScrollBar mi_Scrollbar;
        int        ms32_GlowCount;      // user input
        int        ms32_PlayStep;       // user input
        int        ms32_StartSample;    // show the samples from ms32_StartSample to ms32_StartSample + ms32_GlowCount
        int        ms32_MaxScrollValue; // the maximum for mi_Scrollbar.Value
        bool       mb_AutoRepeat;
        Timer      mi_PlayTimer = new Timer();
        Stopwatch  mi_StopWatch = new Stopwatch();

        public CtrlXY(ToolTip i_Tooltip, HScrollBar i_Scrollbar)
        {
            mi_Scrollbar = i_Scrollbar;

            InitializeComponent();
            i_Tooltip.SetToolTip(lblSmplCount, "The 'Glow Count' defines how long the phosphor glow effect takes to decay.\n"
                                             + "Type a value and hit Enter to store it.");
            i_Tooltip.SetToolTip(lblPlayStep,  "The 'Play Step' defines the speed in play mode.\n"
                                             + "Type a value and hit Enter to store it.");
            i_Tooltip.SetToolTip(btnPlay,      "Play the file in the X/Y plot showing 'Sample Count' samples at once\n"
                                             + "and advancing 'Play Step' samples every 20 ms (50 frames / second).");
            i_Tooltip.SetToolTip(btnStop,      "Stop playing the file");
            i_Tooltip.SetToolTip(btnReset,     "Reset play position to start of file");
            i_Tooltip.SetToolTip(btnRepeat,    "Enable / disable auto repeat");
            i_Tooltip.SetToolTip(btnSet,       "After entering new values for 'Glow Count' and 'Play Step' you must click this button.");
            i_Tooltip.SetToolTip(btnJump,      "Jump to the play location in the Oszi panel, set the cursor and highlight\n"
                                             + "all samples that are currently visible in the X/Y plot.");

            mi_Scrollbar.Scroll += new ScrollEventHandler(OnScrollbar_Scroll);
            mi_PlayTimer.Tick   += new EventHandler      (OnPlayTimer_Tick);

            ms32_GlowCount = Utils.RegReadInteger(eRegKey.XYGlowCount, 100);
            ms32_PlayStep  = Utils.RegReadInteger(eRegKey.XYPlayStep,  1);
            LoadControls();
        }

        // interface IPanelAction
        // The user changed the tab or the old capture has been deleted or a new capture was loaded
        public void OnUserAction(eTab e_ActiveTab, Capture i_Capture)
        {
            Visible = e_ActiveTab == eTab.XYPlot && i_Capture != null;
            if (!Visible)
                mi_PlayTimer.Stop();

            if (i_Capture == null)
            {
                mi_Scrollbar.Value = 0;
                ms32_StartSample   = 0;

                // Only few files support auto repeat. 
                // The file must contain cyclic data that ends exactly where it starts.
                // As most files do not support auto repeat mode --> turn it off when a new file is loaded
                mb_AutoRepeat   = false;
                btnRepeat.Image = Utils.LoadResourceImage("RepeatOff.ico");
                return;
            }

            if (!Visible)
                return;

            if (i_Capture.ms_Path != null)
            {
                // Load the correct play settings for the demo file
                String s_File = Path.GetFileName(i_Capture.ms_Path);
                if (String.Compare(s_File, ms_DemoFile) == 0)
                {
                    ms32_GlowCount = 4000;
                    ms32_PlayStep  =  100;
                }
            }
            LoadControls();
        }

        void LoadControls()
        {
            ms32_GlowCount = Math.Max(MIN_GLOW_COUNT, Math.Min(MAX_GLOW_COUNT, ms32_GlowCount));
            ms32_PlayStep  = Math.Max(MIN_PLAY_STEP,  Math.Min(MAX_PLAY_STEP,  ms32_PlayStep));

            if (Utils.CurCapture != null)
            {
                ms32_GlowCount = Math.Max(1, Math.Min(ms32_GlowCount, Utils.CurCapture.ms32_Samples / 2));
                ms32_PlayStep  = Math.Max(1, Math.Min(ms32_PlayStep,  Utils.CurCapture.ms32_Samples / 10));

                mi_Scrollbar.Maximum     = Utils.CurCapture.ms32_Samples;
                mi_Scrollbar.LargeChange = ms32_GlowCount; // advance when click on scrollbar shaft
                mi_Scrollbar.SmallChange = 1;              // advance when click on scrollbar end buttons

                // LargeChange defines the last sample that can be the start sample if not in auto repeat mode
                // Always ms32_GlowCount samples are painted in DrawLissajous()
                // The last sample that can be reached with the scrollbar is TotalSamples - GlowCount
                ms32_MaxScrollValue = mi_Scrollbar.Maximum - mi_Scrollbar.LargeChange;
            }

            textGlowCount.Text = ms32_GlowCount.ToString();
            textPlayStep .Text = ms32_PlayStep .ToString();

            Utils.PanelXY.OnUserSettgChanged(ms32_GlowCount, mb_AutoRepeat);
        }

        // ========================================================================

        // Called from textSmplCount and textPlayStep
        void OnTextBox_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                btnSet_Click(null, null);
        }
        private void btnSet_Click(object sender, EventArgs e)
        {
            int s32_Count;
            if (!int.TryParse(textGlowCount.Text, out s32_Count))
            {
                MessageBox.Show(Utils.FormMain, "Enter a valid integer number for Sample Count.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textGlowCount.Focus();
                return;
            }

            // -----------------------

            int s32_Step;
            if (!int.TryParse(textPlayStep.Text, out s32_Step))
            {
                MessageBox.Show(Utils.FormMain, "Enter a valid integer number for Play Step.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textPlayStep.Focus();
                return;
            }

            // -----------------------

            ms32_GlowCount = s32_Count;
            ms32_PlayStep  = s32_Step;
            LoadControls(); // FIRST (shrinks ms32_GlowCount and ms32_PlayStep to valid values)

            Utils.RegWriteInteger(eRegKey.XYGlowCount, ms32_GlowCount); // AFTER
            Utils.RegWriteInteger(eRegKey.XYPlayStep,  ms32_PlayStep);
        }

        // ========================================================================

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (!Utils.PanelXY.HasData)
            {
                MessageBox.Show(Utils.FormMain, "Load a capture with 2 analog channels.\n"
                              + "You can use the demo file:\n" + ms_DemoFile, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!mb_AutoRepeat)
            {
                // If scrollbar at end --> jump to begin
                if (mi_Scrollbar.Value + 1 >= ms32_MaxScrollValue)
                {
                    mi_Scrollbar.Value = 0;
                    ms32_StartSample   = 0;
                }
            }

            mi_StopWatch.Restart();
            OnPlayTimer_Tick(null, null);
            Utils.PanelXY.Focus(); // set keyboard focus
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            mi_PlayTimer.Stop();
            Utils.PanelXY.Focus(); // set keyboard focus
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            mi_Scrollbar.Value = 0;
            ms32_StartSample   = 0;
            Utils.PanelXY.Focus(); // set keyboard focus
            Utils.PanelXY.DrawLissajous(ms32_StartSample);
        }

        private void btnRepeat_Click(object sender, EventArgs e)
        {
            mb_AutoRepeat = !mb_AutoRepeat;
            btnRepeat.Image = Utils.LoadResourceImage(mb_AutoRepeat ? "RepeatOn.ico" : "RepeatOff.ico");
            Utils.PanelXY.Focus(); // set keyboard focus
            Utils.PanelXY.OnUserSettgChanged(ms32_GlowCount, mb_AutoRepeat);
        }

        private void btnJump_Click(object sender, EventArgs e)
        {
            if (!Utils.PanelXY.HasData)
            {
                MessageBox.Show(Utils.FormMain, "Load a capture with 2 analog channels.\n"
                              + "You can use the demo file:\n" + ms_DemoFile, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Utils.PanelOszi.SetDisplayFactorForRange(ms32_GlowCount);
            Utils.PanelOszi.UserSelection = new Range(ms32_StartSample, ms32_StartSample + ms32_GlowCount);
            Utils.PanelOszi.JumpToSample(ms32_StartSample);
            Utils.PanelOszi.Invalidate();
            Utils.FormMain.SwitchToTab(eTab.Oszi);
        }

        /// <summary>
        /// The framework fires this event only when the user moves the scrollbar, not when Scrollbar.Value is set programmatically.
        /// </summary>
        void OnScrollbar_Scroll(object sender, ScrollEventArgs e)
        {
            ms32_StartSample = Math.Min(e.NewValue, ms32_MaxScrollValue);
            Utils.PanelXY.DrawLissajous(ms32_StartSample);
        }

        void OnPlayTimer_Tick(object sender, EventArgs e)
        {
            mi_PlayTimer.Stop();

            ms32_StartSample += ms32_PlayStep;
            if (mb_AutoRepeat)
            {
                ms32_StartSample %= mi_Scrollbar.Maximum; // wrap around
            }
            else // play once mode
            {
                // s32_MaxScrollValue is the last sample that can be reached with the scrollbar
                if (ms32_StartSample >= ms32_MaxScrollValue)
                {
                    // move the scrollbar to the end and exit play back
                    mi_Scrollbar.Value = ms32_MaxScrollValue;
                    ms32_StartSample   = ms32_MaxScrollValue;
                    Utils.PanelXY.DrawLissajous(ms32_StartSample);
                    return;
                }
            }

            // In auto repeat mode ms32_StartSample can reach values that the scrollbar cannot display
            mi_Scrollbar.Value = Math.Min(ms32_StartSample, ms32_MaxScrollValue);
            Utils.PanelXY.DrawLissajous(ms32_StartSample);

            // Try to get a 50 frames per second
            // If the count of samples is very high or the CPU is very busy, the drawing may take longer then 20 ms.
            int s32_Elapsed = (int)mi_StopWatch.ElapsedMilliseconds;
            mi_PlayTimer.Interval = Math.Max(1, 20 - s32_Elapsed);
            mi_PlayTimer.Start();
            mi_StopWatch.Restart();
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(Utils.FormMain, "XYPlot");
        }
    }
}
