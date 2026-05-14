using System;
using System.Collections.Generic;
using System.Text;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class SkillsViewer : AppCode.PageBaseClasses.LoggedInPageBase {

    public class FormFields {
      public const string ProjectJobNumber = "ProjectJobNumber";
      public const string ProgramJobIds = "ProgramJobIds";
    }

    public class AjaxAction {
      public const string GetPrograms = "GetPrograms";
      public const string GetStats = "GetStats";
      public const string GetReport = "GetReport";
    }

    public class PartialIDs {
      public const string SummaryScore = "partial_Overview";
      public const string Categories = "partial_Categories";
      public const string QuestionDetail = "partial_QuestionDetail";
      public const string Focus = "partial_Focus";
      public const string HeatMap = "partial_HeatMap";
      public const string QuestionPrePost = "partial_QuestionPrePost";
    }

    public class ReturnValues {
      public const string Message = "Message";
      public const string Html = "Html";
      public const string HasPreSurvey = "HasPreSurvey";
      public const string DisableReportTabs = "DisableReportTabs";
      public const string Company360ResponseCount = "Company360ResponseCount";
    }

    private string UrlProjectJobNumber;
    private List<int> UrlProgramIds;

    public List<DbHelper.Reports.SkillsViewer.ProjectSelectInfo> ProjectList;
    public int Global360ResponseCount;

    protected void Page_Load(object sender, EventArgs e) {

      UrlProjectJobNumber = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber);
      UrlProgramIds = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.ProgramJobId).NullIfEmpty();

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          DoPost(ajax);
        });
        return;
      } else {
        if (!SessionHelper.AppAccess.Insights.CanViewSkillsViewer()) {
          WebHelper.Redirect(PathHelper.Pages.OverviewUpcoming());
          return;
        }
        GetPage();
      }
    }

    void DoPost(AjaxSubmitHelper ajax) {

      if (!SessionHelper.AppAccess.Insights.CanViewSkillsViewer()) {
        ajax.AddDialogMessage("No Access to Capabilities.");
        return;
      }

      if (UrlProjectJobNumber.IsNullOrEmpty()) {
        ajax.AddDialogMessage("Project Number not provided.");
        return;
      }

      if (!SessionHelper.IsUserRoleAdmin && !SessionHelper.GetUserInfoOrNull().IsInProjectAccess(UrlProjectJobNumber)) {
        ajax.AddDialogMessage("No access to project.");
        return;
      }

      var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(UrlProjectJobNumber);

      if (projectInfo == null) {
        ajax.AddDialogMessage($"Project {UrlProjectJobNumber.HTMLEncode()} Not Found.");
        return;
      }

      if (Request.Form[PathHelper.FormKeys.AjaxAction] == AjaxAction.GetPrograms) {
        GetProgramOptionsHtml(ajax, projectInfo);
      } else if (Request.Form[PathHelper.FormKeys.AjaxAction] == AjaxAction.GetStats) {
        GetSurveyStats(ajax, projectInfo);
      }
    }

    void GetPage() {

      PageTitle = "Capabilities";

      DbHelper.Reports.General.GetResponseCount_Global(ConfigHelper.SurveyTypeCodes.Able360, out Global360ResponseCount, out _);

      if (SessionHelper.IsUserRoleAdmin) {
        ProjectList = DbHelper.Reports.SkillsViewer.GetAllProjectsSelectList();
      } else {
        ProjectList = DbHelper.Reports.SkillsViewer.GetProjectSelectList(SessionHelper.GetUserInfoOrNull().ProjectAccessForJobNumbers);
      }
    }

    public string GetProjectOptionsHtml() {

      var sbHtml = new StringBuilder();

      foreach (var project in ProjectList) {
        sbHtml.Append($"<option value =\"{project.ProjectJobNumber}\"");
        if (project.ProjectJobNumber.Equals(UrlProjectJobNumber, StringComparison.OrdinalIgnoreCase)) sbHtml.Append(" selected");
        sbHtml.Append($">{project.ProjectJobNumber}: {project.ProjectFriendlyTitle.ValueIfNullOrEmpty(project.ProjectName).HTMLEncode()} - {project.CompanyName.HTMLEncode()}");
        if (project.IntakeCount != null) sbHtml.Append($" ({project.IntakeCount.Value})");
        sbHtml.Append("</option>");
      }

      return sbHtml.ToString();
    }

    void GetProgramOptionsHtml(AjaxSubmitHelper ajax, DbHelper.Projects.ProjectInfo projectInfo) {

      if (!SessionHelper.IsUserRoleAdmin) {
        if (projectInfo.JobNumber.IsNullOrEmpty() || !SessionHelper.GetUserInfoOrNull().IsInProjectAccess(projectInfo.JobNumber)) {
          ajax.AddReturnValue(ReturnValues.Html, "<option value=\"\">[Select Project]</option>");
          ajax.AddReturnValue(ReturnValues.Company360ResponseCount, 0);
          return;
        }
      }

      var programList = DbHelper.Reports.SkillsViewer.GetProgramSelectList(projectInfo);
      var sbHtml = new StringBuilder();

      foreach (var program in programList) {
        string startDateText = "";
        if (program.StartDateUtc != null) {
          startDateText = WebHelper.DisplayDate(SessionHelper.UtcToUserTime(program.StartDateUtc.Value)) + ": ";
        }
        sbHtml.Append($"<option value =\"{program.ProgramJobId}\"");
        if (!UrlProgramIds.IsNullOrEmpty() && UrlProgramIds.Contains(program.ProgramJobId)) sbHtml.Append(" selected");
        sbHtml.Append($">{(startDateText + program.JobName).HTMLEncode()}");
        if (program.IntakeCount != null) sbHtml.Append($" ({program.IntakeCount.Value})");
        sbHtml.Append("</option>");
      }

      ajax.AddReturnValue(ReturnValues.Html, sbHtml.ToString());
      DbHelper.Reports.General.GetResponseCount_Org(ConfigHelper.SurveyTypeCodes.Able360, projectInfo.CompanyId ?? 0, out int selfCount, out _);
      ajax.AddReturnValue(ReturnValues.Company360ResponseCount, selfCount);
    }

    void GetSurveyStats(AjaxSubmitHelper ajax, DbHelper.Projects.ProjectInfo projectInfo) {

      var surveyStatsList = DbHelper.Reports.SkillsViewer.GetSurveyStatsByType(projectInfo.JobNumber, UrlProgramIds);

      if (surveyStatsList.IsNullOrEmpty()) {
        ajax.AddReturnValue(ReturnValues.Message, "Could not find any compatible surveys, try a different selection.");
        ajax.AddReturnValue(ReturnValues.DisableReportTabs, true);
        return;
      }

      if (surveyStatsList.Count > 1) {
        ajax.AddReturnValue(ReturnValues.Message, "Results contain incompatible surveys, try a different selection.");
        ajax.AddReturnValue(ReturnValues.DisableReportTabs, true);
        return;
      }

      ajax.AddReturnValue(ReturnValues.HasPreSurvey, surveyStatsList[0].HasPreSurvey);
    }

  }
}
