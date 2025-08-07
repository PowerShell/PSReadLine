
using System;

namespace Test
{
    public class ConsoleFixture : IDisposable
    {
        public KeyboardLayout KbLayout { get; private set; }

        public ConsoleFixture()
        {
        }

        public void Initialize()
        {
            KbLayout = new KeyboardLayout();
        }

        public void Dispose()
        {
        }
    }
}
