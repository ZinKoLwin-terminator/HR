#!/usr/bin/env node
// Run with: node .claude/hooks/guard.test.js
// Run from a feature branch: the "allowed" commit and push cases expect a non-protected branch.
const assert = require('node:assert');
const { evaluate } = require('./guard-commands.js');
const { isBlocked } = require('./guard-files.js');

const blocked = [
  'sqlcmd -S prod-db -Q "select 1"',
  'npm test && psql -h db',
  'echo "DROP TABLE Employees"',
  'node run.js "TRUNCATE TABLE Payroll"',
  'dotnet ef database update',
  'npx prisma migrate deploy',
  'ng deploy',
  'npx angular-cli-ghpages',
  'vercel --prod',
  'gh pr merge 12 --squash',
  'gh workflow run ci.yml',
  'gh secret set TOKEN',
  'git push origin main',
  'git push origin HEAD:main',
  'git push origin +main',
  'git push origin gh-pages',
  'git push --force origin feature/x',
  'git push -f',
  'git reset --hard HEAD~1',
  'git clean -fd',
  'cat .env',
  'type .env.production',
  'cat backend/appsettings.Production.json',
  'cat certs/server.pfx',
];

const allowed = [
  'git status',
  'git diff --stat',
  'npm run test:ci',
  'npx playwright test',
  'npm run build',
  'git commit -m "Add leave filter"',
  'git push origin feature/leave-filter',
  'git push -u origin chore/main-menu',
  'git checkout -b fix/payroll',
  'cat .env.example',
  'grep -r "process.env" src',
  'echo mysqlish',
];

const blockedFiles = ['.env', 'D:\\HR\\.env.local', 'api/appsettings.Production.json', 'config/secrets.json', 'tls/site.key', 'a/b.pem'];
const allowedFiles = ['.env.example', 'src/app/app.ts', 'src/environment.ts', 'api/appsettings.Development.json', 'src/keyboard.key.ts'];

let failures = 0;
const check = (name, ok) => {
  if (!ok) {
    failures += 1;
    console.error(`FAIL: ${name}`);
  }
};

for (const c of blocked) check(`should block: ${c}`, evaluate(c, process.cwd()) !== null);
for (const c of allowed) check(`should allow: ${c}`, evaluate(c, process.cwd()) === null);
for (const f of blockedFiles) check(`should block file: ${f}`, isBlocked(f));
for (const f of allowedFiles) check(`should allow file: ${f}`, !isBlocked(f));

assert.strictEqual(failures, 0, `${failures} guard check(s) failed`);
const total = blocked.length + allowed.length + blockedFiles.length + allowedFiles.length;
console.log(`All ${total} guard checks passed.`);
