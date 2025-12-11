using Avalonia.Controls;
using Avalonia.Interactivity;

namespace client.Views.Components;

public partial class Sidebar : UserControl
{
    public Sidebar()
    {
        InitializeComponent();
    }

    private void MenuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Перемикаємо режими
        bool toMenu = !MenuGrid.IsVisible;

        MenuGrid.IsVisible = toMenu;
        MainSidebarGrid.IsVisible = !toMenu;
    }
}
