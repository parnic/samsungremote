using System;
using System.Net;
using System.Threading;
using System.Text;
using System.Net.Sockets;
using Windows.Networking.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.IO;

namespace UnofficialSamsungRemote
{
    public class Discovery
    {
        protected enum SearchState
        {
            NotSearching,
            Searching,
            SearchingCompleted,
        }

        public enum SearchEndReason
        {
            TimedOut,
            Complete,
            Aborted,
            Error,
        }

        private readonly Windows.Networking.HostName multicastAddress = new Windows.Networking.HostName("239.255.255.250");//IPAddress.Parse("239.255.255.250");
        protected const string multicastPort = "1900";

        private String searchTemplate =
            "M-SEARCH * HTTP/1.1\r\n" +
            "HOST: 239.255.255.250:1900\r\n" +
            "MAN: \"ssdp:discover\"\r\n" +
            "USER-AGENT: Windows/6.5 UPnP/1.1 Parnic's Remote\r\n" +
            "ST: {0}\r\n" +
            "MX: {1}\r\n" +
            "CONTENT-LENGTH: 0\r\n" +
            "\r\n";
        private readonly byte[] tvSearchMessage;

        private readonly float tvSearchRetryTimeSeconds = 2;
        private readonly float tvSearchTotalTimeSeconds = 10;

        private Timer tvSearchRetryTimer;
        private Timer tvSearchTimeoutTimer;

        private DatagramSocket tvSearchSocket;

        protected SearchState searchState;

        public delegate void StartedSearchingDelegate();
        public event StartedSearchingDelegate StartedSearching;

        public delegate void TvFoundDelegate(Windows.Networking.HostName TvHost, UInt16 TvPort, string TvResponse);
        public event TvFoundDelegate TvFound;

        public delegate void SearchingEndedDelegate(SearchEndReason reason);
        public event SearchingEndedDelegate SearchingEnded;

        public Discovery()
        {
            tvSearchMessage = Encoding.UTF8.GetBytes(
                String.Format(searchTemplate,
                "urn:samsung.com:device:RemoteControlReceiver:1",
                4));
        }

        public async void FindTvs()
        {
            if (!MainPage.bEnabled)
            {
                return;
            }

            if (searchState == SearchState.Searching)
            {
                StopSearching();
            }

            tvSearchSocket = new DatagramSocket();
            tvSearchSocket.MessageReceived += TvListenCompleted;
            await tvSearchSocket.BindEndpointAsync(null, "");
            tvSearchSocket.JoinMulticastGroup(multicastAddress);
            StartedSearching();
            SendSSDP();

            tvSearchRetryTimer = new Timer(TvSearchRetry, null, TimeSpan.FromSeconds(tvSearchRetryTimeSeconds), TimeSpan.FromMilliseconds(-1));
            tvSearchTimeoutTimer = new Timer(TvSearchTimeout, null, TimeSpan.FromSeconds(tvSearchTotalTimeSeconds), TimeSpan.FromMilliseconds(-1));

            searchState = SearchState.Searching;
        }

        private async void SendSSDP()
        {
            if (tvSearchSocket == null)
            {
                return;
            }

            try
            {
                using (var stream = await tvSearchSocket.GetOutputStreamAsync(multicastAddress, multicastPort.ToString()))
                {
                    await stream.WriteAsync(tvSearchMessage.AsBuffer());
                }
            }
            catch { }
        }

        public void Cleanup()
        {
            try
            {
                tvSearchSocket.Dispose();
            }
            catch { }
        }

        public void StopSearching(SearchEndReason reason = SearchEndReason.Aborted)
        {
            if (tvSearchSocket != null)
            {
                Cleanup();
            }

            if (tvSearchRetryTimer != null)
            {
                tvSearchRetryTimer.Dispose();
                tvSearchRetryTimer = null;
            }
            if (tvSearchTimeoutTimer != null)
            {
                tvSearchTimeoutTimer.Dispose();
            }

            if (SearchingEnded != null)
            {
                SearchingEnded(reason);
            }

            searchState = SearchState.SearchingCompleted;
        }

        public bool HandleBackButton()
        {
            if (searchState == SearchState.Searching)
            {
                StopSearching(SearchEndReason.Aborted);
                return true;
            }

            return false;
        }

        private void TvSearchRetry(object State)
        {
            try
            {
                if (tvSearchSocket != null)
                {
                    SendSSDP();
                    System.Diagnostics.Debug.WriteLine("pinging for tv's again...");
                }
                if (tvSearchRetryTimer != null)
                {
                    tvSearchRetryTimer = new Timer(TvSearchRetry, null, TimeSpan.FromSeconds(tvSearchRetryTimeSeconds), TimeSpan.FromMilliseconds(-1));
                }
            }
            catch (Exception) { }
        }

        private void TvSearchTimeout(object State)
        {
            StopSearching(SearchEndReason.TimedOut);
        }

        private void TvListenCompleted(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs e)
        {
            var response = new StreamReader(e.GetDataStream().AsStreamForRead()).ReadToEnd();
            System.Diagnostics.Debug.WriteLine("Received from {0}: {1}", e.RemoteAddress.ToString(), response);

            if (response.Contains("urn:samsung.com:device:RemoteControlReceiver")
                || response.Contains("RemoteControlReceiver.xml")
                || response.Contains("PersonalMessageReceiver.xml")
                || response.Contains("SamsungMRDesc.xml"))
            {
                if (TvFound != null)
                {
                    TvFound(e.RemoteAddress, UInt16.Parse(e.RemotePort), response);
                }
            }
        }
    }
}
