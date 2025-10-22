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


// This class implements communication with a Rigol oscilloscope.
// Sadly there is no standard for this communication so each vendor cooks his own soup.
// It neither make sense to derive this class from a base class nor an interface because for another brand everything is different.
// So to add a future oscilloscope brand write a new communication class like this and create a new Form with the control elements.
// Rigol is so fucking inconsistent that for each oscilloscope serie other commands are used and different code must be written!
namespace Transfer
{
    /// <summary>
    /// This class implements the most important SCPI commands for Rigol oscilloscopes.
    /// </summary>
    public class Rigol
    {
        #region enums

        enum eCmd
        {
            GetIdent = 0,
            Reset,
            GetSystemError,
            Unlock,
            Run,
            Stop,
            Clear,
            Auto,
            Single,
            ForceTrigger,
            GetTrigStatus,
            GetSampleRate,
            GetMemDepth,
            GetChanMemDepth,
            GetChanScale,
            GetChanOffset,
            SetWaveMode,
            SetWaveFormat,
            SetWaveSource,
            SetWaveStart,
            SetWaveStop,
            GetWavePreamble,
            GetWaveData,
            GetMathDisplay,
            GetAnalDisplay,
            GetDigiDisplay,
            GetPodDisplay,
            GetTimebaseScale,
            // ----------------
            COUNT,
        }

        public enum eOperation
        {
            Run          = eCmd.Run,
            Stop         = eCmd.Stop,
            Clear        = eCmd.Clear,
            Auto         = eCmd.Auto,
            Single       = eCmd.Single,
            ForceTrigger = eCmd.ForceTrigger,
            Reset        = eCmd.Reset,
        }

        #endregion

        #region embedded classes

        class FixData
        {
            public int        ms32_HorRasterLines;   // The count of horizontal squares on the oscilloscope screen
            public int        ms32_HorScreenRes;     // The horizontal screen resolution (for example 1200 or 600)
            public int        ms32_AnalogRes;        // The resolution of the A/D converter in the oscilloscope in bits (Rigol = 8 bit)
            public int        ms32_OpcReplaceDelay;  // See comment of SCPI.OpcReplaceDelay
            public int        ms32_AnalogChannels;   // The count of analog channels
            public int        ms32_Blocksize;        // The size of blocks transferred with :WAVEFORM:DATA?
            public String[]   ms_Commands = new String[(int)eCmd.COUNT];
        }

        public class OsziModel
        {
            public String ms_Brand;
            public String ms_Model;
            public String ms_Serial;
            public String ms_Firmware;
        }

        public class OsziConfig
        {
            public decimal md_SampleRate;     // in Hertz
            public int     ms32_SamplePoints;
            public Int64   ms64_Duration;     // in pico seconds
        }

        class Preamble
        {
            public int    ms32_Format;
            public int    ms32_Type;
            public int    ms32_SamplePoints;
            public int    ms32_Averages;
            // --------- HOR -----------
            public double md_IncrementX;
            public double md_OriginX;
            public double md_ReferenceX;
            // --------- VERT ----------
            public float  mf_IncrementY;
            public int    ms32_OriginY;
            public int    ms32_ReferenceY;
            // --------------------
            public Int64  ms64_Duration;     // in pico seconds
        }

        #endregion

        FixData       mi_FixData;
        FormTransfer  mi_Form;
        eOsziSerie    me_Serie;
        SCPI          mi_Scpi;
        bool          mb_Abort;
        OsziModel     mi_OsziModel;
        int           ms32_LastTick;
        int           ms32_StartTick;

        /// <summary>
        /// returns null if no valid response to *IDN?
        /// </summary>
        public OsziModel Model
        {
            get { return mi_OsziModel; }
        }

        /// <summary>
        /// Abort waveform transfer from the oscilloscope
        /// </summary>
        public void AbortTransfer()
        {
            mb_Abort = true;
        }

        #region Constructor

        /// <summary>
        /// constructor
        /// </summary>
        public Rigol(eOsziSerie e_Serie)
        {
            me_Serie   = e_Serie;
            mi_FixData = new FixData();

            // This table of commands is sadly required because Rigol is such a STUPID Chinese SHIT company that 
            // they are not able to define the same commands for all their oscilloscopes!
            //
            // NOTE: You dont have to care about the UPPERcase/LOWERcase mix in the Rigol manuals.
            // This only shows the characters that are optional. 
            // For example "CHANnel" means that you can send either "CHANNEL" or only "CHAN".
            // There is no advantage using the abbreviations. They only make the commands unreadable: ":LA:POS:RES" --> WTF?

            if (e_Serie == eOsziSerie.Rigol_1000DE)
            {
                mi_FixData.ms32_HorScreenRes    = 600;
                mi_FixData.ms32_HorRasterLines  = 12;
                mi_FixData.ms32_AnalogRes       = 8;   // The oscilloscope uses an 8 bit A/D converter
                mi_FixData.ms32_OpcReplaceDelay = 100; // command "*OPC?" is not supported, make a pause of 100 ms instead.
                mi_FixData.ms32_AnalogChannels  = 2;   // This serie has 2 analog channels
                mi_FixData.ms32_Blocksize       = 0;   // Transfer in blocks is not available for :WAVEFORM:DATA?
                // ---------------------------------
                mi_FixData.ms_Commands[(int)eCmd.GetIdent]          = "*IDN?";
                mi_FixData.ms_Commands[(int)eCmd.Reset]             = "*RST";
                mi_FixData.ms_Commands[(int)eCmd.GetSystemError]    = null;
                mi_FixData.ms_Commands[(int)eCmd.Unlock]            = ":KEY:FORCe";
                // ---------------------------------
                mi_FixData.ms_Commands[(int)eCmd.Run]               = ":RUN";
                mi_FixData.ms_Commands[(int)eCmd.Stop]              = ":STOP";
                mi_FixData.ms_Commands[(int)eCmd.Clear]             = ":DISPlay:CLEar";
                mi_FixData.ms_Commands[(int)eCmd.Auto]              = ":AUTO";
                mi_FixData.ms_Commands[(int)eCmd.Single]            = ":TRIGger:EDGE:SWEep SINGLE";
                mi_FixData.ms_Commands[(int)eCmd.ForceTrigger]      = ":FORCetrig";
                // ---------------------------------
                mi_FixData.ms_Commands[(int)eCmd.GetTrigStatus]     = ":TRIGger:STATus?";
                mi_FixData.ms_Commands[(int)eCmd.GetSampleRate]     = ":ACQuire:SAMPlingrate?";
                mi_FixData.ms_Commands[(int)eCmd.GetMemDepth]       = ":ACQuire:MEMDepth?";
                mi_FixData.ms_Commands[(int)eCmd.GetChanMemDepth]   = ":CHAN{0}:MEMD?";
                mi_FixData.ms_Commands[(int)eCmd.GetChanScale]      = ":CHAN{0}:SCAL?";
                mi_FixData.ms_Commands[(int)eCmd.GetChanOffset]     = ":CHAN{0}:OFFS?";
                mi_FixData.ms_Commands[(int)eCmd.SetWaveMode]       = ":WAVeform:POINts:MODE {0}";
                mi_FixData.ms_Commands[(int)eCmd.SetWaveFormat]     = null;
                mi_FixData.ms_Commands[(int)eCmd.SetWaveSource]     = null;
                mi_FixData.ms_Commands[(int)eCmd.SetWaveStart]      = null;
                mi_FixData.ms_Commands[(int)eCmd.SetWaveStop]       = null;
                mi_FixData.ms_Commands[(int)eCmd.GetWavePreamble]   = null;
                mi_FixData.ms_Commands[(int)eCmd.GetWaveData]       = ":WAVeform:DATA? CHAN{0}";
                mi_FixData.ms_Commands[(int)eCmd.GetMathDisplay]    = ":MATH:DISPlay?";
                mi_FixData.ms_Commands[(int)eCmd.GetAnalDisplay]    = ":CHANnel{0}:DISPlay?";
                mi_FixData.ms_Commands[(int)eCmd.GetDigiDisplay]    = ":DIGital{0}:TURN?";
                mi_FixData.ms_Commands[(int)eCmd.GetPodDisplay]     = null;
                mi_FixData.ms_Commands[(int)eCmd.GetTimebaseScale]  = ":TIMebase:SCALe?";
                return;
            }
            
            // See file "Logfile SCPI Commands DS1074Z.txt" in subfolder "Documentation" showing these commands in action.
            if (e_Serie == eOsziSerie.Rigol_1000Z)
            {
                mi_FixData.ms32_HorScreenRes    = 1200;
                mi_FixData.ms32_HorRasterLines  = 12;     // Using :SYSTem:GAM? does not make sense because the result is always 12.
                mi_FixData.ms32_AnalogRes       = 8;      // The oscilloscope uses an 8 bit A/D converter
                mi_FixData.ms32_OpcReplaceDelay = 0;      // command "*OPC?" is supported.
                mi_FixData.ms32_AnalogChannels  = 4;      // This serie has 4 analog channels
                mi_FixData.ms32_Blocksize       = 250000; // maximum 250 kB can be transferred in one block with :WAVEFORM:DATA?
                // ---------------------------------
                mi_FixData.ms_Commands[(int)eCmd.GetIdent]          = "*IDN?";
                mi_FixData.ms_Commands[(int)eCmd.Reset]             = "*RST";
                mi_FixData.ms_Commands[(int)eCmd.GetSystemError]    = ":SYSTEM:ERROR?";
                mi_FixData.ms_Commands[(int)eCmd.Unlock]            = null;
                // ---------------------------------
                mi_FixData.ms_Commands[(int)eCmd.Run]               = ":RUN";
                mi_FixData.ms_Commands[(int)eCmd.Stop]              = ":STOP";
                mi_FixData.ms_Commands[(int)eCmd.Clear]             = ":CLEAR";
                mi_FixData.ms_Commands[(int)eCmd.Auto]              = ":AUTOSCALE";
                mi_FixData.ms_Commands[(int)eCmd.Single]            = ":SINGLE";
                mi_FixData.ms_Commands[(int)eCmd.ForceTrigger]      = ":TFORCE";
                // ---------------------------------
                mi_FixData.ms_Commands[(int)eCmd.GetTrigStatus]     = ":TRIGGER:STATUS?";
                mi_FixData.ms_Commands[(int)eCmd.GetSampleRate]     = ":ACQUIRE:SRATE?";
                mi_FixData.ms_Commands[(int)eCmd.GetMemDepth]       = ":ACQUIRE:MDEPTH?";
                mi_FixData.ms_Commands[(int)eCmd.GetChanMemDepth]   = null;
                mi_FixData.ms_Commands[(int)eCmd.GetChanScale]      = ":CHANNEL{0}:SCALE?";
                mi_FixData.ms_Commands[(int)eCmd.GetChanOffset]     = ":CHANNEL{0}:OFFSET?";
                mi_FixData.ms_Commands[(int)eCmd.SetWaveMode]       = ":WAVEFORM:MODE {0}";
                mi_FixData.ms_Commands[(int)eCmd.SetWaveFormat]     = ":WAVEFORM:FORMAT {0}";
                mi_FixData.ms_Commands[(int)eCmd.SetWaveSource]     = ":WAVEFORM:SOURCE {0}";
                mi_FixData.ms_Commands[(int)eCmd.SetWaveStart]      = ":WAVEFORM:START {0}";
                mi_FixData.ms_Commands[(int)eCmd.SetWaveStop]       = ":WAVEFORM:STOP {0}";
                mi_FixData.ms_Commands[(int)eCmd.GetWavePreamble]   = ":WAVEFORM:PREAMBLE?";
                mi_FixData.ms_Commands[(int)eCmd.GetWaveData]       = ":WAVEFORM:DATA?";
                mi_FixData.ms_Commands[(int)eCmd.GetMathDisplay]    = ":MATH:DISPLAY?";
                mi_FixData.ms_Commands[(int)eCmd.GetAnalDisplay]    = ":CHANNEL{0}:DISPLAY?";
                mi_FixData.ms_Commands[(int)eCmd.GetDigiDisplay]    = ":LA:DIGITAL{0}:DISPLAY?";
                mi_FixData.ms_Commands[(int)eCmd.GetPodDisplay]     = ":LA:DISPLAY? POD{0}";
                mi_FixData.ms_Commands[(int)eCmd.GetTimebaseScale]  = ":TIMEBASE:SCALE?";
                return;
            }
            
            Debug.Assert(false, "Programming Error: Serie not implemented");
        }

        #endregion

        /// <summary>
        /// o_Param may be for example a channel number, inserted into ":CHANNEL{0}:DISPLAY?"
        /// </summary>
        private String GetCmd(eCmd e_Cmd, Object o_Param = null)
        {
            String s_Command = mi_FixData.ms_Commands[(int)e_Cmd];
            if (s_Command == null)
                throw new Exception("The command " + e_Cmd + " is not supported by the serie " + me_Serie);
            
            if (s_Command.IndexOf('{') > 0)
            {
                if (o_Param == null)
                    throw new Exception("The command " + e_Cmd + " expects a parameter.");

                s_Command = String.Format(s_Command, o_Param);
            }
            return s_Command;
        }

        // ==============================================================================

        // May throw
        public void Connect(FormTransfer i_Form, SCPI i_Scpi)
        {
            Debug.Assert(mi_Scpi == null, "Programming Error: Do not call Open() multiple times!");

            mi_Form = i_Form;
            mi_Scpi = i_Scpi;
            mi_Scpi.OpcReplaceDelay = mi_FixData.ms32_OpcReplaceDelay;

            // Important: Execute the command *IDN? immediatley here.
            // If the communication fails, the connection cannot be used and an error must be displayed to the user.
            // After the computer was in sleep mode the following command may fail with error 31 (ERROR_GEN_FAILURE)
            String s_IDN = mi_Scpi.SendStringCommand(GetCmd(eCmd.GetIdent)); // throws

            // returns "RIGOL TECHNOLOGIES,DS1074Z Plus,DS1ZC204807063,00.04.04.SP4"
            String[] s_Parts = s_IDN.Split(',');

            if (s_Parts.Length == 4)
            {
                mi_OsziModel = new OsziModel();
                mi_OsziModel.ms_Brand    = Utils.FirstToUpper(s_Parts[0]);
                mi_OsziModel.ms_Model    = s_Parts[1];
                mi_OsziModel.ms_Serial   = s_Parts[2];
                mi_OsziModel.ms_Firmware = s_Parts[3];
            }
        }

        public void Disconnect()
        {
            try
            {
                // The Rigol DS1000DE is locked in remote control mode while connected to the computer.
                // It must be explicitely unlocked to make the hardware buttons work again.
                mi_Scpi.SendOpcCommand(GetCmd(eCmd.Unlock));
            }
            catch {} // ignore error
        }

        // ===============================================================================================

        /// <summary>
        /// Does not throw.
        /// returns null if no error is reported or the command is not supported by this oscilloscope serie.
        /// The error is removed from the queue of last errors.
        /// The TMC protocol defines: If a device receives an invalid command, it shall not respond.
        /// The other side runs into a timeout, after that it can request with :SYSTEM:ERROR? the reason of the missing response.
        /// This design is sooo STUPID! The SCPI device should ALWAYS send a response either success or an error message.
        /// But SCPI was designed by people who are not very intelligent.
        /// And the oscilloscope internally has a queue of last errors.
        /// Also this is a stupid design. You don't know to which command corresponds which error message.
        /// </summary>
        public String GetLastError()
        {
            String s_CmdSysErr = mi_FixData.ms_Commands[(int)eCmd.GetSystemError];
            if (s_CmdSysErr == null)
                return null; // The oscilloscope does not support the command

            try
            {
                // :SYSTEM:ERROR? returns "0,\"No error\"\n"
                // :SYSTEM:ERROR? returns "-220,\"Parameter error\"" after sending an invalid command like ":CHANNEL9:DISPLAY?"
                String   s_Resp  = mi_Scpi.SendStringCommand(s_CmdSysErr, 200);
                String[] s_Parts = s_Resp.Split(',');
                if (s_Parts.Length != 2)
                    return s_Resp.Trim('"');
            
                if (s_Parts[0] == "0") return null; // no error
                else                   return s_Parts[1].Trim('"');
            }
            catch 
            {
                // Communication failure --> no error source available.
                return null;
            }
        }

        /// <summary>
        /// If i_Config.ms32_SamplePoints == 0 --> the oscilloscope is not ready.
        /// </summary>
        public OsziConfig GetOsziConfiguration(bool b_Memory)
        {
            decimal d_SampleRate     = (decimal)mi_Scpi.SendDoubleCommand(GetCmd(eCmd.GetSampleRate)); 
            int     s32_SamplePoints = 0;
            Int64   s64_Duration     = 0; // total signal duration in pico seconds

            switch (me_Serie)
            {
                case eOsziSerie.Rigol_1000DE: 
                    // The command :ACQUIRE:MEMDEPTH? is garbage. It returns "NORMAL" or "LONG" instead of the current memory depth.
                    for (int s32_Chan = 1; s32_Chan <= mi_FixData.ms32_AnalogChannels; s32_Chan ++)
                    {
                        // If channel is enabled -> get Channel Memory Depth which is returned as 1048576, 524288, 16384 or 8192
                        if (mi_Scpi.SendBoolCommand(GetCmd(eCmd.GetAnalDisplay, s32_Chan)))
                        {
                            s32_SamplePoints = (int)mi_Scpi.SendDoubleCommand(GetCmd(eCmd.GetChanMemDepth, s32_Chan));
                            break;
                        }
                    }
                    break;

                case eOsziSerie.Rigol_1000Z:  
                    // The Waveform Mode affects the reported SamplePoints
                    mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveMode, b_Memory ? "RAW" : "NORMAL"));

                    // Commamd ":ACQUIRE:MDEPTH?" is garbage. It returns "AUTO" instead of the current memory depth.
                    Preamble  i_Prbl = GetPreamble(false);
                    s32_SamplePoints = i_Prbl.ms32_SamplePoints;
                    break;

                default:
                    throw new NotImplementedException();
            }
                
            // ATTENTION:    if (b_Memory)    does not work here!
            if (s32_SamplePoints > mi_FixData.ms32_HorScreenRes) // Raw Memory mode
            {
                if (d_SampleRate > 0) 
                    s64_Duration = (Int64)(Utils.PICOS_PER_SECOND / d_SampleRate * s32_SamplePoints);
            }
            else // Screen mode
            {
                // returns 0.02 when 20 ms per raster line is selected
                decimal d_TotTime = (decimal)mi_Scpi.SendDoubleCommand(GetCmd(eCmd.GetTimebaseScale)) * mi_FixData.ms32_HorRasterLines;
                s64_Duration = (Int64)(d_TotTime * Utils.PICOS_PER_SECOND);
            }

            OsziConfig i_Config = new OsziConfig();
            i_Config.md_SampleRate     = d_SampleRate;     // in Hertz
            i_Config.ms32_SamplePoints = s32_SamplePoints;
            i_Config.ms64_Duration     = s64_Duration;     // in pico seconds
            return i_Config;
        }

        /// <summary>
        /// ATTENTION:
        /// A correct response looks like: 
        /// ":WAVEFORM:PREAMBLE?" --> "0,2,12000000,1,2.000000e-04,-1.110000e-01,0,0.000000e+00,0,0" 
        /// But there may come invalid responses where SamplePoints == 0
        /// ":WAVEFORM:PREAMBLE?" --> "0,2,0,1,0.000000,0.000000,0,0.000000,0,0"
        /// This may happen after switching from STOP to RUN mode while data is not yet available and a black screen is shown.
        /// It also happens after executing the :CLEAR command.
        /// See file "Logfile SCPI Commands DS1074Z.txt" in subfolder "Documentation"
        /// </summary>
        Preamble GetPreamble(bool b_ThrowNotReady)
        {
            String s_Preamble = mi_Scpi.SendStringCommand(GetCmd(eCmd.GetWavePreamble));
            String[]  s_Parts = s_Preamble.Split(',');
            if (s_Parts.Length != 10)
                throw new Exception("Invalid Preamble: " + s_Preamble);

            int s32_SamplePoints = int.Parse(s_Parts[2]);
            if (s32_SamplePoints == 0 && b_ThrowNotReady)
                throw new Exception("The oscilloscope is not ready.\nIs there a captured waveform on the screen?");

            // in seconds (e.g. 0.00000004)
            double d_IncrementX = double.Parse(s_Parts[4], NumberStyles.Float, CultureInfo.InvariantCulture);

            Preamble i_Preamble = new Preamble();
            i_Preamble.ms32_Format       = int.Parse(s_Parts[0]); // enum BYTE, WORD, ASC      (not used)
            i_Preamble.ms32_Type         = int.Parse(s_Parts[1]); // enum NORMAL, MAXIMUM, RAW (not used)
            i_Preamble.ms32_SamplePoints = s32_SamplePoints;
            i_Preamble.ms32_Averages     = int.Parse(s_Parts[3]);
            // --------------------
            i_Preamble.md_IncrementX     = d_IncrementX; 
            i_Preamble.md_OriginX        = double.Parse(s_Parts[5], NumberStyles.Float, CultureInfo.InvariantCulture);
            i_Preamble.md_ReferenceX     = double.Parse(s_Parts[6], NumberStyles.Float, CultureInfo.InvariantCulture);
            // --------------------
            i_Preamble.mf_IncrementY     = float.Parse(s_Parts[7],  NumberStyles.Float, CultureInfo.InvariantCulture);
            i_Preamble.ms32_OriginY      = int.  Parse(s_Parts[8]);
            i_Preamble.ms32_ReferenceY   = int.  Parse(s_Parts[9]);
            // --------------------
            // duration of the entire signal in picoseconds
            i_Preamble.ms64_Duration = (Int64)((decimal)d_IncrementX * Utils.PICOS_PER_SECOND * s32_SamplePoints);
            return i_Preamble;
        }

        // ===============================================================================================
        
        /// <summary>
        /// Execute Control commands like :RUN, :STOP, :AUTO, etc.
        /// </summary>
        public void ExecuteOperation(eOperation e_Operation)
        {
            String s_Cmd = GetCmd((eCmd)e_Operation);

            mi_Form.PrintStatus("Command   " + s_Cmd, Color.Blue);

            mi_Scpi.SendOpcCommand(s_Cmd);
        }

        // ===============================================================================================

        /// <summary>
        /// b_Memory = true  --> copy the entire memory (high resolution) Only possible in STOP mode and if no MATH is running!
        /// b_Memory = false --> copy only the part of the signal that is visible on the screen (low resolution)
        /// </summary>
        public Capture CaptureAllChannels(bool b_Memory)
        {
            mb_Abort = false;

            // returns TD, WAIT, RUN, AUTO or STOP
            String s_Status = mi_Scpi.SendStringCommand(GetCmd(eCmd.GetTrigStatus));
            if (s_Status != "STOP")
                throw new ArgumentException("The oscilloscope is not able to transfer multiple channels at once "
                                          + "while in RUN mode. The oscilloscope must be in STOP mode otherwise you see all channels "
                                          + "out of sync because the stupid Chinese capture one life channel, transfer it, "
                                          + "then capture the next life channel and transfer it, etc.");

            if (b_Memory && mi_Scpi.SendBoolCommand(GetCmd(eCmd.GetMathDisplay)))
                throw new ArgumentException("During a MATH operation it is not possible to capture the entire memory.");

            OsziConfig i_Config = GetOsziConfiguration(b_Memory);
            if (i_Config.ms32_SamplePoints == 0)
                throw new Exception("The oscilloscope is not ready.\nIs there a captured waveform on the screen?");

            Capture i_Capture = new Capture();
            i_Capture.ms32_AnalogRes = mi_FixData.ms32_AnalogRes; // A/D converter resolution

            // The GARBAGE made in China reports different total durations if analog and digital channels are captured at once.
            // For analog the correct time:        480.000.000 ns -> 480 ms
            // For digital completely wrong: 2.400.000.000.000 ns -> 40 minutes !
            // This happens on DS1074Z when capturing with 12 Megasamples at 25 Mhz with 20 ms per raster division.
            List<Int64> i_Durations = new List<Int64>();

            switch (me_Serie)
            {
                case eOsziSerie.Rigol_1000DE: 
                    CaptureAnalog_Serie_1000DE(i_Capture, b_Memory, i_Durations);
                    if (mb_Abort)
                       return null;

                    // TODO: Digital not implemented.
                    break;

                case eOsziSerie.Rigol_1000Z:  
                    CaptureAnalog_Serie_1000Z(i_Capture, b_Memory, i_Durations);
                    if (mb_Abort)
                       return null;

                    if (b_Memory) CaptureDigitalFromMemory_Serie_1000Z(i_Capture, i_Durations);
                    else          CaptureDigitalFromScreen_Serie_1000Z(i_Capture, i_Durations);
                    break;
            }

            if (mb_Abort)
               return null;

            if (i_Capture.mi_Channels.Count == 0)
                throw new Exception("All channels are turned off or there are digital channels that have no data.");

            // --------- Workaround for serious bug in Rigol SHIT ----------

            if (!i_Durations.Contains(i_Config.ms64_Duration))
                 i_Durations.Add(i_Config.ms64_Duration);

            // There are 3 total durations of the entire capture calculated from different input: 
            // 1.) From the preamble of an analog channel --> 480 ms     (correct)
            // 2.) From the preamble of a digital channel --> 40 minutes (Chinese CRAP)
            // 3.) From GetOsziConfiguration()            --> 480 ms     (correct)
            if (i_Durations.Count > 1)
            {
                String s_Error = "Your shitty chinese crap oscilloscope has sent contradicting total durations for the same signal capture:\n";
                foreach (Int64 s64_Time in i_Durations)
                {
                    s_Error += Utils.FormatTimePico(s64_Time) + "\n";
                }
                MessageBox.Show(mi_Form, s_Error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // The value from GetOsziConfiguration() is correct (at least on my scope with my firmware version)
            // The disadvantage is that the calculation depends on the count of horizontal raster lines 
            // which cannot be obtained by any SCPI command.
            //
            // ATTENTION: When capturing only the visible screen, the sample distance is NOT the samplerate displayed by the oscillscope.
            // The sample distance is always the time between 2 neighboured samples.
            // If you have 1200 samples and a total captured interval of 600 ms the sample distance is 500 µs 
            // and not the 25 MHz (40 ns) displayed by the oscilloscope.
            i_Capture.ms64_SampleDist = (i_Config.ms64_Duration / i_Capture.ms32_Samples); 
            return i_Capture;
        }

        // =============================================================================================

        /// <summary>
        /// Rigol is so fucking inconsistent that we must write a different function for eaach oscilloscope serie.
        /// The commands and the data transfer are totally different.
        /// NOTE: It does not make sense to work with double's for voltages because the oscilloscope has only an A/D resolution of 8 bits.
        /// A double uses 8 byte in memory while a float uses 4 bytes.
        /// The screen of the 1000DE serie does not even have 256 pixels vertical resolution. Each raster square displays 
        /// only 25 vertical pixels on the LCD display, so with 8 vertical rasters we get 8 * 25 = 200 pixels.
        /// </summary>
        private void CaptureAnalog_Serie_1000DE(Capture i_Capture, bool b_Memory, List<Int64> i_Durations)
        {
            mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveMode, b_Memory ? "RAW" : "NORMAL"));

            // Rigol is inconsistent: First analog channel is 1, first digital channel is 0.
            for (int s32_AnalChan = 1; s32_AnalChan <= mi_FixData.ms32_AnalogChannels; s32_AnalChan++)
            {
                // Check if the channel is enabled
                if (!mi_Scpi.SendBoolCommand(GetCmd(eCmd.GetAnalDisplay, s32_AnalChan), 250))
                    continue; // Channel is turned off

                // The serie 1000DE does not support a progress display in the status bar showing how many samples have been tranmsitted.
                String s_Msg = String.Format("Transfer Analog Channel {0}.  Please wait.", s32_AnalChan);
                mi_Form.PrintStatus(s_Msg, Color.Black);

                // Allocate enough Receive buffer!
                // It is extremely important that the buffer is not too small to hold the ENTIRE response from the device!
                // Documentation says (PDF page 82) that max 1048576 bytes can be read --> 2 MB buffer is enough.
                Byte[] u8_Data = mi_Scpi.SendByteCommand(eBinaryTCP.Linefeed, 2000000, GetCmd(eCmd.GetWaveData, s32_AnalChan));

                // Rigol is SOO INCREDIBLY STUPID that they send always only 600 samples in the first command even in RAW mode.
                // So we must send this command twice to get the full memory resolution!
                // See "Rigol DS1000E Waveform Guide.htm" in subfolder "Documentation"
                if (b_Memory && u8_Data.Length <= mi_FixData.ms32_HorScreenRes)
                    u8_Data = mi_Scpi.SendByteCommand(eBinaryTCP.Linefeed, 2000000, GetCmd(eCmd.GetWaveData, s32_AnalChan));

                if (mb_Abort)
                    return;

                double d_Scale  = mi_Scpi.SendDoubleCommand(GetCmd(eCmd.GetChanScale,  s32_AnalChan));
                double d_Offset = mi_Scpi.SendDoubleCommand(GetCmd(eCmd.GetChanOffset, s32_AnalChan));

                Channel i_Channel   = new Channel("Analog " + s32_AnalChan);
                i_Channel.mf_Analog = ConvertRawBytesToAnalog_Serie_1000DE(u8_Data, (float)d_Scale, (float)d_Offset);
                i_Capture.mi_Channels.Add(i_Channel);

                i_Capture.ms32_Samples = i_Channel.mf_Analog.Length;
            }
        }

        // =============================================================================================

        /// <summary>
        /// Rigol is so fucking inconsistent that we must write different functions for eaach oscilloscope serie.
        /// And as if this was not stupid enough, the digital channels are transferred completely different 
        /// from screen and memory for the SAME oscilloscope!
        /// NOTE: It does not make sense to work with double's for voltages because the oscilloscope has only an A/D resolution of 8 bits.
        /// A double uses 8 byte in memory while a float uses 4 bytes.
        /// See file "Logfile SCPI Commands DS1074Z.txt" in subfolder "Documentation" showing an analg WAVEFORM transfer.
        /// </summary>
        void CaptureAnalog_Serie_1000Z(Capture i_Capture, bool b_Memory, List<Int64> i_Durations)
        {
            // Rigol is inconsistent: First analog channel is 1, first digital channel is 0.
            for (int s32_AnalChan = 1; s32_AnalChan <= mi_FixData.ms32_AnalogChannels; s32_AnalChan++)
            {
                // Check if the channel is enabled
                if (!mi_Scpi.SendBoolCommand(GetCmd(eCmd.GetAnalDisplay, s32_AnalChan), 250))
                    continue; // Channel is turned off

                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveMode, b_Memory ? "RAW" : "NORMAL"));
                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveSource, "CHANNEL" + s32_AnalChan));
                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveFormat, "BYTE"));

                Preamble i_Prbl = GetPreamble(true);
                String s_DisplayName = "Analog " + s32_AnalChan;

                Byte[] u8_RawData = ReadRawByteDataStartStop(s_DisplayName, i_Prbl.ms32_SamplePoints);
                if (mb_Abort)
                    return;

                Channel i_Channel   = new Channel(s_DisplayName);
                i_Channel.mf_Analog = ConvertRawBytesToAnalog_Serie_1000Z(u8_RawData, i_Prbl.ms32_OriginY, i_Prbl.ms32_ReferenceY, i_Prbl.mf_IncrementY);
                i_Capture.mi_Channels.Add(i_Channel);

                i_Capture.ms32_Samples = i_Prbl.ms32_SamplePoints;

                if (!i_Durations.Contains(i_Prbl.ms64_Duration))
                     i_Durations.Add(i_Prbl.ms64_Duration);
            }
        }

        /// <summary>
        /// Rigol is so fucking inconsistent that we must write different functions for eaach oscilloscope serie.
        /// And as if this was not stupid enough, the digital channels are transferred completely different 
        /// from screen and memory by the SAME oscilloscope!
        /// </summary>
        void CaptureDigitalFromScreen_Serie_1000Z(Capture i_Capture, List<Int64> i_Durations)
        {
            // The Chinese GARBAGE SHIT from Rigol reports digital channels D0 .. D7 to be active although not even the Logic Probe is connected.
            // The command ":LA:DISPLAY? D0" reports always "1".
            // But command ":LA:DISPLAY? POD1" reports correctly that channels D0..D7 are off.
            // The strange thing is that 4 of the digital channels even have data which seems to be derived from the analog capture.
            bool b_Pod1 = mi_Scpi.SendBoolCommand(GetCmd(eCmd.GetPodDisplay, 1), 250);
            bool b_Pod2 = mi_Scpi.SendBoolCommand(GetCmd(eCmd.GetPodDisplay, 2), 250);

            // Capture from screen --> Read 1 digital channel at once
            // Rigol is inconsistent: First analog channel is 1, first digital channel is 0.
            for (int s32_DigiChan = 0; s32_DigiChan < 16; s32_DigiChan++)
            {
                // Workaround for another ugly bug in the Chinese CRAP.
                if (s32_DigiChan <  8 && !b_Pod1) continue;
                if (s32_DigiChan >= 8 && !b_Pod2) continue;

                try
                {
                    // Check if the channel is enabled.
                    // This shit does not work when the Logic Probe is not connected.
                    if (!mi_Scpi.SendBoolCommand(GetCmd(eCmd.GetDigiDisplay, s32_DigiChan), 250))
                        continue; // Channel is turned off
                }
                catch (TimeoutException)
                {
                    break; // Channel does not exist on this model
                }

                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveMode, "NORMAL")); // Screen mode
                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveSource, "D" + s32_DigiChan));

                Preamble i_Prbl = GetPreamble(true);
                String s_DisplayName = "Digital " + s32_DigiChan;

                // This is stupid. Only Bit 0 of the raw data is used.
                // This could be 8 times faster if there would be any intelligence inside Rigol.
                Byte[] u8_RawData = ReadRawByteDataStartStop(s_DisplayName, i_Prbl.ms32_SamplePoints);
                if (mb_Abort)
                    return;

                if (IsDigitalChannelEmpty(u8_RawData))
                    continue; // skip empty channels

                Channel i_Channel     = new Channel("D" + s32_DigiChan);
                i_Channel.mu8_Digital = u8_RawData; // no conversion required (only Bit 0 has data)
                i_Capture.mi_Channels.Add(i_Channel);

                i_Capture.ms32_Samples  = i_Prbl.ms32_SamplePoints;

                if (!i_Durations.Contains(i_Prbl.ms64_Duration))
                     i_Durations.Add(i_Prbl.ms64_Duration);
            }
        }

        /// <summary>
        /// Rigol is so fucking inconsistent that we must write different functions for eaach oscilloscope serie.
        /// And as if this was not stupid enough, the digital channels are transferred completely different 
        /// from screen and from memory by the SAME oscilloscope!
        /// </summary>
        void CaptureDigitalFromMemory_Serie_1000Z(Capture i_Capture, List<Int64> i_Durations)
        {
            // Capture from memory --> Read 8 digital channels at once
            // Pod1 = Channels D0...D7
            // Pod2 = Channels D8...D15
            for (int s32_DigiPod = 1; s32_DigiPod <= 2; s32_DigiPod ++)
            {
                try
                {
                    // Check if the pod is enabled
                    if (!mi_Scpi.SendBoolCommand(GetCmd(eCmd.GetPodDisplay, s32_DigiPod), 250))
                        continue; // Pod is turned off
                }
                catch (TimeoutException)
                {
                    break; // Pod does not exist on this model
                }

                // Request the first channel of the Pod which will return the data of 8 channels
                // Why did stupid Rigol not implement the same also for copying from screen?
                // This is 8 times faster than transferring each channel separately.
                // And we would not need 2 functions to read digital channels.
                int s32_FirstChannel = (s32_DigiPod - 1) * 8;

                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveMode, "RAW")); // Memory mode
                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveSource, "D" + s32_FirstChannel));
                
                Preamble i_Prbl = GetPreamble(true);
                String s_DisplayName = String.Format("Digital {0} - {1}", s32_FirstChannel, s32_FirstChannel + 7);

                Byte[] u8_RawData = ReadRawByteDataStartStop(s_DisplayName, i_Prbl.ms32_SamplePoints);
                if (mb_Abort)
                    return;

                AppendPodChannels(i_Capture, u8_RawData, s32_FirstChannel);

                i_Capture.ms32_Samples = i_Prbl.ms32_SamplePoints;

                if (!i_Durations.Contains(i_Prbl.ms64_Duration))
                     i_Durations.Add(i_Prbl.ms64_Duration);
            }
        }

        /// <summary>
        /// One Pod contains 8 digital channels which are stored in the 8 bits of one byte.
        /// Empty channels which never change their status are skipped.
        /// </summary>
        public static void AppendPodChannels(Capture i_Capture, Byte[] u8_PodData, int s32_FirstChannel)
        {
            for (int s32_Chan=0; s32_Chan<8; s32_Chan++)
            {
                Byte u8_Mask   = (Byte)(1 << s32_Chan);
                bool b_IsEmpty = true;
                bool b_Last    = false;
                Byte[] u8_Digi = new Byte[u8_PodData.Length];

                for (int S=0; S<u8_PodData.Length; S++)
                {
                    bool b_High = (u8_PodData[S] & u8_Mask) > 0;                       
                    if (S > 0 && b_Last != b_High)
                        b_IsEmpty = false;

                    b_Last = b_High;
                    if (b_High) u8_Digi[S] = 1;
                }

                if (b_IsEmpty)
                    continue; // skip empty channels

                Channel i_Channel     = new Channel("D" + (s32_FirstChannel + s32_Chan));
                i_Channel.mu8_Digital = u8_Digi;
                i_Capture.mi_Channels.Add(i_Channel);
            }
        }

        // =============================================================================================

        /// <summary>
        /// The first 10 bytes in u8_RawData are a useless header that must be skipped.
        /// The serie DS1000DE sends the undocumented header: 35 56 48 48 48 48 56 49 57 50  -->  "5VHHHHVIWP"
        /// The voltage is calculated by the following formula:
        /// A(V) = [(240 - <Raw_Byte>) * (<Volts_Div> / 25) - [(<Vert_Offset> + <Volts_Div> * 4.6)]]
        /// See "Rigol DS1000E Waveform Guide.htm" in subfolder "Documentation"
        /// The bytes in u8_RawData do not use all values form 0 to 255, because the vertical resolution is only
        /// 25 pixels per each of the 8 raster squares = 25 x 8 = 200 vertical pixels on the LCD display.
        /// </summary>
        static float[] ConvertRawBytesToAnalog_Serie_1000DE(Byte[] u8_RawData, float f_VoltsDiv, float f_VertOffset)
        {
            const int HEAD_LEN = 10;
            float[] f_Analog = new float[u8_RawData.Length - HEAD_LEN];

            float f_Factor   = f_VoltsDiv / 25.0f;
            float f_Addition = f_VertOffset + f_VoltsDiv * 4.6f;
            for (int S=0; S<f_Analog.Length; S++)
            {
                f_Analog[S] = (240 - u8_RawData[S + HEAD_LEN]) * f_Factor - f_Addition;
            }
            return f_Analog;
        }

        /// <summary>
        /// This function uses the formula desribed in the DS1000Z documentation on PDF page 237 to calculate the Y voltages:
        /// Voltage = (RawByte - OriginY - ReferenceY) * IncrementY 
        /// See subfolder "Documentation"
        /// </summary>
        static float[] ConvertRawBytesToAnalog_Serie_1000Z(Byte[] u8_RawData, int s32_OriginY, int s32_ReferenceY, float f_IncrementY)
        {
            float[] f_Analog = new float[u8_RawData.Length];
            for (int S=0; S<u8_RawData.Length; S++)
            {
                f_Analog[S] = f_IncrementY * (u8_RawData[S] - s32_OriginY - s32_ReferenceY);
            }
            return f_Analog;
        }

        /// <summary>
        /// If a digital channel contains only 0 or only 1 it is empty
        /// </summary>
        static bool IsDigitalChannelEmpty(Byte[] u8_RawData)
        {
            if (u8_RawData == null || u8_RawData.Length == 0)
                return true;

            Byte u8_Last = u8_RawData[0];
            for (int S=1; S<u8_RawData.Length; S++)
            {
                Byte u8_Sample = u8_RawData[S];
                if (u8_Sample != u8_Last)
                    return false;

                u8_Last = u8_Sample;
            }
            return true;
        }

        // =============================================================================================

        /// <summary>
        /// This function may be used for oscilloscope models which support :WAVEFORM:START and :WAVEFORM:STOP (e.g. DS1000Z)
        /// It reads digital data or analog data in BYTE format and removes the Rigol headers from all response packets.
        /// Pass s_DispChan for status display: "Analog 3" or "Digital 12"
        /// s32_Samples must be calculated correctly to match the extact count of samples on the screen or in memory.
        /// s32_BlockSize = 250000 --> request 250 kbyte in one TMC response.
        /// returns null if user has aborted.
        /// ------------------------------------
        /// The DS1000Z has 24 MB memory. Set ms32_BlockSize to read 250000 samples at once.
        /// DS1000Z documentation says (PDF page 235) that in BYTE mode max 250 kB can be transferred in BYTE mode.
        /// </summary>
        Byte[] ReadRawByteDataStartStop(String s_DispChan, int s32_Samples)
        {
            MemoryStream i_RawData = new MemoryStream(s32_Samples);
            ms32_StartTick = Environment.TickCount;

            while (i_RawData.Length < s32_Samples)
            {
                int s32_Remain = Math.Min(mi_FixData.ms32_Blocksize, s32_Samples - (int)i_RawData.Length);

                // ":WAVEFORM:START 1" --> start reading at the first sample
                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveStart, i_RawData.Length + 1));
                mi_Scpi.SendOpcCommand(GetCmd(eCmd.SetWaveStop,  i_RawData.Length + s32_Remain));

                // Request binary data (BYTE mode) from the oscilloscope memory.
                // For USB it is extremely important that the buffer is not too small to hold the ENTIRE response from the device!
                // For TCP add 11 bytes for the Rigol header.
                Byte[] u8_Data = mi_Scpi.SendByteCommand(eBinaryTCP.MinSize, s32_Remain + 11, GetCmd(eCmd.GetWaveData));

                ExtractRigolHeader_Serie_1000Z(u8_Data, i_RawData);
               
                UpdateStatus(s_DispChan, (int)i_RawData.Length);
                if (mb_Abort)
                    return null;
            }
            return i_RawData.ToArray();
        }

        /// <summary>
        /// Example: 1200 samples are requested.
        /// BYTE  mode starts with a header "#9000001200", then 1200 sample bytes, then a linefeed and optionally a padding byte 4F 
        /// ASCII mode starts with a header "#9000015599", then 15599 characters,  then a linefeed and optionally a padding byte 4F
        /// 
        /// In BYTE mode the returned packet is a mix of first ASCII, then binary data and at the end an ASCII linefeed!
        /// How INCREDIBLY STUPID is this?
        /// 
        /// This header must be skipped. The problem is that at the end there may be additional bytes which are not waveform data.
        /// To be sure the header must be parsed which contains the exact length in bytes, not including the garbage at the end.
        /// I have seen other projects where they simply remove 11 bytes at the begin. This is sloppy! The header must be parsed.
        /// 
        /// The header always starts with '#', followed by the count of digits in the following ASCII number: '9'.
        /// 
        /// BTW: The serie DS1000DE sends the undocumented header: 35 56 48 48 48 48 56 49 57 50  -->  "5VHHHHVIWP"
        /// </summary>
        static void ExtractRigolHeader_Serie_1000Z(Byte[] u8_UsbData, MemoryStream i_RawData)
        {
            int s32_Extract = Math.Min(20, u8_UsbData.Length);

            String s_Header = Encoding.ASCII.GetString(u8_UsbData, 0, s32_Extract);
            if (s_Header[0] != '#')
                throw new Exception("Invalid Rigol header received.");

            int    s32_HeadLen = s_Header[1] - '0'; // ASCII '9' = length of the following ASCII number.
            String s_ByteCount = s_Header.Substring(2, s32_HeadLen);

            int s32_ByteCount;
            if (!int.TryParse(s_ByteCount, out s32_ByteCount))
                throw new Exception("Invalid Rigol header received.");

            // '#' + '9' + 9 digits = 11 byte header length
            int s32_FirstByte = 2 + s32_HeadLen;
            if (u8_UsbData.Length < s32_FirstByte + s32_ByteCount)
                throw new Exception("Incomplete data block received.");

            i_RawData.Write(u8_UsbData, s32_FirstByte, s32_ByteCount);
        }

        // ===============================================================================================

        /// <summary>
        /// Update text in Status ToolStrip 3 times per second.
        /// IMPORTANT: PrintStatus() calls Application.DoEvents().
        /// Without calling this frequently the user cannot abort a transfer.
        /// </summary>
        void UpdateStatus(String s_DispChan, int s32_Samples)
        {
            int s32_Now = Environment.TickCount;
            if (Math.Abs(s32_Now - ms32_LastTick) > 250)
            {
                String s_Msg = String.Format("Transfer Channel {0}, Sample {1:N0}", s_DispChan, s32_Samples);

                int s32_Interval = s32_Now - ms32_StartTick; // time in ms --> speed in kB/s
                if (s32_Interval > 200)
                    s_Msg += String.Format(", Speed {0} kB/s",  s32_Samples / s32_Interval);

                mi_Form.PrintStatus(s_Msg, Color.Black);
                ms32_LastTick = Environment.TickCount;
            }
        }
    }
}
