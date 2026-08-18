import { Component } from "@angular/core";
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  selector: "app-forbidden-page",
  standalone: true,
  imports: [
    HisHopePageLayoutComponent,
    HisHopePageHeaderComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.forbiddenTitle' | hhTranslate: 'Access denied'"
        [subtitle]="
          'admin.forbiddenSubtitle'
            | hhTranslate: 'Your account does not have the required permission.'
        "
      />
    </hh-page-layout>
  `,
})
export class ForbiddenPageComponent {}
