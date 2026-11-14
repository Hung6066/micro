/** Manufacturing permissions enforced by ManufacturingService and Identity. */
export const operatorMobilePermissions = {
  production: { access: "manufacturing.production.execute" },
  quality: { access: "manufacturing.quality.inspect" },
  maintenance: { access: "manufacturing.maintenance.complete" },
  /** Lot scan / genealogy — production-floor access. */
  traceability: { access: "manufacturing.production.execute" },
} as const;
