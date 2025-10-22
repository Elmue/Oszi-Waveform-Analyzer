/*
------------------------------------------------------------
Oscilloscope Waveform Analyzer by ElmüSoft (www.netcult.ch/elmue)
This class is a helper for the OSZI file format.
The OSZI file format is free of any license.
You can use this code even in a closed-source commercial software.
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
using System.Xml;
using System.Drawing;
using System.Globalization;
using System.Collections.Generic;

using Rigol             = Transfer.Rigol;
using Utils             = OsziWaveformAnalyzer.Utils;
using eOsziSerie        = Transfer.TransferManager.eOsziSerie;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;


namespace ExImport
{
    /// <summary>
    /// Every oscilloscope vendor invents a totally different CSV format.
    /// A generic parser is impossible.
    /// </summary>
    public class CsvParser
    {
        #region enums

        enum eChanType
        {
            Invalid,
            // analog channel (float values)
            Analog,
            // digital channel (one byte contains 8 digital channels) Data is stored as hexadecimal bytes
            DigitalPodHex,
        }

        #endregion

        #region classes CsvChan, CsvConfig

        class CsvChan
        {
            // The name of the channel
            public String    ms_Name;
            // The CSV column of the channel
            public int       ms32_Column;
            // The type of channel
            public eChanType me_Type;
            // The first digital channel number of a POD of 8 channels that are captured together into one byte (0 or 8)
            public int       ms32_FirstDigital;
            // Factor to be multiplied with analog voltage: 1.0 for Volt, 0.001 if values are in mV
            public float     mf_Factor;
            // ===============================
            // Sample data
            public List<Byte>  mi_DigiValues = new List<Byte>();
            public List<float> mi_AnalValues = new List<float>();
        }

        class CsvConfig
        {
            // Comma or Semicolon
            public Char   mc_Separator;  
            // The count of rows in the header that must be skipped
            public int    ms32_HeaderLines;
            // The analog resolution (in bits) of the A/D converter in the oscilloscope
            public int    ms32_AnalogRes;
            // ===============================
            public bool   mb_Timestamps;
            // factor to be multiplied with the final time: 1.0 for seconds, 0.001 if time in milliseconds
            public double md_TimeFactor;
            // -----------
            // mb_Timestamps = true --> the column ms32_TimeCol contains timestamps
            public int    ms32_TimeCol;
            // -----------
            // mb_Timestamps = false --> md_TimeIncrement defines the interval between two samples
            public double md_TimeIncrement;
            // ===============================
            public List<CsvChan> mi_Channels = new List<CsvChan>();
        }

        #endregion

        const String ERR_MODEL = "\nDid you select the correct Oszi Model for this CSV file?";

        CsvConfig mi_Config;

        public Capture Parse(String s_Path, eOsziSerie e_Serie, ref bool b_Abort)
        {
            // If the oscilloscope captures 12 Megasamples, the CSV file will have 12 million lines and 300 Megabyte.
            // This requires a StreamReader to avoid loading the entire file to memory.
            using (StreamReader i_Reader = new StreamReader(s_Path))
            {
                LoadConfig(i_Reader, e_Serie);
                return ParseSamples(s_Path, i_Reader, ref b_Abort);
            }
        }

        // ===========================================================================================================
        // ===========================================================================================================

        /// <summary>
        /// Read all the lines after the header
        /// </summary>
        private Capture ParseSamples(String s_Path, StreamReader i_Reader, ref bool b_Abort)
        {
            // ----------- get min column count ----------

            int s32_MinColumns = mi_Config.ms32_TimeCol + 1;
            foreach (CsvChan i_Chan in mi_Config.mi_Channels)
            {
                s32_MinColumns = Math.Max(s32_MinColumns, i_Chan.ms32_Column + 1);
            }

            // ---------- go to first sample row ---------
            
            // return to begin of file
            i_Reader.BaseStream.Seek(0, SeekOrigin.Begin);
            i_Reader.DiscardBufferedData();

            // Skip header lines (some files use one header line, others use 2 lines)
            for (int L=0; L<mi_Config.ms32_HeaderLines; L++)
            {
                i_Reader.ReadLine();
            }

            // -------------- parse samples --------------

            String s_FirstStamp = null;
            String s_LastStamp  = null;
            int    s32_Samples  = 0;
            int    s32_Tick     = 0;

            for (int s32_Line = mi_Config.ms32_HeaderLines; true; s32_Line++)
            {
                if (b_Abort) 
                    return null;

                String s_Line = i_Reader.ReadLine();
                if (s_Line == null)
                    break; // end of file

                // Some stupid companies append the separator to the end of each line.
                s_Line = s_Line.Trim().TrimEnd(mi_Config.mc_Separator);
                if (s_Line.Length == 0)
                    continue; // skip empty lines

                String[] s_Parts = s_Line.Split(mi_Config.mc_Separator);
                if (s_Parts.Length < s32_MinColumns)
                    throw new Exception("The CSV file contains incomplete data in line " + (s32_Line+1) + ERR_MODEL);

                if (mi_Config.mb_Timestamps)
                {
                    if (s_FirstStamp == null)
                        s_FirstStamp = s_Parts[mi_Config.ms32_TimeCol];

                    s_LastStamp = s_Parts[mi_Config.ms32_TimeCol];
                }

                foreach (CsvChan i_CsvChan in mi_Config.mi_Channels)
                {
                    String s_Value = s_Parts[i_CsvChan.ms32_Column];
                    switch (i_CsvChan.me_Type)
                    {
                        case eChanType.DigitalPodHex:
                            i_CsvChan.mi_DigiValues.Add(ParseHexByte(s_Value, s32_Line));
                            break;
                        case eChanType.Analog:
                            i_CsvChan.mi_AnalValues.Add(ParseFloat(s_Value, s32_Line) * i_CsvChan.mf_Factor);
                            break;
                        default:
                            throw new ArgumentException(); // internal programming error
                    }
                }

                s32_Samples ++;

                // Reading a 300 MB file takes some time...
                if (Math.Abs(Environment.TickCount - s32_Tick) > 330)
                {
                    Utils.FormMain.PrintStatus("Reading sample " + s32_Samples + ". Please wait.", Color.Black);
                    s32_Tick = Environment.TickCount;
                }
            }

            if (s32_Samples < 3)
                throw new Exception("The CSV file is empty.");

            // ------------ Calulate Timing ------------------

            // ATTENTION: When capturing only the visible screen, the sample distance is NOT the samplerate displayed by the oscillscope.
            // The sample distance is always the time between 2 neighboured samples.
            // If you have 1200 samples and a total captured interval of 600 ms the sample distance is 500 µs 
            // and not the 25 MHz (40 ns) displayed by the oscilloscope.
            if (mi_Config.mb_Timestamps)
            {
                double d_Start = ParseDouble(s_FirstStamp, mi_Config.ms32_HeaderLines);
                double d_End   = ParseDouble(s_LastStamp,  mi_Config.ms32_HeaderLines + s32_Samples);
                 
                // Some stupid companies store the timestamps in reverse order!
                double d_TotTime = Math.Max(d_Start, d_End) - Math.Min(d_Start, d_End);

                // Subtract one because there is 1 interval between 2 samples.
                mi_Config.md_TimeIncrement = d_TotTime / (s32_Samples - 1);
            }

            // interval between 2 samples in seconds
            mi_Config.md_TimeIncrement *= mi_Config.md_TimeFactor;

            // --------------- Store in Capture class ---------------

            Capture i_Capture = new Capture();
            i_Capture.ms_Path         = s_Path;
            i_Capture.ms32_Samples    = s32_Samples;
            i_Capture.ms32_AnalogRes  = mi_Config.ms32_AnalogRes;
            i_Capture.ms64_SampleDist = (Int64)((decimal)mi_Config.md_TimeIncrement * Utils.PICOS_PER_SECOND);

            foreach (CsvChan i_CsvChan in mi_Config.mi_Channels)
            {
                switch (i_CsvChan.me_Type)
                {
                    case eChanType.DigitalPodHex:
                        Rigol.AppendPodChannels(i_Capture, i_CsvChan.mi_DigiValues.ToArray(), i_CsvChan.ms32_FirstDigital);
                        break;
                    case eChanType.Analog:
                        Channel i_AnalChan = new Channel(i_CsvChan.ms_Name);
                        i_AnalChan.mf_Analog = i_CsvChan.mi_AnalValues.ToArray();
                        i_Capture.mi_Channels.Add(i_AnalChan);
                        break;
                }
            }
            return i_Capture;
        }

        // ===========================================================================================================
        // ===========================================================================================================

        /// <summary>
        /// Load mi_Config with the oscilloscope specific settings
        /// </summary>
        private void LoadConfig(StreamReader i_Reader, eOsziSerie e_Serie)
        {
            mi_Config = new CsvConfig();

            // ---------- Read the first 3 lines ------------

            List<String[]> i_Header = new List<String[]>();
            while (i_Header.Count < 3)
            {
                String s_Line = i_Reader.ReadLine();
                if (s_Line == null)
                    throw new Exception("The CSV file is empty.");

                // Some stupid companies append the separator to the end of each line.
                s_Line = s_Line.Trim().TrimEnd(',', ';');
                if (s_Line.Length == 0)
                    continue; // skip empty lines

                // Detect the separator in the first line.
                // IMPORTANT:
                // Do not rely on a fix separator character per oscilloscope model here!
                // The separator may depend on the Windows configuration.
                // An english windows defines comma, while a german Windows defines semicolon.
                // So both are possible for the same oscilloscope model, if the CSV file was created by a Windows application.
                // This behaviour makes sense if the user opens the CSV firle in Excel which parses using the Windows separator.
                // It is an extremly stupid Microsoft design that a CSV (!COMMA! Separated Values) file uses semicolons!
                // If semicolon is used as separator, all floating point values use comma instead of dot.
                if (i_Header.Count == 0)
                {
                    // Some files use semicolon instead of comma
                         if (s_Line.Contains(";")) mi_Config.mc_Separator = ';';
                    else if (s_Line.Contains(",")) mi_Config.mc_Separator = ',';
                    else throw new Exception("The separator could not be detected." + ERR_MODEL);
                }

                i_Header.Add(s_Line.Split(mi_Config.mc_Separator));
            }

            // ---------- Parse the header lines ------------

            switch (e_Serie)
            {
                case eOsziSerie.Rigol_1000Z:    LoadConfigRigol1000Z   (i_Header); break;
                case eOsziSerie.Rigol_1000DE:   LoadConfigRigol1000DE  (i_Header); break;
                case eOsziSerie.OWON_1022:      LoadConfigOwon1022     (i_Header); break;
                case eOsziSerie.Picoscope_3206: LoadConfigPicoscope3206(i_Header); break;
                default:
                    throw new Exception("The oszi serie " + e_Serie + " is not implemented in the CSV parser.");
            }

            if (mi_Config.mi_Channels.Count == 0)
                throw new Exception("The CSV file must contain at least one channel." + ERR_MODEL);
        }

        // ===========================================================================================================
        // ===========================================================================================================

        /// <summary>
        /// X,CH1,D7-D0,D15-D8,Start,Increment,                       ATTENTION: This lines has two additional columns!
        /// Sequence,Volt,,,-1.144000e-03,2.000000e-06                ATTENTION: This lines has two additional columns!
        /// 0,4.00e-02,0x00,0x00,
        /// 1,-1.20e-01,0x00,0x00,
        /// 2,-1.20e-01,0x00,0x00,
        /// 
        /// The first column is a completely useless counter.
        /// </summary>
        private void LoadConfigRigol1000Z(List<String[]> i_Header)
        {
            mi_Config.ms32_HeaderLines = 2;
            mi_Config.ms32_AnalogRes   = 8;
            mi_Config.mb_Timestamps    = false;
            mi_Config.md_TimeIncrement = GetHeaderDouble(i_Header, 1, -1); // get value in last column (2.000000e-06)
            mi_Config.md_TimeFactor    = 1.0; // Increment has the unit seconds

            String[] s_Values = i_Header[2];
            for (int C=1; C<s_Values.Length; C++)
            {
                CsvChan i_Chan = new CsvChan();
                i_Chan.ms32_Column = C;
                i_Chan.ms_Name     = GetHeaderString(i_Header, 0, C);

                if (i_Chan.ms_Name.StartsWith("D") && i_Chan.ms_Name.Contains("-"))
                {
                    i_Chan.me_Type = eChanType.DigitalPodHex;
                    // The serie DS1000Z uses 0, 8 or 16 digital channels which are transmitted in one hex byte per 8 channels.
                    i_Chan.ms32_FirstDigital = i_Chan.ms_Name.Contains("D8") ? 8 : 0;
                }
                else
                {
                    i_Chan.me_Type = eChanType.Analog;
                    i_Chan.mf_Factor = GetHeaderUnitFactor(i_Header, 1, C); // "Volt" --> 1.0
                }
                mi_Config.mi_Channels.Add(i_Chan);
            }
        }

        /// <summary>
        /// Time,X(CH2),
        /// Second,Volt,
        /// -1.1999999e-03,-4.00000e-02,
        /// -1.1959999e-03,4.00000e-02,
        /// -1.1919999e-03,-0.00000e+00,
        /// </summary>
        private void LoadConfigRigol1000DE(List<String[]> i_Header)
        {
            mi_Config.ms32_HeaderLines = 2;
            mi_Config.ms32_AnalogRes   = 8;
            mi_Config.mb_Timestamps    = true;
            mi_Config.ms32_TimeCol     = 0;
            mi_Config.md_TimeFactor    = GetHeaderUnitFactor(i_Header, 1, 0); // "Second" --> 1.0

            String[] s_Values = i_Header[2];
            for (int C=1; C<s_Values.Length; C++)
            {
                CsvChan i_Chan = new CsvChan();
                i_Chan.ms32_Column = C;
                i_Chan.me_Type     = eChanType.Analog;
                i_Chan.ms_Name     = GetHeaderString(i_Header, 0, C);
                i_Chan.ms_Name     = ExtractBetween(i_Chan.ms_Name, '(', ')', 0);
                i_Chan.mf_Factor   = GetHeaderUnitFactor(i_Header, 1, C); // "Volt" --> 1.0
                mi_Config.mi_Channels.Add(i_Chan);
            }
        }

        /// <summary>
        /// OWON is the most stupid and the most sloppy company under the sun!
        /// Their Java software version 1.1.5 stores this format:
        /// 
        /// #,Time(ms),CH1(mV)
        /// 0,3.0000000,10320.00
        /// 1,2.9960000,10320.00
        /// 2,2.9920000,10240.00
        /// 
        /// The first column is a completely useless counter.
        /// Timestamps are decreasing!
        /// 
        /// --------------------------------------------------------------
        /// Their Java software version 1.1.7 stores this format:
        /// 
        /// Unit:(mV)
        /// ,CH1
        /// Frequency,866.567 Hz
        /// Period,1.154 ms
        /// 1,10480.00
        /// 2,10480.00
        /// 3,10480.00
        /// 
        /// It is not anymore possible to import the CSV file from the garbage version 1.1.7
        /// The timestamps are completely missing in the file.
        /// The values "Frequency" and "Period" are complete garbage.
        /// The above example is from a capture with 1 ms/div
        /// This file cannot be imported.
        /// Not even their CRAP application itself can load this file!
        /// Version 1.1.7 is unusable Chinese buggy crap.
        /// </summary>
        private void LoadConfigOwon1022(List<String[]> i_Header)
        {
            mi_Config.ms32_HeaderLines = 1;
            mi_Config.ms32_AnalogRes   = 8;
            mi_Config.mb_Timestamps    = true;
            mi_Config.ms32_TimeCol     = 1;

            String s_TimeHead = GetHeaderString(i_Header, 0, 1);
            String s_TimeUnit = ExtractBetween(s_TimeHead, '(', ')', 0);
            mi_Config.md_TimeFactor = GetUnitFactor(s_TimeUnit, 0); // "ms" --> 0.001

            String[] s_Values = i_Header[1];
            for (int C=2; C<s_Values.Length; C++)
            {
                String s_ChanHead = GetHeaderString(i_Header, 0, C);
                String s_ChanUnit = ExtractBetween (s_ChanHead, '(', ')', 0);

                CsvChan i_Chan = new CsvChan();
                i_Chan.ms32_Column = C;
                i_Chan.me_Type     = eChanType.Analog;
                i_Chan.ms_Name     = ExtractBefore(s_ChanHead, '(', 0);
                i_Chan.mf_Factor   = GetUnitFactor(s_ChanUnit, 0);  // "mV" --> 0.001
                mi_Config.mi_Channels.Add(i_Chan);
            }
        }

        /// <summary>
        /// Time;Channel A;Channel B
        /// (ms);(V);(V)
        /// 
        /// -0,00500800;12,12598419;11,49606323
        /// -0,00250400;12,12598419;11,49606323
        /// 0,00000000;12,12598419;11,49606323
        /// </summary>
        private void LoadConfigPicoscope3206(List<String[]> i_Header)
        {
            mi_Config.ms32_HeaderLines = 2;
            mi_Config.ms32_AnalogRes   = 8;
            mi_Config.mb_Timestamps    = true;
            mi_Config.ms32_TimeCol     = 0;

            String s_TimeHead = GetHeaderString(i_Header, 1, 0);
            String s_TimeUnit = ExtractBetween(s_TimeHead, '(', ')', 1);
            mi_Config.md_TimeFactor = GetUnitFactor(s_TimeUnit, 1); // "ms" --> 0.001

            String[] s_Values = i_Header[2];
            for (int C=1; C<s_Values.Length; C++)
            {
                CsvChan i_Chan = new CsvChan();
                i_Chan.ms32_Column = C;
                i_Chan.me_Type     = eChanType.Analog;
                i_Chan.ms_Name     = GetHeaderString(i_Header, 0, C);

                String s_ChanHead = GetHeaderString(i_Header, 1, C);
                String s_ChanUnit = ExtractBetween(s_ChanHead, '(', ')', 1);
                i_Chan.mf_Factor  = GetUnitFactor(s_ChanUnit, 1); // "V" --> 1.0
                mi_Config.mi_Channels.Add(i_Chan);
            }
        }

        // ===========================================================================================================
        // ===========================================================================================================

        /// <summary>
        /// Read a string value from the CSV header
        /// s32_Line and s32_Col are zero-based.
        /// s32_Col = -1 --> read last column, s32_Col = -2 --> last but one column
        /// </summary>
        private String GetHeaderString(List<String[]> i_Header, int s32_Line, int s32_Col)
        {
            String[] s_Parts = i_Header[s32_Line];
            if (s32_Col < 0)
                s32_Col = s_Parts.Length + s32_Col;

            if (s32_Col < 0 || s32_Col >= s_Parts.Length)
                throw new Exception("The CSV line "+(s32_Line+1)+" does not contain "+(s32_Col+1)+" columns." + ERR_MODEL);

            return s_Parts[s32_Col];
        }

        // -----------------------

        private double GetHeaderDouble(List<String[]> i_Header, int s32_Line, int s32_Col)
        {
            String s_Value = GetHeaderString(i_Header, s32_Line, s32_Col);
            return ParseDouble(s_Value, s32_Line);
        }

        private double ParseDouble(String s_Value, int s32_Line)
        {
            s_Value = s_Value.Trim().Replace(',', '.');
            double d_Value;
            if (!double.TryParse(s_Value, NumberStyles.Float, CultureInfo.InvariantCulture, out d_Value))
                throw new Exception("The CSV value '"+s_Value+"' in line "+(s32_Line+1)+" is not a valid floating point value." + ERR_MODEL);

            return d_Value;
        }

        private float ParseFloat(String s_Value, int s32_Line)
        {
            s_Value = s_Value.Trim().Replace(',', '.');
            float f_Value;
            if (!float.TryParse(s_Value, NumberStyles.Float, CultureInfo.InvariantCulture, out f_Value))
                throw new Exception("The CSV value '"+s_Value+"' in line "+(s32_Line+1)+" is not a valid floating point value." + ERR_MODEL);

            return f_Value;
        }

        private Byte ParseHexByte(String s_Hex, int s32_Line)
        {
            String s_Value = s_Hex.Trim();
            if (s_Value.StartsWith("0x"))
                s_Value = s_Value.Substring(2);

            Byte u8_Value;
            if (!Byte.TryParse(s_Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out u8_Value))
                throw new Exception("The value '"+s_Hex+"' in line "+(s32_Line+1)+" is not a valid hexadecimal byte." + ERR_MODEL);

            return u8_Value;
        }

        // -----------------------

        private float GetHeaderUnitFactor(List<String[]> i_Header, int s32_Line, int s32_Col)
        {
            String s_Unit = GetHeaderString(i_Header, s32_Line, s32_Col);
            return GetUnitFactor(s_Unit, s32_Line);
        }

        /// <summary>
        /// Factor is 1.0 if the standard physical units are used (seconds for time and Volt for channels)
        /// If a CSV file has values in millivolt or milliseconds the factor must be 0.001
        /// </summary>
        private float GetUnitFactor(String s_Unit, int s32_Line)
        {
            switch (s_Unit.Trim().ToLower())
            {
                case "v":      // Volt
                case "volt":
                case "s":      // seconds
                case "sec": 
                case "second": 
                case "seconds": 
                    return 1.0f;
                case "mv":
                case "ms":
                    return 0.001f;
                case "uv":
                case "us":
                case "µv":
                case "µs":
                    return 0.000001f;
                default:
                    throw new Exception("Unknown unit '"+s_Unit+"' in line "+(s32_Line+1)+" in CSV file." + ERR_MODEL);
            }
        }

        // -----------------------

        /// <summary>
        /// s_Value="X (CH)", c_Start='(', c_End=')' --> return "CH"
        /// </summary>
        private String ExtractBetween(String s_Value, Char c_Start, Char c_End, int s32_Line)
        {
            int s32_Start = s_Value.IndexOf(c_Start);
            int s32_End   = s_Value.IndexOf(c_End);
            if (s32_Start < 0 || s32_End < 0)
                throw new Exception("The CSV value '"+s_Value+"' does not contain '"+c_Start+"' and '"+c_End+"' in line "+(s32_Line+1)+"." + ERR_MODEL);

            return s_Value.Substring(++s32_Start, s32_End - s32_Start).Trim();
        }

        /// <summary>
        /// s_Value="CH1(mV)", c_End='(' --> return "CH1"
        /// </summary>
        private String ExtractBefore(String s_Value, Char c_End, int s32_Line)
        {
            int s32_End = s_Value.IndexOf(c_End);
            if (s32_End < 0)
                throw new Exception("The CSV value '"+s_Value+"' does not contain '"+c_End+"' in line "+(s32_Line+1)+"." + ERR_MODEL);

            return s_Value.Substring(0, s32_End).Trim();
        }
    }
}
