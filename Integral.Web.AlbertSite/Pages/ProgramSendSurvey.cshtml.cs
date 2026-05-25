using Integral.Web.Services;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProgramSendSurvey : AppCode.PageBaseClasses.ProgramPageBase {

    public class FormFields {
      public const string SurveyId_FieldName = "SurveyId";
      public const string SelfClose_FieldName = "CloseDateSelf";
      public const string RaterClose_FieldName = "CloseDateRater";
      public const string SendDate_FieldName = "SendDate";
    }

    private int? urlTemplateSurveyId;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Send New Survey to Program (" + ProgramInfo.ParticipantCount + " participants)";

      urlTemplateSurveyId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.TemplateSurveyId);

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          SendSurvey(ajax);
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

        sb.Append("<option ");
        if (template.SurveyId == urlTemplateSurveyId) sb.Append("selected ");
        sb.Append("data-ratersonly=\"" + (template.AlbertRatersOnly ? "1" : "0") + "\" ");
        sb.Append("data-noraters=\"" + (noRaters ? "1" : "0") + "\" ");
        sb.Append("value=\"" + template.SurveyId + "\">");
        sb.Append((template.IsAlbertSurvey ? "Able: " : "Jarvis: "));
        sb.Append(SystemWeb.HtmlEncode(template.SurveyName));
        sb.Append("</option>");
      }

      return sb.ToString();
    }

    bool SendSurvey(AjaxSubmitHelper ajax) {

      // Get required info from form.
      int templateSurveyId = ajax.CheckFieldID(FormFields.SurveyId_FieldName, "Survey", true, "Invalid Survey Selection");
      DateTime closeDateSelfLocal = ajax.GetDatePickerDateUnspecified(FormFields.SelfClose_FieldName, "Participant Close Date", true, "Invalid participant close date.").GetValueOrDefault();
      DateTime closeDateRaterLocal = ajax.GetDatePickerDateUnspecified(FormFields.RaterClose_FieldName, "Rater Close Date", true, "Invalid rater close date.").GetValueOrDefault();
      DateTime sendDateUser = ajax.GetDatePickerDateUnspecified(FormFields.SendDate_FieldName, "Send Date", true, "Invalid send date.").GetValueOrDefault();

      if (ajax.BadFieldCount > 0) return false;

      var sendDateUTC = SessionHelper.UserTimeToUtc(sendDateUser);
      bool isScheduledInFuture = (sendDateUTC > DateTime.UtcNow);

      // Get list of coachees to add.
      var coacheesInSurvey = DbHelper.AblePrograms.GetCoacheesInProgram(ProgramInfo.ProgramJobId);

      DbHelper.AlbertSurveys.CreateSurveyInfo newSurveyInfo = null;
      string exMessage = null;

      try {
        newSurveyInfo = DbHelper.AlbertSurveys.CreateSurvey(
          userId: userInfo.UserId,
          companyId: ProgramInfo.CompanyId,
          programJobId: ProgramInfo.ProgramJobId,
          isProgramSurvey: true,
          templateSurveyId: templateSurveyId,
          intakeTitle: ProgramInfo.ProgramJobNumber + ", " + TimeHelper.UtcToAppDefaultTimeZone(DateTime.UtcNow).ToString("d MMM yyyy"),
          closeDateSelfLocal: closeDateSelfLocal,
          closeDateRaterLocal: closeDateRaterLocal,
          coacheeIdsToAdd: coacheesInSurvey,
          scheduledSendDateUtc: isScheduledInFuture ? (DateTime?)sendDateUTC : null);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("ProgramSendSurvey_CreateSurvey")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty("ProgramJobId", ProgramInfo?.ProgramJobId)
          .WithProperty("ProgramJobNumber", ProgramInfo?.ProgramJobNumber)
          .WithProperty("TemplateSurveyId", templateSurveyId)
          .WithProperty("CoacheeCount", coacheesInSurvey?.Count)
          .Track();

        exMessage = ex.Message;
      }

      if (newSurveyInfo == null || !newSurveyInfo.Success) {
        ajax.AddDialogMessage("Failed to create a new survey. Please try again later."
          + (newSurveyInfo != null && !newSurveyInfo.Message.IsNullOrEmpty() ? ("<br/>" + newSurveyInfo.Message) : "")
          + (!exMessage.IsNullOrEmpty() ? ("<br/>" + exMessage) : ""));
        return false;
      }

      // Send Intercom event for survey creation
      SendEvent(
        intercom => intercom.SurveyCreated()
          .FromSession()
          .WithSurvey(newSurveyInfo.SurveyInfo.SurveyId, newSurveyInfo.SurveyInfo.SurveyName)
          .WithProgram(ProgramInfo.ProgramJobId, ProgramInfo.ProgramJobName)
          .WithParticipantCount(coacheesInSurvey.Count),
        operationName: "ProgramSendSurvey_SurveyCreated",
        requestRawUrl: SystemWeb.RequestRawUrl,
        telemetryProperties: new Dictionary<string, object> {
          ["SurveyId"] = newSurveyInfo.SurveyInfo.SurveyId,
          ["SurveyName"] = newSurveyInfo.SurveyInfo.SurveyName,
          ["ProgramJobId"] = ProgramInfo.ProgramJobId,
          ["ParticipantCount"] = coacheesInSurvey.Count
        }
      );

      // If survey is scheduled for a future date, nothing else to do here.
      if (isScheduledInFuture) {
        ajax.AddSuccessDialog("Survey Scheduled for Delivery on " + WebHelper.DisplayDate(sendDateUser));
        ajax.SetRedirectUrl(PathHelper.Pages.ProgramSurveyStatus(true));
        return true;
      }

      // Send invitations to coachees.
      foreach (var newPartInfo in newSurveyInfo.Participants) {
        try {
          SendInviteToCoachee(ajax, newSurveyInfo.SurveyInfo, newPartInfo.ParticipantInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("ProgramSendSurvey_SendInviteToCoachee")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(newPartInfo?.CoacheeInfo?.UserGuid))
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty("SurveyId", newSurveyInfo?.SurveyInfo?.SurveyId)
            .WithProperty("ParticipantEmail", newPartInfo?.ParticipantInfo?.EmailAddr)
            .Track();

          ajax.AddDialogMessage("Unable to Send the Survey Invitation. Please try again later.");
          return false;
        }
      }

      ajax.AddSuccessDialog("Survey Sent.");
      ajax.SetRedirectUrl(PathHelper.Pages.ProgramSurveyStatus(true));
      return true;
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
          partInfo.CoacheeId, ProgramInfo.ProgramJobId,
          ProjectInfo.ComputedSenderEmailName.ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_FullName),
          ProjectInfo.ComputedSenderEmailAddress.ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_Address),
          new AlbertEmails.Addressee(partInfo.FirstName, partInfo.LastName, partInfo.EmailAddr),
          new AlbertEmails.Addressee(userInfo),
          ProgramInfo.CompanyName, false);
      } catch (Exception e) {
        if (!ConfigHelper.IsDevServer) EmailHelper.SendInternalSupportEmail(e, "Error Sending Program Email.");
        throw;
      }
      if (emailSent) DbHelper.Participants.UpdateFirstInvitationSent(partInfo.NewPartId, DateTime.UtcNow);

      ajax.AddReturnValue(WebHelper.AjaxReturnValues.NewSurveyId, surveyInfo.SurveyId.ToString());

    } // SendSurvey

  }
}
