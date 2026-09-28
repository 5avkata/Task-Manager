using TaskManager.Services;
using TaskManager.UI;

namespace TaskManager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test"))
        {
            Environment.ExitCode = Diagnostics.SelfTests.Run();
            return;
        }

        // A second writer could otherwise replace changes made by the first window.
        using var instance = new Mutex(true, "Local\\SchoolPortfolio.TaskManager." + Environment.UserName, out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Task Manager is already running. Please use the existing window.",
                "Task Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.Run(new MainForm(new TaskStore()));
    }
}
