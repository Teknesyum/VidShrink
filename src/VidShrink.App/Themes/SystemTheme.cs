using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;

namespace VidShrink.App.Themes;

/// <summary>
/// AYR-P4: işletim sisteminin açık ya da koyu oluşu. Gerçeği <see cref="PlatformThemeSource"/>;
/// testler sahtesini verir, gerçek sistem teması okunmaz ve değiştirilmez.
/// </summary>
internal interface ISystemThemeSource
{
    /// <summary>Sistem açık temada mı; okunamıyorsa <c>null</c>.</summary>
    bool? IsLight { get; }

    bool IsHighContrast => false;

    event EventHandler? Changed;
}

/// <summary>Avalonia'nın platform renk değerleri ve <c>ColorValuesChanged</c> olayı.</summary>
internal sealed class PlatformThemeSource : ISystemThemeSource, IDisposable
{
    private readonly IPlatformSettings? _settings;

    public PlatformThemeSource(IPlatformSettings? settings)
    {
        _settings = settings;
        if (_settings is not null) _settings.ColorValuesChanged += OnColorValuesChanged;
    }

    public bool? IsLight
    {
        get
        {
            try
            {
                return _settings?.GetColorValues().ThemeVariant switch
                {
                    PlatformThemeVariant.Light => true,
                    PlatformThemeVariant.Dark => false,
                    _ => null,
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public bool IsHighContrast
    {
        get
        {
            try
            {
                return _settings?.GetColorValues().ContrastPreference == ColorContrastPreference.High;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public event EventHandler? Changed;

    public void Dispose()
    {
        if (_settings is not null) _settings.ColorValuesChanged -= OnColorValuesChanged;
    }

    private void OnColorValuesChanged(object? sender, PlatformColorValues values) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// AYR-P4: "sistem temasını izle" açıkken sistem değişince açık ya da koyu seçime geçer.
/// Seçim <see cref="PaletteCatalog.Pick"/>, uygulama yalnız <see cref="PaletteCatalog.Use"/>;
/// palet ikinci bir yerde birleştirilmez. Kapanınca kullanıcının seçtiği palete döner.
/// </summary>
internal sealed class SystemThemeFollower : IDisposable
{
    private readonly ISystemThemeSource _source;
    private bool _enabled;

    /// <summary>Gerçek kaynağı kuran dikiş; testler sahtesini verir.</summary>
    internal static Func<ISystemThemeSource> CreateSource { get; set; }
        = () => new PlatformThemeSource(Application.Current?.PlatformSettings);

    public SystemThemeFollower(ISystemThemeSource source)
    {
        _source = source;
        _source.Changed += OnSourceChanged;
    }

    public string? Chosen { get; set; }

    public bool ChosenByUser { get; set; } = true;

    public string? Light { get; set; }

    public string? Dark { get; set; }

    public bool? SystemIsLight => _source.IsLight;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Apply();
        }
    }

    /// <summary>Uygulanan paletin adı değişince.</summary>
    public event EventHandler<string>? Applied;

    public string Apply()
    {
        var name = PaletteCatalog.Use(PaletteCatalog.Pick(Chosen, ChosenByUser, _enabled, _enabled ? _source.IsLight : null, Light, Dark, _source.IsHighContrast));
        Applied?.Invoke(this, name);
        return name;
    }

    public void Dispose()
    {
        _source.Changed -= OnSourceChanged;
        if (_source is IDisposable disposable) disposable.Dispose();
    }

    private bool Reacts => _enabled || !ChosenByUser;

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        if (!Reacts) return;
        if (Dispatcher.UIThread.CheckAccess()) Apply();
        else Dispatcher.UIThread.Post(() => { if (Reacts) Apply(); });
    }
}
