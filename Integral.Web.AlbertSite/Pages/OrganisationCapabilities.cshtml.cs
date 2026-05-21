using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class OrganisationCapabilities : AppCode.PageBaseClasses.CompanyInfoBase {

    public class PartialIDs {
      public const string SummaryScore = "partial_Overview";
      public const string Categories = "partial_Categories";
      public const string Detail = "partial_Detail";
      public const string Focus = "partial_Focus";
      public const string HeatMap = "partial_HeatMap";
      public const string PrePost = "partial_PrePost";
    }

    public class AjaxAction {
      public const string GetHasPreSurvey = "GetHasPreSurvey";
    }

    public class FormFields {
      public const string SurveyCompanyId = "SurveyCompanyId";
      public const string GblAnswerTypeId = "GblAnswerTypeId";
    }

    public class ReturnValues {
      public const string Message = "Message";
      public const string Html = "Html";
      public const string HasPreSurvey = "HasPreSurvey";
      public const string DisableReportTabs = "DisableReportTabs";
    }

    public int ReportableSurveyCount = 0;
    public bool HasPrePost = false;
    public bool HasPreSurvey = false;
    public string FailedReportMessage = null;
    public List<DbHelper.Reports.Company.SurveyStats> SurveyStatsList = null;
    public DbHelper.Reports.Company.SurveyStats SurveyStats = null;
    public Dictionary<int, string> SurveyTypeOptions = new Dictionary<int, string>();

    private int? urlGblAnswerTypeId;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      if (!SessionHelper.AppAccess.Companies.CanViewOrganisationCapabilities(CompanyInfo)) {
        SetFallbackRedirectNoAccess();
        return new EmptyResult();
      }

      PageTitle = "Organisation Capabilities";

      urlGblAnswerTypeId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.GblAnswerTypeId);

      // Get survey stats for company.
      // If no GblAnswerTypeId is requested, and there's more than one 360 type (i.e. with different GblAnswerTypeId's)
      // then SurveyStatsList will contain more than 1 item - one for each GblAnswerTypeId.
      // In that case, default to the first one, and present a dropdown to the user to change to the other one(s).
      SurveyStatsList = DbHelper.Reports.Company.GetSurveyStatsByType(
        companyIdOrNullForAll: CompanyInfo.CompanyId,
        projectJobNumbersOrNullForAll: SessionHelper.IsUserRoleAdmin ? null : SessionHelper.GetUserInfoOrNull().ProjectAccessForJobNumbers.Join(","),
        programJobIdsOrNullForAll: null,
        gblAnswerTypeIdOrNullForAll: urlGblAnswerTypeId,
        surveyTypeCode: ConfigHelper.SurveyTypeCodes.Able360,
        onlySurveysWithRptQnGroupId: ConfigHelper.RptQnGroupId_SkillsViewer);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {
          if (ajax.Action == AjaxAction.GetHasPreSurvey) {
            GetHasPreSurvey(ajax);
          }
          return;
        });

        return new EmptyResult();
      }

      if (!SurveyStatsList.IsNullOrEmpty()) {

        SurveyStats = SurveyStatsList[0]; // Default to first item. If GblAnswerTypeId was provided, it will also be the first item.
        HasPreSurvey = SurveyStats.HasPreSurvey;
        HasPrePost = DbHelper.Reports.Company.GetHasPrePost(CompanyInfo.CompanyId);

      } else {

        SurveyStats = null; // No surveys found.
        FailedReportMessage = "Could not find any compatible surveys.";
      }

      return Page();
    }

    void GetHasPreSurvey(AjaxSubmitHelper ajax) {

      if (SurveyStatsList.IsNullOrEmpty()) {

        ajax.AddReturnValue(ReturnValues.Message, "Could not find any compatible surveys.");
        ajax.AddReturnValue(ReturnValues.DisableReportTabs, true);
        return;

      } else if (SurveyStatsList.Count > 1) {

        ajax.AddReturnValue(ReturnValues.Message, "Results contain incompatible surveys.");
        ajax.AddReturnValue(ReturnValues.DisableReportTabs, true);
        return;
      }

      ajax.AddReturnValue(ReturnValues.HasPreSurvey, SurveyStatsList[0].HasPreSurvey);
    }

    public string GetSurveyTypeOptionsHtml() {

      string html = "";

      foreach (var stats in SurveyStatsList) {

        // Use config preferred description if present.
        string ansTypeDescr = ConfigHelper.GblAnsTypeDescrById.FindOrDefault(stats.GblAnswerTypeId, stats.GblAnswerTypeDescr);

        html += $"<option value =\"{stats.GblAnswerTypeId}\"";
        if (stats.GblAnswerTypeId == urlGblAnswerTypeId) html += " selected";
        html += $">{ansTypeDescr.HTMLEncode()} ({stats.SurveyCount} {"survey".ToPlural(stats.SurveyCount)})";
        html += "</option>";
      }

      return html;
    }

  }
}
