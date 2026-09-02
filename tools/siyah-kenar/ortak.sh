KOK="C:/Users/Administrator/Desktop/Projeler/Vidshrink"
WT="$KOK/.claude/worktrees/T134"
HAVUZ="$KOK/.calisma/kaynak"
IS="$WT/.calisma/t134"
KAYNAK="$IS/kaynak"
CIKTI="$IS/cikti"
OLCU="$IS/olcu"
YOKLA="$IS/yokla"
RENK="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -color_range pc"
ARA_KOD="-c:v libx264 -preset veryfast -crf 12 -pix_fmt yuv420p10le -x264-params keyint=120:min-keyint=120:scenecut=0 -an"

# ad | taban | ss | sure | aktif_h | ust_bant | alt_bant | tip
KATALOG="
KA|parca-1.mkv|20|20|804|138|138|duz
KB|parca-2.mkv|20|20|872|104|104|duz
KC|parca-3.mkv|20|20|1036|22|22|duz
KD|parca-1.mkv|20|20|804|120|156|gurultulu
NA|parca-1.mkv|20|20|1080|0|0|kenarsiz
NB|parca-2.mkv|20|20|1080|0|0|kenarsiz
VD|parca-3.mkv|20|20|804|138|138|degisken
"

satirlar () { echo "$KATALOG" | grep -v '^$'; }
alan () { echo "$1" | cut -d'|' -f"$2"; }
