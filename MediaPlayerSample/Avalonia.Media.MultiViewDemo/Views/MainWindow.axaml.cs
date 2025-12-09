using Avalonia.Controls;
using Avalonia.Media.MultiViewDemo.ViewModels;

namespace Avalonia.Media.MultiViewDemo.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void NewDataContext()
    {
        DataContext = new SimpleViewModel();
    }
}
