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
            RefreshLog();
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

        void RefreshLog()
        {
            LogLabel.Text = AppLog.HasLog ? AppLog.ReadTail(20_000) : "No log entries.";
            ShareLogButton.IsEnabled = AppLog.HasLog;
            ClearLogButton.IsEnabled = AppLog.HasLog;
        }

        async void OnShareLogClicked(object? sender, EventArgs e)
        {
            if (!AppLog.HasLog) return;
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "app.log",
                File = new ShareFile(AppLog.LogFilePath)
            });
        }

        void OnRefreshLogClicked(object? sender, EventArgs e) => RefreshLog();

        void OnClearLogClicked(object? sender, EventArgs e)
        {
            AppLog.Clear();
            RefreshLog();
        }

        //The Blazor side keeps its HttpClient for the lifetime of the WebView, so the app restarts the
        //main page to pick up the new server.
        void Apply(string url)
        {
            ServerSettings.BaseUrl = url;
            if (Window != null) Window.Page = App.CreateMainPage();
        }
    }
}
