using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Integral.Web;
using Integral.Web.WebHelpers;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the SurveyForm ViewComponent. Mirrors the public API of the legacy
  // UserControls/SurveyForm.ascx.cs codebehind so the .cshtml view can read the
  // same fields the original <% %> blocks did.
  public class SurveyFormModel {

    public static class FormFields {
      public const string NextPageNumber = "NextPageNumber";
    }

    public static class AjaxAction {
      public const string Update = "update";
      public const string Delete = "delete";
    }

    public int CurrentPage = 1;
    public string SubmitButtonText;
    public string SurveyInstructionsHtml;
    public bool IsSurveyLinkValid, IsSurveyClosed, HasVisibleQuestions, ShowSurveyRandomAnswersButton, ShowSurveyDataEntryButton;
    public bool ShowParticipantInfo, ShowSurveyTitle, CanViewPrevButton, AllowRedirect;

    public DbHelper.AlbertSurveys.SurveyInfo SurveyInfo = null;
    public DbHelper.Participants.ParticipantInfo PartInfo = null;
    public DbHelper.Questions.QuestionList QuestionsForPage = null;

    public Dictionary<int, string> cachedOptionHtml = new Dictionary<int, string>();

    public bool IsDevelopmentPlan, IsViewingSharedSurvey, CanShareSurvey;
    public bool CanShowDevPlanReportSlideout, CanDeleteDevPlan, HasLatest360;
    public string LatestSurveyAISummary;
    public DbHelper.SurveyShare.SharedSurveysInfo SharedSurveyInfo;
    public DbHelper.OrganisationUsers.ProfileInfo ProfileInfo;

    // IsJSPartial is set to true by an embedding page which is itself a partial loaded from the front end
    // (e.g. CoacheeSurveyEmbed which is loaded as a JS partial on CoacheeSurveySummaryReport).
    public bool IsJSPartial;

    // Mirrors the legacy Page_Load. Performs all the GET-time setup the codebehind
    // used to do, including any back-end redirects (these are short-circuited when
    // AllowRedirect is false, i.e. when embedded as a JS partial).
    public static SurveyFormModel Build(HttpContext httpContext, bool isJSPartial) {

      var model = new SurveyFormModel {
        IsJSPartial = isJSPartial,
        AllowRedirect = !isJSPartial
      };

      if (SessionHelper.IsUserLoggedIn && !SessionHelper.IsUserRoleLeader) {
        SessionHelper.SetUserRole(ConfigHelper.UserRole.Leader); // Required for survey pages.
        if (model.AllowRedirect) {
          WebHelper.Redirect(SystemWeb.RequestRawUrl); // Reload the page as Leader.
        }
        return model;
      }

      string urlSurveyUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId));
      if (urlSurveyUID.IsNullOrEmpty()) urlSurveyUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue("svid")); // 2021-02-25 Temporary fallback for older invite emails.

      string urlPartUID = DbHelper.Participants.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartUId));
      if (urlPartUID.IsNullOrEmpty()) urlPartUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue("part")); // 2021-02-25 Temporary fallback for older invite emails.

      if (urlSurveyUID.IsNullOrEmpty() || urlPartUID.IsNullOrEmpty()) {
        if (model.IsDevelopmentPlan && model.AllowRedirect) {
          WebHelper.Redirect(PathHelper.Pages.DevelopmentPlan());
        }
        return model;
      }

      // Find survey & participant.
      model.SurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUID, urlPartUID);
      if (model.SurveyInfo?.FoundParticipantBrief != null) {
        model.PartInfo = DbHelper.Participants.GetParticipantInfo(null, model.SurveyInfo.SurveyId, model.SurveyInfo.FoundParticipantBrief.PartId);
      }

      if (model.PartInfo == null) {
        if (PathHelper.IsCurrentPage(PathHelper.Pages.DevelopmentPlanForm()) && model.AllowRedirect) {
          WebHelper.Redirect(PathHelper.Pages.DevelopmentPlan());
        }
        return model;
      }

      model.IsDevelopmentPlan = model.SurveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan;

      if (model.IsDevelopmentPlan) {
        model.DevPlanSetup();
        if (WebHelper.IsRequestExiting()) return model;
      } else {
        model.SurveySetup();
        if (WebHelper.IsRequestExiting()) return model;
      }

      model.SurveyInstructionsHtml = model.PartInfo.IsSelf ? model.SurveyInfo.Instructions_Self : model.SurveyInfo.Instructions_Rater;

      if (!model.SurveyInstructionsHtml.IsNullOrEmpty()) {

        model.SurveyInstructionsHtml = model.SurveyInstructionsHtml.ReplaceTags(
          new Dictionary<string, string>() {
            { SurveyHelper.InstructionsTags.SurveyName, model.SurveyInfo.SurveyName },
            { SurveyHelper.InstructionsTags.CloseDate, model.SurveyInfo.CloseDateRatersLocal.ToString("d MMM yyyy") },
            { SurveyHelper.InstructionsTags.FirstName, model.PartInfo.FirstName },
            { SurveyHelper.InstructionsTags.LastName, model.PartInfo.LastName },
            { SurveyHelper.InstructionsTags.SelfName, model.PartInfo.SelfName }
          }
        );

        model.SurveyInstructionsHtml = Regex.Replace(model.SurveyInstructionsHtml, @"style\s*=\s*""[^""]*""", "");
      }

      int.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyPage), out model.CurrentPage); // Requested page to display.
      if (model.CurrentPage <= 0) model.CurrentPage = 1;

      model.QuestionsForPage = DbHelper.Questions.GetQuestionsForPagePublic(model.PartInfo.SurveyUID, model.PartInfo.PartUID, model.CurrentPage, model.PartInfo.IsSelf, 1);

      model.CurrentPage = model.CurrentPage.Limit(1, model.QuestionsForPage.TotalPages);

      model.CanViewPrevButton = model.CurrentPage > 1;

      if (model.QuestionsForPage?.Questions != null) {
        model.QuestionsForPage.Questions.RemoveAll(q => !SurveyHelper.IsQuestionVisible(q, model.PartInfo));
        model.HasVisibleQuestions = model.QuestionsForPage.Questions.Exists(q => !q.IsHeading);
      }

      if (model.HasVisibleQuestions) {
        model.ShowSurveyRandomAnswersButton = SessionHelper.AppAccess.Surveys.CanViewRandomAnswersButton(model.QuestionsForPage.Questions);
        model.ShowSurveyDataEntryButton = SessionHelper.AppAccess.Surveys.CanViewDataEntryButton;
      }

      if (model.CurrentPage < model.QuestionsForPage.TotalPages) {
        model.SubmitButtonText = "Next Page";
      } else {
        model.SubmitButtonText = model.IsDevelopmentPlan ? "Save" : "Submit Your Answers";
      }

      return model;
    }

    private void SurveySetup() {

      // If already completed, go to completed page.
      if (PartInfo.CompletedUTC != null) {
        if (AllowRedirect) {
          WebHelper.Redirect(PathHelper.Pages.GetSurveyCompletedURL(PartInfo, SurveyInfo));
        }
        return;
      }

      // If user is logged in, check they're not accessing another user's survey (selfs only).
      if (SessionHelper.IsUserLoggedIn && PartInfo.IsSelf && PartInfo.UserId != SessionHelper.GetUserIdOrNull()) {
        return;
      }

      // If showing public page, check if survey should be viewed in participant portal instead.
      if (PathHelper.IsCurrentPage(PathHelper.Pages.ParticipantSurvey())) {
        if (SurveyHelper.ShowSurveyLoggedIn(SurveyInfo, PartInfo)) {

          if (SessionHelper.IsUserLoggedIn) {
            if (AllowRedirect) {
              WebHelper.Redirect(PathHelper.Pages.SurveyQuestions(PartInfo.SurveyUID, PartInfo.PartUID));
              return;
            }

          } else {
            var user = DbHelper.AbleUser.GetUserByIdOrNull(PartInfo.UserId ?? 0, DbHelper.AbleUser.RegisteredFilter.Any);
            if (user != null && !user.IsRegistered) {
              if (user.InviteCode.IsNullOrEmpty()) {
                int invitedByUserId = DbHelper.AbleUser.GetInvitedByUserId(user);
                DbHelper.AbleUser.UpdateInviteDetails(user, invitedByUserId);
              }
              SessionHelper.RedirectIfNotLoggedIn(PathHelper.Pages.RegisterInvited(user.InviteCode));
            } else {
              SessionHelper.RedirectIfNotLoggedIn(PathHelper.Pages.Home());
            }
            return;
          }
        }
      }

      IsSurveyClosed = SurveyInfo.IsClosed || (PartInfo.IsSelf && SurveyInfo.IsClosedSelf);

      if (IsSurveyClosed) return;

      IsSurveyLinkValid = true;
      ShowSurveyTitle = true;
      ShowParticipantInfo = true;
    }

    private void DevPlanSetup() {

      // For devplans, and if not shown as partial, ensure we're on the DevelopmentPlanForm page (i.e. not the normal survey page).
      if (IsDevelopmentPlan && !IsJSPartial) {
        if (!PathHelper.IsCurrentPage(PathHelper.Pages.DevelopmentPlanForm())) {
          WebHelper.Redirect(PathHelper.Pages.DevelopmentPlanForm(PartInfo.SurveyUID, PartInfo.PartUID));
          return;
        }
      }

      // If this survey doesn't belong to the current user,
      // it can only be viewed if it was shared with the current user.
      if (SurveyInfo.FoundParticipantBrief.UserId != SessionHelper.GetUserIdOrNull()) {
        SurveyHelper.GetSharedSurveyInfo(out SharedSurveyInfo, out IsViewingSharedSurvey);
        if (!IsViewingSharedSurvey) {
          if (AllowRedirect) {
            WebHelper.Redirect(PathHelper.Pages.DevelopmentPlan());
          }
          return;
        }
      }

      CanShareSurvey = SessionHelper.AppAccess.Surveys.CanShareSurvey(SurveyInfo) && !IsJSPartial;

      // Development Plan surveys are always open, they can always be edited.
      IsSurveyLinkValid = true;

      CanDeleteDevPlan = !IsJSPartial && SessionHelper.AppAccess.Surveys.CanDeleteDevPlan(SurveyInfo);

      // Determine if we can show the report slideout.
      // Only available if we're not a partial, since we may already be inside a slideout.
      if (!IsJSPartial) {
        ProfileInfo = DbHelper.OrganisationUsers.GetProfileInfo(SurveyInfo.FoundParticipantBrief.UserGuid.Value);
        HasLatest360 = ProfileInfo?.UserActivity?.Latest360PartId != null;
        if (HasLatest360) {
          CanShowDevPlanReportSlideout = true;
          LatestSurveyAISummary = DbHelper.Participants.ParticipantAICoachSummary(ProfileInfo.UserActivity.Latest360PartId.Value);
        }
      }
    }

    public string GetPlanDateDisplay() {

      DateTime? planClosed = SurveyInfo.FoundParticipantBrief.PersonalSurveyClosedUtc;
      string dateText = SurveyInfo.CreatedUtc.ToString("MMMM yyyy");

      if (planClosed == null || planClosed > DateTime.Now) {
        dateText += " and Ahead";
      } else {
        dateText += $" to {planClosed.ToString("MMMM yyyy")}";
      }

      return dateText;
    }

    public string GetInputName(DbHelper.Questions.SurveyQuestionInfo question) {
      return "q_" + question.QuestionId;
    }

  }
}
