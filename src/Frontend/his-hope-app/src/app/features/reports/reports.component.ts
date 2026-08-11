import { Component, ChangeDetectionStrategy } from '@angular/core';
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-reports',
    standalone: true,
    imports: [HisHopePageLayoutComponent, HisHopePageHeaderComponent, HisHopeStateComponent, HisHopeTranslatePipe],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'reports.title' | hhTranslate:'Báo cáo & Thống kê'"
                      [subtitle]="'reports.subtitle' | hhTranslate:'Tính năng đang được phát triển'" />
      <hh-state kind="empty" icon="bar_chart"
                [message]="'reports.message' | hhTranslate:'Trang báo cáo và thống kê sẽ sớm được ra mắt.'" />
    </hh-page-layout>
  `,
})
export class ReportsComponent {}
