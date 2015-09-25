using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace UnofficialSamsungRemote
{
    public sealed partial class MainPage : Page
    {
        internal static MainPage instance;

        internal static Discovery discoverer;
        internal static TvConnection directConn;
        private bool bFirstLoad = true;
        private DateTime inputShown;
        public static bool bEnabled { get; private set; }

        private const int IanaInterfaceType_Ethernet = 6;
        private const int IanaInterfaceType_WiFi = 71;

        private Type AboutPageType { get { return typeof(About); } }
        private Type SettingsPageType { get { return typeof(UserSettings); } }

        private List<NavMenuItem> navlist = new List<NavMenuItem>()
        {
            new NavMenuItem()
            {
                Symbol = Symbol.List,
                Label = "tv list",
                DestPage = typeof(ControllerPages.TvList)
            },
            new NavMenuItem()
            {
                Symbol = Symbol.Keyboard,
                Label = "numpad",
                DestPage = typeof(ControllerPages.Numpad)
            },
            new NavMenuItem()
            {
                Symbol = Symbol.Forward,
                Label = "navigation",
                DestPage = typeof(ControllerPages.Navigation)
            },
            new NavMenuItem()
            {
                Symbol = Symbol.Play,
                Label = "control",
                DestPage = typeof(ControllerPages.Control)
            },
            new NavMenuItem()
            {
                Symbol = Symbol.More,
                Label = "misc",
                DestPage = typeof(ControllerPages.Misc)
            },
        };

        public MainPage()
        {
            instance = this;

            this.InitializeComponent();

            DataContext = App.ViewModel;

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

            NavigationCacheMode = NavigationCacheMode.Enabled;

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
                frame.Focus(FocusState.Programmatic);
            }
            else if (discoverer.HandleBackButton())
            {
                if (e != null)
                {
                    e.Handled = true;
                }
            }
            else
            {
                if (frame.CanGoBack)
                {
                    frame.GoBack();
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
                        bEnabled = false;
                        SetProgressText("Timed out searching for a TV.");
                        await btnDemoMode.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            btnDemoMode.Visibility = Visibility.Visible;
                        });
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

        async void discoverer_StartedSearching()
        {
            ToggleProgressBar(true);
            SetProgressText("Searching for TV...");
            bEnabled = true;
            await btnDemoMode.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                btnDemoMode.Visibility = Visibility.Collapsed;
            });
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
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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

            frame.Navigate(typeof(ControllerPages.TvList));
        }

        private async Task ConditionalFindDevices()
        {
            bool bConnectedToNonCellNetwork = false;
            var profiles = Windows.Networking.Connectivity.NetworkInformation.GetConnectionProfiles();
            foreach (var profile in profiles)
            {
                if (profile != null
                    && (profile.IsWlanConnectionProfile || profile.NetworkAdapter.IanaInterfaceType == IanaInterfaceType_Ethernet || profile.NetworkAdapter.IanaInterfaceType == IanaInterfaceType_WiFi))
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

        internal void OnQwertyButtonPressed(object sender, EventArgs args)
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

        internal void OnPowerButtonPressed(object sender, EventArgs args)
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

            frame.Navigate(typeof(ControllerPages.TvList));
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
        internal void ConnectTo(Windows.Networking.HostName host, UInt16 port)
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

        async void directConn_Disconnected()
        {
            ToggleProgressBar(false);
            SetProgressText("Disconnected from TV.");
            if (directConn != null && !directConn.bSentPowerOff)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    OnPowerButtonPressed(null, null);
                });
                await DisplayOkBox("Lost connection to device.");
            }
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

        private async void NavMenuList_ItemInvoked(object sender, ListViewItem listViewItem)
        {
            var navListSender = sender as NavMenuListView;
            var item = navListSender.ItemFromContainer(listViewItem) as NavMenuItem;

            if (item != null)
            {
                if (!item.OnInvoked(frame, sender))
                {
                    // ugly, but immediately updating the navlist status was not working, so...
                    await Task.Delay(50);
                    SelectNavEntryMatching(frame.CurrentSourcePageType);
                }
            }
        }

        /// <summary>
        /// Enable accessibility on each nav menu item by setting the AutomationProperties.Name on each container
        /// using the associated Label of each item.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void NavMenuItemContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue && args.Item != null && args.Item is NavMenuItem)
            {
                args.ItemContainer.SetValue(AutomationProperties.NameProperty, ((NavMenuItem)args.Item).Label);
            }
            else
            {
                args.ItemContainer.ClearValue(AutomationProperties.NameProperty);
            }
        }

        /// <summary>
        /// Ensures the nav menu reflects reality when navigation is triggered outside of
        /// the nav menu buttons.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNavigatingToPage(object sender, NavigatingCancelEventArgs e)
        {
            SelectNavEntryMatching(e.SourcePageType);
        }

        private void SelectNavEntryMatching(Type type)
        {
            var item = (from p in NavMenuListControls.Items where (p as NavMenuItem).DestPage == type select p).FirstOrDefault();
            if (item == null)
            {
                item = (from p in NavMenuList.Items where (p as NavMenuItem).DestPage == type select p).FirstOrDefault();
            }
            if (item == null && frame.BackStackDepth > 0)
            {
                // In cases where a page drills into sub-pages then we'll highlight the most recent
                // navigation menu item that appears in the BackStack
                foreach (var entry in frame.BackStack.Reverse())
                {
                    item = (from p in this.navlist where p.DestPage == entry.SourcePageType select p).FirstOrDefault();
                    if (item != null)
                    {
                        break;
                    }
                }
            }

            var NavMenu = NavMenuList;
            var container = NavMenu.ContainerFromItem(item) as ListViewItem;
            if (container == null)
            {
                NavMenu = NavMenuListControls;
                container = NavMenu.ContainerFromItem(item) as ListViewItem;
            }

            // While updating the selection state of the item prevent it from taking keyboard focus.  If a
            // user is invoking the back button via the keyboard causing the selected nav menu item to change
            // then focus will remain on the back button.
            if (container != null)
            {
                container.IsTabStop = false;
                NavMenuList.SetSelectedItem(container);
                NavMenuListControls.SetSelectedItem(container);
                container.IsTabStop = true;
            }
        }

        private void OnNavigatedToPage(object sender, NavigationEventArgs e)
        {
            // After a successful navigation set keyboard focus to the loaded page
            if (e.Content is Page && e.Content != null)
            {
                var control = (Page)e.Content;
                control.Loaded += NavToPage_Loaded;
            }
        }

        private void NavToPage_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as Page).Focus(FocusState.Programmatic);
            (sender as Page).Loaded -= NavToPage_Loaded;
            this.CheckTogglePaneButtonSizeChanged();
        }

        /// <summary>
        /// Callback when the SplitView's Pane is toggled open or close.  When the Pane is not visible
        /// then the floating hamburger may be occluding other content in the app unless it is aware.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TogglePaneButton_Checked(object sender, RoutedEventArgs e)
        {
            this.CheckTogglePaneButtonSizeChanged();
        }

        public Rect TogglePaneButtonRect
        {
            get;
            private set;
        }

        public event TypedEventHandler<MainPage, Rect> TogglePaneButtonRectChanged;

        /// <summary>
        /// Check for the conditions where the navigation pane does not occupy the space under the floating
        /// hamburger button and trigger the event.
        /// </summary>
        private void CheckTogglePaneButtonSizeChanged()
        {
            if (this.RootSplitView.DisplayMode == SplitViewDisplayMode.Inline ||
                this.RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                var transform = this.TogglePaneButton.TransformToVisual(this);
                var rect = transform.TransformBounds(new Rect(0, 0, this.TogglePaneButton.ActualWidth, this.TogglePaneButton.ActualHeight));
                this.TogglePaneButtonRect = rect;
            }
            else
            {
                this.TogglePaneButtonRect = new Rect();
            }

            var handler = this.TogglePaneButtonRectChanged;
            if (handler != null)
            {
                // handler(this, this.TogglePaneButtonRect);
                handler.DynamicInvoke(this, this.TogglePaneButtonRect);
            }
        }
    }
}
