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
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using Utils             = OsziWaveformAnalyzer.Utils;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using IOperation        = Operations.OperationManager.IOperation;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using PlatformManager   = Platform.PlatformManager;
using Range             = OsziWaveformAnalyzer.Utils.Range;

namespace Operations
{
    public partial class SetCursor : Form, IOperation
    {
        int ms32_Sample;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (Utils.PanelOszi.CursorSample >= 0 || Utils.PanelOszi.UserSelection.IsValid)
            {
                GraphMenuItem i_Remove = new GraphMenuItem();
                i_Remove.ms_MenuText  = "Remove cursor and selection";
                i_Remove.ms_ImageFile = "CursorRemove.ico";
                i_Remove.mo_Tag       = "Remove";

                if (Utils.PanelOszi.RasterON)
                    i_Remove.ms_MenuText  = "Remove the cursor and the raster";

                i_Items.Add(i_Remove);
            }

            GraphMenuItem i_Cursor = new GraphMenuItem();
            i_Cursor.ms_MenuText  = "Set the cursor to the mouse position (move with arrow keys)";
            i_Cursor.ms_ImageFile = "Cursor.ico";
            i_Cursor.mo_Tag       = "Cursor";
            i_Items.Add(i_Cursor);

            GraphMenuItem i_Raster = new GraphMenuItem();
            i_Raster.ms_MenuText  = "Set cursor and show raster lines (move with arrow keys)";
            i_Raster.ms_ImageFile = "CursorRaster.ico";
            i_Raster.mo_Tag       = "Raster";
            i_Items.Add(i_Raster);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            ms32_Sample = s32_Sample;

            switch ((String)o_Tag)
            {
                case "Cursor":
                    Utils.PanelOszi.SetCursor(s32_Sample, -1m);
                    Utils.PanelOszi.Invalidate();
                    break;
                case "Remove":
                    Utils.PanelOszi.SetCursor(-1, -1m);
                    Utils.PanelOszi.UserSelection = new Range(0, 0);
                    Utils.PanelOszi.Invalidate();
                    break;
                case "Raster":
                    InitializeComponent();
                    ShowDialog(Utils.FormMain);
                    break;
            }
            return null;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            comboUnit   .Text = Utils.RegReadString(eRegKey.RasterUnit,     "milli seconds");
            textInterval.Text = Utils.RegReadString(eRegKey.RasterInterval, "12.5");
            textInterval.KeyDown += new KeyEventHandler(OnTextIntervalKeyDown);

            if (comboUnit.SelectedIndex < 0)
                comboUnit.SelectedIndex = 3;
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "MeasureTime");
        }

        void OnTextIntervalKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                btnSetCursor_Click(null, null);
        }

        private void btnSetCursor_Click(object sender, EventArgs e)
        {
            textInterval.Text = textInterval.Text.Replace(',', '.');

            decimal d_Interval;
            if (!decimal.TryParse(textInterval.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out d_Interval))
            {
                MessageBox.Show(this, "Enter a valid interval.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textInterval.Focus();
                return;
            }

            switch (comboUnit.SelectedIndex)
            {
                case 0: d_Interval *= 1m;             break; // Pico
                case 1: d_Interval *= 1000m;          break; // Nano
                case 2: d_Interval *= 1000000m;       break; // Micro
                case 3: d_Interval *= 1000000000m;    break; // Milli
                case 4: d_Interval *= 1000000000000m; break; // Seconds
                default: Debug.Assert(false, "Programming Error: Invalid unit"); break;
            }

            decimal d_MinDistance = Utils.CurCapture.ms64_SampleDist * 10;
            if (d_Interval < d_MinDistance)
            {
                String s_Mesg = String.Format("The resolution of the current capture is insufficient.\n"
                                            + "The minimum raster distance for this signal is {0} (at Display Factor x 1).", Utils.FormatTimePico(d_MinDistance));

                MessageBox.Show(this, s_Mesg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Utils.RegWriteString(eRegKey.RasterUnit,     comboUnit.Text);
            Utils.RegWriteString(eRegKey.RasterInterval, textInterval.Text);

            Utils.PanelOszi.SetCursor(ms32_Sample, d_Interval);
            Utils.PanelOszi.Invalidate();

            DialogResult = DialogResult.OK;
        }
    }
}

