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

using IPlatform         = Platform.PlatformManager.IPlatform;
using IDevice           = Platform.PlatformManager.IDevice;
using RtfViewer         = OsziWaveformAnalyzer.RtfViewer;
using ScpiCombo         = Transfer.SCPI.ScpiCombo;
using Utils             = OsziWaveformAnalyzer.Utils;

namespace Platform
{
    public class PlfMono : IPlatform
    {
        /// <summary>
        /// Adapt RichTextBox to use the new MsftEdit.dll (Microsoft only)
        /// </summary>
        public void RtfBoxCreateParams(CreateParams i_Params)
        {
            // not required here
        }

        /// <summary>
        /// Adapt .NET framework to support clickable RTF links in RichTextBox
        /// Does Mono support clickable RTF links at all ???
        /// </summary>
        public void RtfBoxWndProc(RtfViewer iRtfViewer, ref Message k_Msg)
        {
        }

        /// <summary>
        /// Open the default browser and jump to the given chapter in the file Manual.htm
        /// </summary>
        public void ShowHelp(Form i_Owner, String s_Chapter = "")
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Open the default browser and jump to the URL
        /// </summary>
        public void ShowURL(Form i_Owner, String s_URL)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a shortcut to Oszi Waveform Analyzer in the start menu once.
        /// </summary>
        public void CreateShortcutInStartMenu()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Associate file extension *.oszi with this program so the user can double click a *.oszi file and will open in this program.
        /// This will be handled in FormMain.OnCmdLineTimer()
        /// </summary>
        public void AssociateOsziExtension()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Prevent that the operating system goes into sleep mode
        /// </summary>
        public void PreventSleep()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This function is called from the button "Install Driver" that should exist in all the Capture Forms.
        /// </summary>
        public void InstallDriver(Form i_Owner)
        {
            // https://www.ivifoundation.org/Shared-Components/default.html#usbtmc-kernel-driver-packages-for-linux
            MessageBox.Show(Utils.FormMain, "The driver from the IVI Foundation is included in the Linux Kernel "
                            + "since version 4.20. No extra installation is required.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Load the combobox with all SCPI devices currently connected over USB.
        /// The ComboBox Items must be ScpiCombo classes.
        /// </summary>
        public void EnumerateUsbDevices(ComboBox i_ComboUsbDevice)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a new instance of the class that is derived from IDevice.
        /// The constructor of this class opens the device with fopen("/dev/usbtmc0", "r+")
        /// i_Combo comes from EnumerateScpiDevices()
        /// </summary>
        public IDevice OpenUsbDevice(ScpiCombo i_Combo)
        {
            throw new NotImplementedException();
        }
    }
}
