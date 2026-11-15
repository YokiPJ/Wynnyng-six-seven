using UnityEngine;
using Wynnyng.Core;

namespace Wynnyng.Runtime
{
    public sealed class UnityGameLog : IGameLog
    {
        private readonly bool enabled;
        public UnityGameLog(bool enabled) { this.enabled = enabled; }
        public void Info(string message) { if (enabled) Debug.Log(message); }
        public void Error(string message) { Debug.LogError(message); }
    }
}
