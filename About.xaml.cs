using System.Windows;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;

namespace SamsungRemoteWP7
{
    public partial class About : PhoneApplicationPage
    {
        public About()
        {
            InitializeComponent();

            ApplicationTitle.Text = ApplicationTitle.Text.Replace("{v}", MainPage.GetVersionNumber());
        }

        private void Contact_Click(object sender, RoutedEventArgs e)
        {
            var emailTask = new EmailComposeTask();
            emailTask.To = "samsungremotewp7@perniciousgames.com";
            emailTask.Subject = "Unofficial Samsung Remote support request";
            emailTask.Body = "My Unofficial Samsung Remote version: " + MainPage.GetVersionNumber() + System.Environment.NewLine + System.Environment.NewLine;
            emailTask.Show();
        }

        private void RateUs_Click(object sender, RoutedEventArgs e)
        {
            MarketplaceReviewTask marketplaceReviewTask = new MarketplaceReviewTask();
            marketplaceReviewTask.Show();
        }
    }
}
