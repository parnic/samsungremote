using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnofficialSamsungRemote
{
    class Utilities
    {
        public static string GetAnonymousId()
        {
            return Windows.System.Profile.AnalyticsInfo.DeviceForm;
        }
    }
}
