using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class InsightsIOSReports : AppCode.PageBaseClasses.LoggedInPageModel {

    public DbHelper.OrgReportsCached.ReportData ReportData;
    public DbHelper.OrgReportsCached.ReportParticipantInfo ReportParticipantInfo;
    public List<DbHelper.AlbertSurveys.SurveyInfo> SurveyList;
    public DbHelper.AlbertSurveys.SurveyInfo SurveyInfo;
    public int NoContentStatusCode;
    public bool ReportIsAvailable = false;
    public bool ShowReportList = false;
    public string RequestedCtrlName;

    public class AjaxAction {
      public const string GetData = "getdata";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      if (!SessionHelper.AppAccess.Insights.CanCurrentRoleViewIOSReports()) {
        WebHelper.Redirect(FallbackUrl);
        return new EmptyResult();
      }

      NoContentStatusCode = WebHelper.GetHttpStatusCode(WebHelper.HttpStatusEnum.NoContent);

      if (PageAjaxAction == AjaxAction.GetData) {
        WebHelper.EndRequest();
        return new EmptyResult();
      }

      // Check if requesting usercontrol content.
      RequestedCtrlName = WebHelper.GetQueryStringValue("ctrlname").EmptyIfNull().ToLower();
      if (!RequestedCtrlName.IsNullOrEmpty()) {
        return Page();
      }

      string urlSurveyUId = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId);

      if (urlSurveyUId.IsNullOrEmpty()) {
        // No specific survey requested, show list.
        ShowReportList = true;
        PageTitle = "IOS Reports";
        SurveyList = DbHelper.AlbertSurveys.GetOrgSurveysForClient(SessionHelper.UserInfo);
        return Page();
      }

      SurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUId);

      if (!SessionHelper.AppAccess.Surveys.CanViewReports(SurveyInfo)) return Page();

      PageTitle = $"IOS Report {WebHelper.DisplayDate(SurveyInfo.CloseDateSelfLocal)}";

      if (Reports.OrgReports.IsReloadRequested()) DbHelper.OrgReportsCached.ReportData_ClearCache(urlSurveyUId);

      bool clearCachedResults = !WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.IOSClearCachedResults).IsNullOrEmpty();

      ReportData = DbHelper.OrgReportsCached.GetReportData(
        new DbHelper.OrgReportsCached.GetReportDataParams(SurveyInfo.SurveyUID, null, SystemWeb.SessionID, Reports.OrgReports.GetReportFiltersFromQuery()),
        clearCachedResults);

      ReportIsAvailable = ReportData != null;

      return Page();
    }
  }
}
