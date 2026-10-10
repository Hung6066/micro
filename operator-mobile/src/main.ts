import { bootstrapApplication } from "@angular/platform-browser";
import { HisHopePerformanceTelemetryService } from "@his-hope/frontend-foundation";
import { appConfig } from "./app/app.config";
import { AppComponent } from "./app/app.component";

bootstrapApplication(AppComponent, appConfig)
  .then((appRef) =>
    appRef.injector.get(HisHopePerformanceTelemetryService).observeWebVitals(),
  )
  .catch((error) => console.error("[His.Hope Mobile]", error));
