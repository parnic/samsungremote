using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UnofficialSamsungRemote
{
    public sealed partial class MainPage : Page
    {
        private Discovery discoverer;
        private static TvConnection directConn;
        private bool bFirstLoad = true;
        private DateTime inputShown;
        public static bool bEnabled { get; private set; }

        private const int IanaInterfaceType_Ethernet = 6;

        public MainPage()
        {
            this.InitializeComponent();

            MainPivot.Title = (MainPivot.Title as string).Replace("{v}", MainPage.GetVersionNumber());

            DataContext = App.ViewModel;

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;

            discoverer = new Discovery();
            discoverer.StartedSearching += new Discovery.StartedSearchingDelegate(discoverer_StartedSearching);
            discoverer.SearchingEnded += new Discovery.SearchingEndedDelegate(discoverer_SearchingEnded);
            discoverer.TvFound += new Discovery.TvFoundDelegate(discoverer_TvFound);
        }

        private void OnBackRequested(object sender = null, BackRequestedEventArgs e = null)
        {
            if (TextInputOverlay.Visibility == Visibility.Visible)
            {
                TextInputOverlay.Visibility = Visibility.Collapsed;
                if (e != null)
                {
                    e.Handled = true;
                }
                txtInput.Text = "";
                MainPivot.Focus(FocusState.Programmatic);
            }
            else if (discoverer.HandleBackButton())
            {
                if (e != null)
                {
                    e.Handled = true;
                }
            }
        }

        public static string GetVersionNumber()
        {
            int major, minor;
            if (GetVersion(out major, out minor))
            {
                return major + "." + minor;
            }

            return "1.0";
        }

        public static bool GetVersion(out int Major, out int Minor)
        {
            try
            {
                var asm = typeof(MainPage).GetTypeInfo().Assembly;
                var parts = asm.FullName.Split(',');
                var version = parts[1].Split('=')[1].Split('.');

                Major = Convert.ToInt32(version[0]);
                Minor = Convert.ToInt32(version[1]);

                return true;
            }
            catch (Exception) { }

            Major = 0;
            Minor = 0;

            return false;
        }

        public static void SendKey(EKey key)
        {
            if (directConn != null && bEnabled)
            {
                directConn.SendKey(key);
            }
        }

        public static void SendText(string text)
        {
            if (directConn != null && bEnabled)
            {
                directConn.SendText(text);
            }
        }

        #region discovery callbacks and processing
        async void discoverer_SearchingEnded(Discovery.SearchEndReason reason)
        {
            switch (reason)
            {
                case Discovery.SearchEndReason.Error:
                    bEnabled = false;
                    SetProgressText("TV search failed.");
                    ToggleProgressBar(true);
                    break;

                case Discovery.SearchEndReason.TimedOut:
                default:
                    if (App.ViewModel.TvItems.Count == 0)
                    {
                        SetProgressText("Timed out searching for a TV.");
                        await btnDemoMode.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            btnDemoMode.Visibility = Visibility.Visible;
                        });
                        bEnabled = false;
                        ToggleProgressBar(true);
                    }
                    else
                    {
                        ToggleProgressBar(false);
                    }
                    break;
            }
        }

        async void discoverer_TvFound(Windows.Networking.HostName TvHost, UInt16 TvPort, string TvResponse)
        {
            ToggleProgressBar(false);
            SetProgressText("Found TV.");
            await AddTvUnique(TvHost, TvPort);

            await GetTvNameFrom(TvHost, TvPort, TvResponse);
        }

        void discoverer_StartedSearching()
        {
            SetProgressText("Searching for TV...");
        }

        private async Task<string> GetTvNameFrom(Windows.Networking.HostName TvHost, UInt16 TvPort, string tvData)
        {
            var tvDataLines = tvData.Split('\n');
            var TvKeyValuePairs = new Dictionary<string, string>();

            foreach (var line in tvDataLines)
            {
                string key = null, value = null;
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == ':')
                    {
                        key = line.Substring(0, i).Trim();
                        value = line.Substring(i + 1).Trim();
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    TvKeyValuePairs.Add(key.ToLower(), value);
                }
            }

            if (TvKeyValuePairs.Count > 0)
            {
                await SetMetaDataForTv(TvHost, TvKeyValuePairs);

                if (TvKeyValuePairs.ContainsKey("location"))
                {
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                    var resp = System.Text.Encoding.UTF8.GetString(await client.GetByteArrayAsync(TvKeyValuePairs["location"]));
                    var doc = XDocument.Parse(resp);
                    string tvName = doc.Descendants().Where(x => x.Name.LocalName == "friendlyName").First().Value;
                    await SetTvName(TvHost, tvName);
                }
            }

            return null;
        }
        #endregion

        #region utilities
        private async Task SetTvName(Windows.Networking.HostName TvHost, string TvName)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (var i in App.ViewModel.TvItems)
                {
                    if (i.TvAddress == TvHost.ToString())
                    {
                        i.TvName = TvName;
                        break;
                    }
                }
            });
        }

        private async Task SetMetaDataForTv(Windows.Networking.HostName TvHost, Dictionary<string, string> TvKeyValuePairs)
        {
            await TvListBox.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (var i in App.ViewModel.TvItems)
                {
                    if (i.Equals(TvHost))
                    {
                        i.TvMetaData = TvKeyValuePairs;
                        break;
                    }
                }
            });
        }

        async Task AddTvUnique(Windows.Networking.HostName TvHost, UInt16 TvPort)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (var i in App.ViewModel.TvItems)
                {
                    if (i.Equals(TvHost))
                    {
                        return;
                    }
                }

                App.ViewModel.TvItems.Add(new TvItemViewModel()
                {
                    Port = TvConnection.TvDirectPort.ToString(),
                    TvAddress = TvHost.ToString(),
                });
            });
        }

        async void SetProgressText(string text)
        {
            System.Diagnostics.Debug.WriteLine(text);
            await progressText.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                progressText.Text = text;
            });
        }

        public static async Task DisplayOkBox(string message, string title = null)
        {
            if (CoreWindow.GetForCurrentThread() == null)
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await InternalDisplayOkBox(message, title);
                });
            }
            else
            {
                await InternalDisplayOkBox(message, title);
            }
        }

        private static async Task InternalDisplayOkBox(string message, string title = null)
        {
            MessageDialog msg = null;
            if (title == null)
            {
                msg = new MessageDialog(message);
            }
            else
            {
                msg = new MessageDialog(message, title);
            }

            msg.Commands.Add(new UICommand("OK"));
            await msg.ShowAsync();
        }

        public static async Task DisplayYesNoBox(string message, string title = null, UICommandInvokedHandler YesHandler = null, UICommandInvokedHandler NoHandler = null)
        {
            if (CoreWindow.GetForCurrentThread() == null)
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await InternalDisplayYesNoBox(message, title, YesHandler, NoHandler);
                });
            }
            else
            {
                await InternalDisplayYesNoBox(message, title, YesHandler, NoHandler);
            }
        }

        private static async Task InternalDisplayYesNoBox(string message, string title = null, UICommandInvokedHandler YesHandler = null, UICommandInvokedHandler NoHandler = null)
        {
            MessageDialog msg = null;
            if (title == null)
            {
                msg = new MessageDialog(message);
            }
            else
            {
                msg = new MessageDialog(message, title);
            }

            msg.Commands.Add(new UICommand("Yes", YesHandler));
            msg.Commands.Add(new UICommand("No", NoHandler));
            await msg.ShowAsync();
        }
        #endregion

        #region button/app event handlers
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (bFirstLoad)
            {
                await ConditionalFindDevices();
            }

            bFirstLoad = false;
        }

        private async Task ConditionalFindDevices()
        {
            btnDemoMode.Visibility = Visibility.Collapsed;

            bool bConnectedToNonCellNetwork = false;
            var profiles = Windows.Networking.Connectivity.NetworkInformation.GetConnectionProfiles();
            foreach (var profile in profiles)
            {
                if (profile != null
                    && (profile.IsWlanConnectionProfile || profile.NetworkAdapter.IanaInterfaceType == IanaInterfaceType_Ethernet))
                {
                    bConnectedToNonCellNetwork = true;
                    break;
                }
            }

            ToggleProgressBar(true);

            if (bConnectedToNonCellNetwork)
            {
                bEnabled = true;
                discoverer.FindTvs();
            }
            else
            {
                SetProgressText("Remote disabled.");
                btnDemoMode.Visibility = Visibility.Visible;
                TransparentOverlay.Visibility = Visibility.Visible;

                await DisplayOkBox("You must be connected to a non-cell network in order to connect to a TV. Connect to the same network as your device and try again.");
            }
        }

        private async void ToggleProgressBar(bool? bEnableProgressBar = false)
        {
            if (!bEnableProgressBar.HasValue)
            {
                ToggleProgressBar(!customIndeterminateProgressBar.IsIndeterminate);
                return;
            }

            await customIndeterminateProgressBar.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                customIndeterminateProgressBar.IsIndeterminate = bEnableProgressBar.Value;

                if (bEnableProgressBar.Value)
                {
                    if (!bEnabled)
                    {
                        customIndeterminateProgressBar.Visibility = Visibility.Collapsed;
                        customIndeterminateProgressBar.IsIndeterminate = false;
                    }
                    else
                    {
                        customIndeterminateProgressBar.Visibility = Visibility.Visible;
                    }

                    TransparentOverlay.Visibility = Visibility.Visible;
                    TransparentOverlay.Opacity = 0.85;
                }
                else
                {
                    customIndeterminateProgressBar.Visibility = Visibility.Collapsed;
                    TransparentOverlay.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void TvListBox_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (!bEnabled)
            {
                MainPivot.SelectedIndex = 1;
                return;
            }

            var tv = (TvListBox.SelectedItem as TvItemViewModel);
            if (tv != null)
            {
                ConnectTo(new Windows.Networking.HostName(tv.TvAddress), ushort.Parse(tv.Port));
                MainPivot.SelectedIndex = 1;
                discoverer.StopSearching(Discovery.SearchEndReason.Complete);
            }
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Pivot piv = sender as Pivot;
            if (piv == null)
            {
                return;
            }

            if (piv.SelectedIndex == 0)
            {
                BottomAppBar.Visibility = Visibility.Visible;
            }
            else
            {
                BottomAppBar.Visibility = Visibility.Collapsed;
            }
        }

        private void btnDemoMode_Click(object sender, RoutedEventArgs e)
        {
            bEnabled = false;

            btnDemoMode.Visibility = Visibility.Collapsed;

            ToggleProgressBar(false);

            var rand = new Random((int)DateTime.Now.Ticks);
            for (int i = 1; i <= 9; i++)
            {
                App.ViewModel.TvItems.Add(new TvItemViewModel()
                {
                    Port = TvConnection.TvDirectPort.ToString(),
                    TvAddress = "192.168.100." + rand.Next(1, 255),
                    TvName = "Example TV " + i
                });
            }
        }

        private async void RefreshTvList_Click(object sender, RoutedEventArgs e)
        {
            App.ViewModel.TvItems.Clear();
            await ConditionalFindDevices();
        }

        private void OnQwertyButtonPressed(object sender, EventArgs args)
        {
            txtInput.Focus(FocusState.Programmatic);
            TextInputOverlay.Visibility = Visibility.Visible;
        }

        private void txtInput_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (txtInput.Text.Length > 0)
                {
                    SendText(txtInput.Text);
                }
                OnBackRequested();
            }
        }

        private void txtInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (TextInputOverlay.Visibility == Visibility.Visible && (DateTime.Now - inputShown).TotalMilliseconds > 250)
            {
                OnBackRequested();
            }
        }

        private void txtInput_GotFocus(object sender, RoutedEventArgs e)
        {
            inputShown = DateTime.Now;
        }

        private void OnPowerButtonPressed(object sender, EventArgs args)
        {
            if (directConn != null && directConn.ConnectedHostName != null)
            {
                for (int i = App.ViewModel.TvItems.Count - 1; i >= 0; i--)
                {
                    if (App.ViewModel.TvItems[i].TvAddress == directConn.ConnectedHostName.ToString())
                    {
                        App.ViewModel.TvItems.RemoveAt(i);
                    }
                }

                directConn.bSentPowerOff = true;
            }

            MainPivot.SelectedIndex = 0;
        }

        public static void NotifyAppFreshStart()
        {
            //bFirstLoad = true;
        }

        public static void NotifyAppDeactivated()
        {
            if (directConn != null)
            {
                directConn.NotifyAppDeactivated();
            }
        }

        public static void NotifyAppActivated()
        {
            if (directConn != null)
            {
                directConn.NotifyAppActivated();
            }
        }
        #endregion

        #region direct connection handling/callbacks
        private void ConnectTo(Windows.Networking.HostName host, UInt16 port)
        {
            directConn = new TvConnection(host, port);
            directConn.Connecting += new TvConnection.ConnectingDelegate(directConn_Connecting);
            directConn.Connected += new TvConnection.ConnectedDelegate(directConn_Connected);
            directConn.Disconnected += new TvConnection.DisconnectedDelegate(directConn_Disconnected);
            directConn.Registering += new TvConnection.RegisteringDelegate(directConn_Registering);
            directConn.RegistrationFailed += new TvConnection.RegistrationFailedDelegate(directConn_RegistrationFailed);
            directConn.RegistrationAccepted += new TvConnection.RegistrationAcceptedDelegate(directConn_RegistrationAccepted);
            directConn.RegistrationDenied += new TvConnection.RegistrationDeniedDelegate(directConn_RegistrationDenied);
            directConn.RegistrationTimedOut += new TvConnection.RegistrationTimedOutDelegate(directConn_RegistrationTimedOut);
            directConn.RegistrationWaiting += new TvConnection.RegistrationWaitingDelegate(directConn_RegistrationWaiting);
            directConn.Connect();
        }

        void directConn_Disconnected()
        {
            ToggleProgressBar(false);
            SetProgressText("Disconnected from TV.");
        }

        void directConn_Connected()
        {
            SetProgressText("Connected to TV. Sending registration...");
        }

        void directConn_RegistrationWaiting()
        {
            SetProgressText("Waiting for user authorization...");
        }

        async void directConn_RegistrationTimedOut()
        {
            SetProgressText("Remote connection timed out.");
            await DisplayOkBox("Remote connection timed out.");
        }

        async void directConn_RegistrationDenied()
        {
            SetProgressText("Remote connection denied.");
            await DisplayOkBox("Remote connection denied.");
        }

        void directConn_RegistrationAccepted()
        {
            SetProgressText("Remote approved!");
            ToggleProgressBar(false);
        }

        async void directConn_RegistrationFailed()
        {
            SetProgressText("Sending remote registration failed.");
            ToggleProgressBar(false);
            await DisplayOkBox("Sending remote registration failed.");
        }

        void directConn_Registering()
        {
            SetProgressText("Registering remote with TV...");
        }

        void directConn_Connecting()
        {
            ToggleProgressBar(true);
            SetProgressText("Connecting to TV at " + directConn.ConnectedHostName.ToString() + "...");
        }
        #endregion

        private void ShowAppinfo_Click(object sender, RoutedEventArgs e)
        {
            if (discoverer != null)
            {
                discoverer.StopSearching();
            }

            Frame.Navigate(typeof(About));
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            if (discoverer != null)
            {
                discoverer.StopSearching();
            }

            Frame.Navigate(typeof(UserSettings));
        }
    }
}
