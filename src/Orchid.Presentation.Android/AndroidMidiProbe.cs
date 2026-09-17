using Android.Content;
using Android.Hardware.Usb;
using Android.Media.Midi;
using Android.OS;

namespace Orchid.Presentation.Android;

internal sealed class AndroidMidiProbe : IDisposable
{
    private readonly Context context;
    private readonly Action<string> writeLine;
    private readonly Handler mainHandler = new(Looper.MainLooper!);
    private readonly List<MidiDevice> openDevices = [];
    private readonly List<MidiOutputPort> openPorts = [];
    private readonly List<MidiMessageReceiver> receivers = [];
    private readonly List<DeviceOpenedListener> pendingListeners = [];
    private int scanGeneration;

    public AndroidMidiProbe(Context context, Action<string> writeLine)
    {
        this.context = context;
        this.writeLine = writeLine;
    }

    public void Scan()
    {
        CloseConnections();
        var generation = ++scanGeneration;

        WritePlatformInformation();
        WriteUsbDevices();
        OpenMidiDevices(generation);
    }

    public void Dispose()
    {
        scanGeneration++;
        CloseConnections();
        mainHandler.Dispose();
    }

    private void WritePlatformInformation()
    {
        var packageManager = context.PackageManager;

        writeLine("=== Platform ===");
        writeLine($"Device: {Build.Manufacturer} {Build.Model}");
        writeLine($"Android compatibility layer: {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt})");
        writeLine($"USB host feature: {FormatSupport(packageManager?.HasSystemFeature("android.hardware.usb.host") == true)}");
        writeLine($"MIDI feature: {FormatSupport(packageManager?.HasSystemFeature("android.software.midi") == true)}");
        writeLine(string.Empty);
    }

    private void WriteUsbDevices()
    {
        writeLine("=== USB devices ===");

        if (context.GetSystemService(Context.UsbService) is not UsbManager usbManager)
        {
            writeLine("USB manager is unavailable.");
            writeLine(string.Empty);
            return;
        }

        var deviceList = usbManager.DeviceList;
        var devices = deviceList is null
            ? []
            : deviceList.Values.OrderBy(device => device.DeviceName).ToArray();

        if (devices.Length == 0)
        {
            writeLine("No USB devices detected.");
        }

        foreach (var device in devices)
        {
            writeLine(
                $"{device.DeviceName}: VID 0x{device.VendorId:x4}, PID 0x{device.ProductId:x4}, " +
                $"class {device.DeviceClass}, interfaces {device.InterfaceCount}");
        }

        writeLine(string.Empty);
    }

    private void OpenMidiDevices(int generation)
    {
        writeLine("=== MIDI devices ===");

        if (context.GetSystemService(Context.MidiService) is not MidiManager midiManager)
        {
            writeLine("MIDI manager is unavailable on this device.");
            return;
        }

#pragma warning disable CS0618, CA1422
        var devices = (midiManager.GetDevices() ?? []).OrderBy(device => device.Id).ToArray();
#pragma warning restore CS0618, CA1422

        if (devices.Length == 0)
        {
            writeLine("No MIDI devices detected. Connect the piano and tap Refresh.");
            return;
        }

        foreach (var deviceInfo in devices)
        {
            var name = GetDeviceName(deviceInfo);
            writeLine(
                $"[{deviceInfo.Id}] {name}: {deviceInfo.OutputPortCount} output port(s), " +
                $"{deviceInfo.InputPortCount} input port(s)");

            var listener = new DeviceOpenedListener(
                device => OnDeviceOpened(deviceInfo, name, device, generation));
            pendingListeners.Add(listener);
            midiManager.OpenDevice(deviceInfo, listener, mainHandler);
        }
    }

    private void OnDeviceOpened(
        MidiDeviceInfo deviceInfo,
        string deviceName,
        MidiDevice? device,
        int generation)
    {
        if (generation != scanGeneration)
        {
            device?.Dispose();
            return;
        }

        if (device is null)
        {
            writeLine($"Could not open MIDI device '{deviceName}'.");
            return;
        }

        openDevices.Add(device);
        var connectedPortCount = 0;

        for (var portNumber = 0; portNumber < deviceInfo.OutputPortCount; portNumber++)
        {
            var outputPort = device.OpenOutputPort(portNumber);

            if (outputPort is null)
            {
                writeLine($"Could not open output port {portNumber} on '{deviceName}'.");
                continue;
            }

            var source = $"{deviceName} / port {portNumber}";
            var receiver = new MidiMessageReceiver(message => OnNoteReceived(source, message));
            outputPort.Connect(receiver);
            openPorts.Add(outputPort);
            receivers.Add(receiver);
            connectedPortCount++;
        }

        writeLine($"Listening to '{deviceName}' on {connectedPortCount} output port(s).");
    }

    private void OnNoteReceived(string source, MidiNoteMessage message)
    {
        var action = message.IsPressed ? "ON " : "OFF";
        var line =
            $"{action}  {MidiNoteName.Get(message.MidiNoteNumber),-4} " +
            $"key {message.MidiNoteNumber,3}  channel {message.Channel,2}  " +
            $"velocity {message.Velocity,3}  source {source}";

        mainHandler.Post(() => writeLine(line));
    }

    private void CloseConnections()
    {
        foreach (var port in openPorts)
        {
            port.Dispose();
        }

        foreach (var receiver in receivers)
        {
            receiver.Dispose();
        }

        foreach (var device in openDevices)
        {
            device.Dispose();
        }

        foreach (var listener in pendingListeners)
        {
            listener.Dispose();
        }

        openPorts.Clear();
        receivers.Clear();
        openDevices.Clear();
        pendingListeners.Clear();
    }

    private static string GetDeviceName(MidiDeviceInfo deviceInfo)
    {
        var properties = deviceInfo.Properties;

        return properties?.GetString(MidiDeviceInfo.PropertyName)
            ?? properties?.GetString(MidiDeviceInfo.PropertyProduct)
            ?? $"MIDI device {deviceInfo.Id}";
    }

    private static string FormatSupport(bool isSupported)
    {
        return isSupported ? "supported" : "not reported";
    }

    private sealed class DeviceOpenedListener(Action<MidiDevice?> deviceOpened)
        : Java.Lang.Object, MidiManager.IOnDeviceOpenedListener
    {
        public void OnDeviceOpened(MidiDevice? device)
        {
            deviceOpened(device);
        }
    }
}
