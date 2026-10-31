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
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using IOperation        = Operations.OperationManager.IOperation;
using Utils             = OsziWaveformAnalyzer.Utils;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using PlatformManager   = Platform.PlatformManager;
using Range             = OsziWaveformAnalyzer.Utils.Range;

namespace Operations
{
    public partial class NoiseFilter : Form, IOperation
    {
        Channel mi_Channel;
        float[] mf_Original;
        Range   mk_Selection;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null || !b_Analog)
                return;

            if (b_Analog && Utils.PanelOszi.CommonAnalogDrawing)
                return; // Analog channels cannot be distinguished while drawn one on top of the other

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText  = "Noise Suppression";
            i_Item.ms_ImageFile = "Filter.ico";
            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            mi_Channel  = i_Channel;
            mf_Original = (float[])i_Channel.mf_Analog.Clone();

            InitializeComponent();
            if (ShowDialog(Utils.FormMain) == DialogResult.OK)
            {
                // OK --> apply filter to entire channel
                Utils.StartBusyOperation(null);
                ApplyFilter(Utils.FormMain, false);
                Utils.EndBusyOperation(null);
                Utils.CurCapture.mb_Dirty = true; // user has unsaved changes
            }
            else 
            {
                // Cancel --> restore original values
                mi_Channel.mf_Analog = mf_Original;
            }
            mi_Channel.mi_SampleMinMax.mb_AnalogOK = false; // must be re-calculated

            Utils.PanelOszi.RecalculateEverything();

            Utils.RegWriteInteger(eRegKey.NoiseSuppress, trackSuppr .Value);
            Utils.RegWriteInteger(eRegKey.NoiseCycles,   trackCycles.Value);           
            return null;
        }

        protected override void OnLoad(EventArgs e)
        {
 	        base.OnLoad(e);

            lblChannel.Text = "Channel:  " + mi_Channel;
            lblChannel.ForeColor = PanelOszi.GetChannelColor(mi_Channel);

            String s_Display;
            mk_Selection  = Utils.PanelOszi.GetUserSelectionOrAll(out s_Display);
            lblApply.Text = "Apply to: " + s_Display;

            int s32_Supres = Utils.RegReadInteger(eRegKey.NoiseSuppress, 300); // 30 %
            int s32_Cycles = Utils.RegReadInteger(eRegKey.NoiseCycles, 6);
            trackSuppr .Value = Math.Max(trackSuppr .Minimum, Math.Min(trackSuppr .Maximum, s32_Supres));
            trackCycles.Value = Math.Max(trackCycles.Minimum, Math.Min(trackCycles.Maximum, s32_Cycles));

            trackBar_Scroll(null, null);
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "NoiseSuppression");
        }

        private void trackBar_Scroll(object sender, EventArgs e)
        {
            lblSuppr .Text = "Noise Suppression: " + (trackSuppr.Value / 10) + " %";
            lblCycles.Text = "Apply the filter " + trackCycles.Value + " times";

            // Apply filter only to the part that is visible on the screen
            ApplyFilter(this, true); 

            mi_Channel.mi_SampleMinMax.mb_AnalogOK = false; // must be re-calculated

            // recalculate only part of the signal that is visible on the screen (speed optimization)
            Utils.PanelOszi.CalcSampleMinMax(mi_Channel, true);
            Utils.PanelOszi.Invalidate();
        }

        /// <summary>
        /// Apply an EWMA Filter: Exponentially Weighted Moving Average Filter, which is basically a low-pass filter.
        /// </summary>
        void ApplyFilter(Form i_Form, bool b_VisibleOnly)
        {
            i_Form.Cursor = Cursors.WaitCursor;

            Range k_Sel = mk_Selection;
            if (b_VisibleOnly) 
            {
                // If the user has slected a range --> apply the filter only on this range.
                // If the selected range lies outside the visible screen --> show the preview on the entire visible screen.
                k_Sel = k_Sel.Intersection(Utils.PanelOszi.DisplayRange);
                if (!k_Sel.IsValid)
                     k_Sel = Utils.PanelOszi.DisplayRange;
            }

            // Suppression Trackbar has values from 0 to 950
            float  f_Alpha = (1000.0f - trackSuppr.Value) / 1000.0f;
            float[] f_Data = (float[])mf_Original.Clone();

            // Cycles Trackbar has values from 1 to 15
            for (int C=0; C<trackCycles.Value; C++)
            {
                float f_Output = f_Data[k_Sel.ms32_Start];
                for (int S=k_Sel.ms32_Start+1; S<k_Sel.ms32_End; S++)
                {
                    f_Output += f_Alpha * (f_Data[S] - f_Output);
                    f_Data[S] = f_Output;
                }
            }
            mi_Channel.mf_Analog = f_Data;

            i_Form.Cursor = Cursors.Arrow;
        }
    }
}

