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
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Windows.Forms;

using RtfDocument       = OsziWaveformAnalyzer.RtfDocument;
using RtfBuilder        = OsziWaveformAnalyzer.RtfBuilder;
using OsziPanel         = OsziWaveformAnalyzer.OsziPanel;
using Utils             = OsziWaveformAnalyzer.Utils;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;

namespace Operations
{
    /// <summary>
    /// This class manages the operations that can be selected in the combobox "Operation" and executed with button "Execute"
    /// </summary>
    public class OperationManager
    {
        #region class GraphMenuItem

        /// <summary>
        /// This class has to be filled in by every Operation class that is derived from IOperation
        /// </summary>
        public class GraphMenuItem
        {
            // The text to be displayed in the menu
            public String ms_MenuText;
            
            // The name of the image or icon file that must be in subfolder "Resources" and must be compiled as "Embedded Resource".
            // For example "ArrowLeft.ico" or "Port.ico"
            // Optimally the image should have 32x32 pixel, but also 16x16 is possible. Icon files can have multiple dimensions.
            // If this is null, no icon is displayed (not recommended)
            public String ms_ImageFile;
            
            // An optional Tag that is passed back to the function Execute()
            public Object mo_Tag;
        }

        #endregion

        #region class MenuTag

        private class MenuTag
        {
            public IOperation mi_Operation;
            public Object     mo_Tag;
            public int        ms32_Sample;
            public Channel    mi_Channel;
            public bool       mb_Analog;
        }

        #endregion

        #region interface IOperation

        /// <summary>
        /// Create any class that implements IOperation to extecute an operation.
        /// The class may open a Form if required.
        /// Each class will add one or more entries to the right-click menu in the OsziPanel.
        /// </summary>
        public interface IOperation
        {
            /// <summary>
            /// GetMenuItems() returns the items to be displayed in the right-click menu in the OsziPanel.
            /// i_Channel is the channel that the user has right-clicked or null if the user did not click on a channel.
            /// See comments in class GraphMenuItem.
            /// </summary>
            void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items);

            /// <summary>
            /// i_Channel is the channel that the user has right-clicked or null if the user did not click on a channel.
            /// ATTENTION: i_Channel may be null if the user did not click on a signal.
            /// b_Analog = true / false depending if the click was on the analog or digital part of a Channel.
            /// s32_Sample is the sample of the click position.
            /// o_Tag comes from GraphMenuItem.mo_Tag
            /// Execute() can return a text that is displayed in the status bar.
            /// If the text starts with "Error:" it is displayed in red, otherwise green.
            /// </summary>
            String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag);
        }

        #endregion

        // ========================= STATIC ==============================

        static List<Type> mi_Operations = new List<Type>();

        static OperationManager()
        {
            // The order matters !

            // ---------- Operations that affect all channels ------------
            mi_Operations.Add(typeof(SetCursor));
            mi_Operations.Add(typeof(DeleteSamples));

            // ---------- Operations that affect one channel ------------
            mi_Operations.Add(typeof(SetChannelName));
            mi_Operations.Add(typeof(MoveChannel));
            mi_Operations.Add(typeof(DeleteChannel));
            mi_Operations.Add(typeof(CopyPaste));
            mi_Operations.Add(typeof(InvertChannel));
            mi_Operations.Add(typeof(ConvertAD));
            mi_Operations.Add(typeof(Mathematical));
            mi_Operations.Add(typeof(NoiseFilter));

            // ---------- Decoder Operations ------------
            mi_Operations.Add(typeof(DecodeUART));
            mi_Operations.Add(typeof(DecodeSPI));
            mi_Operations.Add(typeof(DecodeI2C));
            mi_Operations.Add(typeof(DecodeCanBus));
            mi_Operations.Add(typeof(DecodeUSB));
            mi_Operations.Add(typeof(DecodeMagStripe));
            mi_Operations.Add(typeof(DecodeInfrared));
            mi_Operations.Add(typeof(MoreDecoders));

            // TODO: Add more logic analyzer functionality here.
            // Create a new class with Form or without Form and derive it from IOperation and add it's Type here.

            // -------------------------------------------------------

            #if DEBUG // In DEBUG mode check for correct implementation
                foreach (Type t_Operation in mi_Operations)
                {
                    try
                    {
                        // throws on any error during construction
                        CreateInstance(t_Operation);
                    }
                    catch
                    {
                        Debug.Assert(false, "Programming Error in constructor of class " + t_Operation.Name);
                    }
                }
            #endif
        }

        /// <summary>
        /// throws
        /// </summary>
        static IOperation CreateInstance(Type t_Operation)
        {
            ConstructorInfo i_Constr = t_Operation.GetConstructor(new Type[0]);
            if (i_Constr == null)
                throw new Exception("All classes derived from IOperation must have a parameterless public constructor.");

            Object o_Class = i_Constr.Invoke(new Object[0]);
            if (!(o_Class is IOperation))
                throw new Exception("All classes added in OperationManager must be derived from IOperation.");

            return (IOperation)o_Class;
        }

        // =================================================================================================

        /// <summary>
        /// Called from OsziPanel on right-click
        /// </summary>
        public static void ShowMenu(int s32_Sample, Channel i_Channel, bool b_Analog, Point k_Mouse)
        {
            try
            {
                ContextMenuStrip    i_Menu  = new ContextMenuStrip();
                List<GraphMenuItem> i_Items = new List<GraphMenuItem>();

                bool b_Separator1 = false;
                bool b_Separator2 = false;
                foreach (Type t_Operation in mi_Operations)
                {
                    IOperation i_Operation = CreateInstance(t_Operation);

                    i_Items.Clear();
                    i_Operation.GetMenuItems(i_Channel, b_Analog, i_Items);

                    foreach (GraphMenuItem i_Graph in i_Items)
                    {
                        if (!b_Separator1 && i_Graph.ms_MenuText.Contains("Channel"))
                        {
                            b_Separator1 = true;
                            i_Menu.Items.Add("-"); // adds a seperator line
                        }

                        if (!b_Separator2 && i_Graph.ms_MenuText.Contains("Decode"))
                        {
                            b_Separator2 = true;
                            i_Menu.Items.Add("-"); // adds a seperator line
                        }

                        ToolStripMenuItem i_Strip = new ToolStripMenuItem();
                        
                        MenuTag i_Tag = new MenuTag();
                        i_Tag.mi_Operation = i_Operation;
                        i_Tag.ms32_Sample  = s32_Sample;
                        i_Tag.mi_Channel   = i_Channel;
                        i_Tag.mb_Analog    = b_Analog;
                        i_Tag.mo_Tag       = i_Graph.mo_Tag;

                        i_Strip.Tag    = i_Tag;
                        i_Strip.Text   = i_Graph.ms_MenuText;
                        i_Strip.Click += new EventHandler(OnMenuClick);

                        if (!String.IsNullOrEmpty(i_Graph.ms_ImageFile))
                            i_Strip.Image = Utils.LoadResourceImage(i_Graph.ms_ImageFile);

                        i_Menu.Items.Add(i_Strip);
                    }
                }

                k_Mouse.X += 10;
                k_Mouse.Y += 10;
                i_Menu.Show(Utils.OsziPanel, k_Mouse, ToolStripDropDownDirection.BelowRight);
            }
            catch
            {
                Debug.Assert(false, "Programming Error creating right-click menu.");
            }
        }

        private static void OnMenuClick(object i_Sender, EventArgs e)
        {
            if (!Utils.CheckMinSamples())
                return;

            if (!Utils.StartBusyOperation(null))
                return;

            Utils.EndBusyOperation(null);

            try
            {          
                ToolStripMenuItem i_MenuItem = (ToolStripMenuItem)i_Sender;
                MenuTag i_Tag = (MenuTag)i_MenuItem.Tag;

                String s_Status = i_Tag.mi_Operation.Execute(i_Tag.mi_Channel, i_Tag.ms32_Sample, i_Tag.mb_Analog, i_Tag.mo_Tag);

                // ------------ Show Status -----------

                if (s_Status != null)
                {
                    Color c_Color = Color.Green;
                    if (s_Status.StartsWith("Error:"))
                    {
                        c_Color = Color.Red;
                        s_Status = s_Status.Substring(6).Trim(); // Cut "Error:"
                    }
                    Utils.FormMain.PrintStatus(s_Status, c_Color);
                }

                // display status "unsaved changes"
                Utils.FormMain.UpdateDirty();

                // ------------ Sort Marks -----------

                if (OsziPanel.CurCapture != null) // all channels may have been deleted --> Capture == null here
                {
                    foreach (Channel i_Channel in OsziPanel.CurCapture.mi_Channels)
                    {
                        if (i_Channel.mi_MarkRows != null)
                        {
                            foreach (List<SmplMark> i_MarkRow in i_Channel.mi_MarkRows)
                            {
                                // sort by ascending sample number
                                if (i_MarkRow != null) i_MarkRow.Sort();
                            }
                        }
                    }
                }

                // --------- Garbage Collection ---------

                foreach (ToolStripItem i_Item in i_MenuItem.Owner.Items)
                {
                    // The ToolStripItem.Tag contains a MenuTag class which contains an instance of IOperation.
                    // Setting the Tag to null results in faster garbage collection of all classes created by CreateInstance()
                    i_Item.Tag = null;
                }
            }
            catch (Exception Ex)
            {
                Utils.EndBusyOperation(null);
                Utils.ShowExceptionBox(null, Ex);
            }
        }
    }
}

