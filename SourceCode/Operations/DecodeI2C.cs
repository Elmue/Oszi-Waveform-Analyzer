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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using IOperation        = Operations.OperationManager.IOperation;
using RtfDocument       = OsziWaveformAnalyzer.RtfDocument;
using RtfBuilder        = OsziWaveformAnalyzer.RtfBuilder;
using Utils             = OsziWaveformAnalyzer.Utils;
using OsziPanel         = OsziWaveformAnalyzer.OsziPanel;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using eDigiState        = OsziWaveformAnalyzer.Utils.eDigiState;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using eCtrlChar         = OsziWaveformAnalyzer.Utils.eCtrlChar;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using eI2cChip          = PostDecoder.PostDecoderManager.eI2cChip;
using PostDecoderManager= PostDecoder.PostDecoderManager;
using PlatformManager   = Platform.PlatformManager;

namespace Operations
{
    public partial class DecodeI2C : Form, IOperation
    {
        #region enums

        enum eBit
        {
            A6,
            A5,
            A4,
            A3,
            A2,
            A1,
            A0,
            RW,
            ACKA,
        // -------
            D7,
            D6,
            D5,
            D4,
            D3,
            D2,
            D1,
            D0,
            ACKD,
        }

        public enum eACK
        {
            Invalid,  // default value
            ACK,
            NAK,
        }

        public enum eData
        {
            Invalid,  // default value
            Error,    // bus error detected
            Start,    // Start Condition
            AdrRead,  // Address byte + Read bit
            AdrWrite, // Address byte + Write bit
            Data,     // Data byte
            Stop,     // Stop Condition
        }

        #endregion

        #region class I2CByte

        public class I2CByte
        {
            public eData me_Data;
            public int   ms32_Value;       // Address or Data byte
            public eACK  me_Ack;
            public int   ms32_StartSample; // The sample of the first clock of the byte or the Start/Stop condition
            public int   ms32_EndSample;

            public I2CByte(int s32_StartSmpl, eData e_Data = eData.Invalid)
            {
                ms32_StartSample = s32_StartSmpl;
                ms32_EndSample   = s32_StartSmpl;
                me_Data          = e_Data;
                ms32_Value       = -1;
                me_Ack           = eACK.Invalid;
            }

            public override string ToString()
            {
                switch (me_Data)
                {
                    case eData.Data:
                    case eData.AdrRead:
                    case eData.AdrWrite:
                        return String.Format("{0} {1:X2} {2}", me_Data, ms32_Value, me_Ack);
                    default:
                        return me_Data.ToString();
                }
            }
        }

        #endregion

        #region class I2CPacket

        public class I2CPacket
        {
            public bool          mb_Write;
            public bool          mb_HasError;
            public Byte          mu8_Address;
            public List<I2CByte> mi_Bytes = new List<I2CByte>();
            public List<Byte>    mi_Data  = new List<Byte>();
            public int           ms32_StartSample; // The sample of the first bit of the first byte or Start/Stop condition
            public int           ms32_EndSample;

            public I2CPacket(int s32_StartSmpl)
            {
                ms32_StartSample = s32_StartSmpl;
                ms32_EndSample   = s32_StartSmpl;
            }
        }

        #endregion

        static Dictionary<String, String> mi_DemoFiles = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Static Constructor
        /// </summary>
        static DecodeI2C()
        {
            mi_DemoFiles.Add("ISO 14443 RFID Mifare Card with PN532 over I2C.oszi", eI2cChip.PN532.ToString());

            foreach (String s_DemoFile in mi_DemoFiles.Keys)
            {
                Utils.CheckDemoFileExists(s_DemoFile);
            }
        }

        int     ms32_Errors;
        int     ms32_Decoded;
        int     ms32_SmplPerBit;
        Channel mi_SCL;
        Channel mi_SDA;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            // At least 2 digital channels must exist for SPI
            if (i_Channel == null || b_Analog || OsziPanel.CurCapture.ms32_DigitalCount < 2)
                return; 

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText  = "Decode I²C bus";
            i_Item.ms_ImageFile = "I2C.ico";
            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            mi_SCL = null;
            mi_SDA = null;

            foreach (Channel i_Chan in OsziPanel.CurCapture.mi_Channels)
            {
                if (i_Chan.mu8_Digital == null)
                    continue;

                if (i_Chan.ms_Name.ToUpper() == "SCL") mi_SCL = i_Chan;
                if (i_Chan.ms_Name.ToUpper() == "SDA") mi_SDA = i_Chan;
            }

            if (mi_SCL == null || mi_SDA == null)
                throw new Exception("To decode I²C bus you need two digital channels which must have the names 'SCL' and 'SDA'.");

            InitializeComponent();
            if (ShowDialog(Utils.FormMain) != DialogResult.OK)
                return null;

            return String.Format("{0} bytes decoded, {1} errors detected.", ms32_Decoded, ms32_Errors);
        }

        // =============================================================================================================

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            foreach (eI2cChip e_Chip in Enum.GetValues(typeof(eI2cChip)))
            {
                comboChip.Items.Add(e_Chip);
            }

            String s_Chip;
            if (OsziPanel.CurCapture.ms_Path == null ||
                !mi_DemoFiles.TryGetValue(Path.GetFileName(OsziPanel.CurCapture.ms_Path), out s_Chip))
                s_Chip = Utils.RegReadString(eRegKey.I2C_Chip);

            lblChip.Text = "";

            comboChip.Text = s_Chip;
            if (comboChip.SelectedIndex < 0)
                comboChip.SelectedIndex = 0; // "None"
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "DecodeI2C");
        }

        private void comboChip_SelectedIndexChanged(object sender, EventArgs e)
        {
            lblChip.Text = "";
            eI2cChip e_Chip = (eI2cChip)comboChip.SelectedItem;
            if (e_Chip != eI2cChip.None)
                lblChip.Text = Utils.GetDescriptionAttribute(e_Chip);
        }

        private void btnDecode_Click(object sender, EventArgs e)
        {
            ms32_Errors  = 0;
            ms32_Decoded = 0;
            Utils.RegWriteString(eRegKey.I2C_Chip, comboChip.Text);

            Cursor = Cursors.WaitCursor;
            try
            {
                I2CByte  [] i_ByteList   = DecodeBytes();
                I2CPacket[] i_I2CPackets = DecodePackets(i_ByteList);
                ShowRtf(i_I2CPackets);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
            }
            Cursor = Cursors.Arrow;

            Utils.OsziPanel.RecalculateEverything();
            DialogResult = DialogResult.OK;
        }

        // =============================================================================================================

        /// <summary>
        /// Throws
        /// </summary>
        I2CByte[] DecodeBytes()
        {
            List<I2CByte>  i_ByteList  = new List<I2CByte>();
            List<SmplMark> i_ClkMarks  = new List<SmplMark>();
            List<SmplMark> i_DataMarks = new List<SmplMark>();
            mi_SCL.mi_MarkRows = new List<SmplMark>[] { i_ClkMarks  };
            mi_SDA.mi_MarkRows = new List<SmplMark>[] { i_DataMarks };

            int     s32_BitSmpl   = -1; // start sample of bit
            int     s32_ByteStart = -1; // start sample of byte (address or data)
            int     s32_ByteEnd   = -1;
            int     s32_ByteVal   =  0; // value of byte (address or data)
            eBit    e_CurBit      = eBit.A6;
            bool    b_Idle        = true;
            I2CByte i_I2CByte     = null;
            Byte    u8_RiseSDA    = 0;  // state of SDA sampled at rising clock edge

            Byte u8_LastSCL = mi_SCL.mu8_Digital[0];
            Byte u8_LastSDA = mi_SDA.mu8_Digital[0];
            for (int S=1; S<OsziPanel.CurCapture.ms32_Samples; S++)
            {
                Byte u8_SCL = mi_SCL.mu8_Digital[S];
                Byte u8_SDA = mi_SDA.mu8_Digital[S];

                bool b_StartCond = u8_LastSCL == 1 && u8_SCL == 1 && u8_LastSDA == 1 && u8_SDA == 0;
                bool b_StopCond  = u8_LastSCL == 1 && u8_SCL == 1 && u8_LastSDA == 0 && u8_SDA == 1;
                bool b_ClkRise   = u8_LastSCL == 0 && u8_SCL == 1; 
                bool b_ClkFall   = u8_LastSCL == 1 && u8_SCL == 0; 

                u8_LastSCL = u8_SCL;
                u8_LastSDA = u8_SDA;

                // --------------------------------

                if (b_StartCond) // START condition found
                {
                    // i_I2CByte is not null here if a previous byte has not been finished
                    if (i_I2CByte != null)
                    {
                        SmplMark i_ErrMark = new SmplMark(eMark.Error, s32_ByteEnd, -1, " Error");
                        i_ErrMark.mi_TxtBrush = Utils.ERROR_BRUSH;
                        i_DataMarks.Add(i_ErrMark);

                        i_I2CByte.me_Data = eData.Error;
                        i_ByteList.Add(i_I2CByte);
                        i_I2CByte = null;
                    }

                    b_Idle        = false;
                    e_CurBit      = eBit.A6; // next bit is address bit 6
                    s32_ByteVal   =  0;
                    s32_ByteStart = -1;
                    s32_ByteEnd   = -1;
                    s32_BitSmpl   = -1;
                    SmplMark i_Mark = new SmplMark(eMark.Text, S, -1, " <");
                    i_Mark.mi_TxtBrush = Brushes.Lime;
                    i_DataMarks.Add(i_Mark);
                    i_ByteList .Add(new I2CByte(S, eData.Start));
                    continue;
                }

                if (b_StopCond) // STOP condition found
                {
                    eMark e_Mark = eMark.Text;

                    // i_I2CByte is null here if a Stop condition follows immediately after a Start condition
                    if (i_I2CByte != null)
                    {
                        if (e_CurBit != eBit.D7)
                        {
                            // At least one address byte must have appeared on the bus.
                            e_Mark = eMark.Error;
                            ms32_Errors ++;

                            i_I2CByte.me_Data = eData.Error;
                            i_I2CByte.ms32_EndSample = S;
                            i_ByteList.Add(i_I2CByte);
                            i_I2CByte = null;
                        }
                    }

                    b_Idle = true;
                    SmplMark i_Mark = new SmplMark(e_Mark, S, -1, " >");
                    i_Mark.mi_TxtBrush = Brushes.Lime;
                    i_DataMarks.Add(i_Mark);
                    i_ByteList .Add(new I2CByte(S, eData.Stop));
                    continue;
                }

                // ignore anything until a start condition is found
                if (b_Idle)
                    continue;
 
                // --------------------------------

                if (b_ClkRise) // Clock rising edge --> sample SDA
                {
                    u8_RiseSDA = u8_SDA; // SDA at rising clock edge

                    if ((e_CurBit >= eBit.A6 && e_CurBit <= eBit.A0) || // order is revese!
                        (e_CurBit >= eBit.D7 && e_CurBit <= eBit.D0))
                    {
                        s32_ByteVal <<= 1;
                        s32_ByteVal |= u8_RiseSDA;
                    }
                    
                    s32_BitSmpl = S;       // store sample where bit starts
                    if (s32_ByteStart < 0)
                        s32_ByteStart = S; // store sample where byte starts

                    s32_ByteEnd = S;       // store sample where byte ends
                }

                if (b_ClkFall) // Clock falling edge
                {
                    if (s32_BitSmpl < 0)
                        continue; // ignore falling edge if no previous rising edge

                    SmplMark i_ClkMark = new SmplMark(eMark.Text, s32_BitSmpl, -1, e_CurBit.ToString());
                    i_ClkMarks.Add(i_ClkMark);

                    if (i_I2CByte == null)
                        i_I2CByte = new I2CByte(s32_BitSmpl);

                    switch (e_CurBit)
                    {
                        case eBit.A0:
                        case eBit.D0:
                            i_I2CByte.ms32_Value = s32_ByteVal;
                            ms32_Decoded ++;
                            break;

                        case eBit.RW:
                            SmplMark i_AdrMark = new SmplMark(eMark.Text, s32_ByteStart, s32_ByteEnd);
                            i_AdrMark.ms_Text = i_I2CByte.ms32_Value.ToString("X2");
                            i_DataMarks.Add(i_AdrMark);

                            int s32_AvrgSmpl = (s32_BitSmpl - s32_ByteStart) / 7; // address = 7 bits
                            if (ms32_SmplPerBit == 0)
                                ms32_SmplPerBit = s32_AvrgSmpl;
                            else
                                ms32_SmplPerBit = Math.Min(ms32_SmplPerBit, s32_AvrgSmpl);

                            if (u8_RiseSDA == 0)
                            {
                                i_ClkMark.ms_Text = "WR";
                                i_I2CByte.me_Data = eData.AdrWrite;
                                i_AdrMark.mi_TxtBrush = Brushes.Cyan;
                            }
                            else
                            {
                                i_ClkMark.ms_Text = "RD";
                                i_I2CByte.me_Data = eData.AdrRead;
                                i_AdrMark.mi_TxtBrush = Brushes.Magenta;
                            }
                            break;

                        case eBit.ACKA:
                        case eBit.ACKD:
                            if (u8_RiseSDA == 0)
                            {
                                i_ClkMark.ms_Text = "ACK";
                                i_ClkMark.mi_TxtBrush = Brushes.Lime;
                                i_I2CByte.me_Ack  = eACK.ACK;
                            }
                            else
                            {
                                i_ClkMark.ms_Text = "NAK";
                                i_ClkMark.mi_TxtBrush = Utils.ERROR_BRUSH;
                                i_I2CByte.me_Ack  = eACK.NAK;
                            }

                            if (e_CurBit == eBit.ACKD)
                            {
                                SmplMark i_DataMark = new SmplMark(eMark.Text, s32_ByteStart, s32_ByteEnd, s32_ByteVal.ToString("X2"));
                                i_DataMark.mi_TxtBrush = Brushes.Yellow;
                                i_DataMarks.Add(i_DataMark);
                                i_I2CByte.me_Data = eData.Data;
                            }

                            i_I2CByte.ms32_EndSample = S;
                            i_ByteList.Add(i_I2CByte);
                            i_I2CByte     = null;
                            e_CurBit      = eBit.D7; // After ACK comes Data or Stop condition
                            s32_ByteVal   = 0;
                            s32_ByteStart = -1;
                            s32_ByteEnd   = -1;
                            s32_BitSmpl   = -1;
                            continue;
                    }
                    e_CurBit ++;

                } // if (b_ClkFall)
            } // for (S)

            return i_ByteList.ToArray();
        }

        // =============================================================================================================

        I2CPacket[] DecodePackets(I2CByte[] i_ByteList)
        {
            List<I2CPacket> i_PackList = new List<I2CPacket>();
            I2CPacket       i_Packet   = null;
            foreach (I2CByte i_I2CByte in i_ByteList)
            {
                if (i_Packet == null)
                    i_Packet = new I2CPacket(i_I2CByte.ms32_StartSample);

                switch (i_I2CByte.me_Data)
                {
                    case eData.Start:
                        if (i_Packet.mi_Bytes.Count > 0)
                        {                           
                            i_PackList.Add(i_Packet);
                            i_Packet = new I2CPacket(i_I2CByte.ms32_StartSample);
                        }
                        break;

                    case eData.Stop:
                        i_Packet.mi_Bytes.Add(i_I2CByte);
                        i_Packet.ms32_EndSample = i_I2CByte.ms32_EndSample;
                        i_PackList.Add(i_Packet);
                        i_Packet = null;
                        continue;

                    case eData.AdrRead:
                    case eData.AdrWrite:
                        i_Packet.mb_Write    = (i_I2CByte.me_Data == eData.AdrWrite);
                        i_Packet.mu8_Address = (Byte)i_I2CByte.ms32_Value;
                        break;

                    case eData.Data:
                        i_Packet.mi_Data.Add((Byte)i_I2CByte.ms32_Value);
                        break;

                    case eData.Error:
                        i_Packet.mb_HasError = true;
                        break;

                    default:
                        Debug.Assert(false, "Programming Error: Invalid data type");
                        break;
                }

                i_Packet.mi_Bytes.Add(i_I2CByte);
                i_Packet.ms32_EndSample = i_I2CByte.ms32_EndSample;
            }

            Debug.Assert(i_Packet == null || i_Packet.mi_Bytes.Count == 0, "Empty Packet!");

            return i_PackList.ToArray();
        }

        // =============================================================================================================

        void ShowRtf(I2CPacket[] i_Packets)
        {
            RtfDocument i_RtfDoc  = new RtfDocument(Color.White);
            RtfBuilder  i_Builder = i_RtfDoc.CreateNewBuilder();

            i_Builder.AppendLine(Color.Lime,    "<  Start Condition");
            i_Builder.AppendLine(Color.Cyan,    "Write Chip Address");
            i_Builder.AppendLine(Color.Magenta, "Read  Chip Address");
            i_Builder.AppendLine(Color.Yellow,  "Data Bytes");
            i_Builder.AppendLine(Color.Lime,    ">  Stop Condition");

            eI2cChip e_Chip = (eI2cChip)comboChip.SelectedItem;
            if (e_Chip != eI2cChip.None)
                i_Builder.AppendFormat(Color.White, "Post Decoding for {0} - {1}\n", e_Chip, Utils.GetDescriptionAttribute(e_Chip));

            i_Builder.AppendNewLine();

            // ---------------------------------------------------

            PostDecoderManager i_PostDecoder = new PostDecoderManager(e_Chip);

            foreach (I2CPacket i_I2CPacket in i_Packets)
            {
                foreach (I2CByte i_I2CByte in i_I2CPacket.mi_Bytes)
                {
                    switch (i_I2CByte.me_Data)
                    {
                        case eData.Start:
                            i_Builder.AppendNewLineOnce(); // if preceeded by a Stop condition a NewLine has already been output
                            i_Builder.AppendTimestampLine(i_I2CPacket.ms32_StartSample, i_I2CPacket.ms32_EndSample, true);
                            i_Builder.AppendText(Color.Lime, "< ");
                            break;

                        case eData.Stop:
                            i_Builder.AppendText(Color.Lime, " >\n");
                            break;

                        case eData.AdrRead:
                            i_Builder.AppendFormat(Color.Magenta, "R {0:X2}:", i_I2CByte.ms32_Value);
                            break;

                        case eData.AdrWrite:
                            i_Builder.AppendFormat(Color.Cyan,    "W {0:X2}:", i_I2CByte.ms32_Value);
                            break;

                        case eData.Data:
                            i_Builder.AppendFormat(Color.Yellow,  " {0:X2}",   i_I2CByte.ms32_Value);
                            break;

                        case eData.Error:
                            i_Builder.AppendText(Utils.ERROR_COLOR, " ERROR ");
                            break;

                        default:
                            Debug.Assert(false, "Programming Error: Invalid data type");
                            break;
                    } // switch

                    if (i_I2CByte.me_Ack == eACK.NAK)
                        i_Builder.AppendText(Color.Orange, " NAK");

                } // I2CByte

                if (!i_I2CPacket.mb_HasError)
                     i_PostDecoder.DecodeI2C(i_I2CPacket, i_Builder);

            } // I2CPacket

            if (i_Packets.Length == 0)
                i_Builder.AppendLine(Utils.ERROR_COLOR, "Nothing detected");

            // Show RTF and switch to tab "Decoder"
            Utils.FormMain.ShowAnalysisResult(i_RtfDoc, ms32_SmplPerBit); 
        }
    }
}

