import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type HisHopeMobileIconName =
  | 'home'
  | 'clients'
  | 'users'
  | 'roles'
  | 'consents'
  | 'biometric'
  | 'theme'
  | 'logout'
  | 'refresh'
  | 'more'
  | 'security'
  | 'mfa'
  | 'qr'
  | 'key'
  | 'link'
  | 'verified'
  | 'error'
  | 'offline'
  | 'forbidden'
  | 'empty'
  | 'next';

const MOBILE_ICON_GLYPHS: Record<HisHopeMobileIconName, string> = {
  home: 'dashboard',
  clients: 'vpn_key',
  users: 'people',
  roles: 'badge',
  consents: 'checklist',
  biometric: 'fingerprint',
  theme: 'contrast',
  logout: 'logout',
  refresh: 'refresh',
  more: 'more_vert',
  security: 'verified_user',
  mfa: 'shield',
  qr: 'qr_code_2',
  key: 'key',
  link: 'link',
  verified: 'verified',
  error: 'error_outline',
  offline: 'cloud_off',
  forbidden: 'lock',
  empty: 'inbox',
  next: 'arrow_forward',
};

@Component({
  selector: 'hh-mobile-icon',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="hh-mobile-icon material-symbols-rounded"
      [class]="'hh-mobile-icon hh-mobile-icon--' + size()"
      [attr.aria-hidden]="decorative() ? 'true' : null"
      [attr.role]="decorative() ? null : 'img'"
      [attr.aria-label]="decorative() ? null : label()">
      {{ glyph() }}
    </span>
  `,
  styles: [`
    :host { display: inline-grid; place-items: center; flex: 0 0 auto; width: 24px; height: 24px; line-height: 1; vertical-align: middle; }
    .hh-mobile-icon { display: grid; place-items: center; width: 100%; height: 100%; margin: 0; line-height: 1; font-family: 'Material Symbols Rounded', 'Material Icons'; font-weight: 400; font-style: normal; font-size: 20px; font-feature-settings: 'liga'; font-variation-settings: 'FILL' 0, 'wght' 500, 'GRAD' 0, 'opsz' 24; }
    .hh-mobile-icon--small { font-size: 18px; }
    .hh-mobile-icon--medium { font-size: 20px; }
    .hh-mobile-icon--large { font-size: 24px; }
  `],
})
export class HisHopeMobileIconComponent {
  readonly name = input.required<HisHopeMobileIconName>();
  readonly size = input<'small' | 'medium' | 'large'>('medium');
  readonly label = input('');
  readonly decorative = input(true);

  glyph(): string {
    return MOBILE_ICON_GLYPHS[this.name()];
  }
}
