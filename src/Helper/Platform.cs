using System.Globalization;

namespace Mobsub.Helper;

public static class Platform
{
    internal static bool IsWindows()
    {
        return Environment.OSVersion.Platform is PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.Win32NT or PlatformID.WinCE;
    }

    public static string[] GetInstalledFontDir()
    {
        List<string> path = [];
        var os = Environment.OSVersion;
        
        path.Add(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));

        if (IsWindows() && os.Version.Major >= 10)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path.Add(Path.Combine(localAppData, @"Microsoft\Windows\Fonts"));
        }

        return path.ToArray();
    }
}