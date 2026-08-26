export const operatorMobilePermissions = {
  production: { execute: "manufacturing.production.execute", read: "manufacturing.production.view" },
  quality: { inspect: "manufacturing.quality.inspect", read: "manufacturing.quality.view" },
  maintenance: { complete: "manufacturing.maintenance.complete", read: "manufacturing.maintenance.view" },
  traceability: { read: "manufacturing.traceability.view" },
} as const;
