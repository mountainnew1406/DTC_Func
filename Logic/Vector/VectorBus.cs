using System;
using System.Collections.Generic;
using vxlapi_NET;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// Vector CAN bus — wraps XLDriver from vxlapi_NET.dll.
    /// Lifecycle mirrors python-can's VectorBus: discover → configure → open → activate → tx/rx → dispose.
    /// </summary>
    public sealed class VectorBus : ICanBus
    {
        private readonly XLDriver _driver = new XLDriver();
        private readonly string _appName;
        private readonly int _appChannel;
        private readonly uint _bitrate;

        private int    _portHandle = -1;
        private ulong  _channelMask;
        private ulong  _permissionMask;
        private int    _notifyHandle;
        private bool   _disposed;

        private readonly List<PeriodicSendTask> _periodicTasks = new List<PeriodicSendTask>();

        // ─────────────────────────────────────────────────────────────────────
        // Constructor / open
        // ─────────────────────────────────────────────────────────────────────

        public VectorBus(string appName, int appChannel, int bitrate)
        {
            _appName    = appName;
            _appChannel = appChannel;
            _bitrate    = (uint)bitrate;

            CheckInit(_driver.XL_OpenDriver(), "XL_OpenDriver");
            OpenConfiguredChannel();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Static helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Enumerates all channels visible to the XL Driver.</summary>
        public static IReadOnlyList<VectorChannelConfig> GetChannelConfigs()
        {
            var driver = new XLDriver();
            CheckInit(driver.XL_OpenDriver(), "XL_OpenDriver");
            try
            {
                var cfg = new XLClass.xl_driver_config();
                CheckInit(driver.XL_GetDriverConfig(ref cfg), "XL_GetDriverConfig");

                var list = new List<VectorChannelConfig>();
                for (int i = 0; i < cfg.channelCount; i++)
                    list.Add(new VectorChannelConfig(cfg.channel[i], i));

                return list;
            }
            finally
            {
                driver.XL_CloseDriver();
            }
        }

        /// <summary>Registers an application-to-hardware channel mapping.</summary>
        public static void SetApplicationConfig(
            string appName, int appChannel,
            XLDefine.XL_HardwareType hwType, uint hwIndex, uint hwChannel)
        {
            var driver = new XLDriver();
            CheckInit(driver.XL_OpenDriver(), "XL_OpenDriver");
            try
            {
                CheckInit(
                    driver.XL_SetApplConfig(appName, (uint)appChannel,
                        hwType, hwIndex, hwChannel,
                        XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN),
                    "XL_SetApplConfig");
            }
            finally { driver.XL_CloseDriver(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ICanBus — Send
        // ─────────────────────────────────────────────────────────────────────

        public void Send(CanMessage message, int? timeoutMs = null)
        {
            ThrowIfDisposed();
            if (message == null) throw new ArgumentNullException(nameof(message));

            var txEvent = new XLClass.xl_event();
            txEvent.tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG;
            txEvent.tagData.can_Msg.dlc = (ushort)message.Dlc;
            txEvent.tagData.can_Msg.id  = message.ArbitrationId;

            if (message.IsExtendedId)
                txEvent.tagData.can_Msg.id |= 0x80000000u;

            txEvent.tagData.can_Msg.data = new byte[8];
            Array.Copy(message.Data, txEvent.tagData.can_Msg.data, message.Dlc);

            CheckOp(_driver.XL_CanTransmit(_portHandle, _channelMask, txEvent), "XL_CanTransmit");
        }

        // ─────────────────────────────────────────────────────────────────────
        // ICanBus — Recv
        // Uses XL_WaitForSingleObject (built into XLDriver, no kernel32 needed).
        // ─────────────────────────────────────────────────────────────────────

        public CanMessage Recv(int? timeoutMs = null)
        {
            ThrowIfDisposed();

            int waitMs = timeoutMs ?? 1000;
            XLDefine.XL_Status waitResult =
                (XLDefine.XL_Status)_driver.XL_WaitForSingleObject(_notifyHandle, waitMs);

            // Timeout or error — nothing in queue
            if (waitResult != XLDefine.XL_Status.XL_SUCCESS)
                return null;

            // Drain queue — return first valid RX CAN message
            while (true)
            {
                var rxEvent = new XLClass.xl_event();
                XLDefine.XL_Status status = _driver.XL_Receive(_portHandle, ref rxEvent);

                if (status == XLDefine.XL_Status.XL_ERR_QUEUE_IS_EMPTY)
                    return null;

                if (status != XLDefine.XL_Status.XL_SUCCESS)
                    continue; // stale/chip-state events

                if (rxEvent.tag != XLDefine.XL_EventTags.XL_RECEIVE_MSG)
                    continue;

                // Skip TX acknowledgement frames (bit 0 of flags)
                if (((int)rxEvent.tagData.can_Msg.flags & 0x0001) != 0)
                    continue;

                uint  rawId  = rxEvent.tagData.can_Msg.id;
                bool  extId  = (rawId & 0x80000000u) != 0;
                uint  arbId  = rawId & 0x1FFFFFFFu;
                int   dlc    = Math.Min((int)rxEvent.tagData.can_Msg.dlc, 8);
                byte[] data  = new byte[dlc];
                if (rxEvent.tagData.can_Msg.data != null)
                    Array.Copy(rxEvent.tagData.can_Msg.data, data, dlc);

                return new CanMessage(arbId, data, extId) { IsRx = true };
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ICanBus — Periodic
        // ─────────────────────────────────────────────────────────────────────

        public PeriodicSendTask SendPeriodic(
            IReadOnlyList<CanMessage> messages,
            TimeSpan period,
            TimeSpan? duration = null,
            bool autostart = true,
            Func<CanMessage, CanMessage> modifierCallback = null)
        {
            var task = new PeriodicSendTask(Send, messages, period, duration, modifierCallback);
            _periodicTasks.Add(task);
            if (autostart) task.Start();
            return task;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private
        // ─────────────────────────────────────────────────────────────────────

        private void OpenConfiguredChannel()
        {
            XLDefine.XL_HardwareType hwType   = 0;
            uint hwIndex   = 0;
            uint hwChannel = 0;

            CheckInit(
                _driver.XL_GetApplConfig(
                    _appName, (uint)_appChannel,
                    ref hwType, ref hwIndex, ref hwChannel,
                    XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN),
                "XL_GetApplConfig");

            _channelMask = _driver.XL_GetChannelMask(hwType, (int)hwIndex, (int)hwChannel);
            if (_channelMask == 0)
                throw new VectorInitializationError(
                    $"XL_GetChannelMask returned 0 — verify Vector Hardware Config for '{_appName}'.");

            _permissionMask = _channelMask;

            CheckInit(
                _driver.XL_OpenPort(
                    ref _portHandle,
                    _appName,
                    _channelMask,
                    ref _permissionMask,
                    16384,
                    XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION,
                    XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN),
                "XL_OpenPort");

            // Set bitrate only when we own the channel (Tx permission granted)
            if (_permissionMask == _channelMask)
            {
                CheckInit(
                    _driver.XL_CanSetChannelBitrate(_portHandle, _channelMask, _bitrate),
                    "XL_CanSetChannelBitrate");
            }

            // Kernel event for efficient blocking receive
            CheckInit(
                _driver.XL_SetNotification(_portHandle, ref _notifyHandle, 1),
                "XL_SetNotification");

            CheckInit(
                _driver.XL_ActivateChannel(
                    _portHandle, _channelMask,
                    XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                    XLDefine.XL_AC_Flags.XL_ACTIVATE_RESET_CLOCK),
                "XL_ActivateChannel");

            _driver.XL_FlushReceiveQueue(_portHandle);
        }

        private static void CheckInit(XLDefine.XL_Status s, string fn)
        {
            if (s == XLDefine.XL_Status.XL_SUCCESS) return;
            throw new VectorInitializationError($"{fn} failed: {s}", (int)s, fn);
        }

        private static void CheckOp(XLDefine.XL_Status s, string fn)
        {
            if (s == XLDefine.XL_Status.XL_SUCCESS) return;
            throw new VectorOperationError($"{fn} failed: {s}", (int)s, fn);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VectorBus));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Dispose
        // ─────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var t in _periodicTasks) t.Dispose();

            if (_portHandle != -1)
            {
                _driver.XL_DeactivateChannel(_portHandle, _channelMask);
                _driver.XL_ClosePort(_portHandle);
                _portHandle = -1;
            }

            _driver.XL_CloseDriver();
        }
    }
}
