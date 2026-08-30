#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <new>

static const CLSID CLSID_VidShrinkCommand =
{ 0x7b8b4a16, 0xe3f5, 0x4c4a, { 0xa8, 0xd2, 0x26, 0xb2, 0xf8, 0x95, 0xbe, 0x58 } };

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
    if (!PathAppendW(path, L"VidShrink.exe")) return E_FAIL;
    return S_OK;
}

class VidShrinkCommand final : public IExplorerCommand
{
    long references = 1;

public:
    VidShrinkCommand() { InterlockedIncrement(&objectCount); }
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
        return CopyText(PRIMARYLANGID(GetUserDefaultUILanguage()) == LANG_TURKISH
            ? L"Bu Videoyu VidShrink ile Aç"
            : L"Open this video with VidShrink", title);
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
        *name = CLSID_VidShrinkCommand;
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
        if (!items) return E_INVALIDARG;
        DWORD count = 0;
        auto result = items->GetCount(&count);
        if (FAILED(result) || count == 0) return FAILED(result) ? result : E_INVALIDARG;

        IShellItem* item = nullptr;
        result = items->GetItemAt(0, &item);
        if (FAILED(result)) return result;

        PWSTR selected = nullptr;
        result = item->GetDisplayName(SIGDN_FILESYSPATH, &selected);
        item->Release();
        if (FAILED(result)) return result;

        wchar_t launcher[MAX_PATH];
        result = LauncherPath(launcher, ARRAYSIZE(launcher));
        if (SUCCEEDED(result))
        {
            auto launched = ShellExecuteW(nullptr, L"open", launcher, selected, nullptr, SW_SHOWNORMAL);
            if (reinterpret_cast<INT_PTR>(launched) <= 32)
                result = HRESULT_FROM_WIN32(static_cast<DWORD>(reinterpret_cast<INT_PTR>(launched)));
        }
        CoTaskMemFree(selected);
        return result;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (!flags) return E_POINTER;
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        if (!commands) return E_POINTER;
        *commands = nullptr;
        return E_NOTIMPL;
    }
};

class CommandFactory final : public IClassFactory
{
    long references = 1;

public:
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
        auto command = new (std::nothrow) VidShrinkCommand();
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
    if (classId != CLSID_VidShrinkCommand) return CLASS_E_CLASSNOTAVAILABLE;
    auto factory = new (std::nothrow) CommandFactory();
    if (!factory) return E_OUTOFMEMORY;
    auto result = factory->QueryInterface(interfaceId, value);
    factory->Release();
    return result;
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return objectCount == 0 ? S_OK : S_FALSE;
}
