namespace AutoElevateLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length == 2 && args[0] == "--run-item")
        {
            MessageBox.Show($"Runner mode will execute item {args[1]} after Task 4 is complete.", "Auto Elevate Launcher");
            return 0;
        }

        Application.Run(new Form { Text = "Auto Elevate Launcher", Width = 900, Height = 600 });
        return 0;
    }
}