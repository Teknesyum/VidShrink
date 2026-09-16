using System.Globalization;
using System.Text;
using VidShrink.Cli;

Console.OutputEncoding = new UTF8Encoding(false);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

return await CliApp.RunAsync(args, Console.Out, Console.Error, CliText.For(CultureInfo.CurrentUICulture), CliServices.Default, cts.Token);
