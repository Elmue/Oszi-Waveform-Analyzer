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
using OsziPanel         = OsziWaveformAnalyzer.OsziPanel;
using Utils             = OsziWaveformAnalyzer.Utils;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using eDigiState        = OsziWaveformAnalyzer.Utils.eDigiState;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using eCtrlChar         = OsziWaveformAnalyzer.Utils.eCtrlChar;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using PostDecoderManager= PostDecoder.PostDecoderManager;
using eUartChip         = PostDecoder.PostDecoderManager.eUartChip;
using PlatformManager   = Platform.PlatformManager;

namespace Operations
{
    public partial class DecodeUART : Form, IOperation
    {
        #region enums

        enum eStopBits
        {
            None,
            One,
            Two,
            OneAndHalf,
        }

        #endregion

        #region class UartPacket

        public class UartPacket
        {
            public String ms_Name;          // The name of the channel
            public bool   mb_Rx;            // true --> data is from mi_ChannelRx
            public bool   mb_Error;         // true --> at least one byte has decoding error
            public int    ms32_StartSample; // The sample of the start bit of the first byte
            public int    ms32_EndSample;

            // Do not use Byte here because some RS485 buses use a 9 bit protocol.
            // A -1 in mi_Data means decoding error.
            public List<int> mi_Data = new List<int>(); 

            // The current buadrate as entered by the user.
            // The PostDecoder can change this baudrate if required and further decoding takes the new baudrate.
            public int ms32_Baudrate;
        }

        #endregion

        // If there are less than 10 samples per capture a reliable detection is not possible
        const int MIN_SAMPLES_BIT = 12;

        static Dictionary<String, String> mi_DemoFiles = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Static Constructor
        /// </summary>
        static DecodeUART()
        {
            mi_DemoFiles.Add("ISO 14230 K-Line Bus-Init 10400 Baud.oszi",                 "10400,Low,8,None,One,ISO14230,false");
            mi_DemoFiles.Add("Yamaha XTZ 250 K-Line OWON VDS 1022.csv",                   "15400,Low,8,None,One,None,false");
            mi_DemoFiles.Add("Yamaha XTZ 250 K-Line OWON VDS 1022.bin",                   "15400,Low,8,None,One,None,false");
            mi_DemoFiles.Add("Yamaha XTZ 250 K-Line Picoscope 3206.csv",                  "15400,Low,8,None,One,None,false");
            mi_DemoFiles.Add("RS232 Pinpad 19200 Odd 1 Stop.oszi",                        "19200,High,8,Odd,One,None,false");
            mi_DemoFiles.Add("RS232 Pinpad 115200 Mark 2 Stop.oszi",                      "115200,High,8,Mark,Two,None,false");
            mi_DemoFiles.Add("ISO 7816 Smartcard 12795 + 51182 Baud - Clk 4,76 MHz.oszi", "12795,Low,8,Even,Two,ISO7816,true");
            mi_DemoFiles.Add("ISO 7816 Halfduplex Signal with Noise 51182 Baud.oszi",     "51182,Low,8,Even,Two,None,true");

            foreach (String s_DemoFile in mi_DemoFiles.Keys)
            {
                Utils.CheckDemoFileExists(s_DemoFile);
            }
        }

        Channel    mi_ChannelRx;
        Channel    mi_ChannelTx;
        int        ms32_SmplPerBit;
        int        ms32_Errors;
        int        ms32_Decoded;
        String     ms_HexFormat;
        RtfBuilder mi_RtfBinary;
        RtfBuilder mi_RtfAnsi;
        Color      mc_DataRx;
        Color      mc_CtrlRx;
        Color      mc_DataTx;
        Color      mc_CtrlTx;
        PostDecoderManager mi_PostDecoder;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null || b_Analog)
                return; // the user did not click on a digital channel

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText  = "Decode asynchronous data: UART / RS232 / RS422 / RS485";
            i_Item.ms_ImageFile = "Port.ico";
            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            mi_ChannelRx = i_Channel;

            InitializeComponent();
            if (ShowDialog(Utils.FormMain) != DialogResult.OK)
                return null;

            if (ms32_Decoded == 0)
                return "Error: Nothing detected.";
            else
                return String.Format("Decoded {0} bytes, found {1} errors", ms32_Decoded, ms32_Errors);
        }

        protected override void OnLoad(EventArgs e)
        {
 	        base.OnLoad(e);

            foreach (eDigiState e_State in Enum.GetValues(typeof(eDigiState)))
            {
                comboStartbit.Items.Add(e_State);
            }
            foreach (Parity e_Parity in Enum.GetValues(typeof(Parity)))
            {
                comboParity.Items.Add(e_Parity);
            }
            foreach (eStopBits e_StopBits in Enum.GetValues(typeof(eStopBits)))
            {
                comboStopbits.Items.Add(e_StopBits);
            }

            foreach (eUartChip e_Chip in Enum.GetValues(typeof(eUartChip)))
            {
                comboChip.Items.Add(e_Chip);
            }
            lblChip.Text = "";

            lblNameA.Text = mi_ChannelRx.ms_Name;
            lblNameA   .ForeColor = OsziPanel.GetChannelColor(mi_ChannelRx);
            lblChannelA.ForeColor = lblNameA.ForeColor;

            // Load the correct settings for the demo files, otherwise from registry
            String s_Settings;
            if (OsziPanel.CurCapture.ms_Path == null ||
                !mi_DemoFiles.TryGetValue(Path.GetFileName(OsziPanel.CurCapture.ms_Path), out s_Settings))
                s_Settings = Utils.RegReadString(eRegKey.RS232_Settg);

            foreach (Channel i_ChannelB in OsziPanel.CurCapture.mi_Channels)
            {
                if (i_ChannelB != mi_ChannelRx && i_ChannelB.mu8_Digital != null)
                    comboChannelB.Items.Add(i_ChannelB);
            }

            Utils.SetControlValues(s_Settings, comboBaudrate, comboStartbit, comboDatabits, comboParity, comboStopbits, comboChip, checkHalfduplex);

            if (comboChannelB.Items.Count == 0)
            {
                checkHalfduplex.Checked = false;
                checkHalfduplex.Enabled = false;
            }
            else comboChannelB.SelectedIndex = 0;
 
            checkHalfduplex_CheckedChanged(null, null);

            if (comboChip.SelectedIndex < 0)
                comboChip.SelectedIndex = 0; // "None"
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "DecodeAsync");
        }

        private void comboChannelB_SelectedIndexChanged(object sender, EventArgs e)
        {
            mi_ChannelTx = (Channel)comboChannelB.SelectedItem;
            lblChannelB.ForeColor = OsziPanel.GetChannelColor(mi_ChannelTx);
        }

        private void checkHalfduplex_CheckedChanged(object sender, EventArgs e)
        {
            comboChannelB.Enabled = checkHalfduplex.Checked;
            lblChannelB  .Enabled = checkHalfduplex.Checked;

            lblChannelA.Text = checkHalfduplex.Checked ? "Channel A:" : "Channel:";
            lblChannelB.Text = checkHalfduplex.Checked ? "Channel B:" : "Channel:";
        }

        private void comboChip_SelectedIndexChanged(object sender, EventArgs e)
        {
            lblChip.Text = "";
            eUartChip e_Chip = (eUartChip)comboChip.SelectedItem;
            if (e_Chip != eUartChip.None)
                lblChip.Text = Utils.GetDescriptionAttribute(e_Chip);
        }

        // =========================================================================================================

        private void btnDecode_Click(object sender, EventArgs e)
        {
            // "10400,High,8,None,One,None"
            String s_Settings = Utils.GetControlValues(comboBaudrate, comboStartbit, comboDatabits, comboParity, comboStopbits, comboChip, checkHalfduplex);
            Utils.RegWriteString(eRegKey.RS232_Settg, s_Settings);

            int s32_Baudrate;
            if (!int.TryParse(comboBaudrate.Text, out s32_Baudrate) || s32_Baudrate < 5)
            {
                MessageBox.Show(this, "Enter a valid baudrate.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int s32_DataBits;
            if (!int.TryParse(comboDatabits.Text, out s32_DataBits) || s32_DataBits < 5)
            {
                MessageBox.Show(this, "Enter valid data bits.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (comboStartbit.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Select the start bit.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (comboParity.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Select the parity.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (comboStopbits.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Select the stop bits.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            eDigiState e_StartBit = (eDigiState)comboStartbit.SelectedItem;
            Parity     e_Parity   = (Parity)    comboParity  .SelectedItem;
            eStopBits  e_StopBits = (eStopBits) comboStopbits.SelectedItem;

            // ---------------------------------------------------------------

            // Two Rich Texts are built separately. i_RtfAnsi will be appended to i_RtfBinary in the final output.
            RtfDocument i_RtfDoc    = new RtfDocument(Color.White);
            mi_RtfBinary = i_RtfDoc.CreateNewBuilder();
            mi_RtfAnsi   = i_RtfDoc.CreateNewBuilder();

            mc_DataRx = OsziPanel.GetChannelColor(mi_ChannelRx);
            mc_CtrlRx = Color.FromArgb(mc_DataRx.R *5 /6, mc_DataRx.G *2 /3, mc_DataRx.B *3 /4);
            if (checkHalfduplex.Checked)
            {
                mc_DataTx = OsziPanel.GetChannelColor(mi_ChannelTx);
                mc_CtrlTx = Color.FromArgb(mc_DataTx.R *2 /3, mc_DataTx.G *2 /3, mc_DataTx.B *3 /4);
            }

            mi_RtfBinary.AppendText(mc_DataRx, "Channel " + mi_ChannelRx.ms_Name + "\n");
            mi_RtfBinary.AppendText(mc_CtrlRx, "Control Characters\n");

            if (checkHalfduplex.Checked)
            {
                mi_RtfBinary.AppendText(mc_DataTx, "Channel " + mi_ChannelTx.ms_Name + "\n");
                mi_RtfBinary.AppendText(mc_CtrlTx, "Control Characters\n");
            }

            eUartChip e_Chip = (eUartChip)comboChip.SelectedItem;
            if (e_Chip != eUartChip.None)
                mi_RtfBinary.AppendFormat(Color.White, "Post Decoding for {0} - {1}\n", e_Chip, Utils.GetDescriptionAttribute(e_Chip));

            mi_RtfBinary.AppendText(Color.White,   "\nDecoded binary data:\n", FontStyle.Underline);
            mi_RtfAnsi  .AppendText(Color.White, "\n\nDecoded ASCII data:\n",  FontStyle.Underline);

            // ---------------------------------------------------------------

            mi_PostDecoder = new PostDecoderManager(e_Chip);
            ms32_Decoded   = 0; // for status display
            ms32_Errors    = 0; // for status display

            Channel i_ChanTx = mi_ChannelTx;
            if (!checkHalfduplex.Checked)
            {
                // If only one channel is decoded create a second dummy channel full of STOP bits
                // This avoids a thousand of   if(halfduplex){...}   in the decoder.
                i_ChanTx = new Channel("Dummy");
                i_ChanTx.mu8_Digital = new Byte[mi_ChannelRx.mu8_Digital.Length];
                if (e_StartBit == eDigiState.Low)
                    Utils.FillArray(i_ChanTx.mu8_Digital, 1);
            }

            Cursor = Cursors.WaitCursor;
            try
            {
                Channel[] i_Channels = new Channel[] { mi_ChannelRx, i_ChanTx };
                Decode(i_Channels, s32_Baudrate, e_StartBit, s32_DataBits, e_Parity, e_StopBits);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
            }
            Cursor = Cursors.Arrow;

            // Show RTF, if created and switch to tab "Decoder"
            Utils.FormMain.ShowAnalysisResult(i_RtfDoc, ms32_SmplPerBit); 

            Utils.OsziPanel.RecalculateEverything();
            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// This function detects multiple types of errors:
        /// -- Wrong Parity bit (if used)
        /// -- Stop bit is not Low
        /// -- Data status changes between 20% and 80% of the bit length, which means the baudrate is wrong.
        /// </summary>
        void Decode(Channel[] i_Channels, int s32_Baudrate, eDigiState e_StartBit, int s32_DataBits, 
                    Parity e_Parity, eStopBits e_StopBits)
        {
            double d_SamplesPerBit = (double)(Utils.PICOS_PER_SECOND / (OsziPanel.CurCapture.ms64_SampleDist * s32_Baudrate));
            if (d_SamplesPerBit < MIN_SAMPLES_BIT)
            {
                MessageBox.Show(this, "The resolution of the capture is too low for a reliable detection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ms32_SmplPerBit  = (int)(d_SamplesPerBit + 0.5);
            int s32_MaxPause = (int)(d_SamplesPerBit * 10);  // after this pause create a new packet
            ms_HexFormat = "X" + ((s32_DataBits + 3) / 4);   // 8 bits == 2 hex digits --> HexFormat = "X2"

            // -------- chars for mark row 1 --------

            List<String> i_BitChars = new List<String>();

            i_BitChars.Add("<");                // Start bit
            for (int B=0; B<s32_DataBits; B++)  // LSB first
            {
                i_BitChars.Add(new String((Char)('0'+B), 1)); // Data bit
            }

            if (e_Parity != Parity.None)
                i_BitChars.Add("P");            // Parity bit
            
            switch (e_StopBits)                 // Stop bits
            {
                case eStopBits.One:        i_BitChars.Add(">"); break;
                case eStopBits.Two:        i_BitChars.Add(">"); i_BitChars.Add(">"); break;
                case eStopBits.OneAndHalf: i_BitChars.Add("≥"); break;
            }

            // ----------------------------

            i_Channels[0].mi_MarkRows = new List<SmplMark>[] { new List<SmplMark>(), new List<SmplMark>() };
            i_Channels[1].mi_MarkRows = new List<SmplMark>[] { new List<SmplMark>(), new List<SmplMark>() };

            Channel i_Active   = null; // The channel which is currently decoded.
            Channel i_Inactive = null; // The other half-duplex channel which must not have activity at the same time.

            // ----------------------------

            UartPacket i_Packet = null;

            // Normally the start bit is High. If the signal is inverted, the definition of High and Low must be inverted.
            int  STATE_HIGH   = (e_StartBit == eDigiState.High) ? 1 : 0;
            int  STATE_LOW    = (e_StartBit == eDigiState.High) ? 0 : 1;
            int  s32_Sample   = 0;
            int  s32_SplCount = i_Channels[0].mu8_Digital.Length;
            int  s32_State    = -1;    // state of current bit   (set invalid)
            int  s32_ByteEnd  = -1;    // sample where byte ends (set invalid)

            // byte loop
            while (true)
            {
                // -------- Find Idle Status (Low) ---------

                while (true)
                {
                    if (s32_Sample >= s32_SplCount)
                    {
                        ShowPacket(i_Packet);
                        return; // end of data
                    }

                    int s32_StateRx = i_Channels[0].mu8_Digital[s32_Sample];
                    int s32_StateTx = i_Channels[1].mu8_Digital[s32_Sample ++];

                    if (s32_StateRx == STATE_LOW && s32_StateTx == STATE_LOW)
                        break; // both channels are idle
                }

                // --------- Find Start Bit (High) ----------

                bool b_Rx;
                while (true)
                {
                    if (s32_Sample >= s32_SplCount)
                    {
                        ShowPacket(i_Packet);
                        return; // end of data
                    }

                    int s32_StateRx = i_Channels[0].mu8_Digital[s32_Sample];
                    int s32_StateTx = i_Channels[1].mu8_Digital[s32_Sample ++];

                    if (s32_StateRx == STATE_HIGH)
                    {
                        b_Rx = true;
                        i_Active   = i_Channels[0]; // Rx
                        i_Inactive = i_Channels[1]; // Tx
                        break; // Start bit on Rx channel found
                    }
                    if (s32_StateTx == STATE_HIGH)
                    {
                        b_Rx = false;
                        i_Active   = i_Channels[1]; // Tx
                        i_Inactive = i_Channels[0]; // Rx
                        break; // Start bit on Tx channel found
                    }
                }
                s32_State = STATE_HIGH;

                // ------------ Flush Packet ---------------

                // the current start bit (s32_Sample) is behind the last byte + s32_MaxPause --> flush the packet
                if (i_Packet == null || i_Packet.mb_Rx != b_Rx || s32_Sample > s32_ByteEnd + s32_MaxPause)
                {
                    if (i_Packet != null)
                    {
                        ShowPacket(i_Packet); // calls PostDecoder if enabled

                        // The PostDecoder has changed the baudrate
                        if (i_Packet.ms32_Baudrate != s32_Baudrate)
                        {
                            String s_Msg = String.Format("Switching baudrate from {0} to {1} baud.", s32_Baudrate, i_Packet.ms32_Baudrate);
                            mi_RtfBinary.AppendLine(Color.White, s_Msg);
                            mi_RtfAnsi  .AppendLine(Color.White, s_Msg);

                            s32_Baudrate    = i_Packet.ms32_Baudrate;
                            d_SamplesPerBit = (double)(Utils.PICOS_PER_SECOND / (OsziPanel.CurCapture.ms64_SampleDist * s32_Baudrate));
                            s32_MaxPause    = (int)(d_SamplesPerBit * 10); 

                            // If baudrate has increased --> adapt to the faster baudrate
                            ms32_SmplPerBit = Math.Min(ms32_SmplPerBit, (int)(d_SamplesPerBit + 0.5));
                        }
                    }

                    i_Packet = new UartPacket();
                    i_Packet.ms_Name          = i_Active.ms_Name;
                    i_Packet.mb_Rx            = b_Rx;
                    i_Packet.ms32_StartSample = s32_Sample - 1; // = s32_ByteStart
                    i_Packet.ms32_Baudrate    = s32_Baudrate;
                }

                // ------------- Decode Bits ----------------

                // Stream.Position already points to the next sample after reading the current sample.
                int  s32_ByteStart = s32_Sample - 1; // sample where byte starts
                int  s32_Value     = 0;              // the decoded byte value
                int  s32_Mask      = 1;              // the mask that adds a bit to s32_Value
                int  s32_Parity    = 0;              // the parity calculated from all data bits
                bool b_ByteError   = false;

                // bit loop
                for (int s32_Bit = 0; s32_Bit < i_BitChars.Count; s32_Bit ++)
                {
                    String s_BitChar = i_BitChars[s32_Bit]; // "<", "0...7", ">"

                    double d_BitStart = s32_ByteStart + d_SamplesPerBit * s32_Bit; // sample where bit starts
                    double d_BitEnd   = d_BitStart    + d_SamplesPerBit;           // sample where bit ends
                    if (s_BitChar == "≥") // one and half stop bits
                        d_BitEnd  += d_SamplesPerBit / 2.0;

                    bool  b_BitError = false;
                    int s32_BitStart = (int)(d_BitStart + 0.5);
                    int s32_BitEnd   = (int)(d_BitEnd   + 0.5);
                    s32_ByteEnd      = s32_BitEnd;

                    // For a detection of wrong baud rates I define a "steady range" from 20% to 80% of the bit length
                    // in which the state of the data is not allowed to change.
                    // Normally asynchronous communication demands 2 % baudrate precision.
                    // But this would require a very high capture resolution which is not always available.
                    // If the data state changes between sample s32_SteadyStart and s32_SteadyEnd this inidicates an invalid baudrate.
                    // The data value is taken at 50 % bit time.
                    // Neither s32_SteadyStart nor s32_SteadyEnd have any effect on the decoding. They are just for baudrate checking.
                    int s32_SteadyStart = (int)(d_BitStart + 0.2 * d_SamplesPerBit + 0.5); // 20 %
                    int s32_SamplePoint = (int)(d_BitStart + 0.5 * d_SamplesPerBit + 0.5); // 50 %
                    int s32_SteadyEnd   = (int)(d_BitStart + 0.8 * d_SamplesPerBit + 0.5); // 80 %

                    // sample loop
                    while (s32_Sample < s32_BitEnd)
                    {
                        if (s32_Sample >= s32_SplCount)
                        {
                            ShowPacket(i_Packet);
                            return; // end of data
                        }

                        int s32_NewState  = i_Active  .mu8_Digital[s32_Sample];
                        int s32_IdleState = i_Inactive.mu8_Digital[s32_Sample ++];

                        if (s32_IdleState != STATE_LOW) // the inactive channel must always be idle
                            throw new Exception("Error at sample " + s32_Sample + "\n"
                                              + "The 2 channels do not contain alternating Rx/Tx data.\n"
                                              + "This is not a valid half-duplex capture or the baudrate is wrong.\n"
                                              + "Try decoding these channels separately.\n"
                                              + "Please read the Help file.");

                        if (s32_State != s32_NewState) // data has changed
                        {
                            s32_State = s32_NewState;
                            if (s32_Sample > s32_SteadyStart && s32_Sample < s32_SteadyEnd)
                                b_BitError = true;
                        }

                        // Abort the loop at 80% of the last stop bit to assure that the next start bit is not missed if it comes early.
                        if (s32_Bit == i_BitChars.Count -1 && s32_Sample >= s32_SteadyEnd)
                            break;

                        // Take the data value in the middle of the bit
                        if (s32_Sample == s32_SamplePoint)
                        {
                            switch (s_BitChar)
                            {
                                case "<": // start bit must be High (Space)
                                    if (s32_State != STATE_HIGH) 
                                        b_BitError = true;
                                    break;

                                case ">": // stop bit must be Low (Mark)
                                case "≥": // one and half stop bits
                                    if (s32_State != STATE_LOW) 
                                        b_BitError = true;
                                    break;

                                case "P": // check parity
                                    switch (e_Parity)
                                    {
                                        case Parity.Even:
                                            s32_Parity ^= s32_State;
                                            if (s32_Parity != STATE_HIGH) b_BitError = true;
                                            break;
                                        case Parity.Odd:
                                            s32_Parity ^= s32_State;
                                            if (s32_Parity != STATE_LOW)  b_BitError = true;
                                            break;
                                        // Mark and Space are not parity bits. They have always the same constant value.
                                        case Parity.Mark:
                                            if (s32_State != STATE_LOW)   b_BitError = true;
                                            break;
                                        case Parity.Space:
                                            if (s32_State != STATE_HIGH)  b_BitError = true;
                                            break;
                                    }
                                    break;

                                default: // data bit (Attention: RS232 takes data inverse: Low = Mark = bit is set!)
                                    if (s32_State == STATE_LOW)
                                    {
                                        s32_Value  |= s32_Mask;
                                        s32_Parity ^= 1;
                                    }
                                    s32_Mask <<= 1;
                                    break;
                            }
                        } // center position
                    } // sample loop

                    eMark e_Mark = eMark.Text;
                    if (b_BitError) // show wrong bit with a red character and red separator lines
                    {
                        e_Mark = eMark.Error;
                        b_ByteError = true;
                    }

                    // add one mark for each bit in mark row 1
                    i_Active.mi_MarkRows[0].Add(new SmplMark(e_Mark, s32_BitStart, s32_BitEnd, s_BitChar));
                } // bit loop

                if (b_ByteError)
                {
                    // add an error in mark row 2
                    i_Active.mi_MarkRows[1].Add(new SmplMark(eMark.Error, s32_ByteStart, s32_ByteEnd, "ERR"));
                    i_Packet.mi_Data.Add(-1);
                    i_Packet.mb_Error = true;
                    i_Packet.ms32_EndSample = s32_ByteEnd;
                    ms32_Errors ++;
                }
                else
                {
                    // add one mark for each valid byte in mark row 2
                    i_Active.mi_MarkRows[1].Add(new SmplMark(eMark.Text, s32_ByteStart, s32_ByteEnd, s32_Value.ToString(ms_HexFormat)));
                    i_Packet.mi_Data.Add(s32_Value);
                    i_Packet.ms32_EndSample = s32_ByteEnd;
                    ms32_Decoded ++;
                }
            } // byte loop
        }

        void ShowPacket(UartPacket i_Packet)
        {
            if (i_Packet.mi_Data.Count == 0)
                return;

            mi_RtfBinary.AppendTimestampLine(i_Packet.ms32_StartSample, i_Packet.ms32_EndSample, true);
            mi_RtfAnsi  .AppendTimestampLine(i_Packet.ms32_StartSample, i_Packet.ms32_EndSample, true);

            mi_RtfBinary.AppendText(Color.Magenta, "> ");
            mi_RtfAnsi  .AppendText(Color.Magenta, "> ");

            foreach (int s32_Byte in i_Packet.mi_Data)
            {
                if (s32_Byte < 0) // Error
                {
                    mi_RtfBinary.AppendText(Utils.ERROR_COLOR, "Error ");
                    mi_RtfAnsi  .AppendText(Utils.ERROR_COLOR, "?");
                }
                else
                {
                    Color c_Data = i_Packet.mb_Rx ? mc_DataRx : mc_DataTx;
                    Color c_Ctrl = i_Packet.mb_Rx ? mc_CtrlRx : mc_CtrlTx;
                   
                    mi_RtfBinary.AppendText(c_Data, s32_Byte.ToString(ms_HexFormat) + ' '); // Hex byte

                    Char c_Char = (Char)s32_Byte;
                    if (c_Char < 0x20) // control character
                    {
                        eCtrlChar e_Ctrl = (eCtrlChar)c_Char;
                        mi_RtfAnsi.AppendFormat(c_Ctrl, "<{0}>", e_Ctrl);
                    }
                    else // printable character
                    {
                        mi_RtfAnsi.TextColor = c_Data;
                        mi_RtfAnsi.AppendChar(c_Char);
                    }
                }
            }

            mi_RtfAnsi  .AppendNewLine();
            mi_RtfBinary.AppendNewLine();

            if (!i_Packet.mb_Error)
                mi_PostDecoder.DecodeUART(i_Packet, mi_RtfBinary);
        }
    }
}

