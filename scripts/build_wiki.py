"""Build an offline Thunderstore wiki draft from production balance/upgrade code.
Does not contact Thunderstore, use credentials, build the mod or publish pages.
"""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile
import argparse

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs/wiki-source"
OUT = ROOT / "docs/wiki"


def number(value):
    return f"{float(value):.2f}".rstrip("0").rstrip(".")


def table(headers, rows):
    def row(values):
        return "| " + " | ".join(str(x).replace("\n", "<br>") for x in values) + " |"
    return "\n".join([row(headers), row(["---"] * len(headers)), *(row(r) for r in rows)])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--status',choices=['draft','released'],default='draft')
    args=parser.parse_args()
    package_version=json.loads((ROOT/"package/manifest.json").read_text(encoding="utf-8-sig"))["version_number"]
    package = ROOT / "dist" / f"IvoryBastion-{package_version}.zip"
    old_hash = hashlib.sha256(package.read_bytes()).hexdigest() if package.exists() else None
    subprocess.run(["dotnet", "run", "--project", str(ROOT / "scripts/wiki_export/WikiExport.csproj"), "-c", "Release", "--", str(ROOT)], cwd=ROOT, check=True)
    data = json.loads((ROOT / ".build/wiki-data.json").read_text())
    defaults = data["Defaults"]
    nodes = data["Nodes"]
    version = data["Version"]
    cone = re.search(r"ConeDegrees\s*=\s*([\d.]+)f", (ROOT / "src/BastionNeedleRules.cs").read_text()).group(1)
    values = {k: number(v) for k, v in defaults.items()}
    values.update(Version=version, NeedleCone=number(cone), MineEffective=number(defaults["MineDamage"] * (1 + defaults["MineChainBonus"] / 100)),
        CombinedBaseDR=number(100 * (1 - (1-defaults["SiegeShieldDR"]/100)*(1-defaults["AegisDR"]/100))),
        HardenedDR=number(defaults["MobileShieldDR"]+defaults["HardeningBonus"]),
        CombinedHardenedDR=number(100*(1-(1-(defaults["SiegeShieldDR"]+defaults["HardeningBonus"])/100)*(1-defaults["HardeningDR"]/100))),
        NeedleDps=number(defaults["BaseDamage"]*defaults["NeedleDamage"]/100*defaults["NeedleRate"]))
    for name,key in (("Missile","MissileRadius"),("Heavy","HeavyRadius"),("Laser","LaserRadius"),("Wingburst","WingburstRadius"),("Mine","MineBlastRadius"),("Skyfall","SkyfallRadius")):
        values[name+"Diameter"]=number(defaults[key]*2)
    for key in ("CostOne","CostTwo","CostThree"):
        values[key+"RankTwo"]=number(defaults[key]*1.5)
        values[key+"RankThree"]=number(defaults[key]*2)
    values.update(GunnerBulletDamage=number(defaults["GunnerDamage"]/100*defaults["BaseDamage"]),
        GunnerDps=number(defaults["GunnerDamage"]/100*defaults["BaseDamage"]/defaults["GunnerInterval"]),
        CombinedDamageTaken=number((1-defaults["SiegeShieldDR"]/100)*(1-defaults["AegisDR"]/100)*100),
        TwentyFourShieldCost=number(24*(1-defaults["MobileShieldDR"]/100)))
    pages = sorted(SOURCE.glob("[0-9][0-9]-*.md"))
    titles = {p.name: p.read_text(encoding="utf-8").splitlines()[0].removeprefix("# ") for p in pages}
    navigation = "**Wiki pages:** " + " · ".join(f"[{title}]({name})" for name, title in titles.items())

    def upgrade_value(node, rank):
        v = rank["Values"]
        f = lambda k: number(v[k])
        return {
            "MissileRacks": lambda: f("Capacity")+" missiles",
            "Autoloader": lambda: f("Reload")+" missiles/s; heavy every "+f("HeavyEvery")+" shots",
            "IonOptics": lambda: f("LaserPercent")+"% Ion Lance damage",
            "IonCapacitors": lambda: f("LaserCooldown")+"s Ion Lance recharge",
            "WidowFabricators": lambda: f("MineCount")+" spiders; "+f("MineField")+"m outer ring",
            "ChargedNetwork": lambda: f("MinePercent")+"% charged detonation; "+f("MineRadius")+"m blast radius",
            "Hangar": lambda: f("Slots")+" persistent drone slots",
            "Blueprints": lambda: ["Gunner", "Gunner + Interceptor", "Gunner + Interceptor + Repairer"][rank["Rank"]-1],
            "ShieldMatrix": lambda: "+"+f("ShieldCapacityBonus")+"% shared shield capacity",
            "Hardening": lambda: "+"+f("ShieldCapacityBonus")+"% total shared capacity; "+f("ShieldDR")+"% Seraph DR / "+f("AegisDR")+"% Aegis DR",
            "AegisCircuit": lambda: f("BarrierPercent")+"% max-HP Aegis barrier",
            "Renewal": lambda: f("HealingPercent")+"% max-HP healing across Aegis",
            "Airframe": lambda: "+"+f("FlightBonus")+"% flight speed",
            "ThrusterTanks": lambda: f("Fuel")+"s base boost reserve",
            "HeavenfallRigs": lambda: f("RushDistance")+"m dash; "+f("RushShots")+" missiles; "+f("RushCooldown")+"s recharge",
            "BlueShiftDriver": lambda: "+"+f("NeedleBonus")+"% Needlefire damage; "+f("ShiftDuration")+"s Blue Shift",
        }[node["Id"]]()

    sections = []
    for group in ("Siege", "Defense", "Mobile"):
        sections.append("## " + group)
        for node in [n for n in nodes if n["Group"] == group]:
            parent = next((n for n in nodes if n["Id"] == node["Parent"]), None)
            requires = "No prerequisite" if parent is None else parent["Name"].title()+" rank "+str(node["RequiredRank"])
            sections.append("### "+node["Name"].title()+"\n\n"+requires+". Tier "+str(node["Tier"])+"; "+str(node["Maximum"])+" ranks.\n\n"+
                table(["Rank", "Cost (FAB)", "Result after purchase"], [(r["Rank"],r["Cost"],upgrade_value(node,r)) for r in node["Ranks"]])+"\n\n**Max-rank mastery — "+node["Mastery"]+"**")
    values["EngineeringTables"] = "\n\n".join(sections)
    proc_keys = [("Needlefire", "NeedleProc"),("Normal missile","MissileProc"),("Heavy missile","HeavyProc"),("Ion Lance","LaserProc"),("Skyfall warhead","SkyfallProc"),("Heavenfall micro-missile","WingburstProc"),("Widow explosion","MineProc"),("Drone bullet","DroneProc"),("Blue Shift ricochet","RicochetProc")]
    values["ProcTable"] = table(["Attack", "Proc coefficient", "Example: 10% base item chance"], [(name,number(defaults[key]),number(defaults[key]*10)+"%") for name,key in proc_keys])
    values["DroneTable"] = table(["Drone", "Role", "Health × yours", "Armor", "Main effect"], [
        ("Bulwark Sentinel","Control / distraction",number(defaults["BulwarkHealth"]),number(defaults["BulwarkArmor"]),number(defaults["BulwarkDamage"])+"% shot / "+number(defaults["BulwarkInterval"])+"s; "+number(defaults["BulwarkRange"])+"m gun range"),
        ("Gunner","Sustained damage",number(defaults["GunnerHealth"]),number(defaults["GunnerArmor"]),number(defaults["GunnerDamage"])+"% shot / "+number(defaults["GunnerInterval"])+"s; "+number(defaults["GunnerRange"])+"m range"),
        ("Interceptor","Projectile defense",number(defaults["InterceptorHealth"]),number(defaults["InterceptorArmor"]),"Deletes one eligible hostile projectile / "+number(defaults["InterceptorCooldown"])+"s within "+number(defaults["InterceptorRange"])+"m"),
        ("Repairer","Health sustain",number(defaults["RepairHealth"]),number(defaults["RepairArmor"]),number(defaults["RepairHealing"])+"% ally max HP/s within "+number(defaults["RepairRange"])+"m")])
    values["TotalPoints"] = str(sum(n["Maximum"] for n in nodes))
    values["FullTreeCost"] = str(sum(r["Cost"] for n in nodes for r in n["Ranks"]))
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = {"namespace":"Kuriyukihime","package":"IvoryBastion","version":version,"status":"released" if args.status=='released' else "unpublished draft","pages":[]}
    for source in pages:
        content = source.read_text(encoding="utf-8")
        def substitute(match):
            key = match.group(1)
            if key not in values: raise ValueError(f"Unknown wiki placeholder {key} in {source.name}")
            return values[key]
        content = re.sub(r"\{\{(\w+)\}\}", substitute, content)
        lines = content.splitlines()
        banner = f"> **Unreleased {version} review draft.** These are default settings for the review build, not the current public release. Host configuration and items can change them.\n\n"
        if args.status=='released':
            banner=f"> **Ivory Bastion {version} — default settings.** Items, Engineering ranks and the host's balance configuration can change these values.\n\n"
        rendered = lines[0]+"\n\n"+banner+"\n".join(lines[1:]).lstrip()+"\n\n---\n\n"+navigation+"\n"
        (OUT/source.name).write_bytes(rendered.encode("utf-8"))
        manifest["pages"].append({"key":source.stem,"title":titles[source.name],"file":source.name,"sha256":hashlib.sha256(rendered.encode()).hexdigest()})
    (OUT/"wiki-manifest.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
    for page in pages:
        text = (OUT/page.name).read_text(encoding="utf-8")
        assert "{{" not in text and len(text)<100_000
        for link in re.findall(r"\]\(([^)#]+)(?:#[^)]*)?\)",text):
            if "://" not in link: assert (OUT/link).is_file(), f"Broken wiki link {link} in {page.name}"
    bundle = ROOT/"dist"/f"IvoryBastion-Wiki-{'Draft-' if args.status=='draft' else ''}{version}.zip"
    with zipfile.ZipFile(bundle,"w",zipfile.ZIP_DEFLATED) as archive:
        for page in pages: archive.write(OUT/page.name,page.name)
        archive.write(OUT/"wiki-manifest.json","wiki-manifest.json")
        for filename in ("MAINTAINERS.md","RESEARCH.md"):
            if (OUT/filename).is_file(): archive.write(OUT/filename,filename)
    if old_hash is not None: assert old_hash==hashlib.sha256(package.read_bytes()).hexdigest(),"Wiki generation must not modify the mod ZIP"
    print(f"Built and validated {len(pages)} linked {args.status} wiki pages, {len(nodes)} real upgrade paths / {values['TotalPoints']} points / {values['FullTreeCost']} FAB. Mod ZIP unchanged.\n{bundle}")


if __name__ == "__main__":
    main()
