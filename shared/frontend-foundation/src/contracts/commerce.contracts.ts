/** Mirrors CommerceService order DTOs (camelCase JSON). */

export interface HisHopeCommerceOrderLineDto {
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

export interface HisHopeCommerceOrderDto {
  id: string;
  tenantKey: string;
  buyerUserId: string;
  status: string;
  totalAmount: number;
  createdAt: string;
  lines: HisHopeCommerceOrderLineDto[];
}

export interface HisHopeCommerceOrderListResponse {
  items: HisHopeCommerceOrderDto[];
}

export type HisHopeCommerceOrderStatus =
  | "pending"
  | "confirmed"
  | "shipped"
  | "cancelled"
  | string;

export interface HisHopeCommerceProductCatalogItemDto {
  id: string;
  sku: string;
  name: string;
  description: string;
  effectiveUnitPrice: number;
  listUnitPrice: number;
  wholesaleUnitPrice: number;
  minOrderQty: number;
  supportsPrivateLabel: boolean;
  supportsExport: boolean;
  priceTier: string;
  tenantKey: string;
}

export interface HisHopeCommerceProductListResponse {
  items: HisHopeCommerceProductCatalogItemDto[];
}

export interface HisHopeCommerceRfqLineDto {
  productId: string;
  quantity: number;
  notes?: string | null;
}

export interface HisHopeCommerceRfqDto {
  id: string;
  tenantKey: string;
  buyerUserId: string;
  status: string;
  message: string;
  quotedTotal?: number | null;
  operatorNotes?: string | null;
  createdAt: string;
  respondedAt?: string | null;
  lines: HisHopeCommerceRfqLineDto[];
}

export interface HisHopeCommerceRfqListResponse {
  items: HisHopeCommerceRfqDto[];
}

export interface HisHopeCreateCommerceRfqRequest {
  message: string;
  lines: HisHopeCommerceRfqLineDto[];
}

export interface HisHopeRespondCommerceRfqRequest {
  quotedTotal: number;
  operatorNotes: string;
  status: string;
}
