using System;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Globalization;

namespace Integral.Database.CoachingSessions {

  // TODO: Change this to a "custom enum".
  public enum SessionStatusEnum {
    Normal = 0,
    Cancelled = 1,
    Cancelled_Late = 2,
    No_Show = 3
  }

}

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class CoachingSessions {

      private const string TblPfx = "cs";

      private static AbleSessionList GetSessionInfoList(
        int? topOrNullForAll, string extraJoins, string sqlWhereConditions, string sqlOrderBy,
        int? offsetRows, int? fetchRows,  // Need both or none.
        params SqlParameter[] sqlWhereParams) {

        var sessionList = new AbleSessionList(offsetRows, fetchRows);

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        string sql = $@"
          SELECT {sqlTop}
            COUNT(*) OVER() AS TotalRows,

            {TblPfx}.CoachingSessionId, {TblPfx}.AbleCoacheeId, ac.UserId as CoacheeUserId, {TblPfx}.CoachingSessionTypeId,
            {TblPfx}.ApptDateUtc, {TblPfx}.CoacheeTimeZoneIANA, {TblPfx}.DurationMins, {TblPfx}.ApptRecheduleReason,
            {TblPfx}.ApptCancelledUtc, {TblPfx}.ApptCancelledLate, {TblPfx}.ApptNoShow, {TblPfx}.CoachNotes,
            {TblPfx}.QuoteItemId, {TblPfx}.SessionPrice,
            {TblPfx}.CalendlyEventUuid, {TblPfx}.ApptNotes, {TblPfx}.ApptVenue, {TblPfx}.ApptVenueAddr,

            cu.TimeZoneIdIANA AS CoachTimeZoneIdIANA, cu.FirstName as CoachFirstName, cu.LastName as CoachLastName,
            cst.SessionTypeDisplayName, cst.InPerson, cst.EventSessionTypeDisplayName,
            ac.FirstName AS CoacheeFirstName, ac.LastName AS CoacheeLastName, ac.EmailAddress AS CoacheeEmail, ac.SessionsAllocated,
            j.JobId, j.JobNumber, j.JobName, j.CompanyId, sc.CompanyName, prj.ProjectName,

            ac.CoachUserId,                   j.Partner_DeliveryPercentage,
            j.Partner_UserId AS SalesUserId,  j.Partner_SalesDeliveryPercentage,
            j.LeadConsultantUserId,           j.Partner_PLCPercentage,

            apc.ComponentId, apc.QuoteItemId AS ComponentQuoteItemId, apc.ComponentPrice, apc.LockedDateUtc AS ComponentLockedDateUtc,
            cp.CoachingRevenue,
            IIF(ISNULL(pli.PLICount, 0) > 0, 1, 0) AS HasPLI,
            {Subscriptions.User.GetSubscriptionOuterApplySelectionSQL},
            {AbleUser.GetUserActivityLeftJoinSelectionSQL}

          FROM id_CoachingSession {TblPfx}
          INNER JOIN al_Coachees ac ON ac.CoacheeId = {TblPfx}.AbleCoacheeId
          LEFT OUTER JOIN sv_User cu ON cu.UserId = ac.CoachUserId
          OUTER APPLY (SELECT SUM(cp.ComponentPrice) AS CoachingRevenue FROM al_Component cp WHERE cp.CoacheeId = ac.CoacheeId) AS cp
          LEFT OUTER JOIN id_Job j ON j.JobId = ac.ProgramJobId
          LEFT OUTER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          LEFT OUTER JOIN sv_SurveyCompany sc ON sc.SvCompanyId = j.CompanyId
          LEFT OUTER JOIN al_Component apc ON apc.CoachingSessionId = {TblPfx}.CoachingSessionId
          {AbleUser.GetUserActivityInfoLeftJoinSQL("ac", "ac")}
          OUTER APPLY (SELECT COUNT(*) AS PLICount FROM al_PLPeriodItem pli WHERE pli.ComponentId = apc.ComponentId) AS pli

          OUTER APPLY (
            SELECT ct.CoachingTypeName AS SessionTypeDisplayName, CASE WHEN cst.InPerson IS NULL THEN 0 ELSE cst.InPerson END AS InPerson,
              cst.SessionTypeDisplayName as EventSessionTypeDisplayName
            FROM id_CoachingSession csi
            LEFT OUTER JOIN al_CoachingTypes ct ON ct.CoachingTypeId = ac.CoachingTypeId
            LEFT JOIN al_CoachingTypeSessions cts on ct.CoachingTypeId = cts.CoachingTypeId and cts.SessionNumber = 1
            LEFT JOIN  al_CoachingSessionTypes cst on cst.CoachingSessionTypeId = cts.CoachingSessionTypeId
            WHERE csi.CoachingSessionId = {TblPfx}.CoachingSessionId
          ) AS cst
          {Subscriptions.User.GetUserSubscriptionOuterApplySQL("ac")}
          {extraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (topOrNullForAll == null && !sqlOrderBy.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          sessionList.OffsetRows = offsetRows;
          sessionList.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        var paramsSql = new List<SqlParameter>();
        paramsSql = DbHelper.AbleUser.GetUserActivityInfoParamsSQL();
        paramsSql.AddRange(sqlWhereParams);

        Common.Query(sql,
          dr => {
            if (sessionList.TotalRows == 0) sessionList.TotalRows = dr.GetInt("TotalRows");
            var sessionInfo = new AbleSessionInfo(
              dr.GetInt("CoachingSessionId"),
              dr.GetIntOrNull("CoachUserId"),
              dr.GetString("CoachFirstName"),
              dr.GetString("CoachLastName"),
              dr.GetString("CoachTimeZoneIdIANA"),
              dr.GetInt("AbleCoacheeId"),
              dr.GetInt("CoacheeUserId"),
              dr.GetString("CoacheeFirstName"),
              dr.GetString("CoacheeLastName"),
              dr.GetString("CoacheeEmail"),
              dr.GetIntOrNull("JobId"),
              dr.GetString("JobNumber"),
              dr.GetString("JobName"),
              dr.GetString("ProjectName"),
              dr.GetIntOrNull("CompanyId"),
              dr.GetString("CompanyName"),
              dr.GetIntOrNull("CoachingSessionTypeId"),
              dr.GetString("SessionTypeDisplayName"),
              dr.GetBoolFromInt("InPerson"),
              dr.GetString("EventSessionTypeDisplayName"),
              dr.GetString("CoachNotes"),
              dr.GetDateTime("ApptDateUtc"),
              dr.GetString("CoacheeTimeZoneIANA"),
              dr.GetInt("DurationMins"),
              dr.GetString("ApptRecheduleReason"),
              dr.GetDateTimeOrNull("ApptCancelledUtc"),
              dr.GetBoolFromInt("ApptCancelledLate"),  // Cancelled late.
              dr.GetBoolFromInt("ApptNoShow"), // Coachee did not show up or missed online appt.
              dr.GetString("CalendlyEventUuid"),
              dr.GetBoolFromInt("HasPLI"),
              dr.GetDecimalOrNull("CoachingRevenue"),
              dr.GetInt("SessionsAllocated"),
              dr.GetDecimalOrNull("Partner_DeliveryPercentage"),
              dr.GetIntOrNull("SalesUserId"),
              dr.GetDecimalOrNull("Partner_SalesDeliveryPercentage"),
              dr.GetIntOrNull("LeadConsultantUserId"),
              dr.GetDecimalOrNull("Partner_PLCPercentage"),
              dr.GetIntOrNull("QuoteItemId"),
              dr.GetDecimalOrNull("SessionPrice"),
              dr.GetIntOrNull("ComponentId"),
              dr.GetDecimalOrNull("ComponentPrice"),
              dr.GetIntOrNull("ComponentQuoteItemId"),
              dr.GetDateTimeOrNull("ComponentLockedDateUtc") != null ? true : false,
              dr.GetString("ApptNotes"),
              dr.GetString("ApptVenue"),
              dr.GetString("ApptVenueAddr"),
              Subscriptions.User.GetUserSubscriptionInfo(dr),
              AbleUser.GetUserActivityInfo(dr)
            );
            sessionList.SessionInfoList.Add(sessionInfo);
          },
          paramsSql.ToArray()
        );
        return sessionList;
      }

      private static AbleSessionInfo GetSingleSessionInfo(string extraJoins, string sqlWhereConditions, params SqlParameter[] sqlWhereParams) {
        var result = GetSessionInfoList(1, extraJoins, sqlWhereConditions, "", null, null, sqlWhereParams);
        if (result == null || result.SessionInfoList == null || result.SessionInfoList.Count == 0) return null;
        return result.SessionInfoList[0];
      }

      public static AbleSessionInfo GetSessionInfoOrNull(int coacheeId, int sessionId) {
        return GetSingleSessionInfo("",
          $"{TblPfx}.CoachingSessionId = @CoachingSessionId AND {TblPfx}.AbleCoacheeId = @CoacheeId",
          Common.NewSqlParameter("@CoacheeId", coacheeId),
          Common.NewSqlParameter("@CoachingSessionId", sessionId));
      }

      public static AbleSessionInfo GetSessionInfoOrNull(int coachingSessionId) {
        return GetSingleSessionInfo("", $"{TblPfx}.CoachingSessionId = @CoachingSessionId", Common.NewSqlParameter("@CoachingSessionId", coachingSessionId));
      }

      public static AbleSessionInfo GetSessionInfoOrNull(int coacheeId, DateTime apptDateUtc) {
        return GetSingleSessionInfo("",
          $"{TblPfx}.AbleCoacheeId = @CoacheeId AND {TblPfx}.ApptDateUtc = @ApptDateUtc",
          Common.NewSqlParameter("@CoacheeId", coacheeId),
          Common.NewSqlParameter("@ApptDateUtc", apptDateUtc));
      }

      public static AbleSessionInfo GetSessionInfoOrNull(string coacheeEmail, DateTime apptDateUtc) {
        return GetSingleSessionInfo("",
          $"ac.EmailAddress = @CoacheeEmail AND {TblPfx}.ApptDateUtc = @ApptDateUtc",
          Common.NewSqlParameter("@CoacheeEmail", coacheeEmail),
          Common.NewSqlParameter("@ApptDateUtc", apptDateUtc));
      }

      public enum SessionSort {
        DateDescending = 1,
        DateAscending = 2
      }
      public static List<AbleSessionInfo> GetSessionsForCoacheeId(int coacheeId, SessionSort sort = SessionSort.DateDescending) {
        return GetSessionInfoList(null, "",
          $"{TblPfx}.AbleCoacheeId = @CoacheeId",
          $"{TblPfx}.ApptDateUTC {(sort == SessionSort.DateDescending ? "DESC" : "")}",
          null, null,
          Common.NewSqlParameter("@CoacheeId", coacheeId)).SessionInfoList;
      }

      public static AbleSessionInfo GetLatestSessionOrNull(int? forCoacheeIdOrNullForAll) {
        var result = GetSessionInfoList(1, "",
          $"(@CoacheeId IS NULL OR (@CoacheeId IS NOT NULL AND {TblPfx}.AbleCoacheeId = @CoacheeId))",
          $"{TblPfx}.ApptDateUTC DESC",
          null, null,
          Common.NewSqlParameter("@CoacheeId", forCoacheeIdOrNullForAll));
        if (result == null || result.SessionInfoList == null || result.SessionInfoList.Count == 0) return null;
        return result.SessionInfoList[0];
      }

      public static List<AbleSessionInfo> GetSessionsByDate(int? forProgramJobIdOrNullForAll, int? forCoacheeIdOrNullForAll, bool onlyFutureSessions) {
        var result = GetSessionInfoList(null,
          "",
          $"(@ProgramJobId IS NULL OR (@ProgramJobId IS NOT NULL AND ac.ProgramJobId = @ProgramJobId))"
          + $" AND (@CoacheeId IS NULL OR (@CoacheeId IS NOT NULL AND {TblPfx}.AbleCoacheeId = @CoacheeId))"
          + (onlyFutureSessions ? $" AND ({TblPfx}.ApptDateUTC >= GETUTCDATE())" : ""),
          $"{TblPfx}.ApptDateUTC {(onlyFutureSessions ? "" : "DESC")}",
          null, null,
          Common.NewSqlParameter("@ProgramJobId", forProgramJobIdOrNullForAll),
          Common.NewSqlParameter("@CoacheeId", forCoacheeIdOrNullForAll));
        if (result == null || result.SessionInfoList == null || result.SessionInfoList.Count == 0) return null;
        return result.SessionInfoList;
      }

      public static List<AbleSessionInfo> GetUpcomingSessionsForCoach(int coachUserId, DateTime earliestItemDateUtc) {
        var result = GetSessionInfoList(null, "",
          $@"WHERE ac.CoachUserId = @CoachUserId
               AND {TblPfx}.ApptDateUtc >= @EarliestItemDateUtc
               AND NOT EXISTS (SELECT NULL FROM id_PayRunItems pri WHERE pri.CoachingSessionId = {TblPfx}.CoachingSessionId)",
          $"{TblPfx}.ApptDateUTC",
          null, null,
          Common.NewSqlParameter("CoachUserId", coachUserId),
          Common.NewSqlParameter("EarliestItemDateUtc", earliestItemDateUtc));
        if (result == null || result.SessionInfoList == null || result.SessionInfoList.Count == 0) return null;
        return result.SessionInfoList;
      }

      public static int? CreateSessionInFreeComponent(SqlTransaction trans,
        AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
        DateTime apptDateUtc, int durationMins,
        string apptVenue, string calendlyEventUuid) {

        return CreateSessionInFreeComponent(trans, coacheeInfo, apptDateUtc, durationMins, apptVenue, null, false, false, "", calendlyEventUuid, "");
      }

      // Create session if there is a free component for this coachee.
      // Note that component price and quoteitem is not be updated here, only info related to the session itself.
      // Returns new CoachingSessionId or null if a free component was not found.
      public static int? CreateSessionInFreeComponent(SqlTransaction trans,
        AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
        DateTime apptDateUtc, int durationMins, string apptVenue,
        DateTime? cancelledUtc, bool cancelledLate, bool noShow, string coachNotes,
        string calendlyEventUuid, string sessionNotes) {

        return Common.GetScalarQueryIntOrNull(trans, $@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          -- Find first free component for this coachee in order of SessionNumber.
          DECLARE @ComponentId INT = (
            SELECT TOP 1 ComponentId
            FROM al_Component WITH (UPDLOCK, ROWLOCK, SERIALIZABLE)
            WHERE CoacheeId = @CoacheeId
              AND SessionNumber > 0
              AND CoachingSessionId IS NULL
              AND LockedDateUtc IS NULL
            ORDER BY SessionNumber
          );

          IF @ComponentId IS NOT NULL BEGIN

            INSERT INTO id_CoachingSession
                   (AbleCoacheeId, ApptDateUtc,  DurationMins,  ApptVenue,  CoachUserId,  CoachNotes,  ApptCancelledUtc,  ApptCancelledLate,  ApptNoShow,  CalendlyEventUuid, ApptNotes)
            VALUES (@CoacheeId,    @ApptDateUtc, @DurationMins, @ApptVenue, @CoachUserId, @CoachNotes, @ApptCancelledUtc, @ApptCancelledLate, @ApptNoShow, @CalendlyEventUuid, @ApptNotes);

            DECLARE @NewCoachingSessionId INT = SCOPE_IDENTITY();

            UPDATE al_Component SET
              RowUpdatedUtc = GETUTCDATE(),
              CoachingSessionId = @NewCoachingSessionId,
              CompletedDateUtc = @ApptDateUtc,
              PartnerUserId = @CoachUserId
            WHERE ComponentId = @ComponentId;

            SELECT @NewCoachingSessionId;

          END
          ELSE SELECT NULL; -- Free component not found for this coachee.

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("CoacheeId", coacheeInfo.CoacheeId),
          Common.NewSqlParameter("ApptDateUtc", apptDateUtc),
          Common.NewSqlParameter("DurationMins", durationMins),
          Common.NewSqlParameter("ApptVenue", apptVenue.LimitLengthTo(200)),
          Common.NewSqlParameter("CoachUserId", coacheeInfo.CoachUserId),
          Common.NewSqlParameter("CoachNotes", coachNotes.LimitLengthTo(8000)),
          Common.NewSqlParameter("ApptCancelledUtc", cancelledUtc),
          Common.NewSqlParameter("ApptCancelledLate", cancelledLate),
          Common.NewSqlParameter("ApptNoShow", noShow),
          Common.NewSqlParameter("CalendlyEventUuid", calendlyEventUuid),
          Common.NewSqlParameter("ApptNotes", sessionNotes.LimitLengthTo(8000))
        );
      }

      // Note only updates if component is not locked.
      public static void UpdateSessionUnlocked(SqlTransaction trans,
        AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
        int sessionId, DateTime apptDateUTC, int durationMins, string coachNotes,
        DateTime? apptCancelledUtc, bool apptCancelledLate, bool apptNoShow, string sessionNotes) {

        Common.GetScalarQueryInt(trans, $@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          UPDATE al_Component WITH (ROWLOCK, UPDLOCK, SERIALIZABLE)
          SET CompletedDateUtc = @ApptDateUTC,
              PartnerUserId = @CoachUserId,
              RowUpdatedUtc = GETUTCDATE()
          WHERE CoachingSessionId = @CoachingSessionId
          AND LockedDateUtc IS NULL;

          IF @@ROWCOUNT = 1
          BEGIN
            UPDATE id_CoachingSession SET
              ApptDateUTC = @ApptDateUTC,
              DurationMins = @DurationMins,
              CoachUserId = @CoachUserId,
              CoachNotes = @CoachNotes,
              ApptCancelledUTC = @ApptCancelledUTC,
              ApptCancelledLate = @ApptCancelledLate,
              ApptNoShow = @ApptNoShow,
              ApptNotes = @ApptNotes
            WHERE CoachingSessionId = @CoachingSessionId
          END;

          SELECT @@ROWCOUNT;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("CoachingSessionId", sessionId),
          Common.NewSqlParameter("ApptDateUTC", apptDateUTC),
          Common.NewSqlParameter("DurationMins", durationMins),
          Common.NewSqlParameter("CoachUserId", coacheeInfo.CoachUserId),
          Common.NewSqlParameter("CoachNotes", coachNotes),
          Common.NewSqlParameter("ApptCancelledUtc", apptCancelledUtc),
          Common.NewSqlParameter("ApptCancelledLate", apptCancelledLate),
          Common.NewSqlParameter("ApptNoShow", apptNoShow),
          Common.NewSqlParameter("ApptNotes", sessionNotes)
        );
      }

      // Limited update of session info if component is locked.
      public static void UpdateSessionLocked(SqlTransaction trans, int coachingSessionId, string coachNotes, string sessionNotes) {

        Common.GetNonQueryInt(trans, $@"
          UPDATE id_CoachingSession
          SET CoachNotes = @CoachNotes,
          ApptNotes = @ApptNotes
          WHERE CoachingSessionId = @CoachingSessionId",
          Common.NewSqlParameter("CoachingSessionId", coachingSessionId),
          Common.NewSqlParameter("CoachNotes", coachNotes),
          Common.NewSqlParameter("ApptNotes", sessionNotes)
        );
      }

      // Only deletes if component is not locked.
      public static bool DeleteSessionUnlocked(SqlTransaction trans, int coachingSessionId, int coacheeId) {

        return Common.GetScalarQueryInt(trans, $@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          UPDATE al_Component SET
            CoachingSessionId = NULL,
            CompletedDateUtc = NULL,
            RowUpdatedUtc = GETUTCDATE()
          WHERE CoachingSessionId = @CoachingSessionId
            AND CoacheeId = @CoacheeId
            AND LockedDateUtc IS NULL;

          IF @@ROWCOUNT = 1
          BEGIN
            DELETE FROM id_CoachingSession
            WHERE CoachingSessionId = @CoachingSessionId
          END;

          SELECT @@ROWCOUNT;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("CoachingSessionId", coachingSessionId),
          Common.NewSqlParameter("CoacheeId", coacheeId)

        ) == 1;
      }

      public static SessionStats GetSessionStats(SqlTransaction trans, int coacheeId, int? coachUserId = null) {
        var statsList = GetSessionStats(trans, coacheeId, coachUserId, null);
        if (statsList == null || statsList.Count == 0) return null;
        return statsList[0]; // There will be only 1 item.
      }

      public static List<SessionStats> GetSessionStats(SqlTransaction trans, DateTime lastUpdateFromUtc, bool includeNonActive = false) {
        return GetSessionStats(trans, null, null, lastUpdateFromUtc, includeNonActive);
      }

      private static List<SessionStats> GetSessionStats(SqlTransaction trans, int? coacheeId, int? coachUserId, DateTime? lastUpdateFromUtc) {
        return GetSessionStats(trans, coacheeId, coachUserId, lastUpdateFromUtc, false);
      }

      private static List<SessionStats> GetSessionStats(SqlTransaction trans, int? coacheeId, int? coachUserId, DateTime? lastUpdateFromUtc, bool includeNonActive) {

        // Get a set of stats from any coachee meeting the criteria:
        // 1. matches coacheeId, or all CoacheeIds if coacheeId is null,
        // 2. matches coachUserId, or all CoachUserIds if coachUserId is null,
        // 3. coachees whose latest session is on or after lastUpdateFromUtc, or any date if lastUpdateFromUtc is null.
        // TODO: 4. coachees whose lastUpdatedUtc date is > lastUpdateFromUtc or all if lastUpdateFromUtc is null.
        //
        // Sessions are counted as follows:
        // 1. No-shows (ApptNoShow) and late cancellations (ApptCancelledLate) are still counted as used sessions.
        // 2. Valid cancellations (ApptCancelledUtc and ApptCancelledLate = 0) are NOT counted as used sessions.
        // 3. Note that, as of Apr 2020, it's assumed that most cancelled sessions will be deleted in Able,
        //    but "late -cancelled" are NOT deleted, as they count toward used sessions.
        //    Late-cancelled sessions are flagged as such, and ApptCancelledUTC is set.
        //    Thought - ApptCancelledUTC is not useful in this context, as late-cancel means
        //    it was cancelled very close to the appt date anyway.

        string sql = $@"
          SELECT
            ac.CoacheeId, ac.CoachUserId, ac.EmailAddress,
            ac.SessionsAllocated, ac.WelcomeEmailUtc, ac.MeetCoachEmailUtc,
            COUNT({TblPfx}.ApptDateUtc) AS CountBooked,
            COUNT(IIF({TblPfx}.ApptCancelledUTC IS NULL AND {TblPfx}.ApptDateUtc > GETUTCDATE(), 1, NULL)) AS CountUpcoming,
            MIN(IIF({TblPfx}.ApptCancelledUTC IS NULL AND {TblPfx}.ApptDateUtc > GETUTCDATE(), {TblPfx}.ApptDateUtc, NULL)) AS NextDateUtc,
            MIN({TblPfx}.ApptDateUtc) AS EarliestDateUtc,
            MAX({TblPfx}.ApptDateUtc) AS LatestDateUtc,
            css.ApptDateList
          FROM al_Coachees ac
          LEFT OUTER JOIN id_CoachingSession {TblPfx}
              ON {TblPfx}.AbleCoacheeId = ac.CoacheeId
            AND ({TblPfx}.ApptCancelledUTC IS NULL OR {TblPfx}.ApptCancelledLate = 1) /* exclude 'properly' cancelled sessions (i.e. not late) */
          CROSS APPLY (
            SELECT (
              STUFF((
                SELECT ',' + CONVERT(VARCHAR, css.ApptDateUTC, 20)
                FROM id_CoachingSession css
                WHERE css.AbleCoacheeId = {TblPfx}.AbleCoacheeId
                  AND ({TblPfx}.ApptCancelledUTC IS NULL OR {TblPfx}.ApptCancelledLate = 1) /* exclude 'properly' cancelled sessions (i.e. not late) */
                ORDER BY css.ApptDateUTC
                FOR XML PATH (''), TYPE, ROOT
              ).value('root[1]', 'nvarchar(max)'), 1, 1, '')) AS ApptDateList
          ) AS css
          WHERE ac.DeletedUtc IS NULL
          GROUP BY
            ac.CoacheeId, ac.CoachUserId, ac.EmailAddress, ac.NextBookingTargetDateUtc, ac.SessionsBooked, ac.ProgramStatusId,
            ac.SessionsAllocated, ac.WelcomeEmailUtc, ac.WelcomeEmailSentUtc, ac.MeetCoachEmailUtc,
            css.ApptDateList
          HAVING
            (@AbleCoacheeId IS NOT NULL AND ac.CoacheeId = @AbleCoacheeId)
            OR
            (@CoachUserId IS NOT NULL AND ac.CoachUserId = @CoachUserId)
            OR
            (
              @AbleCoacheeId IS NULL AND @CoachUserId IS NULL

              {(includeNonActive ? "" : "AND ac.ProgramStatusId < " + CoacheeProgramStatus.GetStatus_Paused().ProgramStatusId)}

              AND
              (
                (@LatestSessionFromUtc IS NULL OR (@LatestSessionFromUtc IS NOT NULL AND (MAX({TblPfx}.ApptDateUTC) >= @LatestSessionFromUtc OR ISNULL(ac.WelcomeEmailUtc, ac.WelcomeEmailSentUtc) >= @LatestSessionFromUtc)))
                OR
                (ac.SessionsBooked < ac.SessionsAllocated AND (ac.NextBookingTargetDateUtc IS NULL OR ac.NextBookingTargetDateUtc < GETUTCDATE()))
              )
            )";

        var statsList = new List<SessionStats>();

        Common.Query(trans, sql,
          dr => {

            var stats = new SessionStats() {
              CoacheeId = dr.GetInt("CoacheeId"),
              CoachUserId = dr.GetIntOrNull("CoachUserId"),
              CoacheeEmail = dr.GetString("EmailAddress"),
              MeetCoachEmailUTC = dr.GetDateTimeOrNull("MeetCoachEmailUtc"),
              SessionsAllocated = dr.GetIntOrDefault("SessionsAllocated", 0),
              SessionsBooked = dr.GetInt("CountBooked"),
              SessionsUpcoming = dr.GetInt("CountUpcoming"),
              NextApptDateUTC = dr.GetDateTimeOrNull("NextDateUTC"),
              EarliestApptDateUTC = dr.GetDateTimeOrNull("EarliestDateUTC"),
              LatestApptDateUTC = dr.GetDateTimeOrNull("LatestDateUTC")
            };

            // Add dates from string list of session appt dates.
            string apptDateList = dr.GetString("ApptDateList");
            if (!apptDateList.IsNullOrEmpty()) {
              stats.ListOfApptDateUtc = new List<DateTime>();
              foreach (string dateStr in apptDateList.Split(',')) {
                DateTime dt;
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) {
                  throw new ApplicationException("Invalid ApptListDate: " + dateStr);
                }
                stats.ListOfApptDateUtc.Add(dt);
              }
            }

            statsList.Add(stats);

          },
          Common.NewSqlParameter("AbleCoacheeId", coacheeId),
          Common.NewSqlParameter("CoachUserId", coachUserId),
          Common.NewSqlParameter("LatestSessionFromUtc", lastUpdateFromUtc)
        );

        return statsList.Count == 0 ? null : statsList;
      }

      public static List<SessionBookingReminderInfo> GetBookingRemindersOrderedByDaysPassed() {

        // Get all coachees that need a reminder to book their next coaching session.
        // "DaysPassed" is calculated as the number of days after either:
        // a) the last coaching session, or
        // b) MeetCoachEmail date, if there are no sessions yet.
        // Resultset must be ordered by DaysPassed.

        /*
        All conditions for sending booking reminders:
        ---------------------------------------------
        Coachee status is Active or Onboarding
        A Coach is assigned.
        CoachingType not "No"
        Sessions allocated > 0
        Not all sessions booked
        Meet Coach Email date is set (won't sent if blank)
        Meet Coach email not sent yet
        Next Booking Target Date is set (won't send if blank)
        Latest session booking (if there is one) is in the past
        Last booking reminder (if there is one) was sent over 2 days ago.
        CalendlyUrl must be set for the coach
        Reminders stop after 2 months (business rule)
        */

        var bookingReminders = new List<SessionBookingReminderInfo>();

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          string sql = $@"

            SELECT
              ac.CoacheeId, ac.CoacheeUId, ac.FirstName AS CoacheeFirstName, ac.LastName AS CoacheeLastName,
              ac.EmailAddress AS CoacheeEmailAddress,
              ac.ProgramJobId, ac.ProgramStatusId, ac.CoachingTypeId,
              ac.SessionsAllocated, ac.SessionsBooked,
              ac.SessionsAllocated - ac.SessionsBooked AS SessionsRemaining,
              ac.WelcomeEmailUtc, ac.MeetCoachEmailUtc, ac.MeetCoachEmailSentUtc, ac.BookingReminderLastSentUtc,
              {TblPfx}.LatestBookingUtc,
              dp.DaysPassed,
              ac.NextBookingTargetDateUtc, ac.BookingReminderCount,
              u.UserId AS CoachUserId,
              u.FirstName AS CoachFirstName, u.LastName AS CoachLastName,
              u.Email AS CoachEmailAddress, u.CalendlyUrlName,
              dbo.fnGetUserSenderEmailName(u.UserId) AS ComputedSenderEmailName,
              dbo.fnGetUserSenderEmailAddress(u.UserId) AS ComputedSenderEmailAddress,
              ap.JobNumber, ap.FriendlyProjectTitle, ap.ProjectName, ap.SvCompanyId,
              ap.BookSessionEmailCustomHTML, ap.BookingReminderCadenceDays,
              p_org.OrgId AS TenantOrgId, p_org.OrgGuid AS ParentOrgGuid, p_org.OrgOwnerUserId,
              b_org.OrgGuid AS BrandingOrgGuid
            FROM al_Coachees ac
            INNER JOIN id_Job j ON ac.ProgramJobId = j.JobId
            INNER JOIN al_Project ap ON ap.JobNumber = j.JobNumber
            INNER JOIN sv_User u ON ac.CoachUserId = u.UserId
            INNER JOIN sv_Organisation p_org ON p_org.OrgId = ap.ParentOrgId
            LEFT OUTER JOIN sv_Organisation b_org ON b_org.OrgId = ap.BrandingOrgId
            OUTER APPLY (
              SELECT MAX({TblPfx}.ApptDateUTC) AS LatestBookingUtc
              FROM id_CoachingSession {TblPfx}
              WHERE {TblPfx}.AbleCoacheeId = ac.CoacheeId
                AND {TblPfx}.ApptCancelledUTC IS NULL
            ) AS {TblPfx}
            CROSS APPLY (
              SELECT DATEDIFF(DAY, CAST(ISNULL({TblPfx}.LatestBookingUtc, ac.MeetCoachEmailUtc) AS DATE), CAST(GETUTCDATE() AS DATE)) AS DaysPassed
            ) AS dp
            WHERE (ac.ProgramStatusId = @ActiveProgramStatusId OR ac.ProgramStatusId = @OnboardingStatusId)
              AND ac.CoachingTypeId > 0
              AND ac.CoachingTypeId <> @CoachingTypeNoCoachingId
              AND ac.SessionsAllocated > 0
              AND ac.SessionsBooked < ac.SessionsAllocated
              AND ac.MeetCoachEmailUtc IS NOT NULL
              AND ac.MeetCoachEmailSentUtc IS NOT NULL
              AND ac.NextBookingTargetDateUtc IS NOT NULL
              AND ({TblPfx}.LatestBookingUtc IS NULL OR {TblPfx}.LatestBookingUtc < GETUTCDATE())
              AND (ac.BookingReminderLastSentUtc IS NULL OR DATEDIFF(DAY, ac.BookingReminderLastSentUtc, GETUTCDATE()) > 1)
              AND ac.DeletedUtc IS NULL
            ORDER BY dp.DaysPassed";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@ActiveProgramStatusId", DbHelper.CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId);
            cmd.Parameters.AddInt("@OnboardingStatusId", DbHelper.CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId);
            cmd.Parameters.AddInt("@CoachingTypeNoCoachingId", DbHelper.AlbertCoachingTypes.GetType_NoCoaching().CoachingTypeId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {

                var br = new SessionBookingReminderInfo(

                  projectJobNumber: dr.GetString("JobNumber"),
                  friendlyProjectTitle: dr.GetString("FriendlyProjectTitle").ValueIfNullOrEmpty(dr.GetString("ProjectName")),

                  tenantOrgId: dr.GetInt("TenantOrgId"),
                  tenantOrgOwnerUserId: dr.GetInt("OrgOwnerUserId"),

                  tenantOrgGuid: dr.GetGuid("ParentOrgGuid"),
                  brandingOrgGuid: dr.GetGuidOrNull("BrandingOrgGuid"),

                  coacheeId: dr.GetInt("CoacheeId"),
                  coacheeGuid: dr.GetGuid("CoacheeUid"),
                  coacheeFirstName: dr.GetString("CoacheeFirstName"),
                  coacheeLastName: dr.GetString("CoacheeLastName"),
                  coacheeEmailAddress: dr.GetString("CoacheeEmailAddress"),
                  programJobId: dr.GetInt("ProgramJobId"),
                  companyId: dr.GetInt("SvCompanyId"),
                  programStatusId: dr.GetInt("ProgramStatusId"),
                  coachingTypeId: dr.GetInt("CoachingTypeId"),
                  sessionsAllocated: dr.GetInt("SessionsAllocated"),
                  sessionsBooked: dr.GetInt("SessionsBooked"),
                  welcomeEmailUtc: dr.GetDateTimeOrNull("WelcomeEmailUtc"),
                  meetCoachEmailUtc: dr.GetDateTimeOrNull("MeetCoachEmailUtc"),
                  meetCoachEmailSentUtc: dr.GetDateTimeOrNull("MeetCoachEmailSentUtc"),
                  latestBookingUtc: dr.GetDateTimeOrNull("LatestBookingUtc"),
                  bookingReminderLastSentUtc: dr.GetDateTimeOrNull("BookingReminderLastSentUtc"),
                  daysPassed: dr.GetInt("DaysPassed"),
                  nextBookingTargetDateUtc: dr.GetDateTime("NextBookingTargetDateUtc"),
                  bookingReminderCount: dr.GetInt("BookingReminderCount"),
                  coachFirstName: dr.GetString("CoachFirstName"),
                  coachLastName: dr.GetString("CoachLastName"),
                  coachEmailAddress: dr.GetString("CoachEmailAddress"),
                  emailSenderName: dr.GetString("ComputedSenderEmailName"),
                  emailSenderAddress: dr.GetString("ComputedSenderEmailAddress"),
                  bookSessionEmailCustomHTML: dr.GetString("BookSessionEmailCustomHTML"),
                  bookingReminderCadenceDays: dr.GetString("BookingReminderCadenceDays")
                );
                bookingReminders.Add(br);
              }
            }
          }
        }
        return bookingReminders;
      }

      public static void UpdateBookingReminderSent(int coacheeId, int newBookingReminderCount) {

        Common.GetNonQueryInt(@"

          UPDATE al_Coachees
          SET LastSessionBookingReminderUtc = GETUTCDATE(),
              BookingReminderCount = @NewBookingReminderCount
          WHERE CoacheeId = @CoacheeId",

          Common.NewSqlParameter("CoacheeId", coacheeId),
          Common.NewSqlParameter("NewBookingReminderCount", newBookingReminderCount)
        );
      }

      public static void RescheduleUnlocked(SqlTransaction trans, int coachingSessionId, DateTime apptUtc, string recheduleReason, string calendlyEventUuid) {

        Common.GetScalarQueryInt($@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          UPDATE al_Component WITH (ROWLOCK, UPDLOCK, SERIALIZABLE)
          SET CompletedDateUtc = @ApptDateUTC,
              RowUpdatedUtc = GETUTCDATE()
          WHERE CoachingSessionId = @CoachingSessionId
          AND LockedDateUtc IS NULL;

          IF @@ROWCOUNT = 1
          BEGIN
            UPDATE id_CoachingSession SET
              ApptDateUTC = @ApptDateUTC,
              ApptRecheduleReason = @ApptRecheduleReason,
              CalendlyEventUuid = @CalendlyEventUuid
            WHERE CoachingSessionId = @CoachingSessionId
          END;

          SELECT @@ROWCOUNT;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("ApptDateUTC", apptUtc),
          Common.NewSqlParameter("CoachingSessionId", coachingSessionId),
          Common.NewSqlParameter("ApptRecheduleReason", recheduleReason),
          Common.NewSqlParameter("CalendlyEventUuid", calendlyEventUuid));
      }

      public static bool UpdatePostSessionEvalSent(int coachingSessionId, DateTime setEvalSentUtc) {
        int rowsAffected = Common.UpdateSingleColumn(
          "id_CoachingSession",
          new Common.SimpleWhereCondition("CoachingSessionId", coachingSessionId),
          new Common.ColumnSetting("EvalCoacheeFirstSentUTC", setEvalSentUtc));
        return rowsAffected > 0 ? true : false;
      }

      public enum CoachingSessionsForUserPeriod { All, Historical, Upcoming }

      public static List<AbleSessionInfo> GetCoachingSessionsForUser(int userId, int? maxRows, CoachingSessionsForUserPeriod period) {

        var result = new List<AbleSessionInfo>();

        string topClause = maxRows.HasValue ? $"TOP {maxRows.Value}" : "";
        string whereClause = "";

        if (period == CoachingSessionsForUserPeriod.Upcoming) {
          whereClause = " AND cs.ApptDateUtc >= @UtcNow";
        } else if (period == CoachingSessionsForUserPeriod.Historical) {
          whereClause = " AND cs.ApptDateUtc < @UtcNow";
        }

        Common.Query($@"
          SELECT {topClause}
            cs.CoachingSessionId, cs.AbleCoacheeId, ac.ProgramJobId,
            cst.EventSessionTypeDisplayName, cst.InPerson, cs.ApptDateUtc,
            cs.ApptVenue, cs.ApptVenueAddr, ac.ProgramStatusId,
            ac.CoachUserId, cu.FirstName as CoachFirstName, cu.LastName as CoachLastName,
            j.JobNumber
          FROM id_CoachingSession cs
          INNER JOIN al_Coachees ac ON ac.CoacheeId = cs.AbleCoacheeId
          LEFT OUTER JOIN id_Job j ON j.JobId = ac.ProgramJobId
          LEFT OUTER JOIN sv_User cu ON cu.UserId = ac.CoachUserId
          OUTER APPLY (
            SELECT ct.CoachingTypeName AS SessionTypeDisplayName, CASE WHEN cst.InPerson IS NULL THEN 0 ELSE cst.InPerson END AS InPerson,
                cst.SessionTypeDisplayName as EventSessionTypeDisplayName
            FROM id_CoachingSession csi
            LEFT OUTER JOIN al_CoachingTypes ct ON ct.CoachingTypeId = ac.CoachingTypeId
            LEFT JOIN al_CoachingTypeSessions cts on ct.CoachingTypeId = cts.CoachingTypeId and cts.SessionNumber = 1
            LEFT JOIN  al_CoachingSessionTypes cst on cst.CoachingSessionTypeId = cts.CoachingSessionTypeId
            WHERE csi.CoachingSessionId = cs.CoachingSessionId
          ) AS cst
          WHERE ac.UserId = @UserId AND ac.DeletedUtc IS NULL
          {whereClause}
          ORDER BY cs.ApptDateUTC",
          dr => {
            result.Add(new AbleSessionInfo(
              dr.GetInt("CoachingSessionId"),
              dr.GetInt("AbleCoacheeId"),
              dr.GetInt("ProgramJobId"),
              dr.GetBoolFromInt("InPerson", false),
              dr.GetString("EventSessionTypeDisplayName"),
              dr.GetDateTime("ApptDateUtc"),
              dr.GetString("ApptVenue"),
              dr.GetString("ApptVenueAddr"),
              dr.GetIntOrNull("ProgramStatusId"),
              dr.GetIntOrNull("CoachUserId"),
              dr.GetString("CoachFirstName"),
              dr.GetString("CoachLastName"),
              dr.GetString("JobNumber")
            ));
          },
          Common.NewSqlParameter("UserId", userId),
          Common.NewSqlParameter("UtcNow", DateTime.UtcNow)
        );

        return result;
      }

      public class AbleSessionList {

        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public List<AbleSessionInfo> SessionInfoList { get; internal set; }

        internal AbleSessionList(List<AbleSessionInfo> sessionInfoList = null) {
          Init(sessionInfoList, null, null, 0);
        }
        internal AbleSessionList(int? offsetRows, int? fetchRows) {
          Init(null, offsetRows, fetchRows, 0);
        }
        private void Init(List<AbleSessionInfo> sessionInfoList, int? offsetRows, int? fetchRows, int totalRows) {
          OffsetRows = offsetRows;
          FetchRows = fetchRows;
          TotalRows = totalRows;
          if (SessionInfoList == null) SessionInfoList = new List<AbleSessionInfo>();
          else SessionInfoList = sessionInfoList;
        }
      }

      public class AbleSessionInfo {

        public int SessionId { get; protected set; }
        public int? CoachUserId { get; protected set; }
        public string CoachFirstName { get; protected set; }
        public string CoachLastName { get; protected set; }
        public string CoachTimeZoneIdIANA { get; protected set; }
        public int CoacheeId { get; set; }
        public int CoacheeUserId { get; set; }
        public string CoacheeFirstName { get; protected set; }
        public string CoacheeLastName { get; protected set; }
        public string CoacheeEmail { get; protected set; }
        public int? ProgramJobId { get; protected set; }
        public string ProgramJobNumber { get; protected set; }
        public string ProgramJobName { get; protected set; }
        public string ProjectName { get; protected set; }
        public int? CoacheeProgramStatusId { get; protected set; }
        public int? CompanyId { get; protected set; }
        public string CompanyName { get; protected set; }
        public int? CoachingSessionTypeId { get; protected set; }
        public string SessionTypeDisplayName { get; protected set; }
        public string EventSessionTypeDisplayName { get; protected set; }
        public bool SessionTypeInPerson { get; protected set; }
        public string CoachNotes { get; set; }
        public DateTime ApptDateUTC { get; set; }
        public string CoacheeTimeZoneIANA { get; protected set; }
        public int DurationMins { get; set; }
        public string ApptRecheduleReason { get; protected set; }
        public int ApptRescheduleCount { get; internal set; }       // Returned but not externally assigned.
        public DateTime? ApptCancelledUtc { get; set; }   // Date marked as cancelled or no-show.
        public bool ApptCancelledLate { get; set; }       // Late cancellation.
        public bool ApptNoShow { get; set; }              // Coachee didn't turn up or missed online appt.
        public string CalendlyEventUuid { get; set; }
        public bool HasPLI { get; protected set; }                  // Has at least 1 Pay period item pointing to this component (if there is a component).
        public decimal? CoacheeRevenue { get; internal set; }
        public int CoacheeSessionsAllocated { get; protected set; }
        public decimal? ProgramDeliveryPercentage { get; internal set; }
        public int? ProgramSalesUserId { get; internal set; }
        public decimal? ProgramSalesPercentage { get; internal set; }
        public int? ProgramPLCUserId { get; internal set; }
        public decimal? ProgramPLCPercentage { get; internal set; }
        public int? QuoteItemId { get; set; }
        public decimal? SessionPrice { get; set; }
        public int? ComponentId { get; set; }
        public decimal? ComponentPrice { get; set; }
        public int? ComponentQuoteItemId { get; set; }
        public bool ComponentLocked { get; internal set; } // Component (if any) is locked.
        public string SessionNotes { get; set; }
        public string ApptVenue { get; set; }
        public string ApptVenueAddress { get; set; }
        public Subscriptions.User.UserSubscriptionInfo UserSubscription;
        public AbleUser.UserActivityInfo UserActivity;

        public AbleSessionInfo() { }

        public AbleSessionInfo(
          int sessionId, int? coachUserId, string coachFirstName, string coachLastName, string coachTimeZoneIdIANA, int ableCoacheeId, int coacheeUserId, string coacheeFirstName, string coacheeLastName, string coacheeEmail,
          int? programJobId, string programJobNumber, string programJobName, string projectName, int? companyId, string companyName,
          int? coachingSessionTypeId, string sessionTypeDisplayName, bool sessionTypeInPerson, string eventSessionTypeDisplayName, string coachNotes, DateTime apptDateUTC,
          string coacheeTimeZoneIANA, int durationMins, string apptRecheduleReason, DateTime? apptCancelledUtc, bool apptCancelledLate, bool apptNoShow,
          string calendlyEventUuid,
          bool hasPLI, decimal? coacheeRevenue, int coacheeSessionsAllocated,
          decimal? programDeliveryPercentage, int? programSalesUserId, decimal? programSalesPercentage, int? programPLCUserId, decimal? programPLCPercentage,
          int? quoteItemId, decimal? sessionPrice,
          int? componentId, decimal? componentPrice, int? componentQuoteItemId, bool componentLocked, string sessionNotes, string apptVenue, string apptVenueAddress,
          Subscriptions.User.UserSubscriptionInfo userSubscription, AbleUser.UserActivityInfo userActivity
        ) {
          this.SessionId = sessionId;
          this.CoachUserId = coachUserId;
          this.CoachFirstName = coachFirstName;
          this.CoachLastName = coachLastName;
          this.CoachTimeZoneIdIANA = coachTimeZoneIdIANA;
          this.CoacheeId = ableCoacheeId;
          this.CoacheeUserId = coacheeUserId;
          this.CoacheeFirstName = coacheeFirstName;
          this.CoacheeLastName = coacheeLastName;
          this.CoacheeEmail = coacheeEmail;
          this.ProgramJobId = programJobId;
          this.ProgramJobNumber = programJobNumber;
          this.ProgramJobName = programJobName;
          this.ProjectName = projectName;
          this.CompanyId = companyId;
          this.CompanyName = companyName;
          this.CoachingSessionTypeId = coachingSessionTypeId;
          this.SessionTypeDisplayName = sessionTypeDisplayName;
          this.SessionTypeInPerson = sessionTypeInPerson;
          this.EventSessionTypeDisplayName = eventSessionTypeDisplayName;
          this.ApptRecheduleReason = apptRecheduleReason;
          this.CoachNotes = coachNotes;
          this.ApptDateUTC = apptDateUTC;
          this.CoacheeTimeZoneIANA = coacheeTimeZoneIANA;
          this.DurationMins = durationMins;
          this.ApptCancelledUtc = apptCancelledUtc;
          this.ApptCancelledLate = apptCancelledLate;
          this.ApptNoShow = apptNoShow;
          this.CalendlyEventUuid = calendlyEventUuid;
          this.HasPLI = hasPLI;
          this.CoacheeRevenue = coacheeRevenue;
          this.CoacheeSessionsAllocated = coacheeSessionsAllocated;
          this.ProgramDeliveryPercentage = programDeliveryPercentage;
          this.ProgramSalesUserId = programSalesUserId;
          this.ProgramSalesPercentage = programSalesPercentage;
          this.ProgramPLCUserId = programPLCUserId;
          this.ProgramPLCPercentage = programPLCPercentage;
          this.QuoteItemId = quoteItemId;
          this.SessionPrice = sessionPrice;
          this.ComponentId = componentId;
          this.ComponentPrice = componentPrice;
          this.ComponentQuoteItemId = componentQuoteItemId;
          this.ComponentLocked = componentLocked;
          this.SessionNotes = sessionNotes;
          this.ApptVenue = apptVenue;
          this.ApptVenueAddress = apptVenueAddress;
          this.UserSubscription = userSubscription;
          this.UserActivity = userActivity;
        }

        public AbleSessionInfo(
          int sessionId, int ableCoacheeId, int programJobId, bool sessionTypeInPerson, string eventSessionTypeDisplayName, DateTime apptDateUTC,
           string apptVenue, string apptVenueAddress, int? coacheeProgramStatusId, int? coachUserId, string coachFirstName, string coachLastName, string programJobNumber
        ) {
          this.SessionId = sessionId;
          this.CoacheeId = ableCoacheeId;
          this.ProgramJobId = programJobId;
          this.EventSessionTypeDisplayName = eventSessionTypeDisplayName;
          this.SessionTypeInPerson = sessionTypeInPerson;
          this.ApptDateUTC = apptDateUTC;
          this.ApptVenue = apptVenue;
          this.ApptVenueAddress = apptVenueAddress;
          this.CoacheeProgramStatusId = coacheeProgramStatusId;
          this.CoachUserId = coachUserId;
          this.CoachFirstName = coachFirstName;
          this.CoachLastName = coachLastName;
          this.ProgramJobNumber = programJobNumber;
        }

        public AbleSessionInfo(int? coachUserId, int ableCoacheeId, AlbertCoachingTypes.SessionTypeInfo sessionType) {
          this.CoachUserId = coachUserId;
          this.CoacheeId = ableCoacheeId;
          this.CoachingSessionTypeId = sessionType.CoachingSessionTypeId;
          this.DurationMins = sessionType.DurationMins;
        }

        public DateTime GetApptDateInCoachTZ() {
          return (DateTime)TimeHelper.UtcToTimeZoneId(this.ApptDateUTC, this.CoachTimeZoneIdIANA).ToDateTimeOrNull();
        }
      }

      public class SessionStats {

        public int CoacheeId;
        public int? CoachUserId;
        public string CoacheeEmail;
        public DateTime? MeetCoachEmailUTC;
        public DateTime? Send360ReportUtc;
        public int SessionsAllocated;
        public int TotalSessions;
        public int SessionsBooked;
        public int SessionsUpcoming;
        public DateTime? NextApptDateUTC;
        public DateTime? EarliestApptDateUTC;
        public DateTime? LatestApptDateUTC;
        public DateTime? NextBookingTargetDateUtc;
        public DateTime? NextBookingSendReminderEmailUtc;
        public DateTime? LastSessionBookingReminderUtc;
        public int SessionBookingReminderCount;
        public List<DateTime> ListOfApptDateUtc;

        public SessionStats() {
          ListOfApptDateUtc = new List<DateTime>();
        }
      }

      public class SessionBookingReminderInfo {

        public string ProjectJobNumber { get; set; }
        public string FriendlyProjectTitle { get; set; }

        public int TenantOrgId { get; set; }
        public Guid TenantOrgGuid { get; set; }
        public int TenantOrgOwnerUserId { get; set; }
        public Guid? BrandingOrgGuid { get; set; }

        public int CoacheeId { get; set; }
        public Guid CoacheeGuid { get; set; }
        public string CoacheeFirstName { get; set; }
        public string CoacheeLastName { get; set; }
        public string CoacheeEmailAddress { get; set; }
        public int ProgramJobId { get; set; }
        public int CompanyId { get; set; }
        public int ProgramStatusId { get; set; }
        public int CoachingTypeId { get; set; }
        public int SessionsAllocated { get; set; }
        public int SessionsBooked { get; set; }
        public DateTime? WelcomeEmailUtc { get; set; }
        public DateTime? MeetCoachEmailUtc { get; set; }
        public DateTime? MeetCoachEmailSentUtc { get; set; }
        public DateTime? LatestBookingUtc { get; set; }
        public DateTime? BookingReminderLastSentUtc { get; set; }
        public int DaysPassed { get; set; }
        public int MonthsPassed { get; set; }
        public DateTime NextBookingTargetDateUtc { get; set; }
        public int BookingReminderCount { get; set; }
        public string CoachFirstName { get; set; }
        public string CoachLastName { get; set; }
        public string CoachEmailAddress { get; set; }
        public string EmailSenderName { get; set; }
        public string EmailSenderAddress { get; set; }
        public string BookSessionEmailCustomHTML { get; set; }
        public string BookingReminderCadenceDays { get; set; }

        public SessionBookingReminderInfo(

          string projectJobNumber,
          string friendlyProjectTitle,

          int tenantOrgId,
          Guid tenantOrgGuid,
          int tenantOrgOwnerUserId,
          Guid? brandingOrgGuid,

          int coacheeId,
          Guid coacheeGuid,
          string coacheeFirstName,
          string coacheeLastName,
          string coacheeEmailAddress,
          int programJobId,
          int companyId,
          int programStatusId,
          int coachingTypeId,
          int sessionsAllocated,
          int sessionsBooked,
          DateTime? welcomeEmailUtc,
          DateTime? meetCoachEmailUtc,
          DateTime? meetCoachEmailSentUtc,
          DateTime? latestBookingUtc,
          DateTime? bookingReminderLastSentUtc,
          int daysPassed,
          DateTime nextBookingTargetDateUtc,
          int bookingReminderCount,
          string coachFirstName,
          string coachLastName,
          string coachEmailAddress,
          string emailSenderName,
          string emailSenderAddress,
          string bookSessionEmailCustomHTML,
          string bookingReminderCadenceDays
        ) {
          this.ProjectJobNumber = projectJobNumber;
          this.FriendlyProjectTitle = friendlyProjectTitle;

          this.TenantOrgId = tenantOrgId;
          this.TenantOrgOwnerUserId = tenantOrgOwnerUserId;

          this.TenantOrgGuid = tenantOrgGuid;
          this.BrandingOrgGuid = brandingOrgGuid;
          this.CoacheeId = coacheeId;
          this.CoacheeGuid = coacheeGuid;
          this.CoacheeFirstName = coacheeFirstName;
          this.CoacheeLastName = coacheeLastName;
          this.CoacheeEmailAddress = coacheeEmailAddress;
          this.ProgramJobId = programJobId;
          this.CompanyId = companyId;
          this.ProgramStatusId = programStatusId;
          this.CoachingTypeId = coachingTypeId;
          this.SessionsAllocated = sessionsAllocated;
          this.SessionsBooked = sessionsBooked;
          this.WelcomeEmailUtc = welcomeEmailUtc;
          this.MeetCoachEmailUtc = meetCoachEmailUtc;
          this.MeetCoachEmailSentUtc = meetCoachEmailSentUtc;
          this.LatestBookingUtc = latestBookingUtc;
          this.BookingReminderLastSentUtc = bookingReminderLastSentUtc;
          this.DaysPassed = daysPassed;
          this.MonthsPassed = daysPassed / 30;
          this.NextBookingTargetDateUtc = nextBookingTargetDateUtc;
          this.BookingReminderCount = bookingReminderCount;
          this.CoachFirstName = coachFirstName;
          this.CoachLastName = coachLastName;
          this.CoachEmailAddress = coachEmailAddress;
          this.EmailSenderName = emailSenderName;
          this.EmailSenderAddress = emailSenderAddress;
          this.BookSessionEmailCustomHTML = bookSessionEmailCustomHTML;
          this.BookingReminderCadenceDays = bookingReminderCadenceDays;
        }
      }

      public class SessionListItemInfo : DbHelper.ProgramComponents.SessionComponentInfo {

        public DateTime? CompletedLocal { get; set; }

        public SessionListItemInfo(
          DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
          int sessionNumber, int? sessionId, DateTime? completedUtc, int? durationMins, decimal? componentPrice, int? quoteItemId, bool isLocked) {

          SessionNumber = sessionNumber;
          SessionId = sessionId;
          CompletedUtc = completedUtc;
          CompletedLocal = TimeHelper.UtcToTimeZoneId(completedUtc, coacheeInfo.UserActivity.CoachTimeZoneIdIANA).ToDateTimeOrNull();
          DurationMins = durationMins;
          ComponentPrice = componentPrice;
          QuoteItemId = quoteItemId;
          IsLocked = isLocked;
        }
      }
    }
  }
}
