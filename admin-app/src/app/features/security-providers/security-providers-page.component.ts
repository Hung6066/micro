import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { HttpClient } from "@angular/common/http";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSelectModule } from "@angular/material/select";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { HisHopeI18nService } from "@his-hope/frontend-foundation";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import QRCode from "qrcode";
import { catchError, firstValueFrom, of, tap } from "rxjs";
import { IdentitySetting } from "../../core/contracts/admin.contracts";
import { SecuritySettingsApiService } from "../../core/services/security-settings-api.service";
import { ApiErrorMessageService } from "../../core/services/api-error-message.service";
import { environment } from "../../../environments/environment";
import { PasskeyProviderCardComponent } from "./passkey-provider-card.component";

interface ProviderSettings {
  passkeys: { rpId: string; origins: string };
  ldap: {
    enabled: boolean;
    server: string;
    port: number;
    useSsl: boolean;
    searchBase: string;
    searchFilter: string;
    groupRoleMapping: string;
  };
  saml: {
    enabled: boolean;
    issuer: string;
    singleSignOnDestination: string;
    idpMetadata: string;
    emailClaim: string;
    groupClaim: string;
    groupRoleMapping: string;
  };
}

@Component({
  selector: "app-security-providers-page",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    PasskeyProviderCardComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.securityProviders' | hhTranslate: 'Security providers'"
        [subtitle]="
          'admin.securityProvidersSubtitle'
            | hhTranslate
              : 'Create a passkey and configure LDAP/AD or SAML for the His.Hope identity service.'
        "
      >
        <button
          *ngIf="canWrite"
          mat-raised-button
          color="primary"
          (click)="save()"
          [disabled]="loading || saving"
        >
          <mat-icon>save</mat-icon
          >{{ "admin.saveSettings" | hhTranslate: "Save settings" }}
        </button>
      </hh-page-header>
      <mat-form-field appearance="outline" *ngIf="facilityIds.length > 1"
        ><mat-label>{{ "admin.facility" | hhTranslate: "Facility" }}</mat-label
        ><mat-select [(ngModel)]="selectedFacilityId" (selectionChange)="load()"
          ><mat-option
            *ngFor="let facilityId of facilityIds"
            [value]="facilityId"
            >{{ facilityId }}</mat-option
          ></mat-select
        ></mat-form-field
      >
      <div class="notice">
        <mat-icon>info</mat-icon
        ><span>{{
          "admin.securityProvidersNotice"
            | hhTranslate
              : "Bind passwords, signing certificates and private keys stay in Vault/environment secrets. These settings may require an identity-service restart."
        }}</span>
      </div>
      <section class="provider-grid">
        <app-passkey-provider-card />
        <mat-card class="provider-card mfa-card">
          <mat-card-header
            ><mat-icon mat-card-avatar>verified_user</mat-icon
            ><mat-card-title>{{
              "admin.mfaTotp" | hhTranslate: "MFA / TOTP"
            }}</mat-card-title
            ><mat-card-subtitle>{{
              "admin.authenticatorApp" | hhTranslate: "Authenticator app"
            }}</mat-card-subtitle></mat-card-header
          >
          <mat-card-content>
            <p>
              {{
                "admin.mfaDescription"
                  | hhTranslate
                    : "Protect the current admin account with a six-digit authenticator code."
              }}
            </p>
            <p class="status" [class.ready]="mfa.enabled">
              {{
                mfa.enabled
                  ? ("admin.mfaEnabled" | hhTranslate: "MFA is enabled.")
                  : ("admin.mfaNotEnabled" | hhTranslate: "MFA is not enabled.")
              }}
            </p>
            <p class="error" *ngIf="mfaError">{{ mfaError }}</p>
            <div class="mfa-setup" *ngIf="mfa.qrCodeUri">
              <strong>{{
                "admin.mfaSetupStep1"
                  | hhTranslate: "Step 1: Add to your authenticator app"
              }}</strong>
              <div class="qr-frame" *ngIf="mfa.qrCodeDataUrl">
                <img
                  [src]="mfa.qrCodeDataUrl"
                  [alt]="
                    'admin.mfaSetupStep1' | hhTranslate: 'MFA setup QR code'
                  "
                />
              </div>
              <label>{{ "admin.secretKey" | hhTranslate: "Secret key" }}</label
              ><code>{{ mfa.secretKey }}</code>
              <label>{{ "admin.otpUri" | hhTranslate: "OTP URI" }}</label
              ><textarea readonly rows="3" [value]="mfa.qrCodeUri"></textarea>
              <strong>{{
                "admin.mfaSetupStep2"
                  | hhTranslate: "Step 2: Verify the six-digit code"
              }}</strong>
              <mat-form-field appearance="outline"
                ><mat-label>{{
                  "admin.authenticatorCode" | hhTranslate: "Authenticator code"
                }}</mat-label
                ><input
                  matInput
                  inputmode="numeric"
                  maxlength="6"
                  autocomplete="one-time-code"
                  [(ngModel)]="mfa.verificationCode"
              /></mat-form-field>
              <button
                mat-flat-button
                color="primary"
                (click)="verifyMfa()"
                [disabled]="mfaBusy || mfa.verificationCode.length !== 6"
              >
                {{
                  "admin.verifyEnableMfa" | hhTranslate: "Verify and enable MFA"
                }}
              </button>
              <div class="recovery" *ngIf="mfa.recoveryCodes.length">
                <strong>{{
                  "admin.saveRecoveryCodes"
                    | hhTranslate: "Save recovery codes now"
                }}</strong
                ><code>{{ mfa.recoveryCodes.join(" · ") }}</code>
              </div>
            </div>
          </mat-card-content>
          <mat-card-actions
            ><button
              mat-stroked-button
              (click)="enrollMfa()"
              [disabled]="mfaBusy || mfa.enabled"
            >
              <mat-spinner *ngIf="mfaBusy" diameter="18"></mat-spinner
              ><mat-icon *ngIf="!mfaBusy">security</mat-icon
              >{{
                mfa.qrCodeUri
                  ? ("admin.regenerateSetup" | hhTranslate: "Regenerate setup")
                  : ("admin.setupMfa" | hhTranslate: "Set up MFA")
              }}
            </button></mat-card-actions
          >
        </mat-card>
        <mat-card class="provider-card">
          <mat-card-header
            ><mat-icon mat-card-avatar>account_tree</mat-icon
            ><mat-card-title>{{
              "admin.ldapAd" | hhTranslate: "LDAP / AD"
            }}</mat-card-title
            ><mat-card-subtitle>{{
              "admin.directoryAuthentication"
                | hhTranslate: "Directory authentication"
            }}</mat-card-subtitle></mat-card-header
          >
          <mat-card-content class="form-grid">
            <mat-checkbox [(ngModel)]="settings.ldap.enabled">{{
              "admin.enableLdapLogin" | hhTranslate: "Enable LDAP/AD login"
            }}</mat-checkbox>
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.server" | hhTranslate: "Server"
              }}</mat-label
              ><input
                matInput
                [(ngModel)]="settings.ldap.server"
                placeholder="dc01.hospital.local"
            /></mat-form-field>
            <mat-form-field appearance="outline"
              ><mat-label>{{ "admin.port" | hhTranslate: "Port" }}</mat-label
              ><input matInput type="number" [(ngModel)]="settings.ldap.port"
            /></mat-form-field>
            <mat-checkbox [(ngModel)]="settings.ldap.useSsl">{{
              "admin.useLdaps" | hhTranslate: "Use LDAPS/TLS"
            }}</mat-checkbox>
            <mat-form-field appearance="outline" class="wide"
              ><mat-label>{{
                "admin.searchBase" | hhTranslate: "Search base"
              }}</mat-label
              ><input
                matInput
                [(ngModel)]="settings.ldap.searchBase"
                [placeholder]="
                  'admin.searchBasePlaceholder'
                    | hhTranslate: 'DC=hospital,DC=local'
                "
            /></mat-form-field>
            <mat-form-field appearance="outline" class="wide"
              ><mat-label>{{
                "admin.searchFilter" | hhTranslate: "Search filter"
              }}</mat-label
              ><input matInput [(ngModel)]="settings.ldap.searchFilter"
            /></mat-form-field>
            <mat-form-field appearance="outline" class="wide"
              ><mat-label>{{
                "admin.groupRoleMapping"
                  | hhTranslate: "Group → role mapping (JSON)"
              }}</mat-label
              ><textarea
                matInput
                rows="3"
                [(ngModel)]="settings.ldap.groupRoleMapping"
                placeholder='{"CN=Doctors":"Provider"}'
              ></textarea>
            </mat-form-field>
          </mat-card-content>
        </mat-card>
        <mat-card class="provider-card">
          <mat-card-header
            ><mat-icon mat-card-avatar>business</mat-icon
            ><mat-card-title>{{
              "admin.samlSso" | hhTranslate: "SAML SSO"
            }}</mat-card-title
            ><mat-card-subtitle>{{
              "admin.enterpriseIdentityProvider"
                | hhTranslate: "Enterprise identity provider"
            }}</mat-card-subtitle></mat-card-header
          >
          <mat-card-content class="form-grid">
            <mat-checkbox [(ngModel)]="settings.saml.enabled">{{
              "admin.enableSamlLogin" | hhTranslate: "Enable SAML login"
            }}</mat-checkbox>
            <mat-form-field appearance="outline" class="wide"
              ><mat-label>{{
                "admin.serviceProviderIssuer"
                  | hhTranslate: "Service provider issuer"
              }}</mat-label
              ><input matInput [(ngModel)]="settings.saml.issuer"
            /></mat-form-field>
            <mat-form-field appearance="outline" class="wide"
              ><mat-label>{{
                "admin.idpSsoDestination" | hhTranslate: "IdP SSO destination"
              }}</mat-label
              ><input
                matInput
                [(ngModel)]="settings.saml.singleSignOnDestination"
                placeholder="https://idp.example.com/sso"
            /></mat-form-field>
            <mat-form-field appearance="outline" class="wide"
              ><mat-label>{{
                "admin.idpMetadata" | hhTranslate: "IdP metadata"
              }}</mat-label
              ><textarea
                matInput
                rows="5"
                [(ngModel)]="settings.saml.idpMetadata"
                [placeholder]="
                  'admin.idpMetadataPlaceholder'
                    | hhTranslate: 'Paste metadata URL'
                "
              ></textarea>
            </mat-form-field>
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.emailClaim" | hhTranslate: "Email claim"
              }}</mat-label
              ><input
                matInput
                [(ngModel)]="settings.saml.emailClaim"
                placeholder="email"
            /></mat-form-field>
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.groupClaim" | hhTranslate: "Group claim"
              }}</mat-label
              ><input
                matInput
                [(ngModel)]="settings.saml.groupClaim"
                placeholder="groups"
            /></mat-form-field>
            <mat-form-field appearance="outline" class="wide"
              ><mat-label>{{
                "admin.groupRoleMapping"
                  | hhTranslate: "Group → role mapping (JSON)"
              }}</mat-label
              ><textarea
                matInput
                rows="3"
                [(ngModel)]="settings.saml.groupRoleMapping"
                placeholder='{"HisHope-Admins":"Admin"}'
              ></textarea>
            </mat-form-field>
          </mat-card-content>
        </mat-card>
      </section>
      <div class="loading" *ngIf="loading">
        <mat-spinner diameter="32"></mat-spinner>
      </div>
    </hh-page-layout>
  `,
  styles: [
    `
      .notice {
        display: flex;
        gap: 10px;
        align-items: flex-start;
        padding: 14px 16px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-muted);
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .notice mat-icon {
        color: var(--color-primary);
      }
      .provider-grid {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: var(--space-5);
      }
      .provider-card {
        min-height: 330px;
        border-color: var(--border-default);
        background: var(--surface-white);
        color: var(--text-primary);
      }
      .provider-card mat-card-avatar {
        display: grid;
        place-items: center;
        background: var(--color-primary-soft);
        color: var(--color-primary);
      }
      .form-grid {
        display: grid;
        gap: var(--space-2);
      }
      .wide {
        grid-column: 1/-1;
      }
      .status {
        padding: 10px 12px;
        border-radius: var(--radius-input);
        background: var(--surface-muted);
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .status.ready {
        background: var(--surface-success);
        color: var(--color-success);
      }
      .error {
        color: var(--color-danger);
        font-size: var(--font-size-caption);
      }
      .mfa-setup {
        display: grid;
        gap: var(--space-2);
        margin-top: var(--space-3);
      }
      .mfa-setup label {
        font-size: var(--font-size-caption);
        color: var(--text-muted);
      }
      .mfa-setup code,
      .recovery code {
        display: block;
        overflow-wrap: anywhere;
        padding: 9px;
        border-radius: var(--radius-input);
        background: var(--surface-muted);
        color: var(--text-primary);
        font-family: var(--font-mono);
        font-size: var(--font-size-caption);
      }
      .mfa-setup textarea {
        width: 100%;
        resize: vertical;
        font: var(--font-size-caption) var(--font-mono);
        padding: 8px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-input);
        background: var(--surface-white);
        color: var(--text-primary);
      }
      .qr-frame {
        display: grid;
        place-items: center;
        width: fit-content;
        padding: 12px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
      }
      .qr-frame img {
        display: block;
        width: 180px;
        height: 180px;
      }
      .recovery {
        display: grid;
        gap: 6px;
        padding: 10px;
        border: 1px solid var(--color-warning);
        background: var(--surface-warning);
        border-radius: var(--radius-input);
      }
      .loading {
        display: grid;
        place-items: center;
        padding: 40px;
      }
      @media (max-width: 980px) {
        .provider-grid {
          grid-template-columns: 1fr 1fr;
        }
      }
      @media (max-width: 680px) {
        .provider-grid {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class SecurityProvidersPageComponent {
  private readonly i18n = inject(HisHopeI18nService);
  private readonly api = inject(SecuritySettingsApiService);
  private readonly permissions = inject(HisHopePermissionService);
  get canWrite(): boolean {
    return this.permissions.has("admin.settings.write");
  }
  private readonly http = inject(HttpClient);
  private readonly snackBar = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly errorMessages = inject(ApiErrorMessageService);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<IdentitySetting[]>(
    this.destroyRef,
  );
  private readonly settingsByKey = new Map<string, IdentitySetting>();
  facilityIds = this.permissions.snapshot()?.facilityIds ?? [];
  selectedFacilityId?: string =
    this.facilityIds.length > 1 ? this.facilityIds[0] : undefined;

  get loading(): boolean {
    return this.resource.loading();
  }
  saving = false;
  mfaBusy = false;
  mfaError = "";
  mfa: {
    enabled: boolean;
    enrolledAt?: string;
    recoveryCodesRemaining: number;
    secretKey: string;
    qrCodeUri: string;
    qrCodeDataUrl: string;
    recoveryCodes: string[];
    verificationCode: string;
  } = {
    enabled: false,
    recoveryCodesRemaining: 0,
    secretKey: "",
    qrCodeUri: "",
    qrCodeDataUrl: "",
    recoveryCodes: [],
    verificationCode: "",
  };
  settings: ProviderSettings = {
    passkeys: { rpId: "localhost", origins: "" },
    ldap: {
      enabled: false,
      server: "",
      port: 636,
      useSsl: true,
      searchBase: "",
      searchFilter: "(&(objectClass=user)(objectCategory=person))",
      groupRoleMapping: '{\n  "CN=Doctors": "Provider"\n}',
    },
    saml: {
      enabled: false,
      issuer: "",
      singleSignOnDestination: "",
      idpMetadata: "",
      emailClaim: "email",
      groupClaim: "groups",
      groupRoleMapping: "{}",
    },
  };

  constructor() {
    this.load();
    this.loadMfaStatus();
  }

  private loadMfaStatus(): void {
    firstValueFrom(
      this.http.get<{
        enabled: boolean;
        enrolledAt?: string;
        recoveryCodesRemaining: number;
      }>(`${environment.authApiUrl}/mfa/status`),
    )
      .then((status) => {
        this.mfa = { ...this.mfa, ...status };
        this.cdr.markForCheck();
      })
      .catch((error) => {
        this.mfaError = this.readApiError(
          error,
          this.i18n.t(
            "admin.unableLoadMfaStatus",
            "Unable to load MFA status.",
          ),
        );
        this.cdr.markForCheck();
      });
  }

  async enrollMfa(): Promise<void> {
    this.mfaBusy = true;
    this.mfaError = "";
    try {
      const enrollment = await firstValueFrom(
        this.http.post<{
          secretKey: string;
          qrCodeUri: string;
          recoveryCodes: string[];
        }>(`${environment.authApiUrl}/mfa/enroll`, {}),
      );
      this.mfa = {
        ...this.mfa,
        ...enrollment,
        qrCodeDataUrl: await QRCode.toDataURL(enrollment.qrCodeUri, {
          width: 180,
          margin: 1,
          errorCorrectionLevel: "M",
        }),
        verificationCode: "",
      };
    } catch (error) {
      this.mfaError = this.readApiError(
        error,
        this.i18n.t("admin.unableStartMfa", "Unable to start MFA enrollment."),
      );
    } finally {
      this.mfaBusy = false;
      this.cdr.markForCheck();
    }
  }

  async verifyMfa(): Promise<void> {
    this.mfaBusy = true;
    this.mfaError = "";
    try {
      await firstValueFrom(
        this.http.post(`${environment.authApiUrl}/mfa/verify`, {
          code: this.mfa.verificationCode,
        }),
      );
      this.mfa = {
        ...this.mfa,
        enabled: true,
        qrCodeUri: "",
        qrCodeDataUrl: "",
        secretKey: "",
        verificationCode: "",
      };
      this.snackBar.open(
        this.i18n.t("admin.mfaEnabledSuccessfully", "MFA enabled successfully"),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
      this.loadMfaStatus();
    } catch (error) {
      this.mfaError = this.readApiError(
        error,
        this.i18n.t("admin.unableVerifyMfa", "Unable to verify MFA code."),
      );
    } finally {
      this.mfaBusy = false;
      this.cdr.markForCheck();
    }
  }

  load(): void {
    this.settingsByKey.clear();
    this.resource.load(
      this.api.getIdentitySettings(this.selectedFacilityId).pipe(
        tap((items) => {
          items.forEach((item) => this.settingsByKey.set(item.key, item));
          this.readSettings();
          this.cdr.markForCheck();
        }),
        catchError((error) => {
          this.snackBar.open(
            this.errorMessages.message(
              error,
              "errors.api.providerSettingsLoadFailed",
            ),
            this.i18n.t("admin.close", "Close"),
            { duration: 5000 },
          );
          this.cdr.markForCheck();
          return of([] as IdentitySetting[]);
        }),
      ),
    );
  }

  private readSettings(): void {
    const value = (key: string, fallback: unknown) =>
      this.settingsByKey.get(key)?.value ?? fallback;
    const rawMetadata = String(value("Saml2:IdPMetadata", ""));
    // Repair values saved by the earlier UI where the claim labels could be
    // appended to the metadata textarea value.
    const legacyEmailClaim = rawMetadata
      .match(/Email claim:\s*([^\r\n]+)/i)?.[1]
      ?.trim();
    const legacyGroupClaim = rawMetadata
      .match(/Group claim:\s*([^\r\n]+)/i)?.[1]
      ?.trim();
    const metadata = rawMetadata
      .replace(/\r?\n(?:Email claim|Group claim):[^\r\n]*/gi, "")
      .trim();
    this.settings = {
      passkeys: {
        rpId: String(value("Passkeys:RpId", "localhost")),
        origins: this.toText(value("Passkeys:Origins", "")),
      },
      ldap: {
        enabled: this.toBool(value("Ldap:Enabled", false)),
        server: String(value("Ldap:Server", "")),
        port: Number(value("Ldap:Port", 636)),
        useSsl: this.toBool(value("Ldap:UseSsl", true)),
        searchBase: String(value("Ldap:SearchBase", "")),
        searchFilter: String(
          value(
            "Ldap:SearchFilter",
            "(&(objectClass=user)(objectCategory=person))",
          ),
        ),
        groupRoleMapping: String(
          value("Ldap:GroupRoleMapping", '{\n  "CN=Doctors": "Provider"\n}'),
        ),
      },
      saml: {
        enabled: this.toBool(value("Saml2:Enabled", false)),
        issuer: String(value("Saml2:Issuer", "")),
        singleSignOnDestination: String(
          value("Saml2:SingleSignOnDestination", ""),
        ),
        idpMetadata: metadata,
        emailClaim: String(
          value("Saml2:EmailClaim", legacyEmailClaim ?? "email"),
        ),
        groupClaim: String(
          value("Saml2:GroupClaim", legacyGroupClaim ?? "groups"),
        ),
        groupRoleMapping: String(value("Saml2:GroupRoleMapping", "{}")),
      },
    };
  }

  save(): void {
    if (!this.canWrite) return;
    this.saving = true;
    const updates = [
      { key: "Passkeys:RpId", value: this.settings.passkeys.rpId },
      { key: "Passkeys:Origins", value: this.settings.passkeys.origins },
      { key: "Ldap:Enabled", value: this.settings.ldap.enabled },
      { key: "Ldap:Server", value: this.settings.ldap.server },
      { key: "Ldap:Port", value: this.settings.ldap.port },
      { key: "Ldap:UseSsl", value: this.settings.ldap.useSsl },
      { key: "Ldap:SearchBase", value: this.settings.ldap.searchBase },
      { key: "Ldap:SearchFilter", value: this.settings.ldap.searchFilter },
      {
        key: "Ldap:GroupRoleMapping",
        value: this.settings.ldap.groupRoleMapping,
      },
      { key: "Saml2:Enabled", value: this.settings.saml.enabled },
      { key: "Saml2:Issuer", value: this.settings.saml.issuer },
      {
        key: "Saml2:SingleSignOnDestination",
        value: this.settings.saml.singleSignOnDestination,
      },
      { key: "Saml2:IdPMetadata", value: this.settings.saml.idpMetadata },
      { key: "Saml2:EmailClaim", value: this.settings.saml.emailClaim },
      { key: "Saml2:GroupClaim", value: this.settings.saml.groupClaim },
      {
        key: "Saml2:GroupRoleMapping",
        value: this.settings.saml.groupRoleMapping,
      },
    ];
    this.api.saveIdentitySettings(updates, this.selectedFacilityId).subscribe({
      next: () => {
        this.saving = false;
        this.snackBar.open(
          this.i18n.t("admin.providerSettingsSaved", "Provider settings saved"),
          this.i18n.t("admin.close", "Close"),
          { duration: 3000 },
        );
        this.cdr.markForCheck();
      },
      error: (error) => {
        this.saving = false;
        this.snackBar.open(
          this.errorMessages.message(
            error,
            "errors.api.providerSettingsSaveFailed",
          ),
          this.i18n.t("admin.close", "Close"),
          { duration: 5000 },
        );
        this.cdr.markForCheck();
      },
    });
  }

  private readApiError(error: unknown, fallback: string): string {
    return this.errorMessages.message(error, fallback);
  }
  private toBool(value: unknown): boolean {
    return value === true || value === "true";
  }
  private toText(value: unknown): string {
    return Array.isArray(value) ? value.join(",\n") : String(value ?? "");
  }
}
