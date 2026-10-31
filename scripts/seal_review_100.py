"""Verify and describe the six-file 1.0 release archive."""
from pathlib import Path
import hashlib
import json
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ARCHIVE = ROOT/'dist/IvoryBastion-1.0.0.zip'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest().upper()
expected = {name: ROOT/'package'/name for name in ('manifest.json','README.md','CHANGELOG.md','icon.png','LICENSE')}
expected['BepInEx/plugins/IvoryBastion/IvoryBastion.dll'] = ROOT/'bin/Release/netstandard2.1/IvoryBastion.dll'
with zipfile.ZipFile(ARCHIVE) as package:
    assert len(package.namelist()) == 6 and set(package.namelist()) == set(expected)
    for name, path in expected.items():
        assert package.read(name) == path.read_bytes(), f'Package differs from source: {name}'
    manifest = json.loads(package.read('manifest.json'))
    assert manifest['name'] == 'IvoryBastion' and manifest['version_number'] == '1.0.0'
    for name in ('README.md','CHANGELOG.md','manifest.json'):
        text = package.read(name).decode('utf-8')
        assert '\ufffd' not in text and '\u00e2\u2020' not in text, f'Broken text encoding: {name}'
    notes = package.read('CHANGELOG.md').decode('utf-8')
    assert '**Siege / Defense / Mobile**' in notes and 'prerequisites' in notes
    assert '**two charges**' in notes and '**10s**' in notes and '**15m**' in notes
    assert '**24 spider mines**' in notes and '**90% damage in 8m**' in notes
    with zipfile.ZipFile(ROOT/'dist/IvoryBastion-0.32.0.zip') as original:
        assert package.read('icon.png') == original.read('icon.png'), 'Approved mobile portrait changed'
assert sha(ROOT/'dist/IvoryBastion-0.33.0.zip') == 'D97E7D0438ABD5592F94777B78298B332130F7BFAC3E1A0DF0D1FF4B40CA0DDC'
budget = json.loads((ROOT/'.build/upgrade-damage-budget.json').read_text())
assert budget['Economy']['TotalPoints'] == 46 and budget['Economy']['AllTreeFabrication'] == 5655
assert len(budget['Rows']) == 6 and all(row['Simulated600s']['Dps'] > 0 for row in budget['Rows'])
assert not json.loads((ROOT/'.build/engineering-text-layout.json').read_text())['approximateFontClips']
def log_text(name):
    data=(ROOT/'.build'/name).read_bytes()
    return data.decode('utf-16' if data.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')

release=log_text('release-100-upgrade-tree.log')
protected=log_text('protected-100-upgrade-tree.log')
assert '0 Error(s)' in release and 'IvoryBastion-1.0.0.zip' in release
assert not re.search(r'Unhandled exception|\bFAIL\b|error CS\d',release+'\n'+protected)
assert '139 protected methods' in protected and 'two intentional accepted-cast mastery hooks reviewed' in protected
def passed_count(label):
    match=re.search(r'PASS: ([\d,]+) '+label,release)
    assert match, f'Missing release check: {label}'
    return int(match.group(1).replace(',',''))

approved=(ROOT/'.build/approved-100-gameplay-sha256.txt')
if approved.exists():
    assert sha(expected['BepInEx/plugins/IvoryBastion/IvoryBastion.dll']) == approved.read_text().strip(), 'Approved gameplay DLL changed'
receipt_path=ROOT/'dist/IvoryBastion-1.0.0-upload.json'
receipt=json.loads(receipt_path.read_text(encoding='utf-8-sig')) if receipt_path.exists() else None
published=bool(receipt and receipt['version']=='1.0.0' and receipt['sha256']==sha(ARCHIVE) and receipt['package']=='Kuriyukihime-IvoryBastion')
proof = {
    'version': '1.0.0', 'published': published, 'installed': False, 'game_launched': False,
    'gameplay_dll_sha256': sha(expected['BepInEx/plugins/IvoryBastion/IvoryBastion.dll']),
    'upload_receipt': str(receipt_path.relative_to(ROOT)) if published else None,
    'bytes': ARCHIVE.stat().st_size, 'sha256': sha(ARCHIVE),
    'release_log': '.build/release-100-upgrade-tree.log',
    'protected_log': '.build/protected-100-upgrade-tree.log',
    'damage_report': '.build/upgrade-damage-budget.json',
    'engineering_layout': '.build/engineering-layout.json',
    'upgrade_checks': passed_count('ranked upgrade'),
    'solo_pause_checks': passed_count('solo Engineering pause'),
    'engineering_layout_checks': passed_count('production Engineering'),
    'utility_checks': passed_count('utility, mine lifecycle'),
    'network_checks': passed_count('networking checks'),
    'native_config_checks': passed_count('real BepInEx config'),
    'balance_schema': 4, 'upgrade_schema': 1,
}
(ROOT/'.build/release-100-seal.json').write_text(json.dumps(proof, indent=2)+'\n', encoding='utf-8')
review = ROOT/'dist/IvoryBastion-1.0.0-review'
review.mkdir(parents=True, exist_ok=True)
state='PUBLISHED RELEASE' if published else 'UNPUBLISHED REVIEW'
publication='Published to the existing Thunderstore listing.' if published else 'Not published.'
review.joinpath('REVIEW.txt').write_text(f'''Ivory Bastion 1.0.0 - {state}
ZIP: dist/IvoryBastion-1.0.0.zip
Bytes: {proof['bytes']:,}
SHA256: {proof['sha256']}
{publication} No profile installation or game launch.

ENGINEERING
Three pages: Siege / Defense / Mobile. Defense is shared by both forms.
16 focused nodes, 46 points, prerequisites and distinct max-rank masteries.
Tier I 20/30/40 FAB; II 60/90/120; III 150/225/300. Two-point nodes stop
after the second price. Core node 90; core plus advanced path 765.
Heavenfall stays entirely in Mobile. See packaged README for every effect.
Single-player Engineering pauses the world; purchases apply immediately.
Closing restores game speed. Multiplayer always keeps running.
New-run progression is intended; all lobby players need this same build.

RELIABILITY
Existing purchase requests, retries, deduplication and receipts retained.
Separate recurring upgrade snapshots validate ranks, graph and sequence.
Points and mastery cooldowns remain master-owned across stage/revival.
Two accepted-cast mastery hooks are explicitly audited; original transform,
flight, shield absorption and existing message layouts stay protected.
Reflow consumes transferred Aegis points; it cannot duplicate reserves.
Emergency Rebuild applies after actual drone destruction, not corpse polling.

RETAINED KIT
C transforms. Rush: 2 charges, 10s each, 15m, 32x40% / 4m blasts.
Widow: 24 mines, rings 5/10/15m, detection16/pursuit20m, 60s after landing,
48 per owner. Shared attachment limits5 normal/8 elite/12 boss. Actual owner
missile/laser hits detonate attached marks only. No autonomous/propagation
damage. Base charged burst90%/8m. Stock30; Skyfall10x500%/15m.
Recorded transformation Foley, accepted voices, shield/Aegis HUD, approved
mobile launcher portrait and concise skill descriptions retained.

REVIEW MATERIAL
engineering-*-fresh.png and engineering-*-mastered.png are offline previews
from the real C# page layout with approximate fonts, not game screenshots.
docs/UPGRADE_TREE_100.md records implementation and QA. Native checks and
engine stand-ins do not establish live Unity rendering, host/client latency,
practical balance or performance. The user handles live review testing.
''', encoding='utf-8')
print(f"SEALED {state} 1.0.0: {proof['bytes']:,} bytes; SHA256 {proof['sha256']}")
