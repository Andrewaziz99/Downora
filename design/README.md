# Design reference

`stitch/` is the Google Stitch output the WPF UI is built from (Phase 2 onward):

- `stitch/fluent_velocity/DESIGN.md` is the design system ("Fluent Velocity"): colors for dark and light mode, the type ramp, spacing, radii, status colors and component specs. Phase 2 turns it into WPF resource dictionaries.
- `stitch/<screen>/code.html` holds the screen mockups (HTML + Tailwind).
- `stitch/screens/*.png` holds 1440×900 renders of those mockups. Fonts fall back to a serif because Segoe UI isn't on the render machine.

The mockups contain sample data and some features that aren't in the product scope (for example NVENC hardware engine, bitrate spectrum, channel subscribers). The implementation plan decides what gets built. Anything not in scope is left out of the UI, not shown as a dead control.
