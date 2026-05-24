using System;
using System.Collections.Generic;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// CAN bus abstraction — mirrors python-can's Bus interface.
    /// </summary>
    public interface ICanBus : IDisposable
    {
        void Send(CanMessage message, int? timeoutMs = null);
        CanMessage Recv(int? timeoutMs = null);
        PeriodicSendTask SendPeriodic(
            IReadOnlyList<CanMessage> messages,
            TimeSpan period,
            TimeSpan? duration = null,
            bool autostart = true,
            Func<CanMessage, CanMessage> modifierCallback = null);
    }
}
