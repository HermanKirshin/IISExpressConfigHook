using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Inject
{
    public static class EntryPoint
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributesW(string lpFileName);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint GetFinalPathNameByHandle(IntPtr hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetFileSize(IntPtr hFile, IntPtr lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW([MarshalAs(UnmanagedType.LPTStr)] string filename, uint access, uint share, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        private static readonly IReadOnlyCollection<ReplacementMap> config = ReplacementMap.Load();

        public static void GetUpdatedConfigP(IntPtr commandLine, IntPtr newCommandLine)
        {
            var commandLineText = Marshal.PtrToStringUni(commandLine);
            try
            {
                int numArgs;
                var argArray = CommandLineToArgvW(commandLineText, out numArgs);
                if (argArray != IntPtr.Zero)
                {
                    var pointerArray = new IntPtr[numArgs];
                    Marshal.Copy(argArray, pointerArray, 0, numArgs);
                    var arguments = pointerArray.Select(x => Marshal.PtrToStringUni(x)).ToArray();

                    var configFile = arguments.FirstOrDefault(x => x.EndsWith(".config", StringComparison.OrdinalIgnoreCase));
                    var matchedSection = config.FirstOrDefault(x => configFile.ToString().IndexOf(x.Branch, StringComparison.OrdinalIgnoreCase) >= 0);
                  
                    if (matchedSection != null && configFile != null && configFile.StartsWith("/config:", StringComparison.OrdinalIgnoreCase) && commandLineText.IndexOf("wcfsvchost", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        configFile = configFile.Substring("/config:".Length);
                        
                        var content = File.ReadAllText(configFile);
                        foreach (var replacement in matchedSection.Replacements)
                            content = content.Replace(replacement.Find, replacement.ReplaceWith);

                        var tempFile = Path.GetTempFileName();
                        MoveFileEx(tempFile, null, 4);
                        File.WriteAllText(tempFile, content);

                        commandLineText = commandLineText.Replace(configFile, tempFile);
                    }
                }
            }
            catch
            {
            }
            Marshal.Copy(commandLineText.ToCharArray(), 0, newCommandLine, commandLineText.Length);
        }

        public static void GetUpdatedConfigF(IntPtr handle, IntPtr newHandleAddress, uint access, uint share, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile)
        {
            try
            {
                if (config == null)
                    return;
                var path = new StringBuilder(260);
                if (GetFinalPathNameByHandle(handle, path, (uint)path.Capacity, 0) == 0)
                    return;
                var matchedSection = config.FirstOrDefault(x => path.ToString().IndexOf(x.Branch, StringComparison.OrdinalIgnoreCase) >= 0);
                if (matchedSection == null)
                    return;
                var size = GetFileSize(handle, IntPtr.Zero);
                if (size == 0)
                    return;
                var buffer = new byte[size];
                uint bytesRead;
                if (!ReadFile(handle, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero))
                    return;
                var content = Encoding.UTF8.GetString(buffer);
                foreach (var replacement in matchedSection.Replacements)
                    content = content.Replace(replacement.Find, replacement.ReplaceWith);
                var tempFile = Path.GetTempFileName();
                MoveFileEx(tempFile, null, 4);
                File.WriteAllText(tempFile, content);
                var newHandle = CreateFileW(tempFile, access, share, securityAttributes, creationDisposition, flagsAndAttributes, templateFile);
                Marshal.WriteIntPtr(newHandleAddress, newHandle);
            }
            catch
            {
            }
        }
    }
}
