using System;
using System.Threading.Tasks;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace UnofficialSamsungRemote.ControllerPages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TvList : Page
    {
        public TvList()
        {
            this.InitializeComponent();

            MSAdControl.ErrorOccurred += async (sender, e) =>
            {
                await SetAdControlVisibility(Windows.UI.Xaml.Visibility.Collapsed);
            };

            MSAdControl.AdRefreshed += async (s, e) =>
            {
                await ShowOrHideAdControl();
            };
        }

        private static bool IsTrial()
        {
#if DEBUG
            return true;
#else
            return Windows.ApplicationModel.Store.CurrentApp.LicenseInformation.IsTrial;
#endif
        }

        private async Task SetAdControlVisibility(Windows.UI.Xaml.Visibility visibility)
        {
            if (!MSAdControl.Dispatcher.HasThreadAccess)
            {
                await MSAdControl.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await SetAdControlVisibility(visibility);
                });

                return;
            }

            MSAdControl.Visibility = visibility;
            //MSAdControl.IsAutoRefreshEnabled = visibility == Windows.UI.Xaml.Visibility.Visible;
        }

        private async Task ShowOrHideAdControl()
        {
            if (IsTrial())
            {
                await SetAdControlVisibility(Windows.UI.Xaml.Visibility.Visible);
            }
            else
            {
                await SetAdControlVisibility(Windows.UI.Xaml.Visibility.Collapsed);
            }
        }

        private void TvListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPage.bEnabled)
            {
                var tv = (TvListBox.SelectedItem as TvItemViewModel);
                if (tv != null)
                {
                    MainPage.discoverer.StopSearching(Discovery.SearchEndReason.Complete);
                    MainPage.instance.ConnectTo(new Windows.Networking.HostName(tv.TvAddress), ushort.Parse(tv.Port));
                }
            }

            Frame.Navigate(typeof(Numpad));
        }

        private async void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile")
            {
                MSAdControl.Width = 480;
                MSAdControl.Height = 80;
                AdViewbox.MaxHeight = MSAdControl.Height;
            }

            if (System.Diagnostics.Debugger.IsAttached)
            {
                MSAdControl.ApplicationId = "test_client";
                MSAdControl.AdUnitId = "Image480_80";
            }
            else
            {
                await ShowOrHideAdControl();

                Windows.ApplicationModel.Store.CurrentApp.LicenseInformation.LicenseChanged += async () =>
                {
                    await ShowOrHideAdControl();
                };
            }
        }
    }
}
