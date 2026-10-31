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
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;


namespace OsziWaveformAnalyzer
{
    public class VUMeter : Panel
    {
        const double MIN_DB = -35.0;
        const double MAX_DB =   0.0;

        // Show a slowly decreasing VU bar: subtract pixels when no volume.
        // This value is perfect if ShowVolume() is called every 100 ms.
        const int DECAY = 12;

        Image     mi_VuMeter = Utils.LoadResourceImage("VUMeter.png");
        Image     mi_VuBar   = Utils.LoadResourceImage("VUBar.png");
        Rectangle mr_SrcRect;
        Rectangle mr_DestRect;

        int ms32_BarLeft;
        int ms32_BarRight;

        public VUMeter()
        {
            DoubleBuffered = true;
            Size = mi_VuMeter.Size;

            mr_SrcRect  = new Rectangle(0, 0, mi_VuBar.Width, mi_VuBar.Height);
            mr_DestRect = mr_SrcRect;
        }

        /// <summary>
        /// This should be called every 100 ms for the VU meter to work correctly.
        /// f_Data = float[ChannelCount][SampleCount]
        /// </summary>
        public void ShowVolume(float[][] f_Data)
        {
            ms32_BarLeft = Calculate(f_Data[0], ms32_BarLeft);
            
            if (f_Data.Length > 1) ms32_BarRight = Calculate(f_Data[1], ms32_BarRight); // Stereo
            else                   ms32_BarRight = ms32_BarLeft; // Mono

            Invalidate();
        }

        /// <summary>
        /// Reset bars to zero
        /// </summary>
        public void Reset()
        {
            ms32_BarLeft  = 0;
            ms32_BarRight = 0;
            Invalidate();
        }

        int Calculate(float[] f_Samples, int s32_LastValue)
        {
            float f_Max = 0.00001f; // never zero!
            for (int i = 0; i < f_Samples.Length; i++)
            {
                f_Max = Math.Max(f_Max, Math.Abs(f_Samples[i]));
            }

            double d_Decibel = 20.0 * Math.Log10(f_Max);

            // limit to the decibel display range in VUMeter.png
            d_Decibel = Math.Min(MAX_DB, Math.Max(MIN_DB, d_Decibel));

            // factor to convert decibel --> pixel
            double d_Factor = (d_Decibel - MIN_DB) / (MAX_DB - MIN_DB);

            // Always show two green pixels if there is no audio signal (amplitude = -35 dB or less)
            int  s32_Pixels = Math.Max(2, (int)(mi_VuBar.Width * d_Factor));

            // Decay the previous volume every 100 ms, so the bar goes back to zero slowly
            return Math.Max(s32_LastValue - DECAY, s32_Pixels);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImage(mi_VuMeter, 0, 0, mi_VuMeter.Width, mi_VuMeter.Height);

            mr_SrcRect .Width = ms32_BarLeft;
            mr_DestRect.Width = ms32_BarLeft;
            mr_DestRect.X = 2;
            mr_DestRect.Y = 12;
            e.Graphics.DrawImage(mi_VuBar, mr_DestRect, mr_SrcRect, GraphicsUnit.Pixel);

            mr_SrcRect .Width = ms32_BarRight;
            mr_DestRect.Width = ms32_BarRight;
            mr_DestRect.X = 2;
            mr_DestRect.Y = 57;
            e.Graphics.DrawImage(mi_VuBar, mr_DestRect, mr_SrcRect, GraphicsUnit.Pixel);
        }
    }
}
