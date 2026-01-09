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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using PlatformManager   = Platform.PlatformManager;
using PlfMicrosoft      = Platform.PlfMicrosoft;
using eRuntime          = Platform.PlatformManager.eRuntime;

namespace OsziWaveformAnalyzer
{
    public class RtfViewer : RichTextBox
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams i_Params = base.CreateParams;

                // Adapt RichTextBox to use the new MsftEdit.dll
                PlatformManager.Instance.RtfBoxCreateParams(i_Params);
                return i_Params;
            }
        }

        protected override void WndProc(ref Message k_Msg)
        {
            // Adapt .NET framework to support clickable RTF links in RichTextBox
            PlatformManager.Instance.RtfBoxWndProc(this, ref k_Msg);

            base.WndProc(ref k_Msg);
        }

        /// <summary>
        /// OnLinkClicked does not work correctly using the new MsftEdit.dll --> do not use
        /// </summary>
        protected override void OnLinkClicked(LinkClickedEventArgs e)
        {
            if (PlatformManager.Runtime != eRuntime.Microsoft)
                OnTimestampClicked(e.LinkText);
        }

        /// <summary>
        /// The user has clicked a timestamp --> jump in OsziPanel to this location.
        /// Microsoft : This is called from class PlfMicrosoft
        /// Mono :      This is called from OnLinkClicked() above (if Mono supports clickable links)
        /// </summary>
        public void OnTimestampClicked(String s_LinkUrl)
        {
            String[] s_Parts = s_LinkUrl.Split(',');
            int s32_StartSample, s32_EndSample;
            if (s_Parts.Length != 2 || !int.TryParse(s_Parts[0], out s32_StartSample) ||
                                       !int.TryParse(s_Parts[1], out s32_EndSample))
            {
                Debug.Assert(false, "Programming Error: Invalid Link URL in RichTextBox.");
                return;
            }

            Utils.OsziPanel.JumpToSample(s32_StartSample);
            Utils.FormMain.SwitchToTab(0); // switch to tab "OsziPanel"
        }
    }

    // ==============================================================================================

    /// <summary>
    /// This class has never been designed to be thread safe.
    /// Important: Do not reuse this class. It must be re-created each time RTF is generated.
    /// </summary>
    public class RtfDocument
    {
        Color            mc_DefaultColor;
        String           ms_FontName;
        int              ms32_FontSize;
        List<Color>      mi_Colors     = new List<Color>();
        List<RtfBuilder> mi_Builders   = new List<RtfBuilder>();

        public Color DefaultColor
        {
            get { return mc_DefaultColor; }
        }

        public bool IsEmpty
        {
            get
            {
                foreach (RtfBuilder i_Builder in mi_Builders)
                {
                    if (!i_Builder.IsEmpty)
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public RtfDocument(Color c_DefaultColor, String s_FontName = "Courier New", int s32_FontSize = 11)
        {
            mc_DefaultColor = c_DefaultColor;
            ms_FontName     = s_FontName;
            ms32_FontSize   = s32_FontSize;
            Clear();
        }

        public void Clear()
        {
            mi_Builders.Clear();

            mi_Colors.Clear();
            mi_Colors.Add(mc_DefaultColor); // default text color
        }

        /// <summary>
        /// This allows to create multiple RTF texts in parallel which later are merged together in BuildRTF()
        /// </summary>
        public RtfBuilder CreateNewBuilder()
        {
            RtfBuilder i_Builder = new RtfBuilder(this);
            mi_Builders.Add(i_Builder);
            return i_Builder;
        }

        public int GetColorIndex(Color c_Color)
        {
            int s32_ColorIdx = mi_Colors.IndexOf(c_Color);
            if (s32_ColorIdx < 0)
            {
                s32_ColorIdx = mi_Colors.Count;
                mi_Colors.Add(c_Color);
            }
            return s32_ColorIdx;
        }

        /// <summary>
        /// https://www.biblioscape.com/rtf15_spec.htm
        /// </summary>
        public String BuildRTF()
        {
            StringBuilder i_RTF = new StringBuilder();

            i_RTF.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil\fcharset0 ");
            i_RTF.Append(ms_FontName);
            i_RTF.AppendLine(";}}");

            i_RTF.Append(@"{\colortbl;");
            foreach (Color c_Col in mi_Colors)
            {
                i_RTF.Append(@"\red");
                i_RTF.Append(c_Col.R);
                i_RTF.Append(@"\green");
                i_RTF.Append(c_Col.G);
                i_RTF.Append(@"\blue");
                i_RTF.Append(c_Col.B);
                i_RTF.Append(";");
            }
            i_RTF.AppendLine("}");
                
            i_RTF.Append(@"\viewkind5\uc1");

            // Set black background (RichTextBox.BackColor is already Black, but this is required for saving to *.RTF file)
            i_RTF.AppendLine(@"{\*\background{\shp{\*\shpinst");
            i_RTF.AppendLine(@"{\sp{\sn fillType}{\sv 0}}");
            i_RTF.AppendLine(@"{\sp{\sn fillColor}{\sv 0}}");
            i_RTF.AppendLine(@"{\sp{\sn fillBackColor}{\sv 0}}");
            i_RTF.AppendLine(@"{\sp{\sn fillFocus}{\sv 0}}");
            i_RTF.AppendLine(@"{\sp{\sn fillBlip}{\sv {\pict\wmetafile0\picscalex1\picscaley1");
            i_RTF.AppendLine(@"}}}}}}");

            // Set font 0 and font size
            i_RTF.Append(@"\pard\lang3082\f0\fs");
            i_RTF.Append(ms32_FontSize * 2); // Halfpoint --> Point
            i_RTF.AppendLine();

            foreach (RtfBuilder i_Builder in mi_Builders)
            {
                i_RTF.Append(i_Builder.GetRichText());
            }

            i_RTF.AppendLine(@"\par}");
            return i_RTF.ToString();
        }
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Important: Do not reuse this class. It must be re-created each time RTF is generated.
    /// </summary>
    public class RtfBuilder
    {
        // Do not use tabs which may result in 8 spaces / tab
        const int INDENT_RTF = 300; // equivalent to one tab

        RtfDocument   mi_Doc;
        StringBuilder mi_RichText    = new StringBuilder();
        FontStyle     me_FontStyle   = FontStyle.Regular;
        FontStyle     me_LastStyle   = FontStyle.Regular;
        int           ms32_LastColor = -1;
        int           ms32_ColorIdx;
        int           ms32_Indent;
        bool          mb_Linebreak;
        Int64         ms64_TimeFactor;
        String        ms_TimeUnit;
        Color         mc_LinkColor = Color.FromArgb(0x80, 0x80, 0xFF); // blue

        public bool IsEmpty
        {
            get { return mi_RichText.Length == 0; }
        }

        /// <summary>
        /// Set the indentation for the following text (one tab per value)
        /// </summary>
        public int Indent
        {
            get { return ms32_Indent; }
            set { ms32_Indent = Math.Max(0, value); }
        }

        /// <summary>
        /// Set the text color for the next text written with AppendText()
        /// </summary>
        public Color TextColor
        {
            set
            {
                if (value == Color.Empty || value == mi_Doc.DefaultColor)
                    ms32_ColorIdx = 0;
                else
                    ms32_ColorIdx = mi_Doc.GetColorIndex(value);
            }
        }

        public Color LinkColor
        {
            set { mc_LinkColor = value; }
        }

        public FontStyle FontStyle
        {
            set { me_FontStyle = value; }
        }

        // =====================================================================================

        public RtfBuilder(RtfDocument i_Doc)
        {
            mi_Doc = i_Doc;
        }

        public String GetRichText()
        {
            // reset to default color
            ms32_ColorIdx = 0; 
            WriteColor();

            // reset all font styles
            me_FontStyle = FontStyle.Regular;
            WriteFontStyle();

            return mi_RichText.ToString();
        }

        // =====================================================================================

        public void AppendLine(Color c_TextColor, Object o_Line, FontStyle e_Style = FontStyle.Regular)
        {
            TextColor = c_TextColor;
            FontStyle = e_Style;

            AppendText(o_Line.ToString());
            AppendNewLine();

            TextColor = mi_Doc.DefaultColor;
            FontStyle = FontStyle.Regular;
        }

        public void AppendText(Color c_TextColor, Object o_Text, FontStyle e_Style = FontStyle.Regular)
        {
            TextColor = c_TextColor;
            FontStyle = e_Style;

            AppendText(o_Text.ToString());
            
            TextColor = mi_Doc.DefaultColor;
            FontStyle = FontStyle.Regular;
        }

        public void AppendFormat(Color c_TextColor, String s_Format, params Object[] o_Args)
        {
            TextColor = c_TextColor;
            AppendText(String.Format(s_Format, o_Args));
            TextColor = mi_Doc.DefaultColor;
        }

        public void AppendText(String s_Text)
        {
            foreach (Char c_Char in s_Text)
            {
                AppendChar(c_Char);
            }
        }

        /// <summary>
        /// Appends then enum name with c_ColEnum and the enum [Descritption] attribute with c_ColDescr
        /// </summary>
        public void AppendEnum(Color c_ColEnum, int s32_Padding, Color c_ColDescr, Type t_Enum)
        {
            foreach (Enum e_Flag in Enum.GetValues(t_Enum))
            {
                Append2ColorPair(c_ColEnum, e_Flag + ":", s32_Padding, c_ColDescr, Utils.GetDescriptionAttribute(e_Flag));
            }
        }

        /// <summary>
        /// Appends a Property with c_ColProp and a Value with c_ColVal
        /// </summary>
        public void Append2ColorPair(Color c_ColProp, String s_Property, int s32_Padding, Color c_ColVal, String s_ValFormat, params Object[] o_Args)
        {
            AppendText  (c_ColProp, s_Property.PadRight(s32_Padding));
            AppendFormat(c_ColVal,  s_ValFormat + "\n", o_Args);
        }

        /// <summary>
        /// Adds a new line with a timestamp link of the form "XXX.YYY uu" (for example "97.281 µs")
        /// that the user can click and it jumps to this sample in the OsziPanel.
        /// The URL of the link are the start and end sample number separated by a comma.
        /// b_OneLine = true --> Print the timestamp and the following data into the same line
        /// </summary>
        public void AppendTimestampLine(int s32_StartSmpl, int s32_EndSmpl, bool b_OneLine)
        {
            if (ms_TimeUnit == null)
            {
                Int64 s64_TotPico = OsziPanel.CurCapture.ms64_SampleDist * OsziPanel.CurCapture.ms32_Samples;
                ms64_TimeFactor = 1;
                for (int U=0; U<Utils.TIME_UNITS.Length; U++)
                {
                    ms_TimeUnit = Utils.TIME_UNITS[U];
                    if (s64_TotPico < 1000)
                        break;
                    
                    s64_TotPico     /= 1000;
                    ms64_TimeFactor *= 1000;
                }
            }

            Int64  s64_SplPico = OsziPanel.CurCapture.ms64_SampleDist * s32_StartSmpl * 1000 / ms64_TimeFactor;
            String s_Timestamp = String.Format("{0}.{1:D3} {2}", s64_SplPico / 1000, s64_SplPico % 1000, ms_TimeUnit);
            String s_URL       = String.Format("{0},{1}", s32_StartSmpl, s32_EndSmpl);

            AppendNewLineOnce();
            AppendLink(s_URL, s_Timestamp);
            if (b_OneLine)
            {
                WriteFontStyle(); // remove underline
                AppendChar(' ');
            }
            else
            {
                AppendNewLine();
            }
        }

        // ----------------------------------------------------

        public void AppendChar(Char c_Char)
        {
            switch (c_Char)
            {
                case '\r':
                    return;

                case '\n':
                    AppendNewLine();
                    return;

                case '\t':
                    break; // Do nothing here. RTF allows Tabs in the code and shows them.
            }

            WriteIndentIfPending();

            if (c_Char != ' ')
            {
                WriteColor();
                WriteFontStyle();
            }

            switch (c_Char)
            {
                case '\\': mi_RichText.Append("\\\\"); break;
                case '{':  mi_RichText.Append("\\{");  break;
                case '}':  mi_RichText.Append("\\}");  break;
                default:   
                    if (c_Char < 256) mi_RichText.Append(c_Char);                        // Ansi
                    else              mi_RichText.AppendFormat("\\u{0}?", (uint)c_Char); // Unicode
                    break;
            }
        }

        /// <summary>
        /// Write a new line only if the previous line is not a new line
        /// </summary>
        public void AppendNewLineOnce()
        {
            if (mb_Linebreak)
                return;

            AppendNewLine();
        }

        public void AppendNewLine()
        {
            // Write the indentation even if the previous line was completely empty (for correct cursor position)
            WriteIndentIfPending();

            mi_RichText.AppendLine(@"\par");

            // The Rtf code for indentation cannot yet be written because the 
            // the indentation may be modified before the next character is written !
            mb_Linebreak = true;
        }

        /// <summary>
        /// Appends an underlined link with LinkColor
        /// </summary>
        public void AppendLink(String s_Url, String s_Display)
        {
            TextColor = mc_LinkColor;
            FontStyle = FontStyle.Underline;

            WriteColor();
            WriteFontStyle();

            String s_RichLink = @"{\field{\*\fldinst{HYPERLINK <URL>}}{\fldrslt{<DISP>}}}";
            s_RichLink = s_RichLink.Replace("<URL>",  '"' + s_Url + '"');
            s_RichLink = s_RichLink.Replace("<DISP>", s_Display);
            mi_RichText.Append(s_RichLink);

            TextColor = mi_Doc.DefaultColor;
            FontStyle = FontStyle.Regular;
        }

        // ================================= private =====================================

        void WriteIndentIfPending()
        {
            if (mb_Linebreak)
            {
                mb_Linebreak = false;

                if (ms32_Indent > 0)
                    mi_RichText.AppendFormat("\\li{0} ", ms32_Indent * INDENT_RTF);
            }
        }

        void WriteColor()
        {
            if (ms32_LastColor == ms32_ColorIdx)
                return;
            
            mi_RichText.AppendFormat("\\cf{0} ", ms32_ColorIdx + 1);

            ms32_LastColor = ms32_ColorIdx;
        }

        void WriteFontStyle()
        {
            if (me_LastStyle == me_FontStyle)
                return;
            
            if ((me_FontStyle & FontStyle.Bold)      > 0 && (me_LastStyle & FontStyle.Bold)      == 0) mi_RichText.Append("\\b ");
            if ((me_FontStyle & FontStyle.Italic)    > 0 && (me_LastStyle & FontStyle.Italic)    == 0) mi_RichText.Append("\\i ");
            if ((me_FontStyle & FontStyle.Underline) > 0 && (me_LastStyle & FontStyle.Underline) == 0) mi_RichText.Append("\\ul ");
            if ((me_FontStyle & FontStyle.Strikeout) > 0 && (me_LastStyle & FontStyle.Strikeout) == 0) mi_RichText.Append("\\strike ");

            if ((me_FontStyle & FontStyle.Bold)      == 0 && (me_LastStyle & FontStyle.Bold)      > 0) mi_RichText.Append("\\b0 ");
            if ((me_FontStyle & FontStyle.Italic)    == 0 && (me_LastStyle & FontStyle.Italic)    > 0) mi_RichText.Append("\\i0 ");
            if ((me_FontStyle & FontStyle.Underline) == 0 && (me_LastStyle & FontStyle.Underline) > 0) mi_RichText.Append("\\ulnone ");
            if ((me_FontStyle & FontStyle.Strikeout) == 0 && (me_LastStyle & FontStyle.Strikeout) > 0) mi_RichText.Append("\\strike0 ");

            me_LastStyle = me_FontStyle;
        }
    }
}
