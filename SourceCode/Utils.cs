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
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

using SplMinMax         = OsziWaveformAnalyzer.OsziPanel.SplMinMax;
using PlatformManager   = Platform.PlatformManager;

namespace OsziWaveformAnalyzer
{
    public class Utils
    {
        #region enums

        public enum eRegKey
        {
            MainWindow,
            OsziSerie,
            InstallPath,
            AnalogHeightSeparate,
            AnalogHeightCommon,
            DigitalHeight,
            ShowLegend,
            SeparateChannel,
            CaptureMemory,
            SendCommand,
            ConnectMode,
            ConnectUSB,
            ConnectTCP,
            ConnectVXI,
            LinkVXI,
            // ------------
            RasterInterval,
            RasterUnit,
            RS232_Settg,
            I2C_Chip,
            SPI_Settg,
            AD_Result,
            AD_ThresholdLo,
            AD_ThresholdHi,
            NoiseSuppress,
            NoiseCycles,
            CanBaudStd,
            CanBaudFD,
            CanSplPointStd,
            CanSplPointFD,
        }

        public enum eMark : byte
        {
            Text,   // ms_Text displayed in white
            Error,  // ms_Text displayed in red
        }

        public enum eDigiState
        {
            Low  = 0,
            High = 1,
        }

        public enum eCtrlChar
        {
            NUL = 0,
            SOH, //  1, 0x01
            STX, //  2, 0x02
            ETX, //  3, 0x03
            EOT, //  4, 0x04
            ENQ, //  5, 0x05
            ACK, //  6, 0x06
            BEL, //  7, 0x07
            BS,  //  8, 0x08
            HT,  //  9, 0x09
            LF,  // 10, 0x0A
            VT,  // 11, 0x0B
            FF,  // 12, 0x0C
            CR,  // 13, 0x0D
            SO,  // 14, 0x0E
            SI,  // 15, 0x0F
            DLE, // 16, 0x10
            DC1, // 17, 0x11  also CS1, XON
            DC2, // 18, 0x12
            DC3, // 19, 0x13  also XOFF
            DC4, // 20, 0x14
            NAK, // 21, 0x15
            SYN, // 22, 0x16
            ETB, // 23, 0x17
            CAN, // 24, 0x18
            EM,  // 25, 0x19
            SUB, // 26, 0x1A  also EOF
            ESC, // 27, 0x1B
            FS,  // 28, 0x1C
            GS,  // 29, 0x1D
            RS,  // 30, 0x1E
            US,  // 31, 0x1F
        }

        #endregion

        #region class Capture

        public class Capture
        {
            // These must be assigned when a new class is created
            public String        ms_Path;           // original load path
            public int           ms32_Samples;      // total count of samples in this capture
            public Int64         ms64_SampleDist;   // the time between 2 neighboured samples in pico seconds
            public int           ms32_AnalogRes;    // the resolution of the A/D converter in the oscilloscope in bits (Rigol = 8 bit)
            public List<Channel> mi_Channels     = new List<Channel>();
            public int[]         ms32_Separators = new int[0]; // Sample positions where to draw a red vertical line (used for OWON Frames)
            public bool          mb_Dirty;          // the user has modified channels after loading the capture for the first time (unsaved changes).
            // -------------------------------
            // The following are assigned in CalcAnalogMinMax()
            public int           ms32_AnalogCount;  // count of channels with analog data 
            public int           ms32_DigitalCount; // count of channels with digital data 

            /// <summary>
            /// After deleting the Min/Max values they will be recalculated in OnPaint()
            /// Call this ONLY if really required! It causes a notable delay if 24 million samples have to be re-calcuated.
            /// </summary>
            public void ResetSampleMinMax()
            {
                foreach (Channel i_Channel in mi_Channels)
                {
                    i_Channel.mi_SampleMinMax.mb_AnalogOK  = false;
                    i_Channel.mi_SampleMinMax.mb_DigitalOK = false;
                }
            }

            /// <summary>
            /// Remove all mark rows
            /// </summary>
            public void ResetMarks()
            {
                foreach (Channel i_Channel in mi_Channels)
                {
                    i_Channel.mi_MarkRows = null;
                }
            }

            /// <summary>
            /// Calculate the duration of n samples
            /// </summary>
            public String FormatInterval(int s32_Sample)
            {
                return FormatTimePico((decimal)(ms64_SampleDist * s32_Sample));
            }

            /// <summary>
            /// Calculate the frequency that is equivalent to the duration of n samples
            /// </summary>
            public String FormatFrequency(int s32_Sample)
            {
                decimal d_Pico = (decimal)(ms64_SampleDist * s32_Sample);
                if (d_Pico <= 0.0m)
                    return "Invalid";

                decimal d_Frequency = PICOS_PER_SECOND / d_Pico;
                return Utils.FormatFrequency(d_Frequency);
            }

            public void ClearThresholds()
            {
                foreach (Channel i_Channel in mi_Channels)
                {
                    i_Channel.mb_Threshold = false;
                }
            }

            /// <summary>
            /// returns an existing channel with the given name or inserts a new channel with the given name 
            /// behind the last channel in i_InsertAfter
            /// </summary>
            public Channel FindOrCreateChannel(String s_Name, params Channel[] i_InsertAfter)
            {
                foreach(Channel i_Channel in mi_Channels)
                {
                    if (i_Channel.ms_Name == s_Name)
                        return i_Channel;
                }

                Channel i_NewChannel = new Channel(s_Name);

                // Search a color index for a new channel which is not yet in use.
                // If the color index exceeds the available colors, the colors will repeat.
                for (int s32_Color=0; true; s32_Color++)
                {
                    bool b_Found = false;
                    foreach (Channel i_Chan in mi_Channels)
                    {
                        if (i_Chan.ms32_ColorIdx == s32_Color)
                        {
                            b_Found = true;
                            break;
                        }
                    }
                    if (!b_Found) // an unused color index was found
                    {
                        i_NewChannel.ms32_ColorIdx = s32_Color;
                        break;
                    }
                }

                if (i_InsertAfter != null)
                {
                    // Insert the new channel behind the last channel in i_InsertAfter
                    int s32_Pos = 0;
                    foreach (Channel i_ChanAfter in i_InsertAfter)
                    {
                        s32_Pos = Math.Max(s32_Pos, mi_Channels.IndexOf(i_ChanAfter));
                    }
                    mi_Channels.Insert(s32_Pos + 1, i_NewChannel);
                }
                else
                {
                    // Append as last channel
                    mi_Channels.Add(i_NewChannel);
                }
                return i_NewChannel;
            }
        }

        #endregion

        #region class Channel

        /// <summary>
        /// A channel may conatin only analog data, only digital data or both.
        /// </summary>
        public class Channel
        {
            public String           ms_Name;
            // -------------------------------
            public float[]          mf_Analog;     // analog  channel data (voltage)
            public Byte[]           mu8_Digital;   // digital channel data (the bytes contain only 0 or 1) (do not use bool which occupies 4 bytes in memory)
            // -------------------------------
            // The following are assigned in CalcAnalogMinMax()
            public float            mf_Min;        // minimum analog voltage of all samples
            public float            mf_Max;        // maximum analog voltage of all samples
            // -------------------------------
            public float            mf_ThreshLo;   // low hysteresis threshold voltage for the D/A converter
            public float            mf_ThreshHi;   // high hysteresis threshold voltage for the D/A converter
            public bool             mb_Threshold;  // true --> draw the threshold lines in the OsziPanel
            // -------------------------------
            public List<SmplMark>[] mi_MarkRows;   // Mark Row 1: clock marks and decoded bits, Mark Row 2: decoded bytes / ASCII
            
            // ===================================================
            //    Used internally by OsziPanel. Do not modify!
            // ===================================================
            public int       ms32_ColorIdx;   // A channel has the same color as long as it lives
            public bool      mb_AnalHidden;   // true --> do not draw the analog signal (checkbox is off)
            public SplMinMax mi_SampleMinMax = new SplMinMax();

            public Channel(String s_Name)
            {
                ms_Name = s_Name;
            }

            public override string ToString()
            {
                return ms_Name;
            }

            /// <summary>
            /// Channels which never change their state are empty. They are not saved to disk.
            /// </summary>
            public bool IsDigiEmpty()
            {
                Byte u8_First = mu8_Digital[0];
                for (int S=0; S<mu8_Digital.Length; S++)
                {
                    if (mu8_Digital[S] != u8_First)
                        return false;
                }
                return true;
            }
        }

        #endregion

        #region class SmplMark

        public class SmplMark : IComparable
        {
            // required for drawing on OsziPanel
            public eMark  me_Mark;
            public int    ms32_FirstSample; 
            public int    ms32_LastSample;   
            public String ms_Text;        // optional text
            public Brush  mi_TxtBrush;    // red / white
            public Pen    mi_PenStart;    // for vertical line (red / white)
            public Pen    mi_PenEnd;      // for vertical line (magenta / white)
            
            // used internally by some decoders
            public int    ms32_Value;

            public SmplMark(eMark e_Mark, int s32_SampleA, int s32_SampleB = -1, String s_Text = null, int s32_Value = 0)
            {
                if (s32_SampleB < 0)
                    s32_SampleB = s32_SampleA;

                ms32_FirstSample = Math.Min(s32_SampleA, s32_SampleB); // samples may be reverse
                ms32_LastSample  = Math.Max(s32_SampleA, s32_SampleB);
                me_Mark          = e_Mark;
                ms_Text          = s_Text;
                ms32_Value       = s32_Value;

                switch (e_Mark)
                {
                    case eMark.Text:  mi_PenStart = Pens.White; mi_TxtBrush = Brushes.White; break;
                    case eMark.Error: mi_PenStart = Pens.Red;   mi_TxtBrush = ERROR_BRUSH;   break;
                    default: throw new ArgumentException();
                }
                mi_PenEnd = mi_PenStart;
            }

            /// <summary>
            /// for sorting
            /// </summary>
            int IComparable.CompareTo(Object o_Comp)
            {
                SmplMark i_Mark = (SmplMark)o_Comp;
                return ms32_FirstSample.CompareTo(i_Mark.ms32_FirstSample);
            }

            /// <summary>
            /// For debugging
            /// </summary>
            public override string ToString()
            {
                String s_Text = (me_Mark == eMark.Text) ? "'" + ms_Text + "'" : me_Mark.ToString();
                int s32_Last  = Math.Max(ms32_LastSample, ms32_FirstSample); // ms32_LastSample may be zero or -1
                return String.Format("{0}: Samples from {1:N0} to {2:N0}", 
                                     s_Text, ms32_FirstSample, s32_Last);
            }
        }

        #endregion

        #region class ComboPath

        /// <summary>
        /// For ComboBox "Input File": Store full Path but display only Filename.
        /// </summary>
        public class ComboPath
        {
            public String ms_Path;

            public ComboPath(String s_Path)
            {
                ms_Path = s_Path;
            }

            public override string ToString()
            {
                if (ms_Path == null)
                    return "No files found";

                return Path.GetFileName(ms_Path);
            }
        }

        #endregion

        public delegate bool delInvokeBool();

        public  const  String     APP_VERSION       = "v2.4"; // displayed in Main Window Title
        public  const  int        MIN_VALID_SAMPLES = 100;    // Error if loaded file contains less samples
        public  const  String     ERR_MIN_SAMPLES   = "The minimum amount of samples is 100.";
        public  const  String     NO_SAMPLES_LOADED = "No samples loaded. Use button 'Capture' or select an Input file.";
        public  const  Decimal    PICOS_PER_SECOND  = 1000000000000; // pico seconds in one seond (12 zeros)
        public  const  String     CMD_LINE_ACTION   = " -open ";
        public  static Color      ERROR_COLOR       = Color.FromArgb(0xFF, 0x55, 0x33); // Red is too dark
        public  static Brush      ERROR_BRUSH       = new SolidBrush(ERROR_COLOR);
        public  static int        MIN_ANAL_RES      =  7; // Minimum analog resolution = 128 steps
        public  static int        MAX_ANAL_RES      = 16; // Maximum analog resolution = 65536 steps
        public  static String[]   TIME_UNITS        = new String[] { "ps", "ns", "µs", "ms", "sec" };
        public  static Encoding   ENCODING_ANSI     = Encoding.GetEncoding(1252);
        // -----------------------------------------
        private const  String     REGISTRY_PATH     = "HKEY_CURRENT_USER\\Software\\ElmueSoft\\Oszi Waveform Analyzer";               
        private static FormMain   mi_Main;
        private static OsziPanel  mi_OsziPanel;
        private static String     ms_AppDir;
        private static String     ms_SampleDir;
        private static String     ms_WinInstaller;
        private static String     ms_HelpHtmlPath;
        private static bool       mb_Busy;
        private static Image      mi_OsziImg;

        public static String AppDir
        {
            get { return ms_AppDir; }
        }

        public static String SampleDir
        {
            get { return ms_SampleDir; }
            set { ms_SampleDir = value; }
        }

        public static String WinInstallerPath
        {
            get { return ms_WinInstaller; }
        }

        public static String HelpHtmlPath
        {
            get { return ms_HelpHtmlPath; }
        }

        public static FormMain FormMain
        {
            get { return mi_Main; }
        }

        public static OsziPanel OsziPanel
        {
            get { return mi_OsziPanel; }
        }

        public static bool IsBusy
        {
            get { return mb_Busy; }
        }

        public static Image OsziImg
        {
            get 
            { 
                if (mi_OsziImg == null)
                    mi_OsziImg = LoadResourceImage("Oszi.png");

                return mi_OsziImg; 
            }
        }

        // --------------------------------------------------------------------------

        public static void Init(FormMain i_Main, OsziPanel i_OsziPanel)
        {
            mi_Main      = i_Main;
            mi_OsziPanel = i_OsziPanel;

            try
            {
                ms_AppDir    = Path.GetDirectoryName(Application.ExecutablePath);
                ms_SampleDir = ms_AppDir + "\\Samples";

                ms_WinInstaller = ms_AppDir + "\\Driver\\dpinst-amd64.exe";
                String s_Driver = ms_AppDir + "\\Driver\\amd64\\ausbtmc.sys";
                if (!File.Exists(ms_WinInstaller) || !File.Exists(s_Driver))
                    throw new Exception("Driver files not found");

                ms_HelpHtmlPath = ms_AppDir + "\\Manual.htm";
                if (!File.Exists(ms_HelpHtmlPath))
                    throw new Exception("Help HTML file not found");

                if (!File.Exists(ms_AppDir + "\\Images\\OsziWaveformAnalyzer.png"))
                    throw new Exception("Help HTML is incomplete");
            }
            catch (Exception Ex)
            {
                ShowExceptionBox(i_Main, Ex, "The installation is corrupt.\nPlease download the ZIP file and extract ALL files!");
                Application.Exit();
            }

            // ----------------------------------------------------------

            // Check that subfolder "Samples" has write permission
            try
            {
                if (!Directory.Exists(SampleDir))
                     Directory.CreateDirectory(SampleDir);

                String s_DummyFile = SampleDir + "adh9dah293je12kdui9hd38r3.txt";
                File.WriteAllText(s_DummyFile, "Test");
                File.Delete(s_DummyFile);
            }
            catch (Exception Ex)
            {
                ShowExceptionBox(i_Main, Ex, "Make sure that the subfolder 'Samples' has write permission:\n" + SampleDir);
                Application.Exit();
            }

            // ----------------------------------------------------------

            // Associate file extension *.oszi with this program
            PlatformManager.Instance.AssociateOsziExtension();

            // ----------------------------------------------------------

            // Create shortcut in startmenu once. (If the user deletes it, it will not be created again)
            PlatformManager.Instance.CreateShortcutInStartMenu();
        }

        // --------------------------------------------------------------------------

        /// <summary>
        /// Make the combobox drop down so wide that no text is cropped.
        /// </summary>
        public static void ComboAdjustDropDownWidth(ComboBox i_Combo)
        {
            int s32_Width = i_Combo.Width;

            foreach (Object o_Item in i_Combo.Items)
            {
                Size k_Size = TextRenderer.MeasureText(o_Item.ToString(), i_Combo.Font);
                s32_Width = Math.Max(s32_Width, k_Size.Width + 5);
            }

            if (i_Combo.Items.Count > i_Combo.MaxDropDownItems)
                s32_Width += SystemInformation.VerticalScrollBarWidth;

            i_Combo.DropDownWidth = s32_Width;
        }

        // --------------------------------------------------------------------------

        /// <summary>
        /// Format a time interval given in pico seconds (10E-12 seconds)
        /// </summary>
        public static String FormatTimePico(decimal d_Time)
        {
            String s_Sign = (d_Time < 0) ? "-" : "";
            d_Time = Math.Abs(d_Time);

            if (d_Time < 1000)
                return String.Format("{0}{1:0.###} ps", s_Sign, d_Time).Replace(',', '.'); // pico

            d_Time /= 1000;
            if (d_Time < 1000)
                return String.Format("{0}{1:0.###} ns", s_Sign, d_Time).Replace(',', '.'); // nano

            d_Time /= 1000;
            if (d_Time < 1000)
                return String.Format("{0}{1:0.###} µs", s_Sign, d_Time).Replace(',', '.'); // micro

            d_Time /= 1000;            
            if (d_Time < 1000)
                return String.Format("{0}{1:0.###} ms", s_Sign, d_Time).Replace(',', '.'); // milli

            d_Time /= 1000;
            if (d_Time < 60) // < 1 minute
                return String.Format("{0}{1:0.###} sec", s_Sign, d_Time).Replace(',', '.');

            int s32_Sec  = (int)d_Time;
            int s32_Min  = s32_Sec / 60;
            int s32_Hour = s32_Min / 60;
            if (s32_Hour == 0) // < 1 hour
                return String.Format("{0}{1:D2}:{2:D2} min",  s_Sign, s32_Min % 60, s32_Sec % 60);
            else
                return String.Format("{0}{1:D2}:{2:D2} hour", s_Sign, s32_Hour, s32_Min % 60);
        }

        public static String FormatFrequency(decimal d_Frequ)
        {
            if (d_Frequ < 1000)
                return String.Format("{0:0.###} Hz", d_Frequ).Replace(',', '.');

            d_Frequ /= 1000;
            if (d_Frequ < 1000)
                return String.Format("{0:0.###} kHz", d_Frequ).Replace(',', '.');

            d_Frequ /= 1000;
            if (d_Frequ < 1000)
                return String.Format("{0:0.###} MHz", d_Frequ).Replace(',', '.');

            d_Frequ /= 1000;
            return String.Format("{0:0.###} GHz", d_Frequ).Replace(',', '.');
        }       

        public static String FormatSize(decimal d_Size)
        {
            if (d_Size < 1024)
                return d_Size + " Byte";

            d_Size /= 1024;
            if (d_Size < 1024)
                return String.Format("{0:0.#} kB", d_Size).Replace(',', '.');

            d_Size /= 1024;
            if (d_Size < 1024)
                return String.Format("{0:0.#} MB", d_Size).Replace(',', '.');

            d_Size /= 1024;
            return String.Format("{0:0.#} GB", d_Size).Replace(',', '.');
        }

        // --------------------------------------------------------------------------

        /// <summary>
        /// Converts all words in the text which are entriely uppercase into mixed case.
        /// All words which are already mixed case stay unchanged.
        /// For example "APPLE iPhone" --> "Apple iPhone"
        /// </summary>
        public static String FirstToUpper(String s_Text)
        {
            StringBuilder i_Out = new StringBuilder();
            foreach (String s_Part in s_Text.Split(' '))
            {
                if (i_Out.Length > 0)
                    i_Out.Append(' ');

                // Leave short words unchanged ("FTDI", "Scantool.net LLC", "USA", etc)
                if (s_Part.Length > 4)
                {
                    String s_Upper = s_Part.ToUpperInvariant();
                    String s_Lower = s_Part.ToLowerInvariant();
                    if (s_Part == s_Upper || s_Part == s_Lower)
                    {
                        i_Out.Append(s_Upper.Substring(0, 1));
                        i_Out.Append(s_Lower.Substring(1));
                        continue;
                    }
                }
                i_Out.Append(s_Part);
            }
            return i_Out.ToString();
        }

        // --------------------------------------------------------------------------

        public static void FillArray(Byte[] u8_Array, Byte u8_Value)
        {
            for (int i=0; i<u8_Array.Length; i++)
            {
                u8_Array[i] = u8_Value;
            }
        }

        public static bool CompareBytes(List<Byte> i_Data, int s32_Start, params Byte[] u8_Data)
        {
            if (i_Data.Count < s32_Start + u8_Data.Length)
                return false;

            for (int i=0; i<u8_Data.Length; i++)
            {
                if (i_Data[s32_Start + i] != u8_Data[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// s32_DataLen = the count of bytes in u8_Data that have valid values.
        /// Reverse search u8_Pattern in u8_Data.
        /// If s32_MaxCount == 4 --> check only the last 4 bytes of u8_Data.
        /// returns the index of the found byte or -1 if not found.
        /// </summary>
        public static int FindByteReverse(Byte[] u8_Data, int s32_DataLen, int s32_MaxCount, Byte u8_Pattern)
        {
            int s32_Limit = Math.Max(0, s32_DataLen - s32_MaxCount);
            for (int i=s32_DataLen - 1; i >= s32_Limit; i--)
            {
                if (u8_Data[i] == u8_Pattern)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Search u8_Pattern in i_Data
        /// </summary>
        public static int FindBytes(List<Byte> i_Data, int s32_Start, params Byte[] u8_Pattern)
        {
            if (s32_Start + u8_Pattern.Length > i_Data.Count || u8_Pattern.Length == 0)
                return -1;

            int s32_Last = i_Data.Count - (s32_Start + u8_Pattern.Length);
            for (int D=s32_Start; D<=s32_Last; D++)
            {
                if (i_Data[D] != u8_Pattern[0])
                    continue;

                bool b_Found = true;
                for (int P=1; P<u8_Pattern.Length; P++)
                {
                    if (i_Data[D+P] != u8_Pattern[P])
                    {
                        b_Found = false;
                        break;
                    }
                }
                if (b_Found)
                    return D;
            }
            return -1;
        }

        public static List<Byte> ExtractBytes(List<Byte> i_Data, int s32_Start, int s32_Count = -1)
        {
            if (s32_Count < 0)
                s32_Count = i_Data.Count - s32_Start;

            if (s32_Start < 0 || s32_Count < 0 || s32_Start + s32_Count > i_Data.Count)
                return null;
            
            Byte[] u8_Copy = new Byte[s32_Count];
            i_Data.CopyTo(s32_Start, u8_Copy, 0, s32_Count);
            return new List<Byte>(u8_Copy);
        }

        /// <summary>
        /// SPI may use different data lengths other than 8 bit.
        /// Oszi Waveform Analyzer supports SPI packets up to 64 bit.
        /// If data has 8 bit length the SPI data can be converted with this function.
        /// </summary>
        public static List<Byte> ConvertList64To8Bit(List<UInt64> i_SpiData)
        {
            if (i_SpiData == null)
                return null;

            List<Byte> i_Bytes = new List<Byte>();
            foreach (UInt64 u64_Long in i_SpiData)
            {
                if (u64_Long > 0xFF)
                    return null;

                i_Bytes.Add((Byte)u64_Long);
            }
            return i_Bytes;
        }

        // --------------------------------------------------------------------------

        /// <summary>
        /// Get the [Description("Bla Bla")] attribute
        /// </summary>
        public static String GetDescriptionAttribute(Object o_Object)
        {
            FieldInfo i_Field = o_Object.GetType().GetField(o_Object.ToString());
            DescriptionAttribute[] i_Descr = i_Field.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            if (i_Descr != null)
                return i_Descr[0].Description;

            throw new Exception("Programminmg Error: Description attribute missing for " + o_Object);
        }

        // --------------------------------------------------------------------------

        public static bool StartBusyOperation(Form i_Form)
        {
            if (i_Form == null)
                i_Form = mi_Main;

            if (mb_Busy)
            {
                MessageBox.Show(i_Form, "Please wait while I'am busy!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            i_Form.Cursor = Cursors.WaitCursor;

            mb_Busy = true;
            return true;
        }

        public static void EndBusyOperation(Form i_Form)
        {
            if (i_Form == null)
                i_Form = mi_Main;

            i_Form.Cursor = Cursors.Arrow;
            mb_Busy = false;
        }

        public static void ShowExceptionBox(Form i_Owner, Exception i_Exception, String s_Mesg = "")
        {
            if (i_Owner == null)
                i_Owner = mi_Main;

            i_Owner.Cursor = Cursors.Arrow;

            if (s_Mesg.Length > 0)
                s_Mesg += "\n";

            String s_Text = s_Mesg + i_Exception.Message;
            #if DEBUG
                s_Text += "\n\n" + i_Exception.StackTrace.Replace("\n", "\n\n");
            #endif
            MessageBox.Show(i_Owner, s_Text, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // --------------------------------------------------------------------------

        public static String RegReadString(eRegKey e_Key, String s_Default = "")
        {
            Object o_Value = Registry.GetValue(REGISTRY_PATH, e_Key.ToString(), null);
            if (o_Value is String) return (String)o_Value;
            else                   return s_Default;
        }

        public static void RegWriteString(eRegKey e_Key, String s_Value)
        {
            Registry.SetValue(REGISTRY_PATH, e_Key.ToString(), s_Value);
        }

        // ---------------------

        public static bool RegReadBool(eRegKey e_Key, bool b_Default = false)
        {
            Object o_Value = Registry.GetValue(REGISTRY_PATH, e_Key.ToString(), null);
            if (o_Value is int) return (int)o_Value == 1;
            else                return b_Default;
        }

        public static void RegWriteBool(eRegKey e_Key, bool b_Value)
        {
            Registry.SetValue(REGISTRY_PATH, e_Key.ToString(), b_Value ? 1 : 0);
        }

        // ---------------------

        public static int RegReadInteger(eRegKey e_Key, int s32_Default = 0)
        {
            Object o_Value = Registry.GetValue(REGISTRY_PATH, e_Key.ToString(), null);
            if (o_Value is int) return (int)o_Value;
            else                return s32_Default;
        }

        public static void RegWriteInteger(eRegKey e_Key, int s32_Value)
        {
            Registry.SetValue(REGISTRY_PATH, e_Key.ToString(), s32_Value);
        }

        // ---------------------

        public static Rectangle RegReadRectangle(eRegKey e_Key)
        {
            String   s_Rect  = RegReadString(e_Key);
            String[] s_Parts = s_Rect.Split('|');
            
            int X, Y, W, H;
            if (s_Parts.Length == 4             &&
                int.TryParse(s_Parts[0], out X) &&
                int.TryParse(s_Parts[1], out Y) &&
                int.TryParse(s_Parts[2], out W) &&
                int.TryParse(s_Parts[3], out H) &&
                H > 100 && W > 100)
                    return new Rectangle(X, Y, W, H);
                
            return Rectangle.Empty;
        }
        public static void RegWriteRectangle(eRegKey e_Key, Rectangle r_Rect)
        {
            RegWriteString(e_Key, String.Format("{0}|{1}|{2}|{3}", r_Rect.X, r_Rect.Y, r_Rect.Width, r_Rect.Height));
        }

        // ---------------------

        public static void LoadWndPosFromRegistry(Form i_Form, eRegKey e_RegKey)
        {
            Rectangle r_Rect = RegReadRectangle(e_RegKey);
            if (r_Rect == Rectangle.Empty)
                return;

            i_Form.StartPosition = FormStartPosition.Manual;

            Rectangle r_Wnd = LimitRectOnMonitor(r_Rect, i_Form.MinimumSize);

            if (i_Form.FormBorderStyle == FormBorderStyle.Sizable)
                i_Form.Bounds = r_Wnd;
            else
                i_Form.Location = r_Wnd.Location;
        }

		public static Rectangle LimitRectOnMonitor(Rectangle r_Rect, Size k_MinSize)
		{
            // Never returns null. If the rectangle is outside any screen -> retruns the closest screen.
            Screen    i_Screen = Screen.FromRectangle(r_Rect);
			Rectangle r_Screen = i_Screen.WorkingArea;

            if (r_Rect.Width  < k_MinSize.Width)  r_Rect.Width  = k_MinSize.Width;
            if (r_Rect.Height < k_MinSize.Height) r_Rect.Height = k_MinSize.Height;
            
            if (r_Rect.Width  > r_Screen.Width)   r_Rect.Width  = r_Screen.Width;
            if (r_Rect.Height > r_Screen.Height)  r_Rect.Height = r_Screen.Height;
            
            if (r_Rect.Left   < r_Screen.Left)    r_Rect.X = r_Screen.Left;
            if (r_Rect.Top    < r_Screen.Top)     r_Rect.Y = r_Screen.Top;

            if (r_Rect.Right  > r_Screen.Right)   r_Rect.X = r_Screen.Right  - r_Rect.Width;
            if (r_Rect.Bottom > r_Screen.Bottom)  r_Rect.Y = r_Screen.Bottom - r_Rect.Height;

            return r_Rect;
		}

        // -----------------------------------------------------

        /// <summary>
        /// Set settings of multiple ComboBoxes, CheckBoxes and RadioButtons from one comma separated settings string.
        /// IMPORTANT: All RadioButtons must be passed, because setting False does not check the other button.
        /// </summary>
        public static bool SetControlValues(String s_Settings, params Control[] i_Ctrls)
        {
            String[] s_Parts = s_Settings.Split(',');

            if (s_Parts.Length != i_Ctrls.Length)
            {
                Debug.Assert(false, "Invalid Control settings");
                return false; // Invalid s_Settings string or programming error.
            }

            for (int i=0; i<s_Parts.Length; i++)
            {
                ComboBox    i_Combo = i_Ctrls[i] as ComboBox;
                CheckBox    i_Check = i_Ctrls[i] as CheckBox;
                RadioButton i_Radio = i_Ctrls[i] as RadioButton;
                
                     if (i_Combo != null) i_Combo.Text    = s_Parts[i].Trim();
                else if (i_Check != null) i_Check.Checked = s_Parts[i].Trim().ToUpper() == "TRUE";
                else if (i_Radio != null) i_Radio.Checked = s_Parts[i].Trim().ToUpper() == "TRUE";
                else Debug.Assert(false, "Programming Error: Control not implemented");
            }
            return true;
        }

        /// <summary>
        /// Write settings of multiple ComboBoxes, CheckBoxes and RadioButtons into one comma separated settings string.
        /// IMPORTANT: All RadioButtons must be passed, because setting False does not check the other button.
        /// </summary>
        public static String GetControlValues(params Control[] i_Ctrls)
        {
            String s_Settings = "";
            foreach (Control i_Ctrl in i_Ctrls)
            {
                if (s_Settings.Length > 0)
                    s_Settings += ",";

                ComboBox    i_Combo = i_Ctrl as ComboBox;
                CheckBox    i_Check = i_Ctrl as CheckBox;
                RadioButton i_Radio = i_Ctrl as RadioButton;
                
                     if (i_Combo != null) s_Settings += i_Combo.Text;
                else if (i_Check != null) s_Settings += i_Check.Checked;
                else if (i_Radio != null) s_Settings += i_Radio.Checked;
                else Debug.Assert(false, "Programming Error: Control not implemented");
            }
            return s_Settings;
        }

        // -----------------------------------------------------

        public static bool CheckChannelNames(Form i_Form = null, String s_NewName = null)
        {
            List<String> i_Names = new List<String>();
            if (s_NewName != null)
                i_Names.Add(s_NewName);

            foreach (Channel i_Channel in OsziPanel.CurCapture.mi_Channels)
            {
                if (i_Names.Contains(i_Channel.ms_Name))
                {
                    if (i_Form == null) i_Form = mi_Main;
                    MessageBox.Show(i_Form, "Please assign a unique name to all channels.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                i_Names.Add(i_Channel.ms_Name);
            }
            return true;
        }

        /// <summary>
        /// Called before saving and analyzing
        /// </summary>
        public static bool CheckMinSamples()
        {
            if (OsziPanel.CurCapture == null || OsziPanel.CurCapture.ms32_Samples == 0)
            {
                MessageBox.Show(mi_Main, NO_SAMPLES_LOADED, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (OsziPanel.CurCapture.ms32_Samples < MIN_VALID_SAMPLES)
            {
                MessageBox.Show(mi_Main, ERR_MIN_SAMPLES, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        // -------------------------------------------------------

        public static Image LoadResourceImage(String s_ImageName)
        {
			Assembly i_Ass  = Assembly.GetExecutingAssembly();
			Stream   i_Strm = i_Ass.GetManifestResourceStream("OsziWaveformAnalyzer.Resources." + s_ImageName);
            if (i_Strm == null)
                throw new Exception("Resource Image not found: "+s_ImageName + "\nDid you compile it as 'Embedded Resource'?");

            return Image.FromStream(i_Strm);
        }

        // ================================= INI DATA ===========================================

        /// <summary>
        /// Reads INI data of the form:
        /// Key = Value\n
        /// throws
        /// [Sections] are not implemented becasue not needed in this project
        /// </summary>
        public static Dictionary<String, String> ReadIniFile(String s_IniPath)
        {
            Dictionary<String, String> i_Dict = new Dictionary<String, String>();

            String[] s_Lines = File.ReadAllLines(s_IniPath);
            for (int L=0; L<s_Lines.Length; L++)
            {
                String s_Line = s_Lines[L].Trim();
                if (s_Line.Length == 0 || s_Line.StartsWith(";"))
                    continue;

                int s32_Equal = s_Line.IndexOf('=');
                if (s32_Equal < 0)
                    throw new Exception("Syntax error in INI line " + (L+1) + " in\n" + s_IniPath);

                String s_Key = s_Line.Substring(0, s32_Equal).Trim();
                String s_Val = s_Line.Substring(s32_Equal +1).Trim();

                if (i_Dict.ContainsKey(s_Key))
                    throw new Exception("The INI key '" + s_Key + "' is defined multiple times in\n" + s_IniPath);

                i_Dict[s_Key] = s_Val;
            }
            return i_Dict;
        }

        /// <summary>
        /// Does not throw
        /// </summary>
        public static bool MoveToRecycler(Form i_Owner, String s_Path)
        {
            try
            {
                if (File.Exists(s_Path))
                    FileSystem.DeleteFile(s_Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                
                return true;
            }
            catch (Exception Ex)
            {
                ShowExceptionBox(i_Owner, Ex, "Error moving file to recycler. It is probably open.\n" + s_Path);
                return false;
            }
        }
    }
}
