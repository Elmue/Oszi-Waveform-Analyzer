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
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using PlatformManager   = Platform.PlatformManager;
using IPanelAction      = OsziWaveformAnalyzer.FormMain.IPanelAction;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;

namespace OsziWaveformAnalyzer
{
    public partial class CtrlStartup : UserControl, IPanelAction
    {
        public CtrlStartup()
        {
            InitializeComponent();
        }

        // interface IPanelAction
        // The user changed the tab or the old capture has been deleted or a new capture was loaded
        public void OnUserAction(eTab e_ActiveTab, Capture i_Capture)
        {
            // Decoder RTF has no controls
            Visible = e_ActiveTab == eTab.Decoder || i_Capture == null;
        }

        private void linkUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowURL(Utils.FormMain, "https://netcult.ch/elmue/Oszi-Waveform-Analyzer");
        }
    }
}
