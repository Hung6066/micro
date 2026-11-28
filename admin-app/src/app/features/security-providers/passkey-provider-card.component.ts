import { CommonModule } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  ChangeDetectorRef,
  inject,
} from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { HisHopeBrowserPasskeyClient } from "@his-hope/frontend-foundation";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
import { firstValueFrom } from "rxjs";
import { ApiErrorMessageService } from "../../core/services/api-error-message.service";
import { PasskeyApiService } from "../../core/services/passkey-api.service";

@Component({
  selector: "app-passkey-provider-card",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeTranslatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card class="provider-card">
      <mat-card-header>
        <mat-icon mat-card-avatar>fingerprint</mat-icon>
        <mat-card-title>{{
          "admin.passkey" | hhTranslate: "Passkey"
        }}</mat-card-title>
        <mat-card-subtitle>{{
          "admin.passkeySubtitle" | hhTranslate: "WebAuthn / FIDO2"
        }}</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <p>{{ "admin.passkeyDescription" | hhTranslate }}</p>
        <p class="status" [class.ready]="ready">
          {{
            ready
              ? ("admin.passkeyRegistered"
                | hhTranslate: "Passkey is registered for this account.")
              : ("admin.passkeyNotRegistered"
                | hhTranslate: "No passkey is registered for this account.")
          }}
          @if (ready && count !== null) {
            ({{ count }}{{ minimumRequired ? " / " + minimumRequired : "" }})
          }
        </p>
        @if (ready && minimumRequired && count !== null && count < minimumRequired) {
          <p class="hint">
            {{
              "admin.passkeyBelowMinimum"
                | hhTranslate
                  : "Add another passkey (for example on your phone) so passkeys alone count as MFA, or enable MFA / TOTP."
            }}
          </p>
        }
        <p class="error" *ngIf="error">{{ error }}</p>
      </mat-card-content>
      <mat-card-actions>
        <hh-action-button
          [disabled]="busy"
          (pressed)="create()"
          kind="primary"
          icon="add"
          [label]="
            ready
              ? ('admin.addAnotherPasskey' | hhTranslate: 'Add another passkey')
              : ('admin.createPasskey' | hhTranslate: 'Create passkey')
          "
        />
      </mat-card-actions>
    </mat-card>
  `,
})
export class PasskeyProviderCardComponent {
  private readonly api = inject(PasskeyApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly errors = inject(ApiErrorMessageService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly client = new HisHopeBrowserPasskeyClient();

  ready = false;
  count: number | null = null;
  minimumRequired: number | null = null;
  busy = false;
  error = "";

  constructor() {
    this.loadStatus();
  }

  private loadStatus(): void {
    this.api.getStatus().subscribe({
      next: (status) => {
        this.ready = status.registered;
        this.count = typeof status.count === "number" ? status.count : null;
        this.minimumRequired =
          typeof status.minimumRequired === "number" ? status.minimumRequired : null;
        this.cdr.markForCheck();
      },
      error: (error) => {
        this.error = this.errors.message(
          error,
          this.i18n.t(
            "admin.unableLoadPasskeyStatus",
            "Unable to load passkey status.",
          ),
        );
        this.cdr.markForCheck();
      },
    });
  }

  async create(): Promise<void> {
    this.error = "";
    const support = await this.client.support();
    if (!support.supported) {
      this.error = this.i18n.t(
        "admin.browserNoPasskeys",
        "This browser does not support passkeys.",
      );
      return;
    }
    this.busy = true;
    try {
      const options = await firstValueFrom(this.api.getRegistrationOptions());
      const credential = await this.client.create(options);
      const response = credential.response as AuthenticatorAttestationResponse;
      await firstValueFrom(
        this.api.completeRegistration({
          id: credential.id,
          rawId: this.encode(credential.rawId),
          type: credential.type,
          response: {
            clientDataJSON: this.encode(response.clientDataJSON),
            attestationObject: this.encode(response.attestationObject),
          },
        }),
      );
      this.ready = true;
      this.loadStatus();
    } catch (error) {
      this.error = this.errors.message(
        error,
        this.i18n.t(
          "admin.passkeyRegistrationFailed",
          "Passkey registration failed.",
        ),
      );
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  private encode(value: ArrayBuffer): string {
    const bytes = new Uint8Array(value);
    let binary = "";
    bytes.forEach((byte) => (binary += String.fromCharCode(byte)));
    return btoa(binary)
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=+$/g, "");
  }
}
