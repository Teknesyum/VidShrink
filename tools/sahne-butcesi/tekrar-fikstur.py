import io, os, sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(newline=chr(10))

kok = sys.argv[1]
senaryo = {
    "gurultu-sifir":  ("1.273", "1.273", "ayni",   "ustunde"),
    "gurultu-kucuk":  ("1.273", "1.293", "farkli", "ustunde"),
    "gurultu-buyuk":  ("1.273", "1.373", "farkli", "altinda"),
}
for ad, (a, b, sha, _) in senaryo.items():
    d = os.path.join(kok, ad, ".calisma", "T114")
    os.makedirs(d, exist_ok=True)
    with io.open(os.path.join(d, "tekrar-yedek-p1-karisik.csv"), "w", encoding="utf-8") as f:
        f.write("olcu;kosum1;kosum2;fark;birim;not\n")
        f.write("dosya boyutu;61426513;61426513;0;bayt;\n")
        f.write("sha256;AAAA;BBBB;%s;-;\n" % sha)
        f.write("MAE(verilen,hak);%s;%s;0;pp;uydurma girdi\n" % (a, b))
print("\n".join("%s;%s" % (k, v[3]) for k, v in senaryo.items()))
