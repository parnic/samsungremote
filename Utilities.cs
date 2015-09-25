using System;
using Windows.Phone.Devices.Notification;

namespace UnofficialSamsungRemote
{
    class Utilities
    {
        public static string GetAnonymousId()
        {
            return Windows.System.UserProfile.AdvertisingManager.AdvertisingId ?? String.Empty;
        }

        public static bool CanVibrate
        {
            get
            {
                return Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice");
            }
        }

        public static void VibrateDevice(TimeSpan length)
        {
            if (CanVibrate)
            {
                VibrationDevice.GetDefault()?.Vibrate(length);
            }
        }
    }
}
