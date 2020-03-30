using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Gtk;

namespace DriveMirror
{
    class MainClass
    {
#if WINDOWS
        public static bool IsElevated => new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
#endif
        public static async Task Main(string[] args)
        {
#if WINDOWS
            var PATH = Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable("PATH", PATH + ";" + System.IO.Path.Combine(Environment.CurrentDirectory, "dlls"));
            LoadLibrary("libglib-2.0-0.dll");
            LoadLibrary("libgtk-win32-2.0-0.dll");
#endif
            Application.Init();
            if (args?.Length > 0) {
                for (int i = 0; i < args.Length; i++) { 
                    switch (args[i].ToLower().Trim(' ', '\r', '\n', '/', '\\', '-')) {
                        case "debug":
                            System.Diagnostics.Debugger.Launch();
                            break;
                        case "service":
                            await Server.OpenServer();
                            return;
                        case "instance":
                            Server.ServerInstance = args[++i];
                            break;
                        case "i":
                        case "install":
                            if (!IsUnix)
                                return;
                            Setup.InstallMe();
                            return;
                        case "u":
                        case "uninstall":
                            if (!IsUnix)
                                return;
                            Setup.UninstallMe();
                            return;
                        default:
                            if (IsUnix)
                            {
                                Console.WriteLine("DriveMirror - By Marcussacana");
                                Console.WriteLine("-install\tInstall or Update the DriveMirror to this user");
                                Console.WriteLine("-install\tUninstall the DriveMirror to this user");
                            }
                            return;
                    }
                }
            }
            MainWindow win = new MainWindow();
            win.Show();
            Application.Run();
        }

#if WINDOWS
        [DllImport("kernel32", EntryPoint = "LoadLibraryW", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string FileName);
#endif

        public static bool IsUnix
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                if ((p == 4) || (p == 6) || (p == 128))
                    return true;
                return false;
            }
        }
    }
}
