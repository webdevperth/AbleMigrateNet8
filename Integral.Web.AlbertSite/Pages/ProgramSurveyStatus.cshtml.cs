using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProgramSurveyStatus : AppCode.PageBaseClasses.ProgramPageBase {

    public class FormKeys {
      public const string CloseDateSelf = "CloseDateSelf";
      public const string CloseDateRaters = "CloseDateRaters";
    }
    public class AjaxAction {
      public const string UpdateCloseDateSelf = "UpdateCloseDateSelf";
      public const string UpdateCloseDateRaters = "UpdateCloseDateRaters";
      public const string SendReminders = "sendreminders";
    }

    public List<DbHelper.AlbertSurveys.SurveySelectItem> SurveyList;
    public DbHelper.AlbertSurveys.SurveyInfo FoundSurveyInfo;
    public DbHelper.Participants.ParticipantList ParticipantList;

    public int incompleteParts = 0;
    private string urlSelectedSurveyUId; // Survey to show the details for.
    private int urlSelectedIntakeNumber;

    public DbHelper.Questions.SurveyQuestionInfo questionListItem;
    public int questionRowCount = 0;
    public string qnRowClass, questionTextClass;
    public string answerTextClass, answerText;
    public string answerNumericClass, answerNumeric;
    public string questionNumber;
    public ActiveTabEnum activeTab = ActiveTabEnum.Participants;
    public bool CanViewParticipants, CanSendSurvey, CanChangeCloseDates;
    public bool NoSurveyVisible = false;
    public bool SurveyInfoVisible = false;

    public enum ActiveTabEnum { Participants = 1, Responses = 2 }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Program Survey Status";

      CanSendSurvey = SessionHelper.AppAccess.Programs.CanSendSurveys(ProgramInfo);
      CanChangeCloseDates = SessionHelper.AppAccess.Surveys.CanChangeCloseDate(ProgramInfo);

      WebHelper.GetQueryStringSurveyUIDAndIntakeNumber(PathHelper.AbleUrlKeys.SurveyUId, out urlSelectedSurveyUId, out urlSelectedIntakeNumber);

      SurveyList = DbHelper.AlbertSurveys.GetSurveySelectForProgram(ProgramInfo.ProgramJobId, urlSelectedSurveyUId, urlSelectedIntakeNumber, out FoundSurveyInfo, 2);

      if (SurveyList == null || SurveyList.Count == 0) {
        NoSurveyVisible = true;
        return Page();
      }

      if (!urlSelectedSurveyUId.IsNullOrEmpty() && FoundSurveyInfo == null) {
        WebHelper.Redirect(PathHelper.Pages.ProjectPrograms(ProgramInfo.ProgramJobNumber));  // Invalid survey uid given - go back to list.
        return new EmptyResult();
      }

      if (FoundSurveyInfo != null) {
        ParticipantList = DbHelper.Participants.GetSelfList(FoundSurveyInfo.SurveyId, FoundSurveyInfo.IntakeNumber);
        CanViewParticipants = SessionHelper.AppAccess.Programs.CanViewProgramParticipants(ProgramInfo);
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          switch (PageAjaxAction) {
            case AjaxAction.UpdateCloseDateSelf:
              if (!CanChangeCloseDates) {
                ajax.AddErrorToast("Operation not allowed");
                return;
              }
              if (FoundSurveyInfo != null) UpdateCloseDate(ajax, true);
              break;
            case AjaxAction.UpdateCloseDateRaters:
              if (!CanChangeCloseDates) {
                ajax.AddErrorToast("Operation not allowed");
                return;
              }
              if (FoundSurveyInfo != null) UpdateCloseDate(ajax, false);
              break;
            case AjaxAction.SendReminders:
              if (!CanSendSurvey) {
                ajax.AddErrorToast("Operation not allowed");
                return;
              }
              if (ParticipantList != null) SendReminders(ParticipantList);
              break;
          }
        });
        return new EmptyResult();
      }

      // Activate participant tab.
      activeTab = ActiveTabEnum.Participants;

      if (FoundSurveyInfo != null) {
        SurveyInfoVisible = true;
      }

      return Page();
    }

    public string GetParticipantRowLinkUrl() {
      // If user can only view profile as read only, redirect to profile tab.
      if (SessionHelper.AppAccess.Participants.CanViewNonProfileTabs()) {
        return PathHelper.Pages.CoacheeEdit(PathHelper.CoacheeTabEnum.surveys, null);
      } else {
        return PathHelper.Pages.CoacheeEdit(PathHelper.CoacheeTabEnum.settings, null);
      }
    }

    public string GetDatePickerCloseDateSelf() {
      if (FoundSurveyInfo == null) return "";
      return FoundSurveyInfo.CloseDateSelfLocal.ToString("dd/MM/yyyy");
    }

    public string GetDatePickerCloseDateRaters() {
      if (FoundSurveyInfo == null) return "";
      return FoundSurveyInfo.CloseDateRatersLocal.ToString("dd/MM/yyyy");
    }

    public string GetSurveyListOptionSelected(DbHelper.AlbertSurveys.SurveySelectItem survey) {
      return urlSelectedSurveyUId + "-" + urlSelectedIntakeNumber == survey.SurveyUId + "-" + survey.IntakeNumber ? "selected" : "";
    }

    public string GetSurveyListOptionValue(DbHelper.AlbertSurveys.SurveySelectItem survey) {
      return survey.SurveyUId + "-" + survey.IntakeNumber;
    }

    public string GetSurveyListOptionText(DbHelper.AlbertSurveys.SurveySelectItem survey) {
      return
        WebHelper.DisplayDate(survey.IntakeCloseDateLocal) + ": "
        + survey.SurveyTitle.HTMLEncode();
    }

    public string GetCloseDateSelf() {
      if (FoundSurveyInfo == null) return "";
      string rtn = WebHelper.DisplayDate(FoundSurveyInfo.CloseDateSelfLocal);
      //if (foundSurveyInfo.IsClosed) rtn += " (Closed)";
      return rtn;
    }

    public string GetCloseDateRaters() {
      if (FoundSurveyInfo == null) return "";
      string rtn = WebHelper.DisplayDate(FoundSurveyInfo.CloseDateRatersLocal);
      if (FoundSurveyInfo.IsClosed) rtn += " (Closed)";
      return rtn;
    }

    public string GetPartName(DbHelper.Participants.ParticipantInfo participant) {
      string html = participant.FullName.HTMLEncode();
      if (participant.CoacheeId != null) {
        html.SurroundWith(
          @"<a class=""survey-status-link"" href="""
          + PathHelper.Pages.CoacheeEdit(participant.CoacheeId.Value, PathHelper.CoacheeTabEnum.surveys, participant.SurveyUID, participant.PartUID)
          + @""">", "</a>");
      }
      return html;
    }

    void UpdateCloseDate(AjaxSubmitHelper ajax, bool updatingSelfCloseDate) {

      if (FoundSurveyInfo == null) return;

      string formCloseDateKey = updatingSelfCloseDate ? FormKeys.CloseDateSelf : FormKeys.CloseDateRaters;

      var dt = ajax.GetDatePickerDateUnspecified(formCloseDateKey, "Close Date", true, "Invalid Close Date");
      if (dt == null || ajax.HasErrors) return;

      var formCloseDate = dt.Value;

      DateTime closeDateSelf = FoundSurveyInfo.CloseDateSelfLocal;
      DateTime closeDateRaters = FoundSurveyInfo.CloseDateRatersLocal;

      // If survey has no raters, or if it's only raters (no self) then force both close dates to be set the same.
      bool datesMustBeSame = FoundSurveyInfo.IsRatersOnly || FoundSurveyInfo.FeedbackOption == DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters;

      if (updatingSelfCloseDate) {
        closeDateSelf = formCloseDate;
        if (datesMustBeSame) closeDateRaters = closeDateSelf;
      } else {
        closeDateRaters = formCloseDate;
        if (datesMustBeSame) closeDateSelf = closeDateRaters;
      }

      if (closeDateSelf > closeDateRaters) {
        ajax.AddBadField(formCloseDateKey, "Self Close Date cannot be later than the Rater Close Date.");
        return;
      }

      try {
        DbHelper.AlbertSurveys.UpdateIntakeCloseDates(FoundSurveyInfo.SurveyId, FoundSurveyInfo.IntakeNumber, closeDateSelf, closeDateRaters);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UpdateIntakeCloseDates")
          .FromSession()
          .WithProperty("SurveyId", FoundSurveyInfo.SurveyId)
          .WithProperty("IntakeNumber", FoundSurveyInfo.IntakeNumber)
          .WithProperty("JobNumber", ProjectInfo?.JobNumber)
          .Track();
        ajax.AddBadField("date", "Unable to update Close Date. Please try again later.");
        return;
      }

      ajax.SetReloadPage("Close Date has been updated.", AjaxSubmitHelper.PageMessageType.SuccessToast);
    }

    void SendReminders(DbHelper.Participants.ParticipantList participantList) {

      // Go through rater list and send reminders to each incomplete one.
      foreach (var partInfo in participantList.Participants) {

        if (partInfo.CompletedUTC != null || partInfo.CoacheeId == null) continue;

        int coacheeId = (int)partInfo.CoacheeId;
        bool emailSent = false;

        try {

          var coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeId);
          var coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(coacheeInfo.CoachUserId);

          emailSent = AlbertEmails.SendSelfInvitationEmail(
            ProjectInfo, FoundSurveyInfo, "",
            partInfo.PartId, partInfo.PartUID,
            partInfo.CoacheeId, ProgramInfo.ProgramJobId,
            ProjectInfo.ComputedSenderEmailName.ValueIfNullOrEmpty(FoundSurveyInfo.ComputedSenderEmailName),
            ProjectInfo.ComputedSenderEmailAddress.ValueIfNullOrEmpty(FoundSurveyInfo.ComputedSenderEmailAddress),
            new AlbertEmails.Addressee(coacheeInfo.FirstName, coacheeInfo.LastName, coacheeInfo.EmailAddress),
            coachInfo == null || coachInfo.UserId == ConfigHelper.UserId.Unassigned
              ? new AlbertEmails.Addressee(userInfo)
              : new AlbertEmails.Addressee(coachInfo.FirstName, coachInfo.LastName, coachInfo.EmailAddress),
            coacheeInfo.CompanyName,
            false, false);

        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("SendReminders_SendSelfInvitationEmail")
            .FromSession()
            .WithProperty("SurveyId", FoundSurveyInfo.SurveyId)
            .WithProperty("PartId", partInfo.PartId)
            .WithProperty("CoacheeId", coacheeId)
            .WithProperty("JobNumber", ProjectInfo?.JobNumber)
            .Track();
        }

        if (userInfo.IsUserTester) break; // test send 1 only
      }
    }

  }
}
