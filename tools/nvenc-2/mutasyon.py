# -*- coding: utf-8 -*-
import os, re, subprocess

KOK = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.chdir(KOK)
FILTRE = "FullyQualifiedName~NvencLookaheadTests|FullyQualifiedName~FfmpegArgumentsTests"
ORTAM = dict(os.environ, VIDSHRINK_LIBMPV=os.path.join(KOK, "tools", "libmpv", "libmpv-2.dll"))
FA = "src/VidShrink.Core/FfmpegArguments.cs"
DOC = "docs/olcumler/nvenc-kalite-kollari.md"

KOSUL = '        else if (CodecModel.Vendor(codec) == EncoderVendor.Nvenc && Supported("-lookahead_level", "3"))\n'
GOVDE = '            args.AddRange(new[] { "-rc-lookahead", "20", "-lookahead_level", "3" });\n'

KESIMLER = [
    ("M1 satici kapisi dusuruldu (her kodlayici lookahead aliyor)", FA, KOSUL,
     '        else if (Supported("-lookahead_level", "3"))\n'),
    ("M2 kabul yoklamasi dusuruldu (kosulsuz yaziliyor)", FA, KOSUL,
     '        else if (CodecModel.Vendor(codec) == EncoderVendor.Nvenc)\n'),
    ("M3 seviye 3 yerine 1 yaziliyor", FA, GOVDE,
     '            args.AddRange(new[] { "-rc-lookahead", "20", "-lookahead_level", "1" });\n'),
    ("M4 kol hic yazilmiyor", FA, GOVDE, '            { }\n'),
    ("M5 rc-lookahead penceresi 20 yerine 40", FA, GOVDE,
     '            args.AddRange(new[] { "-rc-lookahead", "40", "-lookahead_level", "3" });\n'),
    ("M6 belgedeki kabul cumlesi alinmadiya cevrildi", DOC,
     "`-rc-lookahead 20 -lookahead_level 3` **alındı**",
     "`-rc-lookahead 20 -lookahead_level 3` **alınmadı**"),
]


def kos():
    d = subprocess.run(["dotnet", "build", "VidShrink.sln", "-c", "Release", "-warnaserror", "-m:2"],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if d.returncode != 0:
        return "DERLEME KIRMIZI", 0
    t = subprocess.run(["dotnet", "test", "tests/VidShrink.Tests/VidShrink.Tests.csproj", "-c", "Release",
                        "--no-build", "--filter", FILTRE],
                       capture_output=True, text=True, encoding="utf-8", errors="replace", env=ORTAM)
    m = re.search(r"Başarısız:\s+(\d+), Başarılı:\s+(\d+)", t.stdout)
    return (int(m.group(1)), int(m.group(2))) if m else ("SAYIM OKUNAMADI", 0)


print("== taban ==", kos(), flush=True)
for ad, yol, eski, yeni in KESIMLER:
    ham = open(yol, "rb").read()
    bom = ham.startswith(b"\xef\xbb\xbf")
    s = ham.decode("utf-8-sig")
    if s.count(eski) != 1:
        print(f"{ad}: KESIM TUTMADI ({s.count(eski)} eslesme)", flush=True)
        continue
    open(yol, "wb").write((b"\xef\xbb\xbf" if bom else b"") + s.replace(eski, yeni).encode("utf-8"))
    try:
        print(f"{ad}: kirmizi={kos()[0]}", flush=True)
    finally:
        open(yol, "wb").write(ham)
print("== geri konuldu ==", kos(), flush=True)
