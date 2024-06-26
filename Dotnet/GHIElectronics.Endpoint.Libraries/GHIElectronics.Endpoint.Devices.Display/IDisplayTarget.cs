using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace GHIElectronics.Endpoint.Devices.Display {
    public interface IDisplayProvider : IDisposable {

        public DisplayConfiguration Configuration { get; }
        public void Flush(byte[] data) => this.Flush(data, 0, data.Length);
        public abstract void Flush(byte[] data, int offset, int length);

        public abstract void Flush(byte[] data, int offset, int length, int x, int y, int width, int height, int originalWidth);


    }
}
