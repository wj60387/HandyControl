using System;
using System.Windows;

namespace HandyControl.Tools.Extension
{
    internal static class ExtensionMethods
    {
        public static void RaiseEvent(this EventHandler eventHandler, object source) => eventHandler.RaiseEvent(source, EventArgs.Empty);

        public static void RaiseEvent(this EventHandler eventHandler, object source, EventArgs args) => eventHandler?.Invoke(source, args);

        public static bool IsConnectedToPresentationSource(this DependencyObject obj) => PresentationSource.FromDependencyObject(obj) != null;
    }
}
