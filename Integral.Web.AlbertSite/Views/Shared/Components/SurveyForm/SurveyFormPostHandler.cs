using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Integral.Web;
using Integral.Web.WebHelpers;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.ViewComponents {

  // Handles the AJAX update/delete actions for SurveyForm. The legacy codebehind
  // ran this branch from Page_Load after building up survey/participant state.
  // ViewComponents only render, so the future Razor Page calls Handle() from
  // OnPostAsync() before invoking the ViewComponent. The handler shares
  // SurveyFormModel.Build() so the same setup logic populates the survey/
  // participant/question state the action needs.
  public static class SurveyFormPostHandler {

    public static void Handle(HttpContext httpContext, bool isJSPartial = false) {

      if (!SystemWeb.IsHttpPost) return;

      var model = SurveyFormModel.Build(httpContext, isJSPartial);

      if (WebHelper.IsRequestExiting()) return;
      if (model.PartInfo == null || model.QuestionsForPage == null) return;

      AjaxSubmitHelper.Process(ajax => {

        if (ajax.Action == SurveyFormModel.AjaxAction.Update) {

          // Save and respond with errors or which page to show next.
          ValidateSaveAndRedirect(model, ajax);
          return;

        } else if (model.IsDevelopmentPlan && ajax.Action == SurveyFormModel.AjaxAction.Delete) {

          if (model.CanDeleteDevPlan) {
            DeletePlan(model, ajax);
          } else {
            ajax.AddDialogMessage("Deletion not allowed.");
          }
          return;
        }
      });
    }

    private static void DeletePlan(SurveyFormModel model, AjaxSubmitHelper ajax) {

      try {
        bool deletedPlan = DbHelper.DevelopmentPlans.DeleteDevPlan(null, model.SurveyInfo);
        if (deletedPlan) {
          ajax.SetRedirectUrl(PathHelper.Pages.DevelopmentPlan(), "Development plan removed.", AjaxSubmitHelper.PageMessageType.SuccessToast);
        } else {
          ajax.AddErrorToast("Couldn't remove Development plan.");
        }
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("SurveyForm_DeletePlan")
          .WithProperty(ApplicationInsightsConstants.SurveyId, model.SurveyInfo?.SurveyId)
          .Track();
        ajax.AddErrorToast("Couldn't remove Development plan.");
        return;
      }
    }

    private static void ValidateSaveAndRedirect(SurveyFormModel model, AjaxSubmitHelper ajax) {

      int nextPage = WebHelper.GetFormValueIntOrDefault(SurveyFormModel.FormFields.NextPageNumber, model.CurrentPage + 1);

      // Limit what nextPage can be - only from 1 to TotalPages + 1.
      // If nextPage = TotalPages + 1 that means we are submitting the survey for completion.
      // Hack check: Make sure user only goes to TotalPages + 1 from the final page (i.e. only by clicking Submit on the final page).
      // If user isn't on the final page, next page can only be from 1 to TotalPages.
      nextPage = nextPage.Limit(1, model.QuestionsForPage.TotalPages + 1);
      if (model.CurrentPage < model.QuestionsForPage.TotalPages && nextPage > model.QuestionsForPage.TotalPages) {
        nextPage = model.QuestionsForPage.TotalPages;
      }

      // Only validate questions on the current page if we're going forward. If going back, just save without validation.
      bool doValidation = nextPage > model.CurrentPage;

      bool completingSurvey = nextPage > model.QuestionsForPage.TotalPages;

      // Get list of *visible* questions (not headings, not hidden for this participant)
      var visibleQuestions = new List<DbHelper.Questions.SurveyQuestionInfo>();

      // DevPlan: Number of text questions answered, and list of text questions.
      int textAnswerCount = 0;
      var textQuestions = model.QuestionsForPage.Questions.FindAll(
        q => q.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_MULTILINE.Value
          || q.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_SINGLELINE.Value);

      foreach (var qnInfo in model.QuestionsForPage.Questions) {

        if (qnInfo.IsHeading || !SurveyHelper.IsQuestionVisible(qnInfo, model.PartInfo)) continue;

        visibleQuestions.Add(qnInfo);

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

          if (!qnInfo.AnswerText.IsNullOrEmpty()) textAnswerCount++;

        } else {

          if (qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.OPTIONS_MULTIPLE.Value) {
            // Multiple choice - save multiple answers.

            qnInfo.AnswersMulti = new List<DbHelper.Questions.AnswersMulti>();
            if (!formValue.IsNullOrEmpty()) {
              foreach (string value in formValue.Split(',')) {
                if (!value.IsNullOrEmpty() && int.TryParse(value, out int code)) {
                  var codeInfo = qnInfo.GetCodeInfoByCodeOrNull(code);
                  if (codeInfo != null) {
                    qnInfo.AnswersMulti.Add(new DbHelper.Questions.AnswersMulti() {
                      AnswerCode = codeInfo.Code,
                      AnswerCodeId = codeInfo.CodeId,
                      ParticipantId = model.PartInfo.PartId
                    });
                  }
                }
              }
            }

          } else {
            // Numeric or NA answer.

            isAnswerNA = formValue.ToLower() == "na" ? true : false;
            if (!isAnswerNA && int.TryParse(formValue, out int testValue)) {
              var codeInfo = qnInfo.GetCodeInfoByCodeOrNull(testValue);
              if (codeInfo != null) {
                qnInfo.AnswerCode = codeInfo.Code;
                qnInfo.AnswerCodeId = codeInfo.CodeId;
              }
            }
          }
        }

        // Ranked optional text line is added to the current answer as a "text answer".
        if (qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.RANKED.Value) {
          qnInfo.AnswerText = WebHelper.GetFormValue("ans_text_" + qnInfo.QuestionId, "");
        }

        // If user is going forward a page, check here for errors in the current question.
        if (doValidation) {

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

          if (qnInfo.InputType == DbHelper.AnswerTypes.InputTypes.RANKED.Value && qnInfo.AddTextBox) {
            if (qnInfo.AnswerCode > 0) {
              if (qnInfo.AnswerText.IsNullOrEmpty()) {
                ajax.AddBadField("ans_text_" + qnInfo.QuestionId, "Please add a description for this ranked item, or remove the rank.");
              }
            } else {
              if (!qnInfo.AnswerText.IsNullOrEmpty()) {
                ajax.AddBadField("ans_text_" + qnInfo.QuestionId, "This description hasn't been assigned a rank. If not ranked, leave the box empty.");
              }
            }
          }
        }
      }

      if (ajax.HasErrors) return;

      if (doValidation) {

        CheckRankedQuestionPageRules(ajax, model.QuestionsForPage);

        if (ajax.HasErrors) return;
      }

      if (visibleQuestions.Count > 0) {
        DbHelper.Participants.UpdateSurveyResponse(model.PartInfo, visibleQuestions);
        DbHelper.Participants.UpdateSurveyLastUpdatedUtc(model.PartInfo, DateTime.UtcNow);
        if (doValidation) {
          DbHelper.Participants.UpdateSurveyValidatedUpToPageNumber(model.PartInfo, model.CurrentPage);
        }
      }

      if (model.IsDevelopmentPlan) {

        // Dev plans only have 1 page, so pagination is ignored, and submitting the page means completing the survey.
        // DevPlan "completion" is done differently to normal surveys.

        int percentCompleted = (int)((decimal)textAnswerCount / textQuestions.Count * 100);

        if (percentCompleted == 0 && textAnswerCount > 0) textAnswerCount = 1;
        if (percentCompleted == 100 && textAnswerCount < textQuestions.Count) textAnswerCount = 99;

        bool surveyCompleted = textAnswerCount == textQuestions.Count;

        DbHelper.Participants.UpdatePercentCompleted(model.PartInfo.PartId, percentCompleted);
        DbHelper.Participants.UpdateCompletedUtc(model.PartInfo.PartId, surveyCompleted ? (DateTime?)DateTime.UtcNow : null);

        if (model.AllowRedirect) {
          ajax.SetRedirectUrl(PathHelper.Pages.DevelopmentPlan());
        } else {
          ajax.AddSuccessToast("Development Plan Updated");
        }

        return;
      }

      if (!completingSurvey) {
        if (PathHelper.IsCurrentPage(PathHelper.Pages.SurveyQuestions())) {
          ajax.SetRedirectUrl(PathHelper.Pages.SurveyQuestions(model.PartInfo.SurveyUID, model.PartInfo.PartUID, nextPage));
        } else {
          ajax.SetRedirectUrl(PathHelper.Pages.ParticipantSurvey(model.PartInfo.SurveyUID, model.PartInfo.PartUID, nextPage));
        }
        return;
      }

      // Submitted final page, mark survey as completed.

      model.PartInfo.CompletedUTC = DateTime.UtcNow;
      DbHelper.Participants.UpdateCompletedUtc(model.PartInfo.PartId, model.PartInfo.CompletedUTC);

      if (model.PartInfo.ProgramJobId != null) {
        try {
          DbHelper.AlbertSurveys.PatchSurveysInProgram(model.PartInfo.ProgramJobId.Value, null);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("SurveyForm_PatchSurveysInProgram")
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, model.PartInfo.ProgramJobId.Value)
            .Track();
          // ignore for now
        }
      }

      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo = null;
      if (model.PartInfo.CoacheeId != null) coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(model.PartInfo.CoacheeId.Value);

      if (coacheeInfo != null) {

        if (model.PartInfo.IsSelf) {

          var sessionStats = DbHelper.CoachingSessions.GetSessionStats(null, model.PartInfo.CoacheeId.Value);
          if (coacheeInfo != null && sessionStats != null) {
            DbHelper.AlbertCoachees.UpdateSessionStatsAndTargetDates(null, coacheeInfo, sessionStats);
          }

        } else { // Rater

          if (model.SurveyInfo.IsStrictRaterLimits && model.SurveyInfo.RatersSuggestedMax == 1) {

            var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(model.PartInfo.JobNumber);

            if (projectInfo?.NotifySelfWhen180RaterCompleted == true) {

              var selfSurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(model.PartInfo.SurveyUID, model.PartInfo.SelfPartUID);

              if (selfSurveyInfo?.FoundParticipantBrief != null) {
                AlbertEmails.Send180RaterCompleted(projectInfo, coacheeInfo, selfSurveyInfo, model.PartInfo);
              }
            }
          }
        }
      }

      // Show corresponding "Completed" page.
      // If this is a rater, redirect to an external landing page.
      if (model.PartInfo.IsSelf) {
        ajax.SetRedirectUrl(PathHelper.Pages.GetSurveyCompletedURL(model.PartInfo, model.SurveyInfo), "Thank you for your participation!", AjaxSubmitHelper.PageMessageType.SuccessDialog);
      } else {
        ajax.SetRedirectUrl("https://www.integral.global/raters?email=" + SystemWeb.UrlEncode(model.PartInfo.Email));
      }
    }

    private static void CheckRankedQuestionPageRules(AjaxSubmitHelper ajax, DbHelper.Questions.QuestionList questionsForPage) {

      var rankedQns = questionsForPage.Questions.FindAll(q => q.InputType == DbHelper.AnswerTypes.InputTypes.RANKED.Value);
      if (rankedQns.IsNullOrEmpty()) return;

      var codesToAllocate = rankedQns[0].Codes.FindAll(c => c.Code >= 1);

      var codeFlags = new List<bool>(new bool[codesToAllocate.Count]);
      foreach (var qn in rankedQns) {
        if (qn.AnswerCode > 0 && qn.AnswerCode <= codesToAllocate.Count) {
          if (codeFlags[qn.AnswerCode.Value - 1]) {
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
