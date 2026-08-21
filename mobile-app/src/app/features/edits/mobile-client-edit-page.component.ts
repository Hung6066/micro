import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ValidationErrors,
  Validators,
} from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeActionButtonComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
  HisHopeMobileEntityEditPageComponent,
  HisHopePhiMaskDirective,
  HisHopeStateComponent,
  HisHopeToastService,
  focusFirstInvalidControl,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeMaterialFormFieldComponent } from "@his-hope/frontend-foundation/forms";
import { catchError, of } from "rxjs";
import { map } from "rxjs/operators";
import { OidcClient } from "../../core/contracts/mobile.contracts";
import { ClientsApiService } from "../../core/services/clients-api.service";

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
  standalone: true,
  imports: [
    CommonModule,
    HisHopeActionButtonComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeMaterialFormFieldComponent,
    HisHopeMobileEntityEditPageComponent,
    HisHopePhiMaskDirective,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading) {
      <hh-state kind="loading" [message]="'admin.loading' | hhTranslate" />
    } @else {
      <hh-mobile-entity-edit-page
        [title]="isEdit ? 'admin.editClient' : 'admin.createClient'"
        [titleFallback]="isEdit ? 'Edit client' : 'Create client'"
        subtitle="admin.clientDialogSubtitle"
        [formGroup]="formGroup"
        [saving]="saving"
        [saveLabel]="createdSecret ? 'admin.done' : 'admin.save'"
        (save)="createdSecret ? finishCreate() : save()"
        (cancel)="goBack()"
      >
        <hh-form-layout>
          <hh-form-section
            [title]="'admin.basicInformation' | hhTranslate"
            [description]="'admin.clientProfileDescription' | hhTranslate"
            [span]="2"
          >
            <hh-mat-form-field
              [control]="formGroup.controls['clientId']"
              [label]="'admin.clientId' | hhTranslate"
            />
            <hh-mat-form-field
              [control]="formGroup.controls['displayName']"
              [label]="'admin.displayName' | hhTranslate"
            />
            <hh-mat-form-field
              [control]="formGroup.controls['clientType']"
              [label]="'admin.clientType' | hhTranslate: 'Client type'"
              kind="select"
              [options]="[
                { value: 'Public', label: ('admin.public' | hhTranslate) },
                {
                  value: 'Confidential',
                  label: ('admin.confidential' | hhTranslate),
                },
              ]"
            />
          </hh-form-section>
          <hh-form-section
            [title]="'admin.redirectUrisSection' | hhTranslate"
            [description]="'admin.redirectUrisHint' | hhTranslate"
            [span]="2"
          >
            <hh-mat-form-field
              [control]="formGroup.controls['redirectUris']"
              [label]="'admin.redirectUrisOnePerLine' | hhTranslate"
              [multiline]="true"
              [rows]="3"
              [messages]="{
                httpsUri:
                  ('admin.clientRedirectUrisHttpsInvalid'
                  | hhTranslate: 'Use HTTPS redirect URIs.'),
                redirectUriRequired:
                  ('admin.clientRedirectUriRequired'
                  | hhTranslate
                    : 'At least one redirect URI is required for the authorization code grant.'),
              }"
            />
            <hh-mat-form-field
              [control]="formGroup.controls['postLogoutRedirectUris']"
              [label]="'admin.postLogoutUris' | hhTranslate"
              [multiline]="true"
              [rows]="2"
              [messages]="{
                httpsUri:
                  ('admin.clientPostLogoutUrisInvalid'
                  | hhTranslate: 'Use HTTPS post-logout redirect URIs.'),
              }"
            />
          </hh-form-section>
          <hh-form-section
            [title]="'admin.access' | hhTranslate"
            [description]="'admin.accessDescription' | hhTranslate"
            [span]="2"
          >
            <hh-mat-form-field
              [control]="formGroup.controls['scopes']"
              [label]="'admin.scopes' | hhTranslate"
              kind="select"
              [multiple]="true"
              [options]="scopeOptions"
            />
            <hh-mat-form-field
              [control]="formGroup.controls['grantTypes']"
              [label]="'admin.grantTypes' | hhTranslate"
              kind="select"
              [multiple]="true"
              [options]="grantTypeOptions"
            />
          </hh-form-section>
          <hh-form-section
            [title]="'admin.advancedSecurity' | hhTranslate"
            [description]="'admin.advancedSecurityDescription' | hhTranslate"
            [span]="2"
          >
            <hh-mat-form-field
              [control]="formGroup.controls['jwks']"
              [label]="'admin.publicJwks' | hhTranslate"
              [multiline]="true"
              [rows]="2"
              [messages]="{
                invalidJson:
                  ('admin.clientJwksInvalid'
                  | hhTranslate: 'Enter valid JSON for the public JWK set.'),
              }"
            />
          </hh-form-section>
        </hh-form-layout>
        @if (createdSecret) {
          <div class="secret-panel" hhMobileEntityEditExtra role="alert">
            <strong>{{ "admin.copySecretNow" | hhTranslate }}</strong>
            <code [hhPhiMask]="createdSecret"></code>
            <hh-action-button
              (pressed)="copySecret()"
              kind="secondary"
              icon="content_copy"
              [label]="'admin.copySecret' | hhTranslate"
            />
          </div>
        }
      </hh-mobile-entity-edit-page>
    }
  `,
  styles: [
    `
      .secret-panel {
        display: grid;
        gap: var(--space-inset);
        margin-top: var(--size-timeline-dot);
        padding: var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-success);
      }
      .secret-panel code {
        overflow-wrap: anywhere;
        padding: var(--space-sm);
        background: var(--surface-white);
        border: 1px solid var(--border-default);
      }
    `,
  ],
})
export class MobileClientEditPageComponent implements OnInit {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ClientsApiService);
  private readonly toast = inject(HisHopeToastService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);

  isEdit = false;
  loading = false;
  saving = false;
  createdSecret: string | null = null;
  formGroup!: FormGroup;
  private initialClient: OidcClient | null = null;

  readonly scopeOptions = [
    { value: "openid", label: "openid" },
    { value: "profile", label: "profile" },
    { value: "email", label: "email" },
    { value: "roles", label: "roles" },
    { value: "hishop:permissions", label: "hishop:permissions" },
    { value: "hishop:admin", label: "hishop:admin" },
    { value: "offline_access", label: "offline_access" },
  ];

  readonly grantTypeOptions = [
    {
      value: "authorization_code",
      label: this.i18n.t("admin.authorizationCode", "Authorization code"),
    },
    {
      value: "refresh_token",
      label: this.i18n.t("admin.refreshToken", "Refresh token"),
    },
    {
      value: "client_credentials",
      label: this.i18n.t("admin.clientCredentials", "Client credentials"),
    },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get("id");
    this.isEdit = !!id;
    if (this.isEdit && id) {
      this.loading = true;
      this.api.getClient(id).subscribe({
        next: (client) => {
          this.initialClient = client;
          this.initForm(client);
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.toast.error(
            this.i18n.t("admin.loadClientsFailed", "Failed to load client."),
          );
          this.goBack();
        },
      });
      return;
    }
    this.initForm(null);
  }

  private initForm(data: OidcClient | null): void {
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
    if (this.saving || this.formGroup.invalid) {
      if (this.formGroup.invalid) {
        focusFirstInvalidControl(this.host.nativeElement);
      }
      return;
    }
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
        catchError(() => {
          this.toast.error(
            this.i18n.t("admin.saveClientFailed", "Failed to save client"),
          );
          this.saving = false;
          this.cdr.markForCheck();
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (!result) return;
        if (
          !this.isEdit &&
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
          );
          this.cdr.markForCheck();
          return;
        }
        this.toast.success(
          this.i18n.t("admin.clientSaved", "Client saved successfully"),
        );
        this.goBack();
      });
  }

  copySecret(): void {
    if (this.createdSecret) navigator.clipboard?.writeText(this.createdSecret);
  }

  finishCreate(): void {
    this.goBack();
  }

  goBack(): void {
    void this.router.navigateByUrl("/admin/clients");
  }

  private uriLines(value: string): string[] {
    return value
      .split("\n")
      .map((uri) => uri.trim())
      .filter(Boolean);
  }
}
