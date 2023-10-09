using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Devices.UsbHost
{
    public class InputReader : IDisposable
    {
        public delegate void RaiseData(byte[] data);
        public delegate void RaiseDisconnected();

        public event RaiseData OnData;
        public event RaiseDisconnected OnDisconnected;


        private const int BufferLength = 16;

        private readonly byte[] _buffer = new byte[BufferLength];

        private FileStream _stream;
        private bool _disposing;
        

        public InputReader(string path)
        {
            this._stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            Task.Run(this.Run);
        }

        private void Run()
        {
            while (true)
            {
                if (this._disposing)
                    break;

                if (Directory.Exists("/dev/input/"))
                {
                    try
                    {
                        this._stream.Read(this._buffer, 0, BufferLength);
                        OnData?.Invoke(this._buffer);
                    }
                    catch { 
                    
                    }
                }
                else
                {
                    OnDisconnected?.Invoke();

                    break;
                }
            }
        }        
        public void Dispose()
        {
            this._disposing = true;
            this._stream.Dispose();
            this._stream = null;
        }
    }
}
