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
    /// <summary>
    /// Read Part 3 in 'USB Tutorial.chm' in subfolder 'Documentation'
    /// </summary>
    public class DecodeUSB : IOperation
    {
        #region enums

        enum eSpeed
        {
            Low,  // 1.5 MBit
            Full, // 12  MBit
            High, // 480 MBit
        }
        
        enum eBusState
        {
            Zero    =  0x01, // D+ low,  D- high  (Differential '0')
            One     =  0x10, // D+ high, D- low   (Differential '1')
            SE0     =  0x00, // Both low          (Single Ended Zero)
            SE1     =  0x11, // Both high         (Single Ended One)  This state is illegal!
            // ------------
            K       = Zero,
            J       = One,
            Idle    = One,
        }

        enum eParse
        {
            Sync,     // 8 bit
            PID,      // 4+4 bit
            BusAddr,  //  7 bit (Device address on the USB bus, assigned by host computer, IN, OUT, SETUP)
            Endpt,    //  4 bit (Endpoint IN / OUT)
            Frame,    // 11 bit (SOF)
            Data,     //  8 bit
            CRC5,     //  5 bit
            EOP,      //  3 bit
            Finished,
        }

        enum ePid
        {
            Invalid = -1,
            // Token
            OUT   = 0x01,
            IN    = 0x09,
            SOF   = 0x05,
            SETUP = 0x0D,
            // Data
            DATA0 = 0x03,
            DATA1 = 0x0B,
            DATA2 = 0x07, // not used for Full Speed
            MDATA = 0x0F, // not used for Full Speed
            // Handshake
            ACK   = 0x02,
            NAK   = 0x0A,
            STALL = 0x0E,
            NYET  = 0x06, // not used for Full Speed
            // Special
            RESV  = 0x00, // not used for Full Speed
            PRE   = 0x0C, // not used for Full Speed
            SPLIT = 0x08, // not used for Full Speed
            PING  = 0x04, // not used for Full Speed
        };

        [FlagsAttribute]
        enum eError
        {
            [Description("Wrong timing detected")]
            Timing = 0x01,
            
            [Description("Invalid SE1 state")]
            SE1    = 0x02,

            [Description("Stuff Bit (SB) error")]
            Stuff  = 0x04,

            [Description("Invalid Sync pattern")]
            Sync   = 0x08,

            [Description("Corrupt PID")]
            PID    = 0x10,

            [Description("Corrupted Data")]
            Data   = 0x20,

            [Description("Invalid CRC")]
            CRC    = 0x40,
        }

        enum eSetupRecip : byte // Bits 0,1,2,3,4 of u8_RequestType
        {
            Device    = 0x00,
            Interface = 0x01,
            Endpoint  = 0x02,
            Other     = 0x03,
            MASK      = 0x1F,
        };
    
        enum eSetupType : byte  // Bits 5,6 of u8_RequestType
        {
            Standard = 0x00, // 0 << 5
            Class    = 0x20, // 1 << 5
            Vendor   = 0x40, // 2 << 5
            MASK     = 0x60,
        };

        enum eDirection : byte  // Bit 7 of u8_RequestType, also used for endpoints
        {
            Out  = 0x00,
            In   = 0x80,
            MASK = 0x80,
        };

        #endregion

        #region embedded classes

        class Timing
        {
            public  double   md_SmplPerBit;
            private double   md_BitStart;     //   0%   Sample == start of bit
            private double   md_SteadyStart;  //  20%   Sample --> after this sample a bit status change is not allowed, otherwise Error
            private double   md_SamplePoint;  //  50%   Sample --> capture status
            private double   md_SteadyEnd;    //  80%   Sample --> after this sample a bit status change is allowed again
            public  double   md_BitEnd;       // 100%   Sample == end of bit

            public int BitStart    { get { return (int)(md_BitStart    + 0.5); }}
            public int SteadyStart { get { return (int)(md_SteadyStart + 0.5); }}
            public int SamplePoint { get { return (int)(md_SamplePoint + 0.5); }}
            public int SteadyEnd   { get { return (int)(md_SteadyEnd   + 0.5); }}
            public int BitEnd      { get { return (int)(md_BitEnd      + 0.5); }}

            public Timing(eSpeed e_Speed)
            {
                switch (e_Speed)
                {
                    // 12 MHz = 83.3333 ns
                    case eSpeed.Full:
                        md_SmplPerBit = 1000000.0 / 12.0 / OsziPanel.CurCapture.ms64_SampleDist;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (md_SmplPerBit < MIN_SAMPLES_BIT)
                    throw new Exception("The resolution of the capture is too low for a reliable detection.");
            }

            public void CalcSamples(double d_BitStart)
            {
                // IMPORTANT:
                // The Steady Start / End are only to detect if the signal is corrupt
                // It has no meaning for the decoding.
                // Do not use a narrower range (10 % ... 90 %) because it results in errors when
                // the A/D conversion was not made with extreme precision or insufficient samples are available.
                md_BitStart    = d_BitStart;
                md_SteadyStart = d_BitStart + md_SmplPerBit * 0.20; // 20 %
                md_SamplePoint = d_BitStart + md_SmplPerBit * 0.50; // 50 %
                md_SteadyEnd   = d_BitStart + md_SmplPerBit * 0.80; // 80 %
                md_BitEnd      = d_BitStart + md_SmplPerBit;
            }
        }

        class UsbPacket
        {
            public ePid           me_PID;
            public eError         me_Error;
            public int            ms32_BusAddr  = -1;
            public int            ms32_Endpoint = -1;
            public int            ms32_SofFrame = -1;
            public int            ms32_StartSample; // The start sample of the SOF bit
            public int            ms32_EndSample;
            public Setup          mi_Setup;
            public List<Byte>     mi_Data  = new List<Byte>();
            public List<SmplMark> mi_Marks = new List<SmplMark>();

            /// <summary>
            /// Abort parsing further bits after severe errors
            /// </summary>
            public bool HasError
            {
                get { return me_Error > 0; }
            }

            public String DataLen
            {
                get
                {
                    if (mi_Data.Count == 0)
                        return "---";
                    else
                        return "[" + mi_Data.Count + "]";
                }
            }

            public override string ToString()
            {
                StringBuilder i_Builder = new StringBuilder();
                if (ms32_BusAddr  >= 0)   i_Builder.AppendFormat("Adr: {0}",   ms32_BusAddr);  // optional
                if (ms32_Endpoint >= 0)   i_Builder.AppendFormat(", EP: {0}",  ms32_Endpoint); // optional
                if (i_Builder.Length > 0) i_Builder.Append(", ");
                i_Builder.Append(me_PID.ToString());
                if (me_Error      >  0) i_Builder.AppendFormat(", Error: {0}", me_Error);
                if (ms32_SofFrame >= 0) i_Builder.AppendFormat(", Frame: {0}", ms32_SofFrame);
                if (me_PID == ePid.DATA0 || me_PID == ePid.DATA1)
                {
                    i_Builder.Append(", ");
                    i_Builder.Append(DataLen);
                    i_Builder.Append(" ");
                    i_Builder.Append(Utils.ByteListToHex(mi_Data));
                }
                return i_Builder.ToString();
            }
        }

        class Setup
        {
            public bool          mb_Valid;
            public eSetupRecip   me_Recipient;
            public eSetupType    me_Type;
            public eDirection    me_Direction;
            public Byte          mu8_Request;
            public UInt16        mu16_Value;
            public UInt16        mu16_Index;
            public UInt16        mu16_Length;

            public Setup(List<Byte> i_Data)
            {
                if (i_Data.Count == 8)
                {
                    mb_Valid     = true;
                    me_Recipient = (eSetupRecip)i_Data[0] & eSetupRecip.MASK;
                    me_Type      = (eSetupType) i_Data[0] & eSetupType .MASK;
                    me_Direction = (eDirection) i_Data[0] & eDirection .MASK;
                    mu8_Request  = i_Data[1];
                    mu16_Value   = (UInt16)(i_Data[2] | (i_Data[3] << 8));
                    mu16_Index   = (UInt16)(i_Data[4] | (i_Data[5] << 8));
                    mu16_Length  = (UInt16)(i_Data[6] | (i_Data[7] << 8));
                }
            }

            public override string ToString()
            {
                if (!mb_Valid) 
                    return "Invalid";

                return String.Format("{0}, Type {1}, Recip {2}, Request {3}, Value {4}, Index {5}, Length {6}", 
                                     me_Direction, me_Type, me_Recipient, mu8_Request, mu16_Value, mu16_Value, mu16_Length);
            }
        }

        #endregion

        // minimum sample resolution
        const int MIN_SAMPLES_BIT = 12; 
        // The 3 bits of the EOP packet
        const int EOP_BITS = (0x7F << 24) | ((int)eBusState.SE0 << 16) | ((int)eBusState.SE0 << 8) | (int)eBusState.J;
        // Display name of Stuff Bits
        const String STUFF_BIT = "SB";

        Brush BRUSH_STUFF = new SolidBrush(Color.FromArgb(0x77, 0x55, 0xFF));
        Brush BRUSH_SEO   = Brushes.Orange;
        
        Timing          mi_Timing;
        UsbPacket       mi_Packet;
        eParse          me_ParsePos;    // state machine
        int             ms32_CurBit;
        int             ms32_BitsCrc5;
        ePid            me_PID;
        SmplMark        mi_CurSmpl;     // Mark Row 2
        int[]           ms32_BitCount;
        Brush[]         mi_Brushes;
        int             ms32_ThreeStates;
        List<SmplMark>  mi_DataSamples = new List<SmplMark>();

        public DecodeUSB()
        {
            ms32_BitCount = new int[(int)eParse.Finished];
            ms32_BitCount[(int)eParse.Sync]    =  8;
            ms32_BitCount[(int)eParse.PID]     =  8;
            ms32_BitCount[(int)eParse.BusAddr] =  7;
            ms32_BitCount[(int)eParse.Frame]   = 11;
            ms32_BitCount[(int)eParse.Endpt]   =  4;
            ms32_BitCount[(int)eParse.Data]    =  8;
            ms32_BitCount[(int)eParse.CRC5]    =  5;
            ms32_BitCount[(int)eParse.EOP]     =  3;

            mi_Brushes = new Brush[(int)eParse.Finished];
            mi_Brushes[(int)eParse.Sync]    = Brushes.LightBlue;
            mi_Brushes[(int)eParse.PID]     = Brushes.Yellow;
            mi_Brushes[(int)eParse.BusAddr] = Brushes.Lime;
            mi_Brushes[(int)eParse.Endpt]   = Brushes.Magenta;
            mi_Brushes[(int)eParse.Frame]   = Brushes.White;
            mi_Brushes[(int)eParse.Data]    = Brushes.White;
            mi_Brushes[(int)eParse.CRC5]    = Brushes.Cyan;
            mi_Brushes[(int)eParse.EOP]     = BRUSH_SEO;
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null || b_Analog)
                return;

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText   = "Decode USB Full Speed (12 Mbit)";
            i_Item.ms_ImageFile  = "USB.ico";

            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_ChanPlus, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            Channel i_ChanMinus = null;
            if (i_ChanPlus.ms_Name == "D+")
            {
                i_ChanMinus = OsziPanel.CurCapture.FindChannel("D-");
            }
            if (i_ChanPlus.ms_Name == "D-")
            {
                i_ChanMinus = i_ChanPlus;
                i_ChanPlus  = OsziPanel.CurCapture.FindChannel("D+");
            }

            if (i_ChanPlus  == null || i_ChanPlus .mu8_Digital == null ||
                i_ChanMinus == null || i_ChanMinus.mu8_Digital == null)
                throw new Exception("Two digital USB channels are required.\nThey must have the names 'D+' and 'D-'");

            Utils.StartBusyOperation(null); // show wait cursor

            UsbPacket[] i_Packets = DecodePackets(i_ChanMinus, i_ChanPlus);
            ShowRtf(i_Packets);
            Utils.OsziPanel.RecalculateEverything();

            Utils.EndBusyOperation(null);

            return i_Packets.Length + " USB packets detected";
        }

        UsbPacket[] DecodePackets(Channel i_ChanMinus, Channel i_ChanPlus)
        {
            List<UsbPacket> i_Packets     = new List<UsbPacket>();
            MemoryStream    i_StreamMinus = new MemoryStream(i_ChanMinus.mu8_Digital);
            MemoryStream    i_StreamPlus  = new MemoryStream(i_ChanPlus .mu8_Digital);
            List<SmplMark>  i_MarkRow1    = new List<SmplMark>();
            List<SmplMark>  i_MarkRow2    = new List<SmplMark>();
            List<SmplMark>  i_MarkRow3    = new List<SmplMark>();
            
            i_ChanMinus.mi_MarkRows = new List<SmplMark>[] { i_MarkRow1, i_MarkRow2, i_MarkRow3 };

            mi_Timing = new Timing(eSpeed.Full);

            eBusState e_BusState;
            int       s32_Minus;
            int       s32_Plus;

            // packet loop
            while (true)
            {
                // ------ Find idle state for at least 1 bit duration --------

                int s32_IdleCount = 0;
                while (true)
                {
                    s32_Minus = i_StreamMinus.ReadByte();
                    s32_Plus  = i_StreamPlus .ReadByte();
                    if (s32_Minus < 0 || s32_Plus < 0)
                        return i_Packets.ToArray(); // end of stream

                    e_BusState = (eBusState)((s32_Plus << 4) | s32_Minus);
                    if (e_BusState == eBusState.Idle) s32_IdleCount ++;
                    else                              s32_IdleCount = 0;

                    // The pause between EOP and start of next packet may be < 2 bits !
                    if (s32_IdleCount >= mi_Timing.md_SmplPerBit)
                        break;
                }

                // ------ Find packet start --------

                while (true)
                {
                    s32_Minus = i_StreamMinus.ReadByte();
                    s32_Plus  = i_StreamPlus .ReadByte();
                    if (s32_Minus < 0 || s32_Plus < 0)
                        return i_Packets.ToArray(); // end of stream

                    e_BusState = (eBusState)((s32_Plus << 4) | s32_Minus);
                    if (e_BusState != eBusState.Idle)
                        break;
                }

                // ------ Read Bytes --------

                mi_Packet = new UsbPacket();
                mi_Packet.ms32_StartSample = (int)i_StreamMinus.Position - 1;
                double d_BitStart          = (int)i_StreamMinus.Position - 1; 
                
                int s32_PrevMinus = 0;           // state of precious D- line
                int s32_IdleBits  = 0;           // count of consecutive idle bits
                int s32_OneBits   = 0;           // count of consecutive '1' bits
                ms32_ThreeStates  = 0x7FFFFFFF;  // the last 3 bus states
                ms32_BitsCrc5     = 0;           // the last 11 + 5 bits for CRC5 calculation
                ms32_CurBit       = 0;           // current bit position
                me_ParsePos       = eParse.Sync;
                me_PID            = ePid.Invalid;
                mi_CurSmpl        = null;
                mi_DataSamples.Clear();

                // bit loop (state machine)
                while (me_ParsePos != eParse.Finished)
                {
                    mi_Timing.CalcSamples(d_BitStart);

                    // add one mark for each bit in mark row 1
                    SmplMark i_Mark = new SmplMark(eMark.Text, mi_Timing.BitStart, mi_Timing.BitEnd);

                    bool b_TimingError = false;

                    // sample loop: reads one bit
                    while (true)
                    {
                        int s32_CurSample = (int)i_StreamMinus.Position - 1;
                        if (s32_CurSample >= mi_Timing.BitEnd) // (Stream points already to next bit)
                            break;

                        int s32_NewMinus = i_StreamMinus.ReadByte();
                        int s32_NewPlus  = i_StreamPlus .ReadByte();
                        if (s32_NewMinus < 0 || s32_NewPlus < 0)
                            return i_Packets.ToArray(); // end of stream

                        // ---------------- D- State Change -----------------

                        if (s32_Minus != s32_NewMinus)
                        {
                            s32_Minus = s32_NewMinus;
                            b_TimingError = s32_CurSample > mi_Timing.SteadyStart && s32_CurSample < mi_Timing.SteadyEnd;

                            // Synchronize to bit stream at the rising edge of the D- line
                            if (s32_Minus == 1)
                            {
                                if (s32_CurSample > mi_Timing.SamplePoint) 
                                {
                                    // Bit change is from the next bit
                                    mi_Timing.md_BitEnd = s32_CurSample; // loaded into d_BitStart at end of loop
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
                            e_BusState = (eBusState)((s32_NewPlus << 4) | s32_NewMinus);

                            if (e_BusState == eBusState.Idle) s32_IdleBits ++;
                            else                              s32_IdleBits = 0;

                            // Store the last 3 bus states as 0x7Fxxyyzz
                            ms32_ThreeStates <<= 8;
                            ms32_ThreeStates  |= (int)e_BusState;
                            ms32_ThreeStates  |= 0x7F000000;

                            if (mi_CurSmpl == null)
                            {
                                mi_CurSmpl = new SmplMark(eMark.Text, i_Mark.ms32_FirstSample);
                                mi_CurSmpl.mi_TxtBrush = mi_Brushes[(int)me_ParsePos];
                                ms32_CurBit = 0;
                            }

                            switch (e_BusState)
                            {
                                case eBusState.SE0:
                                    i_Mark.ms32_Value  = -1;
                                    i_Mark.ms_Text     = "SE0";
                                    i_Mark.mi_TxtBrush = BRUSH_SEO;
                                    break;

                                case eBusState.SE1: // invalid state
                                    i_Mark.ms32_Value   = -1;
                                    i_Mark.ms_Text      = "SE1";
                                    i_Mark.mi_TxtBrush  = Utils.ERROR_BRUSH;
                                    mi_Packet.me_Error |= eError.SE1;
                                    break;

                                default: // J, K
                                    // NRZI encoded bits: Bus Toggle --> Value = 0, Bus No Change --> Value = 1
                                    if (s32_PrevMinus == s32_Minus)
                                    {
                                        i_Mark.ms32_Value =  1;
                                        i_Mark.ms_Text    = "1";

                                        mi_CurSmpl.ms32_Value |= 1 << ms32_CurBit; // LSB first
                                        s32_OneBits ++;

                                        // Mor ethan 6 consecutive 111111 are invalid
                                        if (s32_OneBits > 6)
                                        {
                                            mi_Packet.me_Error |= eError.Stuff;
                                            i_Mark.mi_TxtBrush  = Utils.ERROR_BRUSH;
                                        }
                                    }
                                    else // Bit is zero
                                    {
                                        i_Mark.ms32_Value =  0;
                                        i_Mark.ms_Text    = "0";

                                        // After 6 consecutive 111111 bits a 0 stuff bit is inserted
                                        if (s32_OneBits == 6)
                                        {
                                            i_Mark.ms_Text     = STUFF_BIT; // "SB"
                                            i_Mark.mi_TxtBrush = BRUSH_STUFF;
                                            ms32_CurBit --;
                                        }
                                        s32_OneBits = 0;
                                    }

                                    if (i_Mark.ms_Text != STUFF_BIT)
                                    {
                                        // Store bits for CRC5 calculation
                                        ms32_BitsCrc5 <<= 1;
                                        ms32_BitsCrc5  |= i_Mark.ms32_Value;
                                    }

                                    mi_CurSmpl.ms32_LastSample = i_Mark.ms32_LastSample;
                                    break;
                            }

                            s32_PrevMinus = s32_Minus;
                            ms32_CurBit ++;

                            // If the last 3 bits represent the EOP, the packet is finished
                            if (ms32_ThreeStates == EOP_BITS)
                            {
                                if (me_ParsePos == eParse.Data)
                                    ProcessCRC16(i_MarkRow2, i_MarkRow3);

                                // Set the start sample to 3 bits before the current bit (also if previous data is corrupt!)
                                if (i_MarkRow1.Count > 3)
                                    mi_CurSmpl.ms32_FirstSample = i_MarkRow1[i_MarkRow1.Count - 2].ms32_FirstSample;

                                mi_CurSmpl.mi_TxtBrush = mi_Brushes[(int)eParse.EOP];
                                i_MarkRow2.Add(mi_CurSmpl);
                                i_MarkRow3.Add(mi_CurSmpl.Clone(eParse.EOP.ToString()));

                                if (ms32_CurBit != 3) // invalid bits remaining
                                    mi_Packet.me_Error |= eError.Data;

                                mi_CurSmpl  = null;
                                me_ParsePos = eParse.Finished;
                            }
                            else if (!mi_Packet.HasError && ms32_CurBit == ms32_BitCount[(int)me_ParsePos]) 
                            {
                                if (me_ParsePos != eParse.EOP)
                                    mi_CurSmpl.ms_Text = mi_CurSmpl.ms32_Value.ToString("X2");

                                String s_Section = me_ParsePos.ToString();
                                switch (me_ParsePos) // state machine
                                {
                                    case eParse.Sync:
                                        // Sync = bits 00000001 --> LSB first --> 0x80
                                        if (mi_CurSmpl.ms32_Value != 0x80)
                                        {
                                            mi_CurSmpl.mi_TxtBrush = Utils.ERROR_BRUSH;
                                            mi_Packet.me_Error |= eError.Sync;
                                        }                
                                        me_ParsePos = eParse.PID; // next step in state machine
                                        break;
                                    case eParse.PID:
                                        ProcessPID(); // sets me_PID
                                        s_Section = me_PID.ToString();
                                        break;
                                    case eParse.BusAddr: // IN, OUT, SETUP
                                        mi_Packet.ms32_BusAddr = mi_CurSmpl.ms32_Value;
                                        me_ParsePos = eParse.Endpt; // next step in state machine
                                        break;
                                    case eParse.Endpt: // IN, OUT, SETUP
                                        mi_Packet.ms32_Endpoint = mi_CurSmpl.ms32_Value;
                                        me_ParsePos = eParse.CRC5;  // next step in state machine
                                        break;
                                    case eParse.Frame: // SOF
                                        mi_Packet.ms32_SofFrame = mi_CurSmpl.ms32_Value;
                                        me_ParsePos = eParse.CRC5;  // next step in state machine
                                        break;
                                    case eParse.Data:
                                        mi_DataSamples.Add(mi_CurSmpl);
                                        s_Section = "Byte " + mi_DataSamples.Count;
                                        break;
                                    case eParse.CRC5:
                                        ProcessCRC5();
                                        break;
                                    case eParse.EOP:
                                        // A valid EOP is detected above, it never comes here.
                                        // Set error status if EOP is expected but not found in data stream.
                                        mi_CurSmpl.mi_TxtBrush = Utils.ERROR_BRUSH;
                                        mi_Packet.me_Error |= eError.Data;
                                        break;
                                }

                                i_MarkRow2.Add(mi_CurSmpl);
                                i_MarkRow3.Add(mi_CurSmpl.Clone(s_Section));

                                mi_CurSmpl = null;
                            }
                        }
                    } // sample loop

                    if (b_TimingError)
                    {
                        mi_Packet.me_Error |= eError.Timing;
                        i_Mark.mi_TxtBrush  = Utils.ERROR_BRUSH;
                    }
                                         
                    i_Mark.ms32_LastSample   = mi_Timing.BitEnd; 
                    mi_Packet.ms32_EndSample = mi_Timing.BitEnd; 
                    mi_Packet.mi_Marks.Add(i_Mark); 
                    i_MarkRow1.Add(i_Mark);

                    // More than 6 idle bit would normally require a stuff bit --> 8 idle bits means no more data will come.
                    // This check is only to detect the end of data in case of a severe decoding error.
                    if (s32_IdleBits > 7)
                        me_ParsePos = eParse.Finished;

                    d_BitStart = mi_Timing.md_BitEnd;
                } // bit loop

                mi_Packet.me_PID = me_PID;
                i_Packets.Add(mi_Packet);
            } // packet loop
        }

        void ProcessPID()
        {
            int s32_PID   = (mi_CurSmpl.ms32_Value)      & 0xF;
            int s32_Compl = (mi_CurSmpl.ms32_Value >> 4) & 0xF;

            me_PID = (ePid)s32_PID;
            if (s32_PID != (s32_Compl ^ 0xF))
                me_PID = ePid.Invalid;

            switch (me_PID)
            {
                case ePid.IN:
                case ePid.OUT:
                case ePid.SETUP:
                    me_ParsePos = eParse.BusAddr; // next step in state machine
                    break;
                case ePid.ACK:
                case ePid.NAK:
                case ePid.STALL:
                    me_ParsePos = eParse.EOP;     // next step in state machine
                    break;
                case ePid.DATA0:
                case ePid.DATA1:
                    me_ParsePos = eParse.Data;    // next step in state machine
                    break;
                case ePid.SOF:
                    me_ParsePos = eParse.Frame;   // next step in state machine
                    break;
                default: // Invalid PID
                    mi_CurSmpl.mi_TxtBrush = Utils.ERROR_BRUSH;
                    mi_Packet.me_Error |= eError.PID;
                    break;
            }
        }

        /// <summary>
        /// The CRC5 is calculated over the last 11 bits before the CRC and the 5 bits of the CRC itself:
        /// IN, OUT, SETUP -->  7 bit bus address + 4 bit endpoint + 5 bit CRC
        /// SOF            --> 11 bit frame number                 + 5 bit CRC
        /// </summary>
        void ProcessCRC5()
        {
            int s32_Inp  = ms32_BitsCrc5;
            int s32_Poly = 0x05;
            int s32_CRC  = 0x1F;

            // Process 11 + 5 bits, LSB first.
            // Bit 0 of ms32_BitsCrc5 is the last bit that has been added (MSB)
            for (int s32_Mask = 0x8000; s32_Mask > 0; s32_Mask >>= 1)
            {
                bool b_Input  = (s32_Inp & s32_Mask) != 0;
                bool b_TopCrc = (s32_CRC & 0x10)     != 0;

                s32_CRC <<= 1;
                if (b_Input != b_TopCrc)
                    s32_CRC ^= s32_Poly;
            }

            // If only 11 bits would be calculated, s32_CalcCRC would be the same as mi_CurSmpl.ms32_Value
            // s32_CalcCRC = Reflect(~s32_CRC, 5); 

            // ---------------------------------------------------

            // The CRC over 11 bits + 5 CRC bits is always 0x0C
            if ((s32_CRC & 0x1F) != 0x0C)
            {
                mi_CurSmpl.mi_TxtBrush = Utils.ERROR_BRUSH;
                mi_Packet.me_Error |= eError.CRC;
            }
            me_ParsePos = eParse.EOP;
        }

        /// <summary>
        /// After the PIDs DATA0 and DATA1 all data bytes are sent followed by the 16 bit CRC
        /// The packet ends with the EOP, the CRC are the last 2 data bytes before the EOP.
        /// </summary>
        void ProcessCRC16(List<SmplMark> i_MarkRow2, List<SmplMark> i_MarkRow3)
        {
            int s32_DataLen = mi_DataSamples.Count - 2;

            // If a SETUP transfer sends no data bytes, only a CRC = 0x0000 is sent
            if (s32_DataLen < 0)
            {
                mi_CurSmpl.mi_TxtBrush = Utils.ERROR_BRUSH;
                mi_Packet.me_Error |= eError.Data;
                return;
            }

            // mi_DataSamples contains all data butes, where the last 2 bytes are the 16 bit CRC
            int s32_CrcLo  = mi_DataSamples[mi_DataSamples.Count - 2].ms32_Value;
            int s32_CrcHi  = mi_DataSamples[mi_DataSamples.Count - 1].ms32_Value;
            int s32_BusCRC = (s32_CrcHi << 8) | s32_CrcLo;

            int s32_LastSample = mi_DataSamples[mi_DataSamples.Count - 1].ms32_LastSample;

            // ---------------------------------------------------

            // remove the Mark of the second CRC byte
            i_MarkRow2.RemoveAt(i_MarkRow2.Count - 1);
            i_MarkRow3.RemoveAt(i_MarkRow3.Count - 1);

            // Convert the Mark of the first CRC byte from 8 bit Data to 16 bit CRC
            SmplMark i_CrcMark2 = i_MarkRow2[i_MarkRow2.Count - 1];
            SmplMark i_CrcMark3 = i_MarkRow3[i_MarkRow3.Count - 1];

            i_CrcMark2.ms_Text = s32_BusCRC.ToString("X4");
            i_CrcMark3.ms_Text = "CRC16";

            i_CrcMark2.ms32_LastSample = s32_LastSample;
            i_CrcMark3.ms32_LastSample = s32_LastSample;

            i_CrcMark2.mi_TxtBrush = mi_Brushes[(int)eParse.CRC5]; // CRC16 uses the same color as CRC5
            i_CrcMark3.mi_TxtBrush = mi_Brushes[(int)eParse.CRC5];

            // ---------------------------------------------------

            int s32_Poly = 0x8005;
            int s32_CRC  = 0xFFFF;

            // Process all data bytes including the last two CRC bytes --> constant result, LSB first.
            for (int B=0; B<mi_DataSamples.Count; B++)
            {
                int s32_Data = mi_DataSamples[B].ms32_Value;

                // Do not store CRC bytes in mi_Data
                if (B < s32_DataLen)
                    mi_Packet.mi_Data.Add((Byte)s32_Data);

                for (int i=0; i<8; i++)
                {
                    bool b_Input  = (s32_Data & 1)      != 0;
                    bool b_TopCrc = (s32_CRC  & 0x8000) != 0;

                    s32_Data >>= 1;
                    s32_CRC  <<= 1;
                    if (b_Input != b_TopCrc)
                        s32_CRC ^= s32_Poly;
                }
            }

            // If only s32_DataLen bytes would be calculated, s32_CalcCRC would be the same as s32_BusCRC.
            // s32_CalcCRC = Reflect(~s32_CRC, 16); 

            // ---------------------------------------------------

            // The CRC over all Data bits + 16 CRC bits is always 0x800D
            if ((s32_CRC & 0xFFFF) != 0x800D)
            {
                i_CrcMark2.mi_TxtBrush = Utils.ERROR_BRUSH;
                i_CrcMark3.mi_TxtBrush = Utils.ERROR_BRUSH;
                mi_Packet.me_Error |= eError.CRC;
            }
        }

        /*
        // Revert the bit order (required by some CRC algorithms)
        int Reflect(int s32_In, int s32_Bits)
        {
            int s32_Out = 0;
            for (int B=0; B<s32_Bits; B++)
            {
                s32_Out <<= 1;
                if ((s32_In & 1) > 0) s32_Out |= 1;
                s32_In >>= 1;
            }
            return s32_Out;
        }
        */ 

        // =============================================================================================================

        void ShowRtf(UsbPacket[] i_Packets)
        {
            int s32_Errors = 0;

            RtfDocument i_RtfDoc = new RtfDocument(Color.White);
            RtfBuilder i_Builder = i_RtfDoc.CreateNewBuilder();

            i_Builder.AppendText(Color.White, "Errors:\n", FontStyle.Underline);
            i_Builder.AppendEnum(Utils.ERROR_COLOR, 11, Color.White, typeof(eError));

            i_Builder.AppendText(Color.White, "\n\nDecoded Packets:\n", FontStyle.Underline);
            foreach (UsbPacket i_Pack in i_Packets)
            {
                i_Builder.AppendTimestampLine(i_Pack.ms32_StartSample, i_Pack.ms32_EndSample, true);

                Color c_PID = (i_Pack.me_PID == ePid.STALL) ? Utils.ERROR_COLOR : Color.Yellow;
                i_Builder.AppendText(c_PID, i_Pack.me_PID.ToString().PadRight(5));

                if (i_Pack.ms32_BusAddr  >= 0) i_Builder.AppendFormat(Color.Lime,    "  BusAdr {0}",   i_Pack.ms32_BusAddr);
                if (i_Pack.ms32_Endpoint >= 0) i_Builder.AppendFormat(Color.Magenta, "  EP {0}",    i_Pack.ms32_Endpoint);
                if (i_Pack.ms32_SofFrame >= 0) i_Builder.AppendFormat(Color.White,   "  Frame {0}", i_Pack.ms32_SofFrame);

                if (i_Pack.me_PID == ePid.DATA0 || i_Pack.me_PID == ePid.DATA1)
                {
                    i_Builder.AppendText(Color.Orange, "  ");
                    i_Builder.AppendText(Color.Orange, i_Pack.DataLen);
                    i_Builder.AppendText(Color.Orange, " ");
                    i_Builder.AppendText(Color.White,  Utils.ByteListToHex(i_Pack.mi_Data));
                }
               
                if (i_Pack.me_Error > 0)
                {
                    i_Builder.AppendText(Utils.ERROR_COLOR, " " + i_Pack.me_Error.ToString());
                    s32_Errors ++;
                }

                i_Builder.AppendText("\n");
            }

            if (s32_Errors > 1)
            {
                i_Builder.AppendLine(Utils.ERROR_COLOR, "\nMultiple errors detected.");
            }
            else if (i_Packets.Length == 0)
            {
                i_Builder.AppendLine(Utils.ERROR_COLOR, "\nNothing detected");
            }
            else if (s32_Errors == 0)
            {
                i_Builder.AppendLine(Color.Lime, "\nNo errors detected.");
            }

            // ==========================================================================================

            // Behind a USB Hub all host poll requests to all connected devices are visible, but the responses only from one device.
            // An ACK or NAK packet has no bus address. It responds to the previous IN, OUT, SETUP request.
            // For IN transfers an ACK may also come from the host but it may have been sent to another device.

            // Read Part 3 in 'USB Tutorial.chm' in subfolder 'Documentation'
            // The following capture on the USB bus was taken at the device with address 3.
            // Only traffic with device 3 is fully visible. From devices 2 ad 4 only the host side is visible.

            // ---------------
            // SETUP  BusAdr 2  EP 0               <-- Device 2: host sends SETUP request
            // DATA0  [8] C1 16 00 00 00 00 0A 00  <-- Device 2: host sends SETUP packet
            //                                     <-- Device 2: responds ACK, but this is invisible
            // ---------------
            // IN     BusAdr 3  EP 1               <-- Device 3: host polls IN EP 1
            // NAK                                 <-- Device 3: responds NAK
            // ---------------
            // IN     BusAdr 2  EP 0               <-- Device 2: host polls SETUP IN data
            //                                     <-- Device 2: responds SETUP IN data, but this is invisible
            // ACK                                 <-- Device 2: host sends ACK
            // ---------------
            // IN     BusAdr 2  EP 1               <-- Device 2: host polls IN EP 1
            //                                     <-- Device 2: responds NAK, but this is invisble
            // ---------------
            // OUT    BusAdr 2  EP 0               <-- Device 2: host sends SETUP OUT request
            // DATA1  [0]                          <-- Device 2: host sends SETUP OUT Data
            //                                     <-- Device 2: responds ACK, but this is invisible
            // ---------------
            // IN     BusAdr 4  EP 2               <-- Device 4: host polls IN EP 2
            //                                     <-- Device 4: responds NAK, but this is invisible
            // ---------------
            // IN     BusAdr 3  EP 1               <-- Device 3: host polls IN EP 1
            // NAK                                 <-- Device 3: responds NAK
            // ---------------

            // One integer for each Bus Address (0 to 127)
            int[] s32_AddrActive = new int[128];

            UsbPacket i_Prev = null;
            foreach (UsbPacket i_Pack in i_Packets)
            {
                if (i_Prev != null)
                {
                    switch (i_Pack.me_PID)
                    {
                        case ePid.ACK:
                        case ePid.NAK:
                        case ePid.STALL:
                            if (i_Prev.ms32_BusAddr >= 0)
                                s32_AddrActive[i_Prev.ms32_BusAddr] ++;

                            i_Pack.ms32_BusAddr  = i_Prev.ms32_BusAddr;
                            i_Pack.ms32_Endpoint = i_Prev.ms32_Endpoint;
                            break;

                        // Data packets do not have a bus address, copy from previous token
                        case ePid.DATA0:
                        case ePid.DATA1:
                            if (i_Prev.me_PID == ePid.SETUP || i_Prev.me_PID == ePid.OUT || i_Prev.me_PID == ePid.IN)
                            {
                                i_Pack.ms32_BusAddr  = i_Prev.ms32_BusAddr;
                                i_Pack.ms32_Endpoint = i_Prev.ms32_Endpoint;
                            }
                            break;
                    }
                }
                i_Prev = i_Pack;
            }

            // Find the bus address with the most handshake packets
            int s32_Max  = 0;
            int s32_Addr = -1;
            for (int A=0; A<128; A++)
            {
                if (s32_AddrActive[A] > s32_Max)
                {
                    s32_Max  = s32_AddrActive[A];
                    s32_Addr = A;
                }
            }

            if (s32_Addr < 0 || s32_Max < 20)
            {
                i_Builder.AppendLine(Utils.ERROR_COLOR, "\nInsufficient capture data for detection of active bus address.");
            }
            else // ==========================================================================================
            {
                i_Builder.AppendLine(Color.White, "\n———————————————————————————————————————————————————————————————");
                i_Builder.AppendLine(Color.White, "\nFiltered for bus address " + s32_Addr + ":", FontStyle.Underline);

                i_Prev = null;
                foreach (UsbPacket i_Pack in i_Packets)
                {
                    if (i_Prev != null && 
                        i_Pack.ms32_BusAddr == s32_Addr &&
                        (i_Pack.me_PID == ePid.DATA0 || i_Pack.me_PID == ePid.DATA1) &&
                        i_Pack.me_Error == 0 &&
                        i_Prev.me_Error == 0)
                    {
                        bool b_Display = false;
                        switch (i_Prev.me_PID)
                        {
                            case ePid.SETUP:
                                i_Pack.mi_Setup = new Setup(i_Pack.mi_Data);
                                b_Display = true;
                                break;

                            case ePid.IN:
                            case ePid.OUT:
                                b_Display = i_Pack.mi_Data.Count > 0;
                                break;
                        }

                        if (b_Display)
                        {
                            i_Builder.AppendTimestampLine(i_Prev.ms32_StartSample, i_Prev.ms32_EndSample, true);
                            i_Builder.AppendText(Color.Magenta, "EP " + i_Prev.ms32_Endpoint.ToString().PadRight(3));
                            i_Builder.AppendText(Color.Yellow,  i_Prev.me_PID.ToString().PadRight(7));

                            if (i_Pack.mi_Setup != null)
                            {
                                i_Builder.AppendLine(Color.LightBlue, i_Pack.mi_Setup.ToString());
                            }
                            else // IN,OUT
                            {
                                i_Builder.AppendText(Color.Orange, i_Pack.DataLen);
                                i_Builder.AppendText(Color.Orange, " ");
                                i_Builder.AppendLine(Color.White,  Utils.ByteListToHex(i_Pack.mi_Data));
                            }
                        }
                    }

                    i_Prev = i_Pack;
                }
            }

            // ==========================================================================================

            // Show RTF, if created and switch to tab "Decoder"
            Utils.FormMain.ShowAnalysisResult(i_RtfDoc, (int)(mi_Timing.md_SmplPerBit + 0.5)); 
        }
    }
}

