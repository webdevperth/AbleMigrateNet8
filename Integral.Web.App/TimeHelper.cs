using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TimeZoneConverter;
using TimeZoneNames;

namespace Integral.Web {

  public class TimeHelper {

    public static IDictionary<string, string> GetIANATimeZonesForSelect() {
      var languageCode = CultureInfo.InstalledUICulture.Name;
      return TZNames.GetDisplayNames(languageCode, useIanaZoneIds: true);
    }

    public static string IANAToWindowsTimeZoneId(string timeZoneIdIana) {
      return TZConvert.IanaToWindows(timeZoneIdIana);
    }

    public static TimeZoneInfo GetTimeZoneInfo(string timeZoneIdWindowsOrIana) {
      return TZConvert.GetTimeZoneInfo(timeZoneIdWindowsOrIana);
    }

    // Note for all timezone functions, if no timeZone is given, default time zone (from ConfigHelper) is used.

    public static bool ValidateIANATimeZone(ref string timeZoneIdIana, bool assignDefaultIfBlank = true, bool throwIfInvalid = true) {
      if (timeZoneIdIana.IsNullOrEmpty()) {
        if (assignDefaultIfBlank) timeZoneIdIana = ConfigHelper.DefaultTimeZoneIdIana;
        else if (throwIfInvalid) throw new ArgumentException("IANA Time Zone ID is blank.");
        else return true; // Ok to be blank.
      }
      if (TZConvert.KnownIanaTimeZoneNames.Contains(timeZoneIdIana)) {
        return true; // Name is valid.
      } else {
        if (throwIfInvalid) throw new ArgumentException("Invalid IANA Time Zone Name: " + timeZoneIdIana);
        else return false; // Don't throw, instead return false.
      }
    }

    public static TimeZoneInfo GetTimeZoneInstanceOrDefaultAusWST(string timeZoneId) {
      TimeZoneInfo timeZoneInfo = null;
      try {
        TZConvert.TryGetTimeZoneInfo(timeZoneId, out timeZoneInfo);
      } catch (Exception) {
        // ignore
      }
      if (timeZoneInfo == null) return ConfigHelper.DefaultTimeZoneInfo;
      return timeZoneInfo;
    }

    public static TimeZoneInfo IANAToTimeZoneOrNull(string timeZoneIdIana) {
      if (timeZoneIdIana.IsNullOrEmpty()) return null;
      if (TZConvert.TryGetTimeZoneInfo(timeZoneIdIana, out var tz)) return tz;
      return null;
    }

    public static TimeZoneInfo IANAToTimeZoneOrAppDefault(string timeZoneIdIana) {
      var tz = IANAToTimeZoneOrNull(timeZoneIdIana);
      if (tz == null) return ConfigHelper.DefaultTimeZoneInfo;
      return tz;
    }

    public static DateTime? UtcToAppDefaultTimeZone(DateTime? dateTimeUtc) {
      return dateTimeUtc == null ? null : (DateTime?)TimeZoneInfo.ConvertTimeFromUtc((DateTime)dateTimeUtc, ConfigHelper.DefaultTimeZoneInfo);
    }

    public static DateTime UtcNowToAppDefaultTimeZone() {
      return (DateTime)UtcToAppDefaultTimeZone(DateTime.UtcNow);
    }

    public static DateTimeOffset? UtcToTimeZoneId(DateTime? fromUtc, string toTimeZoneIdWindowsOrIana) {
      if (fromUtc == null) return null;
      if (fromUtc == DateTime.MinValue) return DateTimeOffset.MinValue;
      if (toTimeZoneIdWindowsOrIana.IsNullOrEmpty()) toTimeZoneIdWindowsOrIana = ConfigHelper.DefaultTimeZoneIdIana;
      fromUtc = DateTime.SpecifyKind(fromUtc.Value, DateTimeKind.Utc);
      var toTimeZone = TZConvert.GetTimeZoneInfo(toTimeZoneIdWindowsOrIana);
      return TimeZoneInfo.ConvertTime(new DateTimeOffset(fromUtc.Value), toTimeZone);
    }

    public static DateTime? TimeZoneIdToUtc(DateTime? fromLocal, string fromTimeZoneIdWindowsOrIana) {
      if (fromLocal == null) return null;
      if (fromLocal == DateTime.MinValue) return DateTime.MinValue;
      if (fromTimeZoneIdWindowsOrIana.IsNullOrEmpty()) fromTimeZoneIdWindowsOrIana = ConfigHelper.DefaultTimeZoneIdIana;
      fromLocal = DateTime.SpecifyKind(fromLocal.Value, DateTimeKind.Unspecified);
      var fromTimeZone = TZConvert.GetTimeZoneInfo(fromTimeZoneIdWindowsOrIana);
      return TimeZoneInfo.ConvertTimeToUtc(fromLocal.Value, fromTimeZone);
    }

    public static TimeSpan? GetUtcOffset(DateTime? dateTimeLocal, string timeZoneIdWindowsOrIana) {
      if (dateTimeLocal == null) return null;
      if (timeZoneIdWindowsOrIana.IsNullOrEmpty()) timeZoneIdWindowsOrIana = ConfigHelper.DefaultTimeZoneIdIana;
      dateTimeLocal = DateTime.SpecifyKind(dateTimeLocal.Value, DateTimeKind.Unspecified);
      var fromTimeZone = TZConvert.GetTimeZoneInfo(timeZoneIdWindowsOrIana);
      return TZConvert.GetTimeZoneInfo(timeZoneIdWindowsOrIana).GetUtcOffset((DateTime)dateTimeLocal);
    }

    public static DateTimeOffset? GetDateTimeOffset(DateTime? dateTimeLocal, string timeZoneIdWindowsOrIana) {
      if (dateTimeLocal == null || dateTimeLocal == DateTime.MinValue) return null;
      dateTimeLocal = DateTime.SpecifyKind(dateTimeLocal.Value, DateTimeKind.Unspecified);
      var utcOffset = GetUtcOffset(dateTimeLocal, timeZoneIdWindowsOrIana);
      if (utcOffset == null) return null;
      return new DateTimeOffset((DateTime)dateTimeLocal, (TimeSpan)utcOffset);
    }

    public static void GetFirstAndLastDaysOfMonth(DateTime startDate, DateTime endDate, out DateTime firstDayOfStartMonth, out DateTime lastDayOfEndMonth) {
      firstDayOfStartMonth = new DateTime(startDate.Year, startDate.Month, 1); // First day of month of the start date.
      lastDayOfEndMonth = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(1).AddDays(-1); // Last day of month of the end date.
    }

    public static int GetCalendarMonthsSpanning(DateTime startDate, DateTime endDate) {
      return (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month + 1;
    }

  }
}
