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
//    #define PRINT_RAW_SAMPLES
#endif

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
using Crc32             = ExImport.Crc32;

namespace Operations
{
    /// <summary>
    /// Operation Decode Infrared Remote Control uses a generic decoder that works for all infrared controls.
    /// It detects the length of one bit (the shortest unit) and relates the other signals to this length.
    /// The result is converted to an ASCII string which is unique to each remote control command.
    /// The meaning of the buttons can be defined in RemoteControl.ini
    /// </summary>
    public partial class DecodeInfrared : IOperation
    {
        #region class IRPacket

        class IRPacket
        {
            public String ms_BitChars;
            public UInt32 mu32_CRC;
            public String ms_IniButton;
            public int    ms32_StartSample; // The start sample of the first bit
            public int    ms32_EndSample;

            public IRPacket(int s32_StartSmpl, int s32_EndSmpl, String s_BitChars, UInt32 u32_CRC, String s_IniButton)
            {
                ms32_StartSample = s32_StartSmpl;
                ms32_EndSample   = s32_EndSmpl;
                ms_BitChars      = s_BitChars;
                mu32_CRC         = u32_CRC;
                ms_IniButton     = s_IniButton;
            }
        }

        #endregion

        /// <summary>
        /// Static Constructor
        /// </summary>
        static DecodeInfrared()
        {
            Utils.CheckDemoFileExists("Infrared Remote Control Grundig.oszi");
            Utils.CheckDemoFileExists("Infrared Remote Control Yamaha.oszi");
        }

        // The time in µs after which idle state is detected (no signal)
        const int MAX_SILENCE = 15000;

        // If a HIGH or LOW interval is longer than MAX_BITLEN bits, it is replaced with 'X' or 'x'.
        const int MAX_BITLEN  = 4; 

        int                        ms32_LimitHi;
        int                        ms32_LimitLo;
        int                        ms32_AverageHi;
        int                        ms32_AverageLo;
        List<SmplMark>             mi_MarkRow1;
        Dictionary<String, String> mi_RemoteIni;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null || b_Analog)
                return; 

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText  = "Decode infrared remote control";
            i_Item.ms_ImageFile = "RemoteControl.ico";
            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            try
            {
                String s_IniPath = Path.Combine(Utils.AppDir, "RemoteControl.ini");
                if (File.Exists(s_IniPath))
                    mi_RemoteIni = Utils.ReadIniFile(s_IniPath);

                IRPacket[] i_Packets = Decode(i_Channel);
                ShowRtf(i_Packets);

                Utils.OsziPanel.RecalculateEverything();

                if (i_Packets.Length == 0)
                    return "Error: No remote control packets found.";
                else
                    return "Decoded " + i_Packets.Length + " IR packets";
            }
            catch (Exception Ex)
            {
                Utils.OsziPanel.RecalculateEverything();
                return "Error: " + Ex.Message;
            }
        }

        // =============================================================================================================

        IRPacket[] Decode(Channel i_Channel)
        {
            Byte[] u8_Digi = i_Channel.mu8_Digital;

            // The count of samples after which idle state is detected (no signal)
            int s32_IdleSamples = (int)((Int64)MAX_SILENCE * 1000000 / OsziPanel.CurCapture.ms64_SampleDist);

            // -----------------------------------------

            // Find out if the signal is inverted.
            // Normally High = inactive (idle)
            // Search for a pause longer than s32_IdleSamples and detect if the idle state is High or Low.
            Byte u8_Inactive = 0xFF; // invalid;

            int s32_Samples = 0;
            Byte u8_Last = u8_Digi[0];
            for (int S=1; S<u8_Digi.Length; S++)
            {
                Byte u8_State = u8_Digi[S];
                if (u8_State == u8_Last)
                {
                    s32_Samples ++;
                    if (s32_Samples > s32_IdleSamples)
                    {
                        u8_Inactive = u8_State;
                        break;
                    }
                }
                else
                {
                    s32_Samples = 0;
                    u8_Last = u8_State;
                }
            }

            if (u8_Inactive == 0xFF)
                throw new Exception("No idle interval detected.");

            // -----------------------------------------

            List<int> i_IntervalHi = new List<int>();
            List<int> i_IntervalLo = new List<int>();

            s32_Samples = 0;
            u8_Last = u8_Digi[0];
            for (int S=1; S<u8_Digi.Length; S++)
            {
                s32_Samples ++;

                Byte u8_State = u8_Digi[S];
                if (u8_State != u8_Last)
                {
                    if (u8_Last == u8_Inactive) i_IntervalHi.Add(s32_Samples);
                    else                        i_IntervalLo.Add(s32_Samples);

                    s32_Samples = 0;
                    u8_Last = u8_State;
                }
            }

            // -----------------------------------------

            // Get the shortest interval of all intervals separately for High and Low.
            int s32_ShortestHi = int.MaxValue;
            int s32_ShortestLo = int.MaxValue;
            foreach (int s32_Interval in i_IntervalHi)
            {
                s32_ShortestHi = Math.Min(s32_ShortestHi, s32_Interval);
            }
            foreach (int s32_Interval in i_IntervalLo)
            {
                s32_ShortestLo = Math.Min(s32_ShortestLo, s32_Interval);
            }

            // Get the average of all short intervals (which are shorter than s32_Shortest * 1.5)
            int s32_AvgCountLo = 0;
            int s32_AvgCountHi = 0;
            ms32_AverageLo     = 0;
            ms32_AverageHi     = 0;
            ms32_LimitLo       = s32_ShortestLo + s32_ShortestLo / 2;
            ms32_LimitHi       = s32_ShortestHi + s32_ShortestHi / 2;

            // -----------------------------------------

            // Calculate averages Hi and Lo of short intervals (shorter than limit)

            foreach (int s32_Interval in i_IntervalHi)
            {
                if (s32_Interval < ms32_LimitHi)
                {
                    ms32_AverageHi += s32_Interval;
                    s32_AvgCountHi ++;
                }
            }
            foreach (int s32_Interval in i_IntervalLo)
            {
                if (s32_Interval < ms32_LimitLo)
                {
                    ms32_AverageLo += s32_Interval;
                    s32_AvgCountLo ++;
                }
            }

            if (s32_AvgCountLo > 0) ms32_AverageLo /= s32_AvgCountLo; 
            if (s32_AvgCountHi > 0) ms32_AverageHi /= s32_AvgCountHi;

            #if PRINT_RAW_SAMPLES
                Debug.Print(String.Format("Lo: Shortest: {0,4} Spl, Limit: {1,4} Spl, Average over {2,2} short intervals: {3,4} Spl", 
                                          s32_ShortestLo, ms32_LimitLo, s32_AvgCountLo, ms32_AverageLo));

                Debug.Print(String.Format("Hi: Shortest: {0,4} Spl, Limit: {1,4} Spl, Average over {2,2} short intervals: {3,4} Spl", 
                                          s32_ShortestHi, ms32_LimitHi, s32_AvgCountHi, ms32_AverageHi));
            #endif

            // Check that average Low and average High do not deviate too much
            if ((ms32_AverageHi > ms32_AverageLo * 2) || (ms32_AverageLo > ms32_AverageHi * 2))
                throw new Exception("No valid shortest Low / High bit lengths detected.");

            // -----------------------------------------

            mi_MarkRow1 = new List<SmplMark>();
            i_Channel.mi_MarkRows = new List<SmplMark>[] { mi_MarkRow1 };

            s32_Samples = 0;
            List<int> i_TogglePoints = new List<int>();

            List<IRPacket> i_Packets = new List<IRPacket>();
            u8_Last = u8_Digi[0];
            for (int S=0; S<u8_Digi.Length; S++)
            {
                Byte u8_State = u8_Digi[S];
                if (u8_State == u8_Last)
                {
                    s32_Samples ++;
                    if (s32_Samples > s32_IdleSamples && u8_State == u8_Inactive && i_TogglePoints.Count > 0)
                    {
                        i_Packets.Add(DecodePacket(i_TogglePoints));
                        i_TogglePoints.Clear();
                    }
                }
                else
                {
                    // The signal must be non-idle to store the first interval
                    if (u8_State != u8_Inactive || i_TogglePoints.Count > 0)
                        i_TogglePoints.Add(S);

                    s32_Samples = 0;
                    u8_Last = u8_State;
                }
            }
            return i_Packets.ToArray();
        }

        // =============================================================================================================

        /// <summary>
        /// The first toggle point in the List is always from inactive to active level (IR LED turned on)
        /// </summary>
        IRPacket DecodePacket(List<int> i_TogglePoints)
        {
            StringBuilder i_BitChars = new StringBuilder();
            Crc32 i_CRC = new Crc32();

            bool b_High = false;
            int s32_Last = i_TogglePoints[0];
            for (int i=1; i<i_TogglePoints.Count; i++)
            {
                int s32_Interval = i_TogglePoints[i] - s32_Last;
                int s32_Average  = b_High ? ms32_AverageHi : ms32_AverageLo;
                double d_Factor  = (double)s32_Interval / s32_Average;
                int s32_BitLen   = (int)(d_Factor + 0.5);

                // Convert LOW intervals into lowercase characters and HIGH intervals into upper case characters
                Char c_Char = (Char)((b_High ? 'A' : 'a') + Math.Max(0, s32_BitLen - 1)); 

                // At the beginning of the IR command there are usually very long intervals of 5, 8 or 16 bits of HIGH or LOW.
                // These are used to wake up the microprocesser in the Hifi device / TV. 
                // They are not precise (not an integer multiple of the average short interval) so they may be for example one time 'p' and next time 'o'.
                // For that reason they are replaced with an 'X' or 'x' here.
                if (s32_BitLen > MAX_BITLEN)
                    c_Char = b_High ? 'X' : 'x';

                i_BitChars.Append(c_Char);
                i_CRC.Calc((Byte)c_Char);

                #if PRINT_RAW_SAMPLES            
                    int s32_Limit = b_High ? ms32_LimitHi : ms32_LimitLo;
                    Debug.Print(String.Format("{0}: {1,5} Spl {2} --> Factor: {3,5:0.00} --> Len: {4,2} --> Char: '{5}'", b_High ? "Hi" : "Lo",
                                              s32_Interval, s32_Interval < s32_Limit ? "short" : "     ", d_Factor, s32_BitLen, c_Char));
                #endif    

                mi_MarkRow1.Add(new SmplMark(eMark.Text, s32_Last, i_TogglePoints[i], new String(c_Char, 1)));
        
                s32_Last = i_TogglePoints[i];
                b_High   = !b_High;
            }

            UInt32 u32_CRC = i_CRC.Finish();

            String s_IniButton = null;
            if (mi_RemoteIni != null)
                mi_RemoteIni.TryGetValue(u32_CRC.ToString("X8"), out s_IniButton);

            return new IRPacket(i_TogglePoints[0], s32_Last, i_BitChars.ToString(), u32_CRC, s_IniButton);
        }

        // =============================================================================================================

        void ShowRtf(IRPacket[] i_Packets)
        {
            // i_Summary is printed before mi_RtfPackets
            RtfDocument i_RtfDoc  = new RtfDocument(Color.White);
            RtfBuilder  i_Builder = i_RtfDoc.CreateNewBuilder();

            i_Builder.AppendText("This universal IR decoder works with any remote control.\n");
            i_Builder.AppendText("It creates a unique CRC for each data packet. Buttons can be stored in RemoteControl.ini\n\n");
            i_Builder.AppendLine(Color.Magenta, "Decoded " + i_Packets.Length + " packets:\n", FontStyle.Underline);

            foreach (IRPacket i_Packet in i_Packets)
            {
                i_Builder.AppendTimestampLine(i_Packet.ms32_StartSample, i_Packet.ms32_EndSample, true);

                String s_CRC = i_Packet.mu32_CRC.ToString("X8");
                String s_Cmd = "RC - Command: " + s_CRC;
                if (i_Packet.ms_IniButton != null)
                    s_Cmd += "  " + i_Packet.ms_IniButton;

                i_Builder.AppendLine(Color.Yellow, "Decoded Bits: " + i_Packet.ms_BitChars);
                i_Builder.AppendLine(Color.Lime,   s_Cmd);
            }

            if (i_Packets.Length == 0)
                i_Builder.AppendLine(Utils.ERROR_COLOR, "Nothing detected");

            int s32_SmplPerBit = (ms32_AverageHi + ms32_AverageLo) / 2;

            // Show RTF, if created and switch to tab "Decoder"
            Utils.FormMain.ShowAnalysisResult(i_RtfDoc, s32_SmplPerBit); 
        }
    }
}

