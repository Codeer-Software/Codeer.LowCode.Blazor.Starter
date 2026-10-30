using LowCodeApp.Maui.Services;

namespace LowCodeApp.Maui
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            ServerUrlEntry.Text = ServerSettings.BaseUrl;
            DefaultLabel.Text = $"Default (appsettings.json): {ServerSettings.DefaultBaseUrl}";
        }

        void OnSaveClicked(object? sender, EventArgs e)
        {
            var text = ServerUrlEntry.Text?.Trim() ?? string.Empty;
            if (!ServerSettings.IsValidUrl(text))
            {
                ErrorLabel.IsVisible = true;
                return;
            }
            ErrorLabel.IsVisible = false;
            Apply(text.EndsWith('/') ? text : text + "/");
        }

        void OnResetClicked(object? sender, EventArgs e) => Apply(ServerSettings.DefaultBaseUrl);

        //HttpClient は WebView の寿命の間使い回すので、メインページを作り直して新しいサーバーに切り替える
        void Apply(string url)
        {
            ServerSettings.BaseUrl = url;
            if (Window != null) Window.Page = App.CreateMainPage();
        }
    }
}
