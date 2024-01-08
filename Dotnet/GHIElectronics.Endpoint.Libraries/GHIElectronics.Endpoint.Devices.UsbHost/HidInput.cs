using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Devices.UsbHost
{
    public class HidInput : IDisposable
    {
        private List<InputReader> readers = new();

        public delegate void RaiseKeyPress(KeyPressEvent e);
        public delegate void RaiseMouseMove(MouseMoveEvent e);
        public delegate void RaiseDisconnected();


        public event RaiseKeyPress OnButtonPress;
        public event RaiseMouseMove OnMouseMove;
        public event RaiseDisconnected OnDisconnected;

        private bool disposed;
        public HidInput()
        {

            if (Directory.Exists("/dev/input/"))
            {
                var files = Directory.GetFiles("/dev/input/", "event*");

                foreach (var file in files)
                {
                    var reader = new InputReader(file);

                    reader.OnData += this.HandleOnData;
                    reader.OnDisconnected += this.ReaderOnDisconnected;

                    this.readers.Add(reader);
                }
            }
            else
            {
                throw new Exception("HID device not found.");
            }
        }

        private void ReaderOnDisconnected() => OnDisconnected?.Invoke();

        private void HandleOnData(byte[] data)
        {
            var type = BitConverter.ToInt16(new[] { data[8], data[9] }, 0);
            var code = BitConverter.ToInt16(new[] { data[10], data[11] }, 0);
            var value = BitConverter.ToInt32(new[] { data[12], data[13], data[14], data[15] }, 0);

            var eventType = (EventType)type;

            switch (eventType)
            {
                case EventType.EV_KEY:
                    this.HandleKeyPressEvent(code, value);
                    break;
                case EventType.EV_REL:
                    this.HandleMouseMoveEvent(code, value);
                    break;
            }
        }

        private void HandleKeyPressEvent(short code, int value) => OnButtonPress?.Invoke(new KeyPressEvent((EventCode)code, (KeyState)value));

        private void HandleMouseMoveEvent(short code, int value) => OnMouseMove?.Invoke(new MouseMoveEvent((MouseAxis)code, value));

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <exclude />
        protected void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                foreach (var reader in this.readers)
                {
                    reader.OnData -= this.HandleOnData;
                    reader.OnDisconnected -= this.ReaderOnDisconnected;
                    reader.Dispose();
                }

                this.readers.Clear();
                this.readers = null;
            }

            this.disposed = true;
        }
    }
}
