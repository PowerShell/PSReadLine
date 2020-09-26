using System;

namespace Microsoft.PowerShell
{
    internal sealed class Disposable : IDisposable
    {
        private Action m_onDispose;

        internal static readonly Disposable NonOp = new Disposable();

        private Disposable()
        {
            m_onDispose = null;
        }

        public Disposable(Action onDispose)
        {
            m_onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public void Dispose()
        {
            if (m_onDispose != null)
            {
                m_onDispose();
                m_onDispose = null;
            }
        }
    }
}

