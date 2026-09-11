using System.Runtime.InteropServices;

namespace VidShrink.Player;

[StructLayout(LayoutKind.Sequential)]
internal struct MpvRenderParam
{
    public int Type;
    public IntPtr Data;

    public MpvRenderParam(int type, IntPtr data)
    {
        Type = type;
        Data = data;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEvent
{
    public int EventId;
    public int Error;
    public ulong ReplyUserdata;
    public IntPtr Data;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventProperty
{
    public IntPtr Name;
    public int Format;
    public IntPtr Data;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventLogMessage
{
    public IntPtr Prefix;
    public IntPtr Level;
    public IntPtr Text;
    public int LogLevel;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventEndFile
{
    public int Reason;
    public int Error;
    public long PlaylistEntryId;
    public long PlaylistInsertId;
    public int PlaylistInsertNumEntries;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvRenderFrameInfo
{
    public ulong Flags;
    public long TargetTime;
}

internal static unsafe class Native
{
    public const string Lib = "libmpv-2";

    public const int MPV_FORMAT_FLAG = 3;
    public const int MPV_FORMAT_INT64 = 4;
    public const int MPV_FORMAT_DOUBLE = 5;

    public const int MPV_EVENT_NONE = 0;
    public const int MPV_EVENT_SHUTDOWN = 1;
    public const int MPV_EVENT_LOG_MESSAGE = 2;
    public const int MPV_EVENT_COMMAND_REPLY = 5;
    public const int MPV_EVENT_END_FILE = 7;
    public const int MPV_EVENT_FILE_LOADED = 8;
    public const int MPV_EVENT_VIDEO_RECONFIG = 17;
    public const int MPV_EVENT_SEEK = 20;
    public const int MPV_EVENT_PLAYBACK_RESTART = 21;
    public const int MPV_EVENT_PROPERTY_CHANGE = 22;

    public const int MPV_END_FILE_REASON_ERROR = 4;

    public const int MPV_RENDER_PARAM_INVALID = 0;
    public const int MPV_RENDER_PARAM_API_TYPE = 1;
    public const int MPV_RENDER_PARAM_NEXT_FRAME_INFO = 11;
    public const int MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME = 12;
    public const int MPV_RENDER_PARAM_SW_SIZE = 17;
    public const int MPV_RENDER_PARAM_SW_FORMAT = 18;
    public const int MPV_RENDER_PARAM_SW_STRIDE = 19;
    public const int MPV_RENDER_PARAM_SW_POINTER = 20;

    public const ulong MPV_RENDER_UPDATE_FRAME = 1;
    public const ulong MPV_RENDER_FRAME_INFO_PRESENT = 1;
    public const ulong MPV_RENDER_FRAME_INFO_REDRAW = 2;

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong mpv_client_api_version();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_error_string(int error);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(IntPtr data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_create();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_initialize(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_set_option_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_set_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command(IntPtr ctx, IntPtr* args);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command_async(IntPtr ctx, ulong replyUserdata, IntPtr* args);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_get_property(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, void* data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_get_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_observe_property(IntPtr ctx, ulong replyUserdata,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_request_log_messages(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string minLevel);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvEvent* mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_wakeup(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_create(IntPtr* res, IntPtr mpv, MpvRenderParam* parameters);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_get_info(IntPtr ctx, MpvRenderParam param);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_set_update_callback(IntPtr ctx,
        delegate* unmanaged[Cdecl]<IntPtr, void> callback, IntPtr callbackCtx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong mpv_render_context_update(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_render(IntPtr ctx, MpvRenderParam* parameters);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(IntPtr ctx);

    public static string Error(int code) => Marshal.PtrToStringUTF8(mpv_error_string(code)) ?? code.ToString();
}
