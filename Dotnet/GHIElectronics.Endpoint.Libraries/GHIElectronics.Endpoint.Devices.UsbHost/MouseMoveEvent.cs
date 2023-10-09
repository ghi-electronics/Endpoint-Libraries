using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronics.Endpoint.Devices.UsbHost
{
    public class MouseMoveEvent : EventArgs
    {
        public MouseMoveEvent(MouseAxis axis, int amount)
        {
            this.Axis = axis;
            this.Amount = amount;
        }

        public MouseAxis Axis { get; }

        public int Amount { get; set; }
    }
}
