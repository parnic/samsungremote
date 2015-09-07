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

            SystemNavigationManager.GetForCurrentView().BackRequested += (s, e) =>
            {
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                    e.Handled = true;
                }
            };
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
    }
}
