using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Integral.Web {

  public class LogHelper : HelperBase<LogHelper> {

    private const string StartupLogFileName = "startup.log.txt";
    private static string startupLogFilePath = null; // Full path + filename.

    static LogHelper() {

      if (ConfigHelper.IsDevServer) {
        startupLogFilePath = ConfigHelper.LogsFolderPath + StartupLogFileName;
        if (System.IO.File.Exists(startupLogFilePath)) System.IO.File.Delete(startupLogFilePath);
      }
    }

    public static void WriteStartupLogLine(string logLine) {
      if (ConfigHelper.IsDevServer && startupLogFilePath != null) {
        System.IO.File.AppendAllText(startupLogFilePath, $"{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")}: {logLine}\n");
      }
    }

    public static void DebugWrite() {
      DebugWrite(string.Empty);
    }

    public static void DebugWrite(string debugText) {

      if (!ConfigHelper.IsDevServer) return;

      var frame = new StackFrame(1);
      var method = frame.GetMethod();
      var type = method.DeclaringType == null ? string.Empty : method.DeclaringType.Name;
      var name = method.Name;

      debugText = string.Empty + debugText;

      if (debugText.Length >= name.Length && debugText.Substring(0, name.Length) == name) {
        debugText = debugText.Substring(name.Length);
      }

      Console.WriteLine($"[ {type}.{name} ] {debugText}");
    }

    public static void LogError(string errorMessage) {
      DebugWrite($"[Error] {errorMessage}");
    }

    public static void LogError(string errorMessage, Exception ex) {
      DebugWrite($"[Error] {errorMessage}: {ex?.Message}");
      StackTraceToConsole(ex);
    }

    public static void StackTraceToConsole(Exception ex) {

      if (!ConfigHelper.IsDevServer) return;

      string trace = Regex.Replace(ex.ToString(), "^\\s*at system\\.web\\.mvc.*[\\r\\n]+", string.Empty, RegexOptions.IgnoreCase & RegexOptions.Multiline);

      Console.WriteLine("");
      Console.WriteLine("*** ***** ***");
      Console.WriteLine("*** ERROR *** " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss"));
      Console.WriteLine("*** ***** *** " + ex.Message);
      Console.WriteLine("*** ***** ***");
      Console.WriteLine(trace);
      Console.WriteLine("*** ERROR ***");
      Console.WriteLine("*** ***** ***");
      Console.WriteLine("");
    }

    public static SqlQueryLogInfo LogLatestSQL(SqlCommand sqlCmd, bool writeResponseLog = false, bool forceLive = false) {

      SqlQueryLogInfo queryLogInfo = null;

      try { // Make sure this never raises an error.

        if (ConfigHelper.IsLiveServer && !forceLive) return null; // Never do this live.

        if (sqlCmd == null) {
          if (writeResponseLog) ResponseLog("sqlCmd = null");
          return null;
        }

        string commandAsSQL = string.Empty;

        foreach (SqlParameter par in sqlCmd.Parameters) {

          string parType = string.Empty;
          string parValue = string.Empty;
          bool unknownType = false;

          switch (par.DbType) {
            case System.Data.DbType.Byte:
              parType = "TINYINT";
              parValue = par.Value == null ? "NULL" : par.Value.ToString();
              break;
            case System.Data.DbType.Int16:
            case System.Data.DbType.Int32:
            case System.Data.DbType.Int64:
              parType = "INT";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : par.Value.ToString();
              break;
            case System.Data.DbType.Date:
              parType = "DATE";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : ((DateTime)par.Value).ToString("yyyy-MM-dd").SurroundWith("'", true);
              break;
            case System.Data.DbType.DateTime:
              parType = "DATETIME";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : ((DateTime)par.Value).ToString("yyyy-MM-dd hh:mm:ss").SurroundWith("'", true);
              break;
            case System.Data.DbType.DateTime2:
              parType = "DATETIME2";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : ((DateTime)par.Value).ToString("yyyy-MM-dd hh:mm:ss").SurroundWith("'", true);
              break;
            case System.Data.DbType.DateTimeOffset:
              parType = "DATETIMEOFFSET";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : ((DateTimeOffset)par.Value).ToString().SurroundWith("'", true);
              break;
            case System.Data.DbType.Decimal:
              parType = "DECIMAL";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : par.Value.ToString();
              break;
            case System.Data.DbType.Double:
            case System.Data.DbType.VarNumeric:
              parType = "DOUBLE";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : par.Value.ToString();
              break;
            case System.Data.DbType.Guid:
              parType = "UNIQUEIDENTIFIER";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : par.Value.ToString().SurroundWith("'", true);
              break;
            case System.Data.DbType.Single:
              parType = "SINGLE";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : par.Value.ToString();
              break;
            case System.Data.DbType.String:
            case System.Data.DbType.StringFixedLength:
            case System.Data.DbType.AnsiString:
            case System.Data.DbType.AnsiStringFixedLength:
              parType = "VARCHAR(MAX)";
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : par.Value.ToString().SurroundWith("'", true);
              break;
            default:
              parType = par.SqlDbType.ToString();
              parValue = (par.Value == null || par.Value == DBNull.Value) ? "NULL" : par.Value.ToString().SurroundWith("'", true);
              unknownType = true;
              break;
          }
          commandAsSQL += $"DECLARE {par.ParameterName} {parType} = {parValue}; {(unknownType ? " -- UNKNOWN TYPE" : string.Empty)}\r\n";
        }
        commandAsSQL += sqlCmd.CommandText + ";";
        queryLogInfo = AppHelper.GetRequestInfo().AddToSQLQueryLog(commandAsSQL, new TimeSpan(0));
        if (writeResponseLog) ResponseLog(commandAsSQL);

      } catch (Exception) {
        // ignore
      }
      return queryLogInfo;
    }

    public static void SetSqlQueryDuration(SqlQueryLogInfo sqlLogInfo, TimeSpan duration) {
      sqlLogInfo?.SetDuration(duration);
    }

    public static void SetLatestSqlDuration(TimeSpan duration) {
      GetLatestSQLQueryInfo()?.SetDuration(duration);
    }

    public static string GetLatestSqlQueryText() {
      return GetLatestSQLQueryInfo()?.QueryText;
    }

    public static SqlQueryLogInfo GetLatestSQLQueryInfo() {
      return AppHelper.GetRequestInfo().SqlQueryLog?.LastOrDefault();
    }

    public static SqlQueryLogInfo GetLongestSqlQueryInfo() {
      return AppHelper.GetRequestInfo().SqlQueryLog?.OrderByDescending(l => l.QueryDuration).First();
    }

    [MethodImplAttribute(MethodImplOptions.NoInlining)] // Allows stack trace to work properly.
    public static void ResponseLog(
      string logText,
      bool forceStaging = false,
      bool forceLive = false,
      [CallerFilePath] string callerFilePath = "",
      [CallerLineNumber] int callerLineNumber = 0,
      [CallerMemberName] string callerMemberName = "") {

      // This adds to a request-level logging buffer.
      // It is usually output in the master template, contained inside a hidden div just before </body>
      if (ConfigHelper.IsLiveServer && !forceLive || ConfigHelper.IsStagingServer && !forceStaging) return;

      List<ResponseLogItem> responseLog;

      if (!SystemWeb.RequestItemExists(ConfigHelper.RequestItems.LogHelper_ResponseLog)) {
        responseLog = new List<ResponseLogItem>();
        SystemWeb.SetRequestItemValue(ConfigHelper.RequestItems.LogHelper_ResponseLog, responseLog);
      } else {
        responseLog = (List<ResponseLogItem>)SystemWeb.GetRequestItemValue(ConfigHelper.RequestItems.LogHelper_ResponseLog);
      }

      // Include trace to last couple of callers - only on dev.
      string logTrace = string.Empty;
      if (ConfigHelper.IsDevServer) {
        StackTrace stackTrace = new StackTrace();
        if (stackTrace.FrameCount > 0) {
          logTrace += "[[";
          var frame = stackTrace.GetFrame(1); // Just the last caller.
          var method = frame.GetMethod();
          //logTrace += (method == null ? string.Empty : method.DeclaringType.FullName) + " : " + frame.GetFileLineNumber();
          // Note that callerLineNumber isn't 100% accurate in uncompiled aspx.cs as it's coming from the temp cache file not the original file.
          logTrace += method.DeclaringType.FullName.Replace("Integral.Web.PortalSite.", string.Empty) + " : ~" + callerLineNumber + " (" + callerMemberName + ")";
          logTrace += "]]";
        }
      }

      // Simpler impl.
      responseLog.Add(new ResponseLogItem() { LogText = logText, LogTrace = logTrace });
    }

    // Note that there are 3 ways the log is returned to the browser:
    // 1. Javascript at the end of site.master,
    // 2. Javascript at the end of a HTML partial (see Application_EndRequest() in global.asax )
    // 3. In JSON response when using AjaxSubmit (see AjaxSubmitHelper.cs),
    public static List<ResponseLogItem> GetResponseLog() {

      if (!SystemWeb.HasRequest) return null;

      var logList = SystemWeb.GetRequestItemValue(ConfigHelper.RequestItems.LogHelper_ResponseLog) as List<ResponseLogItem>;

      if (logList == null) {
        logList = new List<ResponseLogItem>();
        SystemWeb.SetRequestItemValue(ConfigHelper.RequestItems.LogHelper_ResponseLog, logList);
      }

      return logList;
    }

    public static string GetResponseLogText(bool includeTrace, string beforeText, string afterText, string beforeTrace, string afterTrace) {
      // Return all responselog lines.

      string logText = string.Empty;
      var logList = GetResponseLog();

      if (beforeText == null) beforeText = string.Empty;
      if (afterText == null) beforeText = string.Empty;
      if (beforeTrace == null) beforeText = string.Empty;
      if (afterTrace == null) beforeText = string.Empty;

      if (!logList.IsNullOrEmpty()) {
        foreach (var logItem in logList) {
          logText += beforeText + logItem.LogText + afterText;
          if (includeTrace && !logItem.LogTrace.IsNullOrEmpty()) logText += beforeTrace + logItem.LogTrace + afterTrace;
        }
      }
      return logText;
    }

    public static void ClearResponseLog() {

      if (!SystemWeb.HasRequest) return;

      var logList = GetResponseLog();
      if (logList != null) {
        logList.Clear();
        logList = null;
        SystemWeb.RequestItemRemove(ConfigHelper.RequestItems.LogHelper_ResponseLog);
      }
    }

    public static string GetBriefStackTrace(string RemoveLinesContainingRegex) {

      if (ConfigHelper.IsLiveServer) return string.Empty; // Never do this live, only on dev.

      string trace = Environment.StackTrace;

      // Remove unnecessary stuff.
      trace = Regex.Replace(trace, @"^\s*at system\..*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
      trace = Regex.Replace(trace, @"Integral\.Web\.[a-z]+Site\.", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
      trace = Regex.Replace(trace, @"[a-z]:\\(?:[a-z0-9 .]+\\)+", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
      trace = Regex.Replace(trace, @"Integral\.Web\.", string.Empty, RegexOptions.IgnoreCase);
      trace = Regex.Replace(trace, @"^\s*at LogHelper.GetBriefStackTrace.*", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
      trace = Regex.Replace(trace, @"^\s*at ASP\..*", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
      // Remove requested lines.
      trace = Regex.Replace(trace, @"^.*" + RemoveLinesContainingRegex + ".*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
      // Remove empty lines.
      trace = Regex.Replace(trace, @"\n{2,}", string.Empty, RegexOptions.Singleline);

      return trace;
    }

    public const string StackTraceCSS = @"
      .error { color: red; }
      pre { font: normal 10pt 'roboto mono',consolas,fixed; border: 2px solid #ccc; padding: 10px 15px; white-space: pre-wrap; background-color: #FFFFCC; color: #777; }
      .sourceline { color: #000; line-height: 160%; display: inline-block; margin-bottom: 6px; text-decoration: underline; }
      .sourcefile {  }
      .function { color: #005fc2; }";

    public static string GetResponseLogJS() {

      var responseLog = GetResponseLog();
      if (responseLog.IsNullOrEmpty()) return string.Empty;

      string html = @"
        {
          let logItem = {};";
      foreach (var logItem in responseLog) {
        html += $@"
          try {{
            logItem = {GetResponseLogItemJson(logItem)};
            if (console.serverlog) console.serverlog(logItem); else console.log(logItem.LogText);
          }} catch(e) {{ }} // ignore";
      }
      html += @"
        }";

      return html;
    }

    public static string GetResponseLogJson() {
      var logList = GetResponseLog();
      if (logList.IsNullOrEmpty()) return "[]";
      return JsonSerializer.Serialize(logList, new JsonSerializerOptions() { WriteIndented = false });
    }

    public static string GetResponseLogItemJson(ResponseLogItem logItem) {
      if (logItem == null) return string.Empty;
      return JsonSerializer.Serialize(logItem, new JsonSerializerOptions() { WriteIndented = false });
    }

    public static string GetStackTraceFormattedHtml(Exception ex, bool includeCSSStyles, string prefixStyles = "") {

      if (ex == null) return string.Empty;

      var html = new StringBuilder();

      if (includeCSSStyles) {
        html.AppendLine("<style>");
        html.AppendLine(StackTraceCSS.RegexReplace(@"^\s*", prefixStyles + " ", RegexOptions.Multiline));
        html.AppendLine("</style>");
      }

      var traceHtml = ex.ToString().HTMLEncode();

      // Highlight primary error.
      traceHtml = Regex.Replace(traceHtml, @"(---\&gt;)", "<br>$1");
      traceHtml = Regex.Replace(traceHtml, @"(---\&gt; .*)", "<span class=\"error\">$1</span>");
      // Split line at " at " and highlight all references to source files with line numbers, e.g. "ProgramCostItems.aspx.cs:line 80".
      traceHtml = Regex.Replace(traceHtml, @"(in [a-z]:(?:\\[a-z_.-]+)+:line [0-9]+)", "\n      <span class=\"sourceline\">$1</span>", RegexOptions.IgnoreCase);
      traceHtml = Regex.Replace(traceHtml, @"([a-z]+(?:[\.[a-z]+):line [0-9]+)", "<span class=\"sourcefile\">$1</span>", RegexOptions.IgnoreCase);
      // Highlight function names.
      traceHtml = Regex.Replace(traceHtml, @"(?<=\.)([a-z_]+)\(", "<span class=\"function\">$1</span>(", RegexOptions.IgnoreCase);
      // Highlight SQL Exception
      traceHtml = Regex.Replace(traceHtml, @"(Microsoft.Data.SqlClient.SqlException \([^\)]+\):[^\r\n]+)", "<span class=\"sql-exception\">$1</span>", RegexOptions.IgnoreCase);
      traceHtml = Regex.Replace(traceHtml, @"^\s*at\s*System.Data[^\r\n]+[\r\n]+", string.Empty, RegexOptions.Multiline);

      html.AppendLine("<pre>");
      html.AppendLine(traceHtml);
      html.AppendLine("</pre>");
      var latestQuery = GetLatestSQLQueryInfo();
      if (traceHtml.Contains("TdsParser.TryRun") && latestQuery != null) {
        html.AppendLine($"<p>Last SQL Logged:</p>");
        html.AppendLine("<pre>");
        html.AppendLine(latestQuery.QueryText);
        html.AppendLine("Duration: " + latestQuery.QueryDuration.TotalSeconds.RoundAwayFromZero(3) + "s.");
        html.AppendLine("</pre>");
      }

      return html.ToString();
    }

    public class ResponseLogItem {
      public string LogText { get; set; }
      public string LogTrace { get; set; }
    }

    public class SqlQueryLogInfo {

      public string QueryText { get; private set; }
      public TimeSpan QueryDuration { get; private set; }

      public SqlQueryLogInfo(string queryText, TimeSpan queryDuration) {
        QueryText = queryText;
        QueryDuration = queryDuration;
      }
      internal void SetDuration(TimeSpan duration) => QueryDuration = duration;
      public double QueryDurationSeconds => QueryDuration.TotalSeconds;
    }

  }
}
