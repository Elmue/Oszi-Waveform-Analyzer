/*
------------------------------------------------------------
Oscilloscope Waveform Analyzer by ElmüSoft (www.netcult.ch/elmue)
This code is released under the terms of the GNU General Public License.
This class uses the NAudio project from https://github.com/naudio/NAudio to read the lossless WAV, AIFF and FLAC files.
The NAudio.Flac.dll in this project has been recompiled for .NET Framework 4.0 by Elmü.
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
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Flac;

using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Utils             = OsziWaveformAnalyzer.Utils;
using eComboImg         = OsziWaveformAnalyzer.ComboBoxEx.eComboImg;
using eRuntime          = Platform.PlatformManager.eRuntime;

namespace ExImport
{
    /// <summary>
    /// This class imports WAV, AIFF and FLAC audio files, either Mono or Stereo.
    /// The values are normalized to a range of -1.0 Volt to +1.0 Volt.
    /// Oscilloscope music uses Left = X and Right = Y, which can be displayed with the X/Y Plot.
    /// </summary>
    public static class AudioFile
    {
        const  int SMPL_PER_BLOCK   = 0x4000; // read blocks of 16 kB from the audio stream
        static int ms32_FileMaximum = Utils.MAX_MEMORY; 

        public static bool IsInputFile(String s_Extension, ref eComboImg e_Image)
        {
            switch (s_Extension)
            {
                // These are always lossless file formats:
                case ".aif":
                case ".aiff": // Audio Interchange File Format: Apple's standard uncompressed audio format.
                case ".flac": // Free Lossless Audio Codec (compressed)
                case ".wav":
                case ".wave": // uncompressed, lossless Wave
                
                // The following file formats may be lossless or lossy:
                case ".m4a":  // MPEG-4 Audio: Apple's most common container,  lossless if ALAC encoded
                case ".mp4":  // MPEG-4 Video: Multi Media Container, audio is lossless if ALAC encoded
                case ".wma":  // Windows Media Audio, lossless if WMA Lossless encoded
                    e_Image = eComboImg.Music;
                    return true;
                default:
                    return false;
            }
        }

        public static Capture LoadFile(String s_Path, ref bool b_Abort)
        {
            String s_Ext = Path.GetExtension(s_Path).ToLower();
            WaveStream i_WaveStream = null;
            try
            {
                switch (s_Ext)
                {
                    // Windows + Mono
                    case ".aif":
                    case ".aiff": i_WaveStream = new AiffFileReader(s_Path); break; // NAudio.dll
                    case ".flac": i_WaveStream = new FlacReader    (s_Path); break; // NAudio.Flac.dll
                    case ".wav":
                    case ".wave": i_WaveStream = new WaveFileReader(s_Path); break; // NAudio.dll
                    // Windows only
                    case ".m4a":
                    case ".mp4":
                    case ".wma":
                        if (Platform.PlatformManager.Runtime != eRuntime.Microsoft)
                            throw new Exception("The file formats M4A, MP4 and WMA can only be loaded on Windows.");

                        i_WaveStream = new MediaFoundationReader(s_Path); // NAudio.dll
                        break;
                    default:
                        throw new ArgumentException("Invalid audio format");
                }

                WaveFormat i_Fmt = i_WaveStream.WaveFormat;

                // The class Capture stores the channel data as float[]
                int s32_BytesPerSample = i_Fmt.Channels * sizeof(float);

                int s32_TotSamples = (int)(i_WaveStream.Length / i_Fmt.BlockAlign);
                if (s32_TotSamples < Utils.MIN_VALID_SAMPLES)
                    throw new Exception("The audio file contains only " + s32_TotSamples + " samples.");

                int s32_MemSize = s32_TotSamples * s32_BytesPerSample;
                if (s32_MemSize > ms32_FileMaximum)
                {
                    DialogResult e_Res = MessageBox.Show(Utils.FormMain, String.Format("The WAV file has {0:N0} samples ({1})\n"
                                         + "and will occupy {2} RAM.\n\n"
                                         + "Do you want to load the entire file into memory?\n"
                                         + "If you click 'No' only the first {3} will be loaded.",
                                            s32_TotSamples, 
                                            Utils.FormatTimePico(Utils.PICOS_PER_SECOND * s32_TotSamples / i_Fmt.SampleRate), 
                                            Utils.FormatSize(s32_MemSize, 0), 
                                            Utils.FormatSize(ms32_FileMaximum, 0)),
                                         "Audio File Import", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (e_Res)
                    {
                        case DialogResult.Yes:
                            // Remember the maximum size that the user has allowed for future file loads.
                            // NOTE: Oszi Waveform Analyser runs as 64 bit process, so it can use as much memory as it likes.
                            // But loading huge files makes no sense because processing and displaying becomes very slow.
                            ms32_FileMaximum = s32_MemSize;
                            break;

                        case DialogResult.No:
                            s32_TotSamples = ms32_FileMaximum / s32_BytesPerSample;
                            break;

                        default:
                            return null;
                    }
                }

                // -----------------------------------------------------------

                Capture i_Capture = new Capture();
                i_Capture.ms_Path         = s_Path;
                i_Capture.ms32_Samples    = s32_TotSamples;
                i_Capture.ms64_SampleDist = (Int64)Utils.PICOS_PER_SECOND / i_Fmt.SampleRate;
                // A 32 bit float variable has a maximum resolution of 23 bits + sign bit. 8 bits are used for the exponent.
                // The highest resolution that a Capture can store are 24 bits.
                i_Capture.ms32_AnalogRes  = Math.Min(24, i_Fmt.BitsPerSample);

                for (int C=0; C<i_Fmt.Channels; C++)
                {
                    Channel i_Channel   = new Channel("Audio " + (C+1));
                    i_Channel.mf_Analog = new float[s32_TotSamples];
                    i_Capture.mi_Channels.Add(i_Channel);
                }

                switch (i_Fmt.Channels)
                {
                    case 1:
                        i_Capture.mi_Channels[0].ms_Name = "Mono";
                        break;
                    case 2:
                        i_Capture.mi_Channels[0].ms_Name = "Left";
                        i_Capture.mi_Channels[1].ms_Name = "Right";
                        break;
                }

                // -----------------------------------------------------------

                // Automatically converts any PCM/audio format to 32-bit float (-1.0f to 1.0f)
                ISampleProvider i_SampleProvider = i_WaveStream.ToSampleProvider();

                float[] f_Buffer = new float[SMPL_PER_BLOCK * i_Fmt.Channels];
                int s32_SmplIdx  = 0;
                while (true)
                {
                    int s32_Remain = Math.Min(SMPL_PER_BLOCK, s32_TotSamples - s32_SmplIdx);
                    if (s32_Remain == 0)
                    {
                        Utils.FormMain.PrintStatus("Reading sample " + s32_SmplIdx.ToString("N0"), Color.Black);
                        break;
                    }

                    int s32_Read = i_SampleProvider.Read(f_Buffer, 0, s32_Remain * i_Fmt.Channels);
                    s32_Read /= i_Fmt.Channels;

                    if (s32_Read != s32_Remain)
                        throw new Exception("Inconsistent data reading audio stream");

                    int s32_BufIdx = 0;
                    for (int i=0; i<s32_Read; i++)
                    {
                        // Data in f_Buffer comes as Left, Right, Left, Right,....
                        for (int C=0; C<i_Fmt.Channels; C++)
                        {
                            i_Capture.mi_Channels[C].mf_Analog[s32_SmplIdx] = f_Buffer[s32_BufIdx++];
                        }
                        s32_SmplIdx ++;
                    }

                    if ((s32_SmplIdx & 0xFFFFF) < SMPL_PER_BLOCK) // display at 0x100000, 0x200000, 0x300000,....
                        Utils.FormMain.PrintStatus("Reading sample " + s32_SmplIdx.ToString("N0") + ". Please wait.", Color.Black);

                    if (b_Abort)
                        return null;
                }
                return i_Capture;
            }
            finally
            {
                if (i_WaveStream != null)
                    i_WaveStream.Dispose();
            }
        }
    }
}
