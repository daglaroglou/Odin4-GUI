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

        // Apply dark theme
        Settings.Default.ApplicationPreferDarkTheme = true;

        Window window = new Window("My GTK# App");
        window.SetDefaultSize(400, 300);
        window.DeleteEvent += (o, args) => Application.Quit();

        // Create a box to hold the label and button
        Box vbox = new Box(Orientation.Vertical, 2);

        // Create the label
        Label label = new Label("Hello, Linux GUI with C#!");
        vbox.PackStart(label, true, true, 0);

        // Create the theme toggle button
        Button themeToggleButton = new Button();
        Image sunIcon = new Image(Stock.Add, IconSize.Button);
        Image moonIcon = new Image(Stock.Remove, IconSize.Button);
        themeToggleButton.Image = moonIcon;
        themeToggleButton.Clicked += (sender, e) =>
        {
            if (Settings.Default.ApplicationPreferDarkTheme)
            {
                Settings.Default.ApplicationPreferDarkTheme = false;
                themeToggleButton.Image = sunIcon;
            }
            else
            {
                Settings.Default.ApplicationPreferDarkTheme = true;
                themeToggleButton.Image = moonIcon;
            }
        };

        // Align the button to the top right
        Box alignmentBox = new Box(Orientation.Horizontal, 0);
        alignmentBox.PackEnd(themeToggleButton, false, false, 0);
        vbox.PackStart(alignmentBox, false, false, 0);

        window.Add(vbox);
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
