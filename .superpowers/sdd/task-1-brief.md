### Task 1: Dark Mode + Mobile Responsive

**Files:**
- Modify `src/app/app.component.ts` — add theme toggle + media query
- Modify `src/app/app.component.html` — dark mode toggle button in toolbar
- Modify `src/app/app.component.scss` — responsive sidebar (collapse on mobile)
- Modify `src/styles/_theme.scss` — dark theme variant
- Modify `src/index.html` — class-based theme switching

**Implementation:**
- CSS custom properties for colors (light + dark map)
- `mat-sidenav` mode changes to `over` on mobile (<768px)
- Toggle button in toolbar: `brightness_6` icon, persists to localStorage
- Dark theme: dark backgrounds (#1a1a2e), muted text, reduced brightness on cards

---

