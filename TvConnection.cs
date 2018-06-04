using System;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage.Streams;

namespace UnofficialSamsungRemote
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
        private const int MaxBytesToRead = 16384;

        protected Windows.Networking.Sockets.StreamSocket TvDirectSocket = null;

        protected readonly byte[] ALLOWED_BYTES = new byte[] { 0x64, 0x00, 0x01, 0x00 };
        protected readonly byte[] DENIED_BYTES = new byte[] { 0x64, 0x00, 0x00, 0x00 };
        protected readonly byte[] TIMEOUT_BYTES = new byte[] { 0x65, 0x00 };
        // this seems to sometimes end with 0200 and sometimes 0100...who knows if there are other endings too, but i have no idea if each message means something different or not
        protected readonly byte[] AWAITING_APPROVAL_PREFIX = new byte[] { 0xa, 0x00 };//, 0x02, 0x00, 0x00, 0x00 };

        protected readonly int AWAITING_APPROVAL_TOTAL = 6;

        protected const string appName = "wp7.app.perniciousgames";

        protected TvConnectionState ConnectionState;
        public Windows.Networking.HostName ConnectedHostName { get; protected set; }
        public UInt16 ConnectedPort { get; protected set; }

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

        private DataWriter Writer;
        private DataReader Reader;

        public TvConnection(Windows.Networking.HostName host, UInt16 port)
        {
            ConnectedHostName = host;
            ConnectedPort = port;
        }

        public bool IsConnected
        {
            get
            {
                return TvDirectSocket != null && !bSentPowerOff && ConnectedHostName != null;
            }
        }

        private void newConnected(IAsyncAction info, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {
                ConnectionState = TvConnectionState.AwaitingAuthorization;
                Reader = new DataReader(TvDirectSocket.InputStream);
                Writer = new DataWriter(TvDirectSocket.OutputStream);

                Connected?.Invoke();

                var reg = GetRegistration();
                SendBytes(reg, newRegistrationSent);
            }
            else
            {
                NotifyDisconnected();
            }
        }

        private void SendBytes(byte[] reg, AsyncOperationCompletedHandler<uint> completed)
        {
            Writer.WriteBytes(reg);
            var storeResult = Writer.StoreAsync();
            if (completed != null)
            {
                storeResult.Completed += completed;
            }
        }

        private async void newRegistrationSent(IAsyncOperation<uint> info, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {
                Registering?.Invoke();
                await ListenForRegistrationResponse();
            }
            else
            {
                RegistrationFailed?.Invoke();
                ConnectionState = TvConnectionState.Disconnected;
            }
        }

        private async Task ListenForRegistrationResponse()
        {
            Reader.InputStreamOptions = InputStreamOptions.Partial;
            await Reader.LoadAsync(MaxBytesToRead);
            ReadResponseHeader(Reader);
            var regResponse = GetBytes(Reader);
            if (regResponse != null)
            {
                HandleRegistrationResponse(regResponse);
            }
        }

        private async void HandleRegistrationResponse(byte[] regResponse)
        {
            bool bDisconnect = true;
            bool bUpdateConnectionState = true;

            if (AreArraysEqual(regResponse, ALLOWED_BYTES))
            {
                System.Diagnostics.Debug.WriteLine("ALLOWED");
                bDisconnect = false;
                RegistrationAccepted?.Invoke();
            }
            else if (AreArraysEqual(regResponse, DENIED_BYTES))
            {
                System.Diagnostics.Debug.WriteLine("DENIED");
                RegistrationDenied?.Invoke();
            }
            else if (AreArraysEqual(regResponse, TIMEOUT_BYTES))
            {
                System.Diagnostics.Debug.WriteLine("TIMEOUT");
                RegistrationTimedOut?.Invoke();
            }
            else if (ArrayStartsWith(AWAITING_APPROVAL_PREFIX, regResponse, AWAITING_APPROVAL_TOTAL))
            {
                System.Diagnostics.Debug.WriteLine("AWAITING");
                RegistrationWaiting?.Invoke();
                bDisconnect = false;
                bUpdateConnectionState = false;
            }
            else
            {
                //RegistrationDenied?.Invoke();
                System.Diagnostics.Debug.WriteLine("UNKNOWN");
                bDisconnect = false;
                bUpdateConnectionState = false;
            }

            if (bDisconnect)
            {
                System.Diagnostics.Debug.WriteLine("[reg] disconnecting (bdisconnect)");
                Cleanup();
                NotifyDisconnected();
            }
            else
            {
                if (bUpdateConnectionState)
                {
                    ConnectionState = TvConnectionState.Connected;
                }
                else
                {
                    await ListenForRegistrationResponse();
                }
            }
        }

        private void ReadResponseHeader(DataReader reader)
        {
            if (reader.UnconsumedBufferLength > 0)
            {
                reader.ReadByte();
                /*var appName = */GetString(reader);
            }
        }

        public void Connect()
        {
            TvDirectSocket = new Windows.Networking.Sockets.StreamSocket();
            TvDirectSocket.ConnectAsync(ConnectedHostName, ConnectedPort.ToString()).Completed += newConnected;
            ConnectionState = TvConnectionState.Connecting;

            Connecting?.Invoke();
        }

        private byte GetLengthHeader(DataReader reader)
        {
            byte length = 0;
            if (reader.UnconsumedBufferLength > 2)
            {
                length = reader.ReadByte();
                var delimiter = reader.ReadByte();
                if (delimiter != 0)
                {
                    throw new Exception("Unexpected input " + delimiter);
                }
            }
            return length;
        }

        private string GetString(DataReader reader)
        {
            return reader.ReadString(GetLengthHeader(reader));
        }

        private byte[] GetBytes(DataReader reader)
        {
            var len = GetLengthHeader(reader);
            byte[] ret = null;
            if (len > 0)
            {
                ret = new byte[len];
                reader.ReadBytes(ret);
            }
            return ret;
        }

        public void Cleanup()
        {
            try
            {
                Writer.DetachStream();
                Reader.DetachStream();
                Writer.Dispose();
                Reader.Dispose();

                TvDirectSocket.Dispose();
                TvDirectSocket = null;
            }
            catch { }
        }

        public void SendKey(EKey key)
        {
// 		    if (logger != null) logger.v(TAG, "Sending key " + key.getValue() + "...");
// 		    checkConnection();
            try
            {
                InternalSendKey(key);
            }
            catch
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
            if (TvDirectSocket != null)
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

        private byte[] GetRegistration()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((char)0x0);
            WriteText(sb, appName);
            WriteText(sb, GetRegistrationPayload("0.0.0.0"));

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private String GetRegistrationPayload(String ip)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((char)0x64);
            sb.Append((char)0x00);
            WriteBase64Text(sb, ip);

            WriteBase64Text(sb, "Windows device");
            var name = new EasClientDeviceInformation().FriendlyName;
            if (!string.IsNullOrEmpty(name))
            {
                WriteBase64Text(sb, name);
            }
            return sb.ToString();
        }

        private StringBuilder WriteText(StringBuilder writer, String text)
        {
            var bytes = BitConverter.GetBytes((UInt16)text.Length);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return writer.Append(Encoding.ASCII.GetChars(bytes)).Append(text);
        }

        private StringBuilder WriteBase64Text(StringBuilder writer, String text)
        {
            string s = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            return WriteText(writer, s);
        }

        private void InternalSendKey(EKey key)
        {
            if (TvDirectSocket == null)
            {
                return;
            }

            StringBuilder writer = new StringBuilder();
            writer.Append((char)0x00);
            WriteText(writer, appName);
            WriteText(writer, GetKeyPayload(key));

            SendBytes(Encoding.UTF8.GetBytes(writer.ToString()), SendKeyResponse);
        }

        private async void SendKeyResponse(IAsyncOperation<UInt32> info, AsyncStatus status)
        {
            try
            {
                if (status == AsyncStatus.Completed)
                {
                    Reader.InputStreamOptions = InputStreamOptions.Partial;
                    await Reader.LoadAsync(MaxBytesToRead);
                    ReadResponseHeader(Reader);
                    /*var regResponse = */
                    GetBytes(Reader);
                }
            }
            catch
            {
                status = AsyncStatus.Error;
            }

            if (status != AsyncStatus.Completed)
            {
                Cleanup();
                NotifyDisconnected();
            }
        }

        private String GetKeyPayload(EKey key)
        {
            StringBuilder writer = new StringBuilder();
            writer.Append((char)0x00);
            writer.Append((char)0x00);
            writer.Append((char)0x00);
            WriteBase64Text(writer, key.ToString());
            return writer.ToString();
        }

        private void InternalSendText(String text)
        {
            if (TvDirectSocket == null)
            {
                return;
            }

            StringBuilder writer = new StringBuilder();
		    writer.Append((char)0x01);
		    WriteText(writer, appName);
		    WriteText(writer, GetTextPayload(text));

            SendBytes(Encoding.UTF8.GetBytes(writer.ToString()), null);
        }

        public void SendText(String text)
        {
// 		    if (logger != null) logger.v(TAG, "Sending text \"" + text + "\"...");
// 		        checkConnection();
            try
            {
                InternalSendText(text);
            }
            catch
            {
// 			    if (logger != null) logger.v(TAG, "Could not send key because the server closed the connection. Reconnecting...");
// 			    initialize();
// 			    if (logger != null) logger.v(TAG, "Sending text \"" + text + "\" again...");
// 			    internalSendText(text);
            }
		    //if (logger != null) logger.v(TAG, "Successfully sent text \"" + text + "\"");
	    }

	    private String GetTextPayload(String text)
        {
            StringBuilder writer = new StringBuilder();
		    writer.Append((char)0x01);
		    writer.Append((char)0x00);
		    WriteBase64Text(writer, text);
		    return writer.ToString();
	    }

        protected void NotifyDisconnected()
        {
            ConnectionState = TvConnectionState.Disconnected;
            Disconnected?.Invoke();
        }

        protected bool AreArraysEqual(byte[] arr1, byte[] arr2)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(arr1, arr2);
        }

        protected bool ArrayStartsWith(byte[] prefixArray, byte[] fullArray, int fullArrayLength)
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
