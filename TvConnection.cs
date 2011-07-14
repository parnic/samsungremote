using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

namespace SamsungRemoteWP7
{
    public class TvConnection
    {
        protected enum TvConnectionState
        {
            NotConnected,
            Connecting,
            AwaitingAuthorization,
            Connected,
            Disconnected,
        }

        public const int TvDirectPort = 55000;

        protected Socket TvDirectSock = null;

        protected readonly char[] ALLOWED_BYTES = new char[] { (char)0x64, (char)0x00, (char)0x01, (char)0x00 };
        protected readonly char[] DENIED_BYTES = new char[] { (char)0x64, (char)0x00, (char)0x00, (char)0x00 };
        protected readonly char[] TIMEOUT_BYTES = new char[] { (char)0x65, (char)0x00 };
        // this seems to sometimes end with 0200 and sometimes 0100...who knows if there are other endings too, but i have no idea if each message means something different or not
        protected readonly char[] AWAITING_APPROVAL_PREFIX = new char[] { (char)0xa, (char)0x00 };//, (char)0x02, (char)0x00, (char)0x00, (char)0x00 };

        protected readonly int AWAITING_APPROVAL_TOTAL = 6;

        protected const string appName = "wp7.app.perniciousgames";

        protected TvConnectionState connectionState;
        public IPEndPoint connectedEndpoint { get; protected set; }

        public delegate void ConnectingDelegate();
        public event ConnectingDelegate Connecting;

        public delegate void ConnectedDelegate();
        public event ConnectedDelegate Connected;

        public delegate void DisconnectedDelegate();
        public event DisconnectedDelegate Disconnected;

        public delegate void RegisteringDelegate();
        public event RegisteringDelegate Registering;

        public delegate void RegistrationFailedDelegate();
        public event RegistrationFailedDelegate RegistrationFailed;

        public delegate void RegistrationDeniedDelegate();
        public event RegistrationDeniedDelegate RegistrationDenied;

        public delegate void RegistrationAcceptedDelegate();
        public event RegistrationAcceptedDelegate RegistrationAccepted;

        public delegate void RegistrationTimedOutDelegate();
        public event RegistrationTimedOutDelegate RegistrationTimedOut;

        public delegate void RegistrationWaitingDelegate();
        public event RegistrationWaitingDelegate RegistrationWaiting;

        private bool bReconnectAfterUnlock;

        public bool bSentPowerOff;

        public TvConnection(IPEndPoint endpoint)
        {
            connectedEndpoint = endpoint;
        }

        public void Connect()
        {
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.RemoteEndPoint = connectedEndpoint;
            socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(RegistrationComplete);

            TvDirectSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            TvDirectSock.ConnectAsync(socketEventArg);

            connectionState = TvConnectionState.Connecting;

            if (Connecting != null)
            {
                Connecting();
            }
        }

        public void Cleanup()
        {
            try
            {
                TvDirectSock.Close();
            }
            catch { }
        }

        public void SendKey(TvKeyControl.EKey key)
        {
// 		    if (logger != null) logger.v(TAG, "Sending key " + key.getValue() + "...");
// 		    checkConnection();
            try
            {
                InternalSendKey(key);
            }
            catch (SocketException)
            {
// 			    if (logger != null) logger.v(TAG, "Could not send key because the server closed the connection. Reconnecting...");
// 			    initialize();
// 			    if (logger != null) logger.v(TAG, "Sending key " + key.getValue() + " again...");
// 			    internalSendKey(key);
            }
//		    if (logger != null) logger.v(TAG, "Successfully sent key " + key.getValue());
        }

        public void NotifyAppDeactivated()
        {
            if (TvDirectSock != null && TvDirectSock.Connected)
            {
                bReconnectAfterUnlock = true;
            }
        }

        public void NotifyAppActivated()
        {
            if (bReconnectAfterUnlock)
            {
                bReconnectAfterUnlock = false;

                if (!bSentPowerOff)
                {
                    Connect();
                }
            }
        }

        private void RegistrationComplete(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.SendTo:
                    if (connectionState == TvConnectionState.AwaitingAuthorization)
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            if (Registering != null)
                            {
                                Registering();
                            }

                            e.SetBuffer(new byte[0x1000], 0, 0x1000);
                            TvDirectSock.ReceiveFromAsync(e);
                        }
                        else
                        {
                            if (RegistrationFailed != null)
                            {
                                RegistrationFailed();
                            }
                            connectionState = TvConnectionState.Disconnected;
                        }
                    }
                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    if (connectionState == TvConnectionState.AwaitingAuthorization)
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
                                bDisconnect = false;
                                if (RegistrationAccepted != null)
                                {
                                    RegistrationAccepted();
                                }
                            }
                            else if (AreArraysEqual(regResponse, DENIED_BYTES))
                            {
                                if (RegistrationDenied != null)
                                {
                                    RegistrationDenied();
                                }
                            }
                            else if (AreArraysEqual(regResponse, TIMEOUT_BYTES))
                            {
                                if (RegistrationTimedOut != null)
                                {
                                    RegistrationTimedOut();
                                }
                            }
                            else if (ArrayStartsWith(AWAITING_APPROVAL_PREFIX, regResponse, AWAITING_APPROVAL_TOTAL))
                            {
                                if (RegistrationWaiting != null)
                                {
                                    RegistrationWaiting();
                                }
                                bDisconnect = false;
                            }
                            else
                            {
                                if (RegistrationDenied != null)
                                {
                                    RegistrationDenied();
                                }
                            }

                            if (bDisconnect)
                            {
                                TvDirectSock.Close();
                                if (Disconnected != null)
                                {
                                    Disconnected();
                                }
                                NotifyDisconnected();
                            }
                            else
                            {
                                TvDirectSock.ReceiveFromAsync(e);
                                connectionState = TvConnectionState.Connected;
                            }
                        }
                        else
                        {
                            NotifyDisconnected();
                        }
                    }
                    else if (connectionState == TvConnectionState.Connected)
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            if (e.BytesTransferred > 0)
                            {
                                for (int i = e.Offset; i < e.BytesTransferred; i++)
                                {
                                    System.Diagnostics.Debug.WriteLine("0x{0:x}", e.Buffer[i]);
                                }

                                string responseStr = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);

                                StreamReader sr = new System.IO.StreamReader(new MemoryStream(e.Buffer, e.Offset, e.BytesTransferred));
                                sr.Read();
                                string inApp = ReadString(sr);
                                char[] keyResponse = ReadCharArray(sr);

                                System.Diagnostics.Debug.WriteLine("tv returned: " + inApp);
                            }
                            try
                            {
                                TvDirectSock.ReceiveFromAsync(e);
                            }
                            catch (System.Exception)
                            {
                                NotifyDisconnected();
                            }
                        }
                        else
                        {
                            NotifyDisconnected();
                        }
                    }
                    break;

                case SocketAsyncOperation.Connect:
                    if (connectionState == TvConnectionState.Connecting)
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            SendRegistrationTo(e);

                            connectionState = TvConnectionState.AwaitingAuthorization;
                            if (Connected != null)
                            {
                                Connected();
                            }
                        }
                        else
                        {
                            NotifyDisconnected();
                        }
                    }
                    break;
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

        private void InternalSendKey(TvKeyControl.EKey key)
        {
            if (TvDirectSock == null || !TvDirectSock.Connected)
            {
                return;
            }

            StringBuilder writer = new StringBuilder();
            writer.Append((char)0x00);
            WriteText(writer, appName);
            WriteText(writer, GetKeyPayload(key));

            byte[] TvRegistrationMessage = Encoding.UTF8.GetBytes(writer.ToString());
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.RemoteEndPoint = TvDirectSock.RemoteEndPoint;
            e.SetBuffer(TvRegistrationMessage, 0, TvRegistrationMessage.Length);
            TvDirectSock.SendToAsync(e);
        }

        private String GetKeyPayload(TvKeyControl.EKey key)
        {
            StringBuilder writer = new StringBuilder();
            writer.Append((char)0x00);
            writer.Append((char)0x00);
            writer.Append((char)0x00);
            WriteBase64Text(writer, key.ToString());
            return writer.ToString();
        }

        /*
        private void internalSendText(String text)
        {
		    writer.append((char)0x01);
		    writeText(writer, TV_APP_STRING);
		    writeText(writer, getTextPayload(text));
		    writer.flush();
		    if (!reader.ready()) {
			    return;
		    }
		    int i = reader.read(); // Unknown byte 0x02
		    System.out.println(i);
		    String t = readText(reader); // Read "iapp.samsung"
		    char[] c = readCharArray(reader);
		    System.out.println(i);
		    System.out.println(t);
		    for (char a : c) System.out.println(Integer.toHexString(a));
	    }
	
	    public void sendText(String text)
        {
		    if (logger != null) logger.v(TAG, "Sending text \"" + text + "\"...");
		    checkConnection();
		    try {
			    internalSendText(text);
		    } catch (SocketException e) {
			    if (logger != null) logger.v(TAG, "Could not send key because the server closed the connection. Reconnecting...");
			    initialize();
			    if (logger != null) logger.v(TAG, "Sending text \"" + text + "\" again...");
			    internalSendText(text);
		    }
		    if (logger != null) logger.v(TAG, "Successfully sent text \"" + text + "\"");
	    }

	    private String getTextPayload(String text)
        {
		    StringWriter writer = new StringWriter();
		    writer.append((char)0x01);
		    writer.append((char)0x00);
		    writeBase64Text(writer, text);
		    writer.flush();
		    return writer.toString();
	    }
        */

        protected void NotifyDisconnected()
        {
            connectionState = TvConnectionState.Disconnected;
            if (Disconnected != null)
            {
                Disconnected();
            }
        }

        protected bool AreArraysEqual(char[] arr1, char[] arr2)
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

        protected bool ArrayStartsWith(char[] prefixArray, char[] fullArray, int fullArrayLength)
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
    }
}
