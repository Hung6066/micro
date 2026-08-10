import { execFileSync } from 'node:child_process';

// Feature applications are migrated incrementally. The foundation owns the
// token contract, so this gate checks newly added foundation component lines.
const diff = execFileSync('git', ['diff', '--unified=0', '--', 'shared/frontend-foundation/src'], { encoding: 'utf8' });
const violations = [];
let file = '';
for (const line of diff.split(/\r?\n/)) {
  const fileMatch = line.match(/^\+\+\+ b\/(.+)$/);
  if (fileMatch) { file = fileMatch[1]; continue; }
  if (!line.startsWith('+') || line.startsWith('+++') || !file) continue;
  if (file.includes('/styles/') || file.includes('/presets/') || file.includes('.stories.') || line.includes('token-lint-ignore')) continue;
  const checks = [
    /#[0-9a-f]{3,8}\b/i,
    /\b(?:rgb|rgba|hsl|hsla)\(/i,
    /font-(?:family|size|weight)\s*:\s*(?!var\()/i,
  ];
  if (checks.some(check => check.test(line))) violations.push(`${file}: ${line.slice(1).trim()}`);
}
if (violations.length) {
  console.error('Design token violations found in changed lines:');
  violations.forEach(violation => console.error(`- ${violation}`));
  process.exit(1);
}
console.log('Design token validation passed.');
