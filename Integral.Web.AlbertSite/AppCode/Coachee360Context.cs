using System;
using Microsoft.AspNetCore.Http;

namespace Integral.Web.PortalSite.AppCode {

  // Request-scoped data container shared by every ChartAlbert360 ViewComponent on
  // one HTTP request. The first ViewComponent to call GetOrLoad on a request does
  // the coachee lookup and the Coachee360Results fetch; subsequent calls on the
  // same request return the cached instance.
  //
  // Mirrors OrgReportContext, but for the coachee-side (Coachee360) data flow.
  public class Coachee360Context {

    private const string Items_CacheKey_Org    = "Integral.Web.PortalSite.Coachee360Context.Org";
    private const string Items_CacheKey_Global = "Integral.Web.PortalSite.Coachee360Context.Global";

    public DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo { get; private set; }
    public DbHelper.Reports.Coachee360.Coachee360Results ReportResults { get; private set; }

    // True when CoacheeInfo and ReportResults are loaded. When false, the
    // ViewComponent should short-circuit to an empty result.
    public bool IsAvailable { get; private set; }

    private Coachee360Context() { }

    public static Coachee360Context GetOrLoad(HttpContext httpContext, bool useGlobalBench = false) {

      string cacheKey = useGlobalBench ? Items_CacheKey_Global : Items_CacheKey_Org;
      if (httpContext.Items.TryGetValue(cacheKey, out object existing) && existing is Coachee360Context cached) {
        return cached;
      }

      var ctx = Load(useGlobalBench);
      httpContext.Items[cacheKey] = ctx;
      return ctx;
    }

    private static Coachee360Context Load(bool useGlobalBench) {

      var ctx = new Coachee360Context();

      if (!Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeGuid).EmptyIfNull(), out Guid urlCoacheeUID)) {
        return ctx;
      }

      ctx.CoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(urlCoacheeUID);
      if (ctx.CoacheeInfo == null) return ctx;

      string urlSelectedSurveyUId = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId);
      int? benchCompanyId = useGlobalBench ? (int?)null : ctx.CoacheeInfo.CompanyId;

      ctx.ReportResults = DbHelper.Reports.Coachee360.GetCoachee360ReportResults(
        ctx.CoacheeInfo.CoacheeId, urlSelectedSurveyUId, benchCompanyId);

      if (ctx.ReportResults == null) return ctx;

      ctx.IsAvailable = true;
      return ctx;
    }

  }

}
