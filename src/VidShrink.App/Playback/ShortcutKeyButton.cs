using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

/// <summary>
/// Kısayol tablosundaki tuş kutusu. Tıklanınca dinlemeye geçer; ilk tuş (değiştiricileriyle)
/// ya da orta/geri/ileri fare düğmesi yeni atama olur, Esc ya da sol/sağ tık vazgeçer.
/// Dinlerken girdi TopLevel'ın tünel sınıf işleyicisinde tutulur ve işlenmiş sayılır; oynatıcı
/// ve düzenleyici kendi işleyicilerinde onu hiç görmez.
/// </summary>
internal sealed class ShortcutKeyButton : Button
{
    private static ShortcutKeyButton? _listening;
    private static Key? _swallowKeyUp;
    private static bool _swallowRelease;

    private readonly TextBlock _text;

    static ShortcutKeyButton()
    {
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(OnTopKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.KeyUpEvent.AddClassHandler<TopLevel>(OnTopKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerPressedEvent.AddClassHandler<TopLevel>(OnTopPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerReleasedEvent.AddClassHandler<TopLevel>(OnTopReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    internal ShortcutKeyButton(ShortcutSlot slot, ControlTheme? theme = null, ControlTheme? textTheme = null)
    {
        Slot = slot;
        if (theme is not null) Theme = theme;
        _text = new TextBlock { Theme = textTheme };
        Content = _text;
        Click += (_, _) => Listen();
        Show();
    }

    protected override Type StyleKeyOverride => typeof(Button);

    internal static ShortcutKeyButton? Listening => _listening;

    internal static bool IsListening => _listening is not null;

    internal ShortcutSlot Slot { get; }

    internal string Text => _text.Text ?? "";

    internal event EventHandler<PlayerInput>? Captured;

    internal static string Display(PlayerInput? input)
        => Keymap.Boxed(input is { } bound ? Keymap.Gesture(bound) : Strings.Get("shortcuts.unassigned"));

    internal void Listen()
    {
        if (_listening is { } other && !ReferenceEquals(other, this)) other.Stop();
        _listening = this;
        Classes.Set("listening", true);
        _text.Text = Strings.Get("shortcuts.listening");
        Focus();
    }

    internal void Stop()
    {
        if (ReferenceEquals(_listening, this)) _listening = null;
        Classes.Set("listening", false);
        Show();
    }

    internal void Finish(PlayerInput input)
    {
        Stop();
        Captured?.Invoke(this, input);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Theme is null && this.TryFindResource("ShortcutKeyButton", out var theme) && theme is ControlTheme button) Theme = button;
        if (_text.Theme is null && this.TryFindResource("MonoValue", out var mono) && mono is ControlTheme text) _text.Theme = text;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (ReferenceEquals(_listening, this)) Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void Show()
    {
        _text.Text = Display(Slot.Current);
        ToolTip.SetTip(this, Strings.Get("shortcuts.change-tip"));
        AutomationProperties.SetName(this, Slot.Label + " " + _text.Text);
    }

    private KeyModifiers Clean(KeyModifiers modifiers)
        => Slot.Map == ShortcutMap.Editor ? VidShrink.App.Editing.EditorKeymap.Clean(modifiers) : Keymap.Clean(modifiers);

    private static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System;

    private static void OnTopKeyDown(TopLevel top, KeyEventArgs e)
    {
        if (_listening is not { } button) return;
        e.Handled = true;
        if (IsModifier(e.Key) || e.Key == Key.None) return;
        _swallowKeyUp = e.Key;
        if (e.Key == Key.Escape) button.Stop();
        else button.Finish(PlayerInput.OnKey(e.Key, button.Clean(e.KeyModifiers)));
    }

    private static void OnTopKeyUp(TopLevel top, KeyEventArgs e)
    {
        if (_listening is not null) e.Handled = true;
        else if (_swallowKeyUp == e.Key)
        {
            _swallowKeyUp = null;
            e.Handled = true;
        }
    }

    private static void OnTopPressed(TopLevel top, PointerPressedEventArgs e)
    {
        if (_listening is not { } button) return;
        e.Handled = true;
        _swallowRelease = true;
        switch (e.GetCurrentPoint(null).Properties.PointerUpdateKind)
        {
            case PointerUpdateKind.MiddleButtonPressed:
                button.Finish(PlayerInput.OnPress(PlayerButton.Middle));
                break;
            case PointerUpdateKind.XButton1Pressed:
                button.Finish(PlayerInput.OnPress(PlayerButton.Back));
                break;
            case PointerUpdateKind.XButton2Pressed:
                button.Finish(PlayerInput.OnPress(PlayerButton.Forward));
                break;
            default:
                button.Stop();
                break;
        }
    }

    private static void OnTopReleased(TopLevel top, PointerReleasedEventArgs e)
    {
        if (_listening is null && !_swallowRelease) return;
        _swallowRelease = false;
        e.Handled = true;
    }
}
