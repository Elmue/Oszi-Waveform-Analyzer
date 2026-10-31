/*
------------------------------------------------------------
Oscilloscope Waveform Analyzer by ElmüSoft (www.netcult.ch/elmue)
This code is released under the terms of the GNU General Public License.
Parts of this file have been written by Peter Schwendner, improved and integrated into the project by ELmü
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
using System.ComponentModel;
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
using Range             = OsziWaveformAnalyzer.Utils.Range;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;
using eWindow           = OsziWaveformAnalyzer.PanelFFT.eWindow;
using Peak              = OsziWaveformAnalyzer.PanelFFT.Fourier.Peak;
using IPanelAction      = OsziWaveformAnalyzer.FormMain.IPanelAction;
using eMouseDrag        = OsziWaveformAnalyzer.Utils.eMouseDrag;

namespace OsziWaveformAnalyzer
{
    /// <summary>
    /// This class draws Spectrum Fast Fourier Transformations
    /// </summary>
    public class PanelFFT : Panel, IPanelAction
    {
        #region enums

        public enum eWindow
        {
            [Description("Blackman Harris")]      BlackmanHarris,
            [Description("Hann")]                 Hann,
            [Description("Hamming")]              Hamming,
            [Description("Rectangular (frequ)")]  Rectangular,
            [Description("Flat Top (amplitude)")] FlatTop,
        }

        public enum eScale
        {
            [Description("dBV (RMS)")]    Decibel,
            [Description("Volt (peak)")]  Volt,
        }

        #endregion

        #region class SpectrumFFT

        public class SpectrumFFT : IOperation
        {
            /// <summary>
            /// Implementation of interface IOperation
            /// </summary>
            public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
            {
                if (i_Channel == null || !b_Analog || i_Channel.mf_Analog == null)
                    return; // the user did not click on an analog channel

                // When all analog channels are drawn on top of each other it is impossible to know which channel the user wants.
                if (Utils.PanelOszi.CommonAnalogDrawing)
                    return;

                GraphMenuItem i_Item = new GraphMenuItem();
                i_Item.ms_MenuText  = "FFT Spectrum Analysis";
                i_Item.ms_ImageFile = "SpectrumFFT.ico";
                i_Items.Add(i_Item);
            }

            /// <summary>
            /// Implementation of interface IOperation
            /// </summary>
            public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
            {
                Utils.FormMain.SwitchToTab(eTab.FFT);
                Application.DoEvents();
                Utils.PanelFFT.AnalyzeChannel(i_Channel);
                return null;
            }
        }

        #endregion

        #region class Fourier

        /// <summary>
        /// Fast Fourier Transform without any external dependencies.
        /// </summary>
        public class Fourier
        {
            public class Peak
            {
                public double md_Frequency; // Hz, interpolated between bins
                public double md_Amplitude; // Volt peak

                // for debugging in Visual Studio
                public override string ToString()
                {
                    return String.Format("Frequ: {0}, Amplitude: {1}", FormatFreq(md_Frequency, 3), Utils.FormatVoltage(md_Amplitude, 3));
                }
            }

            // Limit memory usage: 4 M samples need 2 x 32 MB for the complex FFT buffers
            public const int MAX_FFT_SIZE = 1 << 22;

            /// <summary>
            /// Returns the one-sided amplitude spectrum (bins 0 ... FFT size / 2) in Volt peak.
            /// A sine wave of amplitude A produces a peak of A (exactly A with the flat top window).
            /// The samples are zero-padded to the next power of 2.
            /// </summary>
            public static double[] AmplitudeSpectrum(float[] f_Samples, int s32_Start, int s32_Count, eWindow e_Window,
                                                     bool b_RemoveDC, out int s32_FftSize)
            {
                s32_FftSize = NextPowerOf2(s32_Count);

                double d_Mean = 0;
                if (b_RemoveDC)
                {
                    for (int i=0; i<s32_Count; i++)
                    {
                        d_Mean += f_Samples[s32_Start + i];
                    }
                    d_Mean /= s32_Count;
                }

                double[] d_Win = CreateWindow(e_Window, s32_Count);
                double   d_Sum = 0;
                double[] d_Re  = new double[s32_FftSize];
                double[] d_Im  = new double[s32_FftSize];
                for (int i=0; i<s32_Count; i++)
                {
                    d_Re[i] = (f_Samples[s32_Start + i] - d_Mean) * d_Win[i];
                    d_Sum  += d_Win[i];
                }

                Transform(d_Re, d_Im);

                int      s32_Bins = s32_FftSize / 2 + 1;
                double[] d_Amp    = new double[s32_Bins];
                double   d_Scale  = 2.0 / d_Sum;
                for (int k=0; k<s32_Bins; k++)
                {
                    d_Amp[k] = Math.Sqrt(d_Re[k] * d_Re[k] + d_Im[k] * d_Im[k]) * d_Scale;
                }
                // DC and Nyquist exist only once
                d_Amp[0] /= 2;
                d_Amp[s32_Bins - 1] /= 2;
                return d_Amp;
            }

            /// <summary>
            /// Finds the local maxima in d_Amp and sorts them the strongest first.
            /// The frequency is refined by parabolic interpolation of the logarithmic magnitude in d_Refine.
            /// This is very accurate with the Blackman-Harris window (error < 1% of a bin), but not with the flat top window,
            /// whose flat main lobe does not have a parabolic shape. Therefore d_Refine should be a Blackman-Harris spectrum
            /// of the same samples, while d_Amp may use any window. d_Refine = null --> refine with d_Amp.
            /// s32_MinDist = minimum distance between two peaks in bins
            /// </summary>
            public static List<Peak> FindAllPeaks(double[] d_Amp, double[] d_Refine, double d_BinWidth, int s32_MinDist)
            {
                if (d_Refine == null)
                    d_Refine = d_Amp;

                List<int> i_Candidates = new List<int>();
                for (int k=2; k<d_Amp.Length - 1; k++) // skip DC
                {
                    if (d_Amp[k] > d_Amp[k - 1] && d_Amp[k] >= d_Amp[k + 1])
                        i_Candidates.Add(k);
                }
                i_Candidates.Sort(delegate(int a, int b) { return d_Amp[b].CompareTo(d_Amp[a]); });
              
                double d_Strongest = i_Candidates.Count > 0 ? d_Amp[i_Candidates[0]] : 0;
                // Ignore peaks 60 dB below the strongest peak.
                // If the strongest peak is 800 mV, the list will contain peaks down to 800 µV.
                // Lower peaks are not interesting and make this loop very slow returning ten thousands of usless peaks.
                double d_Limit = d_Strongest / 1000;

                List<int>  i_Taken = new List<int>();
                List<Peak> i_Peaks = new List<Peak>();
                foreach (int k in i_Candidates)
                {
                    // Ignore peaks 60 dB below the strongest peak.
                    // If the strongest peak is 800 mV, the list will contain peaks down to 800 µV.
                    // Lower peaks are not interesting and make this loop very slow returning ten thousands of usless peaks.
                    if (d_Amp[k] < d_Limit) 
                        break;

                    bool b_TooClose = false;
                    foreach (int s32_Taken in i_Taken)
                    {
                        if (Math.Abs(s32_Taken - k) < s32_MinDist)
                            b_TooClose = true;
                    }
                    if (b_TooClose)
                        continue;

                    // The maximum of the refine spectrum at this peak (+/- 1 bin, because the windows have slightly different lobes)
                    int s32_Max = k;
                    for (int i=Math.Max(1, k - 1); i<=Math.Min(d_Refine.Length - 2, k + 1); i++)
                    {
                        if (d_Refine[i] > d_Refine[s32_Max])
                            s32_Max = i;
                    }

                    // A real signal is also a local maximum in the Blackman-Harris spectrum.
                    // A side lobe of another window (Hann, Rectangular,...) lies on the slope of the Blackman-Harris main lobe --> skip it.
                    if (d_Refine[s32_Max] < d_Refine[s32_Max - 1] || d_Refine[s32_Max] < d_Refine[s32_Max + 1])
                        continue;

                    // A real signal has approximately the same amplitude with all windows (+/- 4 dB).
                    // A far side lobe of another window is 30 dB or more below its level in the Blackman-Harris spectrum.
                    if (d_Refine[s32_Max] < d_Amp[k] * 0.25) // -12 dB
                        continue;

                    double a = Math.Log(Math.Max(d_Refine[s32_Max - 1], 1e-15));
                    double b = Math.Log(Math.Max(d_Refine[s32_Max],     1e-15));
                    double c = Math.Log(Math.Max(d_Refine[s32_Max + 1], 1e-15));
                    double d_Denom = a - 2 * b + c;
                    double d_Delta = (d_Denom < 0) ? 0.5 * (a - c) / d_Denom : 0;

                    Peak i_Peak = new Peak();
                    i_Peak.md_Frequency = (s32_Max + d_Delta) * d_BinWidth;
                    i_Peak.md_Amplitude = d_Amp[k];
                    i_Peaks.Add(i_Peak);
                    i_Taken.Add(k);
                }
                return i_Peaks;
            }

            /// <summary>
            /// Filter s32_MaxCount peaks from the list of all peaks that fall into the given frequency range.
            /// i_AllPeaks is already sorted by maximum amplitude first.
            /// </summary>
            public static List<Peak> FilterPeaks(List<Peak> i_AllPeaks, double d_FrequMin, double d_FrequMax, int s32_MaxCount)
            {
                List<Peak> i_FilterPeaks = new List<Peak>();
                foreach (Peak i_Peak in i_AllPeaks)
                {
                    if (i_Peak.md_Frequency < d_FrequMin || i_Peak.md_Frequency > d_FrequMax)
                        continue;

                    i_FilterPeaks.Add(i_Peak);
                    if (i_FilterPeaks.Count >= s32_MaxCount)
                        break;
                }
                return i_FilterPeaks;
            }

            /// <summary>
            /// Symmetric window of length s32_Count
            /// </summary>
            static double[] CreateWindow(eWindow e_Window, int s32_Count)
            {
                double[] d_Win = new double[s32_Count];
                double   d_Div = Math.Max(1, s32_Count - 1);
                for (int n=0; n<s32_Count; n++)
                {
                    double x = 2.0 * Math.PI * n / d_Div;
                    switch (e_Window)
                    {
                        case eWindow.Hann:           d_Win[n] = 0.5  - 0.5  * Math.Cos(x); break;
                        case eWindow.Hamming:        d_Win[n] = 0.54 - 0.46 * Math.Cos(x); break;
                        case eWindow.BlackmanHarris: d_Win[n] = 0.35875 - 0.48829 * Math.Cos(x) + 0.14128 * Math.Cos(2 * x) - 0.01168 * Math.Cos(3 * x); break;
                        case eWindow.FlatTop:        d_Win[n] = 0.21557895 - 0.41663158 * Math.Cos(x) + 0.277263158 * Math.Cos(2 * x)
                                                              - 0.083578947 * Math.Cos(3 * x) + 0.006947368 * Math.Cos(4 * x); break;
                        default:                     d_Win[n] = 1.0; break;
                    }
                }
                return d_Win;
            }

            /// <summary>
            /// In-place iterative radix-2 FFT. The length must be a power of 2.
            /// </summary>
            static void Transform(double[] d_Re, double[] d_Im)
            {
                int N = d_Re.Length;

                // Bit reversal permutation
                for (int i=1, j=0; i<N; i++)
                {
                    int s32_Bit = N >> 1;
                    for (; (j & s32_Bit) != 0; s32_Bit >>= 1)
                        j ^= s32_Bit;
                    j ^= s32_Bit;

                    if (i < j)
                    {
                        double d_Tmp = d_Re[i]; d_Re[i] = d_Re[j]; d_Re[j] = d_Tmp;
                               d_Tmp = d_Im[i]; d_Im[i] = d_Im[j]; d_Im[j] = d_Tmp;
                    }
                }

                for (int s32_Len=2; s32_Len<=N; s32_Len<<=1)
                {
                    int    s32_Half = s32_Len >> 1;
                    double d_Angle  = -2.0 * Math.PI / s32_Len;

                    // Twiddle factors of this stage (calculated directly to avoid accumulating rounding errors)
                    double[] d_Cos = new double[s32_Half];
                    double[] d_Sin = new double[s32_Half];
                    for (int k=0; k<s32_Half; k++)
                    {
                        d_Cos[k] = Math.Cos(d_Angle * k);
                        d_Sin[k] = Math.Sin(d_Angle * k);
                    }

                    for (int s32_Start=0; s32_Start<N; s32_Start+=s32_Len)
                    {
                        for (int k=0; k<s32_Half; k++)
                        {
                            int a = s32_Start + k;
                            int b = a + s32_Half;
                            double d_Re2 = d_Re[b] * d_Cos[k] - d_Im[b] * d_Sin[k];
                            double d_Im2 = d_Re[b] * d_Sin[k] + d_Im[b] * d_Cos[k];
                            d_Re[b] = d_Re[a] - d_Re2;
                            d_Im[b] = d_Im[a] - d_Im2;
                            d_Re[a] += d_Re2;
                            d_Im[a] += d_Im2;
                        }
                    }
                }
            }

            // ============== Helper ============

            static int NextPowerOf2(int s32_Value)
            {
                int s32_Pow = 1;
                while (s32_Pow < s32_Value)
                    s32_Pow <<= 1;
                return s32_Pow;
            }
        }

        #endregion

        private const int    MARGIN_LEFT    = 70;
        private const int    MARGIN_RIGHT   = 20;
        private const int    MARGIN_TOP     = 20;
        private const int    MARGIN_BOTTOM  = 25;
        private const int    LEGEND_PEAKS   =  5;  // show 5 peaks in the top right corner of the graph
        private const double DB_RANGE       = 100; // dynamic decibel range displayed on Y axis
        private const double NOISE_LEVEL    = -45; // draw a line -45 dB below the strongest peak if resolution = 8 bit
        public  const int    RTF_PEAKS      = 20;  // show 20 peaks in the RTF Decoder panel

        eWindow    me_Window;
        eScale     me_Scale;
        bool       mb_Decibel;
        bool       mb_LogFrequ;
        bool       mb_RemoveDC;
        bool       mb_RemoveNoise;
        Rectangle  mr_Plot;
        Image      mi_ImgMarker;
        String     ms_LabelText   = "";
        ToolTip    mi_Tooltip     = new ToolTip();
        List<Peak> mi_AllPeaks    = new List<Peak>();
        List<Peak> mi_LegendPeaks = new List<Peak>();
        Pen        mi_PenNoise    = new Pen(Brushes.Blue);
        Point      mk_LastTipPos;
        Channel    mi_Channel;
        double[]   md_Amp;          // amplitude values
        double     md_SampleRate;   // Hertz
        int        ms32_StartSmpl;
        int        ms32_SmplCount;
        int        ms32_FftSize;
        Range      mk_UserSel;      // in Samples
        double     md_BinWidth;
        double     md_FreqNyquist;
        double     md_FreqMin;      // Zoom
        double     md_FreqMax;      // Zoom
        eMouseDrag me_MouseDrag;
        Point      mk_MouseDownLoc;
        double     md_MoveStartMinFreq;
        double     md_MoveStartMaxFreq;
        Range      mk_Zoom;         // the area (in Pixels) to be zoomed with the mouse

        /// <summary>
        /// Called from FormMain.OnLoad()
        /// </summary>
        public void Init()
        {          
            DoubleBuffered = true; // creates an empty Bitmap that is black
            ResizeRedraw   = true;
            TabStop        = true;

            mi_PenNoise.DashStyle   = DashStyle.Dash;
            mi_PenNoise.DashPattern = new float[] { 10, 10 };

            me_MouseDrag = eMouseDrag.None;
            mi_ImgMarker = Utils.LoadResourceImage("Marker.png");

            // IMPORTANT:
            // This is required for keyboard shortcuts.
            SetStyle(ControlStyles.Selectable, true);
        }

        // interface IPanelAction
        // The user changed the tab or the old capture has been deleted or a new capture was loaded
        public void OnUserAction(eTab e_ActiveTab, Capture i_Capture)
        {
            if (i_Capture == null)
            {
                // garbage collector can free this memory
                mi_Channel     = null;
                md_Amp         = null;
                me_MouseDrag   = eMouseDrag.None;
                ms_LabelText   = "";
                mi_AllPeaks   .Clear();
                mi_LegendPeaks.Clear();
            }

            if (e_ActiveTab == eTab.FFT)
            {
                Focus();

                if (i_Capture != null)
                {
                    if (md_Amp == null)
                    {
                        if (i_Capture.ms32_AnalogCount > 0)
                            ms_LabelText = "Use the right click menu in panel 'Oszi' to start a spectrum analysis.";
                        else
                            ms_LabelText = "The spectrum analysis requires a capture with an analog channel.";
                    }
                    
                    Utils.FormMain.SetLabel(ms_LabelText);
                }
            }
        }

        /// <summary>
        /// b_ShowNoise = true --> show the real curve, not only the maximum values.
        /// </summary>
        public void OnUserSettgChanged(eWindow e_Window, eScale e_Scale, bool b_LogFrequ, bool b_RemoveDC, bool b_RemoveNoise)
        {
            if (mb_LogFrequ != b_LogFrequ)
                ResetZoom();

            me_Window      = e_Window;
            me_Scale       = e_Scale;
            mb_Decibel     = e_Scale == eScale.Decibel;
            mb_LogFrequ    = b_LogFrequ;
            mb_RemoveDC    = b_RemoveDC;
            mb_RemoveNoise = b_RemoveNoise;

            try
            {
                Calculate(md_SampleRate); // calls Invalidate()
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(Utils.FormMain, Ex);
            }

            Focus(); // enable keyboard shortcuts
        }

        /// <summary>
        /// Called when user clicks in the channel right click menu
        /// throws
        /// </summary>
        public void AnalyzeChannel(Channel i_Channel)
        {
            mi_Channel = i_Channel;
            
            double d_SampleRate = (double)(Utils.PICOS_PER_SECOND / Utils.CurCapture.ms64_SampleDist);
            Calculate(d_SampleRate);
        }

        /// <summary>
        /// throws
        /// </summary>
        private void Calculate(double d_SampleRate)
        {
            if (mi_Channel == null)
                return;

            String s_Display;
            mk_UserSel = Utils.PanelOszi.GetUserSelectionOrAll(out s_Display);
            bool b_Truncated = false;

            ms32_StartSmpl = mk_UserSel.ms32_Start;
            ms32_SmplCount = mk_UserSel.Count;
            if (ms32_SmplCount > Fourier.MAX_FFT_SIZE)
            {
                ms32_SmplCount = Fourier.MAX_FFT_SIZE;
                b_Truncated    = true;
            }

            if (ms32_SmplCount < 16)
            {
                md_Amp = null;
                throw new Exception("Not enough samples for Fourier analysis.");
            }

            // -----------------------------

            Utils.FormMain.Cursor = Cursors.WaitCursor;

            double[] d_Amp = Fourier.AmplitudeSpectrum(mi_Channel.mf_Analog, ms32_StartSmpl, ms32_SmplCount, 
                                                       me_Window, mb_RemoveDC, out ms32_FftSize);
            md_BinWidth = d_SampleRate / ms32_FftSize;

            // The main lobe of the window is several bins wide. Zero padding makes it wider in bins.
            // Minimum distance between 2 peaks: 3 resolution bins (a main lobe has only one local maximum).
            // A larger distance would hide the harmonics of short captures (1 kHz square wave with 1200 samples at 500 kSa/s).
            int s32_MinPeakDist = Math.Max(3, (int)(3.0 * ms32_FftSize / ms32_SmplCount));

            // The peak frequencies are always measured in a Blackman-Harris spectrum (see FindPeaks())
            double[] d_Refine = null;
            if (me_Window != eWindow.BlackmanHarris)
            {
                int s32_Dummy;
                d_Refine = Fourier.AmplitudeSpectrum(mi_Channel.mf_Analog, ms32_StartSmpl, ms32_SmplCount, 
                                                     eWindow.BlackmanHarris, mb_RemoveDC, out s32_Dummy);
            }

            mi_AllPeaks = Fourier.FindAllPeaks(d_Amp, d_Refine, md_BinWidth, s32_MinPeakDist);

            // -----------------------------

            bool b_KeepZoom = (md_Amp != null && md_SampleRate == d_SampleRate && md_FreqMax > md_FreqMin);
            md_Amp         = d_Amp;
            md_SampleRate  = d_SampleRate;
            md_FreqNyquist = d_SampleRate / 2.0;
            
            if (b_KeepZoom) SetFreqRange(md_FreqMin, md_FreqMax); // recalculate peaks
            else            ResetZoom(); // reset zoom and recalculate peaks
            Invalidate();

            // -----------------------------

            ms_LabelText = String.Format("Start Sample: {0:N0}   Sample Count: {1:N0}   Samplerate: {2}   FFT Size: {3:N0}   Resolution: {4}   Nyquist: {5}",
                                         ms32_StartSmpl, ms32_SmplCount, FormatFreq(md_SampleRate), ms32_FftSize,
                                         FormatFreq(md_SampleRate / ms32_SmplCount), FormatFreq(md_FreqNyquist));
            if (b_Truncated)
                ms_LabelText += " - truncated to 4M samples";

            Utils.FormMain.SetLabel(ms_LabelText);
            Utils.FormMain.Cursor = Cursors.Default;
        }

        // ================================= Paint ====================================

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // DoubleBuffered creates an empty Bitmap that is black --> no clearing required for a black background
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                Draw(e.Graphics);
            }
            catch (Exception Ex)
            {
                e.Graphics.DrawString(Ex.Message + "\n" + Ex.StackTrace, Font, Brushes.Red, mr_Plot.Left + 15, mr_Plot.Top + 10);
            }
        }

        private bool Draw(Graphics g)
        {
            if (md_Amp == null || md_FreqMax <= md_FreqMin)
                return false;

            double d_MinFreqDisp = MinFreqDisplayed;
            double d_Bottom, d_Top;
            GetYRange(out d_Bottom, out d_Top);

            // ------------------ -45 dB noise level line ------------------

            if (mb_Decibel && mi_AllPeaks.Count > 0 && Utils.CurCapture.ms32_AnalogRes <= 8)
            {
                // draw a line 45dB below the strongest peak if the resolution is 8 bit.
                // for 16 bit resolution the level would be -98 dB --> no line required.
                double d_Strongest = mi_AllPeaks[0].md_Amplitude;
                double d_Factor    = Math.Pow(10, NOISE_LEVEL / 20.0);
                int s32_Ypos = AmplitudeToY(d_Strongest * d_Factor, d_Bottom, d_Top);
                g.DrawLine(mi_PenNoise, mr_Plot.Left, s32_Ypos, mr_Plot.Right, s32_Ypos);
            }

            // ------------ horizontal grid + amplitude labels ------------

            double d_StepY = 10;
            if (!mb_Decibel)
            {
                // The maximum count of horizontal grid lines must depend on the window size.
                // The minimum height of a vertical raster should be 50 pixel
                int s32_MaxLines = mr_Plot.Height / 50;
                d_StepY = Utils.CalcRasterInterval(d_Top - d_Bottom, s32_MaxLines);
            }

            for (double d_Y = Math.Ceiling(d_Bottom / d_StepY) * d_StepY; 
                        d_Y <= d_Top + d_StepY * 1e-6; 
                        d_Y += d_StepY)
            {
                int Y = (int)(mr_Plot.Bottom - (d_Y - d_Bottom) / (d_Top - d_Bottom) * mr_Plot.Height);
                g.DrawLine(Utils.PEN_GRID, mr_Plot.Left, Y, mr_Plot.Right, Y);

                String s_Label = mb_Decibel ? d_Y.ToString("0", CultureInfo.InvariantCulture) + " dBV" : Utils.FormatVoltage(d_Y);
                g.DrawString(s_Label, Font, Utils.BRUSH_LABEL, mr_Plot.Left - 3, Y - 9, Utils.AlignTopRight);
            }

            // ------------ vertical grid + frequency labels ------------

            // Vertical position of bottom labels
            int s32_BotLabel = mr_Plot.Bottom + 6;

            if (mb_LogFrequ)
            {
                // --- draw raster lines ---

                double d_StartFreq  = Math.Pow(10, Math.Floor(Math.Log10(d_MinFreqDisp))); // in Hertz
                int s32_DecadeWidth = FreqToX(d_StartFreq * 10.0) - FreqToX(d_StartFreq);  // in pixel

                // add labels on logarithmic X axis that fit without overlapping, depending on window size and zoom factor.
                // The logarithmic labels for raster lines 1 and 2 are very wide and the labels for 8 and 9 are very near.
                // Add more labels as they fit.
                List<int> i_LabelIdx = new List<int>();
                if (s32_DecadeWidth >   70) i_LabelIdx.Add(1);
                if (s32_DecadeWidth >  200) i_LabelIdx.Add(3);
                if (s32_DecadeWidth >  400) i_LabelIdx.Add(5);
                if (s32_DecadeWidth >  500) i_LabelIdx.Add(2);
                if (s32_DecadeWidth >  600) i_LabelIdx.Add(7);
                if (s32_DecadeWidth >  800) i_LabelIdx.Add(4);
                if (s32_DecadeWidth > 1000) i_LabelIdx.Add(6);
                if (s32_DecadeWidth > 1600) i_LabelIdx.Add(8);
                if (s32_DecadeWidth > 1700) i_LabelIdx.Add(9);

                bool b_HasLabel = false;
                for (double d_Dec = d_StartFreq; d_Dec <= md_FreqMax; d_Dec *= 10)
                {
                    for (int m=1; m<10; m++)
                    {
                        double d_F = d_Dec * m;
                        int X = FreqToX(d_F);

                        if (X < mr_Plot.Left || X > mr_Plot.Right)
                            continue;
                            
                        Pen i_LinePen = (m == 1) ? Utils.PEN_AXIS : Utils.PEN_GRID;
                        g.DrawLine(i_LinePen, X, mr_Plot.Top, X, mr_Plot.Bottom);

                        if (i_LabelIdx.Contains(m))
                        {
                            b_HasLabel = true;
                            g.DrawString(FormatFreq(d_F), Font, Utils.BRUSH_LABEL, X, s32_BotLabel, Utils.AlignTopCenter);
                        }
                    }
                }

                // In extreme zoom mode the raster lines may be outside the visible screen area --> draw 3 fix labes
                if (!b_HasLabel)
                {
                    int s32_Center = mr_Plot.Left + mr_Plot.Width / 2;
                    g.DrawLine(Utils.PEN_GRID, s32_Center, mr_Plot.Top, s32_Center, mr_Plot.Bottom);

                    double d_LeftLabel  = XToFreq(mr_Plot.Left);
                    double d_MidLabel   = XToFreq(s32_Center);
                    double d_RightLabel = XToFreq(mr_Plot.Right);
                    g.DrawString(FormatFreq(d_LeftLabel),  Font, Utils.BRUSH_LABEL, mr_Plot.Left,  s32_BotLabel, Utils.AlignTopLeft);
                    g.DrawString(FormatFreq(d_MidLabel),   Font, Utils.BRUSH_LABEL, s32_Center,    s32_BotLabel, Utils.AlignTopCenter);
                    g.DrawString(FormatFreq(d_RightLabel), Font, Utils.BRUSH_LABEL, mr_Plot.Right, s32_BotLabel, Utils.AlignTopRight);
                }
            }
            else // linear frequency
            {
                // The maximum count of vertical grid lines must depend on the window size.
                // For an axis label (e.g. "173.88 MHz") and it's left/right padding 100 pixels should be reserved.
                int s32_MaxLines = mr_Plot.Width / 100;
                double d_StepX = Utils.CalcRasterInterval(md_FreqMax - md_FreqMin, s32_MaxLines);
                for (double d_F = Math.Ceiling(md_FreqMin / d_StepX) * d_StepX; 
                            d_F <= md_FreqMax + d_StepX * 1e-6; 
                            d_F += d_StepX)
                {
                    int X = FreqToX(d_F);
                    g.DrawLine(Utils.PEN_GRID, X, mr_Plot.Top, X, mr_Plot.Bottom);
                    g.DrawString(FormatFreq(d_F), Font, Utils.BRUSH_LABEL, X, s32_BotLabel, Utils.AlignTopCenter);
                }
            }

            // draw this after the grid lines
            g.DrawRectangle(Utils.PEN_AXIS, mr_Plot);

            // ------------ Draw the curve, Part 1 ------------

            // not PointF here! (buggy: GDI+ omits some lines)
            List<Point> i_Points = new List<Point>(); 

            // Important: Start with the Bin before the first Bin visible on screen to avoid an ugly moving buckle at the first line.
            int s32_Bin = (int)(XToFreq(mr_Plot.Left) / md_BinWidth);
            s32_Bin = Math.Max(1, s32_Bin -1);

            // First loop: add the points of all Bins that are wider than 1 pixel
            // The first loop iterates the Bins.
            double d_MinY   = 0;
            double d_MaxY   = 0;
            int  s32_Repeat = 0;
            int  s32_LastX  = 0;
            for (int L=0; true; L++)
            {
                int    X = FreqToX(s32_Bin * md_BinWidth);
                double Y = md_Amp[s32_Bin];

                if (s32_LastX == X) // same X coordinate as previous Bin
                {
                    d_MinY = Math.Min(d_MinY, Y);
                    d_MaxY = Math.Max(d_MaxY, Y);
                    s32_Repeat ++;
                }
                else
                {
                    if (L > 0)
                    {
                        // draw maximum amplitude
                        int s32_MaxY = AmplitudeToY(d_MaxY, d_Bottom, d_Top);
                        i_Points.Add(new Point(s32_LastX, s32_MaxY));

                        // draw minimum amplitude (if noise suppresion is off)
                        if (!mb_RemoveNoise)
                        {
                            int s32_MinY = AmplitudeToY(d_MinY, d_Bottom, d_Top);
                            if (s32_MinY != s32_MaxY)
                                i_Points.Add(new Point(s32_LastX, s32_MinY));
                        }
                    }

                    if (s32_LastX >= mr_Plot.Right || s32_Repeat >= 5)
                        break; // Five Bins on the same X coordinate --> continue in next loop. 

                    d_MinY     = Y;
                    d_MaxY     = Y;
                    s32_Repeat = 0;
                    s32_LastX  = X;
                }

                s32_Bin ++;
                if (s32_Bin >= md_Amp.Length)
                    break;
            }

            // ------------ Draw the curve, Part 2 ------------

            // Second loop: add the points of all Bins that are only 1 pixel wide or less.
            // This requires a second loop to avoid running through all Bins for speed optimization.
            // The second loop iterates the X coordinate
            for (int X=s32_LastX; X<=mr_Plot.Right; X++)
            {
                int s32_NextBin = (int)(XToFreq(X) / md_BinWidth);
                if (s32_NextBin >= md_Amp.Length)
                    break;

                if (s32_NextBin <= s32_Bin)
                    continue;

                // draw vertical line from minimum to maximum at the same X coordinate
                double d_Min = double.MaxValue;
                double d_Max = double.MinValue;
                for (int k=s32_Bin; k<s32_NextBin; k++)
                {
                    d_Min = Math.Min(d_Min, md_Amp[k]);
                    d_Max = Math.Max(d_Max, md_Amp[k]);
                }
                s32_Bin = s32_NextBin;

                // draw maximum amplitude
                i_Points.Add(new Point(X, AmplitudeToY(d_Max, d_Bottom, d_Top)));

                // draw minimum amplitude (if noise suppression is off)
                if (!mb_RemoveNoise)
                    i_Points.Add(new Point(X, AmplitudeToY(d_Min, d_Bottom, d_Top)));
            }

            if (i_Points.Count > 1)
            {
                // In Decibel mode some lines may go below the X axis --> clip them away
                // In Volt    mode there are no negative voltages     --> allow drawing 0 Volt line on top of the X axis.
                if (mb_Decibel) g.SetClip(mr_Plot);

                g.DrawLines(Pens.Yellow, i_Points.ToArray());

                g.ResetClip();
            }

            // ----------- green zoom rectangle ----------

            // mk_Zoom in Pixels
            if (me_MouseDrag == eMouseDrag.Zoom && mk_Zoom.IsValid)
            {
                // AutoScrollPosition.Y is negative!
                int s32_Top = Math.Max(mr_Plot.Top,    3 - AutoScrollPosition.Y);
                int s32_Bot = Math.Min(mr_Plot.Bottom, mr_Plot.Bottom - AutoScrollPosition.Y - 3);

                // draw 3 nested rectangles: black, lime, black
                Rectangle r_Rect = new Rectangle(mk_Zoom.ms32_Start, s32_Top, mk_Zoom.Count, s32_Bot - s32_Top);
                g.DrawRectangle(Pens.Lime,  r_Rect);
                r_Rect.Inflate(1, 1);
                g.DrawRectangle(Pens.Black, r_Rect);
                r_Rect.Inflate(-2, -2);
                g.DrawRectangle(Pens.Black, r_Rect);
            }

            // ------------ peak arrows ------------
            
            StringBuilder i_Legend = new StringBuilder();
            int s32_Nr = 1;
            foreach (Peak i_Peak in mi_LegendPeaks)
            {
                int X = FreqToX (i_Peak.md_Frequency);
                int Y = AmplitudeToY(i_Peak.md_Amplitude, d_Bottom, d_Top);
                g.DrawImage(mi_ImgMarker, X - 7, Y - 20);
                g.DrawString(s32_Nr.ToString(), Font, Brushes.Lime, X - 4, Y - 36);
                
                i_Legend.AppendFormat("{0}:  {1}   ({2})\n", s32_Nr, FormatFreq(i_Peak.md_Frequency), FormatAmplitude(i_Peak.md_Amplitude));
                s32_Nr ++;
            }

            // ------------ peak legend ------------
            
            if (i_Legend.Length > 0)
            {
                SizeF k_Size = g.MeasureString(i_Legend.ToString(), Font);
                RectangleF r_Box = new RectangleF(mr_Plot.Right - k_Size.Width - 12, mr_Plot.Top + 6, k_Size.Width + 4, k_Size.Height);
                g.FillRectangle(Brushes.Black, r_Box);
                g.DrawString(i_Legend.ToString(), Font, Brushes.Lime, r_Box.Left + 2, r_Box.Top);
            }
            return true;
        }

        // ================================ Events ====================================

        // always called from FormDesigner code in InitializeComponent()
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            mr_Plot = new Rectangle(MARGIN_LEFT, MARGIN_TOP, Width - MARGIN_LEFT - MARGIN_RIGHT, Height - MARGIN_TOP - MARGIN_BOTTOM);
        }

        // ------------------- Mouse ---------------
        
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            mi_Tooltip.Hide(this);
            Utils.FormMain.PrintStatus("", Color.Black);
            Focus(); // required for keyboard shortcuts

            if (e.Button == MouseButtons.Left && mr_Plot.Contains(e.Location))
            {
                mk_MouseDownLoc = e.Location;

                switch (Control.ModifierKeys)
                {
                    case Keys.None:
                        me_MouseDrag = eMouseDrag.Move; // move graph horizontally
                        md_MoveStartMinFreq = md_FreqMin;
                        md_MoveStartMaxFreq = md_FreqMax;
                        Cursor = Cursors.SizeAll;
                        break;

                    case Keys.Control:
                        me_MouseDrag = eMouseDrag.Zoom; // zoom into a range
                        Cursor = Cursors.VSplit;
                        break;

                    case Keys.Shift: // select an area (makes sense only in Oszi Panel)
                        Utils.FormMain.PrintStatus("Selecting an area is not supported in the FFT panel.", Color.Red);
                        Cursor = Cursors.No;
                        break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (md_Amp == null)
                return;

            // -------------- Move ---------------

            // Move the graph horizontally
            if (me_MouseDrag == eMouseDrag.Move)
            {
                int s32_MoveX = e.X - mk_MouseDownLoc.X;
                MoveHorizontal(md_MoveStartMinFreq, md_MoveStartMaxFreq, s32_MoveX);
                return;
            }

            // -------------- Zoom ---------------

            // set mk_Zoom for the green rectangle in pixels
            if (me_MouseDrag == eMouseDrag.Zoom)
            {
                mk_Zoom.ms32_Start = Math.Min(mk_MouseDownLoc.X, e.X);
                mk_Zoom.ms32_End   = Math.Max(mk_MouseDownLoc.X, e.X);
                Invalidate();
                return;
            }

            if (e.Button != MouseButtons.None)
                return; // no tooltip if any button pressed

            // ------------- Tooltip --------------

            if (e.Location != mk_LastTipPos) // avoid flickering tooltip
            {
                mk_LastTipPos = e.Location;

                int s32_Bin = (int)Math.Round(XToFreq(e.X) / md_BinWidth);
                if (mr_Plot.Contains(e.Location) && s32_Bin >= 0 && s32_Bin < md_Amp.Length)
                {
                    String s_Tooltip = String.Format("Channel Name: {0}\n"
                                                   + Utils.TOOLTIP_SEPARATOR
                                                   + "Frequency at mouse X: {1}\n"
                                                   + "Amplitude at mouse X: {2}\n"
                                                   + "Bin at mouse X: {3}\n"
                                                   + Utils.TOOLTIP_SEPARATOR
                                                   + "Start Sample: {4:N0}\n"
                                                   + "Sample Count: {5:N0}\n"
                                                   + "Analog Resolution: {6} bit",
                                                   mi_Channel.ms_Name, 
                                                   FormatFreq(s32_Bin * md_BinWidth, 3),
                                                   FormatAmplitude(md_Amp[s32_Bin]),
                                                   s32_Bin,
                                                   mk_UserSel.ms32_Start, mk_UserSel.Count,
                                                   Utils.CurCapture.ms32_AnalogRes);

                    // Workaround for Windows 10 bug: Without coordinates the tooltip may never show up
                    mi_Tooltip.Show(s_Tooltip, this, e.X + 15, e.Y + 15);
                }
                else
                {
                    mi_Tooltip.Hide(this);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (me_MouseDrag == eMouseDrag.Zoom)
            {
                double d_LowFreq  = XToFreq(Math.Max(mr_Plot.Left,  mk_Zoom.ms32_Start));
                double d_HighFreq = XToFreq(Math.Min(mr_Plot.Right, mk_Zoom.ms32_End));
                SetZoom(d_LowFreq, d_HighFreq);
            }

            me_MouseDrag = eMouseDrag.None;
            Cursor = Cursors.Arrow;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            me_MouseDrag = eMouseDrag.None;
            Cursor = Cursors.Arrow;
            mi_Tooltip.Hide(this);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            ResetZoom();
            Invalidate();
        }

        // ------------------- Keyboard ---------------

        /// <summary>
        /// IMPORTANT:
        /// Without this function the arrow keys never get through to OnKeyDown()
        /// </summary>
        protected override bool IsInputKey(Keys e_Key) 
        {
            switch (e_Key)
            {
                case Keys.Up:  
                case Keys.Down:
                case Keys.Left:
                case Keys.Left  | Keys.Shift:
                case Keys.Right:
                case Keys.Right | Keys.Shift:
                    return true;
            }
            return base.IsInputKey(e_Key);
        }

        /// <summary>
        /// Move the graph left / right in zoom mode.
        /// With SHIFT key pressed move fast.
        /// CTRL and ALT keys are ignored.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            mi_Tooltip.Hide(this);

            if (md_Amp == null)
                return;

            int s32_MoveX;
            switch (e.KeyCode)
            {
                case Keys.Left:  s32_MoveX = -5; break;
                case Keys.Right: s32_MoveX =  5; break;
                case Keys.Up:    Zoom(true);     return;
                case Keys.Down:  Zoom(false);    return;
                default: return;
            }

            if (e.Shift) s32_MoveX *= 5; // fast

            MoveHorizontal(md_FreqMin, md_FreqMax, s32_MoveX);
        }

        // --------------------------------------

        /// <summary>
        /// Zoom In/Out by arrow keys
        /// </summary>
        void Zoom(bool b_ZoomIn)
        {
            double k = 0.23; // Zoom intensity factor

            double d_LowFreq, d_HighFreq;
            if (mb_LogFrequ)
            {
                // convert to logarithmic space
                double d_LogMin  = Math.Log10(Math.Max(md_FreqMin, md_BinWidth));
                double d_LogMax  = Math.Log10(md_FreqMax);
                double d_LogSpan = d_LogMax - d_LogMin;

                double d_NewLogMin, d_NewLogMax;
                if (b_ZoomIn)
                {
                    d_NewLogMin = d_LogMin + (k / 2) * d_LogSpan;
                    d_NewLogMax = d_LogMax - (k / 2) * d_LogSpan;
                }
                else // Zoom out
                {
                    k = k / (1 - k);
                    d_NewLogMin = d_LogMin - (k / 2) * d_LogSpan;
                    d_NewLogMax = d_LogMax + (k / 2) * d_LogSpan;
                }

                // convert back to frequency
                d_LowFreq  = Math.Pow(10, d_NewLogMin);
                d_HighFreq = Math.Pow(10, d_NewLogMax);
            }
            else // linear
            {
                double d_LinearSpan = md_FreqMax - md_FreqMin;
                if (b_ZoomIn)
                {
                    d_LowFreq  = md_FreqMin + (k / 2) * d_LinearSpan;
                    d_HighFreq = md_FreqMax - (k / 2) * d_LinearSpan;
                }
                else // Zoom Out
                {
                    d_LowFreq  = md_FreqMin - (k / 2) * d_LinearSpan;
                    d_HighFreq = md_FreqMax + (k / 2) * d_LinearSpan;
                }
            }

            SetZoom(d_LowFreq, d_HighFreq);
        }

        /// <summary>
        /// Zoom into the frequency range from d_LowFreq to d_HighFreq
        /// </summary>
        void SetZoom(double d_LowFreq, double d_HighFreq)
        {
            double d_MinWidth = md_BinWidth * 50.0;
            if (d_HighFreq - d_LowFreq < d_MinWidth)
            {
                // Do not move horizontally if at maximum zoom
                d_LowFreq  = md_FreqMin;
                d_HighFreq = md_FreqMin + d_MinWidth;
                if (d_HighFreq > md_FreqNyquist)
                {
                    d_HighFreq = md_FreqNyquist;
                    d_LowFreq  = md_FreqNyquist - d_MinWidth; 
                }

                Utils.FormMain.PrintStatus("The zoom is at maximum. Use Arrow Down key or double click to zoom out.", Color.Red);
            }
            else
            {
                Utils.FormMain.PrintStatus("", Color.Black);
            }

            SetFreqRange(d_LowFreq, d_HighFreq);
            Invalidate(); 
        }

        /// <summary>
        /// Show the entire curve
        /// </summary>
        void ResetZoom()
        {
            SetFreqRange(0.0, md_FreqNyquist);
        }

        // --------------------------------------

        /// <summary>
        /// Move the graph left / right by s32_MoveX pixels in zoom mode.
        /// </summary>
        void MoveHorizontal(double d_FrequStart, double d_FrequEnd, int s32_MoveX)
        {
            if (mb_LogFrequ)
                d_FrequStart = Math.Max(d_FrequStart, md_BinWidth);

            int s32_MinX = FreqToX(mb_LogFrequ ? md_BinWidth : 0);
            int s32_MaxX = FreqToX(md_FreqNyquist);

            int s32_LeftX  = FreqToX(d_FrequStart);
            int s32_RightX = FreqToX(d_FrequEnd);

            if (s32_MoveX > 0) // move right --> decrease frequency
            {
                // Limit movement to minimum frequency
                int s32_Excess = s32_LeftX - s32_MoveX - s32_MinX;
                if (s32_Excess < 0)
                    s32_MoveX += s32_Excess;
            }
            else if (s32_MoveX < 0) // move left --> increase frequency
            {
                // Limit movement to maximum frequency
                int s32_Excess = s32_RightX - s32_MoveX - s32_MaxX;
                if (s32_Excess > 0)
                    s32_MoveX += s32_Excess;
            }

            if (s32_MoveX != 0)
            {
                SetFreqRange(XToFreq(s32_LeftX  - s32_MoveX), 
                             XToFreq(s32_RightX - s32_MoveX));
                Invalidate();
            }
        }

        void SetFreqRange(double d_Min, double d_Max)
        {
            md_FreqMin = Math.Max(d_Min, 0.0);
            md_FreqMax = Math.Min(d_Max, md_FreqNyquist);

            // In zoom mode avoid peak arrows to be drawn outside the plot area.
            // In full spectrum mode show all peaks.
            if (d_Min > MinFreqDisplayed || d_Max < md_FreqNyquist)
            {
                d_Min = XToFreq(mr_Plot.Left  + 8);
                d_Max = XToFreq(mr_Plot.Right - 8);
            }
            mi_LegendPeaks = Fourier.FilterPeaks(mi_AllPeaks, d_Min, d_Max, LEGEND_PEAKS);
        }

        // ============================== User Export ================================

        /// <summary>
        /// Called from insise a try - catch block
        /// </summary>
        public String SaveAsImage(String s_Path)
        {
            using (Bitmap   i_Bitmap   = new Bitmap(Width, Height, PixelFormat.Format24bppRgb))
            using (Graphics i_Graphics = Graphics.FromImage(i_Bitmap))
            {
                if (!Draw(i_Graphics))
                    throw new Exception("No FFT Spectrum to be saved.");

                i_Bitmap.Save(s_Path, ImageFormat.Png);
            }
            return "FFT Spectrum";
        }

        /// <summary>
        /// Show the first MAX_PEAKS peaks in the RTF panel
        /// </summary>
        public void OnButtonShowPeaks()
        {
            if (md_Amp == null)
            {
                MessageBox.Show(this, "No FFT Spectrum loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            List<Peak>  i_RtfPeaks = Fourier.FilterPeaks(mi_AllPeaks, 0.0, md_FreqNyquist, RTF_PEAKS);
            RtfDocument i_RtfDoc   = new RtfDocument(Color.White);
            RtfBuilder  i_Builder  = i_RtfDoc.CreateNewBuilder();

            i_Builder.AppendLine(Color.White, "FFT Spectrum Analysis of channel '" + mi_Channel.ms_Name + "'", FontStyle.Underline);
            i_Builder.Append2ColorPair(Color.Cyan, "Sample rate:",   14, Color.White, "{0}",    FormatFreq(md_SampleRate));
            i_Builder.Append2ColorPair(Color.Cyan, "Nyquist:",       14, Color.White, "{0}",    FormatFreq(md_FreqNyquist));
            i_Builder.Append2ColorPair(Color.Cyan, "Resolution:",    14, Color.White, "{0}",    FormatFreq(md_SampleRate / ms32_SmplCount));
            i_Builder.Append2ColorPair(Color.Cyan, "Bin Width:",     14, Color.White, "{0}",    FormatFreq(md_BinWidth));
            i_Builder.Append2ColorPair(Color.Cyan, "Window:",        14, Color.White, "{0}",    Utils.GetDescriptionAttribute(me_Window));
            i_Builder.Append2ColorPair(Color.Cyan, "FFT size:",      14, Color.White, "{0:N0}", ms32_FftSize);
            i_Builder.Append2ColorPair(Color.Cyan, "Start Sample:",  14, Color.White, "{0:N0}", ms32_StartSmpl);
            i_Builder.Append2ColorPair(Color.Cyan, "Sample Count:",  14, Color.White, "{0:N0}", ms32_SmplCount);
            i_Builder.AppendText      (Color.Cyan, "Signal Start: ");
            i_Builder.AppendTimestamp (false, ms32_StartSmpl, ms32_StartSmpl + ms32_SmplCount - 1);
            i_Builder.AppendText("\n\n");

            if (i_RtfPeaks.Count == 0)
            {
                i_Builder.AppendLine(Utils.ERROR_COLOR, "No peaks found.");
            }
            else
            {
                i_Builder.AppendLine(Color.White, " Nr      Frequency       dBV      Volt peak       Volt RMS      rel. to 1    Harmonic", FontStyle.Underline);

                double d_Fund = i_RtfPeaks[0].md_Frequency;
                for (int P=0; P<i_RtfPeaks.Count; P++)
                {
                    Peak  i_Peak = i_RtfPeaks[P];
                    double d_Rel = 20 * Math.Log10(i_Peak.md_Amplitude / i_RtfPeaks[0].md_Amplitude);

                    // A harmonic is an integer multiple of the strongest peak (within one resolution bin).
                    // Higher multiples are rather artifacts (e.g. of the A/D converter) than real harmonics.
                    String s_Harmonic = "     -";
                    int s32_Mult = (int)Math.Round(i_Peak.md_Frequency / d_Fund);
                    if (P > 0 && s32_Mult >= 2 && s32_Mult <= 100 && 
                        Math.Abs(i_Peak.md_Frequency - s32_Mult * d_Fund) < md_SampleRate / ms32_SmplCount)
                    {
                        s_Harmonic = s32_Mult.ToString().PadLeft(6);
                    }

                    String s_Frequ = FormatFreq(i_Peak.md_Frequency, 3);
                    // For a correct right aligned column the units must all have the same width (" Hz", "kHz", "MHz")
                    s_Frequ = s_Frequ.Replace(" Hz", "  Hz");

                    i_Builder.AppendText(Color.Yellow, String.Format("{0,3}   {1,12}", P + 1, s_Frequ));
                    i_Builder.AppendText(Color.Lime,   String.Format(CultureInfo.InvariantCulture, "{0,10:F1}   {1,12}   {2,12}",
                                                       ToDbV(i_Peak.md_Amplitude), 
                                                       Utils.FormatVoltage(i_Peak.md_Amplitude, 1),
                                                       Utils.FormatVoltage(i_Peak.md_Amplitude / Math.Sqrt(2), 1)));
                    i_Builder.AppendLine(Color.White,  String.Format(CultureInfo.InvariantCulture, "   {0,9:F1} dB  {1}",
                                                       d_Rel, s_Harmonic));
                }
            }

            // Show the text and switch to tab "Decoder". 
            // 0 = do not change the display factor.
            Utils.FormMain.ShowAnalysisResult(i_RtfDoc, 0, "FFT Spectrum Peaks");
        }

        // ================================ Helper ===================================

        /// <summary>
        /// s32_Digits = -1 --> up to  3 decimals behind the decimal point, remove trailing zeroes
        /// s32_Digits =  2 --> always 2 decimals behind the decimal point
        /// s32_Digits =  5 --> always 5 decimals behind the decimal point
        /// </summary>
        static String FormatFreq(double d_Freq, int s32_Digits = -1)
        {
            if (double.IsNaN(d_Freq) || double.IsInfinity(d_Freq) || d_Freq > 1e18)
                return "?";
            return Utils.FormatFrequency((decimal)Math.Max(0, d_Freq), s32_Digits);
        }

        // In logarithmic mode the lowest frequency is the first bin
        double MinFreqDisplayed
        {
            get { return mb_LogFrequ ? Math.Max(md_FreqMin, md_BinWidth) : md_FreqMin; }
        }

        // ------------------------------------

        /// <summary>
        /// Get the X coordinate for a frequency
        /// </summary>
        int FreqToX(double d_Freq)
        {
            double d_Min = MinFreqDisplayed;
            double d_Rel = mb_LogFrequ ? (Math.Log10(d_Freq) - Math.Log10(d_Min)) / (Math.Log10(md_FreqMax) - Math.Log10(d_Min))
                                       : (d_Freq - d_Min) / (md_FreqMax - d_Min);
            return (int)Math.Round(mr_Plot.Left + d_Rel * mr_Plot.Width);
        }

        double XToFreq(int X)
        {
            double d_Min = MinFreqDisplayed;
            double d_Rel = ((double)X - mr_Plot.Left) / mr_Plot.Width;
            if (mb_LogFrequ)
                return Math.Pow(10, Math.Log10(d_Min) + d_Rel * (Math.Log10(md_FreqMax) - Math.Log10(d_Min)));
            else
                return d_Min + d_Rel * (md_FreqMax - d_Min);
        }

        // ------------------------------------

        void GetYRange(out double d_Bottom, out double d_Top)
        {
            double d_Max = 0;
            foreach (double d_Val in md_Amp)
            {
                d_Max = Math.Max(d_Max, d_Val);
            }

            if (mb_Decibel)
            {
                d_Top    = Math.Ceiling((ToDbV(d_Max) + 5) / 10) * 10;
                d_Bottom = d_Top - DB_RANGE;
            }
            else // Volt
            {
                d_Top    = d_Max > 0 ? d_Max * 1.1 : 1;
                d_Bottom = 0;
            }
        }

        int AmplitudeToY(double d_Amp, double d_Bottom, double d_Top)
        {
            double d_Val = mb_Decibel ? ToDbV(d_Amp) : d_Amp;
            double d_Rel = (d_Val - d_Bottom) / (d_Top - d_Bottom);
            d_Rel = Math.Max(-0.01, Math.Min(1.01, d_Rel));
            return (int)(mr_Plot.Bottom - d_Rel * mr_Plot.Height);
        }

        String FormatAmplitude(double d_Amp)
        {
            return mb_Decibel ? ToDbV(d_Amp).ToString("0.0", CultureInfo.InvariantCulture) + " dBV"
                              : Utils.FormatVoltage(d_Amp);
        }

        /// <summary>
        /// Converts Volt peak into dBV (RMS) as displayed by most oscilloscopes. 1 Vrms = 0 dBV
        /// </summary>
        public static double ToDbV(double d_VoltPeak)
        {
            return 20.0 * Math.Log10(Math.Max(d_VoltPeak / Math.Sqrt(2), 1e-12));
        }
    }
}
