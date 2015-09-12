using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UnofficialSamsungRemote
{
    public sealed partial class About : Page
    {
        public About()
        {
            InitializeComponent();

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

        private async void Contact_Click(object sender, RoutedEventArgs e)
        {
            await App.ShowComposeEmail("samsungremote@perniciousgames.com",
                "Unofficial Samsung Remote support request",
                "My Unofficial Samsung Remote version: " + MainPage.GetVersionNumber() + System.Environment.NewLine + System.Environment.NewLine);
        }

        private async void RateUs_Click(object sender, RoutedEventArgs e)
        {
            await App.ShowMyReviewPage();
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
