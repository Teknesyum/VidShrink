namespace VidShrink.Core;

/// <summary>
/// later.md #42: prompter'ın okuma kipindeki görünümü. Ayna metni yatayda çevirir (cam yansıtıcılı düzenekler için),
/// okuma çizgisi gözün durduğu yeri gösterir, yazı boyu <see cref="SizeCount"/> basamaktan biridir; basamakların
/// ölçüsü arayüzün yazı belirteçlerinden gelir.
/// </summary>
public sealed record PrompterLook(bool Mirror = false, bool Line = true, int Size = PrompterLook.DefaultSize)
{
    public const int SizeCount = 3;

    public const int DefaultSize = SizeCount - 1;

    public static int ClampSize(int size) => size >= 0 && size < SizeCount ? size : DefaultSize;

    public bool CanShrink => Size > 0;

    public bool CanGrow => Size < SizeCount - 1;

    public PrompterLook Smaller() => CanShrink ? this with { Size = Size - 1 } : this;

    public PrompterLook Larger() => CanGrow ? this with { Size = Size + 1 } : this;
}
