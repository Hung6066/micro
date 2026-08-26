import { ChangeDetectionStrategy, Component, input, output } from "@angular/core";

/** Shared application frame. Hosts provide navigation and toolbar actions; routing and state stay in the app. */
@Component({
  selector: "hh-app-shell",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="hh-shell__toolbar">
      <button class="hh-shell__icon-button" type="button" [attr.aria-label]="toggleLabel()" (click)="navigationToggle.emit()">
        <span class="material-icons" aria-hidden="true">menu</span>
      </button>
      <span class="hh-shell__brand-mark" aria-hidden="true">+</span>
      <span class="hh-shell__title">{{ title() }}</span>
      <div class="hh-shell__toolbar-actions"><ng-content select="[hhShellToolbar]" /></div>
    </header>
    @if (mobile() && open()) { <button class="hh-shell__backdrop" type="button" [attr.aria-label]="closeLabel()" (click)="navigationToggle.emit()"></button> }
    <div class="hh-shell__body">
      <aside class="hh-shell__sidebar" [class.hh-shell__sidebar--open]="open()" [class.hh-shell__sidebar--mobile]="mobile()" [attr.aria-hidden]="mobile() && !open()">
        <ng-content select="[hhShellNavigation]" />
      </aside>
      <main class="hh-shell__content"><ng-content select="[hhShellContent]" /></main>
    </div>
  `,
  styles: [`
    :host { display:flex; flex-direction:column; height:100dvh; overflow:hidden; color:var(--text-primary); font-family:var(--font-sans); }
    .hh-shell__toolbar { position:relative; z-index:20; display:flex; flex:0 0 var(--shell-header-height); align-items:center; min-height:var(--shell-header-height); gap:var(--space-sm); padding:0 var(--space-md); border-bottom:1px solid color-mix(in srgb, var(--color-on-primary) 18%, transparent); background:var(--shell-header-bg); color:var(--color-on-primary); }
    .hh-shell__icon-button { display:grid; place-items:center; width:var(--touch-target); height:var(--touch-target); border:1px solid transparent; border-radius:var(--radius-button); background:transparent; color:inherit; cursor:pointer; }
    .hh-shell__icon-button:hover { border-color:color-mix(in srgb, var(--color-on-primary) 32%, transparent); background:color-mix(in srgb, var(--color-on-primary) 16%, transparent); }
    .hh-shell__icon-button:focus-visible { border-color:var(--color-on-primary); outline:var(--focus-ring-width) solid var(--color-focus); outline-offset:var(--focus-ring-offset); }
    .hh-shell__brand-mark { display:grid; place-items:center; width:var(--control-height-sm); height:var(--control-height-sm); border-radius:var(--radius-control); background:var(--color-primary); color:var(--color-on-primary); font-size:var(--font-size-title); font-weight:var(--font-weight-bold); }
    .hh-shell__title { min-width:0; overflow:hidden; font-size:var(--font-size-label); font-weight:var(--font-weight-semibold); text-overflow:ellipsis; white-space:nowrap; }
    .hh-shell__toolbar-actions { display:flex; align-items:center; justify-content:flex-end; gap:var(--space-xs); margin-left:auto; min-width:0; }
    :host ::ng-deep .hh-shell-toolbar-slot { display:flex; align-items:center; justify-content:flex-end; gap:var(--space-xs); min-width:0; }
    .hh-shell__body { display:flex; min-height:0; flex:1; }
    .hh-shell__sidebar { width:var(--shell-sidebar-width, 280px); flex:0 0 var(--shell-sidebar-width, 280px); overflow:auto; border-right:1px solid var(--border-subtle); background:var(--surface-white); transition:transform var(--motion-base) var(--ease-standard); }
    :host ::ng-deep .nav-section-label { padding:var(--space-lg) var(--space-lg) var(--space-sm); color:var(--text-muted); font-size:var(--font-size-overline, .68rem); font-weight:var(--font-weight-bold); letter-spacing:var(--tracking-overline); line-height:1.2; text-transform:uppercase; }
    :host ::ng-deep .nav-item { display:flex; align-items:center; gap:var(--space-sm); min-height:var(--touch-target); padding:0 var(--space-md); border-inline-start:var(--focus-ring-width-strong) solid transparent; color:var(--text-primary); font-weight:var(--font-weight-medium, 500); text-decoration:none; transition:background-color var(--motion-fast) var(--ease-standard), color var(--motion-fast) var(--ease-standard), border-color var(--motion-fast) var(--ease-standard); }
    :host ::ng-deep .nav-item:hover { background:var(--surface-hover); }
    :host ::ng-deep .nav-item:focus-visible { outline:var(--focus-ring-width) solid var(--color-focus); outline-offset:calc(var(--focus-ring-offset) * -1); }
    :host ::ng-deep .nav-item.active-link { border-inline-start-color:var(--color-primary); background:var(--color-primary-soft); color:var(--color-primary-deep); font-weight:var(--font-weight-bold); }
    :host ::ng-deep .nav-item__icon { color:var(--text-secondary); font-size:var(--font-size-icon-md); transition:color var(--motion-fast) var(--ease-standard); }
    :host ::ng-deep .nav-item.active-link .nav-item__icon { color:inherit; }
    .hh-shell__content { min-width:0; min-height:0; flex:1; overflow:auto; background:var(--surface); }
    .hh-shell__backdrop { display:none; }
    @media (max-width: 768px) {
      .hh-shell__sidebar { position:fixed; inset:var(--shell-header-height) auto 0 0; z-index:19; transform:translateX(-105%); box-shadow:var(--shadow-drawer); }
      .hh-shell__sidebar--open { transform:translateX(0); }
      .hh-shell__backdrop { position:fixed; inset:var(--shell-header-height) 0 0; z-index:18; display:block; border:0; background:var(--overlay-backdrop-soft); }
    }
    @media (max-width:600px) { .hh-shell__title { display:none; } .hh-shell__toolbar { padding-inline:var(--space-xs); } }
  `],
})
export class HisHopeAppShellComponent {
  readonly title = input("");
  readonly open = input(true);
  readonly mobile = input(false);
  readonly toggleLabel = input("Toggle navigation");
  readonly closeLabel = input("Close navigation");
  readonly navigationToggle = output<void>();
}
