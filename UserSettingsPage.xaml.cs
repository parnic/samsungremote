using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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