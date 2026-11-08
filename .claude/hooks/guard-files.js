#!/usr/bin/env node
// PreToolUse guard for file tools. Exit code 2 blocks the call and shows stderr to Claude.
// It backs up the deny rules in settings.json and fails closed on input it cannot parse.
const SECRET_FILE =
  /(^|[\\/])(\.env(\.(?!example$|sample$|template$)[^\\/]+)?|appsettings\.Production[^\\/]*\.json|secrets\.json|[^\\/]+\.(pem|pfx|p12|key))$/i;

const isBlocked = (filePath) => SECRET_FILE.test(filePath);

if (require.main === module) {
  let raw = '';
  process.stdin.on('data', (chunk) => (raw += chunk));
  process.stdin.on('end', () => {
    let reason;
    try {
      const toolInput = JSON.parse(raw).tool_input ?? {};
      const filePath = String(toolInput.file_path ?? toolInput.notebook_path ?? '');
      if (isBlocked(filePath)) reason = `${filePath} may hold secrets.`;
    } catch {
      reason = 'The guard could not read the file path, so the call was blocked.';
    }
    if (reason) {
      process.stderr.write(`Blocked by .claude/hooks/guard-files.js: ${reason}\n`);
      process.exit(2);
    }
  });
}

module.exports = { isBlocked };
