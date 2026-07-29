import { HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { HisHopeI18nService } from "./his-hope-i18n.service";

/** Applies the platform contract to every API request made by Angular apps. */
export const hisHopeInternationalizationInterceptor: HttpInterceptorFn = (req, next) => {
  const i18n = inject(HisHopeI18nService);
  return next(req.clone({ setHeaders: {
    "Accept-Language": i18n.apiLocale(),
    "X-Timezone": i18n.timeZone(),
    "X-Currency": i18n.currency(),
  }}));
};
