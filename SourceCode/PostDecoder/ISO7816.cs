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
using System.Drawing;
using System.Collections.Generic;
using System.Text;

using SpiPacket         = Operations.DecodeSPI.SpiPacket;
using I2CPacket         = Operations.DecodeI2C.I2CPacket;
using UartPacket        = Operations.DecodeUART.UartPacket;
using RtfDocument       = OsziWaveformAnalyzer.RtfDocument;
using RtfBuilder        = OsziWaveformAnalyzer.RtfBuilder;
using Utils             = OsziWaveformAnalyzer.Utils;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using IPostDecode       = PostDecoder.PostDecoderManager.IPostDecode;

namespace PostDecoder
{
    /// <summary>
    /// This class is absolutely incomplete.
    /// It only executes the baudrate switch after the card has sent the "Answer to Reset" packet.
    /// It works with the demo file, but has not been tested with any other input.
    /// If you are interested in ISO 7816 smartcard decoding, have a look at subfolder "Documentation" 
    /// and  https://github.com/OpenSC/OpenSC/
    /// </summary>
    public class ISO7816 : IPostDecode
    {
        Channel   mi_Reset;
        Channel   mi_Pinpad;
        Channel   mi_Smartcard;
        bool      mb_Finished;
        List<int> mi_RxBytes = new List<int>();

        /// <summary>
        /// Constructor
        /// </summary>
        public ISO7816()
        {
            foreach (Channel i_Channel in Utils.CurCapture.mi_Channels)
            {
                if (i_Channel.mu8_Digital == null)
                    return;

                switch (i_Channel.ms_Name.ToUpper())
                {
                    case "RESET":     mi_Reset     = i_Channel; break;
                    case "PINPAD":    mi_Pinpad    = i_Channel; break;
                    case "SMARTCARD": mi_Smartcard = i_Channel; break;
                }
            }
        }

        /// <summary>
        /// Only the baudrate switch is implemented yet.
        /// </summary>
        public void DecodeUART(UartPacket i_Packet, RtfBuilder i_RtfBuilder)
        {
            // This decoder is incomplete. Currently it only switches the baudrate. 
            // After doing this it is finished and ignores further packets.
            if (mb_Finished)
                return;

            Debug.Assert(i_Packet.ms32_Baudrate > 0, "Programming Error: UartPacket.ms32_Baudrate not set!");

            if (mi_Reset == null || mi_Pinpad == null || mi_Smartcard == null)
            {
                i_RtfBuilder.AppendLine(Utils.ERROR_COLOR, "The ISO 7816 decoder needs 3 digital channels with the names 'Reset', 'Pinpad' and 'Smartcard'.");
                mb_Finished = true;
                return;
            }

            // The first packet is always the ATR packet (Answer To Reset) if the user has captured the channels correctly.
            if (String.Compare(i_Packet.ms_Name, "Smartcard", true) != 0)
            {
                i_RtfBuilder.AppendLine(Utils.ERROR_COLOR, "The first ISO 7816 packet is expected on the channel 'Smartcard'.");
                mb_Finished = true;
                return;
            }

            // The first byte of the ATR packet is either 0x3B or 0x3F if the user has entered BitOrder and StartBit Polarity correctly.
            // See "ISO 7816-03.pdf" page 8 in subfolder "Documentation".
            // But some smartcards send the first byte (0x3B), then make a long pause and then the rest of the packet.
            // The UART decoder decets the pause and sends 2 separate packets.
            // So the first bytes are buffered in mi_RxBytes until at least 10 bytes hav been received.
            mi_RxBytes.AddRange(i_Packet.mi_Data);
            if (mi_RxBytes.Count < 10)
                return;

            if (mi_RxBytes[0] != 0x3B && mi_RxBytes[0] != 0x3F)
            {
                i_RtfBuilder.AppendLine(Utils.ERROR_COLOR, "The first ISO 7816 packet is expected to be the 'Answer To Reset'.");
                mb_Finished = true;
                return;
            }

            // The baudrate must NOT be switched if after this packet the card receives a RESET on the Reset pin (Low).
            // The second reset is the "Soft Reset". After this the card responds again with the same baudrate.
            for (int S=i_Packet.ms32_StartSample; S<Utils.CurCapture.ms32_Samples; S++)
            {
                if (mi_Reset.mu8_Digital[S] == 0)
                {
                    mi_RxBytes.Clear();
                    return; // another card reset will follow --> do not change baudrate yet
                }
            }

            // See "ISO 7816-03.pdf" page 9 in subfolder "Documentation".
            Byte u8_T0  = (Byte)mi_RxBytes[1]; // Encodes Y1 and K            
            Byte u8_TA1 = (Byte)mi_RxBytes[2]; // Encodes Fi and Di

            if (u8_TA1 == 0)
            {
                // The baudrate will not be changed
                mb_Finished = true;
                return;
            }

            // See "ISO 7816-03.pdf" page 18 in subfolder "Documentation".
            int s32_Fi = 0;
            switch (u8_TA1 >> 4) // Fi = clock rate conversion integer
            {
                case 0x0: s32_Fi =  372; break;
                case 0x1: s32_Fi =  372; break;
                case 0x2: s32_Fi =  558; break;
                case 0x3: s32_Fi =  744; break;
                case 0x4: s32_Fi = 1116; break;
                case 0x5: s32_Fi = 1488; break;
                case 0x6: s32_Fi = 1860; break;
                // RFU
                case 0x9: s32_Fi =  512; break;
                case 0xA: s32_Fi =  768; break;
                case 0xB: s32_Fi = 1024; break;
                case 0xC: s32_Fi = 1536; break;
                case 0xD: s32_Fi = 2048; break;
                // RFU
            }

            int s32_Di = 0;
            switch (u8_TA1 & 0x0F) // Di = baud rate adjustment integer 
            {
                // RFU
                case 0x1: s32_Di =  1; break;
                case 0x2: s32_Di =  2; break;
                case 0x3: s32_Di =  4; break;
                case 0x4: s32_Di =  8; break;
                case 0x5: s32_Di = 16; break;
                case 0x6: s32_Di = 32; break;
                case 0x7: s32_Di = 64; break;
                case 0x8: s32_Di = 12; break;
                case 0x9: s32_Di = 20; break;
                // RFU
            }

            if (s32_Fi == 0 || s32_Di == 0)
            {
                i_RtfBuilder.AppendLine(Utils.ERROR_COLOR, "The card has sent an unrecognized 'Answer To Reset' packet.");
                mb_Finished = true;
                return;
            }

            // The ATR packet should always be sent with 1/372 of the clock frequency.
            // Calculate the clock frequency of the card from the baudrate that the user has entered.
            int s32_ClockFrequ = i_Packet.ms32_Baudrate * 372;

            // Calculate the new baudrate as defined by ISO 7816
            i_Packet.ms32_Baudrate = s32_ClockFrequ * s32_Di / s32_Fi;
            mb_Finished = true;
        }

        public void DecodeSPI(SpiPacket i_Packet, RtfBuilder i_RtfBuilder)
        {
            throw new NotImplementedException();
        }
        public void DecodeI2C(I2CPacket i_Packet, RtfBuilder i_RtfBuilder)
        {
            throw new NotImplementedException();
        }
    }
}
