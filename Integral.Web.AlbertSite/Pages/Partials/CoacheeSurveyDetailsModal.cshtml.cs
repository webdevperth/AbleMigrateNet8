using System;
using System.Collections.Generic;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class CoacheeSurveyDetailsModal : AppCode.PageBaseClasses.CoacheeInfoBase {

    public class AjaxAction {
      public const string UpdateCloseDateSelf = "UpdateCloseDateSelf";
      public const string UpdateCloseDateRaters = "UpdateCloseDateRaters";
      public const string SendReminders = "sendreminders";
      public const string SendReports = "SendReports";
    }

    public class FormFields {
      public const string CloseDateSelf = "CloseDateSelf";
      public const string CloseDateRaters = "CloseDateRaters";
      public const string SendWebReport = "SendWebReport";
      public const string SendPDFReport = "SendPDFReport";
    }

    public List<DbHelper.AlbertSurveys.SurveyInfo> SurveyList;
    public DbHelper.Participants.ParticipantList RaterList;
    public DbHelper.Questions.QuestionList QuestionList;

    public int IncompleteRaters;
    public DbHelper.AlbertSurveys.SurveyInfo SurveyInfo = null;
    public DbHelper.Participants.ParticipantInfo CoacheePartInfo = null;
    public string UrlSelectedSurveyUId, UrlSelectedPartUId; // Survey to show the details for.
    public bool IsIOSSurvey = false;
    public bool ReportsAvailable, CanViewSurveyReports, CanEmailSurveyReports, CanChangeCloseDates, CanEditSurveyInJarvis;
    public bool CanViewRaters, CanInviteRaters;
    public bool DefaultSendOnlineReport, DefaultSendPDFReport;

    public int questionRowCount = 0;
    public string qnRowClass, questionTextClass;
    public string answerTextClass, answerText;
    public string answerNumericClass, answerNumeric;
    public string questionNumber;
    public enum ActiveTabEnum { Raters = 1, Responses = 2 }
    public ActiveTabEnum ActiveTab = ActiveTabEnum.Raters;

    public bool Content_ShowNoSurvey = false;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      CanEditSurveyInJarvis = SessionHelper.AppAccess.Surveys.CanEditInJarvis();

      PathHelper.Pages.GetCoacheeSurveyUIDs(out UrlSelectedSurveyUId, out UrlSelectedPartUId);

      // Host page can pass either a survey UID or a SharedSurveyId.

      if (!UrlSelectedSurveyUId.IsNullOrEmpty()) {
        // Passed survey uid.

        SurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfoForUser(UrlSelectedSurveyUId, SessionHelper.UserInfo);
        if (SurveyInfo == null) {
          ShowNoInformation();
          return new EmptyResult();
        }

        if (SurveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.IOS) {

          IsIOSSurvey = true;

          if (!SessionHelper.AppAccess.Surveys.CanViewIOSDetails(SurveyInfo)) {
            ShowNoInformation();
            return new EmptyResult();
          }
          SurveyList = new List<DbHelper.AlbertSurveys.SurveyInfo>() { SurveyInfo };
          ActiveTab = ActiveTabEnum.Responses;
          CanViewSurveyReports = SessionHelper.AppAccess.Surveys.CanViewReports(SurveyInfo);
          ReportsAvailable = CanViewSurveyReports && SurveyInfo.IsOrgReportAvailable_Online;
        }
      }

      if (!IsIOSSurvey) {

        if (!SessionHelper.AppAccess.Surveys.CanViewDetails(ProgramInfo, CoacheeInfo, SurveyInfo)) {
          ShowNoInformation();
          return new EmptyResult();
        }

        if (IsViewingSharedSurvey) {

          SurveyList = DbHelper.AlbertSurveys.GetSurveyInfoListForUser(
            SharedSurveyInfo.UserIdSharedBy.Value, SharedSurveyInfo.SurveyInfo.SurveyUID, SharedSurveyInfo.SurveyInfo.PartUniqueId,
            DbHelper.AlbertSurveys.OnlyViewerCompatible.No,
            DbHelper.AlbertSurveys.OnlyWhereLeaderCanView360Report.No,
            out SurveyInfo);

        } else {

          CanViewRaters = SessionHelper.AppAccess.Surveys.CanViewRaters();
          CanInviteRaters = SessionHelper.AppAccess.Surveys.CanInviteRaters(ProgramInfo, SurveyInfo);
          CanChangeCloseDates = SessionHelper.AppAccess.Surveys.CanChangeCloseDate(ProgramInfo);
          CanEmailSurveyReports = SessionHelper.AppAccess.Surveys.CanEmailReports(ProgramInfo, CoacheeInfo);

          SurveyList = DbHelper.AlbertSurveys.GetSurveyInfoListForUser(
            CoacheeInfo.UserId ?? 0, UrlSelectedSurveyUId, UrlSelectedPartUId,
            DbHelper.AlbertSurveys.OnlyViewerCompatible.No,
            DbHelper.AlbertSurveys.OnlyWhereLeaderCanView360Report.No,
            out SurveyInfo);
        }
      }

      if (SurveyList.IsNullOrEmpty()) {
        if (SystemWeb.IsHttpPost) {
          AjaxSubmitHelper.Process(ajax => {
            ajax.AddDialogMessage("No Surveys Found.");
          });
          return new EmptyResult();
        } else {
          Content_ShowNoSurvey = true; // Show "no survey info".
        }
        return Page();
      }

      if (SurveyInfo == null) {
        if (SystemWeb.IsHttpPost) {
          AjaxSubmitHelper.Process(ajax => {
            ajax.AddDialogMessage("Survey Not Found.");
          });
          return new EmptyResult();
        }
        UrlSelectedSurveyUId = null;
        UrlSelectedPartUId = null;
        SurveyInfo = SurveyList[0]; // default selected to first in list.
      }

      if (SurveyInfo.SurveyType != DbHelper.AlbertSurveys.SurveyTypeEnum.IOS) {

        if (SurveyInfo.IsSelfOnly) {
          ActiveTab = ActiveTabEnum.Responses;
        } else {
          ActiveTab = ActiveTabEnum.Raters;
        }

        CoacheePartInfo = DbHelper.Participants.GetParticipantInfo(null, SurveyInfo.SurveyId, SurveyInfo.FoundParticipantBrief.PartId);

        CanViewSurveyReports = SessionHelper.AppAccess.Surveys.CanViewReports(ProgramInfo, CoacheeInfo, SurveyInfo);

        if (CoacheePartInfo != null) {

          IncompleteRaters = CoacheePartInfo.RatersNotDeclined - CoacheePartInfo.RatersCompleted;
          RaterList = DbHelper.Participants.GetRaterList(CoacheePartInfo.SurveyId, CoacheePartInfo.IntakeCode, CoacheePartInfo.PartId);

          if (CanViewSurveyReports && (SurveyInfo.FoundParticipantBrief?.IsReportAvailable_Online == true || SurveyInfo.FoundParticipantBrief?.IsReportAvailable_PDF == true)) {
            ReportsAvailable = true;
            DefaultSendOnlineReport = true; // Always by default send Online Report
            DefaultSendPDFReport = !SurveyInfo.ReportType.IsAble;
          }
        }

        // Get list of questions & results.
        QuestionList = DbHelper.Questions.GetQuestionsForPagePublic(SurveyInfo.SurveyUID, CoacheePartInfo.PartUID);
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {
            case AjaxAction.UpdateCloseDateSelf:
              if (CanChangeCloseDates && SurveyInfo != null) {
                UpdateCloseDate(ajax, true);
              }
              return;
            case AjaxAction.UpdateCloseDateRaters:
              if (CanChangeCloseDates && SurveyInfo != null) {
                UpdateCloseDate(ajax, false);
              }
              return;
            case AjaxAction.SendReminders:
              if (RaterList != null) {
                SendReminders(ajax);
              }
              return;
            case AjaxAction.SendReports:
              SendReports(ajax);
              return;
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    private void ShowNoInformation() {
      WebHelper.WriteAndEnd("No information to display.");
    }

    public string GetDatePickerCloseDateSelf() {
      if (SurveyInfo == null) return "";
      return SurveyInfo.CloseDateSelfLocal.ToString("dd/MM/yyyy");
    }

    public string GetDatePickerCloseDateRaters() {
      if (SurveyInfo == null) return "";
      return SurveyInfo.CloseDateRatersLocal.ToString("dd/MM/yyyy");
    }

    public string GetCloseDateSelf() {
      return WebHelper.DisplayDate(SurveyInfo.CloseDateSelfLocal) + (SurveyInfo.IsClosedSelf ? " <b>(Closed)</b>" : "");
    }

    public string GetCloseDateRaters() {
      if (SurveyInfo == null) return "";
      return WebHelper.DisplayDate(SurveyInfo.CloseDateRatersLocal) + (SurveyInfo.IsClosedRaters ? " <b>(Closed)</b>" : "");
    }

    public string SetQuestionListItem(DbHelper.Questions.SurveyQuestionInfo qi) {

      questionRowCount++;
      qnRowClass = "";
      questionTextClass = "";
      answerText = "";
      answerTextClass = "displaynone";
      answerNumericClass = "displaynone";
      answerNumeric = "";

      // Question Number
      questionNumber = "";
      if (qi.ShowAutoNumber) questionNumber += qi.AutoNumber.ToString();
      questionNumber += qi.CustomNumber;
      if (questionNumber.IsNullOrEmpty()) questionNumber = "&nbsp;"; else questionNumber += ".";

      // Show or hide question text & number
      if (!qi.ShowQuestionText) {
        questionNumber = "";
        questionTextClass = "displaynone";
        qnRowClass += " withPrev";
        questionRowCount--;
      }

      if (qi.InputType == DbHelper.AnswerTypes.InputTypes.DROPDOWN.Value
          || qi.InputType == DbHelper.AnswerTypes.InputTypes.SCALE.Value
          || qi.InputType == DbHelper.AnswerTypes.InputTypes.OPTIONS_SINGLE.Value) {
        answerNumericClass = "";
        answerNumeric = qi.AnswerScore == null ? "&nbsp;" : qi.AnswerScore.ToString();
        answerTextClass = "";
        answerText = !qi.AnswerAbbrev.IsNullOrEmpty() ? qi.AnswerAbbrev : qi.AnswerValue;
        if (int.TryParse(answerText, out _)) { // Output is a number not text.
          answerTextClass = "displaynone";
          if (qi.AnswerScore == null || qi.AnswerScore == 0) { // No actual numeric score.
            answerNumeric = answerText; // Show this is as the score.
          }
        }
      }

      if (qi.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_SINGLELINE.Value
        || qi.InputType == DbHelper.AnswerTypes.InputTypes.TEXT_MULTILINE.Value) {
        answerTextClass = "";
        answerText = qi.AnswerText.HTMLEncode() + "&nbsp;";
      }

      if (qi.InputType == DbHelper.AnswerTypes.InputTypes.OPTIONS_MULTIPLE.Value) {
        answerTextClass = "";
        answerText = "";
        foreach (var am in qi.AnswersMulti) {
          answerText += am.AnswerValue.HTMLEncode() + "<br>";
        }
      }

      return "";
    }

    public void UpdateCloseDate(AjaxSubmitHelper ajax, bool updatingSelfCloseDate) {

      if (SurveyInfo == null) return;

      string formCloseDateKey = updatingSelfCloseDate ? FormFields.CloseDateSelf : FormFields.CloseDateRaters;

      var dt = ajax.GetDatePickerDateUnspecified(formCloseDateKey, "Close Date", true, "Invalid Close Date");
      if (dt == null || ajax.HasErrors) return;

      var formCloseDate = dt.Value;

      DateTime closeDateSelf = SurveyInfo.CloseDateSelfLocal;
      DateTime closeDateRaters = SurveyInfo.CloseDateRatersLocal;

      // If survey has no raters, or if it's only raters (no self) then force both close dates to be set the same.
      bool datesMustBeSame = SurveyInfo.IsSelfOnly || SurveyInfo.IsRatersOnly;

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
        DbHelper.AlbertSurveys.UpdateIntakeCloseDates(SurveyInfo.SurveyId, SurveyInfo.IntakeNumber, closeDateSelf, closeDateRaters);
      } catch (Exception) {
        ajax.AddBadField("date", "Unable to update Close Date. Please try again later.");
        return;
      }

      ajax.SetRedirectUrl(PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.surveys), "Close Date has been updated.", AjaxSubmitHelper.PageMessageType.SuccessToast);
    }

    public void SendReminders(AjaxSubmitHelper ajax) {

      // Go through rater list and send reminders to each incomplete one.
      foreach (var raterInfo in RaterList.Participants) {
        if (raterInfo.CompletedUTC == null) {
          var emailSent = AlbertEmails.SendRaterInvitationEmail(
            ProjectInfo, SurveyInfo, raterInfo.PartId,
            SurveyInfo.ComputedSenderEmailName, SurveyInfo.ComputedSenderEmailAddress);
          if (userInfo.IsUserTester) break; // test send 1 only
        }
      }
    }

    // Send email containing links to the reports.
    void SendReports(AjaxSubmitHelper ajax) {

      if (SurveyInfo?.FoundParticipantBrief == null) {
        ajax.AddDialogMessage("Survey Participant Not Found.");
        return;
      }

      bool sendWebReport = DefaultSendOnlineReport; // Always send by default
      bool sendPDFReport = WebHelper.GetFormValue(FormFields.SendPDFReport) == "1"
        && SurveyInfo.SurveyType != DbHelper.AlbertSurveys.SurveyTypeEnum.Standard360; // Only send PDF for non-360

      if (!sendWebReport && !sendPDFReport) {
        ajax.AddDialogMessage("Please select at least one report to send.");
        return;
      }

      string coacheeReportUrl = PathHelper.Reports.CoacheeSurvey(CoacheeInfo, SurveyInfo, true);
      string coacheePdfUrl = PathHelper.Reports.ParticipantPDFReport(SurveyInfo.ReportType, SurveyInfo.SurveyId, SurveyInfo.FoundParticipantBrief.PartUniqueId);
      string coachSignoffHtml = "";

      if (CoacheeInfo.CoachUserId != ConfigHelper.UserId.Unassigned) {
        var coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(CoacheeInfo.CoachUserId);
        coachSignoffHtml = "Talk soon,<br/>" + coachInfo.FirstName + " " + coachInfo.LastName;
      }

      var recipients = new List<AlbertEmails.Addressee>() {
        new AlbertEmails.Addressee(CoacheeInfo.FirstName, CoacheeInfo.LastName, CoacheeInfo.EmailAddress)
      };

      var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(CoacheeInfo.ProgramJobNumber);

      string emailSubject = "Your 180/360 Leadership Feedback Report from Integral";
      string fromAddress = CoacheeInfo.UserActivity.CoachComputedSenderEmailAddress;
      string fromName = CoacheeInfo.UserActivity.CoachComputedSenderEmailName;

      try {
        DbHelper.Participants.UpdateReportSent(SurveyInfo.FoundParticipantBrief.PartId);
        DbHelper.AlbertSurveys.PatchCanLeaderView360Report(DbHelper.AlbertSurveys.PatchDbScope.Survey, SurveyInfo.SurveyId);
        DbHelper.EmailHistory.AddEmail(null, CoacheeInfo.CoacheeId, ProgramInfo.ProgramJobId, false, "",
          fromAddress, CoacheeInfo.EmailAddress, emailSubject, null);
      } catch (Exception) {
        ajax.AddDialogMessage("Problem sending report, please try again later.");
        return;
      }

      bool emailSent = AlbertEmails.SendGenericEmail(projectInfo,
        emailSubject,
        null,
        $@"
          Hi {CoacheeInfo.FirstName.HTMLEncode()},<br/>
          <br/>
          Your report is ready. Follow the link(s) below to review it:<br/>
          <br/>
          {(!sendWebReport ? "" : $@"<a href=""{coacheeReportUrl}"">View Online Report</a><br/><br/>")}
          {(!sendPDFReport ? "" : $@"<a href=""{coacheePdfUrl}"">Download PDF Report</a><br/><br/>")}
          <br/>
          {coachSignoffHtml}<br/>
        ",
        false,
        fromName, fromAddress,
        null, out _,
        new MailAddress(CoacheeInfo.EmailAddress, CoacheeInfo.GetFullName()));

      if (emailSent) {
        ajax.AddSuccessToast("Email sent.");
      } else {
        ajax.AddDialogMessage("Problem sending email, please try again later.");
      }
    }
  }
}
