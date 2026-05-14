using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AlbertCoacheeComms {

      private const int DefaultMinDaysBetweenNudgeComms = 6;
      private const int DefaultMinDaysBetweenGeneralComms = 6;
      private static readonly DateTime commsAutomationStartDateUtc = DateTime.Parse("2021-02-10");

      static AlbertCoacheeComms() {
      }

      public static CommsInfo GetCommsByCoacheeIdOrNull(int coacheeId) {
        var mcl = GetCoacheeCommsList(1,
          "CoacheeId = @CoacheeId",
          Common.NewSqlParameter("@CoacheeId", coacheeId));
        if (mcl != null && mcl.CommsInfoList.Count > 0) return mcl.CommsInfoList[0];
        else return null;
      }

      private static CommsList GetCoacheeCommsList(
        int? topOrNullForAll,
        string sqlWhereConditions,
        string sqlOrderBy,
        int? offsetRows,
        int? fetchRows,
        params SqlParameter[] sqlWhereParams
      ) {

        var coacheeCommsList = new CommsList();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        string sql = $@"
          SELECT {sqlTop}
            COUNT(*) OVER() AS TotalRows,
            cc.CoacheeCommsId, cc.OccurredUtc, cc.CoacheeId, cc.NudgeContentId
          FROM al_CoacheeComms cc
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        if (sqlTop.IsNullOrEmpty() && !sqlOrderBy.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          coacheeCommsList.OffsetRows = offsetRows;
          coacheeCommsList.FetchRows = fetchRows;
          sql += $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            if (sqlWhereParams != null) cmd.Parameters.AddRange(sqlWhereParams);
            conn.Open();
            using (var dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                if (coacheeCommsList.TotalRows == 0) coacheeCommsList.TotalRows = dr.GetInt("TotalRows");
                coacheeCommsList.CommsInfoList.Add(new CommsInfo(
                  dr.GetInt("CoacheeCommsId"),
                  dr.GetDateTime("OccurredUtc"),
                  dr.GetInt("CoacheeId"),
                  dr.GetIntOrNull("NudgeContentId")
                ));
              }
            }
          }
        }
        return coacheeCommsList;
      }

      private static CommsList GetCoacheeCommsList(
        int? topOrNullForAll,
        string sqlWhereConditions,
        params SqlParameter[] sqlWhereParams) {
        return GetCoacheeCommsList(topOrNullForAll, sqlWhereConditions, null, null, null, sqlWhereParams);
      }

      public static List<NudgeStatus> GetNudgeStatusList(int coacheeId, bool getNextEvenIfNotDue) {
        return GetNudgeStatusList(DateTime.UtcNow, null, coacheeId, getNextEvenIfNotDue);
      }
      public static List<NudgeStatus> GetNudgeStatusList(CoacheeProgramStatus.ProgramStatusInfo programStatusOrNullForAll, bool getNextEvenIfNotDue) {
        return GetNudgeStatusList(DateTime.UtcNow, programStatusOrNullForAll, null, getNextEvenIfNotDue);
      }
      private static List<NudgeStatus> GetNudgeStatusList(
        DateTime currentDateUtc,
        CoacheeProgramStatus.ProgramStatusInfo programStatusOrNullForAll,
        int? coacheeIdOrNullForAll,
        bool getNextEvenIfNotDue) {

        var nextDueNudgeList = new List<NudgeStatus>();
        int? programStatusId = programStatusOrNullForAll == null ? null : (int?)programStatusOrNullForAll.ProgramStatusId;

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = $@"
            SELECT
              ac.CoacheeId, ac.ProgramJobId, ac.FirstName, ac.LastName, ac.EmailAddress,
              ac.SessionsAllocated, ac.SessionsCompleted,
              ac.CoachUserId, cu.FirstName AS CoachFirstName,
              cu.LastName AS CoachLastName, cu.Email AS CoachEmailAddress,
              dbo.fnGetUserSenderEmailName(cu.UserId) AS ComputedSenderEmailName,
              dbo.fnGetUserSenderEmailAddress(cu.UserId) AS ComputedSenderEmailAddress,
              laNud.LastNSentUtc, laNud.LastNCId, laNud.LastNCO, laNud.LastNTId, laNud.LastNCTitle, laNud.LastNCBodyText,
              nxNud.NextNCId, nxNud.NextNCO, nxNud.NextNTId, nxNud.NextNCTitle, nxNud.NextNCBodyText,
              j.JobNumber,
              ap.FriendlyProjectTitle, ap.SvCompanyId,
              org.OrgGuid AS BrandingOrgGuid,
              {Subscriptions.User.GetSubscriptionOuterApplySelectionSQL}
            FROM al_Coachees ac
            INNER JOIN sv_User cu ON ac.CoachUserId = cu.UserId -- ensure coach is set
            INNER JOIN id_Job j ON ac.ProgramJobId = j.JobId    -- ensure program is set
            INNER JOIN al_Project ap ON ap.JobNumber = j.JobNumber
            LEFT OUTER JOIN sv_Organisation org ON org.OrgId = ap.BrandingOrgId
            -- Latest nudge comms sent, if any.
            OUTER APPLY (
              SELECT TOP 1 cc.OccurredUtc AS LastNSentUtc, cc.NudgeContentId AS LastNCId, nc.NudgeOrder AS LastNCO,
                nc.NudgeTypeId AS LastNTId, nc.NudgeTitle AS LastNCTitle, nc.NudgeBodyText AS LastNCBodyText
              FROM al_CoacheeComms cc
              INNER JOIN al_NudgeContent nc ON nc.NudgeContentId = cc.NudgeContentId
              WHERE cc.CoacheeId = ac.CoacheeId
              ORDER BY cc.OccurredUtc DESC
            ) AS laNud
            -- Next due nudge content.
            OUTER APPLY (
              SELECT TOP 1 nc.NudgeContentId AS NextNCId, nc.NudgeOrder AS NextNCO,
                nc.NudgeTypeId AS NextNTId, nc.NudgeTitle AS NextNCTitle, nc.NudgeBodyText AS NextNCBodyText
              FROM al_NudgeContent nc
              WHERE ac.CoachUserId <> @CoachUserId_Unassigned
                AND ac.CoachingTypeId <> @CoachingTypeId_None
                AND ac.DisableNudges = 0
                AND nc.NudgeTypeId = @NudgeTypeId_Coaching
                AND nc.NudgeOrder > ISNULL(laNud.LastNCO, 0)
                AND (@GetNextEvenIfNotDue = 1 OR laNud.LastNSentUtc IS NULL OR DATEDIFF(DAY, laNud.LastNSentUtc, @CurrentDateUtc) >= @MinDaysBetweenComms)
              ORDER BY nc.NudgeOrder
            ) AS nxNud
            {Subscriptions.User.GetUserSubscriptionOuterApplySQL("ac")}
            WHERE (@CoacheeId IS NULL OR @CoacheeId IS NOT NULL AND ac.CoacheeId = @CoacheeId)
              AND (@ProgramStatusId IS NULL OR @ProgramStatusId IS NOT NULL AND ac.ProgramStatusId = @ProgramStatusId)
              AND (@GetNextEvenIfNotDue = 1 OR nxNud.NextNCId IS NOT NULL)
              AND (@GetNextEvenIfNotDue = 1 OR ac.SessionsCompleted > 1)
              AND ac.DeletedUtc IS NULL";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.Add(
              Common.NewSqlParameter("@ProgramStatusId", programStatusId),
              Common.NewSqlParameter("@CoachingTypeId_None", DbHelper.AlbertCoachingTypes.GetType_NoCoaching().CoachingTypeId),
              Common.NewSqlParameter("@CoachUserId_Unassigned", ConfigHelper.UserId.Unassigned),
              Common.NewSqlParameter("@NudgeTypeId_Coaching", DbHelper.AlbertNudgeType.NudgeType_Coaching.NudgeTypeId),
              Common.NewSqlParameter("@CommsAutomationStartDateUtc", commsAutomationStartDateUtc),
              Common.NewSqlParameter("@CurrentDateUtc", currentDateUtc),
              Common.NewSqlParameter("@CoacheeId", coacheeIdOrNullForAll),
              Common.NewSqlParameter("@MinDaysBetweenComms", DefaultMinDaysBetweenNudgeComms),
              Common.NewSqlParameter("@GetNextEvenIfNotDue", getNextEvenIfNotDue));
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                var nextDueNudge = new NudgeStatus(
                  new CoacheeInfo(dr, "CoacheeId", "FriendlyProjectTitle", "BrandingOrgGuid", "ProgramJobId", "JobNumber", "SvCompanyId", "FirstName", "LastName", "EmailAddress", null),
                  Subscriptions.User.GetUserSubscriptionInfo(dr),
                  new CoachInfo(dr, "CoachUserId", "CoachFirstName", "CoachLastName", "CoachEmailAddress", "ComputedSenderEmailName", "ComputedSenderEmailAddress"),
                  dr.IsDBNull("LastNCId") ? null : new NudgeComms(dr, "LastNSentUtc", "LastNCId", "LastNCO", "LastNTId", "LastNCTitle", "LastNCBodyText"), // last nudge sent
                  dr.IsDBNull("NextNCId") ? null : new NudgeComms(dr, null, "NextNCId", "NextNCO", "NextNTId", "NextNCTitle", "NextNCBodyText") // next nudge to send
                );
                nextDueNudgeList.Add(nextDueNudge);
              }
            }
          }
        }
        return nextDueNudgeList;
      }

      public static int? AddNudgeComms(int coacheeId, int nudgeContentId, DateTime? commsSentUtc = null) {
        if (commsSentUtc == null) commsSentUtc = DateTime.UtcNow;
        return Common.GetScalarQueryInt(
          "INSERT INTO al_CoacheeComms (OccurredUtc, CoacheeId, NudgeContentId) "
          + "OUTPUT INSERTED.CoacheeCommsId "
          + "VALUES (@CommsSentUtc, @CoacheeId, @NudgeContentId)",
          Common.NewSqlParameter("@CommsSentUtc", commsSentUtc),
          Common.NewSqlParameter("@CoacheeId", coacheeId),
          Common.NewSqlParameter("@NudgeContentId", nudgeContentId));
      }

      public static void DeleteComms(int coacheeCommsId) {
        Common.GetScalarQueryInt(
          "DELETE FROM al_CoacheeComms WHERE CoacheeCommsId = @CoacheeCommsId",
          Common.NewSqlParameter("@CoacheeCommsId", coacheeCommsId));
      }

      public class CommsList {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public List<CommsInfo> CommsInfoList { get; internal set; }
        public CommsList() {
          OffsetRows = null;
          FetchRows = null;
          TotalRows = 0;
          CommsInfoList = new List<CommsInfo>();
        }
      }

      public class CommsInfo {

        public int CoacheeCommsId { get; private set; }
        public DateTime CommsSentUtc { get; private set; }
        public int CoacheeId { get; private set; }
        public int? NudgeContentId { get; private set; }

        public CommsInfo(
          int coacheeCommsId,
          DateTime commsSentUtc,
          int coacheeId,
          int? nudgeContentId
        ) {
          this.CoacheeCommsId = coacheeCommsId;
          this.CommsSentUtc = commsSentUtc;
          this.CoacheeId = coacheeId;
          this.NudgeContentId = nudgeContentId;
        }
      }

      public class NudgeStatus {

        public CoacheeInfo Coachee { get; private set; }
        public CoachInfo Coach { get; private set; }
        public NudgeComms LastNudge { get; private set; }
        public NudgeComms NextNudge { get; private set; }
        public Subscriptions.User.UserSubscriptionInfo UserSubscription { get; private set; }

        public NudgeStatus(
          CoacheeInfo coacheeInfo,
          Subscriptions.User.UserSubscriptionInfo userSubscription,
          CoachInfo coachInfo, NudgeComms lastNudgeComms, NudgeComms nextNudgeComms
        ) {
          this.Coachee = coacheeInfo;
          this.UserSubscription = userSubscription;
          this.Coach = coachInfo;
          this.LastNudge = lastNudgeComms;
          this.NextNudge = nextNudgeComms;
        }
      }

      public class CoacheeInfo {

        public int CoacheeId { get; private set; }
        public int ProgramJobId { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public string FriendlyProjectTitle { get; private set; }
        public int CompanyId { get; private set; }
        public Guid? BrandingOrgGuid { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string EmailAddress { get; private set; }
        public int SessionsAllocated { get; private set; }
        public int SessionsCompleted { get; private set; }
        public DateTime? WelcomeEmailUtc { get; private set; }

        public CoacheeInfo(AlbertCoachees.AlbertCoacheeInfo ci) {
          Init(ci.CoacheeId, ci.FriendlyProjectTitle, ci.BrandingOrgGuid,
            (int)ci.ProgramJobId, ci.ProgramJobNumber, ci.CompanyId.Value,
            ci.FirstName, ci.LastName, ci.EmailAddress, ci.WelcomeEmailUtc);
        }
        public CoacheeInfo(
          int coacheeId, string friendlyProjectTitle, Guid? brandingOrgGuid,
          int programJobId, string programJobNumber, int companyId,
          string firstName, string lastName, string emailAddress, DateTime? welcomeEmailUtc) {

          Init(coacheeId, friendlyProjectTitle, brandingOrgGuid, programJobId, programJobNumber, companyId, firstName, lastName, emailAddress, welcomeEmailUtc);
        }
        public CoacheeInfo(SqlDataReader dr,
          string col_coacheeId, string col_friendlyProjectTitle, string col_brandingOrgGuid,
          string col_programJobId, string col_programJobNumber, string col_companyId,
          string col_firstName, string col_lastName, string col_emailAddress, string col_welcomeEmailUtc) {

          Init(
            dr.GetInt(col_coacheeId), dr.GetString(col_friendlyProjectTitle), dr.GetGuidOrNull(col_brandingOrgGuid),
            dr.GetInt(col_programJobId), dr.GetString(col_programJobNumber), dr.GetInt(col_companyId),
            dr.GetString(col_firstName), dr.GetString(col_lastName), dr.GetString(col_emailAddress),
            dr.GetDateTimeOrNull(col_welcomeEmailUtc, true));
        }
        private void Init(
          int coacheeId, string friendlyProjectTitle, Guid? brandingOrgGuid,
          int programJobId, string programJobNumber, int companyId,
          string firstName, string lastName, string emailAddress, DateTime? welcomeEmailUtc) {

          CoacheeId = coacheeId; FirstName = firstName; LastName = lastName; EmailAddress = emailAddress;
          ProgramJobId = programJobId; ProgramJobNumber = programJobNumber; FriendlyProjectTitle = friendlyProjectTitle;
          CompanyId = companyId; BrandingOrgGuid = brandingOrgGuid; WelcomeEmailUtc = welcomeEmailUtc;
        }
      }

      public class CoachInfo {
        public int UserId { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string EmailAddress { get; private set; }
        public string ComputedSenderEmailName { get; private set; }
        public string ComputedSenderEmailAddress { get; private set; }
        public CoachInfo(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {
          Init(coachInfo.UserId, coachInfo.FirstName, coachInfo.LastName, coachInfo.EmailAddress, coachInfo.ComputedSenderEmailName, coachInfo.ComputedSenderEmailAddress);
        }
        public CoachInfo(SqlDataReader dr, string col_userId, string col_firstName, string col_lastName, string col_emailAddress, string col_computedSenderEmailName, string col_computedSenderEmailAddress) {
          Init(dr.GetInt(col_userId), dr.GetString(col_firstName), dr.GetString(col_lastName), dr.GetString(col_emailAddress), dr.GetString(col_computedSenderEmailName), dr.GetString(col_computedSenderEmailAddress));
        }
        private void Init(int userId, string firstName, string lastName, string emailAddress, string computedSenderEmailName, string computedSenderEmailAddress) {
          UserId = userId; FirstName = firstName; LastName = lastName; EmailAddress = emailAddress;
          ComputedSenderEmailName = computedSenderEmailName; ComputedSenderEmailAddress = computedSenderEmailAddress;
        }
      }

      public class Workshop {

        public int? WorkshopEventId { get; private set; }
        public DateTime? StartDateLocal { get; private set; }
        public string TimeZoneIdIana { get; private set; }
        public TimeZoneInfo TimeZone { get; private set; }
        public int? FacilitatorUserId { get; private set; }
        public string FacilitatorFirstName { get; private set; }
        public string FacilitatorLastName { get; private set; }
        public string FacilitatorEmail { get; private set; }

        public Workshop(DbHelper.WorkshopEvents.WorkshopEventInfo workshopEvent) {
          Init(workshopEvent.WorkshopEventId, workshopEvent.WhenStartLocal, workshopEvent.TimeZoneIdIana,
            workshopEvent.KeyFacilitatorUserId, workshopEvent.KeyFacilitatorFirstName, workshopEvent.KeyFacilitatorLastName, workshopEvent.KeyFacilitatorEmail);
        }
        public Workshop(int? workshopEventId, DateTime? startDateLocal, string timeZoneIdIana,
          int? facilitatorUserId, string facilitatorFirstName, string facilitatorLastName, string facilitatorEmail) {
          Init(workshopEventId, startDateLocal, timeZoneIdIana, facilitatorUserId, facilitatorFirstName, facilitatorLastName, facilitatorEmail);
        }
        public Workshop(SqlDataReader dr, string col_workshopEventId, string col_startDateLocal, string col_timeZoneIdIana,
          string col_facilitatorUserId, string col_facilitatorFirstName, string col_facilitatorLastName, string col_facilitatorEmail) {
          Init(dr.GetInt(col_workshopEventId), dr.GetDateTime(col_startDateLocal), dr.GetString(col_timeZoneIdIana),
            dr.GetIntOrNull(col_facilitatorUserId), dr.GetString(col_facilitatorFirstName), dr.GetString(col_facilitatorLastName), dr.GetString(col_facilitatorEmail));
        }
        private void Init(int? workshopEventId, DateTime? startDateLocal, string timeZoneIdIana,
          int? facilitatorUserId, string facilitatorFirstName, string facilitatorLastName, string facilitatorEmail) {
          WorkshopEventId = workshopEventId; StartDateLocal = startDateLocal; TimeZoneIdIana = timeZoneIdIana;
          FacilitatorUserId = facilitatorUserId; FacilitatorFirstName = facilitatorFirstName; FacilitatorLastName = facilitatorLastName; FacilitatorEmail = facilitatorEmail;
          TimeZone = TimeHelper.IANAToTimeZoneOrAppDefault(timeZoneIdIana);
        }
      }

      public class NudgeComms {
        public DateTime? CommsSentUtc { get; private set; }
        public int NudgeContentId { get; private set; }
        public int NudgeContentOrder { get; private set; }
        public int NudgeTypeId { get; private set; }
        public string NudgeContentTitle { get; private set; }
        public string NudgeContentBodyText { get; private set; }

        public NudgeComms(AlbertNudgeContent.NudgeContentInfo ncInfo) {
          Init(null, ncInfo.NudgeContentId, ncInfo.NudgeOrder, ncInfo.NudgeTypeId, ncInfo.NudgeTitle, ncInfo.NudgeBodyText);
        }
        public NudgeComms(DateTime? commsSentUtc, int nudgeContentId, int nudgeContentOrder, int nudgeTypeId, string nudgeContentTitle, string nudgeContentBodyText) {
          Init(commsSentUtc, nudgeContentId, nudgeContentOrder, nudgeTypeId, nudgeContentTitle, nudgeContentBodyText);
        }
        public NudgeComms(SqlDataReader dr, string col_commsSentUtc, string col_nudgeContentId, string col_nudgeContentOrder,
          string col_nudgeTypeId, string col_nudgeContentTitle, string col_nudgeContentBodyText) {
          Init(dr.GetDateTimeOrNull(col_commsSentUtc, true), dr.GetInt(col_nudgeContentId), dr.GetInt(col_nudgeContentOrder),
          dr.GetInt(col_nudgeTypeId), dr.GetString(col_nudgeContentTitle, true), dr.GetString(col_nudgeContentBodyText, true));
        }
        private void Init(DateTime? commsSentUtc, int nudgeContentId, int nudgeContentOrder, int nudgeTypeId, string nudgeContentTitle, string nudgeContentBodyText) {
          CommsSentUtc = commsSentUtc; NudgeContentId = nudgeContentId; NudgeContentOrder = nudgeContentOrder;
          NudgeTypeId = nudgeTypeId; NudgeContentTitle = nudgeContentTitle; NudgeContentBodyText = nudgeContentBodyText;
        }
      }

    }
  }
}

