using System;
using System.Collections.Generic;
using System.Text;

namespace Integral.Web {

  public static partial class StringExt {

    public static T2 FindOrDefault<T1, T2>(this Dictionary<T1, T2> d, T1 key, T2 defaultValue) {
      if (d.ContainsKey(key)) return d[key];
      return defaultValue;
    }

    // Add new key or replace existing key with new value.
    public static void AddOrUpdate<T1, T2>(this Dictionary<T1, T2> d, T1 key, T2 value) {
      if (d.ContainsKey(key)) d[key] = value;
      else d.Add(key, value);
    }

    // This extension calls a lambda function to do something specific for keys that exist.
    // For example, this adds the given value to the existing value if the key exists:
    // myDic.AddOrUpdate(myKey, myValue, existingValue => existingValue + myValue);
    public static void AddOrUpdate<T1, T2>(this IDictionary<T1, T2> d, T1 key, T2 value, Func<T2, T2> onUpdate) {
      if (d.ContainsKey(key)) d[key] = onUpdate(d[key]);
      else d.Add(key, value);
    }

    public static string ToStringList(this IEnumerable<int> l, string defaultIfEmptyOrNull = "") {
      if (l == null) return defaultIfEmptyOrNull;
      return string.Join(",", l).ValueIfNullOrEmpty(defaultIfEmptyOrNull);
    }

    public static string ToStringList(this List<int> l, string defaultIfEmptyOrNull = "") {
      if (l == null || l.Count == 0) return defaultIfEmptyOrNull;
      return string.Join(",", l);
    }

    public static void AddIfMissing<T>(this List<T> list, T item) where T : struct {
      if (!list.Contains(item)) list.Add(item);
    }

    public static void AddIfMissing<T>(this Dictionary<string, T> dic, string key, T item) {
      if (!dic.ContainsKey(key)) dic.Add(key, item);
    }

    public static void AddIfNotNullOrEmpty(this List<string> list, string item) {
      if (!item.IsNullOrEmpty()) list.Add(item);
    }

    public static void RemoveIfPresent<T>(this List<T> list, T item) {
      if (list.Contains(item)) list.Remove(item);
    }

    public static string Join(this List<string> list, string separator) {
      var slist = new StringBuilder();
      if (!list.IsNullOrEmpty()) {
        foreach (var item in list) {
          if (!item.IsNullOrEmpty()) {
            if (slist.Length > 0) slist.Append(separator);
            slist.Append(item);
          }
        }
      }
      return slist.ToString();
    }

    public static string Join<T>(this List<T> list, string separator, Func<T, string> func) {
      var slist = new StringBuilder();
      foreach (var item in list) {
        string toAppend = func(item);
        if (!toAppend.IsNullOrEmpty()) {
          if (slist.Length > 0) slist.Append(separator);
          slist.Append(toAppend);
        }
      }
      return slist.ToString();
    }

    public static List<T> NullIfEmpty<T>(this List<T> list) {
      return list == null || list.Count == 0 ? null : list;
    }

    public static List<T> EmptyIfNull<T>(this List<T> list) {
      return list == null ? new List<T>() : list;
    }

    public static bool IsNullOrEmpty<T>(this List<T> list) {
      return (list == null || list.Count == 0);
    }

    public static bool IsNullOrEmpty<T1, T2>(this Dictionary<T1, T2> list) {
      return (list == null || list.Count == 0);
    }

    public static bool ContainsAny<T>(this List<T> thisList, List<T> anyOfList) {
      foreach (T item in anyOfList) {
        if (thisList.Contains(item)) return true;
      }
      return false;
    }

    public static bool ContainsAll<T>(this List<T> thisList, List<T> allOfList) {
      foreach (T item in allOfList) {
        if (!thisList.Contains(item)) return false;
      }
      return true;
    }

    public static bool IsNullOrEmpty<T>(this T[] array) {
      return (array == null || array == Array.Empty<T>() || array.Length == 0);
    }

  }
}
