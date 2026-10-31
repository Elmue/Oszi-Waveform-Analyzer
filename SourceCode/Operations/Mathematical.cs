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
using System.Globalization;
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using IOperation        = Operations.OperationManager.IOperation;
using Utils             = OsziWaveformAnalyzer.Utils;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using Range             = OsziWaveformAnalyzer.Utils.Range;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using PlatformManager   = Platform.PlatformManager;

namespace Operations
{
    public partial class Mathematical : Form, IOperation
    {
        // Single Channel Analog
        const String MATH_FACTOR      = "Multiply with factor";
        const String MATH_OFFSET      = "Add Offset";
        const String MATH_SET_VOLT    = "Set constant voltage";

        // Single Channel Digital
        const String MATH_SET_STATUS  = "Set digital status";

        // Single Channel Analog + Digital
        const String MATH_INVERT      = "Invert Channel";

        // Dual Channel Analog
        const String MATH_ADD_AB      = "Add Channel A + B";
        const String MATH_SUBTRACT_AB = "Subtract Channel A - B";
        const String MATH_SUBTRACT_BA = "Subtract Channel B - A";
        const String MATH_MULTI_AB    = "Multiply Channel A x B";
        
        // Dual Channel Digital
        const String MATH_LOGIC_AND   = "Logical AND";
        const String MATH_LOGIC_OR    = "Logical OR";
        const String MATH_LOGIC_XOR   = "Logical XOR";

        Channel mi_ChannelA;
        Channel mi_ChannelB;
        Channel mi_Result;
        bool    mb_Analog;
        bool    mb_Single;
        Size    mk_ClientSize;
        Panel[] mi_Panels;
        Range   mk_Selection;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null)
                return; // user did not click on a Channel

            if (b_Analog && Utils.PanelOszi.CommonAnalogDrawing)
                return; // Analog channels cannot be distinguished while drawn one on top of the other

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText  = "Math Operations";
            i_Item.ms_ImageFile = "Calculator.ico";
            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_ChannelA, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            mb_Analog   = b_Analog;
            mi_ChannelA = i_ChannelA;

            InitializeComponent();
            ShowDialog(Utils.FormMain);
            return null;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            mk_ClientSize = ClientSize;
            mi_Panels     = new Panel[] { panelChannelB, panelResult, panelApply, panelParam};

            lblMathMode.Text = mb_Analog ? "Analog" : "Digital";

            String s_Display;
            mk_Selection  = Utils.PanelOszi.GetUserSelectionOrAll(out s_Display);
            lblApply.Text = s_Display;

            foreach (Channel i_Channel in Utils.CurCapture.mi_Channels)
            {
                if (mb_Analog && i_Channel.mf_Analog == null)
                    continue;
                
                if (!mb_Analog && i_Channel.mu8_Digital == null)
                    continue;

                comboChannelA.Items.Add(i_Channel);
                comboChannelB.Items.Add(i_Channel);
            }

            // ---------------------------------------------------

            comboChannelA.SelectedItem = mi_ChannelA;

            // select another channel B than channel A if more than 1 channel
            if (comboChannelB.Items.Count > 1)
                comboChannelB.SelectedIndex = (comboChannelA.SelectedIndex + 1) % comboChannelB.Items.Count;
            else
                panelChannelB.Visible = false;

            // ------------------- !LAST! ------------------------

            if (mb_Analog)
            {
                if (Utils.CurCapture.ms32_AnalogCount > 1)
                {
                    comboMath.Items.Add(MATH_ADD_AB);
                    comboMath.Items.Add(MATH_SUBTRACT_AB);
                    comboMath.Items.Add(MATH_SUBTRACT_BA);
                    comboMath.Items.Add(MATH_MULTI_AB);
                    comboMath.Items.Add(Utils.TOOLTIP_SEPARATOR);
                }
                comboMath.Items.Add(MATH_INVERT);
                comboMath.Items.Add(MATH_FACTOR);
                comboMath.Items.Add(MATH_OFFSET);
                comboMath.Items.Add(MATH_SET_VOLT);
            }
            else // Digital
            {
                if (Utils.CurCapture.ms32_DigitalCount > 1)
                {
                    comboMath.Items.Add(MATH_LOGIC_AND);
                    comboMath.Items.Add(MATH_LOGIC_OR);
                    comboMath.Items.Add(MATH_LOGIC_XOR); 
                    comboMath.Items.Add(Utils.TOOLTIP_SEPARATOR);
                }
                comboMath.Items.Add(MATH_INVERT);
                comboMath.Items.Add(MATH_SET_STATUS);
            }

            // Calls comboMath_SelectedIndexChanged()
            comboMath.Text = Utils.RegReadString(eRegKey.MathOperation);
            if (comboMath.SelectedIndex < 0)
                comboMath.SelectedIndex = 0;
        }

        private void comboMath_SelectedIndexChanged(object sender, EventArgs e)
        {
            mb_Single = false;
            lblUnit .Text = "";
            lblParam.Text = "";

            if (comboMath.Text == Utils.TOOLTIP_SEPARATOR)
            {
                comboMath.SelectedIndex ++;
                return;
            }

            switch (comboMath.Text)
            {
                case MATH_INVERT:
                    mb_Single = true;
                    break;
                case MATH_FACTOR:
                    mb_Single = true;
                    lblParam.Text = "Factor:";
                    break;
                case MATH_OFFSET:
                    mb_Single = true;
                    lblParam.Text = "Offset:";
                    lblUnit .Text = "Volt";
                    break;
                case MATH_SET_VOLT:
                    mb_Single = true;
                    lblParam.Text = "Fix Voltage:";
                    lblUnit .Text = "Volt";
                    break;
                case MATH_SET_STATUS:
                    mb_Single = true;
                    lblParam.Text = "Digital Status:";
                    lblUnit .Text = "(0 or 1)";
                    break;
            }

            panelParam .Visible = lblParam.Text.Length > 0;
            panelResult.Visible = comboChannelB.Visible && !mb_Single;
            panelApply .Visible = comboChannelB.Visible &&  mb_Single;

            // -----------------------------------

            int s32_Y = mi_Panels[0].Top;
            foreach (Panel i_Panel in mi_Panels)
            {
                i_Panel.Top = s32_Y;

                if (i_Panel.Visible) 
                    s32_Y += i_Panel.Height;
            }

            mk_ClientSize.Height = s32_Y + 45;
            ClientSize = mk_ClientSize;
        }

        private void comboChannelA_SelectedIndexChanged(object sender, EventArgs e)
        {
            mi_ChannelA = (Channel)comboChannelA.SelectedItem;
            lblChannelA.ForeColor = PanelOszi.GetChannelColor(mi_ChannelA);
            radioApplyA.ForeColor = lblChannelA.ForeColor;
        }

        private void comboChannelB_SelectedIndexChanged(object sender, EventArgs e)
        {
            mi_ChannelB = (Channel)comboChannelB.SelectedItem;
            lblChannelB.ForeColor = PanelOszi.GetChannelColor(mi_ChannelB);
            radioApplyB.ForeColor = lblChannelB.ForeColor;
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "SplitHalfDuplex");
        }

        private void btnExecuteClose_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            Calculate(true);
            Cursor = Cursors.Arrow;
        }
        private void btnExecute_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            Calculate(false);
            Cursor = Cursors.Arrow;
        }
        void Calculate(bool b_Close)
        {
            Utils.RegWriteString(eRegKey.MathOperation, comboMath.Text);

            try
            {
                Channel[] i_ApplyTo = Prepare(); // throws

                // Get the sample range that the user has selected (if any)
                Range k_Sel = mk_Selection;

                if (mb_Analog)
                {
                    switch (comboMath.Text)
                    {
                        // ------------------------------- Dual -------------------------------------

                        case MATH_ADD_AB:
                            for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                mi_Result.mf_Analog[S] = mi_ChannelA.mf_Analog[S] + mi_ChannelB.mf_Analog[S];

                            mi_Result.mi_SampleMinMax.mb_AnalogOK = false;
                            break;

                        case MATH_SUBTRACT_AB:
                            for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                mi_Result.mf_Analog[S] = mi_ChannelA.mf_Analog[S] - mi_ChannelB.mf_Analog[S];

                            mi_Result.mi_SampleMinMax.mb_AnalogOK = false;
                            break;

                        case MATH_SUBTRACT_BA:
                            for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                mi_Result.mf_Analog[S] = mi_ChannelB.mf_Analog[S] - mi_ChannelA.mf_Analog[S];

                            mi_Result.mi_SampleMinMax.mb_AnalogOK = false;
                            break;

                        case MATH_MULTI_AB:
                            for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                mi_Result.mf_Analog[S] = mi_ChannelA.mf_Analog[S] * mi_ChannelB.mf_Analog[S];

                            mi_Result.mi_SampleMinMax.mb_AnalogOK = false;
                            break;

                        // ------------------------------- Single -------------------------------------

                        case MATH_INVERT:
                            foreach (Channel i_Chan in i_ApplyTo)
                            {
                                float f_Sum = i_Chan.mf_Min + i_Chan.mf_Max;
                                for (int i=k_Sel.ms32_Start; i<k_Sel.ms32_End; i++)
                                    i_Chan.mf_Analog[i] = f_Sum - i_Chan.mf_Analog[i];

                                i_Chan.mi_SampleMinMax.mb_AnalogOK = false; 
                            }
                            break;

                        case MATH_FACTOR:
                            float f_Factor;
                            if (!float.TryParse(textParameter.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out f_Factor))
                            {
                                textParameter.Focus();
                                throw new Exception("Enter a valid factor.");
                            }

                            foreach (Channel i_Chan in i_ApplyTo)
                            {
                                for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                    i_Chan.mf_Analog[S] *= f_Factor;

                                i_Chan.mi_SampleMinMax.mb_AnalogOK = false;
                            }
                            break;

                        case MATH_OFFSET:
                            float f_Offset;
                            if (!float.TryParse(textParameter.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out f_Offset))
                            {
                                textParameter.Focus();
                                throw new Exception("Enter a valid offset.");
                            }

                            foreach (Channel i_Chan in i_ApplyTo)
                            {
                                for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                    i_Chan.mf_Analog[S] += f_Offset;

                                i_Chan.mi_SampleMinMax.mb_AnalogOK = false;
                            }
                            break;

                        case MATH_SET_VOLT:
                            float f_Volt;
                            if (!float.TryParse(textParameter.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out f_Volt))
                            {
                                textParameter.Focus();
                                throw new Exception("Enter a valid voltage.");
                            }

                            foreach (Channel i_Chan in i_ApplyTo)
                            {
                                for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                    i_Chan.mf_Analog[S] = f_Volt;

                                i_Chan.mi_SampleMinMax.mb_AnalogOK = false;
                            }
                            break;
                    }
                }
                else // Digital (Only bit 0 is used)
                {
                    switch (comboMath.Text)
                    {
                        // ------------------------------- Dual -------------------------------------

                        case MATH_LOGIC_AND:
                            for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                mi_Result.mu8_Digital[S] = (Byte)((mi_ChannelA.mu8_Digital[S] & mi_ChannelB.mu8_Digital[S]) & 0x01);

                            mi_Result.mi_SampleMinMax.mb_DigitalOK = false;
                            break;

                        case MATH_LOGIC_OR:
                            for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                mi_Result.mu8_Digital[S] = (Byte)((mi_ChannelA.mu8_Digital[S] | mi_ChannelB.mu8_Digital[S]) & 0x01);

                            mi_Result.mi_SampleMinMax.mb_DigitalOK = false;
                            break;

                        case MATH_LOGIC_XOR: 
                            for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                mi_Result.mu8_Digital[S] = (Byte)((mi_ChannelA.mu8_Digital[S] ^ mi_ChannelB.mu8_Digital[S]) & 0x01);

                            mi_Result.mi_SampleMinMax.mb_DigitalOK = false;
                            break;

                        // ------------------------------- Single -------------------------------------

                        case MATH_INVERT:
                            foreach (Channel i_Chan in i_ApplyTo)
                            {
                                for (int i=k_Sel.ms32_Start; i<k_Sel.ms32_End; i++)
                                    i_Chan.mu8_Digital[i] ^= 0x01;

                                i_Chan.mi_SampleMinMax.mb_DigitalOK = false;
                                i_Chan.mi_MarkRows = null;
                            }
                            break;

                        case MATH_SET_STATUS:
                            Byte u8_Status;
                            if (!Byte.TryParse(textParameter.Text, out u8_Status) || u8_Status > 1)
                            {
                                textParameter.Focus();
                                throw new Exception("Enter either 0 or 1.");
                            }

                            foreach (Channel i_Chan in i_ApplyTo)
                            {
                                for (int S=k_Sel.ms32_Start; S<k_Sel.ms32_End; S++)
                                    i_Chan.mu8_Digital[S] = u8_Status;

                                i_Chan.mi_SampleMinMax.mb_DigitalOK = false;
                                i_Chan.mi_MarkRows = null;
                            }
                            break;
                    }
                }

                Utils.CurCapture.mb_Dirty = true; // user has unsaved changes
                Utils.PanelOszi.RecalculateEverything();

                if (b_Close)
                    DialogResult = DialogResult.OK;
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
            }
        }

        Channel[] Prepare()
        {
            if (mb_Single) // Add Offset, Multiply Factor
            {
                mi_Result = null;

                List<Channel> i_ApplyTo = new List<Channel>();
                if (radioApplyA.Checked)
                {
                    i_ApplyTo.Add(mi_ChannelA);
                }
                else if (radioApplyB.Checked)
                {
                    i_ApplyTo.Add(mi_ChannelB);
                }
                else if (radioApplyAll.Checked)
                {
                    foreach (Channel i_Chan in comboChannelA.Items)
                    {
                        i_ApplyTo.Add(i_Chan);
                    }
                }
                return i_ApplyTo.ToArray();
            }
            else // Add, Subtract, Multiply Channels, AND, OR, XOR
            {
                if (mi_ChannelA == mi_ChannelB)
                    throw new Exception("Select two different channels.");

                int s32_Samples = Utils.CurCapture.ms32_Samples;
                mi_Result = Utils.CurCapture.FindOrCreateChannel(textResult.Text, mi_ChannelA, mi_ChannelB);
            
                if (mb_Analog)
                {
                    if (mi_Result.mf_Analog == null)
                        mi_Result.mf_Analog = new float[s32_Samples];

                    mi_Result.mi_SampleMinMax.mb_AnalogOK = false; // must be re-calculated
                }
                else // digital
                {
                    if (mi_Result.mu8_Digital == null)
                        mi_Result.mu8_Digital = new Byte[s32_Samples];

                    mi_Result.mi_MarkRows = null;
                    mi_Result.mi_SampleMinMax.mb_DigitalOK = false; // must be re-calculated
                }

                if (!comboChannelA.Items.Contains(mi_Result)) comboChannelA.Items.Add(mi_Result);
                if (!comboChannelB.Items.Contains(mi_Result)) comboChannelB.Items.Add(mi_Result);
                return null;
            }
        }
    }
}

