namespace Integral.Web {

  public static partial class StringExt {

    public static int ToBinary(this bool i) {
      return i ? 1 : 0;
    }

    public static string ToValue(this bool? boolValue, string stringIfTrue, string stringIfFalse = "", string stringIfNull = "") {
      if (boolValue == null) return stringIfNull;
      return (bool)boolValue ? stringIfTrue : stringIfFalse;
    }

    public static string ToValue(this bool boolValue, string stringIfTrue, string stringIfFalse = "") {
      return ToValue((bool?)boolValue, stringIfTrue, stringIfFalse);
    }

    public static int? ToBinaryOrNull(this bool? boolValue) {
      if (boolValue == null) return null;
      return (bool)boolValue ? 1 : 0;
    }

    public static string ToJSTrueFalse(this bool boolValue) {
      return boolValue ? "true" : "false";
    }

    public static string ToJSTrueFalseOrNull(this bool? boolValue) {
      if (boolValue == null) return "null";
      else if (boolValue == true) return "true";
      return "false";
    }

  }
}
