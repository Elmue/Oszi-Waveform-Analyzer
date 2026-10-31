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
using System.IO.Ports;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;

using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using Utils             = OsziWaveformAnalyzer.Utils;
using CsvParser         = ExImport.CsvParser;
using eConnectMode      = Transfer.SCPI.eConnectMode;
using ComboBoxEx        = OsziWaveformAnalyzer.ComboBoxEx;
using ComboItem         = OsziWaveformAnalyzer.ComboBoxEx.ComboItem;
using eComboImg         = OsziWaveformAnalyzer.ComboBoxEx.eComboImg;
using ExImportManager   = ExImport.ExImportManager;

namespace Transfer
{
    /// <summary>
    /// This class manages transferring analog and digital channel data from an oscilloscope over USB.
    /// 
    /// In the future new Forms can be added here that have controls for other oscilloscope models.
    /// It does not amke any sense to derive them from a base class or interface because 
    /// the oscilloscopes and their SCPI commands are COMPLETELY different.
    /// Each company needs it's own classes and Forms.
    /// Each future Form should have a button "Install Driver".
    /// </summary>
    public class TransferManager
    {
        #region enums

        /// <summary>
        /// The Description of this enum is displayed in the same order in the Combobox
        /// Rigol is such an incredibly **STUPID** company that the SCPI commands are not even consistent
        /// within their OWN series of products. What a Chinese crap!
        /// </summary>
        public enum eOsziSerie
        {
            [Description("Rigol DS1000D / DS1000E Series")] // for example DS1102E
            Rigol_1000DE,

            [Description("Rigol DS1000Z / MSO1000Z Series")] // for example DS1074Z
            Rigol_1000Z,  

            [Description("Hameg Compact Series (HMOxxxx)")] // HMO722, HMO1002, HMO1022, HMO1202, HMO1522, HMO2022
            Hameg_HMO1522,

            [Description("Hameg CombiScope Series (HMxxxx)")] // HM1008, HM1508, HM2005-2, HM2008
            Hameg_HM2008,

            [Description("Hameg HM507")] // RS232 only, proprietary binary protocol, also HM504 (analog only, no waveforms)
            Hameg_HM507,

            [Description("OWON VDS 1022 and VDS 2052")]
            OWON_1022,  

            [Description("Picoscope 3206 MSO")]
            Picoscope_3206,

            // TODO: Add more oscilloscope brands like Tektronix, Rhode & Schwarz, Siglent, ...

            // MUST be the last entry!
            [Description("Invalid")]
            Invalid = 10000,
        }

        /// <summary>
        /// Every digit in the 16 bit value stores a RS232 property.
        /// Underscores are replaced with spaces before displaying theses options in the ComboBox.
        /// </summary>
        public enum eRs232Settg
        {
            _8N1_None = (8 << 12) | (Parity.None << 8) | (StopBits.One << 4) | Handshake.None,
            _8N2_None = (8 << 12) | (Parity.None << 8) | (StopBits.Two << 4) | Handshake.None,
            _8N1_RTS  = (8 << 12) | (Parity.None << 8) | (StopBits.One << 4) | Handshake.RequestToSend,
            _8N2_RTS  = (8 << 12) | (Parity.None << 8) | (StopBits.Two << 4) | Handshake.RequestToSend,
        }

        #endregion

        #region class OsziFeatures

        public class OsziFeatures
        {
            // OR of flags of all supported modes
            public eConnectMode me_SupportedModes = (eConnectMode)0;
            
            // These values are loaded into the ComboBoxes / Text fields in FormTransfer
            public String ms_Rs232Settg = "?"; // only valid if RS232 supported
            public String ms_Rs232Baud  = "?"; // only valid if RS232 supported
            public String ms_TcpPort    = "?"; // only valid if TCP   supported
            public String ms_VxiLink    = "?"; // only valid if VXI   supported

            public bool IsModeSupported(eConnectMode e_Mode)
            {
                if (e_Mode == eConnectMode.Invalid)
                    return false;

                return (e_Mode & me_SupportedModes) != 0;
            }

            public String AllModesToString()
            {
                StringBuilder i_Builder = new StringBuilder();
                if (IsModeSupported(eConnectMode.USB))   i_Builder.AppendLine("TMC over USB");
                if (IsModeSupported(eConnectMode.VXI))   i_Builder.AppendLine("VXI over TCP / UDP");
                if (IsModeSupported(eConnectMode.TCP))   i_Builder.AppendLine("SCPI over TCP");
                if (IsModeSupported(eConnectMode.RS232)) i_Builder.AppendLine("RS232 or Virtual COM");
                return i_Builder.ToString().Trim();
            }

            public eConnectMode GetFirstValidMode()
            {
                foreach (eConnectMode e_Mode in Enum.GetValues(typeof(eConnectMode)))
                {
                    if (IsModeSupported(e_Mode))
                        return e_Mode;
                }
                Debug.Assert(false, "No connect mode supported"); // This should never happen
                return eConnectMode.Invalid;
            }
        }

        #endregion

        #region interface ITransferPanel

        /// <summary>
        /// Used for all UserControl's that are displayed in Form Transfer
        /// </summary>
        public interface ITransferPanel
        {
            // Get the default settings for the selected oszi model
            OsziFeatures OsziFeatures { get; }

            // This is called when the Form is opened
            // Must not throw
            void OnLoad(eOsziSerie e_OsziSerie);

            // Open the connection to the oscilloscope, update GUI
            // Throws on error
            void OpenDevice();

            // Close the connection to the oscilloscope, update GUI
            // Must not throw
            void CloseDevice();

            // Sends a command that the user has typed and displays the response in i_TextReponse.
            // Must not throw, but display error in i_TextReponse
            void SendManualCommand(String s_Command, TextBox i_TextReponse);

            // ---------- DC Capture ------------

            // The user has clicked the "DC Capture" button in FormTransfer.
            // d_Interval and d_TotTime are the values that the user has entered in FormTransfer.
            Capture StartDcCapture(decimal d_Interval, decimal d_TotTime);

            // The timer is due --> add a new sample to the capture.
            // i_DcCapture is the Capture that was returned from OnStartDcCapture()
            // s32_CaptureIdx is the sample index in i_DcCapture where to store the new analog value.
            void CaptureDcSample(Capture i_DcCapture, int s32_CaptureIdx);
        }

        #endregion

        public static void FillComboOsziModel(ComboBoxEx i_ComboOsziModel)
        {
            i_ComboOsziModel.Sorted = false; // IMPORTANT!
            i_ComboOsziModel.BeginUpdate();
            i_ComboOsziModel.Items.Clear();
            foreach (eOsziSerie e_Serie in Enum.GetValues(typeof(eOsziSerie)))
            {
                if (e_Serie == eOsziSerie.Invalid)
                    continue;

                eComboImg e_Image = eComboImg.Capture; // Transfer is implemented
                switch (e_Serie)
                {
                    case eOsziSerie.OWON_1022:
                    case eOsziSerie.Picoscope_3206:
                        e_Image = eComboImg.Text; // Only file import is implemented
                        break;
                }

                String s_Descript = Utils.GetDescriptionAttribute(e_Serie);
                i_ComboOsziModel.Items.Add(new ComboItem(s_Descript, e_Image));
            }
            i_ComboOsziModel.EndUpdate();
            Utils.ComboAdjustDropDownWidth(i_ComboOsziModel);

            i_ComboOsziModel.Text = Utils.RegReadString(eRegKey.OsziSerie);
            if (i_ComboOsziModel.SelectedIndex < 0)
                i_ComboOsziModel.SelectedIndex = 0;
        }

        /// <summary>
        /// Opens a Form that allows to capture from the selected oscilloscope over the USB SCPI protocol
        /// Throws
        /// </summary>
        public static void Transfer(ComboBox i_ComboOsziModel)
        {
            Utils.RegWriteString(eRegKey.OsziSerie, i_ComboOsziModel.Text);

            ITransferPanel i_Panel;
            eOsziSerie e_OsziSerie = (eOsziSerie)i_ComboOsziModel.SelectedIndex;
            switch (e_OsziSerie)
            {
                case eOsziSerie.Rigol_1000DE:
                case eOsziSerie.Rigol_1000Z:  
                    i_Panel = new PanelRigol(); 
                    break;
                
                case eOsziSerie.OWON_1022:
                    throw new Exception("For OWON the SCPI transfer is not yet implemented.\nBut you can import a CSV, BIN or CAP file.");

                case eOsziSerie.Picoscope_3206:
                    throw new Exception("For Picoscope 3206 the SCPI transfer is not yet implemented.\nBut you can import a CSV file.");

                case eOsziSerie.Hameg_HMO1522:
                case eOsziSerie.Hameg_HM2008:
                case eOsziSerie.Hameg_HM507:
                    i_Panel = new PanelHameg();
                    break;

                // TODO: Add Tektronix, Rhode & Schwarz, Siglent, ...

                default:
                    Debug.Assert(false, "Programming Error: OsziSerie not implemented");
                    return;
            }

            Form i_Form = new FormTransfer(e_OsziSerie, i_Panel);
            i_Form.ShowDialog(Utils.FormMain);
        }

        public static bool IsInputFile(String s_Extension, ref eComboImg e_Image)
        {
            switch (s_Extension)
            {
                case ".csv":
                    e_Image = eComboImg.Text;
                    return true;
                case ".cap":
                case ".bin":
                case ".wfm":
                    e_Image = eComboImg.Binary;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Parse proprietary vendor-specific CSV or BIN files
        /// Throws
        /// </summary>
        public static Capture ParseVendorFile(String s_Path, ComboBox i_ComboOsziModel, ref bool b_Abort)
        {
            Utils.RegWriteString(eRegKey.OsziSerie, i_ComboOsziModel.Text);

            String     s_FileExt   = Path.GetExtension(s_Path).ToLower();
            eOsziSerie e_OsziSerie = (eOsziSerie)i_ComboOsziModel.SelectedIndex;

            if (s_FileExt == ".wfm")
            {
                ExImportManager.ShowWfmErrorBox();
                return null;
            }

            if (s_FileExt == ".csv")
            {
                CsvParser i_Parser = new CsvParser();
                return i_Parser.Parse(s_Path, e_OsziSerie, ref b_Abort); // throws if CSV import is not implemented
            }

            // Import other formats than CSV
            switch (e_OsziSerie)
            {
                case eOsziSerie.Rigol_1000DE:
                case eOsziSerie.Rigol_1000Z:
                case eOsziSerie.Picoscope_3206:
                    throw new Exception("Import of *" + s_FileExt + " file:\n"
                                      + "For " + i_ComboOsziModel.Text + " only CSV file import is implemented.\n"
                                      + "Did you select the correct Oszi Model?");

                case eOsziSerie.OWON_1022:
                    if (s_FileExt == ".cap") return ExImport.OWON.ParseBinaryFile(s_Path, ref b_Abort);
                    if (s_FileExt == ".bin") return ExImport.OWON.ParseBinaryFile(s_Path, ref b_Abort);
                    throw new Exception("Import of *" + s_FileExt + " file:\n"
                                      + "For " + i_ComboOsziModel.Text + " only CSV, BIN and CAP file import is implemented.\n"
                                      + "Did you select the correct Oszi Model?");

                case eOsziSerie.Hameg_HMO1522:
                case eOsziSerie.Hameg_HM2008:
                case eOsziSerie.Hameg_HM507:
                    throw new Exception("For " + i_ComboOsziModel.Text + " file import is not implemented.\n"
                                      + "Use 'Transfer' to load the waveforms over RS232, USB or TCP.");

                // TODO: Add Tektronix, Rhode & Schwarz, Siglent, ... which probably use their own proprietary CSV format

                default:
                    Debug.Assert(false, "Programming Error: OsziSerie not implemented");
                    return null;
            }
        }
    }
}
