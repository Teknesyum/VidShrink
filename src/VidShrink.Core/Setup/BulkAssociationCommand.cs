using System.Text;

namespace VidShrink.Core.Setup;

public static class BulkAssociationCommand
{
    public const string ScriptUrl =
        "https://raw.githubusercontent.com/DanysysTeam/PS-SFTA/22a32292e576afc976a1167d92b50741ef523066/SFTA.ps1";

    public const string ScriptSha256 =
        "3eb6f6dee3fd8c91604042060b9d658f08ec85d3fd0a14769119dfd78bc30851";

    public const string ToolPage = "https://github.com/DanysysTeam/PS-SFTA";

    public static string Build(string progId, IReadOnlyList<string> extensions)
    {
        var list = new StringBuilder();
        for (var i = 0; i < extensions.Count; i++)
        {
            if (i > 0) list.Append(',');
            list.Append('\'').Append('.').Append(extensions[i]).Append('\'');
        }

        return string.Join(
            "; ",
            "Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force",
            $"$u='{ScriptUrl}'",
            $"$h='{ScriptSha256}'",
            "$p=Join-Path $env:TEMP 'VidShrink-SFTA.ps1'",
            "Invoke-WebRequest -Uri $u -OutFile $p",
            "Unblock-File $p",
            "if((Get-FileHash -Algorithm SHA256 $p).Hash -ne $h){Write-Error 'VidShrink: dosya butunlugu dogrulanamadi, islem durduruldu'; return}",
            $". $p",
            $"$e=@({list})",
            $"for($i=0;$i -lt $e.Count;$i++){{ Write-Progress -Activity 'VidShrink varsayilan atama' -Status $e[$i] -PercentComplete (($i+1)*100/$e.Count); Set-FTA '{progId}' $e[$i] }}",
            "Write-Progress -Activity 'VidShrink varsayilan atama' -Completed");
    }
}
