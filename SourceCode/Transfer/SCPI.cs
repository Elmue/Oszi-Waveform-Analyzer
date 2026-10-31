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

// --------------------------------------------------------------

// Writes debug output to the debugger (or SysInternals DbgView)
// See file "Logfile SCPI Commands DS1074Z.txt" in subfolder "Documentation" which shows a successful communication.
#if DEBUG
//   #define TRACE_OUTPUT
#endif

// --------------------------------------------------------------

using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using Utils             = OsziWaveformAnalyzer.Utils;
using IUsbDevice        = Platform.PlatformManager.IUsbDevice;
using PlatformManager   = Platform.PlatformManager;
using eRs232Settg       = Transfer.TransferManager.eRs232Settg;
using Progress          = Transfer.FormTransfer.Progress;
using AbortException    = OsziWaveformAnalyzer.Utils.AbortException;

namespace Transfer
{
    /// <summary>
    /// This class implements the TMC (Test and Measurement Class) 
    /// using the SCPI protocol (Standard Command for Programmable Instruments) over USB.
    /// https://en.wikipedia.org/wiki/Standard_Commands_for_Programmable_Instruments
    /// 
    /// SCPI is an additional layer on top of the IEEE 488.2 specification.
    /// See subfolder "Documentation" for more details.
    /// 
    /// All commands and responses are ASCII text. Only the command :WAVEFORM may be configured to return binary data.
    /// 
    /// You can enable TRACE_OUTPUT in this class to see debug output of the entire USB communication.
    /// 
    /// This class has not been designed to be thread safe.
    /// 
    /// This class makes it unnecessary to install huge software packets of one gigabyte from NI, IVI, Tektronix
    /// or the huge Rigol UltraSigma which is bad quality primitive software.
    /// 
    /// Only the tiny IVI USB driver of 24 kilobyte size is required. (ausbtmc.sys)
    /// It is included in this project.
    /// 
    /// This class communicates directly with the oscilloscope using the standard Windows API without any external dependencies. 
    /// It implements non-blocking API calls using Overlapped I/O which is controlled by a timeout.
    /// No fancy C# "async Task" or "await" or "CancellationToken" are required here which you find other projects.
    /// They would only bloat up the code to the double size and produce a lot of unnecessary thread switching.
    /// By the way: USB High-Speed runs at 480 MBit/s and the communications is very fast. The wait time is a few millisconds.
    /// 
    /// This class has been written by a software developer with 45 years of experience in programming and cracking.
    /// It has been tested with a Rigol DS1074Z, but it should also work with other brands, even with function generators, mulimeters, etc.
    /// 
    /// This class was inspired by klasyc/ScpiNet. However his code has several design errors, it was rewritten from scratch bu Elmü.
    /// </summary>
    public class SCPI : IDisposable
    {
        #region enums

        /// <summary>
        /// This enum is only used for transferring binary data from :WAVEFORM:DATA? over a TCP connection.
        /// As SCPI is extremely primitive the only way to detect the last data packet is the Linefeed at the end.
        /// Unlike for USB there is no struct kTmcHeader that has a flag indicating the last packet.
        /// How to make sure that a byte of 0x0A within the binary data does not terminate the transmission?
        /// This enum defines how to detect the end of the binary data for a TCP connection.
        /// ATTENTION:
        /// The option to receive data until a timeout is no option because it would make the transfer EXTREMELY slow.
        /// </summary>
        public enum eBinaryTCP
        {
            // It seems that the Rigol serie DS1000DE does not send linefeed bytes inside the binary data.
            // At least the file "Rigol DS1000E Waveform Guide.htm" in subfolder Documentation says so:
            // "....each byte should have a value of between 15 and 240."
            // "The top of the LCD display of the scope represents byte value 25 and the bottom is 225."
            // So it seems that they take care not to send linefeeds withinh the data.
            // In this mode the transmission ends when a packet ends with a linefeed.
            Linefeed,

            // The Rigol serie DS1000Z definitely sends bytes of value 0x0A within the binary data.
            // But the oscilloscope sends reliably the count of samples that are to be transmitted for the current configuration.
            // In this mode the transmission ends when the given minimum count of bytes was received.
            // Behind the last binary byte comes the linefeed and sometimes a completely useless padding byte.
            // These are eliminated here.
            MinSize,
        }

        [FlagsAttribute]
        public enum eConnectMode
        {
            Invalid = 0,
            USB     = 0x01, // This is for TMC over USB using the IVI driver, NOT for vritual COM ports
            VXI     = 0x02, // VXI over TCP and UDP
            TCP     = 0x04, // SCPI over TCP
            RS232   = 0x08, // Real RS232 plugs and virtual COM ports
        }

        #endregion

        #region TMC

        enum eTmcMsgId : byte
        {
            DEV_DEP_MSG_OUT        = 1,
            REQUEST_DEV_DEP_MSG_IN = 2,
        }

        /// <summary>
        /// USB TMC frame header according to the TMC specification. It is sent with each read and write request.
        /// See USBTMC specification in subfolder "Documentation".
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct kTmcHeader 
        {
            public eTmcMsgId e_MsgID;   // Byte  0
            public byte u8_Tag;         // Byte  1  = Counter, must always be > 0
            public byte u8_TagInverse;  // Byte  2  = One's complement of Tag
            public byte u8_Reserved1;   // Byte  3  = must be 0
            public int  s32_DataLen;    // Byte 4-7 = sent or requested data length, not including header and padding
            public byte u8_Attributes;  // Byte  8  = meaning depends on e_MsgID
            public byte u8_TermChar;    // Byte  9  = optional termination character (not used)
            public byte u8_Reserved2;   // Byte 10  = must be 0
            public byte u8_Reserved3;   // Byte 11  = must be 0
        }
       
        #endregion

        #region ScpiCombo

        /// <summary>
        /// This class is stored in ComboBox.Items of the combobox that shows the connected USB devices
        /// </summary>
        public class ScpiCombo
        {
            // Windows: Microsoft's unique device string from the registry (mostly this is the serial number of the USB device)
            // Linux:   Any string that uniquely identifies the oscilloscope among all connected SCPI devices
            public String ms_Display;    
            
            // Windows: "\\?\USB#VID_1AB1&PID_04CE#DS1ZC204807063#{A9FDBB24-128A-11D5-9961-00108335E361}"
            // Linux:   "/dev/usbtmc0"
            public String ms_DevicePath;

            public ScpiCombo(String s_Display, String s_DevicePath)
            {
                ms_Display    = s_Display;
                ms_DevicePath = s_DevicePath;
            }

            public override string ToString()
            {
                return ms_Display;
            }
        }

        #endregion

        // 128 byte buffer is sufficient for all SCPI commands that return an ASCII response 
        const int BUF_SIZE_ASCII = 128;

        // The default timeout for TCP connection is 20 seconds which is much too long.
        const int TCP_CONNECT_TIMEOUT = 1500;
        const int WSAETIMEDOUT        = 10060; // SocketException
        const int SIZE_OF_TMC_HEADER  = 12;    // Expected size of kTmcHeader (if compiled correctly) Never change this.
        const int DEFAULT_TIMEOUT     = 1000;  // milliseconds (This is far more than enough. Usb High-Speed runs at 480 MBit/s)

        eConnectMode  me_Mode;
        Byte          mu8_Tag; 
        int           ms32_OpcReplaceDelay;
        IntPtr        mp_HeaderMem;
        IUsbDevice    mi_UsbDevice;
        Socket        mi_TcpSocket;
        VxiClient     mi_VxiClient;
        SerialPort    mi_SerialPort;

        /// <summary>
        /// A delay which replaces the *OPC? command.
        /// For devices that do not support the command *OPC? it is required to make a fix delay which gives the device
        /// the required time to process the command. If you set OpcReplaceDelay = 100 and then call SendOpcCommand()
        /// the command will be sent and after a fix delay of 100 ms the funtion returns without
        /// sending the command *OPC? and ignoring the timeout passed to SendOpcCommand().
        /// If you set OpcReplaceDelay = 0 this delay is turned off and the command *OPC? will be sent instead.
        /// </summary>
        public int OpcReplaceDelay
        {
            set { ms32_OpcReplaceDelay = value; }
        }

        // =============================================================================================

        /// <summary>
        /// i_Combo comes from EnumerateScpiDevices()
        /// </summary>
        public void ConnectUsb(ScpiCombo i_Combo)
        {
            #if TRACE_OUTPUT
                Debug.Print("Open USB device \"" + i_Combo + "\"");
            #endif

            Debug.Assert(Marshal.SizeOf(typeof(kTmcHeader)) == SIZE_OF_TMC_HEADER, "struct compilation error");

            me_Mode      = eConnectMode.USB;
            mi_UsbDevice = PlatformManager.Instance.OpenUsbDevice(i_Combo);
            mp_HeaderMem = Marshal.AllocHGlobal(SIZE_OF_TMC_HEADER);
        }

        /// <summary>
        /// s_Endpoint   = "192.168.0.240 : 618" --> Connect directly to the control port 618.
        /// s_Endpoint   = "192.168.0.240"       --> Request the control port from the portmapper and connect to it.
        /// s_DeviceName = "inst0" for Rigol     ATTENTION: CASE SENSITIVE!
        /// </summary>
        public void ConnectVxi(String s_Endpoint, String s_DeviceName)
        {
            #if TRACE_OUTPUT
                Debug.Print("Open VXI connection to " + s_Endpoint);
            #endif

            UInt16 u16_Port; // Port is allowed to be 0 here
            IPAddress i_IpAddr = ParseEndpoint(s_Endpoint, out u16_Port);

            me_Mode      = eConnectMode.VXI;
            mi_VxiClient = new VxiClient();
            mi_VxiClient.ConnectDevice(i_IpAddr, u16_Port);

            mi_VxiClient.CreateLink(s_DeviceName);
        }

        public void ConnectTcp(String s_Endpoint)
        {
            #if TRACE_OUTPUT
                Debug.Print("Open TCP connection to " + s_Endpoint);
            #endif

            UInt16 u16_TcpPort;
            IPAddress i_IpAddress = ParseEndpoint(s_Endpoint, out u16_TcpPort);

            if (u16_TcpPort == 0)
                Throw("Enter IP address and port separated by colon like: \"192.168.0.240 : 1234\"");

            me_Mode      = eConnectMode.TCP;
            mi_TcpSocket = ConnectTcpSocketAsync(i_IpAddress, u16_TcpPort);
        }

        /// <summary>
        /// Open a COM port (RS232 or USB virtual COM port)
        /// s_PortName = "COM3" on Windows or "/dev/ttyUSB0" on Linux
        /// s_Settg = "8N1 RTS", "8N2 None", etc...
        /// </summary>
        public void ConnectRS232(String s_PortName, String s_Baudrate, String s_Settg)
        {
            #if TRACE_OUTPUT
                Debug.Print("Open port " + s_PortName + " with " + s_Baudrate + " " + s_Settg);
            #endif

            if (s_PortName.Length == 0)
                Throw("Select the RS232 port");

            eRs232Settg e_Setting;
            if (!Enum.TryParse<eRs232Settg>("_" + s_Settg.Replace(' ', '_'), out e_Setting))
                Throw("Invalid RS232 Setting"); // this will never happen.

            // "8N1 RTS"
            int s32_DataBits  = ((int)e_Setting & 0xF000) >> 12; // 8
            int s32_Parity    = ((int)e_Setting & 0x0F00) >>  8; // Parity.None
            int s32_StopBits  = ((int)e_Setting & 0x00F0) >>  4; // StopBits.One
            int s32_Handshake = ((int)e_Setting & 0x000F);       // Handshake.RequestToSend

            SerialPort i_Port = new SerialPort();
            i_Port.PortName     = s_PortName;
            i_Port.DataBits     = s32_DataBits;
            i_Port.Parity       = (Parity)   s32_Parity;
            i_Port.StopBits     = (StopBits) s32_StopBits;
            i_Port.Handshake    = (Handshake)s32_Handshake;
            i_Port.WriteTimeout = 2000; // all commands are very short
            i_Port.DtrEnable    = true;

            // Without hardware handshake RTS must be set manually, otherwise the CTS input of the scope stays inactive.
            // With Handshake.RequestToSend the driver controls RTS and setting it would throw.
            if (i_Port.Handshake != Handshake.RequestToSend)
                i_Port.RtsEnable = true;

            i_Port.Open(); // throws if the port does not exist or is in use
            i_Port.DiscardInBuffer();
            i_Port.DiscardOutBuffer();

            me_Mode       = eConnectMode.RS232;
            mi_SerialPort = i_Port;
        }

        /// <summary>
        /// Parse enpoint string "192.168.0.240 : 618" into IP Address and port.
        /// If the port is missing or invalid --> return u16_Port = 0.
        /// If the IP Address is invalid --> throw exception
        /// </summary>
        IPAddress ParseEndpoint(String s_Endpoint, out UInt16 u16_Port)
        {
            u16_Port = 0;
            String[] s_Parts = s_Endpoint.Split(':');

            String s_IpAddress = s_Endpoint.Trim();
            if (s_Parts.Length == 2)
            {
                s_IpAddress =   s_Parts[0].Trim();
                UInt16.TryParse(s_Parts[1].Trim(), out u16_Port);
            }
            return IPAddress.Parse(s_IpAddress);
        }

        // =============================================================================================

        /// <summary>
        /// Finalizer (called on garbage collection)
        /// </summary>
        ~SCPI()
        {
            Dispose();
        }

        /// <summary>
        /// IMPORTANT: This must be called before opening again.
        /// </summary>
        public void Dispose()
        {
            if (mi_UsbDevice != null)
            {
                #if TRACE_OUTPUT
                    Debug.Print("Close USB device");
                #endif
                mi_UsbDevice.Dispose();
                mi_UsbDevice = null;
            }

            if (mi_TcpSocket != null)
            {
                #if TRACE_OUTPUT
                    Debug.Print("Close TCP connection");
                #endif
                mi_TcpSocket.Dispose();
                mi_TcpSocket = null;
            }

            if (mi_VxiClient != null)
            {
                #if TRACE_OUTPUT
                    Debug.Print("Close VXI connection");
                #endif
                mi_VxiClient.Dispose();
                mi_VxiClient = null;
            }

            if (mi_SerialPort != null)
            {
                #if TRACE_OUTPUT
                    Debug.Print("Close COM port");
                #endif
                try { mi_SerialPort.Dispose(); } // Dispose() calls Close()
                catch {} // a USB virtual COM port that was unplugged throws here
                mi_SerialPort = null;
            }

            if (mp_HeaderMem != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(mp_HeaderMem);
                mp_HeaderMem = IntPtr.Zero;
            }
        }

        // ================================ USB Communication ==========================================

        /// <summary>
        /// The command *OPC? queries if the last operation has finished. 
        /// This can be used for all commands that do not return data, for example ":RUN"
        /// 
        /// The Rigol documentation says the scope returns "0" or "1".
        /// But the Rigol oscilloscopes do not even comply their own documentation.
        /// If the command ":AUTOSCALE" is still busy it never responds with "0".
        /// It sends nothing until the command has finished calibrating the scope and then it responds "1", after 6 seconds!
        /// A very long timeout is needed. Don't forget to show the wait cursor.
        /// 
        /// If OpcReplaceDelay has been set for devices that do not support the command *OPC?, a fix pause
        /// of OpcReplaceDelay milliseconds will be made instead of sending the command *OPC? and s32_Timeout is ignored.
        /// </summary>
        public void SendOpcCommand(String s_Command, int s32_Timeout = 10000)
        {
            TransmitString(s_Command);

            if (ms32_OpcReplaceDelay > 0)
            {
                #if TRACE_OUTPUT
                    Debug.Print("Replace *OPC? with delay of "+ms32_OpcReplaceDelay+ " ms");
                #endif

                Thread.Sleep(ms32_OpcReplaceDelay);
                return;
            }

            Stopwatch i_Watch = new Stopwatch();
            i_Watch.Start();

            while (true)
            {
                if (i_Watch.ElapsedMilliseconds > s32_Timeout)
                {
                    // ATTENTION:
                    // This is EXTREMELY important! If it is missing you get TIMEOUT's again and again
                    if (mi_UsbDevice != null)
                        mi_UsbDevice.CancelTransfer();

                    Throw("The SCPI command did not execute within the timeout.", true);
                }

                String s_Status = SendStringCommand("*OPC?", s32_Timeout);
                if (s_Status == "1")
                    break;

                Thread.Sleep(200); // in case a "0" is received.
            }
        }

        // ----------------------------------------

        /// <summary>
        /// Send an ASCII command that does not return a response, and do not wait for the command to finish.
        /// </summary>
        public void SendVoidCommand(String s_Command, int s32_Timeout = DEFAULT_TIMEOUT)
        {
            TransmitString(s_Command, s32_Timeout);
        }

        /// <summary>
        /// Send an ASCII command and return a double
        /// </summary>
        public double SendDoubleCommand(String s_Command, int s32_Timeout = DEFAULT_TIMEOUT)
        {
            String s_Float = SendStringCommand(s_Command, s32_Timeout);

            // On a German Windows doubles and floats use comma instead of dot!
            double d_Value;
            if (!Double.TryParse(s_Float, NumberStyles.Float, CultureInfo.InvariantCulture, out d_Value))
                Throw("The oscilloscope has returned an invalid floating point value: " + s_Float);

            return d_Value;
        }

        /// <summary>
        /// Send an ASCII command and return a bool
        /// </summary>
        public bool SendBoolCommand(String s_Command, int s32_Timeout = DEFAULT_TIMEOUT)
        {
            String s_Status = SendStringCommand(s_Command, s32_Timeout);
            return s_Status == "1" || s_Status == "ON";
        }

        /// <summary>
        /// Send an ASCII command and return a string
        /// </summary>
        public String SendStringCommand(String s_Command, int s32_Timeout = DEFAULT_TIMEOUT)
        {
            TransmitString(s_Command, s32_Timeout);
            String s_Response = ReceiveString(s32_Timeout);

            // Debug.Print("'{0}' --> '{1}'", s_Command, s_Response);
            return s_Response;
        }

        /// <summary>
        /// Send an ASCII command and return a byte array.
        /// ATTENTION: If s32_BlockSize is too small to receive the ENTIRE response, the USB communication will crash.
        /// s32_Timeout = timeout used for sending and for receiving one chunk
        /// </summary>
        public Byte[] SendByteCommand(eBinaryTCP e_BinaryTcp, int s32_BlockSize, String s_Command, 
                                      Progress i_Progress, int s32_Timeout = DEFAULT_TIMEOUT)
        {
            TransmitString(s_Command, s32_Timeout);

            #if TRACE_OUTPUT
                Debug.Print(">> SendByteCommand() timeout= "+s32_Timeout);
            #endif

            Byte[] u8_Data = null;
            switch (me_Mode)
            {
                case eConnectMode.VXI:   u8_Data = mi_VxiClient.DeviceRead(               i_Progress, s32_Timeout); break;
                case eConnectMode.USB:   u8_Data = ReceiveUsb(s32_BlockSize,              i_Progress, s32_Timeout); break;
                case eConnectMode.TCP:   u8_Data = ReceiveTcp(e_BinaryTcp, s32_BlockSize, i_Progress, s32_Timeout); break;
                case eConnectMode.RS232: Debug.Assert(false, "Programming Error: Use SendBlockCommand() for RS232."); break;
            }

            #if TRACE_OUTPUT
                Debug.Print("<< SendByteCommand() response= {0:N0} byte", u8_Data.Length);
            #endif
            return u8_Data;
        }

        /// <summary>
        /// Send an ASCII command that returns an IEEE 488.2 definite length block "#<n><length><data>" + linefeed
        /// and return only the payload <data> without header and linefeed.
        /// Over a stream connection (RS232, TCP) exactly the announced count of bytes is read,
        /// so the data may contain any byte values including linefeeds.
        /// The indefinite block "#0<data>\n" is also supported, but then the data must not contain a linefeed.
        /// s32_MaxSize = maximum expected size of the entire response (only used for USB)
        /// s32_Timeout = timeout for the first byte and for each following chunk of data
        /// i_Progress throws AbortException if the user has aborted.
        /// </summary>
        public Byte[] SendBlockCommand(String s_Command, int s32_MaxSize, int s32_Timeout, Progress i_Progress)
        {
            if (me_Mode == eConnectMode.USB || me_Mode == eConnectMode.VXI)
            {
                // USB and VXI deliver the entire message at once
                Byte[] u8_Response = SendByteCommand(eBinaryTCP.MinSize, s32_MaxSize, s_Command, i_Progress, s32_Timeout);
                return ExtractBlock(u8_Response);
            }

            TransmitString(s_Command, s32_Timeout);

            #if TRACE_OUTPUT
                Debug.Print(">> SendBlockCommand() timeout= "+s32_Timeout);
            #endif

            // Skip whitespace that some devices send before the block
            Byte u8_Char;
            do
            {
                u8_Char = ReceiveSingleByte(s32_Timeout);
            }
            while (Char.IsWhiteSpace((Char)u8_Char));

            if (u8_Char != '#')
            {
                // The device has sent an ASCII response instead of a block
                String s_Text = ((Char)u8_Char) + ReadAsciiLine(s32_Timeout);
                Throw("The oscilloscope has not sent a data block but: \"" + s_Text + "\"");
            }

            Byte u8_Digits = ReceiveSingleByte(s32_Timeout);
            if (u8_Digits < '0' || u8_Digits > '9')
                Throw("The oscilloscope has sent an invalid data block header.");

            if (u8_Digits == '0') // indefinite length block terminated by linefeed
                return Encoding.ASCII.GetBytes(ReadAsciiLine(s32_Timeout));

            Byte[] u8_Data;
            try
            {
                Byte[] u8_Length = ReceiveFixLength(u8_Digits - '0', s32_Timeout, null);

                int s32_Length;
                if (!int.TryParse(Encoding.ASCII.GetString(u8_Length), out s32_Length) || s32_Length < 0)
                    Throw("The oscilloscope has sent an invalid data block length.");

                u8_Data = ReceiveFixLength(s32_Length, s32_Timeout, i_Progress);
            }
            catch (AbortException Ex)
            {
                // User abort: the rest of the block is still arriving. Discard it, otherwise the next response is corrupt.
                DiscardInput();
                throw Ex;
            }

            // Remove the linefeed that terminates the response.
            // Wait only a short time because some devices do not send it.
            try
            {
                if (ReceiveSingleByte(200) == '\r')
                    ReceiveSingleByte(200);
            }
            catch (TimeoutException) {}

            #if TRACE_OUTPUT
                Debug.Print("<< SendBlockCommand() response= {0:N0} byte", u8_Data.Length);
            #endif
            return u8_Data;
        }

        /// <summary>
        /// Removes the IEEE 488.2 block header "#<n><length>" and the trailing linefeed from a complete response
        /// </summary>
        Byte[] ExtractBlock(Byte[] u8_Response)
        {
            if (u8_Response.Length < 2 || u8_Response[0] != '#' || u8_Response[1] < '0' || u8_Response[1] > '9')
                Throw("The oscilloscope has not sent a data block.");

            int s32_Digits = u8_Response[1] - '0';
            int s32_Start  = 2 + s32_Digits;
            int s32_Length = 0;
            if (s32_Digits == 0) // indefinite length block
            {
                s32_Length = u8_Response.Length - s32_Start;
                while (s32_Length > 0 && (u8_Response[s32_Start + s32_Length - 1] == '\n' ||
                                          u8_Response[s32_Start + s32_Length - 1] == '\r'))
                {
                    s32_Length --;
                }
            }
            else
            {
                if (u8_Response.Length < s32_Start ||
                    !int.TryParse(Encoding.ASCII.GetString(u8_Response, 2, s32_Digits), out s32_Length))
                    Throw("The oscilloscope has sent an invalid data block header.");

                if (u8_Response.Length < s32_Start + s32_Length)
                    Throw("The oscilloscope has sent an incomplete data block.");
            }

            Byte[] u8_Data = new Byte[s32_Length];
            Array.Copy(u8_Response, s32_Start, u8_Data, 0, s32_Length);
            return u8_Data;
        }

        /// <summary>
        /// Discards any data that the device may still be sending on a stream connection (RS232, TCP)
        /// after an aborted transfer or a timeout. Returns when nothing was received for s32_Timeout ms.
        /// </summary>
        public void DiscardInput(int s32_Timeout = 300)
        {
            Byte[] u8_Dummy = new Byte[4096];
            try
            {
                while (true)
                {
                    switch (me_Mode)
                    {
                        case eConnectMode.RS232:
                            mi_SerialPort.ReadTimeout = s32_Timeout;
                            mi_SerialPort.Read(u8_Dummy, 0, u8_Dummy.Length); // throws TimeoutException
                            break;
                        case eConnectMode.TCP:
                            mi_TcpSocket.ReceiveTimeout = s32_Timeout;
                            if (mi_TcpSocket.Receive(u8_Dummy) == 0) // throws SocketException on timeout
                                return;
                            break;
                        default:
                            return;
                    }
                }
            }
            catch {}
        }

        // ==================================== PRIVATE =====================================

        private void TransmitString(String s_Command, int s32_Timeout = DEFAULT_TIMEOUT)
        {
            #if TRACE_OUTPUT
                if (s_Command != "*OPC?") Debug.Print("-------------------------------");
                Debug.Print(">> TransmitString() command= \"" + s_Command + "\"");
            #endif

            Byte[] u8_TxCommand = Encoding.ASCII.GetBytes(s_Command + '\n');
            switch (me_Mode)
            {
                case eConnectMode.VXI: mi_VxiClient.DeviceWrite(u8_TxCommand); break;
                case eConnectMode.USB: SendUsbPacket    (u8_TxCommand, 0, s32_Timeout); break;
                case eConnectMode.TCP: mi_TcpSocket.Send(u8_TxCommand, 0, u8_TxCommand.Length, SocketFlags.None); break;
                case eConnectMode.RS232:
                    // SCPI is strictly request / response. If the device has responded after a previous timeout,
                    // this late response must be removed, otherwise all following responses would be shifted.
                    mi_SerialPort.DiscardInBuffer();
                    try
                    {
                        mi_SerialPort.Write(u8_TxCommand, 0, u8_TxCommand.Length);
                    }
                    catch (TimeoutException)
                    {
                        // With RTS/CTS handshake the write blocks while the CTS line is not active
                        Throw("Timeout sending to the COM port.\nThe oscilloscope does not activate the CTS line.\n"
                            + "Is it turned on and are the cable and the handshake setting correct?", true);
                    }
                    break;
            }
        
            #if TRACE_OUTPUT
                Debug.Print("<< TransmitString() finished");
            #endif
        }

        private String ReceiveString(int s32_Timeout = DEFAULT_TIMEOUT)
        {
            #if TRACE_OUTPUT
                Debug.Print(">> ReceiveString() timeout= "+s32_Timeout);
            #endif

            Byte[] u8_RxData = null;
            switch (me_Mode)
            {
                case eConnectMode.VXI:   u8_RxData = mi_VxiClient.DeviceRead(           null, s32_Timeout); break;
                case eConnectMode.USB:   u8_RxData = ReceiveUsb(BUF_SIZE_ASCII,         null, s32_Timeout); break;
                case eConnectMode.TCP:   u8_RxData = ReceiveTcp(eBinaryTCP.Linefeed, 0, null, s32_Timeout); break;
                case eConnectMode.RS232: u8_RxData = Encoding.ASCII.GetBytes(ReadAsciiLine(s32_Timeout));   break;
            }
            String s_Response = Encoding.ASCII.GetString(u8_RxData);

            // ATTENTION: There may be garbage behind the response: "1.000000e+09\nO"
            int s32_LF = s_Response.IndexOf('\n');
            if (s32_LF > 0)
                s_Response = s_Response.Substring(0, s32_LF);

            #if TRACE_OUTPUT
                Debug.Print("<< ReceiveString() response= \"" + s_Response + "\"");
            #endif
            return s_Response;
        }

        // ================================== USB ===================================

        /// <summary>
        /// ATTENTION: If s32_MaxRxData is too small to receive the entire response, the USB communication will crash.
        /// s32_Timeout is for a chunk of data received in one TMC frame.
        /// USB High-Speed bulk data transfer is extremely fast: approx 100 kB in less than 20 ms.
        /// </summary>
        private Byte[] ReceiveUsb(int s32_MaxRxData, Progress i_Progress, int s32_Timeout = DEFAULT_TIMEOUT)
        {
            Byte[] u8_RxBuffer = new Byte[s32_MaxRxData + SIZE_OF_TMC_HEADER];

            MemoryStream i_Stream = new MemoryStream();

            // This loop runs until the device sets the EndOfMessage bit.
            // When requesting 100.000 bytes WaveForm data, Rigol sends one respone packet of 512 byte,
            // then another TMC header with 99524 bytes which are received in one single block.
            while (true)
            {
                // Request response from device
                SendUsbPacket(null, s32_MaxRxData, s32_Timeout);

                #if TRACE_OUTPUT
                    Debug.Print("  >> Receive() requesting " + s32_MaxRxData + " data bytes ...");
                #endif

                // The IVI driver does not allow to call Receive() first to request only the TMC header and then again to read the data.
                // This is no problem with other drivers, but you will screw up the entire communication when you try this here.
                int s32_BytesRead = mi_UsbDevice.Receive(u8_RxBuffer, s32_Timeout);
                if (s32_BytesRead < SIZE_OF_TMC_HEADER)
                {
                    Throw("The USB device has sent a crippled response of " + s32_BytesRead + " bytes.\n"
                        + "This may happen if the receive buffer is too small.");
                }
                // Copy the first bytes of u8_RxBuffer into k_Header
                Marshal.Copy(u8_RxBuffer, 0, mp_HeaderMem, SIZE_OF_TMC_HEADER);
                kTmcHeader k_Header = (kTmcHeader)Marshal.PtrToStructure(mp_HeaderMem, typeof(kTmcHeader));

                // The TMC USB specification says clearly that the reponse must meet the following criteria otherwise it is invalid.
                // This is the only way to check in the primitve TMC protocol if valid data was received as there are no checksums or other methods.
                // If any bytes remained in the driver's receive buffer from a previous aborted transfer, this is the only way to detect this.
                // If any sloppy devices like Keysight multimeters do not set the Tag and so do not comply with these minimum requirements, 
                // they are buggy crap and cannot be used with this class. Demand a firmware update from the vendor.
                // Even such a sloppy company as Rigol sets the Tag correctly.
                if (k_Header.e_MsgID       != eTmcMsgId.REQUEST_DEV_DEP_MSG_IN ||
                    k_Header.u8_Tag        != mu8_Tag                          ||
                    k_Header.u8_TagInverse != (Byte)(~mu8_Tag)                 ||
                    k_Header.s32_DataLen   > s32_MaxRxData)
                {
                    Throw("The USB device has sent an invalid response header");
                }

                i_Stream.Write(u8_RxBuffer, SIZE_OF_TMC_HEADER, s32_BytesRead - SIZE_OF_TMC_HEADER);

                if (i_Progress != null)
                    i_Progress.PrintProgress((int)i_Stream.Length);

                bool b_EndOfMsg = (k_Header.u8_Attributes & 1) > 0;
                #if TRACE_OUTPUT
                    Debug.Print("  << Receive() response= " + s32_BytesRead + " bytes, EndOfMsg= " + b_EndOfMsg);
                #endif

                if (b_EndOfMsg)
                    break;
            }
            return i_Stream.ToArray();
        }

        // ----------------------------------------

        /// <summary>
        /// ATTENTION: If s32_MaxRxData is too small to receive the entire response, the USB communication will crash.
        /// u8_TxCommand != null --> u8_TxCommand is appended to the 12 byte header and sent in one or multiple Bulk OUT packets of 512 bytes
        /// u8_TxCommand == null --> A response is requested from the device in a Bulk IN transfer
        /// </summary>
		private void SendUsbPacket(Byte[] u8_TxCommand, int s32_MaxRxData, int s32_Timeout)
		{
            bool b_Command = u8_TxCommand != null;
            #if TRACE_OUTPUT
                if (b_Command) Debug.Print("  >> SendUsbPacket() sending command (TxData= " + u8_TxCommand.Length + " byte)");
                else           Debug.Print("  >> SendUsbPacket() requesting response (MaxRxData= " + s32_MaxRxData + " byte)");
            #endif

            // incremet counter, always > 0
            mu8_Tag = (Byte)Math.Max(1, mu8_Tag + 1);

            // Create the write header:
            kTmcHeader k_Header    = new kTmcHeader();
            k_Header.u8_Tag        = mu8_Tag;
            k_Header.u8_TagInverse = (Byte)~mu8_Tag;

            if  (b_Command) // Send Command
            {
                k_Header.e_MsgID       = eTmcMsgId.DEV_DEP_MSG_OUT; // (Device Dependent Command Message, sent on Bulk OUT)
                k_Header.s32_DataLen   = u8_TxCommand.Length; // length of command, not including header + padding
                k_Header.u8_Attributes = 1; // 1 = EOM = End Of Message --> all data is sent in one packet
            }
            else // Request IN transfer
            {
                k_Header.e_MsgID       = eTmcMsgId.REQUEST_DEV_DEP_MSG_IN; // (command sent on Bulk OUT requesting the device to send a response on Bulk-IN)
                k_Header.s32_DataLen   = s32_MaxRxData; // maximum data length for the requested Bulk IN packet
                k_Header.u8_Attributes = 0; // 0 = Device must ignore u8_TermChar
            }

            // Copy k_Header into u8_Header
            Byte[] u8_Header = new Byte[SIZE_OF_TMC_HEADER];
            Marshal.StructureToPtr(k_Header, mp_HeaderMem, false);
            Marshal.Copy(mp_HeaderMem, u8_Header, 0, SIZE_OF_TMC_HEADER);

            List<Byte> i_Transfer = new List<Byte>();
            i_Transfer.AddRange(u8_Header);            
            if (b_Command)
                i_Transfer.AddRange(u8_TxCommand);

            // align to 4 byte boundary
            while ((i_Transfer.Count & 0x3) > 0) 
            {
                i_Transfer.Add(0);
            }

            mi_UsbDevice.Send(i_Transfer.ToArray(), s32_Timeout);

            #if TRACE_OUTPUT
                Debug.Print("  << SendUsbPacket() finished");
            #endif
        }

        // ================================== TCP ===================================

        /// <summary>
        /// Microsoft forgot to implement a function Socket.Connect(int Timeout)
        /// The default connect timeout is 20 seconds which is much too long --> Use TCP_CONNECT_TIMEOUT instead.
        /// </summary>
        public static Socket ConnectTcpSocketAsync(IPAddress i_IpAddress, UInt16 u16_TcpPort)
        {
            Socket i_TcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            i_TcpSocket.ReceiveTimeout = 2000; // changed later
            i_TcpSocket.SendTimeout    = 1000; // all commands are very short (< 50 byte)

            ManualResetEvent     i_Event = new ManualResetEvent(false);
            SocketAsyncEventArgs i_Args  = new SocketAsyncEventArgs();
            i_Args.RemoteEndPoint = new IPEndPoint(i_IpAddress, u16_TcpPort);
            i_Args.SocketError    = SocketError.TimedOut;
            i_Args.Completed     += delegate(Object o_Sender, SocketAsyncEventArgs i_EvArgs)
            {
                i_Event.Set();
            };

            if (i_TcpSocket.ConnectAsync(i_Args) &&   // returns true if connection is pending
               !i_Event.WaitOne(TCP_CONNECT_TIMEOUT)) // returns false on timeout
                Throw("Could not connect to " + i_Args.RemoteEndPoint + "  (Timeout)");

            if (i_Args.SocketError != SocketError.Success)
                Throw("Could not connect to " + i_Args.RemoteEndPoint + "  (" + i_Args.SocketError + ")");

            return i_TcpSocket;
        }

        /// <summary>
        /// While for USB the struct kTmcHeader has a flag indicating the last packet,
        /// over TCP no such information is available and the linefeed at the end is the only way to detect the end.
        /// But the binary data may also contain multiple values of 0x0A within the data.
        /// The oscilloscope sends the data in many separate packets. Rigol sends packets of 5840 bytes.
        /// See comment of eBinaryTCP for more details.
        /// s32_MinSize is only used in mode eBinaryTCP.MinSize
        /// </summary>
        private Byte[] ReceiveTcp(eBinaryTCP e_BinaryTcp, int s32_MinSize, Progress i_Progress, int s32_Timeout)
        {
            #if TRACE_OUTPUT
                if (e_BinaryTcp == eBinaryTCP.MinSize) Debug.Print("  >> ReceiveTcp(MinSize = {0:N0} byte)", s32_MinSize);
                else                                   Debug.Print("  >> ReceiveTcp(Linefeed)");
            #endif

            MemoryStream i_Stream = new MemoryStream();
            Byte[] u8_Buffer = new Byte[0x8000];

            mi_TcpSocket.ReceiveTimeout = s32_Timeout;
            while (true)
            {
                // s32_RxCount will never be zero. A timeout exception is thrown in case nothing was received.  
                int s32_RxCount = 0;
                try
                {
                    s32_RxCount = mi_TcpSocket.Receive(u8_Buffer, u8_Buffer.Length, SocketFlags.None);
                }
                catch (SocketException Ex)
                {
                    // In case of Timeout the SocketException must be converted into a TimeoutException. 
                    if (Ex.ErrorCode == WSAETIMEDOUT)
                        Throw("Timeout. No response from the oscilloscope.\n"
                            + "Some oscilloscopes do not respond over TCP if the USB cable is also plugged in!\n"
                            + "If so, disconnect USB and reboot the oscilloscope.", true);

                    #if TRACE_OUTPUT
                        Debug.Print("*** " + Ex.Message);
                    #endif
                    throw Ex;
                }

                #if TRACE_OUTPUT
                    Debug.Print("     Rx block of {0} byte, Total Rx = {1:N0} byte", s32_RxCount, i_Stream.Length + s32_RxCount);
                #endif
                   
                if (e_BinaryTcp == eBinaryTCP.Linefeed || 
                   (e_BinaryTcp == eBinaryTCP.MinSize && i_Stream.Length + s32_RxCount >= s32_MinSize))
                {
                    // Abort when the block ends with linefeed.
                    // ATTENTION: There may be padding bytes behind the linefeed.
                    // The linefeed is not necessarily the last byte --> search the last 4 bytes for a linefeed.
                    int s32_LF = Utils.FindByteReverse(u8_Buffer, s32_RxCount, 4, 0x0A);
                    if (s32_LF > -1)
                    {
                        // Append all received bytes except the linefeed (and padding) at the end
                        i_Stream.Write(u8_Buffer, 0, s32_LF);
                        break;
                    }
                }

                // Append the entire block, more data will follow.
                i_Stream.Write(u8_Buffer, 0, s32_RxCount);

                if (i_Progress != null)
                    i_Progress.PrintProgress((int)i_Stream.Length);
            }

            #if TRACE_OUTPUT
                Debug.Print("  << ReceiveTcp() --> received {0:N0} bytes", i_Stream.Length);
            #endif
            return i_Stream.ToArray();
        }

        // ============================= RS232 + TCP Stream ==============================

        /// <summary>
        /// Sends raw bytes without appending a linefeed. For devices with a proprietary (non SCPI) protocol like Hameg HM507.
        /// b_ByteByByte = true --> send each byte separately and wait until it has been sent.
        /// This is required for old devices with a small receive buffer that cannot handle the 16 byte FIFO of the PC's UART.
        /// Only for RS232 and TCP.
        /// </summary>
        public void SendRawCommand(Byte[] u8_Command, bool b_ByteByByte, int s32_Timeout)
        {
            #if TRACE_OUTPUT
                Debug.Print(">> SendRawCommand() " + BitConverter.ToString(u8_Command));
            #endif

            switch (me_Mode)
            {
                case eConnectMode.RS232:
                    // Remove a late response that has arrived after a previous timeout
                    mi_SerialPort.DiscardInBuffer();
                    mi_SerialPort.WriteTimeout = s32_Timeout;
                    try
                    {
                        if (!b_ByteByByte)
                        {
                            mi_SerialPort.Write(u8_Command, 0, u8_Command.Length);
                            break;
                        }

                        Stopwatch i_Watch = Stopwatch.StartNew();
                        for (int i=0; i<u8_Command.Length; i++)
                        {
                            mi_SerialPort.Write(u8_Command, i, 1);
                            while (mi_SerialPort.BytesToWrite > 0) // waits for CTS if RTS/CTS handshake is used
                            {
                                if (i_Watch.ElapsedMilliseconds > s32_Timeout)
                                    Throw("Timeout", true);

                                Thread.Sleep(0);
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        mi_SerialPort.DiscardOutBuffer();
                        Throw("Timeout sending to the COM port.\nThe oscilloscope does not activate the CTS line.\n"
                            + "Is it turned on and are the cable and the handshake setting correct?", true);
                    }
                    break;

                case eConnectMode.TCP:
                    mi_TcpSocket.Send(u8_Command, 0, u8_Command.Length, SocketFlags.None);
                    break;

                default:
                    Throw("This oscilloscope can only be connected over RS232 (or a RS232 to Ethernet converter over TCP).");
                    break;
            }
        }

        /// <summary>
        /// Receives exactly s32_Count raw bytes. Only for RS232 and TCP.
        /// s32_Timeout is the maximum time to wait for the next chunk of data, not for the entire data.
        /// b_Progress = true --> call the BlockProgress callback which allows the user to abort.
        /// Returns null if the user has aborted. Throws TimeoutException on timeout.
        /// i_Progress throws AbortException if the user has aborted.
        /// </summary>
        public Byte[] ReceiveRawResponse(int s32_Count, int s32_Timeout, Progress i_Progress)
        {
            try
            {
                Byte[] u8_Data = ReceiveFixLength(s32_Count, s32_Timeout, i_Progress);
                #if TRACE_OUTPUT
                    if (s32_Count <= 32) Debug.Print("<< ReceiveRawResponse() " + BitConverter.ToString(u8_Data));
                    else                 Debug.Print("<< ReceiveRawResponse() {0:N0} bytes", s32_Count);
                #endif
                return u8_Data;
            }
            catch (AbortException Ex)
            {
                DiscardInput();
                throw Ex;
            }
        }

        /// <summary>
        /// Receives one raw byte. Only for RS232 and TCP. Throws TimeoutException on timeout.
        /// </summary>
        public Byte ReceiveSingleByte(int s32_Timeout)
        {
            Byte[] u8_Byte = ReceiveFixLength(1, s32_Timeout, null);
            return u8_Byte[0];
        }

        /// <summary>
        /// Reads until linefeed. The linefeed and a preceding carriage return are not returned.
        /// </summary>
        private String ReadAsciiLine(int s32_Timeout)
        {
            StringBuilder i_Line = new StringBuilder();
            while (true)
            {
                Char c_Char = (Char)ReceiveSingleByte(s32_Timeout);
                if (c_Char == '\n')
                    break;
                i_Line.Append(c_Char);
            }
            if (i_Line.Length > 0 && i_Line[i_Line.Length - 1] == '\r')
                i_Line.Length --;

            #if TRACE_OUTPUT
                Debug.Print("  << ReadAsciiLine() --> \"" + i_Line + "\"");
            #endif
            return i_Line.ToString();
        }

        /// <summary>
        /// Reads exactly s32_Count bytes from the COM port or the TCP socket.
        /// s32_Timeout is the maximum time to wait for the next chunk of data, not for the entire data.
        /// b_Progress = true --> call the BlockProgress callback which allows the user to abort.
        /// i_Progress throws AbortException if the user has aborted.
        /// </summary>
        private Byte[] ReceiveFixLength(int s32_Count, int s32_Timeout, Progress i_Progress)
        {
            if (i_Progress != null)
                i_Progress.SetTotalBytes(s32_Count);

            Byte[] u8_Buffer = new Byte[s32_Count];
            int s32_Done = 0;
            while (s32_Done < s32_Count)
            {
                int s32_Read = 0;
                try
                {
                    switch (me_Mode)
                    {
                        case eConnectMode.RS232:
                            mi_SerialPort.ReadTimeout = s32_Timeout;
                            s32_Read = mi_SerialPort.Read(u8_Buffer, s32_Done, s32_Count - s32_Done);
                            break;
                        case eConnectMode.TCP:
                            mi_TcpSocket.ReceiveTimeout = s32_Timeout;
                            s32_Read = mi_TcpSocket.Receive(u8_Buffer, s32_Done, s32_Count - s32_Done, SocketFlags.None);
                            if (s32_Read == 0)
                                Throw("The oscilloscope has closed the TCP connection.");
                            break;
                        default:
                            Debug.Assert(false, "Programming Error: ReceiveFixLength() is only for RS232 and TCP");
                            break;
                    }
                }
                catch (TimeoutException)
                {
                    Throw("Timeout. No response from the oscilloscope.\nRead the Help file!", true);
                }
                catch (SocketException Ex)
                {
                    if (Ex.ErrorCode == WSAETIMEDOUT)
                        Throw("Timeout. No response from the oscilloscope.\nRead the Help file!", true);
                    throw;
                }
                s32_Done += s32_Read;

                if (i_Progress != null)
                    i_Progress.PrintProgress(s32_Done); // throws on user abort
            }
            return u8_Buffer;
        }

        // ================================== Helper ====================================

        /// <summary>
        /// The TimeoutException has a special treatment: 
        /// It is is handled in PanelRigol.SendManualCommand() when manually sending an invalid command.
        /// </summary>
        static void Throw(String s_Message, bool b_Timeout = false)
        {
            #if TRACE_OUTPUT
                Debug.Print("*** " + s_Message);
            #endif

            if (b_Timeout) throw new TimeoutException(s_Message);
            else           throw new Exception(s_Message);
        }
    }
}
