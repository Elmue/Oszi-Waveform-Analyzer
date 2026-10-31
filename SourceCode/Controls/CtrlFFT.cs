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
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using PanelFFT          = OsziWaveformAnalyzer.PanelFFT;
using eScale            = OsziWaveformAnalyzer.PanelFFT.eScale;
using eWindow           = OsziWaveformAnalyzer.PanelFFT.eWindow;
using IPanelAction      = OsziWaveformAnalyzer.FormMain.IPanelAction;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using PlatformManager   = Platform.PlatformManager;

namespace OsziWaveformAnalyzer
{
    public partial class CtrlFFT : UserControl, IPanelAction
    {
        bool mb_InitDone;
        bool mb_MessagboxOnce = true;

        public CtrlFFT(ToolTip i_Tooltip)
        {
            InitializeComponent();

            i_Tooltip.SetToolTip(checkLog,    "Toggle the X axis between linar / logarithmic frequency.");
            i_Tooltip.SetToolTip(checkDC,     "Removes the average voltage, otherwise the DC component may hide low frequencies.");
            i_Tooltip.SetToolTip(btnPeaks,    "Show the "+PanelFFT.RTF_PEAKS+" strongest peaks as a table with details in the tab 'Decoder'.");
            i_Tooltip.SetToolTip(comboScale,  "Toggle the Y axis between linar / logarithmic amplitude");
            i_Tooltip.SetToolTip(comboWindow, "Select the window function (a math curve applied to the signal) for the FFT.\nSee help for details.");

            foreach (eWindow e_Window in Enum.GetValues(typeof(eWindow)))
            {
                comboWindow.Items.Add(Utils.GetDescriptionAttribute(e_Window));
            }
            foreach (eScale e_Scale in Enum.GetValues(typeof(eScale)))
            {
                comboScale.Items.Add(Utils.GetDescriptionAttribute(e_Scale));
            }

            comboWindow.SelectedIndex = Utils.RegReadInteger(eRegKey.FFTWindow,      0);
            comboScale .SelectedIndex = Utils.RegReadInteger(eRegKey.FFTScale,       0);
            checkLog  .Checked        = Utils.RegReadBool   (eRegKey.FFTLogarithmic, true);
            checkDC   .Checked        = Utils.RegReadBool   (eRegKey.FFTRemoveDC,    true);
            checkNoise.Checked        = Utils.RegReadBool   (eRegKey.FFTRemoveNoise, true);

            UpdateSettings(); // Send all settings to PanelFFT
            mb_InitDone = true;
        }

        // interface IPanelAction
        // The user changed the tab or the old capture has been deleted or a new capture was loaded
        public void OnUserAction(eTab e_ActiveTab, Capture i_Capture)
        {
            Visible = e_ActiveTab == eTab.FFT && i_Capture != null;
        }

        private void comboWindow_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!mb_InitDone)
                return;

            if (mb_MessagboxOnce && comboWindow.SelectedIndex != (int)eWindow.BlackmanHarris)
            {
                mb_MessagboxOnce = false;
                MessageBox.Show(this, "If you do not use the Blackman Harris window you will notice that the peaks do not match precisely the curve.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Utils.RegWriteInteger(eRegKey.FFTWindow, comboWindow.SelectedIndex);
            UpdateSettings();
        }

        private void comboScale_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!mb_InitDone)
                return;

            Utils.RegWriteInteger(eRegKey.FFTScale, comboScale.SelectedIndex);
            UpdateSettings();
        }

        private void checkLog_CheckedChanged(object sender, EventArgs e)
        {
            if (!mb_InitDone)
                return;

            Utils.RegWriteBool(eRegKey.FFTLogarithmic, checkLog.Checked);
            UpdateSettings();
        }

        private void checkDC_CheckedChanged(object sender, EventArgs e)
        {
            if (!mb_InitDone)
                return;

            Utils.RegWriteBool(eRegKey.FFTRemoveDC, checkDC.Checked);
            UpdateSettings();
        }

        private void checkNoise_CheckedChanged(object sender, EventArgs e)
        {
            if (!mb_InitDone)
                return;

            Utils.RegWriteBool(eRegKey.FFTRemoveNoise, checkNoise.Checked);
            UpdateSettings();
        }

        void UpdateSettings()
        {
            Utils.PanelFFT.OnUserSettgChanged((eWindow)comboWindow.SelectedIndex,
                                              (eScale) comboScale .SelectedIndex,
                                              checkLog.Checked, checkDC.Checked, checkNoise.Checked);
        }

        private void btnPeaks_Click(object sender, EventArgs e)
        {
            Utils.PanelFFT.OnButtonShowPeaks();
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(Utils.FormMain, "FFT");
        }
    }
}
