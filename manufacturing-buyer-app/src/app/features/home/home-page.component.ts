import {
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { Router, RouterLink } from "@angular/router";
import { DatePipe } from "@angular/common";
import { catchError, of, take } from "rxjs";
import { MatIconModule } from "@angular/material/icon";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeContentArticleDto } from "@his-hope/frontend-foundation/contracts";
import { AuthService } from "../../core/services/auth.service";
import { BUYER_TENANT_KEY, ContentApiService } from "../../core/services/content-api.service";
import {
  BLOG_POSTS,
  FEATURED_PRODUCTS,
  FOUNDER_STORY,
  HERO_SLIDES,
  categoryImageUrl,
  productImageUrl,
} from "../../core/utils/product-media.util";

interface StatItem {
  value: string;
  label: string;
}

interface StoryBlock {
  id: string;
  title: string;
  body: string;
  tag: string;
  image: string;
}

interface HeroSlideView {
  id: string;
  image: string;
  eyebrow: string;
  title: string;
  subtitle: string;
  translateEyebrow: boolean;
  translateTitle: boolean;
  translateSubtitle: boolean;
}

interface FounderStoryView {
  title: string;
  body: string;
  image: string;
}

@Component({
  standalone: true,
  imports: [MatIconModule, RouterLink, DatePipe, HisHopeTranslatePipe],
  templateUrl: "./home-page.component.html",
  styleUrls: ["./home-page.component.scss"],
})
export class HomePageComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly contentApi = inject(ContentApiService);
  readonly i18n = inject(HisHopeI18nService);
  private readonly localeEffect = effect(() => {
    this.loadHome(this.i18n.locale());
  });

  readonly heroSlides = signal<HeroSlideView[]>(
    HERO_SLIDES.map((slide) => ({
      id: slide.id,
      image: slide.image,
      eyebrow: slide.eyebrow,
      title: slide.title,
      subtitle: slide.subtitle,
      translateEyebrow: true,
      translateTitle: true,
      translateSubtitle: true,
    })),
  );
  readonly founderStory = signal<FounderStoryView>({
    title: FOUNDER_STORY.title,
    body: FOUNDER_STORY.body,
    image: FOUNDER_STORY.image,
  });
  readonly featuredProducts = FEATURED_PRODUCTS;
  readonly blogPosts = signal<HisHopeContentArticleDto[]>([]);
  readonly activeSlide = signal(0);

  readonly stats: readonly StatItem[] = [
    { value: "500+", label: "buyer.home.stat.farmers" },
    { value: "8", label: "buyer.home.stat.collections" },
    { value: "100%", label: "buyer.home.stat.sameDay" },
    { value: "63", label: "buyer.home.stat.provinces" },
  ];

  readonly stories: readonly StoryBlock[] = [
    {
      id: "xoai",
      title: "buyer.home.story.mango.title",
      tag: "buyer.home.story.mango.tag",
      image: categoryImageUrl("xoai"),
      body: "buyer.home.story.mango.body",
    },
    {
      id: "thom",
      title: "buyer.home.story.pineapple.title",
      tag: "buyer.home.story.pineapple.tag",
      image: categoryImageUrl("thom"),
      body: "buyer.home.story.pineapple.body",
    },
    {
      id: "chanh-day",
      title: "buyer.home.story.passion.title",
      tag: "buyer.home.story.passion.tag",
      image: categoryImageUrl("chanh-day"),
      body: "buyer.home.story.passion.body",
    },
    {
      id: "mix",
      title: "buyer.home.story.mix.title",
      tag: "buyer.home.story.mix.tag",
      image: categoryImageUrl("mix"),
      body: "buyer.home.story.mix.body",
    },
    {
      id: "tac",
      title: "buyer.home.story.kumquat.title",
      tag: "buyer.home.story.kumquat.tag",
      image: categoryImageUrl("tac"),
      body: "buyer.home.story.kumquat.body",
    },
    {
      id: "chom",
      title: "buyer.home.story.rambutan.title",
      tag: "buyer.home.story.rambutan.tag",
      image: categoryImageUrl("chom"),
      body: "buyer.home.story.rambutan.body",
    },
  ];

  ngOnInit(): void {
    const timerId = window.setInterval(() => this.nextSlide(), 6000);
    this.destroyRef.onDestroy(() => window.clearInterval(timerId));

  }

  private loadHome(locale: string): void {
    this.contentApi
      .getHome(locale)
      .pipe(
        catchError(() => of(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((home) => {
        if (!home) {
          this.blogPosts.set(this.fallbackArticles());
          return;
        }

        if (home.banners.length) {
          this.heroSlides.set(
            home.banners.map((banner) => ({
              id: banner.slideKey,
              image: banner.imageUrl,
              eyebrow: banner.eyebrowKey,
              title: banner.titleKey,
              subtitle: banner.subtitleKey,
              translateEyebrow: true,
              translateTitle: true,
              translateSubtitle: true,
            })),
          );
        }

        if (home.founderStory) {
          this.founderStory.set({
            title: home.founderStory.title,
            body: home.founderStory.excerpt,
            image: home.founderStory.imageUrl,
          });
        }

        this.blogPosts.set(
          home.articles.length ? home.articles : this.fallbackArticles(),
        );
      });
  }

  productImage(sku: string): string {
    return productImageUrl(sku);
  }

  nextSlide(): void {
    this.activeSlide.update(
      (index) => (index + 1) % this.heroSlides().length,
    );
  }

  prevSlide(): void {
    this.activeSlide.update(
      (index) =>
        (index - 1 + this.heroSlides().length) % this.heroSlides().length,
    );
  }

  goToSlide(index: number): void {
    this.activeSlide.set(index);
  }

  startOrdering(): void {
    this.auth.isAuthenticated$
      .pipe(take(1), takeUntilDestroyed(this.destroyRef))
      .subscribe((isAuth) => {
        if (isAuth) {
          void this.router.navigateByUrl("/catalog");
        } else {
          this.auth.login("/catalog");
        }
      });
  }

  goToCooperation(): void {
    void this.router.navigateByUrl("/cooperation");
  }

  scrollToStory(): void {
    document.getElementById("cau-chuyen")?.scrollIntoView({ behavior: "smooth" });
  }

  private fallbackArticles(): HisHopeContentArticleDto[] {
    return BLOG_POSTS.map((post, index) => ({
      id: `fallback-${index}`,
      tenantKey: BUYER_TENANT_KEY,
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
