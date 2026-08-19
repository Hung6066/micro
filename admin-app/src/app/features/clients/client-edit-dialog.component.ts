import { Component, Inject, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatChipsModule } from "@angular/material/chips";
import { MatIconModule } from "@angular/material/icon";
import { OidcClient } from "../../core/contracts/admin.contracts";
import { ClientsApiService } from "../../core/services/clients-api.service";
import { catchError, map } from "rxjs/operators";
import { of } from "rxjs";
import {
  HisHopeCreateDialogShellComponent,
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
  HisHopePhiMaskDirective,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeMaterialFormFieldComponent } from "@his-hope/frontend-foundation/forms";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";

function httpsUriLinesValidator(
  control: AbstractControl,
): ValidationErrors | null {
  const values = String(control.value ?? "")
    .split("\n")
    .map((value) => value.trim())
    .filter(Boolean);
  const invalid = values.some((value) => {
    try {
      return new URL(value).protocol !== "https:";
    } catch {
      return true;
    }
  });
  return invalid ? { httpsUri: true } : null;
}

function clientFormValidator(
  control: AbstractControl,
): ValidationErrors | null {
  const grants = control.get("grantTypes")?.value as string[] | undefined;
  const redirectControl = control.get("redirectUris");
  const redirectUris = String(redirectControl?.value ?? "").trim();
  const required = grants?.includes("authorization_code") && !redirectUris;
  const errors = { ...(redirectControl?.errors ?? {}) };

  if (required) {
    errors["redirectUriRequired"] = true;
  } else {
    delete errors["redirectUriRequired"];
  }
  redirectControl?.setErrors(Object.keys(errors).length ? errors : null, {
    emitEvent: false,
  });

  return required ? { redirectUriRequired: true } : null;
}

function jwksValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? "").trim();
  if (!value) return null;
  try {
    return typeof JSON.parse(value) === "object" ? null : { invalidJson: true };
  } catch {
    return { invalidJson: true };
  }
}

@Component({
  selector: "app-client-edit-dialog",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopePhiMaskDirective,
    HisHopeTranslatePipe,
    HisHopeMaterialFormFieldComponent,
  ],
  template: `
    <hh-create-dialog-shell
      [title]="
        (isEdit ? 'admin.editClient' : 'admin.createClient') | hhTranslate
      "
      [subtitle]="'admin.clientDialogSubtitle' | hhTranslate"
    >
      <div hhCreateDialogContent>
        <form class="dialog-form" [formGroup]="formGroup" (ngSubmit)="save()">
          <hh-form-layout>
            <hh-form-section
              [title]="'admin.basicInformation' | hhTranslate"
              [description]="'admin.clientProfileDescription' | hhTranslate"
              [span]="2"
            >
              <div class="form-grid">
                <hh-mat-form-field
                  [control]="formGroup.controls['clientId']"
                  [label]="'admin.clientId' | hhTranslate"
                  [messages]="{
                    required:
                      ('admin.clientIdRequired'
                      | hhTranslate: 'Client ID is required.'),
                  }"
                />
                <hh-mat-form-field
                  [control]="formGroup.controls['displayName']"
                  [label]="'admin.displayName' | hhTranslate"
                  [messages]="{
                    required:
                      ('admin.clientDisplayNameRequired'
                      | hhTranslate: 'Display name is required.'),
                  }"
                />
                <hh-mat-form-field
                  [control]="formGroup.controls['clientType']"
                  [label]="'admin.clientType' | hhTranslate: 'Client type'"
                  kind="select"
                  [options]="[
                    {
                      value: 'Public',
                      label: ('admin.public' | hhTranslate),
                    },
                    {
                      value: 'Confidential',
                      label: ('admin.confidential' | hhTranslate),
                    },
                  ]"
                />
              </div>
            </hh-form-section>
            <hh-form-section
              [title]="'admin.redirectUrisSection' | hhTranslate"
              [description]="'admin.redirectUrisHint' | hhTranslate"
              [span]="2"
            >
              <hh-mat-form-field
                [control]="formGroup.controls['redirectUris']"
                [label]="'admin.redirectUrisOnePerLine' | hhTranslate"
                [hint]="
                  'admin.redirectUrisProductionHint'
                    | hhTranslate
                      : 'Use exact HTTPS callback URLs in production.'
                "
                [multiline]="true"
                [rows]="2"
                [placeholder]="'https://app.example.com/auth/callback'"
                [messages]="{
                  httpsUri:
                    ('admin.clientRedirectUrisInvalid'
                    | hhTranslate
                      : 'Use HTTPS redirect URIs; authorization code requires one.'),
                  redirectUriRequired:
                    ('admin.clientRedirectUrisInvalid'
                    | hhTranslate
                      : 'Use HTTPS redirect URIs; authorization code requires one.'),
                }"
              />
              <hh-mat-form-field
                [control]="formGroup.controls['postLogoutRedirectUris']"
                [label]="'admin.postLogoutUris' | hhTranslate"
                [messages]="{
                  httpsUri:
                    ('admin.clientPostLogoutUrisInvalid'
                    | hhTranslate: 'Use HTTPS post-logout redirect URIs.'),
                }"
                [multiline]="true"
                [rows]="2"
                [placeholder]="'https://app.example.com/auth/login'"
              />
            </hh-form-section>
            <hh-form-section
              [title]="'admin.access' | hhTranslate"
              [description]="'admin.accessDescription' | hhTranslate"
              [span]="2"
            >
              <div class="form-grid">
                <hh-mat-form-field
                  [control]="formGroup.controls['scopes']"
                  [label]="'admin.scopes' | hhTranslate"
                  kind="select"
                  [multiple]="true"
                  [options]="[
                    { value: 'openid', label: 'openid' },
                    { value: 'profile', label: 'profile' },
                    { value: 'email', label: 'email' },
                    { value: 'roles', label: 'roles' },
                    {
                      value: 'hishop:permissions',
                      label: 'hishop:permissions',
                    },
                    { value: 'hishop:admin', label: 'hishop:admin' },
                    { value: 'offline_access', label: 'offline_access' },
                  ]"
                />
                <hh-mat-form-field
                  [control]="formGroup.controls['grantTypes']"
                  [label]="'admin.grantTypes' | hhTranslate"
                  kind="select"
                  [multiple]="true"
                  [options]="[
                    {
                      value: 'authorization_code',
                      label: ('admin.authorizationCode' | hhTranslate),
                    },
                    {
                      value: 'refresh_token',
                      label: ('admin.refreshToken' | hhTranslate),
                    },
                    {
                      value: 'client_credentials',
                      label: ('admin.clientCredentials' | hhTranslate),
                    },
                  ]"
                />
              </div>
            </hh-form-section>
            <hh-form-section
              [title]="'admin.advancedSecurity' | hhTranslate"
              [description]="'admin.advancedSecurityDescription' | hhTranslate"
              [span]="2"
            >
              <hh-mat-form-field
                [control]="formGroup.controls['jwks']"
                [label]="'admin.publicJwks' | hhTranslate"
                [hint]="'admin.publicKeysHint' | hhTranslate"
                [messages]="{
                  invalidJson:
                    ('admin.clientJwksInvalid'
                    | hhTranslate: 'Enter valid JSON for the public JWK set.'),
                }"
                [multiline]="true"
                [rows]="2"
                placeholder='{"keys":[...]}'
              />
            </hh-form-section>
          </hh-form-layout>
        </form>
        <div *ngIf="createdSecret" class="secret-panel" role="alert">
          <strong>{{ "admin.copySecretNow" | hhTranslate }}</strong
          ><code [hhPhiMask]="createdSecret || ''"></code>
          <hh-action-button
            (pressed)="copySecret()"
            kind="secondary"
            icon="content_copy"
            [label]="'admin.copySecret' | hhTranslate"
          />
        </div>
      </div>
      <div hhCreateDialogFooter>
        <hh-action-button
          kind="secondary"
          icon="close"
          [label]="'admin.cancel' | hhTranslate: 'Cancel'"
          (pressed)="cancel()"
        />
        <hh-action-button
          *ngIf="createdSecret"
          (pressed)="finishCreate()"
          kind="primary"
          icon="check"
          [label]="'admin.done' | hhTranslate"
        />
        <hh-action-button
          *ngIf="!createdSecret"
          [disabled]="saving"
          (pressed)="save()"
          kind="primary"
          icon="save"
          [label]="
            saving
              ? ('admin.saving' | hhTranslate)
              : ('admin.save' | hhTranslate)
          "
        />
      </div>
    </hh-create-dialog-shell>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      hh-form-layout {
        display: block;
      }
      .form-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: var(--form-field-gap);
      }
      .full-width {
        width: 100%;
      }
      .secret-panel {
        display: grid;
        gap: var(--space-2);
        margin: var(--space-2) 0 var(--space-4);
        padding: var(--space-3);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-success);
      }
      .secret-panel code {
        overflow-wrap: anywhere;
        padding: var(--space-2);
        background: var(--surface-white);
        border: 1px solid var(--border-default);
        color: var(--text-primary);
      }
      @media (max-width: 720px) {
        .form-grid {
          grid-template-columns: minmax(0, 1fr);
        }
      }
    `,
  ],
})
export class ClientEditDialogComponent {
  private readonly api = inject(ClientsApiService);
  private readonly dialogRef = inject(
    HisHopeDialogRef<ClientEditDialogComponent>,
  );
  private readonly toast = inject(HisHopeToastService);
  private readonly i18n = inject(HisHopeI18nService);

  readonly isEdit: boolean;
  readonly formGroup: FormGroup;
  private readonly initialClient: OidcClient | null;
  saving = false;
  createdSecret: string | null = null;

  constructor(@Inject(HIS_HOPE_DIALOG_DATA) data: OidcClient | null) {
    this.isEdit = !!data;
    this.initialClient = data;
    this.formGroup = new FormGroup(
      {
        clientId: new FormControl(
          { value: data?.clientId ?? "", disabled: this.isEdit },
          { nonNullable: true, validators: [Validators.required] },
        ),
        displayName: new FormControl(data?.displayName ?? "", {
          nonNullable: true,
          validators: [Validators.required],
        }),
        clientType: new FormControl(data?.clientType ?? "Public", {
          nonNullable: true,
          validators: [Validators.required],
        }),
        redirectUris: new FormControl((data?.redirectUris ?? []).join("\n"), {
          nonNullable: true,
          validators: [httpsUriLinesValidator],
        }),
        postLogoutRedirectUris: new FormControl(
          (data?.postLogoutRedirectUris ?? []).join("\n"),
          { nonNullable: true, validators: [httpsUriLinesValidator] },
        ),
        scopes: new FormControl(
          data?.scopes ?? ["openid", "profile", "email", "roles"],
          { nonNullable: true, validators: [Validators.required] },
        ),
        grantTypes: new FormControl(
          data?.grantTypes ?? ["authorization_code", "refresh_token"],
          { nonNullable: true, validators: [Validators.required] },
        ),
        jwks: new FormControl(data?.jwks ?? "", {
          nonNullable: true,
          validators: [jwksValidator],
        }),
      },
      { validators: [clientFormValidator] },
    );
  }

  save(): void {
    this.formGroup.markAllAsTouched();
    if (this.saving || this.formGroup.invalid) return;
    this.saving = true;
    const values = this.formGroup.getRawValue();
    const clientRequest: Partial<OidcClient> = {
      ...this.initialClient,
      clientId: values.clientId.trim(),
      displayName: values.displayName.trim(),
      clientType: values.clientType,
      redirectUris: this.uriLines(values.redirectUris),
      postLogoutRedirectUris: this.uriLines(values.postLogoutRedirectUris),
      scopes: values.scopes,
      grantTypes: values.grantTypes,
      jwks: values.jwks.trim() || undefined,
    };

    const saveRequest = this.isEdit
      ? this.api
          .updateClient(this.initialClient!.id!, clientRequest)
          .pipe(
            map(
              (result) =>
                result as
                  | OidcClient
                  | { clientSecret?: string; clientId?: string },
            ),
          )
      : this.api
          .createClient(clientRequest)
          .pipe(
            map(
              (result) =>
                result as
                  | OidcClient
                  | { clientSecret?: string; clientId?: string },
            ),
          );

    saveRequest
      .pipe(
        catchError((err) => {
          this.toast.error(
            this.i18n.t("admin.saveClientFailed", "Failed to save client"),
            { duration: 3000 },
          );
          this.saving = false;
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) {
          if (
            !this.isEdit &&
            result &&
            "clientSecret" in result &&
            result.clientSecret
          ) {
            this.createdSecret = result.clientSecret;
            this.formGroup.disable();
            this.saving = false;
            this.toast.success(
              this.i18n.t(
                "admin.clientCreated",
                "Client created. Copy the secret now; it will not be shown again.",
              ),
              { duration: 5000 },
            );
            return;
          }
          this.toast.success(
            this.i18n.t("admin.clientSaved", "Client saved successfully"),
            { duration: 2000 },
          );
          this.saving = false;
          this.dialogRef.close(true);
        }
      });
  }

  copySecret(): void {
    if (this.createdSecret) navigator.clipboard?.writeText(this.createdSecret);
  }

  finishCreate(): void {
    this.dialogRef.close(true);
  }

  private uriLines(value: string): string[] {
    return value
      .split("\n")
      .map((uri) => uri.trim())
      .filter(Boolean);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
