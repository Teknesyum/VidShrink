Option Explicit
Dim fso, sh, kok, proj, exe, dotnet
Set fso = CreateObject("Scripting.FileSystemObject")
Set sh  = CreateObject("WScript.Shell")

kok  = fso.GetParentFolderName(fso.GetParentFolderName(WScript.ScriptFullName))
proj = kok & "\src\VidShrink.App\VidShrink.App.csproj"
exe  = kok & "\src\VidShrink.App\bin\Release\net8.0\VidShrink.App.exe"

dotnet = ""
If fso.FileExists(sh.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\Microsoft\dotnet\dotnet.exe") Then
  dotnet = sh.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\Microsoft\dotnet\dotnet.exe"
ElseIf fso.FileExists("C:\Program Files\dotnet\dotnet.exe") Then
  dotnet = "C:\Program Files\dotnet\dotnet.exe"
End If

If dotnet <> "" And fso.FileExists(proj) Then
  sh.Run """" & dotnet & """ build -c Release """ & proj & """", 0, True
End If

If fso.FileExists(exe) Then
  sh.Run """" & exe & """", 1, False
Else
  MsgBox "VidShrink derlenmemis." & vbCrLf & exe, 16, "VidShrink"
End If
