using System;
using System.Net;
using System.ComponentModel;
using System.Collections.Generic;
using Windows.Networking;

namespace UnofficialSamsungRemote
{
    public class TvItemViewModel : INotifyPropertyChanged, IEquatable<TvItemViewModel>, IEquatable<HostName>
    {
        private string tvAddress;
        /// <summary>
        /// Sample ViewModel property; this property is used in the view to display its value using a Binding.
        /// </summary>
        /// <returns></returns>
        public string TvAddress
        {
            get
            {
                return tvAddress;
            }
            set
            {
                if (value != tvAddress)
                {
                    tvAddress = value;
                    NotifyPropertyChanged(nameof(TvAddress));
                }
            }
        }

        private string port;
        /// <summary>
        /// Sample ViewModel property; this property is used in the view to display its value using a Binding.
        /// </summary>
        /// <returns></returns>
        public string Port
        {
            get
            {
                return port;
            }
            set
            {
                if (value != port)
                {
                    port = value;
                    NotifyPropertyChanged(nameof(Port));
                }
            }
        }

        private string tvName;
        /// <summary>
        /// Sample ViewModel property; this property is used in the view to display its value using a Binding.
        /// </summary>
        /// <returns></returns>
        public string TvName
        {
            get
            {
                return tvName;
            }
            set
            {
                if (value != tvName)
                {
                    tvName = value;
                    NotifyPropertyChanged(nameof(TvName));
                }
            }
        }

        public Dictionary<string, string> TvMetaData;

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public bool Equals(TvItemViewModel other)
        {
            return TvAddress.Equals(other.TvAddress)
                && Port.Equals(other.Port)
                && TvName.Equals(other.TvName);
        }

        public bool Equals(HostName other)
        {
            return other.ToString().Equals(TvAddress);
        }
    }
}
