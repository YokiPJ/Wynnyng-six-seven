using System;

namespace Wynnyng.Core
{
    public interface IRandomSource
    {
        uint NextUInt();
        float NextUnit();
    }

    /// <summary>Xorshift32. Constants define the algorithm, not gameplay tuning.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        public const uint NonZeroSeed = 0x6D2B79F5u;
        private uint state;
        public SeededRandom(uint seed) { state = seed == 0 ? NonZeroSeed : seed; }
        public uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }
        public float NextUnit() => (NextUInt() >> 8) * (1f / 16777216f);
    }

    public interface IGameLog
    {
        void Info(string message);
        void Error(string message);
    }

    public interface IEventChannel<T> where T : struct
    {
        void Subscribe(Action<T> listener);
        void Unsubscribe(Action<T> listener);
        void Publish(T value);
    }

    /// <summary>Single thread, fixed capacity. Subscribe outside dispatch; publish does not allocate.</summary>
    public sealed class EventChannel<T> : IEventChannel<T> where T : struct
    {
        private readonly Action<T>[] listeners;
        private int count;
        private bool publishing;
        public EventChannel(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            listeners = new Action<T>[capacity];
        }
        public void Subscribe(Action<T> listener)
        {
            if (publishing) throw new InvalidOperationException("Subscribe outside dispatch.");
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            for (int i = 0; i < count; i++) if (listeners[i] == listener) return;
            if (count == listeners.Length) throw new InvalidOperationException("Event channel is full.");
            listeners[count++] = listener;
        }
        public void Unsubscribe(Action<T> listener)
        {
            if (publishing) throw new InvalidOperationException("Unsubscribe outside dispatch.");
            for (int i = 0; i < count; i++)
            {
                if (listeners[i] != listener) continue;
                for (int j = i; j < count - 1; j++) listeners[j] = listeners[j + 1];
                listeners[--count] = null;
                return;
            }
        }
        public void Publish(T value)
        {
            if (publishing) throw new InvalidOperationException("Recursive dispatch is unsupported.");
            publishing = true;
            try { for (int i = 0; i < count; i++) listeners[i](value); }
            finally { publishing = false; }
        }
    }

    public struct SimulationClock
    {
        public long Tick { get; private set; }
        public float StepSeconds { get; }
        public double ElapsedSeconds => Tick * (double)StepSeconds;
        public SimulationClock(int frequency)
        {
            if (frequency < 1) throw new ArgumentOutOfRangeException(nameof(frequency));
            Tick = 0;
            StepSeconds = 1f / frequency;
        }
        public void Advance() { Tick++; }
    }
}
