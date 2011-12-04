using Microsoft.Phone.Controls;

namespace SamsungRemoteWP7
{
    public partial class UserSettingsPage : PhoneApplicationPage
    {
        public UserSettingsPage()
        {
            InitializeComponent();

            ApplicationTitle.Text = ApplicationTitle.Text.Replace("{v}", MainPage.GetVersionNumber());
        }
    }
}