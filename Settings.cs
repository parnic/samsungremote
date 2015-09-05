using System.ComponentModel;

namespace UnofficialSamsungRemote
{
    public class Settings : INotifyPropertyChanged
    {
        public static Settings LoadedSettings = new Settings();
        private static bool bIsLoading = true;

        private bool _bShouldVibrateOnKeyPress;
        public bool bShouldVibrateOnKeyPress
        {
            get
            {
                return _bShouldVibrateOnKeyPress;
            }
            set
            {
                _bShouldVibrateOnKeyPress = value;
                NotifyPropertyChanged(nameof(bShouldVibrateOnKeyPress));

                if (!bIsLoading)
                {
                    TvKeyControl.ConditionalConfirmationVibration();
                    App.SaveSetting(nameof(bShouldVibrateOnKeyPress), _bShouldVibrateOnKeyPress);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void SetDefaults()
        {
            bShouldVibrateOnKeyPress = true;
        }

        public void OnLoaded()
        {
            bIsLoading = false;
        }
    }
}
