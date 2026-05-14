using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class AppHelper {

    public class HttpHeaders {
      public const string IsLive = "X-Able-IsLive";
      public const string IsAjax = "X-Able-IsAjax";
      public const string AjaxAction = "X-Able-AjaxAction";
      public const string IsPartial = "X-Able-IsPartial";
      public const string Referrer = "X-Able-Referrer";
      public const string IsTabulator = "X-Able-IsTabulator";
      public const string DebugMessage = "X-Able-DebugMessage";
    }

    public class RequestItemKey {
      public const string BodyClass = "BodyClass";
      public const string ReferrerUri = "ReferrerUri";
      public const string SequentialNumber = "SequentialNumber";
      public const string RequestExiting = "RequestExiting";
    }

    public static class Regex {

      private const string InvalidEmailChars = @"\@\[\]\<\>\\"" :;,";

      public static readonly string Email = $@"^[^{InvalidEmailChars}]+@[^\.{InvalidEmailChars}]+(\.[^\.{InvalidEmailChars}]+)+$";
      public static readonly string Mobile = @"^\+?[0-9]+(?:[ \-]?[0-9]{2,})+$";
      public static readonly string PostCode = "^([0-9]{4,6})?$";
      public static readonly string GeneralText = @"^[a-z0-9(),./!@#$%&*_:;'’“""? +–\-\r\n=]*$";
      public static readonly string HTML = GeneralText.Replace("]", @"<>\\\*\|]");
      public static readonly string Password = "^[a-z0-9!@#$%&?_]+$";
      public static readonly string OnlyLetters = "^[a-z]*$";
      public static readonly string OnlyNumbers = "^[0-9]*$";
      public static readonly string IntegerList = @"^\s*[0-9]+(?:\s*,\s*[0-9]+)*\s*$";
      public static readonly string FileName = @"^[^\\\/:\*""<>|]+$";
      public static readonly string Domain = @"^(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?:[a-z]{2,}|xn--[a-z0-9-]+)$";
      public static readonly string URL = $@"^(https?|ftp):\/\/{Domain.Substring(1, Domain.Length - 2)}(?:\/[^\s]*)?$";
      public static readonly string UserName = @"^[a-zA-Z0-9(),./!@#$%&*_:;'’“""? +–\-\r\n=]*$";
    }

    public const string RequestInfoItemKey = "RequestInfoItemKey";
    private static DateTime _appStartTimeUtc = DateTime.MinValue;

    public static Random GetRandomInstance() {
      // A more reliable unique seed for when new Random is occasionally called repeatedly too quickly.
      return new Random(Guid.NewGuid().GetHashCode() + System.Diagnostics.Process.GetCurrentProcess().Id);
    }

    private static Dictionary<string, object> UnitTestingRequestItems = new Dictionary<string, object>();

    internal static RequestInfo GetRequestInfo() {
      var ri = GetRequestItemOrNull(RequestInfoItemKey) as RequestInfo;
      if (ri == null) {
        ri = new RequestInfo();
        SetRequestItem(RequestInfoItemKey, ri);
      }
      return ri;
    }

    public static DateTime? GetRequestStartTimeUtc() {
      return GetRequestInfo().RequestStartTimeUtc;
    }
    public static void SetRequestStartTimeUtcNow() {
      GetRequestInfo().RequestStartTimeUtc = DateTime.UtcNow;
    }

    public static void SetAppStartTime(DateTime appStartTimeUtc) {
      _appStartTimeUtc = appStartTimeUtc;
    }

    public static object GetRequestItemOrNull(string key) {
      if (ConfigHelper.IsUnitTestRunning) {
        if (UnitTestingRequestItems.ContainsKey(key)) return UnitTestingRequestItems[key];
      } else {
        if (SystemWeb.RequestItemExists(key)) return SystemWeb.GetRequestItemValue(key);
      }
      return null;
    }

    public static T? GetRequestItemOrNull<T>(string key) where T : struct {
      if (ConfigHelper.IsUnitTestRunning) {
        if (UnitTestingRequestItems.ContainsKey(key)) {
          if (UnitTestingRequestItems[key] is T value) return value;
        }
      } else if (SystemWeb.RequestItemExists(key)) {
        if (SystemWeb.GetRequestItemValue(key) is T value) return value;
      }
      return null;
    }

    public static void SetRequestItem<T>(string key, T value) {
      if (ConfigHelper.IsUnitTestRunning) {
        UnitTestingRequestItems[key] = value;
      } else {
        SystemWeb.SetRequestItemValue(key, value);
      }
    }

    public static bool RequestItemExists(string key) {
      if (ConfigHelper.IsUnitTestRunning) {
        return UnitTestingRequestItems.ContainsKey(key);
      } else {
        return SystemWeb.RequestItemExists(key);
      }
    }

    public static void RemoveRequestItem(string key) {
      if (ConfigHelper.IsUnitTestRunning) {
        if (UnitTestingRequestItems.ContainsKey(key)) UnitTestingRequestItems.Remove(key);
      } else {
        if (SystemWeb.RequestItemExists(key)) SystemWeb.RequestItemRemove(key);
      }
    }

    public static string MapPath(string httpPath) {
      if (ConfigHelper.IsUnitTestRunning) {
        return (ConfigHelper.AppRootPath + httpPath.Replace("/", @"\")).Replace(@"\\", @"\");
      } else {
        return SystemWeb.ServerMapPath(httpPath);
      }
    }

    internal class RequestInfo {
      internal DateTime? RequestStartTimeUtc { get; set; }
      internal List<LogHelper.SqlQueryLogInfo> SqlQueryLog { get; set; } = new List<LogHelper.SqlQueryLogInfo>();
      internal LogHelper.SqlQueryLogInfo AddToSQLQueryLog(string queryText, TimeSpan duration) {
        var logItem = new LogHelper.SqlQueryLogInfo(queryText, duration);
        SqlQueryLog.Add(logItem);
        return logItem;
      }
    }

    public class ParamEnum {
      public enum UserAccessLevel { Normal, TenantAdmin, GlobalAdmin };
      public enum ProjectAccessLevel { None, RelatedOrInvited, TenantOrg, All };
      public enum UserSearchFilter { None, TenantOrg, All };
    }

  }
}
