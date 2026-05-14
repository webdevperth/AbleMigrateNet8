using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Integral.Web.WebHelpers;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.UserControls {
  public partial class SurveyForm : System.Web.UI.UserControl {

    public class FormFields {
      public const string NextPageNumber = "NextPageNumber";
    }

    public class AjaxAction {
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

    public Dictionary<int, string> cachedOptionHtml = new Dictionary<int, string>(); // cacheing option HTML by AnswerTypeId

    // For dev plans.
    public bool IsDevelopmentPlan, IsViewingSharedSurvey, CanShareSurvey;
    public bool CanShowDevPlanReportSlideout, CanDeleteDevPlan, HasLatest360;
    public string LatestSurveyAISummary;
    public DbHelper.SurveyShare.SharedSurveysInfo SharedSurveyInfo;
    public DbHelper.OrganisationUsers.ProfileInfo ProfileInfo;

    // IsJSPartial is set to true by an embedding page which is itself a partial loaded from the front end
    // (e.g. CoacheeSurveyEmbed.aspx which is loaded as a JS partial on CoacheeSurveySummaryReport.aspx).
    public bool IsJSPartial { get; set; }

    protected void Page_Load(object sender, EventArgs e) {

      // When IsJSPartial is true, don't do a back-end redirect as it won't work.
      // Instead just show the default "not found" message to the user, or when saving show a toast message instead of redirect.
      AllowRedirect = !IsJSPartial;

      if (SessionHelper.IsUserLoggedIn && !SessionHelper.IsUserRoleLeader) {
        SessionHelper.SetUserRole(ConfigHelper.UserRole.Leader); // Required for survey pages.
        if (AllowRedirect) {
          WebHelper.Redirect(Request.RawUrl); // Reload the page as Leader.
          return;
        }
        return;
      }

      string urlSurveyUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId));
      if (urlSurveyUID.IsNullOrEmpty()) urlSurveyUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue("svid")); // 2021-02-25 Temporary fallback for older invite emails.

      string urlPartUID = DbHelper.Participants.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartUId));
      if (urlPartUID.IsNullOrEmpty()) urlPartUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue("part")); // 2021-02-25 Temporary fallback for older invite emails.

      if (urlSurveyUID.IsNullOrEmpty() || urlPartUID.IsNullOrEmpty()) {
        // If dev plan, take user back to the dev plan list, otherwise a messae will be shown.
        if (IsDevelopmentPlan && AllowRedirect) {
          WebHelper.Redirect(PathHelper.Pages.DevelopmentPlan());
          return;
        }
        return;
      }

      // Find survey & participant.
      SurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUID, urlPartUID);
      if (SurveyInfo?.FoundParticipantBrief != null) {
        PartInfo = DbHelper.Participants.GetParticipantInfo(null, SurveyInfo.SurveyId, SurveyInfo.FoundParticipantBrief.PartId);
      }

      if (PartInfo == null) {
        // If on DevPlanForm page, take user back to the dev plan list, otherwise return and the default message will be shown.
        if (PathHelper.IsCurrentPage(PathHelper.Pages.DevelopmentPlanForm()) && AllowRedirect) {
          WebHelper.Redirect(PathHelper.Pages.DevelopmentPlan());
          return;
        }
        return;
      }

      IsDevelopmentPlan = SurveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan;

      if (IsDevelopmentPlan) {

        DevPlanSetup();
        if (WebHelper.IsRequestExiting()) return;

      } else {

        SurveySetup();
        if (WebHelper.IsRequestExiting()) return;
      }

      SurveyInstructionsHtml = PartInfo.IsSelf ? SurveyInfo.Instructions_Self : SurveyInfo.Instructions_Rater;

      if (!SurveyInstructionsHtml.IsNullOrEmpty()) {

        SurveyInstructionsHtml = SurveyInstructionsHtml.ReplaceTags(
          new Dictionary<string, string>() {
            { SurveyHelper.InstructionsTags.SurveyName, SurveyInfo.SurveyName },
            { SurveyHelper.InstructionsTags.CloseDate, SurveyInfo.CloseDateRatersLocal.ToString("d MMM yyyy") },
            { SurveyHelper.InstructionsTags.FirstName, PartInfo.FirstName },
            { SurveyHelper.InstructionsTags.LastName, PartInfo.LastName },
            { SurveyHelper.InstructionsTags.SelfName, PartInfo.SelfName }
          }
        );

        // Remove inline styles.
        SurveyInstructionsHtml = Regex.Replace(SurveyInstructionsHtml, @"style\s*=\s*""[^""]*""", "");
      }

      int.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyPage), out CurrentPage); // Requested page to display.
      if (CurrentPage <= 0) CurrentPage = 1;

      // Get questions for this page.
      QuestionsForPage = DbHelper.Questions.GetQuestionsForPagePublic(PartInfo.SurveyUID, PartInfo.PartUID, CurrentPage, PartInfo.IsSelf, 1);

      CurrentPage = CurrentPage.Limit(1, QuestionsForPage.TotalPages);

      CanViewPrevButton = CurrentPage > 1;

      // Remove questions not shown to this participant.
      if (QuestionsForPage?.Questions != null) {
        QuestionsForPage.Questions.RemoveAll(q => !SurveyHelper.IsQuestionVisible(q, PartInfo));
        HasVisibleQuestions = QuestionsForPage.Questions.Exists(q => !q.IsHeading);
      }

      if (HasVisibleQuestions) {
        ShowSurveyRandomAnswersButton = SessionHelper.AppAccess.Surveys.CanViewRandomAnswersButton(QuestionsForPage.Questions);
        ShowSurveyDataEntryButton = SessionHelper.AppAccess.Surveys.CanViewDataEntryButton;
      }

      if (CurrentPage < QuestionsForPage.TotalPages) {
        SubmitButtonText = "Next Page";
      } else {
        SubmitButtonText = IsDevelopmentPlan ? "Save" : "Submit Your Answers";
      }

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          if (ajax.Action == AjaxAction.Update) {

            // Save and respond with errors or which page to show next.
            ValidateSaveAndRedirect(ajax);
            return;

          } else if (IsDevelopmentPlan && ajax.Action == AjaxAction.Delete) {

            if (CanDeleteDevPlan) {
              DeletePlan(ajax);
            } else {
              ajax.AddDialogMessage("Deletion not allowed.");
            }
            return;

          }
        });

        return;
      }

    }

    private void SurveySetup() {

      // If already completed, go to completed page.
      if (PartInfo.CompletedUTC != null) {
        if (AllowRedirect) {
          WebHelper.Redirect(PathHelper.Pages.GetSurveyCompletedURL(PartInfo, SurveyInfo));
          return;
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
            // View survey on the logged in survey page if possible.
            // Note if page is embedded (AllowResponseRedirect = false) it's ok to show the survey as-is (no need to return from here, just continue to show it).

            if (AllowRedirect) {
              WebHelper.Redirect(PathHelper.Pages.SurveyQuestions(PartInfo.SurveyUID, PartInfo.PartUID));
              return;
            }

          } else {
            // User not logged in. Redirect to either login page or registration page.

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
            return;
          }
          return;
        }
      }

      CanShareSurvey = SessionHelper.AppAccess.Surveys.CanShareSurvey(SurveyInfo) && !IsJSPartial;

      // Development Plan surveys are always open, they can always be edited.
      // The usual survey heading info is not shown.
      IsSurveyLinkValid = true;

      CanDeleteDevPlan = !IsJSPartial && SessionHelper.AppAccess.Surveys.CanDeleteDevPlan(SurveyInfo);

      // Determine if we can show the report slideout.
      // Only available if we're not a partial, since we may already be inside a slideout.
      if (!IsJSPartial) {
        // Get info required for slideout.
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

    private void DeletePlan(AjaxSubmitHelper ajax) {

      try {
        bool deletedPlan = DbHelper.DevelopmentPlans.DeleteDevPlan(null, SurveyInfo);
        if (deletedPlan) {
          ajax.SetRedirectUrl(PathHelper.Pages.DevelopmentPlan(), "Development plan removed.", AjaxSubmitHelper.PageMessageType.SuccessToast);
        } else {
          ajax.AddErrorToast("Couldn't remove Development plan.");
        }
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("SurveyForm_DeletePlan")
          .WithProperty(ApplicationInsightsConstants.SurveyId, SurveyInfo?.SurveyId)
          .Track();
        ajax.AddErrorToast("Couldn't remove Development plan.");
        return;
      }
    }

    public string GetInputName(DbHelper.Questions.SurveyQuestionInfo question) {
      return "q_" + question.QuestionId;
    }

    public void ValidateSaveAndRedirect(AjaxSubmitHelper ajax) {

      int nextPage = WebHelper.GetFormValueIntOrDefault(FormFields.NextPageNumber, CurrentPage + 1);

      // Limit what nextPage can be - only from 1 to TotalPages + 1.
      // If nextPage = TotalPages + 1 that means we are submitting the survey for completion.
      // Hack check: Make sure user only goes to TotalPages + 1 from the final page (i.e. only by clicking Submit on the final page).
      // If user isn't on the final page, next page can only be from 1 to TotalPages.
      nextPage = nextPage.Limit(1, QuestionsForPage.TotalPages + 1);
      if (CurrentPage < QuestionsForPage.TotalPages && nextPage > QuestionsForPage.TotalPages) {
        nextPage = QuestionsForPage.TotalPages; // Either by bug or hack, user is trying to submit from before the final page. Instead, take them to the final page.
      }

      // Only validate questions on the current page if we're going forward. If going back, just save without validation.
      bool doValidation = nextPage > CurrentPage;

      bool completingSurvey = nextPage > QuestionsForPage.TotalPages;

      // Save and respond with errors or which page to show next.

      // Get list of *visible* questions (not headings, not hidden for this participant)
      var visibleQuestions = new List<DbHelper.Questions.SurveyQuestionInfo>();

      // DevPlan: Number of text questions answered, and list of text questions.
      int textAnswerCount = 0;
      var textQuestions = this.QuestionsForPage.Questions.FindAll(
        q => q.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_MULTILINE.Value
          || q.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_SINGLELINE.Value);

      foreach (var qnInfo in QuestionsForPage.Questions) {

        if (qnInfo.IsHeading || !SurveyHelper.IsQuestionVisible(qnInfo, PartInfo)) continue;

        visibleQuestions.Add(qnInfo);

        // Save form values in qnInfo
        // Get code from form and attempt to get codeid for it.
        qnInfo.AnswerCode = null;
        qnInfo.AnswerCodeId = null;
        qnInfo.AnswerText = "";
        bool isAnswerNA = false;
        string formValue = "";

        string formFieldPrefix;
        switch (DbHelper.AnswerTypes.InputTypes.GetId(qnInfo.InputType)) {
          case DbHelper.AnswerTypes.InputTypes.Ids.TextMultiline:
          case DbHelper.AnswerTypes.InputTypes.Ids.TextSingleLine:
            formFieldPrefix = "ans_text_";
            break;
          default:
            formFieldPrefix = "ans_";
            break;
        }
        try {
          formValue = WebHelper.GetFormValue(formFieldPrefix + qnInfo.QuestionId, "");
        } catch (Exception) { } // Ignore any form errors, shouldn't happen unless form has been fiddled with.

        if (qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_MULTILINE.Value || qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_SINGLELINE.Value) {

          // Text answer.
          qnInfo.AnswerText = formValue.TrimWhitespace();

          if (!qnInfo.AnswerText.IsNullOrEmpty()) textAnswerCount++; // DevPlan: Number of text questions answered.

        } else {

          if (qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.OPTIONS_MULTIPLE.Value) {
            // Multiple choice - save multiple answers.

            qnInfo.AnswersMulti = new List<DbHelper.Questions.AnswersMulti>();
            if (!formValue.IsNullOrEmpty()) {
              foreach (string value in formValue.Split(',')) {
                if (!value.IsNullOrEmpty() && int.TryParse(value, out int code)) {
                  var codeInfo = qnInfo.GetCodeInfoByCodeOrNull(code); // check if code is valid.
                  if (codeInfo != null) {
                    qnInfo.AnswersMulti.Add(new DbHelper.Questions.AnswersMulti() {
                      AnswerCode = codeInfo.Code,
                      AnswerCodeId = codeInfo.CodeId,
                      ParticipantId = PartInfo.PartId
                    });
                  }
                }
              }
            }

          } else {
            // Numeric or NA answer.

            isAnswerNA = formValue.ToLower() == "na" ? true : false;
            if (!isAnswerNA && int.TryParse(formValue, out int testValue)) {
              var codeInfo = qnInfo.GetCodeInfoByCodeOrNull(testValue); // check if code is valid.
              if (codeInfo != null) {
                qnInfo.AnswerCode = codeInfo.Code; // got numeric code from form for this question.
                qnInfo.AnswerCodeId = codeInfo.CodeId; // valid, get codeid.
              }
            }
          }
        }

        // Ranked optional text line is added to the current answer as a "text answer".
        // So in this one case, an sv_Answers row has a numeric answer AND a text answer.
        if (qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.RANKED.Value) {
          qnInfo.AnswerText = WebHelper.GetFormValue("ans_text_" + qnInfo.QuestionId, "");
        }

        // If user is going forward a page, check here for errors in the current question.
        // Rules are ignored if going back a page.
        if (doValidation) {

          // Is question required?
          if (qnInfo.Required) {
            bool isUnanswered;
            if (qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_MULTILINE.Value || qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_SINGLELINE.Value) {
              isUnanswered = qnInfo.AnswerText.IsNullOrEmptyOrWhitespace();
            } else {
              isUnanswered = qnInfo.AnswerCode == null && !isAnswerNA;
            }
            if (isUnanswered) {
              ajax.AddDialogMessage("Please provide answers to all questions.<br/>Select 'NA' if a question does not apply.");
              return;
            }
          }

          // For Ranked questions with additional text, check if text is required or not.
          if (qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.RANKED.Value && qnInfo.AddTextBox) {
            if (qnInfo.AnswerCode > 0) {
              // When assigned a rank, the text is required.
              if (qnInfo.AnswerText.IsNullOrEmpty()) {
                ajax.AddBadField("ans_text_" + qnInfo.QuestionId, "Please add a description for this ranked item, or remove the rank.");
              }
            } else {
              // When not assigned a rank, text should be absent.
              if (!qnInfo.AnswerText.IsNullOrEmpty()) {
                ajax.AddBadField("ans_text_" + qnInfo.QuestionId, "This description hasn't been assigned a rank. If not ranked, leave the box empty.");
              }
            }
          }
        }
      }

      if (ajax.HasErrors) return;

      // If doing validation, here we validate questions that need to be assessed as a group not individually.
      if (doValidation) {

        // Rules for Ranked questions.
        CheckRankedQuestionPageRules(ajax, QuestionsForPage);

        if (ajax.HasErrors) return;
      }

      // At this point the page is validated. Save responses and go to to next page.

      if (visibleQuestions.Count > 0) {
        DbHelper.Participants.UpdateSurveyResponse(PartInfo, visibleQuestions);
        DbHelper.Participants.UpdateSurveyLastUpdatedUtc(PartInfo, DateTime.UtcNow);
        if (doValidation) {
          DbHelper.Participants.UpdateSurveyValidatedUpToPageNumber(PartInfo, CurrentPage);
        }
      }

      if (IsDevelopmentPlan) {

        // Dev plans only have 1 page, so pagination is ignored, and submitting the page means completing the survey.
        // DevPlan "completion" is done differently to normal surveys.

        // PercentCompleted indicates how many text questions are filled out.
        int percentCompleted = (int)((decimal)textAnswerCount / textQuestions.Count * 100);

        // "Correct" the percentage in case it doesn't make visual sense due to rounding.
        if (percentCompleted == 0 && textAnswerCount > 0) textAnswerCount = 1; // In case it rounded down to 0 but at least 1 question was answered.
        if (percentCompleted == 100 && textAnswerCount < textQuestions.Count) textAnswerCount = 99; // In case it rounded up to 100 but not all are answered.

        // "Completion" is based on whether all text questions are flled out.
        bool surveyCompleted = textAnswerCount == textQuestions.Count;

        DbHelper.Participants.UpdatePercentCompleted(PartInfo.PartId, percentCompleted);
        DbHelper.Participants.UpdateCompletedUtc(PartInfo.PartId, surveyCompleted ? (DateTime?)DateTime.UtcNow : null);

        // That's it, return to dev plan list. Dev Plans only have 1 page.
        if (AllowRedirect) {
          ajax.SetRedirectUrl(PathHelper.Pages.DevelopmentPlan());
        } else {
          // Can't redirect so show a toast instead.
          ajax.AddSuccessToast("Development Plan Updated");
        }

        return;
      }

      // If not completing survey, navigate to the chosen page.
      if (!completingSurvey) {
        // Display public survey page or logged in survey page.
        if (PathHelper.IsCurrentPage(PathHelper.Pages.SurveyQuestions())) {
          ajax.SetRedirectUrl(PathHelper.Pages.SurveyQuestions(PartInfo.SurveyUID, PartInfo.PartUID, nextPage));
        } else {
          ajax.SetRedirectUrl(PathHelper.Pages.ParticipantSurvey(PartInfo.SurveyUID, PartInfo.PartUID, nextPage));
        }
        return;
      }

      // Submitted final page, mark survey as completed.

      PartInfo.CompletedUTC = DateTime.UtcNow;
      DbHelper.Participants.UpdateCompletedUtc(PartInfo.PartId, PartInfo.CompletedUTC);

      // On survey completion, update survey stats for the program.
      if (PartInfo.ProgramJobId != null) {
        try {
          DbHelper.AlbertSurveys.PatchSurveysInProgram(PartInfo.ProgramJobId.Value, null);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("SurveyForm_PatchSurveysInProgram")
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, PartInfo.ProgramJobId.Value)
            .Track();
          // ignore for now
        }
      }

      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo = null;
      if (PartInfo.CoacheeId != null) coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(PartInfo.CoacheeId.Value);

      if (coacheeInfo != null) {

        if (PartInfo.IsSelf) {

          // For the coachee (self), perform standard "SessionStats" updates when survey is completed.
          var sessionStats = DbHelper.CoachingSessions.GetSessionStats(null, PartInfo.CoacheeId.Value);
          if (coacheeInfo != null && sessionStats != null) {
            DbHelper.AlbertCoachees.UpdateSessionStatsAndTargetDates(null, coacheeInfo, sessionStats);
          }

        } else { // Rater

          // For a rater, if the survey is a "180" (strict maximum 1 rater) and the project's
          // NotifySelfWhen180RaterCompleted flag is set, then send a notification to the self.
          if (SurveyInfo.IsStrictRaterLimits && SurveyInfo.RatersSuggestedMax == 1) {

            var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(PartInfo.JobNumber);

            if (projectInfo?.NotifySelfWhen180RaterCompleted == true) {

              var selfSurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(PartInfo.SurveyUID, PartInfo.SelfPartUID);

              if (selfSurveyInfo?.FoundParticipantBrief != null) {
                AlbertEmails.Send180RaterCompleted(projectInfo, coacheeInfo, selfSurveyInfo, PartInfo);
              }
            }
          }
        }
      }

      // Show corresponding "Completed" page.
      // If this is a rater, redirect to an external landing page.
      if (PartInfo.IsSelf) {
        ajax.SetRedirectUrl(PathHelper.Pages.GetSurveyCompletedURL(PartInfo, SurveyInfo), "Thank you for your participation!", AjaxSubmitHelper.PageMessageType.SuccessDialog);
      } else {
        // Is rater
        ajax.SetRedirectUrl("https://www.integral.global/raters?email=" + SystemWeb.UrlEncode(PartInfo.Email));
      }
    }

    private void CheckRankedQuestionPageRules(AjaxSubmitHelper ajax, DbHelper.Questions.QuestionList questionsForPage) {

      var rankedQns = questionsForPage.Questions.FindAll(q => q.InputType == DbHelper.AnswerTypes.InputTypes.RANKED.Value);
      if (rankedQns.IsNullOrEmpty()) return;

      var codesToAllocate = rankedQns[0].Codes.FindAll(c => c.Code >= 1);

      // Make sure all codes are allocated and only once each.
      var codeFlags = new List<bool>(new bool[codesToAllocate.Count]);
      foreach (var qn in rankedQns) {
        if (qn.AnswerCode > 0 && qn.AnswerCode <= codesToAllocate.Count) {
          if (codeFlags[qn.AnswerCode.Value - 1]) {
            // Rank has already been assigned to another question.
            ajax.AddDialogMessage($"Rank {qn.AnswerCode.Value} has been assigned more than once.<br/><br/>Please assign each rank to one statement only.");
            return;
          }
          codeFlags[qn.AnswerCode.Value - 1] = true;
        }
      }
      int missingCode = codeFlags.IndexOf(false);
      if (missingCode >= 0) {
        ajax.AddDialogMessage($"Rank {missingCode + 1} hasn't been assigned.<br/><br/>Please assign ranks {1}-{codesToAllocate.Count} to the statements on this page.");
        return;
      }
    }

  }
}
