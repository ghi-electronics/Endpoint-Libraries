using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Devices.UsbHost
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
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            Task.Run(Run);
        }

        private void Run()
        {
            while (true)
            {
                if (_disposing)
                    break;

                if (Directory.Exists("/dev/input/"))
                {
                    try
                    {
                        _stream.Read(_buffer, 0, BufferLength);
                        OnData?.Invoke(_buffer);
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
            _disposing = true;
            _stream.Dispose();
            _stream = null;
        }
    }
}
