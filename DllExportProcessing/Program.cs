using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DllExportProcessing
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            var ildasm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\ildasm.exe");
            var ilasm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework64\v4.0.30319\ilasm.exe");
            var sn = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\sn.exe");

            var dll = args[0];
            var fileFolder = Path.GetDirectoryName(dll);
            var il = Path.Combine(fileFolder, Path.GetFileNameWithoutExtension(dll) + ".il");
            var res = Path.Combine(fileFolder, Path.GetFileNameWithoutExtension(dll) + ".res");
            var snk = Path.Combine(fileFolder, Path.GetFileNameWithoutExtension(dll) + ".snk");

            using (var process = Process.Start(new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                FileName = ildasm,
                Arguments = "\"" + dll + "\" /OUT=\"" + il + "\""
            }))
            {
                process.WaitForExit();
                Console.WriteLine(process.StandardOutput.ReadToEnd());
                Console.WriteLine(process.StandardError.ReadToEnd());
            }

            using (var process = Process.Start(new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                FileName = sn,
                Arguments = "-k \"" + snk + "\""
            }))
            {
                process.WaitForExit();
                Console.WriteLine(process.StandardOutput.ReadToEnd());
                Console.WriteLine(process.StandardError.ReadToEnd());
            }

            var methodNames = new[]
            {
                "GetUpdatedConfigF",
                "GetUpdatedConfigP"
            };

            var lines = File.ReadAllLines(il).ToList();
            if (File.Exists(il))
            {
                File.Delete(il);
            }

            var counter = 1;
            foreach (var methodName in methodNames)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line.Trim().StartsWith(".method") && line.Contains(methodName))
                    {
                        for (int j = i; j < lines.Count; j++)
                        {
                            var methodLine = lines[j];
                            if (methodLine.Trim() == "{")
                            {
                                lines[i] = line.Insert(line.IndexOf(methodName + "("), "modopt([mscorlib]System.Runtime.InteropServices.CallConvStdCall)");
                                lines.Insert(j + 1, $".vtentry 1 : {counter.ToString(CultureInfo.InvariantCulture)}");
                                lines.Insert(j + 1, $".export [{counter.ToString(CultureInfo.InvariantCulture)}] as " + methodName);
                                File.WriteAllLines(il, lines.ToArray());
                                counter++;
                                break;
                            }
                        }
                        break;
                    }
                }
            }
            
            using (var process = Process.Start(new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                FileName = ilasm,
                Arguments = "\"" + il + "\" /DLL " + string.Join(" ", args.Skip(1).Select(x => "\"" + x + "\"")) + " /OUT=\"" + dll + "\" /RESOURCE=\"" + res + "\" \"/KEY=" + snk + "\""
            }))
            {
                process.WaitForExit();
                Console.WriteLine(process.StandardOutput.ReadToEnd());
                Console.WriteLine(process.StandardError.ReadToEnd());
            }

            if (File.Exists(il))
            {
                File.Delete(il);
            }
            if (File.Exists(res))
            {
                File.Delete(res);
            }
            if (File.Exists(snk))
            {
                File.Delete(snk);
            }

            return 0;
        }
    }
}
