using Avalonia.Controls;

namespace VidShrink.App.Recorder;

/// <summary>
/// later.md #42, EKB-15: prompter penceresine açılan düğme. Pencere sahipsiz açılır, kaydedici küçülse de
/// önde kalır. Kayıt başlayınca ya da sürünce metin akar, duraklayınca ya da bitince durur; geçiş anında
/// bir kez, elle durdurulan akış sonraki şerit tazelemesinde yeniden başlamaz.
/// </summary>
internal partial class RecorderView
{
    private bool _prompterRunning;

    internal RecorderPrompter? PrompterWindow { get; private set; }

    private void InitPrompter() => BtnPrompter.Click += (_, _) => ShowPrompter();

    internal RecorderPrompter ShowPrompter()
    {
        if (PrompterWindow is { } open)
        {
            open.Activate();
            return open;
        }

        var window = new RecorderPrompter(_settings.PrompterText, _settings.PrompterStep, _settings.PrompterLook);
        window.LookChanged += look =>
        {
            _settings.PrompterLook = look;
            _settings.Save(_settingsPath);
        };
        window.Saved += (text, step) =>
        {
            if (_settings.PrompterText == text && _settings.PrompterStep == step) return;
            _settings.PrompterText = text;
            _settings.PrompterStep = step;
            _settings.Save(_settingsPath);
        };
        window.Closed += (_, _) => PrompterWindow = null;
        PrompterWindow = window;
        window.Show();
        if (_prompterRunning) window.Start();
        return window;
    }

    internal void SyncPrompter(bool running)
    {
        if (running == _prompterRunning) return;
        _prompterRunning = running;
        if (PrompterWindow is not { } window) return;
        if (running) window.Start();
        else window.Stop();
    }
}
