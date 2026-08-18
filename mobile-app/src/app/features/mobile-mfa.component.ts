import { ChangeDetectorRef, Component, OnInit, inject } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeMobileIconComponent,
  HisHopeMobileOtpComponent,
  HisHopeStateComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import { catchError, finalize, firstValueFrom, of } from "rxjs";
import QRCode from "qrcode";
import {
  MobileAdminApiService,
  MobileMfaEnrollment,
} from "../core/admin-api.service";
import { NativeCapabilityService } from "../core/native-capability.service";

@Component({
  standalone: true,
  imports: [
    RouterLink,
    HisHopeMobileIconComponent,
    HisHopeMobileOtpComponent,
    HisHopeStateComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <section class="mfa-page">
      <hh-toolbar [label]="'mobile.mfaControls' | hhTranslate"
        ><a hhToolbarTitle routerLink="/admin/dashboard">{{
          "mobile.mfaSecurity" | hhTranslate
        }}</a></hh-toolbar
      >
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'mobile.loadingMfa' | hhTranslate"
        />
      } @else if (error) {
        <hh-state kind="error" [message]="error"
          ><button
            type="button"
            class="hh-button hh-button--secondary"
            (click)="loadMfaStatus()"
          >
            <hh-mobile-icon name="refresh" />{{ "common.retry" | hhTranslate }}
          </button></hh-state
        >
      } @else if (!enrollment && !mfaEnabled) {
        <section class="mfa-card" aria-labelledby="mfa-title">
          <p class="eyebrow">{{ "mobile.identitySecurity" | hhTranslate }}</p>
          <h1 id="mfa-title">{{ "mobile.authenticatorApp" | hhTranslate }}</h1>
          <p>{{ "mobile.mfaDescription" | hhTranslate }}</p>
          <button
            type="button"
            class="hh-button hh-button--primary"
            (click)="enroll()"
          >
            <hh-mobile-icon name="mfa" />{{
              "mobile.startMfa" | hhTranslate
            }}</button
          ><a routerLink="/admin/dashboard" class="mfa-cancel">{{
            "common.cancel" | hhTranslate
          }}</a>
        </section>
      } @else {
        <section class="mfa-card" aria-labelledby="mfa-title">
          <p class="eyebrow">{{ "mobile.identitySecurity" | hhTranslate }}</p>
          <h1 id="mfa-title">{{ "mobile.authenticatorApp" | hhTranslate }}</h1>
          @if (enrollment) {
            <p>{{ "mobile.scanQr" | hhTranslate }}</p>
            <div class="mfa-qr" aria-labelledby="mfa-qr-title">
              <div class="mfa-qr__heading">
                <hh-mobile-icon name="qr" /><strong id="mfa-qr-title">{{
                  "mobile.scanSetupCode" | hhTranslate
                }}</strong>
              </div>
              @if (qrDataUrl) {
                <img
                  class="mfa-qr__image"
                  [src]="qrDataUrl"
                  [alt]="'mobile.qrAlt' | hhTranslate"
                />
              } @else if (qrError) {
                <p class="mfa-qr__error" role="alert">
                  <hh-mobile-icon name="error" />{{ qrError }}
                </p>
              } @else {
                <div class="mfa-qr__loading" role="status">
                  <hh-mobile-icon name="refresh" />{{
                    "mobile.preparingQr" | hhTranslate
                  }}
                </div>
              }
            </div>
            <p class="mfa-secret">
              <strong
                ><hh-mobile-icon name="key" />{{
                  "mobile.manualKey" | hhTranslate
                }}</strong
              ><code>{{ enrollment.secretKey }}</code>
            </p>
            <details>
              <summary>
                <hh-mobile-icon name="link" />{{
                  "mobile.authenticatorUri" | hhTranslate
                }}
              </summary>
              <code class="mfa-codes">{{ enrollment.qrCodeUri }}</code>
            </details>
          } @else {
            <p>{{ "mobile.mfaAlreadyEnabled" | hhTranslate }}</p>
          }
          <div class="mfa-passkey">
            <p>
              <strong>{{ "mobile.nativeAuthenticator" | hhTranslate }}</strong>
            </p>
            <p>{{ "mobile.nativeDescription" | hhTranslate }}</p>
            <button
              type="button"
              class="hh-button hh-button--secondary"
              (click)="registerDevicePasskey()"
              [disabled]="loading"
            >
              <hh-mobile-icon name="verified" />{{
                "mobile.useDeviceMfa" | hhTranslate
              }}
            </button>
            @if (passkeyError) {
              <p class="mfa-qr__error" role="alert">
                <hh-mobile-icon name="error" />{{ passkeyError }}
              </p>
            }
            @if (passkeyStatus) {
              <p class="mfa-success" role="status">{{ passkeyStatus }}</p>
            }
          </div>
          @if (enrollment) {
            <hh-mobile-otp
              [label]="'mobile.authenticatorCode' | hhTranslate"
              (completed)="verify($event)"
            />
            @if (status) {
              <p class="mfa-success" role="status">
                <hh-mobile-icon name="verified" />{{ status }}
              </p>
            }
            <details>
              <summary>
                <hh-mobile-icon name="key" />{{
                  "mobile.recoveryCodes" | hhTranslate
                }}
              </summary>
              <p>{{ "mobile.recoveryDescription" | hhTranslate }}</p>
              <code class="mfa-codes">{{
                enrollment.recoveryCodes.join("\\n")
              }}</code>
            </details>
          } @else if (mfaEnabled) {
            <p>{{ "mobile.fallbackDescription" | hhTranslate }}</p>
            <hh-mobile-otp
              [label]="'mobile.authenticatorFallback' | hhTranslate"
              (completed)="verify($event)"
            />
            @if (status) {
              <p class="mfa-success" role="status">
                <hh-mobile-icon name="verified" />{{ status }}
              </p>
            }
          }
        </section>
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .mfa-page {
        display: grid;
        gap: 16px;
      }
      .mfa-page hh-toolbar a {
        color: inherit;
        text-decoration: none;
      }
      .mfa-card {
        display: grid;
        gap: 16px;
        padding: 20px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
      }
      .mfa-card h1 {
        margin: 0;
        font-size: 24px;
      }
      .mfa-card p {
        margin: 0;
        color: var(--text-secondary);
        line-height: 1.5;
      }
      .eyebrow {
        color: var(--color-primary) !important;
        font-size: 11px;
        font-weight: var(--font-weight-semibold);
        letter-spacing: 0.08em;
      }
      .mfa-qr {
        display: grid;
        justify-items: center;
        gap: 12px;
        padding: 16px;
        border: 1px solid var(--border-light);
        border-radius: var(--radius-card);
        background: var(--surface-subtle);
      }
      .mfa-qr__heading {
        display: flex;
        align-items: center;
        gap: 8px;
        color: var(--text-primary);
      }
      .mfa-qr__heading .material-icons {
        font-size: 20px;
        color: var(--color-primary);
      }
      .mfa-qr__image {
        width: min(220px, 70vw);
        height: auto;
        aspect-ratio: 1;
        border: 10px solid #fff;
        border-radius: 12px;
        box-shadow: 0 4px 16px
          color-mix(in srgb, var(--text-primary) 12%, transparent);
        image-rendering: pixelated;
      }
      .mfa-qr__loading,
      .mfa-qr__error {
        display: flex;
        align-items: center;
        gap: 8px;
        min-height: 220px;
        justify-content: center;
        text-align: center;
      }
      .mfa-qr__loading .material-icons {
        color: var(--color-primary);
        animation: mfa-spin 1s linear infinite;
      }
      .mfa-qr__error {
        color: var(--color-danger) !important;
      }
      .mfa-cancel {
        color: var(--text-secondary);
        text-align: center;
        text-decoration: none;
      }
      .mfa-secret {
        display: grid;
        gap: 6px;
      }
      .mfa-secret strong,
      .mfa-success {
        display: flex;
        align-items: center;
        gap: 8px;
      }
      .mfa-secret strong .material-icons,
      .mfa-success .material-icons {
        font-size: 18px;
        color: var(--color-primary);
      }
      .mfa-secret code,
      .mfa-codes {
        display: block;
        overflow: auto;
        padding: 10px;
        border-radius: var(--radius-input);
        background: var(--surface-subtle);
        color: var(--text-primary);
        white-space: pre-wrap;
        overflow-wrap: anywhere;
      }
      .mfa-success {
        color: var(--color-success) !important;
        font-weight: var(--font-weight-semibold);
      }
      details {
        border-top: 1px solid var(--border-light);
        padding-top: 12px;
      }
      summary {
        display: flex;
        align-items: center;
        gap: 8px;
        cursor: pointer;
        font-weight: var(--font-weight-semibold);
      }
      summary .material-icons {
        font-size: 18px;
        color: var(--color-primary);
      }
      @keyframes mfa-spin {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class MobileMfaComponent implements OnInit {
  private readonly api = inject(MobileAdminApiService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly native = inject(NativeCapabilityService);
  private readonly i18n = inject(HisHopeI18nService);
  enrollment: MobileMfaEnrollment | null = null;
  loading = false;
  error = "";
  status = "";
  passkeyStatus = "";
  passkeyError = "";
  mfaEnabled = false;
  qrDataUrl = "";
  qrError = "";

  async ngOnInit(): Promise<void> {
    await this.loadMfaStatus();
  }

  async loadMfaStatus(): Promise<void> {
    this.loading = true;
    this.error = "";
    try {
      const mfa = await firstValueFrom(this.api.getMfaStatus());
      this.mfaEnabled = mfa.enabled;
    } catch {
      this.error = this.i18n.t("mobile.mfaLoadFailed");
    } finally {
      this.loading = false;
      this.changeDetector.detectChanges();
    }
  }

  enroll(): void {
    this.loading = true;
    this.error = "";
    this.status = "";
    this.qrDataUrl = "";
    this.qrError = "";
    this.api
      .enrollMfa()
      .pipe(
        finalize(() => {
          this.loading = false;
          this.changeDetector.detectChanges();
        }),
        catchError(() => {
          this.error = this.i18n.t("mobile.mfaSetupFailed");
          return of(null);
        }),
      )
      .subscribe((value) => {
        this.enrollment = value;
        if (value) void this.renderQrCode(value.qrCodeUri);
        this.changeDetector.detectChanges();
      });
  }

  private async renderQrCode(uri: string): Promise<void> {
    try {
      this.qrDataUrl = await QRCode.toDataURL(uri, {
        errorCorrectionLevel: "M",
        margin: 2,
        width: 220,
        color: { dark: "#10251d", light: "#ffffff" },
      });
    } catch {
      this.qrError = this.i18n.t("mobile.qrFailed");
    } finally {
      this.changeDetector.detectChanges();
    }
  }

  verify(code: string): void {
    if (code.length !== 6 || !this.enrollment) return;
    this.loading = true;
    this.error = "";
    this.api
      .verifyMfa(code)
      .pipe(
        finalize(() => {
          this.loading = false;
          this.changeDetector.detectChanges();
        }),
        catchError(() => {
          this.error = this.i18n.t("mobile.codeInvalid");
          return of(null);
        }),
      )
      .subscribe((value) => {
        if (value?.status === "ok")
          this.status = this.i18n.t("mobile.mfaEnabled");
        this.changeDetector.detectChanges();
      });
  }

  async registerDevicePasskey(): Promise<void> {
    this.loading = true;
    this.error = "";
    this.passkeyStatus = "";
    this.passkeyError = "";
    try {
      if (!(await this.native.nativePasskeySupported())) {
        throw new Error(this.i18n.t("mobile.nativeUnsupported"));
      }
      const options = await firstValueFrom(this.api.registerPasskeyOptions());
      const response = await this.native.registerNativePasskey(options);
      const result = await firstValueFrom(
        this.api.completePasskeyRegistration(response),
      );
      if (!result.registered)
        throw new Error(this.i18n.t("mobile.deviceRegistrationFailed"));
      this.passkeyStatus = this.i18n.t("mobile.deviceRegistered");
    } catch (value) {
      this.passkeyError = this.errorMessage(
        value,
        this.i18n.t("mobile.deviceRegistrationFailed"),
      );
    } finally {
      this.loading = false;
      this.changeDetector.detectChanges();
    }
  }

  private errorMessage(value: unknown, fallback: string): string {
    if (value instanceof Error && value.message) return value.message;
    if (typeof value === "object" && value !== null && "message" in value) {
      const message = (value as { message?: unknown }).message;
      if (typeof message === "string" && message.trim()) return message;
    }
    return fallback;
  }
}
