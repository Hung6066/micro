import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { map } from "rxjs";
import {
  HisHopeCommerceProductCatalogItemDto,
  HisHopeCommerceRfqDto,
  HisHopeCommerceRfqListResponse,
  HisHopeCreateCommerceRfqRequest,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";

export type ProductCatalogItem = HisHopeCommerceProductCatalogItemDto;
export type Product = ProductCatalogItem & { unitPrice: number };
export type Rfq = HisHopeCommerceRfqDto;

export interface CartLine {
  productId: string;
  quantity: number;
}

export interface OrderLine {
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

export interface Order {
  id: string;
  tenantKey: string;
  buyerUserId: string;
  status: string;
  totalAmount: number;
  createdAt: string;
  lines: OrderLine[];
}

export interface Profile {
  userId: string;
  tenantKey: string;
  displayName: string;
  email: string;
  phone: string;
  companyName: string;
}

export interface NotificationItem {
  id: string;
  title: string;
  message: string;
  createdAt: string;
  isRead: boolean;
}

export interface Availability {
  tenantKey: string;
  sku: string;
  releasedQuantity: number;
  uom: string;
  asOf: string;
}

@Injectable({ providedIn: "root" })
export class CommerceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.commerceApiUrl;
  private readonly manufacturingBase = environment.manufacturingApiUrl;

  getProducts() {
    return this.http
      .get<{ items: ProductCatalogItem[] }>(`${this.base}/products`)
      .pipe(
        map((response) => ({
          items: response.items.map((item) => ({ ...item, unitPrice: item.effectiveUnitPrice })),
        })),
      );
  }

  getProduct(productId: string) {
    return this.http
      .get<ProductCatalogItem>(`${this.base}/products/${encodeURIComponent(productId)}`)
      .pipe(map((item) => ({ ...item, unitPrice: item.effectiveUnitPrice })));
  }

  getCart() {
    return this.http.get<{ tenantKey: string; lines: CartLine[] }>(`${this.base}/cart`);
  }

  updateCart(lines: CartLine[]) {
    return this.http.put<{ tenantKey: string; lines: CartLine[] }>(`${this.base}/cart`, { lines });
  }

  checkout() {
    return this.http.post<Order>(`${this.base}/orders`, {});
  }

  getOrders() {
    return this.http.get<{ items: Order[] }>(`${this.base}/orders`);
  }

  getOrder(orderId: string) {
    return this.http.get<Order>(`${this.base}/orders/${encodeURIComponent(orderId)}`);
  }

  getProfile() {
    return this.http.get<Profile>(`${this.base}/profile`);
  }

  updateProfile(body: Pick<Profile, "displayName" | "phone" | "companyName">) {
    return this.http.put<Profile>(`${this.base}/profile`, body);
  }

  getNotifications() {
    return this.http.get<{ items: NotificationItem[] }>(`${this.base}/notifications`);
  }

  markNotificationRead(notificationId: string) {
    return this.http.patch<void>(
      `${this.base}/notifications/${encodeURIComponent(notificationId)}/read`,
      {},
    );
  }

  getRfqs() {
    return this.http.get<HisHopeCommerceRfqListResponse>(`${this.base}/rfqs`);
  }

  getRfq(rfqId: string) {
    return this.http.get<Rfq>(`${this.base}/rfqs/${encodeURIComponent(rfqId)}`);
  }

  createRfq(body: HisHopeCreateCommerceRfqRequest) {
    return this.http.post<Rfq>(`${this.base}/rfqs`, body);
  }

  getAvailability(sku: string) {
    return this.http.get<Availability>(`${this.manufacturingBase}/products/${encodeURIComponent(sku)}/availability`);
  }
}
