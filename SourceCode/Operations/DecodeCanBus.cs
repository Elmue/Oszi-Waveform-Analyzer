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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using IOperation        = Operations.OperationManager.IOperation;
using RtfDocument       = OsziWaveformAnalyzer.RtfDocument;
using RtfBuilder        = OsziWaveformAnalyzer.RtfBuilder;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using Utils             = OsziWaveformAnalyzer.Utils;
using OsziPanel         = OsziWaveformAnalyzer.OsziPanel;
using PlatformManager   = Platform.PlatformManager;

namespace Operations
{
    public partial class DecodeCanBus : Form, IOperation
    {
        #region enums

        [FlagsAttribute]
        enum ePackFlags
        {
            // MUST be Bit 0 ! (Index for FRAME_BITS)
            [Description("Extended ID (29 bit)")]
            IDE = 0x01, 

            // MUST be Bit 1 ! (Index for FRAME_BITS)
            [Description("CAN FD frame (Flexible Datarate Frame)")]
            FDF = 0x02, 

            [Description("CAN FD frame with Baud Rate Switch")]
            BRS = 0x04,
            
            [Description("CAN FD frame with Error State Indicator")]
            ESI = 0x08,
            
            [Description("Remote Request Frame (no data)")]
            RTR = 0x10,
            
            [Description("Packet has been acknowledged")]
            ACK = 0x20,
        }

        [FlagsAttribute]
        enum eError
        {
            [Description("Wrong timing detected (Baudrate, Samplepoint)")]
            Timing    = 0x01,
            
            [Description("Invalid CRC of the packet")]
            CRC       = 0x02,

            [Description("Invalid status of a fix bit (e.g. a Delimiter)")]
            BitStatus = 0x04,

            [Description("5-bit dynamic Stuff Bit (SB) error")]
            DynStuff  = 0x08,
            
            [Description("4-bit Fix Stuff Bit (FSB) error (CAN FD)")]
            FixStuff  = 0x10,
            
            [Description("Invalid Stuff Bit Count or Parity (CAN FD)")]
            STC       = 0x20,
        }

        enum eState
        {
            Header,
            Data,
            CRC,
            Trailer,
            Finished,
        }

        #endregion

        #region class Timing

        /// <summary>
        /// The baudrate is switched exactly at the sample point in the bits "BRS" and the CRC Delimiter "DL1".
        /// See "Bosch CAN FD.pdf" page 28 in subfolder "Documentation"
        /// This document uses for Standard baudrate 11 / 15 time quantums (73.333 %) as sample point
        /// and for High baudrate 6 / 10 time quantums (60 %)
        /// IMPORTANT:
        /// For CAN FD the SamplePoint must be configured to the same position in all nodes of the network.
        /// This is an extremely critical settings. A wrong sample point results in a wrong bit length of the BRS and DL1 bits.
        /// </summary>
        class Timing
        {
            public  double md_SmplPerBit;   // switched between md_SmplBitStd and md_SmplBitFD
            private double md_SmplBitStd;   // samples per bit for Standard baudrate
            private double md_SmplBitFD;    // samples per bit for CAN FD High baudrate

            private double md_PointFactor;  // switched between md_PointStd and md_PointFD
            private double md_PointStd;     // sample point for Standard baudrate
            private double md_PointFD;      // sample point for CAN FD baudrate

            private double md_BitStart;     //   0%   Sample == start of bit
            private double md_SteadyStart;  //  20%   Sample --> after this sample a bit status change is not allowed, otherwise eError.Baudrate
            private double md_SamplePoint;  //  87.5% Sample
            private double md_SteadyEnd;    //  80%   Sample --> after this sample a bit status change is allowed again
            public  double md_BitEnd;       // 100%   Sample == end of bit

            private bool   mb_LongerACK;    // In CAN FD the DL1 bit may be 1 or 2 bits long --> increase SteadyStart of ACK bit.

            public int BitStart    { get { return (int)(md_BitStart    + 0.5); }}
            public int SteadyStart { get { return (int)(md_SteadyStart + 0.5); }}
            public int SamplePoint { get { return (int)(md_SamplePoint + 0.5); }}
            public int SteadyEnd   { get { return (int)(md_SteadyEnd   + 0.5); }}
            public int BitEnd      { get { return (int)(md_BitEnd      + 0.5); }}

            public Timing(double d_SplBitStd, double d_SplBitFD, double d_PointStd, double d_PointFD)
            {
                md_SmplBitStd = d_SplBitStd;
                md_SmplBitFD  = d_SplBitFD;
                md_PointStd   = d_PointStd / 100.0; // Percent --> Factor
                md_PointFD    = d_PointFD  / 100.0; // Percent --> Factor
            }

            /// <summary>
            /// ATTENTION:
            /// This function returns wrong End values for the bits BRS and DL1 where baurate switching takes place.
            /// These bits must be adapted by calling SwitchBaudrate() exactly at the sample point
            /// </summary>
            public void CalcSamples(double d_BitStart)
            {
                // IMPORTANT:
                // The Steady Start / End are only to detect if the user has chosen a wrong baudrate.
                // It has no meaning for the decoding.
                // Do not use a narrower range (10 % ... 90 %) because it results in errors when
                // the A/D conversion was not made with extreme precision or insufficient samples are available.
                md_BitStart    = d_BitStart;
                md_SteadyStart = d_BitStart + md_SmplPerBit * 0.20;           // 20 %
                md_SamplePoint = d_BitStart + md_SmplPerBit * md_PointFactor; // 87.5 %
                md_SteadyEnd   = d_BitStart + md_SmplPerBit * 0.80;           // 80 %
                md_BitEnd      = d_BitStart + md_SmplPerBit;

                // Bosch documentation says:
                // "In CAN FD the CRC DELIMITER (DL1) may consist of one or two recessive bits"
                // --> allow the following ACK bit to go dominant one entire bit later than usual.
                if (mb_LongerACK)
                {
                    mb_LongerACK = false;
                    md_SteadyStart += md_SmplBitFD;
                }
            }

            /// <summary>
            /// Reset to settings for CAN Standard
            /// </summary>
            public void Reset()
            {
                md_SmplPerBit  = md_SmplBitStd;
                md_PointFactor = md_PointStd;
            }

            /// <summary>
            /// This must be called exactly at the sample point of BRS and DL1.
            /// Call this only for CAN FD and only if baud rate is switched.
            /// </summary>
            public void SwitchBaudrate(bool b_High)
            {
                if (b_High) // switch to CAN FD High baudrate at BRS
                {
                    md_SmplPerBit  = md_SmplBitFD;
                    md_PointFactor = md_PointFD;
                }
                else // switch to CAN standard baudrate at DL1
                {
                    md_SmplPerBit  = md_SmplBitStd;
                    md_PointFactor = md_PointStd;
                    mb_LongerACK   = true; // next bit will be ACK
                }

                // Allow bit state change immediatly to avoid errors during the bits BRS and DL1.
                md_SteadyEnd = md_SamplePoint; 

                // If samplepoint == 87.5%:  BitEnd = 87.5% of baudrate 1  +  12.5% of baudrate 2.
                md_BitEnd = md_SamplePoint + md_SmplPerBit * (1.0 - md_PointFactor);
            }
        }

        #endregion

        #region class CanPacket

        class CanPacket
        {
            public ePackFlags     me_Flags;
            public eError         me_Error;
            public int            ms32_ID;
            public int            ms32_DLC;
            public int            ms32_CRC; // CRC read from CAN bus
            public List<Byte>     mi_Data  = new List<Byte>();
            public List<SmplMark> mi_Marks = new List<SmplMark>();
            public int            ms32_StartSample; // The start sample of the SOF bit
            public int            ms32_EndSample;

            public bool Is29Bit
            {
                get { return (me_Flags & ePackFlags.IDE) > 0; }
            }
            public bool IsCanFD
            {
                get { return (me_Flags & ePackFlags.FDF) > 0; }
            }
            public bool IsRemote
            {
                get { return (me_Flags & ePackFlags.RTR) > 0; }
            }
            public bool SwitchBaudrate
            {
                get { return (me_Flags & ePackFlags.BRS) > 0; }
            }
            public bool HasError
            {
                get { return me_Error > 0; }
            }
        }

        #endregion

        #region class CanCrc

        /// <summary>
        /// See "Bosch CAN FD.pdf" page 14 and "ISO 11898-1 (2015) CRC.png" in subfolder "Documentation"
        /// </summary>
        class CanCrc
        {
            int ms32_CRC, ms32_Poly, ms32_HiBit, ms32_Mask;

            public CanCrc(CanPacket i_Packet)
            {
                int s32_CrcLen = 15;
                if (i_Packet.IsCanFD)
                    s32_CrcLen = i_Packet.ms32_DLC > 16 ? 21 : 17;

                ms32_HiBit =  1 << (s32_CrcLen - 1); // 15 bit --> 0x4000
                ms32_Mask  = (1 << s32_CrcLen) - 1;  // 15 bit --> 0x7FFF

                switch (s32_CrcLen)
                {
                    case 15: ms32_Poly = 0x004599; ms32_CRC = 0;          break; // CAN classic
                    case 17: ms32_Poly = 0x01685B; ms32_CRC = ms32_HiBit; break; // CAN FD
                    case 21: ms32_Poly = 0x102899; ms32_CRC = ms32_HiBit; break; // CAN FD
                }
            }

            public void Calculate(bool b_Bit)
            {
                bool b_Highest = (ms32_CRC & ms32_HiBit) > 0;
                ms32_CRC <<= 1;
                if (b_Highest ^ b_Bit)
                    ms32_CRC  ^= ms32_Poly; 
            }

            public int Finish()
            {
                return ms32_CRC & ms32_Mask;
            }
        }

        #endregion

        #region BIT Names

        // See "Bosch CAN FD.pdf" page 10 in subfolder "Documentation"
        // Note: The FDF bit may also be called EDL.
        // IMPORTANT: Do  not rename any of the bits! You will break the code!
        static String[] HEADER_BITS = new String[]
        {
            // Index 0 (No flags):  CAN Classic 11 Bit
            "SOF,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,RTR,IDE,FDF,DLC,DLC,DLC,DLC",
            // Index 1 (IDE flag):  CAN Classic 29 Bit
            "SOF,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,SRR,IDE,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,RTR,FDF,r0,DLC,DLC,DLC,DLC",
            // Index 2 (FDF flag):  CAN FD 11 Bit
            "SOF,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,r1,IDE,FDF,r0,BRS,ESI,DLC,DLC,DLC,DLC",
            // Index 3 (IDE + FDF): CAN FD 29 Bit
            "SOF,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,SRR,IDE,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,ID,r1,FDF,r0,BRS,ESI,DLC,DLC,DLC,DLC",
        };

        // RTR is bit 12 (must later be renamed into SRR)
        const int RTR_OFFSET = 12;

        static String DATA_BITS    = "D7,D6,D5,D4,D3,D2,D1,D0";

        // CAN Classic 15 bit CRC
        static String CRC_15_BITS  = "CRC,CRC,CRC,CRC,CRC,CRC,CRC,CRC,CRC,CRC,CRC,CRC,CRC,CRC,CRC";
        // CAN FD StuffCount + Parity + 17 bit CRC + Fix Stuff Bits
        static String CRC_17_BITS  = "FSB,STC,STC,STC,PAR,FSB,CRC,CRC,CRC,CRC,FSB,CRC,CRC,CRC,CRC,FSB,CRC,CRC,CRC,CRC,FSB,CRC,CRC,CRC,CRC,FSB,CRC";
        // CAN FD StuffCount + Parity + 21 bit CRC + Fix Stuff Bits
        static String CRC_21_BITS  = "FSB,STC,STC,STC,PAR,FSB,CRC,CRC,CRC,CRC,FSB,CRC,CRC,CRC,CRC,FSB,CRC,CRC,CRC,CRC,FSB,CRC,CRC,CRC,CRC,FSB,CRC,CRC,CRC,CRC,FSB,CRC";

        // DL1 = Delimiter after CRC --> switch back to classic baudrate
        // DL2 = Delimiter after ACK
        static String TRAILER_BITS = "DL1,ACK,DL2,EOF,EOF,EOF,EOF,EOF,EOF,EOF";

        #endregion

        const int MIN_SAMPLES_BIT = 12; // minimum sample resolution
        Brush BRUSH_STUFF = new SolidBrush(Color.FromArgb(0x77, 0x55, 0xFF));
        Brush BRUSH_ID    = Brushes.Orange;
        Brush BRUSH_DLC   = Brushes.Magenta;
        Brush BRUSH_DATA  = Brushes.Yellow;
        Brush BRUSH_CRC   = Brushes.Cyan;

        static Dictionary<String, String> mi_DemoFiles = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Static Constructor
        /// </summary>
        static DecodeCanBus()
        {
            mi_DemoFiles.Add("CAN Classic ISO 15765 500k Baud 11 bit.oszi", "500 k");
            mi_DemoFiles.Add("CAN Classic SAE J1939 500k Baud 29 bit.oszi", "500 k");
            mi_DemoFiles.Add("CAN FD Test 500k & 2M Baud 11 + 29 bit.oszi", "2 M");

            foreach (String s_DemoFile in mi_DemoFiles.Keys)
            {
                Debug.Assert(File.Exists(Utils.SampleDir + '\\' + s_DemoFile), "File does not exist: " + s_DemoFile);
            }
        }

        eState    me_State;        // state machine
        bool      mb_BitStuffing;  // Bit stuffing is turned off at the end of the frame
        int       ms32_SmplPerBit; // highest Samples per bit (FD baudrate)
        int       ms32_PrevValue;  // The value of the previous bit (0 or 1)
        int       ms32_CurBit;     // Current bit index in ms_BitNames
        String[]  ms_BitNames;     // FRAME_BITS, DATA_BITS, CRC_XX_BITS, TRAILER_BITS
        SmplMark  mi_SmplID;       // Mark Row 2
        SmplMark  mi_SmplDLC;      // Mark Row 2
        SmplMark  mi_SmplData;     // Mark Row 2
        SmplMark  mi_SmplCRC;      // Mark Row 2
        Timing    mi_Timing;
        CanPacket mi_Packet;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null || b_Analog)
                return;

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText   = "Decode CAN bus";
            i_Item.ms_ImageFile  = "CanBus.ico";

            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            InitializeComponent();
            if (ShowDialog(Utils.FormMain) == System.Windows.Forms.DialogResult.OK)
            {
                Utils.StartBusyOperation(null); // show wait cursor

                CanPacket[] i_Packets = DecodePackets(i_Channel);
                ShowRtf(i_Packets);
                Utils.OsziPanel.RecalculateEverything();

                Utils.EndBusyOperation(null);

                return i_Packets.Length + " CAN Bus packets detected";
            }
            return null; // user closed the window
        }

        protected override void OnLoad(EventArgs e)
        {
 	        base.OnLoad(e);

            comboBaudStd.Text = Utils.RegReadString(eRegKey.CanBaudStd,     "500 k");
            comboBaudFD .Text = Utils.RegReadString(eRegKey.CanBaudFD,      "500 k");
            textSmplStd .Text = Utils.RegReadString(eRegKey.CanSplPointStd, "87.5");
            textSmplFD  .Text = Utils.RegReadString(eRegKey.CanSplPointFD,  "75.0");

            // Load the correct settings for the demo files
            String s_BaudFD;
            if (OsziPanel.CurCapture.ms_Path != null && 
                mi_DemoFiles.TryGetValue(Path.GetFileName(OsziPanel.CurCapture.ms_Path), out s_BaudFD))
            {
                comboBaudStd.Text = "500 k";
                comboBaudFD .Text = s_BaudFD;
                textSmplStd .Text = "87.5";
                textSmplFD  .Text = "75.0";
                radioIdleHigh.Checked = true;
            }
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "DecodeCAN");
        }

        private void btnDecode_Click(object sender, EventArgs e)
        {
            double d_PointStd, d_PointFD;
            double d_SplBitStd = GetSamplesPerBit(comboBaudStd, eRegKey.CanBaudStd, textSmplStd, eRegKey.CanSplPointStd, out d_PointStd);
            if (d_SplBitStd == 0.0)
                return;
            double d_SplBitFD  = GetSamplesPerBit(comboBaudFD,  eRegKey.CanBaudFD,  textSmplFD,  eRegKey.CanSplPointFD,  out d_PointFD);
            if (d_SplBitFD == 0.0)
                return;

            if (d_SplBitFD > d_SplBitStd)
            {
                MessageBox.Show(this, "The CAN FD baudrate cannot be lower than the CAN Standard baudrate.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Used to adjust Display Factor
            ms32_SmplPerBit = (int)(d_SplBitFD + 0.5);

            mi_Timing = new Timing(d_SplBitStd, d_SplBitFD, d_PointStd, d_PointFD);
            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// returns 0 on error
        /// </summary>
        double GetSamplesPerBit(ComboBox i_ComboBaud, eRegKey e_RegBaud, TextBox i_TextSmpl, eRegKey e_RegSmpl, out double d_SamplePoint)
        {
            d_SamplePoint = 0;

            String s_Baud = i_ComboBaud.Text.Replace(" ", "").ToUpper();
            s_Baud = s_Baud.Replace("K", "000");
            s_Baud = s_Baud.Replace("M", "000000");

            int s32_Baud;
            if (!int.TryParse(s_Baud, out s32_Baud) || s32_Baud < 1000 || s32_Baud > 100000000)
            {
                MessageBox.Show(this, "Enter a valid baudrate.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0.0;
            }

            Utils.RegWriteString(e_RegBaud, i_ComboBaud.Text);

            double d_SamplesPerBit = (double)(Utils.PICOS_PER_SECOND / (OsziPanel.CurCapture.ms64_SampleDist * s32_Baud));
            if (d_SamplesPerBit < MIN_SAMPLES_BIT)
            {
                String s_Msg = String.Format("The resolution of the capture is too low for a reliable detection with {0:N0} baud.", s32_Baud);
                MessageBox.Show(this, s_Msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0.0;
            }

            if (!Double.TryParse(i_TextSmpl.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out d_SamplePoint) ||
                d_SamplePoint < 50 || d_SamplePoint > 90)
            {
                MessageBox.Show(this, "Enter a valid sample point between 50 % and 90 %", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0.0;
            }

            return d_SamplesPerBit;
        }

        // =======================================================================================================

        /// <summary>
        /// CAN FD switches the baudrate twice in the middle of the packet.
        /// This makes decoding complicated and requires a complex state machine.
        /// https://en.wikipedia.org/wiki/CAN_bus
        /// https://www.csselectronics.com/pages/can-fd-flexible-data-rate-intro
        /// https://sunnygiken.jp/product/can-fd-tool/about_canfd/
        /// And see subfolder "Documentation" !!
        /// </summary>
        CanPacket[] DecodePackets(Channel i_Channel)
        {
            List<CanPacket> i_Packets  = new List<CanPacket>();
            List<SmplMark>  i_MarkRow1 = new List<SmplMark>();
            List<SmplMark>  i_MarkRow2 = new List<SmplMark>();
            
            i_Channel.mi_MarkRows = new List<SmplMark>[] { i_MarkRow1, i_MarkRow2 };
            MemoryStream i_Stream = new MemoryStream(i_Channel.mu8_Digital);

            int s32_IdleState = radioIdleHigh.Checked ? 1 : 0;
            int s32_State     = -1; // invalid value

            // packet loop
            while (true)
            {
                // ------ Find idle status (recessive) --------

                while (s32_State != s32_IdleState)
                {
                    s32_State = i_Stream.ReadByte();
                    if (s32_State < 0)
                        return i_Packets.ToArray(); // end of stream
                }

                // --------- Find SOF bit (dominant) ----------

                while (s32_State == s32_IdleState)
                {
                    s32_State = i_Stream.ReadByte();
                    if (s32_State < 0)
                        return i_Packets.ToArray(); // end of stream
                }

                mb_BitStuffing  = true; // 5-Bit Stuffing is used until CRC (CAN FD) or until Trailer (CAN Classic)
                ms32_CurBit     = 0;    // Current bit in ms_BitNames
                ms32_PrevValue  = -1;
                ms_BitNames     = null; // Names of all bits of the current state
                me_State        = eState.Header;
                mi_Packet       = new CanPacket();
                mi_SmplID       = null; // Mark Row 2
                mi_SmplDLC      = null; // Mark Row 2
                mi_SmplData     = null; // Mark Row 2
                mi_SmplCRC      = null; // Mark Row 2

                mi_Timing.Reset(); // set standard baudrate

                mi_Packet.ms32_StartSample = (int)i_Stream.Position - 1; // sample where packet starts (Stream points already to next bit)
                double d_BitStart          = (int)i_Stream.Position - 1; 
                int s32_IdleCount          = 0;
                int s32_StuffCount         = 1;  // Count of bits before Stuff Bit
                int s32_StuffState         = -1; // invalid

                // bit loop (state machine)
                while (me_State != eState.Finished)
                {
                    mi_Timing.CalcSamples(d_BitStart);

                    // add one mark for each bit in mark row 1
                    SmplMark i_Mark = new SmplMark(eMark.Text, mi_Timing.BitStart, mi_Timing.BitEnd);

                    bool b_TimingError = false;

                    // sample loop: reads one bit
                    while (true)
                    {
                        int s32_CurSample = (int)i_Stream.Position - 1; // (Stream points already to next bit)
                        if (s32_CurSample >= mi_Timing.BitEnd)
                            break;

                        int s32_NewState = i_Stream.ReadByte();
                        if (s32_NewState < 0)
                            return i_Packets.ToArray(); // end of stream

                        // ---------------- State Change -----------------

                        if (s32_State != s32_NewState) // data has changed
                        {
                            s32_State = s32_NewState;
                            b_TimingError = s32_CurSample > mi_Timing.SteadyStart && s32_CurSample < mi_Timing.SteadyEnd;

                            // See "Bosch CAN FD - Timing Requirements.pdf" in subfolder "Documentation" page 04-3
                            // CAN nodes synchronize on received edges from recessive to dominant.
                            // The dominant bit after FDF is hard synchronization.
                            if (s32_NewState == 0) // 0 = dominant
                            {
                                if (s32_CurSample > mi_Timing.SamplePoint) 
                                {
                                    // Bit change is from the next bit
                                    mi_Timing.md_BitEnd = (int)s32_CurSample; // loaded into d_BitStart at end of loop
                                }
                                else 
                                {
                                    // Bit change is from the current bit
                                    mi_Timing.md_BitEnd     = (int)(s32_CurSample + mi_Timing.md_SmplPerBit + 0.5);
                                    i_Mark.ms32_FirstSample = s32_CurSample;
                                }
                            }
                        }

                        // ---------------- Samplepoint -----------------

                        // Take the bit status at the sample point
                        if (s32_CurSample == mi_Timing.SamplePoint)
                        {
                            bool b_Idle = s32_State == s32_IdleState;
                            i_Mark.ms32_Value = b_Idle ? 1 : 0;

                            if (b_Idle) s32_IdleCount ++;
                            else        s32_IdleCount = 0;

                            // CAN Classic: 5-Bit Stuffing ends after CRC
                            // CAN FD:      5-Bit Stuffing ends after Data
                            if (mb_BitStuffing)
                            {
                                if (s32_StuffCount == 5)
                                {
                                    if (s32_StuffState != s32_State) 
                                    {
                                        i_Mark.mi_TxtBrush = BRUSH_STUFF;
                                    }
                                    else // Stuffing error
                                    {
                                        mi_Packet.me_Error |= eError.DynStuff;
                                        i_Mark.mi_TxtBrush  = Utils.ERROR_BRUSH;
                                    }
                                    i_Mark.ms_Text = "SB"; // Stuff Bit
                                }

                                if (s32_StuffState == s32_State) s32_StuffCount ++;
                                else                             s32_StuffCount = 1;

                                s32_StuffState = s32_State;
                            }

                            // The following functions must be called exactly at the samplepoint
                            // because they switch the CAN FD baudrate

                            if (i_Mark.ms_Text != "SB") // do not process dynamic stuff bits
                            {
                                switch (me_State) // state machine
                                {
                                    case eState.Header:
                                        ProcessHeader(i_Mark, i_MarkRow2);
                                        break;
                                    case eState.Data:
                                        ProcessData(i_Mark, i_MarkRow2);
                                        break;
                                    case eState.CRC:
                                        ProcessCRC(i_Mark, i_MarkRow2);
                                        break;
                                    case eState.Trailer:
                                        ProcessTrailer(i_Mark); // sets me_State = eState.Finished when done
                                        break;
                                }
                            }
                        }
                    } // sample loop

                    if (b_TimingError)
                    {
                        mi_Packet.me_Error |= eError.Timing;
                        i_Mark.mi_TxtBrush  = Utils.ERROR_BRUSH;
                    }
                                         
                    // correct last sample for bits BRS and DL1
                    i_Mark.ms32_LastSample   = mi_Timing.BitEnd; 
                    mi_Packet.ms32_EndSample = i_Mark.ms32_LastSample;
                    mi_Packet.mi_Marks.Add(i_Mark); 
                    i_MarkRow1.Add(i_Mark);

                    // In case of error --> detect end of frame after 7 idle bits
                    if (mi_Packet.HasError && s32_IdleCount > 6)
                        me_State = eState.Finished;

                    d_BitStart     = mi_Timing.md_BitEnd;
                    ms32_PrevValue = i_Mark.ms32_Value; // only used to verify FSB status
                } // bit loop

                i_Packets.Add(mi_Packet);
            } // packet loop
        }

        // =============================================================================================================

        /// <summary>
        /// i_Mark = next Mark read from the state machine for Mark Row 1
        /// mi_SmplID, i_SmplDLC for Mark Row 2
        /// This function is called exactly at the sample point
        /// </summary>
        void ProcessHeader(SmplMark i_Mark, List<SmplMark> i_MarkRow2)
        {
            if (ms32_CurBit == 0)
                ms_BitNames = HEADER_BITS[0].Split(','); // SOF, ID, etc...

            String s_Name = ms_BitNames[ms32_CurBit++];
            i_Mark.ms_Text = s_Name;

            if (i_Mark.ms32_Value == 1) // recessive
            {
                if (s_Name == "RTR") mi_Packet.me_Flags |= ePackFlags.RTR; // Remote frame Request
                if (s_Name == "IDE") mi_Packet.me_Flags |= ePackFlags.IDE; // Extended ID
                if (s_Name == "FDF") mi_Packet.me_Flags |= ePackFlags.FDF; // FD frame
                if (s_Name == "ESI") mi_Packet.me_Flags |= ePackFlags.ESI; // Error flag

                if (s_Name == "BRS") 
                { 
                    mi_Packet.me_Flags |= ePackFlags.BRS; 
                    mi_Timing.SwitchBaudrate(true);  // switch to High baudrate
                    i_Mark.mi_PenEnd = Pens.Magenta; // display the calculated end of the BRS bit in magenta
                } 
                                
                if (s_Name == "IDE" || s_Name == "FDF")
                {
                    // switch the type of the packet based on bits 0 and 1 of mi_Packet.me_Flags
                    ms_BitNames = HEADER_BITS[(int)mi_Packet.me_Flags & 3].Split(',');

                    // Extended Frame : the RTR bit comes after the ID --> reset here, set later again
                    // FD Frame:        the RTR bit is not used --> reset always
                    mi_Packet.me_Flags &= ~ePackFlags.RTR; 
                }
            }

            switch (i_Mark.ms_Text)
            {
                case "ID":  
                    i_Mark.mi_TxtBrush = BRUSH_ID; 
                    mi_Packet.ms32_ID <<= 1;
                    mi_Packet.ms32_ID |= i_Mark.ms32_Value;

                    if (mi_SmplID == null) mi_SmplID = new SmplMark(eMark.Text, i_Mark.ms32_FirstSample);
                    mi_SmplID.ms32_LastSample = i_Mark.ms32_LastSample;                   
                    break;

                case "DLC": 
                    i_Mark.mi_TxtBrush = BRUSH_DLC;   
                    mi_Packet.ms32_DLC <<= 1;
                    mi_Packet.ms32_DLC |= i_Mark.ms32_Value;

                    if (mi_SmplDLC == null) mi_SmplDLC = new SmplMark(eMark.Text, i_Mark.ms32_FirstSample);
                    mi_SmplDLC.ms32_LastSample = i_Mark.ms32_LastSample;
                    break;
            }

            // ----------------------------------------------------

            // last header bit reached
            if (ms32_CurBit == ms_BitNames.Length)
            {   
                // ---------- Mark Row 1 : RTR ---------

                foreach (SmplMark i_Prev in mi_Packet.mi_Marks) 
                {
                    if (i_Prev.ms_Text == "RTR")
                    {
                        // CAN Classic 29 bit or CAN FD --> rename RTR --> SRR
                        i_Prev.ms_Text = ms_BitNames[RTR_OFFSET];

                        // Bosch documentation says: The Substitute Remote Request (SRR) bit is recessive.
                        if (i_Prev.ms_Text == "SRR")
                        {
                            if (i_Prev.ms32_Value != 1)
                            {
                                i_Prev.mi_TxtBrush = Utils.ERROR_BRUSH;
                                mi_Packet.me_Error |= eError.BitStatus;
                            }
                        }
                        break;
                    }
                }

                // -------- Mark Row 2 : CAN ID --------

                mi_SmplID.ms_Text = mi_Packet.ms32_ID.ToString(mi_Packet.Is29Bit ? "X8" : "X3");
                i_MarkRow2.Add(mi_SmplID);

                // -------- Mark Row 2 : DLC --------

                if (mi_Packet.IsCanFD)
                {
                    switch (mi_Packet.ms32_DLC)
                    {
                        case  9: mi_Packet.ms32_DLC = 12; break;
                        case 10: mi_Packet.ms32_DLC = 16; break;
                        case 11: mi_Packet.ms32_DLC = 20; break;
                        case 12: mi_Packet.ms32_DLC = 24; break;
                        case 13: mi_Packet.ms32_DLC = 32; break;
                        case 14: mi_Packet.ms32_DLC = 48; break;
                        case 15: mi_Packet.ms32_DLC = 64; break;
                    }
                }
               
                mi_SmplDLC.ms_Text = mi_Packet.ms32_DLC.ToString();
                i_MarkRow2.Add(mi_SmplDLC);

                // Switch state machine to DATA phase
                ms32_CurBit = 0;
                me_State ++;

                // Bosch documentation says:
                // For Remote Frames there is no DATA field, independent of the value of DLC.
                // Skip Data phase and switch the state machine to CRC phase
                if (mi_Packet.ms32_DLC == 0 || mi_Packet.IsRemote)
                    me_State ++;
            }
        }

        // =============================================================================================================

        /// <summary>
        /// i_Mark = next Mark read from the state machine for Mark Row 1
        /// mi_SmplData for Mark Row 2
        /// This function is called exactly at the sample point
        /// </summary>
        void ProcessData(SmplMark i_Mark, List<SmplMark> i_MarkRow2)
        {
            if (ms32_CurBit == 0)
            {
                ms_BitNames = DATA_BITS.Split(','); // D0, D1, D2, etc...
                mi_SmplData = new SmplMark(eMark.Text, i_Mark.ms32_FirstSample);
            }

            mi_SmplData.ms32_LastSample = i_Mark.ms32_LastSample;

            i_Mark.ms_Text     = ms_BitNames[ms32_CurBit++];
            i_Mark.mi_TxtBrush = BRUSH_DATA;

            mi_SmplData.ms32_Value <<= 1;
            mi_SmplData.ms32_Value |= i_Mark.ms32_Value;

            // ----------------------------------------------------

            // last data bit reached
            if (ms32_CurBit == ms_BitNames.Length)
            {
                mi_SmplData.ms_Text = mi_SmplData.ms32_Value.ToString("X2");
                mi_Packet.mi_Data.Add((Byte)mi_SmplData.ms32_Value);
                i_MarkRow2.Add(mi_SmplData);

                ms32_CurBit = 0;

                // Switch state machine to CRC phase
                if (mi_Packet.mi_Data.Count == mi_Packet.ms32_DLC)
                {
                    me_State ++;

                    // CAN FD turns off 5-Bit Stuffing after Data phase
                    if (mi_Packet.IsCanFD)
                        mb_BitStuffing = false;
                }
            }
        }

        // =============================================================================================================

        /// <summary>
        /// i_Mark = next Mark read from the state machine for Mark Row 1
        /// mi_SmplCRC, miSmplSTC for Mark Row 2
        /// This function is called exactly at the sample point
        /// </summary>
        void ProcessCRC(SmplMark i_Mark, List<SmplMark> i_MarkRow2)
        {
            if (ms32_CurBit == 0)
            {
                String s_Names = CRC_15_BITS; // CAN Classic
                if (mi_Packet.IsCanFD)
                    s_Names = mi_Packet.ms32_DLC > 16 ? CRC_21_BITS : CRC_17_BITS;

                ms_BitNames = s_Names.Split(',');
            }

            String s_Name = ms_BitNames[ms32_CurBit++];
            i_Mark.ms_Text     = s_Name;
            i_Mark.mi_TxtBrush = BRUSH_CRC;

            // The Fix Stuff Bit is at fix loactions and is always the inversion of the previous bit
            if (s_Name == "FSB") 
            {
                i_Mark.mi_TxtBrush = BRUSH_STUFF;

                if (i_Mark.ms32_Value == ms32_PrevValue)
                {
                    mi_Packet.me_Error |= eError.FixStuff;
                    i_Mark.mi_TxtBrush  = Utils.ERROR_BRUSH;
                }
            }

            if (s_Name == "CRC")
            {
                mi_Packet.ms32_CRC <<= 1;
                mi_Packet.ms32_CRC |= i_Mark.ms32_Value;

                if (mi_SmplCRC == null)
                    mi_SmplCRC = new SmplMark(eMark.Text, i_Mark.ms32_FirstSample);
            }

            if (mi_SmplCRC != null)
                mi_SmplCRC.ms32_LastSample = i_Mark.ms32_LastSample;

            // last CRC bit reached ?
            if (ms32_CurBit < ms_BitNames.Length)
                return;

            // ----------------------------------------------------

            SmplMark i_SmplSTC = null; // Mark Row 2
            int s32_CountSB    =  0;   // Count of 5-Bit Stuff Bits
            int s32_GrayCode   =  0;   // Gray Code value of the 3 STC bits
            int s32_ParityBit  = -1;   // Parity Bit in packet
            int s32_ParityCalc =  0;   // Parity Calculated

            CanCrc i_CRC = new CanCrc(mi_Packet);

            // -------- Calc CRC + STC + PAR --------

            foreach (SmplMark i_Prev in mi_Packet.mi_Marks)
            {
                if (i_Prev.ms_Text == "SB")
                {
                    s32_CountSB ++;
                    if (!mi_Packet.IsCanFD)
                        continue; // CAN classic -> 5-Bit stuff bits are not included in CRC
                }

                // CAN classic: abort CRC calculation before first CRC bit
                if (i_Prev.ms_Text == "CRC")
                    break;

                // ignore FSB bits for CRC
                if (i_Prev.ms_Text != "FSB")
                    i_CRC.Calculate(i_Prev.ms32_Value > 0);

                // process the 3 Stuff Bit Count bits
                if (i_Prev.ms_Text == "STC")
                {
                    if (i_SmplSTC == null)
                        i_SmplSTC = new SmplMark(eMark.Text, i_Prev.ms32_FirstSample);

                    s32_GrayCode  <<= 1;
                    s32_GrayCode   |= i_Prev.ms32_Value;
                    s32_ParityCalc ^= i_Prev.ms32_Value; // even parity
                }

                // process Parity bit
                if (i_Prev.ms_Text == "PAR")
                {
                    i_SmplSTC.ms32_LastSample = i_Prev.ms32_LastSample;

                    s32_ParityBit = i_Prev.ms32_Value;
                    i_Prev.mi_TxtBrush = (s32_ParityBit == s32_ParityCalc) ? Brushes.Lime : Utils.ERROR_BRUSH;

                    // CAN FD: STC and PAR are included into CRC, then abort CRC calculation
                    break;
                }
            }

            // ---------- Check STC ----------

            // The 3 STC bits contain the 3 lower bits of the count of all "SB" stuff bits in the entire packet
            if (i_SmplSTC != null)
            {
                // Convert Gray Code --> STC Count.  https://en.wikipedia.org/wiki/Gray_code
                int s32_STC = -1;
                switch (s32_GrayCode) 
                {
                    case 0: s32_STC = 0; break;
                    case 1: s32_STC = 1; break;
                    case 2: s32_STC = 3; break;
                    case 3: s32_STC = 2; break;
                    case 4: s32_STC = 7; break;
                    case 5: s32_STC = 6; break;
                    case 6: s32_STC = 4; break;
                    case 7: s32_STC = 5; break;
                }

                // Display the expected count of Stuff Bits (lowest 3 bits only)
                i_SmplSTC.ms_Text = s32_STC.ToString();

                if (s32_STC == (s32_CountSB & 7) && s32_ParityBit == s32_ParityCalc)
                {
                    i_SmplSTC.mi_TxtBrush = Brushes.Lime;
                }
                else
                {
                    i_SmplSTC.mi_TxtBrush = Utils.ERROR_BRUSH;
                    mi_Packet.me_Error |= eError.STC;
                }
                i_MarkRow2.Add(i_SmplSTC);
            }

            // ------------ Check CRC ------------

            int s32_Calc = i_CRC.Finish();
            if (mi_Packet.ms32_CRC != s32_Calc)
            {
                mi_Packet.me_Error    |= eError.CRC;
                mi_SmplCRC.mi_TxtBrush = Utils.ERROR_BRUSH;
                mi_SmplCRC.ms_Text = String.Format("{0:X4} != {1:X4}", mi_Packet.ms32_CRC, s32_Calc);
            }
            else 
            {
                mi_SmplCRC.mi_TxtBrush = Brushes.Lime;
                mi_SmplCRC.ms_Text     = String.Format("{0:X4}", mi_Packet.ms32_CRC);
            }
            i_MarkRow2.Add(mi_SmplCRC);

            // Switch state machine to Trailer phase
            ms32_CurBit = 0;
            me_State ++;

            // CAN Classic turns off 5-Bit Stuffing after CRC phase
            mb_BitStuffing = false;
        }

        // =============================================================================================================

        /// <summary>
        /// i_Mark = next Mark read from the state machine for Mark Row 1
        /// This function is called exactly at the sample point
        /// </summary>
        void ProcessTrailer(SmplMark i_Mark)
        {
            if (ms32_CurBit == 0)
                ms_BitNames = TRAILER_BITS.Split(',');

            String s_Name  = ms_BitNames[ms32_CurBit++];
            i_Mark.ms_Text = s_Name;

            if (s_Name == "ACK")
            {
                if (i_Mark.ms32_Value == 0) // ACK
                {
                    i_Mark.mi_TxtBrush  = Brushes.Lime;
                    mi_Packet.me_Flags |= ePackFlags.ACK; // dominant
                }
                else // No ACK
                {
                    i_Mark.mi_TxtBrush = Utils.ERROR_BRUSH;
                }
            }

            if (s_Name == "DL1" && mi_Packet.SwitchBaudrate)
            {
                mi_Timing.SwitchBaudrate(false); // switch back to standard baudrate
                i_Mark.mi_PenEnd = Pens.Magenta; // display the calculated end of the DL1 bit in magenta
            }

            switch (s_Name)
            {
                case "DL1":
                case "DL2":
                case "EOF":
                    if (i_Mark.ms32_Value != 1) // these bits must be always recessive
                    {
                        i_Mark.mi_TxtBrush  = Utils.ERROR_BRUSH;
                        mi_Packet.me_Error |= eError.BitStatus;
                    }
                    break;
            }

            // Switch state machine to Finished
            if (ms32_CurBit == ms_BitNames.Length)
                me_State ++;
        }

        // =============================================================================================================

        void ShowRtf(CanPacket[] i_Packets)
        {
            int s32_Errors = 0;

            // Display DLC only with 2 digits for CAN FD
            int s32_DlcDigits = 1;
            foreach (CanPacket i_Pack in i_Packets)
            {
                if (i_Pack.ms32_DLC > 9)
                    s32_DlcDigits = 2;
            }

            RtfDocument i_RtfDoc = new RtfDocument(Color.White);
            RtfBuilder i_Builder = i_RtfDoc.CreateNewBuilder();

            i_Builder.AppendText(Color.White, "Packet Flags:\n", FontStyle.Underline);
            i_Builder.AppendEnum(Color.Lime,              11, Color.White, typeof(ePackFlags));
            i_Builder.AppendEnum(Utils.ERROR_COLOR, 11, Color.White, typeof(eError));

            i_Builder.AppendText(Color.White, "\n\nDecoded Packets:\n", FontStyle.Underline);
            foreach (CanPacket i_Pack in i_Packets)
            {
                i_Builder.AppendTimestampLine(i_Pack.ms32_StartSample, i_Pack.ms32_EndSample, true);

                String s_ID = i_Pack.ms32_ID.ToString(i_Pack.Is29Bit ? "X8" : "X3");
                i_Builder.AppendText(Color.Yellow, s_ID + ": ");

                i_Builder.AppendText(Color.Magenta, "[" + i_Pack.ms32_DLC.ToString().PadLeft(s32_DlcDigits) + "] ");

                foreach (Byte u8_Byte in i_Pack.mi_Data)
                {
                    i_Builder.AppendFormat(Color.White, "{0:X2} ", u8_Byte);
                }
                i_Builder.AppendText("  ");

                if (i_Pack.me_Flags > 0)
                    i_Builder.AppendText(Color.Lime, i_Pack.me_Flags.ToString());
                
                if (i_Pack.me_Error > 0)
                {
                    i_Builder.AppendText(Utils.ERROR_COLOR, " " + i_Pack.me_Error.ToString());
                    s32_Errors ++;
                }

                i_Builder.AppendText("\n");
            }

            if (s32_Errors > 1)
            {
                i_Builder.AppendText(Utils.ERROR_COLOR, "\nMultiple errors detected.\nEither the signal is invalid or the settings are wrong.\n");
            }
            else if (i_Packets.Length == 0)
            {
                i_Builder.AppendLine(Utils.ERROR_COLOR, "\nNothing detected");
            }
            else if (s32_Errors == 0)
            {
                i_Builder.AppendText(Color.Lime, "\nNo errors detected.\n");
            }

            // Show RTF, if created and switch to tab "Decoder"
            Utils.FormMain.ShowAnalysisResult(i_RtfDoc, ms32_SmplPerBit); 
        }
    }
}

