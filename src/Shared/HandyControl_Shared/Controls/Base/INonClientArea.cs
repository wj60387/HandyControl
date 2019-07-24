using System.Windows;

namespace HandyControl.Controls
{
    public interface INonClientArea
    {
        int HitTest(Point point);
    }
}