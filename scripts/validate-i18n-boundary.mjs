import { spawnSync } from 'node:child_process';

const diffResult = spawnSync('git', ['diff', '--unified=0', '--', 'mobile-app/src/app', 'admin-app/src/app'], { encoding: 'utf8' });
const diff = diffResult.stdout ?? '';
let file = '';
const violations = [];

for (const line of diff.split(/\r?\n/)) {
  if (line.startsWith('+++ b/')) { file = line.slice(6); continue; }
  if (!line.startsWith('+') || line.startsWith('+++')) continue;
  const added = line.slice(1);
  if (/>\s*[A-Z][^<{]*</.test(added) || /\b(?:aria-label|alt|label|message|placeholder|subtitle|title)=(["'])[A-Z]/.test(added) || /\blabel:\s*['"][A-Z][^'"]*['"]/.test(added)) {
    violations.push(`${file}: ${added.trim()}`);
  }
}

if (violations.length) {
  console.error('New hardcoded UI text detected. Use @his-hope/frontend-foundation i18n keys instead:');
  for (const violation of violations) console.error(`- ${violation}`);
  process.exit(1);
}
console.log('i18n boundary validation passed.');
