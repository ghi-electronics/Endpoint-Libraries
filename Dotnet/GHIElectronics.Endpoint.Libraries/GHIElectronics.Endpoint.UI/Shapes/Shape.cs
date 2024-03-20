////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using GHIElectronics.Endpoint.UI.Media;

namespace GHIElectronics.Endpoint.UI.Shapes {
    public abstract class Shape : UIElement {
        public Media.Brush Fill {
            get {
                this._fill ??= new SolidColorBrush(Colors.Black) {
                        Opacity = Bitmap.OpacityTransparent
                    };

                return this._fill;
            }

            set {
                this._fill = value;
                this.Invalidate();
            }
        }

        public Media.Pen Stroke {
            get {
                this._stroke ??= new Media.Pen(Colors.White, 0);

                return this._stroke;
            }

            set {
                this._stroke = value;
                this.Invalidate();
            }
        }

        private Media.Brush _fill;
        private Media.Pen _stroke;
    }
}


