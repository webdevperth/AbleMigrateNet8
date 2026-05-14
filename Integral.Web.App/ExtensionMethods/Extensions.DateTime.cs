using System;

namespace Integral.Web {

  public static partial class StringExt {

    private static DateTime unixEpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);

    public static string ToString(this DateTime dt, string format) {
      return dt.ToString(format);
    }
    public static string ToString(this DateTime? dt, string format, string defaultIfNull = "") {
      if (dt == null) return defaultIfNull;
      return ToString((DateTime)dt, format);
    }

    public static DateTime ToUniversalTime(this DateTime localTime, TimeZoneInfo timeZoneOrNullForDefault) {
      // If no timeZone is given, default time zone is used.
      localTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
      return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZoneOrNullForDefault ?? ConfigHelper.DefaultTimeZoneInfo);
    }
    public static DateTime? ToUniversalTimeOrNull(this DateTime? localTime, TimeZoneInfo timeZoneOrNullForDefault = null) {
      if (localTime == null) return null;
      localTime = DateTime.SpecifyKind(localTime.Value, DateTimeKind.Unspecified);
      return ToUniversalTime((DateTime)localTime, timeZoneOrNullForDefault);
    }

    public static DateTime SpecifyKind(this DateTime dt, DateTimeKind kind) {
      return DateTime.SpecifyKind(dt, kind);
    }
    public static DateTime? SpecifyKindOrNull(this DateTime? dt, DateTimeKind kind) {
      if (dt == null) return null;
      return SpecifyKind((DateTime)dt, kind);
    }

    // Note for all timezone functions, if no timeZone is given, default time zone (from ConfigHelper) is used.

    public static DateTime UtcToTZ(this DateTime dateTimeUtc, TimeZoneInfo timeZoneOrNullForDefault = null) {
      return (DateTime)TimeHelper.UtcToTimeZoneId(dateTimeUtc, timeZoneOrNullForDefault?.Id).ToDateTimeOrNull();
    }
    public static DateTime? UtcToTZOrNull(this DateTime? dateTimeUtc, TimeZoneInfo timeZoneOrNullForDefault = null) {
      return TimeHelper.UtcToTimeZoneId(dateTimeUtc, timeZoneOrNullForDefault?.Id).ToDateTimeOrNull();
    }
    public static DateTime UtcToTZId(this DateTime dateTimeUtc, string timeZoneIdWindowsOrIana = null) {
      return (DateTime)TimeHelper.UtcToTimeZoneId(dateTimeUtc, timeZoneIdWindowsOrIana).ToDateTimeOrNull();
    }
    public static DateTime? UtcToTZIdOrNull(this DateTime? dateTimeUtc, string timeZoneIdWindowsOrIana = null) {
      return TimeHelper.UtcToTimeZoneId(dateTimeUtc, timeZoneIdWindowsOrIana).ToDateTimeOrNull();
    }

    public static string UtcToJSOrEmptyIfNull(this DateTime? dateTimeUtc) {
      if (dateTimeUtc == null) return string.Empty;
      // Note accuracy fixed to milliseconds which is the limit for JS.
      return dateTimeUtc.Value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
    }
    public static string UtcToJS(this DateTime dateTimeUtc) {
      return UtcToJSOrEmptyIfNull(dateTimeUtc);
    }

    public static string UtcToMomentJS(this DateTime dateTimeUtc) {
      // Accuracy to seconds.
      return dateTimeUtc.ToString("yyyy-MM-dd'T'HH:mm:ss+00:00");
    }

    public static DateTime FromSecondsSinceUnixEpoch(this DateTime anyDateTime, long secondsSinceUnixEpochUtc) {
      return unixEpochUtc.AddSeconds(secondsSinceUnixEpochUtc);
    }
    public static long SecondsSinceUnixEpoch(this DateTime dateTimeUtc) {
      return (dateTimeUtc - unixEpochUtc).TotalSeconds.RoundAwayFromZero();
    }

    public static DateTime? AddLocalTimeToUtc(this DateTime? dateTimeUtc, int days, int hours, int minutes, TimeZoneInfo localTimeZone, bool discardTime = false) {
      if (dateTimeUtc == null) return null;
      dateTimeUtc = DateTime.SpecifyKind(dateTimeUtc.Value, DateTimeKind.Utc);
      return ((DateTime)dateTimeUtc).AddLocalTimeToUtc(days, hours, minutes, localTimeZone, discardTime);
    }

    public static DateTime AddLocalTimeToUtc(this DateTime dateTimeUtc, int days, int hours, int minutes, TimeZoneInfo localTimeZone, bool discardTime = false) {
      if (localTimeZone == null) localTimeZone = ConfigHelper.DefaultTimeZoneInfo;
      dateTimeUtc = DateTime.SpecifyKind(dateTimeUtc, DateTimeKind.Utc);
      DateTime dtLocal = ((DateTime)dateTimeUtc).UtcToTZ(localTimeZone);
      if (discardTime) dtLocal = dtLocal.Date; // Add from the date at 00:00
      if (days != 0) dtLocal = dtLocal.AddDays(days);
      if (hours != 0) dtLocal = dtLocal.AddHours(hours);
      if (minutes != 0) dtLocal = dtLocal.AddMinutes(minutes);
      return dtLocal.ToUniversalTime(localTimeZone);
    }

    public static string ToStringOrDefaultIfNull(this DateTimeOffset? obj, string format, string defaultValue) {
      if (obj == null)
        return defaultValue;
      else
        return ((DateTimeOffset)obj).ToString(format);
    }

    public static DateTime? ToDateTimeOrNull(this DateTimeOffset? dto) {
      if (dto == null) return null;
      return ((DateTimeOffset)dto).DateTime;
    }

    public static DateTime? ToUtcDateTimeOrNull(this DateTimeOffset? dto) {
      if (dto == null) return null;
      return ((DateTimeOffset)dto).UtcDateTime;
    }

    public static DateTimeOffset ToDateTimeOffset(this DateTime dt, TimeSpan offset) {
      dt = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
      return new DateTimeOffset(dt, offset);
    }

    public static DateTimeOffset ToDateTimeOffset(this DateTime dt, int hours, int minutes = 0, int seconds = 0) {
      dt = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
      return new DateTimeOffset(dt, new TimeSpan(hours, minutes, seconds));
    }

    public static DateTimeOffset ToDateTimeOffset(this DateTime localDateTime, TimeZoneInfo localTimeZone) {
      localDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
      return new DateTimeOffset(localDateTime, localTimeZone.GetUtcOffset(localDateTime));
    }

    public static DateTimeOffset? ToDateTimeOffset(this DateTime? localDateTime, string timeZoneIdWindowsOrIana) {
      if (localDateTime == null) return null;
      localDateTime = DateTime.SpecifyKind(localDateTime.Value, DateTimeKind.Unspecified);
      return TimeHelper.GetDateTimeOffset(localDateTime, timeZoneIdWindowsOrIana);
    }

  }
}
