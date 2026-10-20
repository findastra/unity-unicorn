"""shaders-shop (Unity Stable): which shaders a Unity world uses, read from its files without opening Unity.

Claude, 2026-10-18. Read-only. Follows each scene (the build list, else every scene under Assets/) to the materials it
uses, directly and through prefabs, the skybox included; reads each material's shader; names the shader from its
file (or Unity's built-in id) and matches it to the shop's catalog. Writes the same record the Unity shop writes
(schema unity-unicorn.world-shaders/v1), so the shop can show which worlds use each shader.

Usage: python scan-world-shaders-20261018.py <project folder> --world "<world name>" --catalog <shaders-catalog.json> --out <file.json>
"""
import argparse, json, os, re, sys
from datetime import datetime, timezone

GUID = re.compile(rb"guid: ([0-9a-f]{32}), type: ([23])")
SHADER = re.compile(rb"m_Shader: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d)\}")
NAME = re.compile(r'^\s*Shader\s+"([^"]+)"', re.M)
BUILTIN_EXTRA = "0000000000000000f000000000000000"
BUILTIN_IDS = {46: "Standard", 45: "Standard (Specular setup)", 10750: "Unlit/Transparent", 10751: "Unlit/Transparent Cutout",
               10752: "Unlit/Texture", 10755: "Unlit/Color", 106: "Skybox/Procedural", 103: "Skybox/Cubemap", 104: "Skybox/6 Sided",
               200: "Legacy Shaders/Particles/Additive", 203: "Legacy Shaders/Particles/Alpha Blended", 10720: "Mobile/Particles/Additive",
               210: "Particles/Standard Unlit", 211: "Particles/Standard Surface", 4800000: "(shader file)"}


def index(project):
    by_guid = {}
    for top in ("Assets", "Packages"):
        for d, _, files in os.walk(os.path.join(project, top)):
            for f in files:
                if f.endswith(".meta"):
                    p = os.path.join(d, f)
                    try:
                        with open(p, "rb") as fh:
                            m = re.search(rb"guid: ([0-9a-f]{32})", fh.read(2048))
                    except OSError:
                        continue
                    if m:
                        by_guid[m.group(1).decode()] = os.path.relpath(p[:-5], project).replace("\\", "/")
    return by_guid


def scenes_of(project):
    eb = os.path.join(project, "ProjectSettings", "EditorBuildSettings.asset")
    out = []
    if os.path.exists(eb):
        txt = open(eb, encoding="utf-8", errors="replace").read()
        for m in re.finditer(r"enabled: 1\s+path: (.+?\.unity)", txt):
            if os.path.exists(os.path.join(project, m.group(1))):
                out.append(m.group(1).strip())
    if not out:
        for d, _, files in os.walk(os.path.join(project, "Assets")):
            out += [os.path.relpath(os.path.join(d, f), project).replace("\\", "/") for f in files if f.endswith(".unity")]
    return out


def materials_in(project, path, by_guid, seen):
    """Material paths a scene/prefab uses, following prefabs (type 3 refs to .prefab files)."""
    mats = set()
    try:
        data = open(os.path.join(project, path), "rb").read()
    except OSError:
        return mats
    for g, t in GUID.findall(data):
        p = by_guid.get(g.decode())
        if not p:
            continue
        if p.endswith(".mat"):
            mats.add(p)
        elif p.endswith(".prefab") and p not in seen:
            seen.add(p)
            mats |= materials_in(project, p, by_guid, seen)
    return mats


def shader_of(project, mat, by_guid):
    try:
        data = open(os.path.join(project, mat), "rb").read()
    except OSError:
        return None, None
    m = SHADER.search(data)
    if not m:
        return None, None
    fid, guid = int(m.group(1)), m.group(2).decode()
    if guid == BUILTIN_EXTRA or guid.startswith("0000000000000000"):
        return BUILTIN_IDS.get(fid, f"built-in shader {fid}"), "Resources/unity_builtin_extra"
    p = by_guid.get(guid)
    if not p:
        return f"missing shader {guid}", ""
    if p.endswith(".shader"):
        try:
            n = NAME.search(open(os.path.join(project, p), encoding="utf-8", errors="replace").read())
            return (n.group(1) if n else os.path.basename(p)), p
        except OSError:
            return os.path.basename(p), p
    return os.path.basename(p), p


def match(catalog, name, path):
    sh = catalog.get("shaders", [])
    for s in sh:
        if s["install"] != "builtin" and any(path.startswith(d + "/") for d in s.get("detect_paths", [])):
            return s
    for s in sh:
        if s["install"] != "builtin" and any(name.startswith(p) for p in s.get("shader_prefixes", [])):
            return s
    if path.startswith("Packages/com.vrchat.") or name.startswith("VRChat/"):
        return next((s for s in sh if s["id"] == "vrchat-sdk"), None)
    if path in ("Resources/unity_builtin_extra", ""):
        return next((s for s in sh if s["id"] == "unity-builtin"), None)
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("project"); ap.add_argument("--world", required=True); ap.add_argument("--catalog", required=True); ap.add_argument("--out", required=True)
    a = ap.parse_args()
    catalog = json.load(open(a.catalog, encoding="utf-8"))
    by_guid = index(a.project)
    rec = dict(schema="unity-unicorn.world-shaders/v1", world=a.world, scanned_at=datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
               unity=open(os.path.join(a.project, "ProjectSettings", "ProjectVersion.txt")).readline().split(":")[1].strip(),
               scanned_by="shaders-shop scan-world-shaders-20261018.py (read-only, from files)", scenes=scenes_of(a.project), shaders=[])
    uses = {}
    for scene in rec["scenes"]:
        for mat in materials_in(a.project, scene, by_guid, set()):
            name, path = shader_of(a.project, mat, by_guid)
            if not name:
                continue
            u = uses.get(name)
            if u is None:
                e = match(catalog, name, path or "")
                u = dict(shader=name, shader_path=path or "", catalog_id=e["id"] if e else ("in-assets" if (path or "").startswith("Assets/") else "unknown"),
                         catalog_name=e["name"] if e else ("inside the world's Assets folder (own or bundled; check)" if (path or "").startswith("Assets/") else "not in the catalog"),
                         license=e["license"] if e else ("check" if (path or "").startswith("Assets/") else "unknown"),
                         source=(path.split("/")[1] if (path or "").startswith("Packages/") else os.path.dirname(path) if (path or "").startswith("Assets/") else "Unity built-in"),
                         version="", materials=[], scenes=[])
                uses[name] = u
            if mat not in u["materials"]:
                u["materials"].append(mat)
            if scene not in u["scenes"]:
                u["scenes"].append(scene)
    rec["shaders"] = sorted(uses.values(), key=lambda u: (u["catalog_id"], u["shader"]))
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(rec, f, indent=2); f.write("\n")
    print(f"{a.world}: {len(rec['scenes'])} scenes, {len(rec['shaders'])} shaders -> {a.out}")
    for u in rec["shaders"]:
        print(f"  {u['shader'][:44]:44} {u['catalog_id']:16} {len(u['materials']):4} materials")


if __name__ == "__main__":
    main()
