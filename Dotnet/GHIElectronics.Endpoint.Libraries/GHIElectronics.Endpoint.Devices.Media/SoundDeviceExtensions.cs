using Iot.Device.Media;

namespace GHIElectronics.Endpoint.Devices.Media {
    /// <summary>
    /// MP3 for .NET IoT's SoundDevice (Iot.Device.Media), which plays and records WAV,
    /// and sets the volume, on any sound card of the board:
    /// <code>
    /// using var sound = SoundDevice.Create(new SoundConnectionSettings());   // the first sound card
    /// sound.Play("/media/usb0/beep.wav");
    /// sound.PlayMp3("/media/usb0/song.mp3");
    /// </code>
    /// A USB sound card is "hw:1" (the DeviceName of its UsbHostController event);
    /// with more than one card, give it as PlaybackDeviceName = "plughw:1" (the "plug"
    /// converts any WAV to what the card plays) and MixerDeviceName = "hw:1".
    /// SoundDevice.Create(settings) turns the sound on with the card's first control:
    /// on a card whose first control has no playback switch it throws ("Set playback
    /// mute error"); then use SoundDevice.Create(settings, false).
    /// </summary>
    public static class SoundDeviceExtensions {
        /// <summary>Plays an MP3 file to its end (the calling thread waits).</summary>
        public static void PlayMp3(this SoundDevice device, string mp3Path) {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(mp3Path);
            using var wav = new Mp3WavStream(mp3Path);
            device.Play(wav);
        }

        /// <summary>Plays an MP3 from a stream to its end (the calling thread waits).</summary>
        public static void PlayMp3(this SoundDevice device, Stream mp3Stream) {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(mp3Stream);
            using var memory = new MemoryStream();
            mp3Stream.CopyTo(memory);
            using var wav = new Mp3WavStream(memory.ToArray());
            device.Play(wav);
        }
    }
}
