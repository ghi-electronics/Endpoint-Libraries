////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using GHIElectronics.Endpoint.UI.Media;

namespace GHIElectronics.Endpoint.UI.Controls {
    public class Control : UIElement {
        public Media.Brush Background {
            get {
                this.VerifyAccess();

                return this._background;
            }

            set {
                this.VerifyAccess();

                this._background = value;
                this.Invalidate();
            }
        }

        public GHIElectronics.Endpoint.Drawing.Font Font {
            get => this._font;

            set {
                this.VerifyAccess();

                this._font = value;
                this.InvalidateMeasure();
            }
        }

        public Media.Brush Foreground {
            get {
                this.VerifyAccess();

                return this._foreground;
            }

            set {
                this.VerifyAccess();

                this._foreground = value;
                this.Invalidate();
            }
        }

        public override void OnRender(DrawingContext dc) {
            if (this._background != null) {
                dc.DrawRectangle(this._background, null, 0, 0, this._renderWidth, this._renderHeight);
            }
        }

        protected internal Media.Brush _background = null;
        protected internal Media.Brush _foreground = new SolidColorBrush(Colors.Black);
        protected internal GHIElectronics.Endpoint.Drawing.Font _font;
    }
}


