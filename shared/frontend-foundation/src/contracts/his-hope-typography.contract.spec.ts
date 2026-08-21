import {
  DEFAULT_HIS_HOPE_TYPOGRAPHY_SCALE,
  HIS_HOPE_TYPOGRAPHY_TOKENS,
  hisHopeTypographyCssVariables,
} from "./his-hope-typography.contract";

describe("his-hope typography contract", () => {
  it("maps scale values to css custom properties", () => {
    expect(hisHopeTypographyCssVariables(DEFAULT_HIS_HOPE_TYPOGRAPHY_SCALE)).toEqual({
      [HIS_HOPE_TYPOGRAPHY_TOKENS.body]: "14px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.title]: "24px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.input]: "16px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.nav]: "11px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.overline]: "10px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.caption]: "12px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.label]: "13px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.bodyEmphasis]: "15px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.toolbar]: "16px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.subhead]: "17px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.iconSm]: "18px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.section]: "20px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.iconMd]: "20px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.headline]: "22px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.iconLg]: "24px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.display]: "28px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.displayMd]: "32px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.displayLg]: "40px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.displayXl]: "48px",
      [HIS_HOPE_TYPOGRAPHY_TOKENS.micro]: "9px",
    });
  });
});
