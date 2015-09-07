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

        private void Contact_Click(object sender, RoutedEventArgs e)
        {
            /*var emailTask = new EmailComposeTask();
            emailTask.To = "samsungremotewp7@perniciousgames.com";
            emailTask.Subject = "Unofficial Samsung Remote support request";
            emailTask.Body = "My Unofficial Samsung Remote version: " + MainPage.GetVersionNumber() + System.Environment.NewLine + System.Environment.NewLine;
            emailTask.Show();*/
        }

        private async void RateUs_Click(object sender, RoutedEventArgs e)
        {
            await App.ShowMyReviewPage();
        }
    }
}
