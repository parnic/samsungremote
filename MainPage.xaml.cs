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

namespace SamsungRemoteWP7
{
    public partial class MainPage : PhoneApplicationPage
    {
        private static String SearchTemplate =
            "M-SEARCH * HTTP/1.1\r\n" +
            "HOST: 239.255.255.250:1900\r\n" +
            "MAN: \"ssdp:discover\"\r\n" +
            "USER-AGENT: Windows/6.5 UPnP/1.1 Parnics Remote\r\n" +
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

            first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "asking..."; }));
            TvListen();
        }

        private void TvListen()
        {
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += new EventHandler<SocketAsyncEventArgs>(TvListenCompleted);
            e.SetBuffer(TvSearchMessage, 0, TvSearchMessage.Length);
            e.RemoteEndPoint = multicastEndpoint;
            TvSearchSock.SendToAsync(e);
        }

        void TvListenCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                if (e.LastOperation == SocketAsyncOperation.Receive || e.LastOperation == SocketAsyncOperation.ReceiveFrom)
                {
                    string response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                    System.Diagnostics.Debug.WriteLine("Received from {0}: {1}", e.RemoteEndPoint.ToString(), response);

                    if (response.Contains("RemoteControlReceiver.xml"))
                    {
                        first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "found tv"; }));
                        TvSearchSock.Close();
                        ConnectTo(new IPEndPoint((e.RemoteEndPoint as IPEndPoint).Address, TvDirectPort));
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
                    first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "listening..."; }));
                    if (!TvSearchSock.ReceiveFromAsync(e))
                    {
                        first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "listen fail"; }));
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("op: {0}, error: {1}", e.LastOperation, e.SocketError);
            }
        }

        private void ConnectTo(IPEndPoint endpoint)
        {
            first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "connecting..."; }));

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
                        first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "regsent"; }));

                        e.SetBuffer(new byte[0x1000], 0, 0x1000);
                        TvDirectSock.ReceiveFromAsync(e);
                    }
                    else
                    {
                        first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "regfail"; }));
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
                        first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "regdone"; }));
                        e.UserToken = 1;
                        TvDirectSock.Close();
                        //tvt.ReceiveFromAsync(e);
                    }
                    else
                    {
                        first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "received fail"; }));
                    }
                }
            }
            else if (e.LastOperation == SocketAsyncOperation.Connect)
            {
                if ((int)e.UserToken == 0)
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append((char)0x0);
                        WriteText(sb, "iphone.iapp.samsung");
                        WriteText(sb, GetRegistrationPayload("10.0.0.3"));

                        byte[] TvRegistrationMessage = Encoding.UTF8.GetBytes(sb.ToString());
                        e.SetBuffer(TvRegistrationMessage, 0, TvRegistrationMessage.Length);
                        TvDirectSock.SendToAsync(e);

                        first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "connected..."; }));
                    }
                    else
                    {
                        first.Dispatcher.BeginInvoke(new Action(delegate { first.Header = "connect fail"; }));
                    }
                }
            }
        }

        private String GetRegistrationPayload(String ip)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((char)0x64);
            sb.Append((char)0x00);
            WriteBase64Text(sb, ip);
            WriteBase64Text(sb, "00:50:C2:00:11:22");
            WriteBase64Text(sb, "WP7 Remote");
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
    }
}