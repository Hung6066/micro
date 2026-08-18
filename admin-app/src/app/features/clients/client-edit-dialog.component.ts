import { Component, Inject, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { FormGroup } from "@angular/forms";
import {
  MatDialogModule,
  MatDialogRef,
  MAT_DIALOG_DATA,
} from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatButtonModule } from "@angular/material/button";
import { MatChipsModule } from "@angular/material/chips";
import { MatIconModule } from "@angular/material/icon";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { OidcClient } from "../../core/contracts/admin.contracts";
import { ClientsApiService } from "../../core/services/clients-api.service";
import { catchError, map } from "rxjs/operators";
import { of } from "rxjs";
import {
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeFormFieldSchema,
  HisHopeFormRendererComponent,
  HisHopeFormSchema,
  createHisHopeFormGroup,
} from "@his-hope/frontend-foundation/forms";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-client-edit-dialog",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatSnackBarModule,
    HisHopeCreateDialogShellComponent,
    HisHopeFormRendererComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-create-dialog-shell
      [title]="
        (isEdit ? 'admin.editClient' : 'admin.createClient') | hhTranslate
      "
      [subtitle]="'admin.clientDialogSubtitle' | hhTranslate"
    >
      <div hhCreateDialogContent>
        <hh-form-layout>
          <hh-form-section
            [title]="'admin.basicInformation' | hhTranslate"
            [description]="'admin.clientProfileDescription' | hhTranslate"
            [span]="2"
          >
            <div class="form-grid">
              <hh-form-renderer
                [fields]="fields"
                [form]="formGroup"
                (submitted)="save($event)"
              />
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>{{
                  "admin.clientType" | hhTranslate: "Client type"
                }}</mat-label>
                <mat-select name="clientType" [(ngModel)]="form.clientType">
                  <mat-option value="Public">{{
                    "admin.public" | hhTranslate
                  }}</mat-option>
                  <mat-option value="Confidential">{{
                    "admin.confidential" | hhTranslate
                  }}</mat-option>
                </mat-select>
              </mat-form-field>
            </div>
          </hh-form-section>
          <hh-form-section
            [title]="'admin.redirectUrisSection' | hhTranslate"
            [description]="'admin.redirectUrisHint' | hhTranslate"
            [span]="2"
          >
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{
                "admin.redirectUrisOnePerLine" | hhTranslate
              }}</mat-label>
              <textarea
                matInput
                name="redirectUris"
                [(ngModel)]="redirectUrisText"
                rows="2"
                placeholder="https://app.example.com/auth/callback"
              ></textarea>
              <mat-hint>{{
                "admin.redirectUrisProductionHint"
                  | hhTranslate: "Use exact HTTPS callback URLs in production."
              }}</mat-hint>
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{ "admin.postLogoutUris" | hhTranslate }}</mat-label>
              <textarea
                matInput
                name="postLogoutUris"
                [(ngModel)]="postLogoutUrisText"
                rows="2"
                placeholder="https://app.example.com/auth/login"
              ></textarea>
            </mat-form-field>
          </hh-form-section>
          <hh-form-section
            [title]="'admin.access' | hhTranslate"
            [description]="'admin.accessDescription' | hhTranslate"
            [span]="2"
          >
            <div class="form-grid">
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>{{ "admin.scopes" | hhTranslate }}</mat-label>
                <mat-select name="scopes" [(ngModel)]="form.scopes" multiple>
                  <mat-option value="openid">openid</mat-option
                  ><mat-option value="profile">profile</mat-option
                  ><mat-option value="email">email</mat-option
                  ><mat-option value="roles">roles</mat-option
                  ><mat-option value="hishop:permissions"
                    >hishop:permissions</mat-option
                  ><mat-option value="hishop:admin">hishop:admin</mat-option
                  ><mat-option value="offline_access"
                    >offline_access</mat-option
                  >
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>{{ "admin.grantTypes" | hhTranslate }}</mat-label>
                <mat-select
                  name="grantTypes"
                  [(ngModel)]="form.grantTypes"
                  multiple
                >
                  <mat-option value="authorization_code">{{
                    "admin.authorizationCode" | hhTranslate
                  }}</mat-option
                  ><mat-option value="refresh_token">{{
                    "admin.refreshToken" | hhTranslate
                  }}</mat-option
                  ><mat-option value="client_credentials">{{
                    "admin.clientCredentials" | hhTranslate
                  }}</mat-option>
                </mat-select>
              </mat-form-field>
            </div>
          </hh-form-section>
          <hh-form-section
            [title]="'admin.advancedSecurity' | hhTranslate"
            [description]="'admin.advancedSecurityDescription' | hhTranslate"
            [span]="2"
          >
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{ "admin.publicJwks" | hhTranslate }}</mat-label>
              <textarea
                matInput
                name="jwks"
                [(ngModel)]="form.jwks"
                rows="2"
                placeholder='{"keys":[...]}'
              ></textarea>
              <mat-hint>{{ "admin.publicKeysHint" | hhTranslate }}</mat-hint>
            </mat-form-field>
          </hh-form-section>
        </hh-form-layout>
        <div *ngIf="createdSecret" class="secret-panel" role="alert">
          <strong>{{ "admin.copySecretNow" | hhTranslate }}</strong
          ><code>{{ createdSecret }}</code>
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
          [disabled]="formGroup.invalid || saving"
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
  private readonly dialogRef = inject(MatDialogRef<ClientEditDialogComponent>);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(HisHopeI18nService);

  isEdit: boolean;
  readonly formGroup: FormGroup;
  readonly fields: readonly HisHopeFormFieldSchema<unknown>[];
  saving = false;
  createdSecret: string | null = null;

  form: Partial<OidcClient> = {
    clientId: "",
    displayName: "",
    clientType: "Public",
    scopes: ["openid", "profile", "email", "roles"],
    grantTypes: ["authorization_code", "refresh_token"],
  };

  redirectUrisText = "";
  postLogoutUrisText = "";

  constructor(@Inject(MAT_DIALOG_DATA) data: OidcClient | null) {
    this.isEdit = !!data;
    if (data) {
      this.form = { ...data };
      this.redirectUrisText = (data.redirectUris || []).join("\n");
      this.postLogoutUrisText = (data.postLogoutRedirectUris || []).join("\n");
    }
    const schema: HisHopeFormSchema<Record<string, unknown>> = {
      fields: {
        clientId: {
          key: "clientId",
          label: this.i18n.t("admin.clientId"),
          initialValue: this.form.clientId ?? "",
          disabled: this.isEdit,
          required: true,
        },
        displayName: {
          key: "displayName",
          label: this.i18n.t("admin.displayName"),
          initialValue: this.form.displayName ?? "",
          required: true,
        },
      },
    };
    this.fields = Object.values(schema.fields);
    this.formGroup = createHisHopeFormGroup(schema);
  }

  save(values: Record<string, unknown> = this.formGroup.getRawValue()): void {
    this.saving = true;
    this.form.clientId = String(values["clientId"] || this.form.clientId || "");
    this.form.displayName = String(
      values["displayName"] || this.form.displayName || "",
    );
    this.form.redirectUris = this.redirectUrisText
      .split("\n")
      .filter((u) => u.trim());
    this.form.postLogoutRedirectUris = this.postLogoutUrisText
      .split("\n")
      .filter((u) => u.trim());

    const request = this.isEdit
      ? this.api
          .updateClient(this.form.id!, this.form)
          .pipe(
            map(
              (result) =>
                result as
                  | OidcClient
                  | { clientSecret?: string; clientId?: string },
            ),
          )
      : this.api
          .createClient(this.form)
          .pipe(
            map(
              (result) =>
                result as
                  | OidcClient
                  | { clientSecret?: string; clientId?: string },
            ),
          );

    request
      .pipe(
        catchError((err) => {
          this.snackBar.open(
            this.i18n.t("admin.saveClientFailed", "Failed to save client"),
            this.i18n.t("admin.close", "Close"),
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
            this.snackBar.open(
              this.i18n.t(
                "admin.clientCreated",
                "Client created. Copy the secret now; it will not be shown again.",
              ),
              this.i18n.t("admin.close", "Close"),
              { duration: 5000 },
            );
            return;
          }
          this.snackBar.open(
            this.i18n.t("admin.clientSaved", "Client saved successfully"),
            this.i18n.t("admin.close", "Close"),
            { duration: 2000 },
          );
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

  cancel(): void {
    this.dialogRef.close();
  }
}
