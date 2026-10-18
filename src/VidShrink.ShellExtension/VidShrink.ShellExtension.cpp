#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <new>
#include <string>

static const CLSID CLSID_VidShrinkCommand =
{ 0x7b8b4a16, 0xe3f5, 0x4c4a, { 0xa8, 0xd2, 0x26, 0xb2, 0xf8, 0x95, 0xbe, 0x58 } };

static const CLSID CLSID_VidShrinkShrinkCommand =
{ 0x565576f4, 0x81a6, 0x4fe2, { 0xb3, 0xe2, 0x02, 0xe8, 0x07, 0x2b, 0x33, 0x4a } };

#define VIDSHRINK_APP_NAME L"VidShrink Player"

static const wchar_t* const LabelKey = L"Software\\Teknesyum\\VidShrinkPlayer\\ShellLabels";

static const wchar_t* const ShrinkFlag = L"--kucult";

struct QuickShrinkTarget
{
    const wchar_t* key;
    int megabytes;
    const wchar_t* fallback;
};

static const QuickShrinkTarget QuickShrinkMenu[] =
{
    { L"01-size-8", 8, L"8 MB" },
    { L"02-whatsapp-chat", 16, L"WhatsApp (16 MB)" },
    { L"03-discord-free", 20, L"Discord (20 MB)" },
    { L"04-email-gmail", 25, L"Gmail (25 MB)" },
    { L"05-discord-nitro-basic", 50, L"Discord Nitro Basic (50 MB)" },
    { L"06-size-100", 100, L"100 MB" },
};

static const ULONG TargetCount = static_cast<ULONG>(ARRAYSIZE(QuickShrinkMenu));

static HMODULE moduleHandle;
static long objectCount;

static HRESULT CopyText(const wchar_t* value, PWSTR* destination)
{
    if (!destination) return E_POINTER;
    return SHStrDupW(value, destination);
}

static HRESULT LauncherPath(wchar_t* path, DWORD capacity)
{
    if (!GetModuleFileNameW(moduleHandle, path, capacity)) return HRESULT_FROM_WIN32(GetLastError());
    if (!PathRemoveFileSpecW(path) || !PathRemoveFileSpecW(path)) return E_FAIL;
    if (!PathAppendW(path, L"VidShrinkPlayer.exe")) return E_FAIL;
    return S_OK;
}

static HRESULT MenuLabel(const wchar_t* name, wchar_t* label, DWORD capacity)
{
    DWORD bytes = capacity * sizeof(wchar_t);
    auto status = RegGetValueW(HKEY_CURRENT_USER, LabelKey, name,
        RRF_RT_REG_SZ, nullptr, label, &bytes);
    if (status != ERROR_SUCCESS) return HRESULT_FROM_WIN32(status);
    return label[0] ? S_OK : E_FAIL;
}

static HRESULT SelectedPath(IShellItemArray* items, DWORD index, PWSTR* path)
{
    IShellItem* item = nullptr;
    auto result = items->GetItemAt(index, &item);
    if (FAILED(result)) return result;
    result = item->GetDisplayName(SIGDN_FILESYSPATH, path);
    item->Release();
    return result;
}

static HRESULT Launch(const wchar_t* parameters)
{
    wchar_t launcher[MAX_PATH];
    auto result = LauncherPath(launcher, ARRAYSIZE(launcher));
    if (FAILED(result)) return result;
    auto launched = ShellExecuteW(nullptr, L"open", launcher, parameters, nullptr, SW_SHOWNORMAL);
    if (reinterpret_cast<INT_PTR>(launched) <= 32)
        return HRESULT_FROM_WIN32(static_cast<DWORD>(reinterpret_cast<INT_PTR>(launched)));
    return S_OK;
}

enum class CommandKind { Open, Shrink, Target };

class VidShrinkCommand final : public IExplorerCommand
{
    long references = 1;
    CommandKind kind;
    ULONG target;

public:
    VidShrinkCommand(CommandKind commandKind, ULONG targetIndex)
        : kind(commandKind), target(targetIndex) { InterlockedIncrement(&objectCount); }
    ~VidShrinkCommand() { InterlockedDecrement(&objectCount); }

    IFACEMETHODIMP QueryInterface(REFIID id, void** value) override
    {
        if (!value) return E_POINTER;
        *value = nullptr;
        if (id == IID_IUnknown || id == IID_IExplorerCommand)
            *value = static_cast<IExplorerCommand*>(this);
        if (!*value) return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&references); }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        auto remaining = InterlockedDecrement(&references);
        if (!remaining) delete this;
        return remaining;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
    {
        wchar_t label[256];
        if (kind == CommandKind::Target)
        {
            std::wstring name = L"target.";
            name += QuickShrinkMenu[target].key;
            if (SUCCEEDED(MenuLabel(name.c_str(), label, ARRAYSIZE(label)))) return CopyText(label, title);
            return CopyText(QuickShrinkMenu[target].fallback, title);
        }
        if (kind == CommandKind::Shrink)
        {
            if (SUCCEEDED(MenuLabel(L"shrink", label, ARRAYSIZE(label)))) return CopyText(label, title);
            return CopyText(L"Shrink with " VIDSHRINK_APP_NAME, title);
        }
        if (SUCCEEDED(MenuLabel(L"open", label, ARRAYSIZE(label)))) return CopyText(label, title);
        return CopyText(L"Open this video with " VIDSHRINK_APP_NAME, title);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        wchar_t path[MAX_PATH];
        auto result = LauncherPath(path, ARRAYSIZE(path));
        return SUCCEEDED(result) ? CopyText(path, icon) : result;
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tooltip) override
    {
        if (!tooltip) return E_POINTER;
        *tooltip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* name) override
    {
        if (!name) return E_POINTER;
        if (kind == CommandKind::Target)
        {
            *name = GUID_NULL;
            return E_NOTIMPL;
        }
        *name = kind == CommandKind::Shrink ? CLSID_VidShrinkShrinkCommand : CLSID_VidShrinkCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override
    {
        if (!state) return E_POINTER;
        *state = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        if (kind == CommandKind::Shrink) return E_NOTIMPL;
        if (!items) return E_INVALIDARG;
        DWORD count = 0;
        auto result = items->GetCount(&count);
        if (FAILED(result) || count == 0) return FAILED(result) ? result : E_INVALIDARG;

        if (kind == CommandKind::Open)
        {
            PWSTR selected = nullptr;
            result = SelectedPath(items, 0, &selected);
            if (FAILED(result)) return result;
            result = Launch(selected);
            CoTaskMemFree(selected);
            return result;
        }

        std::wstring parameters = ShrinkFlag;
        parameters += L' ';
        parameters += std::to_wstring(QuickShrinkMenu[target].megabytes);
        for (DWORD index = 0; index < count; index++)
        {
            PWSTR selected = nullptr;
            result = SelectedPath(items, index, &selected);
            if (FAILED(result)) return result;
            parameters += L" \"";
            parameters += selected;
            parameters += L'"';
            CoTaskMemFree(selected);
        }
        return Launch(parameters.c_str());
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (!flags) return E_POINTER;
        *flags = kind == CommandKind::Shrink ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override;
};

class TargetEnumerator final : public IEnumExplorerCommand
{
    long references = 1;
    ULONG position;

public:
    explicit TargetEnumerator(ULONG start) : position(start) { InterlockedIncrement(&objectCount); }
    ~TargetEnumerator() { InterlockedDecrement(&objectCount); }

    IFACEMETHODIMP QueryInterface(REFIID id, void** value) override
    {
        if (!value) return E_POINTER;
        *value = nullptr;
        if (id == IID_IUnknown || id == IID_IEnumExplorerCommand)
            *value = static_cast<IEnumExplorerCommand*>(this);
        if (!*value) return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&references); }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        auto remaining = InterlockedDecrement(&references);
        if (!remaining) delete this;
        return remaining;
    }

    IFACEMETHODIMP Next(ULONG count, IExplorerCommand** commands, ULONG* fetched) override
    {
        if (!commands) return E_POINTER;
        ULONG done = 0;
        while (done < count && position < TargetCount)
        {
            auto command = new (std::nothrow) VidShrinkCommand(CommandKind::Target, position);
            if (!command) break;
            commands[done++] = command;
            position++;
        }
        if (fetched) *fetched = done;
        return done == count ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP Skip(ULONG count) override
    {
        auto left = TargetCount - position;
        auto skipped = count < left ? count : left;
        position += skipped;
        return skipped == count ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP Reset() override
    {
        position = 0;
        return S_OK;
    }

    IFACEMETHODIMP Clone(IEnumExplorerCommand** copy) override
    {
        if (!copy) return E_POINTER;
        auto clone = new (std::nothrow) TargetEnumerator(position);
        *copy = clone;
        return clone ? S_OK : E_OUTOFMEMORY;
    }
};

IFACEMETHODIMP VidShrinkCommand::EnumSubCommands(IEnumExplorerCommand** commands)
{
    if (!commands) return E_POINTER;
    *commands = nullptr;
    if (kind != CommandKind::Shrink) return E_NOTIMPL;
    auto enumerator = new (std::nothrow) TargetEnumerator(0);
    if (!enumerator) return E_OUTOFMEMORY;
    *commands = enumerator;
    return S_OK;
}

class CommandFactory final : public IClassFactory
{
    long references = 1;
    CommandKind kind;

public:
    explicit CommandFactory(CommandKind commandKind) : kind(commandKind) {}

    IFACEMETHODIMP QueryInterface(REFIID id, void** value) override
    {
        if (!value) return E_POINTER;
        *value = nullptr;
        if (id == IID_IUnknown || id == IID_IClassFactory)
            *value = static_cast<IClassFactory*>(this);
        if (!*value) return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&references); }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        auto remaining = InterlockedDecrement(&references);
        if (!remaining) delete this;
        return remaining;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID id, void** value) override
    {
        if (outer) return CLASS_E_NOAGGREGATION;
        auto command = new (std::nothrow) VidShrinkCommand(kind, 0);
        if (!command) return E_OUTOFMEMORY;
        auto result = command->QueryInterface(id, value);
        command->Release();
        return result;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        InterlockedExchangeAdd(&objectCount, lock ? 1 : -1);
        return S_OK;
    }
};

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, void*)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        moduleHandle = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID classId, REFIID interfaceId, void** value)
{
    CommandKind kind;
    if (classId == CLSID_VidShrinkCommand) kind = CommandKind::Open;
    else if (classId == CLSID_VidShrinkShrinkCommand) kind = CommandKind::Shrink;
    else return CLASS_E_CLASSNOTAVAILABLE;
    auto factory = new (std::nothrow) CommandFactory(kind);
    if (!factory) return E_OUTOFMEMORY;
    auto result = factory->QueryInterface(interfaceId, value);
    factory->Release();
    return result;
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return objectCount == 0 ? S_OK : S_FALSE;
}
