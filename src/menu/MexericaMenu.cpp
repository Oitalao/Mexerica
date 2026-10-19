// Extensão do menu do botão direito do Explorador: um submenu "Mexerica" só, sem o limite de 16 itens
// dos menus feitos pelo registro, e com todos os arquivos selecionados entregues de uma vez.
//
// Ela não decide nada sozinha: lê %LOCALAPPDATA%\Mexerica\menu.txt, que o Mexerica escreve
// (uma linha por opção: extensão, grupo, modo, rótulo, texto), e ao clicar chama
//   Mexerica.exe --menu-lista <modo> <rótulo> <arquivo com a lista>
// Tudo o que roda dentro do Explorador fica aqui: pouco código, sem dependências, sem exceções.

#define WIN32_LEAN_AND_MEAN
#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#include <windows.h>
#include <shlobj.h>
#include <shellapi.h>
#include <string>
#include <vector>
#include <new>

// {7A1C3E52-4B9D-4F0E-9C2A-6D5E8F1B3A47}
static const CLSID CLSID_MexericaMenu = { 0x7a1c3e52, 0x4b9d, 0x4f0e, { 0x9c, 0x2a, 0x6d, 0x5e, 0x8f, 0x1b, 0x3a, 0x47 } };

static LONG g_refs = 0;
static HINSTANCE g_inst = nullptr;

struct Item { std::wstring group, mode, label, text; };

static std::wstring Lower(std::wstring s) { CharLowerBuffW(&s[0], (DWORD)s.size()); return s; }

static std::wstring DataDir()
{
    PWSTR p = nullptr;
    std::wstring r;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &p))) r = std::wstring(p) + L"\\Mexerica";
    CoTaskMemFree(p);
    return r;
}

static std::wstring ReadUtf8(const std::wstring& path)
{
    HANDLE h = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
    if (h == INVALID_HANDLE_VALUE) return L"";
    std::string bytes;
    LARGE_INTEGER size{};
    if (GetFileSizeEx(h, &size) && size.QuadPart > 0 && size.QuadPart < 8 * 1024 * 1024)
    {
        bytes.resize((size_t)size.QuadPart);
        DWORD got = 0;
        if (!ReadFile(h, &bytes[0], (DWORD)bytes.size(), &got, nullptr)) got = 0;
        bytes.resize(got);
    }
    CloseHandle(h);
    if (bytes.size() >= 3 && (unsigned char)bytes[0] == 0xEF) bytes.erase(0, 3);
    if (bytes.empty()) return L"";
    int n = MultiByteToWideChar(CP_UTF8, 0, bytes.data(), (int)bytes.size(), nullptr, 0);
    std::wstring w(n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, bytes.data(), (int)bytes.size(), &w[0], n);
    return w;
}

static std::vector<std::wstring> Split(const std::wstring& s, wchar_t sep)
{
    std::vector<std::wstring> out;
    size_t a = 0;
    for (;;)
    {
        size_t b = s.find(sep, a);
        out.push_back(s.substr(a, b == std::wstring::npos ? std::wstring::npos : b - a));
        if (b == std::wstring::npos) break;
        a = b + 1;
    }
    return out;
}

// Ícone do Mexerica como bitmap com transparência, para o item do menu.
static HBITMAP IconBitmap(const std::wstring& exe)
{
    int cx = GetSystemMetrics(SM_CXSMICON), cy = GetSystemMetrics(SM_CYSMICON);
    HICON icon = nullptr;
    if (FAILED(SHDefExtractIconW(exe.c_str(), 0, 0, nullptr, &icon, MAKELONG(0, cx))) || !icon) return nullptr;
    BITMAPINFO bi{};
    bi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bi.bmiHeader.biWidth = cx; bi.bmiHeader.biHeight = -cy;
    bi.bmiHeader.biPlanes = 1; bi.bmiHeader.biBitCount = 32; bi.bmiHeader.biCompression = BI_RGB;
    void* bits = nullptr;
    HDC screen = GetDC(nullptr);
    HBITMAP bmp = CreateDIBSection(screen, &bi, DIB_RGB_COLORS, &bits, nullptr, 0);
    HDC dc = CreateCompatibleDC(screen);
    ReleaseDC(nullptr, screen);
    if (bmp && dc)
    {
        HGDIOBJ old = SelectObject(dc, bmp);
        DrawIconEx(dc, 0, 0, icon, cx, cy, 0, nullptr, DI_NORMAL);
        SelectObject(dc, old);
    }
    if (dc) DeleteDC(dc);
    DestroyIcon(icon);
    return bmp;
}

class Menu : public IShellExtInit, public IContextMenu
{
    LONG refs = 1;
    std::vector<std::wstring> files;
    std::vector<Item> items;      // opções na ordem dos ids de comando
    std::wstring exe;
    HBITMAP icon = nullptr;

public:
    Menu() { InterlockedIncrement(&g_refs); }
    ~Menu() { if (icon) DeleteObject(icon); InterlockedDecrement(&g_refs); }

    // IUnknown
    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IShellExtInit) *ppv = static_cast<IShellExtInit*>(this);
        else if (riid == IID_IContextMenu) *ppv = static_cast<IContextMenu*>(this);
        else { *ppv = nullptr; return E_NOINTERFACE; }
        AddRef();
        return S_OK;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&refs); }
    STDMETHODIMP_(ULONG) Release() override { LONG r = InterlockedDecrement(&refs); if (!r) delete this; return r; }

    // IShellExtInit: guarda os arquivos selecionados.
    STDMETHODIMP Initialize(PCIDLIST_ABSOLUTE, IDataObject* data, HKEY) override
    {
        files.clear();
        if (!data) return E_INVALIDARG;
        FORMATETC fmt = { CF_HDROP, nullptr, DVASPECT_CONTENT, -1, TYMED_HGLOBAL };
        STGMEDIUM med{};
        if (FAILED(data->GetData(&fmt, &med))) return E_INVALIDARG;
        HDROP drop = (HDROP)GlobalLock(med.hGlobal);
        if (drop)
        {
            UINT n = DragQueryFileW(drop, 0xFFFFFFFF, nullptr, 0);
            for (UINT i = 0; i < n; i++)
            {
                UINT len = DragQueryFileW(drop, i, nullptr, 0);
                std::wstring f(len, L'\0');
                DragQueryFileW(drop, i, &f[0], len + 1);
                files.push_back(f);
            }
            GlobalUnlock(med.hGlobal);
        }
        ReleaseStgMedium(&med);
        return files.empty() ? E_INVALIDARG : S_OK;
    }

    // Chave de cada arquivo no menu.txt: a extensão, ou <pasta>.
    static std::wstring KeyOf(const std::wstring& f)
    {
        DWORD a = GetFileAttributesW(f.c_str());
        if (a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY)) return L"<pasta>";
        size_t slash = f.find_last_of(L"\\/"), dot = f.find_last_of(L'.');
        if (dot == std::wstring::npos || (slash != std::wstring::npos && dot < slash)) return L"";
        return Lower(f.substr(dot));
    }

    // Opções que valem para todos os tipos selecionados, na ordem do primeiro tipo.
    std::vector<Item> Options()
    {
        std::vector<Item> result;
        auto text = ReadUtf8(DataDir() + L"\\menu.txt");
        if (text.empty()) return result;
        std::vector<std::vector<std::wstring>> lines;
        std::vector<std::pair<std::wstring, std::wstring>> kinds; // extensão -> tipo (imagem, vídeo...)
        for (auto& line : Split(text, L'\n'))
        {
            auto l = line;
            if (!l.empty() && l.back() == L'\r') l.pop_back();
            auto f = Split(l, L'\t');
            if (f.size() == 2 && f[0] == L"exe") exe = f[1];
            else if (f.size() == 3 && f[0] == L"kind") kinds.push_back({ f[1], f[2] });
            else if (f.size() == 5) lines.push_back(f);
        }
        if (exe.empty()) return result;

        // Tipos da seleção. Tipos diferentes (foto + vídeo) só têm em comum o que vale para qualquer arquivo.
        std::vector<std::wstring> keys, kindsSeen;
        for (auto& f : files)
        {
            auto k = KeyOf(f);
            std::wstring kind = k == L"<pasta>" ? L"Folder" : L"Other";
            for (auto& p : kinds) if (p.first == k) kind = p.second;
            if (kind == L"Other") k = L"<outro>";
            bool seen = false;
            for (auto& x : keys) if (x == k) seen = true;
            if (!seen) keys.push_back(k);
            seen = false;
            for (auto& x : kindsSeen) if (x == kind) seen = true;
            if (!seen) kindsSeen.push_back(kind);
        }
        if (kindsSeen.size() > 1) keys.assign(1, L"<misto>");

        // Mesmo tipo em formatos diferentes (JPG + PNG): junta as opções, sem repetir, na ordem do primeiro.
        for (auto& k : keys)
            for (auto& f : lines)
            {
                if (f[0] != k) continue;
                bool dup = false;
                for (auto& r : result) if (r.mode == f[2] && r.label == f[3]) dup = true;
                if (!dup) result.push_back({ f[1], f[2], f[3], f[4] });
            }
        // Itens da roda e ferramentas depois das conversões, mesmo quando vieram de outro formato.
        std::vector<Item> ordered;
        for (const wchar_t* g : { L"C", L"F", L"R" })
            for (auto& r : result) if (r.group == g) ordered.push_back(r);
        return ordered;
    }

    STDMETHODIMP QueryContextMenu(HMENU menu, UINT index, UINT first, UINT last, UINT flags) override
    {
        if (flags & CMF_DEFAULTONLY) return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);
        items = Options();
        if (items.empty()) return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);

        HMENU sub = CreatePopupMenu(), tools = nullptr;
        UINT id = first;
        bool hasConv = false, hasTools = false;
        for (auto& it : items) { if (it.group == L"C") hasConv = true; if (it.group == L"F") hasTools = true; }
        // Com conversões, as ferramentas ganham um submenu próprio; sem elas (pastas, compactados) vão direto.
        if (hasConv && hasTools) tools = CreatePopupMenu();
        bool needSep = false;
        auto add = [&](HMENU m, const Item& it, UINT cmd) {
            if (needSep && m == sub) { AppendMenuW(sub, MF_SEPARATOR, 0, nullptr); needSep = false; }
            AppendMenuW(m, MF_STRING, cmd, it.text.c_str());
        };
        for (size_t i = 0; i < items.size() && id <= last; i++, id++)
            if (items[i].group == L"C") add(sub, items[i], id);
        if (hasConv) needSep = true;
        id = first;
        for (size_t i = 0; i < items.size() && id <= last; i++, id++)
            if (items[i].group == L"F") add(tools ? tools : sub, items[i], id);
        if (tools)
        {
            if (needSep) { AppendMenuW(sub, MF_SEPARATOR, 0, nullptr); needSep = false; }
            AppendMenuW(sub, MF_POPUP, (UINT_PTR)tools, L"Ferramentas");
        }
        if (hasTools) needSep = true;
        id = first;
        for (size_t i = 0; i < items.size() && id <= last; i++, id++)
            if (items[i].group == L"R") add(sub, items[i], id);

        MENUITEMINFOW mi{};
        mi.cbSize = sizeof(mi);
        mi.fMask = MIIM_SUBMENU | MIIM_STRING | MIIM_ID;
        mi.hSubMenu = sub;
        mi.wID = first + (UINT)items.size(); // id próprio só para o item-pai (não executa nada)
        mi.dwTypeData = const_cast<LPWSTR>(L"Mexerica");
        if (!icon) icon = IconBitmap(exe);
        if (icon) { mi.fMask |= MIIM_BITMAP; mi.hbmpItem = icon; }
        InsertMenuItemW(menu, index, TRUE, &mi);
        return MAKE_HRESULT(SEVERITY_SUCCESS, 0, (USHORT)(items.size() + 1));
    }

    STDMETHODIMP InvokeCommand(CMINVOKECOMMANDINFO* ci) override
    {
        if (!ci || HIWORD(ci->lpVerb) != 0) return E_FAIL; // verbos por nome não são nossos
        UINT i = LOWORD(ci->lpVerb);
        if (i >= items.size() || exe.empty()) return E_FAIL;

        // A lista de arquivos vai num arquivo temporário (a linha de comando tem limite de tamanho).
        wchar_t tmpDir[MAX_PATH + 1];
        GetTempPathW(MAX_PATH, tmpDir);
        wchar_t list[MAX_PATH + 1];
        wsprintfW(list, L"%smexerica-menu-%lu-%lu.txt", tmpDir, GetCurrentProcessId(), GetTickCount());
        std::wstring all;
        for (auto& f : files) { all += f; all += L"\n"; }
        int n = WideCharToMultiByte(CP_UTF8, 0, all.c_str(), (int)all.size(), nullptr, 0, nullptr, nullptr);
        std::string utf8(n, '\0');
        WideCharToMultiByte(CP_UTF8, 0, all.c_str(), (int)all.size(), &utf8[0], n, nullptr, nullptr);
        HANDLE h = CreateFileW(list, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_TEMPORARY, nullptr);
        if (h == INVALID_HANDLE_VALUE) return E_FAIL;
        DWORD wrote = 0;
        WriteFile(h, utf8.data(), (DWORD)utf8.size(), &wrote, nullptr);
        CloseHandle(h);

        std::wstring cmd = L"\"" + exe + L"\" --menu-lista " + items[i].mode + L" \"" + items[i].label + L"\" \"" + list + L"\"";
        STARTUPINFOW si{}; si.cb = sizeof(si);
        PROCESS_INFORMATION pi{};
        if (!CreateProcessW(exe.c_str(), &cmd[0], nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi)) { DeleteFileW(list); return E_FAIL; }
        AllowSetForegroundWindow(pi.dwProcessId);
        CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
        return S_OK;
    }

    STDMETHODIMP GetCommandString(UINT_PTR, UINT, UINT*, CHAR*, UINT) override { return E_NOTIMPL; }
};

class Factory : public IClassFactory
{
    LONG refs = 1;
public:
    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IClassFactory) { *ppv = this; AddRef(); return S_OK; }
        *ppv = nullptr; return E_NOINTERFACE;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&refs); }
    STDMETHODIMP_(ULONG) Release() override { LONG r = InterlockedDecrement(&refs); if (!r) delete this; return r; }
    STDMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (outer) return CLASS_E_NOAGGREGATION;
        Menu* m = new (std::nothrow) Menu();
        if (!m) return E_OUTOFMEMORY;
        HRESULT hr = m->QueryInterface(riid, ppv);
        m->Release();
        return hr;
    }
    STDMETHODIMP LockServer(BOOL lock) override { if (lock) InterlockedIncrement(&g_refs); else InterlockedDecrement(&g_refs); return S_OK; }
};

extern "C" BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) { g_inst = inst; DisableThreadLibraryCalls(inst); }
    return TRUE;
}

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (clsid != CLSID_MexericaMenu) return CLASS_E_CLASSNOTAVAILABLE;
    Factory* f = new (std::nothrow) Factory();
    if (!f) return E_OUTOFMEMORY;
    HRESULT hr = f->QueryInterface(riid, ppv);
    f->Release();
    return hr;
}

extern "C" HRESULT __stdcall DllCanUnloadNow() { return g_refs == 0 ? S_OK : S_FALSE; }
