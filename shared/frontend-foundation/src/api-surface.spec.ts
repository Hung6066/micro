/**
 * Golden-file test for the package's public API surface.
 *
 * Every export list below was captured from an actual build/test run. If this
 * test fails, either the change was accidental (fix the export) or it is an
 * intentional API change (update the expected list here in the same PR so the
 * removal/addition is explicit in review, per COMPATIBILITY.md).
 */
import * as mainEntry from "./index";
import * as uiEntry from "../ui/src/public-api";
import * as authEntry from "../auth/src/public-api";
import * as formsEntry from "../forms/src/index";
import * as domainEntry from "../domain/src/index";
import * as queryEntry from "../query/src/public-api";
import * as i18nEntry from "../i18n/src/public-api";
import * as contractsEntry from "./contracts/public-api";

const EXPECTED = {
  main: [
    "DEFAULT_HIS_HOPE_DESIGN_PRESET",
    "HIS_HOPE_DESIGN_PRESETS",
    "HisHopeAuditFeedbackService",
    "HisHopeBrowserPasskeyClient",
    "HisHopeErrorReportingService",
    "HisHopeGlobalErrorHandler",
    "HisHopePerformanceTelemetryService",
    "HisHopePresetSwitcherComponent",
    "HisHopeThemeService",
    "RuntimeConfigService",
    "createHisHopeAdaptiveMfaState",
    "createHisHopeBearerTokenInterceptor",
    "getHisHopeAdaptiveMfaAlternateMethods",
    "getHisHopeDesignPreset",
    "hisHopeCookieSessionInterceptor",
    "hisHopeCorrelationIdInterceptor",
    "hisHopeErrorInterceptor",
    "setHisHopeAdaptiveMfaAlternateMethodsOpen",
    "unwrapCollection",
  ],
  ui: [
    "HisHopeActionButtonComponent",
    "HisHopeAlertComponent",
    "HisHopeBrandComponent",
    "HisHopeBreadcrumbComponent",
    "HisHopeButtonComponent",
    "HisHopeCommandPaletteComponent",
    "HisHopeConfirmDialogComponent",
    "HisHopeChipsComponent",
    "HisHopeCreateDialogShellComponent",
    "HisHopeDataTableCellDirective",
    "HisHopeDataTableComponent",
    "HisHopeDataTableDetailDirective",
    "HisHopeDateRangeComponent",
    "HisHopeDescriptionListComponent",
    "HisHopeDialogRef",
    "HisHopeDialogService",
    "HisHopeDrawerComponent",
    "HisHopeFileUploadComponent",
    "HisHopeFilterToolbarComponent",
    "HisHopeFormFieldComponent",
    "HisHopeFormLayoutComponent",
    "HisHopeFormSectionComponent",
    "HisHopeIconButtonComponent",
    "HisHopeMenuComponent",
    "HisHopeMenuItemDirective",
    "HisHopeMenuTriggerDirective",
    "HisHopeMetaItemComponent",
    "HisHopeMetricCardComponent",
    "HisHopeMobileAccordionComponent",
    "HisHopeMobileActionSheetComponent",
    "HisHopeMobileAvatarComponent",
    "HisHopeMobileBottomSheetComponent",
    "HisHopeMobileDateTimeComponent",
    "HisHopeMobileIconComponent",
    "HisHopeMobileInfiniteListComponent",
    "HisHopeMobileListComponent",
    "HisHopeMobileListItemComponent",
    "HisHopeMobileOtpComponent",
    "HisHopeMobileRefresherComponent",
    "HisHopeMobileSearchbarComponent",
    "HisHopeMobileSegmentComponent",
    "HisHopeMultiSelectComponent",
    "HisHopeOfflineBannerComponent",
    "HisHopePageHeaderComponent",
    "HisHopePageLayoutComponent",
    "HisHopePageSectionComponent",
    "HisHopePermissionButtonComponent",
    "HisHopePhiMaskDirective",
    "HisHopePopoverComponent",
    "HisHopeSelectComponent",
    "HisHopeSessionTimeoutDialogComponent",
    "HisHopeSkeletonComponent",
    "HisHopeStateComponent",
    "HisHopeStatusBadgeComponent",
    "HisHopeTableEditorComponent",
    "HisHopeTableShellComponent",
    "HisHopeTableStateComponent",
    "HisHopeTabsComponent",
    "HisHopeToastComponent",
    "HisHopeToastService",
    "HisHopeToolbarComponent",
    "HisHopeTooltipDirective",
    "HisHopeTooltipPanelComponent",
    "HisHopeWorkspaceHeaderComponent",
    "HIS_HOPE_DIALOG_DATA",
    "createHisHopeCursorQuery",
    "parseHisHopeDataTableQuery",
    "sameHisHopeDataTableQuery",
    "serializeHisHopeDataTableQuery",
    "toHisHopeTableEditorValue",
    "toggleHisHopeDataTableSort",
  ],
  auth: [
    "HisHopeAuthCoordinator",
    "HisHopeHasPermissionDirective",
    "HisHopePermissionService",
  ],
  forms: [
    "HisHopeFormRendererComponent",
    "HisHopeMaterialValidationErrorComponent",
    "HisHopeMaterialFormFieldComponent",
    "HisHopeSelectFieldComponent",
    "HisHopeTextFieldComponent",
    "HisHopeValidationMessageRegistry",
    "createHisHopeFormGroup",
  ],
  domain: [
    "HisHopeDiffViewerComponent",
    "HisHopeTimelineComponent",
    "HisHopeTransferListComponent",
    "HisHopeTreeComponent",
  ],
  query: [
    "HisHopeQueryStateService",
    "HisHopeRequestCacheService",
    "HisHopeResourceState",
    "HisHopeResourceStore",
  ],
  i18n: [
    "HIS_HOPE_LOCALIZATION_API_URL",
    "HisHopeI18nService",
    "HisHopeLanguageSwitcherComponent",
    "HisHopeLocalizationApiService",
    "HisHopeTranslatePipe",
    "hisHopeEn",
    "hisHopeInternationalizationInterceptor",
    "hisHopeViVN",
  ],
  contracts: [
    "decorateReferenceRow",
    "friendlyNameLabel",
    "friendlyReferenceLabel",
  ],
} as const;

describe("public API surface (golden file)", () => {
  const actual: Record<string, string[]> = {
    main: Object.keys(mainEntry).sort(),
    ui: Object.keys(uiEntry).sort(),
    auth: Object.keys(authEntry).sort(),
    forms: Object.keys(formsEntry).sort(),
    domain: Object.keys(domainEntry).sort(),
    query: Object.keys(queryEntry).sort(),
    i18n: Object.keys(i18nEntry).sort(),
    contracts: Object.keys(contractsEntry).sort(),
  };

  for (const entryPoint of Object.keys(EXPECTED) as (keyof typeof EXPECTED)[]) {
    it(`"${entryPoint}" entry point exports match the recorded surface`, () => {
      expect(actual[entryPoint]).toEqual([...EXPECTED[entryPoint]].sort());
    });
  }
});
