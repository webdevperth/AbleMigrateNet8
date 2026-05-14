using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Integral.Web {

  public static partial class StringExt {

    private const string REPLACE_TAG_START = "[";
    private const string REPLACE_TAG_END = "]";

    public enum IgnoreCase { No, Yes }
    public enum ApplyWhenString { Always, NotNull, NotNullOrEmpty, NotNullOrEmptyOrWhitespace }
    public enum Ensure { Always, IfNotBlank }

    public static List<string> ToList(this string s, string separator, StringSplitOptions option) {
      return new List<string>(s.EmptyIfNull().Split(new string[] { separator }, option));
    }

    public static string Chop(this string s, int length) {

      if (String.IsNullOrEmpty(s)) return string.Empty;

      var words = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

      if (words[0].Length > length)
        throw new ArgumentException("First word is too long");

      var sb = new StringBuilder();

      foreach (var word in words) {
        if ((sb + word).Length > length)
          return string.Format("{0}...", sb.ToString().TrimEnd(' '));
        sb.Append(word + " ");
      }

      return string.Format("{0}...", sb.ToString().TrimEnd(' '));
    }

    public static bool EqualsIgnoreCase(this string s, string compareTo) {
      if (s == null || compareTo == null) return s == compareTo;
      return s.Equals(compareTo, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsIgnoreCase(this string s, string value) {
      if (s == null || value == null) return false;
      return s.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string JSONEncode(this string s, bool includeSurroundingQuotes = false) {
      string jsonString = JsonConvert.ToString(s); // Note this adds quotes around the string.
      if (!includeSurroundingQuotes) {
        jsonString = Regex.Replace(jsonString, "^\"|\"$", string.Empty);
      }
      return jsonString;
    }

    public static string JSEncode(this string value) {
      if (string.IsNullOrWhiteSpace(value)) return string.Empty;
      return SystemWeb.JavaScriptStringEncode(value)
        .Replace("<", "\\x3c")
        .Replace(">", "\\x3e");
    }

    public static string Join(this string[] stringArray, string separator, Func<string, string> convert = null) {
      if (stringArray == null) return null;
      if (convert != null) {
        for (int i = 0; i < stringArray.Length; i++) {
          stringArray[i] = convert(stringArray[i]);
        }
      }
      return string.Join(separator, stringArray);
    }

    public static TEnum ToEnum<TEnum>(this string value, TEnum defaultIfNotDefined, IgnoreCase ignoreCase = IgnoreCase.No) where TEnum : struct {
      if (value == null) return defaultIfNotDefined;
      if (!Enum.TryParse(value, ignoreCase == IgnoreCase.Yes, out TEnum result)) return defaultIfNotDefined; // TryParse failed.
      if (int.TryParse(result.ToString(), out _)) return defaultIfNotDefined; // Result should not be a number (should be name of enum item).
      return result;
    }

    public static string ToUtf8String(this string s) {
      if (String.IsNullOrEmpty(s))
        return string.Empty;

      var utf8Encoding = new System.Text.UTF8Encoding();
      var encodedString = utf8Encoding.GetBytes(s);

      return utf8Encoding.GetString(encodedString);

    }

    public static string ToUtf8String(this StringBuilder s) {
      if (s == null)
        return string.Empty;

      if (String.IsNullOrEmpty(s.ToString()))
        return string.Empty;

      var utf8Encoding = new System.Text.UTF8Encoding();
      var encodedString = utf8Encoding.GetBytes(s.ToString());

      return utf8Encoding.GetString(encodedString);

    }

    public static string Left(this string s, int length) {
      if (s == null) return null;
      else if (s.Length <= length) return s;
      else return s.Substring(0, length);
    }

    public static string Right(this string s, int length) {
      if (s == null) return null;
      else if (s.Length <= length) return s;
      else return s.Substring(s.Length - length, length);
    }

    /// <summary>
    /// Returns substring of a string as per VB.
    /// </summary>
    /// <param name="start">1-based start position</param>
    /// <param name="length">Length</param>
    /// <returns>String</returns>
    public static string Mid(this string s, int start, int length) {
      if (s == null) return null;
      if (start <= 0 || length <= 0 || start > s.Length) return string.Empty;
      else if (start + length - 1 > s.Length) return s.Substring(start - 1);
      else return s.Substring(start - 1, length);
    }

    public static string Mid(this string s, int start) {
      if (s == null) return null;
      return Mid(s, start, s.Length);
    }

    public static string ValueIfHTMLEmptyTags(this string s, string valueIfHTMLEmptyTags) {
      if (s == null || (!Regex.IsMatch(s.Replace("&nbsp;", string.Empty), @"[^<>\s]+\s*(?:<|(?:\s*$))") && !Regex.IsMatch(s, "<(?:img|iframe)", RegexOptions.IgnoreCase))) {
        return valueIfHTMLEmptyTags;
      } else {
        return s;
      }
    }

    public static string ValueIfNull(this string s, string valueIfNull) { // Saves using "String.IsNullOrEmpty(var)"
      return s == null ? valueIfNull : s;
    }

    public static string ValueIfNullOrEmpty(this string s, string valueIfNullOrEmpty) { // Saves using "String.IsNullOrEmpty(var)"
      return s.IsNullOrEmpty() ? valueIfNullOrEmpty : s;
    }

    public static string IfNotNullOrEmpty(this string s, string value) { // Saves doing x.isnullorempty() ? string.Empty : "something"
      return s.IsNullOrEmpty() ? string.Empty : value;
    }

    public static string IfNotNullOrEmpty(this string s, Func<string, string> result) { // Allows doing someobj.membername.IfNotNullOrEmpty(s => $"blah{s.HTMLEncode()}blah")
      return result(s);
    }

    public static bool IsNullOrEmpty(this string s) { // Saves using "String.IsNullOrEmpty(var)"
      return string.IsNullOrEmpty(s);
    }

    public static bool IsNullOrEmptyOrWhitespace(this string s) {
      return string.IsNullOrWhiteSpace(s);
    }

    public static string NumericCSVOrDefault(this string str, string defaultValue) {
      // If string is not a comma-delimited list of numbers, return the default value.
      str = Regex.Replace(str.EmptyIfNull(), "\\s", string.Empty, RegexOptions.Singleline); // First remove spaces.
      if (Regex.IsMatch(str, "[0-9]+(?:,[0-9]+)*"))
        return str;
      else
        return defaultValue;
    }

    public static int LengthOrZero(this string s) => (s?.Length) ?? 0;

    public static string LimitLengthTo(this string s, int MaxLength, string appendIfLonger = "") {
      if (s.IsNullOrEmpty() || s.Length <= MaxLength)
        return s;
      else
        return s.Substring(0, MaxLength) + appendIfLonger;
    }

    public static object OrDBNullIfEmpty(this string s) {
      if (s.IsNullOrEmpty())
        return DBNull.Value;
      else
        return s;
    }

    public static object ToIntOrDBNull(this string s) {
      int i;
      if (!String.IsNullOrEmpty(s) && int.TryParse(s, out i))
        return i;
      else
        return DBNull.Value;
    }

    public static int ToIntOrDefault(this string s, int defaultValue) {
      if (String.IsNullOrEmpty(s)) return defaultValue;
      int i;
      if (int.TryParse(s, out i))
        return i;
      else
        return defaultValue;
    }

    public static int? ToIntOrNull(this string s) {
      if (String.IsNullOrEmpty(s)) return null;
      if (int.TryParse(s, out int i))
        return i;
      else
        return null;
    }

    public static decimal ToDecimalOrDefault(this string s, decimal defaultValue) {
      if (String.IsNullOrEmpty(s)) return defaultValue;
      decimal i;
      if (decimal.TryParse(s, out i))
        return i;
      else
        return defaultValue;
    }

    public static decimal? ToDecimalOrNull(this string s) {
      if (String.IsNullOrEmpty(s)) return null;
      decimal i;
      if (decimal.TryParse(s, out i))
        return i;
      else
        return null;
    }

    public static List<int> ToIntList(this string list, char delimiter = ',') {
      if (list.IsNullOrEmpty()) return new List<int>();
      var strArray = list.Split(delimiter);
      var intArray = Array.ConvertAll(list.Split(delimiter), int.Parse);
      var intList = new List<int>(intArray);
      return intList;
    }

    // Take a string like "abc-xyz" and return 2 strings: left side "abc", right side "xyz".
    // Note if there is more than 1 separator, e.g. "123-456-789" then the right side will be "456-789".
    public static void SplitToStrings(this string joinedString, char separator, out string leftSide, out string rightSide) {

      leftSide = null;
      rightSide = null;

      if (joinedString.IsNullOrEmpty()) return;

      int separatorPos = joinedString.IndexOf(separator);

      if (separatorPos == -1) { // No right side.
        leftSide = joinedString;
        return;
      }

      leftSide = joinedString.Substring(0, separatorPos); // Characters before the separator if any.

      if (joinedString.Length > separatorPos + 1) rightSide = joinedString.Substring(separatorPos + 1); // Characters after the separator if any.
    }

    public static int ToIntOrZero(this string s) {
      return ToIntOrNull(s).GetValueOrDefault(0);
    }

    public static int? ToPositiveIntOrNull(this string s) {
      if (String.IsNullOrEmpty(s)) return null;
      int i;
      if (!int.TryParse(s, out i) || i <= 0) return null;
      return i;
    }

    public static int ToPositiveIntOrZero(this string s) {
      int? i = ToPositiveIntOrNull(s);
      return i.GetValueOrDefault(0);
    }

    public static int ToIntOrMin(this string obj, int MinValue) { // Return integer no less than MinValue
      return Math.Max(ToIntOrDefault(obj, int.MinValue), MinValue);
    }

    public static int ToIntOrMax(this string obj, int MaxValue) { // Return integer no greater than MaxValue
      return Math.Min(ToIntOrDefault(obj, int.MaxValue), MaxValue);
    }

    public static bool ToBooleanOrDefault(this string s, bool defaultValue) {
      return ToBooleanOrDefault((object)s, defaultValue);
    }

    public static Guid ToGuidOrEmpty(this string s) {
      return s.ToGuidOrDefault(Guid.Empty).Value;
    }

    public static Guid? ToGuidOrNull(this string s) {
      return s.ToGuidOrDefault(null);
    }

    public static Guid? ToGuidOrDefault(this string s, Guid? defaultValue) {
      if (s != null && Guid.TryParse(s, out Guid result)) return result;
      return defaultValue;
    }

    public static string NoHTML(this string source) {
      if (source.IsNullOrEmpty()) return source;
      string result = source;
      result = Regex.Replace(result, "</?(?:p|ul|ol|li|div)\b[^>]*>", "\n", RegexOptions.IgnoreCase); // New line for block elements
      result = Regex.Replace(result, "</?[^>]*>", string.Empty); // Remove all html blocks.
      result = Regex.Replace(result, "\n{2,}", "\n");
      result = result.Replace("&nbsp;", " ");
      return result;
    }

    public static string NoSQL(this string obj) {
      if (obj == null) return null;
      string adj = obj;
      do {
        obj = adj;
        adj = Regex.Replace(obj, "--|sp_|';|_", string.Empty);
      } while (adj != obj);
      return adj;
    }

    public static string PlainText(this string obj, string allowCharacters) {
      if (obj == null) return null;
      return obj.NoHTML().NoSQL();
    }
    public static string PlainText(this string obj) {
      return obj.PlainText(string.Empty);
    }

    public static string EmptyIfNull(this string obj) {
      return obj == null ? string.Empty : obj;
    }

    public static bool IsEmailAddress(this string s) {
      // A very basic check to see if the format is right, i.e. "not-@" followed by "@" followed by anything with at least 1 dot.
      if (Regex.IsMatch(s, "[\r\n\t]")) return false;
      return Regex.IsMatch(s, @"^[^@]+@[^\.]+(\.[^.]+)+$", RegexOptions.Singleline);
    }

    public static string TrimWhitespace(this string obj) {
      return String.IsNullOrEmpty(obj) ? obj : Regex.Replace(obj.EmptyIfNull(), "^\\s+|\\s+$", string.Empty);
    }

    public static string WrapIfNotNullOrEmpty(this string obj, string before, string after) {
      return String.IsNullOrEmpty(obj) ? obj : Wrap(obj, before, after);
    }

    public static string Wrap(this string obj, string before, string after) {
      if (obj == null && before == null && after == null) return null;
      return before.EmptyIfNull() + obj.EmptyIfNull() + after.EmptyIfNull();
    }

    public static string ToPlural(this string str, int amount) {
      if (str.IsNullOrEmpty() || amount == 1) return str;
      if (str.EndsWith("y", StringComparison.OrdinalIgnoreCase) && !str.EndsWith("survey", StringComparison.OrdinalIgnoreCase)) {
        return str.Substring(0, str.Length - 1) + "ies";
      } else {
        return str + "s";
      }
    }

    public static string AppendWithSeparator(this string s, string separator, string toAppend) {
      // Only include separator if both strings have content.
      if (toAppend.IsNullOrEmpty()) return s;
      if (s.IsNullOrEmpty()) return toAppend;
      return s + separator.EmptyIfNull() + toAppend;
    }

    public static string SurroundWith(this string s, string surroundWith, bool evenIfEmptyOrNull = false) {
      return SurroundWith(s, surroundWith, surroundWith, evenIfEmptyOrNull);
    }
    public static string SurroundWith(this string s, string onLeft, string onRight, bool evenIfEmptyOrNull = false) {
      if (s.IsNullOrEmpty() && !evenIfEmptyOrNull) return s;
      if (s == null && onLeft == null && onRight == null) return null;
      return $"{onLeft.EmptyIfNull()}{s.EmptyIfNull()}{onRight.EmptyIfNull()}";
    }

    public static string URLEncode(this string s) {
      if (s.IsNullOrEmpty()) return string.Empty;
      else return SystemWeb.UrlEncode(s); // Note this DOES changes spaces to "+" which is good.
    }

    public static string URLDecode(this string s) {
      if (s.IsNullOrEmpty()) return string.Empty;
      else return SystemWeb.UrlDecode(s);
    }

    public static string HTMLEncode(this string s) {
      if (s.IsNullOrEmpty()) return string.Empty;
      else return SystemWeb.HtmlEncode(s);
    }

    public static string JavaScriptEncode(this string s) {
      if (s.IsNullOrEmpty()) return string.Empty;
      else return SystemWeb.JavaScriptStringEncode(s);
    }

    // Keep improving this function to make HTML output "safe",
    // e.g. remove tags like <script>, <link>, <meta> etc.
    // This is very quick and basic at the moment, needs improvement.
    public static string SafeHTML(this string html) {
      if (html.IsNullOrEmpty()) return string.Empty;
      return RegexReplace(html, @"<\s*/?\s*(?:script|link|meta)[^>]*>", string.Empty);
    }

    public static string HTMLEncodeIfRequired(this string obj) {
      if (obj.IsNullOrEmpty()) return string.Empty;
      for (var i = 0; i < obj.Length; i++) {
        var c = obj.Substring(i, 1);
        if (c == "<" || c == ">" || c == "\"" || (int)obj[i] >= 128) return SystemWeb.HtmlEncode(obj);
      }
      return obj;
    }

    public static string EnsureStartsWith(this string value, string startWith, Ensure ensure) {
      return value.EnsureStartsWith(startWith, ensure == Ensure.IfNotBlank);
    }

    public static string EnsureStartsWith(this string value, string startWith, bool onlyIfNotEmptyOrWhitespace = false) {
      if (value.IsNullOrEmptyOrWhitespace() && onlyIfNotEmptyOrWhitespace) return value;
      if (startWith == " ") { // space is special case
        if (value.StartsWith(" ")) return value;
      } else if (value.TrimStart().StartsWith(startWith.TrimStart(), StringComparison.InvariantCultureIgnoreCase)) return value; // already present
      return startWith + value;
    }

    public static string EnsureEndsWith(this string value, string startWith, Ensure ensure) {
      return value.EnsureEndsWith(startWith, ensure == Ensure.IfNotBlank);
    }

    public static string EnsureEndsWith(this string value, string endWith, bool onlyIfNotEmptyOrWhitespace = false) {
      if (value.IsNullOrEmptyOrWhitespace() && onlyIfNotEmptyOrWhitespace) return value;
      if (endWith == " ") { // space is special case
        if (value.EndsWith(" ")) return value;
      } else if (value.TrimEnd().EndsWith(endWith.TrimEnd(), StringComparison.InvariantCultureIgnoreCase)) return value; // already present
      return value + endWith;
    }

    public static string AddPosessive(this string value) {
      // Adds "'s" or "'" on end as appropriate, as in a possessive noun. e.g. Andrew = Andrew's, Chris = Chris'
      if (value.IsNullOrEmpty()) return "'s";
      if (value.ToLower().EndsWith("s")) return value + "'";
      else return value + "'s";
    }

    public static DateTime? ToDateTimeOrNull(this string dateText) {
      if (dateText.IsNullOrEmpty()) return null;
      DateTime dt;
      if (DateTime.TryParse(dateText, out dt)) return dt;
      else return null;
    }

    public static DateTimeOffset? ToDateTimeOffsetOrNull(this string dateText) {
      if (dateText.IsNullOrEmpty()) return null;
      DateTimeOffset dto;
      if (DateTimeOffset.TryParse(dateText, out dto)) return dto;
      else return null;
    }

    public static string UCaseFirstChar(this string source) {
      if (source.IsNullOrEmpty()) return source;
      return source.Substring(0, 1).ToUpper() + (source.Length == 1 ? string.Empty : source.Substring(1));
    }

    public static string RegexReplace(this string source, string pattern, string replacement, RegexOptions regexOptions = RegexOptions.None) {
      return Regex.Replace(source.EmptyIfNull(), pattern.EmptyIfNull(), replacement.EmptyIfNull(), regexOptions);
    }
    public static Match RegexMatch(this string source, string pattern, RegexOptions regexOptions = RegexOptions.None) {
      return Regex.Match(source.EmptyIfNull(), pattern.EmptyIfNull(), regexOptions);
    }
    public static string RegexMatchStringOrNull(this string source, string pattern, RegexOptions regexOptions = RegexOptions.None) {
      var match = Regex.Match(source.EmptyIfNull(), pattern.EmptyIfNull(), regexOptions);
      if (match.Success) return match.Value;
      return null;
    }
    public static string RegexFirstGroupOrNull(this string source, string pattern, RegexOptions regexOptions = RegexOptions.None) {
      var m = Regex.Match(source.EmptyIfNull(), pattern.EmptyIfNull(), regexOptions);
      if (m != null && m.Groups.Count > 1) return m.Groups[1].Value;
      return null;
    }
    public static bool RegexIsMatch(this string source, string pattern, RegexOptions regexOptions = RegexOptions.None) {
      if (source == null || pattern.IsNullOrEmpty()) return false;
      return Regex.IsMatch(source, pattern.EmptyIfNull(), regexOptions);
    }

    public static string ReplaceTags(this string source, Dictionary<string, string> replacements) {
      return ReplaceTags(source.EmptyIfNull(), REPLACE_TAG_START, REPLACE_TAG_END, replacements);
    }
    public static string ReplaceTags(this string source, string tagStart, string tagEnd, Dictionary<string, string> replacements) {

      if (source.IsNullOrEmpty() || replacements == null || replacements.Count == 0) return source;
      if (tagStart == null) tagStart = REPLACE_TAG_START;
      if (tagEnd == null) tagEnd = REPLACE_TAG_END;

      var result = new StringBuilder();

      int startIndex = source.IndexOf(tagStart);
      int sourceIndex = 0;
      while (startIndex >= 0) {
        int tagNameIndex, tagNameEndIndex;
        string tagNameOriginal = string.Empty;
        // Get content between start and end tags, but repeat if any nested found (i.e. "[one[two]..." - just get "[two]")
        while (true) {
          tagNameIndex = startIndex + tagStart.Length; // first character of tag name.
          tagNameEndIndex = source.IndexOf(tagEnd, tagNameIndex);
          if (tagNameEndIndex == -1) break; // Can't do any more.
          tagNameOriginal = source.Substring(tagNameIndex, tagNameEndIndex - tagNameIndex);
          int nestedTagIndex = tagNameOriginal.IndexOf(tagStart);
          if (nestedTagIndex == -1) break; // No nested tag, all ok.
          // Repeat from start of the nested tag.
          startIndex += nestedTagIndex + 1;
        }
        if (tagNameEndIndex == -1) break; // Can't do any more.
        string tagNameLcase = tagNameOriginal.ToLower();
        tagNameEndIndex += tagEnd.Length; // character after tagEnd.
        // replace whichever tag this is.
        string replaceWith = tagStart + tagNameOriginal + tagEnd; // i.e. don't change it if it's not found.
        bool tagFound = false;
        foreach (var item in replacements) {
          if (item.Key.IndexOf("|") > 0) { // Multiple tags can be specified to replace with the same value.
            foreach (string splitKey in item.Key.Split('|')) {
              if (tagNameLcase == splitKey.ToLower()) {
                replaceWith = item.Value;
                tagFound = true;
                break;
              }
            }
          } else {
            if (tagNameLcase == item.Key.ToLower()) {
              replaceWith = item.Value;
              tagFound = true;
            }
          }
          if (tagFound) break;
        }
        result.Append(source.Substring(sourceIndex, startIndex - sourceIndex)); // source up to start of tag.
        result.Append(replaceWith);
        sourceIndex = tagNameEndIndex;
        startIndex = source.IndexOf(tagStart, tagNameEndIndex);
      }
      // Append remainder of string to result.
      if (sourceIndex < source.Length) result.Append(source.Substring(sourceIndex));

      return result.ToString();
    }

    // StringBuilder
    public static StringBuilder AppendWithSeparator(this StringBuilder sb, string toAppend, string leadingSeparator = " ") {
      if (sb != null) {
        if (sb.Length > 0) sb.Append(" ");
        sb.Append(toAppend);
      }
      return sb;
    }
  }
}
