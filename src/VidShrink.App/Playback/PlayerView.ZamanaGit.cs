using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private bool _goToWired;

    internal bool GoToVisible => GoToBox.IsVisible;

    /// <summary>
    /// OYA-05: zamana git kutusu "ss", "dd:ss" ya da "sa:dd:ss" kabul eder; saniye kesirli yazılabilir (nokta ya da
    /// virgül), dakika ve saniye alanı 60'ı geçmez. Boş ya da bozuk giriş reddedilir.
    /// </summary>
    internal static bool TryParseGoTo(string? text, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Trim().Split(':');
        if (parts.Length > 3) return false;

        double total = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index].Trim();
            var last = index == parts.Length - 1;
            if (part.Length == 0) return false;
            double value;
            if (last)
            {
                if (!double.TryParse(part.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) return false;
            }
            else
            {
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var whole)) return false;
                value = whole;
            }

            if (index > 0 && value >= 60) return false;
            total = total * 60 + value;
        }

        if (!double.IsFinite(total)) return false;
        seconds = total;
        return true;
    }

    internal void ShowGoTo()
    {
        if (!_goToWired)
        {
            _goToWired = true;
            GoToText.AddHandler(KeyDownEvent, OnGoToKey, RoutingStrategies.Tunnel);
            GoToText.LostFocus += (_, _) => GoToBox.IsVisible = false;
        }

        GoToText.Text = "";
        GoToBox.IsVisible = true;
        GoToText.Focus();
        _trace.Add("goto -> open");
    }

    internal void SubmitGoTo(string? text)
    {
        GoToBox.IsVisible = false;
        if (!TryParseGoTo(text, out var at))
        {
            _trace.Add("goto -> invalid");
            ShowOsd(Strings.Get("player.goto.invalid", text?.Trim() ?? ""));
            return;
        }

        var duration = _seek.Duration;
        var target = double.IsFinite(duration) && duration > 0 ? Math.Min(at, duration) : at;
        _trackPaused = false;
        KullaniciIslemi();
        _seek.GoTo(target);
        _trace.Add("goto -> " + Saat.Tani.Konum(target));
    }

    internal MenuItem SeekStepsMenu()
    {
        EnsureSettings();
        var steps = _settings.SeekSteps;
        var items = new List<Control>();
        foreach (var slot in SeekSteps.Slots)
        {
            var which = slot;
            var current = steps.Of(which);
            var choices = new List<Control>();
            foreach (var preset in SeekSteps.Presets(which))
            {
                var value = preset;
                choices.Add(Choice(Strings.Get("main.player.menu.seek", "±" + Saat.Adim(value, Strings.Culture)), value == current, () => SetSeekStep(which, value)));
            }

            var row = Keymap.Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Key && r.Action.Step == which && r.Action.Amount > 0);
            var header = Strings.Get("main.player.menu.seek", "±" + Saat.Adim(current, Strings.Culture));
            items.Add(Submenu(row is null ? header : header + " " + Keymap.Gesture(row.Input), choices));
        }

        return Submenu(Strings.Get("player.view.seek-steps"), items);
    }

    internal void SetSeekStep(SeekStep step, double seconds)
    {
        EnsureSettings();
        var next = _settings.SeekSteps.With(step, seconds);
        if (next == _settings.SeekSteps) return;
        _settings.SeekSteps = next;
        if (SettingsFile() is not null) Keymap.Steps = next;
        SaveSettings();
        _trace.Add("seekstep " + step + " -> " + Saat.Tani.Konum(seconds));
        RefreshSerit();
        AdvancedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnGoToKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SubmitGoTo(GoToText.Text);
            Focus();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            GoToBox.IsVisible = false;
            Focus();
        }
    }
}
