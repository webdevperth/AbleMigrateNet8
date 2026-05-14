using System;

namespace Integral.Web {

  public static partial class StringExt {

    public static int? NullIf(this int value, int nullIfValue) {
      if (value == nullIfValue) return null;
      return value;
    }

    public static bool TryGetEnum<TEnum>(this int value, out TEnum result) where TEnum : struct, Enum {

      if (Enum.IsDefined(typeof(TEnum), value)) {
        result = (TEnum)(object)value;
        return true;
      }

      result = default;
      return false;
    }

    public static int Limit(this int i, int MinValue, int MaxValue) {
      if (i < MinValue) return MinValue;
      if (i > MaxValue) return MaxValue;
      return i;
    }

    public static bool Outside(this int i, int MinValue, int MaxValue) {
      return i < MinValue || i > MaxValue;
    }

    public static object OrDBNullIfZero(this int obj) {
      if (obj == 0)
        return DBNull.Value;
      else
        return obj;
    }

    public static int SwapIfGreaterThan(this int ShouldBeLesser, ref int ShouldBeGreater) {
      if (ShouldBeLesser <= ShouldBeGreater) return ShouldBeLesser; // no change
      int temp = ShouldBeGreater;
      ShouldBeGreater = ShouldBeLesser;
      return temp; // swapped
    }

    // Int?

    public static int Limit(this int? i, int MinValue, int MaxValue) {
      if (i == null) return MinValue;
      return ((int)i).Limit(MinValue, MaxValue);
    }

    public static string ToStringOrNullValue(this int? i, string StringIfNull) {
      if (i == null) return StringIfNull;
      else return i.ToString();
    }
  }
}
