import { ChangeDetectionStrategy, Component, input } from "@angular/core";
import { RouterLink, RouterLinkActive } from "@angular/router";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeMobileIconComponent,
  HisHopeMobileIconName,
} from "./his-hope-mobile-icon.component";

export interface HisHopeMobileNavItem {
  route: string;
  icon: HisHopeMobileIconName;
  labelKey: string;
  labelFallback?: string;
  /** Read permission required to see the entry. Filtering is the caller's job. */
  permission?: string;
}

/**
 * Bottom navigation bar driven by a nav item list. Permission filtering stays
 * with the hosting app so this component has no auth dependency.
 */
@Component({
  selector: "hh-mobile-config-nav",
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    HisHopeMobileIconComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav
      class="hh-mobile-config-nav"
      [attr.aria-label]="label() | hhTranslate: labelFallback()"
      [style.grid-template-columns]="'repeat(' + items().length + ', 1fr)'"
    >
      @for (item of items(); track item.route) {
        <a
          [routerLink]="item.route"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          ><hh-mobile-icon [name]="item.icon" size="small" /><span>{{
            item.labelKey | hhTranslate: (item.labelFallback ?? "")
          }}</span></a
        >
      }
    </nav>
  `,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-mobile-config-nav {
        position: fixed;
        left: 50%;
        bottom: max(10px, env(safe-area-inset-bottom));
        z-index: 10;
        display: grid;
        width: min(calc(100% - var(--space-2xl)), 720px);
        transform: translateX(-50%);
        padding: var(--space-compact);
        border: 1px solid
          color-mix(in srgb, var(--border-default) 82%, var(--surface-white));
        border-radius: var(--radius-glass-nav);
        background: color-mix(in srgb, var(--surface-white) 94%, transparent);
        box-shadow: var(--shadow-glass-bar);
        backdrop-filter: blur(var(--blur-glass));
      }
      .hh-mobile-config-nav a {
        position: relative;
        display: grid;
        justify-items: center;
        align-content: center;
        grid-template-rows: var(--size-nav-icon-track) auto;
        gap: var(--space-xxs);
        min-height: var(--mobile-toolbar-height);
        padding: var(--space-2xs) var(--space-hairline) var(--space-snug);
        border-radius: var(--radius-panel);
        color: var(--text-secondary);
        font-size: var(--font-size-nav);
        line-height: 1.2;
        text-decoration: none;
        transition:
          color 0.16s ease,
          transform 0.16s ease,
          background 0.16s ease,
          box-shadow 0.16s ease;
      }
      .hh-mobile-config-nav a:active {
        transform: scale(0.96);
      }
      .hh-mobile-config-nav a.active {
        color: var(--color-primary);
        font-weight: var(--font-weight-semibold);
        background: color-mix(
          in srgb,
          var(--color-primary-soft) 84%,
          var(--surface-white)
        );
        box-shadow: var(--shadow-glass-bar-active);
      }
      .hh-mobile-config-nav a.active::before {
        content: "";
        position: absolute;
        top: 0;
        left: 50%;
        z-index: 1;
        width: var(--size-status-dot);
        height: var(--size-status-dot);
        border-radius: var(--radius-full) var(--radius-full) var(--radius-full) 0;
        background: var(--color-primary);
        transform: translateX(-50%) rotate(-45deg);
        transform-origin: center;
        animation: hh-mobile-config-nav-droplet 1.8s ease-in-out infinite;
      }
      .hh-mobile-config-nav a.active::after {
        content: "";
        position: absolute;
        right: 50%;
        top: var(--space-hairline);
        width: var(--size-config-nav-indicator);
        height: var(--focus-ring-width-strong);
        border-radius: var(--radius-pill);
        background: var(--color-primary);
        transform: translateX(50%);
      }
      .hh-mobile-config-nav a hh-mobile-icon {
        width: var(--control-height-compact);
        height: var(--page-padding-block);
        border-radius: var(--radius-chip);
        color: currentColor;
        transition:
          background 0.16s ease,
          color 0.16s ease;
      }
      .hh-mobile-config-nav a.active hh-mobile-icon {
        background: var(--color-primary);
        color: var(--surface-white);
        box-shadow: var(--shadow-icon-active);
      }
      .hh-mobile-config-nav a:focus-visible {
        outline: var(--focus-ring-width-strong) solid
          color-mix(in srgb, var(--color-primary) 35%, transparent);
        outline-offset: var(--focus-ring-width);
      }
      @keyframes hh-mobile-config-nav-droplet {
        0%,
        100% {
          margin-left: -9px;
          opacity: 0.7;
        }
        50% {
          margin-left: 9px;
          opacity: 1;
        }
      }
      @media (prefers-reduced-motion: reduce) {
        .hh-mobile-config-nav a.active::before {
          animation: none;
        }
        .hh-mobile-config-nav a {
          transition: none;
        }
      }
      @media (max-width: 380px) {
        .hh-mobile-config-nav {
          width: calc(100% - var(--space-lg));
          padding: var(--space-xs);
          border-radius: var(--radius-sheet);
        }
        .hh-mobile-config-nav a {
          font-size: var(--font-size-overline);
        }
      }
    `,
  ],
})
export class HisHopeMobileConfigNavComponent {
  readonly items = input.required<readonly HisHopeMobileNavItem[]>();
  readonly label = input("mobile.adminNavigation");
  readonly labelFallback = input("Admin navigation");
}
