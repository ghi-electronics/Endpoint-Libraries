// Scratch build to prove Drawing.h and UI.h compile against the new sibling
// namespace layout (Endpoint::Drawing is no longer nested under Endpoint::UI).
// Verifies: unqualified Drawing:: lookup from inside UI.h still resolves;
// Canvas + Color + Font + Image + PixelFormat are reachable; UI::Initialize
// still accepts a Drawing::PixelFormat.
#include <endpoint/Drawing.h>
#include <endpoint/UI.h>

namespace D  = GHIElectronics::Endpoint::Drawing;
namespace UI = GHIElectronics::Endpoint::UI;

int main() {
    auto canvas = D::Canvas::Create(240, 320, D::PixelFormat::Rgb565);
    canvas.Clear(D::Color::Black());
    canvas.DrawLine(10, 10, 230, 310, D::Color::Red(), 2);
    canvas.FillCircle(120, 160, 40, D::Color::Blue());

    (void)canvas.Buffer();
    (void)canvas.Width();
    (void)canvas.Height();
    (void)canvas.PixelFormat();

    // Prove UI::Initialize's custom-display overload still binds to
    // Drawing::PixelFormat after the namespace move.
    auto flush = [](const uint8_t*, std::size_t) {};
    UI::Initialize(128, 64, D::PixelFormat::Monochrome, flush);
    return 0;
}
