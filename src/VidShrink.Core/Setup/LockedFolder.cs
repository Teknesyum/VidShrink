using System.Diagnostics;
using System.Runtime.Versioning;

namespace VidShrink.Core.Setup;

public static class LockedFolder
{
    public const int Attempts = 6;

    public const int FirstDelayMilliseconds = 200;

    public static readonly TimeSpan HolderWait = TimeSpan.FromSeconds(120);

    private static readonly string[] HolderProcessNames = { "VidShrink.App", "VidShrink" };

    public static bool Holds(string? processPath, string root) =>
        !string.IsNullOrEmpty(processPath) && processPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);

    public static async Task RunAsync(string root, Action<string> operation, SetupHost host, CancellationToken cancellationToken)
    {
        var delay = FirstDelayMilliseconds;
        var waited = 0;
        var lastMessage = "";
        var holderRounds = 0;

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            if (TryRun(root, operation, ref lastMessage)) return;

            var holders = host.FindHolders(root);
            holderRounds = holders.Count > 0 ? holderRounds + 1 : 0;
            if (holderRounds >= 2)
            {
                host.Log(SetupText.Get("setup.lock.waiting", Names(holders), (int)host.HolderWait.TotalSeconds));
                foreach (var holder in holders)
                {
                    try { holder.Kill(); }
                    catch (Exception) { }
                }
                foreach (var holder in holders)
                {
                    try { holder.WaitForExit(host.HolderWait); }
                    catch (Exception) { }
                }

                var still = host.FindHolders(root);
                if (still.Count > 0)
                {
                    throw new SetupException(SetupText.Get(
                        "setup.lock.still-open", (int)host.HolderWait.TotalSeconds, Names(still), root));
                }

                holderRounds = 0;
                if (TryRun(root, operation, ref lastMessage)) return;
            }

            if (attempt < Attempts)
            {
                host.Log(SetupText.Get("setup.lock.retry", delay, attempt, Attempts));
                await host.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
                waited += delay;
                delay *= 2;
            }
        }

        throw new SetupException(SetupText.Get("setup.lock.failed", Attempts, waited, root, lastMessage));
    }

    public static void CloseHolders(string root, SetupHost host)
    {
        foreach (var holder in host.FindHolders(root))
        {
            try
            {
                holder.Kill();
                holder.WaitForExit(TimeSpan.FromSeconds(5));
            }
            catch (Exception) { }
        }
    }

    private static bool TryRun(string root, Action<string> operation, ref string lastMessage)
    {
        try
        {
            operation(root);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            lastMessage = exception.Message;
            return false;
        }
    }

    private static string Names(IEnumerable<IRootHolder> holders) =>
        string.Join(", ", holders.Select(h => $"{h.Name} (PID {h.Id})"));

    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<IRootHolder> FindProcesses(string root)
    {
        var found = new List<IRootHolder>();
        foreach (var name in HolderProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                string? path = null;
                try { path = process.MainModule?.FileName; }
                catch (Exception) { }
                if (Holds(path, root)) found.Add(new ProcessHolder(process));
                else process.Dispose();
            }
        }
        return found;
    }

    private sealed class ProcessHolder : IRootHolder
    {
        private readonly Process _process;

        public ProcessHolder(Process process) => _process = process;

        public string Name => _process.ProcessName;

        public int Id => _process.Id;

        public void Kill() => _process.Kill();

        public bool WaitForExit(TimeSpan timeout) => _process.WaitForExit(timeout);
    }
}
