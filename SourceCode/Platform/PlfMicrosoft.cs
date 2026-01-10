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
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Management;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32;

using IPlatform         = Platform.PlatformManager.IPlatform;
using IDevice           = Platform.PlatformManager.IDevice;
using eRuntime          = Platform.PlatformManager.eRuntime;
using Utils             = OsziWaveformAnalyzer.Utils;
using RtfViewer         = OsziWaveformAnalyzer.RtfViewer;
using eRegKey           = OsziWaveformAnalyzer.Utils.eRegKey;
using ScpiCombo         = Transfer.SCPI.ScpiCombo;

namespace Platform
{
    public class PlfMicrosoft : IPlatform
    {
        #region Structures and constants for RichTextBox

        [StructLayout(LayoutKind.Sequential)]
        struct NMHDR
        {
            public IntPtr hwndFrom;
            public IntPtr idFrom;   // This is declared as UINT_PTR in winuser.h
            public int    code;     // padded to 8 byte in 64 bit process
        }

        // There is a bug in the Richtext control: 
        // The 64 bit MsftEdit.dll sends an invalid struct because it does not respect the IA64 struct alignment conventions.
        // The member "msg" occupies only 4 bytes instead of beeing padded to 8 bytes.
        // All the following members behind "msg" have a wrong offset.
        // But when specifying Pack = 4 we adapt to this bug. Normally Pack = 8 would be used for 64 bit processes.
        // 32 Bit: Marshal.OffsetOf(typeof(ENLINK), "charRange") --> 24 Byte (correct)
        // 64 bit: Marshal.OffsetOf(typeof(ENLINK), "charRange") --> 44 Byte (adaption to bug, normally this would be at offset 48)
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        class ENLINK                       // Process = 32 bit  / 64 bit
        {                                  // ---------------------------
            public NMHDR     nmhdr;        // Size    = 12 byte / 24 byte
            public int       msg;          // Size    =  4 byte /  4 byte (Bug)
            public IntPtr    wParam;       // Size    =  4 byte /  8 byte
            public IntPtr    lParam;       // Size    =  4 byte /  8 byte
            public CHARRANGE charRange;    // Size    =  8 byte /  8 byte
        }

        [StructLayout(LayoutKind.Sequential)]
        class CHARRANGE
        {
            public int  cpMin;
            public int  cpMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        class TEXTRANGE
        {
            public CHARRANGE charRange;
            public IntPtr    lpstrText; // allocated by caller, zero terminated by RichEdit
        }

        const int EN_LINK         = 0x070b;
        const int WM_NOTIFY       = 0x004E;
        const int WM_LBUTTONDOWN  = 0x0201;
        const int WM_USER         = 0x0400;
        const int WM_REFLECT      = 0x2000;
        const int EM_GETTEXTRANGE = WM_USER + 75;

        #endregion

        #region Structures and constants for UsbWin

        const String           USB_TMC_CLASS_GUID   = "{A9FDBB24-128A-11D5-9961-00108335E361}"; // "USB Test and Measurement Devices"
        static readonly IntPtr INVALID_HANDLE_VALUE = (IntPtr)(-1);
        const int              ERROR_FILE_NOT_FOUND =   2;
        const int              ERROR_GEN_FAILURE    =  31; // A device attached to the system is not functioning.
        const int              WAIT_TIMEOUT         = 258;
        const int              ERROR_IO_PENDING     = 997; // This is not an error. It only says the operation has not yet finished.

        [Flags]
        enum eFileAccess : uint
        {
            GenericRead  = 0x80000000,
            GenericWrite = 0x40000000,
        }

        [Flags]
        enum eFileShare : uint
        {
            Read   = 0x01,
            Write  = 0x02,
        }

        enum eCreationDisposition : uint
        {
            New              = 1,
            CreateAlways     = 2,
            OpenExisting     = 3,
            OpenAlways       = 4,
            TruncateExisting = 5
        }

        [Flags]
        enum eFileAttributes : uint
        {
            None       = 0,
            Overlapped = 0x40000000,
        }

        #endregion

        #region enums

        [FlagsAttribute]
        private enum eThreadExecution : uint
        {
            ES_SYSTEM_REQUIRED   = 0x00000001, // Forces the system to be in the working state by resetting the system idle timer.
            ES_DISPLAY_REQUIRED  = 0x00000002, // Forces the display to be on
            ES_USER_PRESENT      = 0x00000004, // ONLY XP, otherwise failure !!
            ES_AWAYMODE_REQUIRED = 0x00000040, // Should be used only by media-recording applications
            ES_CONTINUOUS        = 0x80000000, // Informs the system that the state being set should remain in effect until the next call that uses ES_CONTINUOUS and one of the other state flags is cleared.
        }       

        #endregion

        #region DLL Import

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFileW(String lpFileName, eFileAccess dwDesiredAccess, eFileShare dwShareMode, IntPtr lpSecurityAttributes, eCreationDisposition dwCreationDisposition, eFileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(IntPtr hHandle, Byte[] bytes, int numBytesToRead, out int numBytesRead, ref NativeOverlapped overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(IntPtr hHandle, Byte[] bytes, int numBytesToWrite, out int numBytesWritten, ref NativeOverlapped overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateEventW(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, String lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int WaitForSingleObject(IntPtr hHandle, int dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetOverlappedResult(IntPtr hHandle, ref NativeOverlapped overlapped, out int lpNumberOfBytesTransferred, bool bWait);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool CancelIo(IntPtr hHandle);

        [DllImport("kernel32.dll", EntryPoint="LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(String s_File);

        [DllImport("Kernel32.dll")]
        private static extern eThreadExecution SetThreadExecutionState(eThreadExecution e_State); 

        [DllImport("Shlwapi.dll", EntryPoint="AssocQueryStringW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int AssocQueryString(int AssocF, int AssocStr, String pszAssoc, String pszExtra, [Out] StringBuilder pszOut, ref int pcchOut);

        [DllImport("User32.dll", EntryPoint="SendMessageW")]
        static extern IntPtr SendMessage(IntPtr h_Wnd, int s32_Message, IntPtr wParam, TEXTRANGE lParam);

        #endregion

        #region interface IShellLink

        /// <summary>
        /// This is required to create shortcuts
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        #endregion

        #region RichTextBox Modifications

        /// <summary>
        /// Performance boost:
        /// The .NET framework uses by default the Window Class "Richedit20W" in RichEd20.DLL.
        /// But this DLL has a bad performance. It needs 4 minutes to load a 2,5 MB RTF file with 45000 lines.
        /// Microsoft has fixed this in the new Richedit50W which needs only 2 seconds for the same RTF document.
        /// Additionally the new DLL has a new feature: It supports clickable RTF links with a hidden URL.
        /// </summary>
        public void RtfBoxCreateParams(CreateParams i_Params)
        {
            if (PlatformManager.Runtime == eRuntime.Microsoft)
            {
                // The new DLL is available since Windows XP SP1
                IntPtr h_Module = LoadLibrary("MsftEdit.dll");
                if (h_Module != IntPtr.Zero)
                {
                    // Replace the Window Class name "RichEdit20W" with "RichEdit50W"
                    i_Params.ClassName = "RichEdit50W";
                }
            }
        }

        // ---------------------------

        // We need some code to support the new feature of clickable RTF links in RichTextBox.
        // The .NET framework has been written for the old RichEd20.DLL where it works correctly.
        // But above we switch to the new MsftEdit.dll which supports a new RTF format of clickable links that was not available before:
        // {\field{\*\fldinst{HYPERLINK "824788,899109"}}{\fldrslt{32.991 ms}}}
        // Here the visible link text is "32.991 ms" and the invisible link URL is "824788,899109" containing the Start/End samples.
        // The .NET framework has never been tested with this type of links because it was written for the old DLL.

        // In the RichTextBox class in the .NET framework there is a workaround for Whidbey in the function CharRangeToString()
        // See https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/RichTextBox.cs,e50e843694f23c30
        // The following is Microsoft code:

        /* -----------------------------------------------------------------------------
        //Windows bug: 64-bit windows returns a bad range for us.  VSWhidbey 504502.  
        //Putting in a hack to avoid an unhandled exception.
        if (c.cpMax > Text.Length || c.cpMax-c.cpMin <= 0)                                <--- WRONG !
            return string.Empty;
        -------------------------------------------------------------------------------*/

        // In this "hack" Microsoft supposes that cpMax is always smaller than RichTextBox.Text.Length
        // This was correct for the old DLL but does not apply anymore as we use the new MsftEdit.dll here.

        // Example:
        // Visible on the screen in RichTextBox.Text = 
        // 13.669 ms
        // < R 48: 01 00 00 FF 00 FF 00 NAK >
        //
        // But internally the new DLL stores the Text in this form:
        // HYPERLINK "341735,362014"13.669 ms
        // < R 48: 01 00 00 FF 00 FF 00 NAK >
        //
        // So the internally used Text is longer than RichTextBox.Text.Length
        // The consequence of this "hack" for Whidbey is that the last links at the end of the RTF document
        // do not fire the RichTextBox.OnLinkClicked() event handler anymore.
        //
        // The following 2 small functions fix this problem:

        /// <summary>
        /// This is called from the Window Procedure of the RichTextBox.
        /// When the user has clicked a link in the RTF document a message WM_NOTIFY is sent to the parent control.
        /// It it is sent back to the control itself as a reflected message.
        /// </summary>
        public void RtfBoxWndProc(RtfViewer i_RtfViewer, ref Message k_Msg)
        {
            if (k_Msg.Msg == WM_REFLECT + WM_NOTIFY) 
            {
                NMHDR k_Notify = (NMHDR)k_Msg.GetLParam(typeof(NMHDR));
                if (k_Notify.code == EN_LINK) // the mouse is hovering a link or the link has been clicked
                {
                    ENLINK k_Link = (ENLINK)k_Msg.GetLParam(typeof(ENLINK));
                    if (k_Link.msg == WM_LBUTTONDOWN) // the user has clicked the link
                        OnLinkMouseDown(i_RtfViewer, k_Link.charRange);
                }
            }
        }

        /// <summary>
        /// WM_LBUTTONDOWN click on a RTF link --> call RtfViewer.OnTimestampClicked()
        /// </summary>
        void OnLinkMouseDown(RtfViewer i_RtfViewer, CHARRANGE k_CharRange)
        {
            int s32_CharCount = k_CharRange.cpMax - k_CharRange.cpMin;

            // k_CharRange contains the location of the link URL in the internal Text of the control.
            // It will never be more than 20 characters long.
            if (k_CharRange.cpMin < 1 || s32_CharCount < 1 || s32_CharCount > 1000)
            {
                Debug.Assert(false, "RtfViewer: Invalid URL character range from EN_LINK event.");
                return;
            }

            TEXTRANGE k_TxtRange = new TEXTRANGE();
            k_TxtRange.charRange = k_CharRange;
            k_TxtRange.lpstrText = Marshal.AllocHGlobal((s32_CharCount + 1) * 2); // 1 unicode char = 2 byte + terminating zero

            // EM_GETTEXTRANGE extracts the invisible URL "341735,362014" from the Text that the control stores internally:
            // HYPERLINK "341735,362014"13.669 ms
            int s32_RetCount = (int)PlfMicrosoft.SendMessage(i_RtfViewer.Handle, EM_GETTEXTRANGE, IntPtr.Zero, k_TxtRange);
            if (s32_RetCount == s32_CharCount)
            {
                String s_LinkUrl = Marshal.PtrToStringUni(k_TxtRange.lpstrText);
                i_RtfViewer.OnTimestampClicked(s_LinkUrl);
            }
            else
            {
                Debug.Assert(false, "RtfViewer: Invalid URL character count from EM_GETTEXTRANGE");
            }
            Marshal.FreeHGlobal(k_TxtRange.lpstrText);
        }

        #endregion

        #region class UsbWin

        /// <summary>
        /// SCPI USB transfer implementation for Windows.
        /// This class communicates with the USB TMC driver Ausbtmc.sys from the IVI Foundation.
        /// </summary>
        public class UsbWin : IDevice
        {
            IntPtr           mh_Device;
            NativeOverlapped mk_Overlap = new NativeOverlapped();

            /// <summary>
            /// Contructor opens the device
            /// </summary>
            public UsbWin(ScpiCombo i_Combo)
            {
                // The handle mh_Device allows to directly send to the USB bulk OUT pipe using WriteFile() 
                // and receive from USB bulk IN pipe using ReadFile(). Programming is the same as for RS232.
                // See "USB Tutorial.chm" in subfolder "Documentation"
                mh_Device = CreateFileW(i_Combo.ms_DevicePath,
                                        eFileAccess.GenericRead | eFileAccess.GenericWrite,
                                        eFileShare.Read | eFileShare.Write,
                                        IntPtr.Zero,
                                        eCreationDisposition.OpenExisting,
                                        eFileAttributes.Overlapped,  // use non-blocking I/O
                                        IntPtr.Zero);
            
                if (mh_Device == INVALID_HANDLE_VALUE)
                {
                    int s32_Error = Marshal.GetLastWin32Error();

                    // Avoid strange error message "File not found" if the device has been disconnected meanwhile.
                    if (s32_Error == ERROR_FILE_NOT_FOUND)
                        throw new Exception("The device does not exist.\nPlease check if the driver is installed correctly.");

                    throw new Win32Exception(s32_Error);
                }

                // This event is fired by Windows when the overloapped I/O operation has completed.
                mk_Overlap.EventHandle = CreateEventW(IntPtr.Zero, true, false, null);
            }

            // ATTENTION: Do not implement a Finializer here! Class SCPI cares about this.
            public void Dispose()
            {
                if (mk_Overlap.EventHandle != IntPtr.Zero)
                {
                    CloseHandle(mk_Overlap.EventHandle);
                    mk_Overlap.EventHandle = IntPtr.Zero;
                }

                if (mh_Device != IntPtr.Zero)
                {
                    CloseHandle(mh_Device);
                    mh_Device = IntPtr.Zero;
                }
            }

            // --------------------------------------------------------------------

            public void CancelTransfer()
            {
                // Cancels all pending input and output (I/O) operations that are issued by the calling thread for the specified handle.
                CancelIo(mh_Device);
            }

            public void Send(Byte[] u8_Packet, int s32_Timeout)
            {
                // Remove any possibly remaining bytes from the driver's input buffer
                CancelIo(mh_Device);

                int s32_BytesWritten; // Write USB OUT transfer
                if (!WriteFile(mh_Device, u8_Packet, u8_Packet.Length, out s32_BytesWritten, ref mk_Overlap))
                {
                    int s32_Error = Marshal.GetLastWin32Error();
                    if (s32_Error != ERROR_IO_PENDING)
                        throw new Win32Exception(s32_Error);
                    
                    // Do NOT throw a TimeoutException here, which must only be used while waiting for a response.
                    if (WAIT_TIMEOUT == WaitForSingleObject(mk_Overlap.EventHandle, s32_Timeout))
                    {
                        // ATTENTION:
                        // This CancelIo() is EXTREMELY important! If it is missing you get TIMEOUT's again and again
                        CancelIo(mh_Device);
                        throw new Exception("Timeout sending command to device");
                    }

                    if (!GetOverlappedResult(mh_Device, ref mk_Overlap, out s32_BytesWritten, false))
                    {
                        s32_Error = Marshal.GetLastWin32Error();

                        // This error happens frequently after Windows was in Sleep mode. The reason is mostly a buggy driver.
                        if (s32_Error == ERROR_GEN_FAILURE)
                            throw new Exception("The USB device is not functioning.\nThis may happen after the computer was in Sleep mode.\nPlease reconnect the USB cable.");

                        throw new Win32Exception(s32_Error);
                    }
                }

                if (s32_BytesWritten != u8_Packet.Length)
                    throw new Exception("Error sending USB OUT packet");
            }

            /// <summary>
            /// returns the count of bytes written to u8_RxBuffer
            /// The IVI driver is a primitive crap.
            /// It is not possible to call this function first to request only the TMC header and then again to read the data.
            /// This is no problem with other drivers, but you will screw up the entire communication when you try this here.
            /// </summary>
            public int Receive(Byte[] u8_RxBuffer, int s32_Timeout)
            {
                int s32_BytesRead; // Read USB IN transfer
                if (!ReadFile(mh_Device, u8_RxBuffer, u8_RxBuffer.Length, out s32_BytesRead, ref mk_Overlap))
                {
                    int s32_Error = Marshal.GetLastWin32Error();
                    if (s32_Error != ERROR_IO_PENDING)
                        throw new Win32Exception(s32_Error);
                    
                    if (WAIT_TIMEOUT == WaitForSingleObject(mk_Overlap.EventHandle, s32_Timeout))
                    {
                        Debug.Print("  << Receive() --> TIMEOUT");

                        // ATTENTION:
                        // This CancelIo() is EXTREMELY important! If it is missing you get TIMEOUT's again and again
                        CancelIo(mh_Device);
                        throw new TimeoutException("Timeout waiting for response from device.\n"
                                                 + "This may happen when an invalid SCPI command was sent or the wrong oscilloscope "
                                                 + "serie was selected or when the oscilloscope is busy.");
                    }

                    if (!GetOverlappedResult(mh_Device, ref mk_Overlap, out s32_BytesRead, false))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                return s32_BytesRead;
            }
        }

        #endregion

        /// <summary>
        /// This function is called from the button "Install Driver" that should exist in all the Capture Forms.
        /// The driver installer must run elevated.
        /// </summary>
        public void InstallDriver(Form i_Owner)
        {
            try
            {
                ProcessStartInfo k_Info = new ProcessStartInfo(Utils.WinInstallerPath, "");
                k_Info.UseShellExecute = true;
                k_Info.Verb = "runas"; // run as administrator
                Process.Start(k_Info);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(i_Owner, Ex, "Error starting the installer");
            }
        }

        /// <summary>
        /// Open the default browser and jump to the URL
        /// </summary>
        public void ShowURL(Form i_Owner, String s_URL)
        {
            try
            {
                ProcessStartInfo k_Info = new ProcessStartInfo(s_URL);
                k_Info.WindowStyle = ProcessWindowStyle.Maximized;
                k_Info.UseShellExecute = true;
                Process.Start(k_Info);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(i_Owner, Ex);
            }
        }

        /// <summary>
        /// Open the default browser and jump to the given chapter in the file Manual.htm
        /// Process.Start(@"C:\....\UserManual.htm#Chapter") results in an error.
        /// It is not possible to pass a path that does not end with a file extension.
        /// </summary>
        public void ShowHelp(Form i_Owner, String s_Chapter = "")
        {
            try
            {
                if (s_Chapter.Length > 0) s_Chapter = "#" + s_Chapter;

                // "file://C:/Program Files/Oszi Waveform Analyzer/Help/UserManual.htm#Chapter"
                String s_URL = "file://" + Utils.HelpHtmlPath.Replace('\\', '/') + s_Chapter;

                // "C:\\Program Files\\Mozilla Firefox\\firefox.exe" -osint -url "%1"
                String s_CmdLine = GetAssociatedProgram(".htm");
                s_CmdLine = s_CmdLine.Replace("%1", s_URL);

                ProcessStartInfo k_Info = new ProcessStartInfo(s_CmdLine, "");
                k_Info.UseShellExecute = false;
                Process.Start(k_Info);
            }
            catch (Exception Ex)
            {
                Utils.ShowExceptionBox(i_Owner, Ex, "Error opening the help file in the browser");
            }
        }

        /// <summary>
        /// returns the Command Line:  "C:\\Program Files\\Mozilla Firefox\\firefox.exe" -osint -url "%1"
        /// Resolving a file extension into the default program is very complicated.
        /// Once upon a time in Windows 95 you could simply look it up in the registry under HKLM\Software\Classes
        /// But with every new Windows version it became more complicated.
        /// Then the user could override this in in HKCU. 
        /// Then Microsoft added the option to select an "Open With" program for each file extension.
        /// The registry entry "UserChoice" was added that overrides all the previous registry keys.
        /// And in the next Windows version Microsoft may change this again.
        /// A complex lookup process is required to open a .HTML file in the same browser 
        /// that opens when the user double clicks a HTML file in Explorer.
        /// If you try to resolve this manually from the registry you will end up with the wrong browser opening
        /// or you may even get a path to a browser that has been uninstalled long ago.
        /// The only bullet proof way is to let the Shell do this work "the Microsoft way".
        /// The API FindExecutable() may return useless results (for example the path to a DLL).
        /// The only API that always gives corret results is AssocQueryString()
        /// </summary>
        String GetAssociatedProgram(String s_FileExtension)
        {
            if (!s_FileExtension.StartsWith("."))
                 s_FileExtension = "." + s_FileExtension;

            StringBuilder i_CmdLine = new StringBuilder(300); // MAX_PATH = 260
            int s32_Length = i_CmdLine.Capacity;

            const int ASSOCSTR_COMMAND = 1;
            int s32_Error = AssocQueryString(0, ASSOCSTR_COMMAND, s_FileExtension, null, i_CmdLine, ref s32_Length);
            if (s32_Error != 0)
                throw new Win32Exception(s32_Error); // HRESULT

            return i_CmdLine.ToString();
        }

        /// <summary>
        /// Create a shortcut in the startmenu once. (If the user deletes it, it will not be created again)
        /// </summary>
        public void CreateShortcutInStartMenu()
        {
            String s_LastInstalled = Utils.RegReadString(eRegKey.InstallPath);
            if (String.Compare(Application.ExecutablePath, s_LastInstalled, true) != 0)
            {
                try
                {
                    String s_StartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                    String s_LinkPath  = Path.Combine(s_StartMenu, "Oszi Waveform Analyzer.lnk");

                    // Always update an existing shortcut in case the user has moved the executable to another folder
                    CreateShortcut(Application.ExecutablePath, Utils.AppDir, s_LinkPath, "Oszi Waveform Analyzer - Capture, Display, A/D, Logicanalyzer bu ElmüSoft");
                }
                catch (Exception Ex)
                {
                    Utils.ShowExceptionBox(null, Ex, "Error creating shortcut.");
                }
                Utils.RegWriteString(eRegKey.InstallPath, Application.ExecutablePath);
            }
        }

        /// <summary>
        /// Create a shortcut (*.lnk file)
        /// </summary>
        void CreateShortcut(String s_DestPath, String s_WorkDir, String s_LinkPath, String s_Description)
        {
            Debug.Assert(s_LinkPath.EndsWith(".lnk"), "Programming Error: Wrong file extension");

            IShellLink i_Link = (IShellLink)new ShellLink();
            i_Link.SetDescription     (s_Description);
            i_Link.SetWorkingDirectory(s_WorkDir);
            i_Link.SetPath            (s_DestPath);

            IPersistFile i_File = (IPersistFile)i_Link;
            i_File.Save(s_LinkPath, false);
        }

        /// <summary>
        /// Associate file extension *.oszi with this program so the user can double click a *.oszi file and it will open in this program.
        /// s_Open may be:
        /// "C:\Program Files\OsziWaveformAnalyzer.exe" -open "%1" 
        /// Windows will insert the path of the double clicked file into "%1" and start the EXE with the resulting command line:
        /// "C:\Program Files\OsziWaveformAnalyzer.exe" -open "D:\Temp\MyCapture.oszi" 
        /// The command line will be handled in FormMain.OnCmdLineTimer()
        /// </summary>
        public void AssociateOsziExtension()
        {
            // Register file extension *.oszi for the current user.
            // The icon will become visible after logging off the next time.
            String s_Icon = "\"" + Application.ExecutablePath + "\",0";
            String s_Open = "\"" + Application.ExecutablePath + "\"" + Utils.CMD_LINE_ACTION + "\"%1\"";

            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\.oszi", "", "OsziWaveformFile");
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\OsziWaveformFile\DefaultIcon",        "", s_Icon);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\OsziWaveformFile\shell\open\command", "", s_Open);
        }

        /// <summary>
        /// Prevent that the operating system goes into sleep mode
        /// </summary>
        public void PreventSleep()
        {
            // Calling SetThreadExecutionState without ES_CONTINUOUS simply resets the idle timer; 
            // to keep the system in the working state, the thread must call this function periodically.
            SetThreadExecutionState(eThreadExecution.ES_SYSTEM_REQUIRED);
        }

        // ============================ SCPI ================================

        /// <summary>
        /// Throws
        /// Gets the list of the Symbolic Links of the connected Measurement USB devices, like
        /// \\?\USB#VID_1AB1&PID_04CE#DS1ZC204807063#{A9FDBB24-128A-11D5-9961-00108335E361}
        /// and loads them into the ComboBox
        /// The ComboBox Items are ScpiCombo classes.
        /// </summary>
        public void EnumerateUsbDevices(ComboBox i_ComboDevices)
        {
            i_ComboDevices.Items.Clear();

            using (ManagementClass i_Entity = new ManagementClass("Win32_PnPEntity"))
            {
                foreach (ManagementObject i_Inst in i_Entity.GetInstances())
                {
                    Object o_ClassGUID = i_Inst.GetPropertyValue("ClassGuid");
                    if (o_ClassGUID == null || o_ClassGUID.ToString().ToUpper() != USB_TMC_CLASS_GUID)
                        continue;
                   
                    // s_DeviceID = USB\VID_1AB1&PID_04CE\DS1ZC204807063
                    String s_DeviceID = i_Inst.GetPropertyValue("PnpDeviceID").ToString();

                    // Same link as in HKLM\SYSTEM\CurrentControlSet\Control\DeviceClasses\{a9fdbb24-128a-11d5-9961-00108335e361}
                    String s_SymbolicLink = @"\\?\" + s_DeviceID.Replace('\\', '#') + '#' + USB_TMC_CLASS_GUID;

                    String[] s_Parts = s_DeviceID.Split('\\');
                    String  s_Serial = s_Parts[s_Parts.Length -1].ToUpper(); // DS1ZC204807063
                    
                    i_ComboDevices.Items.Add(new ScpiCombo(s_Serial, s_SymbolicLink));
                }
            }

            Utils.ComboAdjustDropDownWidth(i_ComboDevices);

            if (i_ComboDevices.Items.Count > 0)
                i_ComboDevices.SelectedIndex = 0;
        }

        /// <summary>
        /// Create a new instance of the class that is derived from IDevice and open the device
        /// i_Combo comes from EnumerateScpiDevices()
        /// </summary>
        public IDevice OpenUsbDevice(ScpiCombo i_Combo)
        {
            return new UsbWin(i_Combo); // opens the device
        }
    }
}
