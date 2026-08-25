using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Avalonia;

namespace VidShrink.Tests;

/// <summary>
/// Avalonia çalışma zamanını süreç başına bir kez, <b>kendi iş parçacığında</b> kurar.
///
/// Kurulum çağrıyı yapan iş parçacığını arayüz iş parçacığı ilan eder. xUnit ölçüm
/// sınıflarını havuzdaki herhangi bir iş parçacığında koşturduğu için kurulumu ilk
/// gören sınıf kimse arayüz iş parçacığı o olur ve sonraki sınıf başka bir iş
/// parçacığından denetim kurmaya kalkınca "Call from invalid thread" ile düşer —
/// koşudan koşuya değişen, koda bağlı olmayan bir kararsızlık.
///
/// Bu yüzden kurulum ayrı bir iş parçacığına alındı; Avalonia nesnesine dokunan ölçüm
/// <see cref="Run{T}"/> ile o iş parçacığına gönderilir ve hangi sınıfın önce koştuğu
/// artık sonucu değiştirmez.
/// </summary>
internal static class AppHost
{
    private static readonly object Gate = new();
    private static readonly BlockingCollection<Action> Queue = new();
    private static Thread? _host;
    private static bool _ready;

    internal static void Ensure()
    {
        lock (Gate)
        {
            if (_ready) return;

            var started = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                if (Application.Current is null)
                    AppBuilder.Configure<VidShrink.App.App>().UseSkia().UseWin32().SetupWithoutStarting();

                started.Set();

                foreach (var work in Queue.GetConsumingEnumerable()) work();
            })
            {
                IsBackground = true,
                Name = "avalonia-test-host"
            };

            if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            started.Wait();

            _host = thread;
            _ready = true;
        }
    }

    internal static T Run<T>(Func<T> work)
    {
        Ensure();

        if (Thread.CurrentThread == _host) return work();

        var done = new ManualResetEventSlim();
        T result = default!;
        ExceptionDispatchInfo? failure = null;

        Queue.Add(() =>
        {
            try { result = work(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
            finally { done.Set(); }
        });

        done.Wait();
        failure?.Throw();
        return result;
    }

    internal static void Run(Action work) => Run<object?>(() => { work(); return null; });
}
