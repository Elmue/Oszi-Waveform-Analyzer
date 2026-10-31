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
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

using Utils             = OsziWaveformAnalyzer.Utils;
using AudioFile         = ExImport.AudioFile;
using TransferManager   = Transfer.TransferManager;
using OsziFile          = ExImport.OsziFile;

namespace OsziWaveformAnalyzer
{
    /// <summary>
    /// A ComboBox that shows Text and Images
    /// -------------------------------
    /// On Mono you must install:
    /// sudo apt install libgdiplus
    /// </summary>
    public class ComboBoxEx : ComboBox
    {
        #region enums

        public enum eComboImg
        {
            Binary,
            Capture,
            Decoder,
            Image,
            Music,
            OSZI,
            Text,
        }

        #endregion

        #region class ComboPath

        /// <summary>
        /// For ComboBox "Input File": Store full Path but display only Filename.
        /// </summary>
        public class ComboPath : ComboItem
        {
            public String ms_Path;

            public ComboPath(String s_Path) : base(Path.GetFileName(s_Path), null)
            {
                ms_Path = s_Path;
                if (s_Path == null)
                    return; // display "No files found"

                String s_Ext = Path.GetExtension(s_Path).ToLower();

                eComboImg e_Image = (eComboImg)(-1);
                if (!TransferManager.IsInputFile(s_Ext, ref e_Image) &&
                    !OsziFile       .IsInputFile(s_Ext, ref e_Image) &&
                    !AudioFile      .IsInputFile(s_Ext, ref e_Image))
                {
                    Debug.Assert(false, "Programming Error: File type not implemented: " + s_Ext);
                }

                mi_Image = Utils.GetComboImage(e_Image);
            }

            public override string ToString()
            {
                if (ms_Path == null)
                    return "No files found";

                return Path.GetFileName(ms_Path);
            }
        }

        #endregion

        #region class ComboItem

        public class ComboItem
        {
            public String ms_Text;

            // Mono may have problems to draw transaprent Icon's correctly.
            // As a workaround we use 32 bit Bitmaps with Alpha transparency.
            public Image mi_Image;

            public ComboItem(String s_Text, eComboImg e_Image)
                : this(s_Text, Utils.GetComboImage(e_Image))
            {
            }

            public ComboItem(String s_Text, Image i_Image = null)
            {
                if (i_Image != null)
                {
                    if (i_Image.PixelFormat != PixelFormat.Format32bppArgb)
                        throw new ArgumentException("All images in ComboBoxEx must be 32 bit ARGB");

                    if (i_Image.Width != ICON_SIZE || i_Image.Height != ICON_SIZE)
                        throw new ArgumentException("All images in ComboBoxEx must be "+ICON_SIZE+" x "+ICON_SIZE+" pixels");
                }
                ms_Text  = s_Text;
                mi_Image = i_Image;
            }

            public override string ToString()
            {
                return ms_Text;
            }
        }

        #endregion

        // Only icons 16 x 16 are implemented.
        public const int ICON_SIZE  = 16;
        public const int ICON_SPACE = ICON_SIZE + 2;  // horiz icon width  + padding
        public const int ROW_HEIGHT = ICON_SIZE + 1;  // vert  icon height + padding

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            DrawMode = DrawMode.OwnerDrawVariable;

            // DropDownHeight must be set here otherwise the drop down list will be crippled.
            DropDownHeight = 500;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) 
                return; // invalid index

            Color c_ForeColor;
            bool b_EditBox = e.State.HasFlag(DrawItemState.ComboBoxEdit);
            if  (b_EditBox)
            {
                // avoid that that the not-dropped-down Combox is painted white on blue
                e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds); // white
                c_ForeColor = SystemColors.WindowText;                    // black
            }
            else
            {
                e.DrawBackground(); // draw white / highlight (blue) background
                c_ForeColor = e.ForeColor;
            }

            Rectangle r_Text = e.Bounds;
            Object    o_Item = Items[e.Index];
            if (o_Item is ComboItem && !b_EditBox)
            {
                ComboItem i_Item = (ComboItem)o_Item;
                if (i_Item.mi_Image != null)
                    e.Graphics.DrawImageUnscaled(i_Item.mi_Image, e.Bounds.Location);

                r_Text.X     += ICON_SPACE;
                r_Text.Width -= ICON_SPACE;
            }

            const TextFormatFlags e_Flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, o_Item.ToString(), e.Font, r_Text, c_ForeColor, e_Flags);
        }

        protected override void OnMeasureItem(MeasureItemEventArgs e)
        {
            e.ItemHeight = ROW_HEIGHT;
        }
    }
}
