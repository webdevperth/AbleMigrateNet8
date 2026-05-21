using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class EvalViewer : AppCode.PageBaseClasses.LoggedInPageModel {

    public class FormFields {
      public const string ProjectSearchTerm = "ProjectSearchTerm";
      public const string ProjectJobNumber = "ProjectJobNumber";
      public const string ProgramJobIds = "ProgramJobIds";
      public const string IntakeCodeIds = "IntakeCodeIds";
    }

    public class AjaxAction {
      public const string GetProjects = "GetProjects";
      public const string GetPrograms = "GetPrograms";
      public const string GetSurveys = "GetSurveys";
      public const string GetReport = "GetReport";
    }

    public class PartialIDs {
      public const string SummaryScore = "partial_Overview";
      public const string Categories = "partial_Categories";
      public const string QuestionDetail = "partial_QuestionDetail";
      public const string Comments = "partial_Comments";
    }

    public class AjaxKey {
      public const string OptionsHtml = "OptionsHtml";
    }

    public (
      DbHelper.Projects.ProjectInfo ProjectInfo,
      List<int> ProgramJobIds,
      List<int> IntakeIdList,
      int TemplateSurveyId,
      bool HasTextAnswers,
      ConfigHelper.EvalType EvalType
    ) QueryValues;

    public (
      bool ShowNoAccessToProjects,
      bool ShowSelectionTab,
      bool ShowOpenTextTab,
      List<int> IntakeIdsForReport,
      ConfigHelper.EvalType EvalType
    ) PageValues;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      if (!GetQueryValues() || !SessionHelper.AppAccess.Insights.CanViewEvalViewer()) {
        // Couldn't get data from provided query.
        if (SystemWeb.IsHttpPost) {
          AjaxSubmitHelper.Process(ajax => {
            ajax.RespondNoAccessToFunction();
            return;
          });
        } else {
          WebHelper.Redirect(PathHelper.Pages.Home());
          return new EmptyResult();
        }
        return new EmptyResult();
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          DoPost(ajax);
        });
        WebHelper.EndRequest();
        return new EmptyResult();
      }

      GetPage();
      return Page();
    }

    void GetPage() {

      PageTitle = "Evaluation Results";

      // No intakes given in url, show the selection tab.
      if (QueryValues.IntakeIdList.IsNullOrEmpty()) {
        PageValues.ShowSelectionTab = true;
        return;
      }

      // Show report results based on query, without the selection tab.

      PageValues.ShowSelectionTab = false;
      PageValues.IntakeIdsForReport = QueryValues.IntakeIdList;
      PageValues.EvalType = QueryValues.EvalType;
      PageValues.ShowOpenTextTab = QueryValues.HasTextAnswers;

      // Set project submenu.
      ProjectInfo = QueryValues.ProjectInfo;
      if (QueryValues.ProgramJobIds.Count == 1) ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(QueryValues.ProgramJobIds[0]);
      MenuThirdLayerActive_Programs = ProjectMenuIsActive = true;

      SetPageTitle();
    }

    void SetPageTitle() {

      PageTitle = "Evaluation Results: ";

      switch (QueryValues.EvalType) {
        case ConfigHelper.EvalType.Coaching:
          PageTitle += "Coaching";
          break;
        case ConfigHelper.EvalType.Workshop:
          if (PageValues.IntakeIdsForReport.Count == 1) {
            var workshop = DbHelper.WorkshopEvents.GetWorkshopFromEvalIntakeId(ProgramInfo, PageValues.IntakeIdsForReport[0]);
            PageTitle += workshop?.WorkshopTitle ?? "Workshop";
          } else {
            PageTitle += "Workshops";
          }
          break;
        case ConfigHelper.EvalType.PostProgram:
          PageTitle += "Post-Program";
          break;
        case ConfigHelper.EvalType.PostProject:
          PageTitle += "Post-Project";
          break;
        default:
          PageTitle += "Program";
          break;
      }
    }

    bool GetQueryValues() {

      string selectedJobNumber = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber);
      var selectedProject = DbHelper.Projects.GetProjectInfoOrNull(selectedJobNumber, SessionHelper.GetUserInfoOrNull());

      // Project not found or user doesn't have access.
      if (selectedProject == null || !SessionHelper.AppAccess.Projects.CanViewProject(selectedProject)) return false;

      var selectedProgramIds = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.ProgramJobId);
      if (selectedProgramIds.IsNullOrEmpty()) return false; // Must provide at least one program id.

      // Are all the selected programs part of the same project?
      var allPrograms = DbHelper.AblePrograms.GetProgramsByJobNumber(selectedProject.JobNumber);
      foreach (int programId in selectedProgramIds) {
        if (allPrograms.ProgramInfoList.Find(p => p.ProgramJobId == programId) == null) return false; // Not found.
      }

      var selectedEvalType = WebHelper.GetQueryStringEnum(PathHelper.AbleUrlKeys.EvalType, ConfigHelper.EvalType.All);

      var selectedIntakeIds = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.SurveyIntakeCodeId);
      // For MVP phase 1, just get all evals in the program. Refining type of eval todo later.
      int templateSurveyId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.TemplateSurveyId) ?? 0;
      // Get all eval intakes in the selected program(s).
      var intakesInProgram = DbHelper.Reports.EvalViewer.GetSurveySelectList(selectedProject.JobNumber, selectedProgramIds, selectedEvalType);

      // If user hasn't selected specific intakes, select all of them.
      // Otherwise check that all selected intakes exist in the selected program(s).
      if (selectedIntakeIds.IsNullOrEmpty()) {
        selectedIntakeIds = intakesInProgram.ConvertAll(i => i.IntakeCodeId);
      } else {
        intakesInProgram.RemoveAll(s => !selectedIntakeIds.Contains(s.IntakeCodeId)); // Reduce list to only the selected intakes.
        if (intakesInProgram.Count != selectedIntakeIds.Count) return false; // Lists aren't the same, meaning selection contains invalid intake ids.
      }

      // Return the validated selections.
      QueryValues.ProjectInfo = selectedProject;
      QueryValues.ProgramJobIds = selectedProgramIds;
      QueryValues.IntakeIdList = selectedIntakeIds;
      QueryValues.TemplateSurveyId = templateSurveyId;
      QueryValues.EvalType = selectedEvalType;
      QueryValues.HasTextAnswers = intakesInProgram != null && intakesInProgram.Find(i => i.HasTextAnswers) != null;
      return true;
    }

    void DoPost(AjaxSubmitHelper ajax) {

      if (!SessionHelper.AppAccess.Insights.CanViewEvalViewer()) {
        ajax.AddDialogMessage("No Access to Eval Viewer.");
        return;
      }

      if (ajax.Action == AjaxAction.GetProjects) {
        GetProjectOptionsHtml(ajax);
        return;
      }

      // Job number should always be provided, to list programs.
      string selectedJobNumber = ajax.CheckFieldPlainText(FormFields.ProjectJobNumber, "", true);

      if (ajax.HasErrors) return;

      var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(selectedJobNumber, userInfo);

      if (projectInfo == null) {
        ajax.AddDialogMessage($"Project {selectedJobNumber.HTMLEncode()} Not Found.");
        return;
      }

      if (!SessionHelper.AppAccess.Reports.CanViewEvalsForProject(projectInfo)) {
        ajax.AddDialogMessage("No access to project.");
        return;
      }

      if (ajax.Action == AjaxAction.GetPrograms) {

        GetProgramOptionsHtml(ajax, projectInfo);

      } else if (ajax.Action == AjaxAction.GetSurveys) {

        var selectedProgramJobIds = WebHelper.GetFormIntListOrDefault(FormFields.ProgramJobIds);

        if (!selectedProgramJobIds.IsNullOrEmpty()) {
          GetSurveyOptionsHtml(ajax, projectInfo, selectedProgramJobIds);
        }
      }
    }

    public string GetSelectedProjectOption() {
      if (QueryValues.ProjectInfo == null) return "";
      return $@"<option value=""{QueryValues.ProjectInfo.JobNumber.HTMLEncode()}"">{QueryValues.ProjectInfo.JobNumber.HTMLEncode()}</option>";
    }

    public void GetProjectOptionsHtml(AjaxSubmitHelper ajax) {

      string projectSearchTerm = WebHelper.GetFormValue(FormFields.ProjectSearchTerm, "");
      var projectList = DbHelper.Projects.GetSearchResults(projectSearchTerm);

      var result = new WebHelper.Select2AjaxResult();
      foreach (var project in projectList.InfoList) {
        result.results.Add(new WebHelper.Select2AjaxResult.SelectOption(project.JobNumber,
          $"{project.FriendlyProjectTitle.ValueIfNullOrEmpty(project.ProjectName).HTMLEncode()} ({project.JobNumber.HTMLEncode()})"));
      }
      ajax.AddReturnValue(AjaxKey.OptionsHtml, result);
    }

    void GetProgramOptionsHtml(AjaxSubmitHelper ajax, DbHelper.Projects.ProjectInfo projectInfo) {

      if (!SessionHelper.IsUserRoleAdmin) {
        if (projectInfo.JobNumber.IsNullOrEmpty() || !SessionHelper.GetUserInfoOrNull().IsInProjectAccess(projectInfo.JobNumber)) {
          ajax.AddReturnValue("html", "<option value=\"\">[Select Project]</option>");
          return;
        }
      }

      var programList = DbHelper.Reports.EvalViewer.GetProgramSelectList(projectInfo);

      var sbHtml = new StringBuilder();

      foreach (var program in programList) {
        string startDateText = "";
        if (program.StartDateUtc != null) {
          startDateText = WebHelper.DisplayDate(SessionHelper.UtcToUserTime(program.StartDateUtc.Value)) + ": ";
        }
        sbHtml.Append($"<option value =\"{program.ProgramJobId}\"");
        if (!QueryValues.ProgramJobIds.IsNullOrEmpty() && QueryValues.ProgramJobIds.Contains(program.ProgramJobId)) sbHtml.Append(" selected");
        sbHtml.Append($">{(startDateText + program.JobName).HTMLEncode()}");
        if (program.SurveyCount != null) sbHtml.Append($" ({program.SurveyCount.Value})");
        sbHtml.Append("</option>");
      }

      ajax.AddReturnValue(AjaxKey.OptionsHtml, sbHtml.ToString());
    }

    void GetSurveyOptionsHtml(AjaxSubmitHelper ajax, DbHelper.Projects.ProjectInfo projectInfo, List<int> selectedProgramJobIds) {

      var surveyList = DbHelper.Reports.EvalViewer.GetSurveySelectList(projectInfo.JobNumber, selectedProgramJobIds, ConfigHelper.EvalType.All);

      var sbHtml = new StringBuilder();

      foreach (var intake in surveyList) {
        bool isSelected = QueryValues.IntakeIdList != null && QueryValues.IntakeIdList.Contains(intake.IntakeCodeId);
        sbHtml.Append("<option");
        if (isSelected) sbHtml.Append(" selected");
        sbHtml.Append($" value =\"{intake.IntakeCodeId}\">");
        sbHtml.Append(intake.IntakeCloseDateLocal.ToString("d MMM yyyy"));
        sbHtml.Append(": ");
        sbHtml.Append(intake.IntakeName.HTMLEncode());
        sbHtml.Append(" - ");
        sbHtml.Append(intake.SurveyPublicTitle.ValueIfNullOrEmpty(intake.SurveyTitle).HTMLEncode());
        sbHtml.Append("</option>");
      }

      ajax.AddReturnValue(AjaxKey.OptionsHtml, sbHtml.ToString());
    }

  }
}
