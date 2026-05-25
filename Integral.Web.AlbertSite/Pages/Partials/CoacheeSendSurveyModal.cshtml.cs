using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public class CoacheeSendSurveyModal : AppCode.PageBaseClasses.CoacheeInfoBase {

    public class AjaxAction {
      public const string SendSurvey = "SendSurvey";
    }

    public class FormFields {
      public const string SurveyId = "SurveyId";
      public const string SelfClose = "CloseDateSelf";
      public const string RaterClose = "CloseDateRater";
      public const string SendDate = "SendDate";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Send New Survey";

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          if (PageAjaxAction == AjaxAction.SendSurvey) {
            SendSurvey(ajax);
            return;
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    public string GetSurveySelectOptions() {
      var templateList = DbHelper.AlbertSurveys.GetTemplateList();
      var sb = new System.Text.StringBuilder();
      foreach (var template in templateList) {
        bool noRaters = template.FeedbackOption == DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters ? true : false;
        int selfOpenDays = ConfigHelper.AbleSurveyDefaultOpenDays_Self;
        if (template.SurveyId == ConfigHelper.TemplateSurveyIds.Intake) selfOpenDays = ConfigHelper.Email_Welcome_SurveyOpenDays;
        sb.Append("<option "
          + "data-ratersonly=\"" + (template.AlbertRatersOnly ? "1" : "0") + "\" "
          + "data-noraters=\"" + (noRaters ? "1" : "0") + "\" "
          + "data-selfopendays=\"" + selfOpenDays + "\" "
          + "value=\"" + template.SurveyId + "\">"
          + (template.IsAlbertSurvey ? "Able: " : "Jarvis: ")
          + template.SurveyName.HTMLEncode()
          + (template.ShowSurveyLoggedIn ? " (in-portal)" : "")
          + "</option>");
      }
      return sb.ToString();
    }

    public DateTime GetSelfCloseDate() {
      return TimeHelper.UtcToAppDefaultTimeZone(DateTime.UtcNow).Value.AddDays(ConfigHelper.AbleSurveyDefaultOpenDays_Self);
    }

    public DateTime GetRaterCloseDate() {
      return TimeHelper.UtcToAppDefaultTimeZone(DateTime.UtcNow).Value.AddDays(ConfigHelper.AbleSurveyDefaultOpenDays_Rater);
    }

    private void SendSurvey(AjaxSubmitHelper ajax) {

      if (CoacheeInfo.ProgramJobId == null) {
        ajax.AddDialogMessage("Participant must belong to a program.<br/>Please select a program from the "
          + "<a href=\"" + PathHelper.Pages.CoacheeEdit(true) + "\">Profile</a> page.");
        return;
      }

      // Get required info from form.
      int templateSurveyId = ajax.CheckFieldID(FormFields.SurveyId, "Survey", true, "Invalid Survey Selection");
      DateTime closeDateSelfLocal = ajax.GetDatePickerDateUnspecified(FormFields.SelfClose, "Close Date Self", true, "") ?? DateTime.MinValue;
      DateTime closeDateRaterLocal = ajax.GetDatePickerDateUnspecified(FormFields.RaterClose, "Close Date Self", false, "") ?? closeDateSelfLocal.AddDays(10);
      DateTime sendDateLocal = ajax.GetDatePickerDateUnspecified(FormFields.SendDate, "Send Date", true, "") ?? DateTime.MinValue;

      if (ajax.BadFieldCount > 0) return;

      var sendDateUTC = SessionHelper.UserTimeToUtc(sendDateLocal);
      bool isScheduledInFuture = (sendDateUTC > DateTime.UtcNow);

      // Get coachees to add as a list.
      var coacheesInSurvey = new List<int> { CoacheeInfo.CoacheeId };

      DbHelper.AlbertSurveys.CreateSurveyInfo newSurveyInfo = null;
      string exMessage = null;

      try {
        newSurveyInfo = DbHelper.AlbertSurveys.CreateSurvey(
          userId: userInfo.UserId,
          companyId: CoacheeInfo.CompanyId,
          programJobId: CoacheeInfo.ProgramJobId,
          isProgramSurvey: false,
          templateSurveyId: templateSurveyId,
          intakeTitle: CoacheeInfo.ProgramJobNumber + ", " + TimeHelper.UtcToAppDefaultTimeZone(DateTime.UtcNow).ToString("d MMM yyyy"),
          closeDateSelfLocal: closeDateSelfLocal,
          closeDateRaterLocal: closeDateRaterLocal,
          coacheeIdsToAdd: coacheesInSurvey,
          scheduledSendDateUtc: isScheduledInFuture ? (DateTime?)sendDateUTC : null);
      } catch (Exception ex) {
        exMessage = ex.Message;
      }
      if (newSurveyInfo == null || !newSurveyInfo.Success) {
        ajax.AddDialogMessage("Failed to create a new survey. Please try again later."
          + (newSurveyInfo != null && !newSurveyInfo.Message.IsNullOrEmpty() ? ("<br/>" + newSurveyInfo.Message) : "")
          + (!exMessage.IsNullOrEmpty() ? ("<br/>" + exMessage) : ""));
        return;
      }

      // Send Intercom event for survey creation
      if (newSurveyInfo.SurveyInfo != null) {
        SendEvent(
          intercom => intercom.SurveyCreated()
            .FromSession()
            .WithSurvey(newSurveyInfo.SurveyInfo.SurveyId, newSurveyInfo.SurveyInfo.InternalTitle ?? "Custom Survey")
            .WithProgram(CoacheeInfo.ProgramJobId, CoacheeInfo.ProgramJobNumber)
            .WithParticipantCount(1),
          operationName: "CoacheeSendSurveyModal_SurveyCreated",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["SurveyId"] = newSurveyInfo.SurveyInfo.SurveyId,
            ["ParticipantEmail"] = CoacheeInfo?.EmailAddress
          }
        );
      }

      // If survey is scheduled for a future date, nothing else to do here.
      if (isScheduledInFuture) {
        SetRedirect(PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId), "Survey Scheduled for Delivery on " + WebHelper.DisplayDate(sendDateLocal));
        return;
      }

      // Not scheduled, so send invitations now to coachee.
      foreach (var newPartInfo in newSurveyInfo.Participants) {
        try {
          SendInviteToCoachee(ajax, newSurveyInfo.SurveyInfo, newPartInfo.ParticipantInfo);
        } catch (Exception) {
          ajax.AddDialogMessage("Unable to Send the Survey Invitation. Please try again later.");
          return;
        }
      }

      ajax.SetRedirectUrl(PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.surveys), "Survey Sent.", AjaxSubmitHelper.PageMessageType.SuccessToast);
    }

    private void SendInviteToCoachee(
      AjaxSubmitHelper ajax,
      DbHelper.AlbertSurveys.SurveyInfo surveyInfo,
      DbHelper.Participants.AddParticipantToSurveyInfo partInfo) {

      // Send initial invitation email.
      bool emailSent = false;
      try {
        emailSent = AlbertEmails.SendSelfInvitationEmail(
          ProjectInfo, surveyInfo, "",
          partInfo.NewPartId, partInfo.NewPartUID,
          CoacheeInfo.CoacheeId, CoacheeInfo.ProgramJobId,
          ProjectInfo.ComputedSenderEmailName, ProjectInfo.ComputedSenderEmailAddress,
          new AlbertEmails.Addressee(partInfo.FirstName, partInfo.LastName, partInfo.EmailAddr),
          new AlbertEmails.Addressee(userInfo),
          CoacheeInfo.CompanyName, false);
      } catch (Exception e) {
        if (!ConfigHelper.IsDevServer) EmailHelper.SendInternalSupportEmail(e, "Error Sending Coachee Invite Email.");
        throw;
      }
      if (emailSent) DbHelper.Participants.UpdateFirstInvitationSent(partInfo.NewPartId, DateTime.UtcNow);

      ajax.AddReturnValue(WebHelper.AjaxReturnValues.NewSurveyId, surveyInfo.SurveyId.ToString());

    } // SendSurvey

  }
}
