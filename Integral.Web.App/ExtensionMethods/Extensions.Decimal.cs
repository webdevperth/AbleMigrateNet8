using System;

namespace Integral.Web {

  public static partial class StringExt {

    public static decimal Limit(this decimal i, decimal MinValue, decimal MaxValue) {
      if (i < MinValue) return MinValue;
      if (i > MaxValue) return MaxValue;
      return i;
    }

    public static decimal SwapIfGreaterThan(this decimal ShouldBeLesser, ref decimal ShouldBeGreater) {
      if (ShouldBeLesser <= ShouldBeGreater) return ShouldBeLesser; // no change
      decimal temp = ShouldBeGreater;
      ShouldBeGreater = ShouldBeLesser;
      return temp; // swapped
    }

    public static int RoundAwayFromZero(this decimal i) {
      return (int)Math.Round(i, 0, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundAwayFromZero(this decimal i, int decimalPlaces) {
      return Math.Round(i, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    // decimal?

    public static int? RoundAwayFromZeroOrNull(this decimal? i) {
      if (i == null) return null;
      return (int)Math.Round((decimal)i, 0, MidpointRounding.AwayFromZero);
    }

    public static decimal? RoundAwayFromZeroOrNull(this decimal? i, int decimalPlaces) {
      if (i == null) return null;
      return Math.Round((decimal)i, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static decimal Limit(this decimal? i, decimal MinValue, decimal MaxValue) {
      if (i == null) return MinValue;
      return ((decimal)i).Limit(MinValue, MaxValue);
    }

    public static decimal? LimitOrNull(this decimal? i, decimal MinValue, decimal MaxValue) {
      if (i == null) return null;
      return ((decimal)i).Limit(MinValue, MaxValue);
    }

    public static string ToStringOrNullValue(this decimal? i, string StringIfNull) {
      if (i == null) return StringIfNull;
      else return i.ToString();
    }

    public static decimal Round(this decimal d, int decimalPlaces, MidpointRounding midpointRounding) {
      return Math.Round(d, decimalPlaces, midpointRounding);
    }
    public static decimal? Round(this decimal? d, int decimalPlaces, MidpointRounding midpointRounding, decimal? defaultIfNull = null) {
      if (d == null) return defaultIfNull;
      return Math.Round(d.Value, decimalPlaces, midpointRounding);
    }

    public static int? ToIntOrDefault(this decimal? d, int? defaultIfNull = null) {
      if (d == null) return defaultIfNull;
      return (int)d.Value;
    }
    public static int ToInt(this decimal d) {
      return (int)d;
    }

    public static string ToString(this decimal? d, string format, string valueIfNull) {
      if (d == null)
        return valueIfNull;
      else
        return d.Value.ToString(format);
    }
    public static string ToString(this decimal? d, string format, decimal valueIfNull) {
      return (d ?? valueIfNull).ToString(format);
    }

  }
}
