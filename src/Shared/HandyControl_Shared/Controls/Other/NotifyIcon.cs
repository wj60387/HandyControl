﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HandyControl.Data;
using HandyControl.Tools;
using HandyControl.Tools.Interop;

namespace HandyControl.Controls
{
    public class NotifyIcon : FrameworkElement, IDisposable
    {
        private bool _isMouseOver;

        private bool _added;

        private readonly object _syncObj = new object();

        private readonly int _id;

        private static int NextId;

        private ImageSource _icon;

        private IntPtr _iconCurrentHandle;

        private IntPtr _iconDefaultHandle;

        private IconHandle _iconHandle;

        private const int WmTrayMouseMessage = NativeMethods.WM_USER + 1024;

        private string _windowClassName;

        private int _wmTaskbarCreated;

        private IntPtr _messageWindowHandle;

        private readonly WndProc _callback;

        private Popup _contextContent;

        private bool _doubleClick;

        private DispatcherTimer _dispatcherTimerBlink;

        private DispatcherTimer _dispatcherTimerPos;

        private bool _isTransparent;

        private bool _isDisposed;

        static NotifyIcon()
        {
            VisibilityProperty.OverrideMetadata(typeof(NotifyIcon), new PropertyMetadata(Visibility.Visible, OnVisibilityChanged));
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (NotifyIcon) d;
            var v = (Visibility)e.NewValue;

            if (v == Visibility.Visible)
            {
                if (ctl._iconCurrentHandle == IntPtr.Zero)
                {
                    ctl.OnIconChanged();
                }
                ctl.UpdateIcon(true);
            }
            else if(ctl._iconCurrentHandle != IntPtr.Zero)
            {
                ctl.UpdateIcon(false);
            }
        }

        public NotifyIcon()
        {
            _id = ++NextId;
            _callback = Callback;

            Loaded += (s, e) => Init();

            if (Application.Current != null) Application.Current.Exit += (s, e) => Dispose();
        }

        ~NotifyIcon()
        {
            Dispose(false);
        }

        public void Init()
        {
            RegisterClass();
            if (Visibility == Visibility.Visible)
            {
                OnIconChanged();
                UpdateIcon(true);
            }

            _dispatcherTimerPos = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _dispatcherTimerPos.Tick += DispatcherTimerPos_Tick;
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(NotifyIcon), new PropertyMetadata(default(string)));

        public string Text
        {
            get => (string) GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            "Icon", typeof(ImageSource), typeof(NotifyIcon), new PropertyMetadata(default(ImageSource), OnIconChanged));

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (NotifyIcon)d;
            ctl._icon = (ImageSource)e.NewValue;
            ctl.OnIconChanged();
        }

        public ImageSource Icon
        {
            get => (ImageSource)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty ContextContentProperty = DependencyProperty.Register(
            "ContextContent", typeof(object), typeof(NotifyIcon), new PropertyMetadata(default(object)));

        public object ContextContent
        {
            get => GetValue(ContextContentProperty);
            set => SetValue(ContextContentProperty, value);
        }

        public static readonly DependencyProperty BlinkIntervalProperty = DependencyProperty.Register(
            "BlinkInterval", typeof(TimeSpan), typeof(NotifyIcon), new PropertyMetadata(TimeSpan.FromMilliseconds(500), OnBlinkIntervalChanged));

        private static void OnBlinkIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (NotifyIcon)d;
            if (ctl._dispatcherTimerBlink != null)
            {
                ctl._dispatcherTimerBlink.Interval = (TimeSpan) e.NewValue;
            }
        }

        public TimeSpan BlinkInterval
        {
            get => (TimeSpan) GetValue(BlinkIntervalProperty);
            set => SetValue(BlinkIntervalProperty, value);
        }

        public static readonly DependencyProperty IsBlinkProperty = DependencyProperty.Register(
            "IsBlink", typeof(bool), typeof(NotifyIcon), new PropertyMetadata(ValueBoxes.FalseBox, OnIsBlinkChanged));

        private static void OnIsBlinkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (NotifyIcon)d;
            if (ctl.Visibility != Visibility.Visible) return;
            if ((bool) e.NewValue)
            {
                if (ctl._dispatcherTimerBlink == null)
                {
                    ctl._dispatcherTimerBlink = new DispatcherTimer
                    {
                        Interval = ctl.BlinkInterval
                    };
                    ctl._dispatcherTimerBlink.Tick += ctl.DispatcherTimerBlinkTick;
                }
                ctl._dispatcherTimerBlink.Start();
            }
            else
            {
                ctl._dispatcherTimerBlink?.Stop();
                ctl._dispatcherTimerBlink = null;
                ctl.UpdateIcon(true);
            }
        }

        private void DispatcherTimerBlinkTick(object sender, EventArgs e)
        {
            if (Visibility != Visibility.Visible || _iconCurrentHandle == IntPtr.Zero) return;
            UpdateIcon(true, !_isTransparent);
        }

        private bool CheckMouseIsEnter()
        {
            var isTrue = FindNotifyIcon(out var rectNotify);
            if (!isTrue) return false;
            NativeMethods.GetCursorPos(out var point);
            if (point.X >= rectNotify.Left && point.X <= rectNotify.Right &&
                point.Y >= rectNotify.Top && point.Y <= rectNotify.Bottom)
            {
                return true;
            }

            return false;
        }

        private void DispatcherTimerPos_Tick(object sender, EventArgs e)
        {
            if (CheckMouseIsEnter())
            {
                if (!_isMouseOver)
                {
                    _isMouseOver = true;
                    RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                    {
                        RoutedEvent = MouseEnterEvent
                    });
                    _dispatcherTimerPos.Interval = TimeSpan.FromMilliseconds(500);
                }
            }
            else
            {
                _dispatcherTimerPos.Stop();
                _isMouseOver = false;
                RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                {
                    RoutedEvent = MouseLeaveEvent
                });
            }
        }

        //referenced from http://www.cnblogs.com/sczmzx/p/5158127.html
        private IntPtr FindTrayToolbarWindow()
        {
            var hWnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (hWnd != IntPtr.Zero)
            {
                hWnd = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "TrayNotifyWnd", null);
                if (hWnd != IntPtr.Zero)
                {

                    hWnd = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SysPager", null);
                    if (hWnd != IntPtr.Zero)
                    {
                        hWnd = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "ToolbarWindow32", null);

                    }
                }
            }
            return hWnd;
        }

        //referenced from http://www.cnblogs.com/sczmzx/p/5158127.html
        private IntPtr FindTrayToolbarOverFlowWindow()
        {
            var hWnd = NativeMethods.FindWindow("NotifyIconOverflowWindow", null);
            if (hWnd != IntPtr.Zero)
            {
                hWnd = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "ToolbarWindow32", null);
            }
            return hWnd;
        }

        private bool FindNotifyIcon(out NativeMethods.RECT rect)
        {
            var rectNotify = new NativeMethods.RECT();
            var hTrayWnd = FindTrayToolbarWindow();
            var isTrue = FindNotifyIcon(hTrayWnd, ref rectNotify);
            if (!isTrue)
            {
                hTrayWnd = FindTrayToolbarOverFlowWindow();
                isTrue = FindNotifyIcon(hTrayWnd, ref rectNotify);
            }
            rect = rectNotify;
            return isTrue;
        }

        //referenced from http://www.cnblogs.com/sczmzx/p/5158127.html
        private bool FindNotifyIcon(IntPtr hTrayWnd, ref NativeMethods.RECT rectNotify)
        {
            NativeMethods.GetWindowRect(hTrayWnd, out var rectTray);
            var count = (int)NativeMethods.SendMessage(hTrayWnd, NativeMethods.TB_BUTTONCOUNT, 0, IntPtr.Zero);

            var isFind = false;
            if (count > 0)
            {
                NativeMethods.GetWindowThreadProcessId(hTrayWnd, out var trayPid);
                var hProcess = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.VMOperation | NativeMethods.ProcessAccess.VMRead | NativeMethods.ProcessAccess.VMWrite, false, trayPid);
                var address = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, 1024, NativeMethods.AllocationType.Commit, NativeMethods.MemoryProtection.ReadWrite);

                var btnData = new NativeMethods.TBBUTTON();
                var trayData = new NativeMethods.TRAYDATA();
                var handel = Process.GetCurrentProcess().Id;

                for (uint i = 0; i < count; i++)
                {
                    NativeMethods.SendMessage(hTrayWnd, NativeMethods.TB_GETBUTTON, i, address);
                    var isTrue = NativeMethods.ReadProcessMemory(hProcess, address, out btnData, Marshal.SizeOf(btnData), out _);
                    if (!isTrue) continue;
                    if (btnData.dwData == IntPtr.Zero)
                    {
                        btnData.dwData = btnData.iString;
                    }
                    NativeMethods.ReadProcessMemory(hProcess, btnData.dwData, out trayData, Marshal.SizeOf(trayData), out _);
                    NativeMethods.GetWindowThreadProcessId(trayData.hwnd, out var dwProcessId);
                    if (dwProcessId == (uint)handel)
                    {
                        var rect = new NativeMethods.RECT();
                        var lngRect = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, Marshal.SizeOf(typeof(Rect)), NativeMethods.AllocationType.Commit, NativeMethods.MemoryProtection.ReadWrite);
                        NativeMethods.SendMessage(hTrayWnd, NativeMethods.TB_GETITEMRECT, i, lngRect);
                        NativeMethods.ReadProcessMemory(hProcess, lngRect, out rect, Marshal.SizeOf(rect), out _);

                        NativeMethods.VirtualFreeEx(hProcess, lngRect, Marshal.SizeOf(rect), NativeMethods.FreeType.Decommit);
                        NativeMethods.VirtualFreeEx(hProcess, lngRect, 0, NativeMethods.FreeType.Release);

                        var left = rectTray.Left + rect.Left;
                        var top = rectTray.Top + rect.Top;
                        var botton = rectTray.Top + rect.Bottom;
                        var right = rectTray.Left + rect.Right;
                        rectNotify = new NativeMethods.RECT
                        {
                            Left = left,
                            Right = right,
                            Top = top,
                            Bottom = botton
                        };
                        isFind = true;
                        break;
                    }
                }
                NativeMethods.VirtualFreeEx(hProcess, address, 0x4096, NativeMethods.FreeType.Decommit);
                NativeMethods.VirtualFreeEx(hProcess, address, 0, NativeMethods.FreeType.Release);
                NativeMethods.CloseHandle(hProcess);
            }
            return isFind;            
        }

        public bool IsBlink
        {
            get => (bool) GetValue(IsBlinkProperty);
            set => SetValue(IsBlinkProperty, value);
        }

        private void OnIconChanged()
        {
            if (_icon != null)
            {
                IconHelper.GetIconHandlesFromImageSource(_icon, out _, out _iconHandle);
                _iconCurrentHandle = _iconHandle.CriticalGetHandle();
            }
            else
            {
                if (_iconDefaultHandle == IntPtr.Zero)
                {
                    IconHelper.GetDefaultIconHandles(out _, out _iconHandle);
                    _iconDefaultHandle = _iconHandle.CriticalGetHandle();
                }
                _iconCurrentHandle = _iconDefaultHandle;
            }
        }

        private void UpdateIcon(bool showIconInTray, bool isTransparent = false)
        {
            lock (_syncObj)
            {
                if (DesignerHelper.IsInDesignMode) return;

                _isTransparent = isTransparent;
                var data = new NOTIFYICONDATA
                {
                    uCallbackMessage = WmTrayMouseMessage,
                    uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                    hWnd = _messageWindowHandle,
                    uID = _id,
                    dwInfoFlags = NativeMethods.NIF_TIP,
                    hIcon = isTransparent ? IntPtr.Zero : _iconCurrentHandle,
                    szTip = Text
                };

                if (showIconInTray)
                {
                    if (!_added)
                    {
                        UnsafeNativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, data);
                        _added = true;
                    }
                    else
                    {
                        UnsafeNativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, data);
                    }
                }
                else if (_added)
                {
                    UnsafeNativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, data);
                    _added = false;
                }
            }
        }

        private void RegisterClass()
        {
            _windowClassName = $"HandyControl.Controls.NotifyIcon{Guid.NewGuid()}";
            var wndclass = new WNDCLASS
            {
                style = 0,
                lpfnWndProc = _callback,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = IntPtr.Zero,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = "",
                lpszClassName = _windowClassName
            };

            UnsafeNativeMethods.RegisterClass(wndclass);
            _wmTaskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            _messageWindowHandle = UnsafeNativeMethods.CreateWindowEx(0, _windowClassName, "", 0, 0, 0, 1, 1,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        private IntPtr Callback(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam)
        {
            if (IsLoaded)
            {
                if (msg == _wmTaskbarCreated)
                {
                    UpdateIcon(true);
                }
                else
                {
                    switch (lparam.ToInt64())
                    {
                        case NativeMethods.WM_LBUTTONDBLCLK:
                            WmMouseDown(MouseButton.Left, 2);
                            break;
                        case NativeMethods.WM_LBUTTONUP:
                            WmMouseUp(MouseButton.Left);
                            break;
                        case NativeMethods.WM_RBUTTONUP:
                            ShowContextMenu();
                            WmMouseUp(MouseButton.Right);
                            break;
                        case NativeMethods.WM_MOUSEMOVE:
                            if (!_dispatcherTimerPos.IsEnabled)
                            {
                                _dispatcherTimerPos.Interval = TimeSpan.FromMilliseconds(200);
                                _dispatcherTimerPos.Start();
                            }
                            break;
                    }
                }
            }

            return UnsafeNativeMethods.DefWindowProc(hWnd, msg, wparam, lparam);
        }

        private void WmMouseDown(MouseButton button, int clicks)
        {
            if (clicks == 2)
            {
                RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
                {
                    RoutedEvent = MouseDoubleClickEvent
                });
                _doubleClick = true;
            }
        }

        private void WmMouseUp(MouseButton button)
        {
            if (!_doubleClick && button == MouseButton.Left)
            {
                RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
                {
                    RoutedEvent = ClickEvent
                });
            }
            _doubleClick = false;
        }

        private void ShowContextMenu()
        {

            if (ContextContent != null)
            {
                if (_contextContent == null)
                {
                    _contextContent = new Popup
                    {
                        Placement = PlacementMode.Mouse,
                        AllowsTransparency = true,
                        StaysOpen = false,
                        UseLayoutRounding = true,
                        SnapsToDevicePixels = true
                    };
                }

                _contextContent.Child = new ContentControl
                {
                    Content = ContextContent
                };
                _contextContent.IsOpen = true;
                var handle = IntPtr.Zero;
                var hwndSource = (HwndSource)PresentationSource.FromVisual(_contextContent.Child);
                if (hwndSource != null)
                {
                    handle = hwndSource.Handle;
                }
                UnsafeNativeMethods.SetForegroundWindow(handle);
            }
            else if (ContextMenu != null)
            {
                ContextMenu.Placement = PlacementMode.Mouse;
                ContextMenu.IsOpen = true;

                var handle = IntPtr.Zero;
                var hwndSource = (HwndSource)PresentationSource.FromVisual(ContextMenu);
                if (hwndSource != null)
                {
                    handle = hwndSource.Handle;
                }
                UnsafeNativeMethods.SetForegroundWindow(handle);
            }
        }

        public static readonly RoutedEvent ClickEvent =
            EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(NotifyIcon));

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        public static readonly RoutedEvent MouseDoubleClickEvent =
            EventManager.RegisterRoutedEvent("MouseDoubleClick", RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(NotifyIcon));

        public event RoutedEventHandler MouseDoubleClick
        {
            add => AddHandler(MouseDoubleClickEvent, value);
            remove => RemoveHandler(MouseDoubleClickEvent, value);
        }       

        private void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                if (_dispatcherTimerBlink != null && IsBlink)
                {
                    _dispatcherTimerBlink.Stop();
                }
                UpdateIcon(false);
            }

            _isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void CloseContextControl()
        {
            if (_contextContent != null)
            {
                _contextContent.IsOpen = false;
            }
            else if (ContextMenu != null)
            {
                ContextMenu.IsOpen = false;
            }
        }
    }
}
