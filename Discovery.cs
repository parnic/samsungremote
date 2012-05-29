using System;
using System.Net;
using System.Threading;
using System.Text;
using System.Net.Sockets;

namespace SamsungRemoteWP7
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

        private static IPAddress multicastAddress = IPAddress.Parse("239.255.255.250");

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

        protected readonly int multicastPort = 1900;
        protected IPEndPoint multicastEndpoint;
        protected IPEndPoint listenEndpoint;

        protected Socket tvSearchSock;

        protected SearchState searchState;

        public delegate void StartedSearchingDelegate();
        public event StartedSearchingDelegate StartedSearching;

        public delegate void TvFoundDelegate(EndPoint TvEndpoint, string TvResponse);
        public event TvFoundDelegate TvFound;

        public delegate void SearchingEndedDelegate(SearchEndReason reason);
        public event SearchingEndedDelegate SearchingEnded;

        public Discovery()
        {
            tvSearchMessage = Encoding.UTF8.GetBytes(
                String.Format(searchTemplate,
                "urn:samsung.com:device:RemoteControlReceiver:1",
                4));

            multicastEndpoint = new IPEndPoint(multicastAddress, multicastPort);
            listenEndpoint = new IPEndPoint(IPAddress.Any, multicastPort);
        }

        public void FindTvs()
        {
            if (!MainPage.bEnabled)
            {
                return;
            }

            if (searchState == SearchState.Searching)
            {
                StopSearching();
            }

            tvSearchSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            tvSearchSock.SendToAsync(GetSearchSocketEventArgs());

            tvSearchRetryTimer = new Timer(TvSearchRetry, null, TimeSpan.FromSeconds(tvSearchRetryTimeSeconds), TimeSpan.FromMilliseconds(-1));
            tvSearchTimeoutTimer = new Timer(TvSearchTimeout, null, TimeSpan.FromSeconds(tvSearchTotalTimeSeconds), TimeSpan.FromMilliseconds(-1));

            searchState = SearchState.Searching;
        }

        public void Cleanup()
        {
            try
            {
                tvSearchSock.Close();
            }
            catch { }
        }

        public void StopSearching(SearchEndReason reason = SearchEndReason.Aborted)
        {
            if (tvSearchSock != null)
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
                if (tvSearchSock != null)
                {
                    tvSearchSock.SendToAsync(GetSearchSocketEventArgs());
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

        private void TvListenCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                if (e.LastOperation == SocketAsyncOperation.ReceiveFrom)
                {
                    string response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                    System.Diagnostics.Debug.WriteLine("Received from {0}: {1}", e.RemoteEndPoint.ToString(), response);

                    if (response.Contains("RemoteControlReceiver.xml"))
                    {
                        if (TvFound != null)
                        {
                            TvFound(e.RemoteEndPoint, response);
                        }
                    }

                    bool bKeptListening = false;
                    try
                    {
                        bKeptListening = tvSearchSock.ReceiveFromAsync(e);
                    }
                    catch (Exception) { }
                    finally
                    {
                        if (!bKeptListening)
                        {
                            StopSearching(SearchEndReason.Error);
                        }
                    }
                }
                else if (e.LastOperation == SocketAsyncOperation.SendTo)
                {
                    e.SetBuffer(new byte[0x1000], 0, 0x1000);
                    e.RemoteEndPoint = listenEndpoint;

                    try
                    {
                        if (tvSearchSock.ReceiveFromAsync(e))
                        {
                            if (StartedSearching != null)
                            {
                                StartedSearching();
                            }
                        }
                        else
                        {
                            StopSearching(SearchEndReason.Error);
                        }
                    }
                    catch (Exception)
                    {
                        StopSearching(SearchEndReason.Error);
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("op: {0}, error: {1}", e.LastOperation, e.SocketError);
                if (e.SocketError != SocketError.OperationAborted)
                {
                    StopSearching(SearchEndReason.Error);
                    searchState = SearchState.NotSearching;
                }
            }
        }

        protected SocketAsyncEventArgs GetSearchSocketEventArgs()
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += new EventHandler<SocketAsyncEventArgs>(TvListenCompleted);
            e.SetBuffer(tvSearchMessage, 0, tvSearchMessage.Length);
            e.RemoteEndPoint = multicastEndpoint;
            return e;
        }
    }
}
