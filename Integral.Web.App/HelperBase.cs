using System.Collections.Generic;

namespace Integral.Web {

  public class HelperBase<T>
  where T : HelperBase<T> { // see https://stackoverflow.com/questions/3064227/get-derived-class-type-from-a-bases-class-static-method

    private static string RequestItemName_Log;
    private static bool NoLogging = false;

    static HelperBase() {
      RequestItemName_Log = ConfigHelper.RequestItems.HelperLog_Prefix + typeof(T).Name; // Name of derived class.
      NoLogging = !ConfigHelper.IsDevServer;
    }

    public static class Logging {

      public static List<string> GetLog() {
        if (NoLogging || RequestItemName_Log.IsNullOrEmpty()) return null;
        if (!AppHelper.RequestItemExists(RequestItemName_Log)) {
          AppHelper.SetRequestItem(RequestItemName_Log, new List<string>());
          AddLog("Begin Log For " + typeof(T).Name);
        }
        return (List<string>)AppHelper.GetRequestItemOrNull(RequestItemName_Log);
      }

      public static void AddLog(string logText) {
        if (NoLogging || RequestItemName_Log.IsNullOrEmpty() || logText.IsNullOrEmpty()) return;
        var RequestLog = GetLog();
        if (RequestLog != null) RequestLog.Add(logText);
      }
      public static string GetLastLogText() {
        if (NoLogging || RequestItemName_Log.IsNullOrEmpty()) return "";
        var RequestLog = GetLog();
        if (RequestLog == null || RequestLog.Count == 0) return "";
        else return RequestLog[RequestLog.Count - 1];
      }
      public static string GetLogAsText(string delimeter = "\n") {
        if (NoLogging || RequestItemName_Log.IsNullOrEmpty()) return "";
        var RequestLog = GetLog();
        if (RequestLog == null || RequestLog.Count == 0) return "";
        else return RequestLog.ToArray().Join(delimeter);
      }
      public static string GetClassName() {
        return typeof(T).Name;
      }

    }
  }
}
