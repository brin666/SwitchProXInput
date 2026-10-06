using System.Windows;

namespace SwitchProXInput.App;

public partial class App : Application
{
    public static bool StartImmediately { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        StartImmediately = e.Args.Any(argument =>
            string.Equals(argument, "--start", StringComparison.OrdinalIgnoreCase));
        base.OnStartup(e);
    }
}
