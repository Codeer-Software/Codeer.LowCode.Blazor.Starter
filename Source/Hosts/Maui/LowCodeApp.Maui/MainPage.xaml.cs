namespace LowCodeApp.Maui
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        async void OnSettingsClicked(object? sender, EventArgs e)
            => await Navigation.PushAsync(new SettingsPage());

        //BlazorWebView を作り直してデザインをサーバーから読み直す (新しいデザインを配置した後に使う)
        void OnReloadClicked(object? sender, EventArgs e)
        {
            if (Window != null) Window.Page = App.CreateMainPage();
        }
    }
}
