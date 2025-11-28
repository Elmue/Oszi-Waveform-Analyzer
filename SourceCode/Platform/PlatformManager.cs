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
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;

using RtfViewer         = OsziWaveformAnalyzer.RtfViewer;
using ScpiCombo         = Transfer.SCPI.ScpiCombo;

namespace Platform
{
    public class PlatformManager
    {
        #region enums

        public enum eRuntime
        {
            Microsoft,
            Mono,
        }

        #endregion

        #region interfaces IPlatform + IDevice

        public interface IPlatform
        {
            // Adapt RichTextBox to use the new MsftEdit.dll
            void RtfBoxCreateParams(CreateParams i_Params);

            // Adapt .NET framework to support clickable RTF links in RichTextBox
            void RtfBoxWndProc(RtfViewer iRtfViewer, ref Message k_Msg);

            // Open the default browser and jump to the chapter in the file Manual.htm
            void ShowHelp(Form i_Owner, String s_Chapter = "");

            // Open the default browser and jump to the URL
            void ShowURL(Form i_Owner, String s_URL);

            // Create a shortcut to Oszi Waveform Analyzer in the start menu
            void CreateShortcutInStartMenu();

            // Associate file extension *.oszi with this program
            // This will be handled in FormMain.OnCmdLineTimer()
            void AssociateOsziExtension();

            // This function is called from the button "Install Driver" that should exist in all the Capture Forms.
            void InstallDriver(Form i_Owner);

            // Load the combobox with all SCPI devices connected over USB
            // The ComboBox Items must be ScpiCombo classes.
            void EnumerateUsbDevices(ComboBox i_ComboUsbDevice);

            // Create a new instance of the class that is derived from IDevice and open the device
            IDevice OpenUsbDevice(ScpiCombo i_Combo);
        }

        public interface IDevice : IDisposable
        {
            // Send a TMC packet to the oscilloscope
            void Send(Byte[] u8_Packet, int s32_Timeout);

            // Receive a TMC packet from the oscilloscope and return the bytes that have been written into the buffer
            int Receive(Byte[] u8_RxBuffer, int s32_Timeout);

            // Abort pending Tx and Rx
            void CancelTransfer();
        }

        #endregion

        static IPlatform mi_Instance;
        static eRuntime  me_Runtime;

        public static IPlatform Instance
        {
            get { return mi_Instance; }
        }

        public static eRuntime Runtime
        {
            get { return me_Runtime; }
        }

        /// <summary>
        /// static constructor
        /// </summary>
        static PlatformManager()
        {
            PlatformID e_ID = Environment.OSVersion.Platform;

            if (e_ID == PlatformID.Unix || Type.GetType("Mono.Runtime") != null)
            {
                me_Runtime  = eRuntime.Mono;
                mi_Instance = new PlfMono();
            }
            else
            {
                me_Runtime  = eRuntime.Microsoft;
                mi_Instance = new PlfMicrosoft();
            }
        }
    }
}
