/* Ivory Bastion documentation only. Run the generated copy on the signed-in
 * Thunderstore wiki editor. No mod package, credentials or permissions change.
 */
(async () => {
  "use strict";
  const data = __IVORY_WIKI_DATA__;
  const root = "/api/experimental/package/Kuriyukihime/IvoryBastion/wiki/";
  const publicRoot = "https://thunderstore.io/c/riskofrain2/p/Kuriyukihime/IvoryBastion/wiki/";
  const normalize = text => String(text).replace(/\r\n/g, "\n").trim();
  const hash = async text => Array.from(new Uint8Array(await crypto.subtle.digest(
    "SHA-256", new TextEncoder().encode(normalize(text))
  )), byte => byte.toString(16).padStart(2, "0")).join("");
  if (location.protocol !== "https:" || location.hostname !== "thunderstore.io" ||
      !location.pathname.includes("/Kuriyukihime/IvoryBastion/wiki/")) {
    throw new Error("Run this on Ivory Bastion's signed-in Thunderstore wiki editor.");
  }
  if (window.__ivoryWikiPublishing) throw new Error("A wiki update is already running in this tab.");
  window.__ivoryWikiPublishing = true;
  try {
    // Match the current Thunderstore editor: SessionContext reads sessionid and
    // apiFetch sends Authorization: Session. A csrftoken is not required by that
    // authentication class. Keep the value in this closure, never in a backup,
    // console output, storage, or a request to any other origin.
    const session = /(?:^|;\s*)sessionid=([^;]+)/.exec(document.cookie)?.[1];
    if (!session) throw new Error("Sign into Thunderstore in this Edge tab, reload the wiki editor, then run this again. No pages changed.");
    async function request(path, body, allowMissing = false) {
      const write = body !== undefined;
      const accountPath = "/api/experimental/current-user/";
      if ((write && path !== root) ||
          (path !== root && path !== accountPath && !/^\/api\/experimental\/wiki\/page\/\d+\/$/.test(path))) {
        throw new Error("Unexpected documentation endpoint.");
      }
      const headers = {"Accept": "application/json"};
      if (write) headers["Content-Type"] = "application/json";
      if (write || path === accountPath) headers.Authorization = "Session " + session;
      const response = await fetch(path + (write ? "" : "?verify=" + crypto.randomUUID()), {
        method: write ? "POST" : "GET", credentials: "same-origin", cache: "no-store", redirect: "error",
        headers,
        ...(write ? {body: JSON.stringify(body)} : {})
      });
      if (allowMissing && response.status === 404) return null;
      if (!response.ok) throw new Error("Wiki request failed (HTTP " + response.status +
        "). Make sure you are signed into the account that manages Kuriyukihime. You may safely rerun after fixing the error.");
      return response.json();
    }
    const account = await request("/api/experimental/current-user/");
    if (!account.username) throw new Error("Your Thunderstore login did not verify. Sign in again, reload the wiki editor, and retry. No pages changed.");
    const index = await request(root, undefined, true);
    const existing = [];
    for (const page of index?.pages || []) {
      if (!/^\d+$/.test(String(page.id))) throw new Error("Invalid public wiki page ID.");
      existing.push(await request("/api/experimental/wiki/page/" + page.id + "/"));
    }
    const mapped = new Map();
    for (const page of data.pages) {
      const matches = existing.filter(candidate => candidate.title === page.title);
      if (matches.length > 1) throw new Error("Duplicate public titles need review: " + page.title);
      let match = matches[0];
      if (!match && page.key === data.home.key && data.home.id) {
        match = existing.find(candidate => String(candidate.id) === String(data.home.id));
        if (match && (match.title !== data.home.title ||
            await hash(match.markdown_content) !== data.home.sha256)) {
          throw new Error("The existing Player Guide changed since preparation. Refresh the source snapshot before replacing it. No pages changed.");
        }
      }
      if (match) mapped.set(page.file, match);
    }
    // Preserve the full old wiki before any write. Unknown pages are never deleted.
    const backup = JSON.stringify({savedAt: new Date().toISOString(), wiki: index, pages: existing}, null, 2);
    window.ivoryBastionWikiBackup = backup;
    const backupUrl = URL.createObjectURL(new Blob([backup], {type: "application/json"}));
    const download = document.createElement("a");
    download.href = backupUrl; download.download = "IvoryBastion-wiki-backup.json";
    download.click(); URL.revokeObjectURL(backupUrl);
    for (const page of data.pages) {
      if (mapped.has(page.file)) continue;
      const markdown = page.markdown.replace(/\[([^\]]+)\]\((\d{2}-[a-z0-9-]+\.md)\)/g, "$1");
      const created = await request(root, {title: page.title, markdown_content: markdown});
      if (!/^\d+$/.test(String(created.id)) || !/^\d+-[a-z0-9-]+$/.test(created.slug)) {
        throw new Error("Unexpected page identity. Reload the wiki before retrying.");
      }
      mapped.set(page.file, created);
      console.info("Created " + page.title);
    }
    // Obtain a renamed index's canonical slug before resolving article links.
    for (const page of data.pages) {
      const target = mapped.get(page.file);
      if (target.title === page.title) continue;
      const markdown = page.markdown.replace(/\[([^\]]+)\]\((\d{2}-[a-z0-9-]+\.md)\)/g, "$1");
      const renamed = await request(root, {id: target.id, title: page.title, markdown_content: markdown});
      if (String(renamed.id) !== String(target.id)) throw new Error("A rename changed the page ID.");
      mapped.set(page.file, renamed);
    }
    const receipt = [];
    for (const page of data.pages) {
      const target = mapped.get(page.file);
      let markdown = page.markdown;
      for (const other of data.pages) {
        const slug = mapped.get(other.file).slug;
        if (!/^\d+-[a-z0-9-]+$/.test(slug)) throw new Error("Invalid wiki slug.");
        markdown = markdown.split("](" + other.file + ")").join("](" + publicRoot + slug + "/)");
      }
      if (/\]\(\d{2}-[a-z0-9-]+\.md/.test(markdown)) throw new Error("Unresolved local wiki link.");
      if (target.title !== page.title || normalize(target.markdown_content) !== normalize(markdown)) {
        const saved = await request(root, {id: target.id, title: page.title, markdown_content: markdown});
        if (String(saved.id) !== String(target.id)) throw new Error("An update changed the page ID.");
      }
      const verified = await request("/api/experimental/wiki/page/" + target.id + "/");
      if (verified.title !== page.title || normalize(verified.markdown_content) !== normalize(markdown)) {
        throw new Error("Saved page did not verify: " + page.title);
      }
      receipt.push({title: verified.title, id: verified.id, url: publicRoot + verified.slug + "/"});
    }
    const finalIndex = await request(root);
    if (receipt.some(page => !finalIndex.pages.some(item => String(item.id) === String(page.id)))) {
      throw new Error("A saved page is missing from the wiki index.");
    }
    window.ivoryBastionWikiReceipt = receipt;
    console.table(receipt);
    console.info("Finished: eight separate wiki pages with working navigation. Reload the wiki tab.");
    return receipt;
  } finally {
    delete window.__ivoryWikiPublishing;
  }
})()
