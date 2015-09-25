using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

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
    }
}
