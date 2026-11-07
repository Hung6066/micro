import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { DatePipe } from "@angular/common";
import { catchError, of, switchMap } from "rxjs";
import {
  HisHopeApiErrorMessageService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeContentArticleDto } from "@his-hope/frontend-foundation/contracts";
import { ContentApiService } from "../../core/services/content-api.service";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, HisHopeTranslatePipe],
  templateUrl: "./blog-detail-page.component.html",
  styleUrls: ["./blog-detail-page.component.scss"],
})
export class BlogDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ContentApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly errors = inject(HisHopeApiErrorMessageService);
  readonly loading = signal(true);
  readonly error = signal("");
  readonly article = signal<HisHopeContentArticleDto | null>(null);

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const slug = params.get("slug") ?? "";
          this.loading.set(true);
          this.error.set("");
          return this.api
            .getArticle(slug)
            .pipe(
              catchError((error) => {
                this.error.set(
                  this.errors.message(error, "buyer.blog.notFound"),
                );
                return of(null);
              }),
            );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((article) => {
        if (!article && !this.error()) this.error.set("buyer.blog.notFound");
        this.article.set(article);
        this.loading.set(false);
      });
  }
}
