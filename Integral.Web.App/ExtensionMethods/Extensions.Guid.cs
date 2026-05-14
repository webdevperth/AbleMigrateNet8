using System;

namespace Integral.Web {

  public static partial class StringExt {

    public static string ToStringNoBraces(this Guid s) {
      return s.ToString().Replace("{", "").Replace("}", "");
    }

    public static string ToStringNoBracesOrNull(this Guid? s) {
      if (s == null) return null;
      return s.ToString().Replace("{", "").Replace("}", "");
    }

    public static string ToStringNoBracesOrDefault(this Guid? s, string defaultStr) {
      if (s == null) return defaultStr;
      return s.ToString().Replace("{", "").Replace("}", "");
    }

    public static bool IsEmpty(this Guid g) {
      return g.CompareTo(Guid.Empty) == 0;
    }
    public static bool IsNullOrEmpty(this Guid? g) {
      return g == null || ((Guid)g).CompareTo(Guid.Empty) == 0;
    }

  }
}
