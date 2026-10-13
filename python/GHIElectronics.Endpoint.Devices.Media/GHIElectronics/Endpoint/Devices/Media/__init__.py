"""Webcams and sound of Endpoint.

Camera: any USB webcam (the DeviceName of a Webcam from UsbHostController):

    from GHIElectronics.Endpoint.Devices.Media import Camera

    with Camera(e.DeviceName) as camera:
        camera.Capture("/media/usb0/photo.jpg")      # a JPEG

Sound, with the API of .NET IoT's SoundDevice (Iot.Device.Media): play WAV and
MP3, record WAV, volume and mute, on any sound card of the board.

    from GHIElectronics.Endpoint.Devices.Media import SoundConnectionSettings, SoundDevice

    with SoundDevice.Create(SoundConnectionSettings()) as sound:   # the first sound card
        sound.Play("/media/usb0/beep.wav")
        sound.PlayMp3("/media/usb0/song.mp3")
        sound.Record(5, "/data/voice.wav")       # 5 s from the microphone

A USB sound card is "hw:1" (the DeviceName of its UsbHostController event):
with more than one card, give it as PlaybackDeviceName, RecordingDeviceName and
MixerDeviceName ("hw:" names are opened as "plughw:", which converts any WAV
to what the card plays). Play, PlayMp3 and Record wait until they are done.
Volumes are in the card's own units, as on .NET.
"""

import ctypes
import os

from GHIElectronics.Endpoint.Core._native import native

from ._camera import (Camera, CameraControl, CameraControlValue, CameraFormat, CameraPixelFormat,
                      FrameReceivedEventArgs)

__all__ = ["Camera", "CameraControl", "CameraControlValue", "CameraFormat", "CameraPixelFormat",
           "FrameReceivedEventArgs", "SoundConnectionSettings", "SoundDevice"]


class SoundConnectionSettings:
    """Which sound card plays, records and has the volume controls, and how recordings sound"""

    def __init__(self):
        self.PlaybackDeviceName = "default"
        self.RecordingDeviceName = "default"
        self.MixerDeviceName = "default"
        self.RecordingSampleRate = 8000
        self.RecordingChannels = 2
        self.RecordingBitsPerSample = 16

    def __repr__(self):
        return (f"SoundConnectionSettings(PlaybackDeviceName={self.PlaybackDeviceName!r}, "
                f"RecordingDeviceName={self.RecordingDeviceName!r}, MixerDeviceName={self.MixerDeviceName!r}, "
                f"RecordingSampleRate={self.RecordingSampleRate}, RecordingChannels={self.RecordingChannels}, "
                f"RecordingBitsPerSample={self.RecordingBitsPerSample})")


def _is_path(value):
    return isinstance(value, (str, os.PathLike))


class SoundDevice:
    @staticmethod
    def Create(settings, unmute=True):
        """A sound device; unmute: turn the sound on (playback and recording), as .NET does"""
        return SoundDevice(settings, unmute)

    def __init__(self, settings, unmute=True):
        self._settings = settings
        handle = native.out_handle()
        native.sound_create(settings.PlaybackDeviceName.encode(), settings.RecordingDeviceName.encode(),
                            settings.MixerDeviceName.encode(), int(settings.RecordingSampleRate),
                            int(settings.RecordingChannels), int(settings.RecordingBitsPerSample), 1 if unmute else 0,
                            native.ref(handle))
        self._handle = handle

    @property
    def Settings(self):
        return self._settings

    def _h(self):
        if self._handle is None:
            raise native.disposed("SoundDevice")
        return self._handle

    def _volume(self, capture):
        volume = native.out_int64()
        native.sound_get_volume(self._h(), capture, native.ref(volume))
        return volume.value

    def _mute(self, capture):
        mute = native.out_int()
        native.sound_get_mute(self._h(), capture, native.ref(mute))
        return bool(mute.value)

    @property
    def PlaybackVolume(self):
        """The card's playback volume, in its own units"""
        return self._volume(0)

    @PlaybackVolume.setter
    def PlaybackVolume(self, value):
        native.sound_set_volume(self._h(), 0, int(value))

    @property
    def PlaybackMute(self):
        return self._mute(0)

    @PlaybackMute.setter
    def PlaybackMute(self, value):
        native.sound_set_mute(self._h(), 0, 1 if value else 0)

    @property
    def RecordingVolume(self):
        """The card's recording volume, in its own units"""
        return self._volume(1)

    @RecordingVolume.setter
    def RecordingVolume(self, value):
        native.sound_set_volume(self._h(), 1, int(value))

    @property
    def RecordingMute(self):
        return self._mute(1)

    @RecordingMute.setter
    def RecordingMute(self, value):
        native.sound_set_mute(self._h(), 1, 1 if value else 0)

    def _play(self, source, mp3):
        if _is_path(source):
            native.sound_play_file(self._h(), os.fsencode(source), mp3)
        else:
            data = source.read() if hasattr(source, "read") else bytes(source)
            pointer, length, keep = native.readable(data)
            native.sound_play_buffer(self._h(), pointer, length, mp3)

    def Play(self, wav):
        """Plays a WAV: a file's path, an open file (binary) or its bytes"""
        self._play(wav, 0)

    def PlayMp3(self, mp3):
        """Plays an MP3: a file's path, an open file (binary) or its bytes"""
        self._play(mp3, 1)

    def Record(self, recordTimeSeconds, output):
        """Records a WAV for the given seconds into a file's path or an open file (binary)"""
        if _is_path(output):
            native.sound_record_file(self._h(), int(recordTimeSeconds), os.fsencode(output))
            return
        s = self._settings
        size = 44 + int(recordTimeSeconds) * s.RecordingSampleRate * s.RecordingChannels * s.RecordingBitsPerSample // 8
        buffer = ctypes.create_string_buffer(size)
        length = native.out_int()
        native.sound_record_buffer(self._h(), int(recordTimeSeconds), buffer, size, native.ref(length))
        output.write(buffer.raw[:length.value])

    def StartRecording(self, outputFilePath):
        """Records a WAV file in the background until StopRecording"""
        native.sound_start_recording(self._h(), os.fsencode(outputFilePath))

    def StopRecording(self):
        native.sound_stop_recording(self._h())

    def Dispose(self):
        if self._handle is not None:
            native.sound_dispose(self._handle)
            self._handle = None

    def __enter__(self):
        return self

    def __exit__(self, *exception):
        self.Dispose()

    def __del__(self):
        self.Dispose()
