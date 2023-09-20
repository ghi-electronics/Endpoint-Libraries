using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHIElectronic.Endpoint.Devices.UsbHost
{
    public class MouseMoveEvent : EventArgs
    {
        public MouseMoveEvent(MouseAxis axis, int amount)
        {
            Axis = axis;
            Amount = amount;
        }

        public MouseAxis Axis { get; }

        public int Amount { get; set; }
    }
}
