---
name: Admission Productivity System
colors:
  surface: '#f8f9fb'
  surface-dim: '#d9dadc'
  surface-bright: '#f8f9fb'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f3f4f6'
  surface-container: '#edeef0'
  surface-container-high: '#e7e8ea'
  surface-container-highest: '#e1e2e4'
  on-surface: '#191c1e'
  on-surface-variant: '#434654'
  inverse-surface: '#2e3132'
  inverse-on-surface: '#f0f1f3'
  outline: '#737685'
  outline-variant: '#c3c6d6'
  surface-tint: '#0c56d0'
  primary: '#003d9b'
  on-primary: '#ffffff'
  primary-container: '#0052cc'
  on-primary-container: '#c4d2ff'
  inverse-primary: '#b2c5ff'
  secondary: '#285ab9'
  on-secondary: '#ffffff'
  secondary-container: '#709bfe'
  on-secondary-container: '#003179'
  tertiary: '#384454'
  on-tertiary: '#ffffff'
  tertiary-container: '#4f5c6c'
  on-tertiary-container: '#c7d4e8'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dae2ff'
  primary-fixed-dim: '#b2c5ff'
  on-primary-fixed: '#001848'
  on-primary-fixed-variant: '#0040a2'
  secondary-fixed: '#d9e2ff'
  secondary-fixed-dim: '#b1c6ff'
  on-secondary-fixed: '#001946'
  on-secondary-fixed-variant: '#00419d'
  tertiary-fixed: '#d6e3f7'
  tertiary-fixed-dim: '#bbc7db'
  on-tertiary-fixed: '#101c2b'
  on-tertiary-fixed-variant: '#3b4858'
  background: '#f8f9fb'
  on-background: '#191c1e'
  surface-variant: '#e1e2e4'
  surface-card: '#FFFFFF'
  text-main: '#172B4D'
  text-muted: '#6B778C'
  border-subtle: '#DFE1E6'
  status-success: '#36B37E'
  status-warning: '#FFAB00'
  status-danger: '#FF5630'
  sidebar-bg: '#0747A6'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  title-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-md:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '600'
    lineHeight: 18px
    letterSpacing: 0.01em
  label-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
  code-sm:
    fontFamily: JetBrains Mono
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  sidebar-width: 260px
  header-height: 64px
  container-max-width: 1440px
  gutter: 1.5rem
  card-padding: 1.25rem
  table-cell-padding: 0.75rem 1rem
  stack-gap-sm: 0.5rem
  stack-gap-md: 1rem
---

## Brand & Style

The brand personality is **utilitarian, precise, and authoritative**. As a tool for admission counselors, the interface must prioritize high-density information and cognitive clarity over decorative elements. The design style follows a **Corporate / Modern** approach with a heavy emphasis on **Minimalism** to manage the complexity of CRM data.

The emotional response should be one of "controlled efficiency." By utilizing a monochromatic blue palette, the system establishes a sense of trust and institutional reliability. High whitespace and a flat UI style ensure that the counselor's attention is directed toward actionable lead data, follow-up reminders, and conversion metrics without the distraction of unnecessary shadows or gradients.

## Colors

The palette is strictly monochromatic blue to maintain a professional, SaaS-focused aesthetic. 

- **Primary Blue:** Used for critical actions, active states, and primary buttons.
- **Deep Navy (Secondary):** Reserved for the fixed left sidebar and high-level navigation to provide strong visual anchoring.
- **Soft Blue (Tertiary):** Used for subtle backgrounds, hover states in lists, and tag backgrounds.
- **Functional Neutrals:** A range of cool grays provides structure. Text is rendered in a deep charcoal (rather than pure black) to reduce eye strain during long working hours.

Success, warning, and danger colors are used sparingly for status badges (e.g., "Enrolled", "Dropped") to ensure they stand out against the blue-themed environment.

## Typography

The system uses **Inter** for all UI elements to ensure maximum legibility at small sizes, which is critical for compact CRM tables and data-heavy dashboards.

- **Headlines:** Use tighter letter spacing and semi-bold weights to create a strong hierarchy.
- **Body Text:** Standardized at 14px for general application use to balance density and readability.
- **Labels:** Small, all-caps or medium-weight labels are used for table headers and form field captions to differentiate them from user-inputted data.
- **Monospace:** **JetBrains Mono** is used for ID strings, phone numbers, or technical metadata to ensure character distinction.

## Layout & Spacing

The design system utilizes a **fixed-fluid hybrid layout**. 

- **Fixed Sidebar:** A 260px left sidebar remains anchored for navigation.
- **Top Header:** A 64px tall header handles global search and quick actions.
- **Content Area:** A fluid 12-column grid system with a maximum width of 1440px. On larger screens, the content centers with wide margins; on smaller screens, it fills the viewport.
- **Data Density:** Spacing is "Compact." Gutters are set to 24px (1.5rem), but internal card spacing and table cell padding are reduced to ensure more information is visible above the fold. 

**Breakpoints:**
- Mobile (<768px): Sidebar collapses to a hamburger menu. Margins reduce to 16px.
- Tablet (768px - 1024px): 2-column card layouts reflow to single column.
- Desktop (>1024px): Full 12-column grid active.

## Elevation & Depth

This system adopts a **Flat UI** philosophy with **Tonal Layering** rather than traditional shadows. Depth is communicated through color contrast:

1.  **Background Layer:** The lowest layer is the neutral `#F4F5F7`, providing a soft canvas.
2.  **Surface Layer (Cards):** All primary content sits on pure white (`#FFFFFF`) cards.
3.  **Low-Contrast Outlines:** Instead of shadows, cards and inputs use a 1px solid border in `#DFE1E6`. This creates a crisp, architectural feel.
4.  **Interactive Depth:** A very subtle, diffused shadow (4px blur, 2% opacity) may be applied only to floating elements like dropdown menus or modals to distinguish them from the base layout.

## Shapes

The shape language is **Soft and Professional**. 

- **Base Radius:** 4px (0.25rem) for most components including input fields, buttons, and small cards. This maintains a "crisp" look that feels modern but not overly clinical.
- **Large Components:** Dashboard containers and lead profile cards use 8px (0.5rem) to slightly soften the overall layout.
- **Status Pills:** Badges and tags utilize a fully rounded (pill) shape to clearly distinguish them from interactive buttons or text inputs.

## Components

- **Buttons:** 
    - *Primary:* Solid `#0052CC` with white text. No gradients.
    - *Secondary:* Ghost style with `#0052CC` border and text.
    - *Tertiary:* Transparent background with `#6B778C` text for low-priority actions.
- **Compact Tables:** 
    - Row height capped at 48px. 
    - Zebra striping is avoided in favor of 1px bottom borders. 
    - Headers use `label-md` typography with a subtle gray background.
- **Input Fields:** 
    - 36px height for standard inputs. 
    - Active state uses a 2px blue border focus ring. 
    - Labels are positioned above the field, never inside as placeholders.
- **Cards:** 
    - Pure white background. 
    - Defined by a 1px `#DFE1E6` border. 
    - Title sections are separated by a subtle horizontal divider.
- **Chips/Badges:** 
    - Small (12px text). 
    - Use "Tonal" styling: a light version of the status color (e.g., light green background for success) with dark text for high legibility.
- **Sidebar Nav:** 
    - Active items use a high-contrast white text and a light-blue left-edge indicator. 
    - Inactive items use a desaturated blue-gray to maintain hierarchy.