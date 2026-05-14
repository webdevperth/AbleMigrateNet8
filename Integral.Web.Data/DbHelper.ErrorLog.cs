using System;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper {

    public class ErrorLog {

      public static void Add(ErrorLogInfo errorLog) {

        if (errorLog == null) return;

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"
            INSERT INTO al_ErrorLog
              (
                OccurredUtc,     ErrorMessage,  StackTrace,       IsJSError,    JSErrorUrl,      JSErrorLine,   JSErrorColumn,   BrowserUserAgent,
                HttpMethod,      RequestUrl,    QueryUrlDecoded,  ReferrerUrl,  RequestHeaders,  FormOriginal,  FormUrlDecoded,
                LoggedInUserId,  UserRole,      SessionGuid,      LatestSql,    LoggedInUserEmail
              )
              VALUES
              (
                @OccurredUtc,    @ErrorMessage, @StackTrace,      @IsJSError,   @JSErrorUrl,     @JSErrorLine,  @JSErrorColumn,  @BrowserUserAgent,
                @HttpMethod,     @RequestUrl,   @QueryUrlDecoded, @ReferrerUrl, @RequestHeaders, @FormOriginal, @FormUrlDecoded,
                @LoggedInUserId, @UserRole,     @SessionGuid,     @LatestSql,   @LoggedInUserEmail
              );",
            conn)) {

            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqlParameter("@OccurredUtc", errorLog.OccurredUtc));
            cmd.Parameters.Add(new SqlParameter("@ErrorMessage", errorLog.ErrorMessage.LimitLengthTo(500) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@StackTrace", errorLog.StackTrace.LimitLengthTo(8000) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@IsJSError", errorLog.IsJSError ? 1 : 0));
            cmd.Parameters.Add(new SqlParameter("@JSErrorUrl", errorLog.JSErrorUrl.LimitLengthTo(1000) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@JSErrorLine", errorLog.JSErrorLine ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@JSErrorColumn", errorLog.JSErrorColumn ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@BrowserUserAgent", errorLog.BrowserUserAgent.LimitLengthTo(500) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@HttpMethod", errorLog.HttpMethod.LimitLengthTo(20) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@RequestUrl", errorLog.RequestUrl.LimitLengthTo(1000) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@QueryUrlDecoded", errorLog.QueryUrlDecoded.LimitLengthTo(1000) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@ReferrerUrl", (object)errorLog.ReferrerUrl.LimitLengthTo(1000) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@RequestHeaders", errorLog.RequestHeaders.LimitLengthTo(8000) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@FormOriginal", errorLog.FormOriginal.LimitLengthTo(8000) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@FormUrlDecoded", errorLog.FormUrlDecoded.LimitLengthTo(8000) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@LoggedInUserId", errorLog.LoggedInUserId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@LoggedInUserEmail", errorLog.LoggedInUserEmail ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@UserRole", errorLog.UserRole.LimitLengthTo(20) ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@SessionGuid", errorLog.SessionGuid.ToStringNoBracesOrNull() ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@LatestSql", errorLog.LatestSql.LimitLengthTo(8000) ?? (object)DBNull.Value));

            conn.Open();
            var result = cmd.ExecuteNonQuery();
          }
        }
      }

      public class ErrorLogInfo {

        public int ErrorLogId { get; private set; }
        public DateTime OccurredUtc { get; private set; }
        public string ErrorMessage { get; private set; }
        public string StackTrace { get; private set; }
        public bool IsJSError { get; private set; }
        public string JSErrorUrl { get; private set; }
        public int? JSErrorLine { get; private set; }
        public int? JSErrorColumn { get; private set; }
        public string BrowserUserAgent { get; private set; }
        public string HttpMethod { get; private set; }
        public string RequestUrl { get; private set; }
        public string QueryUrlDecoded { get; private set; }
        public string ReferrerUrl { get; private set; }
        public string RequestHeaders { get; private set; }
        public string FormOriginal { get; private set; }
        public string FormUrlDecoded { get; private set; }
        public int? LoggedInUserId { get; private set; }
        public string UserRole { get; private set; }
        public Guid? SessionGuid { get; private set; }
        public string LatestSql { get; private set; }
        public string LoggedInUserEmail { get; private set; }

        // Instance must have at least time and error info.
        public ErrorLogInfo(
          DateTime occurredUtc, string errorMessage, string stackTraceText,
          bool isJSError, string jsErrorUrl, int? jsErrorLine, int? jsErrorColumn, string browserUserAgent,
          string httpMethod, string requestUrl, string queryUrlDecoded,
          string referrerUrl, string requestHeaders,
          string formOriginal, string formUrlDecoded,
          int? loggedInUserId, string loggedInUserEmail,
          string userRole, Guid? sessionGuid, string latestSql) {

          OccurredUtc = occurredUtc;
          ErrorMessage = errorMessage;
          StackTrace = stackTraceText;
          IsJSError = isJSError;
          JSErrorUrl = jsErrorUrl;
          JSErrorLine = jsErrorLine;
          JSErrorColumn = jsErrorColumn;
          BrowserUserAgent = browserUserAgent;
          HttpMethod = httpMethod;
          RequestUrl = requestUrl;
          QueryUrlDecoded = queryUrlDecoded;
          ReferrerUrl = referrerUrl;
          RequestHeaders = requestHeaders;
          FormOriginal = formOriginal;
          FormUrlDecoded = formUrlDecoded;
          LoggedInUserId = loggedInUserId;
          LoggedInUserEmail = loggedInUserEmail;
          UserRole = userRole;
          SessionGuid = sessionGuid;
          LatestSql = latestSql;
        }

        public void SetJSInfo(string jsErrorUrl, int? jSLine, int? jSColumn) {
          IsJSError = true;
          JSErrorUrl = jsErrorUrl;
          JSErrorLine = jSLine;
          JSErrorColumn = jSColumn;
        }
      }

    }
  }
}

