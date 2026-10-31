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
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using IOperation        = Operations.OperationManager.IOperation;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using Utils             = OsziWaveformAnalyzer.Utils;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using PlatformManager   = Platform.PlatformManager;
using Range             = OsziWaveformAnalyzer.Utils.Range;

namespace Operations
{
    public partial class ConvertAD : Form, IOperation
    {
        const String METHOD_THRESHOLD = "Digitize at fix threshold with hysteresis";
        const String METHOD_ADAPTIVE  = "Digitize at dynamic adaptive threshold";
        const String METHOD_MIN_MAX   = "Digitize at min / max points of analog waveform";

        Channel mi_Analog;
        float   mf_AdaptiveMinPct;
        Range   mk_Selection;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null || !b_Analog)
                return;

            // When checkbox "Separate Analog Channels" is off all analog channels are drawn on top of each other.
            // When right-clicking it is impossible to know which channel the user wants to convert.
            if (b_Analog && Utils.PanelOszi.CommonAnalogDrawing)
                return; 

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText  = "A/D Converter";
            i_Item.ms_ImageFile = "ConvertAD.ico";
            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            mi_Analog = i_Channel;

            InitializeComponent();
            ShowDialog(Utils.FormMain);
            return null;
        }

        protected override void OnLoad(EventArgs e)
        {
 	        base.OnLoad(e);

            comboMethod.Items.Add(METHOD_THRESHOLD);
            comboMethod.Items.Add(METHOD_ADAPTIVE);
            comboMethod.Items.Add(METHOD_MIN_MAX);

            radioOneChannel.Text = "Convert analog channel  '" + mi_Analog.ms_Name + "'";
            radioOneChannel.ForeColor = PanelOszi.GetChannelColor(mi_Analog);
            textNewName.Text = Utils.RegReadString(eRegKey.AD_Result, "Math Result");

            // ALWAYS start with Threshold A/D
            // The other methods are very special and must be selected each time explicitely by the user.
            comboMethod.SelectedIndex = 0;

            String s_Display;
            mk_Selection  = Utils.PanelOszi.GetUserSelectionOrAll(out s_Display);
            lblApply.Text = "Apply to: " + s_Display;

            try
            {
                trackBarThreshLow.Value = Utils.RegReadInteger(eRegKey.AD_ThresholdLo, 48);
                trackBarThresHigh.Value = Utils.RegReadInteger(eRegKey.AD_ThresholdHi, 52);
            }
            catch {} // avoid that invalid values in the registry cause a crash

            SetThresholds();
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "AD_Conversion");
        }

        private void comboMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboMethod.Text)
            {
                case METHOD_THRESHOLD: 
                    lblHint.Text = "Digitizes at two fix threshold voltages with hysteresis.";
                    break;
                case METHOD_ADAPTIVE: 
                    lblHint.Text = "Adapts the threshold dynamically to 50% between the high and low peak voltages of each half period.";
                    break;
                case METHOD_MIN_MAX:
                    lblHint.Text = "Digitizes at the highest and the lowest analog voltage. Do not use for square signals!";
                    break;
            }
            SetThresholds();
        }

        private void radioAllChannels_CheckedChanged(object sender, EventArgs e)
        {
            SetThresholds();

            if (radioAllChannels.Checked)
                radioSameChannel.Checked = true;

            radioOtherChannel.Enabled = !radioAllChannels.Checked;
        }

        private void radioOtherChannel_CheckedChanged(object sender, EventArgs e)
        {
            textNewName.Enabled = radioOtherChannel.Checked;
        }

        /// <summary>
        /// Trackbar gives values from 0 to 100
        /// </summary>
        void trackHigh_Scroll(object sender, EventArgs e)
        {
            trackBarThreshLow.Value = Math.Min(trackBarThreshLow.Value, trackBarThresHigh.Value);
            SetThresholds();
        }

        void trackLow_Scroll(object sender, EventArgs e)
        {
            trackBarThresHigh.Value = Math.Max(trackBarThreshLow.Value, trackBarThresHigh.Value);
            SetThresholds();
        }

        void SetThresholds()
        {
            bool b_Multi = radioAllChannels.Checked && Utils.CurCapture.ms32_AnalogCount > 1;

            Utils.CurCapture.ClearThresholds(); // remove threshold lines from all channels

            if (comboMethod.Text == METHOD_ADAPTIVE)
            {
                // Convert trackbar range 0 % ... 100 %  -->  5 % ... 95 %
                int s32_Percent   = 5 + trackBarThresHigh.Value * 90 / 100;
                mf_AdaptiveMinPct = (float)s32_Percent / 100;

                String s_Amplitude = String.Format("Minimum Amplitude for noise suppression: {0} %", s32_Percent);
                if (!b_Multi)
                {
                    float f_AdaptiveMinVolt = (mi_Analog.mf_Max - mi_Analog.mf_Min) * mf_AdaptiveMinPct;
                    s_Amplitude += String.Format("  = {0:0.000} Volt", f_AdaptiveMinVolt);
                }
                lblThresholdLow .Text = "";
                lblThresholdHigh.Text = s_Amplitude;
                trackBarThreshLow.Visible = false;
            }
            else
            {
                String s_ThreshLow  = String.Format("Threshold Low: {0} %",  trackBarThreshLow.Value);
                String s_ThreshHigh = String.Format("Threshold High: {0} %", trackBarThresHigh.Value);
                if (!b_Multi)
                {
                    s_ThreshLow  += String.Format("  = {0:0.000} Volt",  mi_Analog.mf_ThreshLo);
                    s_ThreshHigh += String.Format("  = {0:0.000} Volt",  mi_Analog.mf_ThreshHi);
                }
                lblThresholdLow .Text = s_ThreshLow;
                lblThresholdHigh.Text = s_ThreshHigh;
                trackBarThreshLow.Visible = true;

                foreach (Channel i_Channel in Utils.CurCapture.mi_Channels)
                {
                    if (b_Multi || i_Channel == mi_Analog)
                    {
                        float f_Range = i_Channel.mf_Max - i_Channel.mf_Min;
                        i_Channel.mf_ThreshLo  = i_Channel.mf_Min + trackBarThreshLow.Value * f_Range / 100; // Volt
                        i_Channel.mf_ThreshHi  = i_Channel.mf_Min + trackBarThresHigh.Value * f_Range / 100; // Volt
                        i_Channel.mb_Threshold = true; // show threshold lines
                    }
                }
            }
            Utils.PanelOszi.Invalidate();
        }

        private void btnConvertClose_Click(object sender, EventArgs e)
        {
            Convert(true);
        }
        private void btnConvert_Click(object sender, EventArgs e)
        {
            Convert(false);
        }

        private void Convert(bool b_Close)
        {
            Utils.RegWriteString (eRegKey.AD_Result,      textNewName.Text);
            Utils.RegWriteInteger(eRegKey.AD_ThresholdLo, trackBarThreshLow.Value);
            Utils.RegWriteInteger(eRegKey.AD_ThresholdHi, trackBarThresHigh.Value);

            Channel i_Anal = mi_Analog;
            Channel i_Digi = mi_Analog;
            if (radioOtherChannel.Checked)
            {
                if (textNewName.Text.Length < 2)
                {
                    MessageBox.Show(this, "Enter the channel name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    textNewName.Focus();
                    return;
                }
                i_Digi = Utils.CurCapture.FindOrCreateChannel(textNewName.Text, mi_Analog);
            }

            bool b_Multi = radioAllChannels.Checked && Utils.CurCapture.ms32_AnalogCount > 1;

            foreach (Channel i_Channel in Utils.CurCapture.mi_Channels)
            {
                if (b_Multi)
                {
                    i_Anal = i_Channel;
                    i_Digi = i_Channel;
                }

                switch (comboMethod.Text)
                {
                    case METHOD_THRESHOLD: Digitizer.ThresholdAD(i_Anal, i_Digi, mk_Selection); break;
                    case METHOD_ADAPTIVE:  Digitizer.AdaptiveAD (i_Anal, i_Digi, mk_Selection,  mf_AdaptiveMinPct); break;
                    case METHOD_MIN_MAX:   Digitizer.MinMaxAD   (i_Anal, i_Digi, mk_Selection); break;
                    default:
                        Debug.Assert(false, "Programming Error: Invalid ComboBox items");
                        return;
                }

                i_Digi.mi_SampleMinMax.mb_DigitalOK = false;

                if (!b_Multi)
                    break;
            }

            Utils.CurCapture.mb_Dirty = true; // user has unsaved changes
            Utils.PanelOszi.RecalculateEverything();

            if (b_Close)
                DialogResult = DialogResult.OK;
        }
    }
}

