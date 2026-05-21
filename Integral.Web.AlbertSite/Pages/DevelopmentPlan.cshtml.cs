using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class DevelopmentPlan : AppCode.PageBaseClasses.LoggedInPageModel {

    public List<DbHelper.DevelopmentPlans.PlanInfo> PlansForUser;
    public DbHelper.DevelopmentPlans.PlanInfo CurrentPlan;
    public List<DbHelper.DevelopmentPlans.PlanQuestion> CurrentPlanAnswers;
    public List<DbHelper.SurveyShare.SharedSurveysInfo> SharedSurveysWithUser, SharedSurveysByUser;
    public enum SurveyShareEnum { SharedWithUser, SharedByUser }

    public class FormFields {
      public const string ShareSurveyId = "ShareSurveyId";
    }
    public class AjaxAction {
      public const string Create = "Create";
      public const string UnshareSurvey = "UnShareSurvey";
    }

    public class ReturnValue {
      public const string SurveyUID = "SurveyUID";
      public const string UnsharedResult = "UnsharedResult";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Development Plans";

      PlansForUser = DbHelper.DevelopmentPlans.GetPlansForUser(SessionHelper.GetUserIdOrNull().Value, DbHelper.DevelopmentPlans.GetPlansStatus.Any);

      // Find current (open) plan survey.
      CurrentPlan = PlansForUser.Find(plan => plan.ClosedUtc == null);

      if (CurrentPlan != null) {
        // PlansForUser.RemoveAll(plan => plan.SurveyPartId == CurrentPlan.SurveyPartId);
        CurrentPlanAnswers = DbHelper.DevelopmentPlans.GetPlanQuestionsAndAnswers(CurrentPlan);
      }

      SharedSurveysByUser = new List<DbHelper.SurveyShare.SharedSurveysInfo>();
      SharedSurveysWithUser = new List<DbHelper.SurveyShare.SharedSurveysInfo>();
      var sharedSurveys = DbHelper.SurveyShare.GetSharedSurveysForUser(userInfo.UserId, DbHelper.SurveyShare.SharedSurveyTypeEnum.All);

      if (!sharedSurveys.IsNullOrEmpty()) {
        foreach (var ss in sharedSurveys) {
          if (ss.SurveyInfo.SurveyTypeCode != ConfigHelper.SurveyTypeCodes.DevPlan) continue; // Not include surveys that aren Dev Plans

          if (ss.UserIdSharedBy == userInfo.UserId) {
            SharedSurveysByUser.Add(ss);
          } else {
            SharedSurveysWithUser.Add(ss);
          }
        }
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.Create:
              CreateNewPlan(ajax);
              return;

            case AjaxAction.UnshareSurvey:
              UnshareSurvey(ajax);
              break;

          }
        });
        return new EmptyResult();
      }

      return Page();
    }


    public void UnshareSurvey(AjaxSubmitHelper ajax) {
      int? surveyShareId = WebHelper.GetFormValue(FormFields.ShareSurveyId).ToIntOrNull();

      if (surveyShareId == null) {
        ajax.AddDialogMessage("Invalid survey to unshare");
        return;
      }

      bool unshared = DbHelper.SurveyShare.UnshareSurvey(surveyShareId.Value);

      if (unshared) {
        ajax.AddSuccessToast("Survey unshared");
      } else {
        ajax.AddErrorToast("Couldn't unshare survey.");
      }

      ajax.AddReturnValue(ReturnValue.UnsharedResult, unshared);
    }


    public string GetPlanDateDisplay(DateTime? closedUtc) {
      return closedUtc.ToString("MMMM yyyy")
        + (closedUtc == null || closedUtc > DateTime.Now ? " onwards" : $" to {closedUtc.ToString("MMMM yyyy")}");
    }

    public string GetGoalResponseText(string goalText) {
      if (goalText == null) return "";
      int maxLength = 200;
      if (goalText.Length <= maxLength) return goalText;
      return goalText.Substring(0, maxLength) + "...";
    }

    public string GetCoachLinkHtml(DbHelper.DevelopmentPlans.PlanInfo plan) {
      return WebHelper.GetAvatarForTable_User(
        PathHelper.Images.UserPhoto(plan.CoachFirstName, plan.CoachLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true),
        plan.CoachFirstName + " " + plan.CoachLastName, plan.CoachUserId);
    }

    void CreateNewPlan(AjaxSubmitHelper ajax) {

      int newSurveyId;

      try {
        newSurveyId = DbHelper.DevelopmentPlans.CreateDevPlanSurvey(SessionHelper.GetUserInfoOrNull());
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CreateNewPlan_CreateDevPlanSurvey")
          .FromSession()
          .Track();
        ajax.AddDialogMessage("There was a problem creating a new plan. Please try again later.", ex);
        return;
      }

      // Redirect to edit new plan.
      try {
        var newPlan = DbHelper.DevelopmentPlans.GetPlanSurvey(newSurveyId);
        ajax.SetRedirectUrl(PathHelper.Pages.DevelopmentPlanForm(newPlan.SurveyUniqueId, newPlan.SurveyPartUniqueId));
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CreateNewPlan_GetPlanSurvey")
          .FromSession()
          .WithProperty("NewSurveyId", newSurveyId)
          .Track();
        ajax.AddDialogMessage("There was a problem finding the new plan. Please try again later.", ex);
      }
    }

    public string GetSurveyInfoHtml(List<DbHelper.SurveyShare.SharedSurveysInfo> sharedSurveyInfo, SurveyShareEnum surveyShareType) {

      string tableBody = "";

      foreach (var thisSurvey in sharedSurveyInfo) {
        string nameColumn = "";
        if (surveyShareType == SurveyShareEnum.SharedByUser) {
          nameColumn = $"<b>{thisSurvey.GetSharedWithFullName()}</b><br />{thisSurvey.EmailSharedWith}";
        } else {
          nameColumn = $"<b>{thisSurvey.GetSharedByFullName()}</b><br />{thisSurvey.EmailSharedBy}";
        }

        tableBody += $@"
            <tr tabindex=""0"" data-{WebHelper.DataAttrName.SurveyShareId}=""{thisSurvey.SurveyShareId}"" data-rowlink-url=""{PathHelper.Pages.DevelopmentPlanForm(thisSurvey.SurveyInfo.SurveyUID, thisSurvey.SurveyInfo.PartUniqueId, thisSurvey.SurveyShareId)}"">
              <td>{nameColumn}</td>
              <td class=""type-date-range"">{GetPlanDateDisplay(thisSurvey.SurveyInfo.PersonalSurveyClosedUtc)}</td>
              <td>{GetGoalResponseText(thisSurvey.SurveyInfo.GoalText).HTMLEncode()}</td>
              {(surveyShareType == SurveyShareEnum.SharedByUser ? $"<td>{WebHelper.GetUnshareSurveyButtonHtml(thisSurvey, WebHelper.ShareSurveyButtonTypeEnum.ActionButton, WebHelper.CSSClasses.UnshareSurveyClass)}</td>" : "")}
            </tr>";
      }

      return $@"
          <div class=""table-responsive"">
            <table id =""tbl{surveyShareType}"" class=""table table-bordered table-hover table-rowlink"" data-rowlink-url=""{PathHelper.Pages.DevelopmentPlanForm()}"">
              <thead>
                <tr>
                  <th class=""type-description"">Shared {(surveyShareType == SurveyShareEnum.SharedByUser ? "with" : "by")}</th>
                  <th class=""type-date-range"">Plan Period</th>
                  <th>Goals</th>
                  {(surveyShareType == SurveyShareEnum.SharedByUser ? "<th class=\"w100\"></th>" : "")}
                </tr>
              </thead>
              <tbody>
                {tableBody}
              </tbody>
            </table>
          </div>";
    }

  }
}
