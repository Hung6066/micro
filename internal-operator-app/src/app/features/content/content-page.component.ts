import { DatePipe, DecimalPipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import {
  HisHopeActionButtonComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeTabsComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
  HisHopeApiErrorMessageService as ApiErrorMessageService,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeContentArticleDto, HisHopeContentMediaAssetDto, HisHopePartnershipInquiryDto } from "@his-hope/frontend-foundation/contracts";
import { ContentApiService, UpsertArticleRequest } from "../../core/services/content-api.service";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTabsComponent,
    HisHopeTranslatePipe,
  ],
  templateUrl: "./content-page.component.html",
  styleUrls: ["./content-page.component.scss"],
})
export class ContentPageComponent implements OnInit {
  readonly activeTab = signal<"articles" | "inquiries" | "media">("articles");
  selectTab(tab: "articles" | "inquiries" | "media"): void { this.activeTab.set(tab); }
  private readonly api = inject(ContentApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly errors = inject(ApiErrorMessageService);
  readonly i18n = inject(HisHopeI18nService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal("");
  readonly statusKey = signal("");
  readonly articles = signal<HisHopeContentArticleDto[]>([]);
  readonly inquiries = signal<HisHopePartnershipInquiryDto[]>([]);
  readonly media = signal<HisHopeContentMediaAssetDto[]>([]);
  readonly uploading = signal(false);
  readonly mediaStatusKey = signal("");
  editingId: string | null = null;
  draft: UpsertArticleRequest = this.emptyDraft();

  ngOnInit(): void {
    this.reload();
  }

  startNew(): void {
    this.editingId = null;
    this.draft = this.emptyDraft();
    this.statusKey.set("");
  }

  editArticle(article: HisHopeContentArticleDto): void {
    this.editingId = article.id;
    this.draft = {
      slug: article.slug,
      title: article.title,
      excerpt: article.excerpt,
      bodyHtml: article.bodyHtml,
      category: article.category,
      imageUrl: article.imageUrl,
      locale: article.locale,
      status: article.status,
      seoTitle: article.seoTitle ?? null,
      seoDescription: article.seoDescription ?? null,
      seoKeywords: article.seoKeywords ?? null,
      publishedAt: article.publishedAt,
    };
    this.statusKey.set("");
  }

  saveArticle(): void {
    this.saving.set(true);
    this.statusKey.set("");
    const request$ = this.editingId
      ? this.api.updateArticle(this.editingId, this.draft)
      : this.api.createArticle(this.draft);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.statusKey.set("operator.content.saved");
        this.saving.set(false);
        this.startNew();
        this.reloadArticles();
      },
      error: (err) => {
        this.error.set(this.errors.message(err, "operator.content.error"));
        this.saving.set(false);
        this.cdr.markForCheck();
      },
    });
  }

  deleteArticle(articleId: string): void {
    this.api
      .deleteArticle(articleId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.statusKey.set("operator.content.deleted");
          if (this.editingId === articleId) this.startNew();
          this.reloadArticles();
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.content.error"));
          this.cdr.markForCheck();
        },
      });
  }

  updateInquiryStatus(inquiry: HisHopePartnershipInquiryDto, status: string): void {
    this.api
      .updateInquiryStatus(inquiry.id, status)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.reloadInquiries(),
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.content.error"));
          this.cdr.markForCheck();
        },
      });
  }

  onMediaSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.mediaStatusKey.set("");
    this.api
      .uploadMedia(file)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.mediaStatusKey.set("operator.content.media.uploaded");
          this.uploading.set(false);
          this.reloadMedia();
          input.value = "";
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.content.media.error"));
          this.uploading.set(false);
          input.value = "";
          this.cdr.markForCheck();
        },
      });
  }

  useMediaUrl(url: string): void {
    this.draft = { ...this.draft, imageUrl: url };
    this.statusKey.set("operator.content.media.applied");
    this.cdr.markForCheck();
  }

  copyMediaUrl(url: string): void {
    void navigator.clipboard?.writeText(url);
    this.mediaStatusKey.set("operator.content.media.copied");
    this.cdr.markForCheck();
  }

  private reload(): void {
    this.loading.set(true);
    this.error.set("");
    this.api
      .getArticles()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.articles.set(response.items ?? []);
          this.loading.set(false);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.content.error"));
          this.loading.set(false);
          this.cdr.markForCheck();
        },
      });

    this.reloadInquiries();
    this.reloadMedia();
  }

  private reloadArticles(): void {
    this.api
      .getArticles()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.articles.set(response.items ?? []);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.content.error"));
          this.cdr.markForCheck();
        },
      });
  }

  private reloadInquiries(): void {
    this.api
      .getInquiries()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.inquiries.set(response.items ?? []);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.content.error"));
          this.cdr.markForCheck();
        },
      });
  }

  private reloadMedia(): void {
    this.api
      .getMedia()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.media.set(response.items ?? []);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.content.media.error"));
          this.cdr.markForCheck();
        },
      });
  }

  private emptyDraft(): UpsertArticleRequest {
    return {
      slug: "",
      title: "",
      excerpt: "",
      bodyHtml: "<p></p>",
      category: "Tin mới",
      imageUrl: "",
      locale: "vi-VN",
      status: "draft",
      seoTitle: null,
      seoDescription: null,
      seoKeywords: null,
      publishedAt: new Date().toISOString(),
    };
  }
}
