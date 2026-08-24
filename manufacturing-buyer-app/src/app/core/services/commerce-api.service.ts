import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import {
  HisHopeCommerceProductCatalogItemDto,
  HisHopeCommerceProductListResponse,
  HisHopeCommerceRfqDto,
  HisHopeCommerceRfqListResponse,
  HisHopeCreateCommerceRfqRequest,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";

export type ProductCatalogItem = HisHopeCommerceProductCatalogItemDto;

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
  priceTier?: string;
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

export type Rfq = HisHopeCommerceRfqDto;

@Injectable({ providedIn: "root" })
export class CommerceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.commerceApiUrl;
  private readonly manufacturingBase = environment.manufacturingApiUrl;

  getProducts() {
    return this.http.get<HisHopeCommerceProductListResponse>(`${this.base}/products`);
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

  getProfile() {
    return this.http.get<Profile>(`${this.base}/profile`);
  }

  updateProfile(body: Pick<Profile, "displayName" | "phone" | "companyName">) {
    return this.http.put<Profile>(`${this.base}/profile`, body);
  }

  getNotifications() {
    return this.http.get<{ items: NotificationItem[] }>(`${this.base}/notifications`);
  }

  getAvailability(sku: string) {
    return this.http.get<Availability>(`${this.manufacturingBase}/products/${encodeURIComponent(sku)}/availability`);
  }

  getRfqs() {
    return this.http.get<HisHopeCommerceRfqListResponse>(`${this.base}/rfqs`);
  }

  createRfq(body: HisHopeCreateCommerceRfqRequest) {
    return this.http.post<Rfq>(`${this.base}/rfqs`, body);
  }
}
