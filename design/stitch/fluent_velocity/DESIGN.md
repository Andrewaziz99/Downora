---
name: Fluent Velocity
colors:
  surface: '#131315'
  surface-dim: '#131315'
  surface-bright: '#39393b'
  surface-container-lowest: '#0e0e10'
  surface-container-low: '#1b1b1d'
  surface-container: '#1f1f21'
  surface-container-high: '#2a2a2c'
  surface-container-highest: '#353437'
  on-surface: '#e4e2e4'
  on-surface-variant: '#bbcac7'
  inverse-surface: '#e4e2e4'
  inverse-on-surface: '#303032'
  outline: '#859491'
  outline-variant: '#3c4948'
  surface-tint: '#40dcd1'
  primary: '#43ded4'
  on-primary: '#003734'
  primary-container: '#00c2b8'
  on-primary-container: '#004a46'
  inverse-primary: '#006a64'
  secondary: '#b0c6ff'
  on-secondary: '#002d6e'
  secondary-container: '#0068ec'
  on-secondary-container: '#f2f3ff'
  tertiary: '#dfbbff'
  on-tertiary: '#490080'
  tertiary-container: '#cc97ff'
  on-tertiary-container: '#6100a7'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#65f8ed'
  primary-fixed-dim: '#40dcd1'
  on-primary-fixed: '#00201e'
  on-primary-fixed-variant: '#00504b'
  secondary-fixed: '#d9e2ff'
  secondary-fixed-dim: '#b0c6ff'
  on-secondary-fixed: '#001944'
  on-secondary-fixed-variant: '#00429a'
  tertiary-fixed: '#f0dbff'
  tertiary-fixed-dim: '#ddb7ff'
  on-tertiary-fixed: '#2c0051'
  on-tertiary-fixed-variant: '#6900b3'
  background: '#131315'
  on-background: '#e4e2e4'
  surface-variant: '#353437'
typography:
  display:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 28px
    fontWeight: '600'
    lineHeight: 36px
    letterSpacing: -0.015em
  title-lg:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 26px
    letterSpacing: -0.01em
  title-md:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 22px
    letterSpacing: -0.005em
  body-lg:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: 0em
  body-md:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
    letterSpacing: 0em
  body-strong:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 13px
    fontWeight: '600'
    lineHeight: 18px
    letterSpacing: 0em
  caption:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
    letterSpacing: 0.01em
  caption-strong:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.01em
  badge-label:
    fontFamily: Segoe UI Variable, Segoe UI, -apple-system, system-ui
    fontSize: 11px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: 0.02em
  mono-metric:
    fontFamily: JetBrains Mono, Segoe UI Mono, Consolas, monospace
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 1rem
  gutter-compact: 0.5rem
  margin: 1.5rem
  margin-window: 0.75rem
  space-2xs: 0.125rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 0.75rem
  space-base: 1rem
  space-lg: 1.25rem
  space-xl: 1.5rem
  space-2xl: 2rem
---

## Brand & Style

This design system defines an ultra-premium, high-performance native Windows 11 desktop experience engineered for intensive media aggregation and file queue processing. The interface reflects the precision, depth, and material tactility of Microsoft Fluent Design System 2 and WinUI 3, harmonizing translucent spatial materiality with utilitarian speed.

### Aesthetics & Philosophy
- **Mica & Acrylic Materiality:** Tiered structural layers mimic Windows 11 desktop compositor surfaces. The window shell anchors on a dynamic base canvas, supporting translucent cards, elevated flyouts, and subtle noise/blur refraction.
- **Micro-Delicate Contours:** Boundaries rely on hairline 1px interior keylines and dynamic rim lighting rather than heavy, opaque borders.
- **Precision Engineering:** Information architecture accommodates high-density data queues, concurrent transfer progress bars, and FFmpeg terminal telemetry without clutter or visual friction.
- **Kinetic Fluency:** Interactive states prioritize rapid feedback with subtle 150ms spring transitions, directional scale cues, and luminous focus outlines that respect native keyboard navigation.

## Colors

The color architecture is built around deep dark mode defaults with dynamic high-contrast light mode parity. It employs deliberate chromatic roles for operational states, queue telemetry, and background surface tiers.

### Dark Mode Canvas (Default)
- **Base Canvas (Mica Background):** `#1C1C1E`
- **Surface Layer Card (Elevation 1):** `#2A2A2D`
- **Secondary Surface Card / Flyout (Elevation 2):** `#323236`
- **Interactive Layer Hover:** `#3C3C41`
- **Border Outline Default:** `rgba(255, 255, 255, 0.07)`
- **Border Outline Subdued:** `rgba(255, 255, 255, 0.04)`
- **Text Primary:** `#FFFFFF`
- **Text Secondary:** `#A1A1A6`
- **Text Tertiary:** `#6E6E73`

### Light Mode Canvas
- **Base Canvas (Mica Background):** `#F3F3F5`
- **Surface Layer Card (Elevation 1):** `#FFFFFF`
- **Secondary Surface Card (Elevation 2):** `#F8F8FA`
- **Border Outline Default:** `#E5E5EA`
- **Border Outline Subdued:** `rgba(0, 0, 0, 0.04)`
- **Text Primary:** `#1C1C1E`
- **Text Secondary:** `#636366`
- **Text Tertiary:** `#8E8E93`

### Accents & Gradients
- **Primary Accent Teal:** `#00C2B8`
- **Secondary Accent Royal Blue:** `#2D7BFF`
- **Hero Accent Gradient:** Linear 135deg from `#00C2B8` to `#2D7BFF` (utilized across primary action buttons, active progress tracks, and global status markers).

### Status & Telemetry Indicators
- **Waiting:** `#64748B` (Slate Gray)
- **Extracting / Resolving:** `#A855F7` (Electric Violet)
- **Downloading / Active:** `#00C2B8` (Vivid Teal)
- **Processing / FFmpeg:** `#F59E0B` (Amber Orange)
- **Completed:** `#10B981` (Emerald Green)
- **Failed / Error:** `#EF4444` (Rose Red)
- **Paused:** `#94A3B8` (Cool Muted Slate)

## Typography

The type system relies on Microsoft’s `Segoe UI Variable` with automatic optical sizing (`Display`, `Text`, and `Small` axes based on rendered pixel size). Fallbacks degrade gracefully through `Segoe UI` to standard platform system fonts.

### Font Hierarchy & Sizing Rules
- **Display & Section Titles:** Employs `Segoe UI Variable Display` optical axes with tighter negative letter-spacing for razor-sharp heading clarity in dark environments.
- **Interface & Operational Text:** `body-md` (13px/18px) serves as the default desktop baseline, maintaining high data density while eliminating reading fatigue.
- **Technical & Metric Telemetry:** Queue speeds (MB/s), estimated completion times, hashes, and FFmpeg logs utilize a dedicated tabular monospaced configuration (`mono-metric`) to prevent layout shifting during real-time transfers.
- **Font Rendering Directives:** Antialiasing must be set to `subpixel-antialiased` with standard desktop text-rendering engines for crisp outline sharpness on non-high-DPI panels.

## Layout & Spacing

The structural layout strictly adheres to an 8pt spatial grid rhythm, supported by a 4pt micro-grid for compact controls, icon alignments, and status indicators.

### Window Geometry & Shell Structure
- **Canvas Dimensions:** Fixed native canvas reference of `1440x900px` with dynamic scaling downwards to `1024x640px` minimum bounds.
- **Native Custom Title Bar:** Height of `32px` spanning the entire width, hosting integrated drag regions, breadcrumbs, search field, and WinUI-standard minimize, maximize, and close controls (`46x32px` close button target).
- **Navigation Rail / Sidebar:** Static width of `240px` pinned to the left edge with direct mica background tint, separated by an inward 1px line divider.
- **Main Workspace Layout:** A two-tier responsive flex layout. The primary task viewport consumes the remaining horizontal real estate with `16px` (`space-base`) outer padding, hosting variable split panels (download list vs. inspector details drawer at `360px` fixed width when expanded).

### Responsive Adaptation Rules
- **Desktop Standard (> 1200px):** 240px persistent sidebar, primary content queue with multi-column metadata (Title, Status, Speed, ETA, Format, Actions), and open telemetry drawer.
- **Desktop Compact (1024px – 1200px):** Telemetry inspector drawer automatically collapses into a modal overlay flyout; item metadata wraps secondary info.
- **Sidebar Minimized (< 1024px):** Sidebar compacts to an icon-only rail (`48px` width) with flyout tooltips.

## Elevation & Depth

Visual hierarchy does not depend on heavy shadows; instead, it is articulated through physical translucent layers, Mica background illumination, and subtle optical lighting.

### Surface Elevation Levels
- **Layer 0 (Window Base / Mica Canvas):** The base app backdrop featuring dark slate glass (`#1C1C1E`) with subtle multi-layer desktop background sampling.
- **Layer 1 (Cards, Lists, Group Containers):** Solid structural tiles (`#2A2A2D` in Dark Mode / `#FFFFFF` in Light Mode) enclosed with a continuous 1px keyline border:
  - Dark: `border: 1px solid rgba(255, 255, 255, 0.07);`
  - Light: `border: 1px solid rgba(0, 0, 0, 0.08);`
  - Shadow: `0px 2px 4px rgba(0, 0, 0, 0.16), 0px 0px 1px rgba(0, 0, 0, 0.24);`
- **Layer 2 (Overlays, Context Menus, Task Flyouts):** Higher-z floating acrylic surfaces (`#323236`) featuring backdrop blur (`backdrop-filter: blur(20px) saturate(140%)`):
  - Dark: `border: 1px solid rgba(255, 255, 255, 0.12);`
  - Light: `border: 1px solid rgba(0, 0, 0, 0.12);`
  - Shadow: `0px 8px 16px rgba(0, 0, 0, 0.32), 0px 2px 4px rgba(0, 0, 0, 0.24);`
- **Layer 3 (Modal Dialogs & System Alerts):** Pinned central alerts featuring heavy acrylic blur:
  - Dark: `border: 1px solid rgba(255, 255, 255, 0.18);`
  - Shadow: `0px 16px 32px rgba(0, 0, 0, 0.48), 0px 4px 8px rgba(0, 0, 0, 0.3);`

## Shapes

The geometric signature conforms precisely to the Windows 11 WinUI 3 rounded aesthetic, prioritizing cohesive, soft curvature across all nested layers.

### Corner Radii Guidelines
- **Outer Windows & Shell Flyouts:** `8px` (`rounded-lg`) corner radii for top-level windows, contextual dropdowns, and flyout sheets.
- **Surface Cards & Container Modules:** `8px` (`rounded-lg`) corner radii for queue list rows, inspector preview panes, and content group boxes.
- **Interactive Controls (Buttons, Text Inputs, Segmented Tabs):** `4px` to `6px` corner radii, providing balanced visual enclosure for dense interactive targets.
- **Badges, Progress Bars, and Indicator Dots:** Fully rounded pill shapes (`9999px`) for download state tags and meter bars.

## Components

### Title Bar & Custom Window Controls
- **Height:** 32px. Non-client area handling dragging across blank space.
- **System Controls:** Located on the far right.
  - Minimize & Maximize: `46x32px`, subtle hover fill `rgba(255, 255, 255, 0.06)`.
  - Close: `46x32px`, hover fill `#C42B1C`, white glyph on hover.
- **Window Title / Quick Search:** Centered or left-aligned integrated command input (`26px` height, translucent background `rgba(255, 255, 255, 0.04)`).

### Buttons
- **Primary Accent Button:**
  - Background: Gradient from `#00C2B8` to `#2D7BFF` (135deg).
  - Text: `#FFFFFF`, `body-strong`.
  - Padding: `0 14px`, height `32px`, border-radius `4px`.
  - Border: Inset 1px top highlight `rgba(255, 255, 255, 0.2)`.
  - Hover: Opacity brightness filter `1.08`.
  - Active: Scale transform `scale(0.985)`.
- **Standard Surface Button:**
  - Background: `rgba(255, 255, 255, 0.06)` (Dark) / `rgba(0, 0, 0, 0.04)` (Light).
  - Border: 1px solid `rgba(255, 255, 255, 0.08)`.
  - Text: `#FFFFFF`, `body-md`.
  - Hover: Background `rgba(255, 255, 255, 0.1)`.

### Download Queue Item (Card Component)
- **Container:** Background `#2A2A2D`, border 1px solid `rgba(255, 255, 255, 0.07)`, border-radius `8px`, padding `12px 16px`.
- **Layout:** Horizontal alignment with thumbnail media preview (`48x48px`, `rounded-md`), title, format tag, continuous progress bar, speed indicator (`mono-metric`), and trailing quick-action buttons (Pause, Open Folder, Cancel).
- **Interactive State:** Hover elevates card background to `#323236` with border highlight `rgba(0, 194, 184, 0.3)`.

### Linear Progress Indicators
- **Track:** Height `4px`, background `rgba(255, 255, 255, 0.1)`, fully rounded.
- **Fill Bar:** Dynamic linear gradient (`#00C2B8` to `#2D7BFF`), smoothly animated width changes with `transition: width 200ms cubic-bezier(0, 0, 0, 1)`.
- **Indeterminate State (Extracting/Processing):** A continuous glowing teal wave moving horizontally across the bar width.

### Status Badges
- **Structure:** Height `20px`, padding `0 8px`, border-radius `9999px`, typography `badge-label`.
- **Variations (Background & Foreground):**
  - **Waiting:** BG `rgba(100, 116, 139, 0.15)`, Text `#94A3B8`, Border `rgba(100, 116, 139, 0.25)`.
  - **Extracting:** BG `rgba(168, 85, 247, 0.15)`, Text `#C084FC`, Border `rgba(168, 85, 247, 0.3)`.
  - **Downloading:** BG `rgba(0, 194, 184, 0.15)`, Text `#2DD4BF`, Border `rgba(0, 194, 184, 0.3)`.
  - **Processing / FFmpeg:** BG `rgba(245, 158, 11, 0.15)`, Text `#FBBF24`, Border `rgba(245, 158, 11, 0.3)`.
  - **Completed:** BG `rgba(16, 185, 129, 0.15)`, Text `#34D399`, Border `rgba(16, 185, 129, 0.3)`.
  - **Failed:** BG `rgba(239, 68, 68, 0.15)`, Text `#F87171`, Border `rgba(239, 68, 68, 0.3)`.
  - **Paused:** BG `rgba(148, 163, 184, 0.12)`, Text `#CBD5E1`, Border `rgba(148, 163, 184, 0.2)`.

### Inputs & URL Bar
- **Base Input:** Height `32px`, border-radius `4px`, background `rgba(255, 255, 255, 0.04)`, border 1px solid `rgba(255, 255, 255, 0.1)`. Bottom line indicator highlights in `#00C2B8` (2px height) upon active focus state.
- **Quick Download URL Field:** Prominent text field spanning top toolbar; accepts batch links with immediate parsed protocol badge (e.g., `HLS`, `DASH`, `MP4`, `YouTube`).

### Navigation Sidebar Item
- **Row:** Height `36px`, border-radius `4px`, padding `0 12px`, typography `body-md`.
- **Active State:** Left edge vertical pill indicator (`3px` width, `16px` height, `#00C2B8`), background `rgba(255, 255, 255, 0.08)`, text color `#FFFFFF`.
- **Fluent System Icons:** Configured in `20px` regular weight glyphs with a standard 8px right margin.