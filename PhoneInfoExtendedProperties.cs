using Microsoft.Phone.Info;  

namespace SamsungRemoteWP7  
{
    public static class PhoneInfoExtendedProperties  
    {  
        private static readonly int ANIDLength = 32;  
        private static readonly int ANIDOffset = 2;  

        //Note: to get a result requires ID_CAP_IDENTITY_DEVICE  
        // to be added to the capabilities of the WMAppManifest  
        // this will then warn users in marketplace  
        public static byte[] GetDeviceUniqueID()  
        {  
            byte[] result = null;  
            object uniqueId;  
            if (DeviceExtendedProperties.TryGetValue("DeviceUniqueId", out uniqueId))  
                result = (byte[])uniqueId;  
  
            return result;  
        }  

        // NOTE: to get a result requires ID_CAP_IDENTITY_USER  
        //  to be added to the capabilities of the WMAppManifest  
        // this will then warn users in marketplace  
        public static string GetWindowsLiveAnonymousID() 
        {  
            string result = string.Empty;  
            object anid;  
            if (UserExtendedProperties.TryGetValue("ANID", out anid))  
            {  
                if (anid != null && anid.ToString().Length >= (ANIDLength + ANIDOffset))  
                {  
                    result = anid.ToString().Substring(ANIDOffset, ANIDLength);  
                }  
            }  
  
            return result;  
        }  
    }  
} 