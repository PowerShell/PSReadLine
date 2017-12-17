using System;

namespace Microsoft.PowerShell
{
    internal sealed class Disposable : IDisposable
    {
        private Action m_onDispose;

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

