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

using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using IPanelAction      = OsziWaveformAnalyzer.FormMain.IPanelAction;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using PlatformManager   = Platform.PlatformManager;

namespace OsziWaveformAnalyzer
{
    public partial class CtrlOszi : UserControl, IPanelAction
    {
        // Used when starting for the first time (no registry entries exist yet)
        const int DEFAULT_ANAL_SEPARATE_HEIGHT = 150; // pixel
        const int DEFAULT_ANAL_COMMON_HEIGHT   = 300; // pixel
        const int DEFAULT_DIGITAL_HEIGHT       =  35; // pixel

        bool mb_InitDone;

        public CtrlOszi(ToolTip i_Tooltip, out CheckBox i_CheckSepChannels, out ComboBoxEx i_ComboFactor, 
                        out Label i_LblFactorHint, out Label i_LblDispSamples)
        {
            InitializeComponent();

            // FIRST
            checkLegend     .Checked = Utils.RegReadBool(eRegKey.ShowLegend,      true);
            checkSepChannels.Checked = Utils.RegReadBool(eRegKey.SeparateChannel, true);

            // AFTER
            trackAnalogHeight.Minimum  = PanelOszi.MIN_ANALOG_HEIGHT;
            trackAnalogHeight.Maximum  = PanelOszi.MAX_ANALOG_HEIGHT;
            SetAnalogTrackValue(); // needs checkSepChannels

            trackDigitalHeight.Minimum = PanelOszi.MIN_DIGITAL_HEIGHT;
            trackDigitalHeight.Maximum = PanelOszi.MAX_DIGITAL_HEIGHT;
            trackDigitalHeight.Value   = Utils.RegReadInteger(eRegKey.DigitalHeight, DEFAULT_DIGITAL_HEIGHT);

            i_Tooltip.SetToolTip(trackAnalogHeight,  "Set the display height of the analog channels");
            i_Tooltip.SetToolTip(trackDigitalHeight, "Set the display height of the digital channels");

            UpdateControls();

            i_CheckSepChannels = checkSepChannels;
            i_ComboFactor      = comboFactor;
            i_LblFactorHint    = lblFactorHint;
            i_LblDispSamples   = lblDispSamples;
            mb_InitDone = true;
        }

        // interface IPanelAction
        // The user changed the tab or the old capture has been deleted or a new capture was loaded
        public void OnUserAction(eTab e_ActiveTab, Capture i_Capture)
        {
            if (e_ActiveTab != eTab.Oszi || i_Capture == null)
            {
                Visible = false;
                return;
            }
            Visible = true;
            UpdateControls();
        }

        /// <summary>
        /// Called from contructor and when CheckBox 'Separate Channels' has changed
        /// </summary>
        void SetAnalogTrackValue()
        {
            eRegKey e_KeyAnalog = checkSepChannels.Checked ? eRegKey.AnalogHeightSeparate : eRegKey.AnalogHeightCommon;
            int     s32_Default = checkSepChannels.Checked ? DEFAULT_ANAL_SEPARATE_HEIGHT : DEFAULT_ANAL_COMMON_HEIGHT;

            trackAnalogHeight.Value = Utils.RegReadInteger(e_KeyAnalog, s32_Default);

            if (mb_InitDone)
                trackAnalogHeight_Scroll(null, null);
        }

        private void trackAnalogHeight_Scroll(object sender, EventArgs e)
        {
            eRegKey e_KeyAnalog = checkSepChannels.Checked ? eRegKey.AnalogHeightSeparate : eRegKey.AnalogHeightCommon;
            Utils.RegWriteInteger(e_KeyAnalog, trackAnalogHeight.Value);
            UpdateControls();
            Utils.PanelOszi.RecalculateEverything();
            Utils.PanelOszi.Focus();
        }

        private void trackDigitalHeight_Scroll(object sender, EventArgs e)
        {
            Utils.RegWriteInteger(eRegKey.DigitalHeight, trackDigitalHeight.Value);
            UpdateControls();
            Utils.PanelOszi.RecalculateEverything();
            Utils.PanelOszi.Focus();
        }

        private void UpdateControls()
        {
            checkSepChannels.Visible = Utils.CurCapture != null && Utils.CurCapture.ms32_AnalogCount > 1;

            Utils.PanelOszi.AnalogHeight  = trackAnalogHeight.Value;
            Utils.PanelOszi.DigitalHeight = trackDigitalHeight.Value;
            
            lblAnalogHeight .Text = "Analog: "   + trackAnalogHeight.Value  + " px";
            lblDigitalHeight.Text = "Digital:  " + trackDigitalHeight.Value + " px";
            lblDigitalHeight.ForeColor = Color.White;
        }

        private void checkSepChannels_CheckedChanged(object sender, EventArgs e)
        {
            if (mb_InitDone)
            {
                Utils.RegWriteBool(eRegKey.SeparateChannel, checkSepChannels.Checked);
                SetAnalogTrackValue();
            }

            Utils.PanelOszi.RecalculateEverything(); // does nothing if no Capture loaded
        }

        private void checkLegend_CheckedChanged(object sender, EventArgs e)
        {
            if (mb_InitDone)
                Utils.RegWriteBool(eRegKey.ShowLegend, checkLegend.Checked);

            Utils.PanelOszi.ShowLegend = checkLegend.Checked;
            Utils.PanelOszi.RecalculateEverything(); // does nothing if no Capture loaded
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(Utils.FormMain, "KeyboardMouse");
        }
    }
}
