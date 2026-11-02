using Iot.Device.Media;

namespace GHIElectronics.Endpoint.Devices.Sound {
    /// <summary>
    /// MP3 for .NET IoT's <c>SoundDevice</c> (<c>Iot.Device.Media</c>), which
    /// plays and records WAV, and sets the volume, on any sound card of the
    /// board. A USB sound card is <c>"hw:1"</c> (the <c>DeviceName</c> of its
    /// <c>UsbHostController</c> event); with more than one card, give it as
    /// <c>PlaybackDeviceName = "plughw:1"</c> (the "plug" converts any WAV to
    /// what the card plays) and <c>MixerDeviceName = "hw:1"</c>.
    /// <c>SoundDevice.Create(settings)</c> turns the sound on with the card's
    /// first control: on a card whose first control has no playback switch it
    /// throws ("Set playback mute error"); then use
    /// <c>SoundDevice.Create(settings, false)</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// using var sound = SoundDevice.Create(new SoundConnectionSettings());   // the first sound card
    /// sound.Play("/media/usb0/beep.wav");
    /// sound.PlayMp3("/media/usb0/song.mp3");
    /// </code>
    /// </example>
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
