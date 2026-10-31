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
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using Utils             = OsziWaveformAnalyzer.Utils;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using Range             = OsziWaveformAnalyzer.Utils.Range;

namespace Operations
{
    /// <summary>
    /// This class allows to copy from one Capture, load another signal and copy to it, if the sample distance is the same.
    /// </summary>
    public partial class CopyPaste : IOperation
    {
        static Capture mi_Copy;

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null)
                return; // user did not click on a Channel

            GraphMenuItem i_Copy = new GraphMenuItem();
            i_Copy.ms_MenuText  = "Edit Signal - Copy one channel";
            i_Copy.ms_ImageFile = "Copy.ico";
            i_Copy.mo_Tag       = "CopyOne";
            i_Items.Add(i_Copy);

            if (Utils.CurCapture.mi_Channels.Count > 1)
            {
                GraphMenuItem i_All = new GraphMenuItem();
                i_All.ms_MenuText   = "Edit Signal - Copy all channels";
                i_All.ms_ImageFile  = "CopyAll.ico";
                i_All.mo_Tag        = "CopyAll";
                i_Items.Add(i_All);
            }

            if (mi_Copy != null)
            {
                GraphMenuItem i_Over = new GraphMenuItem();
                i_Over.ms_MenuText  = "Edit Signal - Paste Over (replace current signal)";
                i_Over.ms_ImageFile = "Paste.ico";
                i_Over.mo_Tag       = "PasteOver";
                i_Items.Add(i_Over);

                GraphMenuItem i_Insert = new GraphMenuItem();
                i_Insert.ms_MenuText  = "Edit Signal - Paste Insert (insert into current signal)";
                i_Insert.ms_ImageFile = "Paste.ico";
                i_Insert.mo_Tag       = "PasteInsert";
                i_Items.Add(i_Insert);
            }
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            Capture i_Capt = Utils.CurCapture;

            String s_Status = "Error";
            String s_Action = (String)o_Tag;
            switch (s_Action)
            {
                case "CopyOne":
                case "CopyAll":
                {
                    Range k_Sel = Utils.PanelOszi.UserSelection;
                    if (!k_Sel.IsValid)
                        throw new Exception("You must first select the samples to be copied.");

                    int s32_Start = k_Sel.ms32_Start;
                    int s32_End   = k_Sel.ms32_End;
                    int s32_Count = k_Sel.Count;

                    mi_Copy = new Capture();
                    mi_Copy.ms64_SampleDist = i_Capt.ms64_SampleDist;
                    mi_Copy.ms32_Samples    = s32_Count;

                    foreach (Channel i_Src in i_Capt.mi_Channels)
                    {
                        if (s_Action == "CopyOne" && i_Src != i_Channel)
                            continue; // copy only the channel that the user has clicked

                        Channel i_Dest = new Channel("");
                        if (i_Src.mf_Analog != null)
                        {
                            i_Dest.mf_Analog = new float[s32_Count];
                            Array.Copy(i_Src.mf_Analog, s32_Start, i_Dest.mf_Analog, 0, s32_Count);
                        }

                        if (i_Src.mu8_Digital != null)
                        {
                            i_Dest.mu8_Digital = new Byte[s32_Count];
                            Array.Copy(i_Src.mu8_Digital, s32_Start, i_Dest.mu8_Digital, 0, s32_Count);
                        }
                        mi_Copy.mi_Channels.Add(i_Dest);
                    }

                    // Copy separators
                    List<int> i_SepList = new List<int>();
                    foreach (int s32_Sep in i_Capt.ms32_Separators)
                    {
                        if (s32_Sep >= s32_Start && s32_Sep < s32_End)
                            i_SepList.Add(s32_Sep - s32_Start);
                    }
                    mi_Copy.ms32_Separators = i_SepList.ToArray();

                    s_Status = s32_Count.ToString("N0") + " samples copied";
                    break;
                }
                case "PasteOver":
                {
                    if (i_Capt.ms64_SampleDist != mi_Copy.ms64_SampleDist)
                        throw new Exception("The sample distance of the Copy capture and the Paste capture must be identical.");

                    if (mi_Copy.mi_Channels.Count > 1) // previous action was "CopyAll"
                        CheckChannelsMatch();

                    for (int C=0; C<i_Capt.mi_Channels.Count; C++)
                    {
                        Channel i_Dest = i_Capt.mi_Channels[C];
                        Channel i_Src;
                        if (mi_Copy.mi_Channels.Count == 1) // previous action was "CopyOne"
                        {
                            if (i_Dest != i_Channel)
                                continue; // paste only into the channel that the user has clicked

                            i_Src = mi_Copy.mi_Channels[0];
                        }
                        else // previous action was "CopyAll"
                        {
                            i_Src = mi_Copy.mi_Channels[C];
                        }

                        if (i_Src.mf_Analog != null && i_Dest.mf_Analog != null)
                        {
                            int s32_Count = Math.Min(i_Src.mf_Analog.Length, i_Dest.mf_Analog.Length - s32_Sample);
                            Array.Copy(i_Src.mf_Analog, 0, i_Dest.mf_Analog, s32_Sample, s32_Count);

                            i_Dest.mi_SampleMinMax.mb_AnalogOK = false; // re-calculate

                            s_Status = i_Src.mf_Analog.Length.ToString("N0") + " samples pasted";
                        }
                        
                        if (i_Src.mu8_Digital != null && i_Dest.mu8_Digital != null)
                        {
                            int s32_Count = Math.Min(i_Src.mu8_Digital.Length, i_Dest.mu8_Digital.Length - s32_Sample);
                            Array.Copy(i_Src.mu8_Digital, 0, i_Dest.mu8_Digital, s32_Sample, s32_Count);

                            i_Dest.mi_MarkRows = null;
                            i_Dest.mi_SampleMinMax.mb_DigitalOK = false; // re-calculate

                            s_Status = i_Src.mu8_Digital.Length.ToString("N0") + " samples pasted";
                        }     
                    }

                    Utils.CurCapture.mb_Dirty = true; // user has unsaved changes
                    Utils.PanelOszi.RecalculateEverything();
                    break;
                }
                case "PasteInsert":
                {
                    if (i_Capt.ms64_SampleDist != mi_Copy.ms64_SampleDist)
                        throw new Exception("The sample distance of the Copy capture and the Paste capture must be identical.");

                    CheckChannelsMatch();

                    for (int C=0; C<i_Capt.mi_Channels.Count; C++)
                    {
                        Channel i_Dest = i_Capt .mi_Channels[C];
                        Channel i_Src  = mi_Copy.mi_Channels[C];

                        if (i_Src.mf_Analog != null)
                        {
                            List<float> i_Analog = new List<float>(i_Dest.mf_Analog);
                            i_Analog.InsertRange(s32_Sample, i_Src.mf_Analog);
                            i_Dest.mf_Analog = i_Analog.ToArray();

                            i_Dest.mi_SampleMinMax.mb_AnalogOK = false; // re-calculate
                        }
                        
                        if (i_Src.mu8_Digital != null)
                        {
                            List<Byte> i_Digital = new List<Byte>(i_Dest.mu8_Digital);
                            i_Digital.InsertRange(s32_Sample, i_Src.mu8_Digital);
                            i_Dest.mu8_Digital = i_Digital.ToArray();

                            i_Dest.mi_MarkRows = null;
                            i_Dest.mi_SampleMinMax.mb_DigitalOK = false; // re-calculate
                        }     
                    }

                    // ----------- Separators -------------

                    List<int> i_SepList = new List<int>();
                    foreach (int s32_Sep in i_Capt.ms32_Separators)
                    {
                        if (s32_Sep < s32_Sample)
                            i_SepList.Add(s32_Sep); // copy

                        if (s32_Sep >= s32_Sample)
                            i_SepList.Add(s32_Sep + mi_Copy.ms32_Samples); // move right
                    }

                    foreach (int s32_Sep in mi_Copy.ms32_Separators)
                    {
                        i_SepList.Add(s32_Sep + s32_Sample);
                    }

                    i_SepList.Sort();
                    i_Capt.ms32_Separators = i_SepList.ToArray();

                    // -----------------------------

                    i_Capt.ms32_Samples += mi_Copy.ms32_Samples;
                    s_Status = mi_Copy.ms32_Samples.ToString("N0") + " samples pasted";

                    Utils.CurCapture.mb_Dirty = true; // user has unsaved changes
                    Utils.PanelOszi.RecalculateEverything();
                    break;
                }
            }
            return s_Status;
        }

        void CheckChannelsMatch()
        {
            Capture i_Capt = Utils.CurCapture;

            bool b_Match = i_Capt.mi_Channels.Count == mi_Copy.mi_Channels.Count;
            if (b_Match)
            {
                for (int C=0; C<i_Capt.mi_Channels.Count; C++)
                {
                    Channel i_Dest = i_Capt .mi_Channels[C];
                    Channel i_Src  = mi_Copy.mi_Channels[C];

                    if ((i_Dest.mf_Analog != null) != (i_Src.mf_Analog != null))
                        b_Match = false;

                    if ((i_Dest.mu8_Digital != null) != (i_Src.mu8_Digital != null))
                        b_Match = false;
                }
            }

            if (!b_Match)
                throw new Exception("To paste all channels make sure that the count and order "
                                  + "of analog / digital channels is the same as you copied from.");
        }
    }
}

