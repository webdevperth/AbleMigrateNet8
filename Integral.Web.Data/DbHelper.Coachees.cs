using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AlbertCoachees {

      public const int Min_Days_Between_Welcome_And_MeetCoach_Emails = 1;

      private const string SQLParam_CoachUserId = "@QAC_CoachUserId";
      private const string SQLParam_CompanyId = "@QAC_CompanyId";
      private const string SQLParam_ProgramJobId = "@QAC_ProgramJobId";
      private static readonly DateTime MeetCoachEmail_IgnorePriorToUtc = new DateTime(2020, 11, 3); // Date after which to process Welcome emails. Before this date they were handled elsewhere.

      static AlbertCoachees() {
      }

      public static AlbertCoacheeInfo GetCoacheeInfo(int coacheeId) {
        return GetCoacheeInfo((SqlTransaction)null, coacheeId);
      }

      public static AlbertCoacheeInfo GetCoacheeInfo(SqlTransaction trans, int coacheeId) {

        return GetCoacheeInfo(trans,
          null,
          "c.CoacheeId = @CoacheeId",
          null,
          NewSqlParameter("CoacheeId", coacheeId)
        );
      }

      public static AlbertCoacheeInfo GetCoacheeInfo(Guid coacheeUID) {

        return GetCoacheeInfo(null,
          null,
          "c.CoacheeUID = @CoacheeUID",
          null,
          NewSqlParameter("CoacheeUID", coacheeUID)
        );
      }

      public static AlbertCoacheeInfo GetCoacheeInfo(string emailAddress, int programJobId) {

        if (emailAddress.IsNullOrEmpty()) return null;

        return GetCoacheeInfo(null,
          null,
          "c.EmailAddress = @EmailAddress AND c.ProgramJobId = @ProgramJobId",
          null,
          NewSqlParameter("EmailAddress", emailAddress),
          NewSqlParameter("ProgramJobId", programJobId)
        );
      }

      private static AlbertCoacheeInfo GetCoacheeInfo(SqlTransaction trans, string extraJoins, string sqlWhereConditions, string sqlOrderByColumns, params SqlParameter[] sqlWhereParams) {

        var coacheeInfoList = GetCoacheeInfoList(trans, extraJoins, sqlWhereConditions, sqlOrderByColumns, sqlWhereParams);

        if (coacheeInfoList.Count == 0) return null;

        return coacheeInfoList[0];
      }

      // Return list of coachees found by email.
      public static List<AlbertCoacheeInfo> GetCoacheesByEmail(string emailAddress) {

        if (emailAddress.IsNullOrEmpty()) throw new ArgumentException("EmailAddress is required.");

        return GetCoacheeInfoList(null,
          null,
          "c.EmailAddress = @EmailAddress",
          "c.RowCreatedUtc DESC",
          NewSqlParameter("EmailAddress", emailAddress)
        );
      }

      public static List<AlbertCoacheeInfo> GetCoacheesByUserId(int userId) {

        return GetCoacheeInfoList(null,
          null,
          "c.UserId = @UserId",
          "c.RowCreatedUtc DESC",
          NewSqlParameter("UserId", userId)
        );
      }

      public static AlbertCoacheeInfo GetLatestActiveCoacheeByEmail(string emailAddress) {

        if (emailAddress.IsNullOrEmpty()) throw new ArgumentException("EmailAddress is required.");

        return GetCoacheeInfo(null,
          null,
          "c.EmailAddress = @EmailAddress AND c.ProgramStatusId = @ActiveProgramStatusId",
          "j.AbleProgramEndDateUtc DESC",
          NewSqlParameter("EmailAddress", emailAddress),
          NewSqlParameter("ActiveProgramStatusId", CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId)
        );
      }

      // Return latest single coachee by email.
      public static AlbertCoacheeInfo GetLatestCoacheeByEmail(string emailAddress) {

        if (emailAddress.IsNullOrEmpty()) throw new ArgumentException("EmailAddress is required.");

        var coacheeInfoList = GetCoacheeInfoList(null,
          null,
          "c.EmailAddress = @EmailAddress",
          "c.RowCreatedUtc DESC",
          NewSqlParameter("EmailAddress", emailAddress)
        );
        if (coacheeInfoList.Count == 0) return null;
        else return coacheeInfoList[0];
      }

      // Get earliest coachee that isn't end-program and has unbooked session(s).
      // Coachee must have the given coach assigned.
      // Coachee must have SessionsBooked < SessionsAllocated (implies that SessionsAllocated > 0).
      public static AlbertCoacheeInfo GetCoacheeForSessionBooking(string emailAddress, int coachUserId) {

        if (emailAddress.IsNullOrEmpty()) throw new ArgumentException("EmailAddress is required.");

        var coacheeInfoList = GetCoacheeInfoList(null,
          null,
          "c.EmailAddress = @EmailAddress AND c.CoachUserId = @CoachUserId AND c.SessionsBooked < c.SessionsAllocated AND c.ProgramStatusId <> @EndProgramStatusId",
          "c.MeetCoachEmailUtc, c.RowCreatedUtc",
          NewSqlParameter("EmailAddress", emailAddress),
          NewSqlParameter("CoachUserId", coachUserId),
          NewSqlParameter("EndProgramStatusId", CoacheeProgramStatus.GetStatus_EndProgram().ProgramStatusId)
        );

        if (coacheeInfoList.IsNullOrEmpty()) return null;
        return coacheeInfoList[0];
      }

      public static List<AlbertCoacheeInfo> GetCoacheesDueForSetEndProgram() {
        // TODO: 2021-04-19 This needs refining, might be getting a lot of wrong results?

        return GetCoacheeInfoList(null,
          @"OUTER APPLY ( -- Get latest workshop date, if any.
            SELECT COUNT(we.WorkshopEventId) AS WorkshopCount, MAX(we.StartDate) AS LatestWorkshopDate
            FROM ev_WorkshopEvent we
            WHERE we.ProgramJobId = c.ProgramJobId
          ) AS we", $@"
          WHERE c.ProgramStatusId = {CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId}
            AND (j.AbleProgramEndDateUtc IS NULL OR j.AbleProgramEndDateUtc < GETUTCDATE())
            AND (c.CoachUserId IS NULL OR c.CoachUserId = {ConfigHelper.UserId.Unassigned} OR c.SessionsCompleted >= c.SessionsAllocated)
            AND (we.LatestWorkshopDate IS NULL OR DATEDIFF(DAY, we.LatestWorkshopDate, GETUTCDATE()) > 0)",
          "", null
        );
      }

      private static List<AlbertCoacheeInfo> GetCoacheeInfoList(
        SqlTransaction trans,
        string extraJoins,
        string sqlWhereConditions,
        string sqlOrderByColumns,
        params SqlParameter[] sqlWhereParams) {

        var coacheeInfoList = new List<AlbertCoacheeInfo>();

        string sql = $@"
          SELECT
            c.CoacheeId, c.UserId, c.CoachUserId, c.PersonId, c.CompanyId, c.ProgramJobId, c.IntercomUserId, c.FirstName, c.LastName,
            c.EmailAddress, c.EmailAddress as CurrentEmailAddress, c.MobilePhone, c.CoacheeUID, c.CoachUserId,
            c.CoachingTypeId, c.ProgramStatusId,
            c.WelcomeEmailUtc, c.WelcomeEmailSentUtc, c.MeetCoachEmailUtc, c.MeetCoachEmailSentUtc,
            c.Send360ReportUtc, c.DisableNudges, c.PulseSurveyEnabled, c.PulseSurveyId, c.PulseSurveyLastCreatedUtc,
            c.SubscriptionUser, c.NextBookingTargetDateUtc, c.NextBookingSendReminderEmailUtc, BookingReminderLastSentUtc,
            c.CoacheeNotes, c.PrivateCoachNote,
            u.UserGuid,
            cmp.CompanyName,
            j.JobNumber AS ProgramJobNumber, j.JobUID AS ProgramJobUID, j.JobName AS ProgramName,
            j.AbleProgramStartDateUtc, j.AbleProgramEndDateUtc, j.CoachingText AS ProgramNotes,
            prj.FriendlyProjectTitle, prj.ProjectName, prj.PulseSurveyDisabled, prj.PulseSurveyTemplateId, prj.SendIntakeSurveyToAllParticipants,
            p_org.OrgId AS TenantOrgId, p_org.OrgOwnerUserId,
            org.OrgGuid AS BrandingOrgGuid,
            cp.CoachingRevenue,
            IIF(EXISTS(SELECT 1 FROM al_Component com WHERE com.CoacheeId = c.CoacheeId AND com.LockedDateUtc IS NOT NULL), 1, 0) AS HasLockedComponents,
            {Subscriptions.User.GetSubscriptionOuterApplySelectionSQL},
            {AbleUser.GetUserActivityLeftJoinSelectionSQL}
          FROM al_Coachees c
          INNER JOIN sv_User u ON u.UserId = c.UserId
          INNER JOIN id_Job j ON j.JobId = c.ProgramJobId
          INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          INNER JOIN sv_Organisation p_org ON p_org.OrgId = prj.ParentOrgId
          LEFT OUTER JOIN sv_Organisation org ON org.OrgId = prj.BrandingOrgId
          LEFT OUTER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = c.CompanyId
          OUTER APPLY (SELECT SUM(cp.ComponentPrice) AS CoachingRevenue FROM al_Component cp WHERE cp.CoacheeId = c.CoacheeId) AS cp
          {AbleUser.GetUserActivityInfoLeftJoinSQL("c", "c")}
          {Subscriptions.User.GetUserSubscriptionOuterApplySQL("c")}
          {extraJoins.EmptyIfNull()}
          WHERE c.DeletedUtc IS NULL
          {sqlWhereConditions.SurroundWith("AND (", ")", false).EmptyIfNull()}
          {sqlOrderByColumns.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        var sqlParams = new List<SqlParameter>(AbleUser.GetUserActivityInfoParamsSQL());

        foreach (var whereParam in sqlWhereParams) {
          var sqlParam = sqlParams.Find(p => p.ParameterName.Equals(whereParam.ParameterName, StringComparison.OrdinalIgnoreCase));
          if (sqlParam != null) sqlParam.Value = whereParam.Value;
          else sqlParams.Add(whereParam);
        }

        Query(sql, sqlParams, dr => {

          coacheeInfoList.Add(new AlbertCoacheeInfo(

            tenantOrgId: dr.GetInt("TenantOrgId"),
            tenantOrgOwnerUserId: dr.GetInt("OrgOwnerUserId"),

            coacheeId: dr.GetInt("CoacheeId"),
            coachUserId: dr.GetInt("CoachUserId"),
            userId: dr.GetIntOrNull("UserId"),
            userGuid: dr.GetGuidOrNull("UserGuid"),
            personId: dr.GetIntOrNull("PersonId"),
            companyId: dr.GetIntOrNull("CompanyId"),
            companyName: dr.GetString("CompanyName"),
            intercomUserId: dr.GetString("IntercomUserId"),
            firstName: dr.GetString("FirstName"),
            lastName: dr.GetString("LastName"),
            emailAddress: dr.GetString("EmailAddress"),
            currentEmailAddress: dr.GetString("CurrentEmailAddress"),
            mobilePhone: dr.GetString("MobilePhone"),
            coacheeUID: dr.GetGuid("CoacheeUID"),
            coachingRevenue: dr.GetDecimalOrNull("CoachingRevenue"),
            coachingTypeId: dr.GetIntOrNull("CoachingTypeId"),
            programStatusId: dr.GetInt("ProgramStatusId"),
            disableNudges: dr.GetBoolFromInt("DisableNudges"),
            pulseSurveyEnabled: dr.GetBoolFromInt("PulseSurveyEnabled"),
            pulseSurveyId: dr.GetIntOrNull("PulseSurveyId"),
            pulseSurveyLastSentUtc: dr.GetDateTimeOrNull("PulseSurveyLastCreatedUtc"),
            subscriptionUser: dr.GetBoolFromInt("SubscriptionUser"),
            hasLockedComponents: dr.GetBoolFromInt("HasLockedComponents"),
            coacheeNotes: dr.GetString("CoacheeNotes"),
            privateCoachNote: dr.GetString("PrivateCoachNote"),
            welcomeEmailUtc: dr.GetDateTimeOrNull("WelcomeEmailUtc"),
            welcomeEmailSentUtc: dr.GetDateTimeOrNull("WelcomeEmailSentUtc"),
            meetCoachEmailUtc: dr.GetDateTimeOrNull("MeetCoachEmailUtc"),
            meetCoachEmailSentUtc: dr.GetDateTimeOrNull("MeetCoachEmailSentUtc"),
            send360ReportUtc: dr.GetDateTimeOrNull("Send360ReportUtc"),
            nextBookingTargetDateUtc: dr.GetDateTimeOrNull("NextBookingTargetDateUtc"),
            nextBookingSendReminderEmailUtc: dr.GetDateTimeOrNull("NextBookingSendReminderEmailUtc"),
            bookingReminderLastSentUtc: dr.GetDateTimeOrNull("BookingReminderLastSentUtc"),
            programJobId: dr.GetIntOrNull("ProgramJobId"),
            programJobUID: dr.GetString("ProgramJobUID"),
            programJobNumber: dr.GetString("ProgramJobNumber"),
            programJobName: dr.GetString("ProgramName"),
            friendlyProjectTitle: dr.GetString("FriendlyProjectTitle").ValueIfNullOrEmpty(dr.GetString("ProjectName")),
            pulseSurveyDisabled: dr.GetBoolFromInt("PulseSurveyDisabled"),
            pulseSurveyTemplateId: dr.GetIntOrNull("PulseSurveyTemplateId"),
            sendIntakeSurveyToAllParticipants: dr.GetBoolFromInt("SendIntakeSurveyToAllParticipants"),
            brandingOrgGuid: dr.GetGuidOrNull("BrandingOrgGuid"),
            programStartDateUtc: dr.GetDateTimeOrNull("AbleProgramStartDateUtc"),
            programEndDateUtc: dr.GetDateTimeOrNull("AbleProgramEndDateUtc"),
            programNotes: dr.GetString("ProgramNotes"),
            userSubscription: Subscriptions.User.GetUserSubscriptionInfo(dr),
            userActivity: AbleUser.GetUserActivityInfo(dr)
          ));
        });

        return coacheeInfoList;
      }

      public static List<AlbertCoacheeInfo> GetCoacheesToWelcome(int lagWindowDays) {

        return GetCoacheeInfoList(null,
          null,
          $@"c.WelcomeEmailUtc IS NOT NULL
          AND c.WelcomeEmailUtc <= GETUTCDATE()
          AND DATEDIFF(DAY, c.WelcomeEmailUtc, GETUTCDATE()) BETWEEN 0 AND @WelcomeEmail_LagWindowDays
          AND c.WelcomeEmailSentUtc IS NULL
          AND (
            {AbleUser.UserActivityJoinAlias}.WorkshopsAllocated > 0
            OR c.SessionsAllocated > 0
            OR prj.SendIntakeSurveyToAllParticipants = 1
            OR (us.HasAICoaching = 1 AND us.SubscriptionEndUtc > GETUTCDATE() AND us.HasAICoachEnabled = 1)
          )",
          "j.JobNumber, c.WelcomeEmailUtc",
          NewSqlParameter("WelcomeEmail_LagWindowDays", lagWindowDays)
        );
      }

      public static List<AlbertCoacheeInfo> GetCoacheesForMeetCoachEmail() {

        return GetCoacheeInfoList(null,
          null,
          $"({AlbertCoacheeInfo.SQLCondition_AutoMeetCoachEmail("c")})",
          "j.JobNumber, c.MeetCoachEmailUtc, c.CoacheeId",
          NewSqlParameter("MeetCoachEmailIgnorePriorToUtc", MeetCoachEmail_IgnorePriorToUtc)
        );
      }

      public class CoacheesToSendPulseSurvey {
        public int UserId { get; private set; }
        public int CoacheeId { get; private set; }
        public string EmailAddress { get; private set; }
        public int? PulseSurveyId { get; private set; }
        public string PulseSurveyUID { get; private set; }
        public CoacheesToSendPulseSurvey(int userId, int coacheeId, string email, int? pulseSurveyId, string pulseSurveyUID) {
          UserId = userId;
          CoacheeId = coacheeId;
          EmailAddress = email;
          PulseSurveyId = pulseSurveyId;
          PulseSurveyUID = pulseSurveyUID;
        }
      }

      public static List<CoacheesToSendPulseSurvey> GetCoacheesToSendPulseSurvey() {

        var result = new List<CoacheesToSendPulseSurvey>();

        Query($@"
          WITH result
          AS
          (
            SELECT
              su.UserId, su.Email,
              usub.SubscriptionStartUtc,
              sp.CreatedUTC AS PulseCreatedUtc, sp.SurveyId AS PulseSurveyId, sp.sv_uniqueid AS PulseSurveyUID,
              ac.CoacheeId, ac.WelcomeEmailSentUtc, ac.ProgramJobId,
              sp.IntakeCloseDate,
              event.LatestEventUtc,
              ROW_NUMBER() OVER (PARTITION BY su.UserId ORDER BY sp.CreatedUTC DESC) AS PulseRank
            FROM sv_User su
            CROSS APPLY ( -- Must have subscription
              SELECT TOP 1 usub.SubscriptionStartUtc
              FROM al_UserSubscription usub
              INNER JOIN al_Subscription sub ON sub.SubscriptionId = usub.SubscriptionId
              WHERE usub.UserId = su.UserId
                AND GETUTCDATE() BETWEEN usub.SubscriptionStartUtc AND usub.SubscriptionEndUtc
                AND sub.HasPulse = 1
              ORDER BY usub.SubscriptionEndUtc DESC
            ) AS usub
            CROSS APPLY ( -- Latest coachee welcome date
              SELECT TOP 1 ac.CoacheeId, ac.WelcomeEmailSentUtc, ac.ProgramJobId
              FROM al_Coachees ac
              WHERE ac.UserId = su.UserId
                AND ac.ProgramStatusId = @CoacheeProgramStatus_Active
                AND ac.PulseSurveyEnabled = 1
                AND ac.DeletedUtc IS NULL
              ORDER BY ac.RowCreatedUtc DESC
            ) AS ac
            OUTER APPLY ( -- Recent pulse Surveys
              SELECT sp.CreatedUTC, sp.SurveyId, ic.IntakeCloseDate, sv.sv_uniqueid
              FROM sv_360_Participants sp
              INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
              INNER JOIN sv_360_Codes ic ON ic.CodeId = sp.IntakeCodeId
              WHERE sp.UserId = su.UserId
                AND {AlbertSurveys.SQLWhereConditions.Pulse360Surveys("sv")}
            ) AS sp
            CROSS APPLY ( -- Latest event to add pulse gap
              SELECT MAX(v) AS LatestEventUtc
              FROM (VALUES (usub.SubscriptionStartUtc), (ac.WelcomeEmailSentUtc), (sp.CreatedUtc)) AS value (v)
            ) AS event
            WHERE su.RegisteredUtc IS NOT NULL
          )
          SELECT result.UserId, result.CoacheeId, result.Email, result.PulseSurveyId, result.PulseSurveyUID
          FROM result
          INNER JOIN id_Job ij ON ij.JobId = result.ProgramJobId
          INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
          WHERE result.PulseRank = 1
            AND DATEDIFF(DAY, result.LatestEventUtc, GETUTCDATE()) > @PulseSurveyGapDays
            AND (result.IntakeCloseDate IS NULL OR result.IntakeCloseDate < GETUTCDATE()) -- No latest pulse or it has closed.
            AND ap.PulseSurveyDisabled = 0
          ORDER BY Email",
          dr => {
            result.Add(new CoacheesToSendPulseSurvey(
              dr.GetInt("UserId"),
              dr.GetInt("CoacheeId"),
              dr.GetString("Email"),
              dr.GetIntOrNull("PulseSurveyId"),
              dr.GetString("PulseSurveyUID")
            ));
          },
          NewSqlParameter("@CoacheeProgramStatus_Active", CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId),
          NewSqlParameter("@PulseSurveyGapDays", ConfigHelper.PulseSurveyGapDays)
        );

        return result;
      }

      public static void SetWelcomeSent(int coacheeId, DateTime welcomeEmailSentUtc) {
        GetNonQueryInt(null, $@"
          UPDATE al_Coachees SET
            WelcomeEmailSentUtc = @WelcomeEmailSentUtc,
            WelcomeEmailUtc = ISNULL(WelcomeEmailUtc, @WelcomeEmailSentUtc)
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("WelcomeEmailSentUtc", welcomeEmailSentUtc),
          NewSqlParameter("CoacheeId", coacheeId)
        );
      }

      public static void SetMeetCoachSent(SqlTransaction trans, int coacheeId, DateTime meetCoachEmailSentUtc) {
        GetNonQueryInt(trans, $@"
          UPDATE al_Coachees SET
            MeetCoachEmailSentUtc = @MeetCoachEmailSentUtc,
            MeetCoachEmailUtc = ISNULL(MeetCoachEmailUtc, @MeetCoachEmailSentUtc)
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("MeetCoachEmailSentUtc", meetCoachEmailSentUtc),
          NewSqlParameter("CoacheeId", coacheeId)
        );
      }

      public static void SetBookingReminderSent(int coacheeId, DateTime bookingReminderSentUtc) {
        GetNonQueryInt(null, $@"
          UPDATE al_Coachees SET
            BookingReminderLastSentUtc = @BookingReminderLastSentUtc
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("BookingReminderLastSentUtc", bookingReminderSentUtc),
          NewSqlParameter("CoacheeId", coacheeId)
        );
      }

      public static bool UndeleteCoachee(SqlTransaction trans, string coacheeEmail, int programJobId) {

        if (!TryGetSoftDeletedCoacheeId(trans, coacheeEmail, programJobId, out int softDeletedCoacheeId)) {
          return false;
        }

        int rows = GetNonQueryInt(trans, $@"
          UPDATE al_Coachees
          SET DeletedUtc = @DeletedUtc,
              LastUpdatedUtc = SYSUTCDATETIME()
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("CoacheeId", softDeletedCoacheeId),
          NewSqlParameter("DeletedUtc", (DateTime?)null)
        );
        return rows == 1;
      }

      public static bool SoftDeleteCoachee(SqlTransaction trans, int coacheeId) {

        int rows = GetNonQueryInt(trans, $@"
          UPDATE al_Coachees
          SET DeletedUtc = @DeletedUtc,
              LastUpdatedUtc = SYSUTCDATETIME()
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("CoacheeId", coacheeId),
          NewSqlParameter("DeletedUtc", DateTime.UtcNow)
        );
        return rows == 1;
      }

      public static bool TryGetSoftDeletedCoacheeId(SqlTransaction trans, string emailAddress, int programJobId, out int softDeletedCoacheeId) {

        softDeletedCoacheeId = GetScalarQueryIntOrNull(trans, @"
          SELECT CoacheeId
          FROM al_Coachees
          WHERE EmailAddress = @EmailAddress
            AND ProgramJobId = @ProgramJobId
            AND DeletedUtc IS NOT NULL",
          NewSqlParameter("EmailAddress", emailAddress),
          NewSqlParameter("ProgramJobId", programJobId)) ?? 0;

        return softDeletedCoacheeId > 0;
      }

      public static bool Has360WithRatersForCoachAllocation(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

        return GetScalarQueryInt(@"
          IF EXISTS (
            SELECT 1
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            INNER JOIN sv_360_Participants spr ON spr.Self_PartId = sp.PartId
            WHERE sp.AbleCoacheeId = @CoacheeId
              AND sv.SurveyTypeCode = @SurveyTypeCode_360
              AND spr.Completed IS NOT NULL
          )
          BEGIN
            SELECT 1
          END
          ELSE
          BEGIN
            SELECT 0
          END",
          NewSqlParameter("CoacheeId", coacheeInfo.CoacheeId),
          NewSqlParameter("@SurveyTypeCode_360", ConfigHelper.SurveyTypeCodes.Able360)) > 0;
      }

      public static bool HardDeleteCoachee(SqlTransaction trans, int coacheeId) {

        /*
        Note this can be used to find all FKs referencing a key.
          DECLARE @ReferencedTable VARCHAR(100) = 'al_Coachees';
          DECLARE @ReferencedColumn VARCHAR(100) = 'CoacheeId';
          SELECT CONCAT('DELETE FROM ', tab1.name, ' WHERE ', col1.name, ' = @KeyValue') AS DeleteSQL
          FROM sys.foreign_key_columns fkc
          INNER JOIN sys.objects obj ON obj.object_id = fkc.constraint_object_id
          INNER JOIN sys.tables tab1 ON tab1.object_id = fkc.parent_object_id
          INNER JOIN sys.columns col1 ON col1.column_id = parent_column_id AND col1.object_id = tab1.object_id
          INNER JOIN sys.tables tab2 ON tab2.object_id = fkc.referenced_object_id
          INNER JOIN sys.columns col2 ON col2.column_id = referenced_column_id AND col2.object_id = tab2.object_id
          WHERE tab2.name = @ReferencedTable
          AND col2.name = @ReferencedColumn;
        */

        GetNonQueryInt(trans, $@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          UPDATE ij
            SET ij.LastUpdatedUtc = SYSUTCDATETIME()
          FROM id_Job ij
          INNER JOIN al_Coachees ac ON ac.ProgramJobId = ij.JobId
          WHERE ac.CoacheeId = @CoacheeId;

          DELETE FROM al_CalendlyPayloads WHERE CoacheeId = @CoacheeId;
          DELETE FROM al_CoacheeAIComms WHERE CoacheeId = @CoacheeId;
          DELETE FROM al_CoacheeComms WHERE CoacheeId = @CoacheeId;
          DELETE FROM al_Component WHERE CoacheeId = @CoacheeId;
          DELETE FROM al_EmailHistory WHERE RecipientCoacheeId = @CoacheeId;
          DELETE FROM al_EmailHistory WHERE ReceivedFromCoacheeId = @CoacheeId;
          DELETE FROM al_WorkshopAttendance WHERE CoacheeId = @CoacheeId;
          DELETE FROM id_CoachingSession WHERE AbleCoacheeId = @CoacheeId;

          -- Keep survey data just remove ref to coachee.
          UPDATE sv_360_Participants SET AbleCoacheeId = NULL WHERE AbleCoacheeId = @CoacheeId;
          UPDATE sv_Survey SET ForAlbertCoacheeId = NULL WHERE ForAlbertCoacheeId = @CoacheeId;

          DELETE FROM al_Coachees WHERE CoacheeId = @CoacheeId;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          NewSqlParameter("CoacheeId", coacheeId)
        );

        return true; // Ok, deleted.
      }

      public class GetCoacheeListOptions {
        public int? TenantOrgId = null;         // Restrict to tenant OrgId. Only null for global admins.
        public int? CoachUserId = null;         // Not null: Find pax with this CoachUserId.
        public bool UserIsProgramManager = false; // True: CoachUserId is either a coach (as above) OR is PC/PLC of their program.
        public int? ProgramJobId = null;        // Not null: Only pax within a program.
        public bool? StatusEndProgram = false;  // True: Only end-program pax. null: Don't filter.
        public string SearchTerm = null;        // Search for string in various columns.
        public int? OffsetRows = null;          // Paging offset rows.
        public int? FetchRows = null;           // Paging number of rows.
        public List<string> PCForJobNumbers = null; // List of JobNumbers where user is Coordinator.
        public List<string> PLCForJobNumbers = null; // List of JobNumbers where user is Lead Consultant.
      }

      public static AbleCoacheeList GetCoacheeList(GetCoacheeListOptions options) {

        if (options == null) throw new NullReferenceException($"{nameof(options)} is null.");

        var coacheeList = new AbleCoacheeList();
        var sqlWhereList = new List<string>();
        var paramList = NewSqlParameterList();

        // Create WHERE clause from search options.
        // Note important to put brackets around any OR clauses. e.g. "(this OR that)"

        if (options.ProgramJobId != null) {
          sqlWhereList.Add("ac.ProgramJobId = @ProgramJobId");
          paramList.Add(NewSqlParameter("@ProgramJobId", options.ProgramJobId));
        }

        if (options.TenantOrgId != null) {
          sqlWhereList.Add($"prj.ParentOrgId = @TenantOrgId");
          paramList.Add(NewSqlParameter("@TenantOrgId", options.TenantOrgId));
        }

        if (options.CoachUserId != null) {

          string activityForCoachSql = $"{AbleUser.UserActivityJoinAlias}.CoachUserId = @CoachUserId";
          string includePCPLCSql = $"pj.JobNumber IN (SELECT value FROM STRING_SPLIT(@JobNumbersString, ','))";

          if (options.UserIsProgramManager) {
            // Coach or PLC/PC.
            sqlWhereList.Add("(" + activityForCoachSql + " OR " + includePCPLCSql + ")");
            paramList.Add(NewSqlParameter("@JobNumbersString", string.Join(",", options.PCForJobNumbers.ToArray()) + string.Join(",", options.PLCForJobNumbers.ToArray()).EnsureStartsWith(",")));
          } else {
            // Only coach.
            sqlWhereList.Add(activityForCoachSql);
          }
          paramList.Add(NewSqlParameter("@CoachUserId", options.CoachUserId));
        }

        if (options.StatusEndProgram != null) {
          sqlWhereList.Add($"ac.ProgramStatusId {(options.StatusEndProgram.Value ? "=" : "<>")} @StatusId_EndProgram");
          paramList.Add(NewSqlParameter("@StatusId_EndProgram", CoacheeProgramStatus.GetStatus_EndProgram().ProgramStatusId));
        }

        if (!options.SearchTerm.IsNullOrEmpty()) {
          sqlWhereList.Add(
            "(ac.FirstName + ' ' + ac.LastName LIKE '%' + @term + '%' OR ac.LastName LIKE '%' + @term + '%' OR ac.EmailAddress LIKE '%' + @term + '%' OR ac.ProgramJobId LIKE '%' + @term + '%'" +
            " OR pj.JobNumber LIKE '%' + @term + '%' OR pj.JobName LIKE '%' + @term + '%' OR cmp.CompanyName LIKE '%' + @term + '%' OR prj.ProjectName LIKE '%' + @term + '%')");
          paramList.Add(NewSqlParameter("@term", options.SearchTerm.LimitLengthTo(50), 50));
        }

        paramList.AddRange(AbleUser.GetUserActivityInfoParamsSQL());

        // SQL

        string sql = $@"
          SELECT
            COUNT(*) OVER() AS TotalRows,
            ac.CoacheeId, ac.CoacheeUID, ac.FirstName, ac.LastName, ac.EmailAddress,
            ac.MobilePhone, ac.UserId, ac.ProgramJobId, ac.CoachUserId,
            pj.JobNumber AS ProgramJobNumber, pj.JobName AS ProgramJobName,
            cmp.CompanyName,
            prj.ProjectName,
            org.OrgId, org.OrgOwnerUserId,
            {Subscriptions.User.GetSubscriptionOuterApplySelectionSQL},
            {AbleUser.GetUserActivityLeftJoinSelectionSQL}
          FROM al_Coachees ac
          INNER JOIN sv_SurveyCompany cmp ON ac.CompanyId = cmp.SvCompanyId
          INNER JOIN id_Job pj ON ac.ProgramJobId = pj.JobId
          INNER JOIN al_Project prj ON prj.JobNumber = pj.JobNumber
          INNER JOIN sv_Organisation org ON org.OrgId = prj.ParentOrgId
          {AbleUser.GetUserActivityInfoLeftJoinSQL("ac", "ac")}
          {Subscriptions.User.GetUserSubscriptionOuterApplySQL("ac")}
          WHERE ac.DeletedUtc IS NULL
            {(sqlWhereList.IsNullOrEmpty() ? "" : ("AND " + sqlWhereList.Join(" AND ")))}
          ORDER BY ac.FirstName, ac.LastName";

        if (options.FetchRows > 0) {
          options.OffsetRows = Math.Max(options.OffsetRows.GetValueOrDefault(0), 0);
          coacheeList.OffsetRows = options.OffsetRows.GetValueOrDefault(0);
          coacheeList.FetchRows = options.FetchRows;
          sql += $" OFFSET {options.OffsetRows} ROWS FETCH NEXT {options.FetchRows} ROWS ONLY";
        }

        if (ConfigHelper.IsDevServer) coacheeList.SqlText = sql;

        Query(sql, paramList, dr => {

          coacheeList.TotalRows = dr.GetInt("TotalRows");

          var coacheeResultList = new CoacheeListItem(
            coacheeId: dr.GetInt("CoacheeId"),
            coacheeGuid: dr.GetGuid("CoacheeUID"),
            userId: dr.GetInt("UserId"),
            firstName: dr.GetString("FirstName"),
            lastName: dr.GetString("LastName"),
            fullName: dr.GetString("FirstName") + " " + dr.GetString("LastName"),
            emailAddress: dr.GetString("EmailAddress"),
            mobilePhone: dr.GetString("MobilePhone"),
            coachUserId: dr.GetIntOrNull("CoachUserId"),
            companyName: dr.GetString("CompanyName"),
            programJobId: dr.GetIntOrNull("ProgramJobId"),
            programJobNumber: dr.GetString("ProgramJobNumber"),
            programJobName: dr.GetString("ProgramJobName"),
            projectName: dr.GetString("ProjectName"),
            tenantOrgId: dr.GetInt("OrgId"),
            tenantOwnerUserId: dr.GetInt("OrgOwnerUserId"),
            userSubscription: Subscriptions.User.GetUserSubscriptionInfo(dr),
            userActivity: AbleUser.GetUserActivityInfo(dr)
          );
          coacheeList.CoacheeInfoList.Add(coacheeResultList);
        });

        return coacheeList;
      }

      public static void UpdateSessionStatsAndTargetDates(SqlTransaction trans, AlbertCoacheeInfo ci, CoachingSessions.SessionStats sessionStats) {

        if (sessionStats.EarliestApptDateUTC == null) sessionStats.Send360ReportUtc = null;
        else sessionStats.Send360ReportUtc = sessionStats.EarliestApptDateUTC.Value.AddDays(-ConfigHelper.DaysBetweenEvents.Send360Report_To_FirstSessionDate);

        ci.Send360ReportUtc = sessionStats.Send360ReportUtc;
        ci.UserActivity.SessionsBooked = sessionStats.SessionsBooked;
        ci.UserActivity.SessionsUpcoming = sessionStats.SessionsUpcoming;
        ci.UserActivity.SessionsCompleted = sessionStats.SessionsBooked - sessionStats.SessionsUpcoming;

        // NextBookingTargetDateUtc is included in the booking reminder email as the suggested date for coachee to book their next session.
        // NextBookingSendReminderEmailUtc is the earliest date to send the reminder.
        // Note that Intercom is set up only to send the reminder if there are no more *upcoming* sessions.
        ci.NextBookingTargetDateUtc = null;
        ci.NextBookingSendReminderEmailUtc = null;
        if (sessionStats.LatestApptDateUTC != null) {
          ci.NextBookingTargetDateUtc = sessionStats.LatestApptDateUTC.AddLocalTimeToUtc(
            ConfigHelper.DaysBetweenEvents.LatestBooking_to_NextBookingTargetDate, 0, 0, ConfigHelper.DefaultTimeZoneInfo, true);
        } else if (ci.MeetCoachEmailUtc != null) {
          ci.NextBookingTargetDateUtc = ci.MeetCoachEmailUtc.AddLocalTimeToUtc(
            ConfigHelper.DaysBetweenEvents.MeetCoachEmail_to_FirstBookingTargetDate, 0, 0, ConfigHelper.DefaultTimeZoneInfo);
        }
        if (ci.NextBookingTargetDateUtc != null) {
          // Set date for the booking reminder. Note this must be *negative* (i.e. the reminder date is *prior* to the booking date)
          // So AddDays() must be passed a -ve number of days.
          ci.NextBookingSendReminderEmailUtc = ((DateTime)ci.NextBookingTargetDateUtc).AddDays(-ConfigHelper.DaysBetweenEvents.BookingReminder_To_BookingTargetDate);
        }
        // Copy dates back to session stats object.
        sessionStats.NextBookingTargetDateUtc = ci.NextBookingTargetDateUtc;
        sessionStats.NextBookingSendReminderEmailUtc = ci.NextBookingSendReminderEmailUtc;

        GetNonQueryInt(trans, @"
          UPDATE al_Coachees SET
            LastUpdatedUtc = SYSUTCDATETIME(),
            SessionsBooked = @SessionsBooked,
            SessionsCompleted = @SessionsCompleted,
            SessionsUpcoming = @SessionsUpcoming,
            NextBookingTargetDateUtc = @NextBookingTargetDateUtc,
            NextBookingSendReminderEmailUtc = @NextBookingSendReminderEmailUtc,
            Send360ReportUtc = @Send360ReportUtc
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("CoacheeId", ci.CoacheeId),
          NewSqlParameter("SessionsBooked", ci.UserActivity.SessionsBooked),
          NewSqlParameter("SessionsCompleted", ci.UserActivity.SessionsCompleted),
          NewSqlParameter("SessionsUpcoming", ci.UserActivity.SessionsUpcoming),
          NewSqlParameter("NextBookingTargetDateUtc", ci.NextBookingTargetDateUtc),
          NewSqlParameter("NextBookingSendReminderEmailUtc", ci.NextBookingSendReminderEmailUtc),
          NewSqlParameter("Send360ReportUtc", ci.Send360ReportUtc)
        );
      }

      public static List<AlbertCoacheeInfo> GetCoacheesInProject(string projectJobNumber) {

        return GetCoacheeInfoList(null,
          null,
          "prj.JobNumber = @ProjectJobNumber",
          "c.FirstName, c.LastName",
          NewSqlParameter("ProjectJobNumber", projectJobNumber)
        );
      }

      public static List<AlbertCoacheeInfo> GetCoacheesByProgram(int programJobId) {

        return GetCoacheeInfoList(null,
          null,
          "c.ProgramJobId = @ProgramJobId",
          "c.RowCreatedUtc",
          NewSqlParameter("@ProgramJobId", programJobId)
        );
      }

      public static List<AlbertCoacheeInfo> GetCoacheesWithoutUserId() {

        var coacheeList = new List<AlbertCoacheeInfo>();

        Query(@"
          WITH CoacheesWithoutUserId AS (
            SELECT
              LOWER(c.EmailAddress) AS EmailAddress,
              c.FirstName,
              c.LastName,
              c.MobilePhone,
              sc.OrgId,
              ROW_NUMBER() OVER (PARTITION BY LOWER(c.EmailAddress) ORDER BY c.UserId) AS RowNum
            FROM al_Coachees c
            INNER JOIN id_Job j ON j.JobId = c.ProgramJobId
            INNER JOIN sv_SurveyCompany sc ON sc.SvCompanyId = j.CompanyId
            WHERE c.UserId IS NULL AND c.DeletedUtc IS NULL
          )
          SELECT EmailAddress, FirstName, LastName, MobilePhone, OrgId
          FROM CoacheesWithoutUserId
          WHERE RowNum = 1",
          dr => {
            coacheeList.Add(
              new AlbertCoacheeInfo(
               dr.GetString("EmailAddress"),
               dr.GetString("FirstName"),
               dr.GetString("LastName"),
               dr.GetString("MobilePhone"),
               dr.GetInt("OrgId")
              ));
          },
          null
        );

        return coacheeList;
      }

      public static List<CoacheesForProgramEmail> GetCoacheesForProgramEmail(int programJobId, bool excludeEndProgram = false) {
        int statusId_EndProgram = CoacheeProgramStatus.GetStatus_EndProgram().ProgramStatusId;
        string sqlWhere = $"c.ProgramJobId = @ProgramJobId AND c.ProgramStatusId > 0";
        if (excludeEndProgram) sqlWhere += $" AND c.ProgramStatusId <> {statusId_EndProgram}";
        return GetCoacheesForProgramEmail(
          "",
          sqlWhere,
          "c.RowCreatedUtc",
          new List<SqlParameter>() { new SqlParameter("@ProgramJobId", SqlDbType.Int) { Value = programJobId } }
        );
      }

      private static List<CoacheesForProgramEmail> GetCoacheesForProgramEmail(
        string extraJoins,
        string sqlWhereConditions,
        string sqlOrderByColumns,
        List<SqlParameter> sqlWhereParams) {

        var coacheeInfoList = new List<CoacheesForProgramEmail>();

        string sql = $@"
          SELECT
            c.CoacheeId, c.UserId, c.CoachUserId, c.ProgramJobId, c.FirstName, c.LastName,
            c.EmailAddress, c.MobilePhone, c.CoacheeUID, c.ProgramStatusId,
            c.SessionsAllocated, c.SessionsBooked, c.SessionsCompleted, c.CoachingTypeId,
            DATEDIFF(DAY, md.MaxDate, GETUTCDATE()) AS DaysSinceBooking,
            j.JobNumber AS ProgramJobNumber, j.JobName AS ProgramName,
            CASE WHEN sp.CoacheeId IS NOT NULL THEN 1 ELSE 0 END AS HasUncompletedSelfSurvey
          FROM al_Coachees c
          LEFT OUTER JOIN id_Job j ON j.JobId = c.ProgramJobId
          LEFT JOIN (
            SELECT DISTINCT sp.AbleCoacheeId AS CoacheeId
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv
                ON sv.sv_id = sp.SurveyId
            WHERE sp.IsSelf = '1' AND sp.Completed IS NULL AND sv.sv_closedateself IS NULL AND sv.sv_FeedbackOption = 0
          ) sp ON sp.CoacheeId = c.CoacheeId

          CROSS APPLY (
            SELECT
              MAX(cs.ApptDateUTC) AS LatestSessionApptDateUtc
            FROM id_CoachingSession cs
            WHERE cs.AbleCoacheeId = c.CoacheeId
              AND cs.ApptCancelledUTC IS NULL
          ) AS cs
          CROSS APPLY (
            -- Get most recent date as a reference for NotBookedForDays, falling back to RowCreated if necessary.
            SELECT MAX(vals.val) AS MaxDate
            FROM (VALUES (cs.LatestSessionApptDateUtc), (c.MeetCoachEmailSentUtc), (c.WelcomeEmailSentUtc), (c.RowCreatedUtc)) AS vals (val)
          ) AS md
          {extraJoins.EmptyIfNull()}
          WHERE c.DeletedUtc IS NULL
          {sqlWhereConditions.SurroundWith("AND (", ")", false).EmptyIfNull()}
          {sqlOrderByColumns.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        var sqlParams = new List<SqlParameter>(AbleUser.GetUserActivityInfoParamsSQL());

        foreach (var whereParam in sqlWhereParams) {
          var sqlParam = sqlParams.Find(p => p.ParameterName.Equals(whereParam.ParameterName, StringComparison.OrdinalIgnoreCase));
          if (sqlParam != null) sqlParam.Value = whereParam.Value;
          else sqlParams.Add(whereParam);
        }

        Query(sql, sqlParams, dr => {
          coacheeInfoList.Add(new CoacheesForProgramEmail(
            dr.GetInt("CoacheeId"),
            dr.GetIntOrNull("UserId"),
            dr.GetInt("CoachUserId"),
            dr.GetString("FirstName"),
            dr.GetString("LastName"),
            dr.GetString("EmailAddress"),
            dr.GetString("MobilePhone"),
            dr.GetGuid("CoacheeUID"),
            dr.GetIntOrDefault("SessionsAllocated", 0),
            dr.GetIntOrDefault("SessionsBooked", 0),
            dr.GetIntOrDefault("SessionsCompleted", 0),
            dr.GetIntOrNull("CoachingTypeId"),
            dr.GetInt("DaysSinceBooking"),
            dr.GetString("ProgramJobNumber"),
            dr.GetString("ProgramName"),
            dr.GetInt("ProgramStatusId"),
            dr.GetBoolFromInt("HasUncompletedSelfSurvey")
          ));
        });

        return coacheeInfoList;
      }

      public class CoacheesInProgram {
        public int CoacheeId { get; internal set; }
        public string EmailAddress { get; internal set; }
      }

      public static List<CoacheesInProgram> GetCoacheesInProgram(int programJobId) {

        var list = new List<CoacheesInProgram>();

        Query(@"
          SELECT CoacheeId, EmailAddress
          FROM al_Coachees ac
          WHERE ac.ProgramJobId = @ProgramJobId
           AND ac.DeletedUtc IS NULL",
          dr => {
            list.Add(new CoacheesInProgram() {
              CoacheeId = dr.GetInt("CoacheeId"),
              EmailAddress = dr.GetString("EmailAddress")
            });
          },
          NewSqlParameter("@ProgramJobId", programJobId)
        );

        return list;
      }

      public static List<AlbertCoacheeInfo> GetCoacheesForProgramOverview(int programJobId) {

        return GetCoacheeInfoList(null,
          null,
          $"c.ProgramJobId = @ProgramJobId AND ((c.CoachUserId > 0 AND c.CoachUserId <> {ConfigHelper.UserId.Unassigned}) OR c.CoachingTypeId > {AlbertCoachingTypes.GetType_NoCoaching().CoachingTypeId})",
          "c.EmailAddress",
          NewSqlParameter("ProgramJobId", programJobId)
        );
      }

      public static void UpdateBookingTargetDates(int coacheeId, DateTime? nextBookingTargetDateUtc, DateTime? nextBookingSendReminderEmailUtc) {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          var sql = @"

            UPDATE al_Coachees SET
              NextBookingTargetDateUtc = @NextBookingTargetDateUtc,
              NextBookingSendReminderEmailUtc = @NextBookingSendReminderEmailUtc
            WHERE CoacheeId = @CoacheeId

            ";
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@CoacheeId", coacheeId);
            cmd.Parameters.AddDateTime("@NextBookingTargetDateUtc", nextBookingTargetDateUtc);
            cmd.Parameters.AddDateTime("@NextBookingSendReminderEmailUtc", nextBookingSendReminderEmailUtc);
            conn.Open();
            cmd.ExecuteNonQuery();
          }
        }
      }

      // Get rater info needed for emails.
      public static RaterEmailInfo GetRaterEmailInfo(int surveyId, int raterPartId) {

        // Note the LEFT JOIN for sv_SurveyCompany - no assigned company means it's an independent/private coachee.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

            SELECT pr.PartId AS RaterPartId, pr.UniqueId AS RaterUID,
              pr.FirstName AS RaterFirstName, pr.LastName AS RaterLastName, pr.Email AS RaterEmail,
              ac.FirstName AS CoacheeFirstName, ac.LastName AS CoacheeLastName, ac.EmailAddress AS CoacheeEmail,
              u.FirstName AS CoachFirstName, u.LastName AS CoachLastName, u.Email AS CoachEmail,
              cmp.CompanyName
            FROM sv_360_Participants pr                                             -- rater
            INNER JOIN sv_360_Participants ps ON ps.PartId = pr.Self_PartId         -- self
            INNER JOIN al_Coachees ac ON ac.CoacheeId = ps.AbleCoacheeId            -- coachee for self
            INNER JOIN sv_User u ON ac.CoachUserId = u.UserId                       -- coach for coachee
            LEFT OUTER JOIN sv_SurveyCompany cmp ON ac.CompanyId = cmp.SvCompanyId  -- coachee's company if any
            WHERE pr.SurveyId = @SurveyId
            AND pr.PartId = @RaterPartId
            AND pr.IsSelf = 0

            ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", surveyId);
            cmd.Parameters.AddInt("@RaterPartId", raterPartId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              if (dr.Read()) {
                return new RaterEmailInfo(
                  dr.GetInt("RaterPartId"),
                  dr.GetString("RaterUID"),
                  dr.GetString("RaterFirstName"),
                  dr.GetString("RaterLastName"),
                  dr.GetString("RaterEmail"),
                  dr.GetString("CoacheeFirstName"),
                  dr.GetString("CoacheeLastName"),
                  dr.GetString("CoacheeEmail"),
                  dr.GetString("CoachFirstName"),
                  dr.GetString("CoachLastName"),
                  dr.GetString("CoachEmail"),
                  dr.GetString("CompanyName")
                );
              }
            }
          }
        }
        return null;
      }

      // TODO: 1. CreateCoachee should accept s proper "new coachee" dto with required members.
      //       2. Look for soft-deleted coachee first, hard-delete it if found.
      public static int CreateCoachee(SqlTransaction trans, AlbertCoacheeInfo coacheeInfo) {

        // Check if email is already exists in sv_User.
        var existingUserInfo = AbleUser.GetUserByEmailOrNull(trans, coacheeInfo.EmailAddress, AbleUser.RegisteredFilter.Any);

        if (existingUserInfo == null) {

          // No user found, assign a new one with the coachee's email address.
          coacheeInfo.UserId = AbleUser.CreateUserFromCoachee(trans, coacheeInfo);

        } else {

          // User found, assign it to the coachee.
          coacheeInfo.UserId = existingUserInfo?.UserId;

          // Ensure user is flagged as a participant.
          if (!existingUserInfo.IsParticipant) {
            AbleUser.UpdateIsParticipant(trans, existingUserInfo, true);
          }

          // Ensure user is not deleted.
          if (existingUserInfo.IsSoftDeleted) {
            AbleUser.UpdateDeletedUtc(trans, existingUserInfo, null);
          }
        }

        // Create Coachee linking to UserId.
        return GetScalarQueryInt(trans, @"
          INSERT INTO al_Coachees (CoachUserId, FirstName, LastName, EmailAddress, ProgramJobId, IntercomUserId, CompanyId, MobilePhone, UserId, ProgramStatusId, SessionsAllocated, CoachingTypeId)
          OUTPUT INSERTED.CoacheeId
          VALUES (@CoachUserId, @FirstName, @LastName, @EmailAddress, @ProgramJobId, @IntercomUserId, @CompanyId, @MobilePhone, @UserId, @ProgramStatusId, @SessionsAllocated, @CoachingTypeId)",
          NewSqlParameter("@CompanyId", coacheeInfo.CompanyId),
          NewSqlParameter("@FirstName", coacheeInfo.FirstName, 100),
          NewSqlParameter("@LastName", coacheeInfo.LastName, 100),
          NewSqlParameter("@EmailAddress", coacheeInfo.EmailAddress, 100),
          NewSqlParameter("@ProgramJobId", coacheeInfo.ProgramJobId),
          NewSqlParameter("@MobilePhone", coacheeInfo.MobilePhone, 50),
          NewSqlParameter("@CoachUserId", coacheeInfo.CoachUserId),
          NewSqlParameter("@IntercomUserId", coacheeInfo.IntercomUserId, 100),
          NewSqlParameter("@UserId", coacheeInfo.UserId),
          NewSqlParameter("@ProgramStatusId", coacheeInfo.ProgramStatusId),
          NewSqlParameter("@SessionsAllocated", coacheeInfo.UserActivity.SessionsAllocated),
          NewSqlParameter("@CoachingTypeId", coacheeInfo.CoachingTypeId)
        );
      }

      public static bool UpdateCoachee(AlbertCoacheeInfo coacheeInfo, bool changeUserIdIfEmailChanged = false) {
        return UpdateCoachee(null, coacheeInfo, changeUserIdIfEmailChanged);
      }

      public static bool UpdateCoachee(
        SqlTransaction trans,
        AlbertCoacheeInfo coacheeInfo,
        bool changeUserIdIfEmailChanged = false) {

        if (coacheeInfo.UserId != null) {

          AbleUser.UserForCoacheeComparison userForCoacheeComparison = null;
          bool changeUserId = false;
          bool isUpdatingEmail = false;

          if (!coacheeInfo.CurrentEmailAddress.IsNullOrEmpty() && coacheeInfo.EmailAddress != coacheeInfo.CurrentEmailAddress) {

            isUpdatingEmail = true;

            userForCoacheeComparison = AbleUser.GetUserForCoacheeComparison(coacheeInfo.EmailAddress, AbleUser.RegisteredFilter.Any);

            if (userForCoacheeComparison != null) {
              if (changeUserIdIfEmailChanged) {
                changeUserId = true;
              } else {
                throw new ArgumentException("Email is invalid.");
              }
            }
          }

          if (userForCoacheeComparison == null) {
            userForCoacheeComparison = AbleUser.GetUserForCoacheeComparison(coacheeInfo.UserId.Value, AbleUser.RegisteredFilter.Any);
          }

          // Update sv_User for this UserId.
          if (changeUserId) {
            // Coachee is being reasigned to existing user
            coacheeInfo.UserId = userForCoacheeComparison.UserId; // Switch userId to user's email owner.
          }

          bool userUpdated = AbleUser.UpdateUserFromCoachee(trans, userForCoacheeComparison, coacheeInfo);
          if (!userUpdated) throw new ApplicationException("Error updating profile, please try again.");

          if (userUpdated && changeUserId) {
            AbleUser.UpdateIsParticipant(trans, userForCoacheeComparison.UserId, true); // Set user to Participant
          } else if (userUpdated && isUpdatingEmail) {
            // Update active/scheduled surveys
            AlbertSurveys.UpdateParticipantEmailFromCoachee(trans, coacheeInfo);
          }

        } else {

          // Check if email is already registered in sv_User.
          coacheeInfo.UserId = AbleUser.GetUserByEmailOrNull(coacheeInfo.EmailAddress, AbleUser.RegisteredFilter.Any)?.UserId;

          // If it's not registered, create a new user.
          if (coacheeInfo.UserId == null) {
            coacheeInfo.UserId = AbleUser.CreateUserFromCoachee(trans, coacheeInfo);
          }
        }

        return GetNonQueryInt(trans, @"

          UPDATE al_Coachees SET
            LastUpdatedUtc = SYSUTCDATETIME(),
            CompanyId = @CompanyId,
            ProgramJobId = @ProgramJobId,
            FirstName = @FirstName,
            LastName = @LastName,
            EmailAddress = @EmailAddress,
            MobilePhone = @MobilePhone,
            SessionsAllocated = @SessionsAllocated,
            SessionsBooked = @SessionsBooked,
            SessionsCompleted = @SessionsCompleted,
            CoachingTypeId = @CoachingTypeId,
            ProgramStatusId = @ProgramStatusId,
            DisableNudges = @DisableNudges,
            PulseSurveyEnabled = @PulseSurveyEnabled,
            SubscriptionUser = @SubscriptionUser,
            WelcomeEmailUtc = @WelcomeEmailUtc,
            WelcomeEmailSentUtc = @WelcomeEmailSentUtc,
            MeetCoachEmailUtc = @MeetCoachEmailUtc,
            Send360ReportUtc = @Send360ReportUtc,
            CoachUserId = @CoachUserId,
            UserId = @UserId
          WHERE CoacheeId = @CoacheeId",

          NewSqlParameter("CoacheeId", coacheeInfo.CoacheeId),
          NewSqlParameter("CoachUserId", coacheeInfo?.CoachUserId),
          NewSqlParameter("CompanyId", coacheeInfo.CompanyId),
          NewSqlParameter("ProgramJobId", coacheeInfo.ProgramJobId),
          NewSqlParameter("FirstName", coacheeInfo.FirstName, 100),
          NewSqlParameter("LastName", coacheeInfo.LastName, 100),
          NewSqlParameter("EmailAddress", coacheeInfo.EmailAddress, 100),
          NewSqlParameter("MobilePhone", coacheeInfo.MobilePhone, 50),
          NewSqlParameter("SessionsAllocated", coacheeInfo.UserActivity?.SessionsAllocated),
          NewSqlParameter("SessionsBooked", coacheeInfo.UserActivity?.SessionsBooked),
          NewSqlParameter("SessionsCompleted", coacheeInfo.UserActivity?.SessionsCompleted),
          NewSqlParameter("CoachingTypeId", coacheeInfo.CoachingTypeId),
          NewSqlParameter("ProgramStatusId", coacheeInfo.ProgramStatusId),
          NewSqlParameter("DisableNudges", coacheeInfo.DisableNudges),
          NewSqlParameter("PulseSurveyEnabled", coacheeInfo.PulseSurveyEnabled),
          NewSqlParameter("SubscriptionUser", coacheeInfo.SubscriptionUser),
          NewSqlParameter("WelcomeEmailUtc", coacheeInfo.WelcomeEmailUtc),
          NewSqlParameter("WelcomeEmailSentUtc", coacheeInfo.WelcomeEmailSentUtc),
          NewSqlParameter("MeetCoachEmailUtc", coacheeInfo.MeetCoachEmailUtc),
          NewSqlParameter("Send360ReportUtc", coacheeInfo.Send360ReportUtc),
          NewSqlParameter("UserId", coacheeInfo.UserId)
        ) == 1;
      }

      public static bool UpdateCoacheeNotes(SqlTransaction trans, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

        return GetNonQueryInt(trans, @"
          UPDATE al_Coachees SET
            CoacheeNotes = @CoacheeNotes,
            PrivateCoachNote = @PrivateCoachNote
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("CoacheeId", coacheeInfo.CoacheeId),
          NewSqlParameter("CoacheeNotes", coacheeInfo.CoacheeNotes.EmptyIfNull()),
          NewSqlParameter("PrivateCoachNote", coacheeInfo.PrivateCoachNote.EmptyIfNull())
        ) == 1;
      }

      public static bool UpdateCoacheeUserId(SqlTransaction trans, AlbertCoacheeInfo coacheeInfo) {

        return GetNonQueryInt(trans, @"
          UPDATE al_Coachees
          SET UserId = @UserId,
              LastUpdatedUtc = SYSUTCDATETIME()
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("CoacheeId", coacheeInfo.CoacheeId),
          NewSqlParameter("UserId", coacheeInfo.UserId)
        ) == 1;
      }

      public static bool UpdateCoacheeUserId(string coacheeEmailAddress, int? userId) {

        if (userId == null || userId == 0) return false;

        return GetNonQueryInt(null, @"
          UPDATE al_Coachees
          SET UserId = @UserId,
              LastUpdatedUtc = SYSUTCDATETIME()
          WHERE EmailAddress = @EmailAddress",
          NewSqlParameter("EmailAddress", coacheeEmailAddress),
          NewSqlParameter("UserId", userId)
        ) == 1;
      }

      public static bool UpdateCoachee_CoachUserId(SqlTransaction trans, int coacheeId, int coachUserId) {

        return GetNonQueryInt(
          trans, @"
          UPDATE al_Coachees
          SET ac.CoachUserId = @CoachUserId,
              LastUpdatedUtc = SYSUTCDATETIME()
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("CoacheeId", coacheeId),
          NewSqlParameter("CoachUserId", coachUserId)
        ) == 1;
      }

      public static List<CoacheeEventinfo> GetCoacheeEventList(int coacheeId) {

        var eventList = new List<CoacheeEventinfo>();

        string sql = @"
          SELECT p.CreatedUTC AS EventDate,
            p.PartId AS SurveyPartId, p.SurveyId AS SurveyId, s.sv_title AS SurveyTitle, p.Completed AS PartCompleted,
            pr.RatersInvited, pr.RatersCompleted,
            NULL AS CoachingSessionId, NULL AS DurationMins
          FROM sv_360_Participants p
          INNER JOIN sv_Survey s ON p.SurveyId = s.sv_id
          OUTER APPLY (
            SELECT pr.Self_PartId, COUNT(pr.PartId) AS RatersInvited, COUNT(pr.completed) AS RatersCompleted
            FROM sv_360_Participants pr
            WHERE pr.Self_PartId = p.PartId
            GROUP BY pr.Self_PartId) AS pr
          WHERE p.AbleCoacheeId = @CoacheeId
          UNION
          SELECT cs.ApptDateUTC AS EventDate,
            NULL AS SurveyPartId, NULL AS SurveyId, NULL AS SurveyTitle, NULL AS PartCompleted,
            NULL AS RatersInvited, NULL AS RatersCompleted,
            cs.CoachingSessionId, cs.DurationMins
          FROM id_CoachingSession cs
          WHERE cs.AbleCoacheeId = @CoacheeId
          ORDER BY EventDate DESC";

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@CoacheeId", coacheeId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                eventList.Add(new CoacheeEventinfo() {
                  EventDateUtc = dr.GetDateTime("EventDate"),
                  CoachingSessionId = dr.GetIntOrNull("CoachingSessionId"),
                  SessionDurationMins = dr.GetIntOrNull("DurationMins"),
                  SurveyId = dr.GetIntOrNull("SurveyId"),
                  SurveyTitle = dr.GetString("SurveyTitle"),
                  SurveyPartId = dr.GetIntOrNull("SurveyPartId"),
                  PartCompleted = dr.GetDateTimeOrNull("PartCompleted"),
                  RatersInvited = dr.GetIntOrNull("RatersInvited"),
                  RatersCompleted = dr.GetIntOrNull("RatersCompleted"),
                });
              }
            }
          }
        }
        return eventList;
      }

      public static bool UpdateCoacheeStatus(SqlTransaction trans, AlbertCoacheeInfo coacheeInfo, CoacheeProgramStatus.ProgramStatusInfo programStatus) {

        int rowsAffected = GetNonQueryInt(trans, @"
          UPDATE al_Coachees
          SET ProgramStatusId = @ProgramStatusId,
              LastUpdatedUtc = SYSUTCDATETIME()
          WHERE CoacheeId = @CoacheeId",
          NewSqlParameter("CoacheeId", coacheeInfo.CoacheeId),
          NewSqlParameter("ProgramStatusId", programStatus.ProgramStatusId)
        );

        if (rowsAffected > 0) {
          coacheeInfo.ProgramStatusId = programStatus.ProgramStatusId; // If db updated, update DTO.
          return true;
        } else {
          return false;
        }
      }

      public static void UpdateStatusWaitingToOnboarding(AlbertCoacheeInfo coacheeInfo) {

        // Set participant to On-Boarding if status is currently waiting.

        if (coacheeInfo.ProgramStatusId == CoacheeProgramStatus.GetStatus_WaitingLaunch().ProgramStatusId) {
          UpdateCoacheeStatus(null, coacheeInfo, CoacheeProgramStatus.GetStatus_Onboarding());
        }
      }

      public static List<Actions_MissingCoach> GetActions_MissingCoach(int partnerId) {

        var list = new List<Actions_MissingCoach>();

        Query(@"

          SELECT
            ac.CoacheeId, ac.FirstName + ' ' + ac.LastName as FullName,
            j.JobNumber, j.JobName as ProgramName,
            cmp.CompanyName,
            prj.ProjectName
          FROM al_Coachees ac
          INNER JOIN id_Job j ON j.JobId = ac.ProgramJobId
          INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          INNER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = prj.SvCompanyId
          WHERE ac.ProgramStatusId = @CoacheeStatus_Onboarding
            AND ac.CoachUserId = @CoachId_Unassigned
            AND ac.CoachingTypeId <> @CoachingType_NoCoaching
            AND ac.DeletedUtc IS NULL
            AND (j.LeadConsultantUserId = @PartnerId OR j.ProjectCoordinatorUserId = @PartnerId)
            AND (j.ProgramStatusId = @ProgramStatus_Setup OR j.ProgramStatusId = @ProgramStatus_Active)",

          dr => {
            list.Add(new Actions_MissingCoach(
              dr.GetInt("CoacheeId"),
              dr.GetString("FullName"),
              dr.GetString("CompanyName"),
              dr.GetString("JobNumber"),
              dr.GetString("ProgramName"),
              dr.GetString("ProjectName")
            ));
          },

          NewSqlParameter("@PartnerId", partnerId),
          NewSqlParameter("@CoacheeStatus_Onboarding", CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId),
          NewSqlParameter("@CoachId_Unassigned", ConfigHelper.UserId.Unassigned),
          NewSqlParameter("@ProgramStatus_Setup", AlbertProgramStatus.Ids.Setup),
          NewSqlParameter("@ProgramStatus_Active", AlbertProgramStatus.Ids.Active),
          NewSqlParameter("@CoachingType_NoCoaching", AlbertCoachingTypes.ReservedType_NoCoaching.CoachingTypeId)
        );

        return list;
      }

      public static List<Actions_StuckCoachees> GetActions_StuckCoachees(int partnerId) {

        var list = new List<Actions_StuckCoachees>();

        Query(@"

          SELECT
            ac.CoacheeId, ac.FirstName + ' ' + ac.LastName AS FullName,
            DATEDIFF(DAY, md.MaxDate, GETUTCDATE()) AS DaysSinceBooking
          FROM al_Coachees ac
          CROSS APPLY (
            -- Get date of latest actual booking (will be null if no bookings exist).
            SELECT MAX(cs.ApptDateUTC) AS LatestSessionApptDateUtc
              FROM id_CoachingSession cs
              WHERE cs.AbleCoacheeId = ac.CoacheeId
            ) AS cs
          CROSS APPLY (
            -- Get most recent date as a reference for NotBookedForDays, falling back to RowCreated if necessary.
            -- Note this is a cool SQL trick to get the max of a list of values!
            SELECT MAX(vals.val) AS MaxDate
              FROM (VALUES (cs.LatestSessionApptDateUtc), (ac.MeetCoachEmailSentUtc), (ac.WelcomeEmailSentUtc), (ac.RowCreatedUtc)) AS vals (val)
            ) AS md
          WHERE ac.CoachUserId = @CoachUserId
            AND ac.ProgramStatusId = @ProgramStatus_Active  -- Active status
            AND (cs.LatestSessionApptDateUtc IS NULL OR cs.LatestSessionApptDateUtc < GETUTCDATE()) -- No sessions or none booked in future
            AND ac.NextBookingTargetDateUtc < GETUTCDATE()  -- Next booking date is also in the past
            AND ac.DeletedUtc IS NULL AND ac.SessionsAllocated > ac.SessionsBooked",

          dr => {
            list.Add(new Actions_StuckCoachees(
            dr.GetInt("CoacheeId"),
            dr.GetString("FullName"),
            dr.GetInt("DaysSinceBooking")
            ));
          },

          NewSqlParameter("@CoachUserId", partnerId),
          NewSqlParameter("@ProgramStatus_Active", CoacheeProgramStatus.Ids.Active)
        );

        return list;
      }

      public static bool UserHasAnyCoachees(AbleUser.AbleUserBasicInfo loggedInUser, ConfigHelper.UserRole userRole) {

        if (loggedInUser == null) {
          throw new Exception("userInfo required");
        }

        if (userRole == ConfigHelper.UserRole.Admin) return true;

        return GetScalarQueryInt($@"
          SELECT IIF(EXISTS(
            SELECT 1
            FROM al_Coachees ac
            WHERE ac.CoachUserId = @CoachUserId
          ), 1, 0)",
          NewSqlParameter("CoachUserId", loggedInUser?.UserId)) > 0;
      }

      public class Actions_StuckCoachees {
        public int CoacheeId;
        public string FullName;
        public int DaysSinceBooking;
        public Actions_StuckCoachees(
          int coacheeId,
          string fullName,
          int daysSinceBooking
        ) {
          this.CoacheeId = coacheeId;
          this.FullName = fullName;
          this.DaysSinceBooking = daysSinceBooking;
        }
      }

      public class Actions_MissingCoach {
        public int CoacheeId;
        public string FullName;
        public string CompanyName;
        public string JobNumber;
        public string ProgramName;
        public string ProjectName;
        public Actions_MissingCoach(
          int coacheeId,
          string fullName,
          string companyName,
          string jobNumber,
          string programName,
          string projectName
        ) {
          this.CoacheeId = coacheeId;
          this.FullName = fullName;
          this.CompanyName = companyName;
          this.JobNumber = jobNumber;
          this.ProgramName = programName;
          this.ProjectName = projectName;
        }
      }

      public class CoacheeEventinfo {
        public DateTime EventDateUtc;
        public int? SurveyPartId;
        public int? SurveyId;
        public string SurveyTitle;
        public DateTime? PartCompleted;
        public int? RatersInvited;
        public int? RatersCompleted;
        public int? CoachingSessionId;
        public int? SessionDurationMins;
      }

      public class AbleCoacheeList {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public string SqlText { get; internal set; }
        public List<CoacheeListItem> CoacheeInfoList { get; internal set; }
        public AbleCoacheeList() {
          OffsetRows = null;
          FetchRows = null;
          TotalRows = 0;
          CoacheeInfoList = new List<CoacheeListItem>();
        }
      }

      public class CoacheeListItem {

        public int CoacheeId { get; private set; }
        public Guid CoacheeGuid { get; private set; }
        public int UserId { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string FullName { get; private set; }
        public string EmailAddress { get; private set; }
        public string MobilePhone { get; private set; }
        public int? CoachUserId { get; private set; }
        public string CompanyName { get; private set; }
        public int? ProgramJobId { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public string ProgramJobName { get; private set; }
        public string ProjectName { get; private set; }
        public int TenantOrgId { get; private set; }
        public int TenantOrgOwnerUserId { get; private set; }
        public AbleUser.UserActivityInfo UserActivity { get; private set; }
        public Subscriptions.User.UserSubscriptionInfo UserSubscription { get; private set; }

        public CoacheeListItem(
          int coacheeId,
          Guid coacheeGuid,
          int userId,
          string firstName,
          string lastName,
          string fullName,
          string emailAddress,
          string mobilePhone,
          int? coachUserId,
          string companyName,
          int? programJobId,
          string programJobNumber,
          string programJobName,
          string projectName,
          int tenantOrgId,
          int tenantOwnerUserId,
          Subscriptions.User.UserSubscriptionInfo userSubscription,
          AbleUser.UserActivityInfo userActivity
        ) {
          this.CoacheeId = coacheeId;
          this.CoacheeGuid = coacheeGuid;
          this.UserId = userId;
          this.FirstName = firstName;
          this.LastName = lastName;
          this.FullName = fullName;
          this.EmailAddress = emailAddress;
          this.MobilePhone = mobilePhone;
          this.CoachUserId = coachUserId;
          this.CompanyName = companyName;
          this.ProgramJobId = programJobId;
          this.ProgramJobNumber = programJobNumber;
          this.ProgramJobName = programJobName;
          this.ProjectName = projectName;
          this.TenantOrgId = tenantOrgId;
          this.TenantOrgOwnerUserId = tenantOwnerUserId;
          this.UserSubscription = userSubscription;
          this.UserActivity = userActivity;
        }

      }

      public class RaterEmailInfo {
        public int RaterPartId { get; private set; }
        public string RaterPartUID { get; private set; }
        public string RaterFirstName { get; private set; }
        public string RaterLastName { get; private set; }
        public string RaterEmail { get; private set; }
        public string CoacheeFirstName { get; private set; }
        public string CoacheeLastName { get; private set; }
        public string CoacheeEmail { get; private set; }
        public string CoachFirstName { get; private set; }
        public string CoachLastName { get; private set; }
        public string CoachEmail { get; private set; }
        public string CompanyName { get; private set; }
        public RaterEmailInfo(
          int raterPartId,
          string raterPartUID,
          string raterFirstName,
          string raterLastName,
          string raterEmail,
          string coacheeFirstName,
          string coacheeLastName,
          string coacheeEmail,
          string coachFirstName,
          string coachLastName,
          string coachEmail,
          string companyName
        ) {
          this.RaterPartId = raterPartId;
          this.RaterPartUID = raterPartUID;
          this.RaterFirstName = raterFirstName;
          this.RaterLastName = raterLastName;
          this.RaterEmail = raterEmail;
          this.CoacheeFirstName = coacheeFirstName;
          this.CoacheeLastName = coacheeLastName;
          this.CoacheeEmail = coacheeEmail;
          this.CoachFirstName = coachFirstName;
          this.CoachLastName = coachLastName;
          this.CoachEmail = coachEmail;
          this.CompanyName = companyName;
        }
      }

      public class CoacheesForProgramEmail {

        private CoacheeProgramStatus.ProgramStatusInfo _programStatus = CoacheeProgramStatus.GetStatus_WaitingLaunch(); // Default required for valid object.

        public int CoacheeId;
        public int? UserId { get; internal set; }
        public int CoachUserId;
        public string FirstName;
        public string LastName;
        public string EmailAddress;
        public string MobilePhone;
        public Guid CoacheeUID;
        public int SessionsAllocated;
        public int SessionsBooked;
        public int SessionsCompleted;
        public int DaysSinceBooking;
        public string ProgramJobNumber;
        public string ProgramName;
        public int? CoachingTypeId;
        public bool HasUncompletedSelfSurvey;
        public int ProgramStatusId {
          get => _programStatus.ProgramStatusId;
          set {
            _programStatus = CoacheeProgramStatus.GetProgramStatusByIdOrNull(value);
            if (_programStatus == null) throw new ArgumentOutOfRangeException($"Invalid ProgramStatusId assigned: {value}");
          }
        }
        public CoacheeProgramStatus.ProgramStatusInfo ProgramStatus => _programStatus;

        public CoacheesForProgramEmail(
          int coacheeId,
          int? userId,
          int coachUserId,
          string firstName,
          string lastName,
          string emailAddress,
          string mobilePhone,
          Guid coacheeUID,
          int sessionsAllocated,
          int sessionsBooked,
          int sessionsCompleted,
          int? coachingTypeId,
          int daysSinceBooking,
          string programJobNumber,
          string programJobName,
          int programStatusId,
          bool hasUncompletedSelfSurvey
        ) {
          this.CoacheeId = coacheeId;
          this.UserId = userId;
          this.CoachUserId = coachUserId;
          this.FirstName = firstName;
          this.LastName = lastName;
          this.EmailAddress = emailAddress;
          this.MobilePhone = mobilePhone;
          this.CoacheeUID = coacheeUID;
          this.SessionsAllocated = sessionsAllocated;
          this.SessionsBooked = sessionsBooked;
          this.SessionsCompleted = sessionsCompleted;
          this.ProgramJobNumber = programJobNumber;
          this.ProgramName = programJobName;
          this.ProgramStatusId = programStatusId;
          this.DaysSinceBooking = daysSinceBooking;
          this.CoachingTypeId = coachingTypeId;
          this.HasUncompletedSelfSurvey = hasUncompletedSelfSurvey;
        }

        public bool HasCoaching => CoachingTypeId != null && CoachingTypeId != AlbertCoachingTypes.GetType_NoCoaching().CoachingTypeId;

        public string GetFullName() => FirstName + " " + LastName;
      }

      public class AlbertCoacheeInfo {

        private CoacheeProgramStatus.ProgramStatusInfo _programStatus = CoacheeProgramStatus.GetStatus_WaitingLaunch(); // Default required for valid object.

        public int TenantOrgId = ConfigHelper.CoacheeDefaultOrgId; // Default required for valid object.
        public int TenantOrgOwnerUserId;

        public int CoacheeId;
        public int CoachUserId = ConfigHelper.UserId.Unassigned; // Default required for valid object.
        public int? UserId { get; internal set; }
        public Guid? UserGuid { get; private set; }
        public int? PersonId;
        public int? CompanyId;
        public string CompanyName;
        public int? ProgramJobId;
        public string ProgramJobUID;
        public string ProgramJobNumber;
        public string ProgramName;
        public string FriendlyProjectTitle;
        public bool PulseSurveyDisabled;
        public int? PulseSurveyTemplateId;
        public bool SendIntakeSurveyToAllParticipants;
        public Guid? BrandingOrgGuid;
        public DateTime? ProgramStartDateUtc;
        public DateTime? ProgramEndDateUtc;
        public string ProgramNotes;
        public string IntercomUserId;
        public string FirstName;
        public string LastName;
        public string EmailAddress;
        internal string CurrentEmailAddress;
        public string MobilePhone;
        public Guid CoacheeUID;
        public decimal? CoachingRevenue { get; private set; }
        public int? CoachingTypeId;
        public int ProgramStatusId {
          get => _programStatus.ProgramStatusId;
          set {
            _programStatus = CoacheeProgramStatus.GetProgramStatusByIdOrNull(value);
            if (_programStatus == null) throw new ArgumentOutOfRangeException($"Invalid ProgramStatusId assigned: {value}");
          }
        }
        public CoacheeProgramStatus.ProgramStatusInfo ProgramStatus => _programStatus;
        public bool DisableNudges;
        public bool PulseSurveyEnabled;
        public int? PulseSurveyId;
        public DateTime? PulseSurveyLastSentUtc;
        public bool SubscriptionUser;
        public bool HasLockedComponents;
        public DateTime? WelcomeEmailUtc;
        public DateTime? WelcomeEmailSentUtc;
        public DateTime? MeetCoachEmailUtc;
        public DateTime? MeetCoachEmailSentUtc;
        public DateTime? Send360ReportUtc;
        public DateTime? NextBookingTargetDateUtc;
        public DateTime? NextBookingSendReminderEmailUtc;
        public DateTime? BookingReminderLastSentUtc;
        public string CoacheeNotes;
        public string PrivateCoachNote;
        public Subscriptions.User.UserSubscriptionInfo UserSubscription;
        public AbleUser.UserActivityInfo UserActivity;

        // User role properties for Intercom integration - delegate to user object
        private AbleUser.AbleUserBasicInfo _cachedUserInfo;
        private AbleUser.AbleUserBasicInfo GetUserInfo() {
          if (_cachedUserInfo == null && UserId.HasValue) {
            _cachedUserInfo = AbleUser.GetUserByIdOrNull(UserId.Value, AbleUser.RegisteredFilter.Any);
          }
          return _cachedUserInfo;
        }
        public bool IsAbleCoach => GetUserInfo()?.IsAbleCoach ?? false;
        public bool IsAbleClient => GetUserInfo()?.IsAbleClient ?? false;
        public bool IsParticipant => GetUserInfo()?.IsParticipant ?? false;
        public bool IsAbleAdmin => GetUserInfo()?.IsAbleAdmin ?? false;
        public bool IsOrgAdmin => GetUserInfo()?.IsTenantOrgAdmin ?? false;
        public string OrgName => GetUserInfo()?.OrgName ?? "";

        public AlbertCoacheeInfo() {
          this.UserSubscription = new Subscriptions.User.UserSubscriptionInfo();
          this.UserActivity = new AbleUser.UserActivityInfo();
        }

        public AlbertCoacheeInfo(
          string emailAddress,
          string firstName,
          string lastName,
          string mobilePhone,
          int tenantOrgId
        ) {
          this.EmailAddress = emailAddress;
          this.FirstName = firstName;
          this.LastName = lastName;
          this.MobilePhone = mobilePhone;
          this.TenantOrgId = tenantOrgId;
        }

        public AlbertCoacheeInfo(

          int tenantOrgId,
          int tenantOrgOwnerUserId,
          int coacheeId,
          int coachUserId,
          int? userId,
          Guid? userGuid,
          int? personId,
          int? companyId,
          string companyName,
          string intercomUserId,
          string firstName,
          string lastName,
          string emailAddress,
          string currentEmailAddress,
          string mobilePhone,
          Guid coacheeUID,
          decimal? coachingRevenue,
          int? coachingTypeId,
          int programStatusId,
          bool disableNudges,
          bool pulseSurveyEnabled,
          int? pulseSurveyId,
          DateTime? pulseSurveyLastSentUtc,
          bool subscriptionUser,
          bool hasLockedComponents,
          string coacheeNotes,
          string privateCoachNote,
          DateTime? welcomeEmailUtc,
          DateTime? welcomeEmailSentUtc,
          DateTime? meetCoachEmailUtc,
          DateTime? meetCoachEmailSentUtc,
          DateTime? send360ReportUtc,
          DateTime? nextBookingTargetDateUtc,
          DateTime? nextBookingSendReminderEmailUtc,
          DateTime? bookingReminderLastSentUtc,
          int? programJobId,
          string programJobUID,
          string programJobNumber,
          string programJobName,
          string friendlyProjectTitle,
          bool pulseSurveyDisabled,
          int? pulseSurveyTemplateId,
          bool sendIntakeSurveyToAllParticipants,
          Guid? brandingOrgGuid,
          DateTime? programStartDateUtc,
          DateTime? programEndDateUtc,
          string programNotes,
          Subscriptions.User.UserSubscriptionInfo userSubscription,
          AbleUser.UserActivityInfo userActivity
        ) {
          this.TenantOrgId = tenantOrgId;
          this.TenantOrgOwnerUserId = tenantOrgOwnerUserId;

          this.CoacheeId = coacheeId;
          this.CoachUserId = coachUserId;
          this.CoacheeUID = coacheeUID;

          this.UserId = userId;
          this.UserGuid = userGuid;
          this.PersonId = personId;

          this.CompanyId = companyId;
          this.CompanyName = companyName;
          this.IntercomUserId = intercomUserId;

          this.FirstName = firstName;
          this.LastName = lastName;
          this.EmailAddress = emailAddress;
          this.CurrentEmailAddress = currentEmailAddress;
          this.MobilePhone = mobilePhone;

          this.CoachingRevenue = coachingRevenue;
          this.CoachingTypeId = coachingTypeId;
          this.ProgramStatusId = programStatusId;
          this.DisableNudges = disableNudges;
          this.PulseSurveyEnabled = pulseSurveyEnabled;
          this.PulseSurveyId = pulseSurveyId;
          this.PulseSurveyLastSentUtc = pulseSurveyLastSentUtc;
          this.SubscriptionUser = subscriptionUser;
          this.HasLockedComponents = hasLockedComponents;
          this.WelcomeEmailUtc = welcomeEmailUtc;
          this.WelcomeEmailSentUtc = welcomeEmailSentUtc;
          this.MeetCoachEmailUtc = meetCoachEmailUtc;
          this.MeetCoachEmailSentUtc = meetCoachEmailSentUtc;
          this.Send360ReportUtc = send360ReportUtc;
          this.NextBookingTargetDateUtc = nextBookingTargetDateUtc;
          this.NextBookingSendReminderEmailUtc = nextBookingSendReminderEmailUtc;
          this.BookingReminderLastSentUtc = bookingReminderLastSentUtc;
          this.ProgramJobId = programJobId;
          this.ProgramJobUID = programJobUID;
          this.ProgramJobNumber = programJobNumber;
          this.ProgramName = programJobName;
          this.FriendlyProjectTitle = friendlyProjectTitle;
          this.PulseSurveyDisabled = pulseSurveyDisabled;
          this.PulseSurveyTemplateId = pulseSurveyTemplateId;
          this.SendIntakeSurveyToAllParticipants = sendIntakeSurveyToAllParticipants;
          this.BrandingOrgGuid = brandingOrgGuid;
          this.ProgramStartDateUtc = programStartDateUtc;
          this.ProgramEndDateUtc = programEndDateUtc;
          this.ProgramNotes = programNotes;
          this.CoacheeNotes = coacheeNotes;
          this.PrivateCoachNote = privateCoachNote;
          this.UserSubscription = userSubscription;
          this.UserActivity = userActivity;
        }

        public string GetFullName() => FirstName + " " + LastName;

        public bool HasCoaching => CoachingTypeId != null && CoachingTypeId != AlbertCoachingTypes.GetType_NoCoaching().CoachingTypeId;

        public bool HasWorkshops => this.UserActivity?.WorkshopsAllocated > 0;
        public bool HasSubscription => this.UserSubscription != null;
        public bool UserHasAICoach => this.UserSubscription != null && this.UserSubscription.HasAICoaching && this.UserSubscription.UserHasAICoachEnabled;

        public bool IsCoachAssigned => CoachUserId != ConfigHelper.UserId.Unassigned;

        public bool CanReceiveWelcomeEmail => this.HasCoaching || this.HasWorkshops || UserHasAICoach || this.SendIntakeSurveyToAllParticipants;

        public bool CanDelete => this.UserActivity?.SessionsBooked == 0 && !this.HasLockedComponents;

        public AlbertCoachingTypes.SessionTypeInfo GetFirstSessionType() {
          var ct = GetCoachingType();
          return ct == null ? null : ct.GetSessionType(1);
        }
        public AlbertCoachingTypes.SessionTypeInfo GetNextSessionType() {
          var ct = GetCoachingType();
          return ct == null ? null : ct.GetSessionType(UserActivity.SessionsBooked + 1);
        }

        public bool CanReceiveMeetCoachEmail =>
        CoachUserId != ConfigHelper.UserId.Unassigned &&
        (ProgramStatusId == CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId
        || ProgramStatusId == CoacheeProgramStatus.GetStatus_WaitingLaunch().ProgramStatusId
          || ProgramStatusId == CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId);

        // Qualifying conditions for meet coach email via auto-reminders process.
        public static string SQLCondition_AutoMeetCoachEmail(string tablePrefix) =>
          $"({tablePrefix}.ProgramStatusId = {CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId}" +
          $" OR {tablePrefix}.ProgramStatusId = {CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId}) " +
          $"AND {tablePrefix}.CoachingTypeId > 0 AND {tablePrefix}.CoachingTypeId <> {AlbertCoachingTypes.GetType_NoCoaching().CoachingTypeId} " +
          $"AND {tablePrefix}.CoachUserId > 0 AND {tablePrefix}.CoachUserId <> {ConfigHelper.UserId.Unassigned} " +
          $"AND {tablePrefix}.SessionsCompleted = 0 " +
          $"AND {tablePrefix}.MeetCoachEmailSentUtc IS NULL " +
          $"AND {tablePrefix}.MeetCoachEmailUtc IS NOT NULL " +
          $"AND {tablePrefix}.MeetCoachEmailUtc < GETUTCDATE() ";

        public AlbertCoachingTypes.CoachingTypeInfo GetCoachingType() => CoachingTypeId == null ? null : AlbertCoachingTypes.GetCoachingTypeById((int)CoachingTypeId);
      }

    }
  }
}

