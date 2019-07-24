using System;
using System.Windows;
using System.Windows.Media;

namespace HandyControl.Tools
{
    internal static class DpiHelper
    {
        [ThreadStatic]
        private static Matrix _transformToDip;

        public static Point DevicePixelsToLogical(Point devicePoint, double dpiScaleX, double dpiScaleY)
        {
            _transformToDip = Matrix.Identity;
            _transformToDip.Scale(1d / dpiScaleX, 1d / dpiScaleY);
            return _transformToDip.Transform(devicePoint);
        }

        public static Size DeviceSizeToLogical(Size deviceSize, double dpiScaleX, double dpiScaleY)
        {
            var pt = DevicePixelsToLogical(new Point(deviceSize.Width, deviceSize.Height), dpiScaleX, dpiScaleY);

            return new Size(pt.X, pt.Y);
        }

        public static Rect DeviceToLogicalUnits(this Rect deviceSize)
        {
            _transformToDip = Matrix.Identity;
            _transformToDip.Scale(1d / (VisualHelper.DpiX / 96.0), 1d / (VisualHelper.Dpi / 96.0));
            var pArr = new []
            {
                new Point(deviceSize.X, deviceSize.Y),
                new Point(deviceSize.X + deviceSize.Width, deviceSize.Y + deviceSize.Height)
            };
            _transformToDip.Transform(pArr);

            var p1 = pArr[0];
            var p2 = pArr[1];

            return new Rect(p1.X, p1.Y, p2.X - p1.X, p2.Y - p1.Y);
        }

        public static Rect LogicalToDeviceUnits(this Rect deviceSize)
        {
            _transformToDip = Matrix.Identity;
            _transformToDip.Scale(VisualHelper.DpiX / 96.0, VisualHelper.Dpi / 96.0);
            var pArr = new[]
            {
                new Point(deviceSize.X, deviceSize.Y),
                new Point(deviceSize.X + deviceSize.Width, deviceSize.Y + deviceSize.Height)
            };
            _transformToDip.Transform(pArr);

            var p1 = pArr[0];
            var p2 = pArr[1];

            return new Rect(p1.X, p1.Y, p2.X - p1.X, p2.Y - p1.Y);
        }
    }
}