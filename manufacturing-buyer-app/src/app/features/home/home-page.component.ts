import {
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { Router } from "@angular/router";
import { take } from "rxjs";
import { MatIconModule } from "@angular/material/icon";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { AuthService } from "../../core/services/auth.service";
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

@Component({
  standalone: true,
  imports: [MatIconModule, HisHopeTranslatePipe],
  templateUrl: "./home-page.component.html",
  styleUrls: ["./home-page.component.scss"],
})
export class HomePageComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly i18n = inject(HisHopeI18nService);

  readonly heroSlides = HERO_SLIDES;
  readonly founderStory = FOUNDER_STORY;
  readonly featuredProducts = FEATURED_PRODUCTS;
  readonly blogPosts = BLOG_POSTS;
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

  productImage(sku: string): string {
    return productImageUrl(sku);
  }

  nextSlide(): void {
    this.activeSlide.update((index) => (index + 1) % this.heroSlides.length);
  }

  prevSlide(): void {
    this.activeSlide.update(
      (index) => (index - 1 + this.heroSlides.length) % this.heroSlides.length,
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

  scrollToCooperation(): void {
    document.getElementById("hop-tac")?.scrollIntoView({ behavior: "smooth" });
  }

  scrollToStory(): void {
    document.getElementById("cau-chuyen")?.scrollIntoView({ behavior: "smooth" });
  }
}
