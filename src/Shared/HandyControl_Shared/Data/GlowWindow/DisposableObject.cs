using System;
using HandyControl.Tools.Extension;

namespace HandyControl.Data
{
    public class DisposableObject : IDisposable
    {
        private EventHandler _disposing;

        ~DisposableObject()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool IsDisposed { get; private set; }

        public event EventHandler Disposing
        {
            add
            {
                ThrowIfDisposed();
                _disposing += value;
            }
            remove
            {
                ThrowIfDisposed();
                // ReSharper disable once DelegateSubtraction
                _disposing -= value;
            }
        }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        protected void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;
            try
            {
                _disposing.RaiseEvent(this);
                _disposing = null;
                if (disposing)
                    DisposeManagedResources();
                DisposeNativeResources();
            }
            finally
            {
                IsDisposed = true;
            }
        }

        protected virtual void DisposeManagedResources()
        {
        }

        protected virtual void DisposeNativeResources()
        {
        }
    }
}
