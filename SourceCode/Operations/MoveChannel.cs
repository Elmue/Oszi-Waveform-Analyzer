﻿/*
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
using Utils             = OsziWaveformAnalyzer.Utils;
using OsziPanel         = OsziWaveformAnalyzer.OsziPanel;
using IOperation        = Operations.OperationManager.IOperation;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;

namespace Operations
{
    public partial class MoveChannel : IOperation
    {
        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            if (i_Channel == null || OsziPanel.CurCapture.mi_Channels.Count < 2)
                return;

            if (b_Analog && !Utils.OsziPanel.SeparateChannels)
                return; // Analog channels cannot be moved while drawn one on top of the other

            int s32_Index = OsziPanel.CurCapture.mi_Channels.IndexOf(i_Channel);
            if (s32_Index > 0)
            {
                GraphMenuItem i_Up = new GraphMenuItem();
                i_Up.ms_MenuText   = "Move Channel Up";
                i_Up.ms_ImageFile  = "ArrowUp.ico";
                i_Up.mo_Tag        = "Up";
                i_Items.Add(i_Up);
            }

            if (s32_Index < OsziPanel.CurCapture.mi_Channels.Count - 1)
            {
                GraphMenuItem i_Down = new GraphMenuItem();
                i_Down.ms_MenuText  = "Move Channel Down";
                i_Down.ms_ImageFile = "ArrowDown.ico";
                i_Down.mo_Tag       = "Down";
                i_Items.Add(i_Down);
            }
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_ChannelSrc, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            List<Channel> i_Channels = OsziPanel.CurCapture.mi_Channels;
            int s32_SrcIndex = i_Channels.IndexOf(i_ChannelSrc);
            int s32_DstIndex = s32_SrcIndex;

            switch ((String)o_Tag)
            {
                case "Up":   s32_DstIndex --; break;
                case "Down": s32_DstIndex ++; break;
            }
            Channel i_ChannelDst = i_Channels[s32_DstIndex];
            
            // swap channels
            i_Channels[s32_DstIndex] = i_ChannelSrc;
            i_Channels[s32_SrcIndex] = i_ChannelDst;

            OsziPanel.CurCapture.mb_Dirty = true; // user has unsaved changes
            Utils.OsziPanel.RecalculateEverything();

            return "Channel moved.";
        }
    }
}

