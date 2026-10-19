"""shaders-shop (Unity Stable / Unity Unicorn): build the shop's catalog from its sources.

Claude, 2026-10-18. Reads the shelf below (what we stock, why, and where it comes from), then asks each source for
the exact files: VPM listings (VRChat Creator Companion's package format) for packages and their dependencies, GitHub
releases for .unitypackage files, raw GitHub for loose shader files. It writes the catalog the Unity window reads:
Assets/ShadersShop/shaders-catalog.json (schema unity-unicorn.shaders-catalog/v1).

Usage: python refresh-catalog-20261018.py [--out <catalog.json>]
Network: read-only GETs and HEADs to github.com, the listed VPM URLs and raw.githubusercontent.com. Downloads nothing
else; the shop downloads a shader only when someone presses Install.
"""
import argparse, json, re, sys, urllib.request
from datetime import datetime, timezone

UA = {"User-Agent": "unity-stable-shaders-shop"}

# The shelf. "why" is what the research found; licences were read from each repository's LICENSE on 2026-10-18.
SHELF = [
    dict(id="poiyomi-toon", name="Poiyomi Toon", author="Poiyomi", license="MIT", homepage="https://github.com/poiyomi/PoiyomiToonShader",
         kind="toon / effects", good_for=["avatars", "props"], platforms=["pc"],
         features=["toon", "glitter", "sparkle", "iridescence", "rim light", "matcap", "emission", "dissolve"],
         why="The most-used VRChat toon shader: glitter, sparkle, iridescence, rim light and matcap in one material. Lock materials before upload. Heavy for Android.",
         install="vpm", vpm_repo="https://poiyomi.github.io/vpm/index.json", package="com.poiyomi.toon", pin="10.0.25",
         detect_paths=["Packages/com.poiyomi.toon", "Assets/_PoiyomiShaders"], shader_prefixes=[".poiyomi/", "Poiyomi"]),
    dict(id="liltoon", name="lilToon", author="lilxyzw", license="MIT", homepage="https://github.com/lilxyzw/lilToon",
         kind="toon", good_for=["avatars", "props"], platforms=["pc", "android"],
         features=["toon", "rim light", "matcap x2", "glitter", "emission", "outline", "fur"],
         why="Light, polished toon shader with rim light, two matcaps, glitter and emission; has lite variants for Android.",
         install="zip", package="jp.lilxyzw.liltoon", github="lilxyzw/lilToon", asset_pattern=r"^jp\.lilxyzw\.liltoon-.*\.zip$",
         detect_paths=["Packages/jp.lilxyzw.liltoon", "Assets/lilToon"], shader_prefixes=["lilToon", "Hidden/lilToon", "_lil/"]),
    dict(id="orels-shaders", name="ORL Shaders", author="orels1", license="MIT", homepage="https://github.com/orels1/orels-Unity-Shaders",
         kind="world PBR / toon / VFX", good_for=["worlds", "props"], platforms=["pc"],
         features=["standard PBR", "toon", "lightmaps", "Bakery", "parallax", "VFX", "glass"],
         why="Made for VRChat worlds: a PBR Standard with lightmap and Bakery support, a toon shader and VFX shaders. MIT (repo LICENSE).",
         install="vpm", vpm_repo="https://orels1.github.io/orels-Unity-Shaders/index.json", package="sh.orels.shaders", pin="7.3.1",
         detect_paths=["Packages/sh.orels.shaders"], shader_prefixes=["orels1/"]),
    dict(id="mochie-shaders", name="Mochie's Unity Shaders (free)", author="Mochie", license="MIT", homepage="https://github.com/MochiesCode/Mochies-Unity-Shaders",
         kind="world standard / glass / water / particles", good_for=["worlds", "props"], platforms=["pc"],
         features=["standard replacement", "glass", "water", "particles", "toon", "screen effects"],
         why="World favourite: a Standard replacement, a real glass shader, water and soft particles. Forward rendering, built-in pipeline.",
         install="unitypackage", github="MochiesCode/Mochies-Unity-Shaders", asset_pattern=r"Free\.unitypackage$",
         detect_paths=["Assets/Mochie"], shader_prefixes=["Mochie/"]),
    dict(id="unlitwf", name="UnlitWF", author="whiteflare", license="Zlib", homepage="https://github.com/whiteflare/Unlit_WF_ShaderSuite",
         kind="toon / unlit", good_for=["worlds", "avatars", "props"], platforms=["pc", "android"],
         features=["toon", "unlit", "matcap", "rim light", "mobile variants", "gem", "water"],
         why="Popular in Japanese worlds: light toon/unlit shaders with real mobile versions for Android.",
         install="vpm", vpm_repo="https://whiteflare.github.io/vpm-repos/vpm.json", package="jp.whiteflare.unlitwf", pin="2.14.0",
         detect_paths=["Packages/jp.whiteflare.unlitwf"], shader_prefixes=["UnlitWF/"]),
    dict(id="z3y-lit", name="Lit (z3y)", author="z3y", license="MIT", homepage="https://github.com/z3y/shaders",
         kind="world PBR", good_for=["worlds"], platforms=["pc"],
         features=["standard PBR", "lightmaps", "Bakery", "LTCGI"],
         why="A Standard replacement used in many worlds (lightmaps, Bakery, LTCGI). The repository is archived (no more updates) and its last release ships source only, so it is a manual install.",
         install="manual",
         detect_paths=["Packages/com.z3y.shaders", "Assets/z3y"], shader_prefixes=["Lit", "z3y/"]),
    dict(id="skyboxplus", name="SkyboxPlus", author="keijiro", license="MIT", homepage="https://github.com/keijiro/SkyboxPlus",
         kind="sky", good_for=["worlds"], platforms=["pc", "android"],
         features=["gradient sky", "hemisphere sky", "cubemap tint"],
         why="Free gradient skies (up to four gradients, or three-colour hemisphere). No clouds.",
         install="files", files=[("https://raw.githubusercontent.com/keijiro/SkyboxPlus/master/Assets/SkyboxPlus/Gradients.shader", "Gradients.shader"),
                                 ("https://raw.githubusercontent.com/keijiro/SkyboxPlus/master/Assets/SkyboxPlus/Hemisphere.shader", "Hemisphere.shader"),
                                 ("https://raw.githubusercontent.com/keijiro/SkyboxPlus/master/LICENSE", "LICENSE")],
         files_dest="Assets/ShadersShop/Imported/SkyboxPlus", detect_paths=["Assets/ShadersShop/Imported/SkyboxPlus"], shader_prefixes=["SkyboxPlus/"]),
    dict(id="unity-stable-shaders", name="Unity Stable shaders (our own)", author="Astra (built with Claude)", license="own-work", homepage="",
         kind="sky / clouds / sparkle", good_for=["worlds"], platforms=["pc"],
         features=["sunset sky", "sea of clouds", "glitter sparkle"],
         why="Our own shaders, made because no free sea-of-clouds sky exists: a sunset sky, a cloud sea and a sparkle shader.",
         install="own", detect_paths=["Assets/ShadersShop/UnityStable"], shader_prefixes=["Unity Stable/"]),
    dict(id="unity-builtin", name="Unity built-in", author="Unity", license="unity-builtin", homepage="https://docs.unity3d.com/Manual/Built-inShaderGuide.html",
         kind="baseline", good_for=["worlds", "props"], platforms=["pc"], features=["standard PBR", "procedural sky", "particles"],
         why="Always there. Standard, Skybox/Procedural and the legacy particle shaders.", install="builtin",
         detect_paths=[], shader_prefixes=["Standard", "Skybox/", "Legacy Shaders/", "Particles/", "Unlit/", "Mobile/", "UI/", "Sprites/", "Hidden/"]),
    dict(id="vrchat-sdk", name="VRChat SDK shaders", author="VRChat", license="vrchat-sdk", homepage="https://creators.vrchat.com/",
         kind="baseline / mobile", good_for=["worlds", "avatars"], platforms=["pc", "android"], features=["mobile lightmapped", "toon lit (mobile)"],
         why="Come with the SDK. VRChat/Mobile/* is the recommended Android world shader with baked light.", install="builtin",
         detect_paths=["Packages/com.vrchat.base"], shader_prefixes=["VRChat/"]),
    dict(id="sample-sky-shader", name="Sample Sky Shader", author="(Booth seller)", license="store-personal", homepage="https://booth.pm/en/items/8819873",
         kind="sky with volumetric clouds", good_for=["worlds"], platforms=["pc"], features=["raymarched clouds", "day/night", "storm"],
         why="Paid (936 JPY on Booth): raymarched volumetric clouds. Store assets never go in a repo; Astra buys and imports it herself.",
         install="store", detect_paths=[], shader_prefixes=[]),
    dict(id="sorairo-sky", name="Sorairo Sky", author="(Booth seller)", license="store-personal", homepage="https://booth.pm/ja/items/8955808",
         kind="sky with clouds", good_for=["worlds"], platforms=["pc"], features=["drifting clouds", "silver lining"],
         why="Booth: procedural sky with clouds drifting in the wind. Its terms allow public worlds but not redistribution.",
         install="store", detect_paths=[], shader_prefixes=[]),
]


def get(url, binary=False):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=30) as r:
        data = r.read()
    return data if binary else data.decode("utf-8-sig")


def size_of(url):
    try:
        req = urllib.request.Request(url, headers=UA, method="HEAD")
        with urllib.request.urlopen(req, timeout=30) as r:
            return int(r.headers.get("Content-Length") or 0)
    except Exception:
        return 0


def vkey(v):
    core = re.split(r"[-+]", v)[0]
    return [int(x) for x in core.split(".") if x.isdigit()] + [0 if "-" in v else 1]


def satisfies(v, rule):
    if "-" in v:  # never pick a prerelease for a dependency
        return False
    rule = rule.strip()
    if rule.startswith("^"):
        base = rule[1:]
        return vkey(v)[0] == vkey(base)[0] and vkey(v) >= vkey(base)
    if rule.startswith(">="):
        return vkey(v) >= vkey(rule[2:])
    if rule.startswith("~"):
        base = rule[1:]
        return vkey(v)[:2] == vkey(base)[:2] and vkey(v) >= vkey(base)
    return v == rule


def vpm_packages(index, name, version, seen):
    p = index["packages"][name]["versions"][version]
    out = [dict(name=name, version=version, url=p["url"], bytes=size_of(p["url"]))]
    seen.add(name)
    for dep, rule in (p.get("vpmDependencies") or {}).items():
        if dep in seen or dep not in index["packages"]:
            continue
        cands = sorted((v for v in index["packages"][dep]["versions"] if satisfies(v, rule)), key=vkey)
        if cands:
            out += vpm_packages(index, dep, cands[-1], seen)
    return out


def latest_release(repo, pattern):
    rel = json.loads(get(f"https://api.github.com/repos/{repo}/releases/latest"))
    for a in rel.get("assets", []):
        if re.search(pattern, a["name"]):
            return rel["tag_name"], a["browser_download_url"], a["size"], a["name"]
    raise RuntimeError(f"{repo}: no release asset matching {pattern}")


def build():
    shaders = []
    for s in SHELF:
        e = {k: s.get(k, "") for k in ("id", "name", "author", "license", "homepage", "kind", "why", "install", "vpm_repo")}
        e.update(good_for=s["good_for"], platforms=s["platforms"], features=s["features"],
                 detect_paths=s.get("detect_paths", []), shader_prefixes=s.get("shader_prefixes", []),
                 version="", packages=[], unitypackage_url="", unitypackage_bytes=0, files=[], files_dest=s.get("files_dest", ""))
        try:
            if s["install"] == "vpm":
                idx = json.loads(get(s["vpm_repo"]))
                e["packages"] = vpm_packages(idx, s["package"], s["pin"], set())
                e["version"] = s["pin"]
            elif s["install"] == "zip":
                tag, url, size, _ = latest_release(s["github"], s["asset_pattern"])
                e["version"] = tag.lstrip("v")
                e["packages"] = [dict(name=s["package"], version=e["version"], url=url, bytes=size)]
            elif s["install"] == "unitypackage":
                tag, url, size, _ = latest_release(s["github"], s["asset_pattern"])
                e["version"], e["unitypackage_url"], e["unitypackage_bytes"] = tag.lstrip("v"), url, size
            elif s["install"] == "files":
                e["files"] = [dict(url=u, name=n, bytes=size_of(u)) for u, n in s["files"]]
                e["version"] = "master"
        except Exception as ex:  # keep the shelf entry, say what failed
            e["why"] += f" (catalog refresh failed: {ex})"
        shaders.append(e)
        print(f"{e['id']:22} {e['install']:12} v{e['version']:10} {sum(p['bytes'] for p in e['packages']) + e['unitypackage_bytes'] + sum(f['bytes'] for f in e['files']):>12,} bytes")
    return {"schema": "unity-unicorn.shaders-catalog/v1", "tool": "shaders-shop", "tool_version": "1.0.0",
            "updated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "shaders": shaders}


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="Assets/ShadersShop/shaders-catalog.json")
    a = ap.parse_args()
    cat = build()
    with open(a.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cat, f, indent=2)
        f.write("\n")
    print("wrote", a.out)
