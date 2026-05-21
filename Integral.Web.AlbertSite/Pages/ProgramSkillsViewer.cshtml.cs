using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProgramSkillsViewer : AppCode.PageBaseClasses.ProgramPageBase {

    public class AjaxAction {
      public const string GetStats = "GetStats";
    }

    public class PartialIDs {
      public const string SummaryScore = "partial_Overview";
      public const string Categories = "partial_Categories";
      public const string QuestionDetail = "partial_QuestionDetail";
      public const string Focus = "partial_Focus";
      public const string QuestionPrePost = "partial_QuestionPrePost";
    }

    public class ReturnValues {
      public const string Message = "Message";
      public const string Html = "Html";
      public const string ShowPrePostTab = "ShowPrePostTab";
      public const string DisableReportTabs = "DisableReportTabs";
      public const string Company360ResponseCount = "Company360ResponseCount";
    }

    public int Global360ResponseCount;
    public bool ShowPreSurveyOpenWarning, ShowPostSurveyOpenWarning, ShowPrePostTab;

    public DbHelper.Reports.SkillsViewer.SurveyStats SurveyStats;
    public DbHelper.AblePrograms.PrePostSurveyState PrePostSurveyState;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          DoPost(ajax);
        });
        return new EmptyResult();
      }

      PageTitle = "Pre-Post Impact";

      SetSurveyStats(null);

      // Get global self response count.
      DbHelper.Reports.General.GetResponseCountForCompanyOrGlobal(SurveyStats.PrimaryGblAnswerTypeId, null, out Global360ResponseCount, out _);

      PrePostSurveyState = DbHelper.AblePrograms.GetPrePostSurveyState(ProgramInfo.ProgramJobId);
      ShowPreSurveyOpenWarning = !PrePostSurveyState.PreProgramSurveyComplete;

      return Page();
    }

    void DoPost(AjaxSubmitHelper ajax) {

      string sMode = WebHelper.GetFormValue(PathHelper.FormKeys.AjaxAction);

      if (sMode == AjaxAction.GetStats) {
        SetSurveyStats(ajax);
        ajax.AddReturnValue(ReturnValues.ShowPrePostTab, ShowPrePostTab);
      } else {
        AjaxSubmitHelper.RespondNoAccessToFunction();
      }
      return;

    }

    void SetSurveyStats(AjaxSubmitHelper ajax) {

      var surveyStatsList = DbHelper.Reports.SkillsViewer.GetSurveyStatsByType(ProjectInfo.JobNumber, new List<int>() { ProgramInfo.ProgramJobId });

      if (surveyStatsList.IsNullOrEmpty()) {
        if (ajax != null) {
          ajax.AddReturnValue(ReturnValues.Message, "Could not find any compatible surveys, try a different selection.");
          ajax.AddReturnValue(ReturnValues.DisableReportTabs, true);
        }
        return;
      }

      if (surveyStatsList.Count > 1) {
        if (ajax != null) {
          ajax.AddReturnValue(ReturnValues.Message, "Results contain incompatible surveys, try a different selection.");
          ajax.AddReturnValue(ReturnValues.DisableReportTabs, true);
        }
        return;
      }

      SurveyStats = surveyStatsList[0];

      var programPrePostSurveyState = DbHelper.AblePrograms.GetPrePostSurveyState(SurveyStats.ProgramJobIds[0]);
      ShowPrePostTab = programPrePostSurveyState.PreProgramSurveyComplete && programPrePostSurveyState.PostProgramSurveyComplete;
    }

  }
}
