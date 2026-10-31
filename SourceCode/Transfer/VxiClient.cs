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

// Writes TCP communication to the debugger (or SysInternals DbgView)
#if DEBUG
//    #define TRACE_OUTPUT
#endif

// --------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using Progress          = Transfer.FormTransfer.Progress;

namespace Transfer
{
    /// <summary>
    /// Implementation of the VXI-11 protocol.
    /// https://www.scribd.com/document/217242183/VXI-11-spec
    /// </summary>
    public class VxiClient : IDisposable
    {
        #region enums

        enum eMesgType
        {
            Call  = 0,
            Reply = 1,
        }

        enum eProcedure
        {
            Device_Abort      =  1, // device aborts an in-progress call,   sent to abort channel
            Get_Port          =  3, // device reports its core port,        sent to portmapper channel
            Device_Intr_Srq   = 30, // device sends a service request,      sent to interrupt channel
            // --------------------------------------------
            // the following are sent to the core channel:
            Create_Link       = 10, // opens a link to a device
            Device_Write      = 11, // send command to device
            Device_Read       = 12, // device sends reply
            Device_Read_Stb   = 13, // device sends its status byte
            Device_Trigger    = 14, // device executes a trigger
            Device_Clear      = 15, // device clears itself
            Device_Remote     = 16, // device disables its front panel
            Device_Local      = 17, // device enables its front panel
            Device_Lock       = 18, // lock device
            Device_Unlock     = 19, // unlock device
            Device_Enable_Srq = 20, // device enables/disables sending of service requests
            Device_Do_Cmd     = 22, // device executes a command that is defined in eCommand
            Destroy_Link      = 23, // closes a link to the device
            Create_Intr_Chan  = 25, // device creates  interrupt channel
            Destroy_Intr_Chan = 26, // device destroys interrupt channel
        }

        enum eCommand
        {
            Send_Command = 0x020000,
            Bus_Status   = 0x020001,
            ATN_Ctrl     = 0x020002,
            REN_Ctrl     = 0x020003,
            PASS_Ctrl    = 0x020004,
            Bus_Address  = 0x02000A,
            IFC_Ctrl     = 0x020010, // Interface Clear (reset GPIB bus devices)
        }

        enum eReplyState
        {
            Accepted = 0,
            Denied,
        }

        enum eAcceptState
        {
            Success = 0,
            Program_unavailable,
            Program_version_mismatch,
            Procedure_unavailable,
            Invalid_arguments,
            System_error,
        }

        enum eVxiError
        {
            Success                       =  0,
            Syntax_Error                  =  1,
            Device_not_accessible         =  3,
            Invalid_link_identifier       =  4,
            Parameter_error               =  5,
            Channel_not_established       =  6,
            Operation_not_supported       =  8,
            Out_of_resources              =  9,
            Device_locked_by_another_link = 11,
            No_lock_held_by_this_link     = 12,
            IO_Timeout                    = 15,
            IO_Error                      = 17,
            Invalid_address               = 21,
            Aborted                       = 23, // Device_Abort has been sent to the Abort channel
            Channel_already_established   = 29,
        }

        [FlagsAttribute]
        enum eWriteFlags
        {
            None        = 0,
            WaitLocked  = 0x01, // Wait until locked
            End         = 0x08, // End of data
            TermCharSet = 0x80, // Termination character set
        }

        [FlagsAttribute]
        enum eReadFlags
        {
            None      = 0,    // The return buffer is full --> no flag shall be set
            RequCount = 0x01, // Requested char count reached
            TermChar  = 0x02, // Termination character has been transferred
            End       = 0x04, // End of data
        }

        #endregion

        #region ICmdParam

        interface ICmdParam
        {
            int GetTxByteCount();
        }

        #endregion

        #region VxiStream

        /// <summary>
        /// A big-endian MemoryStream
        /// </summary>
        class VxiStream : MemoryStream
        {
            public VxiStream()
            {
            }

            /// <summary>
            /// Write a big-endian integer to the stream
            /// s_Debug = "Header"
            /// </summary>
            public void WriteInt32(int s32_Value, String s_Debug)
            {
                #if TRACE_OUTPUT
                    TraceLine("Write", 4, s_Debug, s32_Value.ToString("X8"));
                #endif

                WriteByte((Byte)(s32_Value >> 24));
                WriteByte((Byte)(s32_Value >> 16));
                WriteByte((Byte)(s32_Value >>  8));
                WriteByte((Byte)(s32_Value));
            }

            /// <summary>
            /// Read a big-endian integer from the stream
            /// s_Debug = "Port", "ErrorFlags"
            /// </summary>
            public int ReadInt32(String s_Debug)
            {
                int s32_Value = 0;
                for (int i=0; i<4; i++)
                {
                    int s32_Byte = ReadByte();
                    if (s32_Byte < 0)
                        Throw("Insufficient bytes received");

                    s32_Value <<= 8;
                    s32_Value  |= s32_Byte;
                }

                #if TRACE_OUTPUT
                    TraceLine("Read", 4, s_Debug, s32_Value.ToString("X8"));
                #endif
                return s32_Value;
            }

            // ======================================================================================

            /// <summary>
            /// Write a structure or class to the stream
            /// ATTENTION: All integers in the struct must be big endian!
            /// </summary>
            public void WriteStruct(ICmdParam i_Param)
            {
                int    s32_Size = Marshal.SizeOf(i_Param.GetType());
                Byte[] u8_Bytes = new Byte[s32_Size];
                IntPtr p_Mem    = Marshal.AllocHGlobal(s32_Size);

                Marshal.StructureToPtr(i_Param, p_Mem, false);
                Marshal.Copy(p_Mem, u8_Bytes, 0, s32_Size);
                Marshal.FreeHGlobal(p_Mem);

                // If the class i_Param has an ASCII string, only the part of u8_Bytes is sent that has valid data.
                int s32_ByteCount = i_Param.GetTxByteCount();
                #if TRACE_OUTPUT
                    TraceLine("Write", s32_ByteCount, i_Param.GetType().Name, BytesToHex(u8_Bytes, s32_ByteCount));
                #endif
                Write(u8_Bytes, 0, s32_ByteCount);
            }

            /// <summary>
            /// Read a structure or class from the stream
            /// ATTENTION: All integers in the struct are big endian!
            /// </summary>
            public T ReadStruct<T>()
            {
                int s32_Size = Marshal.SizeOf(typeof(T));
                Byte[] u8_Bytes = new Byte[s32_Size];
                int    s32_Read = Read(u8_Bytes, 0, s32_Size);

                #if TRACE_OUTPUT
                    TraceLine("Read", s32_Size, typeof(T).Name, BytesToHex(u8_Bytes, s32_Read));

                    // In case of an error it may happen that the device sends an incomplete response 
                    // which contains only the first integer after the RPC struct: ms32_ErrorCode.
                    // The remaining members in the struct will be left zero.
                    if (s32_Read != s32_Size)
                        Debug.Print("   *** Received {0} instead of {1} bytes", s32_Read, s32_Size);
                #endif

                if (s32_Read < 4)
                    Throw("Empty response received");

                IntPtr p_Mem = Marshal.AllocHGlobal(s32_Size);
                Marshal.Copy(u8_Bytes, 0, p_Mem, s32_Read);
                T i_Struct = (T)Marshal.PtrToStructure(p_Mem, typeof(T));
                Marshal.FreeHGlobal(p_Mem);
                return i_Struct;
            }

            // ======================================================================================

            /// <summary>
            /// Read a specific count of bytes from the stream
            /// </summary>
            public Byte[] ReadData(int s32_ByteCount)
            {
                Byte[] u8_Data = new Byte[s32_ByteCount];
                int s32_Read = Read(u8_Data, 0, s32_ByteCount);
                
                #if TRACE_OUTPUT
                    TraceLine("Read", s32_ByteCount, "Data", BytesToAscii(u8_Data, s32_Read));
                #endif

                if (s32_Read != s32_ByteCount) Throw("Incomplete response from the device.");
                return u8_Data;
            }
        }

        #endregion

        #region cIntParam

        /// <summary>
        /// Used to send an integer to the instrument
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cIntParam : ICmdParam
        {
            public int ms32_Value;

            public cIntParam()
            {
            }

            public cIntParam(int s32_Value)
            {
                ms32_Value = Revert(s32_Value);
            }

            public int GetTxByteCount()
            {
                return 4;
            }
        }

        #endregion

        #region cDataParam

        /// <summary>
        /// Used to send variable-length byte arrays to the instrument (ASCII strings)
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cDataParam : ICmdParam
        {
            const int BUF_SIZE = 1024;

            public int ms32_DataLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst= BUF_SIZE)]
            public Byte[] mu8_Buffer = new Byte[BUF_SIZE];

            public int GetTxByteCount()
            {
                int s32_Chars = Revert(ms32_DataLength);
                s32_Chars = ((s32_Chars + 3) / 4) * 4; // always send multiple of 4 bytes
                return s32_Chars + 4;
            }

            public void StoreData(Byte[] u8_Data)
            {
                Debug.Assert(u8_Data.Length <= BUF_SIZE, "Programming Error: Max data length is "+BUF_SIZE+" byte");

                ms32_DataLength = Revert(u8_Data.Length);
                Array.Copy(u8_Data, mu8_Buffer, u8_Data.Length);
            }
        }

        #endregion

        #region cRmtPrcCallParam, cRmtPrcCallReply

        /// <summary>
        /// Sent as the first part of every Remote Procedure Call Request
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cRmtPrcCallParam : ICmdParam
        {
            public int ms32_MessageID;
            public int ms32_MessageType; // eMesgType
            public int ms32_RpcVersion;
            // ---------------------------
            public int ms32_Program;
            public int ms32_ProgramVersion;
            public int ms32_Procedure;   // eProcedure
            // ---------------------------
            public int ms32_AuthType;    // always zero
            public int ms32_AuthData;    // always zero
            public int ms32_VerificType; // always zero
            public int ms32_VerificData; // always zero

            public int GetTxByteCount()
            {
                return 40; // 10 integers
            }
        }

        /// <summary>
        /// Received as the first part of every Remote Procedure Call Reply
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cRmtPrcCallReply
        {
            public int ms32_MessageID;   // same as cRmtPrcCallParam.ms32_MessageID
            public int ms32_MessageType; // eMesgType
            // ---------------------------
            public int ms32_ReplyState;  // eReplyState
            public int ms32_VerificType; // always zero
            public int ms32_VerificData; // always zero
            public int ms32_AcceptState; // eAcceptState
        }

        #endregion

        #region cGetPortParam

        /// <summary>
        /// Sent as the second part of the request eProcedure.Get_Port
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cGetPortParam : ICmdParam
        {
            public int ms32_Program;
            public int ms32_ProgramVersion;
            public int ms32_Protocol;
            public int ms32_Port;        // always zero

            public cGetPortParam(int s32_Program, int s32_ProgramVersion, ProtocolType e_Protocol)
            {
                ms32_Program        = Revert(s32_Program);
                ms32_ProgramVersion = Revert(s32_ProgramVersion);
                ms32_Protocol       = Revert((int)e_Protocol);
            }

            public int GetTxByteCount()
            {
                return 16; // 4 integers
            }
        }

        #endregion

        #region cCreateLinkParam, cCreateLinkReply

        /// <summary>
        /// Sent as the second part of the request eProcedure.Create_Link
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cCreateLinkParam : ICmdParam
        {
            public int        ms32_ClientID;
            public int        ms32_LockDevice;  // bool (0 or 1)
            public int        ms32_LockTimeout;
            public cDataParam mi_DeviceName = new cDataParam();

            public cCreateLinkParam(int s32_ClientID, String s_DeviceName)
            {
                ms32_ClientID = Revert(s32_ClientID);
                mi_DeviceName.StoreData(Encoding.ASCII.GetBytes(s_DeviceName));
            }

            public int GetTxByteCount()
            {
                return 12 + mi_DeviceName.GetTxByteCount();
            }
        }

        /// <summary>
        /// Received as the second part of the reply to eProcedure.Create_Link
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cCreateLinkReply
        {
            public int ms32_ErrorCode;   // eVxiError
            public int ms32_LinkID;
            public int ms32_AbortPort;   // Rigol uses port 619, used for procedure Device_Abort
            public int ms32_MaxRecvSize;
        }

        #endregion

        #region cDeviceWriteParam, cDeviceWriteReply

        /// <summary>
        /// Sent as the second part of the request eProcedure.Device_Write
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cDeviceWriteParam : ICmdParam
        {
            public int        ms32_LinkID;
            public int        ms32_IoTimeout;
            public int        ms32_LockTimeout;
            public int        ms32_WriteFlags;   // eWriteFlags
            public cDataParam mi_Data = new cDataParam();

            public cDeviceWriteParam(int s32_LinkID, Byte[] u8_Data)
            {
                ms32_LinkID     = Revert(s32_LinkID);
                ms32_IoTimeout  = Revert(2000);
                ms32_WriteFlags = Revert((int)eWriteFlags.End); // SCPI commands are always terminated with End char "\n"
                mi_Data.StoreData(u8_Data);
            }

            public int GetTxByteCount()
            {
                return 16 + mi_Data.GetTxByteCount();
            }
        }

        /// <summary>
        /// Received as the second part of the reply to eProcedure.Device_Write
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cDeviceWriteReply
        {
            public int ms32_ErrorCode;   // eVxiError
            public int ms32_Size;
        }

        #endregion

        #region cDeviceReadParam, cDeviceReadReply

        /// <summary>
        /// Sent as the second part of the request eProcedure.Device_Read
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cDeviceReadParam : ICmdParam
        {
            public int ms32_LinkID;
            public int ms32_RequSize;       // bytes requested from device
            public int ms32_IoTimeout;
            public int ms32_LockTimeout;
            public int ms32_WriteFlags;     // eWriteFlags
            public int ms32_TermChar;       // valid if eWriteFlags.TermCharSet is set
            
            public cDeviceReadParam(int s32_LinkID)
            {
                ms32_LinkID    = Revert(s32_LinkID);
                ms32_RequSize  = Revert(100000000); // 100 MB = No buffer limit
                ms32_IoTimeout = Revert(2000);
                ms32_TermChar  = Revert('\n');
            }

            public int GetTxByteCount()
            {
                return 24;
            }
        }

        /// <summary>
        /// Received as the second part of the reply to eProcedure.Device_Read
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        class cDeviceReadReply
        {
            public int ms32_ErrorCode;   // eVxiError
            public int ms32_Reason;      // eReadFlags
            public int ms32_ByteCount;
            // followed by data
        }

        #endregion

        const int UDP_RESPONSE_TIMEOUT = 1500; // normally responses come in less than 15 ms
        const int PORT_MAPPER_PORT     = 111;
        const int RPC_VERSION          = 2;
        const int PORT_MAPPER_PROGRAM  = 100000;
        const int PORT_MAPPER_VERSION  = 2;
        const int DEVICE_CORE_PROGRAM  = 0x607AF;
        const int DEVICE_CORE_VERSION  = 1;
        const int FLAG_LAST_FRAGMENT   = int.MinValue; // 0x80000000
        const int WSAETIMEDOUT         = 10060;        // SocketException

        Socket    mi_Socket;
        VxiStream mi_RxStream    = new VxiStream();
        Byte[]    mu8_RxBuffer   = new Byte[0x8000];      // 32 kB is far more than the length of TCP packets
        int       ms32_ClientID  = 0x4F737A69;            // ASCII == "Oszi"
        int       ms32_MessageID = Environment.TickCount; // set a random start value, incremented with each message
        int       ms32_LinkID;                            // reply from procedure Create_Link, Rigol uses LinkID = 0
        int       ms32_MaxRecvSize;                       // reply from procedure Create_Link
        int       ms32_AbortPort;                         // reply from procedure Create_Link
        bool      mb_LinkCreated;                         // set in CreateLink()

        public void Dispose()
        {
            if (mi_Socket != null)
            {
                DestroyLink(); // does not throw

                mi_Socket.Dispose();
                mi_Socket = null;
            }
        }

        /// <summary>
        /// Send an UDP broadcast request to port 111 to find connected VXI devices.
        /// returns all IP Addresses that have responded.
        /// </summary>
        public void EnumerateVxiDevices(ComboBox i_ComboDevices, bool b_AddPort = false)
        {
            #if TRACE_OUTPUT
                Debug.Print("> FindDevices()");
            #endif

            if (mi_Socket != null)
            {
                Debug.Assert(false, "Programming Error: A connection is already established.");
                Dispose(); // Close connection
            }

            mi_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            mi_Socket.ReceiveTimeout  = UDP_RESPONSE_TIMEOUT; // UDP response delay is less than 15 ms
            mi_Socket.SendTimeout     = 1000;
            mi_Socket.EnableBroadcast = true;

            cGetPortParam i_Param = new cGetPortParam(DEVICE_CORE_PROGRAM, DEVICE_CORE_VERSION, ProtocolType.Tcp);
            Byte[] u8_TxPack = BuildRpcTxPacket(eProcedure.Get_Port, i_Param);

            IPEndPoint i_TxEndPoint = new IPEndPoint(IPAddress.Broadcast, PORT_MAPPER_PORT);
            mi_Socket.SendTo(u8_TxPack, i_TxEndPoint);

            i_ComboDevices.Items.Clear();
            try
            {
                while (true) // run until timeout exception is thrown
                {
                    EndPoint i_RxEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    int s32_Read = mi_Socket.ReceiveFrom(mu8_RxBuffer, ref i_RxEndPoint);
                    String s_IP  = ((IPEndPoint)i_RxEndPoint).Address.ToString();

                    mi_RxStream.SetLength(0);
                    mi_RxStream.Write(mu8_RxBuffer, 0, s32_Read);

                    String s_Error = CheckRpcRxPacket();
                    if (s_Error != null)
                    {
                        #if TRACE_OUTPUT
                            Debug.Print(s_Error);
                        #endif
                    }
                    else if (b_AddPort)
                    {
                        int s32_Port = mi_RxStream.ReadInt32("Port");
                        if (s32_Port > 0 && s32_Port < UInt16.MaxValue)
                            s_IP += " : " + s32_Port;
                    }

                    i_ComboDevices.Items.Add(s_IP);

                    #if TRACE_OUTPUT
                        Debug.Print("    Response from " + s_IP);
                    #endif
                }
            }
            catch (SocketException Ex)
            {
                // Throw any error except timeout
                if (Ex.ErrorCode != WSAETIMEDOUT)
                    throw Ex;
            }

            #if TRACE_OUTPUT
                Debug.Print("< FindDevices()");
            #endif

            Dispose();

            if (i_ComboDevices.Items.Count > 0)
                i_ComboDevices.SelectedIndex = 0;
        }

        /// <summary>
        /// IP = "192.168.0.240", Port = 618 --> Connect directly to the control port 618.
        /// IP = "192.168.0.240", Port = 0   --> Request the control port from the portmapper and connect to it.
        /// </summary>
        public void ConnectDevice(IPAddress i_IpAddress, UInt16 u16_TcpPort)
        {
            if (u16_TcpPort == 0 || u16_TcpPort == PORT_MAPPER_PORT)
            {
                AsyncConnect(i_IpAddress, PORT_MAPPER_PORT);
                u16_TcpPort = GetControlPort();
            }
            AsyncConnect(i_IpAddress, u16_TcpPort);
        }

        void AsyncConnect(IPAddress i_IpAddress, UInt16 u16_TcpPort)
        {
            #if TRACE_OUTPUT
                Debug.Print("> AsyncConnect({0} : {1})", i_IpAddress, s32_Port);
            #endif

            Dispose(); // disconnect if connected

            mi_Socket = SCPI.ConnectTcpSocketAsync(i_IpAddress, u16_TcpPort);

            #if TRACE_OUTPUT
                Debug.Print("< AsyncConnect()");
            #endif
        }

        // ====================================================================================================

        /// <summary>
        /// Get the VXI Control Port of the device by calling procedure Get_Port on the port mapper port 111.
        /// Rigol uses port 618.
        /// </summary>
        UInt16 GetControlPort()
        {
            #if TRACE_OUTPUT
                Debug.Print("> GetControlPort()");
            #endif

            cGetPortParam i_Param = new cGetPortParam(DEVICE_CORE_PROGRAM, DEVICE_CORE_VERSION, ProtocolType.Tcp);
            RpcCall(eProcedure.Get_Port, i_Param);

            int s32_Port = mi_RxStream.ReadInt32("Port");
            if (s32_Port <= 0 || s32_Port > UInt16.MaxValue)
                Throw("Received invalid control Port = zero");

            #if TRACE_OUTPUT
                Debug.Print("< GetControlPort() --> Port= {0}", s32_Port);
            #endif
            return (UInt16)s32_Port;
        }

        /// <summary>
        /// DeviceName = "gpib0", "inst0",... depends on the manufacturer.
        /// ATTENTION: Device name is CASE SENSITIVE!
        /// A link is a "connection" over TCP to a "device" inside the instrument. 
        /// There may be multiple "devices" inside one instrument, each having it's own name.
        /// </summary>
        public void CreateLink(String s_DeviceName)
        {
            #if TRACE_OUTPUT
                Debug.Print("> CreateLink(Device= '{0}')", s_DeviceName);
            #endif

            DestroyLink();

            cCreateLinkParam i_Param = new cCreateLinkParam(ms32_ClientID, s_DeviceName);
            RpcCall(eProcedure.Create_Link, i_Param);

            cCreateLinkReply i_Reply = mi_RxStream.ReadStruct<cCreateLinkReply>();
            eVxiError e_Error = (eVxiError)Revert(i_Reply.ms32_ErrorCode);
            if (e_Error != eVxiError.Success)
                Throw("Error connecting to VXI link '" + s_DeviceName + "':  " + e_Error.ToString().Replace('_', ' '));

            ms32_LinkID      = Revert(i_Reply.ms32_LinkID);      // Rigol sends Link ID = zero!
            ms32_MaxRecvSize = Revert(i_Reply.ms32_MaxRecvSize); // Rigol sends 1500 byte
            ms32_AbortPort   = Revert(i_Reply.ms32_AbortPort);   // Rigol sends port 619, used for procedure Device_Abort
            mb_LinkCreated   = true;

            #if TRACE_OUTPUT
                Debug.Print("< CreateLink() --> LinkID= {0}, MaxRecvSize= {1}, AbortPort= {2}", ms32_LinkID, ms32_MaxRecvSize, ms32_AbortPort);
            #endif
        }

        /// <summary>
        /// Does not throw
        /// </summary>
        void DestroyLink()
        {
            if (!mb_LinkCreated)
                return;

            #if TRACE_OUTPUT
                Debug.Print("> DestroyLink(LinkID= {0})", ms32_LinkID);
            #endif

            try
            {
                cIntParam i_Param = new cIntParam(ms32_LinkID); // Rigol uses LinkID = 0
                RpcCall(eProcedure.Destroy_Link, i_Param);

                #if TRACE_OUTPUT
                    // Rigol does not return an error if a link is destroyed that does not exist
                    eVxiError e_Error = (eVxiError)mi_RxStream.ReadInt32("ErrorFlags");
                    if (e_Error != eVxiError.Success)
                        Debug.Print("*** Error destroying link: " + e_Error.ToString().Replace('_', ' '));
                #endif
            }
            catch {}

            ms32_LinkID      = 0; 
            ms32_MaxRecvSize = 0;
            ms32_AbortPort   = 0;
            mb_LinkCreated   = false;

            #if TRACE_OUTPUT
                Debug.Print("< DestroyLink()");
            #endif
        }

        /// <summary>
        /// Send a data packet to the device (normally an ASCII string like "*IDN?\n")
        /// </summary>
        public void DeviceWrite(Byte[] u8_Data)
        {
            #if TRACE_OUTPUT
                Debug.Print("> DeviceWrite(Data= {0} byte, Ascii= {1})", u8_Data.Length, BytesToAscii(u8_Data, u8_Data.Length));
            #endif

            Debug.Assert(mb_LinkCreated,                     "Programming Error: You must first create a link");
            Debug.Assert(u8_Data.Length <= ms32_MaxRecvSize, "Programming Error: Device allows max data of "+ms32_MaxRecvSize+" byte");

            cDeviceWriteParam i_Param = new cDeviceWriteParam(ms32_LinkID, u8_Data);
            RpcCall(eProcedure.Device_Write, i_Param);

            cDeviceWriteReply i_Reply = mi_RxStream.ReadStruct<cDeviceWriteReply>();
            eVxiError e_Error = (eVxiError)Revert(i_Reply.ms32_ErrorCode);
            if (e_Error != eVxiError.Success)
                Throw("Error writing to device:  " + e_Error.ToString().Replace('_', ' '));

            #if TRACE_OUTPUT
                Debug.Print("< DeviceWrite()");
            #endif
        }

        /// <summary>
        /// Receive data from the device (ASCII responses or Waveform data)
        /// </summary>
        public Byte[] DeviceRead(Progress i_Progress, int s32_Timeout)
        {
            #if TRACE_OUTPUT
                Debug.Print("> DeviceRead()");
            #endif

            Debug.Assert(mb_LinkCreated, "Programming Error: You must first create a link");

            mi_Socket.ReceiveTimeout = s32_Timeout;
            MemoryStream i_Stream = new MemoryStream();
            while (true)
            {
                cDeviceReadParam i_Param = new cDeviceReadParam(ms32_LinkID);
                RpcCall(eProcedure.Device_Read, i_Param);

                cDeviceReadReply i_Reply = mi_RxStream.ReadStruct<cDeviceReadReply>();
                eVxiError e_Error = (eVxiError)Revert(i_Reply.ms32_ErrorCode);
                if (e_Error == eVxiError.IO_Timeout)
                    Throw("Timeout. No response from the oscilloscope.\nRead the Help file!", true);

                if (e_Error != eVxiError.Success)
                    Throw("Error reading from device:  " + e_Error.ToString().Replace('_', ' '));

                int s32_ByteCount  =             Revert(i_Reply.ms32_ByteCount);
                eReadFlags e_Flags = (eReadFlags)Revert(i_Reply.ms32_Reason);

                Byte[] u8_Data = mi_RxStream.ReadData(s32_ByteCount); // writes to Trace
                i_Stream.Write(u8_Data, 0, u8_Data.Length);

                if (i_Progress != null)
                    i_Progress.PrintProgress((int)i_Stream.Length);

                #if TRACE_OUTPUT
                    Debug.Print("    Reason= {0}", e_Flags);
                #endif

                if ((e_Flags & eReadFlags.End) > 0) break;

                // If the flag eReadFlags.End is not set, more data must be received.
                // After requesting 250.000 bytes, Rigol sends 102.400 + 102.400 + 45.212 bytes.
                #if TRACE_OUTPUT
                    Debug.Print("    --------- Get Next Block ----------");
                #endif
            }

            #if TRACE_OUTPUT
                Debug.Print("< DeviceRead() --> total bytes= " + i_Stream.Length);
            #endif
            return i_Stream.ToArray();
        }

        // ====================================================================================================

        /// <summary>
        /// Execute a Remote Procedure Call in the instrument.
        /// Any received data that comes behind the RPC Reply structure (cRmtPrcCallReply) remains in mi_RxStream.
        /// </summary>
        void RpcCall(eProcedure e_Procedure, ICmdParam i_Param)
        {
            Debug.Assert(mi_Socket.ProtocolType == ProtocolType.Tcp, "Programming Error: RpcCall() requires a TCP connection.");

            // ------ Send -------

            Byte[] u8_TxPack = BuildRpcTxPacket(e_Procedure, i_Param);
            #if TRACE_OUTPUT
                TraceLine("Send", u8_TxPack.Length, "TX Packet", "");
            #endif
            mi_Socket.Send(u8_TxPack, 0, u8_TxPack.Length, SocketFlags.None);

            // ----- Receive -----

            mi_RxStream.SetLength(0);
            bool b_Finished = false;
            while (!b_Finished)
            {
                try
                {
                    int s32_Read = mi_Socket.Receive(mu8_RxBuffer, 4, SocketFlags.None);
                    if (s32_Read != 4) Throw("Incomplete header received");
                }
                catch (SocketException Ex)
                {
                    // In case of Timeout the SocketException must be converted into a TimeoutException. 
                    if (Ex.ErrorCode == WSAETIMEDOUT)
                        Throw("Timeout. No response from the oscilloscope.\n"
                           + "Some oscilloscopes do not respond over TCP if the USB cable is also plugged in!", true);

                    #if TRACE_OUTPUT
                        Debug.Print("*** " + Ex.Message);
                    #endif
                    throw Ex;
                }

                int s32_Header = ToInteger(mu8_RxBuffer);

                // b_Finished == false --> more data will follow
                b_Finished = (s32_Header & FLAG_LAST_FRAGMENT) != 0;

                #if TRACE_OUTPUT
                    String s_Fragment = b_Finished ? " (Last)" : " (Fragment)";
                    TraceLine("Recv", 4, "Header", s32_Header.ToString("X8") + s_Fragment);
                #endif

                int s32_RxLen = s32_Header & 0x7FFFFFFF; // Rigol sends TCP packets of 396 byte
                while (s32_RxLen > 0)
                {
                    int s32_Read = mi_Socket.Receive(mu8_RxBuffer, s32_RxLen, SocketFlags.None);
                    mi_RxStream.Write(mu8_RxBuffer, 0, s32_Read);

                    #if TRACE_OUTPUT
                        TraceLine("Recv", s32_Read, "RX Data", "");
                    #endif

                    // Sometimes only 256 byte of the 396 byte arrive --> loop once more
                    s32_RxLen -= s32_Read;
                }
            }

            // ----- Check cRmtPrcCallReply -----

            String s_Error = CheckRpcRxPacket();
            if (s_Error != null)
                Throw(s_Error);
        }

        Byte[] BuildRpcTxPacket(eProcedure e_Procedure, ICmdParam i_Param)
        {
            ms32_MessageID ++;

            cRmtPrcCallParam i_Rpc = new cRmtPrcCallParam();
            VxiStream i_TxStream   = new VxiStream();

            // The header contains the flag FLAG_LAST_FRAGMENT and the count of bytes that follow.
            // If the command is broadcast over UDP the header is omitted.
            if (mi_Socket.ProtocolType == ProtocolType.Tcp)
            {
                int s32_Header = FLAG_LAST_FRAGMENT | (i_Rpc.GetTxByteCount() + i_Param.GetTxByteCount());
                i_TxStream.WriteInt32(s32_Header, "Header");
            }

            i_Rpc.ms32_MessageID   = Revert(ms32_MessageID);
            i_Rpc.ms32_MessageType = Revert((int)eMesgType.Call);
            i_Rpc.ms32_RpcVersion  = Revert(RPC_VERSION); 
            i_Rpc.ms32_Procedure   = Revert((int)e_Procedure);

            if (e_Procedure == eProcedure.Get_Port)
            {
                i_Rpc.ms32_Program        = Revert(PORT_MAPPER_PROGRAM);
                i_Rpc.ms32_ProgramVersion = Revert(PORT_MAPPER_VERSION);
            }
            else
            {
                i_Rpc.ms32_Program        = Revert(DEVICE_CORE_PROGRAM);
                i_Rpc.ms32_ProgramVersion = Revert(DEVICE_CORE_VERSION);    
            }

            i_TxStream.WriteStruct(i_Rpc);
            i_TxStream.WriteStruct(i_Param);
            return i_TxStream.ToArray();
        }

        /// <summary>
        /// Does not throw. Returns a string with an error message
        /// </summary>
        String CheckRpcRxPacket()
        {
            mi_RxStream.Position = 0;
            cRmtPrcCallReply i_RpcReply = mi_RxStream.ReadStruct<cRmtPrcCallReply>();

            if (Revert(i_RpcReply.ms32_MessageID) != ms32_MessageID)
                return "Received RPC response with invalid message ID";

            if (Revert(i_RpcReply.ms32_MessageType) != (int)eMesgType.Reply)
                return "Received RPC response with invalid message type";

            eAcceptState e_Accept = (eAcceptState)Revert(i_RpcReply.ms32_AcceptState);
            if (e_Accept != eAcceptState.Success)
                return "RPC Error response: " + e_Accept.ToString().Replace('_', ' ');

            if (Revert(i_RpcReply.ms32_ReplyState) != (int)eReplyState.Accepted)
                return "The RPC command was not accepted";

            return null;
        }

        // ================================== Helper ====================================

        /// <summary>
        /// Make integer big endian
        /// </summary>
        static int Revert(int s32_Value)
        {
            int s32_Return = 0;
            s32_Return |= (Byte)(s32_Value         >> 24);
            s32_Return |= (s32_Value & 0x00FF0000) >>  8;
            s32_Return |= (s32_Value & 0x0000FF00) <<  8;
            s32_Return |= (s32_Value & 0x000000FF) << 24;
            return s32_Return;
        }

        /// <summary>
        /// Create a big-endian integer from the given bytes
        /// </summary>
        static int ToInteger(Byte[] u8_Data)
        {
            int s32_Return = 0;
            s32_Return |= (int)u8_Data[0] << 24;
            s32_Return |= (int)u8_Data[1] << 16;
            s32_Return |= (int)u8_Data[2] <<  8;
            s32_Return |= (int)u8_Data[3];
            return s32_Return;
        }

        /// <summary>
        /// The TimeoutException has a sepcial treatment: 
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

        // ================================= Debug ======================================

    #if TRACE_OUTPUT

        /// <summary>
        /// Print a line like "    Store  24 byte cCreateLinkParam 4F737A69 00000000 00000000 00000005 696E7374 30000000"
        /// </summary>
        static void TraceLine(String s_Action, int s32_Bytes, String s_Debug, String s_Data)
        {
            Debug.Print("    {0} {1,6} byte {2} {3}", s_Action.PadRight(5), s32_Bytes, s_Debug.PadRight(17), s_Data);
        }

        /// <summary>
        /// returns max. 100 bytes as ASCII
        /// </summary>
        static String BytesToAscii(Byte[] u8_Data, int s32_Count)
        {
            int s32_MaxLen = Math.Min(100, s32_Count);
            String s_Ascii = Encoding.ASCII.GetString(u8_Data, 0, s32_MaxLen);
            if (s32_MaxLen < s32_Count) s_Ascii += " ....";
            return '"' + s_Ascii.Replace("\r", "<CR>").Replace("\n", "<LF>") + '"';
        }

        /// <summary>
        /// returns "00112233 44556677 AABBCCDD"
        /// </summary>
        static String BytesToHex(Byte[] u8_Data, int s32_Count)
        {
            StringBuilder i_Hex = new StringBuilder();
            for (int i=0; i<s32_Count; i++)
            {
                if (i > 0 && (i & 3) == 0)
                {
                    if (i_Hex.Length > 200)
                    {
                        i_Hex.Append(" ....");
                        break;
                    }
                    i_Hex.Append(" ");
                }
                i_Hex.AppendFormat("{0:X2}", u8_Data[i]);
            }
            return i_Hex.ToString();
        }
    #endif
    }
}

