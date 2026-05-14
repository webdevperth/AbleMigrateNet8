using System;
using Integral.Integrations;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class CalendlyPayloads {

      const int PAYLOAD_MAX_LENGTH = 6000;

      public static int SavePayload(string payload) {

        return Common.GetScalarQueryInt(@"
          INSERT INTO al_CalendlyPayloads (PayloadContent) VALUES (@PayloadContent);
          SELECT SCOPE_IDENTITY()",
          Common.NewSqlParameter("@PayloadContent", payload, PAYLOAD_MAX_LENGTH));
      }

      public static void UpdatePayload(Calendly.WebhookInfo webhookInfo) {

        Common.GetNonQueryInt(@"

          UPDATE al_CalendlyPayloads
          SET
            Payload_uuid = @Payload_uuid,
            PayloadEvent = @PayloadEvent,
            EventTypeName = @EventTypeName,
            EventTypeSlug = @EventTypeSlug,
            DurationMins = @DurationMins,
            CoacheeEmail = @CoacheeEmail,
            CoacheeId = @CoacheeId,
            CoachEmail = @CoachEmail,
            CoachUserId = @CoachUserId,
            EventStartTimeString = @EventStartTimeString,
            EventStartTimeUTC = @EventStartTimeUTC,
            InviteeStartTimeString = @InviteeStartTimeString,
            InviteeStartTimeUTC = @InviteeStartTimeUTC,
            InviteeStartTimeTZOffsetMins = @InviteeStartTimeTZOffsetMins,
            InviteeTimeZoneIANA = @InviteeTimeZoneIANA,
            IsReschedule = @IsReschedule,
            IsCancelled = @IsCancelled,
            CancelReason = @CancelReason
          WHERE
            PayloadId = @PayloadId",

          Common.NewSqlParameter("PayloadId", webhookInfo.PayloadId),
          Common.NewSqlParameter("Payload_uuid", webhookInfo.CalendlyEvent_uuid, 50),
          Common.NewSqlParameter("PayloadEvent", webhookInfo.WebhookType.ToString(), 50),
          Common.NewSqlParameter("EventTypeName", webhookInfo.SessionTypeName, 100),
          Common.NewSqlParameter("EventTypeSlug", webhookInfo.SessionTypeSlug, 50),
          Common.NewSqlParameter("DurationMins", webhookInfo.DurationMins),
          Common.NewSqlParameter("CoacheeEmail", webhookInfo.CoacheeEmail, 100),
          Common.NewSqlParameter("CoacheeId", webhookInfo.CoacheeId),
          Common.NewSqlParameter("CoachEmail", webhookInfo.CoachEmail, 100),
          Common.NewSqlParameter("CoachUserId", webhookInfo.CoachUserId),
          Common.NewSqlParameter("EventStartTimeString", webhookInfo.EventStartTimeString, 50),
          Common.NewSqlParameter("EventStartTimeUTC", webhookInfo.EventStartTime == null ? null : (DateTime?)webhookInfo.EventStartTime.Value.UtcDateTime),
          Common.NewSqlParameter("InviteeStartTimeString", webhookInfo.InviteeStartTimeString, 50),
          Common.NewSqlParameter("InviteeStartTimeUTC", webhookInfo.InviteeStartTime == null ? null : (DateTime?)webhookInfo.InviteeStartTime.Value.UtcDateTime),
          Common.NewSqlParameter("InviteeStartTimeTZOffsetMins", webhookInfo.InviteeStartTimeTZOffsetMins),
          Common.NewSqlParameter("InviteeTimeZoneIANA", webhookInfo.InviteeTimeZoneIANA, 500),
          Common.NewSqlParameter("IsReschedule", webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Reschedule),
          Common.NewSqlParameter("IsCancelled", webhookInfo.WebhookType == Calendly.WebhookTypeEnum.Cancel),
          Common.NewSqlParameter("CancelReason", webhookInfo.ReasonText.LimitLengthTo(500), 500)
        );
      }

      // Save error message to payload row.
      public static void UpdatePayloadWithError(int payloadId, string customMessage, Exception ex = null) {

        Common.GetNonQueryInt(@"

          UPDATE al_CalendlyPayloads
          SET
            HasError = @HasError,
            Error_CustomMessage = @Error_CustomMessage,
            Error_ExceptionMessage = @Error_ExceptionMessage
          WHERE
            PayloadId = @PayloadId",

          Common.NewSqlParameter("@PayloadId", payloadId),
          Common.NewSqlParameter("@HasError", true),
          Common.NewSqlParameter("@Error_CustomMessage", customMessage.LimitLengthTo(500), 500),
          Common.NewSqlParameter("@Error_ExceptionMessage", ex == null ? null : ex.Message.LimitLengthTo(500), 500)
        );
      }

      public static void DeletePayload(int payloadId) {

        Common.GetNonQueryInt(@"

          DELETE FROM al_CalendlyPayloads
          WHERE PayloadId = @PayloadId",

          Common.NewSqlParameter("@PayloadId", payloadId)
        );
      }

    }
  }
}
