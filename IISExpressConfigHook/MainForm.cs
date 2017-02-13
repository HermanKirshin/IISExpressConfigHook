using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("IISExpressConfigHook")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("IISExpressConfigHook")]
[assembly: AssemblyCopyright("Copyright ©  2017")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("5de1510b-d128-458e-a940-e19ba8500ed0")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace IISExpressConfigHook
{
    internal sealed class MainForm : Form
    {
        private static readonly IReadOnlyCollection<string> processNames = new[]
        {
            "wcfsvchost",
            "iisexpress",
            "devenv"
        }; 

        private readonly ManagementEventWatcher processWatcher = new ManagementEventWatcher();
        private readonly NotifyIcon notifyIcon = new NotifyIcon();
        private readonly ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
        private readonly ToolStripMenuItem exitToolStripMenuItem = new ToolStripMenuItem();

        public MainForm()
        {
            processWatcher.Query.QueryString = @"SELECT * FROM __InstanceOperationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process' AND (" + string.Join(" OR ", processNames.Select(x => "TargetInstance.Name = '" + x + ".exe'")) + ")";

            processWatcher.EventArrived += (sender, e) =>
            {
                if (e.NewEvent.ClassPath.ClassName == "__InstanceCreationEvent")
                    RemoteThread.HandleProcess((int)(uint)((ManagementBaseObject)e.NewEvent["TargetInstance"]).Properties["ProcessId"].Value);
            };
            
            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.Text = "IISExpressConfigHook";
            notifyIcon.Visible = true;

            contextMenuStrip.Items.AddRange(new ToolStripItem[] { exitToolStripMenuItem});

            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += (sender, e) => Close();
            
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;

            notifyIcon.Icon = SystemIcons.Application;

            processNames.SelectMany(Process.GetProcessesByName).Select(x => x.Id).ToList().ForEach(RemoteThread.HandleProcess);

            processWatcher.Start();
        }
        
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        protected override void Dispose(bool disposing)
        {
            processWatcher.Stop();
            processWatcher.Dispose();
            notifyIcon.Dispose();
            exitToolStripMenuItem.Dispose();
            contextMenuStrip.Dispose();

            base.Dispose(disposing);
        }
    }
}
