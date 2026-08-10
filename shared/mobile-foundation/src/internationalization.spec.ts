import test from "node:test";
import assert from "node:assert/strict";
import { formatHisHopeDateTime, formatHisHopeMoney, getHisHopeRegionalPreferences } from "./internationalization";

test("shared mobile regional preferences use the ISO currency contract", () => {
  const preferences = getHisHopeRegionalPreferences("en-US", "usd");
  assert.equal(preferences.currency, "USD");
  assert.match(formatHisHopeMoney(1234.5, preferences), /1,234\.50/);
  assert.ok(formatHisHopeDateTime("2026-01-01T00:00:00Z", preferences).length > 0);
});
