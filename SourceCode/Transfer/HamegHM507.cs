/*
------------------------------------------------------------
Oscilloscope Waveform Analyzer by ElmüSoft (www.netcult.ch/elmue)
This code is released under the terms of the GNU General Public License.
This class has been written by Peter Schwendner, integrated into the project by ELmü
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
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;

using eConnectMode      = Transfer.SCPI.eConnectMode;
using eOperation        = Transfer.Hameg.eOperation;
using OsziModel         = Transfer.Hameg.OsziModel;
using OsziConfig        = Transfer.Hameg.OsziConfig;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using Utils             = OsziWaveformAnalyzer.Utils;

// This class implements the proprietary RS232 protocol of the Hameg HM507 analog / digital oscilloscope.
// Documentation: "Beschreibung der Schnittstellenkommandos, gueltig fuer HM504, HM504-2, HM507" (26.10.05)
// The HM504 is a pure analog scope which cannot transfer waveforms.
//
// RS232: 8 data bits, no parity, 2 stop bits, RTS/CTS handshake.
// After power on the scope detects the baudrate (110 ... 115200) from the first "SPACE CR" and answers with "0 CR LF".
// Commands are ASCII, terminated with CR. Parameters may be binary. Words are sent low byte first.
//
// Queries:        "CH1?"         --> "CH1:" + 1 binary byte      (the scope repeats the command, followed by a colon)
// Set commands:   "HLDWFM=1"     --> "0 CR LF"                   (Returncode, "0" = no error)
// Waveform:       "RDWFM1:" + Offset(word) + Length(word) --> "RDWFM1:" + Offset + Length + 2048 bytes
// Voltage:        U = (Byte - 128 - YPos) / 25 * Volt/Div        (128 = center line, 25 points per division)
// Time:           200 samples per division
//
// A new command must not be sent before the response of the previous command has been received completely.
// If the user presses the LOCAL (AUTOSET) key the scope sends "ESC RM0" and leaves the remote state.
namespace Transfer
{
    /// <summary>
    /// This class implements the RS232 commands for the Hameg HM507.
    /// </summary>
    public class HamegHM507 : IHamegScope
    {
        const int  CMD_TIMEOUT  = 2000;
        const int  DATA_TIMEOUT = 3000;
        const int  SAMPLES      = 2048;  // memory size per channel
        const Byte CENTER_LINE  = 128;   // A/D value of the center graticule line
        const Byte ESC          = 0x1B;

        // "VOLT/DIV - Zaehler 0-13" in bits D0...D3 of the channel byte: 1mV ... 20V in 1-2-5 sequence
        static readonly decimal[] VOLTS_PER_DIV = { 0.001m, 0.002m, 0.005m, 0.01m, 0.02m, 0.05m, 0.1m, 0.2m, 0.5m, 1m, 2m, 5m, 10m, 20m };

        // "TIME/DIV - Zaehler" in bits D0...D4 of TBA: 00hex = 50ns/DIV ... 15hex = 0.5s/DIV (analog), up to 1Chex = 100s/DIV (store)
        static readonly decimal[] SECONDS_PER_DIV = { 50e-9m, 100e-9m, 200e-9m, 500e-9m, 1e-6m, 2e-6m, 5e-6m, 10e-6m, 20e-6m, 50e-6m,
                                                      100e-6m, 200e-6m, 500e-6m, 1e-3m, 2e-3m, 5e-3m, 10e-3m, 20e-3m, 50e-3m, 100e-3m,
                                                      200e-3m, 500e-3m, 1m, 2m, 5m, 10m, 20m, 50m, 100m };

        static readonly String[] RETURN_CODES = { "no error", "syntax error", "data error", "buffer overflow", "bad data set",
                                                  "adjustment error", "timing error", "SIO data format error (stop bit not detected)" };

        // Bits in the byte of CH1? / CH2?
        const Byte CH_GND = 0x80;
        const Byte CH_AC  = 0x40;
        const Byte CH_INV = 0x20;
        const Byte CH_ON  = 0x10;

        // Bits in the byte of HORMODE?
        const Byte HOR_CT    = 0x80; // component tester
        const Byte HOR_XY    = 0x40;
        const Byte HOR_X10   = 0x20;
        const Byte HOR_STORE = 0x10; // digital storage mode

        // Bits in the byte of VERMODE?
        const Byte VER_PROBE1 = 0x40; // CH1 probe 10:1
        const Byte VER_ADD    = 0x08;
        const Byte VER_PROBE2 = 0x04; // CH2 probe 10:1

        const Byte TBA_SINGLE = 0x20;

        SCPI         mi_Scpi;
        FormTransfer mi_Form;
        OsziModel    mi_OsziModel;
        String       ms_Warnings;

        public OsziModel Model
        {
            get { return mi_OsziModel; }
        }

        public bool HasDeepMemory
        {
            get { return false; } // always 2048 samples
        }

        public bool CanReset
        {
            get { return false; } // there is no command for a factory reset
        }

        public String TransferWarnings
        {
            get { return ms_Warnings; }
        }

        // ==============================================================================

        // May throw
        public void Connect(FormTransfer i_Form)
        {
            Debug.Assert(mi_Scpi == null, "Programming Error: Do not call Connect() multiple times!");

            mi_Form = i_Form;
            mi_Scpi = i_Form.Scpi;

            // "SPACE CR" sets the baudrate after power on and switches the scope into the remote state.
            // If the scope is already in remote state from a previous session, it may respond with a syntax error.
            // Any returncode means that the scope is alive.
            try
            {
                SendCommand(" ");
                ReadReturnCode(CMD_TIMEOUT);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("The HM507 does not respond.\n"
                    + "The scope detects the baudrate only once after power on or after leaving the remote state.\n"
                    + "Press the button AUTOSET (LOCAL) on the scope or switch it off and on and connect again.\n"
                    + "Check the RS232 cable (1:1, not crossed) and the handshake setting (RTS).");
            }
            catch (HM507Exception) {} // returncode != 0 is OK here

            // "VERS:FC5.00 DG3.20" (documented as 15 byte parameter)
            String s_Version = QueryText("VERS?");
            // "ID:...." (documented as 27 byte parameter + CR LF)
            String s_ID      = QueryText("ID?");

            mi_OsziModel = new OsziModel();
            mi_OsziModel.ms_Brand    = "HAMEG";
            mi_OsziModel.ms_Model    = s_ID.Length > 0 ? s_ID : "HM507";
            mi_OsziModel.ms_Serial   = "not reported";
            mi_OsziModel.ms_Firmware = s_Version;
        }

        public void Disconnect()
        {
            if (mi_Scpi == null)
                return;

            try
            {
                // Leave the remote state, so the front panel keys work again.
                SendCommand("RM0");
                ReadReturnCode(500);
            }
            catch {} // ignore error
        }

        // ===============================================================================================

        /// <summary>
        /// Throws
        /// </summary>
        public OsziConfig GetOsziConfiguration()
        {
            Byte u8_TBA     = QueryByte("TBA?");
            Byte u8_HorMode = QueryByte("HORMODE?");
            bool b_Hold     = QueryAscii("HLDWFM?") == '1';

            OsziConfig i_Config = new OsziConfig();
            i_Config.md_TimeBase = GetTimePerDiv(u8_TBA);

            if ((u8_HorMode & HOR_STORE) == 0)
            {
                i_Config.ms_AcqState = "ANALOG";
            }
            else
            {
                if (i_Config.md_TimeBase > 0)
                    i_Config.md_SampleRate = 200 / i_Config.md_TimeBase; // 200 samples per division

                if      (b_Hold)                      i_Config.ms_AcqState = "HOLD";
                else if ((u8_TBA & TBA_SINGLE) != 0)  i_Config.ms_AcqState = "SINGLE";
                else                                  i_Config.ms_AcqState = "RUN";
            }
            return i_Config;
        }

        /// <summary>
        /// Execute control commands. HOLD is used for STOP.
        /// </summary>
        public void ExecuteOperation(eOperation e_Operation)
        {
            String s_Cmd;
            int  s32_Timeout = CMD_TIMEOUT;
            switch (e_Operation)
            {
                case eOperation.Auto:   s_Cmd = "AUTOSET";  s32_Timeout = 15000; break;
                case eOperation.Run:    s_Cmd = "HLDWFM=0"; break;
                case eOperation.Stop:   s_Cmd = "HLDWFM=1"; break;
                case eOperation.Single: s_Cmd = "RES";      break; // SINGLE mode: prepare for the next trigger event
                default: throw new Exception("The HM507 does not support " + e_Operation);
            }

            mi_Form.PrintStatus("Command   " + s_Cmd, Color.Blue);
            SendCommand(s_Cmd);
            ReadReturnCode(s32_Timeout);
        }

        /// <summary>
        /// Sends the text typed by the user + CR and displays all bytes received.
        /// Binary bytes are displayed as hex, e.g. "CH1:<52>"
        /// </summary>
        public String SendManualCommand(String s_Command)
        {
            SendCommand(s_Command);

            StringBuilder i_Resp = new StringBuilder();
            int s32_Timeout = CMD_TIMEOUT; // wait for the first byte
            try
            {
                while (i_Resp.Length < 10000)
                {
                    Byte u8_Byte = mi_Scpi.ReceiveSingleByte(s32_Timeout);
                    s32_Timeout = 300; // then wait until nothing more arrives

                    if      (u8_Byte == '\r' || u8_Byte == '\n') continue;
                    else if (u8_Byte >= 0x20 && u8_Byte < 0x7F)  i_Resp.Append((Char)u8_Byte);
                    else                                         i_Resp.AppendFormat("<{0:X2}>", u8_Byte);
                }
            }
            catch (TimeoutException)
            {
                if (i_Resp.Length == 0)
                    throw;
            }

            String s_Resp = i_Resp.ToString();
            if (s_Resp.Length == 1 && s_Resp[0] >= '0' && s_Resp[0] <= '7')
                s_Resp += " = " + RETURN_CODES[s_Resp[0] - '0'];
            return s_Resp;
        }

        // ===============================================================================================

        /// <summary>
        /// b_Memory is ignored: The HM507 always has 2048 samples per channel.
        /// Throws AbortException if the user has aborted.
        /// </summary>
        public Capture TransferAllChannels(bool b_Memory)
        {
            ms_Warnings = null;
            List<String> i_Warnings = new List<String>();

            Byte u8_HorMode = QueryByte("HORMODE?");
            if ((u8_HorMode & HOR_CT) != 0)
                throw new ArgumentException("The oscilloscope is in component tester mode.");
            if ((u8_HorMode & HOR_XY) != 0)
                throw new ArgumentException("XY mode cannot be transferred.");
            if ((u8_HorMode & HOR_STORE) == 0)
                throw new ArgumentException("The oscilloscope is in analog mode.\n"
                                          + "Press the button STOR. to switch to digital storage mode.");

            Byte u8_TBA  = QueryByte("TBA?");
            bool b_Hold  = QueryAscii("HLDWFM?") == '1';
            bool b_Single = (u8_TBA & TBA_SINGLE) != 0;
            if (!b_Hold && !b_Single)
                throw new ArgumentException("The oscilloscope must be in HOLD mode to transfer the channels.\n"
                                          + "Otherwise the memory is overwritten by new acquisitions while it is being transferred "
                                          + "(this takes 1 second per channel at 19200 baud).\n"
                                          + "Press the button 'Stop' to activate HOLD or use SINGLE mode.");

            if (b_Single && !b_Hold && QueryAscii("TRGSTA?") == '2')
                throw new ArgumentException("The single acquisition is not complete yet.");

            decimal d_TimeDiv = GetTimePerDiv(u8_TBA);
            if (d_TimeDiv <= 0)
                throw new Exception(String.Format("The oscilloscope has reported an invalid timebase: TBA = {0:X2} hex", u8_TBA));

            if ((u8_HorMode & HOR_X10) != 0)
                i_Warnings.Add("The X-MAG x10 magnifier is on. The time scale refers to the timebase without magnifier.");

            if (QueryWord("TBAVAR?") != 0)
                i_Warnings.Add("The timebase is uncalibrated (VAR). The time scale is not correct.");

            // WFMPRE: Trigger address, X resolution (200/DIV), Y resolution (25/DIV), Y1 position, Y2 position
            Byte[] u8_Pre = Query("WFMPRE?", 10);
            int s32_ResX  = BitConverter.ToInt16(u8_Pre, 2);
            int s32_ResY  = BitConverter.ToInt16(u8_Pre, 4);
            int[] s32_YPos = { BitConverter.ToInt16(u8_Pre, 6), BitConverter.ToInt16(u8_Pre, 8) };
            if (s32_ResX <= 0 || s32_ResY <= 0)
                throw new Exception("The oscilloscope has sent an invalid waveform preamble.");

            Byte u8_VerMode = QueryByte("VERMODE?");
            if ((u8_VerMode & VER_ADD) != 0)
                i_Warnings.Add("ADD mode is on. The data of the channels may not be what you expect.");

            Capture i_Capture = new Capture();
            i_Capture.ms32_AnalogRes  = 8;
            i_Capture.ms32_Samples    = SAMPLES;
            i_Capture.ms64_SampleDist = (Int64)(d_TimeDiv / s32_ResX * Utils.PICOS_PER_SECOND);

            for (int s32_Chan=1; s32_Chan<=2; s32_Chan++)
            {
                Byte u8_Chan = QueryByte("CH" + s32_Chan + "?");
                if ((u8_Chan & CH_ON) == 0)
                    continue;

                int s32_VoltIdx = u8_Chan & 0x0F;
                if (s32_VoltIdx >= VOLTS_PER_DIV.Length)
                    throw new Exception(String.Format("The oscilloscope has reported an invalid Volt/Div: CH{0} = {1:X2} hex", s32_Chan, u8_Chan));

                decimal d_VoltDiv = VOLTS_PER_DIV[s32_VoltIdx];
                Byte u8_ProbeBit  = (s32_Chan == 1) ? VER_PROBE1 : VER_PROBE2;
                if ((u8_VerMode & u8_ProbeBit) != 0)
                    d_VoltDiv *= 10; // probe 10:1

                if (QueryByte("CH" + s32_Chan + "VAR?") != 0xFF)
                    i_Warnings.Add("Channel " + s32_Chan + " is uncalibrated (VAR). The voltages are not correct.");
                if ((u8_Chan & CH_GND) != 0)
                    i_Warnings.Add("The input of channel " + s32_Chan + " is set to GND.");

                // "RDWFM1:" + Offset 0000 + Length 0800 hex (low byte first)
                String s_Progress = "Channel " + s32_Chan;
                mi_Form.PrintStatus(s_Progress + ": Requesting data...", Color.Black);

                String s_Cmd = "RDWFM" + s32_Chan + ":";
                Byte[] u8_Cmd = new Byte[s_Cmd.Length + 5];
                Encoding.ASCII.GetBytes(s_Cmd, 0, s_Cmd.Length, u8_Cmd, 0);
                u8_Cmd[s_Cmd.Length + 0] = 0x00;                 // offset low
                u8_Cmd[s_Cmd.Length + 1] = 0x00;                 // offset high
                u8_Cmd[s_Cmd.Length + 2] = (Byte)(SAMPLES & 0xFF); // length low
                u8_Cmd[s_Cmd.Length + 3] = (Byte)(SAMPLES >> 8); // length high
                u8_Cmd[s_Cmd.Length + 4] = (Byte)'\r';
                mi_Scpi.SendRawCommand(u8_Cmd, true, CMD_TIMEOUT);

                // Response: "RDWFM1:" + Offset + Length + 2048 data bytes
                ReadEcho(s_Cmd.TrimEnd(':'), DATA_TIMEOUT);
                Byte[] u8_Header = mi_Scpi.ReceiveRawResponse(4, DATA_TIMEOUT, null);
                int s32_Length = u8_Header[2] | (u8_Header[3] << 8);
                if (s32_Length != SAMPLES)
                    throw new Exception("The oscilloscope has sent a wrong data length: " + s32_Length);

                mi_Form.Progr.Init(s_Progress, SAMPLES);
                Byte[] u8_Data = mi_Scpi.ReceiveRawResponse(SAMPLES, DATA_TIMEOUT, mi_Form.Progr);
                if (u8_Data == null)
                    return null;

                // U = (Byte - 128 - YPos) / 25 * Volt/Div
                float[] f_Analog = new float[SAMPLES];
                for (int S=0; S<SAMPLES; S++)
                {
                    f_Analog[S] = (float)((u8_Data[S] - CENTER_LINE - s32_YPos[s32_Chan - 1]) * d_VoltDiv / s32_ResY);
                }

                String s_Name = "CH" + s32_Chan;
                if ((u8_Chan & CH_INV) != 0)
                    s_Name += " inverted";

                Channel i_Channel = new Channel(s_Name);
                i_Channel.mf_Analog = f_Analog;
                i_Capture.mi_Channels.Add(i_Channel);
            }

            if (i_Capture.mi_Channels.Count == 0)
                throw new Exception("Both channels are turned off.");

            if (i_Warnings.Count > 0)
                ms_Warnings = String.Join("\n", i_Warnings.ToArray());

            return i_Capture;
        }

        // ===============================================================================================

        static decimal GetTimePerDiv(Byte u8_TBA)
        {
            int s32_Idx = u8_TBA & 0x1F;
            if (s32_Idx >= SECONDS_PER_DIV.Length)
                return 0;
            return SECONDS_PER_DIV[s32_Idx];
        }

        /// <summary>
        /// Sends an ASCII command + CR.
        /// The HM507 is an old device: the bytes are sent one by one to avoid overflowing its receive buffer.
        /// </summary>
        void SendCommand(String s_Command)
        {
            mi_Scpi.SendRawCommand(Encoding.ASCII.GetBytes(s_Command + '\r'), true, CMD_TIMEOUT);
        }

        /// <summary>
        /// Sends a query like "CH1?" and returns the s32_ParamLen bytes behind the echo "CH1:"
        /// </summary>
        Byte[] Query(String s_Query, int s32_ParamLen)
        {
            SendCommand(s_Query);
            ReadEcho(s_Query.TrimEnd('?'), CMD_TIMEOUT);
            return mi_Scpi.ReceiveRawResponse(s32_ParamLen, CMD_TIMEOUT, null);
        }

        /// <summary>
        /// For text responses like "VERS:FC5.00 DG3.20" the documented length is not reliable.
        /// Read until linefeed or until nothing more arrives. Returns only printable characters.
        /// </summary>
        String QueryText(String s_Query)
        {
            SendCommand(s_Query);
            ReadEcho(s_Query.TrimEnd('?'), CMD_TIMEOUT);

            List<Byte> i_Text = new List<Byte>();
            try
            {
                while (i_Text.Count < 64)
                {
                    Byte u8_Byte = mi_Scpi.ReceiveSingleByte(300);
                    if (u8_Byte == '\n')
                        break;
                    i_Text.Add(u8_Byte);
                }
            }
            catch (TimeoutException) {}
            return CleanText(i_Text.ToArray());
        }

        Byte QueryByte(String s_Query)
        {
            return Query(s_Query, 1)[0];
        }

        Char QueryAscii(String s_Query)
        {
            return (Char)Query(s_Query, 1)[0];
        }

        int QueryWord(String s_Query)
        {
            Byte[] u8_Word = Query(s_Query, 2);
            return u8_Word[0] | (u8_Word[1] << 8);
        }

        /// <summary>
        /// Reads the repetition of the command up to the colon: "CH1:"
        /// Some responses differ from the command: "HLDWFM?" --> "HOLDWFM:"
        /// If the scope does not understand the command it sends a returncode instead.
        /// </summary>
        void ReadEcho(String s_Expected, int s32_Timeout)
        {
            StringBuilder i_Echo = new StringBuilder();
            while (true)
            {
                Byte u8_Byte = mi_Scpi.ReceiveSingleByte(s32_Timeout);
                if (u8_Byte == ':')
                    return;

                if (u8_Byte == ESC)
                    throw new Exception("The LOCAL key has been pressed on the oscilloscope. The remote state has been left.\n"
                                      + "Close and open the connection again.");

                if (u8_Byte == '\n' && i_Echo.Length > 0 && i_Echo[0] >= '0' && i_Echo[0] <= '7')
                    throw new HM507Exception(s_Expected, i_Echo[0]); // returncode instead of a response

                i_Echo.Append((Char)u8_Byte);
                if (i_Echo.Length > 20)
                    throw new Exception("The oscilloscope has sent an invalid response to " + s_Expected + ": " + CleanText(Encoding.ASCII.GetBytes(i_Echo.ToString())));
            }
        }

        /// <summary>
        /// Reads the returncode "0 CR LF". Throws HM507Exception if it is not "0".
        /// </summary>
        void ReadReturnCode(int s32_Timeout)
        {
            Char c_Code = '\0';
            for (int i=0; i<10; i++)
            {
                Byte u8_Byte = mi_Scpi.ReceiveSingleByte(s32_Timeout);
                if (u8_Byte == ESC)
                    throw new Exception("The LOCAL key has been pressed on the oscilloscope. The remote state has been left.");
                if (u8_Byte == '\n')
                    break;
                if (u8_Byte >= '0' && u8_Byte <= '9' && c_Code == '\0')
                    c_Code = (Char)u8_Byte;
            }
            if (c_Code != '0')
                throw new HM507Exception(null, c_Code);
        }

        static String CleanText(Byte[] u8_Data)
        {
            StringBuilder i_Text = new StringBuilder();
            foreach (Byte u8_Byte in u8_Data)
            {
                if (u8_Byte >= 0x20 && u8_Byte < 0x7F)
                    i_Text.Append((Char)u8_Byte);
            }
            return i_Text.ToString().Trim();
        }

        // ===============================================================================================

        /// <summary>
        /// The oscilloscope has sent a returncode other than "0"
        /// </summary>
        class HM507Exception : Exception
        {
            public HM507Exception(String s_Command, Char c_Code)
                : base(FormatMessage(s_Command, c_Code))
            {
            }

            static String FormatMessage(String s_Command, Char c_Code)
            {
                String s_Msg = "The HM507 has returned error " + c_Code;
                if (c_Code >= '0' && c_Code <= '7')
                    s_Msg += " = " + RETURN_CODES[c_Code - '0'];
                if (s_Command != null)
                    s_Msg += "\nCommand: " + s_Command;
                return s_Msg;
            }
        }
    }
}
