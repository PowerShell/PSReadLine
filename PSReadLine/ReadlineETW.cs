using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;

namespace PSConsoleUtilities
{
    internal enum ReadlineEventId
    {
        KeyEvent = 1,
    }

    [EventSource(Name = "PSReadline-EventSource")]
    class ReadlineEventSource : EventSource
    {
        internal void Key(int KeyChar, ConsoleKey Key, ConsoleModifiers Modifiers)
        {
            if (IsEnabled())
            {
                WriteEvent((int)ReadlineEventId.KeyEvent, KeyChar, (int)Key, (int)Modifiers);
            }
        }
    }

    class ReadlineEventListener : EventListener
    {
        private readonly ConcurrentQueue<string> events = new ConcurrentQueue<string>();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            string payloadString = null;

            switch (eventData.EventId)
            {
            case (int)ReadlineEventId.KeyEvent:
                payloadString = DecodeKeyEventPayload(eventData.Payload);
                break;
            }

            if (!string.IsNullOrWhiteSpace(payloadString))
            {
                events.Enqueue(payloadString);
                if (events.Count > 100)
                {
                    // Avoid growing forever in case our consumer ignores us.
                    events.TryDequeue(out payloadString);
                }
            }
        }

        internal bool TryGetEvent(out string eventString)
        {
            return events.TryDequeue(out eventString);
        }

        private string DecodeKeyEventPayload(ReadOnlyCollection<object> Payload)
        {
            var keyChar = (char)(int)Payload[0];
            var key = (ConsoleKey)Payload[1];
            var modifiers = (ConsoleModifiers)Payload[2];
            var consoleKey = new ConsoleKeyInfo(keyChar, key,
                                                (modifiers & ConsoleModifiers.Shift) != 0,
                                                (modifiers & ConsoleModifiers.Alt) != 0,
                                                (modifiers & ConsoleModifiers.Control) != 0);
            return consoleKey.ToGestureString();
        }
    }
}
