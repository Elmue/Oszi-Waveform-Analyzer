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
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

using Utils             = OsziWaveformAnalyzer.Utils;
using PanelOszi         = OsziWaveformAnalyzer.PanelOszi;
using Capture           = OsziWaveformAnalyzer.Utils.Capture;
using Channel           = OsziWaveformAnalyzer.Utils.Channel;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using Range             = OsziWaveformAnalyzer.Utils.Range;
using PlatformManager   = Platform.PlatformManager;
using IAudioInDevice    = Platform.PlatformManager.IAudioInDevice;
using delInvokeVoid     = OsziWaveformAnalyzer.Utils.delInvokeVoid;
using eTab              = OsziWaveformAnalyzer.FormMain.eTab;

namespace Transfer
{
    public partial class FormRecordAudio : Form
    {
        IAudioInDevice    mi_AudioDevice;
        int               ms32_Samplerate;
        int               ms32_Bits;
        int               ms32_Channels;
        bool              mb_Abort;
        List<float>[]     mi_ChannelData;
        Font              mi_BoldFont;
        Font              mi_NormFont;

        public FormRecordAudio()
        {
            InitializeComponent();

            lblLimit.Text = String.Format("The recording is limited to {0} memory.", Utils.FormatSize(Utils.MAX_MEMORY));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            btnRefresh_Click(null, null);

            comboBits      .Text = Utils.RegReadString(eRegKey.AudioInBits,       "16 bit");
            comboSamplerate.Text = Utils.RegReadString(eRegKey.AudioInSamplerate, "44.100");
            comboChannels  .Text = Utils.RegReadString(eRegKey.AudioInChannels,   "2 (Stereo)");

            if (comboSamplerate.SelectedIndex < 0) comboSamplerate.SelectedIndex = 0;
            if (comboBits.SelectedIndex       < 0) comboBits      .SelectedIndex = 1; // 16 bit
            if (comboChannels.SelectedIndex   < 0) comboChannels  .SelectedIndex = 1; // 2 Channels

            statusLabel.Text = "";

            mi_NormFont = btnStartStop.Font;
            mi_BoldFont = new Font(mi_NormFont, FontStyle.Bold);
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                List<IAudioInDevice> i_Devices = PlatformManager.Instance.EnumerateAudioInputDevices();
                comboDevice.Items.Clear();
                comboDevice.Items.AddRange(i_Devices.ToArray());
                Utils.ComboAdjustDropDownWidth(comboDevice);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(this, Ex, "Error enumerating audio input devices");
                Close();
                return;
            }

            comboDevice.Text = Utils.RegReadString(eRegKey.AudioInDevice);

            if (comboDevice.SelectedIndex < 0 && comboDevice.Items.Count > 0)
                comboDevice.SelectedIndex = 0;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            StopRecording(true);
            base.OnClosing(e);
        }

        private void btnStartStop_Click(object sender, EventArgs e)
        {
            if (btnStartStop.Text == "Stop")
            {
                StopRecording(false);
                return;
            }

            if (comboDevice.Items.Count == 0)
            {
                MessageBox.Show(this, "No audio input devices available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Utils.RegWriteString(eRegKey.AudioInDevice,     comboDevice    .Text);
            Utils.RegWriteString(eRegKey.AudioInBits,       comboBits      .Text);
            Utils.RegWriteString(eRegKey.AudioInSamplerate, comboSamplerate.Text);
            Utils.RegWriteString(eRegKey.AudioInChannels,   comboChannels  .Text);

            if (Utils.FormMain.HasUnsavedChanges())
                return;

            try
            {
                ms32_Samplerate = int.Parse(comboSamplerate.Text.Replace(".", ""));
                ms32_Bits       = int.Parse(comboBits      .Text.Split(' ')[0]); // "16 bit" --> "16"
                ms32_Channels   = int.Parse(comboChannels  .Text.Split(' ')[0]);
                mb_Abort        = false;

                mi_ChannelData = new List<float>[ms32_Channels];
                for (int C=0; C<ms32_Channels; C++)
                {
                    mi_ChannelData[C] = new List<float>();
                }

                mi_AudioDevice = (IAudioInDevice)comboDevice.SelectedItem;
                mi_AudioDevice.StartRecording(ms32_Samplerate, ms32_Channels, ms32_Bits, AudioDataCallback);

                btnStartStop.Text      = "Stop";
                btnStartStop.ForeColor = Color.Red;
                btnStartStop.Font      = mi_BoldFont;

                comboDevice    .Enabled = false;
                comboSamplerate.Enabled = false;
                comboBits      .Enabled = false;
                comboChannels  .Enabled = false;
            }
            catch (Exception Ex)
            {
                mi_AudioDevice = null;
                Utils.ShowExceptionBox(this, Ex, "Error starting audio recording.");
            }
        }

        /// <summary>
        /// f_Data = float[ChannelCount][SampleCount]
        /// Called every 100 ms from a Windows Multimedia API thread.
        /// ATTENTION: This event may even be fired after calling StopRecording()
        /// </summary>
        private void AudioDataCallback(float[][] f_Data)
        {
            if (mb_Abort)
                return;

            for (int C=0; C<ms32_Channels; C++)
            {
                mi_ChannelData[C].AddRange(f_Data[C]);
            }

            int s32_SmplCount = mi_ChannelData[0].Count;
            int s32_MemSize   = s32_SmplCount * ms32_Channels * sizeof(float);
            int s32_Seconds   = s32_SmplCount / ms32_Samplerate;

            BeginInvoke((delInvokeVoid)delegate() 
            {
                statusLabel.Text = String.Format("Samples: {0:N0}, Time: {1}:{2:D2}, Memory: {3}", s32_SmplCount, 
                                                 s32_Seconds / 60, s32_Seconds % 60, Utils.FormatSize(s32_MemSize, 0));
                panelVU.ShowVolume(f_Data);

                // StopRecording() accesses Button.Text --> must be called from GUI thread
                if (s32_MemSize > Utils.MAX_MEMORY)
                    StopRecording(false);
            });
        }

        void StopRecording(bool b_Dismiss)
        {
            mb_Abort = true;

            if (mi_AudioDevice != null)
            {
                mi_AudioDevice.StopRecording();
                mi_AudioDevice = null;
            }

            btnStartStop.Text      = "Start";
            btnStartStop.ForeColor = Color.Black;
            btnStartStop.Font      = mi_NormFont;

            comboDevice    .Enabled = true;
            comboSamplerate.Enabled = true;
            comboBits      .Enabled = true;
            comboChannels  .Enabled = true;

            panelVU.Reset();
            statusLabel.Text = "";

            if (mi_ChannelData != null && mi_ChannelData[0].Count > 0 && !b_Dismiss)
            {
                Capture i_Capture = new Capture();
                i_Capture.ms32_Samples    = mi_ChannelData[0].Count;
                i_Capture.ms64_SampleDist = (Int64)Utils.PICOS_PER_SECOND / ms32_Samplerate;
                // A 32 bit float variable has a maximum resolution of 23 bits + sign bit. 8 bits are used for the exponent.
                // The highest resolution that a Capture can store are 24 bits.
                i_Capture.ms32_AnalogRes  = Math.Min(24, ms32_Bits);

                for (int C=0; C<ms32_Channels; C++)
                {
                    Channel i_Channel = new Channel("Audio " + (C+1));
                    i_Channel.mf_Analog = mi_ChannelData[C].ToArray();
                    i_Capture.mi_Channels.Add(i_Channel);
                }

                switch (ms32_Channels)
                {
                    case 1:
                        i_Capture.mi_Channels[0].ms_Name = "Mono";
                        break;
                    case 2:
                        i_Capture.mi_Channels[0].ms_Name = "Left";
                        i_Capture.mi_Channels[1].ms_Name = "Right";
                        break;
                }

                Utils.FormMain.StoreNewCapture(i_Capture);
                Utils.FormMain.SwitchToTab(eTab.Oszi);
            }

            // Garbage collector will free this memory
            mi_ChannelData = null;
        }
    }
}

