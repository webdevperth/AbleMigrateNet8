using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public static partial class DbExtensions {

    public enum IfKeyExists { UpdateValue, Skip, Throw }

    public static void Add(this SqlParameterCollection parameters, params SqlParameter[] addParams) {
      if (parameters == null || addParams.IsNullOrEmpty()) return;
      parameters.AddRange(addParams);
    }

    public static void Add(this List<SqlParameter> paramList, IfKeyExists ifKeyExists, List<SqlParameter> addParams, params SqlParameter[] moreParams) {

      if (paramList == null) throw new ArgumentException($"{nameof(paramList)} is null.");
      if (addParams.IsNullOrEmpty() && moreParams.IsNullOrEmpty()) return;

      var paramListKeys = new Dictionary<string, SqlParameter>(StringComparer.OrdinalIgnoreCase);
      foreach (var pl in paramList) {
        if (pl == null) continue;
        paramListKeys.Add(pl.ParameterName, pl);
      }

      if (addParams != null) AddParams(paramList, paramListKeys, ifKeyExists, addParams);
      if (moreParams != null) AddParams(paramList, paramListKeys, ifKeyExists, moreParams);
    }

    private static void AddParams(List<SqlParameter> paramList, Dictionary<string, SqlParameter> paramListKeys, IfKeyExists ifKeyExists, ICollection<SqlParameter> addParams) {

      foreach (var ap in addParams) {
        if (ap == null) continue;
        bool keyExists = paramListKeys.TryGetValue(ap.ParameterName, out var existingParam);
        if (keyExists) {
          if (ifKeyExists == IfKeyExists.Throw) {
            throw new InvalidOperationException($"Duplicate key: {ap.ParameterName}");
          } else if (ifKeyExists == IfKeyExists.UpdateValue) {
            existingParam.Value = ap.Value;
          }
        } else {
          paramList.Add(ap);
          paramListKeys.Add(ap.ParameterName, ap);
        }
      }
    }

    public static bool ContainsParamName(this SqlParameter[] paramList, string paramName) {
      if (paramList == null || paramList.Length == 0 || paramName.IsNullOrEmpty()) return false;
      paramName = paramName.Replace("@", "");
      foreach (var param in paramList) {
        if (param == null) continue;
        if (param.ParameterName.Replace("@", "").Equals(paramName, StringComparison.OrdinalIgnoreCase)) {
          return true;
        }
      }
      return false;
    }

    public static SqlParameter AddVarChar(this SqlParameterCollection parameters, string paramName, int maxLength, string value) {
      if (!CheckParamName(ref paramName)) return null;
      if (maxLength < 1 || maxLength > 8000) maxLength = 8000;
      var param = parameters.Add(paramName, SqlDbType.VarChar, maxLength);
      if (value == null) param.Value = DBNull.Value;
      else param.Value = value.Length <= maxLength ? value : value.Substring(0, maxLength);
      return param;
    }

    public static SqlParameter AddVarCharMax(this SqlParameterCollection parameters, string paramName, string value) {
      if (!CheckParamName(ref paramName)) return null;
      var param = parameters.Add(paramName, SqlDbType.VarChar);
      if (value == null) param.Value = DBNull.Value;
      else param.Value = value;
      return param;
    }

    public static SqlParameter AddText(this SqlParameterCollection parameters, string ParamName, string value) {
      if (!CheckParamName(ref ParamName)) return null;
      var param = parameters.Add(ParamName, SqlDbType.Text);
      param.Value = value.OrDBNull();
      return param;
    }

    public static SqlParameter AddNText(this SqlParameterCollection parameters, string ParamName, string value) {
      if (!CheckParamName(ref ParamName)) return null;
      var param = parameters.Add(ParamName, SqlDbType.NText);
      param.Value = value.OrDBNull();
      return param;
    }

    public static SqlParameter AddInt(this SqlParameterCollection parameters, string paramName, int? value, IfKeyExists ifExistsAction = IfKeyExists.Throw) {
      SqlParameter param;
      if (parameters.Contains(paramName)) {
        param = parameters[paramName];
        if (ifExistsAction == IfKeyExists.UpdateValue) {
          parameters[paramName].Value = value;
        } else if (ifExistsAction == IfKeyExists.Throw) {
          throw new ArgumentException($"paramName '{paramName}' already exists in parameters collection.");
        }
      } else {
        param = DbHelper.Common.NewSqlParameter(paramName, value);
        parameters.Add(param);
      }
      return param;
    }

    public static SqlParameter AddTinyInt(this SqlParameterCollection parameters, string paramName, int? value) {
      var newParam = DbHelper.Common.NewSqlParameter(paramName, value, SqlDbType.TinyInt);
      if (newParam == null) return null;
      return parameters.Add(newParam);
    }

    public static SqlParameter AddTinyIntFromBool(this SqlParameterCollection parameters, string paramName, bool? value) {
      var newParam = DbHelper.Common.NewSqlParameter(paramName, value == null ? null : (int?)((bool)value ? 1 : 0), SqlDbType.TinyInt);
      if (newParam == null) return null;
      return parameters.Add(newParam);
    }

    public static SqlParameter AddDecimal(this SqlParameterCollection parameters, string paramName, decimal? value) {
      var newParam = DbHelper.Common.NewSqlParameter(paramName, value);
      if (newParam == null) return null;
      return parameters.Add(newParam);
    }

    public static SqlParameter AddBit(this SqlParameterCollection parameters, string paramName, bool value) {
      if (!CheckParamName(ref paramName)) return null;
      var param = parameters.Add(paramName, SqlDbType.Bit);
      param.Value = (value ? 1 : 0);
      return param;
    }

    public static SqlParameter AddDate(this SqlParameterCollection parameters, string paramName, DateTime? value) {
      if (!CheckParamName(ref paramName)) return null;
      var param = parameters.Add(paramName, SqlDbType.Date);
      if (value == null) param.Value = DBNull.Value;
      else param.Value = ((DateTime)value).Date;
      return param;
    }

    public static SqlParameter AddDateTime(this SqlParameterCollection parameters, string paramName, DateTime value) {
      if (!CheckParamName(ref paramName)) return null;
      var param = parameters.Add(paramName, SqlDbType.DateTime);
      param.Value = value.OrDBNull();
      return param;
    }
    public static SqlParameter AddDateTime(this SqlParameterCollection parameters, string paramName, DateTime? value) {
      if (!CheckParamName(ref paramName)) return null;
      var param = parameters.Add(paramName, SqlDbType.DateTime);
      param.Value = value.OrDBNull();
      return param;
    }

    public static SqlParameter AddDateTimeOffset(this SqlParameterCollection parameters, string paramName, DateTimeOffset? value) {
      if (!CheckParamName(ref paramName)) return null;
      var param = parameters.Add(paramName, SqlDbType.DateTimeOffset);
      param.Value = value.OrDBNull();
      return param;
    }

    public static SqlParameter AddGuid(this SqlParameterCollection parameters, string paramName, Guid? guid) {
      if (!CheckParamName(ref paramName)) return null;
      var param = parameters.Add(paramName, SqlDbType.UniqueIdentifier);
      param.Value = guid.OrDBNull();
      return param;
    }

    private static bool CheckParamName(ref string paramName) {
      if (String.IsNullOrEmpty(paramName)) return false;
      if (paramName.Substring(0, 1) != "@") paramName = paramName.Insert(0, "@");
      return true;
    }

    public static string ParameterValueForSQL(this SqlParameter param) {
      object paramValue = param.Value; //assuming param isn't null

      if (paramValue == null) //TODO: should probably use DBNull.Value instead or in combination with this
        return "NULL"; //TODO: naive code, won't work as is, need to replace later on = NULL with IS NULL at non-Update queries

      switch (param.SqlDbType) {
        case SqlDbType.Char:
        case SqlDbType.NChar:
        case SqlDbType.NText:
        case SqlDbType.NVarChar:
        case SqlDbType.Text:
        case SqlDbType.Time:
        case SqlDbType.VarChar:
        case SqlDbType.Xml:
        case SqlDbType.Date:
        case SqlDbType.DateTime:
        case SqlDbType.DateTime2:
        case SqlDbType.DateTimeOffset:
          return $"'{paramValue.ToString().Replace("'", "''")}'";

        case SqlDbType.Bit:
          return (paramValue.ToBooleanOrDefault(false)) ? "1" : "0";

        case SqlDbType.Structured:
          var sb = new System.Text.StringBuilder();
          var dt = (DataTable)paramValue;

          sb.Append("declare ").Append(param.ParameterName).Append(" ").AppendLine(param.TypeName);

          foreach (DataRow dr in dt.Rows) {
            sb.Append("insert ").Append(param.ParameterName).Append(" values (");

            for (int colIndex = 0; colIndex < dt.Columns.Count; colIndex++) {
              switch (Type.GetTypeCode(dr[colIndex].GetType())) {
                case TypeCode.Boolean:
                  sb.Append(Convert.ToInt32(dr[colIndex]));
                  break;

                case TypeCode.String:
                  sb.Append("'").Append(dr[colIndex]).Append("'");
                  break;

                case TypeCode.DateTime:
                  sb.Append("'").Append(Convert.ToDateTime(dr[colIndex]).ToString("yyyy-MM-dd HH:mm")).Append("'");
                  break;

                default:
                  sb.Append(dr[colIndex]); break;
              }

              sb.Append(", ");
            }

            sb.Length -= 2; // trailing ', '
            sb.AppendLine(")");
          }

          return sb.ToString();

        case SqlDbType.Decimal:
        case SqlDbType.Float:
          return ((double)paramValue).ToString(System.Globalization.CultureInfo.InvariantCulture).Replace("'", "''");

        default:
          return paramValue.ToString().Replace("'", "''");
      }
    }
  }
}
