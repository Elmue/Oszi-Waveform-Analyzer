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
using System.Globalization;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Threading;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

using eOsziSerie        = Transfer.TransferManager.eOsziSerie;
using ScpiCombo         = Transfer.SCPI.ScpiCombo;
using eBinaryTCP        = Transfer.SCPI.eBinaryTCP;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Utils             = OsziWaveformAnalyzer.Utils;
using BigEndianReader   = ExImport.BigEndianReader;


namespace ExImport
{
    /// <summary>
    /// This class implements BIN, CAP and CSV file import for the OWON oscilloscopes VDS1022 and VDS2052.
    /// The format of the proprietary BIN and CAP files from OWON is completely undocumented.
    /// This class is based on reverse engineering the OWON Java software owon-vds-tiny-1.1.5-cf19.jar
    /// Saving  BIN files see: com.owon.uppersoft.dso.model.DataSaverTiny.java
    /// Loading BIN files see: com.owon.uppersoft.dso.model.DataImporterTiny.java
    /// R/W     CAP files see: com\owon\uppersoft\dso\function\record\RecordFileIO.java
    /// </summary>
    public class OWON
    {
        #region enums

        enum eMachineType
        {
            VDS1022 = 100, // and VDS1021
            VDS2052 = 102,
        }

        enum eFileType
        {
            BIN = 0x01000000,
            CAP = 0x03000000,
        }

        #endregion

        #region structs

        struct kHeader
        {
            public Byte[] mu8_Model;         // "SPBVDS1022" or "SPBVDS2052"
            public int    ms32_MachineType;  // enum eMachineType
            public int    ms32_FileVersion;  // (4) ignored by OWON software
            public int    ms32_FileType;     // enum eFileType
            public int    ms32_FileSize;     // size of BIN file

            public kHeader(BigEndianReader i_Reader)
            {
                mu8_Model        = i_Reader.ReadBytes(10);
                ms32_MachineType = i_Reader.ReadInt32();
                ms32_FileVersion = i_Reader.ReadInt32();
                ms32_FileType    = i_Reader.ReadInt32();
                ms32_FileSize    = i_Reader.ReadInt32();
            }
        }

        struct kSettings
        {
            public Byte   mu8_One;             // always 1      (totally useless)
            public int    ms32_Size;
            public Byte   mu8_Seven;           // always 77     (totally useless)
            public int    ms32_Thousand;       // always 1000   (totally useless)
            public int    ms32_AreaHeight;     // always 501    (totally useless)
            public int    ms32_TimeBase;       // stupid index into oszi-model-dependent Time / div lookup table
            public int    ms32_HorTriggerPos;
            public Byte   mu8_PeakDetect;      // bool (0 means no peak detect)

            public kSettings(BigEndianReader i_Reader)
            {
                mu8_One            = i_Reader.ReadByte();
                ms32_Size          = i_Reader.ReadInt32();
                Int64 s64_StartPos = i_Reader.Position;
                mu8_Seven          = i_Reader.ReadByte();
                ms32_Thousand      = i_Reader.ReadInt32();
                ms32_AreaHeight    = i_Reader.ReadInt32();
                ms32_TimeBase      = i_Reader.ReadInt32();
                ms32_HorTriggerPos = i_Reader.ReadInt32();
                mu8_PeakDetect     = i_Reader.ReadByte();

                // s64_Skip is zero in File Version 4. But in a future file version they may add more fields to this struct --> skip.
                Int64 s64_Skip = i_Reader.Position - s64_StartPos - ms32_Size;
                if (s64_Skip < 0) throw new Exception("The file is corrupt");
                i_Reader.Position += s64_Skip;
            }
        }

        struct kChanConf
        {
            public Byte[] mu8_Name;               // channel Name always 3 byte: "CH1", "CH2", "CH4"
            public int    ms32_Size;
            public int    ms32_Inverse;           // this is ignored by OWON software
            public int    ms32_InitPos;
            public int    ms32_ScreenDataLen;     // count of samples
            public int    ms32_PlugDataLength;    // this is ignored by OWON software
            public int    ms32_SinePlugRate;      // this is ignored by OWON software
            public float  mf_LinearPlugRate;      // this is ignored by OWON software
            public int    ms32_PluggedTrgOffset;  // a positive value shifts the signal to the left
            public int    ms32_DataLen;           // count of samples
            public int    ms32_SlowMove;          // This is ignored by OWON software
            public int    ms32_PosZero;           // position of zero marker
            public int    ms32_VbIdx;             // stupid index into oszi-model-dependent Volt / div lookup table
            public int    ms32_ProbeMultiIdx;     // stupid index into oszi-model-dependent probe multiplier lookup table
            public float  mf_Frequency;           // a totally wrong frequency detected by sloppy Chinese engineers
            public float  mf_Period;              // always = 1 / mf_Frequency  (totally useless value)
            public int    ms32_FilePointer;       // offset of analog sample data in the file

            public kChanConf(BigEndianReader i_Reader)
            {
                Int64 s64_StartPos    = i_Reader.Position;
                mu8_Name              = i_Reader.ReadBytes(3);
                ms32_Size             = i_Reader.ReadInt32();
                ms32_Inverse          = i_Reader.ReadInt32();      
                ms32_InitPos          = i_Reader.ReadInt32();
                ms32_ScreenDataLen    = i_Reader.ReadInt32();
                ms32_PlugDataLength   = i_Reader.ReadInt32();
                ms32_SinePlugRate     = i_Reader.ReadInt32();
                mf_LinearPlugRate     = i_Reader.ReadSingle();
                ms32_PluggedTrgOffset = i_Reader.ReadInt32();
                ms32_DataLen          = i_Reader.ReadInt32();
                ms32_SlowMove         = i_Reader.ReadInt32();
                ms32_PosZero          = i_Reader.ReadInt32();
                ms32_VbIdx            = i_Reader.ReadInt32();
                ms32_ProbeMultiIdx    = i_Reader.ReadInt32();
                mf_Frequency          = i_Reader.ReadSingle();
                mf_Period             = i_Reader.ReadSingle();
                ms32_FilePointer      = i_Reader.ReadInt32();

                // s64_Skip is zero in File Version 4. But in a future file version they may add more fields to this struct --> skip.
                Int64 s64_Skip = i_Reader.Position - s64_StartPos - ms32_Size;
                if (s64_Skip < 0) throw new Exception("The file is corrupt");
                i_Reader.Position += s64_Skip;
            }
        }

        struct kFrameHead
        {
            public int    ms32_Size;           // 8103
            public int    ms32_TimeBase;       // stupid index into oszi-model-dependent Time / div lookup table
            public int    ms32_HorTriggerPos;
            public Byte   mu8_PeakDetect;      // bool (0 means no peak detect)
            public int    ms32_LengthDM;       // 5000
            // --------------------------------
            public Int64  ms64_EndPos;

            public kFrameHead(BigEndianReader i_Reader, int s32_FileVersion)
            {
                ms32_Size          = i_Reader.ReadInt32();
                ms64_EndPos        = i_Reader.Position + ms32_Size;
                ms32_TimeBase      = i_Reader.ReadInt32();
                ms32_HorTriggerPos = i_Reader.ReadInt32();
                
                if (s32_FileVersion >= 3) mu8_PeakDetect = i_Reader.ReadByte();
                else                      mu8_PeakDetect = 0;
                if (s32_FileVersion >= 4) ms32_LengthDM  = i_Reader.ReadInt32();
                else                      ms32_LengthDM  = 0;
            }

            public void CheckEndPosition(Stream i_Stream)
            {
                // s64_Skip is zero in File Version 4. But in a future file version they may add more fields to this struct --> skip.
                Int64 s64_Skip = i_Stream.Position - ms64_EndPos;
                if (s64_Skip < 0) throw new Exception("The file is corrupt");
                i_Stream.Position += s64_Skip;
            }
        }

        struct kFrameChannel
        {
            public Byte   mu8_Channel;          // 0 = CH1, 1 = CH2
            public int    ms32_FrameSize;       // 4040
            public int    ms32_Inverse;
            public int    ms32_InitPos;
            public int    ms32_ScreenDataLen;   // 4000
            public int    ms32_DataLen;         // 4000
            public int    ms32_SlowMove; 
            public int    ms32_PosZero;         // position of zero marker
            public int    ms32_VbIdx;           // stupid index into oszi-model-dependent Volt / div lookup table
            public int    ms32_ProbeMultiIdx;   // stupid index into oszi-model-dependent probe multiplier lookup table
            public float  mf_Frequency;         // useless garbage
            public float  mf_Period;            // useless garbage
            // -------------------------------
            private Int64 ms64_StartPos;

            public kFrameChannel(BigEndianReader i_Reader, int s32_FileVersion)
            {
                mu8_Channel        = i_Reader.ReadByte();
                ms32_FrameSize     = i_Reader.ReadInt32();
                ms64_StartPos      = i_Reader.Position;

                if (s32_FileVersion >= 1) ms32_Inverse = i_Reader.ReadInt32();
                else                      ms32_Inverse = 0;

                ms32_InitPos       = i_Reader.ReadInt32();
                ms32_ScreenDataLen = i_Reader.ReadInt32();
                ms32_DataLen       = i_Reader.ReadInt32();
                ms32_SlowMove      = i_Reader.ReadInt32();
                ms32_PosZero       = i_Reader.ReadInt32();
                ms32_VbIdx         = i_Reader.ReadInt32();
                ms32_ProbeMultiIdx = i_Reader.ReadInt32();
                mf_Frequency       = i_Reader.ReadSingle();
                mf_Period          = i_Reader.ReadSingle();
            }

            public void CheckEndPosition(Stream i_Stream)
            {
                // s64_Skip is zero in File Version 4. But in a future file version they may add more fields to this struct --> skip.
                Int64 s64_Skip = i_Stream.Position - ms64_StartPos - ms32_FrameSize;
                if (s64_Skip < 0) throw new Exception("The file is corrupt");
                i_Stream.Position += s64_Skip;
            }
        }

        #endregion

        #region constants

        // The application has a raster of 20 x 10 squares
        const int HORIZ_DIVS = 20;
        const int VERT_DIVS  = 10;

        // mi_TimeBase stores in pico seconds
        const Int64 PICO  = 1;
        const Int64 NANO  = PICO  * 1000;
        const Int64 MICRO = NANO  * 1000;
        const Int64 MILLI = MICRO * 1000;
        const Int64 SECND = MILLI * 1000;

        // See: com\owon\uppersoft\dso\model\machine\params\VDS1022ONE.txt
        // See: com\owon\uppersoft\dso\model\machine\params\VDS2052ONE.txt

        // Instead of storing the time in the BIN file, the STUPID Chinese store an oscilloscope-model-dependent index
        static Int64[] ms64_TimeBase = new Int64[]
        {
            5   * NANO,  // 5ns / div
            10  * NANO,  // 10ns / div
            20  * NANO,  // 20ns / div
            50  * NANO,  // 50ns / div
            100 * NANO,  // 100ns / div
            200 * NANO,  // 200ns / div
            500 * NANO,  // 500ns / div
            1   * MICRO, // 1us / div
            2   * MICRO, // 2us / div
            5   * MICRO, // 5us / div
            10  * MICRO, // 10us / div
            20  * MICRO, // 20us / div
            50  * MICRO, // 50us / div
            100 * MICRO, // 100us / div
            200 * MICRO, // 200us / div
            500 * MICRO, // 500us / div
            1   * MILLI, // 1ms / div
            2   * MILLI, // 2ms / div
            5   * MILLI, // 5ms / div
            10  * MILLI, // 10ms / div
            20  * MILLI, // 20ms / div
            50  * MILLI, // 50ms / div
            100 * MILLI, // 100ms / div
            200 * MILLI, // 200ms / div
            500 * MILLI, // 500ms / div
            1   * SECND, // 1s / div
            2   * SECND, // 2s / div
            5   * SECND, // 5s / div
            10  * SECND, // 10s / div
            20  * SECND, // 20s / div
            50  * SECND, // 50s / div
            100 * SECND, // 100s / div
        };

        // Instead of storing the voltage in the BIN file, the STUPID Chinese store an oscilloscope-model-dependent index
        static float[] mf_VoltBase = new float[]
        {
            0.005f, // 5 mV / div
            0.010f, // 10 mV / div
            0.020f, // 20 mV / div
            0.050f, // 50 mV / div
            0.100f, // 100 mV / div
            0.200f, // 200 mV / div
            0.500f, // 500 mV / div
            1,      // 1 V / div
            2,      // 2 V / div
            5,      // 5 V / div
        };

        // Instead of storing the probe multiplier in the BIN file, the STUPID Chinese store an oscilloscope-model-dependent index
        static int[] ms32_ProbeMulti_1022 = new int[]
        {
            1, 10, 20, 50, 100, 500, 1000
        };

        static int[] ms32_ProbeMulti_2052 = new int[]
        {
            1, 10, 20, 100, 1000
        };

        #endregion

        public static Capture ParseBinaryFile(String s_Path, ref bool b_Abort)
        {
            using (FileStream i_Stream = new FileStream(s_Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // The stupid Chinese store all the data as big endian!
                BigEndianReader i_Reader = new BigEndianReader(i_Stream);

                kHeader k_Header = new kHeader(i_Reader);

                Debug.Assert(k_Header.ms32_FileVersion <= 4, "This class has not yet been tested with file version " + k_Header.ms32_FileVersion);

                // "SPBVDS1022"
                String s_Model = Encoding.ASCII.GetString(k_Header.mu8_Model);
                if (!s_Model.StartsWith("SPBVDS"))
                    throw new Exception("The file is not a valid OWON oscilloscope capture.");

                if (k_Header.ms32_FileSize != i_Stream.Length)
                    throw new Exception("The file is corrupt.");

                int[] s32_ProbeMultiply;
                switch ((eMachineType)k_Header.ms32_MachineType)
                {
                    case eMachineType.VDS1022: s32_ProbeMultiply = ms32_ProbeMulti_1022; break;
                    case eMachineType.VDS2052: s32_ProbeMultiply = ms32_ProbeMulti_2052; break;
                    default: throw new Exception("The oscilloscope model is not implemented.");
                }

                switch ((eFileType)k_Header.ms32_FileType)
                {
                    case eFileType.BIN: return ParseBIN(i_Reader, s32_ProbeMultiply, ref b_Abort);
                    case eFileType.CAP: return ParseCAP(i_Reader, s32_ProbeMultiply, k_Header.ms32_FileVersion, ref b_Abort);
                    default: throw new Exception("The file type is unknown.");
                }
            }
        }

        // ======================================================================================

        /// <summary>
        /// Parse BIN files with 5000 samples
        /// The oscilloscope has a ridiculous buffer of 5 KILO Byte / channel!
        /// </summary>
        private static Capture ParseBIN(BigEndianReader i_Reader, int[] s32_ProbeMultiply, ref bool b_Abort)
        {
            FileStream i_Stream = (FileStream)i_Reader.mi_Stream;

            Byte u8_Zero        = i_Reader.ReadByte();  // always zero  (totally useless)
            int  s32_WfmLength  = i_Reader.ReadInt32(); // length of Waveform data for all channels

            // Skip the Waveform data for all channels.
            // Stupidly kSettings and kChanConf are stored behind the waveform data.
            i_Stream.Position += s32_WfmLength;

            kSettings k_Settings = new kSettings(i_Reader);

            Int64 s64_TimeBase = ms64_TimeBase[k_Settings.ms32_TimeBase]; // Time / div
            Int64 s64_Duration = s64_TimeBase * HORIZ_DIVS;

            // Length of the following kChannel structures = sizeof(kChanConf) x ChannelCount
            int s32_AllChanLen = i_Reader.ReadInt32(); 

            // The Chinese do not store the count of channels.
            // Instead they check if 44 bytes are remaining !
            List<kChanConf> i_ChanConfigs = new List<kChanConf>();
            while (i_Stream.Length - i_Stream.Position > 44)
            {
                i_ChanConfigs.Add(new kChanConf(i_Reader));               
            }

            if (i_ChanConfigs.Count == 0)
                throw new Exception("The file does not contain channel data.");

            Capture i_Capture = new Capture();
            i_Capture.ms_Path         = i_Stream.Name;
            i_Capture.ms32_AnalogRes  = 8;
            i_Capture.ms32_Samples    = i_ChanConfigs[0].ms32_ScreenDataLen;
            i_Capture.ms64_SampleDist = s64_Duration / i_Capture.ms32_Samples;

            foreach (kChanConf k_ChanConf in i_ChanConfigs)
            {
                if (k_ChanConf.ms32_ScreenDataLen != i_Capture.ms32_Samples)
                    throw new Exception("The channels do not have the same sample count.");

                String s_ChanName = Encoding.ASCII.GetString(k_ChanConf.mu8_Name);
                int    s32_Probe  = s32_ProbeMultiply[k_ChanConf.ms32_ProbeMultiIdx];
                float  f_VoltBase = mf_VoltBase[k_ChanConf.ms32_VbIdx] * s32_Probe; // Volt / div
                float  f_Multiply = f_VoltBase * VERT_DIVS / 250;
                float  f_Offset   = k_ChanConf.ms32_PosZero * f_Multiply;

                Channel i_Channel   = new Channel(s_ChanName);
                i_Channel.mf_Analog = new float[i_Capture.ms32_Samples];

                // Set the stream to the first byte of the sample data.
                i_Stream.Position = k_ChanConf.ms32_FilePointer;

                for (int B=0; B<k_ChanConf.ms32_DataLen; B++)
                {
                    i_Channel.mf_Analog[B] = (SByte)i_Reader.ReadByte() * f_Multiply - f_Offset;
                }
                i_Capture.mi_Channels.Add(i_Channel);

                if (b_Abort)
                    return null;
            }
            return i_Capture;
        }

        // ======================================================================================

        /// <summary>
        /// Parse CAP files which contain multiple capture frames of 4000 samples each.
        /// This does not work work for high sample rates and there are gaps between the frames due to USB transfer delays.
        /// </summary>
        private static Capture ParseCAP(BigEndianReader i_Reader, int[] s32_ProbeMultiply, int s32_FileVersion, ref bool b_Abort)
        {
            FileStream i_Stream = (FileStream)i_Reader.mi_Stream;

            int s32_TimeGap    = i_Reader.ReadInt32();
            int s32_FrameCount = i_Reader.ReadInt32();

            List<List<float>> i_Analog = new List<List<float>>();
            List<int> i_Separators = new List<int>();

            Int64 s64_SampleDist = -1;
            for (int H=0; H<s32_FrameCount; H++)
            {
                kFrameHead k_Head = new kFrameHead(i_Reader, s32_FileVersion);

                // This loop runs over each channel.
                // The stupid Chinese do not store how many channels are in the file.
                while (i_Stream.Position < k_Head.ms64_EndPos)
                {
                    kFrameChannel k_Frame = new kFrameChannel(i_Reader, s32_FileVersion);

                    List<float> i_Volt;
                    if (k_Frame.mu8_Channel < i_Analog.Count)
                    {
                        i_Volt = i_Analog[k_Frame.mu8_Channel];
                    }
                    else
                    {
                        i_Volt = new List<float>();
                        i_Analog.Add(i_Volt);
                    }

                    int   s32_Probe  = s32_ProbeMultiply[k_Frame.ms32_ProbeMultiIdx];
                    float f_VoltBase = mf_VoltBase[k_Frame.ms32_VbIdx] * s32_Probe; // Volt / div
                    float f_Multiply = f_VoltBase * VERT_DIVS / 250;
                    float f_Offset   = k_Frame.ms32_PosZero * f_Multiply;

                    for (int B=0; B<k_Frame.ms32_DataLen; B++)
                    {
                        i_Volt.Add((SByte)i_Reader.ReadByte() * f_Multiply - f_Offset);
                    }

                    if (s64_SampleDist < 0) // calculate only once
                    {
                        Int64 s64_TimeBase = ms64_TimeBase[k_Head.ms32_TimeBase]; // Time / div
                        Int64 s64_Duration = s64_TimeBase * HORIZ_DIVS;
                        s64_SampleDist     = s64_Duration / k_Frame.ms32_DataLen;
                    }

                    if (k_Frame.mu8_Channel == 0)
                    {
                        // Store the sample positions where to draw a red vertical separator
                        i_Separators.Add(i_Volt.Count);
                    }

                    k_Frame.CheckEndPosition(i_Stream); // throws
                }

                k_Head.CheckEndPosition(i_Stream);

                if (b_Abort)
                    return null;
            }

            Capture i_Capture = new Capture();
            i_Capture.ms_Path         = i_Stream.Name;
            i_Capture.ms32_AnalogRes  = 8;
            i_Capture.ms32_Samples    = i_Analog[0].Count;
            i_Capture.ms64_SampleDist = s64_SampleDist;
            i_Capture.ms32_Separators = i_Separators.ToArray();

            for (int C=0; C<i_Analog.Count; C++)
            {
                List<float> i_Volt = i_Analog[C];

                Channel i_Channel   = new Channel("CH" + (C+1));
                i_Channel.mf_Analog = i_Volt.ToArray();
                i_Capture.mi_Channels.Add(i_Channel);
            }
            return i_Capture;
        }
    }
}
