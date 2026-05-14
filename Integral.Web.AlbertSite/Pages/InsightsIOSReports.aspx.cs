using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class InsightsIOSReports : AppCode.PageBaseClasses.LoggedInPageBase {

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

    protected void Page_Load(object sender, EventArgs e) {

      if (!SessionHelper.AppAccess.Insights.CanCurrentRoleViewIOSReports()) {
        WebHelper.Redirect(FallbackUrl);
        return;
      }

      NoContentStatusCode = WebHelper.GetHttpStatusCode(WebHelper.HttpStatusEnum.NoContent);

      if (PageAjaxAction == AjaxAction.GetData) {
        WebHelper.EndRequest();
      }

      // Check if requesting usercontrol content.
      RequestedCtrlName = WebHelper.GetQueryStringValue("ctrlname").EmptyIfNull().ToLower();
      if (!RequestedCtrlName.IsNullOrEmpty()) {
        return;
      }

      string urlSurveyUId = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId);

      if (urlSurveyUId.IsNullOrEmpty()) {
        // No specific survey requested, show list.
        ShowReportList = true;
        PageTitle = "IOS Reports";
        SurveyList = DbHelper.AlbertSurveys.GetOrgSurveysForClient(SessionHelper.UserInfo);
        return;
      }

      SurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUId);

      if (!SessionHelper.AppAccess.Surveys.CanViewReports(SurveyInfo)) return;

      PageTitle = $"IOS Report {WebHelper.DisplayDate(SurveyInfo.CloseDateSelfLocal)}";

      if (Reports.OrgReports.IsReloadRequested()) DbHelper.OrgReportsCached.ReportData_ClearCache(urlSurveyUId);

      bool clearCachedResults = !WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.IOSClearCachedResults).IsNullOrEmpty();

      ReportData = DbHelper.OrgReportsCached.GetReportData(
        new DbHelper.OrgReportsCached.GetReportDataParams(SurveyInfo.SurveyUID, null, Session.SessionID, Reports.OrgReports.GetReportFiltersFromQuery()),
        clearCachedResults);

      ReportIsAvailable = ReportData != null;

    }
  }
}
