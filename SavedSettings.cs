using System;
using System.ComponentModel;
using System.IO;
using System.IO.IsolatedStorage;
using ProtoBuf;

namespace SamsungRemoteWP7
{
    [ProtoContract]
    public class SavedSettings : INotifyPropertyChanged
    {
        public static SavedSettings LoadedSettings = null;
        private const string SaveFileName = "samsungremote.settings";
        public static bool bIsLoading = false;

        private bool _bShouldVibrateOnKeyPress;
        [ProtoMember(1)]
        public bool bShouldVibrateOnKeyPress
        {
            get
            {
                return _bShouldVibrateOnKeyPress;
            }
            set
            {
                _bShouldVibrateOnKeyPress = value;
                NotifyPropertyChanged("bShouldVibrateOnKeyPress");

                if (!bIsLoading)
                {
                    TvKeyControl.ConditionalConfirmationVibration();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void SetDefaults()
        {
            bShouldVibrateOnKeyPress = true;
        }

        public static void Load()
        {
            bIsLoading = true;

            try
            {
                using (var IS = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (!IS.FileExists(SaveFileName))
                    {
                        LoadedSettings = new SavedSettings();
                        LoadedSettings.SetDefaults();
                    }
                    else
                    {
                        using (var stream = IS.OpenFile(SaveFileName, FileMode.Open))
                        {
                            LoadedSettings = Serializer.Deserialize<SavedSettings>(stream);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Caught an exception attempting to load user settings: " + e.Message + System.Environment.NewLine + e.StackTrace);
                LoadedSettings = new SavedSettings();
                LoadedSettings.SetDefaults();
            }

            bIsLoading = false;
        }

        public bool Save()
        {
            try
            {
                using (var IS = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (var stream = IS.CreateFile(SaveFileName))
                    {
                        Serializer.Serialize<SavedSettings>(stream, this);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Caught an exception attempting to write user settings: " + e.Message + System.Environment.NewLine + e.StackTrace);
                return false;
            }
        }
    }
}
