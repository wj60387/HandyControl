using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HandyControl.Data;
using HandyControl.Expression.Drawing;
using HandyControl.Tools;
using HandyControl.Tools.Extension;
using HandyControl.Tools.Interop;

namespace HandyControl.Controls
{
    public class CustomChromeWindow : Window
    {
        private readonly GlowWindow[] _glowWindows = new GlowWindow[4];

        private Rect logicalSizeForRestore = Rect.Empty;

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(int), typeof(CustomChromeWindow),
                new FrameworkPropertyMetadata(ValueBoxes.Int0Box, OnCornerRadiusChanged));

        public static readonly DependencyProperty ActiveGlowColorProperty =
            DependencyProperty.Register(nameof(ActiveGlowColor), typeof(Color), typeof(CustomChromeWindow),
                new FrameworkPropertyMetadata(Colors.Transparent, OnGlowColorChanged));

        public static readonly DependencyProperty InactiveGlowColorProperty =
            DependencyProperty.Register(nameof(InactiveGlowColor), typeof(Color), typeof(CustomChromeWindow),
                new FrameworkPropertyMetadata(Colors.Transparent, OnGlowColorChanged));

        public static readonly DependencyProperty NonClientFillColorProperty =
            DependencyProperty.Register(nameof(NonClientFillColor), typeof(Color), typeof(CustomChromeWindow),
                new FrameworkPropertyMetadata(Colors.Black));

        private const int MinimizeAnimationDurationMilliseconds = 200;

        private int lastWindowPlacement;

        private int _deferGlowChangesCount;

        private bool _isGlowVisible;

        private DispatcherTimer _makeGlowVisibleTimer;

        private bool _isNonClientStripVisible;

        private IntPtr ownerForActivate;

        private bool useLogicalSizeForRestore;

        private bool updatingZOrder;

        static CustomChromeWindow()
        {
            ResizeModeProperty.OverrideMetadata(typeof(CustomChromeWindow), new FrameworkPropertyMetadata(OnResizeModeChanged));
        }

        public int CornerRadius
        {
            get => (int)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public Color ActiveGlowColor
        {
            get => (Color)GetValue(ActiveGlowColorProperty);
            set => SetValue(ActiveGlowColorProperty, value);
        }

        public Color InactiveGlowColor
        {
            get => (Color)GetValue(InactiveGlowColorProperty);
            set => SetValue(InactiveGlowColorProperty, value);
        }

        public Color NonClientFillColor
        {
            get => (Color)GetValue(NonClientFillColorProperty);
            set => SetValue(NonClientFillColorProperty, value);
        }

        protected override void OnActivated(EventArgs e)
        {
            UpdateGlowActiveState();
            base.OnActivated(e);
        }

        protected override void OnDeactivated(EventArgs e)
        {
            UpdateGlowActiveState();
            base.OnDeactivated(e);
        }

        private static void OnResizeModeChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) => ((CustomChromeWindow)obj).UpdateGlowVisibility(false);

        private static void OnCornerRadiusChanged( DependencyObject obj, DependencyPropertyChangedEventArgs args) => ((CustomChromeWindow)obj).UpdateClipRegion();

        private static void OnGlowColorChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) => ((CustomChromeWindow)obj).UpdateGlowColors();

        protected override void OnSourceInitialized(EventArgs e)
        {
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(HwndSourceHook);
            CreateGlowWindowHandles();
            base.OnSourceInitialized(e);
        }

        private void CreateGlowWindowHandles()
        {
            for (var direction = 0; direction < _glowWindows.Length; ++direction)
                GetOrCreateGlowWindow(direction).EnsureHandle();
        }

        protected virtual IntPtr HwndSourceHook(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 6:
                    WmActivate(wParam, lParam);
                    break;
                case 12:
                case 128:
                    return CallDefWindowProcWithoutRedraw(hWnd, msg, wParam, lParam, ref handled);
                case 70:
                    WmWindowPosChanging(hWnd, lParam);
                    break;
                case 71:
                    WmWindowPosChanged(hWnd, lParam);
                    break;
                case 131:
                    return WmNcCalcSize(hWnd, wParam, lParam, ref handled);
                case 132:
                    return WmNcHitTest(hWnd, lParam, ref handled);
                case 133:
                    return WmNcPaint(hWnd, wParam, lParam, ref handled);
                case 134:
                    return WmNcActivate(hWnd, wParam, lParam, ref handled);
                case 164:
                case 165:
                case 166:
                    RaiseNonClientMouseMessageAsClient(hWnd, msg, wParam, lParam);
                    handled = true;
                    break;
                case 174:
                case 175:
                    handled = true;
                    break;
                case 274:
                    WmSysCommand(hWnd, wParam, lParam);
                    break;
            }
            return IntPtr.Zero;
        }

        private static void RaiseNonClientMouseMessageAsClient(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            var point = new NativeMethods.POINT
            {
                X = NativeMethods.GetXLParam(lParam.ToInt32()),
                Y = NativeMethods.GetYLParam(lParam.ToInt32())
            };
            NativeMethods.ScreenToClient(hWnd, ref point);
            NativeMethods.SendMessage(hWnd, msg + 513 - 161, new IntPtr(PressedMouseButtons), NativeMethods.MakeParam(point.X, point.Y));
        }

        private static int PressedMouseButtons
        {
            get
            {
                var num = 0;
                if (NativeMethods.IsKeyPressed(1))
                    num |= 1;
                if (NativeMethods.IsKeyPressed(2))
                    num |= 2;
                if (NativeMethods.IsKeyPressed(4))
                    num |= 16;
                if (NativeMethods.IsKeyPressed(5))
                    num |= 32;
                if (NativeMethods.IsKeyPressed(6))
                    num |= 64;
                return num;
            }
        }

        // ReSharper disable once RedundantAssignment
        private IntPtr CallDefWindowProcWithoutRedraw(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            using (new SuppressRedrawScope(hWnd))
            {
                handled = true;
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }

        private void WmActivate(IntPtr wParam, IntPtr lParam)
        {
            if (ownerForActivate == IntPtr.Zero) return;
            NativeMethods.SendMessage(ownerForActivate, NativeMethods.NOTIFYOWNERACTIVATE, wParam, lParam);
        }

        // ReSharper disable once RedundantAssignment
        private IntPtr WmNcActivate(IntPtr hWnd, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = true;
            return NativeMethods.DefWindowProc(hWnd, 134, wParam, NativeMethods.HRGN_NONE);
        }

        private IntPtr WmNcPaint(IntPtr hWnd, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_isNonClientStripVisible)
            {
                var hrgnClip = wParam == new IntPtr(1) ? IntPtr.Zero : wParam;
                var dcEx = NativeMethods.GetDCEx(hWnd, hrgnClip, 155);
                if (dcEx != IntPtr.Zero)
                {
                    try
                    {
                        var nonClientFillColor = NonClientFillColor;
                        var solidBrush = NativeMethods.CreateSolidBrush(nonClientFillColor.B << 16 | nonClientFillColor.G << 8 | nonClientFillColor.R);
                        try
                        {
                            var relativeToWindowRect = GetClientRectRelativeToWindowRect(hWnd);
                            relativeToWindowRect.Top = relativeToWindowRect.Bottom;
                            relativeToWindowRect.Bottom = relativeToWindowRect.Top + 1;
                            NativeMethods.FillRect(dcEx, ref relativeToWindowRect, solidBrush);
                        }
                        finally
                        {
                            UnsafeNativeMethods.DeleteObject(solidBrush);
                        }
                    }
                    finally
                    {
                        NativeMethods.ReleaseDC(hWnd, dcEx);
                    }
                }
            }
            handled = true;
            return IntPtr.Zero;
        }

        private static NativeMethods.RECT GetClientRectRelativeToWindowRect(IntPtr hWnd)
        {
            NativeMethods.GetWindowRect(hWnd, out var lpRect1);
            NativeMethods.GetClientRect(hWnd, out var lpRect2);
            var point = new NativeMethods.POINT
            {
                X = 0,
                Y = 0
            };
            NativeMethods.ClientToScreen(hWnd, ref point);
            lpRect2.Offset(point.X - lpRect1.Left, point.Y - lpRect1.Top);
            return lpRect2;
        }

        // ReSharper disable once RedundantAssignment
        private IntPtr WmNcCalcSize(IntPtr hWnd, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            _isNonClientStripVisible = false;
            if (NativeMethods.GetWindowPlacement(hWnd).showCmd == 3)
            {
                var structure1 = (NativeMethods.RECT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.RECT));
                NativeMethods.DefWindowProc(hWnd, 131, wParam, lParam);
                var structure2 = (NativeMethods.RECT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.RECT));
                var monitorinfo = MonitorInfoFromWindow(hWnd);
                if (monitorinfo.rcMonitor.Height == monitorinfo.rcWork.Height && monitorinfo.rcMonitor.Width == monitorinfo.rcWork.Width)
                {
                    _isNonClientStripVisible = true;
                    --structure2.Bottom;
                }
                structure2.Top = structure1.Top + (int)GetWindowInfo(hWnd).cyWindowBorders;
                Marshal.StructureToPtr(structure2, lParam, true);
            }
            handled = true;
            return IntPtr.Zero;
        }

        private IntPtr WmNcHitTest(IntPtr hWnd, IntPtr lParam, ref bool handled)
        {
            if (!this.IsConnectedToPresentationSource())
                return new IntPtr(0);
            var point1 = new Point(NativeMethods.GetXLParam(lParam.ToInt32()), NativeMethods.GetYLParam(lParam.ToInt32()));
            var point2 = PointFromScreen(point1);
            DependencyObject visualHit = null;
            VisualHelper.HitTestVisibleElements(this, target =>
            {
                visualHit = target.VisualHit;
                return HitTestResultBehavior.Stop;
            }, new PointHitTestParameters(point2));

            var num = 0;

            for (; visualHit != null; visualHit = visualHit.GetVisualOrLogicalParent())
            {
                if (visualHit is INonClientArea nonClientArea)
                {
                    num = nonClientArea.HitTest(point1);
                    if (num != 0)
                        break;
                }
            }

            if (num == 0) num = 1;
            handled = true;
            return new IntPtr(num);
        }

        private void WmSysCommand(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
        {
            var scWparam = NativeMethods.GET_SC_WPARAM(wParam);
            if (scWparam == 61456)
                NativeMethods.RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, NativeMethods.RedrawWindowFlags.Invalidate | NativeMethods.RedrawWindowFlags.NoChildren | NativeMethods.RedrawWindowFlags.UpdateNow | NativeMethods.RedrawWindowFlags.Frame);
            if ((scWparam == 61488 || scWparam == 61472 || scWparam == 61456 || scWparam == 61440) && WindowState == WindowState.Normal && !IsAeroSnappedToMonitor(hWnd))
                logicalSizeForRestore = new Rect(Left, Top, Width, Height);
            if (scWparam == 61456 && WindowState == WindowState.Maximized && logicalSizeForRestore == Rect.Empty)
                logicalSizeForRestore = new Rect(Left, Top, Width, Height);
            if (scWparam != 61728 || WindowState == WindowState.Minimized || (logicalSizeForRestore.Width <= 0.0 || logicalSizeForRestore.Height <= 0.0))
                return;
            Left = logicalSizeForRestore.Left;
            Top = logicalSizeForRestore.Top;
            Width = logicalSizeForRestore.Width;
            Height = logicalSizeForRestore.Height;
            useLogicalSizeForRestore = true;
        }

        private bool IsAeroSnappedToMonitor(IntPtr hWnd)
        {
            var monitorinfo = MonitorInfoFromWindow(hWnd);
            var deviceUnits = new Rect(Left, Top, Width, Height).LogicalToDeviceUnits();
            return MathHelper.AreClose(monitorinfo.rcWork.Height, deviceUnits.Height) && MathHelper.AreClose(monitorinfo.rcWork.Top, deviceUnits.Top);
        }

        private void WmWindowPosChanging(IntPtr hwnd, IntPtr lParam)
        {
            var structure = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
            if (((int)structure.flags & 2) != 0 || ((int)structure.flags & 1) != 0 || structure.cx <= 0 || structure.cy <= 0)
                return;
            var floatRect = new Rect(structure.x, structure.y, structure.cx, structure.cy).DeviceToLogicalUnits();
            if (useLogicalSizeForRestore)
            {
                floatRect = logicalSizeForRestore;
                logicalSizeForRestore = Rect.Empty;
                useLogicalSizeForRestore = false;
            }
            var deviceUnits = ArithmeticHelper.GetOnScreenPosition(floatRect).LogicalToDeviceUnits();
            structure.x = (int)deviceUnits.X;
            structure.y = (int)deviceUnits.Y;
            Marshal.StructureToPtr(structure, lParam, true);
        }

        private void WmWindowPosChanged(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                var structure = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                var windowPlacement = NativeMethods.GetWindowPlacement(hWnd);
                var currentBounds = new NativeMethods.RECT(structure.x, structure.y, structure.x + structure.cx, structure.y + structure.cy);
                if (((int)structure.flags & 1) != 1)
                    UpdateClipRegion(hWnd, windowPlacement, ClipRegionChangeType.FromSize, currentBounds);
                else if (((int)structure.flags & 2) != 2)
                    UpdateClipRegion(hWnd, windowPlacement, ClipRegionChangeType.FromPosition, currentBounds);
                OnWindowPosChanged(hWnd, windowPlacement.showCmd, windowPlacement.rcNormalPosition.ToInt32Rect());
                UpdateGlowWindowPositions(((int)structure.flags & 64) == 0);
                UpdateZOrderOfThisAndOwner();
            }
            catch
            {
                // ignored
            }
        }

        private void UpdateZOrderOfThisAndOwner()
        {
            if (updatingZOrder) return;
            try
            {
                updatingZOrder = true;
                var windowInteropHelper = new WindowInteropHelper(this);
                var handle = windowInteropHelper.Handle;
                foreach (var loadedGlowWindow in LoadedGlowWindows)
                {
                    if (NativeMethods.GetWindow(loadedGlowWindow.Handle, 3) != handle)
                        NativeMethods.SetWindowPos(loadedGlowWindow.Handle, handle, 0, 0, 0, 0, 19);
                    handle = loadedGlowWindow.Handle;
                }
                var owner = windowInteropHelper.Owner;
                if (!(owner != IntPtr.Zero))
                    return;
                UpdateZOrderOfOwner(owner);
            }
            finally
            {
                updatingZOrder = false;
            }
        }

        private void UpdateZOrderOfOwner(IntPtr hwndOwner)
        {
            var lastOwnedWindow = IntPtr.Zero;
            NativeMethods.EnumThreadWindows(NativeMethods.GetCurrentThreadId(), (hwnd, lParam) =>
            {
                if (NativeMethods.GetWindow(hwnd, 4) == hwndOwner)
                    lastOwnedWindow = hwnd;
                return true;
            }, IntPtr.Zero);
            if (!(lastOwnedWindow != IntPtr.Zero) || !(NativeMethods.GetWindow(hwndOwner, 3) != lastOwnedWindow))
                return;
            NativeMethods.SetWindowPos(hwndOwner, lastOwnedWindow, 0, 0, 0, 0, 19);
        }

        protected virtual void OnWindowPosChanged(IntPtr hWnd, int showCmd, Int32Rect rcNormalPosition)
        {
        }

        protected void UpdateClipRegion(ClipRegionChangeType regionChangeType = ClipRegionChangeType.FromPropertyChange)
        {
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (hwndSource == null)
                return;
            NativeMethods.GetWindowRect(hwndSource.Handle, out var lpRect);
            var windowPlacement = NativeMethods.GetWindowPlacement(hwndSource.Handle);
            UpdateClipRegion(hwndSource.Handle, windowPlacement, regionChangeType, lpRect);
        }

        private void UpdateClipRegion(IntPtr hWnd, WINDOWPLACEMENT placement, ClipRegionChangeType changeType, NativeMethods.RECT currentBounds)
        {
            UpdateClipRegionCore(hWnd, placement.showCmd, changeType, currentBounds.ToInt32Rect());
            lastWindowPlacement = placement.showCmd;
        }

        protected virtual bool UpdateClipRegionCore(IntPtr hWnd, int showCmd, ClipRegionChangeType changeType, Int32Rect currentBounds)
        {
            if (showCmd == 3)
            {
                UpdateMaximizedClipRegion(hWnd);
                return true;
            }
            if (changeType != ClipRegionChangeType.FromSize && changeType != ClipRegionChangeType.FromPropertyChange && lastWindowPlacement == showCmd)
                return false;
            if (CornerRadius < 0)
                ClearClipRegion(hWnd);
            else
                SetRoundRect(hWnd, currentBounds.Width, currentBounds.Height);
            return true;
        }

        private WINDOWINFO GetWindowInfo(IntPtr hWnd)
        {
            var pwi = new WINDOWINFO();
            pwi.cbSize = Marshal.SizeOf(pwi);
            NativeMethods.GetWindowInfo(hWnd, ref pwi);
            return pwi;
        }

        private void UpdateMaximizedClipRegion(IntPtr hWnd)
        {
            var relativeToWindowRect = GetClientRectRelativeToWindowRect(hWnd);
            if (_isNonClientStripVisible)
                ++relativeToWindowRect.Bottom;
            var rectRgnIndirect = NativeMethods.CreateRectRgnIndirect(ref relativeToWindowRect);
            NativeMethods.SetWindowRgn(hWnd, rectRgnIndirect, NativeMethods.IsWindowVisible(hWnd));
        }

        private static MONITORINFO MonitorInfoFromWindow(IntPtr hWnd)
        {
            var hMonitor = NativeMethods.MonitorFromWindow(hWnd, 2);
            var monitorInfo = new MONITORINFO
            {
                cbSize = (uint) Marshal.SizeOf(typeof(MONITORINFO))
            };
            NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo);
            return monitorInfo;
        }

        private void ClearClipRegion(IntPtr hWnd) => NativeMethods.SetWindowRgn(hWnd, IntPtr.Zero, NativeMethods.IsWindowVisible(hWnd));

        protected void SetRoundRect(IntPtr hWnd, int width, int height)
        {
            var roundRectRegion = ComputeRoundRectRegion(0, 0, width, height, CornerRadius);
            NativeMethods.SetWindowRgn(hWnd, roundRectRegion, NativeMethods.IsWindowVisible(hWnd));
        }

        private IntPtr ComputeRoundRectRegion(int left, int top, int width, int height, int cornerRadius)
        {
            var nWidthEllipse = (int)(2 * cornerRadius * (VisualHelper.DpiX / 96.0));
            var nHeightEllipse = (int)(2 * cornerRadius * (VisualHelper.Dpi / 96.0));
            return NativeMethods.CreateRoundRectRgn(left, top, left + width + 1, top + height + 1, nWidthEllipse, nHeightEllipse);
        }

        protected IntPtr ComputeCornerRadiusRectRegion(Int32Rect rect, CornerRadius cornerRadius)
        {
            if (MathHelper.AreClose(cornerRadius.TopLeft, cornerRadius.TopRight) && MathHelper.AreClose(cornerRadius.TopLeft, cornerRadius.BottomLeft) && MathHelper.AreClose(cornerRadius.BottomLeft, cornerRadius.BottomRight))
                return ComputeRoundRectRegion(rect.X, rect.Y, rect.Width, rect.Height, (int)cornerRadius.TopLeft);
            var num1 = IntPtr.Zero;
            var num2 = IntPtr.Zero;
            var num3 = IntPtr.Zero;
            var num4 = IntPtr.Zero;
            var num5 = IntPtr.Zero;
            var num6 = IntPtr.Zero;
            var num7 = IntPtr.Zero;
            var num8 = IntPtr.Zero;
            var num9 = IntPtr.Zero;
            // ReSharper disable once RedundantAssignment
            var num10 = IntPtr.Zero;
            try
            {
                num1 = ComputeRoundRectRegion(rect.X, rect.Y, rect.Width, rect.Height, (int)cornerRadius.TopLeft);
                num2 = ComputeRoundRectRegion(rect.X, rect.Y, rect.Width, rect.Height, (int)cornerRadius.TopRight);
                num3 = ComputeRoundRectRegion(rect.X, rect.Y, rect.Width, rect.Height, (int)cornerRadius.BottomLeft);
                num4 = ComputeRoundRectRegion(rect.X, rect.Y, rect.Width, rect.Height, (int)cornerRadius.BottomRight);
                var point = new NativeMethods.POINT
                {
                    X = rect.X + rect.Width / 2,
                    Y = rect.Y + rect.Height / 2
                };
                num5 = NativeMethods.CreateRectRgn(rect.X, rect.Y, point.X + 1, point.Y + 1);
                num6 = NativeMethods.CreateRectRgn(point.X - 1, rect.Y, rect.X + rect.Width, point.Y + 1);
                num7 = NativeMethods.CreateRectRgn(rect.X, point.Y - 1, point.X + 1, rect.Y + rect.Height);
                num8 = NativeMethods.CreateRectRgn(point.X - 1, point.Y - 1, rect.X + rect.Width, rect.Y + rect.Height);
                num9 = NativeMethods.CreateRectRgn(0, 0, 1, 1);
                num10 = NativeMethods.CreateRectRgn(0, 0, 1, 1);
                NativeMethods.CombineRgn(num10, num1, num5, NativeMethods.CombineMode.RGN_AND);
                NativeMethods.CombineRgn(num9, num2, num6, NativeMethods.CombineMode.RGN_AND);
                NativeMethods.CombineRgn(num10, num10, num9, NativeMethods.CombineMode.RGN_OR);
                NativeMethods.CombineRgn(num9, num3, num7, NativeMethods.CombineMode.RGN_AND);
                NativeMethods.CombineRgn(num10, num10, num9, NativeMethods.CombineMode.RGN_OR);
                NativeMethods.CombineRgn(num9, num4, num8, NativeMethods.CombineMode.RGN_AND);
                NativeMethods.CombineRgn(num10, num10, num9, NativeMethods.CombineMode.RGN_OR);
            }
            finally
            {
                if (num1 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num1);
                if (num2 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num2);
                if (num3 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num3);
                if (num4 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num4);
                if (num5 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num5);
                if (num6 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num6);
                if (num7 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num7);
                if (num8 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num8);
                if (num9 != IntPtr.Zero)
                    UnsafeNativeMethods.DeleteObject(num9);
            }
            return num10;
        }

        public static void ShowWindowMenu(HwndSource source, Visual element, Point elementPoint, Size elementSize)
        {
            if (elementPoint.X < 0.0 || elementPoint.X > elementSize.Width || elementPoint.Y < 0.0 || elementPoint.Y > elementSize.Height)
                return;
            var screen = element.PointToScreen(elementPoint);
            ShowWindowMenu(source, screen, true);
        }

        protected static void ShowWindowMenu(HwndSource source, Point screenPoint, bool canMinimize)
        {
            var systemMetrics = NativeMethods.GetSystemMetrics(40);
            var systemMenu = NativeMethods.GetSystemMenu(source.Handle, false);
            var windowPlacement = NativeMethods.GetWindowPlacement(source.Handle);
            using (new SuppressRedrawScope(source.Handle))
            {
                var num = canMinimize ? 0U : 1U;
                if (windowPlacement.showCmd == 1)
                {
                    NativeMethods.EnableMenuItem(systemMenu, 61728U, 1U);
                    NativeMethods.EnableMenuItem(systemMenu, 61456U, 0U);
                    NativeMethods.EnableMenuItem(systemMenu, 61440U, 0U);
                    NativeMethods.EnableMenuItem(systemMenu, 61488U, 0U);
                    NativeMethods.EnableMenuItem(systemMenu, 61472U, 0U | num);
                    NativeMethods.EnableMenuItem(systemMenu, 61536U, 0U);
                }
                else if (windowPlacement.showCmd == 3)
                {
                    NativeMethods.EnableMenuItem(systemMenu, 61728U, 0U);
                    NativeMethods.EnableMenuItem(systemMenu, 61456U, 1U);
                    NativeMethods.EnableMenuItem(systemMenu, 61440U, 1U);
                    NativeMethods.EnableMenuItem(systemMenu, 61488U, 1U);
                    NativeMethods.EnableMenuItem(systemMenu, 61472U, 0U | num);
                    NativeMethods.EnableMenuItem(systemMenu, 61536U, 0U);
                }
            }
            var fuFlags = (uint)(systemMetrics | 256 | 128 | 2);
            var num1 = NativeMethods.TrackPopupMenuEx(systemMenu, fuFlags, (int)screenPoint.X, (int)screenPoint.Y, source.Handle, IntPtr.Zero);
            if (num1 == 0)
                return;
            NativeMethods.PostMessage(source.Handle, 274, new IntPtr(num1), IntPtr.Zero);
        }

        protected override void OnClosed(EventArgs e)
        {
            StopTimer();
            DestroyGlowWindows();
            base.OnClosed(e);
        }

        private bool IsGlowVisible
        {
            get => _isGlowVisible;
            set
            {
                if (_isGlowVisible == value)
                    return;
                _isGlowVisible = value;
                for (var direction = 0; direction < _glowWindows.Length; ++direction)
                    GetOrCreateGlowWindow(direction).IsVisible = value;
            }
        }

        private GlowWindow GetOrCreateGlowWindow(int direction)
        {
            return _glowWindows[direction] ?? (_glowWindows[direction] = new GlowWindow(this, (Dock) direction)
            {
                ActiveGlowColor = ActiveGlowColor,
                InactiveGlowColor = InactiveGlowColor,
                IsActive = IsActive
            });
        }

        private IEnumerable<GlowWindow> LoadedGlowWindows => _glowWindows.Where(w => w != null);

        private void DestroyGlowWindows()
        {
            for (var index = 0; index < _glowWindows.Length; ++index)
            {
                using (_glowWindows[index])
                    _glowWindows[index] = null;
            }
        }

        private void UpdateGlowWindowPositions(bool delayIfNecessary)
        {
            using (DeferGlowChanges())
            {
                UpdateGlowVisibility(delayIfNecessary);
                foreach (var loadedGlowWindow in LoadedGlowWindows)
                    loadedGlowWindow.UpdateWindowPos();
            }
        }

        private void UpdateGlowActiveState()
        {
            using (DeferGlowChanges())
            {
                foreach (var loadedGlowWindow in LoadedGlowWindows)
                    loadedGlowWindow.IsActive = IsActive;
            }
        }

        public void ChangeOwnerForActivate(IntPtr newOwner) => ownerForActivate = newOwner;

        public void ChangeOwner(IntPtr newOwner)
        {
            new WindowInteropHelper(this).Owner = newOwner;
            foreach (var loadedGlowWindow in LoadedGlowWindows)
                loadedGlowWindow.ChangeOwner(newOwner);
            UpdateZOrderOfThisAndOwner();
        }

        private void UpdateGlowVisibility(bool delayIfNecessary)
        {
            var shouldShowGlow = ShouldShowGlow;
            if (shouldShowGlow == IsGlowVisible)
                return;
            if (SystemParameters.MinimizeAnimation & shouldShowGlow & delayIfNecessary)
            {
                if (_makeGlowVisibleTimer != null)
                {
                    _makeGlowVisibleTimer.Stop();
                }
                else
                {
                    _makeGlowVisibleTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(MinimizeAnimationDurationMilliseconds)
                    };
                    _makeGlowVisibleTimer.Tick += OnDelayedVisibilityTimerTick;
                }
                _makeGlowVisibleTimer.Start();
            }
            else
            {
                StopTimer();
                IsGlowVisible = shouldShowGlow;
            }
        }

        protected virtual bool ShouldShowGlow
        {
            get
            {
                var handle = new WindowInteropHelper(this).Handle;
                if (NativeMethods.IsWindowVisible(handle) && !NativeMethods.IsIconic(handle) && !NativeMethods.IsZoomed(handle))
                    return (uint)ResizeMode > 0U;
                return false;
            }
        }

        private void StopTimer()
        {
            if (_makeGlowVisibleTimer == null)
                return;
            _makeGlowVisibleTimer.Stop();
            _makeGlowVisibleTimer.Tick -= OnDelayedVisibilityTimerTick;
            _makeGlowVisibleTimer = null;
        }

        private void OnDelayedVisibilityTimerTick(object sender, EventArgs e)
        {
            StopTimer();
            UpdateGlowWindowPositions(false);
        }

        private void UpdateGlowColors()
        {
            using (DeferGlowChanges())
            {
                foreach (var loadedGlowWindow in LoadedGlowWindows)
                {
                    loadedGlowWindow.ActiveGlowColor = ActiveGlowColor;
                    loadedGlowWindow.InactiveGlowColor = InactiveGlowColor;
                }
            }
        }

        private IDisposable DeferGlowChanges() => new ChangeScope(this);

        private void EndDeferGlowChanges()
        {
            foreach (var loadedGlowWindow in LoadedGlowWindows)
                loadedGlowWindow.CommitChanges();
        }

        protected enum ClipRegionChangeType
        {
            FromSize,
            FromPosition,
            FromPropertyChange,
            FromUndockSingleTab,
        }

        private class SuppressRedrawScope : IDisposable
        {
            private readonly IntPtr _hwnd;

            private readonly bool _suppressedRedraw;

            public SuppressRedrawScope(IntPtr hwnd)
            {
                _hwnd = hwnd;
                if ((NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL.STYLE) & 268435456) == 0)
                    return;
                SetRedraw(false);
                _suppressedRedraw = true;
            }

            public void Dispose()
            {
                if (!_suppressedRedraw)
                    return;
                SetRedraw(true);
                var flags = NativeMethods.RedrawWindowFlags.Invalidate |
                            NativeMethods.RedrawWindowFlags.AllChildren |
                            NativeMethods.RedrawWindowFlags.Frame;
                NativeMethods.RedrawWindow(_hwnd, IntPtr.Zero, IntPtr.Zero, flags);
            }

            private void SetRedraw(bool state) => NativeMethods.SendMessage(_hwnd, 11, new IntPtr(Convert.ToInt32(state)));
        }

        private class ChangeScope : DisposableObject
        {
            private readonly CustomChromeWindow _window;

            public ChangeScope(CustomChromeWindow window)
            {
                _window = window;
                ++_window._deferGlowChangesCount;
            }

            protected override void DisposeManagedResources()
            {
                --_window._deferGlowChangesCount;
                if (_window._deferGlowChangesCount != 0)
                    return;
                _window.EndDeferGlowChanges();
            }
        }

        private sealed class GlowBitmap : DisposableObject
        {
            private static readonly CachedBitmapInfo[] _transparencyMasks = new CachedBitmapInfo[GlowBitmapPartCount];

            public const int GlowBitmapPartCount = 16;

            private const int BytesPerPixelBgra32 = 4;

            private readonly IntPtr _pbits;

            private readonly BITMAPINFO _bitmapInfo;

            public GlowBitmap(IntPtr hdcScreen, int width, int height)
            {
                _bitmapInfo.bmiHeader_biSize = Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER));
                _bitmapInfo.bmiHeader_biPlanes = 1;
                _bitmapInfo.bmiHeader_biBitCount = 32;
                _bitmapInfo.bmiHeader_biCompression = 0;
                _bitmapInfo.bmiHeader_biXPelsPerMeter = 0;
                _bitmapInfo.bmiHeader_biYPelsPerMeter = 0;
                _bitmapInfo.bmiHeader_biWidth = width;
                _bitmapInfo.bmiHeader_biHeight = -height;
                Handle = NativeMethods.CreateDIBSection(hdcScreen, ref _bitmapInfo, 0U, out _pbits, IntPtr.Zero, 0U);
            }

            public IntPtr Handle { get; }

            private IntPtr DIBits => _pbits;

            public int Width => _bitmapInfo.bmiHeader_biWidth;

            public int Height => -_bitmapInfo.bmiHeader_biHeight;

            protected override void DisposeNativeResources() => UnsafeNativeMethods.DeleteObject(Handle);

            private static byte PremultiplyAlpha(byte channel, byte alpha) => (byte)(channel * alpha / (double)byte.MaxValue);

            public static GlowBitmap Create(GlowDrawingContext drawingContext, GlowBitmapPart bitmapPart, Color color)
            {
                var alphaMask = GetOrCreateAlphaMask(bitmapPart);
                var glowBitmap = new GlowBitmap(drawingContext.ScreenDC, alphaMask.Width, alphaMask.Height);
                var ofs = 0;
                var diBit = alphaMask.DIBits[ofs + 3];
                while (ofs < alphaMask.DIBits.Length)
                {
                    var val1 = PremultiplyAlpha(color.R, diBit);
                    var val2 = PremultiplyAlpha(color.G, diBit);
                    var val3 = PremultiplyAlpha(color.B, diBit);
                    Marshal.WriteByte(glowBitmap.DIBits, ofs, val3);
                    Marshal.WriteByte(glowBitmap.DIBits, ofs + 1, val2);
                    Marshal.WriteByte(glowBitmap.DIBits, ofs + 2, val1);
                    Marshal.WriteByte(glowBitmap.DIBits, ofs + 3, diBit);
                    ofs += BytesPerPixelBgra32;
                }
                return glowBitmap;
            }

            private static CachedBitmapInfo GetOrCreateAlphaMask(GlowBitmapPart bitmapPart)
            {
                var index = (int)bitmapPart;
                if (_transparencyMasks[index] == null)
                {
                    var bitmapImage = new BitmapImage(ResourceHelper.MakePackUri(typeof(GlowBitmap).Assembly, $"Resources/{bitmapPart}.png"));
                    var diBits = new byte[BytesPerPixelBgra32 * bitmapImage.PixelWidth * bitmapImage.PixelHeight];
                    var stride = BytesPerPixelBgra32 * bitmapImage.PixelWidth;
                    bitmapImage.CopyPixels(diBits, stride, 0);
                    _transparencyMasks[index] = new CachedBitmapInfo(diBits, bitmapImage.PixelWidth, bitmapImage.PixelHeight);
                }
                return _transparencyMasks[index];
            }

            private sealed class CachedBitmapInfo
            {
                public readonly int Width;
                public readonly int Height;
                public readonly byte[] DIBits;

                public CachedBitmapInfo(byte[] diBits, int width, int height)
                {
                    Width = width;
                    Height = height;
                    DIBits = diBits;
                }
            }
        }

        private enum GlowBitmapPart
        {
            CornerTopLeft,
            CornerTopRight,
            CornerBottomLeft,
            CornerBottomRight,
            TopLeft,
            Top,
            TopRight,
            LeftTop,
            Left,
            LeftBottom,
            BottomLeft,
            Bottom,
            BottomRight,
            RightTop,
            Right,
            RightBottom,
        }

        private sealed class GlowDrawingContext : DisposableObject
        {
            public NativeMethods.BLENDFUNCTION Blend;

            private readonly GlowBitmap _windowBitmap;

            public GlowDrawingContext(int width, int height)
            {
                ScreenDC = NativeMethods.GetDC(IntPtr.Zero);
                if (ScreenDC == IntPtr.Zero)
                    return;
                WindowDC = NativeMethods.CreateCompatibleDC(ScreenDC);
                if (WindowDC == IntPtr.Zero)
                    return;
                BackgroundDC = NativeMethods.CreateCompatibleDC(ScreenDC);
                if (BackgroundDC == IntPtr.Zero)
                    return;
                Blend.BlendOp = 0;
                Blend.BlendFlags = 0;
                Blend.SourceConstantAlpha = byte.MaxValue;
                Blend.AlphaFormat = 1;
                _windowBitmap = new GlowBitmap(ScreenDC, width, height);
                NativeMethods.SelectObject(WindowDC, _windowBitmap.Handle);
            }

            public bool IsInitialized
            {
                get
                {
                    if (ScreenDC != IntPtr.Zero && WindowDC != IntPtr.Zero && BackgroundDC != IntPtr.Zero)
                        return _windowBitmap != null;
                    return false;
                }
            }

            public IntPtr ScreenDC { get; }

            public IntPtr WindowDC { get; }

            public IntPtr BackgroundDC { get; }

            public int Width => _windowBitmap?.Width ?? 0;

            public int Height => _windowBitmap?.Height ?? 0;

            protected override void DisposeManagedResources() => _windowBitmap?.Dispose();

            protected override void DisposeNativeResources()
            {
                if (ScreenDC != IntPtr.Zero)
                    NativeMethods.ReleaseDC(IntPtr.Zero, ScreenDC);
                if (WindowDC != IntPtr.Zero)
                    NativeMethods.DeleteDC(WindowDC);
                if (!(BackgroundDC != IntPtr.Zero))
                    return;
                NativeMethods.DeleteDC(BackgroundDC);
            }
        }

        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private sealed class GlowWindow : HwndWrapper
        {
            private readonly GlowBitmap[] _activeGlowBitmaps = new GlowBitmap[GlowBitmap.GlowBitmapPartCount];

            private readonly GlowBitmap[] _inactiveGlowBitmaps = new GlowBitmap[GlowBitmap.GlowBitmapPartCount];

            private Color _activeGlowColor = Colors.Transparent;

            private Color _inactiveGlowColor = Colors.Transparent;

            private const string GlowWindowClassName = "VisualStudioGlowWindow";

            private const int GlowDepth = 9;

            private const int CornerGripThickness = 18;

            private readonly CustomChromeWindow _targetWindow;

            private readonly Dock _orientation;

            private static short _sharedWindowClassAtom;

            private static WndProc _sharedWndProc;

            private static long _createdGlowWindows;

            private static long _disposedGlowWindows;

            private int _left;

            private int _top;

            private int _width;

            private int _height;

            private bool _isVisible;

            private bool _isActive;

            private FieldInvalidationTypes _invalidatedValues;

            private bool _pendingDelayRender;

            public GlowWindow(CustomChromeWindow owner, Dock orientation)
            {
                _targetWindow = owner ?? throw new ArgumentNullException(nameof(owner));
                _orientation = orientation;
                ++_createdGlowWindows;
            }

            private bool IsDeferringChanges => _targetWindow._deferGlowChangesCount > 0;

            private static short SharedWindowClassAtom
            {
                get
                {
                    if (_sharedWindowClassAtom == 0)
                    {
                        _sharedWindowClassAtom = UnsafeNativeMethods.RegisterClass(new WNDCLASS
                        {
                            cbClsExtra = 0,
                            cbWndExtra = 0,
                            hbrBackground = IntPtr.Zero,
                            hCursor = IntPtr.Zero,
                            hIcon = IntPtr.Zero,
                            lpfnWndProc = _sharedWndProc = NativeMethods.DefWindowProc,
                            lpszClassName = GlowWindowClassName,
                            lpszMenuName = null,
                            style = 0
                        });
                    }

                    return _sharedWindowClassAtom;
                }
            }

            private void UpdateProperty<T>(ref T field, T value, FieldInvalidationTypes invalidatedValues) where T : struct
            {
                if (field.Equals(value)) return;
                field = value;
                _invalidatedValues |= invalidatedValues;
                if (IsDeferringChanges) return;
                CommitChanges();
            }

            public bool IsVisible
            {
                private get => _isVisible;
                set => UpdateProperty(ref _isVisible, value, FieldInvalidationTypes.Render | FieldInvalidationTypes.Visibility);
            }

            private int Left
            {
                get => _left;
                set => UpdateProperty(ref _left, value, FieldInvalidationTypes.Location);
            }

            private int Top
            {
                get => _top;
                set => UpdateProperty(ref _top, value, FieldInvalidationTypes.Location);
            }

            private int Width
            {
                get => _width;
                set => UpdateProperty(ref _width, value, FieldInvalidationTypes.Size | FieldInvalidationTypes.Render);
            }

            private int Height
            {
                get => _height;
                set => UpdateProperty(ref _height, value, FieldInvalidationTypes.Size | FieldInvalidationTypes.Render);
            }

            public bool IsActive
            {
                private get => _isActive;
                set => UpdateProperty(ref _isActive, value, FieldInvalidationTypes.Render);
            }

            public Color ActiveGlowColor
            {
                private get => _activeGlowColor;
                set => UpdateProperty(ref _activeGlowColor, value, FieldInvalidationTypes.ActiveColor | FieldInvalidationTypes.Render);
            }

            public Color InactiveGlowColor
            {
                private get => _inactiveGlowColor;
                set => UpdateProperty(ref _inactiveGlowColor, value, FieldInvalidationTypes.InactiveColor | FieldInvalidationTypes.Render);
            }

            private IntPtr TargetWindowHandle => new WindowInteropHelper(_targetWindow).Handle;

            protected override short CreateWindowClassCore() => SharedWindowClassAtom;

            protected override void DestroyWindowClassCore()
            {
            }

            protected override IntPtr CreateWindowCore() => NativeMethods.CreateWindowEx(524416, new IntPtr(WindowClassAtom), string.Empty, -2046820352, 0, 0, 0, 0, new WindowInteropHelper(_targetWindow).Owner, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            public void ChangeOwner(IntPtr newOwner) => NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GWLP.HWNDPARENT, newOwner);

            protected override bool IsWindowSubclassed => true;

            protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
            {
                switch (msg)
                {
                    case 6:
                        return IntPtr.Zero;
                    case 70:
                        var structure = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                        structure.flags |= 16U;
                        Marshal.StructureToPtr(structure, lParam, true);
                        break;
                    case 126:
                        if (IsVisible)
                        {
                            RenderLayeredWindow();
                        }
                        break;
                    case 132:
                        return new IntPtr(WmNcHitTest(lParam));
                    case 161:
                    case 163:
                    case 164:
                    case 166:
                    case 167:
                    case 169:
                    case 171:
                    case 173:
                        var targetWindowHandle = TargetWindowHandle;
                        NativeMethods.SendMessage(targetWindowHandle, 6, new IntPtr(2), IntPtr.Zero);
                        NativeMethods.SendMessage(targetWindowHandle, msg, wParam, IntPtr.Zero);
                        return IntPtr.Zero;
                }
                return base.WndProc(hwnd, msg, wParam, lParam);
            }

            private int WmNcHitTest(IntPtr lParam)
            {
                var xlParam = NativeMethods.GetXLParam(lParam.ToInt32());
                var ylParam = NativeMethods.GetYLParam(lParam.ToInt32());
                NativeMethods.GetWindowRect(Handle, out var lpRect);
                switch (_orientation)
                {
                    case Dock.Left:
                        if (ylParam - CornerGripThickness < lpRect.Top)
                            return 13;
                        return ylParam + CornerGripThickness > lpRect.Bottom ? 16 : 10;
                    case Dock.Top:
                        if (xlParam - CornerGripThickness < lpRect.Left)
                            return 13;
                        return xlParam + CornerGripThickness > lpRect.Right ? 14 : 12;
                    case Dock.Right:
                        if (ylParam - CornerGripThickness < lpRect.Top)
                            return 14;
                        return ylParam + CornerGripThickness > lpRect.Bottom ? 17 : 11;
                    default:
                        if (xlParam - CornerGripThickness < lpRect.Left)
                            return 16;
                        return xlParam + CornerGripThickness > lpRect.Right ? 17 : 15;
                }
            }

            public void CommitChanges()
            {
                InvalidateCachedBitmaps();
                UpdateWindowPosCore();
                UpdateLayeredWindowCore();
                _invalidatedValues = FieldInvalidationTypes.None;
            }

            private bool InvalidatedValuesHasFlag(FieldInvalidationTypes flag) => (uint)(_invalidatedValues & flag) > 0U;

            private void InvalidateCachedBitmaps()
            {
                if (InvalidatedValuesHasFlag(FieldInvalidationTypes.ActiveColor))
                    ClearCache(_activeGlowBitmaps);
                if (!InvalidatedValuesHasFlag(FieldInvalidationTypes.InactiveColor))
                    return;
                ClearCache(_inactiveGlowBitmaps);
            }

            private void UpdateWindowPosCore()
            {
                if (!InvalidatedValuesHasFlag(FieldInvalidationTypes.Location | FieldInvalidationTypes.Size | FieldInvalidationTypes.Visibility))
                    return;
                int flags = 532;
                if (InvalidatedValuesHasFlag(FieldInvalidationTypes.Visibility))
                {
                    if (IsVisible)
                        flags |= 64;
                    else
                        flags |= 131;
                }
                if (!InvalidatedValuesHasFlag(FieldInvalidationTypes.Location))
                    flags |= 2;
                if (!InvalidatedValuesHasFlag(FieldInvalidationTypes.Size))
                    flags |= 1;
                NativeMethods.SetWindowPos(Handle, IntPtr.Zero, Left, Top, Width, Height, flags);
            }

            private void UpdateLayeredWindowCore()
            {
                if (!IsVisible || !InvalidatedValuesHasFlag(FieldInvalidationTypes.Render))
                    return;
                if (IsPositionValid)
                {
                    BeginDelayedRender();
                }
                else
                {
                    CancelDelayedRender();
                    RenderLayeredWindow();
                }
            }

            private bool IsPositionValid => !InvalidatedValuesHasFlag(FieldInvalidationTypes.Location | FieldInvalidationTypes.Size | FieldInvalidationTypes.Visibility);

            private void BeginDelayedRender()
            {
                if (_pendingDelayRender)
                    return;
                _pendingDelayRender = true;
                CompositionTarget.Rendering += CommitDelayedRender;
            }

            private void CancelDelayedRender()
            {
                if (!_pendingDelayRender)
                    return;
                _pendingDelayRender = false;
                CompositionTarget.Rendering -= CommitDelayedRender;
            }

            private void CommitDelayedRender(object sender, EventArgs e)
            {
                CancelDelayedRender();
                if (!IsVisible) return;
                RenderLayeredWindow();
            }

            private void RenderLayeredWindow()
            {
                using (var drawingContext = new GlowDrawingContext(Width, Height))
                {
                    if (!drawingContext.IsInitialized) return;
                    switch (_orientation)
                    {
                        case Dock.Left:
                            DrawLeft(drawingContext);
                            break;
                        case Dock.Top:
                            DrawTop(drawingContext);
                            break;
                        case Dock.Right:
                            DrawRight(drawingContext);
                            break;
                        default:
                            DrawBottom(drawingContext);
                            break;
                    }

                    var pptDest = new NativeMethods.POINT
                    {
                        X = Left,
                        Y = Top
                    };

                    var psize = new NativeMethods.Win32SIZE
                    {
                        cx = Width,
                        cy = Height
                    };

                    var pptSrc = new NativeMethods.POINT
                    {
                        X = 0,
                        Y = 0
                    };

                    NativeMethods.UpdateLayeredWindow(Handle, drawingContext.ScreenDC, ref pptDest, ref psize, drawingContext.WindowDC, ref pptSrc, 0U, ref drawingContext.Blend, 2U);
                }
            }

            private GlowBitmap GetOrCreateBitmap(GlowDrawingContext drawingContext, GlowBitmapPart bitmapPart)
            {
                GlowBitmap[] glowBitmapArray;
                Color color;
                if (IsActive)
                {
                    glowBitmapArray = _activeGlowBitmaps;
                    color = ActiveGlowColor;
                }
                else
                {
                    glowBitmapArray = _inactiveGlowBitmaps;
                    color = InactiveGlowColor;
                }
                var index = (int)bitmapPart;
                return glowBitmapArray[index] ??
                       (glowBitmapArray[index] = GlowBitmap.Create(drawingContext, bitmapPart, color));
            }

            private void ClearCache(GlowBitmap[] cache)
            {
                for (var index = 0; index < cache.Length; ++index)
                {
                    using (cache[index])
                        cache[index] = null;
                }
            }

            protected override void DisposeManagedResources()
            {
                ClearCache(_activeGlowBitmaps);
                ClearCache(_inactiveGlowBitmaps);
            }

            protected override void DisposeNativeResources()
            {
                base.DisposeNativeResources();
                ++_disposedGlowWindows;
            }

            private void DrawLeft(GlowDrawingContext drawingContext)
            {
                var bitmap1 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.CornerTopLeft);
                var bitmap2 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.LeftTop);
                var bitmap3 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.Left);
                var bitmap4 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.LeftBottom);
                var bitmap5 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.CornerBottomLeft);
                var height = bitmap1.Height;
                var yoriginDest1 = height + bitmap2.Height;
                var yoriginDest2 = drawingContext.Height - bitmap5.Height;
                var yoriginDest3 = yoriginDest2 - bitmap4.Height;
                var hDest = yoriginDest3 - yoriginDest1;
                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap1.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, 0, bitmap1.Width, bitmap1.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap1.Width, bitmap1.Height, drawingContext.Blend);
                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap2.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, height, bitmap2.Width, bitmap2.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap2.Width, bitmap2.Height, drawingContext.Blend);
                if (hDest > 0)
                {
                    NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap3.Handle);
                    NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, yoriginDest1, bitmap3.Width, hDest,
                        drawingContext.BackgroundDC, 0, 0, bitmap3.Width, bitmap3.Height, drawingContext.Blend);
                }

                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap4.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, yoriginDest3, bitmap4.Width, bitmap4.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap4.Width, bitmap4.Height, drawingContext.Blend);
                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap5.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, yoriginDest2, bitmap5.Width, bitmap5.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap5.Width, bitmap5.Height, drawingContext.Blend);
            }

            private void DrawRight(GlowDrawingContext drawingContext)
            {
                var bitmap1 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.CornerTopRight);
                var bitmap2 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.RightTop);
                var bitmap3 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.Right);
                var bitmap4 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.RightBottom);
                var bitmap5 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.CornerBottomRight);
                var height = bitmap1.Height;
                var yoriginDest1 = height + bitmap2.Height;
                var yoriginDest2 = drawingContext.Height - bitmap5.Height;
                var yoriginDest3 = yoriginDest2 - bitmap4.Height;
                var hDest = yoriginDest3 - yoriginDest1;
                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap1.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, 0, bitmap1.Width, bitmap1.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap1.Width, bitmap1.Height, drawingContext.Blend);
                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap2.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, height, bitmap2.Width, bitmap2.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap2.Width, bitmap2.Height, drawingContext.Blend);
                if (hDest > 0)
                {
                    NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap3.Handle);
                    NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, yoriginDest1, bitmap3.Width, hDest,
                        drawingContext.BackgroundDC, 0, 0, bitmap3.Width, bitmap3.Height, drawingContext.Blend);
                }

                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap4.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, yoriginDest3, bitmap4.Width, bitmap4.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap4.Width, bitmap4.Height, drawingContext.Blend);
                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap5.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, 0, yoriginDest2, bitmap5.Width, bitmap5.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap5.Width, bitmap5.Height, drawingContext.Blend);
            }

            private void DrawTop(GlowDrawingContext drawingContext)
            {
                var bitmap1 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.TopLeft);
                var bitmap2 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.Top);
                var bitmap3 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.TopRight);
                var xoriginDest1 = GlowDepth;
                var xoriginDest2 = xoriginDest1 + bitmap1.Width;
                var xoriginDest3 = drawingContext.Width - GlowDepth - bitmap3.Width;
                var wDest = xoriginDest3 - xoriginDest2;
                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap1.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, xoriginDest1, 0, bitmap1.Width, bitmap1.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap1.Width, bitmap1.Height, drawingContext.Blend);
                if (wDest > 0)
                {
                    NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap2.Handle);
                    NativeMethods.AlphaBlend(drawingContext.WindowDC, xoriginDest2, 0, wDest, bitmap2.Height,
                        drawingContext.BackgroundDC, 0, 0, bitmap2.Width, bitmap2.Height, drawingContext.Blend);
                }

                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap3.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, xoriginDest3, 0, bitmap3.Width, bitmap3.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap3.Width, bitmap3.Height, drawingContext.Blend);
            }

            private void DrawBottom(GlowDrawingContext drawingContext)
            {
                var bitmap1 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.BottomLeft);
                var bitmap2 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.Bottom);
                var bitmap3 = GetOrCreateBitmap(drawingContext, GlowBitmapPart.BottomRight);
                var xoriginDest1 = GlowDepth;
                var xoriginDest2 = xoriginDest1 + bitmap1.Width;
                var xoriginDest3 = drawingContext.Width - GlowDepth - bitmap3.Width;
                var wDest = xoriginDest3 - xoriginDest2;
                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap1.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, xoriginDest1, 0, bitmap1.Width, bitmap1.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap1.Width, bitmap1.Height, drawingContext.Blend);
                if (wDest > 0)
                {
                    NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap2.Handle);
                    NativeMethods.AlphaBlend(drawingContext.WindowDC, xoriginDest2, 0, wDest, bitmap2.Height,
                        drawingContext.BackgroundDC, 0, 0, bitmap2.Width, bitmap2.Height, drawingContext.Blend);
                }

                NativeMethods.SelectObject(drawingContext.BackgroundDC, bitmap3.Handle);
                NativeMethods.AlphaBlend(drawingContext.WindowDC, xoriginDest3, 0, bitmap3.Width, bitmap3.Height,
                    drawingContext.BackgroundDC, 0, 0, bitmap3.Width, bitmap3.Height, drawingContext.Blend);
            }

            public void UpdateWindowPos()
            {
                var targetWindowHandle = TargetWindowHandle;
                NativeMethods.GetWindowRect(targetWindowHandle, out var lpRect);
                NativeMethods.GetWindowPlacement(targetWindowHandle);
                if (!IsVisible)
                    return;
                switch (_orientation)
                {
                    case Dock.Left:
                        Left = lpRect.Left - GlowDepth;
                        Top = lpRect.Top - GlowDepth;
                        Width = GlowDepth;
                        Height = lpRect.Height + CornerGripThickness;
                        break;
                    case Dock.Top:
                        Left = lpRect.Left - GlowDepth;
                        Top = lpRect.Top - GlowDepth;
                        Width = lpRect.Width + CornerGripThickness;
                        Height = GlowDepth;
                        break;
                    case Dock.Right:
                        Left = lpRect.Right;
                        Top = lpRect.Top - GlowDepth;
                        Width = GlowDepth;
                        Height = lpRect.Height + CornerGripThickness;
                        break;
                    default:
                        Left = lpRect.Left - GlowDepth;
                        Top = lpRect.Bottom;
                        Width = lpRect.Width + CornerGripThickness;
                        Height = GlowDepth;
                        break;
                }
            }

            [Flags]
            private enum FieldInvalidationTypes
            {
                None = 0,
                Location = 1,
                Size = 2,
                ActiveColor = 4,
                InactiveColor = 8,
                Render = 16,
                Visibility = 32
            }
        }
    }
}
