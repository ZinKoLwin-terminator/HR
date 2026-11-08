#!/usr/bin/env node
// PreToolUse guard for shell commands. Exit code 2 blocks the call and shows stderr to Claude.
// It fails closed: input it cannot parse is blocked.
// This is a safety net against mistakes, not a security boundary. A determined script can get
// around pattern matching, so production credentials must stay out of this environment.
const { execFileSync } = require('node:child_process');

const PROTECTED_BRANCHES = /^(main|master|prod|production|release)$/;
const PROTECTED_REF = /(^|[\s:+/])(main|master|prod|production|gh-pages)(\s|$)/;

// Start of a command: beginning of input or after ; & | ( or a newline.
const cmd = (names) => new RegExp(`(^|[;&|(\\n]\\s*)(sudo\\s+)?(npx(\\.cmd)?\\s+)?(${names})\\b`, 'i');

const RULES = [
  [cmd('sqlcmd|psql|mysql|mariadb|sqlite3|mongosh|mongo|redis-cli|bcp'), 'Database clients are blocked. A human must run database commands.'],
  [/\b(drop\s+(table|database|schema)|truncate\s+table|delete\s+from\s+\w+\s*(;|$|["']))/i, 'Destructive SQL is blocked. A human must review and run it.'],
  [/\bdotnet\s+ef\s+database\s+(update|drop)\b/i, 'Applying migrations is blocked. A human must run them.'],
  [/\b(prisma\s+(migrate\s+(deploy|reset)|db\s+push)|flyway\s+\w+|liquibase\s+\w+)\b/i, 'Applying migrations is blocked. A human must run them.'],
  [cmd('ng\\s+deploy|angular-cli-ghpages|ngh|vercel|netlify'), 'Deployment is blocked. A human must deploy.'],
  [/\bgh\s+(pr\s+merge|workflow\s+run|run\s+rerun|secret|variable)\b/i, 'Merging to main deploys the site, and workflow and secret changes need a human.'],
  [/\b(az\s+webapp\s+deploy|kubectl\s+(apply|delete)|helm\s+(install|upgrade)|terraform\s+(apply|destroy))\b/i, 'Infrastructure changes are blocked. A human must run them.'],
  [/\bgit\b[^;&|\n]*\bpush\b[^;&|\n]*(\s--force(\s|$|=)|\s-f(\s|$))/i, 'Force push is blocked.'],
  [/\bgit\b[^;&|\n]*\b(reset\s+--hard|clean\s+-\w*f)/i, 'This can destroy uncommitted work. Ask the user to run it.'],
  [/(^|[\s'"/\\])\.env(\.(?!example\b|sample\b|template\b)\w+)?(?=$|[\s'";|&])/i, 'Environment files may hold secrets and cannot be used from the shell.'],
  [/appsettings\.Production|secrets\.json|\.(pem|pfx|p12)(?=$|[\s'";|&])/i, 'Secret files cannot be used from the shell.'],
];

function currentBranch(cwd) {
  try {
    return execFileSync('git', ['rev-parse', '--abbrev-ref', 'HEAD'], { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  } catch {
    return '';
  }
}

function checkGit(command, cwd) {
  let onProtected;
  const isProtected = () => (onProtected ??= PROTECTED_BRANCHES.test(currentBranch(cwd)));

  for (const part of command.split(/;|&&|\|\||\||\n/)) {
    if (!/\bgit\b/.test(part)) continue;

    if (/\bgit\b.*\b(commit|merge|rebase|cherry-pick|revert|am)\b/.test(part) && isProtected()) {
      return 'The current branch is protected. Create a feature or fix branch first, as a separate command.';
    }
    const push = part.match(/\bgit\b.*?\bpush\b(.*)$/);
    if (push) {
      const args = push[1].trim();
      if (PROTECTED_REF.test(` ${args} `)) return 'Pushing to a protected branch is blocked. Pushing to main deploys the site.';
      // No refspec, or plain HEAD, pushes the current branch.
      const refspec = args.replace(/(^|\s)-\S+/g, ' ').trim().split(/\s+/)[1];
      if ((!refspec || refspec === 'HEAD') && isProtected()) {
        return 'Pushing from a protected branch is blocked. Pushing to main deploys the site.';
      }
    }
  }
  return null;
}

function evaluate(command, cwd) {
  for (const [pattern, reason] of RULES) {
    if (pattern.test(command)) return reason;
  }
  return checkGit(command, cwd);
}

if (require.main === module) {
  let raw = '';
  process.stdin.on('data', (chunk) => (raw += chunk));
  process.stdin.on('end', () => {
    let reason;
    try {
      const input = JSON.parse(raw);
      reason = evaluate(String(input.tool_input?.command ?? ''), input.cwd || process.cwd());
    } catch {
      reason = 'The guard could not read the command, so it was blocked.';
    }
    if (reason) {
      process.stderr.write(`Blocked by .claude/hooks/guard-commands.js: ${reason}\n`);
      process.exit(2);
    }
  });
}

module.exports = { evaluate };
