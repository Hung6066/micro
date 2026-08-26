import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, effect, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { DatePipe } from "@angular/common";
import { RouterLink } from "@angular/router";
import { catchError, of } from "rxjs";
import {
  HisHopeApiErrorMessageService,
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeContentArticleDto } from "@his-hope/frontend-foundation/contracts";
import { ContentApiService } from "../../core/services/content-api.service";
import { BLOG_POSTS } from "../../core/utils/product-media.util";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, HisHopeTranslatePipe],
  templateUrl: "./blog-list-page.component.html",
  styleUrls: ["./blog-list-page.component.scss"],
})
export class BlogListPageComponent implements OnInit {
  private readonly api = inject(ContentApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly errors = inject(HisHopeApiErrorMessageService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly localeEffect = effect(() => {
    this.loadArticles(this.i18n.locale());
  });
  readonly loading = signal(true);
  readonly error = signal("");
  readonly articles = signal<HisHopeContentArticleDto[]>([]);

  ngOnInit(): void {
  }

  private loadArticles(locale: string): void {
    this.loading.set(true);
    this.api
      .getArticles(locale)
      .pipe(
        catchError((error) => {
          this.error.set(this.errors.message(error, "buyer.blog.error"));
          return of({ items: this.fallbackArticles() });
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => {
          this.articles.set(response.items ?? []);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  private fallbackArticles(): HisHopeContentArticleDto[] {
    return BLOG_POSTS.map((post, index) => ({
      id: `fallback-${index}`,
      tenantKey: "customer-factory-x",
      slug: post.title.toLowerCase().replace(/\s+/g, "-"),
      title: post.title,
      excerpt: post.excerpt,
      bodyHtml: `<p>${post.excerpt}</p>`,
      category: post.category,
      imageUrl: post.image,
      locale: this.i18n.locale(),
      status: "published",
      publishedAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }));
  }
}
