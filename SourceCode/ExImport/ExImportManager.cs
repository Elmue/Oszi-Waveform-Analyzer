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
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Diagnostics;

using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using ComboPath         = OsziWaveformAnalyzer.Utils.ComboPath;
using Utils             = OsziWaveformAnalyzer.Utils;
using OsziPanel         = OsziWaveformAnalyzer.OsziPanel;
using TransferManager   = Transfer.TransferManager;

namespace ExImport
{
    /// <summary>
    /// This class imports and exports waveform data.
    /// You can add your own file format here.
    /// In the future someone may implement support for Matlab or SignalGenerator files.
    /// ATTENTION: 
    /// Any text-based formats will be slow and are deprecated.
    /// The OSZI file format is a proprietary format developed by Elmü which is very fast and secured by a CRC32 check to detect broken files.
    /// </summary>
    public class ExImportManager
    {
        #region enums

        /// <summary>
        /// The Description is displyed in the Combobox
        /// The enums are stored in the same order in the combobox
        /// </summary>
        public enum eSaveAs
        {
            [Description("Oszi File Zip")]
            OsziFileZip = 0,
            
            [Description("Oszi File Plain")]
            OsziFilePlain,

            [Description("Decoder Result")]
            RtfFile,
            
            [Description("Screenshot")]
            Screenshot,
            
            [Description("Full Image")]
            FullImage,

            [Description("WFM file")]
            WfmFile,
        }

        #endregion

        public static void FillComboSaveAs(ComboBox i_ComboSaveAs)
        {
            i_ComboSaveAs.Sorted = false; // IMPORTANT!
            i_ComboSaveAs.Items.Clear();
            foreach (eSaveAs e_Save in Enum.GetValues(typeof(eSaveAs)))
            {
                String s_Descript = Utils.GetDescriptionAttribute(e_Save);
                i_ComboSaveAs.Items.Add(s_Descript);
            }
            Utils.ComboAdjustDropDownWidth(i_ComboSaveAs);
            i_ComboSaveAs.SelectedIndex = 0;
        }

        // ===================================================================================================

        public eSaveAs me_SaveAs;
        public int     ms32_SaveSteps; // from ComboBox Factor "/ 5" --> ms32_SaveSteps = 5 --> save every fifth sample to disk

        TextBox   mi_TextFileName;
        ComboBox  mi_ComboOsziModel;

        /// <summary>
        /// Constructor
        /// </summary>
        public ExImportManager(TextBox i_TextFileName, ComboBox i_ComboOsziModel)
        {
            mi_TextFileName   = i_TextFileName;
            mi_ComboOsziModel = i_ComboOsziModel;
            ms32_SaveSteps    = 1;

            // Test encoding and decoding of digital channels in Mask mode and RLE mode.
            /*
            OsziFile.SelftestDigital();
            OsziFile.SelftestAnalog(); 
            */  
        }

        // ===================================================================================================

        /// <summary>
        /// Called when a file from ComboBox "Input" is selected or from CommandLine.
        /// </summary>
        public Capture Import(String s_Path, ref bool b_Abort)
        {
            // Create an analog and digital test signal with marks
            /*
            return OsziFile.SelftestCreateTestCapture();
            */ 

            mi_TextFileName.Text = Path.GetFileNameWithoutExtension(s_Path);

            String s_Ext = Path.GetExtension(s_Path).ToLower();
            switch (s_Ext)
            {
                case ".bin":
                case ".cap":
                case ".csv":  return TransferManager.ParseVendorFile(s_Path, mi_ComboOsziModel, ref b_Abort);
                case ".oszi": return OsziFile.Load(s_Path, ref b_Abort);
                case ".wfm":  ShowWfmErrorBox(); return null;

                // TODO: You can implement further file formats here

                default: 
                    Debug.Assert(false, "Programming Error: Invalid Import file extension.");
                    return null;
            }
        }

        // ===================================================================================================

        /// <summary>
        /// Called when button "Save" is clicked
        /// </summary>
        public void Export(String s_Path)
        {
            String s_Saved = "";
            switch (me_SaveAs)
            {
                case eSaveAs.OsziFilePlain: s_Saved = OsziFile.Save(OsziPanel.CurCapture, s_Path, false, ms32_SaveSteps); break;
                case eSaveAs.OsziFileZip:   s_Saved = OsziFile.Save(OsziPanel.CurCapture, s_Path, true,  ms32_SaveSteps); break;
                case eSaveAs.Screenshot:    s_Saved = Utils.OsziPanel.SaveAsImage(s_Path, false); break;
                case eSaveAs.FullImage:     s_Saved = Utils.OsziPanel.SaveAsImage(s_Path, true);  break;
                case eSaveAs.RtfFile:       s_Saved = Utils.FormMain.SaveRichText(s_Path); break;
                case eSaveAs.WfmFile:       ShowWfmErrorBox(); return;

                // TODO: You can implement further file formats here

                default: 
                    Debug.Assert(false, "Programming Error: Invalid SaveAs option");
                    break;
            }

            if (s_Saved != null)
                Utils.FormMain.PrintStatus("Saved "+s_Saved+" to "+s_Path, Color.Green);
        }

        /// <summary>
        /// returns the Save path or null on error
        /// </summary>
        public String GetSavePath()
        {
            // Cut any ".oszi" that the user may have pasted
            mi_TextFileName.Text = Path.GetFileNameWithoutExtension(mi_TextFileName.Text);

            if (mi_TextFileName.Text.Length < 5)
            {
                MessageBox.Show(Utils.FormMain, "Enter at least 5 characters.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                mi_TextFileName.Focus();
                return null;
            }

            String s_Path = Path.Combine(Utils.SampleDir, mi_TextFileName.Text);
            switch (me_SaveAs)
            {
                case eSaveAs.OsziFilePlain:
                case eSaveAs.OsziFileZip:  s_Path += ".oszi"; break;
                case eSaveAs.Screenshot:
                case eSaveAs.FullImage:    s_Path += ".png";  break;
                case eSaveAs.RtfFile:      s_Path += ".rtf";  break;
                case eSaveAs.WfmFile:      ShowWfmErrorBox(); return null;
                default: 
                    Debug.Assert(false, "Programming Error: Invalid Save option: " + me_SaveAs);
                    return null;
            }

            if (File.Exists(s_Path))
            {
                if (MessageBox.Show(Utils.FormMain, "The file already exists:\n" + s_Path + 
                                    "\n\nDo you want to move it to the recycle bin?", "Error", 
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                    return null;

                if (!Utils.MoveToRecycler(Utils.FormMain, s_Path))
                    return null;
            }
            return s_Path;
        }

        public void LoadComboInput(ComboBox i_ComboInput)
        {
            try
            {
                List<String> i_Extensions = new List<String>(new String[]{ ".bin", ".cap", ".csv", ".oszi", ".wfm"});

                i_ComboInput.Items.Clear();
                foreach (String s_Path in Directory.EnumerateFiles(Utils.SampleDir))
                {
                    if (i_Extensions.Contains(Path.GetExtension(s_Path).ToLower()))
                    {
                        i_ComboInput.Items.Add(new ComboPath(s_Path));
                    }
                }

                if (i_ComboInput.Items.Count > 0)
                {
                    Utils.FormMain.PrintStatus(Utils.NO_SAMPLES_LOADED, Color.Black);
                }
                else
                {
                    // null --> display "No files found"
                    i_ComboInput.Items.Add(new ComboPath(null));
                    i_ComboInput.SelectedIndex = 0;
                }

                Utils.ComboAdjustDropDownWidth(i_ComboInput);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(null, Ex);
            }
        }

        void ShowWfmErrorBox()
        {
            MessageBox.Show(Utils.FormMain, "The WFM file format is used by some companies. "
                                          + "Documentation is available from Tektronix, but not from Rigol. "
                                          + "Rigol is not able to develop anything that is consistent. "
                                          + "There is a different WFM file header for each of their oscillosope series! "
                                          + "And among each serie there are different versions of the same file header. "
                                          + "To implement WFM format many totally different file formats would have to be implemented. "
                                          + "And tomorrow Rigol will invent another different format again. " 
                                          + "Rigol is the most stupid company!\n"
                                          + "The consequence is simple: Don't use WFM files at all. "
                                          + "Save and load oscilloscope signals in the simple open-source OSZI format instead.", "Not supported", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
