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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using IOperation        = Operations.OperationManager.IOperation;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using Utils             = OsziWaveformAnalyzer.Utils;
using OsziPanel         = OsziWaveformAnalyzer.OsziPanel;
using RtfDocument       = OsziWaveformAnalyzer.RtfDocument;
using RtfBuilder        = OsziWaveformAnalyzer.RtfBuilder;

namespace Operations
{
    /// <summary>
    /// Operation decode magnetic stripe data on cards as defined in ISO 7813
    /// Magnetic Stripe data: 
    /// Track 1 has a lead-in of 58 ZERO's
    /// Track 2 has a lead-in of 21 ZERO's
    /// The frequency relation is 14 : 5
    /// </summary>
    public partial class DecodeMagStripe : IOperation
    {
        /// <summary>
        /// Static Constructor
        /// </summary>
        static DecodeMagStripe()
        {
            Utils.CheckDemoFileExists("ISO 7813 Magnetic Stripe Bank Card.oszi");
            Utils.CheckDemoFileExists("ISO 7813 Magnetic Stripe Card 3 Tracks.oszi");
        }

        // Allow max 20% devitation of the lead-in toggle lengths, which are long intervals ("0")
        const int MAX_LEAD_IN_DEVIATION = 20;

        // Get the first 5 lead-in toggles which have the same length
        const int LEAD_IN_TOGGLES = 5;

        // Add 20% of the lock deviation to the current clock length to adapt to varying slide speed.
        const float CLOCK_ADAPTION = 0.2f;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null)
                return; // the user did not click on a channel

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText  = "Decode ISO 7813 magnetic stripe tracks";
            i_Item.ms_ImageFile = "BankCard.ico";
            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_ClickChannel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            if (OsziPanel.CurCapture.ms32_AnalogCount > 1)
                Utils.OsziPanel.SeparateChannels = true;

            int s32_SmplPerBit = 0;
            foreach (Channel i_Channel in OsziPanel.CurCapture.mi_Channels)
            {
                Digitizer.DigitizeAdaptive(i_Channel, 0.25f);
                if (!DecodeSelfSyncData(i_Channel, ref s32_SmplPerBit))
                {
                    // Track 1 and 3 contain a clean sine wave, which can be digitized with adaptive mode.
                    // But digitizing Track2 is very difficult because it does not contain a clean signal.
                    // If Adaptive mode does not work -> try Min/Max mode.
                    if (i_Channel.mf_Analog != null)
                        i_Channel.mu8_Digital = null;

                    Digitizer.DigitizeAtMinMax(i_Channel, 0.05f);
                    DecodeSelfSyncData(i_Channel, ref s32_SmplPerBit);
                }
            }
            return DecodeTrackData(s32_SmplPerBit);
        }

        /// <summary>
        /// For the self synchronizing data the clock must be regenerated from the data.
        /// The clock may vary depending on the sliding speed of the card.
        /// A ONE is transmitted as two voltage toggles and a ZERO is transmitted as one toggle.
        ///  __    __    ____      ____    __
        /// |  |__|  |__|    |____|    |__|  |__
        ///   One   One  Zero Zero Zero  One
        /// 
        /// The data starts with a lead-in which is a sequence of zeroe's (long toggles)
        /// This is used to get the frequency of the signal, then the toggles are used to synchronize with the following data.
        /// </summary>
        bool DecodeSelfSyncData(Channel i_Channel, ref int s32_SmplPerBit)
        {
            List<int> i_Toggles = FindLeadIn(i_Channel.mu8_Digital);
            if (i_Toggles == null)
                return false; // Lead-in not found

            // The lead-in toggles are a full period == two long intervals ("0", "0")
            // d_Clock represents one long interval ("0")
            int  s32_Start =  i_Toggles[0];
            double d_Clock = (i_Toggles[1] - s32_Start) / 2.0;

            List<SmplMark> i_MarkRow1 = new List<SmplMark>();

            // Add one mark row. The second mark row will be added later when decoding was successful.
            i_Channel.mi_MarkRows    = new List<SmplMark>[2];
            i_Channel.mi_MarkRows[0] = i_MarkRow1;

            // Find toggles and check if they are short or long intervals and adjust s32_Clock to the current clock interval.
            Byte  u8_Last  = i_Channel.mu8_Digital[s32_Start];
            int s32_Shorts = 0; // count of successive short intervals
            int s32_Toggle = s32_Start;
            for (int S=s32_Start; S<i_Channel.mu8_Digital.Length; S++)
            {
                Byte u8_Value = i_Channel.mu8_Digital[S];
                if (u8_Value == u8_Last)
                    continue;

                u8_Last = u8_Value;                
                int s32_Interval = S - s32_Toggle;
                s32_Toggle = S;

                double d_DiffLong  = s32_Interval - d_Clock;
                double d_DiffShort = s32_Interval - d_Clock / 2.0;
                
                if (Math.Abs(d_DiffShort) < Math.Abs(d_DiffLong)) // Short
                {
                    // Two short intervals decode to one "1"
                    if (++s32_Shorts == 2)
                    {
                        s32_Shorts = 0;
                        i_MarkRow1.Add(new SmplMark(eMark.Text, s32_Start, S, "1", 1));
                        s32_Start = S;
                    }
                    d_Clock += d_DiffShort * CLOCK_ADAPTION; // adapt clock
                }
                else // Long
                {
                    // Short intervals must always be pairs.
                    // These are invalid: (Long, Short, Long) or (Long, Short, Short, Short, Long)
                    if (s32_Shorts > 0)
                    {
                        // Set ms32_Value = -1 --> error
                        i_MarkRow1.Add(new SmplMark(eMark.Error, (int)(s32_Start + d_Clock    + 0.5), -1, null, -1));
                        i_MarkRow1.Add(new SmplMark(eMark.Error, (int)(s32_Start + d_Clock *2 + 0.5), -1, null, -1));
                        i_MarkRow1.Add(new SmplMark(eMark.Error, (int)(s32_Start + d_Clock *3 + 0.5), -1, "  Sync Lost", -1));
                        return false;
                    }

                    // One long interval decodes to one "0"
                    i_MarkRow1.Add(new SmplMark(eMark.Text, s32_Start, S, "0", 0));
                    s32_Start = S;

                    d_Clock += d_DiffLong * CLOCK_ADAPTION; // adapt clock
                }
            }

            // Get the shortest samples per bit (from Track 1 or Track 3)
            if (s32_SmplPerBit == 0)
                s32_SmplPerBit = (int)(d_Clock + 0.5);
            else
                s32_SmplPerBit = Math.Min(s32_SmplPerBit, (int)(d_Clock + 0.5));

            return true;
        }

        /// <summary>
        /// Search in mu8_Digital the first (LEAD_IN_TOGGLES -1) periods that have the same length 
        /// with MAX_LEAD_IN_DEVIATION percent deviation.
        /// returns the sample points that were found or null if invalid data
        /// </summary>
        List<int> FindLeadIn(Byte[] u8_Digital)
        {
            if (u8_Digital == null)
                return null;

            List<int> i_Toggles = new List<int>();
            Byte u8_Last =  u8_Digital[0];
            for (int S=1; S<u8_Digital.Length; S++)
            {
                Byte u8_Value = u8_Digital[S];
                if (u8_Last == 0 && u8_Value == 1) // toggle 0 --> 1
                {
                    i_Toggles.Add(S);
                    if (i_Toggles.Count == LEAD_IN_TOGGLES)
                    {
                        int s32_First = i_Toggles[1] - i_Toggles[0];
                        for (int T=2; true; T++)
                        {
                            if (T == LEAD_IN_TOGGLES)
                                return i_Toggles; // success

                            int s32_Interval = i_Toggles[T] - i_Toggles[T-1];
                            int s32_Percent  = 100 * Math.Abs(s32_Interval - s32_First) / s32_First;
                            if (s32_Percent > MAX_LEAD_IN_DEVIATION)
                            {
                                i_Toggles.RemoveAt(0);
                                break; // samples do not match --> remove the first toggle and continue searching
                            }
                        }
                    }
                }
                u8_Last = u8_Value;
            }
            return null;
        }

        String DecodeTrackData(int s32_SmplPerBit)
        {
            RtfDocument i_RtfDoc = new RtfDocument(Color.White);
            RtfBuilder i_Builder = i_RtfDoc.CreateNewBuilder();

            int s32_Success = 0;
            for (int C=0; C<OsziPanel.CurCapture.mi_Channels.Count; C++)
            {
                Channel i_Channel = OsziPanel.CurCapture.mi_Channels[C];
                if (i_Channel.mi_MarkRows == null)
                    continue;

                for (int R=0; R<2; R++)
                {
                    bool b_Reverse = (R>0);

                    int s32_LeadIn, s32_LeadOut = 0;
                    List<SmplMark> i_Bits = ExtractBits(i_Channel.mi_MarkRows[0], b_Reverse, out s32_LeadIn);
                    if (i_Bits == null)
                        break; // MarkRow 1 has error (Sync Lost)

                    int s32_Bits = 6;
                    String s_Track = DecodeChars(i_Channel, i_Bits, s32_Bits, b_Reverse, 0x20, '%', '?', ref s32_LeadOut);
                    if (s_Track == null)
                    {
                        s32_Bits = 4;
                        s_Track  =   DecodeChars(i_Channel, i_Bits, s32_Bits, b_Reverse, 0x30, ';', '?', ref s32_LeadOut);
                    }

                    if (s_Track != null)
                    {
                        Color c_Color = OsziPanel.GetChannelColor(i_Channel);
                        i_Builder.AppendLine(c_Color, i_Channel + ":", FontStyle.Underline);

                        int s32_DataBits = (s_Track.Length + 1) * (s32_Bits + 1);
                        i_Builder.Append2ColorPair(c_Color, "Data:",      11, Color.White, "{0}",                        s_Track);
                        i_Builder.Append2ColorPair(c_Color, "Reverse:",   11, Color.White, "{0}",                        b_Reverse);
                        i_Builder.Append2ColorPair(c_Color, "Bits/Char:", 11, Color.White, "{0} + Parity",               s32_Bits);
                        i_Builder.Append2ColorPair(c_Color, "Lead-In:",   11, Color.White, "{0} Zero's",                 s32_LeadIn);
                        i_Builder.Append2ColorPair(c_Color, "Data Len:",  11, Color.White, "{0} Chars + LRC = {1} Bits", s_Track.Length, s32_DataBits);
                        i_Builder.Append2ColorPair(c_Color, "Lead-Out:",  11, Color.White, "{0} Zero's",                 s32_LeadOut);
                        i_Builder.Append2ColorPair(c_Color, "Total:",     11, Color.White, "{0} Bits\n\n",               s32_LeadIn + i_Bits.Count);

                        s32_Success ++;
                    }
                }
            }

            // This must be called also in case if nothing was detected to show any added digital channels.
            Utils.OsziPanel.RecalculateEverything();

            // Show RTF, if created and switch to tab "Decoder"
            Utils.FormMain.ShowAnalysisResult(i_RtfDoc, s32_SmplPerBit); 

            if (i_Builder.IsEmpty)
                return "Error: Nothing detected.";
            else
                return "Found data in " + s32_Success + " tracks";
        }

        List<SmplMark> ExtractBits(List<SmplMark> i_MarkRow, bool b_Reverse, out int s32_LeadIn)
        {
            List<SmplMark> i_MarkList = new List<SmplMark>();
            s32_LeadIn = 0;
            bool b_Start = false;

            for (int i=0; i<i_MarkRow.Count; i++)
            {
                int M = b_Reverse ? i_MarkRow.Count -1 -i : i;

                SmplMark i_Mark = i_MarkRow[M];
                switch (i_Mark.ms32_Value)
                {
                    case -1:
                        return null; // MarkRow 1 has error (Sync Lost)
                    case 0:
                        if (b_Start) i_MarkList.Add(i_Mark); 
                        else         s32_LeadIn ++;
                        break;
                    case 1:
                        i_MarkList.Add(i_Mark); 
                        b_Start = true;
                        break;
                }
            }
            return i_MarkList;
        }

        /// <summary>
        /// Convert Bit Stream --> Bytes and check parity bit
        /// s32_Bits does not includes parity bit
        /// returns null on parity error
        /// </summary>
        String DecodeChars(Channel i_Channel, List<SmplMark> i_Bits, int s32_Bits, bool b_Reverse,
                           int s32_FirstAscii, Char c_StartSentinel, Char c_EndSentinel, ref int s32_LeadOut)
        {
            List<SmplMark> i_MarkRow2 = new List<SmplMark>();
            StringBuilder  i_TrackTxt = new StringBuilder();
            int s32_Start = 0;
            bool b_LRC = false;
            int s32_XOR = 0;
            while (s32_Start < i_Bits.Count - s32_Bits - 1)
            {
                bool b_Odd   = true;
                int s32_Byte = 0;
                int s32_Mask = 1;
                for (int B=0; B<s32_Bits; B++, s32_Mask <<= 1)
                {
                    if (i_Bits[s32_Start + B].ms32_Value == 1)
                    {
                        s32_Byte |= s32_Mask;
                        b_Odd = !b_Odd;
                    }
                }

                bool b_Parity = i_Bits[s32_Start + s32_Bits].ms32_Value == 1;
                if  (b_Parity != b_Odd)
                    break;

                SmplMark i_Mark0  = i_Bits[s32_Start];            // Bit 0
                SmplMark i_MarkP  = i_Bits[s32_Start + s32_Bits]; // Bit Parity
                int s32_SampleMin = b_Reverse ? i_MarkP.ms32_FirstSample : i_Mark0.ms32_FirstSample;
                int s32_SampleMax = b_Reverse ? i_Mark0.ms32_LastSample  : i_MarkP.ms32_LastSample;

                s32_Start += s32_Bits + 1;

                if (b_LRC)
                {
                    if (s32_XOR != s32_Byte)
                        break;

                    s32_LeadOut = i_Bits.Count - s32_Start;

                    i_MarkRow2.Add(new SmplMark(eMark.Text, s32_SampleMin, s32_SampleMax, "LRC"));
                    i_Channel.mi_MarkRows[1] = i_MarkRow2; // marks will later be sorted
                    return i_TrackTxt.ToString(); // success
                }
                else // Chars
                {
                    Char c_Char = (Char)(s32_FirstAscii + s32_Byte);
                    s32_XOR ^= s32_Byte;

                    if (i_TrackTxt.Length == 0 && c_Char != c_StartSentinel)
                        break;

                    i_TrackTxt.Append(c_Char);
                    i_MarkRow2.Add(new SmplMark(eMark.Text, s32_SampleMin, s32_SampleMax, new String(c_Char, 1)));

                    b_LRC = (c_Char == c_EndSentinel);
                }
            }

            Debug.Print(String.Format("Discard {0}, {1} bits, {2}: '{3}'", i_Channel, s32_Bits, b_Reverse ? "Reverse" : "Normal ", i_TrackTxt));
            return null; // failure
        }
    }
}

