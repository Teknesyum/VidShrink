param([int]$X,[int]$Y,[int]$Wait=1)
Add-Type @"
using System; using System.Runtime.InteropServices;
public class M { [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [StructLayout(LayoutKind.Sequential)] public struct R { public int L,T,Rt,B; } }
"@
$p = Get-Process VidShrink.App | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
[M]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
$r = New-Object M+R; [M]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
[M]::SetCursorPos($r.L+$X, $r.T+$Y) | Out-Null; Start-Sleep -Milliseconds 150
[M]::mouse_event(2,0,0,0,[UIntPtr]::Zero); [M]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
Start-Sleep -Seconds $Wait
