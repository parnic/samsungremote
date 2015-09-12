using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UnofficialSamsungRemote
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserSettings : Page
    {
        public UserSettings()
        {
            this.InitializeComponent();

            ApplicationTitle.Text = ApplicationTitle.Text.Replace("{v}", MainPage.GetVersionNumber());

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
        }

        private void OnBackRequested(object sender = null, BackRequestedEventArgs e = null)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
                if (e != null)
                {
                    e.Handled = true;
                }
            }
        }

        private void Page_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                OnBackRequested();
                e.Handled = true;
            }
        }

        private void Page_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            App.OnCheckMouseBack(sender, e, () => { OnBackRequested(); });
        }
    }
}
