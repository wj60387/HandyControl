using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Media3D;
using HandyControl.Expression.Drawing;
using HandyControl.Tools.Interop;

namespace HandyControl.Tools
{
    /// <summary>
    ///     包含内部使用的一些简单算法
    /// </summary>
    internal class ArithmeticHelper
    {
        /// <summary>
        ///     平分一个整数到一个数组中
        /// </summary>
        /// <param name="num"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static int[] DivideInt2Arr(int num, int count)
        {
            var arr = new int[count];
            var div = num / count;
            var rest = num % count;
            for (var i = 0; i < count; i++)
            {
                arr[i] = div;
            }
            for (var i = 0; i < rest; i++)
            {
                arr[i] += 1;
            }
            return arr;
        }

        /// <summary>
        ///     计算控件在窗口中的可见坐标
        /// </summary>
        public static Point CalSafePoint(FrameworkElement element, FrameworkElement showElement, Thickness thickness = default)
        {
            if (element == null || showElement == null) return default;
            var point = element.PointToScreen(new Point(0, 0));

            if (point.X < 0) point.X = 0;
            if (point.Y < 0) point.Y = 0;

            var maxLeft = SystemParameters.WorkArea.Width -
                          ((double.IsNaN(showElement.Width) ? showElement.ActualWidth : showElement.Width) +
                           thickness.Left + thickness.Right);
            var maxTop = SystemParameters.WorkArea.Height -
                         ((double.IsNaN(showElement.Height) ? showElement.ActualHeight : showElement.Height) +
                          thickness.Top + thickness.Bottom);
            return new Point(maxLeft > point.X ? point.X : maxLeft, maxTop > point.Y ? point.Y : maxTop);
        }

        /// <summary>
        ///     获取布局范围框
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static Rect GetLayoutRect(FrameworkElement element)
        {
            var num1 = element.ActualWidth;
            var num2 = element.ActualHeight;
            if (element is Image || element is MediaElement)
                if (element.Parent is Canvas)
                {
                    num1 = double.IsNaN(element.Width) ? num1 : element.Width;
                    num2 = double.IsNaN(element.Height) ? num2 : element.Height;
                }
                else
                {
                    num1 = element.RenderSize.Width;
                    num2 = element.RenderSize.Height;
                }
            var width = element.Visibility == Visibility.Collapsed ? 0.0 : num1;
            var height = element.Visibility == Visibility.Collapsed ? 0.0 : num2;
            var margin = element.Margin;
            var layoutSlot = LayoutInformation.GetLayoutSlot(element);
            var x = 0.0;
            var y = 0.0;
            switch (element.HorizontalAlignment)
            {
                case HorizontalAlignment.Left:
                    x = layoutSlot.Left + margin.Left;
                    break;
                case HorizontalAlignment.Center:
                    x = (layoutSlot.Left + margin.Left + layoutSlot.Right - margin.Right) / 2.0 - width / 2.0;
                    break;
                case HorizontalAlignment.Right:
                    x = layoutSlot.Right - margin.Right - width;
                    break;
                case HorizontalAlignment.Stretch:
                    x = Math.Max(layoutSlot.Left + margin.Left,
                        (layoutSlot.Left + margin.Left + layoutSlot.Right - margin.Right) / 2.0 - width / 2.0);
                    break;
            }
            switch (element.VerticalAlignment)
            {
                case VerticalAlignment.Top:
                    y = layoutSlot.Top + margin.Top;
                    break;
                case VerticalAlignment.Center:
                    y = (layoutSlot.Top + margin.Top + layoutSlot.Bottom - margin.Bottom) / 2.0 - height / 2.0;
                    break;
                case VerticalAlignment.Bottom:
                    y = layoutSlot.Bottom - margin.Bottom - height;
                    break;
                case VerticalAlignment.Stretch:
                    y = Math.Max(layoutSlot.Top + margin.Top,
                        (layoutSlot.Top + margin.Top + layoutSlot.Bottom - margin.Bottom) / 2.0 - height / 2.0);
                    break;
            }
            return new Rect(x, y, width, height);
        }

        /// <summary>
        ///     计算两点的连线和x轴的夹角
        /// </summary>
        /// <param name="center"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static double CalAngle(Point center, Point p) => Math.Atan2(p.Y - center.Y, p.X - center.X) * 180 / Math.PI;

        /// <summary>
        ///     计算法线
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static Vector3D CalNormal(Point3D p0, Point3D p1, Point3D p2)
        {
            var v0 = new Vector3D(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            var v1 = new Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
            return Vector3D.CrossProduct(v0, v1);
        }

        private static List<MONITORINFO> _displays = new List<MONITORINFO>();

        internal static void FindMonitorRectsFromPoint(Point point, out Rect monitorRect, out Rect workAreaRect)
        {
            var hMonitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT
            {
                X = (int)point.X,
                Y = (int)point.Y
            }, 2);

            monitorRect = new Rect(0.0, 0.0, 0.0, 0.0);
            workAreaRect = new Rect(0.0, 0.0, 0.0, 0.0);
            if (hMonitor == IntPtr.Zero) return;
            var monitorInfo = new MONITORINFO
            {
                cbSize = (uint) Marshal.SizeOf(typeof(MONITORINFO))
            };
            NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo);
            monitorRect = new Rect(monitorInfo.rcMonitor.Position, monitorInfo.rcMonitor.Size);
            workAreaRect = new Rect(monitorInfo.rcWork.Position, monitorInfo.rcWork.Size);
        }

        private static Point GetRectCenter(NativeMethods.RECT rect) => new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

        private static double Distance(NativeMethods.RECT rect1, NativeMethods.RECT rect2) => Distance(GetRectCenter(rect1), GetRectCenter(rect2));

        private static double Distance(Point point1, Point point2) => Math.Sqrt(Math.Pow(point1.X - point2.X, 2.0) + Math.Pow(point1.Y - point2.Y, 2.0));

        internal static int FindDisplayForWindowRect(Rect windowRect)
        {
            var num1 = -1;
            var lprcSrc2 = new NativeMethods.RECT(windowRect);
            long num2 = 0;
            for (var index = 0; index < _displays.Count; ++index)
            {
                var rcWork = _displays[index].rcWork;
                NativeMethods.IntersectRect(out var lprcDst, ref rcWork, ref lprcSrc2);
                long num3 = lprcDst.Width * lprcDst.Height;
                if (num3 > num2)
                {
                    num1 = index;
                    num2 = num3;
                }
            }
            if (-1 == num1)
            {
                var num3 = double.MaxValue;
                for (var index = 0; index < _displays.Count; ++index)
                {
                    var num4 = Distance(_displays[index].rcMonitor, lprcSrc2);
                    if (num4 < num3)
                    {
                        num1 = index;
                        num3 = num4;
                    }
                }
            }
            return num1;
        }

        internal static void FindMaximumSingleMonitorRectangle(NativeMethods.RECT windowRect, out NativeMethods.RECT screenSubRect, out NativeMethods.RECT monitorRect)
        {
            int displayForWindowRect = FindDisplayForWindowRect(new Rect(windowRect.Left, windowRect.Top, windowRect.Width, windowRect.Height));
            screenSubRect = new NativeMethods.RECT
            {
                Left = 0,
                Right = 0,
                Top = 0,
                Bottom = 0
            };
            monitorRect = new NativeMethods.RECT
            {
                Left = 0,
                Right = 0,
                Top = 0,
                Bottom = 0
            };
            if (displayForWindowRect == -1) return;
            var display = _displays[displayForWindowRect];
            var rcWork = display.rcWork;
            NativeMethods.IntersectRect(out var lprcDst, ref rcWork, ref windowRect);
            screenSubRect = lprcDst;
            monitorRect = display.rcWork;
        }

        internal static void FindMaximumSingleMonitorRectangle(Rect windowRect, out Rect screenSubRect, out Rect monitorRect)
        {
            FindMaximumSingleMonitorRectangle(new NativeMethods.RECT(windowRect), out var screenSubRect1, out var monitorRect1);
            screenSubRect = new Rect(screenSubRect1.Position, screenSubRect1.Size);
            monitorRect = new Rect(monitorRect1.Position, monitorRect1.Size);
        }

        internal static Rect GetOnScreenPosition(Rect floatRect)
        {
            var rect = floatRect;
            floatRect = floatRect.LogicalToDeviceUnits();
            FindMaximumSingleMonitorRectangle(floatRect, out var screenSubRect, out _);
            if (MathHelper.IsVerySmall(screenSubRect.Width) ||  MathHelper.IsVerySmall(screenSubRect.Height))
            {
                FindMonitorRectsFromPoint(NativeMethods.GetCursorPos(), out _, out var workAreaRect);
                var logicalUnits = workAreaRect.DeviceToLogicalUnits();
                if (rect.Width > logicalUnits.Width)
                    rect.Width = logicalUnits.Width;
                if (rect.Height > logicalUnits.Height)
                    rect.Height = logicalUnits.Height;
                if (logicalUnits.Right <= rect.X)
                    rect.X = logicalUnits.Right - rect.Width;
                if (logicalUnits.Left > rect.X + rect.Width)
                    rect.X = logicalUnits.Left;
                if (logicalUnits.Bottom <= rect.Y)
                    rect.Y = logicalUnits.Bottom - rect.Height;
                if (logicalUnits.Top > rect.Y + rect.Height)
                    rect.Y = logicalUnits.Top;
            }
            return rect;
        }
    }
}