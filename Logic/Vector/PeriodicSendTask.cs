using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// Sends a list of CAN messages repeatedly at a fixed period.
    /// Mirrors python-can's send_periodic().
    /// </summary>
    public sealed class PeriodicSendTask : IDisposable
    {
        private readonly Action<CanMessage, int?> _send;
        private readonly IReadOnlyList<CanMessage> _messages;
        private readonly TimeSpan _period;
        private readonly TimeSpan? _duration;
        private readonly Func<CanMessage, CanMessage> _modifierCallback;
        private CancellationTokenSource _cts;
        private int _running;

        public PeriodicSendTask(
            Action<CanMessage, int?> send,
            IReadOnlyList<CanMessage> messages,
            TimeSpan period,
            TimeSpan? duration,
            Func<CanMessage, CanMessage> modifierCallback)
        {
            _send = send;
            _messages = messages;
            _period = period;
            _duration = duration;
            _modifierCallback = modifierCallback;
        }

        public void Start()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return;

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            Task.Run(async () =>
            {
                try
                {
                    DateTime start = DateTime.UtcNow;
                    while (!token.IsCancellationRequested)
                    {
                        foreach (CanMessage message in _messages)
                        {
                            CanMessage tx = _modifierCallback?.Invoke(message) ?? message;
                            _send(tx, null);
                        }

                        if (_duration.HasValue && DateTime.UtcNow - start >= _duration.Value)
                            break;

                        await Task.Delay(_period, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
                finally { Interlocked.Exchange(ref _running, 0); }
            }, token);
        }

        public void Stop() => _cts?.Cancel();

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _cts = null;
            Interlocked.Exchange(ref _running, 0);
        }
    }
}
