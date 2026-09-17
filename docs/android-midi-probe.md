# Android MIDI Probe

## Purpose

`Orchid.Presentation.Android` is a diagnostic application for verifying Android USB host and MIDI input support before the full Orchid UI is ported. It does not perform rhythm analysis, play a metronome, or generate piano audio.

The probe reports:

- device model and Android compatibility API level;
- USB host and MIDI feature flags;
- connected USB devices with vendor and product IDs;
- MIDI devices and their ports;
- live Note On and Note Off messages with note name, MIDI key number, channel, and velocity.

The APK targets ARM64 devices and requires Android API 24 or later. USB host and MIDI features are declared as optional so that the probe can install and report their actual availability.

## Build

Install the Android workload once:

```bash
dotnet workload install android
```

Build the debug APK:

```bash
dotnet build src/Orchid.Presentation.Android/Orchid.Presentation.Android.csproj \
  --configuration Debug
```

On WSL, the repository build rules place the signed APK at:

```text
/tmp/orchid-build/Orchid.Presentation.Android/bin/Debug/net10.0-android/android-arm64/io.github.policarpwq.orchid.midiprobe-Signed.apk
```

On Windows, it is written below `src/Orchid.Presentation.Android/bin/Windows/Debug/`.

## Install and verify

1. Copy the signed APK to the tablet and open it. Allow installation from that source if HarmonyOS requests it.
2. Start **Orchid MIDI Probe**.
3. Connect the piano to the tablet through a USB-C OTG adapter.
4. Tap **Refresh** after connecting or reconnecting the piano.
5. Confirm that the piano appears under both **USB devices** and **MIDI devices**.
6. Press and release piano keys. The output should contain `ON` and `OFF` entries.

Example:

```text
ON   C4   key  60  channel  1  velocity  82  source Digital Piano / port 0
OFF  C4   key  60  channel  1  velocity  41  source Digital Piano / port 0
```

If the USB device is listed but no MIDI device appears, the tablet sees the physical USB connection but does not expose the piano through the Android MIDI service. If neither entry appears, check the adapter, cable, and piano USB mode first.
