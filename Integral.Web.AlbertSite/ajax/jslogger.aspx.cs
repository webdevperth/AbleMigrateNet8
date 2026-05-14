using System;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.ajax {

  public partial class jslogger : System.Web.UI.Page {

    protected void Page_Load(object sender, EventArgs e) {

      // RequestUrl should always have a value.
      string requestUrl = SystemWeb.UrlDecode(Request.Form[PathHelper.JSLoggerKeys.RequestUrl]);

      if (!SystemWeb.IsHttpPost || requestUrl.IsNullOrEmpty()) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return;
      }

      string errorMessage = SystemWeb.UrlDecode(Request.Form[PathHelper.JSLoggerKeys.ErrorMessage]);
      string stackTraceText = SystemWeb.UrlDecode(Request.Form[PathHelper.JSLoggerKeys.StackTrace]);
      string errorUrl = SystemWeb.UrlDecode(Request.Form[PathHelper.JSLoggerKeys.ErrorUrl]);
      int? errorLine = ToInt(Request.Form[PathHelper.JSLoggerKeys.ErrorLine]);
      int? errorColumn = ToInt(Request.Form[PathHelper.JSLoggerKeys.ErrorColumn]);
      string requestQuery = SystemWeb.UrlDecode(Request.Form[PathHelper.JSLoggerKeys.RequestQuery]);
      string referrerUrl = SystemWeb.UrlDecode(Request.Form[PathHelper.JSLoggerKeys.ReferrerUrl]);
      string browserUserAgent = SystemWeb.UrlDecode(Request.Form[PathHelper.JSLoggerKeys.BrowserUserAgent]);

      if (errorMessage.IsNullOrEmpty()) {
        // Not an error, just a session keep-alive ping.
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return;
      }

      string queryUrlDecoded = null;
      if (requestQuery != null) {
        try {
          queryUrlDecoded = SystemWeb.UrlDecode(requestQuery);
        } catch { }
      }

      string latestSQLText = null;
      try {
        latestSQLText = LogHelper.GetLatestSqlQueryText();
      } catch {
        // Ignore, shouldn't happen but must continue.
      }

      DbHelper.AbleUser.AbleUserInfo loggedInUser = null;
      try {
        loggedInUser = SessionHelper.GetUserInfoOrNull();
      } catch {
        // Ignore, shouldn't happen but must continue.
      }

      var errorLogInfo = new DbHelper.ErrorLog.ErrorLogInfo(
        occurredUtc: DateTime.UtcNow,
        errorMessage: errorMessage,
        stackTraceText: stackTraceText,
        isJSError: true,
        jsErrorUrl: errorUrl,
        jsErrorLine: errorLine,
        jsErrorColumn: errorColumn,
        httpMethod: "GET",
        requestUrl: requestUrl,
        queryUrlDecoded: queryUrlDecoded,
        referrerUrl: referrerUrl,
        requestHeaders: null,
        formOriginal: null,
        formUrlDecoded: null,
        loggedInUserId: loggedInUser?.UserId,
        loggedInUserEmail: loggedInUser?.EmailAddress,
        userRole: SessionHelper.GetUserRole().ToString(),
        sessionGuid: SessionHelper.GetSessionGuid(),
        latestSql: latestSQLText,
        browserUserAgent: browserUserAgent
      );

      errorLogInfo.SetJSInfo(errorUrl, errorLine, errorColumn);

      // Save error info to db.
      try {
        DbHelper.ErrorLog.Add(errorLogInfo);
      } catch (Exception ex) {
        // Fallback send email.
        try {
          EmailHelper.SendInternalSupportEmail(ex, "Error saving this error to db", $@"
            Error saving this error to db: {ex.Message}\n\n
            JS Error: {errorMessage}\n
            URL: {requestUrl}\n
            Referrer: {referrerUrl}\n
            Line: {errorLine}\n
            Column: {errorColumn}\n");
        } catch {
          // Ignore, no fallback if email fails.
        }
      }

      // Send to Application Insights
      var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
      telemetry?.JavaScriptError(errorMessage, stackTraceText)
        .WithPageUrl(requestUrl)
        .WithErrorUrl(errorUrl)
        .WithErrorLine(errorLine)
        .WithErrorColumn(errorColumn)
        .WithQueryString(queryUrlDecoded)
        .WithReferrer(referrerUrl)
        .WithBrowserUserAgent(browserUserAgent)
        .FromSession()
        .Track();
    }

    private int? ToInt(string s) {
      try {
        if (s != null && int.TryParse(s, out int num)) return num;
      } catch {
      }
      return null;
    }
  }
}
