using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VidShrink.App.Localization;
using VidShrink.Core;
using CoreShare = VidShrink.Core.Share;

namespace VidShrink.App.Share;

internal sealed class ShareSession
{
    private readonly Func<Func<ShareFlow>?> _factory;
    private ShareFlow? _flow;
    private CoreShare.ShareTargetTable? _targets;
    private CoreShare.IHttpTransport? _transport;

    internal ShareSession(Func<Func<ShareFlow>?> factory) => _factory = factory;

    internal CoreShare.ShareTargetTable? Targets => _targets;

    internal bool Running => _flow?.Running ?? false;

    internal void Cancel() => _flow?.Cancel();

    internal CoreShare.ShareTarget? Target(string? overrideId = null)
    {
        try { _targets ??= CoreShare.ShareTargetTable.Load(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
        return overrideId is null ? _targets.DefaultTarget : _targets.Find(overrideId) ?? _targets.DefaultTarget;
    }

    internal ShareFlow Flow() => _flow ??= _factory()?.Invoke() ?? new ShareFlow(target =>
        CoreShare.ShareProviderFactory.Create(
            target,
            _transport ??= new CoreShare.HttpClientTransport(),
            _targets));

    internal Task<CoreShare.ShareResult> ShareAsync(CoreShare.ShareTarget target, string path, IProgress<CoreShare.UploadProgress> progress)
    {
        var flow = Flow();
        return flow.ShareAsync(target, path, target.DefaultRetentionDays, progress);
    }

    internal static string MissingTargets() => Say("settings.share.targets-missing", CoreShare.ShareTargetTable.FileName);

    internal static string Uploading() => Say("settings.share.uploading");

    internal static string Nothing() => Say("settings.share.nothing");

    internal static string Status(CoreShare.ShareResult result)
    {
        if (result.Ok && result.Link is { } link)
            return link.ExpiresAt is { } expires
                ? Say("settings.share.shared-until", Bicim.Damga(expires, Strings.Culture))
                : Say("settings.share.shared");

        return result.Failure == CoreShare.ShareFailure.Cancelled
            ? Say("settings.share.cancelled")
            : $"{Say("settings.share.failed")}: {ShareMessage.Of(result)}";
    }

    private static string Say(string key, params object?[] args)
        => args.Length == 0
            ? LanguageCatalog.Display(Strings.Get(key))
            : string.Format(Strings.Culture, LanguageCatalog.Display(Strings.Get(key)), args);
}
