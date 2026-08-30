using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using LowCodeApp.Wpf.Services;

namespace LowCodeApp.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddWpfBlazorWebView();
            serviceCollection.AddSharedServices();

            Resources.Add("services", serviceCollection.BuildServiceProvider());
        }
    }
}
