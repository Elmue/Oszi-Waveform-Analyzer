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
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Windows.Forms;

using RtfDocument       = OsziWaveformAnalyzer.RtfDocument;
using RtfBuilder        = OsziWaveformAnalyzer.RtfBuilder;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using Utils             = OsziWaveformAnalyzer.Utils;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using Range             = OsziWaveformAnalyzer.Utils.Range;

namespace Operations
{
    /// <summary>
    /// This class contains helper functions to convert analog data into digital data.
    /// </summary>
    public class Digitizer
    {
        /// <summary>
        /// Convert an analog wave into a digital signal at the average voltage of the entire analog signal.
        /// f_HysteresisPct = 0.1 means that 10% hysteresis is applied to average --> 5% below average to 5% above average
        /// This function does not work for Magnetic Stripe Track data.
        /// </summary>
        public static void DigitizeAtAverage(Channel i_Channel, Range k_Range, float f_HysteresisPct)
        {
            if (i_Channel.mu8_Digital != null)
                return; // already digitized

            ApplyHysteresis(i_Channel, f_HysteresisPct);
            ThresholdAD(i_Channel, i_Channel, k_Range);
        }

        /// <summary>
        /// Create two threshold levels, one above and one below the average voltage
        /// f_Hysteresis = 0.1 means that 10% hysteresis is applied to average --> 5% below average to 5% above average
        /// </summary>
        public static void ApplyHysteresis(Channel i_Channel, float f_HysteresisPct)
        {
            if (i_Channel.mf_Analog == null)
                return;

            float f_Avrg  = CalcAverage(i_Channel.mf_Analog);
            float f_Range = i_Channel.mf_Max - i_Channel.mf_Min;
            i_Channel.mf_ThreshLo  = f_Avrg - f_Range * f_HysteresisPct / 2.0f;
            i_Channel.mf_ThreshHi  = f_Avrg + f_Range * f_HysteresisPct / 2.0f;
            i_Channel.mb_Threshold = true;
        }

        /// <summary>
        /// Calculates the average of all analog voltages.
        /// To avoid a double overflow with 24 million samples, calculate partial averages.
        /// </summary>
        public static float CalcAverage(float[] f_Analog)
        {
            if (f_Analog == null)
                return 0;

            int S=0;
            double d_TotAvrg = 0.0;
            int    s32_Total = 0;
            while (S < f_Analog.Length)
            {
                double d_PartAvrg = 0.0;
                int    s32_Parts  = 0;
                for (int i=0; i<10000 && S < f_Analog.Length; i++, S++)
                {
                    float f_Sample = f_Analog[S];
                    d_PartAvrg += f_Sample;
                    s32_Parts ++;
                }

                if (s32_Parts > 0)
                {
                    d_PartAvrg /= s32_Parts;
                    d_TotAvrg  += d_PartAvrg;
                    s32_Total ++;
                }
            }

            if (s32_Total > 1)
                d_TotAvrg /= s32_Total;

            return (float)d_TotAvrg;
        }

        /// <summary>
        /// Convert an analog wave into a digital signal at the thresholds stored in i_Analog.
        /// i_Analog and i_Digital may be the same channel or i_Digital may be a new channel.
        /// This function does not work for Magnetic Stripe Track data.
        /// </summary>
        public static void ThresholdAD(Channel i_Analog, Channel i_Digital, Range k_Range)
        {
            float[] f_Analog = i_Analog.mf_Analog;
            if (f_Analog == null)
                return;

            if (i_Digital.mu8_Digital == null)
                i_Digital.mu8_Digital = new Byte[f_Analog.Length];

            Byte[] u8_Digital = i_Digital.mu8_Digital;
          
            float f_Above = i_Analog.mf_ThreshHi;
            float f_Below = i_Analog.mf_ThreshLo;

            bool b_High = false;
            for (int S=k_Range.ms32_Start; S<k_Range.ms32_End; S++)
            {
                float f_Volt = f_Analog[S];

                if (b_High && f_Volt < f_Below) // below LOW threshold
                {
                    b_High = false;
                }
                else if (!b_High && f_Volt > f_Above) // above HIGH threshold
                {
                    b_High = true;
                }

                u8_Digital[S] = b_High ? (Byte)1 : (Byte)0;
            }

            i_Digital.mi_MarkRows = null;
            i_Digital.mi_SampleMinMax.mb_DigitalOK = false; // must be re-calculated
        }

        // =================================================================================================

        /// <summary>
        /// This function is useful for analog signals that don't have a fix average lavel (e.g. magnetic stripe data).
        /// It digitzes at 50% between the high and low peak voltages of each half period.
        /// f_MinAmplitudePct = 0.1 means that noise below 10% of the signal amplitude is ignored.
        /// </summary>
        public static void DigitizeAdaptive(Channel i_Channel, Range k_Range, float f_MinAmplitudePct)
        {
            if (i_Channel.mu8_Digital != null)
                return; // already digitized

            AdaptiveAD(i_Channel, i_Channel, k_Range, f_MinAmplitudePct);
        }

        /// <summary>
        /// This function is useful for analog signals that don't have a fix average lavel (e.g. magnetic stripe data).
        /// It digitizes at 50% between the high and low peak voltages of each half period.
        /// f_MinAmplitudePct = 0.1 means that noise below 10% of the signal amplitude is ignored.
        /// i_Analog and i_Digital may be the same channel or i_Digital may be a new channel.
        /// </summary>
        public static void AdaptiveAD(Channel i_Analog, Channel i_Digital, Range k_Range, float f_MinAmplitudePct)
        {
            float[] f_Analog  = i_Analog.mf_Analog;
            if (f_Analog == null)
                return;

            if (i_Digital.mu8_Digital == null)
                i_Digital.mu8_Digital = new Byte[f_Analog.Length];

            Byte[] u8_Digital = i_Digital.mu8_Digital;

            float f_MinAmplVolt = (i_Analog.mf_Max - i_Analog.mf_Min) * f_MinAmplitudePct;

            bool b_SearchHigh = false;
            for (int S=k_Range.ms32_Start + 1; S<k_Range.ms32_End; S++)
            {
                if (f_Analog[S] > f_Analog[k_Range.ms32_Start] + f_MinAmplVolt)
                {
                    b_SearchHigh = false;
                    break;
                }
                if (f_Analog[S] < f_Analog[k_Range.ms32_Start] - f_MinAmplVolt)
                {
                    b_SearchHigh = true;
                    break;
                }
            }

            bool  b_LowFound   = false;
            bool  b_HighFound  = false;
            float f_Highest    = float.MinValue;
            float f_Lowest     = float.MaxValue;
            int   s32_SplHigh  = 0;
            int   s32_SplLow   = 0;
            int   s32_SplHL    = 0;
            int   s32_SplLH    = 0;
            for (int S=k_Range.ms32_Start; S<k_Range.ms32_End; S++)
            {
                u8_Digital[S] = 0;

                float f_Volt = f_Analog[S];
                if (b_SearchHigh)
                {
                    if (f_Volt > f_Highest) // rising
                    {
                        f_Highest   = f_Volt;
                        s32_SplHigh = S;
                    }

                    if (f_Volt < f_Highest - f_MinAmplVolt) // falling again
                        b_HighFound = true;
                }
                else
                {
                    if (f_Volt < f_Lowest) // falling
                    {
                        f_Lowest   = f_Volt;
                        s32_SplLow = S;
                    }

                    if (f_Volt > f_Lowest + f_MinAmplVolt) // rising again
                        b_LowFound = true;
                }

                if (b_SearchHigh && b_HighFound)
                {
                    if (b_LowFound)
                    {
                        float f_Average = (f_Highest + f_Lowest) / 2.0f;
                        for (int P=s32_SplLow; P<=s32_SplHigh; P++)
                        {
                            if (f_Analog[P] > f_Average)
                            {
                                s32_SplLH = P;
                                break;
                            }
                        }
                    }
                    b_SearchHigh = false;
                    b_LowFound   = false;
                    f_Lowest     = float.MaxValue;
                }

                if (!b_SearchHigh && b_LowFound)
                {
                    if (b_HighFound)
                    {
                        float f_Average = (f_Highest + f_Lowest) / 2.0f;
                        for (int P=s32_SplHigh; P<=s32_SplLow; P++)
                        {
                            if (f_Analog[P] < f_Average)
                            {
                                s32_SplHL = P;
                                break;
                            }
                        }

                        // fill all the intermediate samples with 1 (HIGH)
                        if (s32_SplLH > 0)
                        {
                            for (int P=s32_SplLH; P<s32_SplHL; P++)
                            {
                                u8_Digital[P] = 1;
                            }
                        }
                    }
                    b_SearchHigh = true;
                    b_HighFound  = false;
                    f_Highest    = float.MinValue;
                }
            }

            i_Digital.mi_MarkRows = null;
            i_Digital.mi_SampleMinMax.mb_DigitalOK = false; // must be re-calculated
        }

        // =================================================================================================

        /// <summary>
        /// This A/D conversion switches the digital state where the highest and the lowest analog voltage are reached.
        /// f_Hysteresis = 0.1 means that 10% hysteresis is applied to average --> 5% below average to 5% above average.
        /// This function is not useful for square signals!
        /// </summary>
        public static void DigitizeAtMinMax(Channel i_Channel, Range k_Range, float f_Hysteresis)
        {
            if (i_Channel.mu8_Digital != null)
                return; // already digitized

            ApplyHysteresis(i_Channel, f_Hysteresis);
            MinMaxAD(i_Channel, i_Channel, k_Range);
        }

        /// <summary>
        /// This A/D conversion switches the digital state where the highest and the lowest analog voltage are reached.
        /// i_Analog and i_Digital may be the same channel or i_Digital may be a new Channel.
        /// This function is not useful for square signals!
        /// </summary>
        public static void MinMaxAD(Channel i_Analog, Channel i_Digital, Range k_Range)
        {
            float[] f_Analog = i_Analog.mf_Analog;
            if (f_Analog == null)
                return;

            if (i_Digital.mu8_Digital == null)
                i_Digital.mu8_Digital = new Byte[f_Analog.Length];

            Byte[] u8_Digital = i_Digital.mu8_Digital;

            float f_Above  =  i_Analog.mf_ThreshHi;
            float f_Below  =  i_Analog.mf_ThreshLo;
            float f_Middle = (i_Analog.mf_ThreshHi + i_Analog.mf_ThreshLo) / 2.0f;

            bool  b_High    = false;
            float f_Highest = float.MinValue;
            float f_Lowest  = float.MaxValue;
            int s32_SplHigh = 0;
            int s32_SplLow  = 0;
            for (int S=k_Range.ms32_Start; S<k_Range.ms32_End; S++)
            {
                u8_Digital[S] = 0;

                float f_Volt = f_Analog[S];
                if (b_High && f_Volt < f_Below) // below threshold
                {
                    b_High = false;
                    f_Highest = f_Middle;
                }
                else if (!b_High && f_Volt > f_Above) // above threshold
                {
                    b_High = true;
                    f_Lowest = f_Middle;
                    if (s32_SplLow > s32_SplHigh)
                    {
                        // fill all the intermediate samples with 1 (HIGH)
                        for (int i=s32_SplHigh; i<s32_SplLow; i++)
                        {
                            u8_Digital[i] = 1;
                        }
                    }
                }
                
                // Do not toggle if there is noise between Below and Above
                if (b_High && f_Volt > f_Highest && f_Volt > f_Above)
                {
                    f_Highest = f_Volt;
                    s32_SplHigh = S;
                }
                else if (!b_High && f_Volt < f_Lowest && f_Volt < f_Below)
                {
                    f_Lowest = f_Volt;
                    s32_SplLow = S;
                }
            }

            i_Digital.mi_MarkRows = null;
            i_Digital.mi_SampleMinMax.mb_DigitalOK = false; // must be re-calculated
        }
    }
}

