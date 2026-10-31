// Offline integration test: the exact generated script runs against a mock API.
// No browser, cookies, credentials or network are used by this check.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const {webcrypto} = require('node:crypto');
const root = path.resolve(__dirname, '..');
const data = JSON.parse(fs.readFileSync(path.join(root, '.build/wiki-manual-payload.json'), 'utf8'));
const script = fs.readFileSync(path.join(root, 'dist', 'IvoryBastion-Wiki-' + data.version + '-Separate-Pages', 'Publish-Wiki-v2.txt'), 'utf8');
const original = JSON.parse(fs.readFileSync(path.join(root, '.build/wiki-manual-live-6502.json'), 'utf8'));
const indexPath = '/api/experimental/package/Kuriyukihime/IvoryBastion/wiki/';
const publicRoot = 'https://thunderstore.io/c/riskofrain2/p/Kuriyukihime/IvoryBastion/wiki/';
const clone = value => JSON.parse(JSON.stringify(value));
const slug = (id, title) => id + '-' + title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
let assertions = 0;
function check(value, message) {assert(value, message); assertions++;}
function store() {
  return new Map([[String(original.id), clone(original)], ['99999', {
    id: '99999', title: 'Unrelated manual page', slug: '99999-unrelated-manual-page', markdown_content: 'Preserve this.'
  }]]);
}
async function run(pages, options = {}) {
  const calls = [], writes = [], downloads = [];
  let created = 0;
  const window = {};
  const context = {
    window, location: {protocol: 'https:', hostname: options.badOrigin ? 'other.example' : 'thunderstore.io', pathname: '/c/riskofrain2/p/Kuriyukihime/IvoryBastion/wiki/new/'},
    document: {cookie: options.noSession ? '' : options.suffixCookie ? 'x_sessionid=offline-test-value' : options.onlyCsrf ? 'csrftoken=offline-test-csrf' : 'sessionid=offline-test-value', createElement: () => ({click() {downloads.push(this.download);}})},
    URL: {createObjectURL: () => 'blob:offline-test', revokeObjectURL() {}},
    Blob, TextEncoder, crypto: webcrypto, Map, console: {info() {}, table() {}},
    fetch: async (raw, init) => {
      calls.push(raw);
      check(raw.startsWith('/api/experimental/'), 'Only same-origin API paths');
      check(init.credentials === 'same-origin', 'Session stays same-origin');
      check(init.redirect === 'error', 'Never forward authentication through a redirect');
      check(init.cache === 'no-store', 'Do not use stale login/wiki responses');
      const url = raw.split('?')[0];
      const write = init.method === 'POST';
      const authenticated = write || url === '/api/experimental/current-user/';
      check(authenticated ? init.headers.Authorization === 'Session offline-test-value' : !init.headers.Authorization,
        'Current editor Session header only on own account and wiki writes');
      check(!init.headers['X-CSRFToken'], 'Modern session authentication does not require CSRF');
      let result;
      if (write) {
        check(url === indexPath, 'Only this package wiki may be written');
        const body = JSON.parse(init.body);
        writes.push(clone(body));
        if (options.denyWrites) return {status: 403, ok: false};
        while (pages.has(String(7000 + created))) created++;
        const id = body.id === undefined ? String(7000 + created++) : String(body.id);
        if (body.id !== undefined) check(pages.has(id), 'Updates preserve existing IDs');
        result = {id, title: body.title, slug: slug(id, body.title), markdown_content: body.markdown_content};
        pages.set(id, result);
        if (options.failWrite === writes.length) throw new Error('Simulated response lost after committed write');
      } else if (url === '/api/experimental/current-user/') {
        if (options.expiredSession) return {status: 401, ok: false};
        result = {username: options.anonymous ? null : 'offline-wiki-owner', teams: ['Kuriyukihime']};
      } else if (url === indexPath) {
        result = {pages: [...pages.values()].map(({id, title, slug}) => ({id, title, slug}))};
      } else {
        const match = url.match(/^\/api\/experimental\/wiki\/page\/(\d+)\/$/);
        check(Boolean(match), 'Only indexed wiki detail reads');
        result = pages.get(match[1]);
      }
      return {ok: Boolean(result), status: result ? 200 : 404, json: async () => clone(result)};
    }
  };
  let error = null, receipt;
  try {receipt = await vm.runInNewContext(script, context, {timeout: 5000});}
  catch (failure) {error = failure;}
  return {calls, writes, downloads, error, receipt, window};
}
(async () => {
  const pages = store();
  const first = await run(pages);
  assert.ifError(first.error);
  check(first.receipt.length === 8, 'Exactly eight separate published articles');
  check(pages.size === 9, 'Seven new articles plus existing home and unrelated page');
  check(pages.get('6502').title === '00 Start Here', 'Reuse original home ID');
  check(pages.get('99999').markdown_content === 'Preserve this.', 'Unknown page untouched');
  check(first.downloads.length === 1 && first.window.ivoryBastionWikiBackup, 'Backup before writes');
  check(!first.window.ivoryBastionWikiBackup.includes('offline-test-value'), 'Session never enters the wiki backup');
  const publicPages = first.receipt.map(item => pages.get(String(item.id)));
  for (const page of publicPages) {
    check(!/\]\(\d{2}-[a-z0-9-]+\.md/.test(page.markdown_content), 'No local file links remain');
    for (const match of page.markdown_content.matchAll(/\]\((https:\/\/thunderstore\.io\/[^)]+)\)/g)) {
      check(first.receipt.some(item => item.url === match[1]), 'Every navigation URL targets a saved article');
    }
    check(page.markdown_content.includes(publicRoot + pages.get('6502').slug + '/'), 'Every article links to canonical renamed home');
  }
  const twice = await run(pages);
  assert.ifError(twice.error);
  check(twice.writes.length === 0, 'Rerun skips all unchanged pages');
  const changed = store(); changed.get('6502').markdown_content += '\nHuman edit.';
  const guarded = await run(changed);
  check(Boolean(guarded.error) && guarded.writes.length === 0, 'Refuse to replace edited original guide');
  const missingSession = await run(store(), {noSession: true});
  check(Boolean(missingSession.error) && missingSession.calls.length === 0, 'No session means no requests');
  const wrongCookie = await run(store(), {suffixCookie: true});
  check(Boolean(wrongCookie.error) && wrongCookie.calls.length === 0, 'Suffix cookie is not a login');
  const legacyOnly = await run(store(), {onlyCsrf: true});
  check(Boolean(legacyOnly.error) && legacyOnly.calls.length === 0, 'A CSRF cookie alone is not a login');
  const expired = await run(store(), {expiredSession: true});
  check(Boolean(expired.error) && expired.writes.length === 0, 'Expired login stops before writes');
  const anonymous = await run(store(), {anonymous: true});
  check(Boolean(anonymous.error) && anonymous.writes.length === 0, 'Anonymous login stops before writes');
  const wrongOrigin = await run(store(), {badOrigin: true});
  check(Boolean(wrongOrigin.error) && wrongOrigin.calls.length === 0, 'Wrong site means no requests');
  const denied = await run(store(), {denyWrites: true});
  check(Boolean(denied.error) && denied.writes.length === 1, 'Permission failure stops immediately');
  const interruptedStore = store();
  const interrupted = await run(interruptedStore, {failWrite: 3});
  check(Boolean(interrupted.error), 'Simulated partial commit interrupts');
  const resumed = await run(interruptedStore);
  assert.ifError(resumed.error);
  check(resumed.receipt.length === 8 && interruptedStore.size === 9, 'Resume reuses partially committed pages without duplicates');
  console.log('Manual wiki publisher: ' + assertions + ' offline assertions passed; eight pages, real links, backups, idempotence, interruption recovery and permission/origin guards.');
})().catch(error => {console.error(error); process.exitCode = 1;});
