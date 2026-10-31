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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using IOperation        = Operations.OperationManager.IOperation;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using Utils             = OsziWaveformAnalyzer.Utils;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using PlatformManager   = Platform.PlatformManager;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;
using IPanelAction      = OsziWaveformAnalyzer.FormMain.IPanelAction;

namespace OsziWaveformAnalyzer
{
    /// <summary>
    /// This class is highly speed optimized.
    /// It draws Lissajous figures
    /// </summary>
    public class PanelXY : Panel, IPanelAction
    {
        #region class FastBitmap

        /// <summary>
        /// This class is highly speed optimized.
        /// It accesses the pixels of the Bitmap directly.
        /// </summary>
        class FastBitmap : IDisposable
        {
            Bitmap    mi_Bitmap;
            Rectangle mk_Bounds;
            int[]     ms32_PixelData; // Byte array with 4 bytes = A, R, G, B for each pixel

            public Bitmap Bitmap
            {
                get { return mi_Bitmap; }
            }

            // -------------------------------------------------------

            // Constructor
            public FastBitmap(int s32_Width, int s32_Height, Color c_BackColor)
            {
                Debug.Assert(c_BackColor.A == 0xFF, "Backcolor must be fully opaque.");

                mi_Bitmap      = new Bitmap(s32_Width, s32_Height, PixelFormat.Format32bppArgb);
                mk_Bounds      = new Rectangle(0, 0, s32_Width, s32_Height);
                ms32_PixelData = new int[s32_Width * s32_Height];

                using (Graphics i_Grapth = Graphics.FromImage(mi_Bitmap))
                {
                    i_Grapth.Clear(c_BackColor);
                }
            }

            public void Dispose()
            {
                // free a lot of memory
                if (mi_Bitmap != null)
                    mi_Bitmap.Dispose();
            
                ms32_PixelData = null; // memory released by garbage collector
            }

            // -------------------------------------------------------

            /// <summary>
            /// Set the pixel data of another bitmap of the same size
            /// </summary>
            public void SetPixelData(int[] s32_Pixels)
            {
                Debug.Assert(s32_Pixels.Length == ms32_PixelData.Length);
                Array.Copy(s32_Pixels, ms32_PixelData, ms32_PixelData.Length); 
            }

            /// <summary>
            /// Copy the bitmap pixels into ms32_PixelData
            /// </summary>
            public int[] CopyToPixelData()
            {
                BitmapData i_Data = mi_Bitmap.LockBits(mk_Bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                Debug.Assert(i_Data.Stride == 4 * mk_Bounds.Width);

                Marshal.Copy(i_Data.Scan0, ms32_PixelData, 0, ms32_PixelData.Length);

                mi_Bitmap.UnlockBits(i_Data);
                return ms32_PixelData;
            }

            /// <summary>
            /// Copy ms32_PixelData into the bitmap pixels
            /// </summary>
            public Bitmap CopyToBitmap()
            {
                BitmapData i_Data = mi_Bitmap.LockBits(mk_Bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                Debug.Assert(i_Data.Stride == 4 * mk_Bounds.Width);

                Marshal.Copy(ms32_PixelData, 0, i_Data.Scan0, ms32_PixelData.Length);

                mi_Bitmap.UnlockBits(i_Data);
                return mi_Bitmap;
            }

            // -------------------------------------------------------

            /// <summary>
            /// Draw a pixel into ms32_PixelData.
            /// The coordinate origin is top left.
            /// </summary>
            public void DrawPixel(int s32_X, int s32_Y, int s32_Color)
            {
                if (s32_X < 0 || s32_X >= mk_Bounds.Width ||
                    s32_Y < 0 || s32_Y >= mk_Bounds.Height)
                    return;

                int s32_Offset = s32_Y * mi_Bitmap.Width + s32_X;
                ms32_PixelData[s32_Offset] = s32_Color;
            }

            /// <summary>
            /// Draw a line into ms32_PixelData.
            /// </summary>
            public void DrawLine(int X0, int Y0, int X1, int Y1, int s32_Color)
            {
                int s32_Steps = Math.Max(Math.Abs(X1 - X0), Math.Abs(Y1 - Y0));
                if (s32_Steps == 0)
                {
                    DrawPixel(X0, Y0, s32_Color);
                    return;
                }

                float dX = (float)(X1 - X0) / s32_Steps;
                float dY = (float)(Y1 - Y0) / s32_Steps;
                for (int i=0; i<s32_Steps; i++)
                {
                    int X = (int)(X0 + i * dX);
                    int Y = (int)(Y0 + i * dY);
                    DrawPixel(X, Y, s32_Color);
                }
            }
        }

        #endregion

        const    int    MARGIN_LEFT   = 55;
        const    int    MARGIN_RIGHT  = 10;
        const    int    MARGIN_TOP    = 10;
        readonly int    MARGIN_BOTTOM = 22 + SystemInformation.HorizontalScrollBarHeight;
        const    double INTENS_MIN    = 0x20; // Alpha of darkest line
        const    double INTENS_MAX    = 0xFF; // Alpha of brightest line

        HScrollBar     mi_Scrollbar;
        Rectangle      mr_Plot;   // The area where the lissajous figure is drawn (without the labels of the coordinate system)
        Point          mk_LastTipPos;
        FastBitmap     mi_Lissajous;
        int[]          ms32_CoordSystem; // FastBitmap pixels exported into int[] array
        Channel        mi_ChannelX;
        Channel        mi_ChannelY;
        bool           mb_Valid;
        bool           mb_AutoRepeat;
        double         md_Scale;
        double         md_ViewX0;
        double         md_ViewY0;
        int            ms32_StartSample;
        int            ms32_GlowCount;
        ToolTip        mi_Tooltip    = new ToolTip();
        Timer          mi_LabelTimer = new Timer();

        public bool HasData
        {
            get { return mb_Valid; }
        }

        /// <summary>
        /// Called from FormMain.OnLoad()
        /// </summary>
        public void Init(HScrollBar i_Scrollbar)
        {
            mi_Scrollbar   = i_Scrollbar;
            DoubleBuffered = true; // creates an empty Bitmap that is black
            TabStop        = true;

            // IMPORTANT:
            // A Panel can normally not be selected (Panel.CanSelect returns false).
            // The following line is required to give the Panel the keyboard focus when clicking into it.
            // This allows scrolling with the mouse wheel.
            // Additionally in OnMouseDown() it is required to call Focus()
            SetStyle(ControlStyles.Selectable, true);

            mi_LabelTimer.Interval = 250;
            mi_LabelTimer.Tick += new EventHandler(OnLabelTimer_Tick);
        }

        // ==================== User Actions =======================

        // interface IPanelAction
        // The user changed the tab or the old capture has been deleted or a new capture was loaded
        public void OnUserAction(eTab e_ActiveTab, Capture i_Capture)
        {
            mi_LabelTimer.Stop();

            // free a lot of memory
            if (i_Capture == null)
            {
                if (mi_Lissajous != null)
                {
                    mi_Lissajous.Dispose(); // free bitmap memory
                    mi_Lissajous = null;
                }

                ms32_StartSample = 0;
                mb_Valid         = false;
                ms32_CoordSystem = null; // memory released by garbage collector
                mi_ChannelX      = null; // memory released by garbage collector
                mi_ChannelY      = null; // memory released by garbage collector
                return;
            }

            if (e_ActiveTab == eTab.XYPlot) // show X/Y plot
            {
                if (i_Capture.ms32_AnalogCount != 2)
                {
                    Utils.FormMain.SetLabel("The X/Y Plot requires a capture with exactly 2 analog channels.");
                    return;
                }

                // Oscilloscope Music uses Left = X, Right = Y
                mi_ChannelX = i_Capture.mi_Channels[0];
                mi_ChannelY = i_Capture.mi_Channels[1];

                // If the user has assigned valid names in wrong order --> swap channels
                if ((mi_ChannelX.ms_Name == "Right" || mi_ChannelX.ms_Name == "Y") && 
                    (mi_ChannelY.ms_Name == "Left"  || mi_ChannelY.ms_Name == "X"))
                {
                    mi_ChannelX = i_Capture.mi_Channels[1];
                    mi_ChannelY = i_Capture.mi_Channels[0];
                }

                mb_Valid = true; // FIRST           
                DrawAll();       // AFTER

                Focus(); // set keyboard focus
                mi_LabelTimer.Start();
            }
        }

        void OnLabelTimer_Tick(object sender, EventArgs e)
        {
            if (mi_ChannelX != null)
                Utils.FormMain.SetLabel(String.Format("Total Samples: {0:N0}   Show {1} Samples: {2:N0} to {3:N0}", 
                                        mi_ChannelX.mf_Analog.Length, ms32_GlowCount, ms32_StartSample, ms32_StartSample + ms32_GlowCount));
        }

        public String SaveAsImage(String s_Path)
        {
            if (!mb_Valid)
                throw new Exception("No X/Y Plot to be saved.");

            mi_Lissajous.Bitmap.Save(s_Path, ImageFormat.Png);
            return "X/Y Plot";
        }

        /// <summary>
        /// Called from FormMain.OnLoad() and when the user has changed the Control settings
        /// </summary>
        public void OnUserSettgChanged(int s32_GlowCount, bool b_AutoRepeat)
        {
            ms32_GlowCount = s32_GlowCount;
            mb_AutoRepeat  = b_AutoRepeat;

            if (mb_Valid)
            {
                Focus();
                DrawAll();
            }
        }

        // ======================== GUI Events ========================

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            mi_Tooltip.Hide(this);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!mb_Valid || e.Button != MouseButtons.None || !mr_Plot.Contains(e.Location))
            {
                mi_Tooltip.Hide(this);
                return;
            }

            if (e.Location == mk_LastTipPos)
                return; // avoid flickering

            mk_LastTipPos = e.Location;

            String s_Tooltip = String.Format("Voltage at mouse X = {0}\n"
                                           + "Voltage at mouse Y = {1}\n"
                                           + "Analog Resolution: {2} bit\n"
                                           + Utils.TOOLTIP_SEPARATOR
                                           + "Start Sample: {3:N0}  ({4})\n" 
                                           + "End Sample: {5:N0}  ({6})", 
                                             Utils.FormatVoltage(PixelXToVolt(e.X)),
                                             Utils.FormatVoltage(PixelYToVolt(e.Y)),
                                             Utils.CurCapture.ms32_AnalogRes,
                                             ms32_StartSample,
                                             Utils.CurCapture.FormatInterval(ms32_StartSample),
                                             ms32_StartSample + ms32_GlowCount,
                                             Utils.CurCapture.FormatInterval(ms32_StartSample + ms32_GlowCount));

            // Workaround for Windows 10 bug: Without coordinates the tooltip may never show up
            mi_Tooltip.Show(s_Tooltip, this, e.X + 15, e.Y + 15);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            mi_Tooltip.Hide(this);
        }

        // ---------------------------------------------

        /// <summary>
        /// IMPORTANT:
        /// Without this function the arrow keys never get through to OnKeyDown()
        /// </summary>
        protected override bool IsInputKey(Keys e_Key) 
        {
            switch (e_Key)
            {
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

            if (e.Alt || e.Control || !mb_Valid)
                return;

            // ATTENTION:
            // This works only if the X/Y Plot has the keyboard focus.
            // Otherwise the keys go to the scrollbar.
            int s32_Offset = e.Shift ? 20 : 1;
            switch (e.KeyCode)
            {
                case Keys.Left:  ms32_StartSample -= s32_Offset; break;
                case Keys.Right: ms32_StartSample += s32_Offset; break;
                default: return;
            }

            ms32_StartSample = Math.Max(ms32_StartSample, 0);
            ms32_StartSample = Math.Min(ms32_StartSample, mi_Scrollbar.Maximum - mi_Scrollbar.LargeChange);

            mi_Scrollbar.Value = ms32_StartSample;

            DrawLissajous();
        }

        // ---------------------------------------------

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (mb_Valid)
                DrawAll(); // create a new Bitmap
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // no clearing required here because mi_Lissajous.Bitmap has a black background
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (mb_Valid)
                e.Graphics.DrawImageUnscaled(mi_Lissajous.Bitmap, 0, 0);
        }

        // ======================== Draw All ========================

        private void DrawAll()
        {
            mr_Plot = new Rectangle(MARGIN_LEFT, MARGIN_TOP, Width - MARGIN_LEFT - MARGIN_RIGHT, Height - MARGIN_TOP - MARGIN_BOTTOM);

            // 5% margin, avoid zero range for constant signals
            double d_SpanX = Math.Max(mi_ChannelX.mf_Max - mi_ChannelX.mf_Min, 1e-6) * 1.1;
            double d_SpanY = Math.Max(mi_ChannelY.mf_Max - mi_ChannelY.mf_Min, 1e-6) * 1.1;
            md_Scale = Math.Max(d_SpanX / mr_Plot.Width, d_SpanY / mr_Plot.Height);

            md_ViewX0 = (mi_ChannelX.mf_Min + mi_ChannelX.mf_Max) / 2 - md_Scale * mr_Plot.Width  / 2;
            md_ViewY0 = (mi_ChannelY.mf_Min + mi_ChannelY.mf_Max) / 2 - md_Scale * mr_Plot.Height / 2;

            if (mi_Lissajous != null)
                mi_Lissajous.Dispose();

            mi_Lissajous = new FastBitmap(Width, Height, Color.Black);

            DrawCoordinateSystem();
            DrawLissajous();
        }

        // ======================== Coordinates ========================

        /// <summary>
        /// Draw the coordinate system and exports the bitmap pixels into ms32_CoordSystem.
        /// This is only called once and when the window is resized.
        /// </summary>
        private void DrawCoordinateSystem()
        {
            Brush i_BrushX = PanelOszi.GetChannelBrush(mi_ChannelX);
            Brush i_BrushY = PanelOszi.GetChannelBrush(mi_ChannelY);

            using(FastBitmap i_FastBmp = new FastBitmap(Width, Height, Color.Black))
            using(Graphics   i_Graph   = Graphics.FromImage(i_FastBmp.Bitmap))
            {
                // -------- Horizontal grid lines with Y voltage labels ---------

                // The maximum count of horizontal grid lines must depend on the window size.
                // The minimum height of a vertical raster should be 40 pixel
                int s32_MaxLines = mr_Plot.Height / 40;
                double d_StepY = Utils.CalcRasterInterval(md_Scale * mr_Plot.Height, s32_MaxLines);
                for (int s32_Grid = (int)(md_ViewY0 / d_StepY); 
                         s32_Grid * d_StepY <= md_ViewY0 + md_Scale * mr_Plot.Height; 
                         s32_Grid++)
                {
                    double d_Volt = s32_Grid * d_StepY;
                    bool   b_Zero = Math.Abs(d_Volt) < d_StepY * 1e-6;
                        
                    int Y = VoltToPixelY(d_Volt);
                    if (Y >= mr_Plot.Bottom)
                        continue;

                    i_Graph.DrawLine(b_Zero ? Utils.PEN_AXIS : Utils.PEN_GRID, mr_Plot.Left, Y, mr_Plot.Right, Y);

                    // do not overwrite the channel name
                    if (Y > mr_Plot.Top + 20)
                    {
                        String s_Label = Utils.FormatVoltage(d_Volt);
                        i_Graph.DrawString(s_Label, Font, i_BrushY, mr_Plot.Left - 3, Y - 5, Utils.AlignTopRight);
                    }
                }

                // --------- Vertical grid lines with X voltage labels ---------

                // The maximum count of vertical grid lines must depend on the window size.
                // For an axis label (e.g. "500 mV") and it's left/right padding 60 pixels should be reserved.
                s32_MaxLines = mr_Plot.Width / 60;
                double d_StepX = Utils.CalcRasterInterval(md_Scale * mr_Plot.Width, s32_MaxLines);
                for (int s32_Grid = (int)(md_ViewX0 / d_StepX); 
                         s32_Grid * d_StepX <= md_ViewX0 + md_Scale * mr_Plot.Width; 
                         s32_Grid++)
                {
                    double d_Volt = s32_Grid * d_StepX;
                    bool   b_Zero = Math.Abs(d_Volt) < d_StepX * 1e-6;
                        
                    int X = VoltToPixelX(d_Volt);
                    if (X <= mr_Plot.Left)
                        continue;

                    i_Graph.DrawLine(b_Zero ? Utils.PEN_AXIS : Utils.PEN_GRID, X, mr_Plot.Top, X, mr_Plot.Bottom);
                        
                    String s_Label = Utils.FormatVoltage(d_Volt);
                    i_Graph.DrawString(s_Label, Font, i_BrushX, X, mr_Plot.Bottom + 5, Utils.AlignTopCenter);
                }

                i_Graph.DrawRectangle(Utils.PEN_AXIS, mr_Plot);

                // channel names
                i_Graph.DrawString(mi_ChannelY.ms_Name, Font, Brushes.White, 0, mr_Plot.Top - 5);
                i_Graph.DrawString(mi_ChannelX.ms_Name, Font, Brushes.White, mr_Plot.Right, mr_Plot.Bottom + 20, Utils.AlignTopRight);

                // ---------- extract pixels ----------

                // Get int[] array from Bitmap data
                ms32_CoordSystem = i_FastBmp.CopyToPixelData();
            }
        }

        // ======================== Lissajous ========================

        public void DrawLissajous(int s32_StartSample)
        {
            ms32_StartSample = s32_StartSample;
            DrawLissajous();
        }

        /// <summary>
        /// Draw the amount of samples in ms32_GlowCount starting at the scroll position into mi_Lissajous.PixelData
        /// The older samples are drawn darker to imitate the phosphor effect.
        /// Finally display the Bitmap in the Panel.
        /// mb_AutoRepeat = true --> after the last samples wrap around to load the firt samples
        /// </summary>
        private void DrawLissajous()
        {
            if (!mb_Valid)
                return; // nothing loaded -> avoid crash

            // Clone coordinate system --> Lissajous Bitmap
            // This speed optimization avoids drawing the coordinate system each time again and again.
            mi_Lissajous.SetPixelData(ms32_CoordSystem);

            // Create a scaling constant that changes when resizing the window.
            int s32_ScalingConst = Width * Height / 50;

            double d_Factor = INTENS_MIN / INTENS_MAX;           // brightness factor runs from 0.125 to 1.0
            double d_Step   = (1.3 - d_Factor) / ms32_GlowCount; // increase d_Factor by d_Step with each sample

            int s32_LastX = 0;
            int s32_LastY = 0;
            for (int Smpl=0; Smpl<ms32_GlowCount; Smpl++, d_Factor += d_Step)
            {
                int s32_SmplIndex = ms32_StartSample + Smpl;

                if (!mb_AutoRepeat && s32_SmplIndex >= mi_ChannelY.mf_Analog.Length)
                    break;

                // Wrap around in auto repeat mode
                s32_SmplIndex %= mi_ChannelY.mf_Analog.Length;

                float f_VoltX = mi_ChannelX.mf_Analog[s32_SmplIndex];
                float f_VoltY = mi_ChannelY.mf_Analog[s32_SmplIndex];

                int s32_X = VoltToPixelX(f_VoltX);
                int s32_Y = VoltToPixelY(f_VoltY);

                if (Smpl > 0)
                {   
                    int s32_Intensity = 0xFF;
                    int s32_dX = s32_X - s32_LastX;
                    int s32_dY = s32_Y - s32_LastY;

                    // 1.) Simulate beam speed: fast lines are darker.
                    // Normally Pytagoras would need a square root here, but for speed optimization this is omitted.
                    // This results in a parabolic curve that makes lines darker the longer they are, like on a real phosphor screen.
                    int s32_SquareLen  = s32_dX * s32_dX + s32_dY * s32_dY;
                    int s32_Brightness = Math.Min(0xFF, s32_ScalingConst / Math.Max(1, s32_SquareLen));

                    // 2.) Simulate time decay: old lines are darker.
                    // The brightness decreases with the time that the line is on the screen.
                    // The oldest lines are drawn first in darker colors, the newer lines are drawn on top of them.
                    // A simple linear brightness decrease over time works perfectly here.
                    s32_Intensity = Math.Min(0xFF, (int)(s32_Brightness * d_Factor));

                    // omit long lines = invisible lines = slow to draw
                    if (s32_Intensity >= INTENS_MIN)
                    {
                        // Create an opaque color between green and cyan
                        //  s32_Color = 0xAARRGGBB
                        int s32_Color = 0xFF          << 24; // Alpha = Opaque
                        s32_Color    |= s32_Intensity <<  8; // Green = 100%
                        s32_Color    |= s32_Intensity >>  1; // Blue  =  50%
                        mi_Lissajous.DrawLine(s32_LastX, s32_LastY, s32_X, s32_Y, s32_Color);
                    }
                }

                s32_LastX = s32_X;
                s32_LastY = s32_Y;
            }

            mi_Lissajous.CopyToBitmap();

            Invalidate();
        }

        // ======================== Helpers ========================

        /// <summary>
        /// Pixel column / row inside the plot area. The trace and the grid lines must use the same formula,
        /// otherwise a trace at a round voltage (e.g. 1 V) is drawn one pixel beside the grid line.
        /// </summary>
        int VoltToPixelX(double d_Volt)
        {
            return mr_Plot.Left + (int)((d_Volt - md_ViewX0) / md_Scale);
        }

        int VoltToPixelY(double d_Volt)
        {
            return mr_Plot.Bottom - 1 - (int)((d_Volt - md_ViewY0) / md_Scale);
        }

        double PixelXToVolt(int s32_X)
        {
            return (s32_X - mr_Plot.Left + 1) * md_Scale + md_ViewX0;
        }

        double PixelYToVolt(int s32_Y)
        {
            return (mr_Plot.Bottom - s32_Y) * md_Scale + md_ViewY0;
        }
    }
}
