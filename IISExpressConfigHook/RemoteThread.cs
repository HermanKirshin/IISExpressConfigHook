using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IISExpressConfigHook
{
    internal static class RemoteThread
    {
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr processHandle, [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

        internal static void HandleProcess(int id)
        {
            try
            {
                var process = Process.GetProcessById(id);
                bool wow64Process;
                IsWow64Process(process.Handle, out wow64Process);

                var hookDllPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), wow64Process ? "Hook32.dll" : "Hook64.dll");
                if (!File.Exists(hookDllPath))
                    File.WriteAllBytes(hookDllPath, wow64Process ? Resources.Hook32 : Resources.Hook64);
                var injectDllPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), wow64Process ? "Inject32.dll" : "Inject64.dll");
                if (!File.Exists(injectDllPath))
                    File.WriteAllBytes(injectDllPath, wow64Process ? Resources.Inject32 : Resources.Inject64);
                var configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.xml");
                if (!File.Exists(configPath))
                    File.WriteAllText(configPath, Resources.config);
                var injectLauncherPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), wow64Process ? "InjectLauncher32.exe" : "InjectLauncher64.exe");
                if (!File.Exists(injectLauncherPath))
                    File.WriteAllBytes(injectLauncherPath, wow64Process ? Resources.InjectLauncher32 : Resources.InjectLauncher64);

                using (var injectLauncher = Process.Start(new ProcessStartInfo
                {
                    FileName = injectLauncherPath,
                    Arguments = id.ToString(CultureInfo.InvariantCulture),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }))
                {
                    injectLauncher.WaitForExit();
                    var output = (injectLauncher.StandardError.ReadToEnd() + Environment.NewLine + injectLauncher.StandardOutput.ReadToEnd()).Trim();
                    if (!string.IsNullOrWhiteSpace(output))
                        throw new Exception(output);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
