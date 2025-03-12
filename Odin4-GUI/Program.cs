using Gtk;
using System;

class Program
{
    static void Main()
    {
        if (!IsRunningAsRoot())
        {
            ShowSudoRequiredMessage();
            return;
        }

        Application.Init();

        Settings.Default.ApplicationPreferDarkTheme = true;

        Window window = new Window("Odin4");
        window.SetDefaultSize(1200, 700);
        window.DeleteEvent += (o, args) => Application.Quit();

        Notebook notebook = new Notebook();

        // Odin Tab
        Box odinBox = new Box(Orientation.Vertical, 2);
        Label odinLabel = new Label("Odin Content");
        odinBox.PackStart(odinLabel, true, true, 0);
        notebook.AppendPage(odinBox, new Label("Odin"));

        // ADB Tab
        Box adbBox = new Box(Orientation.Vertical, 2);
        Label adbLabel = new Label("SOON");
        adbBox.PackStart(adbLabel, true, true, 0);
        notebook.AppendPage(adbBox, new Label("ADB"));

        // GAPPS Tab
        Box gappsBox = new Box(Orientation.Vertical, 2);
        Label gappsLabel = new Label("SOON");
        gappsBox.PackStart(gappsLabel, true, true, 0);
        notebook.AppendPage(gappsBox, new Label("GAPPS"));

        window.Add(notebook);
        window.ShowAll();
        Application.Run();
    }

    private static bool IsRunningAsRoot()
    {
        return Environment.UserName == "root";
    }

    private static void ShowSudoRequiredMessage()
    {
        Application.Init();
        MessageDialog md = new MessageDialog(null, 
            DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok,
            "This program requires sudo permissions to run. Please restart it with 'sudo'.");
        md.Run();
        md.Destroy();
        Application.Quit();
    }
}
