using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

/// <summary>
/// ARC-05: tarama panelinin "listeyi kaydet" düğmesi. Açık kaynağın son sessizlik/siyah kare
/// taraması ve son sahne taraması, plan uygulanmadan, zaman kodlu metin olarak yazılır; liste
/// medya bilgisi penceresinin denetim listesiyle aynı <see cref="AuditReport"/> biçimindedir.
/// </summary>
internal partial class EditorView
{
    private IReadOnlyList<double>? _sceneCuts;
    private string? _sceneSource;

    internal Func<string, Task<string?>>? AuditSavePicker { get; set; }

    internal IReadOnlyList<AuditEntry> AuditEntries => AuditReport.Build(
        CurrentMedia.SamePath(_silenceSource, _source) ? _silenceScan : null,
        CurrentMedia.SamePath(_sceneSource, _source) ? _sceneCuts : null);

    private bool HasAuditList
        => _source is not null
           && ((_silenceScan is not null && CurrentMedia.SamePath(_silenceSource, _source))
               || (_sceneCuts is not null && CurrentMedia.SamePath(_sceneSource, _source)));

    internal async Task<bool> SaveAuditListAsync()
    {
        if (!HasAuditList || _source is not { } source) return false;
        var entries = AuditEntries;
        try
        {
            var picker = AuditSavePicker ?? (name => MediaInfoWindow.PickTextAsync(TopLevel.GetTopLevel(this), name));
            if (await picker(Path.GetFileNameWithoutExtension(source) + ".txt").ConfigureAwait(true) is not { } target) return false;
            var text = entries.Count == 0
                ? Strings.Get("mediainfo.audit.none") + Environment.NewLine
                : AuditReport.Text(entries, MediaInfoWindow.KindName);
            File.WriteAllText(target, text, new UTF8Encoding(false));
            TxtSilenceStatus.Text = Strings.Get("mediainfo.saved", target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TxtSilenceStatus.Text = Strings.Get("editor.silence.failed", ex.Message);
            return false;
        }
    }

    private void RememberScenes(string source, IReadOnlyList<double> cuts)
    {
        _sceneCuts = cuts;
        _sceneSource = source;
    }

    private void RefreshAuditSave()
        => BtnSilenceSaveList.IsEnabled = HasAuditList && !SilenceScanning;
}
