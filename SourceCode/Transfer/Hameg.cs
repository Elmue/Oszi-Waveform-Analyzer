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
using System.Globalization;
using System.Text;
using System.Threading;

using eOsziSerie        = Transfer.TransferManager.eOsziSerie;
using eConnectMode      = Transfer.SCPI.eConnectMode;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using Utils             = OsziWaveformAnalyzer.Utils;

// This class implements communication with Hameg (now Rohde & Schwarz) oscilloscopes.
// The Hameg interface cards HO720 (RS232 + USB virtual COM port), HO730 (Ethernet + USB) and HO740 (GPIB) all speak SCPI.
// Over RS232 all SCPI commands are terminated with linefeed and the scope responds with linefeed.
//
// There are two completely different generations of SCPI commands for reading waveforms:
//
// 1.) CombiScopes HM1008, HM1508, HM2005-2, HM2008 (and HMO scopes with old firmware)
//     Documentation: "SCPI Programmierhandbuch HM1000x HM1008x HM1500x HM1508x HM2005-2 HM2008" (firmware >= 05.303-02.010)
//     :TRACe:SOURce CH1       select the waveform
//     :TRACe:FORMat BYTE      raw A/D values (8 bit, 25 points per division)
//     :TRACe:POINts DEF | MAX DEF = displayed waveform (2048 points), MAX = entire acquisition memory (only in STOP mode)
//     :TRACe:DATA?            returns an IEEE 488.2 block "#42048...."
//     Voltage = (Data - :TRACe:YREFerence?) * :TRACe:YINCrement? + :TRACe:YORigin?
//     Sample distance = :TRACe:XINCrement?
//     The CombiScope must be in digital mode (DSO). In analog mode there is no data to read.
//     The RS232 interface uses "N-8-2 no parity, 8 bits data, 2 stop bits (RTS/CTS hardware protocol)".
//
// 2.) HMO Compact series HMO1002 / HMO1202 / HMO1522 / HMO2022 / HMO722 / HMO1022 ... with current firmware
//     :FORMat REAL,32 ; :FORMat:BORDer LSBF    IEEE 754 floats which are already voltages
//     :CHANnel1:DATA:POINts DEF | DMAX         DEF = displayed, DMAX = all points in the display window
//     :CHANnel1:DATA:HEADer?                   "start time, stop time, count of samples, values per sample"
//     :CHANnel1:DATA?                          returns an IEEE 488.2 block
//     :POD1:DATA? with :FORMat UINT,8          8 logic channels in the 8 bits of each byte
//     These commands are also used by the sigrok driver "hameg-hmo".
//     If the HMO does not respond to :CHANnel1:DATA:HEADer? this class falls back to the :TRACe commands.
//
// RS232 is slow: At 115200 baud the transfer of 10.000 float samples (40 kB) takes 3.5 seconds.
// At 19200 baud the transfer of 2048 bytes from a HM2008 takes approx 1 second per channel.
namespace Transfer
{
    /// <summary>
    /// PanelHameg controls all Hameg oscilloscopes through this interface.
    /// Class Hameg      implements the SCPI protocol of HMO and HM1008 / HM1508 / HM2008.
    /// Class HamegHM507 implements the proprietary binary protocol of HM507.
    /// </summary>
    public interface IHamegScope
    {
        Hameg.OsziModel Model { get; }

        // false --> the scope has only a small memory which is always transferred entirely (HM507: 2048 samples)
        bool    HasDeepMemory { get; }

        // false --> the scope does not support a reset to factory defaults
        bool    CanReset { get; }

        // Warnings from the last transfer (e.g. uncalibrated VARIABLE knob) or null
        String  TransferWarnings { get; }

        void    Connect(FormTransfer i_Form); // throws
        void    Disconnect();                              // does not throw
        Hameg.OsziConfig GetOsziConfiguration();           // throws
        void    ExecuteOperation(Hameg.eOperation e_Operation);
        Capture TransferAllChannels(bool b_Memory);        // throws AbortException if aborted

        // Sends a command typed by the user and returns the response.
        // Throws TimeoutException if the scope does not respond.
        String  SendManualCommand(String s_Command);
    }

    /// <summary>
    /// This class implements the most important SCPI commands for Hameg oscilloscopes.
    /// </summary>
    public class Hameg : IHamegScope
    {
        #region enums

        public enum eOperation
        {
            Reset,
            Auto,
            Run,
            Stop,
            Single,
        }

        #endregion

        #region embedded classes

        public class OsziModel
        {
            public String ms_Brand;
            public String ms_Model;
            public String ms_Serial;
            public String ms_Firmware;
        }

        public class OsziConfig
        {
            public String  ms_AcqState;       // "RUN", "STOP", "COMPLETE" or null if unknown
            public decimal md_TimeBase;       // seconds per division, 0 if unknown
            public decimal md_SampleRate;     // Hertz, 0 if unknown (the CombiScopes do not report it)
        }

        // A channel that has been read from the oscilloscope
        class RxChannel
        {
            public String  ms_Name;
            public float[] mf_Analog;
            public Byte[]  mu8_Pod;
            public decimal md_SampleDist;     // seconds between two samples, 0 if unknown
        }

        #endregion

        const int ANALOG_CHANNELS = 2;        // HMO1522, HM2008 and all CombiScopes have 2 analog channels
        const int ANALOG_RES      = 8;        // all Hameg scopes have 8 bit A/D converters
        const int MAX_BLOCK_SIZE  = 0x800000; // USB receive buffer (HMO1522 has 1 MSample per channel = 4 MB as float)
        const int DATA_TIMEOUT    = 5000;     // the scope may need some time to prepare the data

        SCPI         mi_Scpi;
        FormTransfer mi_Form;
        OsziModel    mi_OsziModel;
        bool         mb_UseTrace;             // true --> use the :TRACe commands, false --> use the :CHANnel:DATA commands
        bool         mb_CombiScope;           // true --> HM1008 / HM1508 / HM2008 which have an analog and a digital mode

        public OsziModel Model
        {
            get { return mi_OsziModel; }
        }

        public bool HasDeepMemory
        {
            get { return true; }
        }

        public bool CanReset
        {
            get { return true; }
        }

        public String TransferWarnings
        {
            get { return null; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public Hameg(eOsziSerie e_Serie)
        {
            mb_UseTrace   = (e_Serie == eOsziSerie.Hameg_HM2008);
            mb_CombiScope = mb_UseTrace;
        }

        // ==============================================================================

        // May throw
        public void Connect(FormTransfer i_Form)
        {
            Debug.Assert(mi_Scpi == null, "Programming Error: Do not call Connect() multiple times!");

            mi_Form = i_Form;
            mi_Scpi = i_Form.Scpi;

            // Important: Execute the command *IDN? immediatley here.
            // If the communication fails, the connection cannot be used and an error must be displayed to the user.
            // HMO1522 returns "HAMEG,HMO1522,012345678,05.886"
            // HM1508  returns "HAMEG,HM1508,000000000,HW10030000,SW05.100-02.005" (hardware and software version)
            String s_IDN = mi_Scpi.SendStringCommand("*IDN?", 2000); // throws

            String[] s_Parts = s_IDN.Split(',');
            if (s_Parts.Length >= 4)
            {
                mi_OsziModel = new OsziModel();
                mi_OsziModel.ms_Brand    = s_Parts[0].Trim();
                mi_OsziModel.ms_Model    = s_Parts[1].Trim();
                mi_OsziModel.ms_Serial   = s_Parts[2].Trim();
                mi_OsziModel.ms_Firmware = String.Join(" ", s_Parts, 3, s_Parts.Length - 3).Trim();

                // The model reported by the scope overrides the model selected by the user.
                // All HMO scopes use the :CHANnel:DATA commands (with fallback to :TRACe for old firmware).
                mb_UseTrace   = !mi_OsziModel.ms_Model.ToUpper().StartsWith("HMO");
                mb_CombiScope = mb_UseTrace;
            }

            // All Hameg scopes support *OPC? (documented in the SCPI manuals of HMO and CombiScopes)
            mi_Scpi.OpcReplaceDelay = 0;
        }

        public void Disconnect()
        {
            if (mi_Scpi == null)
                return;

            try
            {
                // The CombiScope may lock the front panel keys in remote mode.
                if (mb_UseTrace)
                    mi_Scpi.SendVoidCommand(":SYSTem:LOCK OFF");
            }
            catch {} // ignore error
        }

        // ===============================================================================================

        /// <summary>
        /// Does not throw.
        /// returns null if no error is reported or the command is not supported.
        /// The error is removed from the queue of last errors.
        /// </summary>
        public String GetLastError()
        {
            try
            {
                // returns '0,"No error"' if there is no error.
                String s_Error = mi_Scpi.SendStringCommand(":SYSTem:ERRor?");
                if (s_Error.StartsWith("0,") || s_Error.StartsWith("+0,"))
                    return null;
                return s_Error;
            }
            catch
            {
                mi_Scpi.DiscardInput();
                return null;
            }
        }

        /// <summary>
        /// Throws TimeoutException if there is no response, but first tries to get the reason with :SYSTem:ERRor?
        /// </summary>
        public String SendManualCommand(String s_Command)
        {
            // There may be multiple "last errors" in the queue. Delete them all before sending the command.
            for (int i=0; i<20 && GetLastError() != null; i++)
            {
            }

            try
            {
                return mi_Scpi.SendStringCommand(s_Command, 2000);
            }
            catch (TimeoutException)
            {
                mi_Scpi.DiscardInput();
                String s_Error = GetLastError();
                if (s_Error != null)
                    throw new Exception(s_Error);
                throw;
            }
        }

        /// <summary>
        /// Throws
        /// </summary>
        public OsziConfig GetOsziConfiguration()
        {
            OsziConfig i_Config = new OsziConfig();
            i_Config.ms_AcqState = GetAcquisitionState();

            if (mb_UseTrace) // CombiScope
            {
                i_Config.md_TimeBase = ToDecimal(mi_Scpi.SendDoubleCommand(":HORizontal:MAIN:SCALe?"));
            }
            else // HMO
            {
                i_Config.md_TimeBase   = ToDecimal(mi_Scpi.SendDoubleCommand(":TIMebase:SCALe?"));
                i_Config.md_SampleRate = ToDecimal(mi_Scpi.SendDoubleCommand(":ACQuire:SRATe?"));
            }
            return i_Config;
        }

        /// <summary>
        /// returns "RUN", "STOP", "COMPLETE" or null if the scope does not support the command.
        /// The HM2008 returns "STOP" while the last acquisition is still running after the STOP command, then "COMPLETE".
        /// Throws only if the connection is broken.
        /// </summary>
        String GetAcquisitionState()
        {
            try
            {
                String s_State = mi_Scpi.SendStringCommand(":ACQuire:STATe?").Trim().ToUpper();
                if (s_State.StartsWith("RUN"))  return "RUN";
                if (s_State.StartsWith("STOP")) return "STOP";
                if (s_State.StartsWith("COMP")) return "COMPLETE";
                return null;
            }
            catch (TimeoutException)
            {
                mi_Scpi.DiscardInput();
                return null;
            }
        }

        /// <summary>
        /// Execute Control commands like RUN, STOP, AUTOSET, etc.
        /// </summary>
        public void ExecuteOperation(eOperation e_Operation)
        {
            String s_Cmd = null;
            switch (e_Operation)
            {
                case eOperation.Reset:
                    s_Cmd = "*RST";
                    break;
                case eOperation.Auto:
                    s_Cmd = mb_UseTrace ? ":SYSTem:SET:AUTO" : ":AUToscale";
                    break;
                case eOperation.Run:
                    s_Cmd = mb_UseTrace ? ":ACQuire:STATe RUN" : ":RUN";
                    break;
                case eOperation.Stop:
                    s_Cmd = mb_UseTrace ? ":ACQuire:STATe STOP" : ":STOP";
                    break;
                case eOperation.Single:
                    s_Cmd = mb_UseTrace ? ":TRIGger:A:MODE SINGle" : ":SINGle";
                    break;
            }

            mi_Form.PrintStatus("Command   " + s_Cmd, Color.Blue);

            // *OPC? returns when the command has finished. Autoset may take several seconds.
            mi_Scpi.SendOpcCommand(s_Cmd, 15000);
        }

        // ===============================================================================================

        /// <summary>
        /// b_Memory = true  --> copy the entire memory (high resolution) Only possible in STOP mode.
        /// b_Memory = false --> copy only the waveform that is visible on the screen
        /// Throws AbortException if the user has aborted.
        /// </summary>
        public Capture TransferAllChannels(bool b_Memory)
        {
            CheckDigitalMode();

            if (GetAcquisitionState() == "RUN")
                throw new ArgumentException("The oscilloscope must be in STOP mode to transfer the channels.\n"
                                          + "Otherwise each channel comes from a different acquisition and the channels are out of sync.\n"
                                          + "Press the button 'Stop' and wait until the acquisition has finished.");

            List<RxChannel> i_RxChannels = new List<RxChannel>();
            for (int s32_Chan=1; s32_Chan<=ANALOG_CHANNELS; s32_Chan++)
            {
                if (!IsChannelEnabled(s32_Chan))
                    continue;

                RxChannel i_RxChan = null;
                if (!mb_UseTrace)
                {
                    i_RxChan = ReadChannelHmo(s32_Chan, b_Memory);
                    if (i_RxChan == null)
                    {
                        // The HMO has not responded to :CHANnel:DATA:HEADer? --> old firmware --> use the :TRACe commands
                        mi_Form.PrintStatus("The oscilloscope does not support :CHANnel:DATA --> using :TRACe", Color.Blue);
                        mb_UseTrace = true;
                    }
                }
                if (mb_UseTrace)
                    i_RxChan = ReadChannelTrace(s32_Chan, b_Memory);

                if (i_RxChan != null)
                    i_RxChannels.Add(i_RxChan);
            }

            // The logic channels of the HMO series (option HO3508 logic probe)
            if (!mb_UseTrace)
            {
                RxChannel i_Pod = ReadPodHmo(b_Memory);
                if (i_Pod != null)
                    i_RxChannels.Add(i_Pod);
            }

            if (i_RxChannels.Count == 0)
                throw new Exception("All channels are turned off or the oscilloscope has not sent any data.\n"
                                  + "A CombiScope must be in digital mode (DSO) to transfer waveforms.");

            return CreateCapture(i_RxChannels);
        }

        /// <summary>
        /// All channels in a Capture must have the same count of samples and the same sample distance.
        /// </summary>
        Capture CreateCapture(List<RxChannel> i_RxChannels)
        {
            int     s32_Samples  = int.MaxValue;
            decimal d_SampleDist = 0;
            foreach (RxChannel i_RxChan in i_RxChannels)
            {
                int s32_Count = (i_RxChan.mf_Analog != null) ? i_RxChan.mf_Analog.Length : i_RxChan.mu8_Pod.Length;
                s32_Samples = Math.Min(s32_Samples, s32_Count);

                if (i_RxChan.md_SampleDist <= 0)
                    continue;

                if (d_SampleDist == 0)
                    d_SampleDist = i_RxChan.md_SampleDist;
                else if (Math.Abs(d_SampleDist - i_RxChan.md_SampleDist) > d_SampleDist / 1000)
                    throw new Exception("The oscilloscope has sent channels with different sample rates.\n"
                                      + "Transfer the channels separately.");
            }

            if (d_SampleDist <= 0)
                throw new Exception("The oscilloscope has not sent the time between two samples.");

            if (s32_Samples < Utils.MIN_VALID_SAMPLES)
                throw new Exception("The oscilloscope has sent only " + s32_Samples + " samples.");

            Capture i_Capture = new Capture();
            i_Capture.ms32_AnalogRes  = ANALOG_RES;
            i_Capture.ms32_Samples    = s32_Samples;
            i_Capture.ms64_SampleDist = (Int64)(d_SampleDist * Utils.PICOS_PER_SECOND);

            foreach (RxChannel i_RxChan in i_RxChannels)
            {
                if (i_RxChan.mf_Analog != null)
                {
                    Channel i_Channel = new Channel(i_RxChan.ms_Name);
                    i_Channel.mf_Analog = i_RxChan.mf_Analog;
                    if (i_Channel.mf_Analog.Length > s32_Samples)
                        Array.Resize(ref i_Channel.mf_Analog, s32_Samples);
                    i_Capture.mi_Channels.Add(i_Channel);
                }
                else
                {
                    Byte[] u8_Pod = i_RxChan.mu8_Pod;
                    if (u8_Pod.Length > s32_Samples)
                        Array.Resize(ref u8_Pod, s32_Samples);
                    Rigol.AppendPodChannels(i_Capture, u8_Pod, 0);
                }
            }
            return i_Capture;
        }

        /// <summary>
        /// The CombiScopes HM1008 / HM1508 / HM2008 have an analog and a digital mode.
        /// Only in digital mode (DSO) the waveforms are stored and can be transferred.
        /// </summary>
        void CheckDigitalMode()
        {
            if (!mb_CombiScope)
                return;

            String s_Mode;
            try
            {
                s_Mode = mi_Scpi.SendStringCommand(":INSTrument:SELect?").Trim().ToUpper();
            }
            catch (TimeoutException)
            {
                mi_Scpi.DiscardInput();
                return; // not supported by this firmware
            }

            if (s_Mode.StartsWith("AO"))
                throw new ArgumentException("The oscilloscope is in analog mode. Switch it to digital mode (DSO) to store and transfer waveforms.");
            if (s_Mode.StartsWith("CT"))
                throw new ArgumentException("The oscilloscope is in component tester mode. Switch it to digital mode (DSO).");
            if (s_Mode.StartsWith("FFT"))
                throw new ArgumentException("The oscilloscope is in FFT mode. Switch it to digital mode (DSO).\n"
                                          + "You can display the spectrum of the transferred channels with 'FFT Spectrum' (right-click on an analog channel).");
        }

        bool IsChannelEnabled(int s32_Chan)
        {
            return mi_Scpi.SendBoolCommand(":CHANnel" + s32_Chan + ":STATe?");
        }

        // ===============================================================================================
        //                                   :TRACe (CombiScope)
        // ===============================================================================================

        /// <summary>
        /// Reads one analog channel with the :TRACe commands. See comment at the top of this file.
        /// returns null if the channel has no data.
        /// Throws AbortException if the user has aborted.
        /// </summary>
        RxChannel ReadChannelTrace(int s32_Chan, bool b_Memory)
        {
            String s_Progress = "Channel " + s32_Chan;
            mi_Form.PrintStatus(s_Progress + ": Requesting data...", Color.Black);

            // Chaining multiple commands with semicolon is allowed by SCPI and documented by Hameg.
            // FORMat BYTE transfers the 8 bit A/D values which is much faster over RS232 than ASCII.
            mi_Scpi.SendOpcCommand(String.Format(":TRACe:SOURce CH{0};:TRACe:FORMat BYTE;:TRACe:POINts {1}",
                                                 s32_Chan, b_Memory ? "MAX" : "DEF"));

            // In envelope or peak detect mode each point consists of a minimum and a maximum value
            try
            {
                if (mi_Scpi.SendStringCommand(":TRACe:TYPE?").ToUpper().StartsWith("MINM"))
                    throw new ArgumentException("Envelope and peak detect waveforms (Min/Max) cannot be transferred.\n"
                                              + "Select the acquisition mode 'Refresh' and turn off peak detect.");
            }
            catch (TimeoutException)
            {
                mi_Scpi.DiscardInput(); // command not supported by this firmware
            }

            mi_Form.Progr.Init(s_Progress, 0);
            Byte[] u8_Data = mi_Scpi.SendBlockCommand(":TRACe:DATA?", MAX_BLOCK_SIZE, DATA_TIMEOUT, mi_Form.Progr);
            if (u8_Data == null)
                return null;

            // "#10" = empty block: the channel is not displayed or the CombiScope is in analog mode.
            if (u8_Data.Length == 0)
                return null;

            double d_IncY  = mi_Scpi.SendDoubleCommand(":TRACe:YINCrement?");
            double d_OrigY = mi_Scpi.SendDoubleCommand(":TRACe:YORigin?");
            double d_RefY  = mi_Scpi.SendDoubleCommand(":TRACe:YREFerence?");
            double d_IncX  = mi_Scpi.SendDoubleCommand(":TRACe:XINCrement?");

            // U = (Data - YREFerence) * YINCrement + YORigin
            float[] f_Analog = new float[u8_Data.Length];
            for (int S=0; S<u8_Data.Length; S++)
            {
                f_Analog[S] = (float)((u8_Data[S] - d_RefY) * d_IncY + d_OrigY);
            }

            RxChannel i_RxChan = new RxChannel();
            i_RxChan.ms_Name       = "CH" + s32_Chan;
            i_RxChan.mf_Analog     = f_Analog;
            i_RxChan.md_SampleDist = ToDecimal(d_IncX);
            return i_RxChan;
        }

        // ===============================================================================================
        //                                  :CHANnel:DATA (HMO)
        // ===============================================================================================

        /// <summary>
        /// Reads one analog channel with the :CHANnel:DATA commands. See comment at the top of this file.
        /// returns null if the scope does not support these commands.
        /// Throws AbortException if the user has aborted.
        /// </summary>
        RxChannel ReadChannelHmo(int s32_Chan, bool b_Memory)
        {
            String s_Progress = "Channel " + s32_Chan;
            mi_Form.PrintStatus(s_Progress + ": Requesting data...", Color.Black);

            String s_Chan = ":CHANnel" + s32_Chan;
            mi_Scpi.SendOpcCommand(s_Chan + ":DATA:POINts " + (b_Memory ? "DMAX" : "DEF"));

            // "-9.477E-08,9.477E-08,200000,1" = start time, stop time, count of samples, values per sample
            String s_Header;
            try
            {
                s_Header = mi_Scpi.SendStringCommand(s_Chan + ":DATA:HEADer?", 2000);
            }
            catch (TimeoutException)
            {
                mi_Scpi.DiscardInput();
                return null; // not supported --> caller falls back to :TRACe
            }

            // The byte order of the floats must match this computer
            String s_Order = BitConverter.IsLittleEndian ? "LSBF" : "MSBF";
            mi_Form.Progr.Init(s_Progress, 0);
            Byte[] u8_Data = mi_Scpi.SendBlockCommand(":FORMat:BORDer " + s_Order + ";:FORMat REAL,32;" + s_Chan + ":DATA?",
                                                      MAX_BLOCK_SIZE, DATA_TIMEOUT, mi_Form.Progr);
            if (u8_Data == null)
                return null;

            if (u8_Data.Length % 4 != 0)
                throw new Exception("The oscilloscope has sent invalid float data for channel " + s32_Chan);

            float[] f_Analog = new float[u8_Data.Length / 4];
            for (int S=0; S<f_Analog.Length; S++)
            {
                f_Analog[S] = BitConverter.ToSingle(u8_Data, S * 4);
            }

            RxChannel i_RxChan = new RxChannel();
            i_RxChan.ms_Name       = "CH" + s32_Chan;
            i_RxChan.mf_Analog     = f_Analog;
            i_RxChan.md_SampleDist = CalcSampleDistHmo(s_Header, f_Analog.Length);
            return i_RxChan;
        }

        /// <summary>
        /// Reads the 8 logic channels of POD 1 of a HMO.
        /// returns null if the POD is off, not supported
        /// Throws AbortException if the user has aborted.
        /// </summary>
        RxChannel ReadPodHmo(bool b_Memory)
        {
            try
            {
                if (!mi_Scpi.SendBoolCommand(":POD1:STATe?"))
                    return null;
            }
            catch (TimeoutException)
            {
                mi_Scpi.DiscardInput();
                return null; // scope without logic channels
            }

            String s_Progress = "Logic Pod";
            mi_Form.PrintStatus(s_Progress + ": Requesting data...", Color.Black);

            mi_Scpi.SendOpcCommand(":POD1:DATA:POINts " + (b_Memory ? "DMAX" : "DEF"));

            String s_Header = null;
            try
            {
                s_Header = mi_Scpi.SendStringCommand(":POD1:DATA:HEADer?", 2000);
            }
            catch (TimeoutException)
            {
                mi_Scpi.DiscardInput();
            }

            mi_Form.Progr.Init(s_Progress, 0);
            Byte[] u8_Data = mi_Scpi.SendBlockCommand(":FORMat UINT,8;:POD1:DATA?", MAX_BLOCK_SIZE, DATA_TIMEOUT, mi_Form.Progr);
            if (u8_Data == null || u8_Data.Length == 0)
                return null;

            RxChannel i_RxChan = new RxChannel();
            i_RxChan.mu8_Pod       = u8_Data;
            i_RxChan.md_SampleDist = (s_Header != null) ? CalcSampleDistHmo(s_Header, u8_Data.Length) : 0;
            return i_RxChan;
        }

        /// <summary>
        /// s_Header = "-2.9990E-03,3.0000E-03,6000,1" = time of first sample, time of last sample, count of samples, values per sample
        /// If the header cannot be parsed, the sample distance is calculated from the timebase.
        /// </summary>
        decimal CalcSampleDistHmo(String s_Header, int s32_Samples)
        {
            String[] s_Parts = s_Header.Split(',');
            double d_Start, d_Stop;
            if (s_Parts.Length >= 3 &&
                double.TryParse(s_Parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out d_Start) &&
                double.TryParse(s_Parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out d_Stop)  &&
                d_Stop > d_Start && s32_Samples > 1)
            {
                // Start and stop are the times of the first and the last sample.
                // HMO1522 at 1 MSa/s: "-2.9990E-03,3.0000E-03,6000,1" --> 5999 intervals of 1 µs
                return ToDecimal((d_Stop - d_Start) / (s32_Samples - 1));
            }

            // Fallback: The waveform fills the entire screen width
            double d_Scale = mi_Scpi.SendDoubleCommand(":TIMebase:SCALe?");
            double d_Divs  = mi_Scpi.SendDoubleCommand(":TIMebase:DIVisions?");
            return ToDecimal(d_Scale * d_Divs / s32_Samples);
        }

        /// <summary>
        /// Returns 0 for invalid values like NaN or 9.91E+37 which SCPI uses for "not available"
        /// </summary>
        static decimal ToDecimal(double d_Value)
        {
            if (double.IsNaN(d_Value) || double.IsInfinity(d_Value) || d_Value <= 0 || d_Value > 1e15)
                return 0;
            return (decimal)d_Value;
        }
    }
}
