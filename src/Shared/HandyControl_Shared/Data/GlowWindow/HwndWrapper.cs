using System;
using System.Runtime.InteropServices;
using HandyControl.Tools.Interop;

namespace HandyControl.Data
{
    internal abstract class HwndWrapper : DisposableObject
    {
        private IntPtr _handle;

        private bool _isHandleCreationAllowed;

        private short _wndClassAtom;

        private WndProc _wndProc;

        protected short WindowClassAtom
        {
            get
            {
                if (_wndClassAtom == 0)
                    _wndClassAtom = CreateWindowClassCore();
                return _wndClassAtom;
            }
        }

        protected virtual short CreateWindowClassCore()
        {
            return RegisterClass(Guid.NewGuid().ToString());
        }

        protected virtual void DestroyWindowClassCore()
        {
            if (_wndClassAtom == 0)
                return;
            UnsafeNativeMethods.UnregisterClass(new IntPtr(_wndClassAtom), NativeMethods.GetModuleHandle(null));
            _wndClassAtom = 0;
        }

        protected short RegisterClass(string className)
        {
            return UnsafeNativeMethods.RegisterClass(new WNDCLASS
            {
                cbClsExtra = 0,
                cbWndExtra = 0,
                hbrBackground = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hIcon = IntPtr.Zero,
                lpfnWndProc = _wndProc = WndProc,
                lpszClassName = className,
                lpszMenuName = null,
                style = 0
            });
        }

        private void SubclassWndProc()
        {
            _wndProc = WndProc;
            NativeMethods.SetWindowLong(_handle, -4, Marshal.GetFunctionPointerForDelegate(_wndProc));
        }

        protected abstract IntPtr CreateWindowCore();

        protected virtual void DestroyWindowCore()
        {
            if (!(_handle != IntPtr.Zero))
                return;
            NativeMethods.DestroyWindow(_handle);
            _handle = IntPtr.Zero;
        }

        protected virtual IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam) => NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);

        public IntPtr Handle
        {
            get
            {
                EnsureHandle();
                return _handle;
            }
        }

        public void EnsureHandle()
        {
            if (_handle != IntPtr.Zero)
                return;
            if (_isHandleCreationAllowed)
            {
                _isHandleCreationAllowed = false;
                _handle = CreateWindowCore();
                if (!IsWindowSubclassed)
                    return;
                SubclassWndProc();
            }
        }

        protected virtual bool IsWindowSubclassed => false;

        protected override void DisposeNativeResources()
        {
            _isHandleCreationAllowed = false;
            DestroyWindowCore();
            DestroyWindowClassCore();
        }
    }
}
