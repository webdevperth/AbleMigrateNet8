using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Data.SqlTypes;

namespace Integral.Web {

  public static partial class DbExtensions {

    // Datareader Extensions (http://dpatrickcaldwell.blogspot.com.au/2009/10/strongly-typed-getvalue-extension.html)

    public static T GetValue<T>(this SqlDataReader reader, string name) {
      try {
        return (T)Convert.ChangeType(reader[name], typeof(T));
      } catch (IndexOutOfRangeException) {
        throw new IndexOutOfRangeException("Column '" + name + "' not found.");
      }
    }

    public static bool GetBoolFromBit(this SqlDataReader reader, string name) {
      bool val;
      if (reader.IsDBNull(name))
        throw new SqlNullValueException("Column '" + name + "' is dbnull");
      else if (!bool.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' is not Boolean");
      else
        return val;
    }
    public static bool GetBoolFromBit(this SqlDataReader reader, string name, bool DefaultIfNull) {
      bool val;
      if (reader.IsDBNull(name))
        return DefaultIfNull;
      else if (!bool.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' is not Boolean");
      else
        return val;
    }

    public static bool GetBoolFromInt(this SqlDataReader reader, string name, bool DefaultIfNull = false) {
      int val;
      if (reader.IsDBNull(name))
        return DefaultIfNull;
      else if (!int.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' is not integer");
      else if (val == 0)
        return false;
      else
        return true;
    }

    public static bool? GetBoolFromIntOrNull(this SqlDataReader reader, string name, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      if (reader.IsDBNull(name))
        return null;
      else
        return GetBoolFromInt(reader, name);
    }

    public static long GetLong(this SqlDataReader reader, string name) {
      long val;
      if (reader.IsDBNull(name))
        throw new SqlNullValueException("Column '" + name + "' is dbnull");
      else if (!long.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' is not Long");
      else
        return val;
    }

    public static int GetInt(this SqlDataReader reader, string name) {
      int val;
      if (reader.IsDBNull(name))
        throw new SqlNullValueException("Column '" + name + "' is dbnull");
      else if (!int.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' is not Int");
      else
        return val;
    }
    public static int GetInt(this SqlDataReader reader, string name, int DefaultIfNull) {
      int val;
      if (reader.IsDBNull(name))
        return DefaultIfNull;
      else if (!int.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' is not Int");
      else
        return val;
    }
    public static int? GetIntOrNull(this SqlDataReader reader, string name, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      if (reader.IsDBNull(name)) return null;
      return GetInt(reader, name);
    }
    public static int GetIntOrDefault(this SqlDataReader reader, string name, int defaultValue, bool defaultIfNoColName = false) {
      if (name.IsNullOrEmpty() && defaultIfNoColName) return defaultValue;
      if (reader.IsDBNull(name)) return defaultValue;
      return GetInt(reader, name);
    }

    public static List<int> GetIntList(this SqlDataReader reader, string name, bool emptyIfNoColName = false, char delimiter = ',') {
      if (AllowedNullColName(name, emptyIfNoColName) || reader.IsDBNull(name)) return new List<int>();
      return GetString(reader, name).ToIntList(delimiter);
    }

    public static decimal GetDecimal(this SqlDataReader reader, string name, decimal DefaultIfNull = 0) {
      decimal val;
      if (reader.IsDBNull(name))
        return DefaultIfNull;
      else if (!decimal.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' cannot be converted to decimal.");
      else
        return val;
    }
    public static decimal? GetDecimalOrNull(this SqlDataReader reader, string name, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      if (reader.IsDBNull(name)) return null;
      return GetDecimal(reader, name, 0);
    }
    public static decimal GetDecimalOrDefault(this SqlDataReader reader, string name, decimal defaultValue) {
      if (reader.IsDBNull(name)) return defaultValue;
      return GetDecimal(reader, name, 0);
    }

    public static double GetDouble(this SqlDataReader reader, string name, double DefaultIfNull = 0) {
      double val;
      if (reader.IsDBNull(name))
        return DefaultIfNull;
      else if (!double.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' cannot be converted to double.");
      else
        return val;
    }
    public static double? GetDoubleOrNull(this SqlDataReader reader, string name, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      if (reader.IsDBNull(name)) return null;
      return GetDouble(reader, name, 0);
    }

    public static float GetFloat(this SqlDataReader reader, string name, float DefaultIfNull = 0) {
      float val;
      if (reader.IsDBNull(name))
        return DefaultIfNull;
      else if (!float.TryParse(reader[name].ToString(), out val))
        throw new SqlNullValueException("Column '" + name + "' cannot be converted to float.");
      else
        return val;
    }
    public static float? GetFloatOrNull(this SqlDataReader reader, string name, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      if (reader.IsDBNull(name)) return null;
      return GetFloat(reader, name, 0);
    }

    public static string GetString(this SqlDataReader reader, string name, bool nullIfNoColName) {
      return reader.GetString(name, null, nullIfNoColName);
    }
    public static string GetString(this SqlDataReader reader, string name, string DefaultIfDbNull = null, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      int ordinal;
      ordinal = reader.GetOrdinal(name);
      if (reader.IsDBNull(name)) return DefaultIfDbNull;
      else return reader[name].ToString();
    }

    public static Guid GetGuid(this SqlDataReader reader, string name) {
      if (reader.IsDBNull(name)) throw new IndexOutOfRangeException("Column '" + name + "' cannot be null.");
      return reader.GetGuid(reader.GetOrdinal(name));
    }

    public static Guid? GetGuidOrNull(this SqlDataReader reader, string name, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      if (reader.IsDBNull(name)) return null;
      return reader.GetGuid(reader.GetOrdinal(name));
    }

    public static DateTime GetDateTime(this SqlDataReader reader, string name) {
      try {
        var dt = reader.GetDateTime(reader.GetOrdinal(name));
        // if (name.ToLower().EndsWith("utc")) dt = dt.SpecifyKind(DateTimeKind.Utc); TODO make sure this won't cause problems.
        return dt;
      } catch (IndexOutOfRangeException) {
        throw new IndexOutOfRangeException("Column '" + name + "' not found.");
      } catch (SqlNullValueException) {
        throw new IndexOutOfRangeException("Column '" + name + "' is dbnull.");
      }
    }
    public static DateTime? GetDateTimeOrNull(this SqlDataReader reader, string name, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      if (reader.IsDBNull(name)) return null;
      return GetDateTime(reader, name);
    }
    public static DateTime GetDateTimeOrMin(this SqlDataReader reader, string name) {
      if (reader.IsDBNull(name)) return DateTime.MinValue;
      return GetDateTime(reader, name);
    }

    public static DateTimeOffset GetDateTimeOffset(this SqlDataReader reader, string name) {
      try {
        var dt = reader.GetDateTimeOffset(reader.GetOrdinal(name));
        return dt;
      } catch (IndexOutOfRangeException) {
        throw new IndexOutOfRangeException("Column '" + name + "' not found.");
      } catch (SqlNullValueException) {
        throw new IndexOutOfRangeException("Column '" + name + "' is dbnull.");
      }
    }
    public static DateTimeOffset? GetDateTimeOffsetOrNull(this SqlDataReader reader, string name, bool nullIfNoColName = false) {
      if (AllowedNullColName(name, nullIfNoColName)) return null;
      if (reader.IsDBNull(name)) return null;
      return GetDateTimeOffset(reader, name);
    }

    public static bool IsDBNull(this SqlDataReader reader, string name) {
      return reader.IsDBNull(reader.GetOrdinal(name));
    }

    private static bool AllowedNullColName(string colName, bool nullIfNoColName) {
      if (colName.IsNullOrEmpty()) {
        if (nullIfNoColName) return true;
        throw new ArgumentException("Column name cannot be null.");
      }
      return false;
    }

  }
}
