using System.Buffers.Binary;
using GHIElectronics.Endpoint.Core.Internal;

namespace GHIElectronics.Endpoint.Devices.Media {
    // An MP3 as a WAV stream (a 44-byte header, then 16-bit samples), decoded by
    // libendpoint while SoundDevice.Play reads it: a whole song is never in memory.
    // SoundDevice plays whole blocks, so the last one is filled with silence.
    internal sealed unsafe class Mp3WavStream : Stream {
        private readonly byte[] header = new byte[44];
        private readonly short[] samples = new short[8192];
        private nint decoder;
        private int headerAt;
        private int sampleAt;
        private int sampleCount;
        private bool ended;

        public Mp3WavStream(string path) {
            Libendpoint.Check(Libendpoint.Mp3Open(path, null, 0, out this.decoder, out var rate, out var channels, out var total));
            this.WriteHeader(rate, channels, total);
        }

        public Mp3WavStream(byte[] data) {
            fixed (byte* bytes = data) {
                Libendpoint.Check(Libendpoint.Mp3Open(null, bytes, data.Length, out this.decoder, out var rate, out var channels, out var total));
                this.WriteHeader(rate, channels, total);
            }
        }

        private void WriteHeader(int rate, int channels, long total) {
            var dataSize = (uint)Math.Min(total * 2, uint.MaxValue - 36);
            "RIFF"u8.CopyTo(this.header);
            BinaryPrimitives.WriteUInt32LittleEndian(this.header.AsSpan(4), 36 + dataSize);
            "WAVEfmt "u8.CopyTo(this.header.AsSpan(8));
            BinaryPrimitives.WriteUInt32LittleEndian(this.header.AsSpan(16), 16);
            BinaryPrimitives.WriteUInt16LittleEndian(this.header.AsSpan(20), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(this.header.AsSpan(22), (ushort)channels);
            BinaryPrimitives.WriteUInt32LittleEndian(this.header.AsSpan(24), (uint)rate);
            BinaryPrimitives.WriteUInt32LittleEndian(this.header.AsSpan(28), (uint)(rate * channels * 2));
            BinaryPrimitives.WriteUInt16LittleEndian(this.header.AsSpan(32), (ushort)(channels * 2));
            BinaryPrimitives.WriteUInt16LittleEndian(this.header.AsSpan(34), 16);
            "data"u8.CopyTo(this.header.AsSpan(36));
            BinaryPrimitives.WriteUInt32LittleEndian(this.header.AsSpan(40), dataSize);
        }

        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer) {
            ObjectDisposedException.ThrowIf(this.decoder == 0, this);
            var done = 0;
            while (done < buffer.Length && this.headerAt < this.header.Length) {
                buffer[done++] = this.header[this.headerAt++];
            }
            if (done > 0 || this.ended) {
                return done;
            }
            while (done + 1 < buffer.Length) {
                if (this.sampleAt == this.sampleCount) {
                    fixed (short* next = this.samples) {
                        Libendpoint.Check(Libendpoint.Mp3Read(this.decoder, next, this.samples.Length, out this.sampleCount));
                    }
                    this.sampleAt = 0;
                    if (this.sampleCount == 0) {
                        this.ended = true;
                        if (done == 0) {
                            return 0;
                        }
                        buffer[done..].Clear();  // silence to the end of the block
                        return buffer.Length;
                    }
                }
                var take = Math.Min(this.sampleCount - this.sampleAt, (buffer.Length - done) / 2);
                for (var i = 0; i < take; i++) {
                    BinaryPrimitives.WriteInt16LittleEndian(buffer[(done + 2 * i)..], this.samples[this.sampleAt + i]);
                }
                this.sampleAt += take;
                done += 2 * take;
            }
            return done;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            var old = Interlocked.Exchange(ref this.decoder, 0);
            if (old != 0) {
                Libendpoint.Mp3Close(old);
            }
            base.Dispose(disposing);
        }
    }
}
