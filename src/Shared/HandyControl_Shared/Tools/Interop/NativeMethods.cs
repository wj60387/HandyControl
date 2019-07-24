using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Windows;

namespace HandyControl.Tools.Interop
{
    public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    internal class NativeMethods
    {
        public static readonly IntPtr HRGN_NONE = new IntPtr(-1);

        public const int
            WS_CHILD = 0x40000000,
            BITSPIXEL = 12,
            PLANES = 14,
            BI_RGB = 0,
            DIB_RGB_COLORS = 0,
            NIF_MESSAGE = 0x00000001,
            NIF_ICON = 0x00000002,
            NIF_TIP = 0x00000004,
            NIF_INFO = 0x00000010,
            NIN_BALLOONSHOW = WM_USER + 2,
            NIN_BALLOONHIDE = WM_USER + 3,
            NIN_BALLOONTIMEOUT = WM_USER + 4,
            NIM_ADD = 0x00000000,
            NIM_MODIFY = 0x00000001,
            NIM_DELETE = 0x00000002,
            NIIF_NONE = 0x00000000,
            NIIF_INFO = 0x00000001,
            NIIF_WARNING = 0x00000002,
            NIIF_ERROR = 0x00000003,
            WM_KEYDOWN = 0x0100,
            WM_KEYUP = 0x0101,
            WM_SYSKEYDOWN = 0x0104,
            WM_SYSKEYUP = 0x0105,
            WM_MOUSEMOVE = 0x0200,
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_LBUTTONDBLCLK = 0x0203,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_RBUTTONDBLCLK = 0x0206,
            WM_MBUTTONDOWN = 0x0207,
            WM_MBUTTONUP = 0x0208,
            WM_MBUTTONDBLCLK = 0x0209,
            WM_USER = 0x0400,
            TB_GETBUTTON = WM_USER + 23,
            TB_BUTTONCOUNT = WM_USER + 24,
            TB_GETITEMRECT = WM_USER + 29,
            STANDARD_RIGHTS_REQUIRED = 0x000F0000,
            SYNCHRONIZE = 0x00100000,
            PROCESS_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0xFFF,
            MEM_COMMIT = 0x1000,
            MEM_RELEASE = 0x8000,
            PAGE_READWRITE = 0x04,
            TBSTATE_HIDDEN = 0x08,
            VERTRES = 10,
            HORZRES = 8,
            DESKTOPVERTRES = 117,
            DESKTOPHORZRES = 118,
            LOGPIXELSX = 88,
            LOGPIXELSY = 90,
            CXFRAME = 32,
            CXSIZEFRAME = CXFRAME;

        [Flags]
        public enum ProcessAccess
        {
            AllAccess = CreateThread | DuplicateHandle | QueryInformation | SetInformation | Terminate | VMOperation | VMRead | VMWrite | Synchronize,
            CreateThread = 0x2,
            DuplicateHandle = 0x40,
            QueryInformation = 0x400,
            SetInformation = 0x200,
            Terminate = 0x1,
            VMOperation = 0x8,
            VMRead = 0x10,
            VMWrite = 0x20,
            Synchronize = 0x100000
        }

        [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(Rect rect)
            {
                Left = (int)rect.Left;
                Top = (int)rect.Top;
                Right = (int)rect.Right;
                Bottom = (int)rect.Bottom;
            }

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Height
            {
                get => Bottom - Top;
                set => Bottom = Top + value;
            }

            public int Width
            {
                get => Right - Left;
                set => Right = Left + value;
            }

            public Size Size => new Size(Width, Height);

            public void Offset(int dx, int dy)
            {
                Left += dx;
                Right += dx;
                Top += dy;
                Bottom += dy;
            }

            public Point Position => new Point(Left, Top);

            public Int32Rect ToInt32Rect() => new Int32Rect(Left, Top, Width, Height);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TBBUTTON
        {
            public int iBitmap;
            public int idCommand;
            public IntPtr fsStateStylePadding;
            public IntPtr dwData;
            public IntPtr iString;
        }

        [Flags]
        public enum AllocationType
        {
            Commit = 0x1000,
            Reserve = 0x2000,
            Decommit = 0x4000,
            Release = 0x8000,
            Reset = 0x80000,
            Physical = 0x400000,
            TopDown = 0x100000,
            WriteWatch = 0x200000,
            LargePages = 0x20000000
        }

        [Flags]
        public enum MemoryProtection
        {
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            NoAccess = 0x01,
            ReadOnly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            GuardModifierflag = 0x100,
            NoCacheModifierflag = 0x200,
            WriteCombineModifierflag = 0x400
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TRAYDATA
        {
            public IntPtr hwnd;
            public uint uID;
            public uint uCallbackMessage;
            public uint bReserved0;
            public uint bReserved1;
            public IntPtr hIcon;
        }

        [Flags]
        public enum FreeType
        {
            Decommit = 0x4000,
            Release = 0x8000,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        public struct BITMAPINFOHEADER
        {
            internal uint biSize;
            internal int biWidth;
            internal int biHeight;
            internal ushort biPlanes;
            internal ushort biBitCount;
            internal uint biCompression;
            internal uint biSizeImage;
            internal int biXPelsPerMeter;
            internal int biYPelsPerMeter;
            internal uint biClrUsed;
            internal uint biClrImportant;

            internal static BITMAPINFOHEADER Default => new BITMAPINFOHEADER
            {
                biSize = 40,
                biPlanes = 1
            };
        }

        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern int RegisterWindowMessage(string msg);

        [DllImport(ExternDll.User32, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref RECT lpPoints, uint cPoints);

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out TBBUTTON lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out RECT lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out TRAYDATA lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
        public static extern uint SendMessage(IntPtr hWnd, uint Msg, uint wParam, IntPtr lParam);

        [DllImport(ExternDll.User32, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenProcess(ProcessAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int CloseHandle(IntPtr hObject);

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, FreeType dwFreeType);

        [DllImport(ExternDll.User32, SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport(ExternDll.User32, SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport(ExternDll.User32)]
        public static extern int GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
        public static extern bool GetCursorPos(out POINT pt);

        internal static Point GetCursorPos()
        {
            GetCursorPos(out var point1);
            return new Point(point1.X, point1.Y);
        }

        [SecurityCritical]
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [DllImport(ExternDll.User32)]
        public static extern int GetSystemMetrics(SM nIndex);

        [DllImport(ExternDll.User32)]
        internal static extern int GetSystemMetrics(int index);

        [DllImport(ExternDll.User32, EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        public static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport(ExternDll.User32, EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Auto)]
        public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong) => IntPtr.Size == 4
            ? SetWindowLongPtr32(hWnd, nIndex, dwNewLong)
            : SetWindowLongPtr64(hWnd, nIndex, dwNewLong);

        [DllImport(ExternDll.Kernel32, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport(ExternDll.User32, CharSet = CharSet.Unicode)]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport(ExternDll.User32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport(ExternDll.Gdi32, SetLastError = true)]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

        [DllImport(ExternDll.Gdi32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport(ExternDll.User32, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport(ExternDll.User32, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport(ExternDll.Gdi32, SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport(ExternDll.Gdi32, SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        public static int LoWord(int value) => (short)(value & ushort.MaxValue);

        public static int GetXLParam(int lParam) => LoWord(lParam);

        public static int HiWord(int value) => (short)(value >> 16);

        public static int GetYLParam(int lParam) => HiWord(lParam);

        [DllImport(ExternDll.User32, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            int dwExStyle,
            IntPtr classAtom,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        public enum GWLP
        {
            USERDATA = -21,
            ID = -12,
            HWNDPARENT = -8,
            HINSTANCE = -6,
            WNDPROC = -4,
        }

        [DllImport(ExternDll.User32, CharSet = CharSet.Unicode)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport(ExternDll.User32, CharSet = CharSet.Unicode)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, GWLP nIndex, IntPtr dwNewLong) =>
            IntPtr.Size == 8
                ? SetWindowLongPtr(hWnd, (int) nIndex, dwNewLong)
                : new IntPtr(SetWindowLong(hWnd, (int) nIndex, dwNewLong.ToInt32()));

        [DllImport(ExternDll.User32, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int nMsg, IntPtr wParam, IntPtr lParam);

        public static IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam) => SendMessage(hwnd, msg, wParam, IntPtr.Zero);

        [DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            int flags);

        [DllImport(ExternDll.User32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hwnd, WINDOWPLACEMENT lpwndpl);

        public static WINDOWPLACEMENT GetWindowPlacement(IntPtr hwnd)
        {
            var lpwndpl = new WINDOWPLACEMENT();
            return GetWindowPlacement(hwnd, lpwndpl) ? lpwndpl : throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [DllImport(ExternDll.MsImg32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AlphaBlend(
            IntPtr hdcDest,
            int xoriginDest,
            int yoriginDest,
            int wDest,
            int hDest,
            IntPtr hdcSrc,
            int xoriginSrc,
            int yoriginSrc,
            int wSrc,
            int hSrc,
            BLENDFUNCTION pfn);

        public struct Win32SIZE
        {
            public int cx;
            public int cy;
        }

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDest,
            ref POINT pptDest,
            ref Win32SIZE psize,
            IntPtr hdcSrc,
            ref POINT pptSrc,
            uint crKey,
            [In] ref BLENDFUNCTION pblend,
            uint dwFlags);

        [DllImport(ExternDll.User32)]
        public static extern int TrackPopupMenuEx(
            IntPtr hmenu,
            uint fuFlags,
            int x,
            int y,
            IntPtr hwnd,
            IntPtr lptpm);

        [DllImport(ExternDll.User32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, int nMsg, IntPtr wParam, IntPtr lParam);

        public enum GWL
        {
            EXSTYLE = -20,
            STYLE = -16
        }

        [Flags]
        public enum RedrawWindowFlags : uint
        {
            Invalidate = 1,
            InternalPaint = 2,
            Erase = 4,
            Validate = 8,
            NoInternalPaint = 16,
            NoErase = 32,
            NoChildren = 64,
            AllChildren = 128,
            UpdateNow = 256,
            EraseNow = 512,
            Frame = 1024,
            NoFrame = 2048
        }

        [DllImport(ExternDll.User32)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public static int GetWindowLong(IntPtr hWnd, GWL nIndex) => GetWindowLong(hWnd, (int)nIndex);

        [DllImport(ExternDll.User32)]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, RedrawWindowFlags flags);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hwnd);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsZoomed(IntPtr hwnd);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

        public static IntPtr MakeParam(int lowWord, int highWord) => new IntPtr(lowWord & ushort.MaxValue | highWord << 16);

        [DllImport(ExternDll.User32)]
        public static extern short GetKeyState(int vKey);

        public static bool IsKeyPressed(int vKey) => GetKeyState(vKey) < 0;

        private static int vsmNotifyOwnerActivate;

        public static int NOTIFYOWNERACTIVATE
        {
            get
            {
                if (vsmNotifyOwnerActivate == 0)
                    vsmNotifyOwnerActivate = RegisterWindowMessage("HandyNOTIFYOWNERACTIVATE");
                return vsmNotifyOwnerActivate;
            }
        }

        [DllImport(ExternDll.User32)]
        public static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hrgnClip, int dwFlags);

        [DllImport(ExternDll.Gdi32)]
        public static extern IntPtr CreateSolidBrush(int colorref);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FillRect(IntPtr hDC, ref RECT rect, IntPtr hbrush);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnableMenuItem(IntPtr menu, uint uIDEnableItem, uint uEnable);

        [DllImport(ExternDll.User32)]
        public static extern IntPtr GetSystemMenu(IntPtr hwnd, bool bRevert);

        [DllImport(ExternDll.Gdi32, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport(ExternDll.Gdi32, CallingConvention = CallingConvention.StdCall)]
        private static extern int CombineRgn(IntPtr hrngDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

        public enum CombineMode
        {
            RGN_AND = 1,
            RGN_MIN = 1,
            RGN_OR = 2,
            RGN_XOR = 3,
            RGN_DIFF = 4,
            RGN_COPY = 5,
            RGN_MAX = 5,
        }

        public static int CombineRgn(IntPtr hrnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, CombineMode combineMode) => CombineRgn(hrnDest, hrgnSrc1, hrgnSrc2, (int)combineMode);

        [DllImport(ExternDll.Gdi32, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CreateRoundRectRgn(
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse);

        [DllImport(ExternDll.User32, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool redraw);

        [DllImport(ExternDll.User32)]
        public static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO monitorInfo);

        [DllImport(ExternDll.Gdi32, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CreateRectRgnIndirect(ref RECT lprc);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowInfo(IntPtr hwnd, ref WINDOWINFO pwi);

        [DllImport(ExternDll.User32)]
        public static extern IntPtr GetWindow(IntPtr hwnd, int nCmd);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

        [DllImport(ExternDll.Kernel32)]
        public static extern uint GetCurrentThreadId();

        public static int GET_SC_WPARAM(IntPtr wParam) => (int)wParam & 65520;

        [DllImport(ExternDll.User32)]
        public static extern IntPtr MonitorFromPoint(POINT pt, int flags);

        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IntersectRect(out RECT lprcDst, [In] ref RECT lprcSrc1, [In] ref RECT lprcSrc2);
    }
}