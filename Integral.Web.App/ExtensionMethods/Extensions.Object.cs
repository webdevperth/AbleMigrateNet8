using System;

namespace Integral.Web {

  public static partial class StringExt {

    public static string ToStringOrDefaultIfNull(this object obj, string defaultValue) {
      if (obj == null)
        return defaultValue;
      else
        return obj.ToString();
    }

    public static string ToStringOrEmptyIfNull(this object obj) {
      if (obj == null)
        return string.Empty;
      else
        return obj.ToString();
    }

    public static string ToStringOrNull(this object obj) {
      if (obj == null)
        return null;
      else
        return obj.ToString();
    }

    public static object OrDBNull(this object obj) {
      if (obj == null)
        return DBNull.Value;
      else
        return obj;
    }

    public static bool ToBooleanOrDefault(this object o, bool defaultValue) {
      if (o == null) return defaultValue;
      if (o is string) {
        switch (o.ToString().ToLower()) {
          case "yes":
          case "y":
          case "true":
          case "ok":
            return true;
          case "no":
          case "n":
          case "false":
            return false;
        }
        return defaultValue;
      }
      if (decimal.TryParse(o.ToString(), out decimal d)) return d != 0;
      if (bool.TryParse(o.ToString(), out bool b)) return b;
      return defaultValue;
    }

  }
}
