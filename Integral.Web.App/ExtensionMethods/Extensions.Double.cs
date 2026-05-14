using System;

namespace Integral.Web {

  public static partial class StringExt {

    public static double Limit(this double i, double MinValue, double MaxValue) {
      if (i < MinValue) return MinValue;
      if (i > MaxValue) return MaxValue;
      return i;
    }

    public static double SwapIfGreaterThan(this double ShouldBeLesser, ref double ShouldBeGreater) {
      if (ShouldBeLesser <= ShouldBeGreater) return ShouldBeLesser; // no change
      double temp = ShouldBeGreater;
      ShouldBeGreater = ShouldBeLesser;
      return temp; // swapped
    }

    public static int RoundAwayFromZero(this double i) {
      return (int)Math.Round(i, 0, MidpointRounding.AwayFromZero);
    }

    public static double RoundAwayFromZero(this double i, int decimalPlaces) {
      return Math.Round(i, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    // Double?

    public static int? RoundAwayFromZeroOrNull(this double? i) {
      if (i == null) return null;
      return (int)Math.Round((double)i, 0, MidpointRounding.AwayFromZero);
    }

    public static double? RoundAwayFromZeroOrNull(this double? i, int decimalPlaces) {
      if (i == null) return null;
      return Math.Round((double)i, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static double Limit(this double? i, double MinValue, double MaxValue) {
      if (i == null) return MinValue;
      return ((double)i).Limit(MinValue, MaxValue);
    }

    public static double? LimitOrNull(this double? i, double MinValue, double MaxValue) {
      if (i == null) return null;
      return ((double)i).Limit(MinValue, MaxValue);
    }

    public static string ToStringOrNullValue(this double? i, string StringIfNull) {
      if (i == null) return StringIfNull;
      else return i.ToString();
    }

    public static double Round(this double d, int doublePlaces, MidpointRounding midpointRounding) {
      return Math.Round(d, doublePlaces, midpointRounding);
    }
    public static double? Round(this double? d, int doublePlaces, MidpointRounding midpointRounding, double? defaultIfNull = null) {
      if (d == null) return defaultIfNull;
      return Math.Round(d.Value, doublePlaces, midpointRounding);
    }

    public static int? ToIntOrDefault(this double? d, int? defaultIfNull = null) {
      if (d == null) return defaultIfNull;
      return (int)d.Value;
    }
    public static int ToInt(this double d) {
      return (int)d;
    }

    public static string ToString(this double? d, string format, string valueIfNull) {
      if (d == null)
        return valueIfNull;
      else
        return d.Value.ToString(format);
    }
    public static string ToString(this double? d, string format, double valueIfNull) {
      return (d ?? valueIfNull).ToString(format);
    }

  }
}
