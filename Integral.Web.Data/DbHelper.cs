using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Text;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    private const int SqlErrorNumber_DuplicateKey = 2627;

    public enum SqlErrorEnum {
      None = 0,
      DuplicateKey = 2601,
      Other = 9999
    }

    public enum SortDirection {
      ASC = 1,
      DESC = 2
    }

    private enum SQL_ANDOR { AND, OR }

    // Some columns refer to external service resources in text sandboxes which are different from the live resource IDs.
    // An example is Stripe customer IDs and product IDs - those IDs are different in the Stripe sandbox than in the Stripe live account.
    // Therefore the db requires 2 values for some of those resource IDs - one for test and one for production.
    // So when NOT running on prod, the column with the suffix of "_Test" is used.
    private static readonly string TestColumnSuffix = ConfigHelper.IsLiveServer ? string.Empty : "_Test"; // Only add the suffix if not on prod.

    // Returns "[AND/OR] [ColumnName] IN ([List])" provided neither are blank.
    private static string SQL_IN(SQL_ANDOR andOr, string columnName, List<int> intValues) {

      if (intValues.IsNullOrEmpty() || columnName.IsNullOrEmpty()) return string.Empty;

      var joinedString = string.Join(",", intValues);

      return $"{andOr} {columnName} IN ({joinedString})";
    }

    private static string SQL_IN(SQL_ANDOR andOr, string columnName, List<string> stringValues) {

      if (stringValues.IsNullOrEmpty() || columnName.IsNullOrEmpty()) return string.Empty;

      string joinedString = string.Empty;

      if (stringValues.Count > 20) {
        // Many items, use stringbuilder.
        var joinedStringSB = new StringBuilder();
        foreach (string s in stringValues) {
          if (joinedStringSB.Length > 0) joinedStringSB.Append(",");
          joinedStringSB.Append($"'{s.Replace("'", "''")}'");
        }
        joinedString = joinedStringSB.ToString();
      } else {
        // Few items, just use string.
        foreach (string s in stringValues) {
          if (joinedString.Length > 0) joinedString += ",";
          joinedString += $"'{s.Replace("'", "''")}'";
        }
      }
      return $"{andOr} {columnName} IN ({joinedString})";
    }

    public static bool IsDuplicateKeyError(Exception ex) {
      return GetSqlError(ex) == SqlErrorEnum.DuplicateKey;
    }

    public static SqlErrorEnum GetSqlError(Exception ex) {
      // Returns an emum for an sql error, or None if invalid or Other if not covered in SqlErrorEnum.
      if (ex != null && ex is SqlException) {
        int errorNumber = ((SqlException)ex).Number;
        if (errorNumber == SqlErrorNumber_DuplicateKey) return SqlErrorEnum.DuplicateKey;
        if (Enum.IsDefined(typeof(SqlErrorEnum), errorNumber)) return (SqlErrorEnum)errorNumber;
        return SqlErrorEnum.Other;
      } else {
        return SqlErrorEnum.None;
      }
    }

    public class DuplicateKeyException : Exception {
      public DuplicateKeyException() { }
      public DuplicateKeyException(string message) : base(message) { }
      public DuplicateKeyException(string message, Exception inner) : base(message, inner) { }
    }

    public class InfoListPaged {
      public int? OffsetRows { get; internal set; }
      public int? FetchRows { get; internal set; }
      public int TotalRows { get; internal set; }
      public string SqlText { get; internal set; }
    }

    public class InfoListPaged<itemType> : InfoListPaged {

      public List<itemType> InfoList { get; internal set; }

      public InfoListPaged<toItemType> CustomCopy<toItemType>(Func<itemType, toItemType> GetItemToAdd) {
        var result = new InfoListPaged<toItemType>(); // Resulting InfoListPaged object with the new item type.
        // Copy paging info.
        result.OffsetRows = OffsetRows;
        result.FetchRows = FetchRows;
        result.TotalRows = TotalRows;
        // Add new items using callback.
        foreach (var originalItem in InfoList) {
          result.InfoList.Add(GetItemToAdd(originalItem));
        }
        return result;
      }

      public InfoListPaged() {
        OffsetRows = null;
        FetchRows = null;
        TotalRows = 0;
        InfoList = new List<itemType>();
      }

      public void ShallowCopyTo(InfoListPaged tot) {
        tot.OffsetRows = this.OffsetRows;
        tot.FetchRows = this.FetchRows;
        tot.TotalRows = this.TotalRows;
      }

      public void ShallowCopyTo(InfoListPaged tot, Action<itemType> copyItem) {
        ShallowCopyTo(tot);
        foreach (var item in this.InfoList) {
          copyItem(item);
        }
      }
    }

    public class SqlParam {

      SqlParameter _p = new SqlParameter() { IsNullable = true, Value = DBNull.Value };
      internal SqlParameter P => _p;

      public SqlParam(string key, int? value) { _p.ParameterName = key; _p.Value = value.OrDBNull(); _p.SqlDbType = SqlDbType.Int; }
      public SqlParam(string key, decimal? value, byte precision, byte scale) { _p.ParameterName = key; _p.Value = value.OrDBNull(); _p.SqlDbType = SqlDbType.Decimal; _p.Precision = precision; _p.Scale = scale; }
      public SqlParam(string key, string value) {
        _p.ParameterName = key; _p.SqlDbType = SqlDbType.VarChar; _p.Size = 8000;
        if (value != null) _p.Value = value.LimitLengthTo(8000);
      }
      public SqlParam(string key, int maxLength, string value) {
        _p.ParameterName = key; _p.SqlDbType = SqlDbType.VarChar; _p.Size = Math.Min(maxLength, 8000);
        if (value != null) _p.Value = value.LimitLengthTo(Math.Min(maxLength, 8000));
      }
      public SqlParam(string key, bool? value) { _p.ParameterName = key; _p.SqlDbType = SqlDbType.TinyInt; if (value != null) _p.Value = (bool)value ? 1 : 0; }
      public SqlParam(string key, DateTime? value) { _p.ParameterName = key; _p.SqlDbType = SqlDbType.DateTime2; _p.Value = value.OrDBNull(); }
    }

    public class SqlParamList : List<SqlParameter> {

      public SqlParamList(params SqlParam[] sqlParams) {
        if (sqlParams != null && sqlParams.Length > 0) {
          foreach (var sqlParam in sqlParams) {
            if (!sqlParam.P.ParameterName.IsNullOrEmpty()) this.Add(sqlParam.P);
          }
        }
      }
    }

    public class DbColumn {

      public string Name { get; private set; }
      public SqlDbType Type { get; private set; }
      public int? Size { get; private set; }
      private static Dictionary<string, DbColumn> columnList = new Dictionary<string, DbColumn>();
      private DbColumn(string columnName, SqlDbType type) {
        Init(columnName, type, null);
      }
      private DbColumn(string columnName, SqlDbType type, int size) {
        Init(columnName, type, size);
      }
      private void Init(string columnName, SqlDbType type, int? size) {
        this.Name = columnName;
        this.Type = type;
        this.Size = size;
        columnList.Add(columnName, this);
      }
    }

    public class Common {

      #region "Obsolete functions"

      [Obsolete("Instead use the functions that accept Action<SqlDataReader>")]
      public static SqlConnection GetNewConnection() {
        return new SqlConnection(ConfigHelper.IntegralDbConnectionString);
      }

      [Obsolete("Instead use the functions that accept Action<SqlDataReader>")]
      public static SqlConnection GetNewOpenConnection() {

        var conn = GetNewConnection();
        conn.Open();
        SetRollbackOnErrors(conn);

        return conn;
      }

      [Obsolete("Instead use the functions that accept Action<SqlDataReader>")]
      public static SqlConnection GetNewIntegralConnection() {
        return GetNewConnection();
      }

      [Obsolete("Instead use the functions that accept Action<SqlDataReader>")]
      public static SqlConnection GetNewOpenIntegralConnection() {
        return GetNewOpenConnection();
      }

      #endregion

      // Wrap db updates in a transaction.
      // Action must return true to commit or false to rollback.
      public static bool UsingTransaction(Func<SqlTransaction, bool> actions) {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          conn.Open();
          SetRollbackOnErrors(conn);

          using (var trans = conn.BeginTransaction()) {

            bool isCommit = actions(trans);

            if (isCommit) {
              trans.Commit();
            } else if (trans != null && trans.Connection != null) { // Ignore rollback if invalid transaction state.
              trans.Rollback();
            }

            return isCommit;
          }
        }
      }

      // As above, using existing transaction if possible.
      public static bool UsingTransaction(SqlTransaction trans, Func<SqlTransaction, bool> actions) {

        if (trans == null) {
          return UsingTransaction(newTrans => { return actions(newTrans); });
        } else {
          return actions(trans);
        }
      }

      public static void Query(SqlTransaction trans, string sqlQuery, SqlParamList sqlParamList, Action<SqlDataReader> processRowData) {
        Query(trans, sqlQuery, processRowData, sqlParamList.ToArray());
      }

      public static void Query(string sqlQuery, List<SqlParameter> sqlParameters, Action<SqlDataReader> processRowData) {
        Query(null, sqlQuery, processRowData, sqlParameters == null ? null : sqlParameters.ToArray());
      }

      public static void Query(SqlTransaction trans, string sqlQuery, List<SqlParameter> sqlParameters, Action<SqlDataReader> processRowData) {
        Query(trans, sqlQuery, processRowData, sqlParameters == null ? null : sqlParameters.ToArray());
      }

      public static void Query(string sqlQuery, Action<SqlDataReader> processRowData, params SqlParameter[] sqlParameters) {
        Query(null, sqlQuery, processRowData, sqlParameters);
      }

      public static void Query(SqlTransaction trans, string sqlQuery, Action<SqlDataReader> processRowData, params SqlParameter[] sqlParameters) {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sqlQuery, trans != null ? trans.Connection : conn, trans)) {

            cmd.CommandTimeout = 240;

            if (sqlParameters != null) cmd.Parameters.AddRange(sqlParameters);

            Stopwatch stopwatch = null;
            LogHelper.SqlQueryLogInfo queryLogInfo = null;

            if (ConfigHelper.IsDevServer) {
              queryLogInfo = LogHelper.LogLatestSQL(cmd);
              stopwatch = new Stopwatch();
              stopwatch.Start();
            }

            if (trans == null) {
              conn.Open();
              SetRollbackOnErrors(conn);
            }

            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                processRowData(dr);
              }
            }

            if (ConfigHelper.IsDevServer) {
              stopwatch.Stop();
              LogHelper.SetSqlQueryDuration(queryLogInfo, stopwatch.Elapsed);
              stopwatch = null;
            }
          }
        }
      }

      // Ensure rollback occurs on errors.
      private static void SetRollbackOnErrors(SqlConnection conn) {

        // TODO - For SQL efficiency set NOCOUNT ON
        //        Then for all code relying on ADO returning # rows affected,
        //        (e.g. success = ExecuteNonQuery(...) > 0)
        //        change to using "SELECT @@ROWCOUNT" in the query and using ExecuteScalar instead
        //        (e.g. success =  ExecuteScalar(...sql returns @@ROWCOUNT...) & read value from resultset).
        using (var cmd = new SqlCommand("SET XACT_ABORT ON; SET NOCOUNT OFF;", conn)) {
          cmd.ExecuteNonQuery();
        }
      }

      public static object GetScalarQuery(string sqlQuery, params SqlParameter[] sqlParameters) {
        return GetScalarQuery(null, sqlQuery, sqlParameters);
      }

      public static object GetScalarQuery(SqlTransaction trans, string sqlQuery, params SqlParameter[] sqlParameters) {
        // Simple query to return a scalar value.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sqlQuery, trans != null ? trans.Connection : conn, trans)) {

            cmd.CommandTimeout = 240;

            if (sqlParameters != null) cmd.Parameters.AddRange(sqlParameters);

            Stopwatch stopwatch = null;
            LogHelper.SqlQueryLogInfo queryLogInfo = null;

            if (ConfigHelper.IsDevServer) {
              queryLogInfo = LogHelper.LogLatestSQL(cmd);
              stopwatch = new Stopwatch();
              stopwatch.Start();
            }

            if (trans == null) {
              conn.Open();
              SetRollbackOnErrors(conn);
            }

            var result = cmd.ExecuteScalar();

            if (ConfigHelper.IsDevServer) {
              stopwatch.Stop();
              LogHelper.SetSqlQueryDuration(queryLogInfo, stopwatch.Elapsed);
              stopwatch = null;
            }

            return result;
          }
        }
      }

      public static int GetScalarQueryInt(string sqlQuery, params SqlParameter[] sqlParameters) {
        return GetScalarQueryInt(null, sqlQuery, sqlParameters);
      }

      public static int GetScalarQueryInt(SqlTransaction trans, string sqlQuery, params SqlParameter[] sqlParameters) {

        int? result = GetScalarQueryIntOrNull(trans, sqlQuery, sqlParameters);

        if (result == null) throw new Exception("GetScalarQueryInt() returned null. Missing OUTPUT clause?");

        return (int)result;
      }

      public static int? GetScalarQueryIntOrNull(SqlTransaction trans, string sqlQuery, params SqlParameter[] sqlParameters) {
        // Simple query to return a scalar int? value.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sqlQuery, trans != null ? trans.Connection : conn, trans)) {

            cmd.CommandTimeout = 240;

            if (sqlParameters != null) cmd.Parameters.AddRange(sqlParameters);

            Stopwatch stopwatch = null;
            LogHelper.SqlQueryLogInfo queryLogInfo = null;

            if (ConfigHelper.IsDevServer) {
              queryLogInfo = LogHelper.LogLatestSQL(cmd);
              stopwatch = new Stopwatch();
              stopwatch.Start();
            }

            if (trans == null) {
              conn.Open();
              SetRollbackOnErrors(conn);
            }

            var obj = cmd.ExecuteScalar();

            if (ConfigHelper.IsDevServer) {
              stopwatch.Stop();
              LogHelper.SetSqlQueryDuration(queryLogInfo, stopwatch.Elapsed);
              stopwatch = null;
            }

            // Note that obj can be null or int or decimal (e.g. SCOPE_IDENTITY() returns decimal)
            if (obj == null || obj == DBNull.Value) return null;
            if (obj.GetType().Equals(typeof(byte))) return (byte)obj;
            if (obj.GetType().Equals(typeof(short))) return (short)obj;
            if (obj.GetType().Equals(typeof(int))) return (int)obj;
            if (obj.GetType().Equals(typeof(decimal))) return (int)((decimal)obj);

            throw new Exception($"ExecuteScalar() did not return type int, decimal or null ({obj.GetType().ToString()}). Incorrect OUTPUT clause?");
          }
        }
      }

      // Note that GetNonQueryInt() always returns the # rows affected,
      // NOT the result of INSERTED.x or DELETED.x. Use GetScalarQueryInt() for that.
      public static int GetNonQueryInt(string sqlQuery, params SqlParameter[] sqlParameters) {

        return GetNonQueryInt(null, sqlQuery, sqlParameters);
      }

      public static int GetNonQueryInt(SqlTransaction trans, string sqlQuery, params SqlParameter[] sqlParameters) {

        // Simple query to return a scalar int value.
        // Note the connection object is not used/opened if there is a transaction.
        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sqlQuery, trans != null ? trans.Connection : conn, trans)) {

            cmd.CommandTimeout = 240;

            if (sqlParameters != null) cmd.Parameters.AddRange(sqlParameters);

            Stopwatch stopwatch = null;
            LogHelper.SqlQueryLogInfo queryLogInfo = null;

            if (ConfigHelper.IsDevServer) {
              queryLogInfo = LogHelper.LogLatestSQL(cmd);
              stopwatch = new Stopwatch();
              stopwatch.Start();
            }

            if (trans == null) {
              conn.Open();
              SetRollbackOnErrors(conn);
            }

            var result = cmd.ExecuteNonQuery();

            if (ConfigHelper.IsDevServer) {
              stopwatch.Stop();
              LogHelper.SetSqlQueryDuration(queryLogInfo, stopwatch.Elapsed);
              stopwatch = null;
            }

            return result;
          }
        }
      }

      private static bool CheckParamName(ref string paramName) {

        if (string.IsNullOrEmpty(paramName)) return false;
        if (!paramName.StartsWith("@")) paramName = paramName.Insert(0, "@");

        return true;
      }

      private static bool CheckSqlDbTypeForInteger(SqlDbType sqlDbType) {
        return (sqlDbType == SqlDbType.Int || sqlDbType == SqlDbType.BigInt || sqlDbType == SqlDbType.SmallInt || sqlDbType == SqlDbType.TinyInt);
      }

      private static bool CheckSqlDbTypeForString(SqlDbType sqlDbType) {
        return (sqlDbType == SqlDbType.VarChar || sqlDbType == SqlDbType.NVarChar || sqlDbType == SqlDbType.Text || sqlDbType == SqlDbType.NText);
      }

      private static bool CheckSqlDbTypeForDate(SqlDbType sqlDbType) {
        return (sqlDbType == SqlDbType.Date || sqlDbType == SqlDbType.DateTime || sqlDbType == SqlDbType.DateTime2 || sqlDbType == SqlDbType.SmallDateTime);
      }

      private static bool CheckSqlDbTypeForBool(SqlDbType sqlDbType) {
        return (sqlDbType == SqlDbType.TinyInt || sqlDbType == SqlDbType.Bit);
      }

      public static SqlParameter NewSqlParameter(string paramName, string valueVarCharMax, bool isNVarCharMax = false) {
        if (!CheckParamName(ref paramName)) return null;
        var sqlDbType = isNVarCharMax ? SqlDbType.NVarChar : SqlDbType.VarChar;
        var param = new SqlParameter(paramName, sqlDbType, -1); // -1 indicates varchar(MAX)
        param.Value = valueVarCharMax.OrDBNull();
        return param;
      }

      public static SqlParameter NewSqlParameter(string paramName, string value, int maxLength, bool isNVarChar = false) {
        if (!CheckParamName(ref paramName)) return null;
        var sqlDbType = isNVarChar ? SqlDbType.NVarChar : SqlDbType.VarChar;
        var param = new SqlParameter(paramName, sqlDbType, maxLength);
        param.Value = value.LimitLengthTo(maxLength).OrDBNull();
        return param;
      }

      public static SqlParameter NewSqlParameter(string paramName, bool? value, SqlDbType sqlDbType = SqlDbType.TinyInt) {
        if (!CheckParamName(ref paramName)) return null;
        var param = new SqlParameter(paramName, sqlDbType);
        if (sqlDbType == SqlDbType.Bit)
          param.Value = value.OrDBNull();
        else if (sqlDbType == SqlDbType.TinyInt)
          param.Value = value.ToBinaryOrNull().OrDBNull();
        else
          throw new ArgumentException("Invalid sqlDbType for bool: " + sqlDbType.GetType().Name);
        return param;
      }

      public static SqlParameter NewSqlParameter(string paramName, long? value, SqlDbType sqlDbType = SqlDbType.BigInt) {
        if (!CheckParamName(ref paramName)) return null;
        if (!CheckSqlDbTypeForInteger(sqlDbType)) throw new ArgumentException("Invalid sqlDbType for integer: " + sqlDbType.GetType().Name);
        var param = new SqlParameter(paramName, sqlDbType);
        param.Value = value.OrDBNull();
        return param;
      }

      public static SqlParameter NewSqlParameter(string paramName, int? value, SqlDbType sqlDbType = SqlDbType.Int) {
        if (!CheckParamName(ref paramName)) return null;
        if (!CheckSqlDbTypeForInteger(sqlDbType)) throw new ArgumentException("Invalid sqlDbType for integer: " + sqlDbType.GetType().Name);
        var param = new SqlParameter(paramName, sqlDbType);
        param.Value = value.OrDBNull();
        return param;
      }

      public static SqlParameter NewSqlParameter(string paramName, decimal? value) {
        if (!CheckParamName(ref paramName)) return null;
        var param = new SqlParameter(paramName, SqlDbType.Decimal);
        param.Value = value.OrDBNull();
        return param;
      }

      public static SqlParameter NewSqlParameter(string paramName, Guid? value) {
        if (!CheckParamName(ref paramName)) return null;
        var param = new SqlParameter(paramName, SqlDbType.UniqueIdentifier);
        param.Value = value.OrDBNull();
        return param;
      }

      public static SqlParameter NewSqlParameter(string paramName, DateTime? value, SqlDbType sqlDbType = SqlDbType.DateTime2) {
        if (!CheckParamName(ref paramName)) return null;
        if (!CheckSqlDbTypeForDate(sqlDbType)) throw new ArgumentException("Invalid sqlDbType for datetime: " + sqlDbType.GetType().Name);
        var param = new SqlParameter(paramName, sqlDbType);
        param.Value = value.OrDBNull();
        return param;
      }

      public static List<SqlParameter> NewSqlParameterList(params SqlParameter[] sqlParameters) {
        var sqlParamList = new List<SqlParameter>();
        sqlParamList.AddRange(sqlParameters);
        return sqlParamList;
      }

      public static int UpdateSingleColumn(string tableName, SimpleWhereCondition whereCondition, params ColumnSetting[] columnSettings) {

        string sql = $"UPDATE {tableName} SET";

        for (var i = 0; i < columnSettings.Length; i++) {
          if (i > 0) sql += ",";
          sql += $" {columnSettings[i].ColumnName} = {columnSettings[i].ColumnName.EnsureStartsWith("@")}";
        }

        sql += $" WHERE {whereCondition.ColumnName} = {whereCondition.ColumnName.EnsureStartsWith("@")}";
        var paramList = new List<SqlParameter>();
        paramList.Add(whereCondition.GetSqlParameter());

        foreach (var col in columnSettings) paramList.Add(col.GetSqlParameter());

        return GetNonQueryInt(sql, paramList.ToArray());
      }

      /// <summary>
      /// Get the UTC time of 12AM WST.
      /// Usage: $"DECLARE @var DATETIME2(0) = {GetSQL_Today12amWST_UTC()}; ..."
      /// </summary>
      /// <returns>SQL string returning a DATETIMEOFFSET</returns>
      public static string GetSQL_Today12amWST_UTC() {

        return @"(CAST(CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'W. Australia Standard Time'AS DATE) AS DATETIME2(0))
          AT TIME ZONE 'W. Australia Standard Time'
          AT TIME ZONE 'UTC')";
      }

      public class ColumnSetting {

        public string ColumnName { get; private set; }
        public SqlDbType ColumnType { get; private set; }
        public long? IntegerValue { get; private set; }
        public bool? BoolValue { get; private set; }
        public string StringValue { get; private set; }
        public int StringLength { get; private set; }
        public DateTime? DateValue { get; private set; }

        protected ColumnSetting() { }

        public ColumnSetting(string columnName, bool? boolValue, SqlDbType sqlDbType = SqlDbType.TinyInt) {
          SetValue(columnName, boolValue, sqlDbType);
        }
        public ColumnSetting(string columnName, int? integerValue) {
          SetValue(columnName, integerValue, SqlDbType.Int);
        }
        public ColumnSetting(string columnName, DateTime? dateValue) {
          SetValue(columnName, dateValue, SqlDbType.DateTime2);
        }
        public ColumnSetting(string columnName, int stringLength, string stringValue) {
          SetValue(columnName, stringLength, stringValue, SqlDbType.VarChar);
        }

        protected void SetValue(string columnName, bool? boolValue, SqlDbType columnType) {
          this.ColumnName = columnName;
          this.ColumnType = columnType;
          if (columnType == SqlDbType.Bit)
            this.BoolValue = boolValue;
          else if (columnType == SqlDbType.TinyInt)
            this.IntegerValue = boolValue.ToBinaryOrNull();
          else
            throw new ArgumentException("Invalid columnType for boolean: " + columnType.GetType().Name);
        }

        protected void SetValue(string columnName, long? integerValue, SqlDbType columnType) {
          this.ColumnName = columnName;
          this.IntegerValue = integerValue;
          this.ColumnType = columnType;
          if (!CheckSqlDbTypeForInteger(columnType)) throw new ArgumentException("Invalid columnType for integer: " + columnType.GetType().Name);
        }

        protected void SetValue(string columnName, DateTime? dateValue, SqlDbType columnType) {
          this.ColumnName = columnName;
          this.DateValue = dateValue;
          this.ColumnType = columnType;
          if (!CheckSqlDbTypeForDate(columnType)) throw new ArgumentException("Invalid columnType for date: " + columnType.GetType().Name);
        }

        protected void SetValue(string columnName, int stringLength, string stringValue, SqlDbType columnType) {
          this.ColumnName = columnName;
          this.StringValue = stringValue;
          this.StringLength = stringLength;
          this.ColumnType = columnType;
          if (!CheckSqlDbTypeForString(columnType)) throw new ArgumentException("Invalid columnType for string: " + columnType.GetType().Name);
        }

        public SqlParameter GetSqlParameter() {

          switch (this.ColumnType) {
            case SqlDbType.Bit:
              return new SqlParameter(this.ColumnName, this.ColumnType) { Value = this.BoolValue.OrDBNull() };
            case SqlDbType.TinyInt:
            case SqlDbType.SmallInt:
            case SqlDbType.Int:
            case SqlDbType.BigInt:
              return new SqlParameter(this.ColumnName, this.ColumnType) { Value = this.IntegerValue.OrDBNull() };
            case SqlDbType.VarChar:
            case SqlDbType.NVarChar:
            case SqlDbType.Text:
            case SqlDbType.NText:
              return new SqlParameter(this.ColumnName, this.ColumnType, this.StringLength) { Value = this.StringValue.OrDBNull() };
            case SqlDbType.DateTime2:
            case SqlDbType.DateTime:
            case SqlDbType.Date:
              return new SqlParameter(this.ColumnName, this.ColumnType) { Value = this.DateValue.OrDBNull() };
            default:
              throw new ArgumentException("ColumnType is not an accepted type: " + this.ColumnType.GetType().Name);
          }
        }
      }

      public class SimpleWhereCondition : ColumnSetting {

        public SimpleWhereCondition(string columnName, bool? boolValue, SqlDbType sqlDbType = SqlDbType.TinyInt) {
          SetValue(columnName, boolValue, sqlDbType);
        }
        public SimpleWhereCondition(string columnName, long? integerValue, SqlDbType sqlDbType = SqlDbType.Int) {
          SetValue(columnName, integerValue, sqlDbType);
        }
        public SimpleWhereCondition(string columnName, int stringLength, string stringValue, SqlDbType sqlDbType = SqlDbType.VarChar) {
          SetValue(columnName, stringLength, stringValue, sqlDbType);
        }
        public SimpleWhereCondition(string columnName, DateTime? dateValue, SqlDbType sqlDbType = SqlDbType.DateTime2) {
          SetValue(columnName, dateValue, sqlDbType);
        }
      }
    }
  }
}
