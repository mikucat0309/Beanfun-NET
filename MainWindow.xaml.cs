using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace Beanfun;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void GameAccounts_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm = DataContext as MainWindowViewModel;
        vm?.GameAccounts_OnSelectionChanged();
    }

    private void PasswordBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (Keyboard.Modifiers)
        {
            case ModifierKeys.Control when e.Key == Key.A:
            case ModifierKeys.Control when e.Key == Key.C:
                return;
            default:
                e.Handled = true;
                break;
        }
    }

    private void PasswordBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        (e.Source as PasswordBox)!.SelectAll();
    }

    private void PasswordBox_OnGotMouseCapture(object sender, MouseEventArgs e)
    {
        (e.Source as PasswordBox)!.SelectAll();
    }
}