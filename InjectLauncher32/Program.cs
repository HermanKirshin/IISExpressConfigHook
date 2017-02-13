using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace InjectLauncher
{
    internal static class Program
    {
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            IntPtr lpThreadAttributes,
            IntPtr dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            IntPtr lpThreadId);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(
            IntPtr hModule,
            string lpProcName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(
            string lpModuleName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            IntPtr dwSize,
            uint flAllocationType,
            uint flProtect);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            IntPtr lpBuffer,
            IntPtr nSize,
            IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr hThread, out IntPtr lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr processHandle, [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);
        
        private static void Main(string[] args)
        {
            IntPtr injectedDllPathBuffer;

            var process = Process.GetProcessById(int.Parse(args[0], CultureInfo.InvariantCulture));
            bool wow64Process = IntPtr.Size == 4;

            var hookDllPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), wow64Process ? "Hook32.dll" : "Hook64.dll");

            if (process.Modules.Cast<ProcessModule>().Any(x => (x.FileName ?? string.Empty).Equals(hookDllPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            injectedDllPathBuffer = Marshal.StringToHGlobalAnsi(hookDllPath);
            IntPtr injectedDllPathBufferSize = new IntPtr(hookDllPath.Length + 1);

            const int MEM_RESERVE = 0x00002000;
            const int MEM_COMMIT = 0x00001000;
            const int PAGE_READWRITE = 0x04;

            var loadLibraryAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            var injectDllPathAddrRemote = VirtualAllocEx(process.Handle, IntPtr.Zero, injectedDllPathBufferSize, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
            if (!WriteProcessMemory(process.Handle, injectDllPathAddrRemote, injectedDllPathBuffer, injectedDllPathBufferSize, IntPtr.Zero))
            {
                throw new Exception("WriteProcessMemory failed: " + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }
            var threadHandle = CreateRemoteThread(process.Handle, IntPtr.Zero, IntPtr.Zero, loadLibraryAddress, injectDllPathAddrRemote, 0, IntPtr.Zero);
            if (threadHandle == IntPtr.Zero)
            {
                throw new Exception("CreateRemoteThread failed: " + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            const int WAIT_OBJECT_0 = 0;
            var waitResult = WaitForSingleObject(threadHandle, 15000);
            if (waitResult != WAIT_OBJECT_0)
            {
                throw new Exception("WaitForSingleObject failed: " + waitResult.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            IntPtr moduleHandle;
            if (!GetExitCodeThread(threadHandle, out moduleHandle))
            {
                throw new Exception("GetExitCodeThread failed: " + waitResult.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Marshal.FreeHGlobal(injectedDllPathBuffer);
        }
    }
}
