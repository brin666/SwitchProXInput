using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace SwitchProXInput.App.Services;

public static class WindowsElevation
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static Process? RestartElevated(string argument)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法定位当前程序文件。");
        var assemblyLocation = System.Reflection.Assembly.GetEntryAssembly()?.Location;

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = true,
            Verb = "runas"
        };

        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(assemblyLocation))
        {
            startInfo.FileName = processPath;
            startInfo.ArgumentList.Add(assemblyLocation);
        }
        else
        {
            startInfo.FileName = processPath;
        }

        startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo);
    }
}
