using System;
using System.Collections.Generic;
using Integral.Web;
using Integral.Web.Services;
using Newtonsoft.Json;
using System.Net.Http;

namespace Integral.Integrations {

  public class Calendly {

    // CalendlySettings
    private const string PAYLOAD_TIMESTAMP_FORMAT = "yyyy-MM-ddTHH:mm:ssK";
    private const string PAYLOAD_EVENT_STRING_CANCELLED = "invitee.canceled";
    private const string PAYLOAD_EVENT_STRING_CREATED = "invitee.created";

    public enum EmbedWindowMessageEnum { event_type_viewed, date_and_time_selected, event_scheduled }
    public static string GetEmbedWindowMessageString(EmbedWindowMessageEnum message) => "calendly." + message.ToString();

    private const string APIToken = "LDEMNCDMIBM33GQXUTFWJ7NVBKPLDT5N"; // TODO put this in appSettings.config
    public const string BookingUrlDomain = "https://calendly.com";
    private const string UrlWordSeparator = "-";
    private const string BookingUrlStubPrefix = "coaching";
    private const string BookingUrlStubType_InPerson = "session";
    private const string BookingUrlStubType_Remote = "call";
    private const string BookingUrlMinsSuffix = "min";
    private const string BookingUrlNameKey = "name";
    private const string BookingUrlEmailKey = "email";
    private const string BookingUrlStubClientMetting = "client-meeting";

    // Note "a1" etc are custom question numbers that need to be present in the Calendly session type settings.
    private const string AnswerQueryKeyPrefix = "a";

    private static string GetQueryKey(int questionNo) {
      return AnswerQueryKeyPrefix + questionNo;
    }

    public static string GetBookingUrlEventName(bool inPerson, int durationMins) {
      return
        BookingUrlStubPrefix
        + UrlWordSeparator + (inPerson ? BookingUrlStubType_InPerson : BookingUrlStubType_Remote)
        + UrlWordSeparator + durationMins + BookingUrlMinsSuffix;
    }

    public static string GetCalendlyGroupBookingUrl(
      string coachCalendlyUrlName,
      string sessionTypeUrlSlug,
      string bookForName,
      string bookForEmail,
      string projectName,
      string friendlyWorkshopId,
      Guid workshopGuid,
      bool urlEncodeParams = true // pass false to skip encoding, e.g. if displaying url on screen.
    ) {
      if (urlEncodeParams) {
        bookForName = bookForName.URLEncode();
        bookForEmail = bookForEmail.URLEncode();
        projectName = projectName.URLEncode();
      }
      return BookingUrlDomain
        + "/" + coachCalendlyUrlName
        + "/" + sessionTypeUrlSlug
        + "?" + BookingUrlNameKey + "=" + bookForName.EmptyIfNull()
        + "&" + BookingUrlEmailKey + "=" + bookForEmail.EmptyIfNull()
        + "&" + GetQueryKey(ConfigHelper.Calendly_QuestionNo_GroupProjectName) + "=" + (projectName.EmptyIfNull() + friendlyWorkshopId.EmptyIfNull().SurroundWith(" (", ")"))
        + "&" + GetQueryKey(ConfigHelper.Calendly_QuestionNo_GroupWorkshopGuid) + "=" + workshopGuid.ToStringNoBraces();
    }

    public static string GetCalendlyPartnerBookingUrl(
      string coachCalendlyUrlName,
      string bookForName,
      string bookForEmail,
      bool urlEncodeParams = true // pass false to skip encoding, e.g. if displaying url on screen.
    ) {
      if (urlEncodeParams) {
        bookForName = bookForName.URLEncode();
        bookForEmail = bookForEmail.URLEncode();
      }
      return BookingUrlDomain
        + "/" + coachCalendlyUrlName
        + "/" + BookingUrlStubClientMetting
        + "?" + BookingUrlNameKey + "=" + bookForName.EmptyIfNull()
        + "&" + BookingUrlEmailKey + "=" + bookForEmail.EmptyIfNull();
    }

    public static APIWebhookList GetWebhookStatus() {
      return JsonConvert.DeserializeObject<APIWebhookList>(GetWebhookStatusJson());
    }

    public static string GetWebhookStatusJson() {
      // Get the status of the webhooks in Calendly.
      // Just used as a tool for manual testing, see \tools\calendly-status.aspx.

      using (var client = new HttpClient()) {
        client.DefaultRequestHeaders.Add("X-TOKEN", APIToken);
        var response = client.GetAsync("https://calendly.com/api/v1/hooks/").GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
      }
    }

    public static string GetPayloadTimestamp(DateTime localTime, TimeZoneInfo localTimeZone) {
      return GetPayloadTimestamp(new DateTimeOffset(localTime, localTimeZone.GetUtcOffset(localTime)));
    }

    public static string GetPayloadTimestamp(DateTimeOffset timeWithOffset) {
      return timeWithOffset.ToString(PAYLOAD_TIMESTAMP_FORMAT);
    }

    // Get json string from the webhook body.
    public static bool TryParsePayload(string bodyJson, int payloadId, out WebhookInfo webhookInfo, out Exception exception, out string errorText) {

      webhookInfo = null;
      errorText = "";
      exception = null;

      // Convert json payload to object.
      WebhookPayload webhookPayload;
      try {
        webhookPayload = JsonConvert.DeserializeObject<WebhookPayload>(bodyJson);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("Calendly_TryParsePayload_JsonDeserialize")
          .WithProperty(ApplicationInsightsConstants.PayloadId, payloadId)
          .WithProperty(ApplicationInsightsConstants.BodyJsonLength, bodyJson?.Length)
          .Track();
        exception = ex;
        errorText = "JsonConvert failed";
        return false;
      }

      // Check for essential values before continuing.
      if (webhookPayload.@event != PAYLOAD_EVENT_STRING_CREATED && webhookPayload.@event != PAYLOAD_EVENT_STRING_CANCELLED) {
        errorText = "Unknown Calendly Event Type (" + webhookPayload.@event + ").";
        return false;
      }
      if (webhookPayload.payload.@event.extended_assigned_to == null || webhookPayload.payload.@event.extended_assigned_to.Length == 0) {
        errorText = "Booking received for " + webhookPayload.payload.invitee.email + " but no Coach found in the Calendly info";
        return false;
      }

      return TryGetWebhookInfo(payloadId, webhookPayload, out webhookInfo, out exception, out errorText); ;
    }

    // Get required values from the payload object.
    private static bool TryGetWebhookInfo(int payloadId, WebhookPayload webhookPayload, out WebhookInfo webhookInfo, out Exception exception, out string errorText) {

      webhookInfo = null;
      errorText = "";
      exception = null;

      try {
        webhookInfo = new WebhookInfo() {

          WebhookType = WebhookTypeEnum.Unknown,
          PayloadId = payloadId,

          CalendlyEvent_uuid = webhookPayload.payload.@event.uuid,
          SessionTypeName = webhookPayload.payload.event_type.name, // Name of session type, e.g. "Coaching Call (90min)"
          SessionTypeSlug = webhookPayload.payload.event_type.slug, // "Slug" used in the URL e.g. "coaching-call-90min"
          DurationMins = webhookPayload.payload.event_type.duration, // Minutes duration as a number.

          CoacheeName = webhookPayload.payload.invitee.name,
          CoacheeEmail = webhookPayload.payload.invitee.email,

          CoachName = webhookPayload.payload.@event.extended_assigned_to[0].name,
          CoachEmail = webhookPayload.payload.@event.extended_assigned_to[0].email,

          InviteeTimeZoneIANA = webhookPayload.payload.invitee.timezone, // IANA time zone string of coachee.

          EventStartTimeString = webhookPayload.payload.@event.start_time,
          EventStartTime = webhookPayload.payload.@event.start_time.ToDateTimeOffsetOrNull(), // Time with offset for coach.
          EventEndTimeString = webhookPayload.payload.@event.end_time,
          EventEndTime = webhookPayload.payload.@event.end_time.ToDateTimeOffsetOrNull(), // Time with offset for coach.

          EventLocation = webhookPayload.payload.@event.location,

          NewEventStartTimeString = webhookPayload.payload.new_event?.start_time,
          NewEventStartTime = webhookPayload.payload.new_event?.start_time.ToDateTimeOffsetOrNull(),

          InviteeStartTimeString = webhookPayload.payload.@event.invitee_start_time,
          InviteeStartTime = webhookPayload.payload.@event.invitee_start_time.ToDateTimeOffsetOrNull(), // Time with offset for coachee.
          InviteeEndTimeString = webhookPayload.payload.@event.invitee_end_time,
          InviteeEndTime = webhookPayload.payload.@event.invitee_end_time.ToDateTimeOffsetOrNull(), // Time with offset for coachee.

          CalendlyOldEvent_uuid = webhookPayload.payload.old_event?.uuid,
          CalendlyNewEvent_uuid = webhookPayload.payload.new_event?.uuid,

        };
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("Calendly_TryGetWebhookInfo")
          .WithProperty(ApplicationInsightsConstants.PayloadId, payloadId)
          .WithProperty("CalendlyEventUuid", webhookPayload?.payload?.@event?.uuid)
          .WithProperty("InviteeEmail", webhookPayload?.payload?.invitee?.email)
          .Track();
        exception = ex;
        errorText = "TryGetEventInfo failed";
        return false;
      }

      // Set webhook type.
      // Note that a reschedule sends 2 payloads, first a "cancel" then a "create". The create can be ignored.
      if (webhookPayload.@event == PAYLOAD_EVENT_STRING_CREATED) {
        if (webhookPayload.payload.old_invitee?.is_reschedule.GetValueOrDefault(false) == true) {
          webhookInfo.WebhookType = WebhookTypeEnum.ReschedulePart2;
        } else {
          webhookInfo.WebhookType = WebhookTypeEnum.Create;
        }
      } else if (webhookPayload.@event == PAYLOAD_EVENT_STRING_CANCELLED) {
        if (webhookPayload.payload.invitee.is_reschedule.GetValueOrDefault(false) == true) {
          webhookInfo.WebhookType = WebhookTypeEnum.Reschedule;
          webhookInfo.RescheduleFromInviteeStartTime = webhookPayload.payload.@event?.invitee_start_time.ToDateTimeOffsetOrNull();
        } else {
          webhookInfo.WebhookType = WebhookTypeEnum.Cancel;
        }
        webhookInfo.ReasonText = webhookPayload.payload.@event?.cancel_reason;
      }

      // Get custom answers.
      if (webhookPayload?.payload?.questions_and_answers != null && webhookPayload.payload.questions_and_answers.Length > 0) {
        for (int qi = 0; qi < webhookPayload.payload.questions_and_answers.Length; qi++) {
          webhookInfo.Answers.Add(webhookPayload.payload.questions_and_answers[qi].answer);
        }
      }

      return true;
    }

    public enum WebhookTypeEnum {
      Unknown, Create, Cancel, Reschedule, ReschedulePart2
    }

    public class WebhookPayload {

      public string @event { get; set; }
      public string time { get; set; }
      // [JsonProperty(PropertyName = "time")]
      // public DateTimeOffset? TimeOffset { get; set; }
      public Payload payload { get; set; }
      public class Payload {
        public EventType event_type { get; set; }
        public class EventType {
          public string uuid { get; set; }
          public string kind { get; set; }
          public string slug { get; set; }
          public string name { get; set; }
          public int duration { get; set; } // Assume always valid number. All event types have a duration.
          public Owner owner { get; set; }
          public class Owner {
            public string type { get; set; }
            public string uuid { get; set; }
          }
        }
        public Event @event { get; set; }
        public class Event {
          public string uuid { get; set; }
          public string[] assigned_to { get; set; }
          public ExtendedAssignedTo[] extended_assigned_to { get; set; }
          public class ExtendedAssignedTo {
            public string name { get; set; }
            public string email { get; set; }
            public bool primary { get; set; }
          }
          public string start_time { get; set; }
          // [JsonProperty(PropertyName = "start_time")]
          // public DateTimeOffset? StartTimeOffset { get; set; }
          public string start_time_pretty { get; set; }
          public string invitee_start_time { get; set; }
          // [JsonProperty(PropertyName = "invitee_start_time")]
          // public DateTimeOffset? InviteeStartTimeOffset { get; set; }
          public string invitee_start_time_pretty { get; set; }
          public string end_time { get; set; }
          // [JsonProperty(PropertyName = "end_time")]
          // public DateTimeOffset? EndTimeOffset { get; set; }
          public string end_time_pretty { get; set; }
          public string invitee_end_time { get; set; }
          // [JsonProperty(PropertyName = "invitee_end_time")]
          // public DateTimeOffset? InviteeEndTimeOffset { get; set; }
          public string invitee_end_time_pretty { get; set; }
          public string created_at { get; set; }
          // [JsonProperty(PropertyName = "created_at")]
          // public DateTimeOffset? CreatedAtOffset { get; set; }
          public string location { get; set; }
          public bool? canceled { get; set; }
          public string canceler_name { get; set; }
          public string cancel_reason { get; set; }
          public string canceled_at { get; set; }
          // [JsonProperty(PropertyName = "canceled_at")]
          // public DateTimeOffset? CanceledAtOffset { get; set; }
        } // Event
        public Invitee invitee { get; set; }
        public class Invitee {
          public string uuid { get; set; }
          public string first_name { get; set; }
          public string last_name { get; set; }
          public string name { get; set; }
          public string email { get; set; }
          public string text_reminder_number { get; set; }
          public string timezone { get; set; }
          public string created_at { get; set; }
          // [JsonProperty(PropertyName = "created_at")]
          // public DateTimeOffset? CreatedAtOffset { get; set; }
          public bool? is_reschedule { get; set; }
          public Object[] payments { get; set; } // TODO: determine structure of this object.
          public bool? canceled { get; set; }
          public string canceler_name { get; set; }
          public string cancel_reason { get; set; }
          public string canceled_at { get; set; }
          // [JsonProperty(PropertyName = "canceled_at")]
          // public DateTimeOffset? CanceledAtOffset { get; set; }
        }
        public QuestionAndAnswers[] questions_and_answers { get; set; }
        public class QuestionAndAnswers {
          public string question { get; set; }
          public string answer { get; set; }
        }
        public QuestionsAndResponses questions_and_responses { get; set; }
        public class QuestionsAndResponses {
          [JsonProperty(PropertyName = "1_question")]
          public string question_1 { get; set; }
          [JsonProperty(PropertyName = "1_response")]
          public string response_1 { get; set; }
          [JsonProperty(PropertyName = "2_question")]
          public string question_2 { get; set; }
          [JsonProperty(PropertyName = "2_response")]
          public string response_2 { get; set; }
          [JsonProperty(PropertyName = "3_question")]
          public string question_3 { get; set; }
          [JsonProperty(PropertyName = "3_response")]
          public string response_3 { get; set; }
        }
        public Tracking tracking { get; set; }
        public class Tracking {
          public string utm_campaign { get; set; }
          public string utm_source { get; set; }
          public string utm_medium { get; set; }
          public string utm_content { get; set; }
          public string utm_term { get; set; }
          public string salesforce_uuid { get; set; }
        }
        public Payload.Event old_event { get; set; }
        public Payload.Invitee old_invitee { get; set; }
        public Payload.Event new_event { get; set; }
        public Payload.Invitee new_invitee { get; set; }
      }
    }

    public class APIWebhookList {
      public Data[] data;
      public class Data {
        public string type;
        public string id;
        public Attributes attributes;
        public class Attributes {
          public string url;
          public DateTimeOffset created_at;
          public string[] events;
          public string state;
        }
      }
    }

    public class WebhookInfo {

      public WebhookTypeEnum WebhookType;

      public int PayloadId;
      public string CalendlyEvent_uuid;
      public string CalendlyOldEvent_uuid;
      public string CalendlyNewEvent_uuid;
      public string SessionTypeName;
      public string SessionTypeSlug;
      public string EventLocation;
      public int DurationMins;
      public string CoacheeName;
      public string CoacheeEmail;
      public int CoacheeId; // Assigned after finding coachee by email.
      public string CoachName;
      public string CoachEmail;
      public string CalendlyUrlName;
      public int CoachUserId; // Assigned after finding coach user by email.

      public string InviteeTimeZoneIANA;

      public string EventStartTimeString;
      public DateTimeOffset? EventStartTime;
      public string EventEndTimeString;
      public DateTimeOffset? EventEndTime;

      public string NewEventStartTimeString;
      public DateTimeOffset? NewEventStartTime;

      public string InviteeStartTimeString;
      public DateTimeOffset? InviteeStartTime;
      public int? InviteeStartTimeTZOffsetMins;

      public string InviteeEndTimeString;
      public DateTimeOffset? InviteeEndTime;
      public int? InviteeEndTimeTZOffsetMins;

      public DateTimeOffset? RescheduleFromInviteeStartTime;
      public string ReasonText;

      public List<string> Answers = new List<string>();
    }

  }

}
