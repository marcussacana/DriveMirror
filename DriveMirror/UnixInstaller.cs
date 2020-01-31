using System;
using System.Linq;

public static class Setup
{
    internal static void InstallMe()
    {
        UninstallMe();

        var Executable = Server.CurrentExecutable;
        var Directory = GetBinDirectory();
        var NewExe = System.IO.Path.Combine(Directory, "DriveMirror");

        var AppDir = GetApplicationsDirectory();

        var DeskPath = System.IO.Path.Combine(Server.AppDirectory, "DriveMirror.desktop");
        var NewDeskPath = System.IO.Path.Combine(AppDir, "DriveMirror.desktop");
        var PngPath = System.IO.Path.Combine(Server.AppDirectory, "DriveMirror.png");

        var Icon = InstallIcons(PngPath);

        System.IO.File.Copy(Executable, NewExe, true);
        System.IO.File.Copy(DeskPath, NewDeskPath, true);

        UpddateDesktopIcon(NewDeskPath, Icon);
    }

    internal static void UninstallMe()
    {
        var Directory = GetBinDirectory();
        var Executable = System.IO.Path.Combine(Directory, "DriveMirror");

        var AppDir = GetApplicationsDirectory();

        var NewDeskPath = System.IO.Path.Combine(AppDir, "DriveMirror.desktop");
        var PngPath = System.IO.Path.Combine(Server.AppDirectory, "DriveMirror.png");

        UninstallIcons(PngPath);

        if (System.IO.File.Exists(NewDeskPath))
            System.IO.File.Delete(NewDeskPath);
        if (System.IO.File.Exists(Executable))
            System.IO.File.Delete(Executable);
    }

    private static string InstallIcons(string Icon)
    {
        int CurrentSize = 0;
        string BestIcon = string.Empty;
        var IconBaseDir = GetIconsDirectory();

        foreach (var RelDir in System.IO.Directory.GetDirectories(IconBaseDir))
        {
            string IconDir = System.IO.Path.Combine(RelDir, "apps");
            if (!System.IO.Directory.Exists(IconDir))
                continue;

            string IconRel = System.IO.Path.GetFileName(RelDir).ToLower();
            if (!IconRel.Contains("x"))
                continue;
            IconRel = IconRel.Split('@').First();

            string IconPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Icon), System.IO.Path.GetFileNameWithoutExtension(Icon) + $".{IconRel}.png");
            string NewIconPath = System.IO.Path.Combine(IconDir, System.IO.Path.GetFileName(Icon));



            int IconSize = int.Parse(IconRel.Split('x').First());
            if (IconSize > CurrentSize)
            {
                BestIcon = NewIconPath;
                CurrentSize = IconSize;
            }

            if (!System.IO.File.Exists(IconPath))
                throw new Exception("Icon Not Found");

            System.IO.File.Copy(IconPath, NewIconPath, true);
        }

        return BestIcon;
    }

    private static void UninstallIcons(string Icon)
    {
        var IconBaseDir = GetIconsDirectory();

        foreach (var RelDir in System.IO.Directory.GetDirectories(IconBaseDir))
        {
            string IconDir = System.IO.Path.Combine(RelDir, "apps");
            if (!System.IO.Directory.Exists(IconDir))
                continue;

            string IconPath = System.IO.Path.Combine(IconDir, System.IO.Path.GetFileName(Icon));

            if (!System.IO.File.Exists(IconPath))
                continue;

            System.IO.File.Delete(IconPath);
        }
    }

    private static void UpddateDesktopIcon(string Desktop, string Icon)
    {
        string[] Lines = System.IO.File.ReadAllLines(Desktop);
        for (int i = 0; i < Lines.Length; i++)
        {
            if (!Lines[i].Contains("="))
                continue;

            string Name = Lines[i].Split('=').First();
            if (Name != "Icon")
                continue;
            Lines[i] = $"Icon={Icon}";
        }
        System.IO.File.WriteAllLines(Desktop, Lines);
    }

    private static string GetIconsDirectory()
    {
        string[] ValidPaths = new string[] {
                $"/home/{Environment.UserName}/.local/share/icons/hicolor/",
                "/usr/share/icons/hicolor/"
            };


        foreach (var Path in ValidPaths)
            if (System.IO.Directory.Exists(Path))
                return Path;

        throw new Exception("Failed to your ICONS directory");
    }

    private static string GetApplicationsDirectory()
    {
        string[] ValidPaths = new string[] {
                $"/home/{Environment.UserName}/.local/share/applications",
                "/usr/share/applications/",
                "/usr/local/share/applications/"
            };
        foreach (var Path in ValidPaths)
            if (System.IO.Directory.Exists(Path))
                return Path;

        throw new Exception("Failed to your APPLICATIONS directory");
    }

    private static string GetBinDirectory()
    {
        string[] ValidPaths = new string[] {
                $"/home/{Environment.UserName}/.local/bin",
                $"/home/{Environment.UserName}/bin",
                "/usr/local/bin",
                "/usr/bin",
                "/bin"
            };
        foreach (var Path in ValidPaths)
            if (System.IO.Directory.Exists(Path))
                return Path;

        throw new Exception("Failed to your BIN directory");
    }
}