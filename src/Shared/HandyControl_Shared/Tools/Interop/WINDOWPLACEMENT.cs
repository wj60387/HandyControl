using System.Runtime.InteropServices;

namespace HandyControl.Tools.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal class WINDOWPLACEMENT
    {
        public int length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
        public int flags;
        public int showCmd;
        public NativeMethods.POINT ptMinPosition;
        public NativeMethods.POINT ptMaxPosition;
        public NativeMethods.RECT rcNormalPosition;
    }
}