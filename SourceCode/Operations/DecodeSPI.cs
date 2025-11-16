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
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

using OperationManager  = Operations.OperationManager;
using IOperation        = Operations.OperationManager.IOperation;
using RtfDocument       = OsziWaveformAnalyzer.RtfDocument;
using RtfBuilder        = OsziWaveformAnalyzer.RtfBuilder;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using eDigiState        = OsziWaveformAnalyzer.Utils.eDigiState;
using SmplMark          = OsziWaveformAnalyzer.Utils.SmplMark;
using eMark             = OsziWaveformAnalyzer.Utils.eMark;
using eCtrlChar         = OsziWaveformAnalyzer.Utils.eCtrlChar;
using GraphMenuItem     = Operations.OperationManager.GraphMenuItem;
using eSpiChip          = PostDecoder.PostDecoderManager.eSpiChip;
using Utils             = OsziWaveformAnalyzer.Utils;
using OsziPanel         = OsziWaveformAnalyzer.OsziPanel;
using PostDecoderManager= PostDecoder.PostDecoderManager;
using PlatformManager   = Platform.PlatformManager;

namespace Operations
{
    public partial class DecodeSPI : Form, IOperation
    {
        #region class SpiPacket

        public class SpiPacket
        {
            public bool         mb_ChipSel;
            public List<UInt64> mi_MOSI;
            public List<UInt64> mi_MISO;
            public String       ms_Error;
            public int          ms32_StartSample; // The sample of the start of the packet
            public int          ms32_EndSample;
        }

        #endregion

        static Dictionary<String, String> mi_DemoFiles = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Static Constructor
        /// </summary>
        static DecodeSPI()
        {
            // See file "SPI Traffic PN532 RFID Electronic Doorlock.txt" in subfolder "Documentation"
            // for an explanation of the captured SPI data.
            mi_DemoFiles.Add("ISO 14443 RFID Mifare Card with PN532 over SPI Mode 0.oszi", 
                             "True,False,True,Rising Edge,8,LSB first,Low = Chip Selected,PN532");

            foreach (String s_DemoFile in mi_DemoFiles.Keys)
            {
                Utils.CheckDemoFileExists(s_DemoFile);
            }
        }

        Channel mi_CLK;
        Channel mi_MOSI;
        Channel mi_MISO;
        Channel mi_CSEL;
        int ms32_SmplPerBit;
        int ms32_Decoded = 0; // for status display
        int ms32_Errors  = 0; // for status display

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public void GetMenuItems(Channel i_Channel, bool b_Analog, List<GraphMenuItem> i_Items)
        {
            // At least 2 digital channels must exist for SPI
            if (i_Channel == null || b_Analog || OsziPanel.CurCapture.ms32_DigitalCount < 2)
                return; 

            GraphMenuItem i_Item = new GraphMenuItem();
            i_Item.ms_MenuText  = "Decode synchronous data: SPI / Shift register";
            i_Item.ms_ImageFile = "Chip.ico";
            i_Items.Add(i_Item);
        }

        /// <summary>
        /// Implementation of interface IOperation
        /// </summary>
        public String Execute(Channel i_Channel, int s32_Sample, bool b_Analog, Object o_Tag)
        {
            InitializeComponent();
            if (ShowDialog(Utils.FormMain) != DialogResult.OK)
                return null;

            return String.Format("{0} bytes decoded, {1} errors detected.", ms32_Decoded, ms32_Errors);
        }

        protected override void OnLoad(EventArgs e)
        {
 	        base.OnLoad(e);

            checkChipSelect.Checked = false;
            radioHalfDuplex.Checked = true;

            foreach (eSpiChip e_Chip in Enum.GetValues(typeof(eSpiChip)))
            {
                comboChip.Items.Add(e_Chip);
            }
            lblChip.Text = "";

            // Load the correct settings for the demo files
            String s_Settings;
            if (OsziPanel.CurCapture.ms_Path == null ||
                !mi_DemoFiles.TryGetValue(Path.GetFileName(OsziPanel.CurCapture.ms_Path), out s_Settings))
                s_Settings = Utils.RegReadString(eRegKey.SPI_Settg);

            Utils.SetControlValues(s_Settings, radioFullDuplex, radioHalfDuplex, checkChipSelect, comboClkEdge, comboDataBits, comboBitOrder, comboPolarity, comboChip);

            if (comboChip.SelectedIndex < 0)
                comboChip.SelectedIndex = 0; // "None"

            foreach (Channel i_Channel in OsziPanel.CurCapture.mi_Channels)
            {
                comboCLK .Items.Add(i_Channel);
                comboMISO.Items.Add(i_Channel);
                comboMOSI.Items.Add(i_Channel);
                comboCSEL.Items.Add(i_Channel);

                String s_Upper = i_Channel.ms_Name.ToUpper();
                if (s_Upper.Contains("CLK") || s_Upper.Contains("CLOCK") || s_Upper.Contains("SCK"))
                    comboCLK.SelectedItem = i_Channel;

                if (s_Upper.Contains("DATA") || s_Upper.Contains("MOSI"))
                    comboMOSI.SelectedItem = i_Channel;

                if (s_Upper.Contains("MISO"))
                {
                    radioFullDuplex.Checked = true;
                    comboMISO.SelectedItem  = i_Channel;
                }

                if (s_Upper == "CS" || s_Upper.Contains("SEL")) // CSEL, SSEL
                {
                    checkChipSelect.Checked = true;
                    comboCSEL.SelectedItem  = i_Channel;
                }
            }

            radioFullDuplex_CheckedChanged(null, null);
            checkChipSelect_CheckedChanged(null, null);
        }

        private void linkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PlatformManager.Instance.ShowHelp(this, "DecodeSync");
        }

        private void radioFullDuplex_CheckedChanged(object sender, EventArgs e)
        {
            lblMISO  .Enabled = radioFullDuplex.Checked;
            comboMISO.Enabled = radioFullDuplex.Checked;
        }

        private void checkChipSelect_CheckedChanged(object sender, EventArgs e)
        {
            lblChipSel   .Enabled = checkChipSelect.Checked;
            lblPolarity  .Enabled = checkChipSelect.Checked; 
            comboCSEL    .Enabled = checkChipSelect.Checked;
            comboPolarity.Enabled = checkChipSelect.Checked; 
        }

        private void comboChip_SelectedIndexChanged(object sender, EventArgs e)
        {
            lblChip.Text = "";
            eSpiChip e_Chip = (eSpiChip)comboChip.SelectedItem;
            if (e_Chip != eSpiChip.None)
                lblChip.Text = Utils.GetDescriptionAttribute(e_Chip);
        }

        // =============================================================================================================

        private void btnDecode_Click(object sender, EventArgs e)
        {
            mi_CLK  = (Channel)comboCLK .SelectedItem;
            mi_MOSI = (Channel)comboMOSI.SelectedItem;
            mi_MISO = radioFullDuplex.Checked ? (Channel)comboMISO.SelectedItem : null;
            mi_CSEL = checkChipSelect.Checked ? (Channel)comboCSEL.SelectedItem : null;

            if (mi_CLK == null || mi_MOSI == null || comboClkEdge.SelectedIndex < 0 || comboBitOrder.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Select all settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (radioFullDuplex.Checked && mi_MISO == null)
            {
                MessageBox.Show(this, "Select the MISO channel or switch to Half Duplex.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (checkChipSelect.Checked)
            {
                if (mi_CSEL == null || comboPolarity.SelectedIndex < 0)
                {
                    MessageBox.Show(this, "Select the Chip Select settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            int s32_DataBits;
            if (!int.TryParse(comboDataBits.Text, out s32_DataBits) || s32_DataBits < 3 || s32_DataBits > 64)
            {
                MessageBox.Show(this, "Data Bits must be in the range from 3 to 64.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            String s_Settings = Utils.GetControlValues(radioFullDuplex, radioHalfDuplex, checkChipSelect, comboClkEdge, comboDataBits, comboBitOrder, comboPolarity, comboChip);
            Utils.RegWriteString(eRegKey.SPI_Settg, s_Settings);

            bool b_MsbFirst   = comboBitOrder.SelectedIndex == 0;
            Byte u8_ClkStatus = comboClkEdge .SelectedIndex == 0 ? (Byte)1 : (Byte)0;
            Byte u8_SelActive = comboPolarity.SelectedIndex == 0 ? (Byte)0 : (Byte)1;

            Cursor = Cursors.WaitCursor;
            try
            {
                SpiPacket[] i_Packets = Decode(u8_ClkStatus, s32_DataBits, b_MsbFirst, u8_SelActive);
                ShowRtf(i_Packets, s32_DataBits);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex);
            }
            Cursor = Cursors.Arrow;

            Utils.OsziPanel.RecalculateEverything();
            DialogResult = DialogResult.OK;
        }

        // =============================================================================================================

        SpiPacket[] Decode(Byte u8_ClkStatus, int s32_DataBits, bool b_MsbFirst, Byte u8_SelActive)
        {
            ms32_Decoded = 0; // for status display

            OsziPanel.CurCapture.ResetMarks();

            List<SpiPacket> i_Packets = new List<SpiPacket>();
            SpiPacket       i_Packet  = new SpiPacket();

            UInt64 u64_BitMask   = 0;
            UInt64 u64_MOSI      = 0;
            UInt64 u64_MISO      = 0;
            bool   b_Reset       = true;
            int    s32_Bit       = 0;
            int    s32_FirstSmpl = 0;
            Byte   u8_LastClock  = mi_CLK.mu8_Digital[0];
            Byte   u8_LastSel    = 0;
            String s_HexFormat   = "X" + ((s32_DataBits + 3) / 4); // 8 bits == 2 hex digits --> HexFormat = "X2"

            List<SmplMark> i_ClkMarks  = new List<SmplMark>();
            List<SmplMark> i_MosiMarks = new List<SmplMark>();
            List<SmplMark> i_MisoMarks = new List<SmplMark>();
            List<SmplMark> i_SelMarks  = new List<SmplMark>();

            mi_CLK .mi_MarkRows = new List<SmplMark>[] { i_ClkMarks  };
            mi_MOSI.mi_MarkRows = new List<SmplMark>[] { i_MosiMarks };

            if (mi_MISO != null)
                mi_MISO.mi_MarkRows = new List<SmplMark>[] { i_MisoMarks };

            if (mi_CSEL != null)
            {
                mi_CSEL.mi_MarkRows = new List<SmplMark>[] { i_SelMarks };
                u8_LastSel = mi_CSEL.mu8_Digital[0];
            }

            for (int S=0; S<OsziPanel.CurCapture.ms32_Samples; S++)
            {
                if (mi_CSEL != null)
                {
                    if (mi_CSEL.mu8_Digital[S] != u8_LastSel) // ChipSelect has changed
                    {
                        u8_LastSel = mi_CSEL.mu8_Digital[S];

                        if (u8_LastSel == u8_SelActive) // Chip is active
                        {
                            i_SelMarks.Add(new SmplMark(eMark.Text, S, -1, " <"));

                            i_Packet.mb_ChipSel = true; // show "<" and ">" in RTF
                        }
                        else // Chip is disabled
                        {
                            i_Packet.mb_ChipSel = true; // show "<" and ">" in RTF

                            eMark e_Mark = eMark.Text;
                            if (!b_Reset)
                            {
                                e_Mark = eMark.Error;
                                i_ClkMarks.Add(new SmplMark(eMark.Error, S, -1, " more"));
                                i_Packet.ms_Error = "Error: Chip deselect while data is pending.";
                                ms32_Errors ++;
                            }
                            i_SelMarks.Add(new SmplMark(e_Mark, S, -1, " >"));

                            if (i_Packet.mi_MOSI != null)
                            {
                                i_Packet.ms32_EndSample = S;
                                i_Packets.Add(i_Packet);
                            }

                            i_Packet = new SpiPacket();
                            b_Reset  = true;
                        }
                    }

                    if (u8_LastSel != u8_SelActive)
                        continue; // Chip not selected --> ignore data
                }

                // Wait for expected clock edge
                if (mi_CLK.mu8_Digital[S] == u8_LastClock)
                    continue; // Clock did not change

                u8_LastClock = mi_CLK.mu8_Digital[S];
                if (u8_LastClock != u8_ClkStatus)
                    continue;

                if (b_Reset)
                {
                    b_Reset       = false;
                    s32_Bit       = b_MsbFirst ? s32_DataBits -1 : 0;
                    u64_BitMask   = (UInt64)1 << s32_Bit;
                    u64_MOSI      = 0;
                    u64_MISO      = 0;
                    s32_FirstSmpl = S;
                }

                i_ClkMarks.Add(new SmplMark(eMark.Text, S, -1, s32_Bit.ToString("X")));

                if (mi_MOSI.mu8_Digital[S] == 1)
                    u64_MOSI |= u64_BitMask;

                if (mi_MISO != null && mi_MISO.mu8_Digital[S] == 1)
                    u64_MISO |= u64_BitMask;

                if (b_MsbFirst)
                {
                    s32_Bit --;
                    u64_BitMask >>= 1;
                    b_Reset = (s32_Bit < 0);
                }
                else
                {
                    s32_Bit ++;
                    u64_BitMask <<= 1;
                    b_Reset = (s32_Bit >= s32_DataBits);
                }

                if (b_Reset)
                {
                    if (i_Packet.mi_MOSI == null)
                    {
                        i_Packet.mi_MOSI = new List<UInt64>();
                        i_Packet.ms32_StartSample = s32_FirstSmpl;
                    }

                    i_Packet.mi_MOSI.Add(u64_MOSI);
                    i_Packet.ms32_EndSample = S;
                    ms32_Decoded ++;

                    if (mi_MISO != null)
                    {
                        if (i_Packet.mi_MISO == null)
                            i_Packet.mi_MISO = new List<UInt64>();

                        i_Packet.mi_MISO.Add(u64_MISO);
                        ms32_Decoded ++;

                        i_MisoMarks.Add(new SmplMark(eMark.Text, s32_FirstSmpl, S, u64_MISO.ToString(s_HexFormat)));
                    }

                    int s32_AvrgSmpl = (S - s32_FirstSmpl) / (s32_DataBits - 1);
                    if (ms32_SmplPerBit == 0)
                        ms32_SmplPerBit = s32_AvrgSmpl;
                    else
                        ms32_SmplPerBit = Math.Min(ms32_SmplPerBit, s32_AvrgSmpl);

                    i_MosiMarks.Add(new SmplMark(eMark.Text, s32_FirstSmpl, S, u64_MOSI.ToString(s_HexFormat)));
                }
            }

            if (i_Packet.mi_MOSI != null)
                i_Packets.Add(i_Packet);

            return i_Packets.ToArray();
        }

        // =============================================================================================================

        void ShowRtf(SpiPacket[] i_Packets, int s32_DataBits)
        {
            String s_HexFormat = "X" + ((s32_DataBits + 3) / 4); // 8 bits == 2 hex digits --> HexFormat = "X2"

            RtfDocument i_RtfDoc     = new RtfDocument(Color.White);
            RtfBuilder  i_RtfBuilder = i_RtfDoc.CreateNewBuilder();

            i_RtfBuilder.AppendLine(OsziPanel.GetChannelColor(mi_MOSI), "MOSI Data");

            if (mi_MISO != null)
                i_RtfBuilder.AppendLine(OsziPanel.GetChannelColor(mi_MISO), "MISO Data");

            if (mi_CSEL != null)
                i_RtfBuilder.AppendLine(Color.Lime, "Chip is selected between  <  and  >");

            eSpiChip e_Chip = (eSpiChip)comboChip.SelectedItem;
            if (e_Chip != eSpiChip.None)
                i_RtfBuilder.AppendFormat(Color.White, "Post Decoding for {0} - {1}\n", e_Chip, Utils.GetDescriptionAttribute(e_Chip));

            i_RtfBuilder.AppendNewLine();

            // ------------------------------

            PostDecoderManager i_PostDecoder = new PostDecoderManager(e_Chip);

            StringBuilder i_Line = new StringBuilder();
            foreach (SpiPacket i_Packet in i_Packets)
            {
                i_RtfBuilder.AppendTimestampLine(i_Packet.ms32_StartSample, i_Packet.ms32_EndSample, false);

                for (int R=0; R<2; R++)
                {
                    List<UInt64> i_Values = (R == 0) ? i_Packet.mi_MOSI : i_Packet.mi_MISO;
                    if (i_Values == null)
                        continue;

                    if (i_Packet.mb_ChipSel) 
                        i_RtfBuilder.AppendText(Color.Lime, "< ");

                    i_Line.Clear();
                    foreach (UInt64 u64_Value in i_Values)
                    {
                        i_Line.Append(u64_Value.ToString(s_HexFormat));
                        i_Line.Append(' ');
                    }

                    Color c_TxtCol = OsziPanel.GetChannelColor((R == 0) ? mi_MOSI : mi_MISO);
                    i_RtfBuilder.AppendText(c_TxtCol, i_Line.ToString());

                    if (i_Packet.mb_ChipSel) 
                        i_RtfBuilder.AppendText(Color.Lime, ">");

                    i_RtfBuilder.AppendNewLine();
                }

                if (i_Packet.ms_Error != null)
                    i_RtfBuilder.AppendLine(Utils.ERROR_COLOR, i_Packet.ms_Error);
                else
                    i_PostDecoder.DecodeSPI(i_Packet, i_RtfBuilder);
            }

            if (i_Packets.Length == 0)
                i_RtfBuilder.AppendLine(Utils.ERROR_COLOR, "Nothing detected");

            // Show RTF and switch to tab "Decoder"
            Utils.FormMain.ShowAnalysisResult(i_RtfDoc, ms32_SmplPerBit); 
        }
    }
}

