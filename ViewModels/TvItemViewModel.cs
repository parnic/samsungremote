using System;
using System.Net;
using System.ComponentModel;
using System.Collections.Generic;

namespace SamsungRemoteWP7
{
    public class TvItemViewModel : INotifyPropertyChanged
    {
        private IPAddress tvAddress;
        /// <summary>
        /// Sample ViewModel property; this property is used in the view to display its value using a Binding.
        /// </summary>
        /// <returns></returns>
        public IPAddress TvAddress
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
                    NotifyPropertyChanged("TvAddress");
                }
            }
        }

        private int port;
        /// <summary>
        /// Sample ViewModel property; this property is used in the view to display its value using a Binding.
        /// </summary>
        /// <returns></returns>
        public int Port
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
                    NotifyPropertyChanged("Port");
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
                    NotifyPropertyChanged("TvName");
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
    }
}
