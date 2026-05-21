using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class SurveyViewer : AppCode.PageBaseClasses.LoggedInPageModel {

    public class FormFields {
      public const string CompanyIds = "CompanyIds";
      public const string ReportTypeIds = "ReportTypeIds";
      public const string IntakeCodeIds = "IntakeCodeIds";
    }

    public class AjaxAction {
      public const string GetSurveys = "GetSurveys";
      public const string GetReport = "GetReport";
    }

    public class PartialIDs {
      public const string SummaryScore = "partial_Overview";
      public const string Categories = "partial_Categories";
      public const string QuestionDetail = "partial_QuestionDetail";
      public const string Comments = "partial_Comments";
    }

    public List<int> UrlCompanyIdList, UrlReportTypeIdList, UrlIntakeIdList;

    List<DbHelper.ClientCompanies.SurveyViewerCompanyInfo> CompanyList = null;
    List<DbHelper.ReportTypes.ReportTypeForCompanyInfo> ReportTypeList = null;
    List<DbHelper.Reports.SurveyViewer.SurveyListItem> SurveyIntakeList = null;

    public List<DbHelper.ClientCompanies.SurveyViewerCompanyInfo> SelectedCompanyList = null;
    public List<DbHelper.ReportTypes.ReportTypeForCompanyInfo> SelectedReportTypeList = null;
    public List<DbHelper.Reports.SurveyViewer.SurveyListItem> SelectedSurveyIntakeList = null;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      UrlCompanyIdList = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.CompanyId);
      UrlReportTypeIdList = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.SurveyReportType);
      UrlIntakeIdList = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.SurveyIntakeCodeId);

      if (!SystemWeb.IsHttpPost) {

        GetPage();

      } else {

        if (WebHelper.GetFormValue(PathHelper.FormKeys.AjaxAction) == AjaxAction.GetSurveys) {

          GetSurveySelectOptionsHtml();
          return new EmptyResult();

        } else if (WebHelper.GetFormValue(PathHelper.FormKeys.AjaxAction) == AjaxAction.GetReport) {

          //GetReport(ajax);
          return new EmptyResult();
        }
      }

      return Page();
    }

    void GetPage() {

      PageTitle = "Survey Viewer";

      CompanyList = DbHelper.ClientCompanies.GetCompaniesForSurveyViewer();
      ReportTypeList = DbHelper.ReportTypes.GetReportTypesForSurveyViewer();

      if (!UrlCompanyIdList.IsNullOrEmpty()) {
        SelectedCompanyList = CompanyList.FindAll(c => UrlCompanyIdList.Contains(c.CompanyId));
        if (SelectedCompanyList.IsNullOrEmpty()) {
          UrlCompanyIdList = null;
        }
      }

      if (!UrlReportTypeIdList.IsNullOrEmpty()) {
        SelectedReportTypeList = ReportTypeList.FindAll(rt => UrlReportTypeIdList.Contains(rt.ReportTypeId));
        if (SelectedReportTypeList.IsNullOrEmpty()) {
          UrlReportTypeIdList = null;
        }
      }
    }

    void GetSurveySelectOptionsHtml() {

      var companyIdList = WebHelper.GetFormIntListOrDefault(FormFields.CompanyIds);
      var reportTypeIdList = WebHelper.GetFormIntListOrDefault(FormFields.ReportTypeIds);

      if (companyIdList.IsNullOrEmpty() || reportTypeIdList.IsNullOrEmpty()) {
        WebHelper.WriteAndEnd("<option value=\"\">[Select Company and Report Type(s)]</option>");
        return;
      }

      SurveyIntakeList = DbHelper.Reports.SurveyViewer.GetSurveyList(companyIdList, reportTypeIdList);

      var sbHtml = new StringBuilder();

      foreach (var intake in SurveyIntakeList) {
        bool isSelected = UrlIntakeIdList != null && UrlIntakeIdList.Contains(intake.IntakeCodeId);
        sbHtml.Append("<option");
        if (isSelected) sbHtml.Append(" selected");
        sbHtml.Append($" value =\"{intake.IntakeCodeId}\">");
        sbHtml.Append(intake.ReportType);
        sbHtml.Append(": ");
        sbHtml.Append(intake.IntakeCloseDateLocal.ToString("d MMM yyyy"));
        sbHtml.Append(": ");
        sbHtml.Append(intake.IntakeName.HTMLEncode());
        sbHtml.Append(" - ");
        sbHtml.Append(intake.SurveyPublicTitle.ValueIfNullOrEmpty(intake.SurveyTitle).HTMLEncode());
        sbHtml.Append("</option>");
      }

      WebHelper.WriteAndEnd(sbHtml.ToString());
    }

    public string GetCompanyOptionsHtml(List<DbHelper.ClientCompanies.SurveyViewerCompanyInfo> selectedCompanyList) {

      var sbHtml = new StringBuilder();

      if (selectedCompanyList.IsNullOrEmpty()) {
        sbHtml.Append("<option value=\"\">[Select Company]</option>");
      }

      foreach (var cmp in CompanyList) {
        sbHtml.Append("<option");
        if (selectedCompanyList != null && selectedCompanyList.Find(c => c.CompanyId == cmp.CompanyId) != null) sbHtml.Append(" selected");
        sbHtml.Append($" value=\"{cmp.CompanyId}\">");
        sbHtml.Append(cmp.OrgId == ConfigHelper.JarvisOrgId ? "Jarvis: " : "Able: ");
        sbHtml.Append(cmp.CompanyName.HTMLEncode());
        sbHtml.Append("</option>");
      }

      return sbHtml.ToString();
    }

    public string GetReportTypeOptionsHtml(List<DbHelper.ReportTypes.ReportTypeForCompanyInfo> selectedReportTypeList) {

      var sbHtml = new StringBuilder();

      foreach (var rt in ReportTypeList) {
        sbHtml.Append("<option");
        if (selectedReportTypeList != null && selectedReportTypeList.Find(srt => srt.ReportTypeId == rt.ReportTypeId) != null) sbHtml.Append(" selected");
        sbHtml.Append($" value=\"{rt.ReportTypeId}\">");
        sbHtml.Append(rt.ReportTypeName.HTMLEncode());
        sbHtml.Append("</option>");
      }

      return sbHtml.ToString();
    }
  }
}
