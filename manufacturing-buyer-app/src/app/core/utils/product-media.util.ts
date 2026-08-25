/** Nacoms-inspired media map — Unsplash CDN (pilot; swap for owned assets). */
const U = (id: string, w = 1200) =>
  `https://images.unsplash.com/${id}?auto=format&fit=crop&w=${w}&q=80`;

const PRODUCT_IMAGES: Record<string, string> = {
  "FX-MANGO-SOFT": U("photo-1605027990126-548a374fe831", 900),
  "FX-MANGO-CHILI": U("photo-1559181563-c2780ebf9d4d", 900),
  "FX-PINE-SOFT": U("photo-1587049350793-b760d7b16036", 900),
  "FX-PINE-CHILI": U("photo-1550258989-632a7033001a", 900),
  "FX-PASSION": U("photo-1615485290624-6c1f5a1a2ae4", 900),
  "FX-MIX": U("photo-1610837125200-a876848f7aeb", 900),
  "FX-KUMQUAT": U("photo-1587735246450-1d7d43f7f9b7", 900),
  "FX-RAMBUTAN": U("photo-1595475203575-5d2716c8c6c3", 900),
};

const CATEGORY_IMAGES: Record<string, string> = {
  xoai: PRODUCT_IMAGES["FX-MANGO-SOFT"],
  thom: PRODUCT_IMAGES["FX-PINE-SOFT"],
  "chanh-day": PRODUCT_IMAGES["FX-PASSION"],
  mix: PRODUCT_IMAGES["FX-MIX"],
  tac: PRODUCT_IMAGES["FX-KUMQUAT"],
  chom: PRODUCT_IMAGES["FX-RAMBUTAN"],
};

export function productImageUrl(sku: string): string {
  return PRODUCT_IMAGES[sku] ?? U("photo-1610837125200-a876848f7f9b7", 900);
}

export function categoryImageUrl(categoryId: string): string {
  return CATEGORY_IMAGES[categoryId] ?? U("photo-1622206157934-1f4720b5d0b0", 900);
}

export const HERO_SLIDES = [
  {
    id: "story",
    image: U("photo-1622206157934-1f4720b5d0b0", 1600),
    eyebrow: "buyer.home.hero.story.eyebrow",
    title: "buyer.home.hero.story.title",
    subtitle: "buyer.home.hero.story.subtitle",
  },
  {
    id: "mango",
    image: U("photo-1605027990126-548a374fe831", 1600),
    eyebrow: "buyer.home.hero.mango.eyebrow",
    title: "buyer.home.hero.mango.title",
    subtitle: "buyer.home.hero.mango.subtitle",
  },
  {
    id: "process",
    image: U("photo-1464226184884-fa280b87eda0", 1600),
    eyebrow: "buyer.home.hero.process.eyebrow",
    title: "buyer.home.hero.process.title",
    subtitle: "buyer.home.hero.process.subtitle",
  },
] as const;

export const FEATURED_PRODUCTS = [
  { sku: "FX-MANGO-SOFT", name: "Xoài sấy dẻo", unitPrice: 85000 },
  { sku: "FX-MANGO-CHILI", name: "Xoài sấy muối ớt", unitPrice: 85000 },
  { sku: "FX-PINE-SOFT", name: "Thơm sấy dẻo", unitPrice: 79000 },
  { sku: "FX-PINE-CHILI", name: "Thơm sấy muối ớt", unitPrice: 79000 },
  { sku: "FX-PASSION", name: "Chanh dây sấy dẻo", unitPrice: 89000 },
  { sku: "FX-MIX", name: "Trái cây sấy hỗn hợp", unitPrice: 95000 },
  { sku: "FX-KUMQUAT", name: "Tắc sấy mật ong", unitPrice: 92000 },
  { sku: "FX-RAMBUTAN", name: "Chôm chôm sấy dẻo", unitPrice: 98000 },
] as const;

export const FOUNDER_STORY = {
  title: "Câu chuyện Nacoms",
  body:
    "Một lần tình cờ founder lang thang ở Đồng Tháp, ngạc nhiên trước cánh đồng xoài trĩu quả và sự sum suê của trái cây miền Tây. Từ đó, Nacoms ra đời với mong muốn mang nông sản Việt đến gần hơn với người tiêu dùng — qua dòng trái cây sấy dẻo chất lượng cao.",
  image: U("photo-1622206157934-1f4720b5d0b0", 1000),
};

export const BLOG_POSTS = [
  {
    title: "Giới thiệu chôm chôm sấy dẻo — món lạ từ Nacoms",
    date: "15/08/2026",
    category: "Sản phẩm",
    excerpt: "Nhiều khách hàng ngạc nhiên, tò mò và cuối cùng là thích thú với chôm chôm sấy dẻo.",
    image: PRODUCT_IMAGES["FX-RAMBUTAN"],
  },
  {
    title: "Thơm sấy dẻo — chua thanh, dai dai sần sật",
    date: "02/07/2026",
    category: "Sản phẩm",
    excerpt: "Làm từ thơm tươi, loại bỏ nước bằng phương pháp sấy lạnh giữ nguyên vị.",
    image: PRODUCT_IMAGES["FX-PINE-SOFT"],
  },
  {
    title: "Ra mắt trái cây sấy hỗn hợp — phối vị miền Tây",
    date: "20/06/2026",
    category: "Tin mới",
    excerpt: "Mix xoài, chanh dây, thơm — thích hợp làm quà tặng và snack văn phòng.",
    image: PRODUCT_IMAGES["FX-MIX"],
  },
] as const;

export type ProductSort = "default" | "price-asc" | "price-desc" | "name";

export function sortProducts<T extends { name: string; unitPrice: number }>(
  items: T[],
  sort: ProductSort,
): T[] {
  const copy = [...items];
  switch (sort) {
    case "price-asc":
      return copy.sort((a, b) => a.unitPrice - b.unitPrice);
    case "price-desc":
      return copy.sort((a, b) => b.unitPrice - a.unitPrice);
    case "name":
      return copy.sort((a, b) => a.name.localeCompare(b.name, "vi"));
    default:
      return copy;
  }
}

export function productCategoryLabel(sku: string): string {
  if (sku.startsWith("FX-MANGO")) return "Xoài sấy";
  if (sku.startsWith("FX-PINE")) return "Thơm sấy";
  if (sku === "FX-PASSION") return "Chanh dây";
  if (sku === "FX-MIX") return "Hỗn hợp";
  if (sku === "FX-KUMQUAT") return "Tắc mật ong";
  if (sku === "FX-RAMBUTAN") return "Chôm chôm";
  return "Trái cây sấy";
}
