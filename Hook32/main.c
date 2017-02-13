#include <Windows.h>
#include <Shlwapi.h>
#include <stdio.h>
#include "minhook/include/minhook.h"

typedef HANDLE(WINAPI *CREATEFILE)(LPCWSTR lpFileName, DWORD dwDesiredAccess, DWORD dwShareMode, LPSECURITY_ATTRIBUTES lpSecurityAttributes, DWORD dwCreationDisposition, DWORD dwFlagsAndAttributes, HANDLE hTemplateFile);
typedef VOID(WINAPI *FILEHANDLER)(LPCWSTR lpFileName, LPCWSTR newPath, DWORD dwDesiredAccess, DWORD dwShareMode, LPSECURITY_ATTRIBUTES lpSecurityAttributes, DWORD dwCreationDisposition, DWORD dwFlagsAndAttributes, HANDLE hTemplateFile);
typedef BOOL (WINAPI *CREATEPROCESS)(LPCWSTR lpApplicationName, LPWSTR lpCommandLine, LPSECURITY_ATTRIBUTES lpProcessAttributes, LPSECURITY_ATTRIBUTES lpThreadAttributes, BOOL bInheritHandles, DWORD dwCreationFlags, LPVOID lpEnvironment, LPCWSTR lpCurrentDirectory, LPSTARTUPINFOW lpStartupInfo, LPPROCESS_INFORMATION lpProcessInformation);
typedef VOID(WINAPI *PROCESSHANDLER)(LPWSTR lpCommandLine, LPWSTR newCommandLine);

CREATEFILE CreateFileWOriginal;
CREATEPROCESS CreateProcessWOriginal;
HANDLE dll;
FILEHANDLER fileHandler;
PROCESSHANDLER processHandler;
WCHAR modulePath[MAX_PATH];

#define BUFFER_SIZE sizeof(WCHAR) * 32768 

BOOL WINAPI _CreateProcessW(LPCWSTR lpApplicationName, LPWSTR lpCommandLine, LPSECURITY_ATTRIBUTES lpProcessAttributes, LPSECURITY_ATTRIBUTES lpThreadAttributes, BOOL bInheritHandles, DWORD dwCreationFlags, LPVOID lpEnvironment, LPCWSTR lpCurrentDirectory, LPSTARTUPINFOW lpStartupInfo, LPPROCESS_INFORMATION lpProcessInformation)
{
    BOOL result;
    LPWSTR buffer = NULL;
    if (lpCommandLine != NULL && StrStrI(lpCommandLine, L".config") != NULL)
    {
        buffer = (LPWSTR)malloc(BUFFER_SIZE);
        memset(buffer, 0, BUFFER_SIZE);
        processHandler(lpCommandLine, buffer);
        lpCommandLine = buffer;
    }
    result = CreateProcessWOriginal(lpApplicationName, lpCommandLine, lpProcessAttributes, lpThreadAttributes, bInheritHandles, dwCreationFlags, lpEnvironment, lpCurrentDirectory, lpStartupInfo, lpProcessInformation);
    if (buffer != NULL)
        free(buffer);
    return result;
}

HANDLE WINAPI _CreateFileW(LPCWSTR lpFileName, DWORD dwDesiredAccess, DWORD dwShareMode, LPSECURITY_ATTRIBUTES lpSecurityAttributes, DWORD dwCreationDisposition, DWORD dwFlagsAndAttributes, HANDLE hTemplateFile)
{
    DWORD attributes = GetFileAttributesW(lpFileName);
    HANDLE result = CreateFileWOriginal(lpFileName, dwDesiredAccess, dwShareMode, lpSecurityAttributes, dwCreationDisposition, dwFlagsAndAttributes, hTemplateFile);
    HANDLE newFile = NULL;

    if (attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 && (StrStrI(lpFileName, L"web.config") != NULL || StrStrI(lpFileName, L"app.config") != NULL) && StrStrI(lpFileName, L"Windows") == NULL)
    {
        fileHandler(result, &newFile, dwDesiredAccess, dwShareMode, lpSecurityAttributes, dwCreationDisposition, dwFlagsAndAttributes, hTemplateFile);
    }
    if (newFile != NULL)
    {
        CloseHandle(result);
        result = newFile;
    }
    return result;
}

#ifdef _M_X64 
#define INJECT L"Inject64.dll"
#define HOOK L"Hook64.dll"
#endif

#ifdef _M_IX86 
#define INJECT L"Inject32.dll"
#define HOOK L"Hook32.dll"
#endif

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpReserved)
{
    BOOL isDevEnv;
    MH_STATUS status;
    FARPROC createFile = GetProcAddress(GetModuleHandleA("kernel32.dll"), "CreateFileW");
    FARPROC createProcess = GetProcAddress(GetModuleHandleA("kernel32.dll"), "CreateProcessW");
    switch (fdwReason)
    {
    case DLL_PROCESS_ATTACH:
        GetModuleFileName(NULL, modulePath, MAX_PATH);
        isDevEnv = StrStrI(modulePath, L"devenv.exe") != NULL;
        memset(modulePath, 0, sizeof(WCHAR) * MAX_PATH);
        GetModuleFileName(hinstDLL, modulePath, MAX_PATH);
        memcpy(StrStrI(modulePath, HOOK), INJECT, sizeof(INJECT));
        dll = LoadLibrary(modulePath);
        if (dll == NULL)
            return FALSE;
        fileHandler = (FILEHANDLER)GetProcAddress(dll, "GetUpdatedConfigF");
        processHandler = (PROCESSHANDLER)GetProcAddress(dll, "GetUpdatedConfigP");
        MH_Initialize();
        status = MH_CreateHook(createFile, &_CreateFileW, &CreateFileWOriginal);
        if (status == MH_OK && !isDevEnv)
            status = MH_EnableHook(createFile);
        if (status == MH_OK)
            status = MH_CreateHook(createProcess, &_CreateProcessW, &CreateProcessWOriginal);
        if (status == MH_OK && isDevEnv)
            status = MH_EnableHook(createProcess);
        if (fileHandler == NULL || status != MH_OK)
        {
            FreeLibrary(dll);
            return FALSE;
        }
        break;
    case DLL_THREAD_ATTACH:
        break;
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        MH_DisableHook(createProcess);
        MH_RemoveHook(createProcess);
        MH_DisableHook(createFile);
        MH_RemoveHook(createFile);
        FreeLibrary(dll);
        MH_Uninitialize();
        break;
    }
    return TRUE;
}
