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

#if DEBUG
//    #define DEBUG_DRAWING
#endif

using System;
using System.IO;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using Range             = OsziWaveformAnalyzer.Utils.Range;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using OperationManager  = Operations.OperationManager;
using IPanelAction      = OsziWaveformAnalyzer.FormMain.IPanelAction;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;
using eMouseDrag        = OsziWaveformAnalyzer.Utils.eMouseDrag;

namespace OsziWaveformAnalyzer
{
    /// <summary>
    /// This class is speed-optimized in every line of the code.
    /// </summary>
    public class PanelOszi : Panel, IPanelAction
    {
        #region classes DrawChan, DrawPos, SplMinMax, DispFactor

        class DrawChan
        {
            // Voltage
            public float  mf_Min; // Separate channels: the Min/Max voltage of the channel, otherwise Min/Max of all analog channels
            public float  mf_Max;
            public float  mf_Range;

            public String ms_MinVolt; // voltage for legend
            public String ms_MaxVolt;

            // Y position on screen / in bitmap
            public int    ms32_AnalTop;
            public int    ms32_AnalHeight;
            public int    ms32_AnalBot;
            public int    ms32_DigiTop;
            public int    ms32_DigiHeight;
            public int    ms32_DigiBot;
            public int[]  ms32_MarkTop;
            public int[]  ms32_MarkBot;

            // analog legend
            public int    ms32_VoltTop; 
            public int    ms32_VoltBot;
            public int    ms32_NameTop;

            // Count of mark rows that have valid data
            public int    ms32_MarkRows;

            // Checkboxes used if display of separate analog channels is OFF.
            public CheckBox  mi_CheckBox;

            public bool ContainsY(int Y)
            {
                if (Y <= 0) return false;
                if (Y >= ms32_AnalTop && Y <= ms32_AnalBot) return true;
                if (Y >= ms32_DigiTop && Y <= ms32_DigiBot) return true;
                for (int M=0; M<ms32_MarkRows; M++)
                {
                    if (Y >= ms32_MarkTop[M] && Y <= ms32_MarkBot[M])
                        return true;
                }
                return false;
            }

            // Calculate the screen / bitmap Y coordinate for a given voltage
            public int CalcPixelPosY(float f_Volt)
            {
                // avoid division by zero error 
                if (mf_Range == 0.0f)
                    return ms32_AnalBot;

                // Y must be inverted (point 0,0 is top,left in bitmap and on screen)
                return ms32_AnalHeight - (int)((f_Volt - mf_Min) * ms32_AnalHeight / mf_Range) + ms32_AnalTop;
            }

            /// <summary>
            /// This does the opposite of CalcPixelPosY() --> calculate the voltage under the mouse pointer
            /// </summary>
            public float CalcVoltage(int s32_PixelY)
            {
                return ((ms32_AnalBot - s32_PixelY) * mf_Range / ms32_AnalHeight) + mf_Min;
            }
        }

        /// <summary>
        /// This class contains different data which depends if drawing on the screen or into a Bitmap.
        /// </summary>
        class DrawPos
        {
            public int        ms32_SignalTop;   // the Y position of the top of the topmost signal in pixels
            public int        ms32_SignalBot;   // the Y position of the bottom of the bottommost signal in pixels
            public int        ms32_LegendWidth; // with of the legend  in pixels
            public int        ms32_SignalWidth; // with of the signals in pixels
            public DrawChan[] mi_DrawChan;      // Y channel positions per channel
        }

        /// <summary>
        /// Store Min/Max of samples between DispSteps
        /// This is a speed optimization for Min/Max drawing
        /// </summary>
        public class SplMinMax
        {
            public float[] mf_AnalogMin;
            public float[] mf_AnalogMax;
            public Byte[]  mu8_DigitalHi;
            public Byte[]  mu8_DigiChange;

            // Set false to re-calculate Min/Max values
            public bool mb_AnalogOK;
            public bool mb_DigitalOK;
        }

        /// <summary>
        /// Stored in comboFactor.Items
        /// Combox Item = "x 10"     --> ms32_Zoom = 10 --> stretch display by factor 10. This does not affect saving to OSZI file.
        /// Combox Item = "/ 50.000" --> ms32_DispSteps = 50000 --> draw/save every 50 thousand's sample
        /// </summary>
        public class DispFactor
        {
            public readonly int ms32_DispSteps;
            public readonly int ms32_Zoom;

            public DispFactor(bool b_Zoom, int s32_Factor)
            {
                if (b_Zoom)
                {
                    ms32_DispSteps = 1;
                    ms32_Zoom      = s32_Factor;
                }
                else
                {
                    ms32_DispSteps = s32_Factor;
                    ms32_Zoom      = 1;
                }
            }

            public override string ToString()
            {
                if (ms32_DispSteps > 1)
                    return "/ " + ms32_DispSteps.ToString("N0");
                else
                    return "x " + ms32_Zoom; // only 1...20
            }
        }

        #endregion

        public const int MIN_ANALOG_HEIGHT  = 100; // minimum pixel height of analog  signals + 2 x 5 pixel margin
        public const int MAX_ANALOG_HEIGHT  = 600; // maximum pixel height of analog  signals + 2 x 5 pixel margin
        public const int MIN_DIGITAL_HEIGHT = 25;  // minimum pixel height of digital signals + 1 x 5 pixel margin
        public const int MAX_DIGITAL_HEIGHT = 200; // maximum pixel height of digital signals + 1 x 5 pixel margin
        
        const int  SIGNAL_PEN_WIDTH  = 1;   // pixel width of line pen for analog and digital signal
        const Byte ALPHA             = 245; // Colors should not be 100% opaque. When Separate Channels is OFF, the bottom channels shine through.
        const int  MARGIN            = 10;  // Leave a margin of 10 pixels above and below all signals.
        const int  CHECKBOX_LEFT     =  6;  // Checkbox from left space
        const int  LEGEND_HEIGHT     = 14;  // Height of Name String in pixels
        const int  SCROLL_STEPS      = 25;  // The pixels for horizontal scrolling (SmallChange) when pressing the scollbar buttons
        const int  MIN_CURSOR_HEIGHT = 150; // For very snamll signals (e.g. one digital channel) show a minimum height of the cursor

        // ================================== STATIC ===================================

        static Color[] mc_Colors = new Color[] 
        {
            Color.FromArgb(ALPHA, 0xF8, 0xFC, 0x00), // Channel  1 = yellow
            Color.FromArgb(ALPHA, 0x00, 0xFC, 0xF8), // Channel  2 = cyan 
            Color.FromArgb(ALPHA, 0xF8, 0x00, 0xF8), // Channel  3 = magenta
            Color.FromArgb(ALPHA, 0x30, 0xA0, 0xFF), // Channel  4 = blue
            Color.FromArgb(ALPHA, 0xFF, 0x85, 0x00), // Channel  5 = orange
            Color.FromArgb(ALPHA, 0x70, 0xA0, 0xC0), // Channel  6 = gray
            Color.FromArgb(ALPHA, 0x90, 0x40, 0xB0), // Channel  7 = dark magenta
            Color.FromArgb(ALPHA, 0xFF, 0xFF, 0xFF), // Channel  8 = white
            Color.FromArgb(ALPHA, 0xF8, 0x00, 0x00), // Channel  9 = red
            Color.FromArgb(ALPHA, 0x00, 0xFC, 0x00), // Channel 10 = green
            Color.FromArgb(ALPHA, 0x80, 0x80, 0xFF), // Channel 11 = light blue
            Color.FromArgb(ALPHA, 0xC0, 0x80, 0x80), // Channel 12 = brown
            Color.FromArgb(ALPHA, 0x99, 0xAA, 0x50), // Channel 13 = kaki

            // You can add more colors here, otherwise the existing colors will repeat.
        };

        // private variables
        static Brush[]  mi_TxtBrushs;       // draw legend text
        static Pen[]    mi_SignalPens;      // draw analog + digital signals
        static Pen[]    mi_ThresholdPens;   // draw threshold lines

        static public Color GetChannelColor(Channel i_Channel)
        {
            return mc_Colors[i_Channel.ms32_ColorIdx % mc_Colors.Length];
        }

        static public Brush GetChannelBrush(Channel i_Channel)
        {
            return mi_TxtBrushs[i_Channel.ms32_ColorIdx % mc_Colors.Length];
        }

        // ================================== MEMEBR ===================================

        Label            mi_LabelDispSamples;
        CheckBox         mi_CheckSepChannels;
        ComboBox         mi_ComboFactor;
        bool             mb_SeparateChannels;
        bool             mb_ShowLegend;
        int              ms32_DispStart;     // sample at left  of display area
        int              ms32_DispEnd;       // sample at right of display area
        int              ms32_CursorSpl;     // sample of the user's cursor
        Point            mk_MouseDownLoc;    // Position of left-mouse down event
        Point            mk_MouseDownStart;  // X = ms32_DispStart, Y = Autscroll.Y
        eMouseDrag       me_MouseDrag;       // current mouse drag operation
        decimal          md_RasterSamples;   // samples between 2 raster lines
        String           ms_RasterLegend;    // displayed at bottom left
        int              ms32_DispSteps;     // samples between 2 neighbour pixels on the screen
        int              ms32_Zoom;          // zoom mode (1 sample is stretched over ms32_Zoom pixels)
        int              ms32_AnalogHeight;  // set by trackbar "Analog Height"
        int              ms32_DigitalHeight; // set by trackbar "Digital Height"
        Pen              mi_PenGray;         // coordinate system lines
        Pen              mi_PenCursor;       // draw cursor line
        Pen              mi_PenRaster;       // draw raster lines
        Range            mk_Selection;       // the selected area (in Samples) with highlighted background
        Range            mk_Zoom;            // the area (in Pixels) to be zoomed with the mouse
        DrawPos          mi_DrawPos;
        Font             mi_Courier          = new Font("Courier New", 11);
        ToolTip          mi_Tooltip          = new ToolTip();
        Point            mk_LastTipPos       = Point.Empty;

        // ----------------------------------------

        /// <summary>
        /// returns the sample with the cursor or -1 if no cursor is set.
        /// </summary>
        public int CursorSample
        {
            get { return ms32_CursorSpl; }
        }

        public bool RasterON
        {
            get { return md_RasterSamples > 0; }
        }

        // ----------------------------------------

        /// <summary>
        /// The range of all samples
        /// </summary>
        public Range FullRange
        {
            get
            {
                if (Utils.CurCapture == null) return new Range(0, 0);
                else                          return new Range(0, Utils.CurCapture.ms32_Samples);
            }
        }

        /// <summary>
        /// The samples that are visible on the screen
        /// </summary>
        public Range DisplayRange
        {
            get { return new Range(ms32_DispStart, ms32_DispEnd); }
        }

        /// <summary>
        /// The samples that the user has selected.
        /// To turn off the selection set {0, 0}
        /// </summary>
        public Range UserSelection
        {
            // Range is a struct --> return a copy that the caller can modify.
            get { return mk_Selection;  }
            set { mk_Selection = value; }
        }

        /// <summary>
        /// returns the samples to be used in an operation, like Mathematical.
        /// If the user has not selected samples, the operation is executed on the entire capture.
        /// </summary>
        public Range GetUserSelectionOrAll(out String s_Display)
        {
            if (mk_Selection.IsValid)
            {
                s_Display = String.Format("Samples {0:N0} ... {1:N0}", mk_Selection.ms32_Start, mk_Selection.ms32_End);
                return mk_Selection;
            }
            else
            {
                s_Display = "All samples";
                return new Range(0, Utils.CurCapture.ms32_Samples);
            }
        }

        // ----------------------------------------

        public bool ShowLegend
        {
            set { mb_ShowLegend = value; }
        }

        /// <summary>
        /// Set the value of the trackbar "Analog Height"
        /// </summary>
        public int AnalogHeight
        {
            set { ms32_AnalogHeight = value; }
        }

        /// <summary>
        /// Set the value of the trackbar "Digital Height"
        /// </summary>
        public int DigitalHeight
        {
            set { ms32_DigitalHeight = value; }
        }

        // ----------------------------------------

        // ComboBox Factor = "/ 5" --> ms32_DispSteps = 5 --> draw every fifth sample on screen
        // Either DispSteps = 1 and Zoom > 1  
        // or     DispSteps > 1 and Zoom = 1
        public int DispSteps
        {
            get { return ms32_DispSteps; }
            set 
            { 
                if (ms32_DispSteps != value && Utils.CurCapture != null)
                    Utils.CurCapture.ResetSampleMinMax();

                ms32_DispSteps = value; 
            }
        }
        public int Zoom
        {
            set { ms32_Zoom = value; }
            get { return ms32_Zoom;  }
        }

        // ----------------------------------------

        /// <summary>
        /// Attention: mb_SeparateChannels is not the same as mi_CheckSepChannels.Checked
        /// mb_SeparateChannels is also true if less than two analog channels exist.
        /// The checkbox may be invisible. In this case it is ignored.
        /// </summary>
        public bool SeparateChannels
        {
            get { return mb_SeparateChannels; }
            set 
            { 
                if (mi_CheckSepChannels != null)
                    mi_CheckSepChannels.Checked = value; // This invokes FormMain.checkSepChannels_CheckedChanged()
            }
        }

        /// <summary>
        /// returns true if multiple analog channels are drawn on top of other analog channels (Checkbox 'Separate Channels' is OFF).
        /// </summary>
        public bool CommonAnalogDrawing
        {
            get
            {
                if (mb_SeparateChannels || Utils.CurCapture == null)
                    return false;

                int s32_Count = 0;
                foreach (Channel i_Channel in Utils.CurCapture.mi_Channels)
                {
                    if (i_Channel.mf_Analog != null && !i_Channel.mb_AnalHidden)
                        s32_Count ++;
                }
                return s32_Count > 1;
            }
        }

        // =====================================================================================

        /// <summary>
        /// Constructor
        /// </summary>
        public PanelOszi()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            AutoScroll = true;
            AutoSize   = false;

            // IMPORTANT:
            // A Panel can normally not be selected (Panel.CanSelect returns false).
            // The following line is required to give the panel the keyboard focus when clicking into it.
            // This allows scrolling with the mouse wheel.
            // Additionally in OnMouseDown() it is required to call Focus()
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public void Init(Label i_LabelDispSamples, CheckBox i_CheckSepChannels, ComboBox i_ComboFactor)
        {
            mi_LabelDispSamples = i_LabelDispSamples;
            mi_CheckSepChannels = i_CheckSepChannels;
            mi_ComboFactor      = i_ComboFactor;

            mi_SignalPens    = new Pen  [mc_Colors.Length];
            mi_ThresholdPens = new Pen  [mc_Colors.Length];
            mi_TxtBrushs     = new Brush[mc_Colors.Length];
            for (int C=0; C<mc_Colors.Length; C++)
            {
                mi_SignalPens[C] = new Pen(mc_Colors[C], SIGNAL_PEN_WIDTH);
                mi_TxtBrushs [C] = new SolidBrush(mc_Colors[C]);

                Color c_Dark = Color.FromArgb(mc_Colors[C].R / 2, mc_Colors[C].G / 2, mc_Colors[C].B / 2);
                mi_ThresholdPens[C] = new Pen(c_Dark, 1);
                mi_ThresholdPens[C].DashPattern = new float[] {10, 10};
            }

            mi_PenGray   = new Pen(Color.FromArgb(0x80, 0x80, 0x80));
            mi_PenCursor = new Pen(Color.FromArgb(0xC0, 0xC0, 0xC0));
            mi_PenRaster = new Pen(Color.FromArgb(0x80, 0x80, 0x80));
            mi_PenCursor.DashPattern = new float[] {10, 10};
            mi_PenRaster.DashPattern = new float[] { 1, 20};

            mk_Selection.Reset();
            mk_Zoom     .Reset();

            ms32_DispSteps = 1; // must never be zero
            me_MouseDrag   = eMouseDrag.None;
        }

        // =====================================================================================

        // interface IPanelAction
        // The user changed the tab or the old capture has been deleted or a new capture was loaded
        // Only store the Capture, do not display it before ms32_DispSteps has been set.
        public void OnUserAction(eTab e_ActiveTab, Capture i_Capture)
        {
            HideTooltip();

            if (i_Capture == null)
            {
                mk_Selection.Reset();
                mk_Zoom     .Reset();
                ms32_CursorSpl     = -1;  // no cursor
                md_RasterSamples   = -1m; // no raster
                AutoScrollPosition = Point.Empty;
                AutoScrollMinSize  = Size.Empty; // remove scrollbars
                ms32_DispStart     = 0;
                mi_LabelDispSamples.Text = "";
                Invalidate(); // draw black background
                return;
            }

            // Assign a color to each channel which does not change as long as the channel is alive.
            for (int C=0; C<i_Capture.mi_Channels.Count; C++)
            {
                i_Capture.mi_Channels[C].ms32_ColorIdx = C;
            }

            if (e_ActiveTab == eTab.Oszi)
                Focus();
        }

        /// <summary>
        /// Sets the sample position of the cursor and the interval of raster lines in pico seconds.
        /// Set -1 to turn off the Cursor / Raster lines.
        /// If the current resolution results in more raster lines than one line per 10 pixels, the raster lines are not drawn.
        /// </summary>
        public void SetCursor(int s32_Cursor, decimal d_Interval)
        {
            ms32_CursorSpl = s32_Cursor;

            if (d_Interval > 0)
            {
                md_RasterSamples = d_Interval / Utils.CurCapture.ms64_SampleDist;
                ms_RasterLegend  = "Raster: " + Utils.FormatTimePico(d_Interval);
            }
            else
            {
                md_RasterSamples = -1m;
                ms_RasterLegend  = null;
            }
        }

        public void JumpToSample(int s32_Sample)
        {
            ms32_CursorSpl = s32_Sample; // set cursor

            // scroll to 40 pixels before the demanded sample
            ms32_DispStart = Math.Max(0, s32_Sample - 40 * ms32_DispSteps);
            RecalcHorizScrollPos(true);
        }

        // =====================================================================================

        /// <summary>
        /// 1.) Recalculate Min/Max values of all channels if required (if mb_AnalogOK or mb_DigitalOK == false)
        /// 2.) Recalculate Y Draw Position of all channels
        /// 3.) Measure the width of the legend
        /// 4.) Adapt the scrollable area of this panel to the signals.
        /// 5.) Adjust the horizontal scrollbar.
        /// 6.) Invalidate the OsziPanel
        /// This must be called when a new Capture is loaded, when the order of channels has changed,
        /// when channels or marks have been added or removed and when the Display Factor has changed.
        /// b_RestorePos = true --> restore the scrollbar to show the same first sample (ms32_DispStart)
        /// ms32_DispSteps must be set before calling this function.
        /// </summary>
        public void RecalculateEverything(bool b_RestorePos = false)
        {
            if (Utils.CurCapture == null)
                return;

            if (Utils.CurCapture.ms32_Samples > 200000)
                Utils.FormMain.Cursor = Cursors.WaitCursor;

            // if scrollbar height is not subtracted there will be flickering
            // when resizing the panel and vert/hor scrollbars appear/disappear
            int s32_AvailHeight = ClientSize.Height - SystemInformation.HorizontalScrollBarHeight;
            mi_DrawPos = CalcVerticalDrawPos(s32_AvailHeight, true);

            int s32_GraWidth    = Utils.CurCapture.ms32_Samples * ms32_Zoom / ms32_DispSteps;
            int s32_LegendWidth = MeasureLegend(mi_DrawPos, null);
            AutoScrollMinSize   = new Size(s32_GraWidth + s32_LegendWidth, mi_DrawPos.ms32_SignalBot);
            RecalcHorizScrollPos(b_RestorePos);

            if (Utils.CurCapture.ms32_Samples > 200000)
                Utils.FormMain.Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// s32_SamplesPerBit:
        /// In case of self sync data (CAN bus, UART, IR, MagStripe) this corresponds to one bit.
        /// In case of synchronous data (SPI, I2C) this corresponds to a clock period.
        /// Tries to auto-select a Display Factor, so that one digital bit corresponds to approx 25 pixels on the screen.
        /// </summary>
        public void SetDisplayFactorForBit(int s32_SamplesPerBit)
        {
            if (s32_SamplesPerBit == 0)
                return;

            double d_Steps = s32_SamplesPerBit / 25.0;

            double  d_MinDiff = double.MaxValue;
            DispFactor i_Best = null;
            foreach (DispFactor i_Factor in mi_ComboFactor.Items)
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
                mi_ComboFactor.SelectedItem = i_Best;
        }

        /// <summary>
        /// Adjust the display factor so that the amount of s32_Range samples fits on the screen 
        /// </summary>
        public void SetDisplayFactorForRange(int s32_Range)
        {
            if (s32_Range == 0 || mi_DrawPos == null)
                return;

            int s32_GraphWidth = Width - mi_DrawPos.ms32_LegendWidth - 2 * MARGIN;

            DispFactor i_Best = null;
            foreach (DispFactor i_Factor in mi_ComboFactor.Items)
            {
                if (i_Factor.ms32_Zoom > 1)
                    continue;

                if (s32_Range / i_Factor.ms32_DispSteps < s32_GraphWidth)
                {
                    i_Best = i_Factor;
                    break;
                }
            }

            if (i_Best != null)
                mi_ComboFactor.SelectedItem = i_Best;
        }

        // =====================================================================================

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            // Normally the mouse wheel is used for vertical scolling --> no extra treatment required.
            // If the vertical scroll is not visible, the mouse wheel does a horizontal scroll, but OnScroll() will NOT be called!
            if (!VerticalScroll.Visible)
                RecalcHorizScrollPos();
        }

        /// <summary>
        /// The framework fires this event only when the user moves the scrollbar, not when Scrollbar.Value is set programmatically.
        /// </summary>
        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);

            if (e.ScrollOrientation == ScrollOrientation.HorizontalScroll)
                RecalcHorizScrollPos();
        }

        /// <summary>
        /// b_RestorePos = true --> restore the scrollbar to show the same first sample (ms32_DispStart)
        /// when combobox 'Factor' is changed.
        /// Invalidates the OsziPanel
        /// </summary>
        public void RecalcHorizScrollPos(bool b_RestorePos = false)
        {
            if (Utils.CurCapture == null)
                return;
           
            int s32_TotSamples = Utils.CurCapture.ms32_Samples;

            // Calculate the first and last sample visible on the screen.
            int s32_GraWidth = Utils.CurCapture.ms32_Samples * ms32_Zoom / ms32_DispSteps;
            int s32_MaxWidth = Math.Min(ClientSize.Width, s32_GraWidth);

            // This may happen when changing the display factor before settings the new Capture.
            // Avoid divivsion by zero crash.
            if (s32_TotSamples == 0 || s32_GraWidth == 0)
                return;

            // ----------------------------------

            if (b_RestorePos)
            {
                decimal d_ReverseFactor = ((decimal)ms32_DispStart) / s32_TotSamples; // samples, zero based

                // ATTENTION: AutoScollPosition uses negative values for X and Y.
                // But when SETTING a new value it must be positive!
                Point k_Pos = AutoScrollPosition;
                k_Pos.X  = (int)(d_ReverseFactor * s32_GraWidth); // pixels
                k_Pos.Y  = -k_Pos.Y;
                AutoScrollPosition = k_Pos;
                
                // If the scrollbar was far at the right and the screen area has become narrower by a higher division factor, 
                // the scollbar is shorter now and ms32_DispStart is wrong here.
                // In the next step ms32_DispStart must be adapted to the real scroll position.
            }

            decimal d_ScrollFactor = (decimal)-AutoScrollPosition.X / s32_GraWidth; // pixels
            ms32_DispStart = (int)(d_ScrollFactor * s32_TotSamples); // samples, zero based

            // Round the start sample to avoid flickering when horizontally scrolling
            ms32_DispStart = (ms32_DispStart / ms32_DispSteps) * ms32_DispSteps;

            decimal d_WindowFactor = (decimal)s32_MaxWidth / s32_GraWidth;          // pixels
            ms32_DispEnd = ms32_DispStart + (int)(d_WindowFactor * s32_TotSamples); // samples, zero based
            ms32_DispEnd = Math.Min(ms32_DispEnd, s32_TotSamples);

            // ----------------------------------

            int s32_VisibleSamples = ms32_DispEnd - ms32_DispStart;

            Utils.FormMain.SetLabel(String.Format("First Sample: {0}  Screen Interval: {1}  Total Samples: {2:N0}  Total Duration: {3}  Samplerate: {4}  Sample Distance: {5}", 
                                                  Utils.CurCapture.FormatInterval(ms32_DispStart),
                                                  Utils.CurCapture.FormatInterval(s32_VisibleSamples),
                                                  s32_TotSamples, Utils.CurCapture.FormatInterval(s32_TotSamples),
                                                  Utils.FormatFrequency(Utils.PICOS_PER_SECOND / Utils.CurCapture.ms64_SampleDist, 0),
                                                  Utils.FormatTimePico (Utils.CurCapture.ms64_SampleDist)));

            mi_LabelDispSamples.Text = String.Format("{0:N0} samples", Utils.CurCapture.ms32_Samples / ms32_DispSteps);

            // ----------------------------------

            // Adapt scrollbar to Legend width
            int s32_VisiblePixel = s32_VisibleSamples * ms32_Zoom / ms32_DispSteps;
            if (mi_DrawPos != null)
                s32_VisiblePixel -= mi_DrawPos.ms32_LegendWidth;

            if (s32_VisiblePixel > 100)
                HorizontalScroll.LargeChange = s32_VisiblePixel * 7 / 8;

            // Speed up scrolling with the buttons at the scrollbar ends. (SmallChange = 5 is too slow)
            HorizontalScroll.SmallChange = SCROLL_STEPS;

            // ----------------------------------

            foreach (CheckBox i_CheckBox in Controls)
            {
                i_CheckBox.Left = CHECKBOX_LEFT;
            }

            Invalidate();
        }

        // =====================================================================================

        /// <summary>
        /// Recalculates the Min/Max values of the channels if required (if mb_AnalogOK or mb_DigitalOK == false)
        /// Calculates drawing position of all channels and fills a DrawPos class
        /// The DrawPos class contains different data which depends if drawing on the screen or into a Bitmap.
        /// b_GUI = true  --> for display in OsziPanel
        /// b_GUI = false --> for saving image to disk
        /// </summary>
        private DrawPos CalcVerticalDrawPos(int s32_AvailTotalHeight, bool b_GUI)
        {
            DrawPos i_DrawPos = new DrawPos();
            if (Utils.CurCapture == null)
                return i_DrawPos;

            i_DrawPos.mi_DrawChan = new DrawChan[Utils.CurCapture.mi_Channels.Count];

            // Calculate Min/Max of all analog channels
            float f_CommonMin = float.MaxValue;
            float f_CommonMax = float.MinValue;
            int s32_TotalAnalog  = 0;
            int s32_TotalDigital = 0;
            foreach (Channel i_Chan in Utils.CurCapture.mi_Channels)
            {
                // Re-calculate Min/Max only if required (if mb_AnalogOK or mb_DigitalOK == false)
                CalcSampleMinMax(i_Chan);

                if (i_Chan.mf_Analog != null)
                {
                    f_CommonMin = Math.Min(f_CommonMin, i_Chan.mf_Min);
                    f_CommonMax = Math.Max(f_CommonMax, i_Chan.mf_Max);
                    s32_TotalAnalog ++;
                }

                if (i_Chan.mu8_Digital != null) 
                    s32_TotalDigital ++;
            }

            mb_SeparateChannels = mi_CheckSepChannels.Checked;
            if (s32_TotalAnalog <= 1)
                mb_SeparateChannels = true; // otherwise analog and digital channels cannot be moved up/down

            int s32_TotalMarkRow = 0;
            for (int C=0; C<Utils.CurCapture.mi_Channels.Count; C++)
            {
                Channel  i_Chan = Utils.CurCapture.mi_Channels[C];
                DrawChan i_Draw = new DrawChan();
                i_DrawPos.mi_DrawChan[C] = i_Draw;

                i_Draw.mf_Min   = mb_SeparateChannels ? i_Chan.mf_Min : f_CommonMin;
                i_Draw.mf_Max   = mb_SeparateChannels ? i_Chan.mf_Max : f_CommonMax;
                i_Draw.mf_Range = i_Draw.mf_Max - i_Draw.mf_Min;

                i_Draw.ms_MaxVolt = String.Format("{0:0.###} V", i_Draw.mf_Max);
                i_Draw.ms_MinVolt = String.Format("{0:0.###} V", i_Draw.mf_Min);
                
                i_Draw.ms32_MarkRows = 0;
                if (i_Chan.mi_MarkRows != null)
                {
                    foreach (List<SmplMark> i_Marks in i_Chan.mi_MarkRows)
                    {
                        if (i_Marks != null && i_Marks.Count > 0)
                        {
                            i_Draw.ms32_MarkRows ++;
                            s32_TotalMarkRow ++;
                        }
                    }
                }
                i_Draw.ms32_MarkTop = new int[i_Draw.ms32_MarkRows];
                i_Draw.ms32_MarkBot = new int[i_Draw.ms32_MarkRows];
            }

            Utils.CurCapture.ms32_AnalogCount  = s32_TotalAnalog;
            Utils.CurCapture.ms32_DigitalCount = s32_TotalDigital;

            if (!mb_SeparateChannels) // draw all analog channels on top of the others
                s32_TotalAnalog = Math.Min(s32_TotalAnalog, 1); // only 0 or 1

            // ------------------------------------------------------------------

            // Mark rows have a fix height of 20 pixels.
            int s32_AvailSignalHeight = s32_AvailTotalHeight - 20 * s32_TotalMarkRow - 2 * MARGIN;

            int s32_AnalHeight = s32_TotalAnalog  > 0 ? ms32_AnalogHeight  : 0;
            int s32_DigiHeight = s32_TotalDigital > 0 ? ms32_DigitalHeight : 0;

            int s32_Remaining = s32_AvailSignalHeight - s32_TotalAnalog * s32_AnalHeight - s32_TotalDigital * s32_DigiHeight;
            if (s32_Remaining < 0)
                s32_Remaining = 0;

            // ------------------------------------------------------------------

            int s32_AnalTop = MARGIN;
            if (b_GUI) 
            {
                s32_AnalTop += s32_Remaining / 2;
                Controls.Clear(); // remove previous checkboxes
            }
            int s32_DigiTop = s32_AnalTop; 

            i_DrawPos.ms32_SignalTop = s32_AnalTop;

            // If analog channels are not displayed separately the first digital channel is below the common analog display
            if (!mb_SeparateChannels)
                s32_DigiTop += s32_AnalHeight;

            int s32_NameOffset = 0;
            for (int C=0; C<Utils.CurCapture.mi_Channels.Count; C++)
            {
                Channel  i_Chan = Utils.CurCapture.mi_Channels[C];
                DrawChan i_Draw = i_DrawPos .mi_DrawChan[C];

                if (i_Chan.mf_Analog != null)
                {
                    // Leave 5 pixels space at top and bottom of the analog signal
                    i_Draw.ms32_AnalTop    = s32_AnalTop    + 5;
                    i_Draw.ms32_AnalHeight = s32_AnalHeight - 10;
                    i_Draw.ms32_AnalBot    = i_Draw.ms32_AnalTop + i_Draw.ms32_AnalHeight;

                    i_Draw.ms32_VoltTop = i_Draw.ms32_AnalTop;
                    i_Draw.ms32_VoltBot = i_Draw.ms32_AnalBot;
                    i_Draw.ms32_NameTop = i_Draw.ms32_VoltTop + LEGEND_HEIGHT;

                    if (mb_SeparateChannels)
                    {
                        s32_AnalTop += s32_AnalHeight;
                        s32_DigiTop += s32_AnalHeight;
                    }
                    else if (mb_ShowLegend && Utils.CurCapture.ms32_AnalogCount > 1)
                    {
                        if (b_GUI)
                        {
                            i_Draw.mi_CheckBox           = new CheckBox();
                            i_Draw.mi_CheckBox.BackColor = Color.Black; // NOT Transparent! --> invokes OnPaint() for each Checkbox!!
                            i_Draw.mi_CheckBox.ForeColor = GetChannelColor(i_Chan);
                            i_Draw.mi_CheckBox.AutoSize  = true; // important
                            i_Draw.mi_CheckBox.Font      = Font;
                            i_Draw.mi_CheckBox.Text      = i_Chan.ms_Name;
                            i_Draw.mi_CheckBox.Tag       = i_Chan;
                            i_Draw.mi_CheckBox.Top       = i_Draw.ms32_NameTop + s32_NameOffset;
                            i_Draw.mi_CheckBox.Left      = CHECKBOX_LEFT;
                            i_Draw.mi_CheckBox.Checked   = !i_Chan.mb_AnalHidden;
                            i_Draw.mi_CheckBox.CheckedChanged += new EventHandler(OnChannelCheckBoxChanged);
                            Controls.Add(i_Draw.mi_CheckBox);

                            s32_NameOffset += i_Draw.mi_CheckBox.Height;
                        }
                        else // Screenshot
                        {
                            i_Draw.ms32_NameTop += s32_NameOffset;
                            s32_NameOffset      += LEGEND_HEIGHT;
                        }
                    }
                }

                if (i_Chan.mu8_Digital != null)
                {
                    // Leave a variable space at top and 5 pixels at bottom of the digital signal
                    int s32_Space = Math.Max(5, s32_DigiHeight / 10);
                    i_Draw.ms32_DigiTop    = s32_DigiTop    + s32_Space;
                    i_Draw.ms32_DigiHeight = s32_DigiHeight - s32_Space - 5;
                    i_Draw.ms32_DigiBot    = i_Draw.ms32_DigiTop + i_Draw.ms32_DigiHeight;

                    s32_DigiTop += s32_DigiHeight;
                    if (mb_SeparateChannels)
                        s32_AnalTop += s32_DigiHeight;
                }

                // The mark rows have a fix height of 15 pixel + 5 pixel space at the bottom which never changes
                for (int M=0; M<i_Draw.ms32_MarkRows; M++)
                {
                    i_Draw.ms32_MarkTop[M] = s32_DigiTop;
                    i_Draw.ms32_MarkBot[M] = i_Draw.ms32_MarkTop[M] + 15;

                    s32_DigiTop += 20;
                    if (mb_SeparateChannels)
                        s32_AnalTop += 20;
                }
            } // for (Channel)

            i_DrawPos.ms32_SignalBot = Math.Max(s32_AnalTop, s32_DigiTop);
            return i_DrawPos;
        }

        void OnChannelCheckBoxChanged(object o_Sender, EventArgs e)
        {
            CheckBox i_CheckBox = (CheckBox)o_Sender;
            Channel  i_Channel  = (Channel)i_CheckBox.Tag;
            i_Channel.mb_AnalHidden = !i_CheckBox.Checked;
            Invalidate();
        }

        /// <summary>
        /// All samples that are skipped by the Display Factor must be included into the drawing.
        /// Example: 24 millions samples are loaded, Display Factor = 100.000 --> only 240.000 samples are drawn.
        /// Store the minimum and maximum voltages of all samples that are not drawn on the screen.
        /// This is a significant speed optimization when drawing millions of samples.
        /// Two flags are set to avoid re-calculating the Min/Max unnecessarily (mb_AnalogOK and mb_DigitalOK)
        /// b_VisibleOnly = true --> recalculate only samples that are currently visible on the screen (only used by NoiseFilter)
        /// After calling this with b_VisibleOnly = true the original or new values must be loaded and mb_AnalogOK must be set to false!
        /// </summary>
        public void CalcSampleMinMax(Channel i_Channel, bool b_VisibleOnly = false)
        {
            int s32_Start = b_VisibleOnly ? ms32_DispStart : 0;
            int s32_End   = b_VisibleOnly ? ms32_DispEnd   : Utils.CurCapture.ms32_Samples;

            SplMinMax i_MinMax = i_Channel.mi_SampleMinMax;

            int s32_DispSamples = Utils.CurCapture.ms32_Samples / ms32_DispSteps + 1;
                
            if (i_Channel.mf_Analog != null && !i_MinMax.mb_AnalogOK)
            {
                // If ms32_DispSteps == 50 --> this array stores Min/Max of every 50 samples.
                i_MinMax.mf_AnalogMin = new float[s32_DispSamples];
                i_MinMax.mf_AnalogMax = new float[s32_DispSamples];

                // Min/Max of entire channel
                float f_ChanMin = float.MaxValue;
                float f_ChanMax = float.MinValue;

                int P = s32_Start;
                int S = s32_Start;
                int L = s32_Start / ms32_DispSteps;
                while (S < s32_End)
                {
                    float f_StepsMin = float.MaxValue;
                    float f_StepsMax = float.MinValue;
                    for (int M=P; M<=S; M++) // IMPORTANT:  "<=" here
                    {
                        float f_Volt = i_Channel.mf_Analog[M];
                        f_StepsMin = Math.Min(f_StepsMin, f_Volt);
                        f_StepsMax = Math.Max(f_StepsMax, f_Volt);

                        f_ChanMin = Math.Min(f_ChanMin, f_Volt);
                        f_ChanMax = Math.Max(f_ChanMax, f_Volt);
                    }
                    i_MinMax.mf_AnalogMin[L] = f_StepsMin;
                    i_MinMax.mf_AnalogMax[L] = f_StepsMax;

                    P = S;
                    S += ms32_DispSteps;
                    L ++;
                }
                i_MinMax.mb_AnalogOK = true;

                if (!b_VisibleOnly)
                {
                    i_Channel.mf_Min = f_ChanMin;
                    i_Channel.mf_Max = f_ChanMax;
                }
            }

            if (i_Channel.mu8_Digital != null && !i_MinMax.mb_DigitalOK)
            {
                i_MinMax.mu8_DigitalHi  = new Byte[s32_DispSamples];
                i_MinMax.mu8_DigiChange = new Byte[s32_DispSamples];

                int P = s32_Start;
                int S = s32_Start;
                int L = s32_Start / ms32_DispSteps;
                while (S < s32_End)
                {
                    Byte u8_High   = i_Channel.mu8_Digital[S];
                    Byte u8_Change = 0;
                    for (int M=P; M<S; M++) // IMPORTANT:  "<" here
                    {
                        Byte u8_Status = i_Channel.mu8_Digital[M];
                        if (u8_Status != u8_High)
                        {
                            u8_Change = 1;
                            break;
                        }
                    }
                    i_MinMax.mu8_DigitalHi [L] = u8_High;
                    i_MinMax.mu8_DigiChange[L] = u8_Change;

                    P = S;
                    S += ms32_DispSteps;
                    L ++;
                }
                i_MinMax.mb_DigitalOK = true;
            }
        }

        /// <summary>
        /// returns the width of the legend in pixels
        /// </summary>
        int MeasureLegend(DrawPos i_DrawPos, Graphics i_Graphics)
        {
            if (Utils.CurCapture == null || !mb_ShowLegend)
            {
                // Must be reset for mouse position calculation
                i_DrawPos.ms32_LegendWidth = 0;
                return 0;
            }

            if (i_DrawPos.ms32_LegendWidth == 0) // measure once only
            {
                Graphics i_Measure = i_Graphics;
                if (i_Graphics == null)
                    i_Measure = Graphics.FromHwnd(Handle);

                float f_Width = 0;
                for (int C=0; C<Utils.CurCapture.mi_Channels.Count; C++)
                {
                    Channel  i_Chan = Utils.CurCapture.mi_Channels[C];
                    DrawChan i_Draw = i_DrawPos .mi_DrawChan[C];

                    if (i_Draw.mi_CheckBox != null)
                        f_Width = Math.Max(f_Width, i_Draw.mi_CheckBox.Width - i_Draw.mi_CheckBox.Margin.Horizontal);
                    else
                        f_Width = Math.Max(f_Width, i_Measure.MeasureString(i_Chan.ms_Name, Font).Width);

                    if (i_Chan.mf_Analog != null)
                    {
                        f_Width = Math.Max(f_Width, i_Measure.MeasureString(i_Draw.ms_MaxVolt, Font).Width);
                        f_Width = Math.Max(f_Width, i_Measure.MeasureString(i_Draw.ms_MinVolt, Font).Width);
                    }
                }
                i_DrawPos.ms32_LegendWidth = (int)f_Width + 15;

                if (i_Graphics == null)
                    i_Measure.Dispose();
            }
            return i_DrawPos.ms32_LegendWidth;
        }

        // =====================================================================================

        /// <summary>
        /// IMPORTANT:
        /// Without this function the arrow keys never get through to OnKeyDown()
        /// </summary>
        protected override bool IsInputKey(Keys e_Key) 
        {
            switch (e_Key)
            {
                case Keys.Up:   // handled in FormMain
                case Keys.Down: // handled in FormMain
                case Keys.Left:
                case Keys.Left  | Keys.Shift:
                case Keys.Right:
                case Keys.Right | Keys.Shift:
                    return true;
            }
            return base.IsInputKey(e_Key);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Alt || e.Control || Utils.CurCapture == null)
                return;

            // Move the cursor horizontally with keys arrow right and arrow left.
            if (ms32_CursorSpl >= 0)
            {
                int s32_Offset = ms32_DispSteps;
                if (e.Shift) s32_Offset *= 8;

                switch (e.KeyCode)
                {
                    case Keys.Left:  ms32_CursorSpl = Math.Max(ms32_CursorSpl - s32_Offset, 0); break;
                    case Keys.Right: ms32_CursorSpl = Math.Min(ms32_CursorSpl + s32_Offset, 
                                                               Utils.CurCapture.ms32_Samples - 1 - ms32_DispSteps); break;
                    default: return;
                }

                if (mk_LastTipPos != Point.Empty)
                {
                    mk_LastTipPos.X--;          // otherwise tooltip stays unchanged
                    ShowTooltip(mk_LastTipPos); // update tooltip
                }
                Invalidate(); // draw cursor
            }
            else // no cursor --> forward arrow keys to horizontal scrollbar
            {
                // Do not send WM_HSCROLL here for compatibility with Linux / Mac

                int s32_Offset = ms32_DispSteps * 5;
                if (e.Shift) s32_Offset *= 5;
                switch (e.KeyCode)
                {
                    case Keys.Left:  ms32_DispStart += s32_Offset; break;
                    case Keys.Right: ms32_DispStart -= s32_Offset; break;
                    default: return;
                }
                RecalcHorizScrollPos(true);
            }
        }

        // =====================================================================================

        /// <summary>
        /// Right button down: show the dynamic right-click menu
        /// Left  button down: move the entire signal in OnMouseMove()
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Focus(); // required for mouse wheel to work
            HideTooltip();
          
            if (e.Button == MouseButtons.Right)
            {
                int s32_Sample = GetSampleAtScreenX(e.X);
                ShowMenu(e.Location, s32_Sample);
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                mk_MouseDownLoc     = e.Location;
                mk_MouseDownStart.X = ms32_DispStart;
                mk_MouseDownStart.Y = -AutoScrollPosition.Y;

                switch (Control.ModifierKeys)
                {
                    case Keys.None:
                        me_MouseDrag = eMouseDrag.Move; // move graph horizontally + vertically
                        Cursor = Cursors.SizeAll;
                        break;
                    case Keys.Shift:
                        me_MouseDrag = eMouseDrag.Select; // select a range
                        Cursor = Cursors.VSplit;
                        if (mk_Selection.IsValid)
                        {
                            mk_Selection.Reset(); // remove any previous selection
                            Invalidate();
                        }
                        break;
                    case Keys.Control:
                        me_MouseDrag = eMouseDrag.Zoom; // zoom into a range
                        Cursor = Cursors.VSplit;
                        break;
                }
            }
        }

        /// <summary>
        /// Show tooltip, update checkboxes
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (Utils.CurCapture == null || ms32_Zoom == 0)
                return;

            // -------------- Move ---------------

            // Move the signal horizontally and vertically while the left mouse button is down
            if (me_MouseDrag == eMouseDrag.Move)
            {
                int s32_MoveX = mk_MouseDownLoc.X - e.X;
                int s32_MoveY = mk_MouseDownLoc.Y - e.Y;

                if (VerticalScroll.Visible)
                {
                    // ATTENTION: AutoScollPosition uses negative values for X and Y.
                    // But when SETTING a new value it must be positive!
                    AutoScrollPosition = new Point(-AutoScrollPosition.X, mk_MouseDownStart.Y + s32_MoveY);
                }

                ms32_DispStart = mk_MouseDownStart.X + s32_MoveX * ms32_DispSteps / ms32_Zoom;
                RecalcHorizScrollPos(true);
                return;
            }

            // -------------- Select ---------------

            // set mk_Selection in Samples
            if (me_MouseDrag == eMouseDrag.Select)
            {
                int s32_Left  = GetSampleAtScreenX(Math.Min(mk_MouseDownLoc.X, e.X));
                int s32_Right = GetSampleAtScreenX(Math.Max(mk_MouseDownLoc.X, e.X));
                if (s32_Left >= 0 && s32_Right >= 0)
                {
                    mk_Selection.ms32_Start = s32_Left;
                    mk_Selection.ms32_End   = s32_Right;
                    Invalidate();
                }
                return;
            }

            // -------------- Zoom ---------------

            // set mk_Zoom in Pixels
            if (me_MouseDrag == eMouseDrag.Zoom)
            {
                mk_Zoom.ms32_Start = Math.Min(mk_MouseDownLoc.X, e.X);
                mk_Zoom.ms32_End   = Math.Max(mk_MouseDownLoc.X, e.X);
                Invalidate();
                return;
            }

            if (e.Button != MouseButtons.None)
                return; // no tooltip if any button pressed

            // -------------- Tooltip ---------------

            Point k_Mouse = e.Location;
            if (k_Mouse != mk_LastTipPos) // avoid flickering tooltip
            {
                mk_LastTipPos = k_Mouse;
                ShowTooltip(k_Mouse);
            }
        }

        /// <summary>
        /// Hide Tooltip
        /// </summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HideTooltip();
            me_MouseDrag = eMouseDrag.None;
            Cursor = Cursors.Arrow;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (me_MouseDrag == eMouseDrag.Zoom && mk_Zoom.IsValid)
            {
                if (ms32_DispSteps == 1)
                {
                    Utils.FormMain.PrintStatus("The zoom is at maximum. Use Arrow Down key or double click to zoom out.", Color.Red);
                }
                else
                {
                    int s32_Left  = GetSampleAtScreenX(mk_Zoom.ms32_Start);
                    int s32_Right = GetSampleAtScreenX(mk_Zoom.ms32_End);
                    SetDisplayFactorForRange(s32_Right - s32_Left);
                    Utils.PanelOszi.JumpToSample(s32_Left);
                }
                mk_Zoom.Reset();
                Invalidate();
            }

            me_MouseDrag = eMouseDrag.None;
            Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// Rest the zoom factor
        /// </summary>
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (mi_ComboFactor.Items.Count >= 2)
                mi_ComboFactor.SelectedIndex = mi_ComboFactor.Items.Count - 2;
            else if (mi_ComboFactor.Items.Count >= 1)
                mi_ComboFactor.SelectedIndex = mi_ComboFactor.Items.Count - 1;
        }

        // --------------------------------------------------------

        void ShowTooltip(Point k_Mouse)
        {
            String s_Tooltip = GetTooltipText(k_Mouse);
            if (s_Tooltip == null)
            {
                HideTooltip();
                return;
            }
           
            k_Mouse.X += 10;
            k_Mouse.Y += 10;
            s_Tooltip  = s_Tooltip.TrimEnd('\n', '—');

            // If the tooltip is shown below the mouse pointer and does not fit on the screen, it flickers at the right of the screen.
            // In the upper half of the OsziPanel show the tooltip above the mouse.
            if (k_Mouse.Y > Height / 2)
            {
                int s32_Lines  = s_Tooltip.Split('\n').Length;
                int s32_Height = s32_Lines * 15 + 5; // tooltip line height: approx 15 pixels
                k_Mouse.Y     -= s32_Height + 20;    // show tooltip above the mouse
            }

            // Workaround for Windows 10 bug: Without coordinates the tooltip may never show up
            mi_Tooltip.Show(s_Tooltip, this, k_Mouse);
        }

        void HideTooltip()
        {
            mi_Tooltip.Hide(this);       // never call this directly, always use HideTooltip()
            mk_LastTipPos = Point.Empty; // IMPORTANT: Set empty!
        }

        /// <summary>
        /// returns null --> do not show the tooltip
        /// </summary>
        String GetTooltipText(Point k_Mouse)
        {
            String s_Tooltip  = null;
            int    s32_Sample = GetSampleAtScreenX(k_Mouse.X);

            if (s32_Sample < 0 || Utils.CurCapture.mi_Channels.Count != mi_DrawPos.mi_DrawChan.Length)
                return null; // channel added / removed --> count may differ

            s_Tooltip = String.Format("Mouse Position:\n"
                                    + "Sample Number:   {0:N0}\n"
                                    + "Absolute Time:   {1}\n"
                                    + Utils.TOOLTIP_SEPARATOR, 
                                    s32_Sample, Utils.CurCapture.FormatInterval(s32_Sample));

            if (ms32_CursorSpl >= 0)
            {
                // Distance of mouse from cursor in samples
                int s32_CursorDist = s32_Sample - ms32_CursorSpl;

                s_Tooltip += String.Format("Cursor Position:\n"
                                            + "Sample Number:   {0:N0}\n"
                                            + "Absolute Time:   {1}\n"
                                            + Utils.TOOLTIP_SEPARATOR 
                                            + "Distance Cursor to Mouse:\n"
                                            + "Sample Count:   {2:N0}\n"
                                            + "Interval:   {3}\n"
                                            + "Frequency:   {4}\n"
                                            + Utils.TOOLTIP_SEPARATOR,
                                            ms32_CursorSpl, 
                                            Utils.CurCapture.FormatInterval (ms32_CursorSpl),
                                            s32_CursorDist,
                                            Utils.CurCapture.FormatInterval (s32_CursorDist),
                                            Utils.CurCapture.FormatFrequency(Math.Abs(s32_CursorDist)));
            }

            if (mk_Selection.IsValid)
            {
                s_Tooltip += String.Format("Selection:\n"
                                            + "Start:   {0:N0} ({1})\n"
                                            + "End:     {2:N0} ({3})\n"
                                            + "Count:   {4:N0} ({5})\n"
                                            + Utils.TOOLTIP_SEPARATOR,
                                            mk_Selection.ms32_Start,
                                            Utils.CurCapture.FormatInterval(mk_Selection.ms32_Start),
                                            mk_Selection.ms32_End,
                                            Utils.CurCapture.FormatInterval(mk_Selection.ms32_End),
                                            mk_Selection.Count,
                                            Utils.CurCapture.FormatInterval(mk_Selection.Count));
            }

            int s32_PosY = k_Mouse.Y + VerticalScroll.Value;

            for (int C=0; C<Utils.CurCapture.mi_Channels.Count; C++)
            {
                Channel  i_Channel = Utils.CurCapture.mi_Channels[C];
                DrawChan i_Draw    = mi_DrawPos.mi_DrawChan[C];

                if (!i_Draw.ContainsY(s32_PosY))
                    continue; // mouse is not over this channel
                    
                // --------------------- Analog ------------------------------

                if (s32_PosY >= i_Draw.ms32_AnalTop && s32_PosY <= i_Draw.ms32_AnalBot)
                {
                    if (i_Channel.mb_AnalHidden)
                        continue; // the analog channel is invisible or overlayed by another analog channel

                    float f_Voltage = i_Draw.CalcVoltage(s32_PosY);
                    s_Tooltip += String.Format("Analog Resolution: {0} bit\n"
                                                + "Analog Voltage at Mouse:   {1:0.###} V\n", 
                                                Utils.CurCapture.ms32_AnalogRes, f_Voltage);

                    if (i_Channel.mb_Threshold)
                        s_Tooltip += String.Format("Threshold High:   {0:0.###} V\n"
                                                    + "Threshold Low:    {1:0.###} V\n", 
                                                    i_Channel.mf_ThreshHi, i_Channel.mf_ThreshLo);
                    break;
                }

                // ------------------------ Digital ---------------------------

                if (i_Channel.mu8_Digital != null && s32_PosY >= i_Draw.ms32_DigiTop && s32_PosY <= i_Draw.ms32_DigiBot)
                {
                    int  s32_First = s32_Sample;
                    int  s32_Last  = s32_Sample;
                    Byte  u8_State = i_Channel.mu8_Digital[s32_Sample];
                    String s_State = u8_State > 0 ? "High" : "Low";
                    for (int S=s32_Sample; S>=0; S--)
                    {
                        if (i_Channel.mu8_Digital[S] == u8_State) s32_First = S;
                        else break;
                    }
                    for (int S=s32_Sample; S<Utils.CurCapture.ms32_Samples; S++)
                    {
                        if (i_Channel.mu8_Digital[S] == u8_State) s32_Last = S;
                        else break;
                    }
                    int s32_Count = s32_Last - s32_First + 1;
                    s_Tooltip += String.Format("Digital {0} at Mouse:\n"
                                                + "Sample Count:   {1:N0}   ({2:N0} to {3:N0})\n"
                                                + "Interval:   {4}\n"
                                                + "Frequency:   {5}\n",
                                                s_State, s32_Count, s32_First, s32_Last, 
                                                Utils.CurCapture.FormatInterval(s32_Count),
                                                Utils.CurCapture.FormatFrequency(Math.Abs(s32_Count)));
                    break;
                }

                // ------------------------ Mark ---------------------------

                for (int M=0; M<i_Draw.ms32_MarkRows; M++)
                {
                    if (s32_PosY >= i_Draw.ms32_MarkTop[M] && s32_PosY <= i_Draw.ms32_MarkBot[M])
                    {
                        foreach (SmplMark i_Mark in i_Channel.mi_MarkRows[M])
                        {
                            if (s32_Sample >= i_Mark.ms32_FirstSample && s32_Sample <= i_Mark.ms32_LastSample)
                            {
                                String s_Text = (i_Mark.me_Mark == eMark.Text) ? '"' + i_Mark.ms_Text + '"' : i_Mark.me_Mark.ToString();
                                int s32_Last  = Math.Max(i_Mark.ms32_LastSample, i_Mark.ms32_FirstSample); // ms32_LastSample may be zero or -1
                                int s32_Count = s32_Last - i_Mark.ms32_FirstSample + 1;
                                s_Tooltip += String.Format("Sample Mark at Mouse:   {0}\n"
                                                            + "Sample Count:   {1:N0}   ({2:N0} to {3:N0})\n"
                                                            + "Interval:   {4}\n"
                                                            + "Frequency:   {5}\n",
                                                            s_Text, s32_Count, i_Mark.ms32_FirstSample, s32_Last, 
                                                            Utils.CurCapture.FormatInterval(s32_Count),
                                                            Utils.CurCapture.FormatFrequency(Math.Abs(s32_Count)));
                                break;
                            }
                        }
                    }
                }
                break;
            } // for (Channels)

            return s_Tooltip;
        }

        // --------------------------------------------------------

        void ShowMenu(Point k_Mouse, int s32_Sample)
        {
            if (s32_Sample < 0)
                return;

            int s32_PosY = k_Mouse.Y + VerticalScroll.Value;

            bool    b_Analog  = false;
            Channel i_Channel = null;
            for (int C=0; C<Utils.CurCapture.mi_Channels.Count; C++)
            {
                Channel  i_Chan = Utils.CurCapture.mi_Channels[C];
                DrawChan i_Draw = mi_DrawPos.mi_DrawChan[C];

                if (s32_PosY >= i_Draw.ms32_AnalTop && s32_PosY <= i_Draw.ms32_AnalBot)
                {
                    if (i_Chan.mb_AnalHidden)
                        continue;

                    i_Channel = i_Chan;
                    b_Analog  = true;
                    break;
                }

                if (s32_PosY >= i_Draw.ms32_DigiTop && s32_PosY <= i_Draw.ms32_DigiBot)
                {
                    i_Channel = i_Chan;
                    b_Analog  = false;
                    break;
                }
            }

            // Load all menu entries of all IOperation classes and show the menu
            OperationManager.ShowMenu(s32_Sample, i_Channel, b_Analog, k_Mouse);
        }

        // --------------------------------------------------------

        /// <summary>
        /// returns the sample under the mouse or -1 if mouse is outside the valid area
        /// </summary>
        int GetSampleAtScreenX(int s32_X)
        {
            if (mi_DrawPos == null || Utils.CurCapture == null)
                return -1;

            int s32_PosX = s32_X - mi_DrawPos.ms32_LegendWidth;
            if (s32_PosX < 0 || s32_PosX > mi_DrawPos.ms32_SignalWidth)
                return -1;

            int s32_Sample = ms32_DispStart + s32_PosX * ms32_DispSteps / ms32_Zoom;
            if (s32_Sample < 0 || s32_Sample >= Utils.CurCapture.ms32_Samples)
                return -1;

            return s32_Sample;
        }

        // =====================================================================================

        /// <summary>
        /// b_FullWidth = true  --> save image with the width selected in comboFactor (ms32_GraphWidth)
        /// b_FullWidth = false --> save screenshot of visible area
        /// </summary>
        public String SaveAsImage(String s_Path, bool b_FullWidth)
        {
            Rectangle r_Area = ClientRectangle;
            int s32_GraphWidth = Utils.CurCapture.ms32_Samples * ms32_Zoom / ms32_DispSteps + mi_DrawPos.ms32_LegendWidth;

            int s32_First, s32_Last;
            if (b_FullWidth)
            {
                r_Area.Width = s32_GraphWidth;
                s32_First    = 0;
                s32_Last     = Utils.CurCapture.ms32_Samples;
            }
            else // Screenshot
            {
                r_Area.Width = Math.Min(r_Area.Width, s32_GraphWidth);
                s32_First    = ms32_DispStart;
                s32_Last     = ms32_DispEnd;
            }

            r_Area.Width = Math.Min(r_Area.Width, Utils.CurCapture.ms32_Samples);
            if (r_Area.Width > 32760)
                throw new Exception("An image with "+r_Area.Width+" pixels width cannot be generated.\n"
                                  + "The maximum width is 32760 pixels.\n"
                                  + "Please select a smaller display factor.");

            Utils.FormMain.Cursor = Cursors.WaitCursor;

            DrawPos i_Pos = CalcVerticalDrawPos(r_Area.Height, false);
            r_Area.Height = i_Pos.ms32_SignalBot;

            using (Bitmap   i_Bitmap   = new Bitmap(r_Area.Width, r_Area.Height, PixelFormat.Format24bppRgb))
            using (Graphics i_Graphics = Graphics.FromImage(i_Bitmap))
            {
                Draw(i_Graphics, i_Pos, r_Area, s32_First, s32_Last);
                i_Bitmap.Save(s_Path, ImageFormat.Png);
            }

            Utils.FormMain.Cursor = Cursors.Arrow;

            return b_FullWidth ? "full image" : "screenshot";
        }

        // =====================================================================================

        #if DEBUG_DRAWING
            public new void Invalidate()
            {
                Debug.Print("Invalidate()");
                base.Invalidate();
            }

            protected override void WndProc(ref Message k_Msg)
            {
                Debug.Print("WndProc Message 0x{0:X4}", k_Msg.Msg);
                base.WndProc(ref k_Msg);
            }
        #endif

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // OptimizedDoubleBuffer creates an empty Bitmap that is black --> no clearing required for a black background
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            #if DEBUG_DRAWING
                Debug.Print("OnPaint(ClipRect: {0})", e.ClipRectangle);
            #endif

            try
            {
                e.Graphics.TranslateTransform(0, -VerticalScroll.Value);
                Draw(e.Graphics, mi_DrawPos, ClientRectangle, ms32_DispStart, ms32_DispEnd);
            }
            catch (Exception Ex)
            {
                e.Graphics.FillRectangle(Brushes.Black, ClientRectangle);
                e.Graphics.DrawString(Ex.Message + "\n\n" + Ex.StackTrace, Font, Utils.ERROR_BRUSH, AutoScrollPosition.X + 5, 5);
            }
        }

        /// <summary>
        /// Draw on screen or into Bitmap
        /// </summary>
        void Draw(Graphics i_Graphics, DrawPos i_Pos, Rectangle r_Area, int s32_FirstSpl, int s32_LastSpl)
        {
            if (Utils.CurCapture == null || i_Pos == null) 
                return;

            // Signal Left = Legend Width (depends on length of channel names and voltages)
            int s32_SigLeft = MeasureLegend(i_Pos, i_Graphics);

            // The same rounding-up to ms32_DispSteps must be applied as in CalcSampleMinMax() to mu8_DigiChange
            int s32_RoundOffX = ms32_DispSteps - 1 - s32_FirstSpl;
            
            int s32_GraphWidth = Utils.CurCapture.ms32_Samples * ms32_Zoom / ms32_DispSteps;
            i_Pos.ms32_SignalWidth = Math.Min(r_Area.Width, s32_GraphWidth);

            // --------------- Vertical Range --------------

            int s32_VertTop  = mi_DrawPos.ms32_SignalTop - 30;
            int s32_VertBot  = mi_DrawPos.ms32_SignalBot + 30;
            int s32_VertDiff = MIN_CURSOR_HEIGHT - (s32_VertBot - s32_VertTop);
            if (s32_VertDiff > 0)
            {
                s32_VertTop -= s32_VertDiff / 2;
                s32_VertBot += s32_VertDiff / 2;
            }

            // ----------------- Separators ----------------

            foreach (int s32_SeparatorSpl in Utils.CurCapture.ms32_Separators)
            {
                if (s32_SeparatorSpl > s32_FirstSpl && s32_SeparatorSpl < s32_LastSpl)
                {
                    int X = (s32_SeparatorSpl + s32_RoundOffX) * ms32_Zoom / ms32_DispSteps + s32_SigLeft;
                    i_Graphics.DrawLine(Pens.Red, X, s32_VertTop, X, s32_VertBot);
                }
            }

            // --------------- Cursor & Raster -------------

            // Draw a dashed vertical line at the cursor position
            if (ms32_CursorSpl >= s32_FirstSpl)
            {
                int X = (ms32_CursorSpl + s32_RoundOffX) * ms32_Zoom / ms32_DispSteps + s32_SigLeft;
                i_Graphics.DrawLine(mi_PenCursor, X, s32_VertTop, X, s32_VertBot);
            }

            // Draw dotted vertical raster lines
            String s_RasterOff = "     (wrong interval)";
            if (ms32_CursorSpl >= 0 && md_RasterSamples >= ms32_DispSteps * 10)
            {
                s_RasterOff = "";

                // Calculate raster lines before cursor and after cursor
                int s32_Before = (int)((ms32_CursorSpl - s32_FirstSpl) / md_RasterSamples);
                int s32_After  = (int)((s32_LastSpl - ms32_CursorSpl)  / md_RasterSamples);
                if (s32_Before > 0)
                {
                    decimal d_Raster = ms32_CursorSpl - md_RasterSamples * (s32_Before + 1);
                    while  (d_Raster < ms32_CursorSpl)
                    {
                        int X = ((int)(d_Raster + 0.5m) + s32_RoundOffX) * ms32_Zoom / ms32_DispSteps + s32_SigLeft;
                        if (X >= i_Pos.ms32_SignalWidth)
                            break;

                        if (X > s32_SigLeft)
                            i_Graphics.DrawLine(mi_PenRaster, X, s32_VertTop, X, s32_VertBot);

                        d_Raster += md_RasterSamples;
                    }
                }

                if (s32_After > 0)
                {
                    decimal d_Raster = ms32_CursorSpl + md_RasterSamples * (s32_After + 1);
                    while  (d_Raster > ms32_CursorSpl)
                    {
                        int X = ((int)(d_Raster + 0.5m) + s32_RoundOffX) * ms32_Zoom / ms32_DispSteps + s32_SigLeft;
                        if (X <= s32_SigLeft)
                            break;

                        if (X < i_Pos.ms32_SignalWidth)
                            i_Graphics.DrawLine(mi_PenRaster, X, s32_VertTop, X, s32_VertBot);

                        d_Raster -= md_RasterSamples;
                    }
                }
            }

            if (ms_RasterLegend != null)
                i_Graphics.DrawString(ms_RasterLegend + s_RasterOff, Font, Brushes.Gray, 7, r_Area.Bottom - 20);

            // ---------------- Channel Loop ---------------
  
            bool  b_DrawVoltOnce = true;
            bool  b_DrawZeroOnce = true;
            for (int Ch=0; Ch<Utils.CurCapture.mi_Channels.Count; Ch++)
            {
                Channel   i_Chan   = Utils.CurCapture.mi_Channels[Ch];
                DrawChan  i_Draw   = i_Pos.mi_DrawChan[Ch];
                SplMinMax i_MinMax = i_Chan.mi_SampleMinMax;

                Debug.Assert(i_Chan.mf_Analog   == null || i_MinMax.mb_AnalogOK,  "You must always call RecalculateEverything() after modifying analog data in any operation!");
                Debug.Assert(i_Chan.mu8_Digital == null || i_MinMax.mb_DigitalOK, "You must always call RecalculateEverything() after modifying digital data in any operation!");

                int   s32_Color   = i_Chan.ms32_ColorIdx % mc_Colors.Length;
                Pen   i_SigPen    = mi_SignalPens   [s32_Color];
                Pen   i_ThreshPen = mi_ThresholdPens[s32_Color];
                Brush i_TxtBrush  = mi_TxtBrushs    [s32_Color];

                if (mb_SeparateChannels)
                    i_Chan.mb_AnalHidden = false;

                if (mb_ShowLegend)
                {
                    // -------------- Analog Legend ----------------

                    if (i_Chan.mf_Analog != null)
                    {
                        i_Graphics.DrawLine(mi_PenGray, s32_SigLeft -6, i_Draw.ms32_AnalTop, s32_SigLeft -1, i_Draw.ms32_AnalTop); // hor
                        i_Graphics.DrawLine(mi_PenGray, s32_SigLeft -1, i_Draw.ms32_AnalTop, s32_SigLeft -1, i_Draw.ms32_AnalBot); // vert
                        i_Graphics.DrawLine(mi_PenGray, s32_SigLeft -6, i_Draw.ms32_AnalBot, s32_SigLeft -1, i_Draw.ms32_AnalBot); // hor

                        if (mb_SeparateChannels || b_DrawVoltOnce)
                        {
                            // If channels are drawn one on top of the other, ms_MinVolt and ms_MaxVolt are the same for all channels.
                            b_DrawVoltOnce = false;
                            Brush i_VoltBrush = mb_SeparateChannels ? i_TxtBrush : Brushes.White;
                            i_Graphics.DrawString(i_Draw.ms_MaxVolt, Font, i_VoltBrush, s32_SigLeft -8, i_Draw.ms32_VoltTop, Utils.AlignTopRight);
                            i_Graphics.DrawString(i_Draw.ms_MinVolt, Font, i_VoltBrush, s32_SigLeft -8, i_Draw.ms32_VoltBot, Utils.AlignBottomRight);
                        }

                        if (i_Draw.mi_CheckBox == null)
                            i_Graphics.DrawString(i_Chan.ms_Name, Font, i_TxtBrush, s32_SigLeft -8, i_Draw.ms32_NameTop, Utils.AlignTopRight);
                    }

                    // -------------- Digital Legend ----------------

                    if (i_Chan.mu8_Digital != null)
                    {
                        i_Graphics.DrawLine(mi_PenGray, s32_SigLeft -6, i_Draw.ms32_DigiTop, s32_SigLeft -1, i_Draw.ms32_DigiTop); // hor
                        i_Graphics.DrawLine(mi_PenGray, s32_SigLeft -1, i_Draw.ms32_DigiTop, s32_SigLeft -1, i_Draw.ms32_DigiBot); // vert
                        i_Graphics.DrawLine(mi_PenGray, s32_SigLeft -6, i_Draw.ms32_DigiBot, s32_SigLeft -1, i_Draw.ms32_DigiBot); // hor

                        i_Graphics.DrawString(i_Chan.ms_Name, Font, i_TxtBrush, s32_SigLeft -8, i_Draw.ms32_DigiTop + 2, Utils.AlignTopRight);
                    }
                }

                // ---------- Draw analog zero line & threshsold lines -----------

                if (i_Chan.mf_Analog != null && !i_Chan.mb_AnalHidden)
                {
                    // Draw zero line
                    if (i_Chan.mf_Min <= 0.0f && i_Chan.mf_Max >= 0.0f && (mb_SeparateChannels || b_DrawZeroOnce))
                    {
                        b_DrawZeroOnce = false;
                        int s32_Zero = i_Draw.CalcPixelPosY(0.0f);
                        i_Graphics.DrawLine(mi_PenGray, s32_SigLeft, s32_Zero, r_Area.Width, s32_Zero); // horizontal gray
                    }

                    // Draw threshold line
                    if (i_Chan.mb_Threshold)
                    {
                        int s32_Above = i_Draw.CalcPixelPosY(i_Chan.mf_ThreshHi);
                        int s32_Below = i_Draw.CalcPixelPosY(i_Chan.mf_ThreshLo);

                        i_Graphics.DrawLine(i_ThreshPen, s32_SigLeft, s32_Above, r_Area.Width, s32_Above); // horizontal gray
                        i_Graphics.DrawLine(i_ThreshPen, s32_SigLeft, s32_Below, r_Area.Width, s32_Below); // horizontal gray
                    }
                }

                // -------------- Draw digital zero line --------------

                if (i_Chan.mu8_Digital != null)
                {
                    i_Graphics.DrawLine(mi_PenGray, s32_SigLeft, i_Draw.ms32_DigiBot, r_Area.Width, i_Draw.ms32_DigiBot); // horizontal gray
                }

                // ------------------ Prepare Marks -------------------

                int[] s32_MarkIdx = new int[i_Draw.ms32_MarkRows];
                
                // Skip Marks that are scrolled out of view at the left
                if (i_Chan.mi_MarkRows != null)
                {
                    for (int Row=0; Row<i_Draw.ms32_MarkRows; Row++)
                    {
                        List<SmplMark> i_Marks = i_Chan.mi_MarkRows[Row]; // never null (see setting ms32_MarkRows)
                        int s32_Idx = 0;
                        while (s32_Idx < i_Marks.Count && i_Marks[s32_Idx].ms32_LastSample < s32_SigLeft)
                        {
                            s32_Idx ++;
                        }
                        s32_MarkIdx[Row] = s32_Idx;
                    }
                }

                // ------------------- Sample Loop --------------------

                Byte u8_PrevDigi  = 0;
                int  s32_PrevAnal = 0;

                int X = s32_SigLeft;
                int L = X; // last X
                int S = s32_FirstSpl;
                s32_LastSpl = Math.Min(s32_LastSpl, Utils.CurCapture.ms32_Samples);
                while (S < s32_LastSpl)
                {
                    // -------------- Draw analog signal --------------

                    int D = S / ms32_DispSteps;

                    if (i_Chan.mf_Analog != null && !i_Chan.mb_AnalHidden)
                    {
                        if (ms32_DispSteps == 1) // Zoom mode
                        {
                            int s32_AnalY = i_Draw.CalcPixelPosY(i_Chan.mf_Analog[S]);
                            if (X > L) // omit first sample
                                i_Graphics.DrawLine(i_SigPen, L, s32_PrevAnal, X, s32_AnalY); // diagonal
                            
                            s32_PrevAnal = s32_AnalY;
                        }
                        else
                        {
                            int s32_AnalMin = i_Draw.CalcPixelPosY(i_MinMax.mf_AnalogMin[D]);
                            int s32_AnalMax = i_Draw.CalcPixelPosY(i_MinMax.mf_AnalogMax[D]);
                            if (X > L) // omit first sample
                            {
                                if (s32_AnalMin == s32_AnalMax)
                                    i_Graphics.DrawLine(i_SigPen, L, s32_AnalMin, X, s32_AnalMin); // horiz
                                else
                                    i_Graphics.DrawLine(i_SigPen, X, s32_AnalMin, X, s32_AnalMax); // vert
                            }
                            s32_PrevAnal = s32_AnalMin;
                        }
                    }

                    // -------------- Draw digital signal --------------

                    if (i_Chan.mu8_Digital != null)
                    {
                        Byte u8_High   = i_MinMax.mu8_DigitalHi [D];
                        Byte u8_Change = i_MinMax.mu8_DigiChange[D];
                        if (X > L) // omit first sample
                        {
                            if (u8_Change > 0)
                                i_Graphics.DrawLine(i_SigPen, X, i_Draw.ms32_DigiTop, X, i_Draw.ms32_DigiBot); // vert

                            int s32_DigiY = u8_PrevDigi > 0 ? i_Draw.ms32_DigiTop : i_Draw.ms32_DigiBot;
                            i_Graphics.DrawLine(i_SigPen, L, s32_DigiY, X, s32_DigiY); // horiz
                        }
                        u8_PrevDigi = u8_High;
                    }

                    // ---------------- Draw marks ----------------

                    // Draw the clock ticks and the decoded data below the digital signal
                    if (i_Chan.mi_MarkRows != null)
                    {
                        for (int Row=0; Row<i_Draw.ms32_MarkRows; Row++)
                        {
                            List<SmplMark> i_Marks = i_Chan.mi_MarkRows[Row]; // never null (see setting ms32_MarkRows)
                            int s32_Idx = s32_MarkIdx[Row];
                            int s32_Top = i_Draw.ms32_MarkTop[Row];
                            int s32_Bot = i_Draw.ms32_MarkBot[Row];
                            Font i_Font = (Row == 0) ? Font : mi_Courier;

                            while (s32_Idx < i_Marks.Count && S >= i_Marks[s32_Idx].ms32_FirstSample)
                            {
                                SmplMark i_Mark = i_Marks[s32_Idx];

                                int s32_Left  = (i_Mark.ms32_FirstSample + s32_RoundOffX) * ms32_Zoom / ms32_DispSteps + s32_SigLeft;
                                int s32_Right = (i_Mark.ms32_LastSample  + s32_RoundOffX) * ms32_Zoom / ms32_DispSteps + s32_SigLeft;
                                
                                if (s32_Left >= s32_SigLeft) 
                                    i_Graphics.DrawLine(i_Mark.mi_PenStart, s32_Left,  s32_Top, s32_Left,  s32_Bot); // left  vert separator

                                if (s32_Right >= s32_SigLeft) 
                                    i_Graphics.DrawLine(i_Mark.mi_PenEnd, s32_Right, s32_Top, s32_Right, s32_Bot); // right vert separator
                                
                                if (i_Mark.ms_Text != null) 
                                {
                                    // center-align between s32_Left and s32_Right or left-align if both are the same.
                                    StringFormat i_Align = Utils.AlignTopLeft;
                                    int s32_TxtPos = s32_Left + 1;
                                    if (s32_Right > s32_Left)
                                    {
                                        i_Align    = Utils.AlignTopCenter;
                                        s32_TxtPos = (s32_Left + s32_Right) / 2;
                                    }

                                    if (s32_TxtPos >= s32_SigLeft)
                                        i_Graphics.DrawString(i_Mark.ms_Text, i_Font, i_Mark.mi_TxtBrush, s32_TxtPos, s32_Top, i_Align);
                                }

                                s32_Idx ++;
                            }
                            s32_MarkIdx[Row] = s32_Idx;
                        }
                    }

                    S += ms32_DispSteps;
                    L = X;
                    X += ms32_Zoom;
                } // while (d_PosS)
            } // for (Channel)

            // ---------------- Selected Area --------------

            // mk_Selection in Samples
            // Draw with transparent brush on top of the graphs
            if (mk_Selection.IsValid && mk_Selection.ms32_End   >= s32_FirstSpl && 
                                        mk_Selection.ms32_Start <= s32_LastSpl)
            {
                int X1 = (Math.Max(mk_Selection.ms32_Start, s32_FirstSpl) + s32_RoundOffX) * ms32_Zoom / ms32_DispSteps + s32_SigLeft;
                int X2 = (Math.Min(mk_Selection.ms32_End,   s32_LastSpl)  + s32_RoundOffX) * ms32_Zoom / ms32_DispSteps + s32_SigLeft;
                X1 = Math.Max(X1, s32_SigLeft);
                X2 = Math.Min(X2, s32_SigLeft + i_Pos.ms32_SignalWidth);

                // Even at the lowest display factor the selection area should always be visible
                int s32_Width = Math.Max(10, X2 - X1);
                i_Graphics.FillRectangle(Utils.HIGHLIGHT_BRUSH, X1, s32_VertTop, s32_Width, s32_VertBot - s32_VertTop);
            }

            // ---------------- Zoom Rectangle --------------

            // mk_Zoom in Pixels
            if (me_MouseDrag == eMouseDrag.Zoom && mk_Zoom.IsValid)
            {
                int s32_Height = Height;
                if (HorizontalScroll.Visible)
                    s32_Height -= SystemInformation.HorizontalScrollBarHeight;

                // AutoScrollPosition.Y is negative!
                int s32_Top = Math.Max(s32_VertTop, 3 - AutoScrollPosition.Y);
                int s32_Bot = Math.Min(s32_VertBot, s32_Height - AutoScrollPosition.Y - 3);

                // draw 3 nested rectangles: black, lime, black
                Rectangle r_Rect = new Rectangle(mk_Zoom.ms32_Start, s32_Top, mk_Zoom.Count, s32_Bot - s32_Top);
                i_Graphics.DrawRectangle(Pens.Lime,  r_Rect);
                r_Rect.Inflate(1, 1);
                i_Graphics.DrawRectangle(Pens.Black, r_Rect);
                r_Rect.Inflate(-2, -2);
                i_Graphics.DrawRectangle(Pens.Black, r_Rect);
            }
        }
    }
}
