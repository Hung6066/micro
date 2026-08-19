import {
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";

import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";
import {
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamApiAudiencesResponse,
  IamTrustedIssuersResponse,
} from "../../core/contracts/admin.contracts";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-api-audiences-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.apiAudiences' | hhTranslate: 'API audiences'"
      [subtitle]="
        'admin.apiAudiencesSubtitle'
          | hhTranslate: 'Resource audiences exposed by Identity Service.'
      " /><hh-toolbar hhPageToolbar
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <div *ngIf="error" class="hh-state hh-state--error">{{ error }}</div>
    <hh-data-table
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !rows.length"
    ></hh-data-table
  ></hh-page-layout>`,
})
export class IamApiAudiencesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<IamApiAudiencesResponse>(
    this.destroyRef,
  );
  rows: Record<string, unknown>[] = [];
  private actionError = "";
  get loading() {
    return this.resource.loading();
  }
  get error() {
    return (
      this.actionError ||
      (this.resource.error() ? this.resource.errorMessage() : "")
    );
  }
  get columns(): HisHopeDataTableColumn[] {
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key") },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "audience", label: this.i18n.t("admin.audience", "Audience") },
      { key: "scopeId", label: this.i18n.t("admin.scopeId", "Scope") },
      { key: "lifecycleStatus", label: this.i18n.t("admin.status", "Status") },
    ];
  }
  constructor() {
    effect(() => {
      const x = this.resource.data();
      if (x) {
        this.rows = x.audiences.map((item: unknown) => ({
          ...(item as object),
        }));
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit() {
    this.load();
  }
  load() {
    this.actionError = "";
    this.resource.load(this.api.getIamApiAudiences());
  }
}

@Component({
  selector: "app-iam-trusted-issuers-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.trustedIssuers' | hhTranslate: 'Trusted issuers'"
      [subtitle]="
        'admin.trustedIssuersSubtitle'
          | hhTranslate: 'Configured OIDC/SAML issuer metadata.'
      " /><hh-toolbar hhPageToolbar
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <div *ngIf="error" class="hh-state hh-state--error">{{ error }}</div>
    <hh-data-table
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !rows.length"
    ></hh-data-table
  ></hh-page-layout>`,
})
export class IamTrustedIssuersPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<IamTrustedIssuersResponse>(
    this.destroyRef,
  );
  rows: Record<string, unknown>[] = [];
  private actionError = "";
  get loading() {
    return this.resource.loading();
  }
  get error() {
    return (
      this.actionError ||
      (this.resource.error() ? this.resource.errorMessage() : "")
    );
  }
  get columns(): HisHopeDataTableColumn[] {
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key") },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "issuer", label: this.i18n.t("admin.issuer", "Issuer") },
      { key: "protocol", label: this.i18n.t("admin.protocol", "Protocol") },
      { key: "active", label: this.i18n.t("admin.active", "Active") },
    ];
  }
  constructor() {
    effect(() => {
      const x = this.resource.data();
      if (x) {
        this.rows = x.issuers.map((item: unknown) => ({ ...(item as object) }));
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit() {
    this.load();
  }
  load() {
    this.actionError = "";
    this.resource.load(this.api.getIamTrustedIssuers());
  }
}

@Component({
  selector: "app-iam-external-identities-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.externalIdentities' | hhTranslate: 'External identities'"
      [subtitle]="
        'admin.externalIdentitiesSubtitle'
          | hhTranslate
            : 'Configured browser federation providers. Secrets stay server-side.'
      " /><hh-toolbar hhPageToolbar
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <div *ngIf="error" class="hh-state hh-state--error">{{ error }}</div>
    <hh-data-table
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !rows.length"
    ></hh-data-table
  ></hh-page-layout>`,
})
export class IamExternalIdentitiesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<{
    providers: Array<{
      provider: string;
      displayName: string;
      icon?: string;
      protocol?: string;
      loginUrl?: string;
    }>;
  }>(this.destroyRef);
  rows: Record<string, unknown>[] = [];
  private actionError = "";
  get loading() {
    return this.resource.loading();
  }
  get error() {
    return (
      this.actionError ||
      (this.resource.error() ? this.resource.errorMessage() : "")
    );
  }
  get columns(): HisHopeDataTableColumn[] {
    return [
      { key: "provider", label: this.i18n.t("admin.provider", "Provider") },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "protocol", label: this.i18n.t("admin.protocol", "Protocol") },
      { key: "loginUrl", label: this.i18n.t("admin.loginUrl", "Login URL") },
    ];
  }
  constructor() {
    effect(() => {
      const x = this.resource.data();
      if (x) {
        this.rows = x.providers.map((item: unknown) => ({
          ...(item as object),
        }));
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit() {
    this.load();
  }
  load() {
    this.actionError = "";
    this.resource.load(this.api.getExternalIdentityProviders());
  }
}
