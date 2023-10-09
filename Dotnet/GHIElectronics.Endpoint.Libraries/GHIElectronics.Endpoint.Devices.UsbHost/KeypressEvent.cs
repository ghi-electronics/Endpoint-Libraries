using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Devices.UsbHost
{
    public class KeyPressEvent : EventArgs
    {
        public KeyPressEvent(EventCode code, KeyState state)
        {
            this.Code = code;
            this.State = state;
        }

        public EventCode Code { get; }

        public KeyState State { get; }
    }
}
