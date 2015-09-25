using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UnofficialSamsungRemote
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserSettings : Page
    {
        public Visibility CanVibrateAsVisibility
        {
            get
            {
                return Utilities.CanVibrate ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Settings LoadedSettings
        {
            get
            {
                return Settings.LoadedSettings;
            }
        }

        private bool? bShouldVibrateOnKeyPress
        {
            get
            {
                return LoadedSettings?.bShouldVibrateOnKeyPress ?? false;
            }
            set
            {
                if (LoadedSettings != null)
                {
                    LoadedSettings.bShouldVibrateOnKeyPress = value.HasValue ? value.Value : false;
                }
            }
        }

        public UserSettings()
        {
            this.InitializeComponent();
        }
    }
}
