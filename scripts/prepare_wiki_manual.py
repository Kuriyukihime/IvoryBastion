"""Prepare eight separate wiki pages and a signed-in, same-origin manual publisher.
Reads local snapshots only. Does not contact Thunderstore or publish anything.
"""
from pathlib import Path
import hashlib
import json
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    folder = ROOT / "docs/wiki"
    manifest = json.loads((folder / "wiki-manifest.json").read_text(encoding="utf-8-sig"))
    assert manifest["status"] == "released"
    pages = []
    for item in manifest["pages"]:
        raw = (folder / item["file"]).read_bytes()
        assert hashlib.sha256(raw).hexdigest() == item["sha256"]
        pages.append(dict(key=item["key"], title=item["title"], file=item["file"], markdown=raw.decode("utf-8")))
    assert len(pages) == 8 and len({p["file"] for p in pages}) == 8
    known_files = {p["file"] for p in pages}
    for page in pages:
        for link in re.findall(r"\]\((\d{2}-[a-z0-9-]+\.md)\)", page["markdown"]):
            assert link in known_files
    snapshot = json.loads((ROOT / ".build/wiki-manual-live-6502.json").read_text(encoding="utf-8-sig"))
    old = snapshot["markdown_content"].replace("\r\n", "\n").strip()
    assert snapshot["title"] == "Player Guide" and str(snapshot["id"]) == "6502"
    data = dict(version=manifest["version"], pages=pages, home=dict(
        key=pages[0]["key"], id=snapshot["id"], title=snapshot["title"],
        sha256=hashlib.sha256(old.encode("utf-8")).hexdigest()))
    output = ROOT / "dist" / f"IvoryBastion-Wiki-{manifest['version']}-Separate-Pages"
    output.mkdir(exist_ok=True)
    template = (ROOT / "scripts/wiki_manual_publish.template.js").read_text(encoding="utf-8")
    assert template.count("__IVORY_WIKI_DATA__") == 1
    publisher = template.replace("__IVORY_WIKI_DATA__", json.dumps(data, ensure_ascii=False))
    (output / "Publish-Wiki.txt").write_bytes(publisher.encode("utf-8"))
    # A new filename prevents an already-open editor from retaining the old copy.
    (output / "Publish-Wiki-v2.txt").write_bytes(publisher.encode("utf-8"))
    # The JSON is public documentation, never an authentication token.
    (ROOT / ".build/wiki-manual-payload.json").write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
    for page in pages:
        (output / (Path(page["file"]).stem + ".txt")).write_bytes(page["markdown"].encode("utf-8"))
    instructions = """Ivory Bastion 1.2.2 — eight separate wiki pages

Do not paste all eight articles into one page.

ONE-TIME SIGNED-IN PUBLISHER
1. In Edge, sign into the Thunderstore account that manages Kuriyukihime.
2. Open https://thunderstore.io/c/riskofrain2/p/Kuriyukihime/IvoryBastion/wiki/new/
3. Open Publish-Wiki-v2.txt in Notepad and copy its entire contents.
4. Open Edge Developer Tools (F12), select Console, paste, and run it.
5. Wait for 'Finished: eight separate wiki pages with working navigation.'
6. Reload the mod's Wiki tab.

The publisher reuses page 6502 as 00 Start Here, creates the other seven
articles, and replaces local .md links with Thunderstore's real page URLs.
It downloads a backup before writing. It only edits this package's wiki;
it does not upload a mod, read a publishing token, or change permissions.
It matches the current editor's Session authentication using your existing
sign-in, and verifies the login before changing pages. The session stays in
memory and is sent only to Thunderstore's own account/wiki endpoints. No
session value is printed, downloaded or saved. It does not need a CSRF cookie.
Unknown pages are not deleted. Interrupted runs can be repeated: existing
titles are reused rather than creating duplicate pages.

ALTERNATIVE: NATIVE EDITOR
The 00–07 text files contain each article separately. Create the seven
missing pages with their matching first-line titles. Then replace each
local .md link with the corresponding saved Thunderstore URL. Those file
links are source placeholders until page IDs exist, not working web URLs.

No new mod release is needed. Your public 1.2.2 ZIP remains unchanged.
"""
    (output / "README.txt").write_bytes(instructions.encode("utf-8"))
    bundle = ROOT / "dist" / f"IvoryBastion-Wiki-{manifest['version']}-Separate-Pages.zip"
    with zipfile.ZipFile(bundle, "w", zipfile.ZIP_DEFLATED) as archive:
        filenames = [Path(p['file']).stem + '.txt' for p in pages] + ['README.txt', 'Publish-Wiki-v2.txt']
        for filename in sorted(filenames):
            item = output / filename
            archive.write(item, item.name)
    print(f"Prepared {len(pages)} separate articles and the manual publisher. Nothing published.\n{output}\n{bundle}")


if __name__ == "__main__":
    main()
