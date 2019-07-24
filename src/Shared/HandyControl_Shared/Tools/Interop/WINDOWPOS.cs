using System;
using System.Runtime.InteropServices;

namespace HandyControl.Tools.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal class WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }
}