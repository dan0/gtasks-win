using Microsoft.UI.Xaml.Controls;

namespace GTasks.UI.Controls;

public sealed partial class CommandPalette : UserControl
{
    public event EventHandler? CloseRequested;

    public CommandPalette()
    {
        InitializeComponent();
    }
}
