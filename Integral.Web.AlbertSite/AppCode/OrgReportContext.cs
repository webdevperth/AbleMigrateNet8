using Microsoft.AspNetCore.Http;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.AppCode {

  // Request-scoped data container shared by every OrgReport ViewComponent on one HTTP
  // request. Replaces the Page_Init-driven OrgReportControlBase used by the legacy
  // OrgRpt_*.ascx controls. The first ViewComponent to call GetOrLoad on a request
  // does the database fetch and the participant-count gate check; subsequent calls
  // on the same request return the cached instance.
  public class OrgReportContext {

    private const string Items_CacheKey = "Integral.Web.PortalSite.OrgReportContext";

    public string SurveyUID { get; private set; }
    public string PartUID { get; private set; }

    public DbHelper.OrgReportsCached.ReportData ReportData { get; private set; }
    public DbHelper.OrgReportsCached.ReportParticipantInfo ReportParticipantInfo { get; private set; }

    // True when ReportData and ReportParticipantInfo are loaded and the participant-count
    // gate has been satisfied (or skipped via ignorePartCount). When false, the request
    // status code has already been set via WebHelper.EndRequest() and ViewComponents
    // should short-circuit to an empty result.
    public bool IsAvailable { get; private set; }

    private OrgReportContext() { }

    public static OrgReportContext GetOrLoad(HttpContext httpContext, bool ignorePartCount = false) {

      if (httpContext.Items.TryGetValue(Items_CacheKey, out object existing) && existing is OrgReportContext cached) {
        return cached;
      }

      var ctx = Load(ignorePartCount);
      httpContext.Items[Items_CacheKey] = ctx;
      return ctx;
    }

    private static OrgReportContext Load(bool ignorePartCount) {

      var ctx = new OrgReportContext();

      // Try to get survey UID from "external" Org report url, then by "internal" (Client logged in) URL.
      // They both use different query keys, and need to retain backward compatibility with the external links.
      OrgReports.GetExternalIOSUIDsFromQuery(out string surveyUID, out string partUID);
      if (surveyUID.IsNullOrEmpty()) {
        surveyUID = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId);
      }
      ctx.SurveyUID = surveyUID;
      ctx.PartUID = partUID;

      // URL flag for testing to clear cached report results and get the latest data.
      bool clearCachedResults = !WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.IOSClearCachedResults).IsNullOrEmpty();

      ctx.ReportData = OrgReports.GetReportData(surveyUID, partUID, clearCachedResults);

      if (ctx.ReportData == null) {
        WebHelper.EndRequest();
        return ctx;
      }

      ctx.ReportParticipantInfo = OrgReports.GetReportParticipantInfo(surveyUID, partUID);

      if (ctx.ReportParticipantInfo == null) {
        WebHelper.EndRequest();
        return ctx;
      }

      if (!ignorePartCount && ctx.ReportData.ParticipantCount < ctx.ReportData.SurveyInfo.OrgReportMinResponsesToShow) {
        // Return a custom code to indicate not enough data and exit.
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.NoContent);
        return ctx;
      }

      ctx.IsAvailable = true;
      return ctx;
    }

  }

}
