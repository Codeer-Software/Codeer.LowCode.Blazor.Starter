namespace LowCodeApp.Maui
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(CreateMainPage());

        //NavigationPage にして、設定 (SettingsPage) を開くタイトルバーを持たせる
        public static Page CreateMainPage() => new NavigationPage(new MainPage());
    }
}
