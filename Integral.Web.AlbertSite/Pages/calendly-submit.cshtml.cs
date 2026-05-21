using System;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Integral.Integrations;
using Integral.Web;
using System.Collections.Generic;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;
using static Integral.Web.AmplitudeHelper;

// Notes about Calendly payloads:
//
// 1. Creation payloads:
//
//    Calendly sends one "created" payload (payload.event = "invitee.created").
//      - "event.uuid" must be saved to the new CoachingSession record as it may be referred to later, see below.
//      - "event.canceled" will be false.
//      - "invitee.is_reschedule" will be false.
//      - "old_event" and "new_event" will both be null.
//
// 2. Rescheduling payloads:
//
//    Calendly sends TWO webhooks for rescheduling - one "cancel", and one "create".
//
//    Payload 1 - the "cancel" payload (payload.event = "invitee.canceled"):
//      - "event.uuid" will match the column "Payload_uuid" in the CoachingSession table.
//      - "invitee.is_reschedule = true" to indicate it is part 1 of a resceduling pair.
//      - "new_event.uuid" contains the new uuid the event will be changed to.
//      - "event.cancel_reason" contains the reason text typed by the user (note the "create" event does not contain this).
//
//    Payload 2 - the "created" payload (payload.event = "invitee.created"):
//      - "old_event.uuid" will match the column "Payload_uuid" in the CoachingSession table.
//      - "old_invitee.is_reschedule = true" to indicate it is part 2 of a resceduling pair.
//      - "event.uuid" contains a new uuid which "Payload_uuid" must be changed to.
//      - Update CoachingSession table with new event time. Also update duration and SessionTypeId if they differs in the payload's "event_type".
//
// 3. Cancellation payload:
//
//   Calendly sends a single "cancel" payload (payload.event = "invitee.canceled").
//     - "old_event.uuid" will match the column "Payload_uuid" in the CoachingSession table.
//     - "invitee.is_reschedule = false".
//     - "old_invitee.is_reschedule = true" if it was previously rescheduled before cancelling - but ignore this.
//     - "event.cancel_reason" contains the reason text typed by the user (note the "create" event does not contain this).
//     - "new_event" and "new_invitee" objects will both be null.
//
//  Note that values like "event.canceled" and "invitee.canceled" are TRUE for BOTH rescheduling and cancelling, so best to ignore those.
//  Basically if payload.event = "invitee.canceled", then either:
//    a) "invitee.is_reschedule = true" for rescheduling (i.e. a "create" payload will follow), or
//    b) "invitee.is_reschedule = false" for an actual cancellation.
//
// Also note that time zones differ between "event.start_time" and "event.invitee_start_time":
// "event.start_time" is apparently in the time zone of the coach (in this case +08:00 for WST)
// "event.invitee_start_time" is apprently in the time zone of the person making the booking (i.e. +10:00 or +11:00 for EST etc)
//

namespace Integral.Web.PortalSite.Pages_Albert {

  public class CalendlySubmit : PageModel {

    private readonly string SupportEmailSubject = "Calendly Submission";

    public bool showForm = false;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {
      try {
        Main();
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CalendlySubmit_Main")
          .FromSession()
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty("PayloadId", WebHelper.GetQueryStringInt("payloadid", 0))
          .Track();

        LogError(0, "", null, ex, ErrorEmailType.Technical, "Error in Main()");
      }

      if (WebHelper.IsRequestExiting()) return new EmptyResult();
      return Page();
    }

    void Main() {

      int payloadId = WebHelper.GetQueryStringInt("payloadid", 0).Value;

      if (payloadId == 0 && !SystemWeb.IsHttpPost && ConfigHelper.IsDevServer) {
        showForm = true;
        return;
      }

      string bodyJson = "";

      if (payloadId > 0) {

        // Load payload from db.
        bodyJson = (string)DbHelper.Common.GetScalarQuery(null,
          "SELECT PayloadContent FROM al_CalendlyPayloads WHERE PayloadId = @PayloadId",
          DbHelper.Common.NewSqlParameter("PayloadId", payloadId));

        if (bodyJson.IsNullOrEmpty()) {
          LogError(payloadId, "", null, null, ErrorEmailType.None, "Can't find PayloadId " + payloadId);
          return; // Can't continue.
        }

      } else {

        bodyJson = WebHelper.GetRequestBody();

        if (bodyJson.IsNullOrEmpty()) bodyJson = WebHelper.GetFormValue("json"); // From test form.

        if (bodyJson.IsNullOrEmpty()) {
          LogError(0, bodyJson, null, null, ErrorEmailType.Technical, "Payload Error - No content.");
          return; // Can't continue.
        }

        // First save the payload to the db.
        // If anything fails thereafter, at least the payload is safe.
        // Ideally, this should never fail. Keep it as simple as possible.
        // Failure will at least email the payload to tech contact, and we can continue.
        try {
          if (!TrySavePayloadJson(bodyJson, out payloadId)) return;
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("CalendlySubmit_TrySavePayload")
            .FromSession()
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty("BodyJsonLength", bodyJson?.Length)
            .Track();

          LogError(0, bodyJson, null, ex, ErrorEmailType.Technical, "TrySavePayload failed!");
        }

      }

      // From here, payloadId and bodyJson should both be set.

      if (!CheckValidPayload(bodyJson)) {
        LogError(payloadId, bodyJson, null, null, ErrorEmailType.Technical, "Payload Error - Invalid content.");
        return; // Can't continue
      }

      Exception tryException;
      string tryErrorText;

      if (!Calendly.TryParsePayload(bodyJson, payloadId, out Calendly.WebhookInfo webhookInfo, out tryException, out tryErrorText)) {
        if (tryException != null || !tryErrorText.IsNullOrEmpty()) {
          LogError(payloadId, bodyJson, null, tryException, ErrorEmailType.Technical, "TryParsePayload failed. " + tryErrorText);
        }
        return; // Can't continue.
      }

      // Ignore reschedule payload part 2, not needed.
      if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.ReschedulePart2) {
        try {
          DbHelper.CalendlyPayloads.DeletePayload(payloadId);
        } catch (Exception) { }
        if (ConfigHelper.IsDevServer) SystemWeb.ResponseWrite("Reschedule part 2 - ignoring.");
        return;
      }

      // Correct old Integral domain.
      if (webhookInfo.CoachEmail.EndsWith("@integral.org.au")) {
        webhookInfo.CoachEmail = webhookInfo.CoachEmail.Replace("@integral.org.au", "@integral.global");
      }

      if (!TryGetCoachInfo(payloadId, bodyJson, webhookInfo, out DbHelper.AbleUser.AbleUserInfo coachUserInfo)) return;

      if (!TryGetSessionType(webhookInfo, coachUserInfo, out DbHelper.CoachingSessionTypes.SessionType sessionType)) return;

      string responseMessage;

      if (sessionType.IsGroup) {
        responseMessage = HandleWorkshopBooking(payloadId, bodyJson, webhookInfo, coachUserInfo, sessionType);
      } else {
        responseMessage = HandleCoachingSessionBooking(payloadId, bodyJson, webhookInfo, coachUserInfo, sessionType);
      }

      if (!responseMessage.IsNullOrEmpty() && !responseMessage.Contains("Group Session ID")) { // Hack: Don't send an error email for Group Session ID issues.
        responseMessage += "<br/>\nSessionTypeSlug = '" + webhookInfo.SessionTypeSlug + "'";
        LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Technical, responseMessage);
      }

      SystemWeb.ResponseWrite(responseMessage + "<br/>\n");
    }

    string HandleCoachingSessionBooking(
      int payloadId, string bodyJson,
      Calendly.WebhookInfo webhookInfo,
      DbHelper.AbleUser.AbleUserInfo coachUserInfo,
      DbHelper.CoachingSessionTypes.SessionType sessionType) {

      string responseMessage = "";
      DateTime sessionStartUtc = webhookInfo.InviteeStartTime.Value.UtcDateTime;
      DbHelper.CoachingSessions.AbleSessionInfo sessionInfo = null;
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo = null;

      // Essential checks and values...

      // Find active coachee(s) for the coach, by coachee email addr and coach CalendlyUrlName.
      var activeCoachees = GetActiveCoacheesByCalendlyName(webhookInfo);

      if (activeCoachees.IsNullOrEmpty()) {
        // Temporary hack for Woodside (req by CJ 2022-06-24) if coachee not found.
        if (webhookInfo.CoacheeEmail.Contains("@woodside.com.au")) {
          webhookInfo.CoacheeEmail = webhookInfo.CoacheeEmail.Replace("@woodside.com.au", "@woodside.com");
        } else if (webhookInfo.CoacheeEmail.Contains("@woodside.com")) {
          webhookInfo.CoacheeEmail = webhookInfo.CoacheeEmail.Replace("@woodside.com", "@woodside.com.au");
        }
        activeCoachees = GetActiveCoacheesByCalendlyName(webhookInfo); // Try again with adjusted domain.
      }

      if (activeCoachees.IsNullOrEmpty()) {

        var coachees = DbHelper.AlbertCoachees.GetCoacheesByEmail(webhookInfo.CoacheeEmail);
        string coacheeHtml = "";
        foreach (var coachee in coachees) {
          coacheeHtml += $"{coachee.UserActivity.CoachEmail}, {DbHelper.CoacheeProgramStatus.GetProgramStatusById(coachee.ProgramStatusId).DisplayTitle}<br/>";
        }

        // Don't log unnecessary errors - this is a hack to stop sending unnecessary error emails.
        if (webhookInfo.CoacheeEmail != "defence@agsm.edu.au" && webhookInfo.CoacheeEmail != "cj@integral.global" && webhookInfo.CoacheeEmail != "cjmalmsten@gmail.com") {
          LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Admin,
            $"Coachee {webhookInfo.CoacheeEmail} either a) not found, b) coach isn't {webhookInfo.CoachEmail}, c) coachee or program inactive.<br/>"
            + coacheeHtml);
        }

        return responseMessage;
      }

      // Try to find existing session by coachee email and start time.
      sessionInfo = DbHelper.CoachingSessions.GetSessionInfoOrNull(webhookInfo.CoacheeEmail, sessionStartUtc);

      // Session, if found, must belong to the related coach(es).
      if (sessionInfo != null) {
        if (activeCoachees.Find(c => c.CoachUserId == sessionInfo.CoachUserId) == null) {
          LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Admin,
            $"Session exists for {webhookInfo.CoacheeEmail} but inactive or different coach. ");
          return responseMessage;
        }
      }

      if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Create) {

        if (sessionInfo != null) {
          LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Admin,
            "Can't create - session already exists for " + webhookInfo.CoacheeEmail + " at "
            + TimeHelper.UtcToTimeZoneId(sessionInfo.ApptDateUTC, sessionInfo.CoachTimeZoneIdIANA).ToString()
            + "<br/><br/>"
            + PathHelper.Pages.CoacheeEdit(sessionInfo.CoacheeId, PathHelper.CoacheeTabEnum.coaching, true));
          return responseMessage;
        }

        // Get first coachee from active list with free session(s).
        var firstFreeCoachee = activeCoachees.Find(c => c.SessionsAllocated > c.SessionsBooked);
        if (firstFreeCoachee == null) {
          LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Admin, $"No free sessions found for coachee {webhookInfo.CoacheeEmail}.");
          return responseMessage;
        }
        coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(firstFreeCoachee.CoacheeId);

      } else { // Reschedule or Cancel

        if (sessionInfo == null) {
          LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Admin, "Can't find existing session for " + webhookInfo.CoacheeEmail
            + " at " + TimeHelper.UtcToTimeZoneId(sessionStartUtc, coachUserInfo.TimeZoneIdIana).ToStringOrEmptyIfNull());
          return responseMessage;
        }

        if (ConfigHelper.IsDevServer) SystemWeb.ResponseWrite("Found SessionId: " + sessionInfo.SessionId + "<br/>");

        // Get coachee from session info.
        coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(sessionInfo.CoacheeId);
      }

      webhookInfo.CoacheeId = coacheeInfo.CoacheeId;
      DbHelper.CalendlyPayloads.UpdatePayload(webhookInfo);

      // Set coach info to the current coachee's actual coach, in case the Calendly account user is different.
      if (coachUserInfo.UserId != coacheeInfo.CoachUserId) {
        coachUserInfo = DbHelper.AbleUser.GetUserByIdOrNull(coacheeInfo.CoachUserId, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      }

      // All checks passed, webhook can be processed.

      // Ensure coachee's "ProgramStatus" is Active Program, unless they are "paused".

      if (coacheeInfo.ProgramStatusId < DbHelper.CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId) {
        try {
          coacheeInfo.ProgramStatusId = DbHelper.CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId;
          DbHelper.AlbertCoachees.UpdateCoachee(coacheeInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("CalendlySubmit_SetCoacheeActive")
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo.UserGuid))
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty("PayloadId", payloadId)
            .WithProperty("CoacheeEmail", coacheeInfo?.EmailAddress)
            .WithProperty("ProgramStatusId", coacheeInfo?.ProgramStatusId)
            .Track();

          // Send error email but this is not fatal, continue on.
          SendErrorNotice_Technical(payloadId, "Error setting coachee to active: "
            + "<br/>" + ex.Message
            + "<br/>" + coacheeInfo.EmailAddress
            + "<br/><br/>", bodyJson);
        }
      }

      // Ensure program status is Active.
      var programStatus = DbHelper.AblePrograms.GetProgramStatus(coacheeInfo.ProgramJobId.Value);
      if (programStatus?.ProgramStatusId < DbHelper.AlbertProgramStatus.Ids.Active) {
        DbHelper.AblePrograms.UpdateProgramStatus(coacheeInfo.ProgramJobId.Value, DbHelper.AlbertProgramStatus.Statuses.Active);
      }

      if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Create) {

        try {
          responseMessage = DoCreate(bodyJson, payloadId, webhookInfo, coacheeInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("CalendlySubmit_DoCreate")
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo?.UserGuid))
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty("PayloadId", payloadId)
            .WithProperty("CoacheeEmail", webhookInfo?.CoacheeEmail)
            .WithProperty("CoachEmail", webhookInfo?.CoachEmail)
            .WithProperty("SessionTypeSlug", webhookInfo?.SessionTypeSlug)
            .Track();

          LogError(payloadId, bodyJson, webhookInfo, ex, ErrorEmailType.Technical, "Booking Error: " + webhookInfo.PayloadId + ", " + ex.Message);
          return responseMessage;
        }

      } else if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Cancel) {

        try {
          responseMessage = DoCancel(sessionInfo, coacheeInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("CalendlySubmit_DoCancel")
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo?.UserGuid))
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty("PayloadId", payloadId)
            .WithProperty("SessionId", sessionInfo?.SessionId)
            .WithProperty("CoacheeEmail", webhookInfo?.CoacheeEmail)
            .Track();

          LogError(payloadId, bodyJson, webhookInfo, ex, ErrorEmailType.Technical, "Cancellation Error: " + webhookInfo.PayloadId + ", " + ex.Message);
          return responseMessage;
        }

      } else if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Reschedule) {

        try {
          responseMessage = DoReschedule(webhookInfo, sessionInfo, coacheeInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("CalendlySubmit_DoReschedule")
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo?.UserGuid))
            .WithPageUrl(SystemWeb.RequestRawUrl)
            .WithProperty("PayloadId", payloadId)
            .WithProperty("SessionId", sessionInfo?.SessionId)
            .WithProperty("CoacheeEmail", webhookInfo?.CoacheeEmail)
            .WithProperty("NewStartTime", webhookInfo?.NewEventStartTime)
            .Track();

          LogError(payloadId, bodyJson, webhookInfo, ex, ErrorEmailType.Technical, "Reschedule Error: " + webhookInfo.PayloadId + ", " + ex.Message);
          return responseMessage;
        }

      } else {

        LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Technical, "eventInfo.WebhookType = unknown");
        return responseMessage;
      }

      return responseMessage;
    }

    string HandleWorkshopBooking(
      int payloadId, string bodyJson,
      Calendly.WebhookInfo webhookInfo,
      DbHelper.AbleUser.AbleUserInfo coachUserInfo,
      DbHelper.CoachingSessionTypes.SessionType sessionType) {

      if (webhookInfo.Answers.Count < ConfigHelper.Calendly_QuestionNo_GroupWorkshopGuid) {
        return "Group Session ID answer not found.";
      }

      string groupSessionIdAnswer = webhookInfo.Answers[ConfigHelper.Calendly_QuestionNo_GroupWorkshopGuid - 1];

      if (!Guid.TryParse(groupSessionIdAnswer, out Guid workshopGuid)) {
        return "Invalid Group Session ID answer. Answer = '" + groupSessionIdAnswer.HTMLEncode() + "'";
      }

      var workshopInfo = DbHelper.WorkshopEvents.GetWorkshopInfo(workshopGuid);
      if (workshopInfo == null) return "Can't find workshop Guid " + workshopGuid.ToStringNoBraces();

      if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Create) {

        // Update workshop booking.
        // Note we are converting the calendly booking time to UTC then to the workshop's local timezone.
        workshopInfo.SetTime(
          webhookInfo.EventStartTime.Value.UtcDateTime.UtcToTZId(workshopInfo.TimeZoneIdIana),
          webhookInfo.EventEndTime.Value.UtcDateTime.UtcToTZId(workshopInfo.TimeZoneIdIana),
          workshopInfo.TimeZoneIdIana);
        workshopInfo.WorkshopStatusId = DbHelper.WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId;
        workshopInfo.Location = webhookInfo.EventLocation;
        DbHelper.WorkshopEvents.UpdateWorkshopEvent(workshopInfo);

        string postResult = PostToEventGrid(workshopInfo.WorkshopEventId, EventGrid.WorkshopSession.WorkshopUpdateEventType.added);
        if (!postResult.IsNullOrEmpty()) {
          return "Added workshop but could not post to EventGrid: " + postResult;
        }

      } else if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Reschedule) {

        // Send email

      } else if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Cancel) {

        // Send email

      }

      return "";
    }

    string PostToEventGrid(int workshopEventId, Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType eventType) {

      // Note that posting to EventGrid sends an email to the Program Contact, so only do it live.
      // Comment out the line below for testing eventgrid on dev.
      if (!ConfigHelper.IsLiveServer) return ""; // dummy ok result if not live.

      var workshopInfo = DbHelper.WorkshopEvents.GetWorkshopInfo(workshopEventId);
      if (workshopInfo == null) return null;
      var programInfo = DbHelper.AblePrograms.GetProgramInfoOrNull((int)workshopInfo.ProgramJobId);
      if (programInfo == null) return null;

      if (!ConfigHelper.IsLiveServer) {
        // For testing, replace real statuses with "test" statuses.
        if (eventType == EventGrid.WorkshopSession.WorkshopUpdateEventType.added) eventType = EventGrid.WorkshopSession.WorkshopUpdateEventType.added_test;
        else if (eventType == EventGrid.WorkshopSession.WorkshopUpdateEventType.updated) eventType = EventGrid.WorkshopSession.WorkshopUpdateEventType.updated_test;
        else if (eventType == EventGrid.WorkshopSession.WorkshopUpdateEventType.deleted) eventType = EventGrid.WorkshopSession.WorkshopUpdateEventType.deleted_test;
      }

      string postResult = null;
      try {
        postResult = EventGrid.WorkshopSession.PostWorkshopUpdate(
          eventType,
          workshopInfo.WorkshopEventId,
          workshopInfo.WorkshopTitle, workshopInfo.FriendlyWorkshopId,
          programInfo.CompanyName, programInfo.ProgramJobNumber, programInfo.ProgramJobName,
          workshopInfo.WorkshopStatusName,
          workshopInfo.WhenStartLocal.ToUniversalTimeOrNull(ConfigHelper.DefaultTimeZoneInfo),
          workshopInfo.WhenEndLocal.ToUniversalTimeOrNull(ConfigHelper.DefaultTimeZoneInfo),
          workshopInfo.KeyFacilitatorFirstName, workshopInfo.Location);
      } catch (Exception) {
        // Ignore for now.
      }

      return postResult;
    }

    // Save payload to db. Ideally this should never fail, keep as simple as possible.
    bool TrySavePayloadJson(string bodyJson, out int payloadId) {

      payloadId = 0;

      try {
        payloadId = DbHelper.CalendlyPayloads.SavePayload(bodyJson); // Returns ID of the inserted row.
        if (ConfigHelper.IsDevServer) SystemWeb.ResponseWrite("payloadId = " + payloadId + "<br/>");
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CalendlySubmit_SavePayloadJson")
          .FromSession()
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty("BodyJsonLength", bodyJson?.Length)
          .Track();

        // If this ever fails, the payload is emailed along with the error, so it can be manually inserted later.
        LogError(payloadId, bodyJson, null, ex, ErrorEmailType.Technical, "Payload Error - Problem saving to db: " + ex.Message);
        return false;
      }

      return true;
    }

    bool TryGetCoachInfo(int payloadId, string bodyJson, Calendly.WebhookInfo webhookInfo, out DbHelper.AbleUser.AbleUserInfo coachUserInfo) {

      // Get coach user id.
      coachUserInfo = DbHelper.AbleUser.GetUserByEmailOrNull(webhookInfo.CoachEmail, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      if (coachUserInfo == null) {
        LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Admin, $@"
          Received a Calendly booking for coach: {webhookInfo.CoachName.HTMLEncode()} ({webhookInfo.CoachEmail.HTMLEncode()}).<br/>
          <br/>
          Problem: Coach email {webhookInfo.CoachEmail.HTMLEncode()} not found in Able.<br/>");
        return false;
      }

      webhookInfo.CoachUserId = coachUserInfo.UserId;
      return true;
    }

    bool TryGetSessionType(Calendly.WebhookInfo webhookInfo, DbHelper.AbleUser.AbleUserInfo coachUserInfo, out DbHelper.CoachingSessionTypes.SessionType sessionType) {

      sessionType = DbHelper.CoachingSessionTypes.GetSessionTypeBySlug(webhookInfo.SessionTypeSlug);

      if (sessionType != null) return true;

      if (!coachUserInfo.EmailAddress.EndsWith("@" + ConfigHelper.NoInvalidSessionTypeAlertForEmailDomain, StringComparison.OrdinalIgnoreCase)) {

        // Send an alert to the coach about invalid Calendly "Event Type" name.
        AlbertEmails.SendGenericEmail(null,
          "Able Alert - please check your Calendly Event Types", null, $@"
          Able received a Calendly booking for the coachee:<br/>
          {webhookInfo.CoacheeName.HTMLEncode()}<br/>
          <br/>
          However, the Calendly Event Type name is unknown:<br/>
          '{webhookInfo.SessionTypeSlug.HTMLEncode()}'.<br/>
          <br/>
          Please ensure your Calendly Event Type names are within the following list:<br/>
          <ul><li>'{DbHelper.CoachingSessionTypes.SessionTypeList.FindAll(t => !t.IsGroup).Join("'</li><li>'", t => t.CalendlyUrlSlug)}'</li></ul>
          <br/>",
          false,
          ConfigHelper.Email_Coordinator_FullName, ConfigHelper.Email_Coordinator_Address,
          null, out _,
          new System.Net.Mail.MailAddress(coachUserInfo.EmailAddress, coachUserInfo.GetFullName()));
      }

      return false;
    }

    class ActiveCoacheeInfo {
      public int CoacheeId { get; private set; }
      public int CoachUserId { get; private set; }
      public int SessionsAllocated { get; private set; }
      public int SessionsBooked { get; private set; }
      public ActiveCoacheeInfo(int coacheeId, int coachUserId, int sessionsAllocated, int sessionsBooked) {
        CoacheeId = coacheeId;
        CoachUserId = coachUserId;
        SessionsAllocated = sessionsAllocated;
        SessionsBooked = sessionsBooked;
      }
    }

    // Note a single Calendly account (CalendlyUrlName) may be used by more than one Able coach, however
    // the Calendly booking doesn't provide the UrlName, only the coach email address of the account.
    // Therefore we find the coachee not by coach directly, but by all coaches using the same CalendlyUrlName.
    // Usually only 1 coachee is found. Rarely, a coachee may be active in two programs at the same time,
    // In which case we return more than one coachee, in order of program start date.
    List<ActiveCoacheeInfo> GetActiveCoacheesByCalendlyName(Calendly.WebhookInfo webhookInfo) {

      var freeCoacheeList = new List<ActiveCoacheeInfo>();

      DbHelper.Common.Query(@"
        SELECT c.CoacheeId, c.CoachUserId, c.SessionsAllocated, c.SessionsBooked
        FROM al_Coachees c
        INNER JOIN sv_User u ON u.UserId = c.CoachUserId
        INNER JOIN id_Job j ON j.JobId = c.ProgramJobId
        WHERE c.EmailAddress = @CoacheeEmail
          AND u.CalendlyUrlName = (SELECT TOP 1 CalendlyUrlName FROM sv_User WHERE Email = @CoachEmail AND CalendlyUrlName <> '')
          AND c.SessionsAllocated > 0
          AND ISNULL(c.ProgramStatusId, 0) < @CoacheeProgramStatusId_EndProgram
          AND ISNULL(j.ProgramStatusId, 0) <= @ProgramStatusId_Active
        ORDER BY j.AbleProgramStartDateUtc",
        dr => {
          freeCoacheeList.Add(
            new ActiveCoacheeInfo(
              dr.GetInt("CoacheeId"),
              dr.GetInt("CoachUserId"),
              dr.GetIntOrDefault("SessionsAllocated", 0),
              dr.GetIntOrDefault("SessionsBooked", 0)
            )
          );
        },
        DbHelper.Common.NewSqlParameter("CoacheeEmail", webhookInfo.CoacheeEmail),
        DbHelper.Common.NewSqlParameter("CoachEmail", webhookInfo.CoachEmail),
        DbHelper.Common.NewSqlParameter("CoacheeProgramStatusId_EndProgram", DbHelper.CoacheeProgramStatus.GetStatus_EndProgram().ProgramStatusId),
        DbHelper.Common.NewSqlParameter("ProgramStatusId_Active", DbHelper.AlbertProgramStatus.Statuses.Active.ProgramStatusId)
      );

      return freeCoacheeList;
    }

    string DoCreate(
      string bodyJson,
      int payloadId,
      Calendly.WebhookInfo webhookInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      // This is a new booking.

      int? newCoachingSessionId = null;

      try {
        // Add the new session record if there is a free component.
        newCoachingSessionId = DbHelper.CoachingSessions.CreateSessionInFreeComponent(null,
          coacheeInfo, webhookInfo.InviteeStartTime.Value.UtcDateTime, webhookInfo.DurationMins,
          webhookInfo.EventLocation, webhookInfo.CalendlyEvent_uuid);
      } catch (Exception ex) {
        var coacheeUserForError = coacheeInfo?.UserId.HasValue == true ? DbHelper.AbleUser.GetUserByIdOrNull(coacheeInfo.UserId.Value, DbHelper.AbleUser.RegisteredFilter.Any) : null;
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CalendlySubmit_CreateSessionInFreeComponent")
          .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeUserForError?.UserGuid))
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .WithProperty("PayloadId", payloadId)
          .WithProperty("CoacheeEmail", coacheeInfo?.EmailAddress)
          .WithProperty("DurationMins", webhookInfo?.DurationMins)
          .WithProperty("EventLocation", webhookInfo?.EventLocation)
          .Track();

        LogError(payloadId, bodyJson, webhookInfo, ex, ErrorEmailType.Technical, "Error adding CoachingSession row for coachee " + webhookInfo.CoacheeId);
        return null;
      }

      if (newCoachingSessionId == null) {
        LogError(payloadId, bodyJson, webhookInfo, null, ErrorEmailType.Technical, "Could not add new session for coachee " + webhookInfo.CoacheeId);
        return null;
      }

      // Send Intercom event for session creation - track the coachee as the primary user
      var coacheeUser = coacheeInfo.UserId.HasValue ? DbHelper.AbleUser.GetUserByIdOrNull(coacheeInfo.UserId.Value, DbHelper.AbleUser.RegisteredFilter.Any) : null;
      var coach = DbHelper.AbleUser.GetUserByIdOrNull(webhookInfo.CoachUserId, DbHelper.AbleUser.RegisteredFilter.Any);

      // Create external user ID for coach metadata

      SendEvent(
        intercom => intercom.CoachingSessionCreated()
          .WithUser(coacheeInfo.CoacheeId, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeUser.UserGuid))
          .WithSessionId(newCoachingSessionId.Value)
          .WithCoach(ConfigHelper.UserRole.Coach.ToExternalUserId(coach.UserGuid))
          .WithSessionStartUtc(new DateTimeOffset(webhookInfo.InviteeStartTime.Value.UtcDateTime, TimeSpan.Zero))
          .WithDurationMins(webhookInfo.DurationMins)
          .WithSource("calendly"),
        operationName: "CalendlySubmit_CoachingSessionCreated",
        requestRawUrl: SystemWeb.RequestRawUrl,
        telemetryProperties: new Dictionary<string, object> {
          ["SessionId"] = newCoachingSessionId.Value,
          ["CoacheeId"] = coacheeInfo.CoacheeId,
          ["CoachUserId"] = webhookInfo.CoachUserId,
          ["DurationMins"] = webhookInfo.DurationMins,
          ["PayloadId"] = payloadId
        }
      );

      // Send Amplitude event for session booking - track for coachee
      var coacheeExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeUser.UserGuid);
      var coachExternalId = ConfigHelper.UserRole.Coach.ToExternalUserId(coach.UserGuid);
      if (coacheeExternalId.HasValue) {
        var eventPropertiesCoachee = new Dictionary<string, object> {
          { AmplitudePropertyConstants.SessionId, newCoachingSessionId.Value },
          { AmplitudePropertyConstants.CoachId, coachExternalId.HasValue ? coachExternalId.Value.ToString() : null },
          { AmplitudePropertyConstants.CoacheeId, coacheeExternalId.Value.ToString() },
          { AmplitudePropertyConstants.SessionDate, webhookInfo.InviteeStartTime.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") },
          { AmplitudePropertyConstants.DurationMins, webhookInfo.DurationMins },
          { AmplitudePropertyConstants.Source, "calendly" }
        };

        SendEvent(
          amplitude => amplitude.TrackEvent(
            coacheeExternalId.Value,
            null, // deviceId
            AmplitudeEventConstants.SessionBooked,
            eventPropertiesCoachee,
            null  // userPropertyOperations
          ),
          operationName: "CalendlySubmit_SessionBooked_Coachee",
          eventName: AmplitudeEventConstants.SessionBooked,
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["SessionId"] = newCoachingSessionId.Value,
            ["CoacheeId"] = coacheeInfo.CoacheeId,
            ["PayloadId"] = payloadId
          }
        );
      }

      // Send Amplitude event for session booking - track for coach
      if (coachExternalId.HasValue) {
        var eventPropertiesCoach = new Dictionary<string, object> {
          { AmplitudePropertyConstants.SessionId, newCoachingSessionId.Value },
          { AmplitudePropertyConstants.CoachId, coachExternalId.Value.ToString() },
          { AmplitudePropertyConstants.CoacheeId, coacheeExternalId.HasValue ? coacheeExternalId.Value.ToString() : null },
          { AmplitudePropertyConstants.SessionDate, webhookInfo.InviteeStartTime.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") },
          { AmplitudePropertyConstants.DurationMins, webhookInfo.DurationMins },
          { AmplitudePropertyConstants.Source, "calendly" }
        };

        SendEvent(
          amplitude => amplitude.TrackEvent(
            coachExternalId.Value,
            null, // deviceId
            AmplitudeEventConstants.SessionBooked,
            eventPropertiesCoach,
            null  // userPropertyOperations
          ),
          operationName: "CalendlySubmit_SessionBooked_Coach",
          eventName: AmplitudeEventConstants.SessionBooked,
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["SessionId"] = newCoachingSessionId.Value,
            ["CoachUserId"] = webhookInfo.CoachUserId,
            ["PayloadId"] = payloadId
          }
        );
      }

      // Update session stats.
      return UpdateSessionStats(coacheeInfo);
    }

    string DoReschedule(
      Calendly.WebhookInfo webhookInfo,
      DbHelper.CoachingSessions.AbleSessionInfo sessionInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      if (ConfigHelper.IsDevServer) SystemWeb.ResponseWrite("<br/>Reschedule...<br/>");

      if (webhookInfo.NewEventStartTime == null) return "NewEventStartTime is null.";

      DateTime rescheduleFromUtc = ((DateTimeOffset)webhookInfo.RescheduleFromInviteeStartTime).UtcDateTime;
      string rescheduleFromDisplay = rescheduleFromUtc.UtcToTZ(null).ToString("d MMM yyyy, h:mm tt") + " WST";

      DateTime rescheduleToUtc = webhookInfo.NewEventStartTime.Value.UtcDateTime;
      string rescheduleToDisplay = rescheduleToUtc.UtcToTZ(null).ToString("d MMM yyyy, h:mm tt") + " WST";

      if (ConfigHelper.IsDevServer) {
        SystemWeb.ResponseWrite("Coachee Email: " + webhookInfo.CoacheeEmail + "<br/>");
        SystemWeb.ResponseWrite("Reschedule From: " + rescheduleFromDisplay + "<br/>");
        SystemWeb.ResponseWrite("Reschedule To: " + rescheduleToDisplay + "<br/>");
        SystemWeb.ResponseWrite("Reschedule Reason: " + webhookInfo.ReasonText.HTMLEncode() + "<br/>");
      }

      DbHelper.CoachingSessions.RescheduleUnlocked(null, sessionInfo.SessionId, rescheduleToUtc, webhookInfo.ReasonText, webhookInfo.CalendlyEvent_uuid);

      // Send Intercom event for session reschedule - track the coachee as the primary user
      var coacheeUser = coacheeInfo.UserId.HasValue ? DbHelper.AbleUser.GetUserByIdOrNull(coacheeInfo.UserId.Value, DbHelper.AbleUser.RegisteredFilter.Any) : null;
      var coachUser = sessionInfo.CoachUserId.HasValue
        ? DbHelper.AbleUser.GetUserByIdOrNull(sessionInfo.CoachUserId.Value, DbHelper.AbleUser.RegisteredFilter.Any)
        : null;

      if (coacheeUser != null && coachUser != null) {
        SendEvent(
          intercom => intercom.CoachingSessionUpdated()
            .WithUser(coacheeUser.UserId, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeUser?.UserGuid))
            .WithCoach(ConfigHelper.UserRole.Coach.ToExternalUserId(coachUser?.UserGuid))
            .WithSessionId(sessionInfo.SessionId)
            .WithOldStartTime(rescheduleFromUtc)
            .WithNewStartTime(rescheduleToUtc)
            .WithSource("calendly_reschedule"),
          operationName: "CalendlySubmit_CoachingSessionRescheduled",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["SessionId"] = sessionInfo.SessionId,
            ["CoacheeEmail"] = webhookInfo?.CoacheeEmail,
            ["OldStartTime"] = rescheduleFromUtc,
            ["NewStartTime"] = rescheduleToUtc
          }
        );
      }

      // Update session stats.
      return UpdateSessionStats(coacheeInfo);
    }

    string DoCancel(
      DbHelper.CoachingSessions.AbleSessionInfo sessionInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      // This is a cancellation.
      DbHelper.CoachingSessions.DeleteSessionUnlocked(null, sessionInfo.SessionId, coacheeInfo.CoacheeId);

      // Send Intercom event for session cancellation - track the coachee as the primary user
      var coacheeUser = coacheeInfo.UserId.HasValue ? DbHelper.AbleUser.GetUserByIdOrNull(coacheeInfo.UserId.Value, DbHelper.AbleUser.RegisteredFilter.Any) : null;
      var coachUser = sessionInfo.CoachUserId.HasValue
        ? DbHelper.AbleUser.GetUserByIdOrNull(sessionInfo.CoachUserId.Value, DbHelper.AbleUser.RegisteredFilter.Any)
        : null;

      SendEvent(
        intercom => intercom.CoachingSessionDeleted()
          .WithUser(coacheeUser.UserId, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeUser?.UserGuid))
          .WithCoach(ConfigHelper.UserRole.Coach.ToExternalUserId(coachUser?.UserGuid))
          .WithSessionId(sessionInfo.SessionId)
          .WithSource("calendly_cancellation"),
        operationName: "CalendlySubmit_CoachingSessionCancelled",
        requestRawUrl: SystemWeb.RequestRawUrl,
        telemetryProperties: new Dictionary<string, object> {
          ["SessionId"] = sessionInfo.SessionId,
          ["CoacheeId"] = coacheeInfo?.CoacheeId,
          ["CoachUserId"] = sessionInfo.CoachUserId
        }
      );

      // Update session stats.
      return UpdateSessionStats(coacheeInfo);
    }

    string UpdateSessionStats(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      // Update session stats for this coachee.
      DbHelper.CoachingSessions.SessionStats sessionStats;
      try {
        sessionStats = DbHelper.CoachingSessions.GetSessionStats(null, coacheeInfo.CoacheeId);
      } catch (Exception) {
        return "Error retrieving session stats. Please try again later.";
      }
      if (sessionStats == null) {
        return "Failed to retrieve session stats. Please try again later.";
      }
      try {
        DbHelper.AlbertCoachees.UpdateSessionStatsAndTargetDates(null, coacheeInfo, sessionStats);
      } catch (Exception) {
        return "Error updating session stats. Please try again later.";
      }

      return null; // return nothing if all ok.
    }

    bool CheckValidPayload(string bodyJson) {
      return Regex.IsMatch(bodyJson, @"^\s*{\s*""event""\s*:\s*""invitee\.", RegexOptions.IgnoreCase);
    }

    void SendErrorNotice_Technical(int payloadId, string messageHtml, string bodyJson, Exception ex = null) {

      messageHtml
        = $"<p>Payload (id={payloadId})</p>"
        + $"<p>Error: {messageHtml}</p>";

      if (ex != null) {
        messageHtml
          += $"<br/><p><b>{ex.Message}</b></p>"
          + $"<pre>{ex.StackTrace}</pre><br/>";
      }

      try { // Try to prettify the json.
        bodyJson = JToken.Parse(bodyJson).ToString(Formatting.Indented);
      } catch (Exception) { }

      messageHtml += $"<pre>{bodyJson}</pre>";

      EmailHelper.SendInternalSupportEmail(SupportEmailSubject, messageHtml);
    }

    enum ErrorEmailType {
      None, Technical, Admin
    }

    void LogError(int payloadId, string bodyJson,
      Calendly.WebhookInfo webhookInfo,
      Exception ex, ErrorEmailType emailType, string messageHtml) {

      if (messageHtml.IsNullOrEmpty() && ex != null) messageHtml = $"<p>{ex.Message}</p>";

      if (webhookInfo != null) {

        messageHtml += "<br/><p>"
          + $"Booking Type: {webhookInfo.WebhookType.ToString()}<br/>"
          + $"Coach Email: {webhookInfo.CoachEmail}<br/>"
          + $"Coachee Email: {webhookInfo.CoacheeEmail}<br/>"
          + (webhookInfo.CoacheeId == 0 ? "" : $"Coachee Link: <a href=\"{PathHelper.Pages.CoacheeEdit(webhookInfo.CoacheeId, PathHelper.CoacheeTabEnum.coaching)}>{PathHelper.Pages.CoacheeEdit(webhookInfo.CoacheeId, PathHelper.CoacheeTabEnum.coaching)}</a><br/>")
          + $"Session Type: {webhookInfo.SessionTypeName}<br/>"
          + $"Duration: {webhookInfo.DurationMins} mins<br/>";

        if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Create) {
          messageHtml +=
            $"Booking At: {webhookInfo.InviteeStartTimeString}<br/>";
        } else if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Reschedule) {
          messageHtml +=
            $"Reschedule From: {webhookInfo.InviteeStartTimeString}<br/>" +
            $"Reschedule To: {webhookInfo.NewEventStartTimeString}<br/>";
        } else if (webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Cancel) {
          messageHtml +=
            $"Cancel Booking At: {webhookInfo.InviteeStartTimeString}<br/>";
        }

        messageHtml += "</p><br/>";
      }

      if (payloadId > 0) {
        try {
          DbHelper.CalendlyPayloads.UpdatePayloadWithError(payloadId, messageHtml, ex);
        } catch (Exception) { } // ignore
      }

      if (ConfigHelper.IsDevServer) {
        SystemWeb.ResponseWrite($"{messageHtml}<br/>");
        if (ex != null) SystemWeb.ResponseWrite($"Exception: {ex.Message.HTMLEncode()}<br/>");
      }

      if (emailType == ErrorEmailType.Technical) {

        SendErrorNotice_Technical(payloadId, messageHtml, bodyJson, ex);

      } else if (emailType == ErrorEmailType.Admin) {

        EmailHelper.SendInternalSupportEmail(null,
          "Calendly Submission Error",
          "A Calendly booking was made, but encountered the following problem:<br/><br/>" + messageHtml + "<br/>",
          new MailAddress("cj@integral.global", "CJ"),
          new MailAddress("andrew@integral.global", "Andrew"));
      }
    }

  }
}
