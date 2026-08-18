import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-external-identities-page",
  standalone: true,
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
      ><hh-action-button (pressed)="load()" hh-toolbar-actions kind="secondary" icon="refresh" [label]="'admin.refresh' | hhTranslate" /></hh-toolbar
    >
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
  readonly state = new AdminResourceStateController<{
    providers: Array<{
      provider: string;
      displayName: string;
      icon?: string;
      protocol?: string;
      loginUrl?: string;
    }>;
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load external identities.",
  });
  rows: Record<string, unknown>[] = [];
  get loading() {
    return this.state.loading;
  }
  get error() {
    return this.state.error;
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
      const x = this.state.resource.data();
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
    this.state.load(this.api.getExternalIdentityProviders());
  }
}