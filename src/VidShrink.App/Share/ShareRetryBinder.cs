using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Core.Share;

namespace VidShrink.App.Share;

/// <summary>
/// Yeniden deneme düğmesini bir paylaşım sonucuna bağlar: yazısını, basılabilirliğini ve
/// sunucunun istediği beklemenin geri sayımını yürütür.
/// </summary>
/// <remarks>
/// Kararı kendi vermiyor, <see cref="ShareRetryPrompt"/>'a soruyor. Buradaki tek iş
/// saniyeyi düşürmek ve düğmeyi tazelemek; üç gösterim yüzeyi de aynı gövdeyi kullanır.
/// </remarks>
internal sealed class ShareRetryBinder : IDisposable
{
    private readonly Button _button;
    private readonly Func<ShareTargetTable?> _targets;
    private readonly Action<string?> _retry;
    private DispatcherTimer? _timer;
    private ShareResult? _result;
    private int _left;

    public ShareRetryBinder(Button button, Func<ShareTargetTable?> targets, Action<string?> retry)
    {
        _button = button;
        _targets = targets;
        _retry = retry;
        _button.Click += OnClick;
    }

    /// <summary>Ölçüm için: düğmenin o anki hali.</summary>
    internal ShareRetryPrompt Current =>
        _result is null ? default : ShareRetryPrompt.For(_result, _targets(), _left);

    public void Show(ShareResult result)
    {
        Durdur();
        _result = result;
        _left = ShareRetryPrompt.InitialSeconds(result);
        Tazele();

        if (_left > 0)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTick;
            _timer.Start();
        }
    }

    public void Hide()
    {
        Durdur();
        _result = null;
        _button.IsVisible = false;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_left > 0) _left--;
        if (_left == 0) Durdur();
        Tazele();
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        if (_result is null) return;

        var prompt = ShareRetryPrompt.For(_result, _targets(), _left);
        if (!prompt.Visible || !prompt.Enabled) return;

        Hide();
        _retry(prompt.RetryTargetId);
    }

    private void Tazele()
    {
        var prompt = Current;
        _button.IsVisible = prompt.Visible;
        _button.IsEnabled = prompt.Enabled;
        var yazi = prompt.Visible ? Strings.Get(prompt.Key, prompt.Args.ToArray()) : string.Empty;
        _button.Content = yazi;
        AutomationProperties.SetName(_button, prompt.Visible ? yazi : Strings.Get("settings.share.retry"));
    }

    private void Durdur()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    public void Dispose()
    {
        Durdur();
        _button.Click -= OnClick;
    }
}
