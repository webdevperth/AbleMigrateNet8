using System;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integral.Web.PortalSite.Endpoints {

  // Minimal-API ports of the legacy /ajax/*.aspx handlers. Each handler writes its
  // own response via WebHelper / AjaxSubmitHelper, so the lambdas return nothing
  // and the framework just flushes whatever has been written.
  public static class AjaxEndpoints {

    public static void MapEndpoints(IEndpointRouteBuilder app) {

      // SetUserRole — switch the session user role (e.g. Admin <-> Leader) and
      // tell the AjaxSubmit caller to redirect home.
      app.MapPost("/ajax/SetUserRole", () => {

        if (!AuthInitialization.RequireLoggedInUser()) return;

        AjaxSubmitHelper.Process(ajax => {
          if (Enum.TryParse(WebHelper.GetFormValue(PathHelper.FormKeys.UserRole) ?? "", out ConfigHelper.UserRole userRoleRequest)) {
            SessionHelper.SetUserRole(userRoleRequest);
            SessionHelper.SetLoginRequiredRedirectedFrom(SessionHelper.LoginRequiredRedirectedFrom.Login);
            ajax.SetRedirectUrl(PathHelper.Pages.Home());
          }
        });
      });

      // AblePrograms — JSON list of programs for a given company, scoped by the
      // caller's role. Called from CoacheeEdit and similar via $.get, so accept both verbs.
      app.MapMethods("/ajax/AblePrograms", new[] { "GET", "POST" }, () => {

        if (!AuthInitialization.RequireLoggedInUser()) return;

        if (("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId)) == "") return;
        if (!int.TryParse("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId), out int urlCompanyId)) return;
        if (urlCompanyId < 0) return;

        var response = new {
          Programs = DbHelper.AblePrograms.GetProgramListForCompany(
            urlCompanyId,
            SessionHelper.IsUserRoleAdmin
              ? DbHelper.AblePrograms.WhereRelatedUserIs.NoCheck
              : DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_PLC_PC,
            SessionHelper.UserInfo
          )
        };
        WebHelper.WriteJsonAndEnd(response);
      });

      // AbleProjects — JSON list of projects for a given company. Called via $.get
      // from the AddParticipant wizard and QuoteInfo, so accept both verbs.
      app.MapMethods("/ajax/AbleProjects", new[] { "GET", "POST" }, () => {

        if (!AuthInitialization.RequireLoggedInUser()) return;

        if (("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId)) == "") return;
        if (!int.TryParse("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId), out int urlCompanyId)) return;
        if (urlCompanyId < 0) return;

        var response = new {
          Projects = SessionHelper.AppAccess.Quotes.CanSelectProjectsFromAllOrgs()
            ? DbHelper.Projects.GetProjectsForCompany(urlCompanyId)
            : DbHelper.Projects.GetProjectsForCompanyAndUserOrg(urlCompanyId, SessionHelper.GetUserInfoOrNull())
        };
        WebHelper.WriteJsonAndEnd(response);
      });

      // jslogger — POST records a client-side JS error; bare GET is used by the
      // layout as a periodic session keep-alive that simply 204s. No auth required:
      // unauthenticated visitors can still hit JS errors worth logging.
      app.MapMethods("/ajax/jslogger", new[] { "GET", "POST" }, (HttpContext ctx) => {

        string requestUrl = SystemWeb.UrlDecode(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.RequestUrl));

        if (!SystemWeb.IsHttpPost || requestUrl.IsNullOrEmpty()) {
          WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
          return;
        }

        string errorMessage    = SystemWeb.UrlDecode(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.ErrorMessage));
        string stackTraceText  = SystemWeb.UrlDecode(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.StackTrace));
        string errorUrl        = SystemWeb.UrlDecode(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.ErrorUrl));
        int? errorLine         = ParseIntOrNull(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.ErrorLine));
        int? errorColumn       = ParseIntOrNull(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.ErrorColumn));
        string requestQuery    = SystemWeb.UrlDecode(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.RequestQuery));
        string referrerUrl     = SystemWeb.UrlDecode(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.ReferrerUrl));
        string browserUserAgent = SystemWeb.UrlDecode(WebHelper.GetFormValue(PathHelper.JSLoggerKeys.BrowserUserAgent));

        if (errorMessage.IsNullOrEmpty()) {
          // Not an error, just a session keep-alive ping.
          WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
          return;
        }

        string queryUrlDecoded = null;
        if (requestQuery != null) {
          try { queryUrlDecoded = SystemWeb.UrlDecode(requestQuery); } catch { }
        }

        string latestSQLText = null;
        try { latestSQLText = LogHelper.GetLatestSqlQueryText(); } catch { }

        DbHelper.AbleUser.AbleUserInfo loggedInUser = null;
        try { loggedInUser = SessionHelper.GetUserInfoOrNull(); } catch { }

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

        try {
          DbHelper.ErrorLog.Add(errorLogInfo);
        } catch (Exception ex) {
          try {
            EmailHelper.SendInternalSupportEmail(ex, "Error saving this error to db", $@"
              Error saving this error to db: {ex.Message}\n\n
              JS Error: {errorMessage}\n
              URL: {requestUrl}\n
              Referrer: {referrerUrl}\n
              Line: {errorLine}\n
              Column: {errorColumn}\n");
          } catch { }
        }

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
      });

      // logout — clear session for an in-page AJAX call (no redirect).
      app.MapPost("/ajax/logout", () => {
        SessionHelper.LogOut();
      });
    }

    private static int? ParseIntOrNull(string s) {
      if (s != null && int.TryParse(s, out int n)) return n;
      return null;
    }
  }
}
