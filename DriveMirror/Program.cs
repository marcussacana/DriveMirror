using System;
using System.Linq;
using System.Threading.Tasks;
using Gtk;

namespace DriveMirror
{
    class MainClass
    {
        public static async Task Main(string[] args)
        {
            Application.Init();
            if (args?.Length > 0) {
                for (int i = 0; i < args.Length; i++) { 
                    switch (args[i].ToLower().Trim(' ', '\r', '\n', '/', '\\', '-')) {
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
