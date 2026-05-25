using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using Integral.Web.Services;
using static Integral.Web.DbHelper.Common;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class WorkshopEvents {

      public const int WorkshopID_Able = 88; // Dummy workshop row to use for Able workshop events.

      static WorkshopEvents() {
      }

      public enum WorkshopsForUserPeriod { All, Historical, Upcoming }

      public static List<WorkshopListInfo> GetWorkshopsForUser(int userId, int? maxRows, WorkshopsForUserPeriod period) {

        var result = new List<WorkshopListInfo>();

        string topClause = maxRows.HasValue ? $"TOP {maxRows.Value}" : "";
        string whereClause = "";

        if (period == WorkshopsForUserPeriod.Upcoming) {
          whereClause = " AND we.StartDate >= @UtcNow";
        } else if (period == WorkshopsForUserPeriod.Historical) {
          whereClause = " AND we.StartDate < @UtcNow";
        } else if (period == WorkshopsForUserPeriod.All) {
          whereClause = " AND we.StartDate IS NOT NULL";
        }

        Common.Query($@"
          SELECT {topClause} we.WorkshopEventId, we.WorkshopTitle, we.StartDate, we.EndDate, we.IANATimeZone, we.Location, we.IsVirtual,
            we.KeyFacilitatorUserId, u.FirstName as KeyFacilitatorFirstName, u.LastName as KeyFacilitatorLastName,
            ac.CoacheeId, ac.ProgramStatusId, ac.ProgramJobId, j.JobNumber
          FROM ev_WorkshopEvent we
          INNER JOIN al_Coachees ac ON ac.ProgramJobId = we.ProgramJobId
          LEFT OUTER JOIN sv_User u ON u.UserId = we.KeyFacilitatorUserId
          LEFT OUTER JOIN id_Job j ON j.JobId = we.ProgramJobId
          WHERE ac.UserId = @UserId AND ac.DeletedUtc IS NULL
            AND we.WorkshopStatusId = @Status_Confirmed
            {whereClause}
          ORDER BY we.StartDate;",
          dr => {
            result.Add(new WorkshopListInfo(
              workshopEventId: dr.GetInt("WorkshopEventId"),
              workshopTitle: dr.GetString("WorkshopTitle"),
              location: dr.GetString("Location"),
              whenStartLocal: dr.GetDateTime("StartDate"),
              whenEndLocal: dr.GetDateTime("EndDate"),
              timeZoneIdIanaOrNullForDefault: dr.GetString("IANATimeZone"),
              keyFacilitatorUserId: dr.GetInt("KeyFacilitatorUserId"),
              keyFacilitatorFirstName: dr.GetString("KeyFacilitatorFirstName"),
              keyFacilitatorLastName: dr.GetString("KeyFacilitatorLastName"),
              coacheeId: dr.GetIntOrNull("CoacheeId"),
              coacheeProgramStatusId: dr.GetInt("ProgramStatusId"),
              isVirtual: dr.GetBoolFromInt("IsVirtual", false),
              programJobId: dr.GetInt("ProgramJobId"),
              programJobNumber: dr.GetString("JobNumber")
            ));
          },
          Common.NewSqlParameter("UserId", userId),
          Common.NewSqlParameter("Status_Confirmed", DbHelper.WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId),
          Common.NewSqlParameter("UtcNow", DateTime.UtcNow)
        );

        return result;
      }

      public static WorkshopEventInfo GetWorkshopFromEvalIntakeId(AblePrograms.AbleProgramInfo programInfo, int evalIntakeCodeId) {
        return GetWorkshopInfo(
          "w.EvalIntakeCodeId = @EvalIntakeCodeId AND w.ProgramJobId = @ProgramJobId",
          Common.NewSqlParameter("ProgramJobId", programInfo.ProgramJobId),
          Common.NewSqlParameter("EvalIntakeCodeId", evalIntakeCodeId)
        );
      }

      public static WorkshopEventInfo GetWorkshopInfo(AblePrograms.AbleProgramInfo programInfo, int WorkshopEventId) {
        return GetWorkshopInfo(
          "w.WorkshopEventId = @WorkshopEventId AND w.ProgramJobId = @ProgramJobId",
          Common.NewSqlParameter("ProgramJobId", programInfo.ProgramJobId),
          Common.NewSqlParameter("WorkshopEventId", WorkshopEventId)
        );
      }

      // Unrestricted get workshop info. For "internal use" only.
      public static WorkshopEventInfo GetWorkshopInfo(int WorkshopEventId) {
        return GetWorkshopInfo(
          "w.WorkshopEventId = @WorkshopEventId",
          Common.NewSqlParameter("@WorkshopEventId", WorkshopEventId)
        );
      }
      // Unrestricted by UID. For "internal use" only.
      public static WorkshopEventInfo GetWorkshopInfo(Guid workshopEventUID) {
        return GetWorkshopInfo(
          "w.WorkshopEventUID = @WorkshopEventUID",
          Common.NewSqlParameter("@WorkshopEventUID", workshopEventUID)
        );
      }

      private static WorkshopEventInfo GetWorkshopInfo(string sqlWhereConditions, params SqlParameter[] sqlWhereParams) {
        var WorkshopInfoList = GetWorkshopInfoList(1, "", sqlWhereConditions, "", sqlWhereParams);
        if (WorkshopInfoList.Count == 0) return null;
        else return WorkshopInfoList[0];
      }

      // Return list of workshops by program.
      public static List<WorkshopEventInfo> GetWorkshopsInProgram(int programJobId) {
        return GetWorkshopInfoList(null, "",
          "w.ProgramJobId = @ProgramJobId",
          $"ORDER BY CASE WHEN w.WorkshopStatusId = @WorkshopStatusId_NotPlanned THEN 1 ELSE 0 END, ISNULL(w.StartDate, '2070-01-01')",
          Common.NewSqlParameter("@ProgramJobId", programJobId),
          Common.NewSqlParameter("@WorkshopStatusId_NotPlanned", WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId)
        );
      }

      // Return list of workshops in program for a user.
      public static List<WorkshopEventInfo> GetWorkshopsInProgramForUser(AblePrograms.AbleProgramInfo program, DbHelper.AbleUser.AbleUserInfo user) {
        return GetWorkshopInfoList(null, "",
          "w.ProgramJobId = @ProgramJobId AND (w.KeyFacilitatorUserId = @UserId OR w.CoFacilitatorUserId = @UserId)",
          "w.StartDate DESC",
          Common.NewSqlParameter("@ProgramJobId", program.ProgramJobId),
          Common.NewSqlParameter("@UserId", user.UserId)
        );
      }

      // Note we are assuming that 1 component points to 1 workshop.
      // If that changes (i.e. >1 component pointing to a workshop) then it will need to be revised.
      private static List<WorkshopEventInfo> GetWorkshopInfoList(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams
      ) {

        var workshopInfoList = new List<WorkshopEventInfo>();
        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        string sql = $@"
          SELECT {sqlTop}
            w.WorkshopEventId, w.WorkshopEventUID,
            w.ProgramJobId, j.JobNumber, j.JobName, j.CompanyId, sc.CompanyName,
            prj.ProjectName,
            j.Partner_DeliveryPercentage,
            j.Partner_UserId AS SalesUserId,  j.Partner_SalesDeliveryPercentage,
            j.LeadConsultantUserId,           j.Partner_PLCPercentage,

            w.WorkshopTitle, w.Location,
            w.IANATimeZone, w.StartDateLocal, w.EndDateLocal,
            w.KeyFacilitatorUserId, u.FirstName AS KeyFacilitatorFirstName, u.LastName AS KeyFacilitatorLastName,
            u.Email AS KeyFacilitatorEmail, u.CalendlyUrlName AS KeyFacilitatorCalendlyUrlName,
            w.CoFacilitatorUserId, uco.FirstName AS CoFacilitatorFirstName, uco.LastName AS CoFacilitatorLastName, uco.Email AS CoFacilitatorEmail,
            w.IsVirtual, w.IsRTO, w.DisableEvals, w.NotBillable, w.ParticipantAdditionalInfo, w.WorkshopNotes, w.CalendarId, w.FriendlyWorkshopId,
            w.Cancelled, w.CancelReason, w.AddParticipantsToInvite,
            w.LastEvalSentUtc,
            w.WorkshopStatusId, w.WorkshopRevenue,
            w.CoachingSessionTypeId, w.CalendlyPayloadId,
            w.EvalFirstOrLast, w.EvalScoreSum, w.EvalScoreCount,
            w.EvalIntakeCodeId, w.HideFromProgramContent,
            sv.ClonedFromSvId,
            ac.CoacheeCount,
            ws.WorkshopStatusName,
            {DbHelper.ProgramComponents.GetComponentQuoteCrossApply_SelectItems}
          FROM ev_WorkshopEvent w
          INNER JOIN id_Job j ON j.JobId = w.ProgramJobId
          LEFT OUTER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          LEFT OUTER JOIN sv_SurveyCompany sc ON sc.SvCompanyId = j.CompanyId
          LEFT OUTER JOIN sv_User u ON w.KeyFacilitatorUserId = u.UserId
          LEFT OUTER JOIN sv_User uco ON w.CoFacilitatorUserId = uco.UserId
          LEFT OUTER JOIN ev_WorkshopStatus ws ON ws.WorkshopStatusId = w.WorkshopStatusId
          LEFT OUTER JOIN sv_Survey sv ON sv.WorkshopEventId = w.WorkshopEventId
          CROSS APPLY (
            SELECT COUNT(*) AS CoacheeCount FROM al_Coachees ac WHERE ac.ProgramJobId = w.ProgramJobId AND ac.DeletedUtc IS NULL
          ) AS ac
          {DbHelper.ProgramComponents.GetComponentQuoteCrossApply("apc.WorkshopEventId = w.WorkshopEventId")}
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        Common.Query(
          sql,
          dr => {
            var wsInfo = new WorkshopEventInfo(
              workshopEventId: dr.GetInt("WorkshopEventId"),
              workshopEventUID: dr.GetGuid("WorkshopEventUID"),
              programJobId: dr.GetIntOrNull("ProgramJobId"),
              programJobNumber: dr.GetString("JobNumber"),
              programJobName: dr.GetString("JobName"),
              projectName: dr.GetString("ProjectName"),
              companyId: dr.GetIntOrNull("CompanyId"),
              companyName: dr.GetString("CompanyName"),
              workshopTitle: dr.GetString("WorkshopTitle"),
              location: dr.GetString("Location"),
              timeZoneIdIanaOrNullForDefault: dr.GetString("IANATimeZone"),
              whenStartLocal: dr.GetDateTimeOffsetOrNull("StartDateLocal").ToDateTimeOrNull(),
              whenEndLocal: dr.GetDateTimeOffsetOrNull("EndDateLocal").ToDateTimeOrNull(),
              keyFacilitatorUserId: dr.GetIntOrNull("KeyFacilitatorUserId"),
              keyFacilitatorFirstName: dr.GetString("KeyFacilitatorFirstName"),
              keyFacilitatorLastName: dr.GetString("KeyFacilitatorLastName"),
              keyFacilitatorEmail: dr.GetString("KeyFacilitatorEmail"),
              keyFacilitatorCalendlyUrlName: dr.GetString("KeyFacilitatorCalendlyUrlName"),
              coFacilitatorUserId: dr.GetIntOrNull("CoFacilitatorUserId"),
              coFacilitatorFirstName: dr.GetString("CoFacilitatorFirstName"),
              coFacilitatorLastName: dr.GetString("CoFacilitatorLastName"),
              coFacilitatorEmail: dr.GetString("CoFacilitatorEmail"),
              workshopStatusId: dr.GetIntOrNull("WorkshopStatusId"),
              workshopStatusName: dr.GetString("WorkshopStatusName"),
              workshopRevenue: dr.GetDecimalOrNull("WorkshopRevenue"),
              isVirtual: dr.GetBoolFromInt("IsVirtual"),
              isRTO: dr.GetBoolFromInt("IsRTO"),
              addParticipantsToInvite: dr.GetBoolFromInt("AddParticipantsToInvite"),
              disableEvals: dr.GetBoolFromInt("DisableEvals"),
              notBillable: dr.GetBoolFromInt("NotBillable"),
              friendlyWorkshopId: dr.GetString("FriendlyWorkshopId"),
              participantAdditionalInfo: dr.GetString("ParticipantAdditionalInfo"),
              workshopNotes: dr.GetString("WorkshopNotes"),
              attendeeCount: dr.GetInt("CoacheeCount"),
              calendarId: dr.GetString("CalendarId"),
              cancelled: dr.GetDateTimeOrNull("Cancelled"),
              cancelReason: dr.GetString("CancelReason"),
              lastEvalSentUtc: dr.GetDateTimeOrNull("LastEvalSentUtc"),
              programDeliveryPercentage: dr.GetDecimalOrNull("Partner_DeliveryPercentage"),
              programSalesUserId: dr.GetIntOrNull("SalesUserId"),
              programSalesPercentage: dr.GetDecimalOrNull("Partner_SalesDeliveryPercentage"),
              programPLCUserId: dr.GetIntOrNull("LeadConsultantUserId"),
              programPLCPercentage: dr.GetDecimalOrNull("Partner_PLCPercentage"),
              sessionTypeId: dr.GetIntOrNull("CoachingSessionTypeId"),
              calendlyPayloadId: dr.GetIntOrNull("CalendlyPayloadId"),
              evalFirstOrLast: dr.GetIntOrNull("EvalFirstOrLast"),
              evalScoreSum: dr.GetIntOrNull("EvalScoreSum"),
              evalScoreCount: dr.GetIntOrNull("EvalScoreCount"),
              evalIntakeCodeId: dr.GetIntOrNull("EvalIntakeCodeId"),
              hideFromProgramContent: dr.GetBoolFromInt("HideFromProgramContent"),
              evalTemplateSurveyId: dr.GetIntOrNull("ClonedFromSvId"),
              new DbHelper.ProgramComponents.ComponentQuoteInfo(dr)
            );
            workshopInfoList.Add(wsInfo);
          },
          sqlWhereParams
        );
        return workshopInfoList;
      }

      public static List<WorkshopEventInfo> GetUpcomingWorkshopsForCoach(int coachUserId, DateTime earliestItemDateUtc) {
        return GetWorkshopInfoList(null, "",
          @"WHERE j.ProgramStatusId <> @ProgramStatusIdClosed
              AND (w.KeyFacilitatorUserId = @CoachUserId OR w.CoFacilitatorUserId = @CoachUserId)
              AND w.StartDate >= @EarliestItemDateUtc
              AND NOT EXISTS(SELECT NULL FROM id_PayRunItems pri WHERE pri.WorkshopEventId = w.WorkshopEventId)",
          $"IIF(w.WorkshopStatusId = @WorkshopStatusId_NotPlanned, 1, 0), ISNULL(w.StartDate, '2070-01-01'), w.WorkshopEventId",
          Common.NewSqlParameter("CoachUserId", coachUserId),
          Common.NewSqlParameter("EarliestItemDateUtc", earliestItemDateUtc),
          Common.NewSqlParameter("ProgramStatusIdClosed", AlbertProgramStatus.Ids.Closed),
          Common.NewSqlParameter("WorkshopStatusId_NotPlanned", DbHelper.WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId));
      }

      public class NewWorkshopInfo {
        public int WorkshopEventId { get; internal set; }
        public string FriendlyWorkshopId { get; internal set; }
      }

      // Add workshop row and automatically assign a new sequential FriendlyWorkshopId string.
      // The inserted WorkshopEventId and FriendlyWorkshopId values are returned in the given WorkshopEventInfo object.
      public static void AddWorkshopEvent(WorkshopEventInfo wsInfo) {

        // Ensure auto values are all set based on local time and timezone.
        wsInfo.SetTime(wsInfo.WhenStartLocal, wsInfo.WhenEndLocal, wsInfo.TimeZoneIdIana);

        string sql = @"
          INSERT INTO ev_WorkshopEvent (
            WorkshopId, ProgramJobId, WorkshopTitle, Location,
            IANATimeZone, TimeZoneIdWindows, StartDate, EndDate, StartDateUtc, EndDateUtc, StartDateLocal, EndDateLocal,
            KeyFacilitatorUserId, CoFacilitatorUserId, IsVirtual, IsRTO, AddParticipantsToInvite, DisableEvals, NotBillable,
            WorkshopStatusId, WorkshopRevenue, ParticipantAdditionalInfo, WorkshopNotes, CoachingSessionTypeId,
            FriendlyWorkshopId, HideFromProgramContent
          )
          OUTPUT INSERTED.WorkshopEventId, INSERTED.FriendlyWorkshopId
          VALUES (
            @WorkshopId, @ProgramJobId, @WorkshopTitle, @Location,
            @IANATimeZone, @TimeZoneIdWindows, @StartDate, @EndDate, @StartDateUtc, @EndDateUtc, @StartDateLocal, @EndDateLocal,
            @KeyFacilitatorUserId, @CoFacilitatorUserId, @IsVirtual, @IsRTO, @AddParticipantsToInvite, @DisableEvals, @NotBillable,
            @WorkshopStatusId, @WorkshopRevenue, @ParticipantAdditionalInfo, @WorkshopNotes, @CoachingSessionTypeId,
            ( SELECT
              CONCAT(@ProgramJobNumber, '_', MAX(
                CASE WHEN ISNUMERIC(SUBSTRING(we.FriendlyWorkshopId, CHARINDEX('_', we.FriendlyWorkshopId) + 1, 10)) = 1
                  THEN CAST(SUBSTRING(we.FriendlyWorkshopId, CHARINDEX('_', we.FriendlyWorkshopId) + 1, 10) AS INT)
                  ELSE 1
                END) + 1)
              FROM ev_WorkshopEvent we
              WHERE we.FriendlyWorkshopId LIKE @ProgramJobNumber + '_%' ), @HideFromProgramContent
          )";

        // Use transaction to wrap workshop and component updates.
        Common.UsingTransaction(trans => {

          using (var cmd = new SqlCommand(sql, trans.Connection, trans)) {

            cmd.Parameters.AddInt("@WorkshopId", WorkshopID_Able);
            cmd.Parameters.AddInt("@ProgramJobId", wsInfo.ProgramJobId);
            cmd.Parameters.AddVarChar("@ProgramJobNumber", 20, wsInfo.ProgramJobNumber);
            cmd.Parameters.AddVarChar("@WorkshopTitle", 500, wsInfo.WorkshopTitle);
            cmd.Parameters.AddVarChar("@Location", 500, wsInfo.Location);
            cmd.Parameters.AddVarChar("@IANATimeZone", 200, wsInfo.TimeZoneIdIana);
            cmd.Parameters.AddVarChar("@TimeZoneIdWindows", 100, wsInfo.TimeZoneIdWindows);
            cmd.Parameters.AddDateTime("@StartDate", wsInfo.WhenStartDefaultTZ);
            cmd.Parameters.AddDateTime("@EndDate", wsInfo.WhenEndDefaultTZ);
            cmd.Parameters.AddDateTime("@StartDateUtc", wsInfo.WhenStartUtc);
            cmd.Parameters.AddDateTime("@EndDateUtc", wsInfo.WhenEndUtc);
            cmd.Parameters.AddDateTimeOffset("@StartDateLocal", wsInfo.WhenStartLocal.ToDateTimeOffset(wsInfo.TimeZoneIdIana));
            cmd.Parameters.AddDateTimeOffset("@EndDateLocal", wsInfo.WhenEndLocal.ToDateTimeOffset(wsInfo.TimeZoneIdIana));
            cmd.Parameters.AddInt("@KeyFacilitatorUserId", wsInfo.KeyFacilitatorUserId);
            cmd.Parameters.AddInt("@CoFacilitatorUserId", wsInfo.CoFacilitatorUserId);
            cmd.Parameters.AddInt("@CoachingSessionTypeId", wsInfo.SessionTypeId);
            cmd.Parameters.AddTinyIntFromBool("@IsVirtual", wsInfo.IsVirtual);
            cmd.Parameters.AddTinyIntFromBool("@IsRTO", wsInfo.IsRTO);
            cmd.Parameters.AddTinyIntFromBool("@AddParticipantsToInvite", wsInfo.AddParticipantsToInvite);
            cmd.Parameters.AddTinyIntFromBool("@DisableEvals", wsInfo.DisableEvals);
            cmd.Parameters.AddTinyIntFromBool("@NotBillable", wsInfo.NotBillable);
            cmd.Parameters.AddInt("@WorkshopStatusId", wsInfo.WorkshopStatusId);
            cmd.Parameters.AddDecimal("@WorkshopRevenue", wsInfo.WorkshopRevenue);
            cmd.Parameters.AddVarCharMax("@WorkshopNotes", wsInfo.WorkshopNotes);
            cmd.Parameters.AddVarChar("@ParticipantAdditionalInfo", 1000, wsInfo.ParticipantAdditionalInfo);
            cmd.Parameters.AddTinyIntFromBool("@HideFromProgramContent", wsInfo.HideFromProgramContent);

            // Keep trying until no unique key error or some other error.
            int retryCount = 0;
            while (true) {
              try {
                using (var dr = cmd.ExecuteReader()) {
                  if (dr.Read()) {
                    wsInfo.WorkshopEventId = dr.GetInt("WorkshopEventId");
                    wsInfo.FriendlyWorkshopId = dr.GetString("FriendlyWorkshopId");
                    break; // success, exit loop.
                  }
                }
              } catch (Exception ex) {
                var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
                telemetry?.Exception(ex)
                  .WithOperation(nameof(AddWorkshopEvent))
                  .WithOperationContext("GenerateUniqueFriendlyId")
                  .WithProperty(DalApplicationInsightsConstants.ProgramJobId, wsInfo?.ProgramJobId)
                  .WithProperty(DalApplicationInsightsConstants.ProgramJobNumber, wsInfo?.ProgramJobNumber)
                  .WithProperty(DalApplicationInsightsConstants.WorkshopTitle, wsInfo?.WorkshopTitle)
                  .WithProperty(DalApplicationInsightsConstants.RetryCount, retryCount)
                  .WithProperty(DalApplicationInsightsConstants.IsDuplicateKey, IsDuplicateKeyError(ex))
                  .Track();

                if (!IsDuplicateKeyError(ex)) throw;
                retryCount++;
                if (retryCount > 10) {
                  // If this exception occurs, check that all FriendlyWorkshopId values in the table are the correct format.
                  throw new ApplicationException("Unable to assign new WorkshopEvent ID.");
                }
              }
            }
          }

          ProgramComponents.UpsertWorkshop(trans,
            wsInfo.WorkshopEventId, (int)wsInfo.ProgramJobId, wsInfo.KeyFacilitatorUserId,
            wsInfo.WorkshopRevenue, wsInfo.WhenStartLocal.ToUniversalTimeOrNull(null), wsInfo.ComponentQuoteInfo.QuoteItemId);

          return true; // Commit transaction.
        });
      }

      public static void UpdateWorkshopEvent(WorkshopEventInfo workshopInfo) {

        if (workshopInfo == null) throw new ArgumentException("wsInfo is null.");

        // Ensure auto values are all set based on local time and timezone.
        workshopInfo.SetTime(workshopInfo.WhenStartLocal, workshopInfo.WhenEndLocal, workshopInfo.TimeZoneIdIana);

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = @"
            UPDATE ev_WorkshopEvent SET
              LastUpdatedUtc = SYSUTCDATETIME(),
              ProgramJobId = @ProgramJobId,
              WorkshopTitle = @WorkshopTitle,
              Location = @Location,
              IANATimeZone = @IANATimeZone,
              TimeZoneIdWindows = @TimeZoneIdWindows,
              StartDate = @StartDate,
              EndDate = @EndDate,
              StartDateUtc = @StartDateUtc,
              EndDateUtc = @EndDateUtc,
              StartDateLocal = @StartDateLocal,
              EndDateLocal = @EndDateLocal,
              KeyFacilitatorUserId = @KeyFacilitatorUserId,
              CoFacilitatorUserId = @CoFacilitatorUserId,
              CoachingSessionTypeId = @CoachingSessionTypeId,
              WorkshopStatusId = @WorkshopStatusId,
              WorkshopRevenue = @WorkshopRevenue,
              IsVirtual = @IsVirtual,
              IsRTO = @IsRTO,
              AddParticipantsToInvite = @AddParticipantsToInvite,
              DisableEvals = @DisableEvals,
              NotBillable = @NotBillable,
              ParticipantAdditionalInfo = @ParticipantAdditionalInfo,
              WorkshopNotes = @WorkshopNotes,
              HideFromProgramContent = @HideFromProgramContent
            WHERE WorkshopEventId = @WorkshopEventId
              AND WorkshopEventUID = @WorkshopEventUID
          ";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@WorkshopEventId", workshopInfo.WorkshopEventId);
            cmd.Parameters.AddGuid("@WorkshopEventUID", workshopInfo.WorkshopEventUID);
            cmd.Parameters.AddInt("@ProgramJobId", workshopInfo.ProgramJobId);
            cmd.Parameters.AddVarChar("@WorkshopTitle", 500, workshopInfo.WorkshopTitle);
            cmd.Parameters.AddVarChar("@Location", 500, workshopInfo.Location);
            cmd.Parameters.AddVarChar("@IANATimeZone", 200, workshopInfo.TimeZoneIdIana);
            cmd.Parameters.AddVarChar("@TimeZoneIdWindows", 200, workshopInfo.TimeZoneIdWindows);
            cmd.Parameters.AddDateTime("@StartDate", workshopInfo.WhenStartDefaultTZ);
            cmd.Parameters.AddDateTime("@EndDate", workshopInfo.WhenEndDefaultTZ);
            cmd.Parameters.AddDateTime("@StartDateUtc", workshopInfo.WhenStartUtc);
            cmd.Parameters.AddDateTime("@EndDateUtc", workshopInfo.WhenEndUtc);
            cmd.Parameters.AddDateTimeOffset("@StartDateLocal", workshopInfo.WhenStartLocal.ToDateTimeOffset(workshopInfo.TimeZoneIdIana));
            cmd.Parameters.AddDateTimeOffset("@EndDateLocal", workshopInfo.WhenEndLocal.ToDateTimeOffset(workshopInfo.TimeZoneIdIana));
            cmd.Parameters.AddInt("@WorkshopStatusId", workshopInfo.WorkshopStatusId);
            cmd.Parameters.AddDecimal("@WorkshopRevenue", workshopInfo.WorkshopRevenue);
            cmd.Parameters.AddInt("@KeyFacilitatorUserId", workshopInfo.KeyFacilitatorUserId);
            cmd.Parameters.AddInt("@CoFacilitatorUserId", workshopInfo.CoFacilitatorUserId);
            cmd.Parameters.AddInt("@CoachingSessionTypeId", workshopInfo.SessionTypeId);
            cmd.Parameters.AddTinyIntFromBool("@IsVirtual", workshopInfo.IsVirtual);
            cmd.Parameters.AddTinyIntFromBool("@IsRTO", workshopInfo.IsRTO);
            cmd.Parameters.AddTinyIntFromBool("@AddParticipantsToInvite", workshopInfo.AddParticipantsToInvite);
            cmd.Parameters.AddTinyIntFromBool("@DisableEvals", workshopInfo.DisableEvals);
            cmd.Parameters.AddTinyIntFromBool("@NotBillable", workshopInfo.NotBillable);
            cmd.Parameters.AddVarChar("@ParticipantAdditionalInfo", 1000, workshopInfo.ParticipantAdditionalInfo);
            cmd.Parameters.AddVarCharMax("@WorkshopNotes", workshopInfo.WorkshopNotes);
            cmd.Parameters.AddTinyIntFromBool("@HideFromProgramContent", workshopInfo.HideFromProgramContent);
            conn.Open();
            cmd.ExecuteNonQuery();
          }
        }
        ProgramComponents.UpsertWorkshop(
          null, workshopInfo.WorkshopEventId, (int)workshopInfo.ProgramJobId, workshopInfo.KeyFacilitatorUserId,
          workshopInfo.WorkshopRevenue, workshopInfo.WhenStartLocal.ToUniversalTimeOrNull(null), workshopInfo.ComponentQuoteInfo.QuoteItemId);
      }

      public static void UpdateLastEvalSent(SqlTransaction trans, WorkshopEventInfo wsInfo) {
        UpdateLastEvalSent(trans, wsInfo.WorkshopEventId, wsInfo.LastEvalSentUtc);
      }
      public static void UpdateLastEvalSent(SqlTransaction trans, int workshopEventId, DateTime? lastEvalSentUtc) {
        Common.GetNonQueryInt(trans, $@"
          UPDATE ev_WorkshopEvent
          SET LastEvalSentUtc = @LastEvalSentUtc
          WHERE WorkshopEventId = @WorkshopEventId",
          Common.NewSqlParameter("WorkshopEventId", workshopEventId),
          Common.NewSqlParameter("LastEvalSentUtc", lastEvalSentUtc));
      }

      public static bool MoveWorkshopToProgram(int workshopEventId, int toProgramJobId) {

        var fromWorkshop = GetWorkshopInfo(workshopEventId);
        if (fromWorkshop?.ProgramJobId == null) return false;

        int fromProgramJobId = fromWorkshop.ProgramJobId.Value;

        return Common.GetScalarQueryInt(null, $@"

          BEGIN TRANSACTION;

          -- Only update if no locked components (or no components at all) exist.
          IF NOT EXISTS (SELECT 1 FROM al_Component cmp WHERE WorkshopEventId = @WorkshopEventId AND LockedDateUtc IS NOT NULL)
          AND EXISTS(SELECT 1 FROM id_Job WHERE JobId = @ToProgramJobId)
          BEGIN

            UPDATE al_Component
            SET ProgramJobId = @ToProgramJobId
            WHERE WorkshopEventId = @WorkshopEventId;

            UPDATE ev_WorkshopEvent
            SET ProgramJobId = @ToProgramJobId,
                LastUpdatedUtc = SYSUTCDATETIME()
            WHERE WorkshopEventId = @WorkshopEventId;

            UPDATE id_Job
              SET LastUpdatedUtc = SYSUTCDATETIME()
            WHERE JobId = @FromProgramJobId
               OR JobId = @ToProgramJobId;

          END;

          SELECT @@ROWCOUNT;

          COMMIT TRANSACTION;",

          Common.NewSqlParameter("WorkshopEventId", workshopEventId),
          Common.NewSqlParameter("FromProgramJobId", fromProgramJobId),
          Common.NewSqlParameter("ToProgramJobId", toProgramJobId)

        ) > 0;
      }

      public static bool DeleteWorkshop(SqlTransaction trans, int workshopEventId) {

        bool deleted = false;

        // Ensure this is done in a transaction (will use given trans if provided, or will make a new one if it's null).
        Common.UsingTransaction(trans, _ => {

          // Delete component - note it won't be deleted if it's locked.
          ProgramComponents.DeleteWorkshop(trans, workshopEventId);

          // Delete if component was deleted.
          deleted = Common.GetNonQueryInt(trans, $@"
            IF NOT EXISTS(SELECT 1 FROM al_Component WHERE WorkshopEventId = @WorkshopEventId)
            BEGIN

              UPDATE ij
                SET ij.LastUpdatedUtc = SYSUTCDATETIME()
              FROM id_Job ij
              INNER JOIN ev_WorkshopEvent we ON we.ProgramJobId = ij.JobId
              WHERE we.WorkshopEventId = @WorkshopEventId;

              DELETE FROM al_WorkshopAttendance WHERE WorkshopEventId = @WorkshopEventId;

              DELETE FROM ev_WorkshopEvent WHERE WorkshopEventId = @WorkshopEventId;

            END;",
            Common.NewSqlParameter("WorkshopEventId", workshopEventId)
          ) > 0;

          return true;
        });

        return deleted;
      }

      public static List<Actions_WorkshopsToConfirm> GetActions_WorkshopsToConfirm(int partnerId) {

        var list = new List<Actions_WorkshopsToConfirm>();

        Common.Query(@"
          SELECT
            w.WorkshopEventId, w.WorkshopTitle, w.StartDate,
            ws.WorkshopStatusName,
            j.JobId, j.JobNumber, j.CompanyId, j.JobName as ProgramName, j.ProgramStatusId, j.LeadConsultantUserId, j.ProjectCoordinatorUserId,
            prj.ProjectName,
            cmp.CompanyName
          FROM ev_WorkshopEvent w
          INNER JOIN id_Job j ON j.JobId = w.ProgramJobId
          INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          INNER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = j.CompanyId
          LEFT JOIN ev_WorkshopStatus ws ON ws.WorkshopStatusId = w.WorkshopStatusId
          WHERE (w.WorkshopStatusId = @WorkshopStatus_Estimated OR w.WorkshopStatusId = @WorkshopStatus_NotPlanned OR w.WorkshopStatusId = @WorkshopStatus_Postponed)
            AND (j.ProgramStatusId = @ProgramStatus_Setup OR j.ProgramStatusId = @ProgramStatus_Active)
            AND (j.LeadConsultantUserId = @PartnerId OR j.ProjectCoordinatorUserId = @PartnerId)
          ORDER BY w.WorkshopStatusId ASC, CASE WHEN w.StartDate is null THEN 1 ELSE 0 END, w.StartDate ",
          dr => {
            list.Add(new Actions_WorkshopsToConfirm(
              dr.GetInt("JobId"),
              dr.GetInt("WorkshopEventId"),
              dr.GetString("WorkshopTitle"),
              dr.GetString("ProgramName"),
              dr.GetString("JobNumber"),
              dr.GetString("ProjectName"),
              dr.GetString("CompanyName"),
              dr.GetDateTimeOrNull("StartDate"),
              dr.GetString("WorkshopStatusName")
            ));
          },
          Common.NewSqlParameter("@ProgramStatus_Setup", AlbertProgramStatus.Ids.Setup),
          Common.NewSqlParameter("@ProgramStatus_Active", AlbertProgramStatus.Ids.Active),
          Common.NewSqlParameter("@WorkshopStatus_Estimated", WorkshopStatus.WorkshopStatus_Estimated.WorkshopStatusId),
          Common.NewSqlParameter("@WorkshopStatus_NotPlanned", WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId),
          Common.NewSqlParameter("@WorkshopStatus_Postponed", WorkshopStatus.WorkshopStatus_Postponed.WorkshopStatusId),
          Common.NewSqlParameter("@PartnerId", partnerId)
        );

        return list;
      }

      public static List<DbHelper.WorkshopEvents.AttendanceInfo> GetAttendanceList(WorkshopEventInfo workshopEvent) {

        if (workshopEvent == null) return null;

        var list = new List<DbHelper.WorkshopEvents.AttendanceInfo>();

        Query(@"
          SELECT
            c.CoacheeId, c.FirstName, c.LastName,
            wa.ConfirmedDateUtc,
            u.FirstName AS ConfirmerFirstName, u.LastName AS ConfirmerLastName
          FROM al_Coachees c
          LEFT JOIN al_WorkshopAttendance wa ON wa.CoacheeId = c.CoacheeId AND wa.WorkshopEventId = @WorkshopEventId
          LEFT JOIN sv_User u ON u.UserId = wa.ConfirmedByUserId
          WHERE c.ProgramJobId = @ProgramJobId AND c.DeletedUtc IS NULL
          ORDER BY c.FirstName, c.LastName",
          dr => {
            list.Add(new DbHelper.WorkshopEvents.AttendanceInfo {
              CoacheeId = dr.GetInt("CoacheeId"),
              CoacheeFirstName = dr.GetString("FirstName"),
              CoacheeSurname = dr.GetString("LastName"),
              ConfirmedDateTimeUtc = dr.GetDateTimeOrNull("ConfirmedDateUtc"),
              ConfirmedByUserFirstName = dr.GetString("ConfirmerFirstName"),
              ConfirmedByUserLastName = dr.GetString("ConfirmerLastName")
            });
          },
          NewSqlParameter("WorkshopEventId", workshopEvent.WorkshopEventId),
          NewSqlParameter("ProgramJobId", workshopEvent.ProgramJobId)
        );

        return list;
      }

      public static void UpdateAttendance(WorkshopEventInfo workshopEvent, List<int> selectedCoacheeIds, AbleUser.UserIdentity confirmedByUser) {

        UsingTransaction(trans => {

          // Get existing attendance coachee IDs for this workshop.
          var existingCoacheeIds = new List<int>();

          Query(trans, @"
            SELECT CoacheeId
            FROM al_WorkshopAttendance WITH(ROWLOCK, SERIALIZABLE)
            WHERE WorkshopEventId = @WorkshopEventId",
            dr => {
              existingCoacheeIds.Add(dr.GetInt("CoacheeId"));
            },
            NewSqlParameter("WorkshopEventId", workshopEvent.WorkshopEventId)
          );

          var coacheeIdsToDelete = existingCoacheeIds.Except(selectedCoacheeIds).ToList();
          var coacheeIdsToInsert = selectedCoacheeIds.Except(existingCoacheeIds).ToList();

          // Delete attendance rows for coachees no longer marked as attending.
          foreach (var coacheeId in coacheeIdsToDelete) {
            GetNonQueryInt(trans, @"
              DELETE FROM al_WorkshopAttendance
              WHERE WorkshopEventId = @WorkshopEventId AND CoacheeId = @CoacheeId",
              NewSqlParameter("WorkshopEventId", workshopEvent.WorkshopEventId),
              NewSqlParameter("CoacheeId", coacheeId)
            );
          }

          // Insert attendance rows for newly attending coachees.
          var nowUtc = DateTime.UtcNow;
          foreach (var coacheeId in coacheeIdsToInsert) {
            GetNonQueryInt(trans, @"
              INSERT INTO al_WorkshopAttendance (WorkshopEventId, CoacheeId, ConfirmedByUserId, ConfirmedDateUtc)
              VALUES (@WorkshopEventId, @CoacheeId, @ConfirmedByUserId, @ConfirmedDateUtc)",
              NewSqlParameter("WorkshopEventId", workshopEvent.WorkshopEventId),
              NewSqlParameter("CoacheeId", coacheeId),
              NewSqlParameter("ConfirmedByUserId", confirmedByUser.UserId),
              NewSqlParameter("ConfirmedDateUtc", nowUtc)
            );
          }

          return true;
        });
      }

      public class Actions_WorkshopsToConfirm {
        public int WorkshopEventId { get; private set; }
        public string WorkshopTitle { get; set; }
        public int ProgramJobId { get; set; }
        public string JobNumber { get; set; }
        public string ProgramName { get; set; }
        public string ProjectName { get; set; }
        public string CompanyName { get; set; }
        public DateTime? StartDate { get; private set; }
        public string WorkshopStatusName { get; set; }
        public Actions_WorkshopsToConfirm(
          int programJobId,
          int workshopEventId,
          string workshopTitle,
          string programName,
          string jobNumber,
          string projectName,
          string companyName,
          DateTime? startDate,
          string workshopStatusName
        ) {
          this.WorkshopEventId = workshopEventId;
          this.WorkshopTitle = workshopTitle;
          this.ProgramJobId = programJobId;
          this.JobNumber = jobNumber;
          this.ProgramName = programName;
          this.ProjectName = projectName;
          this.CompanyName = companyName;
          this.StartDate = startDate;
          this.WorkshopStatusName = workshopStatusName;
        }
      }

      public class WorkshopEventInfo {

        public int WorkshopEventId { get; internal set; }
        public Guid WorkshopEventUID { get; private set; }
        public int? ProgramJobId { get; set; }
        public string ProgramJobNumber { get; set; }
        public string ProgramJobName { get; set; }
        public string ProjectName { get; set; }
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string WorkshopTitle { get; set; }
        public string Location { get; set; }

        public string TimeZoneIdIana { get; private set; }
        public string TimeZoneIdWindows { get; private set; }
        public TimeZoneInfo TimeZoneInfo { get; private set; }
        public DateTime? WhenStartLocal { get; private set; }
        public DateTime? WhenEndLocal { get; private set; }
        public DateTime? WhenStartUtc { get; private set; }
        public DateTime? WhenEndUtc { get; private set; }
        public DateTime? WhenStartDefaultTZ { get; private set; }
        public DateTime? WhenEndDefaultTZ { get; private set; }

        public int? KeyFacilitatorUserId { get; set; }
        public string KeyFacilitatorFirstName { get; set; }
        public string KeyFacilitatorLastName { get; set; }
        public string KeyFacilitatorEmail { get; set; }
        public string KeyFacilitatorCalendlyUrlName { get; private set; }
        public int? CoFacilitatorUserId { get; set; }
        public string CoFacilitatorFirstName { get; set; }
        public string CoFacilitatorLastName { get; set; }
        public string CoFacilitatorEmail { get; set; }
        public int? WorkshopStatusId { get; set; }
        public string WorkshopStatusName { get; set; }
        public decimal? WorkshopRevenue { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsRTO { get; set; }
        public bool AddParticipantsToInvite { get; set; }
        public bool DisableEvals { get; set; }
        public bool NotBillable { get; set; }
        public string FriendlyWorkshopId { get; set; }
        public string ParticipantAdditionalInfo { get; set; }
        public string WorkshopNotes { get; set; }
        public string CalendarId { get; set; }
        public DateTime? Cancelled { get; set; }
        public string CancelReason { get; set; }
        public int AttendeeCount { get; set; }
        public DateTime? LastEvalSentUtc { get; set; }
        public decimal? ProgramDeliveryPercentage { get; internal set; }
        public int? ProgramSalesUserId { get; internal set; }
        public decimal? ProgramSalesPercentage { get; internal set; }
        public int? ProgramPLCUserId { get; internal set; }
        public decimal? ProgramPLCPercentage { get; internal set; }
        public int? SessionTypeId { get; set; }
        public int? CalendlyPayloadId { get; internal set; }
        public int? EvalFirstOrLast { get; private set; }
        public int? EvalScoreSum { get; private set; }
        public int? EvalScoreCount { get; private set; }
        public int? EvalIntakeCodeId { get; private set; }
        public bool HideFromProgramContent { get; set; }
        public int? EvalTemplateSurveyId { get; set; }
        public DbHelper.ProgramComponents.ComponentQuoteInfo ComponentQuoteInfo { get; set; }

        public WorkshopEventInfo(int programJobId, string programJobNumber) {
          // Essential & default values for new workshop.
          this.ProgramJobId = programJobId;
          this.ProgramJobNumber = programJobNumber;
          SetTime(null, null, ConfigHelper.DefaultTimeZoneIdIana);
          this.ComponentQuoteInfo = new DbHelper.ProgramComponents.ComponentQuoteInfo();
        }

        public WorkshopEventInfo(
          int workshopEventId,
          Guid workshopEventUID,
          int? programJobId,
          string programJobNumber,
          string programJobName,
          string projectName,
          int? companyId,
          string companyName,
          string workshopTitle,
          string location,
          string timeZoneIdIanaOrNullForDefault,
          DateTime? whenStartLocal,
          DateTime? whenEndLocal,
          int? keyFacilitatorUserId,
          string keyFacilitatorFirstName,
          string keyFacilitatorLastName,
          string keyFacilitatorEmail,
          string keyFacilitatorCalendlyUrlName,
          int? coFacilitatorUserId,
          string coFacilitatorFirstName,
          string coFacilitatorLastName,
          string coFacilitatorEmail,
          int? workshopStatusId,
          string workshopStatusName,
          decimal? workshopRevenue,
          bool isVirtual,
          bool isRTO,
          bool addParticipantsToInvite,
          bool disableEvals,
          bool notBillable,
          string friendlyWorkshopId,
          string participantAdditionalInfo,
          string workshopNotes,
          int attendeeCount,
          string calendarId,
          DateTime? cancelled,
          string cancelReason,
          DateTime? lastEvalSentUtc,
          decimal? programDeliveryPercentage,
          int? programSalesUserId,
          decimal? programSalesPercentage,
          int? programPLCUserId,
          decimal? programPLCPercentage,
          int? sessionTypeId,
          int? calendlyPayloadId,
          int? evalFirstOrLast,
          int? evalScoreSum,
          int? evalScoreCount,
          int? evalIntakeCodeId,
          bool hideFromProgramContent,
          int? evalTemplateSurveyId,
          DbHelper.ProgramComponents.ComponentQuoteInfo componentQuoteInfo
        ) {

          SetTime(whenStartLocal, whenEndLocal, timeZoneIdIanaOrNullForDefault.ValueIfNullOrEmpty(ConfigHelper.DefaultTimeZoneIdIana));

          this.WorkshopEventId = workshopEventId;
          this.WorkshopEventUID = workshopEventUID;
          this.ProgramJobId = programJobId;
          this.ProgramJobNumber = programJobNumber;
          this.ProgramJobName = programJobName;
          this.ProjectName = projectName;
          this.CompanyId = companyId;
          this.CompanyName = companyName;
          this.WorkshopTitle = workshopTitle;
          this.Location = location;
          this.KeyFacilitatorUserId = keyFacilitatorUserId;
          this.KeyFacilitatorFirstName = keyFacilitatorFirstName;
          this.KeyFacilitatorLastName = keyFacilitatorLastName;
          this.KeyFacilitatorEmail = keyFacilitatorEmail;
          this.KeyFacilitatorCalendlyUrlName = keyFacilitatorCalendlyUrlName;
          this.CoFacilitatorUserId = coFacilitatorUserId;
          this.CoFacilitatorFirstName = coFacilitatorFirstName;
          this.CoFacilitatorLastName = coFacilitatorLastName;
          this.CoFacilitatorEmail = coFacilitatorEmail;
          this.WorkshopStatusId = workshopStatusId;
          this.WorkshopStatusName = workshopStatusName;
          this.WorkshopRevenue = workshopRevenue;
          this.IsVirtual = isVirtual;
          this.IsRTO = isRTO;
          this.AddParticipantsToInvite = addParticipantsToInvite;
          this.DisableEvals = disableEvals;
          this.NotBillable = notBillable;
          this.FriendlyWorkshopId = friendlyWorkshopId;
          this.ParticipantAdditionalInfo = participantAdditionalInfo;
          this.WorkshopNotes = workshopNotes;
          this.CalendarId = calendarId;
          this.Cancelled = cancelled;
          this.CancelReason = cancelReason;
          this.AttendeeCount = attendeeCount;
          this.LastEvalSentUtc = lastEvalSentUtc;
          this.ProgramDeliveryPercentage = programDeliveryPercentage;
          this.ProgramSalesUserId = programSalesUserId;
          this.ProgramSalesPercentage = programSalesPercentage;
          this.ProgramPLCUserId = programPLCUserId;
          this.ProgramPLCPercentage = programPLCPercentage;
          this.SessionTypeId = sessionTypeId;
          this.CalendlyPayloadId = calendlyPayloadId;
          this.EvalFirstOrLast = evalFirstOrLast;
          this.EvalScoreSum = evalScoreSum;
          this.EvalScoreCount = evalScoreCount;
          this.EvalIntakeCodeId = evalIntakeCodeId;
          this.HideFromProgramContent = hideFromProgramContent;
          this.EvalTemplateSurveyId = evalTemplateSurveyId;
          this.ComponentQuoteInfo = componentQuoteInfo != null ? componentQuoteInfo : new DbHelper.ProgramComponents.ComponentQuoteInfo();
        }

        public void SetTime(DateTime? whenStartLocal, DateTime? whenEndLocal, string timeZoneIdIana) {
          // Caller should handle errors if timeZoneIdIana is invalid.
          this.TimeZoneIdIana = timeZoneIdIana;
          this.TimeZoneIdWindows = TimeHelper.IANAToWindowsTimeZoneId(timeZoneIdIana);
          this.TimeZoneInfo = TimeHelper.GetTimeZoneInfo(timeZoneIdIana);
          this.WhenStartLocal = whenStartLocal;
          this.WhenEndLocal = whenEndLocal;
          this.WhenStartUtc = TimeHelper.TimeZoneIdToUtc(whenStartLocal, timeZoneIdIana);
          this.WhenEndUtc = TimeHelper.TimeZoneIdToUtc(whenEndLocal, timeZoneIdIana);
          this.WhenStartDefaultTZ = this.WhenStartUtc?.AddHours(ConfigHelper.DefaultTimeZoneUTCOffsetHours);
          this.WhenEndDefaultTZ = this.WhenEndUtc?.AddHours(ConfigHelper.DefaultTimeZoneUTCOffsetHours);
        }

        public void SetLastEvalSentUtc(DateTime? lastEvalSentUtc) {
          this.LastEvalSentUtc = lastEvalSentUtc;
        }

        public DateTime? GetLastEvalSentLocal() {
          if (LastEvalSentUtc == null || TimeZoneIdIana.IsNullOrEmpty()) return null;
          return LastEvalSentUtc.Value.UtcToTZId(TimeZoneIdIana);
        }

        public bool HasEvalScore => this.EvalScoreCount > 0;
      }

      public class WorkshopListInfo {

        public int WorkshopEventId { get; private set; }
        public string WorkshopTitle { get; private set; }
        public DateTime WhenStartLocal { get; private set; }
        public DateTime WhenEndLocal { get; private set; }
        public string TimeZoneIdIana { get; private set; }
        public DateTime WhenStartUtc { get; private set; }
        public DateTime WhenEndUtc { get; private set; }
        public string Location { get; private set; }
        public int? KeyFacilitatorUserId { get; private set; }
        public string KeyFacilitatorFirstName { get; private set; }
        public string KeyFacilitatorLastName { get; private set; }
        public int? CoacheeId { get; private set; }
        public int CoacheeProgramStatusId { get; private set; }
        public bool IsVirtual { get; private set; }
        public int ProgramJobId { get; private set; }
        public string ProgramJobNumber { get; private set; }

        public WorkshopListInfo(int workshopEventId, string workshopTitle, string location,
          DateTime whenStartLocal, DateTime whenEndLocal, string timeZoneIdIanaOrNullForDefault,
          int? keyFacilitatorUserId, string keyFacilitatorFirstName, string keyFacilitatorLastName,
          int? coacheeId, int coacheeProgramStatusId, bool isVirtual, int programJobId, string programJobNumber) {

          WorkshopEventId = workshopEventId;
          WorkshopTitle = workshopTitle;
          Location = location;

          WhenStartLocal = whenStartLocal;
          WhenEndLocal = whenEndLocal;
          TimeZoneIdIana = timeZoneIdIanaOrNullForDefault.ValueIfNullOrEmpty(ConfigHelper.DefaultTimeZoneIdIana);
          WhenStartUtc = TimeHelper.TimeZoneIdToUtc(whenStartLocal, TimeZoneIdIana).Value;
          WhenEndUtc = TimeHelper.TimeZoneIdToUtc(whenEndLocal, TimeZoneIdIana).Value;

          KeyFacilitatorUserId = keyFacilitatorUserId;
          KeyFacilitatorFirstName = keyFacilitatorFirstName;
          KeyFacilitatorLastName = keyFacilitatorLastName;

          CoacheeId = coacheeId;
          CoacheeProgramStatusId = coacheeProgramStatusId;

          IsVirtual = isVirtual;
          ProgramJobId = programJobId;
          ProgramJobNumber = programJobNumber;
        }
      }

      // Used to maintain a cache of WorkshopEventInfo to save db calls in loops.
      public class WorkshopInfoCache {

        List<WorkshopEventInfo> _workshopInfoCache;

        public WorkshopInfoCache() {

          _workshopInfoCache = new List<WorkshopEventInfo>();
        }

        // Return workshop in cache if found, or from db then add to cache, or null if not found.
        public WorkshopEventInfo GetWorkshopInfoOrNull(int workshopEventId) {

          var workshopInfo = _workshopInfoCache.Find(w => w.WorkshopEventId == workshopEventId);

          if (workshopInfo == null) {
            workshopInfo = WorkshopEvents.GetWorkshopInfo(workshopEventId); // Get from db if not in cache.
            if (workshopInfo != null) _workshopInfoCache.Add(workshopInfo); // If found, add to cache.
          }

          return workshopInfo;
        }
      }

      public class AttendanceInfo {

        public int CoacheeId { get; set; }
        public string CoacheeFirstName { get; set; }
        public string CoacheeSurname { get; set; }
        public string CoacheeFullname => $"{CoacheeFirstName} {CoacheeSurname}";

        public DateTime? ConfirmedDateTimeUtc { get; set; }
        public string ConfirmedByUserFirstName { get; set; }
        public string ConfirmedByUserLastName { get; set; }
        public bool IsConfirmed => ConfirmedDateTimeUtc.HasValue;
        public string ConfirmedByUser => IsConfirmed ? $"{ConfirmedByUserFirstName} {ConfirmedByUserLastName}" : string.Empty;
      }
    }
  }
}

