import os
import re
import shutil
import subprocess
import sys

FOREIGN = ("/opt/homebrew/", "/usr/local/")


def run(*args):
    return subprocess.run(args, check=True, capture_output=True, text=True).stdout


def install_id(path):
    lines = run("otool", "-D", path).splitlines()[1:]
    return lines[0].strip() if lines else None


def deps(path):
    own = install_id(path)
    out = []
    for line in run("otool", "-L", path).splitlines()[1:]:
        line = line.strip()
        if not line:
            continue
        ref = line.split(" (compatibility")[0].strip()
        if ref != own:
            out.append(ref)
    return out


def rpaths(path):
    lines = run("otool", "-l", path).splitlines()
    out = []
    for i, line in enumerate(lines):
        if line.strip() == "cmd LC_RPATH":
            for j in range(i + 1, min(i + 4, len(lines))):
                m = re.search(r"path (.+) \(offset", lines[j])
                if m:
                    out.append(m.group(1))
                    break
    return out


def resolve(ref, origin, rps):
    base = os.path.dirname(origin)
    if ref.startswith("@loader_path/"):
        cand = os.path.join(base, ref[len("@loader_path/"):])
        return os.path.realpath(cand) if os.path.exists(cand) else None
    if ref.startswith("@rpath/"):
        for rp in rps:
            cand = os.path.join(rp.replace("@loader_path", base), ref[len("@rpath/"):])
            if os.path.exists(cand):
                return os.path.realpath(cand)
        return None
    if ref.startswith("@"):
        return None
    return os.path.realpath(ref) if os.path.exists(ref) else ref


def main():
    root = os.path.realpath(sys.argv[1])
    dest = sys.argv[2]
    os.makedirs(dest, exist_ok=True)
    names = {root: os.path.basename(install_id(root) or root)}
    edits = {}
    queue = [root]
    while queue:
        f = queue.pop()
        rps = rpaths(f)
        changes = []
        for ref in deps(f):
            target = resolve(ref, f, rps)
            if target is None:
                sys.exit(f"unresolved {ref} in {f} rpaths={rps}")
            if not target.startswith(FOREIGN):
                continue
            if target not in names:
                name = os.path.basename(install_id(target) or target)
                if name in names.values():
                    sys.exit(f"name clash {name} for {target}")
                names[target] = name
                queue.append(target)
            changes.append((ref, names[target]))
        edits[f] = (changes, rps)

    for src, name in sorted(names.items(), key=lambda kv: kv[1]):
        out = os.path.join(dest, name)
        shutil.copyfile(src, out)
        os.chmod(out, 0o755)
        changes, rps = edits[src]
        cmd = ["install_name_tool", "-id", f"@rpath/{name}"]
        for ref, new in changes:
            cmd += ["-change", ref, f"@loader_path/{new}"]
        for rp in rps:
            cmd += ["-delete_rpath", rp]
        cmd.append(out)
        subprocess.run(cmd, check=True)
        subprocess.run(["codesign", "--force", "--sign", "-", out], check=True, capture_output=True)
        print(f"bundled {name} <- {src} ({os.path.getsize(out)} B, {len(changes)} refs rewritten, {len(rps)} rpaths dropped)")
    print(f"bundled-count {len(names)}")


if __name__ == "__main__":
    main()
