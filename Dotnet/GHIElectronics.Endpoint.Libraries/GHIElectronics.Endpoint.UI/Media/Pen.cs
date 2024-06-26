////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using GHIElectronics.Endpoint.Drawing;

namespace GHIElectronics.Endpoint.UI.Media {
    public class Pen {
        public Color Color;
        public ushort Thickness;

        public Pen(Color color)
            : this(color, 1) {
        }

        public Pen(Color color, ushort thickness) {
            this.Color = color;
            this.Thickness = thickness;
        }
    }
}


