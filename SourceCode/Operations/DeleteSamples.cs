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
using System.IO;
using System.Text;
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using IOperation        = Operations.OperationManager.IOperation;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using Utils             = OsziWaveformAnalyzer.Utils;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using Range             = OsziWaveformAnalyzer.Utils.Range;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;

namespace Operations
{
    /// <summary>
    /// Delete the samples before or after the click location
    /// </summary>
    public class DeleteSamples : IOperation
    {
        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            GraphMenuItem i_Before = new GraphMenuItem();
            i_Before.ms_MenuText   = "Select between first sample and mouse position";
            i_Before.ms_ImageFile  = "ArrowLeft.ico";
            i_Before.mo_Tag        = "SelectBefore";
            i_Items.Add(i_Before);

            GraphMenuItem i_After  = new GraphMenuItem();
            i_After.ms_MenuText    = "Select between mouse position and last sample";
            i_After.ms_ImageFile   = "ArrowRight.ico";
            i_After.mo_Tag         = "SelectAfter";
            i_Items.Add(i_After);
            
            GraphMenuItem i_Between = new GraphMenuItem();
            i_Between.ms_MenuText   = "Select between cursor and mouse position";
            i_Between.ms_ImageFile  = "ArrowMid.ico";
            i_Between.mo_Tag        = "SelectBetween";
            i_Items.Add(i_Between);

            /*
            // Select All is not required. Not selecting any samples does the same as Select All.
            GraphMenuItem i_All = new GraphMenuItem();
            i_All.ms_MenuText   = "Select all samples";
            i_All.ms_ImageFile  = "ArrowLeftRight.ico";
            i_All.mo_Tag        = "SelectAll";
            i_Items.Add(i_All);
            */ 

            if (Utils.PanelOszi.UserSelection.IsValid)
            {
                GraphMenuItem i_Delete = new GraphMenuItem();
                i_Delete.ms_MenuText   = "Delete selected samples";
                i_Delete.ms_ImageFile  = "Delete.ico";
                i_Delete.mo_Tag        = "Delete";
                i_Items.Add(i_Delete);
            }
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_ClickChannel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            Capture i_Capt = Utils.CurCapture;
            int s32_CursorSpl = Utils.PanelOszi.CursorSample;

            switch ((String)o_Tag)
            {
                case "SelectBefore":
                    Utils.PanelOszi.UserSelection = new Range(0, s32_Sample);
                    Utils.PanelOszi.Invalidate();
                    return "Samples selected.";

                case "SelectAfter":
                    Utils.PanelOszi.UserSelection = new Range(s32_Sample, i_Capt.ms32_Samples);
                    Utils.PanelOszi.Invalidate();
                    return "Samples selected.";

                case "SelectBetween":
                    if (s32_CursorSpl < 0)
                        throw new Exception("You must first set the cursor.");
                    int s32_Start = Math.Min(s32_Sample, s32_CursorSpl);
                    int s32_End   = Math.Max(s32_Sample, s32_CursorSpl);
                    Utils.PanelOszi.UserSelection = new Range(s32_Start, s32_End);
                    Utils.PanelOszi.Invalidate();
                    return "Samples selected.";

                case "SelectAll":
                    Utils.PanelOszi.UserSelection = new Range(0, i_Capt.ms32_Samples);
                    Utils.PanelOszi.Invalidate();
                    return "Samples selected.";
            }

            // ---------- Delete operation ----------

            Range k_Sel = Utils.PanelOszi.UserSelection;
            if (!k_Sel.IsValid)
                throw new Exception("You must first select the samples to delete.");

            int s32_Before = k_Sel.ms32_Start;
            int s32_After  = k_Sel.ms32_End;
            int s32_Count  = i_Capt.ms32_Samples - s32_After;

            int s32_Deleted = s32_After - s32_Before;
            i_Capt.ms32_Samples -= s32_Deleted;

            if (i_Capt.ms32_Samples < Utils.MIN_VALID_SAMPLES)
            {
                // Set Utils.CurCapture = null --> show black screen. eTab.Invalid -> no tab change
                Utils.PanelOszi.OnUserAction(eTab.Invalid, null);
                Utils.FormMain.ResetInputFile(); 
                return "All samples deleted. The minimum sample count is " + Utils.MIN_VALID_SAMPLES;
            }

            foreach (Channel i_Channel in i_Capt.mi_Channels)
            {
                if (i_Channel.mf_Analog != null)
                {
                    float[] f_NewAnalog = new float[i_Capt.ms32_Samples];
                    Array.Copy(i_Channel.mf_Analog, 0,         f_NewAnalog, 0,          s32_Before);
                    Array.Copy(i_Channel.mf_Analog, s32_After, f_NewAnalog, s32_Before, s32_Count);
                    i_Channel.mf_Analog = f_NewAnalog;
                }

                if (i_Channel.mu8_Digital != null)
                {
                    Byte[] u8_NewDigital = new Byte[i_Capt.ms32_Samples];
                    Array.Copy(i_Channel.mu8_Digital, 0,         u8_NewDigital, 0,          s32_Before);
                    Array.Copy(i_Channel.mu8_Digital, s32_After, u8_NewDigital, s32_Before, s32_Count);
                    i_Channel.mu8_Digital = u8_NewDigital;
                }

                // Must be calculated anew
                i_Channel.mi_MarkRows  = null;
                i_Channel.mb_Threshold = false;
            }

            // Delete and adjust separators
            List<int> i_SepList = new List<int>();
            foreach (int s32_Sep in i_Capt.ms32_Separators)
            {
                if (s32_Sep < s32_Before)
                    i_SepList.Add(s32_Sep); // copy unchanged

                if (s32_Sep > s32_After)
                    i_SepList.Add(s32_Sep - s32_Deleted); // move left
            }
            i_Capt.ms32_Separators = i_SepList.ToArray();

            // force recalculation of all Min/Max values of all channels
            i_Capt.ResetSampleMinMax(); 

            // Remove cursor if behind the waveform
            if (Utils.PanelOszi.CursorSample >= i_Capt.ms32_Samples)
                Utils.PanelOszi.SetCursor(-1, -1m); 

            Utils.PanelOszi.UserSelection = new Range(0, 0);

            Utils.CurCapture.mb_Dirty = true; // user has unsaved changes
            Utils.PanelOszi.RecalculateEverything();

            return s32_Deleted + " samples deleted";
        }
    }
}

