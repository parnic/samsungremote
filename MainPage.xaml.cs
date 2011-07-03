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
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.Linq;

namespace SamsungRemoteWP7
{
    public partial class MainPage : PhoneApplicationPage
    {
        private static String SearchTemplate =
            "M-SEARCH * HTTP/1.1\r\n" +
            "HOST: 239.255.255.250:1900\r\n" +
            "MAN: \"ssdp:discover\"\r\n" +
            "USER-AGENT: Windows/6.5 UPnP/1.1 Parnic's Remote\r\n" +
            "ST: {0}\r\n" +
            "MX: {1}\r\n" +
            "\r\n";

        static int MulticastPort = 1900;
        static int TvDirectPort = 55000;

        IPEndPoint multicastEndpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), MulticastPort);
        IPEndPoint listenEndpoint = new IPEndPoint(IPAddress.Any, MulticastPort);

        Socket TvSearchSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        Socket TvDirectSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        byte[] TvSearchMessage = Encoding.UTF8.GetBytes(String.Format(SearchTemplate, "urn:samsung.com:device:RemoteControlReceiver:1", 1));

        private static char[] ALLOWED_BYTES = new char[] { (char)0x64, (char)0x00, (char)0x01, (char)0x00 };
        private static char[] DENIED_BYTES = new char[] { (char)0x64, (char)0x00, (char)0x00, (char)0x00 };
        private static char[] TIMEOUT_BYTES = new char[] { (char)0x65, (char)0x00 };
        // this seems to sometimes end with 0200 and sometimes 0100...who knows if there are other endings too, but i have no idea if each message means something different or not
        private static char[] AWAITING_APPROVAL_PREFIX = new char[] { (char)0xa, (char)0x00 };//, (char)0x02, (char)0x00, (char)0x00, (char)0x00 };

        private static int AWAITING_APPROVAL_TOTAL = 6;

        private string appName = "wp7.app.perniciousgames";

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Set the data context of the listbox control to the sample data
            DataContext = App.ViewModel;
            this.Loaded += new RoutedEventHandler(MainPage_Loaded);
        }

        // Load data for the ViewModel Items
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }
        }

        private void TvListen()
        {
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += new EventHandler<SocketAsyncEventArgs>(TvListenCompleted);
            e.SetBuffer(TvSearchMessage, 0, TvSearchMessage.Length);
            e.RemoteEndPoint = multicastEndpoint;
            TvSearchSock.SendToAsync(e);

            ToggleProgressBar(true);
        }

        void TvListenCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                if (e.LastOperation == SocketAsyncOperation.ReceiveFrom)
                {
                    string response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                    System.Diagnostics.Debug.WriteLine("Received from {0}: {1}", e.RemoteEndPoint.ToString(), response);

                    if (response.Contains("RemoteControlReceiver.xml"))
                    {
                        ToggleProgressBar(false);
                        SetProgressText("Found TV.");
                        AddTvUnique(e.RemoteEndPoint);

                        GetTvNameFrom(e.RemoteEndPoint, response);
                    }
                    else
                    {
                        TvSearchSock.ReceiveFromAsync(e);
                    }
                }
                else if (e.LastOperation == SocketAsyncOperation.SendTo)
                {
                    e.SetBuffer(new byte[0x1000], 0, 0x1000);
                    e.RemoteEndPoint = listenEndpoint;
                    SetProgressText("Searching for TV...");
                    if (!TvSearchSock.ReceiveFromAsync(e))
                    {
                        SetProgressText("TV search failed.");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("op: {0}, error: {1}", e.LastOperation, e.SocketError);
                SetProgressText("Network error when searching for a TV.");
            }
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
                    Port = TvDirectPort,
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

        private void ConnectTo(IPEndPoint endpoint)
        {
            SetProgressText("Connecting to TV at " + endpoint.Address.ToString() + "...");

            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.RemoteEndPoint = endpoint;
            socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(RegistrationComplete);
            socketEventArg.UserToken = 0;
            TvDirectSock.ConnectAsync(socketEventArg);
        }

        void RegistrationComplete(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.SendTo)
            {
                if ((int)e.UserToken == 0)
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        // registration complete
                        SetProgressText("Registering remote with TV...");

                        e.SetBuffer(new byte[0x1000], 0, 0x1000);
                        TvDirectSock.ReceiveFromAsync(e);
                    }
                    else
                    {
                        SetProgressText("Sending remote registration failed.");
                        ToggleProgressBar(false);
                    }
                }
            }
            else if (e.LastOperation == SocketAsyncOperation.ReceiveFrom)
            {
                if ((int)e.UserToken == 0)
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        for (int i = e.Offset; i < e.BytesTransferred; i++)
                        {
                            System.Diagnostics.Debug.WriteLine("0x{0:x}", e.Buffer[i]);
                        }

                        string responseStr = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);

                        StreamReader sr = new System.IO.StreamReader(new MemoryStream(e.Buffer, e.Offset, e.BytesTransferred));
                        sr.Read();
                        string regApp = ReadString(sr);
                        char[] regResponse = ReadCharArray(sr);

                        System.Diagnostics.Debug.WriteLine("tv returned: " + regApp);

                        bool bDisconnect = true;

                        if (AreArraysEqual(regResponse, ALLOWED_BYTES))
                        {
                            SetProgressText("Remote approved!");
                            bDisconnect = false;
                            ToggleProgressBar(false);
                        }
                        else if (AreArraysEqual(regResponse, DENIED_BYTES))
                        {
                            SetProgressText("Remote connection denied.");
                        }
                        else if (AreArraysEqual(regResponse, TIMEOUT_BYTES))
                        {
                            SetProgressText("Remote connection timed out.");
                        }
                        else if (ArrayStartsWith(AWAITING_APPROVAL_PREFIX, regResponse, AWAITING_APPROVAL_TOTAL))
                        {
                            SetProgressText("Waiting for user authorization...");
                            bDisconnect = false;
                        }
                        else
                        {
                            SetProgressText("Unknown response from TV.");
                        }

                        if (bDisconnect)
                        {
                            e.UserToken = 1;
                            TvDirectSock.Close();
                            ToggleProgressBar(false);
                        }
                        else
                        {
                            TvDirectSock.ReceiveFromAsync(e);
                        }
                    }
                    else
                    {
                        SetProgressText("Failure communicating with the TV.");
                        ToggleProgressBar(false);
                        TvDirectSock.Close();
                    }
                }
            }
            else if (e.LastOperation == SocketAsyncOperation.Connect)
            {
                if ((int)e.UserToken == 0)
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        SendRegistrationTo(e);

                        SetProgressText("Connected to TV. Sending registration...");
                    }
                    else
                    {
                        SetProgressText("Failure connecting to TV at " + (e.RemoteEndPoint as IPEndPoint).Address.ToString() + ".");
                        ToggleProgressBar(false);
                    }
                }
            }
        }

        private void SendRegistrationTo(SocketAsyncEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((char)0x0);
            WriteText(sb, appName);
            WriteText(sb, GetRegistrationPayload("0.0.0.0"));

            byte[] TvRegistrationMessage = Encoding.UTF8.GetBytes(sb.ToString());
            e.SetBuffer(TvRegistrationMessage, 0, TvRegistrationMessage.Length);
            TvDirectSock.SendToAsync(e);
        }

        private string ReadString(StreamReader sr)
        {
            char[] buffer = ReadCharArray(sr);
            return new string(buffer);
        }

        private char[] ReadCharArray(StreamReader sr)
        {
            int length = sr.Read();
            int delimiter = sr.Read();
            if (delimiter != 0)
            {
                throw new Exception("Unexpected input " + delimiter);
            }
            char[] buffer = new char[length];
            sr.Read(buffer, 0, length);
            return buffer;
        }

        private String GetRegistrationPayload(String ip)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((char)0x64);
            sb.Append((char)0x00);
            WriteBase64Text(sb, ip);
            string phoneId = PhoneInfoExtendedProperties.GetWindowsLiveAnonymousID();
            if (string.IsNullOrWhiteSpace(phoneId))
            {
                phoneId = "Anonymous WP7 phone";
            }
            WriteBase64Text(sb, phoneId);
            WriteBase64Text(sb, Microsoft.Phone.Info.DeviceStatus.DeviceName);
            return sb.ToString();
        }

        private StringBuilder WriteText(StringBuilder writer, String text)
        {
            return writer.Append((char)text.Length).Append((char)0x0).Append(text);
        }

        private StringBuilder WriteBase64Text(StringBuilder writer, String text)
        {
            string s = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            return WriteText(writer, s);
        }

        private bool AreArraysEqual(char[] arr1, char[] arr2)
        {
            if (arr1.Length != arr2.Length)
            {
                return false;
            }

            for (int i = 0; i < arr1.Length; i++)
            {
                if (arr1[i] != arr2[i])
                {
                    return false;
                }
            }

            return true;
        }

        private bool ArrayStartsWith(char[] prefixArray, char[] fullArray, int fullArrayLength)
        {
            if (fullArray.Length != fullArrayLength)
            {
                return false;
            }

            for (int i = 0; i < prefixArray.Length; i++)
            {
                if (fullArray[i] != prefixArray[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void PhoneApplicationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                TvSearchSock.Close();
            }
            catch { }
            try
            {
                TvDirectSock.Close();
            }
            catch { }
        }

        private void ToggleProgressBar(bool? bEnable = false)
        {
            if (!bEnable.HasValue)
            {
                ToggleProgressBar(!customIndeterminateProgressBar.IsIndeterminate);
                return;
            }

            customIndeterminateProgressBar.Dispatcher.BeginInvoke(new Action(delegate
            {
                customIndeterminateProgressBar.IsIndeterminate = bEnable.Value;

                if (bEnable.Value)
                {
                    customIndeterminateProgressBar.Visibility = Visibility.Visible;
                    TransparentOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    customIndeterminateProgressBar.Visibility = Visibility.Collapsed;
                    TransparentOverlay.Visibility = Visibility.Collapsed;
                }
            }));
        }

        private void InternalSendKey(Key.EKey key) {
            StringBuilder writer = new StringBuilder();
		    writer.Append((char)0x00);
		    WriteText(writer, appName);
		    WriteText(writer, GetKeyPayload(key));

            byte[] TvRegistrationMessage = Encoding.UTF8.GetBytes(writer.ToString());
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.RemoteEndPoint = TvDirectSock.RemoteEndPoint;
            e.SetBuffer(TvRegistrationMessage, 0, TvRegistrationMessage.Length);
            TvDirectSock.SendToAsync(e);

// 		    int i = reader.read(); // Unknown byte 0x00
// 		    String t = readText(reader);  // Read "iapp.samsung"
// 		    char[] c = readCharArray(reader);
// 		    System.out.println(i);
// 		    System.out.println(t);
// 		    for (char a : c) System.out.println(Integer.toHexString(a));
		    //System.out.println(c);
	    }

        public void SendKey(Key.EKey key)
        {
// 		    if (logger != null) logger.v(TAG, "Sending key " + key.getValue() + "...");
// 		    checkConnection();
		    try {
			    InternalSendKey(key);
		    } catch (SocketException) {
// 			    if (logger != null) logger.v(TAG, "Could not send key because the server closed the connection. Reconnecting...");
// 			    initialize();
// 			    if (logger != null) logger.v(TAG, "Sending key " + key.getValue() + " again...");
// 			    internalSendKey(key);
		    }
//		    if (logger != null) logger.v(TAG, "Successfully sent key " + key.getValue());
	    }

	    private String GetKeyPayload(Key.EKey key) {
		    StringBuilder writer = new StringBuilder();
		    writer.Append((char)0x00);
		    writer.Append((char)0x00);
		    writer.Append((char)0x00);
		    WriteBase64Text(writer, key.ToString());
		    return writer.ToString();
	    }

        private void StackPanel_Tap(object sender, GestureEventArgs e)
        {
            SendKey(Key.EKey.KEY_POWEROFF);
        }

        private void TvListPanel_Tap(object sender, GestureEventArgs e)
        {
            if (TvSearchSock != null && TvSearchSock.Connected)
            {
                TvSearchSock.Close();
            }

            ToggleProgressBar(true);
            TvItemViewModel selectedItem = (TvListBox.SelectedItem as TvItemViewModel);
            ConnectTo(new IPEndPoint(selectedItem.TvAddress, selectedItem.Port));
            MainPivot.SelectedIndex = 1;
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
/*                if (System.Diagnostics.Debugger.IsAttached)
                {
                    if (App.ViewModel.TvItems.Count == 0)
                    {
                        App.ViewModel.TvItems.Add(new TvItemViewModel() { Port = TvDirectPort, TvAddress = IPAddress.Parse("10.0.0.38"), TvName = "good times" });
                    }
                }
                else
*/              {
                    TvListen();
                }
            }
        }
    }
}