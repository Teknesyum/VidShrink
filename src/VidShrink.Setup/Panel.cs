using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using VidShrink.Core;
using VidShrink.Core.Setup;
using static VidShrink.Setup.Native;

namespace VidShrink.Setup;

/// <summary>
/// Grafik kurulum paneli: çerçevesiz 720x540 pencere, yerli Win32 + GDI/GDI+. Düzen
/// AmeliyatListe kurulumunun aynısı: beş adım, gradyan çubuk ve yüzde, solan günlük,
/// kurulum yeri satırı, Kur → Kuruluyor → Kapat + Programı Aç. İş
/// <see cref="Task.Run(Func{Task})"/> üstünde, pencere ana iş parçacığında; ikisi yalnız
/// <see cref="SetupPanelProgress"/> ile konuşur.
/// </summary>
internal sealed class Panel
{
    private const int Width = 720;
    private const int Height = 540;
    private const int Margin = 28;
    private const int StepTop = 92;
    private const int StepHeight = 42;
    private const int Circle = 26;
    private const int BarTop = 314;
    private const int BarHeight = 8;
    private const int PercentWidth = 52;
    private const int LogTop = 334;
    private const int LogLineHeight = 15;
    private const int LogPad = 6;
    private const int FadedLines = 3;
    private const int FooterTop = 492;
    private const int ButtonWidth = 150;
    private const int ButtonHeight = 34;
    private const int ButtonGap = 12;
    private const int CloseSize = 28;
    private const int SweepWidth = 140;
    private const double ShimmerStep = 0.012;
    private const int ChangeTarget = 100;
    private const int CloseTarget = 101;
    private const uint PickedMessage = WM_APP + 1;
    private static readonly UIntPtr TimerId = new(1);
    private static readonly string ClassName = "VidShrinkSetupPanel";

    private const uint Background = 0xFF08090A;
    private const uint Glass = 0xD90A0A0F;
    private const uint Text = 0xFFFFFFFF;
    private const uint Cyan = 0xFF00F3FF;
    private const uint Purple = 0xFFB026FF;
    private const uint Pink = 0xFFFF00EA;
    private const uint PinkText = 0xFFFF54EB;
    private const uint Success = 0xFF34D399;
    private const uint Muted = 0xFF71717A;
    private const uint Edge = 0x9900F3FF;
    private const uint Hairline = 0x3300F3FF;

    private static Panel? current;

    private readonly string logPath;
    private readonly bool rehearsal;
    private readonly Func<Action<string>, Action<int, int, string>, SetupHost> createHost;
    private readonly Action<string> sink;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private SetupOptions options;
    private SetupPanelProgress progress;
    private IntPtr window;
    private double scale = 1;
    private IntPtr titleFont, nameFont, detailFont, smallFont, logFont, circleFont, buttonFont, linkFont, icon;
    private TimeSpan lastTick;
    private double shimmer;
    private bool animate = true;
    private SetupPanelPhase phase = SetupPanelPhase.Ready;
    private SetupPanelSnapshot snapshot;
    private SetupPanelChoice choice = SetupPanelChoice.None;
    private int focus = -1;
    private int pressed = -1;
    private volatile bool picking;
    private volatile string? picked;
    private volatile string? launchTarget;

    private Panel(SetupOptions options, string logPath, bool rehearsal, Func<Action<string>, Action<int, int, string>, SetupHost> createHost)
    {
        this.options = options;
        this.logPath = logPath;
        this.rehearsal = rehearsal;
        this.createHost = createHost;
        sink = line => File.AppendAllText(logPath, line + Environment.NewLine);
        progress = new SetupPanelProgress(sink);
        snapshot = progress.Read();
        Choose(SetupPanelPhase.Ready);
    }

    public static int Run(SetupOptions options, bool rehearsal, string logPath, Func<Action<string>, Action<int, int, string>, SetupHost> createHost)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        var panel = new Panel(options, logPath, rehearsal, createHost);
        current = panel;

        var input = new GdiplusStartupInput { GdiplusVersion = 1 };
        GdiplusStartup(out var gdiplus, ref input, IntPtr.Zero);
        try
        {
            panel.Create();
            if (rehearsal) panel.progress.Log(SetupText.Get("setup.panel.rehearsal", options.InstallRoot));
            while (GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessageW(ref message);
            }
        }
        finally
        {
            panel.ReleaseResources();
            GdiplusShutdown(gdiplus);
        }

        return panel.phase == SetupPanelPhase.Done ? 0 : 1;
    }

    private void Start()
    {
        if (picking || phase is not (SetupPanelPhase.Ready or SetupPanelPhase.Failed)) return;
        if (phase == SetupPanelPhase.Failed) progress = new SetupPanelProgress(sink);
        var work = progress;
        var chosen = options;
        var host = createHost(work.Log, work.Step);
        snapshot = work.Read();
        Choose(SetupPanelPhase.Running);
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await SetupRunner.InstallAsync(chosen, host, CancellationToken.None);
                launchTarget = Path.Combine(result.InstallRoot, LauncherUpdate.ExecutableName);
                var done = SetupText.Get("setup.step.done", result.Version);
                work.Log(done);
                work.Complete(done);
            }
            catch (Exception exception)
            {
                work.Fail(SetupText.Get("setup.panel.failed", exception.Message));
                work.Log(SetupText.Get("setup.panel.log-path", logPath));
            }
        });
    }

    private void Choose(SetupPanelPhase next)
    {
        phase = next;
        choice = SetupPanelChoices.For(next, rehearsal, launchTarget is not null);
        focus = choice.Focus is { } wanted ? IndexOf(wanted) : -1;
        if (window != IntPtr.Zero) InvalidateRect(window, IntPtr.Zero, 0);
    }

    private unsafe void Create()
    {
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4));
        }
        catch (EntryPointNotFoundException)
        {
        }

        try
        {
            scale = GetDpiForSystem() / 96.0;
        }
        catch (EntryPointNotFoundException)
        {
            scale = 1;
        }

        animate = SystemParametersInfoFlag(SPI_GETCLIENTAREAANIMATION, 0, out var enabled, 0) == 0 || enabled != 0;

        var instance = GetModuleHandleW(IntPtr.Zero);
        var classIcons = new IntPtr[2];
        var exe = Environment.ProcessPath ?? "";
        PrivateExtractIconsW(exe, 0, 32, 32, classIcons, null, 1, 0);
        var smallIcons = new IntPtr[1];
        PrivateExtractIconsW(exe, 0, 16, 16, smallIcons, null, 1, 0);
        var name = Marshal.StringToHGlobalUni(ClassName);
        var windowClass = new WNDCLASSEXW
        {
            cbSize = (uint)sizeof(WNDCLASSEXW),
            style = CS_HREDRAW | CS_VREDRAW,
            lpfnWndProc = &WindowProcedure,
            hInstance = instance,
            hIcon = classIcons[0],
            hIconSm = smallIcons[0],
            hCursor = LoadCursorW(IntPtr.Zero, new IntPtr(IDC_ARROW)),
            lpszClassName = name
        };
        RegisterClassExW(ref windowClass);

        CreateResources();
        SystemParametersInfoW(SPI_GETWORKAREA, 0, out var area, 0);
        var width = Scaled(Width);
        var height = Scaled(Height);
        var x = area.Left + (area.Right - area.Left - width) / 2;
        var y = area.Top + (area.Bottom - area.Top - height) / 2;
        var title = "VidShrink " + SetupText.Get("setup.panel.title");
        window = CreateWindowExW(WS_EX_APPWINDOW, ClassName, title, WS_POPUP | WS_MINIMIZEBOX | WS_SYSMENU,
            x, y, width, height, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        ShowWindow(window, SW_SHOW);
        SetForegroundWindow(window);
        UpdateWindow(window);
        lastTick = clock.Elapsed;
        SetTimer(window, TimerId, InstallProgress.FrameMilliseconds, IntPtr.Zero);
    }

    private int Scaled(double value) => (int)Math.Round(value * scale);

    private float Px(double value) => (float)(value * scale);

    private void CreateResources()
    {
        titleFont = Font(22, FW_BOLD, "Segoe UI");
        nameFont = Font(15, 600, "Segoe UI");
        detailFont = Font(13, FW_NORMAL, "Segoe UI");
        smallFont = Font(13, FW_NORMAL, "Segoe UI");
        logFont = Font(13, FW_NORMAL, "Consolas");
        circleFont = Font(13, FW_BOLD, "Consolas");
        buttonFont = Font(14, 600, "Segoe UI");
        linkFont = CreateFontW(-Scaled(13), 0, 0, 0, 600, 0, 1, 0, 1, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        var icons = new IntPtr[1];
        if (PrivateExtractIconsW(Environment.ProcessPath ?? "", 0, Scaled(40), Scaled(40), icons, null, 1, 0) == 1) icon = icons[0];
    }

    private IntPtr Font(int pixels, int weight, string face) =>
        CreateFontW(-Scaled(pixels), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, CLEARTYPE_QUALITY, 0, face);

    private void ReleaseResources()
    {
        foreach (var font in new[] { titleFont, nameFont, detailFont, smallFont, logFont, circleFont, buttonFont, linkFont })
        {
            if (font != IntPtr.Zero) DeleteObject(font);
        }

        titleFont = nameFont = detailFont = smallFont = logFont = circleFont = buttonFont = linkFont = IntPtr.Zero;
        if (icon != IntPtr.Zero) DestroyIcon(icon);
        icon = IntPtr.Zero;
    }

    [UnmanagedCallersOnly]
    private static IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (current is { } panel && (panel.window == IntPtr.Zero || panel.window == hwnd))
            {
                if (panel.Handle(hwnd, message, wParam, lParam) is { } handled) return handled;
            }
        }
        catch (Exception)
        {
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private static (int X, int Y) Point(IntPtr lParam) =>
        ((short)((long)lParam & 0xFFFF), (short)(((long)lParam >> 16) & 0xFFFF));

    private unsafe IntPtr? Handle(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WM_ERASEBKGND:
                return new IntPtr(1);
            case WM_PAINT:
                Paint(hwnd);
                return IntPtr.Zero;
            case WM_PRINTCLIENT:
                PaintInto(hwnd, wParam);
                return IntPtr.Zero;
            case WM_TIMER:
                Tick();
                return IntPtr.Zero;
            case WM_NCHITTEST:
            {
                var (sx, sy) = Point(lParam);
                var point = new POINT { X = sx, Y = sy };
                ScreenToClient(hwnd, ref point);
                return new IntPtr(HitTarget(point.X, point.Y) >= 0 ? HTCLIENT : HTCAPTION);
            }
            case WM_SETCURSOR:
            {
                GetCursorPos(out var point);
                ScreenToClient(hwnd, ref point);
                if (HitTarget(point.X, point.Y) < 0) return null;
                SetCursor(LoadCursorW(IntPtr.Zero, new IntPtr(IDC_HAND)));
                return new IntPtr(1);
            }
            case WM_LBUTTONDOWN:
            {
                var (x, y) = Point(lParam);
                pressed = HitTarget(x, y);
                return IntPtr.Zero;
            }
            case WM_LBUTTONUP:
            {
                var (x, y) = Point(lParam);
                var hit = HitTarget(x, y);
                var wasPressed = pressed;
                pressed = -1;
                if (hit >= 0 && hit == wasPressed) ActivateTarget(hit);
                return IntPtr.Zero;
            }
            case WM_KEYDOWN:
                Key((int)wParam);
                return IntPtr.Zero;
            case PickedMessage:
                Picked();
                return IntPtr.Zero;
            case WM_CLOSE:
                if (SetupPanelChoices.CanClose(phase) && !picking) DestroyWindow(hwnd);
                return IntPtr.Zero;
            case WM_DPICHANGED:
            {
                scale = ((int)wParam & 0xFFFF) / 96.0;
                ReleaseResources();
                CreateResources();
                var suggested = *(RECT*)lParam;
                SetWindowPos(hwnd, IntPtr.Zero, suggested.Left, suggested.Top, suggested.Right - suggested.Left, suggested.Bottom - suggested.Top, SWP_NOZORDER | SWP_NOACTIVATE);
                InvalidateRect(hwnd, IntPtr.Zero, 0);
                return IntPtr.Zero;
            }
            case WM_DESTROY:
                KillTimer(hwnd, TimerId);
                PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return null;
        }
    }

    private void Tick()
    {
        var now = clock.Elapsed;
        snapshot = progress.Advance(now - lastTick);
        lastTick = now;
        if (animate) shimmer += ShimmerStep;
        if (phase == SetupPanelPhase.Running && snapshot.State != InstallState.Running) Choose(SetupPanelStages.PhaseOf(snapshot.State));
        InvalidateRect(window, IntPtr.Zero, 0);
    }

    private int IndexOf(SetupPanelButton button)
    {
        for (var i = 0; i < choice.Buttons.Count; i++)
        {
            if (choice.Buttons[i] == button) return i;
        }

        return -1;
    }

    private void Key(int key)
    {
        var enabled = Enumerable.Range(0, choice.Buttons.Count).Where(i => SetupPanelChoices.Enabled(choice.Buttons[i])).ToArray();
        switch (key)
        {
            case VK_TAB or VK_LEFT or VK_RIGHT when enabled.Length > 0:
            {
                var at = Array.IndexOf(enabled, focus);
                var next = at < 0 ? 0 : (at + (key == VK_LEFT ? enabled.Length - 1 : 1)) % enabled.Length;
                focus = enabled[next];
                InvalidateRect(window, IntPtr.Zero, 0);
                break;
            }
            case VK_RETURN or VK_SPACE when enabled.Contains(focus):
                Activate(choice.Buttons[focus]);
                break;
            case VK_ESCAPE when SetupPanelChoices.CanClose(phase) && !picking:
                DestroyWindow(window);
                break;
        }
    }

    private void ActivateTarget(int target)
    {
        switch (target)
        {
            case ChangeTarget:
                PickLocation();
                break;
            case CloseTarget:
                DestroyWindow(window);
                break;
            default:
                Activate(choice.Buttons[target]);
                break;
        }
    }

    private void Activate(SetupPanelButton button)
    {
        if (picking) return;
        switch (button)
        {
            case SetupPanelButton.Install or SetupPanelButton.Retry:
                Start();
                break;
            case SetupPanelButton.OpenApp when launchTarget is { } target:
                try
                {
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) })?.Dispose();
                }
                catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                }

                DestroyWindow(window);
                break;
            case SetupPanelButton.OpenLog:
                try
                {
                    var start = new ProcessStartInfo("notepad.exe") { UseShellExecute = true };
                    start.ArgumentList.Add(logPath);
                    Process.Start(start)?.Dispose();
                }
                catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                }

                break;
            case SetupPanelButton.Close:
                DestroyWindow(window);
                break;
        }
    }

    private void PickLocation()
    {
        if (picking || !SetupPanelChoices.CanChangeLocation(phase)) return;
        picking = true;
        var owner = window;
        var root = SetupPanelLocation.ProgramsOf(options.LocalAppData);
        var title = SetupText.Get("setup.panel.pick-folder");
        var thread = new Thread(() =>
        {
            picked = BrowseFolder(owner, root, title);
            PostMessageW(owner, PickedMessage, IntPtr.Zero, IntPtr.Zero);
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private void Picked()
    {
        picking = false;
        var chosen = picked;
        picked = null;
        if (chosen is null) return;
        if (SetupPanelLocation.Resolve(chosen, options.LocalAppData) is { } root)
        {
            options = options with { InstallRoot = root };
        }
        else
        {
            progress.Log(SetupText.Get("setup.panel.location-invalid", SetupPanelLocation.ProgramsOf(options.LocalAppData)));
        }

        InvalidateRect(window, IntPtr.Zero, 0);
    }

    private static string? BrowseFolder(IntPtr owner, string root, string title)
    {
        var rootList = Directory.Exists(root) ? ILCreateFromPathW(root) : IntPtr.Zero;
        var display = Marshal.AllocHGlobal(MAX_PATH * 2);
        var path = Marshal.AllocHGlobal(MAX_PATH * 2);
        var caption = Marshal.StringToHGlobalUni(title);
        try
        {
            var info = new BROWSEINFOW
            {
                hwndOwner = owner,
                pidlRoot = rootList,
                pszDisplayName = display,
                lpszTitle = caption,
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE
            };
            var list = SHBrowseForFolderW(ref info);
            if (list == IntPtr.Zero) return null;
            try
            {
                return SHGetPathFromIDListW(list, path) != 0 ? Marshal.PtrToStringUni(path) : null;
            }
            finally
            {
                CoTaskMemFree(list);
            }
        }
        finally
        {
            if (rootList != IntPtr.Zero) ILFree(rootList);
            Marshal.FreeHGlobal(display);
            Marshal.FreeHGlobal(path);
            Marshal.FreeHGlobal(caption);
        }
    }

    private (int X, int Y, int W, int H) ButtonRect(int index)
    {
        var count = choice.Buttons.Count;
        var x = Width - Margin - (count - index) * ButtonWidth - (count - 1 - index) * ButtonGap;
        return (x, FooterTop, ButtonWidth, ButtonHeight);
    }

    private (int X, int Y, int W, int H) CloseRect => (Width - 16 - CloseSize, 16, CloseSize, CloseSize);

    private int changeLeft, changeWidth;

    private int HitTarget(int x, int y)
    {
        bool Inside((int X, int Y, int W, int H) r) =>
            x >= Scaled(r.X) && x < Scaled(r.X + r.W) && y >= Scaled(r.Y) && y < Scaled(r.Y + r.H);

        for (var i = 0; i < choice.Buttons.Count; i++)
        {
            if (SetupPanelChoices.Enabled(choice.Buttons[i]) && Inside(ButtonRect(i))) return i;
        }

        if (SetupPanelChoices.CanClose(phase) && Inside(CloseRect)) return CloseTarget;
        if (SetupPanelChoices.CanChangeLocation(phase) && changeWidth > 0
            && x >= changeLeft && x < changeLeft + changeWidth && y >= Scaled(FooterTop) && y < Scaled(FooterTop + ButtonHeight))
        {
            return ChangeTarget;
        }

        return -1;
    }

    private void Paint(IntPtr hwnd)
    {
        var target = BeginPaint(hwnd, out var paint);
        try
        {
            PaintInto(hwnd, target);
        }
        finally
        {
            EndPaint(hwnd, ref paint);
        }
    }

    private void PaintInto(IntPtr hwnd, IntPtr target)
    {
        GetClientRect(hwnd, out var client);
        var width = client.Right;
        var height = client.Bottom;
        var memory = CreateCompatibleDC(target);
        var bitmap = CreateCompatibleBitmap(target, width, height);
        var previous = SelectObject(memory, bitmap);
        try
        {
            Draw(memory, width, height);
            BitBlt(target, 0, 0, width, height, memory, 0, 0, SRCCOPY);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(bitmap);
            DeleteDC(memory);
        }
    }

    private static uint CircleColor(SetupStageState state) => state switch
    {
        SetupStageState.Running => Cyan,
        SetupStageState.Done => Success,
        SetupStageState.Failed => Pink,
        _ => Edge
    };

    private static uint NameColor(SetupStageState state) => state switch
    {
        SetupStageState.Running => Text,
        SetupStageState.Done => Cyan,
        SetupStageState.Failed => PinkText,
        _ => Muted
    };

    private void Draw(IntPtr dc, int width, int height)
    {
        Fill(dc, new RECT(0, 0, width, height), Background);

        var skip = options.SkipShortcuts;
        var states = SetupPanelStages.States(phase, snapshot.Percent, skip);
        var stages = SetupPanelStages.All;
        var bar = phase == SetupPanelPhase.Done ? 100 : snapshot.Bar;
        var pulse = animate ? 0.65 + 0.35 * Math.Sin(shimmer * Math.PI * 4) : 1;

        GdipCreateFromHDC(dc, out var graphics);
        try
        {
            var edge = (float)scale;
            SolidRect(graphics, Edge, 0, 0, width, edge);
            SolidRect(graphics, Edge, 0, height - edge, width, edge);
            SolidRect(graphics, Edge, 0, edge, edge, height - 2 * edge);
            SolidRect(graphics, Edge, width - edge, edge, edge, height - 2 * edge);

            GdipSetSmoothingMode(graphics, SmoothingModeAntiAlias);
            for (var i = 0; i < states.Count; i++) DrawCircle(graphics, states[i], i, pulse);
            DrawCloseGlyph(graphics);
            GdipSetSmoothingMode(graphics, SmoothingModeDefault);

            DrawBar(graphics, bar);
            SolidRect(graphics, Glass, Px(Margin), Px(LogTop), Px(Width - 2 * Margin), Px(SetupPanelProgress.VisibleLines * LogLineHeight + 2 * LogPad));
            SolidRect(graphics, Hairline, Px(Margin), Px(LogTop), Px(Width - 2 * Margin), edge);
        }
        finally
        {
            GdipDeleteGraphics(graphics);
        }

        SetBkMode(dc, TRANSPARENT);
        DrawHeader(dc, width);

        for (var i = 0; i < states.Count; i++)
        {
            var top = StepTop + i * StepHeight;
            var state = states[i];
            var mark = state switch
            {
                SetupStageState.Failed => "!",
                SetupStageState.Skipped => "–",
                SetupStageState.Done => "",
                _ => (i + 1).ToString(CultureInfo.InvariantCulture)
            };
            var markColor = state switch
            {
                SetupStageState.Failed => Text,
                SetupStageState.Running => Text,
                _ => Muted
            };
            if (mark.Length > 0)
            {
                var circle = new RECT(Scaled(Margin), Scaled(top + (StepHeight - Circle) / 2), Scaled(Margin + Circle), Scaled(top + (StepHeight + Circle) / 2));
                DrawCentered(dc, circleFont, mark, markColor, circle);
            }

            var left = Scaled(Margin + Circle + 16);
            var detail = state == SetupStageState.Running && snapshot.Step.Length > 0 ? snapshot.Step : stages[i].Detail;
            DrawLine(dc, nameFont, stages[i].Name, NameColor(state), left, Scaled(top + 3), width - left - Scaled(Margin), Scaled(20));
            DrawLine(dc, detailFont, detail, state == SetupStageState.Pending || state == SetupStageState.Skipped ? Dim(Muted, 0.75) : Muted,
                left, Scaled(top + 23), width - left - Scaled(Margin), Scaled(18));
        }

        var percentText = ((int)Math.Floor(bar)).ToString(CultureInfo.InvariantCulture) + "%";
        var percentRect = new RECT(width - Scaled(Margin + PercentWidth), Scaled(BarTop - 6), width - Scaled(Margin), Scaled(BarTop + BarHeight + 6));
        DrawAligned(dc, logFont, percentText, phase == SetupPanelPhase.Failed ? PinkText : Cyan, percentRect, DT_RIGHT);

        DrawLog(dc);
        DrawFooter(dc, width);
        for (var i = 0; i < choice.Buttons.Count; i++) DrawButton(dc, i, i == focus);
    }

    private void DrawHeader(IntPtr dc, int width)
    {
        if (icon != IntPtr.Zero) DrawIconEx(dc, Scaled(Margin), Scaled(24), icon, Scaled(40), Scaled(40), 0, IntPtr.Zero, DI_NORMAL);
        const string product = "VidShrink";
        var titleLeft = Scaled(Margin + 54);
        var productWidth = TextWidth(dc, titleFont, product);
        DrawLine(dc, titleFont, product, Text, titleLeft, Scaled(20), productWidth + Scaled(4), Scaled(30));
        DrawLine(dc, titleFont, SetupText.Get("setup.panel.title"), Cyan, titleLeft + productWidth + Scaled(7), Scaled(20), Scaled(300), Scaled(30));

        var (subtitle, color) = phase switch
        {
            SetupPanelPhase.Running => (SetupText.Get("setup.panel.running"), Text),
            SetupPanelPhase.Done when rehearsal => (SetupText.Get("setup.panel.rehearsal-done"), Success),
            SetupPanelPhase.Done => (SetupText.Get("setup.panel.done"), Success),
            SetupPanelPhase.Failed => (snapshot.Step, PinkText),
            _ => (SetupText.Get("setup.panel.ready"), Muted)
        };
        DrawLine(dc, smallFont, subtitle, color, titleLeft, Scaled(50), width - titleLeft - Scaled(Margin + CloseSize + 8), Scaled(18));
    }

    private void DrawCircle(IntPtr graphics, SetupStageState state, int index, double pulse)
    {
        var size = Px(Circle);
        var x = Px(Margin);
        var y = Px(StepTop + index * StepHeight + (StepHeight - Circle) / 2.0);
        var color = CircleColor(state);

        if (state is SetupStageState.Running or SetupStageState.Failed)
        {
            for (var k = 1; k <= 4; k++)
            {
                var grow = Px(k * 2);
                var alpha = (uint)(0x30 * (state == SetupStageState.Running ? pulse : 1) / k) << 24;
                GdipCreateSolidFill(alpha | (color & 0x00FFFFFF), out var glow);
                GdipFillEllipse(graphics, glow, x - grow, y - grow, size + 2 * grow, size + 2 * grow);
                GdipDeleteBrush(glow);
            }
        }

        if (state is SetupStageState.Done or SetupStageState.Failed)
        {
            GdipCreateSolidFill(color, out var fill);
            GdipFillEllipse(graphics, fill, x, y, size, size);
            GdipDeleteBrush(fill);
        }
        else
        {
            GdipCreateSolidFill(state == SetupStageState.Running ? 0x2600F3FFu : Glass, out var fill);
            GdipFillEllipse(graphics, fill, x, y, size, size);
            GdipDeleteBrush(fill);
            GdipCreatePen1(state == SetupStageState.Skipped ? Hairline : color, Px(state == SetupStageState.Running ? 1.5 : 1), 0, out var ring);
            GdipDrawEllipse(graphics, ring, x, y, size, size);
            GdipDeletePen(ring);
        }

        if (state == SetupStageState.Done)
        {
            var cx = x + size / 2;
            var cy = y + size / 2;
            var r = size / 2;
            GdipCreatePen1(Background, Px(2), 0, out var tick);
            GdipSetPenLineCap197819(tick, LineCapRound, LineCapRound, LineCapRound);
            GdipDrawLine(graphics, tick, cx - 0.42f * r, cy + 0.02f * r, cx - 0.12f * r, cy + 0.32f * r);
            GdipDrawLine(graphics, tick, cx - 0.12f * r, cy + 0.32f * r, cx + 0.45f * r, cy - 0.3f * r);
            GdipDeletePen(tick);
        }
    }

    private void DrawCloseGlyph(IntPtr graphics)
    {
        var (x, y, w, h) = CloseRect;
        var inset = 9.0;
        var color = SetupPanelChoices.CanClose(phase) ? Muted : Dim(Muted, 0.3);
        GdipCreatePen1(color, Px(1.5), 0, out var pen);
        GdipSetPenLineCap197819(pen, LineCapRound, LineCapRound, LineCapRound);
        GdipDrawLine(graphics, pen, Px(x + inset), Px(y + inset), Px(x + w - inset), Px(y + h - inset));
        GdipDrawLine(graphics, pen, Px(x + w - inset), Px(y + inset), Px(x + inset), Px(y + h - inset));
        GdipDeletePen(pen);
    }

    private void DrawBar(IntPtr graphics, double value)
    {
        var bx = Px(Margin);
        var by = Px(BarTop);
        var bw = Px(Width - 2 * Margin - PercentWidth - 12);
        var bh = Px(BarHeight);
        SolidRect(graphics, Glass, bx, by, bw, bh);
        GdipCreatePen1(Hairline, Px(1), 0, out var outline);
        GdipDrawRectangle(graphics, outline, bx, by, bw, bh);
        GdipDeletePen(outline);

        var filled = (float)Math.Round(bw * Math.Clamp(value, 0, 100) / 100);
        if (filled <= 1) return;

        for (var i = 1; i <= 3; i++)
        {
            var grow = Px(i * 1.5);
            var glowColor = phase == SetupPanelPhase.Failed ? Pink : Purple;
            SolidRect(graphics, ((uint)(0x4D / i) << 24) | (glowColor & 0x00FFFFFF), bx, by - grow, filled, bh + 2 * grow);
        }

        if (phase == SetupPanelPhase.Failed)
        {
            SolidRect(graphics, Pink, bx, by, filled, bh);
            return;
        }

        var track = new RectF(bx - 1, by, bw + 2, bh);
        GdipCreateLineBrushFromRect(ref track, Cyan, Pink, 0, 1, out var gradient);
        GdipSetLinePresetBlend(gradient, new[] { Cyan, Purple, Pink }, new[] { 0f, 0.5f, 1f }, 3);
        GdipFillRectangle(graphics, gradient, bx, by, filled, bh);
        GdipDeleteBrush(gradient);

        if (phase != SetupPanelPhase.Running || !animate) return;
        var sweep = Px(SweepWidth);
        var px = bx + (float)(shimmer % 1.0) * (filled + sweep) - sweep;
        var light = new RectF(px, by, sweep, bh);
        GdipCreateLineBrushFromRect(ref light, 0x00FFFFFF, 0x00FFFFFF, 0, 1, out var shine);
        GdipSetLinePresetBlend(shine, new[] { 0x00FFFFFFu, 0x8CFFFFFFu, 0x00FFFFFFu }, new[] { 0f, 0.5f, 1f }, 3);
        GdipSetClipRect(graphics, bx, by, filled, bh, 0);
        GdipFillRectangle(graphics, shine, px, by, sweep, bh);
        GdipResetClip(graphics);
        GdipDeleteBrush(shine);
    }

    private void DrawLog(IntPtr dc)
    {
        var lines = snapshot.Lines;
        var slots = SetupPanelProgress.VisibleLines;
        var left = Scaled(Margin + 12);
        var right = Scaled(Width - Margin - 12);
        for (var i = 0; i < lines.Count; i++)
        {
            var slot = slots - lines.Count + i;
            var last = i == lines.Count - 1;
            var failure = phase == SetupPanelPhase.Failed && snapshot.Step.Length > 0 && lines[i].EndsWith(snapshot.Step, StringComparison.Ordinal);
            var color = failure ? PinkText : last ? Text : Cyan;
            if (slot < FadedLines) color = Dim(color, Math.Pow((slot + 1) / (double)(FadedLines + 1), 1.5));
            DrawLine(dc, logFont, lines[i], color, left, Scaled(LogTop + LogPad + slot * LogLineHeight), right - left, Scaled(LogLineHeight));
        }
    }

    private void DrawFooter(IntPtr dc, int width)
    {
        var top = Scaled(FooterTop);
        var rowHeight = Scaled(ButtonHeight);
        var label = SetupText.Get("setup.panel.location");
        var left = Scaled(Margin);
        var labelWidth = TextWidth(dc, smallFont, label);
        DrawAligned(dc, smallFont, label, Muted, new RECT(left, top, left + labelWidth + Scaled(2), top + rowHeight), DT_LEFT);

        var buttonsLeft = Scaled(ButtonRect(0).X) - Scaled(16);
        var change = SetupText.Get("setup.panel.change");
        var canChange = SetupPanelChoices.CanChangeLocation(phase);
        var changeText = canChange ? TextWidth(dc, linkFont, change) : 0;
        var pathLeft = left + labelWidth + Scaled(8);
        var pathRight = buttonsLeft - (canChange ? changeText + Scaled(14) : 0);
        var pathRect = new RECT(pathLeft, top, Math.Max(pathLeft, pathRight), top + rowHeight);
        DrawAligned(dc, smallFont, options.InstallRoot, Text, pathRect, DT_LEFT | DT_PATH_ELLIPSIS);

        if (canChange)
        {
            var used = Math.Min(TextWidth(dc, smallFont, options.InstallRoot), pathRect.Right - pathRect.Left);
            changeLeft = pathLeft + used + Scaled(12);
            changeWidth = changeText + Scaled(2);
            DrawAligned(dc, linkFont, change, picking ? Muted : Cyan, new RECT(changeLeft, top, changeLeft + changeWidth, top + rowHeight), DT_LEFT);
        }
        else
        {
            changeWidth = 0;
        }
    }

    private void DrawButton(IntPtr dc, int index, bool focused)
    {
        var button = choice.Buttons[index];
        var (x, y, w, h) = ButtonRect(index);
        var rect = new RECT(Scaled(x), Scaled(y), Scaled(x + w), Scaled(y + h));
        var enabled = SetupPanelChoices.Enabled(button);
        var primary = choice.Primary == button && enabled;
        var line = Math.Max(1, Scaled(1));
        if (primary)
        {
            Fill(dc, rect, Cyan);
        }
        else
        {
            Fill(dc, rect, Background);
            Outline(dc, rect, line, enabled ? Cyan : Muted);
        }

        if (focused)
        {
            var gap = Scaled(3);
            Outline(dc, new RECT(rect.Left - gap, rect.Top - gap, rect.Right + gap, rect.Bottom + gap), line, Text);
        }

        var label = SetupText.Get(button switch
        {
            SetupPanelButton.Install => "setup.panel.install",
            SetupPanelButton.Installing => "setup.panel.installing",
            SetupPanelButton.Retry => "setup.panel.retry",
            SetupPanelButton.OpenApp => "setup.panel.open-app",
            SetupPanelButton.OpenLog => "setup.panel.open-log",
            _ => "setup.panel.close"
        });
        DrawAligned(dc, buttonFont, label, primary ? Background : enabled ? Cyan : Muted, rect, DT_CENTER);
    }

    private static uint Dim(uint color, double keep)
    {
        uint Mix(int shift) => (uint)Math.Round(((color >> shift) & 0xFF) * keep + ((Background >> shift) & 0xFF) * (1 - keep)) << shift;
        return 0xFF000000 | Mix(16) | Mix(8) | Mix(0);
    }

    private static void Outline(IntPtr dc, RECT rect, int line, uint color)
    {
        Fill(dc, new RECT(rect.Left, rect.Top, rect.Right, rect.Top + line), color);
        Fill(dc, new RECT(rect.Left, rect.Bottom - line, rect.Right, rect.Bottom), color);
        Fill(dc, new RECT(rect.Left, rect.Top, rect.Left + line, rect.Bottom), color);
        Fill(dc, new RECT(rect.Right - line, rect.Top, rect.Right, rect.Bottom), color);
    }

    private static void Fill(IntPtr dc, RECT rect, uint color)
    {
        var brush = CreateSolidBrush(ColorRef(color));
        FillRect(dc, ref rect, brush);
        DeleteObject(brush);
    }

    private static void SolidRect(IntPtr graphics, uint argb, float x, float y, float width, float height)
    {
        GdipCreateSolidFill(argb, out var brush);
        GdipFillRectangle(graphics, brush, x, y, width, height);
        GdipDeleteBrush(brush);
    }

    private static int TextWidth(IntPtr dc, IntPtr font, string text)
    {
        var previous = SelectObject(dc, font);
        GetTextExtentPoint32W(dc, text, text.Length, out var size);
        SelectObject(dc, previous);
        return size.cx;
    }

    private static void DrawLine(IntPtr dc, IntPtr font, string text, uint color, int x, int y, int width, int height)
    {
        if (width <= 0) return;
        DrawAligned(dc, font, text, color, new RECT(x, y, x + width, y + height), DT_LEFT);
    }

    private static void DrawCentered(IntPtr dc, IntPtr font, string text, uint color, RECT rect) =>
        DrawAligned(dc, font, text, color, rect, DT_CENTER);

    private static void DrawAligned(IntPtr dc, IntPtr font, string text, uint color, RECT rect, uint align)
    {
        if (rect.Right <= rect.Left) return;
        var previous = SelectObject(dc, font);
        SetTextColor(dc, ColorRef(color));
        var ellipsis = (align & DT_PATH_ELLIPSIS) != 0 ? 0 : DT_END_ELLIPSIS;
        DrawTextW(dc, text, text.Length, ref rect, align | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | ellipsis);
        SelectObject(dc, previous);
    }

    private static uint ColorRef(uint argb) =>
        ((argb & 0xFF) << 16) | (argb & 0xFF00) | ((argb >> 16) & 0xFF);
}
