using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Phone.Controls;
using System.IO;
using System.Xml.Linq;
using Microsoft.Phone.Net.NetworkInformation;

namespace SamsungRemoteWP7
{
    public partial class MainPage : PhoneApplicationPage
    {
        public static bool bEnabled { get; private set; }

        private Discovery discoverer;
        private static TvConnection directConn;

        public MainPage()
        {
            InitializeComponent();

            DataContext = App.ViewModel;
            this.Loaded += new RoutedEventHandler(MainPage_Loaded);

            discoverer = new Discovery();
            discoverer.StartedSearching += new Discovery.StartedSearchingDelegate(discoverer_StartedSearching);
            discoverer.SearchingEnded += new Discovery.SearchingEndedDelegate(discoverer_SearchingEnded);
            discoverer.TvFound += new Discovery.TvFoundDelegate(discoverer_TvFound);

            if ((NetworkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                || NetworkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                /*&& !System.Diagnostics.Debugger.IsAttached*/)
            {
                bEnabled = true;
            }
            else
            {
                MessageBox.Show("You must be connected to a non-cell network in order to connect to a TV. Connect via Wi-Fi or wired through a USB cable and try again.",
                    "Can't search for TV", MessageBoxButton.OK);

                SetProgressText("Remote disabled.");
                ToggleProgressBar(true);
                btnDemoMode.Visibility = Visibility.Visible;
            }
        }

        public static void SendKey(TvKeyControl.EKey key)
        {
            if (directConn != null && bEnabled)
            {
                directConn.SendKey(key);
            }
        }

        #region discovery callbacks and processing
        void discoverer_SearchingEnded(Discovery.SearchEndReason reason)
        {
            switch (reason)
            {
                case Discovery.SearchEndReason.TimedOut:
                    if (App.ViewModel.TvItems.Count == 0)
                    {
                        SetProgressText("Timed out searching for a TV.");
                        btnDemoMode.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            btnDemoMode.Visibility = Visibility.Visible;
                        }));
                        bEnabled = false;
                        ToggleProgressBar(true);
                    }
                    break;

                case Discovery.SearchEndReason.Error:
                    SetProgressText("TV search failed.");
                    break;

                default:
                    ToggleProgressBar(false);
                    break;
            }
        }

        void discoverer_TvFound(EndPoint TvEndpoint, string TvResponse)
        {
            ToggleProgressBar(false);
            SetProgressText("Found TV.");
            AddTvUnique(TvEndpoint);

            GetTvNameFrom(TvEndpoint, TvResponse);
        }

        void discoverer_StartedSearching()
        {
            SetProgressText("Searching for TV...");
        }

        private void GetTvNameFrom(EndPoint endPoint, string tvData)
        {
            var tvDataLines = tvData.Split('\n');
            var TvKeyValuePairs = new Dictionary<string, string>();

            foreach (var line in tvDataLines)
            {
                string key = null, value = null;
                for (int i=0; i<line.Length; i++)
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
                SetMetaDataForTv(endPoint, TvKeyValuePairs);

                if (TvKeyValuePairs.ContainsKey("location"))
                {
                    WebClient client = new WebClient();
                    client.OpenReadCompleted += new OpenReadCompletedEventHandler(TvReceiverDataRequestComplete);
                    client.OpenReadAsync(new Uri(TvKeyValuePairs["location"]), endPoint);
                }
            }
        }

        void TvReceiverDataRequestComplete(object sender, OpenReadCompletedEventArgs e)
        {
            if (e.Error == null && e.UserState is EndPoint)
            {
                try
                {
                    string resultData = new StreamReader(e.Result).ReadToEnd();
                    var doc = XDocument.Parse(resultData);
                    string tvName = doc.Descendants().Where(x => x.Name.LocalName == "friendlyName").First().Value;

                    SetTvName(e.UserState as EndPoint, tvName);
                }
                catch(Exception) { }
            }
        }
        #endregion

        #region utilities
        private void SetTvName(EndPoint endPoint, string tvName)
        {
            TvListBox.Dispatcher.BeginInvoke(new Action(delegate
            {
                IPAddress addr = (endPoint as IPEndPoint).Address;
                foreach (var i in App.ViewModel.TvItems)
                {
                    if (i.TvAddress == addr)
                    {
                        i.TvName = tvName;
                        break;
                    }
                }
            }));
        }

        private void SetMetaDataForTv(EndPoint endPoint, Dictionary<string, string> TvKeyValuePairs)
        {
            TvListBox.Dispatcher.BeginInvoke(new Action(delegate
            {
                IPAddress addr = (endPoint as IPEndPoint).Address;
                foreach (var i in App.ViewModel.TvItems)
                {
                    if (i.TvAddress == addr)
                    {
                        i.TvMetaData = TvKeyValuePairs;
                        break;
                    }
                }
            }));
        }

        private void AddTvUnique(EndPoint endPoint, string inTvName = "")
        {
            TvListBox.Dispatcher.BeginInvoke(new Action(delegate
            {
                IPAddress addr = (endPoint as IPEndPoint).Address;
                foreach (var i in App.ViewModel.TvItems)
                {
                    if (i.TvAddress.ToString() == addr.ToString())
                    {
                        return;
                    }
                }

                App.ViewModel.TvItems.Add(new TvItemViewModel()
                {
                    Port = TvConnection.TvDirectPort,
                    TvAddress = addr,
                    TvName = string.IsNullOrWhiteSpace(inTvName) ? addr.ToString() : inTvName,
                });
            }));
        }

        private void SetProgressText(string p)
        {
            progressText.Dispatcher.BeginInvoke(new Action(delegate
            {
                progressText.Text = p;
            }));
        }
        #endregion

        #region button/app event handlers
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }

            ToggleProgressBar(true);
            discoverer.FindTvs();
        }

        private void PhoneApplicationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (discoverer != null)
            {
                discoverer.Cleanup();
            }
            if (directConn != null)
            {
                directConn.Cleanup();
            }
        }

        private void ToggleProgressBar(bool? bEnableProgressBar = false)
        {
            if (!bEnableProgressBar.HasValue)
            {
                ToggleProgressBar(!customIndeterminateProgressBar.IsIndeterminate);
                return;
            }

            customIndeterminateProgressBar.Dispatcher.BeginInvoke(new Action(delegate
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
                }
                else
                {
                    customIndeterminateProgressBar.Visibility = Visibility.Collapsed;
                    TransparentOverlay.Visibility = Visibility.Collapsed;
                }
            }));
        }

        private void TvListPanel_Tap(object sender, GestureEventArgs e)
        {
            MainPivot.SelectedIndex = 1;

            if (!bEnabled)
            {
                return;
            }

            discoverer.StopSearching(Discovery.SearchEndReason.Complete);

            ToggleProgressBar(true);
            TvItemViewModel selectedItem = (TvListBox.SelectedItem as TvItemViewModel);
            ConnectTo(new IPEndPoint(selectedItem.TvAddress, selectedItem.Port));
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
                ApplicationBar.IsVisible = true;
            }
            else
            {
                ApplicationBar.IsVisible = false;
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
                    Port = TvConnection.TvDirectPort,
                    TvAddress = IPAddress.Parse("192.168.100." + rand.Next(1, 255)),
                    TvName = "Example TV " + i
                });
            }
        }

        private void RefreshTvList_Click(object sender, EventArgs e)
        {
            App.ViewModel.TvItems.Clear();
            bEnabled = true;

            btnDemoMode.Visibility = Visibility.Collapsed;
            discoverer.FindTvs();
            ToggleProgressBar(true);
        }

        private void PhoneApplicationPage_BackKeyPress(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (discoverer.HandleBackButton())
            {
                e.Cancel = true;
            }
        }

        private void OnPowerButtonPressed(object sender, EventArgs args)
        {
            if (directConn != null && directConn.connectedEndpoint != null)
            {
                for (int i = App.ViewModel.TvItems.Count - 1; i >= 0; i--)
                {
                    if (App.ViewModel.TvItems[i].TvAddress.ToString() == directConn.connectedEndpoint.Address.ToString())
                    {
                        App.ViewModel.TvItems.RemoveAt(i);
                    }
                }
            }

            MainPivot.SelectedIndex = 0;
        }
        #endregion

        #region direct connection handling/callbacks
        private void ConnectTo(IPEndPoint selectedEndpoint)
        {
            directConn = new TvConnection(selectedEndpoint);
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
        }

        void directConn_Connected()
        {
            SetProgressText("Connected to TV. Sending registration...");
        }

        void directConn_RegistrationWaiting()
        {
            SetProgressText("Waiting for user authorization...");
        }

        void directConn_RegistrationTimedOut()
        {
            SetProgressText("Remote connection timed out.");
        }

        void directConn_RegistrationDenied()
        {
            SetProgressText("Remote connection denied.");
        }

        void directConn_RegistrationAccepted()
        {
            SetProgressText("Remote approved!");
            ToggleProgressBar(false);
        }

        void directConn_RegistrationFailed()
        {
            SetProgressText("Sending remote registration failed.");
            ToggleProgressBar(false);
        }

        void directConn_Registering()
        {
            SetProgressText("Registering remote with TV...");
        }

        void directConn_Connecting()
        {
            SetProgressText("Connecting to TV at " + directConn.connectedEndpoint.Address.ToString() + "...");
        }
        #endregion
    }
}