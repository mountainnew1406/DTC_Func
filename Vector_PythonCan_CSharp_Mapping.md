# DTC CAN Workflow and C# Function Definition

This document defines the optimized DTC reading workflow and the C# functions/classes needed to implement it on WinForms using Vector XL API (`vxlapi.dll`).

The intended application name in Vector Hardware Configuration is:

```text
DTC_CAN
```

The design follows the same responsibilities as `python-can` Vector backend:

- `can.interface.Bus(...)`
- `can.Message`
- `send(...)`
- `recv(...)`
- `send_periodic(...)`
- `popup_vector_hw_configuration(...)`
- `set_application_config(...)`
- `get_channel_configs(...)`
- Vector exception/error checking

---

## 1. Overall Architecture

```text
WinForms UI
  -> DtcReadService
      -> UdsClient
          -> IsoTpClient
              -> ICanBus
                  -> VectorBus
                      -> vxlapi.dll

ConfigRepository
  -> diag_config.json
  -> DTC mapping JSON files

DtcDecoder
  -> Parse UDS 19 02 response
  -> Decode DTC code
  -> Decode status byte
  -> Lookup DTC name
```

Main rule:

```text
UI must not contain CAN, ISO-TP, UDS, or DTC parsing logic.
```

---

## 2. Connection Workflow

This workflow runs when the user connects Vector hardware or presses the Connect button.

```text
START CONNECT
|
|-- Open Vector XL driver
|
|-- Detect Vector hardware/channel by xlGetDriverConfig
|
|-- Filter channels that support CAN
|
|-- If no CAN channel:
|      Popup Vector Hardware Configuration
|      Show "No Vector CAN channel detected"
|      STOP
|
|-- Show detected CAN channels to user
|
|-- User selects one CAN channel
|
|-- Add/update Vector application configuration:
|      AppName    = DTC_CAN
|      AppChannel = 0
|      HwType     = selected.HwType
|      HwIndex    = selected.HwIndex
|      HwChannel  = selected.HwChannel
|
|-- Open VectorBus:
|      appName = DTC_CAN
|      channel = 0
|      bitrate = 500000
|
|-- Activate selected CAN channel
|
END CONNECT
```

Equivalent python-can concept:

```python
configs = can.interfaces.vector.get_channel_configs()
can.interfaces.vector.VectorBus.set_application_config(
    app_name="DTC_CAN",
    app_channel=0,
    hw_type=selected.hw_type,
    hw_index=selected.hw_index,
    hw_channel=selected.hw_channel
)
bus = can.interface.Bus(
    interface="vector",
    app_name="DTC_CAN",
    channel=0,
    bitrate=500000
)
```

---

## 3. Read DTC Workflow

This is the optimized workflow to replace the Python script logic.

```text
START READ DTC
|
|-- User selects vehicle type: VF3/VF5/VF8/VF9/...
|
|-- Load ECU list for selected vehicle
|
|-- User selects ECU nodes
|
|-- Load diag_config.json
|
|-- For each selected ECU:
|      NodeName
|      RequestId
|      ResponseId
|      DtcMappingFile
|
|-- Ensure VectorBus is connected
|
|-- For each selected ECU:
|      |
|      |-- Send UDS ReadDTCInformation:
|      |      Service     = 0x19
|      |      SubFunction = 0x02
|      |      StatusMask  = 0x09
|      |
|      |-- ISO-TP sends CAN frame:
|      |      03 19 02 09 00 00 00 00
|      |
|      |-- Wait response from ResponseId only
|      |
|      |-- If timeout:
|      |      Add result: No response
|      |      Continue next ECU
|      |
|      |-- If Negative Response 7F 19 78:
|      |      Continue waiting until P2* timeout
|      |
|      |-- If Negative Response 7F 19 NRC:
|      |      Add result: Negative Response + NRC
|      |      Continue next ECU
|      |
|      |-- If Positive Response:
|      |      ISO-TP returns clean UDS payload
|      |      Validate payload starts with 59 02
|      |      Parse DTC records
|      |      Decode DTC code
|      |      Decode status byte
|      |      Lookup DTC name from JSON
|      |      Add result rows
|
|-- Optional: Read VIN with UDS 22 F1 90
|
|-- Return all results to UI
|
END READ DTC
```

Important improvement:

```text
Do not resend the original request repeatedly when ECU returns NRC 0x78.
NRC 0x78 means Response Pending. Keep waiting until P2* timeout.
```

---

## 4. ISO-TP Receive Rules

The ISO-TP layer receives CAN frames and returns only clean UDS payload.

### Single Frame

```text
PCI: 0L
Example CAN data:
06 59 02 FF 12 34 56 09

UDS payload returned:
59 02 FF 12 34 56 09
```

### First Frame and Consecutive Frame

```text
First Frame:
10 14 59 02 FF 12 34 56

Flow Control sent by tester:
30 00 00 00 00 00 00 00

Consecutive Frames:
21 ...
22 ...
23 ...

UDS payload returned after enough bytes:
59 02 FF ...DTC records...
```

### Negative Response

```text
7F 19 78 = Response Pending
7F 19 xx = Negative Response with NRC xx
```

---

## 5. UDS DTC Payload Format

Read DTC request payload:

```text
19 02 09
```

Positive response payload:

```text
59 02 AvailabilityMask DTC_H DTC_M DTC_L Status ...
```

DTC records start at byte index `3`.

Each DTC record is 4 bytes:

```text
Byte 0: DTC high
Byte 1: DTC middle
Byte 2: DTC low
Byte 3: Status byte
```

If there are no records after byte index `3`, result is:

```text
No DTC
```

---

## 6. C# Function and Class Definitions

### 6.1 Constants

```csharp
public static class DtcCanConstants
{
    public const string AppName = "DTC_CAN";
    public const int AppChannel = 0;
    public const int Bitrate = 500000;

    public const byte UdsReadDtcInformation = 0x19;
    public const byte UdsReadDataByIdentifier = 0x22;
    public const byte PositiveReadDtcInformation = 0x59;
    public const byte NegativeResponse = 0x7F;
    public const byte NrcResponsePending = 0x78;

    public static readonly byte[] ReadDtcByStatusMask09 = { 0x19, 0x02, 0x09 };
    public static readonly byte[] ReadVin = { 0x22, 0xF1, 0x90 };
    public static readonly byte[] FlowControlContinueToSend = { 0x30, 0x00, 0x00, 0, 0, 0, 0, 0 };
}
```

### 6.2 CAN Message

Equivalent to `can.Message`.

```csharp
public sealed class CanMessage
{
    public uint ArbitrationId { get; }
    public byte[] Data { get; }
    public bool IsExtendedId { get; }
    public bool IsRx { get; internal set; }
    public DateTimeOffset Timestamp { get; internal set; }

    public int Dlc => Data.Length;

    public CanMessage(uint arbitrationId, IReadOnlyList<byte> data, bool isExtendedId = false)
    {
        if (data.Count > 8)
            throw new ArgumentOutOfRangeException(nameof(data), "Classic CAN payload must be <= 8 bytes.");

        ArbitrationId = arbitrationId;
        Data = data.ToArray();
        IsExtendedId = isExtendedId;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
```

### 6.3 CAN Bus Interface

```csharp
public interface ICanBus : IDisposable
{
    void Send(CanMessage message, int? timeoutMs = null);
    CanMessage? Recv(int? timeoutMs = null);
    PeriodicSendTask SendPeriodic(
        IReadOnlyList<CanMessage> messages,
        TimeSpan period,
        TimeSpan? duration = null,
        bool autostart = true,
        Func<CanMessage, CanMessage>? modifierCallback = null);
}
```

### 6.4 Vector Exceptions

Equivalent to `can.interfaces.vector.exceptions.VectorInitializationError`.

```csharp
public class VectorError : Exception
{
    public int? ErrorCode { get; }
    public string? FunctionName { get; }

    public VectorError(string message, int? errorCode = null, string? functionName = null)
        : base(message)
    {
        ErrorCode = errorCode;
        FunctionName = functionName;
    }
}

public sealed class VectorInitializationError : VectorError
{
    public VectorInitializationError(string message, int? errorCode = null, string? functionName = null)
        : base(message, errorCode, functionName)
    {
    }
}

public sealed class VectorOperationError : VectorError
{
    public VectorOperationError(string message, int? errorCode = null, string? functionName = null)
        : base(message, errorCode, functionName)
    {
    }
}
```

### 6.5 XL API Error Checker

```csharp
public static class XlErrorChecker
{
    public static void CheckInitialization(int status, string functionName)
    {
        if (status == XlConstants.XL_SUCCESS)
            return;

        throw new VectorInitializationError(GetMessage(status, functionName), status, functionName);
    }

    public static void CheckOperation(int status, string functionName)
    {
        if (status == XlConstants.XL_SUCCESS)
            return;

        throw new VectorOperationError(GetMessage(status, functionName), status, functionName);
    }

    private static string GetMessage(int status, string functionName)
    {
        string text = XlApi.xlGetErrorString(status);
        return $"{functionName} failed: {text} ({status})";
    }
}
```

### 6.6 Vector Channel Config

Equivalent to `can.interfaces.vector.get_channel_configs()`.

```csharp
public sealed class VectorChannelConfig
{
    public int ChannelIndex { get; init; }
    public ulong ChannelMask { get; init; }
    public string Name { get; init; } = "";
    public int HwType { get; init; }
    public int HwIndex { get; init; }
    public int HwChannel { get; init; }
    public int SerialNumber { get; init; }
    public int ConnectedBusType { get; init; }
    public string TransceiverName { get; init; } = "";

    public bool IsCan => (ConnectedBusType & XlConstants.XL_BUS_TYPE_CAN) != 0;
}
```

### 6.7 Vector Application Config

Equivalent to `set_application_config(...)` and `get_application_config(...)`.

```csharp
public sealed class VectorApplicationConfig
{
    public string AppName { get; init; } = "";
    public int AppChannel { get; init; }
    public int HwType { get; init; }
    public int HwIndex { get; init; }
    public int HwChannel { get; init; }
}
```

### 6.8 VectorBus

Equivalent to `can.interface.Bus(interface="vector", ...)`.

```csharp
public sealed class VectorBus : ICanBus
{
    private readonly string _appName;
    private readonly int _appChannel;
    private readonly int _bitrate;
    private long _portHandle;
    private ulong _channelMask;
    private ulong _permissionMask;
    private bool _disposed;
    private readonly List<PeriodicSendTask> _periodicTasks = new();

    public VectorBus(string appName, int channel, int bitrate)
    {
        _appName = appName;
        _appChannel = channel;
        _bitrate = bitrate;

        OpenDriver();
        OpenConfiguredChannel();
    }

    public static IReadOnlyList<VectorChannelConfig> GetChannelConfigs()
    {
        int status = XlApi.xlOpenDriver();
        XlErrorChecker.CheckInitialization(status, nameof(XlApi.xlOpenDriver));

        try
        {
            XlDriverConfig driverConfig = default;
            status = XlApi.xlGetDriverConfig(ref driverConfig);
            XlErrorChecker.CheckInitialization(status, nameof(XlApi.xlGetDriverConfig));

            var channels = new List<VectorChannelConfig>();

            for (int i = 0; i < driverConfig.ChannelCount; i++)
            {
                XlChannelConfig native = driverConfig.Channel[i];

                channels.Add(new VectorChannelConfig
                {
                    ChannelIndex = i,
                    ChannelMask = native.ChannelMask,
                    Name = native.Name,
                    HwType = native.HwType,
                    HwIndex = native.HwIndex,
                    HwChannel = native.HwChannel,
                    SerialNumber = native.SerialNumber,
                    ConnectedBusType = native.ConnectedBusType,
                    TransceiverName = native.TransceiverName
                });
            }

            return channels;
        }
        finally
        {
            XlApi.xlCloseDriver();
        }
    }

    public static void SetApplicationConfig(string appName, int appChannel, int hwType, int hwIndex, int hwChannel)
    {
        int status = XlApi.xlSetApplConfig(
            appName,
            appChannel,
            hwType,
            hwIndex,
            hwChannel,
            XlConstants.XL_BUS_TYPE_CAN);

        XlErrorChecker.CheckInitialization(status, nameof(XlApi.xlSetApplConfig));
    }

    public static VectorApplicationConfig GetApplicationConfig(string appName, int appChannel)
    {
        int hwType = 0;
        int hwIndex = 0;
        int hwChannel = 0;
        int busType = 0;

        int status = XlApi.xlGetApplConfig(
            appName,
            appChannel,
            ref hwType,
            ref hwIndex,
            ref hwChannel,
            ref busType);

        XlErrorChecker.CheckInitialization(status, nameof(XlApi.xlGetApplConfig));

        if (busType != XlConstants.XL_BUS_TYPE_CAN)
            throw new VectorInitializationError($"Application {appName}:{appChannel} is not configured for CAN.");

        return new VectorApplicationConfig
        {
            AppName = appName,
            AppChannel = appChannel,
            HwType = hwType,
            HwIndex = hwIndex,
            HwChannel = hwChannel
        };
    }

    public static void PopupVectorHwConfiguration(int waitForFinishMs = 0)
    {
        int status = XlApi.xlPopupHwConfig(null, waitForFinishMs);
        XlErrorChecker.CheckOperation(status, nameof(XlApi.xlPopupHwConfig));
    }

    public void Send(CanMessage message, int? timeoutMs = null)
    {
        ThrowIfDisposed();

        XlCanEvent txEvent = XlCanEvent.FromCanMessage(message);
        uint messageCount = 1;

        DateTime deadline = timeoutMs.HasValue
            ? DateTime.UtcNow.AddMilliseconds(timeoutMs.Value)
            : DateTime.MaxValue;

        while (true)
        {
            int status = XlApi.xlCanTransmit(_portHandle, _channelMask, ref messageCount, ref txEvent);

            if (status == XlConstants.XL_SUCCESS)
                return;

            if (status != XlConstants.XL_ERR_QUEUE_IS_FULL)
                XlErrorChecker.CheckOperation(status, nameof(XlApi.xlCanTransmit));

            if (!timeoutMs.HasValue || DateTime.UtcNow >= deadline)
                throw new TimeoutException("Vector transmit queue is full.");

            Thread.Sleep(1);
        }
    }

    public CanMessage? Recv(int? timeoutMs = null)
    {
        ThrowIfDisposed();

        DateTime deadline = timeoutMs.HasValue
            ? DateTime.UtcNow.AddMilliseconds(timeoutMs.Value)
            : DateTime.MaxValue;

        while (true)
        {
            uint eventCount = 1;
            XlCanEvent rxEvent = default;
            int status = XlApi.xlReceive(_portHandle, ref eventCount, ref rxEvent);

            if (status == XlConstants.XL_SUCCESS && eventCount > 0)
            {
                CanMessage? message = XlCanEvent.ToCanMessage(rxEvent);
                if (message != null)
                    return message;
            }
            else if (status != XlConstants.XL_ERR_QUEUE_IS_EMPTY)
            {
                XlErrorChecker.CheckOperation(status, nameof(XlApi.xlReceive));
            }

            if (timeoutMs.HasValue && DateTime.UtcNow >= deadline)
                return null;

            Thread.Sleep(1);
        }
    }

    public PeriodicSendTask SendPeriodic(
        IReadOnlyList<CanMessage> messages,
        TimeSpan period,
        TimeSpan? duration = null,
        bool autostart = true,
        Func<CanMessage, CanMessage>? modifierCallback = null)
    {
        var task = new PeriodicSendTask(Send, messages, period, duration, modifierCallback);
        _periodicTasks.Add(task);

        if (autostart)
            task.Start();

        return task;
    }

    private void OpenDriver()
    {
        int status = XlApi.xlOpenDriver();
        XlErrorChecker.CheckInitialization(status, nameof(XlApi.xlOpenDriver));
    }

    private void OpenConfiguredChannel()
    {
        VectorApplicationConfig config = GetApplicationConfig(_appName, _appChannel);

        _channelMask = XlApi.xlGetChannelMask(config.HwType, config.HwIndex, config.HwChannel);
        if (_channelMask == 0)
            throw new VectorInitializationError("xlGetChannelMask returned 0.");

        _permissionMask = _channelMask;

        int status = XlApi.xlOpenPort(
            ref _portHandle,
            _appName,
            _channelMask,
            ref _permissionMask,
            rxQueueSize: 16384,
            interfaceVersion: XlConstants.XL_INTERFACE_VERSION,
            busType: XlConstants.XL_BUS_TYPE_CAN);

        XlErrorChecker.CheckInitialization(status, nameof(XlApi.xlOpenPort));

        status = XlApi.xlCanSetChannelBitrate(_portHandle, _channelMask, _bitrate);
        XlErrorChecker.CheckInitialization(status, nameof(XlApi.xlCanSetChannelBitrate));

        status = XlApi.xlActivateChannel(
            _portHandle,
            _channelMask,
            XlConstants.XL_BUS_TYPE_CAN,
            XlConstants.XL_ACTIVATE_RESET_CLOCK);

        XlErrorChecker.CheckInitialization(status, nameof(XlApi.xlActivateChannel));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(VectorBus));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (PeriodicSendTask task in _periodicTasks)
            task.Dispose();

        if (_portHandle != 0)
        {
            XlApi.xlDeactivateChannel(_portHandle, _channelMask);
            XlApi.xlClosePort(_portHandle);
            _portHandle = 0;
        }

        XlApi.xlCloseDriver();
        _disposed = true;
    }
}
```

### 6.9 Periodic Send Task

Equivalent to `send_periodic(...)`.

```csharp
public sealed class PeriodicSendTask : IDisposable
{
    private readonly Action<CanMessage, int?> _send;
    private readonly IReadOnlyList<CanMessage> _messages;
    private readonly TimeSpan _period;
    private readonly TimeSpan? _duration;
    private readonly Func<CanMessage, CanMessage>? _modifierCallback;
    private CancellationTokenSource? _cts;

    public PeriodicSendTask(
        Action<CanMessage, int?> send,
        IReadOnlyList<CanMessage> messages,
        TimeSpan period,
        TimeSpan? duration,
        Func<CanMessage, CanMessage>? modifierCallback)
    {
        _send = send;
        _messages = messages;
        _period = period;
        _duration = duration;
        _modifierCallback = modifierCallback;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        Task.Run(async () =>
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
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
```

### 6.10 Vector Hardware Connect Helper

This is the function WinForms should call when hardware is connected.

```csharp
public static class VectorConnectionService
{
    public static IReadOnlyList<VectorChannelConfig> DetectVectorCanChannels()
    {
        return VectorBus
            .GetChannelConfigs()
            .Where(channel => channel.IsCan)
            .ToList();
    }

    public static void AddDtcCanApplicationToChannel(VectorChannelConfig selectedChannel)
    {
        VectorBus.SetApplicationConfig(
            appName: DtcCanConstants.AppName,
            appChannel: DtcCanConstants.AppChannel,
            hwType: selectedChannel.HwType,
            hwIndex: selectedChannel.HwIndex,
            hwChannel: selectedChannel.HwChannel);
    }

    public static VectorBus OpenDtcCanBus()
    {
        return new VectorBus(
            appName: DtcCanConstants.AppName,
            channel: DtcCanConstants.AppChannel,
            bitrate: DtcCanConstants.Bitrate);
    }

    public static VectorBus ConfigureAndOpen(VectorChannelConfig selectedChannel)
    {
        AddDtcCanApplicationToChannel(selectedChannel);
        return OpenDtcCanBus();
    }
}
```

WinForms example:

```csharp
private void ConnectButton_Click(object sender, EventArgs e)
{
    try
    {
        IReadOnlyList<VectorChannelConfig> channels = VectorConnectionService.DetectVectorCanChannels();

        if (channels.Count == 0)
        {
            VectorBus.PopupVectorHwConfiguration();
            MessageBox.Show("No Vector CAN channel detected.");
            return;
        }

        VectorChannelConfig selected = channels[0]; // Replace by ComboBox selection.
        _bus = VectorConnectionService.ConfigureAndOpen(selected);

        MessageBox.Show("Connected to Vector CAN channel with application name DTC_CAN.");
    }
    catch (VectorError ex)
    {
        MessageBox.Show(ex.Message, "Vector XL API Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
```

### 6.11 ISO-TP Client

```csharp
public sealed class IsoTpClient
{
    private readonly ICanBus _bus;
    private readonly int _p2TimeoutMs;
    private readonly int _p2StarTimeoutMs;

    public IsoTpClient(ICanBus bus, int p2TimeoutMs = 1000, int p2StarTimeoutMs = 5000)
    {
        _bus = bus;
        _p2TimeoutMs = p2TimeoutMs;
        _p2StarTimeoutMs = p2StarTimeoutMs;
    }

    public byte[] SendAndReceive(uint requestId, uint responseId, byte[] udsPayload)
    {
        SendSingleFrameRequest(requestId, udsPayload);
        return ReceiveResponse(requestId, responseId);
    }

    private void SendSingleFrameRequest(uint requestId, byte[] udsPayload)
    {
        if (udsPayload.Length > 7)
            throw new NotSupportedException("This implementation only sends single-frame UDS requests.");

        byte[] frame = new byte[8];
        frame[0] = (byte)udsPayload.Length;
        Array.Copy(udsPayload, 0, frame, 1, udsPayload.Length);

        _bus.Send(new CanMessage(requestId, frame), timeoutMs: 1000);
    }

    private byte[] ReceiveResponse(uint requestId, uint responseId)
    {
        var payload = new List<byte>();
        int expectedLength = -1;
        int nextSequence = 1;
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(_p2TimeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            int remaining = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
            CanMessage? frame = _bus.Recv(remaining);

            if (frame == null)
                continue;

            if (frame.ArbitrationId != responseId)
                continue;

            byte pci = frame.Data[0];
            byte frameType = (byte)(pci & 0xF0);

            if (IsNegativeResponsePending(frame.Data))
            {
                deadline = DateTime.UtcNow.AddMilliseconds(_p2StarTimeoutMs);
                continue;
            }

            if (IsNegativeResponse(frame.Data))
                throw new UdsNegativeResponseException(frame.Data[2]);

            if (frameType == 0x00)
            {
                int length = pci & 0x0F;
                return frame.Data.Skip(1).Take(length).ToArray();
            }

            if (frameType == 0x10)
            {
                expectedLength = ((pci & 0x0F) << 8) | frame.Data[1];
                payload.AddRange(frame.Data.Skip(2).Take(6));
                SendFlowControl(requestId);
                deadline = DateTime.UtcNow.AddMilliseconds(_p2StarTimeoutMs);
                continue;
            }

            if (frameType == 0x20)
            {
                int sequence = pci & 0x0F;
                if (sequence != nextSequence)
                    throw new InvalidOperationException($"ISO-TP sequence error. Expected {nextSequence}, got {sequence}.");

                nextSequence = (nextSequence + 1) & 0x0F;
                payload.AddRange(frame.Data.Skip(1).Take(7));

                if (expectedLength > 0 && payload.Count >= expectedLength)
                    return payload.Take(expectedLength).ToArray();
            }
        }

        throw new TimeoutException("Timeout while waiting for ISO-TP response.");
    }

    private void SendFlowControl(uint requestId)
    {
        _bus.Send(new CanMessage(requestId, DtcCanConstants.FlowControlContinueToSend), timeoutMs: 1000);
    }

    private static bool IsNegativeResponse(byte[] data)
    {
        return data.Length >= 4 && data[1] == DtcCanConstants.NegativeResponse;
    }

    private static bool IsNegativeResponsePending(byte[] data)
    {
        return data.Length >= 4
            && data[1] == DtcCanConstants.NegativeResponse
            && data[3] == DtcCanConstants.NrcResponsePending;
    }
}
```

### 6.12 UDS Client

```csharp
public sealed class UdsClient
{
    private readonly IsoTpClient _isoTp;

    public UdsClient(IsoTpClient isoTp)
    {
        _isoTp = isoTp;
    }

    public byte[] ReadDtcByStatusMask(uint requestId, uint responseId, byte statusMask)
    {
        byte[] request = { 0x19, 0x02, statusMask };
        byte[] response = _isoTp.SendAndReceive(requestId, responseId, request);

        if (response.Length < 3 || response[0] != 0x59 || response[1] != 0x02)
            throw new InvalidOperationException("Invalid ReadDTCInformation positive response.");

        return response;
    }

    public string ReadVin(uint requestId, uint responseId)
    {
        byte[] response = _isoTp.SendAndReceive(requestId, responseId, DtcCanConstants.ReadVin);

        if (response.Length < 3 || response[0] != 0x62 || response[1] != 0xF1 || response[2] != 0x90)
            throw new InvalidOperationException("Invalid VIN positive response.");

        return Encoding.ASCII.GetString(response.Skip(3).ToArray()).TrimEnd('\0');
    }
}
```

### 6.13 DTC Decoder

```csharp
public sealed class DtcRecord
{
    public string Code { get; init; } = "";
    public byte Status { get; init; }
    public byte[] RawBytes { get; init; } = Array.Empty<byte>();
}

public static class DtcDecoder
{
    public static IReadOnlyList<DtcRecord> ParseReadDtcResponse(byte[] response)
    {
        if (response.Length < 3)
            throw new InvalidOperationException("ReadDTCInformation response is too short.");

        if (response[0] != 0x59 || response[1] != 0x02)
            throw new InvalidOperationException("Response is not 59 02.");

        byte[] records = response.Skip(3).ToArray();

        if (records.Length == 0)
            return Array.Empty<DtcRecord>();

        if (records.Length % 4 != 0)
            throw new InvalidOperationException("DTC record payload length is not divisible by 4.");

        var result = new List<DtcRecord>();

        for (int i = 0; i < records.Length; i += 4)
        {
            byte b1 = records[i];
            byte b2 = records[i + 1];
            byte b3 = records[i + 2];
            byte status = records[i + 3];

            result.Add(new DtcRecord
            {
                Code = DecodeDtcCode(b1, b2, b3),
                Status = status,
                RawBytes = new[] { b1, b2, b3, status }
            });
        }

        return result;
    }

    public static string DecodeDtcCode(byte b1, byte b2, byte b3)
    {
        string[] type = { "P", "C", "B", "U" };

        int dtcType = (b1 & 0xC0) >> 6;
        int digit2 = (b1 & 0x30) >> 4;
        int digit3 = b1 & 0x0F;

        return type[dtcType]
            + digit2.ToString("X1")
            + digit3.ToString("X1")
            + b2.ToString("X2")
            + b3.ToString("X2");
    }
}
```

### 6.14 DTC Read Service

```csharp
public sealed class DtcReadJob
{
    public string Node { get; init; } = "";
    public uint RequestId { get; init; }
    public uint ResponseId { get; init; }
    public string DtcMappingFile { get; init; } = "";
}

public sealed class DtcReadResult
{
    public string Node { get; init; } = "";
    public uint RequestId { get; init; }
    public uint ResponseId { get; init; }
    public string ResultType { get; init; } = "";
    public string? DtcCode { get; init; }
    public string? DtcName { get; init; }
    public byte? Status { get; init; }
    public byte? Nrc { get; init; }
    public byte[] RawPayload { get; init; } = Array.Empty<byte>();
}

public sealed class DtcReadService
{
    private readonly UdsClient _udsClient;
    private readonly DtcMappingRepository _mappingRepository;

    public DtcReadService(UdsClient udsClient, DtcMappingRepository mappingRepository)
    {
        _udsClient = udsClient;
        _mappingRepository = mappingRepository;
    }

    public IReadOnlyList<DtcReadResult> ReadDtc(IReadOnlyList<DtcReadJob> jobs)
    {
        var results = new List<DtcReadResult>();

        foreach (DtcReadJob job in jobs)
        {
            try
            {
                byte[] response = _udsClient.ReadDtcByStatusMask(job.RequestId, job.ResponseId, 0x09);
                IReadOnlyList<DtcRecord> records = DtcDecoder.ParseReadDtcResponse(response);

                if (records.Count == 0)
                {
                    results.Add(CreateBaseResult(job, "No DTC", response));
                    continue;
                }

                IReadOnlyDictionary<string, string> dtcMap = _mappingRepository.Load(job.DtcMappingFile);

                foreach (DtcRecord record in records)
                {
                    dtcMap.TryGetValue(record.Code, out string? name);

                    results.Add(new DtcReadResult
                    {
                        Node = job.Node,
                        RequestId = job.RequestId,
                        ResponseId = job.ResponseId,
                        ResultType = "DTC",
                        DtcCode = record.Code,
                        DtcName = name ?? "Not defined",
                        Status = record.Status,
                        RawPayload = response
                    });
                }
            }
            catch (TimeoutException)
            {
                results.Add(CreateBaseResult(job, "No response", Array.Empty<byte>()));
            }
            catch (UdsNegativeResponseException ex)
            {
                results.Add(new DtcReadResult
                {
                    Node = job.Node,
                    RequestId = job.RequestId,
                    ResponseId = job.ResponseId,
                    ResultType = "Negative Response",
                    Nrc = ex.Nrc
                });
            }
        }

        return results;
    }

    private static DtcReadResult CreateBaseResult(DtcReadJob job, string resultType, byte[] rawPayload)
    {
        return new DtcReadResult
        {
            Node = job.Node,
            RequestId = job.RequestId,
            ResponseId = job.ResponseId,
            ResultType = resultType,
            RawPayload = rawPayload
        };
    }
}
```

### 6.15 UDS Negative Response Exception

```csharp
public sealed class UdsNegativeResponseException : Exception
{
    public byte Nrc { get; }

    public UdsNegativeResponseException(byte nrc)
        : base($"UDS negative response NRC=0x{nrc:X2}")
    {
        Nrc = nrc;
    }
}
```

### 6.16 DTC Mapping Repository

```csharp
public sealed class DtcMappingRepository
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache = new();

    public IReadOnlyDictionary<string, string> Load(string filePath)
    {
        if (_cache.TryGetValue(filePath, out IReadOnlyDictionary<string, string>? cached))
            return cached;

        string json = File.ReadAllText(filePath);
        Dictionary<string, string>? data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        if (data == null)
            throw new InvalidOperationException($"Invalid DTC mapping JSON: {filePath}");

        _cache[filePath] = data;
        return data;
    }
}
```

---

## 7. Native XL API Skeleton

The exact struct layout must be verified against the installed Vector XL Driver Library version.

```csharp
internal static class XlApi
{
    private const string DllName = "vxlapi.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlOpenDriver();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlCloseDriver();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int xlOpenPort(
        ref long portHandle,
        string userName,
        ulong accessMask,
        ref ulong permissionMask,
        uint rxQueueSize,
        uint interfaceVersion,
        uint busType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlClosePort(long portHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlActivateChannel(long portHandle, ulong accessMask, uint busType, uint flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlDeactivateChannel(long portHandle, ulong accessMask);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlCanSetChannelBitrate(long portHandle, ulong accessMask, int bitrate);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlCanTransmit(long portHandle, ulong accessMask, ref uint messageCount, ref XlCanEvent xlEvent);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlReceive(long portHandle, ref uint eventCount, ref XlCanEvent xlEvent);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int xlGetApplConfig(
        string appName,
        int appChannel,
        ref int hwType,
        ref int hwIndex,
        ref int hwChannel,
        ref int busType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int xlSetApplConfig(
        string appName,
        int appChannel,
        int hwType,
        int hwIndex,
        int hwChannel,
        int busType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong xlGetChannelMask(int hwType, int hwIndex, int hwChannel);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xlGetDriverConfig(ref XlDriverConfig driverConfig);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int xlPopupHwConfig(string? callSign, int waitForFinishMs);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr xlGetErrorStringNative(int errorCode);

    public static string xlGetErrorString(int errorCode)
    {
        IntPtr ptr = xlGetErrorStringNative(errorCode);
        return Marshal.PtrToStringAnsi(ptr) ?? $"XL error {errorCode}";
    }
}
```

---

## 8. Implementation Order

Recommended build order:

```text
1. Implement Native XL API declarations and structs.
2. Implement Vector exceptions and XlErrorChecker.
3. Implement VectorBus.GetChannelConfigs().
4. Implement SetApplicationConfig("DTC_CAN", ...).
5. Implement VectorBus open/send/recv.
6. Implement IsoTpClient.
7. Implement UdsClient.
8. Implement DtcDecoder.
9. Implement ConfigRepository and DtcMappingRepository.
10. Connect to WinForms UI.
```

First hardware test:

```text
1. Plug Vector hardware.
2. Detect CAN channels.
3. Select one channel.
4. Set application config DTC_CAN.
5. Open bus.
6. Send 03 19 02 09 00 00 00 00 to one known ECU.
7. Receive response by expected response ID.
```

