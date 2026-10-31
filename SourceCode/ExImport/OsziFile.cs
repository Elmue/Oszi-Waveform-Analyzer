/*
------------------------------------------------------------
Oscilloscope Waveform Analyzer by ElmüSoft (www.netcult.ch/elmue)
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
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

using Utils             = OsziWaveformAnalyzer.Utils;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using eComboImg         = OsziWaveformAnalyzer.ComboBoxEx.eComboImg;

namespace ExImport
{
    /*
    ================================================================================================================================
    Other than the completely inconsitent and mostly undocumented WFM format, the OSZI format is very simple and open source.
    WFM files store a lot of irrelevant stuff, like trigger mode, AC/DC coupling, probe ratio, and many other oscilloscope settings.
    OSZI files store only the analog and digital signals and apply the highest possible compression to the data.
    Additionally the plain OSZI data can be written into a ZIP stream which reduces the file size even more.
          
    File header:
    ------------------------------------------------------
    ZIP  Magic        : UInt32  [4 Byte] 0xA53D9FBC  (optional, only if the following data is ZIP compressed)
    File Magic        : UInt32  [4 Byte] 0x6D801FA9
    Oszi File Version : integer [4 Byte] currently only version 1 and 2 exist, allows future expansions
    Sample Count      : integer [4 Byte] the count of samples in the file
    Sample Rate       : Int64   [8 Byte] the samplerate in pico seconds
    Analog Resolution : integer [4 Byte] the A/D resolution of the oscilloscope in bits (e.g. 8, 10, 12 bit)
    Analog Channels   : integer [4 Byte] the count of analog channels in the file
    Digital Channels  : integer [4 Byte] the count of digital channels in the file
    Channel Names     : string  [Length prefixed string] A string that contains all channel names separated by linefeeds.
    Channel Order     : byte[]  [1 byte/channel] defines the original channel order. As digital channels are compressed together
                                                 the original channel order of the user must be saved and later restored.

    Analog Channel Data:     Sample Voltage = Binary Data * Voltage Factor + Voltage Offset
    ------------------------------------------------------
    Analog channel magic   : UInt32 [4 Byte] 0xDA0D153F
    Voltage Offet          : float  [4 Byte] the lowest voltage of all samples
    Voltage Factor         : float  [4 Byte] the factor to multiply the binary data
    Binary analog samples  : 8 bit or 16 bit binary analog data


    Digital Channel Data with Mask compression:
    ------------------------------------------------------
    Mask channels magic    : UInt32  [4 Byte] 0x92F475E0
    Binary digital samples : This format compresses multiple channels together:
                             From each sample in a digital channel one bit is written to a byte on disk until the byte is full.
                             1 digital channel  --> write 8 x 1 sample  into 1 byte,
                             4 digital channels --> write 2 x 4 samples into 1 byte,
                             8 digital channels --> write 1 x 8 samples into 1 byte, 
                             3 digital channels --> write 8 x 3 samples into 3 bytes,  etc ...


    Digital Channel Data with Run Length Encoding compression:
    ------------------------------------------------------
    RLE channel magic      : UInt32 [4 Byte] 0x79B50A22
    Binary digital samples : This format compresses one channel at once.
                             It stores the count of samples until the digital state changes from 0/1 or 1/0 into one "length byte".
                             The highest bit in the length byte defines if more length bytes follow.
                             Up to 4 length bytes can be concatenated allowing length values up to 127 x 127 x 127 x 256 samples.
                             Length 1...127 --> one length byte, Length 128 ... 127 ^ 2 --> two length bytes, etc..

    File termination:
    ------------------------------------------------------
    File Magic        : UInt32 [4 Byte] 0x6D801FA9
    File CRC          : UInt32 [4 Byte] CRC32 of the entire file.
    ================================================================================================================================
    */        
    public class OsziFile
    {   
        // In OSZI_FILE_VERSION 2 saving and loading Capture.ms32_Separators has been added

        const UInt32 OSZI_FILE_VERSION = 2;
        const UInt32 OSZI_PLAIN_MAGIC  = 0x6D801FA9; // first 32 bit in a plain OSZI file
        const UInt32 OSZI_ZIP_MAGIC    = 0xA53D9FBC; // first 32 bit in a zipped OSZI file
        const UInt32 OSZI_ANALOG_CHAN  = 0xDA0D153F;
        const UInt32 OSZI_DIGITAL_RLE  = 0x79B50A22;
        const UInt32 OSZI_DIGITAL_MASK = 0x92F475E0;

        // It does not make sense to store more than 16 bits in an OSZI file.
        // The vertical resolution of the monitor cannot even display 16 bits (65536 steps)
        // Storing more than 16 bits would only produce unnecessary huge files.
        const int MIN_ANAL_RES =  7; // Minimum analog resolution = 128 steps
        const int MAX_ANAL_RES = 16; // Maximum analog resolution = 65536 steps

        static int ms32_StatusTick; // for progress display in statusbar

        /// <summary>
        /// b_ZIP = true      --> write a ZIPed file (smaller file, but read/write is slower)
        /// s32_SaveSteps = 5 --> save every fifth sample in i_Capture and skip the others (user wants to reduce file size).
        /// </summary>
        public static String Save(Capture i_Capture, String s_Path, bool b_ZIP, int s32_SaveSteps, bool b_Shrink8Bit)
        {
            if (!Utils.CheckChannelNames())
                return null;

            int s32_SavedSamples = 0;
            using (FileStream i_FileStream = new FileStream(s_Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            {
                if (b_ZIP)
                {
                    BinaryWriter i_MagicWriter = new BinaryWriter(i_FileStream);
                    i_MagicWriter.Write(OSZI_ZIP_MAGIC);
                    i_MagicWriter.Flush();

                    using (GZipStream i_ZipStream = new GZipStream(i_FileStream, CompressionMode.Compress))
                    {
                        s32_SavedSamples = SaveToStream(i_Capture, i_ZipStream, s32_SaveSteps, b_Shrink8Bit);
                    }
                }
                else // plain OSZI file
                {
                    s32_SavedSamples = SaveToStream(i_Capture, i_FileStream, s32_SaveSteps, b_Shrink8Bit);
                }
            }

            i_Capture.ms_Path  = s_Path;
            i_Capture.mb_Dirty = false;
            Utils.FormMain.UpdateDirty(); // display status "unsaved changes"

            return s32_SavedSamples.ToString("N0") + " samples"; // for status display
        }

        static int SaveToStream(Capture i_Capture, Stream i_Stream, int s32_SaveSteps, bool b_Shrink8Bit)
        {
            Debug.Assert(i_Capture.ms32_AnalogRes >= MIN_ANAL_RES, 
                         "Programming Error: ms32_AnalogRes must ALWAYS have a valid value (even if only digital channels are saved)");

            Debug.Assert(i_Capture.ms32_AnalogRes <= 24, 
                         "Programming Error: A Capture cannot have more than 24 bit resolution which is the maximum of a float variable (Channel.mf_Analog)");

            List<Channel> i_Analog  = new List<Channel>();
            List<Channel> i_Digital = new List<Channel>();
            foreach (Channel i_Chan in i_Capture.mi_Channels)
            {
                if (i_Chan.mf_Analog != null) 
                    i_Analog.Add(i_Chan);

                if (i_Chan.mu8_Digital != null && !i_Chan.IsDigiEmpty()) 
                    i_Digital.Add(i_Chan); 
            }

            int   s32_SaveSamples = i_Capture.ms32_Samples    / s32_SaveSteps;
            Int64 s64_Samplerate  = i_Capture.ms64_SampleDist * s32_SaveSteps;

            using (CrcWriter i_Writer = new CrcWriter(i_Stream))
            {
                // ---------------- Write Header -----------------

                int s32_AnalogRes = Math.Min(i_Capture.ms32_AnalogRes, MAX_ANAL_RES);
                if (b_Shrink8Bit) s32_AnalogRes = Math.Min(s32_AnalogRes, 8);

                i_Writer.Write(OSZI_PLAIN_MAGIC);
                i_Writer.Write(OSZI_FILE_VERSION);
                i_Writer.Write(s32_SaveSamples);
                i_Writer.Write(s64_Samplerate); // in picoseconds
                i_Writer.Write(s32_AnalogRes);
                i_Writer.Write(i_Analog .Count);
                i_Writer.Write(i_Digital.Count);

                // ------------ Write Channel Names ------------

                String s_Names = "";
                foreach (Channel i_Channel in i_Analog)
                {
                    s_Names += i_Channel.ms_Name + "\n";
                }
                foreach (Channel i_Channel in i_Digital)
                {
                    s_Names += i_Channel.ms_Name + "\n";
                }
                i_Writer.Write(s_Names.Substring(0, s_Names.Length -1)); // cut last '\n'

                // ------------- Write Channel Order -------------

                // Digital channels are compressed together in Mask mode.
                // Therefore the channel order of the user must be stored in the file or it will get lost.
                foreach (Channel i_Anal in i_Analog)
                {
                    Byte u8_Order = (Byte)i_Capture.mi_Channels.IndexOf(i_Anal); 
                    i_Writer.Write(u8_Order);
                }
                foreach (Channel i_Digi in i_Digital)
                {
                    Byte u8_Order = (Byte)i_Capture.mi_Channels.IndexOf(i_Digi);
                    i_Writer.Write(u8_Order);
                }

                // ------ Separators (added in version 2) --------

                i_Writer.Write(i_Capture.ms32_Separators.Length);
                foreach (int s32_Sep in i_Capture.ms32_Separators)
                {
                    i_Writer.Write(s32_Sep);
                }

                // ------------ Write Analog Channels ------------

                // ALWAYS write all analog channels FIRST!
                foreach (Channel i_Channel in i_Analog)
                {
                    WriteAnalog(i_Channel, i_Writer, s32_AnalogRes, s32_SaveSamples, s32_SaveSteps);
                }

                // ------------ Write Digital Channels ------------

                if (i_Digital.Count > 0)
                {
                    // Select the method that results in the smallest file size.
                    // This depends on the count of channels and how often the digital status changes.
                    // For data with very long pauses at a high resolution the RLE encoding results in a significantly smaller file size.
                    // I saved a CAN bus capture with 500.000 samples into a 1,3 kB OSZI file.
                    // The resulting data length of mask encoding can be calculated beforehand. It is used as a limit for RLE encoding.
                    // If the RLE stream becomes longer than this limit, RLE encoding is aborted -> return null.

                    // Calculate the bytes needed to store all digital channels when Mask encoding is used.
                    int s32_MaskLen = s32_SaveSamples * i_Digital.Count / 8;

                    Byte[] u8_RLE = WriteDigitalRLE(i_Digital, s32_SaveSamples, s32_MaskLen, s32_SaveSteps);
                    if (u8_RLE != null)
                    {
                        // RLE encoding is more efficient than Mask encoding
                        i_Writer.Write(u8_RLE);
                    }
                    else
                    {
                        // Mask encoding is more efficient than RLE encoding
                        WriteDigitalMask(i_Digital, i_Writer, s32_SaveSamples, s32_SaveSteps);
                    }
                }
               
                i_Writer.Write(OSZI_PLAIN_MAGIC); // write the last termination check
                i_Writer.WriteCRC(); 
            }
            return s32_SaveSamples;
        }

        // -----------------------------------------------------------

        public static bool IsInputFile(String s_Extension, ref eComboImg e_Image)
        {
            switch (s_Extension)
            {
                case ".oszi":
                    e_Image = eComboImg.OSZI;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// See comment for SaveOsziFile()
        /// </summary>
        public static Capture LoadFile(String s_Path, ref bool b_Abort)
        {
            using (FileStream i_FileStream = new FileStream(s_Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                BinaryReader i_MagicReader = new BinaryReader(i_FileStream);
                switch (i_MagicReader.ReadUInt32())
                {
                    case OSZI_PLAIN_MAGIC:
                        i_FileStream.Position = 0;
                        return LoadFromStream(i_FileStream, s_Path, ref b_Abort);

                    case OSZI_ZIP_MAGIC:
                        using (GZipStream i_ZipStream = new GZipStream(i_FileStream, CompressionMode.Decompress))
                        {
                            return LoadFromStream(i_ZipStream, s_Path, ref b_Abort);
                        }

                    default:
                        throw new Exception("The OSZI file has an invalid magic");
                }
            }
        }

        static Capture LoadFromStream(Stream i_Stream, String s_Path, ref bool b_Abort)
        {
            using (CrcReader i_Reader = new CrcReader(i_Stream))
            {
                // ---------- Read Header ----------

                UInt32 u32_Magic     = i_Reader.ReadUInt32();
                UInt32 u32_Version   = i_Reader.ReadUInt32();
                int s32_Samples      = i_Reader.ReadInt32();
                Int64 s64_Samplerate = i_Reader.ReadInt64();
                int s32_AnalogRes    = i_Reader.ReadInt32();
                int s32_AnalChannels = i_Reader.ReadInt32();
                int s32_DigiChannels = i_Reader.ReadInt32();
                int s32_TotChannels  = s32_AnalChannels + s32_DigiChannels;
                String s_Names       = i_Reader.ReadString();
                Byte[] u8_Order      = i_Reader.ReadBytes(s32_TotChannels);

                if (u32_Magic != OSZI_PLAIN_MAGIC ||
                    s32_Samples      <  1  || 
                    s64_Samplerate   <  1  || 
                    s32_AnalChannels <  0  || 
                    s32_DigiChannels <  0  || 
                    s32_TotChannels ==  0  || 
                    s32_TotChannels > 255  || 
                    s32_AnalogRes   < MIN_ANAL_RES ||
                    s32_AnalogRes   > MAX_ANAL_RES)
                    throw new Exception("The OSZI file is corrupt"); 

                foreach (Byte u8_Index in u8_Order)
                {
                    if (u8_Index >= s32_TotChannels)
                        throw new Exception("The OSZI file has invalid order information"); 
                }

                if (u32_Version > OSZI_FILE_VERSION)
                    throw new Exception("Please install the latest version of Oszi Waveform Analyzer to read this file.");

                String[] s_SplitNames = s_Names.Split('\n');
                if (s_SplitNames.Length != s32_TotChannels)
                    throw new Exception("The OSZI file has invalid channel names"); 

                // ------ Separators (added in version 2) --------

                List<int> i_Separators = new List<int>();
                if (u32_Version >= 2)
                {
                    int s32_Count = i_Reader.ReadInt32();
                    for (int i=0; i<s32_Count; i++)
                    {
                        i_Separators.Add(i_Reader.ReadInt32());
                    }
                }
              
                // ---------- Read Channels ----------

                List<Channel> i_NewChannels = new List<Channel>();
                while (i_NewChannels.Count < s32_TotChannels)
                {
                    UInt32 u32_ID = i_Reader.ReadUInt32();
                    switch (u32_ID)
                    {
                        case OSZI_ANALOG_CHAN: // read one analog channel
                            ReadAnalog(i_NewChannels, i_Reader, s32_Samples, s_SplitNames, s32_AnalogRes, ref b_Abort);
                            break;
                        case OSZI_DIGITAL_MASK: // read all digital channels
                            ReadDigitalMask(i_NewChannels, i_Reader, s32_Samples, s_SplitNames, s32_DigiChannels, ref b_Abort);
                            break;
                        case OSZI_DIGITAL_RLE: // read one digital channel
                            ReadDigitalRLE(i_NewChannels, i_Reader, s32_Samples, s_SplitNames, ref b_Abort);
                            break;
                        default:
                            throw new Exception("The OSZI file has invalid channel IDs"); 
                    }

                    if (b_Abort)
                        return null;
                }

                if (i_Reader.ReadUInt32() != OSZI_PLAIN_MAGIC)
                    throw new Exception("The OSZI file has an invalid termination"); 

                if (!i_Reader.CheckCRC())
                    throw new Exception("The OSZI file has an invalid CRC.");

                // ----------- Restore original channel order ----------

                Channel[] i_ChanOrder = new Channel[s32_TotChannels];
                for (int Src=0; Src<s32_TotChannels; Src++)
                {
                    int Dst = u8_Order[Src];
                    if (i_ChanOrder[Dst] == null)
                    {
                        // Add an analog channel or a pure digital channel
                        i_ChanOrder[Dst] = i_NewChannels[Src];
                    }
                    else 
                    {
                        // Add a digital channel to an already stored analog channel
                        Debug.Assert(i_ChanOrder[Dst].ms_Name == i_NewChannels[Src].ms_Name, "Something is wrong here!");
                        i_ChanOrder[Dst].mu8_Digital = i_NewChannels[Src].mu8_Digital;
                    }
                }

                Capture i_Capture = new Capture();
                i_Capture.ms_Path         = s_Path;
                i_Capture.ms32_Samples    = s32_Samples;
                i_Capture.ms64_SampleDist = s64_Samplerate;
                i_Capture.ms32_AnalogRes  = s32_AnalogRes;
                i_Capture.ms32_Separators = i_Separators.ToArray();

                foreach (Channel i_Channel in i_ChanOrder)
                {
                    if (i_Channel != null)
                        i_Capture.mi_Channels.Add(i_Channel);
                }

                // Append 16 digital channels with random data for testing.
                /*
                List<Channel> i_Random = CreateDigiRandomChannels(16, s32_Samples);
                i_Capture.mi_Channels.AddRange(i_Random);
                */ 
                return i_Capture;
            }
        }

        // ========================================= ANALOG ================================================

        /// <summary>
        /// Depending on e_AnalRes the float values for voltages are store as one byte or 2 bytes
        /// </summary>
        static void WriteAnalog(Channel i_Channel, CrcWriter i_Writer, int s32_AnalogRes, int s32_SaveSamples, int s32_SaveSteps)
        {
            int s32_AnalMax = (1 << s32_AnalogRes) - 1; // 8 --> 255, 16 --> 65535

            float f_Factor = (float)s32_AnalMax / (i_Channel.mf_Max - i_Channel.mf_Min);

            i_Writer.Write(OSZI_ANALOG_CHAN); // analog channel marker 
            i_Writer.Write(i_Channel.mf_Min); // minimum voltage (float)
            i_Writer.Write(f_Factor);         // voltage factor  (float)

            // If 10 bits would be written as 2 bytes there would be 6 wasted bits.
            // The BitShifter allows to write only 10 bits stuffing them until the bytes are full.
            BitShifter i_Shifter = new BitShifter(s32_AnalogRes);
            int S = 0;
            for (int W=0; W<s32_SaveSamples; W++)
            {
                int s32_Binary = (int)((i_Channel.mf_Analog[S] - i_Channel.mf_Min) * f_Factor + 0.5f);
                Debug.Assert(s32_Binary >= 0 && s32_Binary <= s32_AnalMax, "Internal Analog Calc Error");

                i_Writer.Write(s32_Binary, i_Shifter);

                if (Math.Abs(Environment.TickCount - ms32_StatusTick) > 330)
                {
                    Utils.FormMain.PrintStatus("Writing Channel " + i_Channel.ms_Name + ", Sample " + W.ToString("N0") + ". Please wait.", Color.Black);
                    ms32_StatusTick = Environment.TickCount;
                }

                S += s32_SaveSteps;
            }

            i_Shifter.mb_Flush = true;
            i_Writer.Write(0, i_Shifter); // flush the remaining bits in the Shifter (if any)
        }

        /// <summary>
        /// OSZI_ANALOG_CHAN has already been removed from the stream.
        /// Adds a new channel to the Capture
        /// </summary>
        static void ReadAnalog(List<Channel> i_NewChannels, CrcReader i_Reader, int s32_Samples, String[] s_SplitNames, int s32_AnalogRes, ref bool b_Abort)
        {
            String  s_Name = s_SplitNames[i_NewChannels.Count];
            float[] f_Data = new float[s32_Samples];

            float f_Min    = i_Reader.ReadSingle();
            float f_Factor = i_Reader.ReadSingle();

            // If 10 bits would be written as 2 bytes there would be 6 wasted bits.
            // The BitShifter allows to write only 10 bits stuffing them until the bytes are full.
            BitShifter i_Shifter = new BitShifter(s32_AnalogRes);
            for (int S=0; S<s32_Samples; S++)
            {
                int s32_Binary = i_Reader.Read(i_Shifter);
                f_Data[S] = f_Min + s32_Binary / f_Factor;

                if (Math.Abs(Environment.TickCount - ms32_StatusTick) > 330)
                {
                    Utils.FormMain.PrintStatus("Reading Channel " + s_Name + ", Sample " + S.ToString("N0") + ". Please wait.", Color.Black);
                    ms32_StatusTick = Environment.TickCount;
                }

                if (b_Abort)
                    return;
            }

            Channel i_Channel = new Channel(s_Name);
            i_Channel.mf_Analog = f_Data;
            i_NewChannels.Add(i_Channel);
        }

        // =========================================== MASK ===================================================

        /// <summary>
        /// Pack always 8 bits of n channels into one byte in the OSZI file.
        /// </summary>
        static void WriteDigitalMask(List<Channel> i_Digital, CrcWriter i_Writer, int s32_SaveSamples, int s32_SaveSteps)
        {
            i_Writer.Write(OSZI_DIGITAL_MASK);

            Byte u8_Bit  = 0;
            Byte u8_Byte = 0;
            Byte u8_Mask = 1;
            int S = 0;
            for (int W=0; W < s32_SaveSamples; W++)
            {
                foreach (Channel i_Channel in i_Digital)
                {
                    if (i_Channel.mu8_Digital[S] > 0)
                        u8_Byte |= u8_Mask;

                    u8_Mask <<= 1;
                    u8_Bit ++;

                    if (u8_Bit == 8)
                    {
                        i_Writer.Write(u8_Byte);
                        u8_Bit  = 0;
                        u8_Byte = 0;
                        u8_Mask = 1;

                        if (Math.Abs(Environment.TickCount - ms32_StatusTick) > 330)
                        {
                            Utils.FormMain.PrintStatus("Packing digital channels, Sample " + W.ToString("N0") + ". Please wait.", Color.Black);
                            ms32_StatusTick = Environment.TickCount;
                        }              
                    }
                }

                S += s32_SaveSteps;
            }

            if (u8_Bit > 0)
                i_Writer.Write(u8_Byte);
        }

        /// <summary>
        /// OSZI_DIGITAL_MASK has already been removed from the stream.
        /// Read all digital channels at once
        /// When this is called the analog channels have already been loaded --> read all remaining channels
        /// </summary>
        static void ReadDigitalMask(List<Channel> i_NewChannels, CrcReader i_Reader, int s32_Samples, String[] s_SplitNames, int s32_DigiChannels, ref bool b_Abort)
        {
            List<Channel> i_Digital = new List<Channel>();
            for (int D=0; D<s32_DigiChannels; D++)
            {
                String s_Name = s_SplitNames[i_NewChannels.Count + D];
                Channel i_Channel     = new Channel(s_Name);
                i_Channel.mu8_Digital = new Byte[s32_Samples];
                i_Digital.Add(i_Channel);
            }
            i_NewChannels.AddRange(i_Digital);

            Byte u8_Bit  = 8;
            Byte u8_Byte = 0;
            Byte u8_Mask = 1;
            int  S = 0;
            int  C = 0;
            while (!b_Abort)
            {
                if (u8_Bit == 8)
                {
                    u8_Bit  = 0;
                    u8_Byte = i_Reader.ReadByte();
                    u8_Mask = 1;

                    if (Math.Abs(Environment.TickCount - ms32_StatusTick) > 330)
                    {
                        Utils.FormMain.PrintStatus("Unpacking digital channels, Sample " + S.ToString("N0") + ". Please wait.", Color.Black);
                        ms32_StatusTick = Environment.TickCount;
                    }
                }

                if ((u8_Byte & u8_Mask) > 0)
                    i_Digital[C].mu8_Digital[S] = 1;

                if (++C == i_Digital.Count)
                {
                    C = 0;
                    if (++S == s32_Samples)
                        break;
                }

                u8_Mask <<= 1;
                u8_Bit ++;
            }
        }

        // =========================================== RLE ====================================================

        /// <summary>
        /// Do the RLE encoding of all channels into a Byte[] array.
        /// With RLE encoding each digital channel is encoded separately.
        /// returns null if the RLE encoded digital channels would result in a larger file size than using mask encoding (s32_MaskLen)
        /// </summary>
        static Byte[] WriteDigitalRLE(List<Channel> i_Digital, int s32_SaveSamples, int s32_MaxLen, int s32_SaveSteps)
        {
            Byte[] u8_RLE = new Byte[4];

            using (MemoryStream i_Mem = new MemoryStream())
            using (BinaryWriter i_Bin = new BinaryWriter(i_Mem))
            {
                foreach (Channel i_Channel in i_Digital)
                {
                    i_Bin.Write(OSZI_DIGITAL_RLE);

                    Byte[] u8_Data = i_Channel.mu8_Digital;
                    int  s32_Bits  = 0;
                    int  s32_Total = 0; // for sanity check
                    bool b_Run = true;

                    // get sample 0
                    bool b_PrevHI = u8_Data[0] > 0;

                    // Write the initial status of sample 0 (HIGH or LOW)
                    i_Bin.Write(b_PrevHI ? (Byte)0xFF : (Byte)0x00);

                    int S = 0;

                    // start loop at sample 1
                    for (int W=1; b_Run; W++)
                    {
                        S += s32_SaveSteps;
                        s32_Bits ++;

                        b_Run = (W < s32_SaveSamples);
                        if (b_Run)
                        {
                            bool b_CurHI = u8_Data[S] > 0;
                            if (b_CurHI == b_PrevHI)
                                continue;

                            b_PrevHI = b_CurHI;
                        }

                        s32_Total += s32_Bits;
                           
                        // The digital status has changed --> store bit count (s32_Bits) RLE encoded
                        // There may be 1, 2, 3 or 4 bytes defining the sample count of a digital HIGH or LOW phase.
                        // If the highest bit is set another length byte will follow.
                        // This is similar to UTF8 encoding.
                        int s32_RleLen = 0;
                        for (int R=3; s32_Bits>0; R--)
                        {
                            u8_RLE[R] = (Byte)(s32_Bits & 0x7F);
                            if (R < 3) u8_RLE[R] |= 0x80;
                            s32_RleLen ++;
                            s32_Bits >>= 7;
                        }

                        // The RLE bytes are written Hi first, so decoding becomes easier.
                        i_Bin.Write(u8_RLE, 4 - s32_RleLen, s32_RleLen);

                        if (Math.Abs(Environment.TickCount - ms32_StatusTick) > 330)
                        {
                            Utils.FormMain.PrintStatus("Compressing Channel " + i_Channel.ms_Name + ", Sample " + W.ToString("N0") + ". Please wait.", Color.Black);
                            ms32_StatusTick = Environment.TickCount;
                        }

                        if (i_Bin.BaseStream.Length > s32_MaxLen)
                            return null; // RLE encoding is more inefficient than mask encoding
                    } // for (Sample)

                    Debug.Assert(s32_Total == s32_SaveSamples, "Internal Error RLE");

                } // foreach(Channel)
                return i_Mem.ToArray();
            }
        }

        /// <summary>
        /// With RLE encoding each digital channel is encoded separately.
        /// OSZI_DIGITAL_RLE has already been removed from the stream.
        /// Adds a new channel to the Capture
        /// </summary>
        static void ReadDigitalRLE(List<Channel> i_NewChannels, CrcReader i_Reader, int s32_Samples, String[] s_SplitNames, ref bool b_Abort)
        {
            String  s_Name = s_SplitNames[i_NewChannels.Count];
            Byte[] u8_Data = new Byte[s32_Samples];

            // Read the initial status
            bool b_High;
            switch (i_Reader.ReadByte())
            {
                case 0x00: b_High = false; break;
                case 0xFF: b_High = true;  break;
                default: throw new Exception("The OSZI file has corrupt RLE data"); 
            }

            int s32_Decoded = 0;
            while (s32_Decoded < s32_Samples)
            {
                // for RLE decoding see comments in WriteRLE()
                int s32_Bits = 0;
                for (int R=0; R<4; R++)
                {
                    Byte u8_RLE = i_Reader.ReadByte();
                    s32_Bits <<= 7;
                    s32_Bits  |= u8_RLE & 0x7F;
                    
                    if ((u8_RLE & 0x80) == 0)
                        break;
                }

                int s32_Last = s32_Decoded + s32_Bits;
                if (b_High)
                {
                    for (int B=s32_Decoded; B<s32_Last; B++)
                    {
                        u8_Data[B] = 1; // write HIGH
                    }
                }
                s32_Decoded = s32_Last;

                b_High = !b_High;

                if (Math.Abs(Environment.TickCount - ms32_StatusTick) > 330)
                {
                    Utils.FormMain.PrintStatus("Extracting Channel " + s_Name + ", Sample " + s32_Decoded.ToString("N0") + ". Please wait.", Color.Black);
                    ms32_StatusTick = Environment.TickCount;
                }

                if (b_Abort)
                    return;
            }

            if (s32_Decoded != s32_Samples)
                throw new Exception("The OSZI file has invalid RLE data"); 

            Channel i_Channel = new Channel(s_Name);
            i_Channel.mu8_Digital = u8_Data;
            i_NewChannels.Add(i_Channel);
        }

        #region Selftest

        // =========================================== SELFTEST ====================================================

        #if DEBUG

        /// <summary>
        /// Test BitShifter, CrcWriter, CrcReader writing 7...16 bits to a Stream.
        /// </summary>
        public static void SelftestAnalog()
        {
            try
            {
                String s_Success   = "";
                int    s32_Samples = 7651;
                Random i_Random    = new Random(14);

                for (int s32_Bits = MIN_ANAL_RES; s32_Bits <= MAX_ANAL_RES; s32_Bits++)
                {
                    int s32_Maximum = (1 << s32_Bits) - 1;

                    // ---- fill random data ----

                    int[] s32_Data = new int[s32_Samples];
                    for (int B=0; B<s32_Samples; B++)
                    {
                        s32_Data[B] = i_Random.Next(9, s32_Maximum);
                    }

                    // ------ write stream ------

                    MemoryStream i_Stream  = new MemoryStream();
                    CrcWriter    i_Writer  = new CrcWriter(i_Stream);
                    BitShifter   i_Shifter = new BitShifter(s32_Bits);

                    foreach (int s32_Value in s32_Data)
                    {
                        i_Writer.Write(s32_Value, i_Shifter);
                    }
                    i_Shifter.mb_Flush = true;
                    i_Writer.Write(0, i_Shifter); // flush the remaining bits in the Shifter (if any)

                    // ----- read + compare -----
                    
                    i_Stream.Position  = 0;
                    CrcReader i_Reader = new CrcReader(i_Stream);
                    i_Shifter = new BitShifter(s32_Bits);

                    bool b_OK = true;
                    for (int B=0; B<s32_Samples; B++)
                    {
                        int s32_Value = i_Reader.Read(i_Shifter);
                        if (s32_Value != s32_Data[B])
                        {
                            b_OK = false;
                            break;
                        }
                    }
                    s_Success += String.Format("Analog Test with {0} samples and {1} bits --> {2}\n", s32_Samples, s32_Bits, b_OK ? "Success" : "Failed");
                    s32_Samples += 13;
                }

                MessageBox.Show(Utils.FormMain, s_Success, "Selftest", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(null, Ex);
            }
        }

        // =======================================================================================================

        /// <summary>
        /// Enable this Selftest in the constructor of ExImportManager.
        /// Create digital channels with random data and check Mask encoding and RLE encoding with different channel counts.
        /// </summary>
        public static void SelftestDigital()
        {
            const int MAX_CHANNELS = 17;

            String s_CurTest = "";
            String s_Success = "";
            bool   b_Abort = false;
            try
            {
                int      s32_SampleCount = 9991;
                String[] s_SplitNames = new String[MAX_CHANNELS]; // dummy

                for (int s32_ChanCount = 1; s32_ChanCount <= MAX_CHANNELS; s32_ChanCount++)
                {
                    List<Channel> i_ChannelsInput = CreateDigiRandomChannels(s32_ChanCount, s32_SampleCount);

                    // ================= RLE ===============

                    s_CurTest = "RLE Selftest: " + s32_ChanCount + " channels " + s32_SampleCount + " samples";

                    // Writes ALL channels compressed into a Byte[] array
                    Byte[] u8_RLE = WriteDigitalRLE(i_ChannelsInput, s32_SampleCount, int.MaxValue, 1);
                    MemoryStream i_MemRLE    = new MemoryStream(u8_RLE);
                    CrcReader    i_ReaderRLE = new CrcReader(i_MemRLE);

                    List<Channel> i_ChannelsRLE = new List<Channel>();
                    for (int C=0; C<s32_ChanCount; C++)
                    {
                        if (i_ReaderRLE.ReadUInt32() != OSZI_DIGITAL_RLE)
                            throw new Exception(s_CurTest + ": Invalid stream ID");

                        // Reads ONE channel and appends it to i_OutputRLE
                        ReadDigitalRLE(i_ChannelsRLE, i_ReaderRLE, s32_SampleCount, s_SplitNames, ref b_Abort);
                    }

                    // throws
                    CompareDigiChannels(i_ChannelsInput, i_ChannelsRLE, s_CurTest);

                    s_Success += s_CurTest + " --> Success\n";

                    // ================= MASK ===============

                    s_CurTest = "Mask Selftest: " + s32_ChanCount + " channels " + s32_SampleCount + " samples";

                    MemoryStream i_MemMask = new MemoryStream();
                    CrcWriter i_WriterMask = new CrcWriter(i_MemMask);

                    // Writes ALL channels into the stream
                    WriteDigitalMask(i_ChannelsInput, i_WriterMask, s32_SampleCount, 1);

                    i_MemMask.Position = 0;
                    CrcReader i_ReaderMask = new CrcReader(i_MemMask);

                    if (i_ReaderMask.ReadUInt32() != OSZI_DIGITAL_MASK)
                        throw new Exception(s_CurTest + ": Invalid stream ID");
                    
                    // Reads ALL channels into i_OutputMask
                    List<Channel> i_ChannelsMask = new List<Channel>();
                    ReadDigitalMask(i_ChannelsMask, i_ReaderMask, s32_SampleCount, s_SplitNames, s32_ChanCount, ref b_Abort);

                    // throws
                    CompareDigiChannels(i_ChannelsInput, i_ChannelsMask, s_CurTest);

                    s_Success += s_CurTest + " --> Success\n";
                    s32_SampleCount += 13;
                }

                MessageBox.Show(Utils.FormMain, s_Success, "Selftest", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(null, Ex, s_CurTest);
            }
        }

        /// <summary>
        /// Create digital channels and load them with random data
        /// </summary>
        static List<Channel> CreateDigiRandomChannels(int s32_ChanCount, int s32_Samples)
        {
            Random i_Random = new Random(22);
            bool   b_High   = true;

            List<Channel> i_Channels = new List<Channel>();
            for (int C=0; C<s32_ChanCount; C++)
            {
                Channel i_Channel = new Channel("Random " + (C+1));
                i_Channel.ms32_ColorIdx = C;
                i_Channel.mu8_Digital   = new Byte[s32_Samples];
                i_Channels.Add(i_Channel);

                // fill channel with random data
                int S=0;
                while (S < s32_Samples)
                {
                    int s32_Random = i_Random.Next(5, 500);
                    //  s32_Random = 10;
                    if (b_High)
                    {
                        for (int R=0; R < s32_Random && S+R < s32_Samples; R++)
                        {
                            i_Channel.mu8_Digital[S+R] = 1;
                        }
                    }

                    S += s32_Random;
                    b_High = !b_High;
                }
            }
            return i_Channels;
        }

        static void CompareDigiChannels(List<Channel> i_Input, List<Channel> i_Output, String s_CurTest)
        {
            if (i_Input.Count != i_Output.Count)
                throw new Exception(s_CurTest + ": Invalid Channel Count");

            for (int C=0; C<i_Input.Count; C++)
            {
                Channel i_ChanIn  = i_Input [C];
                Channel i_ChanOut = i_Output[C];

                if (i_ChanIn.mu8_Digital.Length != i_ChanOut.mu8_Digital.Length)
                    throw new Exception(s_CurTest + ": Invalid Digital Length");

                for (int S=0; S<i_ChanIn.mu8_Digital.Length; S++)
                {
                    if (i_ChanIn.mu8_Digital[S] != i_ChanOut.mu8_Digital[S])
                        throw new Exception(s_CurTest + ": Data mismatch at sample " + S);
                }
            }
        }

        // =======================================================================================================

        /// <summary>
        /// Create a Capture that contains an analog and digital channel with marks.
        /// The High time is 100 samples, the Low time is 100, 101, 102, 103,... samples to check rounding in PanelOszi.
        /// </summary>
        public static Capture SelftestCreateTestCapture()
        {          
            Channel        i_Chan  = new Channel("Test");
            List<SmplMark> i_Marks = new List<SmplMark>();

            const int SAMPLES = 2000;
            i_Chan.mu8_Digital = new Byte [SAMPLES];
            i_Chan.mf_Analog   = new float[SAMPLES];
            i_Chan.mi_MarkRows = new List<SmplMark>[] { i_Marks };

            int s32_Dist = 100;
            int S=0;
            for (int H=1; H<10; H++)
            {
                S += s32_Dist;
                s32_Dist ++;
                i_Marks.Add(new SmplMark(eMark.Text, S, -1, H.ToString()));

                for (int L=0; L<100; L++)
                {
                    i_Chan.mu8_Digital[S] = 1;
                    i_Chan.mf_Analog[S++] = 10.0f;
                }
            }

            Capture i_Capture = new Capture();
            i_Capture.ms32_AnalogCount  = 1;
            i_Capture.ms32_DigitalCount = 1;
            i_Capture.ms32_Samples      = SAMPLES;
            i_Capture.ms64_SampleDist   = 1000000;
            i_Capture.ms32_AnalogRes    = 8;
            i_Capture.mi_Channels.Add(i_Chan);
            return i_Capture;
        }

        #endif     // DEBUG
        #endregion // Selftest
    }
}
