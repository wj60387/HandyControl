namespace HandyControl.Tools.Interop
{
    internal struct WINDOWINFO
    {
        public int cbSize;
        public NativeMethods.RECT rcWindow;
        public NativeMethods.RECT rcClient;
        public int dwStyle;
        public int dwExStyle;
        public uint dwWindowStatus;
        public uint cxWindowBorders;
        public uint cyWindowBorders;
        public ushort atomWindowType;
        public ushort wCreatorVersion;
    }
}