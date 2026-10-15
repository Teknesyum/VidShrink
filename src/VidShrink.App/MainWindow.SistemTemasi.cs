using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using VidShrink.App.Themes;

namespace VidShrink.App;

/// <summary>
/// AYR-P4: Ayarlar'daki "sistem temasını izle" ve "canlandırmaları kapat" anahtarları.
/// Palet yalnız <see cref="PaletteCatalog.Use"/> ile uygulanır; izleme açıkken
/// <see cref="SystemThemeFollower"/> sistemin açık ya da koyu oluşuna göre seçer.
/// </summary>
public partial class MainWindow
{
    private SystemThemeFollower? _themeFollower;
    private string _lightTheme = PaletteCatalog.DefaultLight;
    private string _darkTheme = PaletteCatalog.Default;

    private void WatchSystemThemeAndMotion()
    {
        Watch(ChkReduceMotion, ToggleButton.IsCheckedProperty, OnReduceMotionChanged);
        Watch(ChkFollowSystemTheme, ToggleButton.IsCheckedProperty, OnFollowSystemThemeChanged);
        Watch(CmbThemeLight, SelectingItemsControl.SelectedIndexProperty, OnThemePairChosen);
        Watch(CmbThemeDark, SelectingItemsControl.SelectedIndexProperty, OnThemePairChosen);
        MotionPreference.Changed += OnMotionPreferenceChanged;
    }

    private void ReleaseSystemThemeAndMotion()
    {
        MotionPreference.Changed -= OnMotionPreferenceChanged;
        _themeFollower?.Dispose();
        _themeFollower = null;
    }

    private void ApplyMotion()
    {
        var reduced = MotionPreference.Reduced;
        if (reduced == _motionReduced) return;
        _motionReduced = reduced;
        Classes.Set("reduced-motion", reduced);
    }

    private void OnMotionPreferenceChanged(object? sender, EventArgs e) => ApplyMotion();

    private void OnReduceMotionChanged()
    {
        if (_syncing) return;
        MotionPreference.UserReduced = ChkReduceMotion.IsChecked == true;
        ApplyMotion();
        SaveAppSettings();
    }

    private void OnFollowSystemThemeChanged()
    {
        if (_syncing) return;
        ApplyTheme();
        SaveAppSettings();
    }

    private void OnThemePairChosen()
    {
        if (_syncing) return;
        if (CmbThemeLight.SelectedIndex is var light && light >= 0 && light < _themeOrder.Count)
            _lightTheme = _themeOrder[light];
        if (CmbThemeDark.SelectedIndex is var dark && dark >= 0 && dark < _themeOrder.Count)
            _darkTheme = _themeOrder[dark];
        ApplyTheme();
        SaveAppSettings();
    }

    private bool FollowingSystemTheme => ChkFollowSystemTheme.IsChecked == true;

    /// <summary>
    /// Seçili paleti yürürlüğe koyar: izleme açıksa sistemin açık ya da koyu oluşuna göre,
    /// kapalıysa kullanıcının seçtiği palet.
    /// </summary>
    private void ApplyTheme()
    {
        var follow = FollowingSystemTheme;
        SystemThemePairRow.IsVisible = follow;
        CmbTheme.IsEnabled = !follow;
        if (!follow && _themeFollower is null)
        {
            PaletteCatalog.Use(_theme);
            return;
        }

        _themeFollower ??= new SystemThemeFollower(SystemThemeFollower.CreateSource());
        _themeFollower.Chosen = _theme;
        _themeFollower.Light = _lightTheme;
        _themeFollower.Dark = _darkTheme;
        _themeFollower.Enabled = follow;
    }

    private void RestoreSystemThemeAndMotion(AppSettings settings)
    {
        _theme = PaletteCatalog.Pick(settings.Theme, false, null, null, null);
        _lightTheme = PaletteCatalog.Pick(settings.LightTheme, false, null, null, null);
        _darkTheme = PaletteCatalog.Pick(settings.DarkTheme, false, null, null, null);
        ChkFollowSystemTheme.IsChecked = settings.FollowSystemTheme;
        MarkChosenTheme();
        ApplyTheme();

        ChkReduceMotion.IsChecked = settings.ReduceMotion;
        MotionPreference.UserReduced = settings.ReduceMotion;
        ApplyMotion();
    }

    private void FillThemePairLists(string[] labels)
    {
        CmbThemeLight.ItemsSource = labels;
        CmbThemeDark.ItemsSource = labels.ToArray();
        MarkThemePair();
    }

    private void MarkThemePair()
    {
        CmbThemeLight.SelectedIndex = IndexOfTheme(_lightTheme);
        CmbThemeDark.SelectedIndex = IndexOfTheme(_darkTheme);
    }

    private int IndexOfTheme(string name)
    {
        for (var at = 0; at < _themeOrder.Count; at++)
            if (string.Equals(_themeOrder[at], name, StringComparison.OrdinalIgnoreCase)) return at;
        return -1;
    }
}
