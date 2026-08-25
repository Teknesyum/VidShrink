---
name: vidshrink-ekran-disi-kosu
description: Ekran kapısı kapalıyken gerçek MainWindow'u sürmek — ClassicDesktopStyleApplicationLifetime + SetupWithLifetime, Show'dan önce pozisyon -32000
metadata:
  type: project
---

Ekran kapısı hook'u bin/Release altındaki exe'leri engelliyor ama `--offscreen` gibi
başsız bayraklı komutları geçiriyor (BASSIZ regex'i). Gerçek `VidShrink.App.App` +
`MainWindow` süren deneme exe'si şöyle kurulur:

1. `new ClassicDesktopStyleApplicationLifetime { ShutdownMode = OnExplicitShutdown }`,
   `AppBuilder...SetupWithLifetime(lifetime)` — `StartWithClassicDesktopLifetime` yerine.
   Setup sonrası pencere kurulmuş ama gösterilmemiş: `Position=(-32000,-32000)`,
   `ShowActivated=false`, `ShowInTaskbar=false`, `WindowState=Normal` ver, sonra
   `lifetime.Start(args)`.
2. `AfterSetup` içinde async iş `Dispatcher.UIThread.Post` ile atılır; adımlar bitince
   `lifetime.Shutdown()`.
3. Arayüz kilidi teşhisi için ayrı thread'de bekçi: `Dispatcher.UIThread.InvokeAsync(()=>{})
   .GetTask().Wait(500)` false ise UI tıkalı. Canlı log dosyaya `File.AppendAllText`.
4. ffmpeg sayarken adıyla sayma — uygulamanın örnek kodlaması da ffmpeg açar. WMI
   `Win32_Process.CommandLine` içinde `hstack` arayarak yalnız karşılaştırma borusu sayılır
   (System.Management paketi).
