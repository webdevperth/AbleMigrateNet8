using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Integral.Web.WebHelpers {

  class ServerHelper {

    public static int UTCOffsetMinutes = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).Minutes;

    public static DateTime ServerTimeToUTC(DateTime ServerTime) {
      return ServerTime.ToUniversalTime();
    }

    public static DateTime UTCToServerTime(DateTime TimeUTC) {
      return TimeUTC.UtcToTZ();
    }

  }
}
