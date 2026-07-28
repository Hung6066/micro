// Marks compiled test output as CommonJS regardless of the package root's
// "type": "module", since Node's native test runner + tsc-emitted CJS need
// extensionless relative requires without touching the shipped ESM source.
const fs = require('node:fs');
const path = require('node:path');

const outDir = path.join(__dirname, '..', 'test-dist');
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, 'package.json'), JSON.stringify({ type: 'commonjs' }) + '\n');
