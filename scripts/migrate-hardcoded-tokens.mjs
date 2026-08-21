/**
 * Migrate hardcoded px in inline styles / scss to semantic CSS tokens.
 * Run: node scripts/migrate-hardcoded-tokens.mjs
 */
import { readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { extname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const roots = [
  "shared/frontend-foundation/ui/src",
  "shared/frontend-foundation/forms/src",
  "shared/frontend-foundation/domain/src",
  "shared/frontend-foundation/i18n/src",
  "shared/frontend-foundation/src/styles",
  "shared/frontend-foundation/src/theme",
  "admin-app/src",
  "mobile-app/src",
  "dashboard-app/src",
];
const skipDirs = new Set(["node_modules", "dist", ".angular"]);
const skipFiles = new Set(["_tokens.scss", "_presets.scss", "his-hope-foundation.stories.ts"]);

const FONT_SIZE = {
  9: "--font-size-micro",
  10: "--font-size-overline",
  11: "--font-size-nav",
  12: "--font-size-caption",
  13: "--font-size-label",
  14: "--font-size-body",
  15: "--font-size-body-emphasis",
  16: "--font-size-toolbar",
  17: "--font-size-subhead",
  18: "--font-size-icon-sm",
  20: "--font-size-section",
  22: "--font-size-headline",
  24: "--font-size-title",
  28: "--font-size-display",
  32: "--font-size-display-md",
  40: "--font-size-display-lg",
  48: "--font-size-display-xl",
};

const SPACE = {
  0: "--space-none",
  2: "--space-hairline",
  3: "--space-xxs",
  4: "--space-2xs",
  5: "--space-snug",
  6: "--space-xs",
  7: "--space-compact",
  8: "--space-sm",
  10: "--space-inset",
  12: "--space-md",
  13: "--space-list-item",
  14: "--size-timeline-dot",
  16: "--space-lg",
  20: "--space-xl",
  22: "--size-config-nav-indicator",
  24: "--space-2xl",
  28: "--page-padding-block",
  30: "--size-nav-icon-track",
  32: "--space-3xl",
  36: "--control-height-compact",
  40: "--button-height",
  44: "--touch-target",
  48: "--space-4xl",
  56: "--mobile-toolbar-height",
  64: "--shell-header-height",
  72: "--dialog-footer-min-height",
  78: "--workspace-header-min-height",
};

const RADIUS = {
  2: "--radius-micro",
  4: "--radius-input",
  6: "--radius-button",
  8: "--radius-card",
  9: "--radius-brand-mark",
  10: "--radius-chip",
  12: "--radius-control",
  14: "--radius-feature",
  16: "--radius-panel",
  20: "--radius-sheet",
  22: "--radius-glass-nav",
  24: "--radius-mobile-sheet",
  999: "--radius-pill",
};

const SIZE_PROPS = new Set([
  "width",
  "height",
  "min-width",
  "min-height",
  "max-width",
  "max-height",
  "top",
  "right",
  "bottom",
  "left",
  "inset",
]);

const SPACING_PROPS = new Set([
  "padding",
  "padding-top",
  "padding-right",
  "padding-bottom",
  "padding-left",
  "padding-inline",
  "padding-block",
  "margin",
  "margin-top",
  "margin-right",
  "margin-bottom",
  "margin-left",
  "margin-inline",
  "margin-block",
  "gap",
  "row-gap",
  "column-gap",
  "inset-block",
  "inset-inline",
]);

function walk(dir, files = []) {
  for (const name of readdirSync(dir)) {
    if (skipDirs.has(name)) continue;
    const p = join(dir, name);
    const st = statSync(p);
    if (st.isDirectory()) walk(p, files);
    else if ([".ts", ".scss"].includes(extname(name)) && !skipFiles.has(name)) files.push(p);
  }
  return files;
}

function replacePxValue(px, token) {
  return `var(${token})`;
}

function migrateCssChunk(chunk) {
  let out = chunk;

  // font-size
  out = out.replace(/font-size:\s*(\d+)px/g, (_, n) => {
    const t = FONT_SIZE[Number(n)];
    return t ? `font-size: var(${t})` : `font-size: ${n}px`;
  });

  // border-radius (not 50%)
  out = out.replace(/border-radius:\s*(\d+)px/g, (_, n) => {
    const t = RADIUS[Number(n)];
    return t ? `border-radius: var(${t})` : `border-radius: ${n}px`;
  });

  // line-height numeric px → keep unless matches common
  out = out.replace(/line-height:\s*(\d+)px/g, (_, n) => {
    const map = { 20: "--line-height-compact", 24: "--line-height-relaxed", 32: "--line-height-title" };
    const t = map[Number(n)];
    return t ? `line-height: var(${t})` : `line-height: ${n}px`;
  });

  // property: value with one or more px tokens
  out = out.replace(
    /([a-z-]+):\s*([^;{]+);/gi,
    (full, prop, valuePart) => {
      const propName = prop.toLowerCase();
      if (propName.startsWith("@") || valuePart.includes("var(")) return full;
      if (propName === "border" || propName.endsWith("-width")) return full;

      const isSpacing = SPACING_PROPS.has(propName);
      const isRadius = propName === "border-radius";
      const isFont = propName === "font-size";
      const isOutline =
        propName === "outline" || propName === "outline-offset" || propName === "outline-width";

      if (isFont || isRadius) return full; // handled above

      if (isOutline) {
        let v = valuePart;
        v = v.replace(/\b2px\b/g, "var(--focus-ring-width)");
        v = v.replace(/\b3px\b/g, "var(--focus-ring-width-strong)");
        if (propName === "outline-offset") {
          v = v.replace(/\b2px\b/g, "var(--focus-ring-offset)");
          v = v.replace(/\b1px\b/g, "var(--focus-ring-offset-tight)");
        }
        return v === valuePart ? full : `${prop}: ${v};`;
      }

      if (!isSpacing && !SIZE_PROPS.has(propName)) return full;

      // Skip layout breakpoints / large layout widths in size props
      if (SIZE_PROPS.has(propName) && !isSpacing) {
        const nums = [...valuePart.matchAll(/\b(\d+)px\b/g)].map((m) => Number(m[1]));
        if (nums.some((n) => n >= 96 && ![96, 160].includes(n))) return full;
        // allow common control sizes
      }

      let v = valuePart;
      const replaceInValue = (n, token) => {
        v = v.replace(new RegExp(`\\b${n}px\\b`, "g"), replacePxValue(n, token));
      };

      if (isSpacing || propName === "gap") {
        for (const [n, token] of Object.entries(SPACE).sort((a, b) => b[0] - a[0])) {
          replaceInValue(Number(n), token);
        }
      } else if (SIZE_PROPS.has(propName)) {
        const sizeMap = {
          8: "--size-status-dot",
          14: "--size-timeline-dot",
          16: "--size-timeline-rail",
          22: "--size-config-nav-indicator",
          30: "--size-nav-icon-track",
          32: "--space-3xl",
          36: "--control-height-compact",
          40: "--button-height",
          44: "--touch-target",
          48: "--space-4xl",
          56: "--mobile-toolbar-height",
          64: "--shell-header-height",
          96: "--size-empty-state-icon",
          160: "--size-upload-dropzone-min",
          220: "--max-width-popover-compact",
          240: "--max-width-mobile-nav-label",
          420: "--max-width-auth-card",
          720: "--max-width-mobile-content",
        };
        for (const [n, token] of Object.entries(sizeMap).sort((a, b) => b[0] - a[0])) {
          replaceInValue(Number(n), token);
        }
      }

      return v === valuePart ? full : `${prop}: ${v};`;
    },
  );

  // calc(100% - Npx) patterns
  out = out.replace(/calc\(([^)]+)\)/g, (full, inner) => {
    let v = inner;
    for (const [n, token] of Object.entries(SPACE).sort((a, b) => b[0] - a[0])) {
      v = v.replace(new RegExp(`\\b${n}px\\b`, "g"), `var(${token})`);
    }
    return v === inner ? full : `calc(${v})`;
  });

  // blur()
  out = out.replace(/blur\((\d+)px\)/g, (_, n) => {
    const map = {
      4: "--blur-subtle",
      12: "--blur-toolbar",
      16: "--blur-shell",
      18: "--blur-glass",
      24: "--blur-overlay",
    };
    const token = map[Number(n)];
    return token ? `blur(var(${token}))` : `blur(${n}px)`;
  });

  // common box-shadow presets
  const shadowReplacements = [
    ["box-shadow: 0 24px 64px rgba(15, 35, 25, 0.24)", "box-shadow: var(--shadow-dialog-elevated)"],
    ["box-shadow: 0 0 0 4px #e3f2e8", "box-shadow: var(--shadow-focus-success)"],
    ["box-shadow: inset 3px 0 0 var(--color-primary)", "box-shadow: var(--shadow-nav-inset-active)"],
    ["box-shadow: -12px 0 32px rgba(15,35,25,.16)", "box-shadow: var(--shadow-drawer)"],
    ["box-shadow: -12px 0 32px rgba(15, 35, 25, .16)", "box-shadow: var(--shadow-drawer)"],
    ["box-shadow: 0 6px 18px rgba(22, 119, 200, 0.06)", "box-shadow: var(--shadow-mobile-dashboard)"],
    ["box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15)", "box-shadow: var(--shadow-dropdown)"],
    ["box-shadow: 0 0 0 1px var(--color-primary)", "box-shadow: var(--shadow-dot-ring)"],
    ["box-shadow: 0 -10px 30px var(--overlay-backdrop-soft)", "box-shadow: var(--shadow-sheet-up)"],
    ["transform: translateY(-2px)", "transform: translateY(calc(var(--space-hairline) * -1))"],
    ["outline: 3px solid", "outline: var(--focus-ring-width-strong) solid"],
  ];
  for (const [from, to] of shadowReplacements) {
    out = out.split(from).join(to);
  }

  return out;
}

function migrateFile(content, filePath) {
  if (extname(filePath) === ".scss") {
    return migrateCssChunk(content);
  }
  // Process backtick style blocks in .ts
  return content.replace(/(`(?:\\.|[^\\`])*`)/g, (block) => {
    if (!block.includes("px")) return block;
    if (!/\b(padding|margin|gap|font-size|border-radius|width|height|line-height|top|right|bottom|left|inset|calc|box-shadow|outline|transform)\b/.test(block)) {
      return block;
    }
    const inner = block.slice(1, -1);
    const migrated = migrateCssChunk(inner);
    return migrated === inner ? block : `\`${migrated}\``;
  });
}

const repoRoot = fileURLToPath(new URL("..", import.meta.url));
let changed = 0;
for (const root of roots) {
  const abs = join(repoRoot, root);
  for (const file of walk(abs)) {
    const original = readFileSync(file, "utf8");
    const next = migrateFile(original, file);
    if (next !== original) {
      writeFileSync(file, next, "utf8");
      changed++;
      console.log(relative(repoRoot, file));
    }
  }
}
console.log(`\nUpdated ${changed} files.`);
