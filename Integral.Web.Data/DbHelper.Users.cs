using NanoidDotNet;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using static Integral.Web.DbHelper.Common;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AbleUser {

      private const int PASSWORD_SALT_SIZE = 24; // Don't change this.
      private const int PASSWORD_HASH_SIZE = 24; // Don't change this.
      private const int PASSWORD_HASH_ITERATIONS = 1000; // Don't change this. Multiplier can be changed if iteration increase is needed.
      private const int PASSWORD_HASH_ITERATION_DEFAULT_MULTIPLIER = 1; // Default multiplier for iterations, stored in db as first part of PasswordHashed.
      private const char PASSWORD_HASH_ITERATION_MULTIPLIER_DELIMITER = '$'; // Delimiter in PasswordHashed column between the multiplier and the hash.
      public const string PASSWORD_REQUIRED_SYMBOLS = "!@#$%&*()_=+.?/-";
      public const int PASSWORD_MIN_LENGTH = 8;
      private const int PASSWORD_RESET_EXPIRY_MINS = 60; // How long password resets in the db are valid for.
      private static AbleUserInfo s_automationUser = null;
      internal const string JobNoDelim = "|";

      internal const string LatestCoacheeJoinAlias = "lap";
      internal const string LatestCoacheeColPrefix = "LatestCoachee_";
      internal const string LatestCoachingJoinAlias = "lac";
      internal const string LatestCoachingColPrefix = "LatestCoaching_";
      public const string UserActivityJoinAlias = "cui";

      public enum RegisteredFilter { Any, OnlyRegistered, OnlyUnregistered }
      public enum CreateNewOrg { Yes, No }
      public enum UserProfileType { Unknown, Admin, TenantAdmin, Provider, Client, Leader }

      static AbleUser() {
        s_automationUser = GetUserByIdOrNull(ConfigHelper.UserId.Automation, RegisteredFilter.Any);
      }

      public static AbleUserInfo GetAutomationUser() { return s_automationUser; }

      internal static string GetUserIdentityColumsSQL(string tbl_u, string tbl_o) => $@"
        {tbl_u}.UserId, {tbl_u}.UserGuid, {tbl_u}.FirstName, {tbl_u}.LastName, {tbl_u}.Email, {tbl_u}.OrgId, {tbl_u}.ClientCompanyId,
        {tbl_u}.IsAbleUser, {tbl_u}.IsAlbertAdmin, {tbl_u}.IsOrgAdmin, {tbl_u}.IsAlbertCoach, {tbl_u}.IsClient, {tbl_u}.IsParticipant, {tbl_u}.IsPartnerActive,
        {tbl_u}.IsReportViewer, {tbl_u}.ViewOnlyReportUniqueId,
        {tbl_u}.ProfileHiddenUtc, {tbl_u}.RegisteredUtc, {tbl_u}.DeletedUtc,
        {tbl_o}.OrgGuid, {tbl_o}.OrgOwnerUserId";

      internal static string GetUserBasicColumsSQL => $@"
        {GetUserIdentityColumsSQL("u", "o")},
        u.RoleTitle, u.Mobile, u.State, u.City,
        u.TimeZoneIdIANA,
        u.[Password], u.PasswordSalt, u.PasswordHashed,
        u.CreatedUtc, u.LastLoginUtc,
        u.InviteCode, u.InvitedByUserId, u.InvitedUtc, u.LastInviteReminderSentUtc,
        u.UserContractTypeId, u.SelfRegisteredAsRoleId,
        o.OrgName, o.PlatformFeePercent,
        cmp.CompanyName AS ClientCompanyName, cmp.ClientLeadUserId,
        uct.IncludeInPayRuns,
        dbo.fnGetUserSenderEmailName(u.UserId) AS ComputedSenderEmailName,
        dbo.fnGetUserSenderEmailAddress(u.UserId) AS ComputedSenderEmailAddress,
        {LatestCoacheeJoinAlias}.*,
        {LatestCoachingJoinAlias}.*,
        {Subscriptions.User.GetSubscriptionOuterApplySelectionSQL}";

      //  Get info from latest Coachee linked to user
      internal static string GetLatestCoacheeOuterApplySQL(
        string userTableAlias,
        string colPrefix, string joinAlias,
        bool mustHaveCoaching = false,
        bool specificCoacheeGuid = false) {

        return $@"
          OUTER APPLY (
            SELECT TOP 1
              lac.CoacheeId as {colPrefix}CoacheeId,
              lac.CoacheeUID as {colPrefix}CoacheeGuid,
              lac.ProgramStatusId as {colPrefix}CoacheeProgramStatusId,
              lac.CoachUserId as {colPrefix}CoachUserId,
              lac.SessionsAllocated as {colPrefix}SessionsAllocated,
              lac.SessionsBooked as {colPrefix}SessionsBooked,
              lac.ProgramJobId AS {colPrefix}ProgramJobId,
              lac.ProgramStatusId as {colPrefix}ProgramStatusId,
              laj.LeadConsultantUserId as {colPrefix}PLCUserId,
              laj.JobNumber AS {colPrefix}JobNumber,
              lasc.SvCompanyId as {colPrefix}CompanyId,
              lasc.CompanyName as {colPrefix}CompanyName,
              lac.DisableNudges as {colPrefix}DisableNudges,
              lac.PulseSurveyEnabled as {colPrefix}PulseSurveyEnabled,
              lac.PulseSurveyLastCreatedUtc as {colPrefix}PulseSurveyLastSentUtc,
              lap.CanSelfSelectCoach as {colPrefix}CanSelfSelectCoach
            FROM al_Coachees lac
            LEFT OUTER JOIN id_Job laj ON laj.JobId = lac.ProgramJobId
            LEFT OUTER JOIN al_Project lap ON lap.JobNumber = laj.JobNumber
            LEFT OUTER JOIN sv_SurveyCompany lasc ON lasc.SvCompanyId = lap.SvCompanyId
            WHERE lac.UserId = {userTableAlias}.UserId
              AND lac.DeletedUtc is NULL
              {(mustHaveCoaching ? "AND lac.SessionsAllocated > 0" : "")}
              {(specificCoacheeGuid ? "AND lac.CoacheeUID = @CoacheeGuid" : "")}
            ORDER BY {(mustHaveCoaching ? "lac.NextBookingTargetDateUtc DESC," : "")} lac.RowCreatedUtc DESC
          ) AS {joinAlias}";
      }

      public static List<SqlParameter> GetUserActivityInfoParamsSQL() {
        return new List<SqlParameter>() {
          NewSqlParameter("TodayLocal", TimeHelper.UtcNowToAppDefaultTimeZone()),
          NewSqlParameter("CoacheeStatusId_OnBoarding", CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId),
          NewSqlParameter("CoacheeStatusId_Paused", CoacheeProgramStatus.GetStatus_Paused().ProgramStatusId),
          NewSqlParameter("CoacheeStatusId_Active", CoacheeProgramStatus.GetStatus_ActiveProgram().ProgramStatusId),
          NewSqlParameter("WorkshopStatusId_Confirmed", WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId),
          NewSqlParameter("WorkshopStatusId_NotPlanned", WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId),
          NewSqlParameter("@SurveyTypeCode_360", ConfigHelper.SurveyTypeCodes.Able360),
          NewSqlParameter("@SurveyTypeCode_DevPlan", ConfigHelper.SurveyTypeCodes.DevPlan),
          NewSqlParameter("@SurveyTypeCode_Intake", ConfigHelper.SurveyTypeCodes.Intake),
          NewSqlParameter("@PulseSurveyTemplateId", ConfigHelper.TemplateSurveyIds.Pulse360),
          NewSqlParameter("Unassigned_CoachUserId", ConfigHelper.UserId.Unassigned),
          NewSqlParameter("AICoachEmail", ConfigHelper.AICoach_FromEmail)
        };
      }

      public static string GetUserActivityLeftJoinSelectionSQL => $@"
        cui.CoacheeId, cui.RegisteredUtc, cui.LastLoginUTC, cui.InvitedUtc, cui.LastInviteReminderSentUtc,
        cui.DateOfBirth, cui.RoleTitle, cui.City, cui.Country, cui.OrgRoleId, cui.DeletedUtc,
        cui.AIMsgsLast30Days, cui.TotalAIMsgs, cui.LastAIMessageSentUtc,
        cui.CoacheeStatusId, cui.SessionsAllocated, cui.SessionsBooked, cui.SessionsCompleted, cui.SessionsUpcoming,
        cui.CoachUserId, cui.CoachUserGuid, cui.CoachFirstName, cui.CoachLastName, cui.CoachEmail, cui.CoachTimeZoneIdIANA, cui.CoachCalendlyUrlName,
        cui.CoachComputedSenderEmailName, cui.CoachComputedSenderEmailAddress,
        cui.DaysSinceBooking, cui.MeetCoachEmailSentUtc, cui.CoachingTypeId,
        cui.LatestCoachingSession, cui.NextSessionApptDateUtc,
        cui.LatestWorkshop, cui.WorkshopsAllocated, cui.WorkshopsAttended,
        cui.LatestCompletedSurvey, cui.LatestIntakeCompleted, cui.LatestIntakeCreatedOpen, cui.LatestIntakeClosed,
        cui.LatestEvalCompleted,  cui.LatestDevPlan,
        cui.Latest360IntakeCodeId, cui.Latest360CompletedUtc, cui.Latest360CoacheeId, cui.Latest360CoacheeGuid,
        cui.Latest360SvUID, cui.Latest360PartId, cui.Latest360PartUID, cui.LatestPulseCompletedUtc, cui.AISummaryText";

      public static string GetUserActivityInfoLeftJoinSQL(string userIdColumn, string coacheeIdColumnIfSpecificCoachee, bool onlyWhereLeaderCanView360Report = false) {

        return $@"
          LEFT OUTER JOIN (
              SELECT
                cp.CoacheeId, cui.UserId, cui.RegisteredUtc, cui.InvitedUtc, cui.LastInviteReminderSentUtc,
                cui.DateOfBirth, cui.RoleTitle, cui.City, cui.Country, cui.OrgRoleId, cui.DeletedUtc,
                uls.LastLoginUTC,
                aim.AIMsgsLast30Days, aim.TotalAIMsgs, aim.LastAIMessageSentUtc,
                cp.ProgramStatusId AS CoacheeStatusId, cp.SessionsAllocated, cp.SessionsBooked, cp.SessionsCompleted, cs.SessionsUpcoming,
                cp.CoachUserId, cd.CoachUserGuid, cd.CoachFirstName, cd.CoachLastName, cd.CoachEmail, cd.CoachTimeZoneIdIANA, cd.CoachCalendlyUrlName,
                cd.CoachComputedSenderEmailName, cd.CoachComputedSenderEmailAddress,
                md.DaysSinceBooking,
                cp.MeetCoachEmailSentUtc, cp.CoachingTypeId,
                cs.LatestCoachingSession, cs.NextSessionApptDateUtc,
                wd.LatestWorkshop, wd.WorkshopsAllocated, wd.WorkshopsAttended,
                sd.LatestCompletedSurvey, sd.LatestIntakeCompleted, sd.LatestIntakeCreatedOpen, sd.LatestIntakeClosed, sd.LatestEvalCompleted, sd.LatestDevPlan,
                sv360.Latest360IntakeCodeId, sv360.Latest360CompletedUtc, sv360.Latest360CoacheeId, sv360.Latest360CoacheeGuid, sv360.Latest360SvUID,
                sv360.Latest360PartId, sv360.Latest360PartUID, svPulse.LatestPulseCompletedUtc,
                AITxt.AISummaryText
              FROM sv_User cui

              {(coacheeIdColumnIfSpecificCoachee.IsNullOrEmpty() // If CoacheeId is provided, do the join, otherwise select the last CoacheeId
              ? @"
                OUTER APPLY (
                  SELECT TOP 1
                    cp.CoacheeId, cp.CoacheeUID, cp.ProgramJobId, cp.ProgramStatusId, cp.SessionsAllocated, cp.SessionsBooked, cp.SessionsCompleted,
                    cp.CoachUserId, cp.MeetCoachEmailSentUtc, cp.CoachingTypeId, cp.WelcomeEmailSentUtc, cp.RowCreatedUtc
                  FROM al_Coachees cp
                  WHERE cp.DeletedUtc IS NULL AND cp.UserId = cui.UserId
                  ORDER BY cp.CoacheeId DESC
                ) AS cp"
              : "LEFT JOIN al_Coachees cp ON cp.UserId = cui.UserId")}

              OUTER APPLY (
                SELECT
                  MAX(wep.StartDateUtc) AS LatestWorkshop, COUNT(wep.WorkshopEventId) AS WorkshopsAllocated,
                  SUM(CASE WHEN wep.StartDate < GETUTCDATE() THEN 1 ELSE 0 END) AS WorkshopsAttended
                FROM ev_WorkshopEvent wep
                WHERE wep.WorkshopStatusId IN (@WorkshopStatusId_Confirmed, @WorkshopStatusId_NotPlanned) and cp.ProgramJobId = wep.ProgramJobId
              ) wd

              OUTER APPLY (
                SELECT
                  MAX(svp.Completed) AS LatestCompletedSurvey, MAX(CASE WHEN sv.SurveyTypeCode = @SurveyTypeCode_Intake THEN svp.Completed END) AS LatestIntakeCompleted,
                  MAX(CASE WHEN sv.SurveyTypeCode = @SurveyTypeCode_Intake AND svp.Completed IS NULL AND sv.sv_closedate IS NULL THEN sv.sv_createdUTC END) AS LatestIntakeCreatedOpen,
                  MAX(CASE WHEN sv.SurveyTypeCode = @SurveyTypeCode_Intake THEN sv.sv_closedate END) AS LatestIntakeClosed,
                  MAX(CASE WHEN svp.IsSelf = 1 AND svp.Completed IS NOT NULL AND sv.PrimaryGblAnswerTypeId = 3050 THEN svp.Completed END) AS LatestEvalCompleted,
                  MAX(CASE WHEN sv.SurveyTypeCode = @SurveyTypeCode_DevPlan THEN svp.Completed END) AS LatestDevPlan
                FROM sv_360_Participants svp
                INNER JOIN sv_Survey sv ON svp.SurveyId = sv.sv_id
                WHERE svp.AbleCoacheeId = cp.CoacheeId
              ) sd

              OUTER APPLY (
                SELECT
                  u.UserGuid as CoachUserGuid, u.Email AS CoachEmail, u.FirstName AS CoachFirstName, u.LastName AS CoachLastName,
                  u.TimeZoneIdIANA AS CoachTimeZoneIdIANA, u.CalendlyUrlName AS CoachCalendlyUrlName,
                  dbo.fnGetUserSenderEmailName(u.UserId) AS CoachComputedSenderEmailName,
                  dbo.fnGetUserSenderEmailAddress(u.UserId) AS CoachComputedSenderEmailAddress
                FROM sv_User u
                WHERE u.UserId = cp.CoachUserId
              ) cd

              OUTER APPLY (
                SELECT
                  COUNT(CASE WHEN aim.SentUtc >= DATEADD(day, -30, GETUTCDATE()) THEN 1 ELSE NULL END) AS AIMsgsLast30Days,
                  COUNT(*) AS TotalAIMsgs, MAX(aim.SentUtc) AS LastAIMessageSentUtc
                FROM al_AIMessage aim
                WHERE aim.IsFromAI = 0 and aim.UserId = cui.UserId
              ) AS aim

              OUTER APPLY (
                SELECT
                  MIN(CASE WHEN cs.ApptDateUtc > GETUTCDATE() THEN cs.ApptDateUtc ELSE NULL END) AS NextSessionApptDateUtc,
                  MAX(cs.ApptDateUTC) AS LatestCoachingSession,
                  COUNT(CASE WHEN cs.ApptDateUtc > GETUTCDATE() THEN 1 ELSE NULL END) AS SessionsUpcoming
                FROM id_CoachingSession cs
                WHERE cs.ApptCancelledUTC IS NULL and cs.AbleCoacheeId = cp.CoacheeId
              ) AS cs

              OUTER APPLY (
                SELECT DATEDIFF(DAY,  MAX(vals.val), GETUTCDATE()) AS DaysSinceBooking
                FROM (VALUES (cs.LatestCoachingSession), (cp.MeetCoachEmailSentUtc), (cp.WelcomeEmailSentUtc), (cp.RowCreatedUtc)) AS vals(val)
              ) AS md

              OUTER APPLY (
                SELECT TOP 1 uls.LastRequestUtc as LastLoginUTC
                FROM al_UserLoginSession uls
                WHERE uls.UserId = cui.UserId
                ORDER BY uls.LastRequestUtc DESC
              ) uls

              OUTER APPLY (
                SELECT TOP 1
                  sv.sv_uniqueid AS Latest360SvUID,
                  sp.PartId AS Latest360PartId,
                  sp.UniqueId AS Latest360PartUID,
                  sp.Completed AS Latest360CompletedUtc,
                  sp.IntakeCodeId AS Latest360IntakeCodeId,
                  sp.AbleCoacheeId AS Latest360CoacheeId,
                  ac.CoacheeUID AS Latest360CoacheeGuid
                FROM sv_360_Participants sp
                INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
                INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
                WHERE sp.UserId = cui.UserId
                  AND sp.IsSelf = 1
                  AND sv.SurveyTypeCode = @SurveyTypeCode_360
                  AND sv.HasLeadershipQuestions = 1
                  AND sv.ClonedFromSvId <> @PulseSurveyTemplateId
                  AND sp.Completed IS NOT NULL
                  {(onlyWhereLeaderCanView360Report ? "AND sp.CanLeaderView360Report = 1" : "")}
                ORDER BY sp.Completed DESC
              ) AS sv360

              OUTER APPLY (
                SELECT TOP 1 sp.Completed AS LatestPulseCompletedUtc, sp.IntakeCodeId AS LatestPulseIntakeCodeId
                FROM sv_360_Participants sp
                INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
                WHERE sp.UserId = cui.UserId
                  AND sp.IsSelf = 1
                  AND sv.ClonedFromSvId = @PulseSurveyTemplateId
                  AND sp.Completed IS NOT NULL
                ORDER BY sp.Completed DESC
              ) AS svPulse

              OUTER APPLY (
                SELECT TOP 1 uai.AISummaryText
                FROM al_UserAISummary uai
                WHERE uai.UserId = cui.UserId
                ORDER BY uai.CreatedUtc DESC
              ) AS AITxt

          ) AS cui ON cui.UserId = {userIdColumn}.UserId
          {(coacheeIdColumnIfSpecificCoachee.IsNullOrEmpty() ? "" : $" AND cui.CoacheeId = {coacheeIdColumnIfSpecificCoachee}.CoacheeId")}"; // cui = Coachee User Info
      }

      // Note this function should remain private,
      // and is the *only* place where strings are injected into SQL.
      // The purpose is to reduce repeat code, other functions just provide extra SQL.
      private static string GetUserSelectSQL(string sqlExtraJoins, string sqlWhereConditions, string sqlOrderByColumns, RegisteredFilter registeredFilter) {

        string sql = $@"
          SELECT
            {GetUserBasicColumsSQL},
            o.DocTagName,
            u.IOSClientHR, u.AccessOnlySurveyId,
            u.AbleBioShort, u.AbleWebProfileUrl,
            ISNULL(cmp.CompanyGUID, lc_cmp.CompanyGUID) AS CompanyGUID,
            ISNULL(cmp.DisplayLogoInNavBar, lc_cmp.DisplayLogoInNavBar) AS DisplayLogoInNavBar,

            -- Companies in which User is PLC
            (
              SELECT STRING_AGG(ap.SvCompanyId, ',')
              FROM (
                SELECT DISTINCT ap.SvCompanyId
                FROM id_Job j
                INNER JOIN al_Project ap ON ap.JobNumber = j.JobNumber
                WHERE j.ProjectCoordinatorUserId = u.UserId
              ) AS ap
            ) AS PCCompanyIds,

            -- Companies in which User is PC
            (
              SELECT STRING_AGG(ap.SvCompanyId, ',')
              FROM (
                SELECT DISTINCT ap.SvCompanyId
                FROM id_Job j
                INNER JOIN al_Project ap ON ap.JobNumber = j.JobNumber
                WHERE j.LeadConsultantUserId = u.UserId
              ) AS ap
            ) AS PLCCompanyIds,

            -- Companies in which User is Sales Partner
            (
              SELECT STRING_AGG(ap.SvCompanyId, ',')
              FROM (
                SELECT DISTINCT ap.SvCompanyId
                FROM id_Job j
                INNER JOIN al_Project ap ON ap.JobNumber = j.JobNumber
                WHERE j.Partner_UserId = u.UserId
              ) AS ap
            ) AS SPCompanyIds,

            -- Companies in which User is a workshop Key or Co Facilitator
            ( SELECT STRING_AGG(ap.SvCompanyId, ',')
              FROM al_Project ap
              WHERE EXISTS (
                SELECT 1 FROM ev_WorkshopEvent we
                INNER JOIN id_Job ij ON we.ProgramJobId = ij.JobId
                WHERE ij.JobNumber = ap.JobNumber AND (we.KeyFacilitatorUserId = u.UserId OR we.CoFacilitatorUserId = u.UserId))
            ) AS WKFCompanyIds,

            -- Companies in which user is a Coach.
            ( SELECT STRING_AGG(ac.CompanyId, ',')
              FROM
              ( SELECT DISTINCT ac.CompanyId
                FROM al_Coachees ac
                WHERE ac.CoachUserId = u.UserId
              ) AS ac
            ) AS CoachCompanyIds,

            -- Projects in which User is a Program Coordinator
            ( SELECT STRING_AGG(j.JobNumber, ',')
              FROM
              ( SELECT DISTINCT j.JobNumber
                FROM id_Job j
                WHERE j.ProjectCoordinatorUserId = u.UserId
              ) AS j
            ) AS PCJobNos,

            -- Projects in which User is a Lead Consultant
            ( SELECT STRING_AGG(j.JobNumber, ',')
              FROM
              ( SELECT DISTINCT j.JobNumber
                FROM id_Job j
                WHERE j.LeadConsultantUserId = u.UserId
              ) AS j
            ) AS PLCJobNos,

            -- Projects in which User is a workshop Key or Co Facilitator
            ( SELECT STRING_AGG(ap.JobNumber, ',')
              FROM al_Project ap
              WHERE EXISTS (
                SELECT 1 FROM ev_WorkshopEvent we
                INNER JOIN id_Job ij ON we.ProgramJobId = ij.JobId
                WHERE ij.JobNumber = ap.JobNumber AND (we.KeyFacilitatorUserId = u.UserId OR we.CoFacilitatorUserId = u.UserId))
            ) AS WKFJobNos,

            -- Projects in which User has Consulting Items.
            ( SELECT STRING_AGG(ap.JobNumber, ',')
              FROM al_Project ap
              WHERE EXISTS (
                SELECT 1 FROM al_ConsultingItems ci
                INNER JOIN id_Job ij ON ci.ProgramJobId = ij.JobId
                WHERE ij.JobNumber = ap.JobNumber AND ci.ConsultantUserId = u.UserId)
            ) AS PCIJobNos,

            -- Projects in which user is quote owner.
            ( SELECT STRING_AGG(q.JobNumber, ',')
              FROM
              ( SELECT DISTINCT q.JobNumber
                FROM al_Quote q
                WHERE q.OwnerUserId = u.UserId
              ) AS q
            ) AS QuoteOwnerJobNos,

            -- Projects in which user is a Coach.
            ( SELECT STRING_AGG(j.JobNumber, ',')
              FROM
              ( SELECT DISTINCT j.JobNumber
                FROM id_Job j
                INNER JOIN al_Coachees ac ON j.JobId = ac.ProgramJobId
                WHERE ac.CoachUserId = u.UserId
              ) AS j
            ) AS CoachJobNos,

            -- Projects for which user is Quote Contact.
            (
              SELECT STRING_AGG(jn.JobNumber, ',')
              FROM (
                SELECT DISTINCT q.JobNumber
                FROM al_Quote q
                WHERE q.QuoteUserId = u.UserId
              ) AS jn
            ) AS QuoteContactJobNos,

            -- Projects for which user has Project Access.
            (SELECT STRING_AGG(jn.JobNumber, ',')
              FROM
              ( SELECT DISTINCT ap.JobNumber
                FROM al_UserProjectAccess upa
                INNER JOIN al_Project ap ON ap.ProjectId = upa.ProjectId
                WHERE upa.UserId = u.UserId
              ) AS jn
            ) AS ProjectAccessJobNos,

            -- Companies in which user has Project Access.
            (SELECT string_agg(ap.SvCompanyId, ',')
              FROM
              ( SELECT DISTINCT ap.SvCompanyId
                FROM al_UserProjectAccess upa
                INNER JOIN al_Project ap ON ap.ProjectId = upa.ProjectId
                WHERE upa.UserId = u.UserId
              ) AS ap
            ) AS ProjectAccessCompanyIds

          FROM sv_User u
          INNER JOIN sv_Organisation o ON o.OrgId = u.OrgId
          LEFT OUTER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = u.ClientCompanyId
          {Subscriptions.User.GetUserSubscriptionOuterApplySQL("u")}
          {GetLatestCoacheeOuterApplySQL("u", LatestCoacheeColPrefix, LatestCoacheeJoinAlias)}
          {GetLatestCoacheeOuterApplySQL("u", LatestCoachingColPrefix, LatestCoachingJoinAlias, mustHaveCoaching: true)}
          LEFT OUTER JOIN sv_SurveyCompany lc_cmp ON lc_cmp.SvCompanyId = {LatestCoachingJoinAlias}.{LatestCoachingColPrefix}CompanyId
          LEFT OUTER JOIN al_UserContractType uct ON uct.UserContractTypeId = u.UserContractTypeId
          {sqlExtraJoins}

          WHERE u.IsAbleUser = 1";

        if (registeredFilter == RegisteredFilter.OnlyRegistered) sql += " AND u.RegisteredUtc IS NOT NULL";
        else if (registeredFilter == RegisteredFilter.OnlyUnregistered) sql += " AND u.RegisteredUtc IS NULL";

        if (!sqlWhereConditions.IsNullOrEmpty()) sql += $" AND ({sqlWhereConditions})";

        sql += " ORDER BY " + sqlOrderByColumns.ValueIfNullOrEmpty("u.FirstName, u.LastName");

        return sql;
      }

      private static string GetUserSelectSQL(string sqlWhereConditions, RegisteredFilter registeredFilter) {
        return GetUserSelectSQL("", sqlWhereConditions, "", registeredFilter);
      }

      public static AbleUserInfo GetQuoteContactUserOrNull(int quoteContactUserId) {
        return GetUserByIdOrNull(quoteContactUserId, RegisteredFilter.Any); // Note these can include unregistered users.
      }

      public static AbleUserInfo GetProjectAccessUserOrNull(int quoteContactUserId) {
        return GetUserByIdOrNull(quoteContactUserId, RegisteredFilter.Any); // Note these can include unregistered users.
      }

      public static AbleUserInfo GetUserByIdOrNull(int userId, RegisteredFilter registeredFilter) {

        AbleUserInfo u = null;
        Query(GetUserSelectSQL($"u.UserId = @UserId", registeredFilter),
          dr => {
            u = new AbleUserInfo(dr);
          },
          NewSqlParameter("UserId", userId)
        );
        return u;
      }

      public static AbleUserInfo GetUserByEmailOrNull(string emailAddress, RegisteredFilter registeredFilter) {

        return GetUserByEmailOrNull(null, emailAddress, registeredFilter);
      }

      public static AbleUserInfo GetUserByEmailOrNull(SqlTransaction trans, string emailAddress, RegisteredFilter registeredFilter) {

        AbleUserInfo u = null;
        Query(trans, GetUserSelectSQL($"u.Email = @Email", registeredFilter),
          dr => {
            u = new AbleUserInfo(dr);
          },
          NewSqlParameter("Email", emailAddress, 100)
        );
        return u;
      }

      public static AbleUserInfo GetUserByFullName(string fullName) {

        AbleUserInfo u = null;
        Query(GetUserSelectSQL("u.FirstName + ' ' + u.LastName = @FullName", RegisteredFilter.OnlyRegistered),
          dr => {
            u = new AbleUserInfo(dr);
          },
          NewSqlParameter("FullName", fullName, 100)
        );
        return u;
      }

      public static AbleUserInfo GetOwnerForQuote(int quoteId) {

        int? userId = null;
        Query(
          "SELECT OwnerUserId FROM al_Quote WHERE QuoteId = @QuoteId",
          dr => {
            userId = dr.GetIntOrNull("OwnerUserId");
          },
          NewSqlParameter("QuoteId", quoteId)
        );
        return userId == null ? null : GetUserByIdOrNull((int)userId, RegisteredFilter.OnlyRegistered);
      }

      public static List<AbleUserBasicInfo> SearchByNameOrEmail(
        string searchText,
        AppHelper.ParamEnum.UserSearchFilter userSearchFilter,
        UserIdentity userContext,
        RegisteredFilter registeredFilter) {

        if (userSearchFilter == AppHelper.ParamEnum.UserSearchFilter.None) return null;

        var whereCondition = "(u.FirstName + ' ' + u.LastName LIKE '%' + @SearchText + '%' OR u.Email LIKE '%' + @SearchText + '%')";

        if (userSearchFilter == AppHelper.ParamEnum.UserSearchFilter.TenantOrg) {
          whereCondition += " AND u.OrgId = @FilterOrgId";
        }

        return GetBasicInfoList(null, "", whereCondition, registeredFilter,
          NewSqlParameter("SearchText", searchText),
          NewSqlParameter("FilterOrgId", userContext?.OrgId)
        );
      }

      public static List<AbleUserBasicInfo> GetAdminDropdownList() {

        var list = GetBasicInfoList(null, "", "u.IsAlbertAdmin = 1", RegisteredFilter.OnlyRegistered);
        return list;
      }

      public static List<AbleUserBasicInfo> GetQuoteOwnerDropdownList() {

        var list = GetBasicInfoList(null, "",
          "u.IsAlbertCoach = 1 AND u.UserId <> @UnassignedUserId",
          RegisteredFilter.OnlyRegistered,
          NewSqlParameter("UnassignedUserId", ConfigHelper.UserId.Unassigned));
        return list;
      }

      public static List<AbleUserBasicInfo> GetClientDropdownList() {

        var list = GetBasicInfoList(null, "", $"u.IsClient = 1", RegisteredFilter.OnlyRegistered);
        return list;
      }

      public static List<AbleUserBasicInfo> GetAllDropdownList() {

        var list = GetBasicInfoList(null, "", $@"
          u.UserId <> @AutomationUserId
          AND u.UserId <> @UnassignedUserId
          AND APIToken IS NULL",
          RegisteredFilter.OnlyRegistered,
          NewSqlParameter("AutomationUserId", ConfigHelper.UserId.Automation),
          NewSqlParameter("UnassignedUserId", ConfigHelper.UserId.Unassigned));
        return list;
      }

      public static AbleUserBasicInfo GetBasicInfoById(int userId, RegisteredFilter registeredFilter) {
        return GetBasicInfoById(null, userId, registeredFilter);
      }
      public static AbleUserBasicInfo GetBasicInfoById(SqlTransaction trans, int userId, RegisteredFilter registeredFilter) {
        var list = GetBasicInfoList(trans, "",
          "u.UserId = @UserId",
          registeredFilter,
          NewSqlParameter("UserId", userId));
        return list.IsNullOrEmpty() ? null : list[0];
      }

      public static AbleUserBasicInfo GetBasicInfoByEmail(SqlTransaction trans, string emailAddress, RegisteredFilter registeredFilter) {
        var list = GetBasicInfoList(trans, "",
          "u.Email = @EmailAddress",
          registeredFilter,
          NewSqlParameter("EmailAddress", emailAddress));
        return list.IsNullOrEmpty() ? null : list[0];
      }

      // Note this function should remain private,
      // and is the *only* place where strings are injected into SQL.
      // The purpose is to reduce repeat code, other functions just provide extra SQL.
      private static List<AbleUserBasicInfo> GetBasicInfoList(
        SqlTransaction trans,
        string sqlExtraJoins,
        string sqlWhereConditions,
        RegisteredFilter registeredFilter,
        params SqlParameter[] sqlParams
      ) {

        var users = new List<AbleUserBasicInfo>();

        string sql = $@"
          SELECT {GetUserBasicColumsSQL}
          FROM sv_User u
          INNER JOIN sv_Organisation o ON o.OrgId = u.OrgId
          LEFT OUTER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = u.ClientCompanyId
          LEFT OUTER JOIN al_UserContractType uct ON uct.UserContractTypeId = u.UserContractTypeId
          {sqlExtraJoins.EmptyIfNull()}
          {Subscriptions.User.GetUserSubscriptionOuterApplySQL("u")}
          {GetLatestCoacheeOuterApplySQL("u", LatestCoacheeColPrefix, LatestCoacheeJoinAlias)}
          {GetLatestCoacheeOuterApplySQL("u", LatestCoachingColPrefix, LatestCoachingJoinAlias, mustHaveCoaching: true)}
          WHERE u.IsAbleUser = 1
            {(registeredFilter == RegisteredFilter.OnlyRegistered ? "AND u.RegisteredUtc IS NOT NULL" : "")}
            {(registeredFilter == RegisteredFilter.OnlyUnregistered ? "AND u.RegisteredUtc IS NULL" : "")}
            {(!sqlWhereConditions.IsNullOrEmpty() ? $"AND ({sqlWhereConditions})" : "")}
          ORDER BY u.FirstName, u.LastName";

        Query(trans, sql,
          dr => {
            var user = new AbleUserBasicInfo(dr);
            users.Add(user);
          },
          sqlParams);

        return users;
      }

      public static List<AbleUserInfo> GetAllAlbertCoaches() {

        var coachList = new List<AbleUserInfo>();

        Query(GetUserSelectSQL("u.IsAlbertCoach = 1", RegisteredFilter.OnlyRegistered),
          dr => {
            coachList.Add(new AbleUserInfo(dr));
          }
        );
        return coachList;
      }

      public static List<AbleUserBasicInfo> GetUsersForRegistrationInviteEmails() {

        // Note the query spaces reminders apart by @RegistrationInvitationGapDays days,
        // and if user is a Participant, make sure a non-deleted coachee exists (any coachee, with or without coaching).
        return GetBasicInfoList(null, "", $@"
          (u.IsClient = 1 OR u.IsAlbertCoach = 1 OR (u.IsParticipant = 1 AND {LatestCoacheeJoinAlias}.LatestCoachee_CoacheeId IS NOT NULL))
          AND u.RegisteredUtc IS NULL
          AND u.InviteCode <> ''
          AND DATEDIFF(DAY, ISNULL(u.LastInviteReminderSentUtc, u.InvitedUtc), GETUTCDATE()) >= @RegistrationInvitationGapDays
          AND DATEDIFF(DAY, u.InvitedUtc, GETUTCDATE()) <= @ReminderPeriodDays",
          RegisteredFilter.OnlyUnregistered,
          NewSqlParameter("RegistrationInvitationGapDays", ConfigHelper.RegistrationInvitationGapDays),
          NewSqlParameter("ReminderPeriodDays", ConfigHelper.RegistrationInvitationGapDays * ConfigHelper.RegistrationInvitationMaxReminders));
      }

      public static List<AbleUserBasicInfo> GetClientsToInviteForQuotesAcceptedPreviousDay() {

        return GetBasicInfoList(null,
          "INNER JOIN al_Quote q ON q.QuoteUserId = u.UserId",
          "u.IsClient = 1 " +
          "AND u.RegisteredUtc IS NULL " +
          "AND u.LastInviteReminderSentUtc IS NULL " +
          "AND DATEDIFF(DAY, q.ClientAcceptedUtc, GETDATE()) = 1",
          RegisteredFilter.Any)
          .GroupBy(c => c.UserId).Select(c => c.First()).ToList(); // Ensure distinct userids.
      }

      public static List<LogInAsUser> GetLogInAsUsers() {

        var users = new List<LogInAsUser>();

        Query($@"
          SELECT {GetUserIdentityColumsSQL("u", "o")},
            sub.SubscriptionName
          FROM sv_User u
          INNER JOIN sv_Organisation o ON o.OrgId = u.OrgId
          LEFT OUTER JOIN al_UserSubscription usub ON usub.UserId = u.UserId AND usub.SubscriptionEndUtc > SYSUTCDATETIME()
          LEFT OUTER JOIN al_Subscription sub ON sub.SubscriptionId = usub.SubscriptionId
          WHERE u.IsAbleUser = 1
            AND u.RegisteredUtc IS NOT NULL
          ORDER BY u.OrgId, u.Email",
          dr => {
            users.Add(new LogInAsUser(dr));
          }
        );

        return users;
      }

      private static bool UpdateTenantOrgId(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, int tenantOrgId) {

        if (userBasicInfo == null) return false;

        bool updated = GetNonQueryInt(trans, $@"
          UPDATE {DbTable.User}
          SET OrgId = @OrgId
          WHERE UserId = @UserId",
          NewSqlParameter("UserId", userBasicInfo.UserId),
          NewSqlParameter("OrgId", tenantOrgId)
        ) == 1;

        if (updated) userBasicInfo.OrgId = tenantOrgId;

        return updated;
      }

      private static bool UpdateIsTenantOrgAdmin(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, bool isTenantOrgAdmin) {

        if (userBasicInfo == null) return false;

        bool updated = GetNonQueryInt(trans, $@"
          UPDATE {DbTable.User}
          SET IsOrgAdmin = @IsOrgAdmin
          WHERE UserId = @UserId",
          NewSqlParameter("UserId", userBasicInfo.UserId),
          NewSqlParameter("IsOrgAdmin", isTenantOrgAdmin)
        ) == 1;

        if (updated) userBasicInfo.IsTenantOrgAdmin = isTenantOrgAdmin;

        return updated;
      }

      public static bool UpdateLastLoginUtc(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, DateTime lastLoginUtc) {

        if (userBasicInfo == null) return false;

        bool updated = GetNonQueryInt(trans, $@"
          UPDATE {DbTable.User}
          SET LastLoginUtc = @LastLoginUtc
          WHERE UserId = @UserId",
          NewSqlParameter("UserId", userBasicInfo.UserId),
          NewSqlParameter("LastLoginUtc", lastLoginUtc)
        ) == 1;

        if (updated) userBasicInfo.LastLoginUtc = lastLoginUtc;

        return updated;
      }

      public static bool UpdateIsClient(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, bool isClient) {

        if (userBasicInfo == null) return false;

        bool updated = GetNonQueryInt(trans, $@"
          UPDATE {DbTable.User}
          SET IsClient = @IsClient
          WHERE UserId = @UserId",
          NewSqlParameter("UserId", userBasicInfo.UserId),
          NewSqlParameter("IsClient", isClient)
        ) == 1;

        if (updated) userBasicInfo.IsAbleClient = isClient;

        return updated;
      }

      private class NewUserStub : AbleUserBasicInfo {

        public NewUserStub(
          int userId, int orgId,
          string firstNane, string lastName, string roleTitle,
          string emailAddress, string mobileNumber, string state, string city,
          bool isCoach, bool isClient, bool isParticipant, bool isTenantOrgAdmin,
          DateTime? registeredUtc, int? selfRegisteredAsRoleId
        ) {
          UserId = userId;
          OrgId = orgId;
          FirstName = firstNane;
          LastName = lastName;
          RoleTitle = roleTitle;
          EmailAddress = emailAddress;
          MobileNumber = mobileNumber;
          State = state;
          City = city;
          IsAbleCoach = isCoach;
          IsAbleClient = isClient;
          IsParticipant = isParticipant;
          IsTenantOrgAdmin = isTenantOrgAdmin;
          RegisteredUtc = registeredUtc;
          SelfRegisteredAsRoleId = selfRegisteredAsRoleId;
        }
      }

      private static NewUserStub CreateAbleUserStub(
        SqlTransaction trans, int orgId,
        string firstName, string lastName, string roleTitle,
        string emailAddress, string mobileNumber, string state, string city,
        bool isCoach, bool isClient, bool isParticipant, bool isTenantOrgAdmin,
        DateTime? registeredUtc = null, int? selfRegisteredAsRoleId = null) {

        var newUserStub = new NewUserStub(
          userId: 0,
          orgId: orgId,
          firstNane: firstName,
          lastName: lastName,
          roleTitle: roleTitle,
          emailAddress: emailAddress,
          mobileNumber: mobileNumber,
          state: state,
          city: city,
          isCoach: isCoach,
          isClient: isClient,
          isParticipant: isParticipant,
          isTenantOrgAdmin: isTenantOrgAdmin,
          registeredUtc: registeredUtc,
          selfRegisteredAsRoleId: selfRegisteredAsRoleId
        );

        int newUserId = GetScalarQueryInt(
          trans, @"
          INSERT INTO sv_User (OrgId, LoginName, FirstName, LastName, Email, Mobile, RoleTitle, State, City, IsAlbertCoach, IsClient, IsParticipant, RegisteredUtc, SelfRegisteredAsRoleId)
          OUTPUT INSERTED.UserId
          VALUES (@OrgId, @LoginName, @FirstName, @LastName, @Email, @Mobile, @RoleTitle, @State, @City, @IsAlbertCoach, @IsClient, @IsParticipant, @RegisteredUtc, @SelfRegisteredAsRoleId)",
          NewSqlParameter("OrgId", newUserStub.OrgId),
          NewSqlParameter("LoginName", (string)null),
          NewSqlParameter("FirstName", newUserStub.FirstName),
          NewSqlParameter("LastName", newUserStub.LastName),
          NewSqlParameter("Email", newUserStub.EmailAddress),
          NewSqlParameter("Mobile", newUserStub.MobileNumber),
          NewSqlParameter("RoleTitle", newUserStub.RoleTitle),
          NewSqlParameter("State", newUserStub.State),
          NewSqlParameter("City", newUserStub.City),
          NewSqlParameter("IsAlbertCoach", newUserStub.IsAbleCoach),
          NewSqlParameter("IsClient", newUserStub.IsAbleClient),
          NewSqlParameter("IsParticipant", newUserStub.IsParticipant),
          NewSqlParameter("RegisteredUtc", newUserStub.RegisteredUtc),
          NewSqlParameter("SelfRegisteredAsRoleId", newUserStub.SelfRegisteredAsRoleId)
        );

        newUserStub.UserId = newUserId;
        return newUserStub;
      }

      public static Guid CreatePasswordReset(int userId) {

        DeletePasswordReset(userId);

        return (Guid)GetScalarQuery(@"
          INSERT INTO id_PasswordReset (UserId)
          OUTPUT INSERTED.ResetId
          VALUES (@UserId)",
        NewSqlParameter("UserId", userId));
      }

      public static AbleUserInfo GetPasswordResetUserInfoOrNull(Guid resetId) {

        // First delete any expired password reset requests.
        DeleteExpiredPasswordResets();

        int userId = 0;
        Query(
          "SELECT UserId FROM id_PasswordReset WHERE ResetId = @ResetId",
          dr => { userId = dr.GetInt("UserId"); },
          NewSqlParameter("ResetId", resetId)
        );

        if (userId == 0) return null;
        return GetUserByIdOrNull(userId, RegisteredFilter.Any);
      }

      // Note this will throw an error if the user is required by related records so can't be deleted.
      public static void DeleteUser(int userId) {
        GetNonQueryInt($@"
          DELETE FROM {DbTable.User}
          WHERE UserId = @UserId",
          NewSqlParameter("UserId", userId)
        );
      }

      public static bool RegisterNewUser(SqlTransaction trans,
        string firstName, string lastName, string emailAddress, string password, string newCompanyName,
        bool isCoach, bool isClient, bool isParticipant, int? selfRegisteredAsRoleId = null) {

        if (trans == null) throw new ArgumentNullException(nameof(trans), "Transaction required.");

        emailAddress = emailAddress.TrimWhitespace();
        if (!emailAddress.IsEmailAddress()) throw new ArgumentException("Email address is invalid.");

        // Add new user then update registration record with user id.

        UserRegistration.CreateRegistration(trans, firstName, lastName, emailAddress, null, null);

        var newUserStub = CreateAbleUserStub(
          trans: trans,
          orgId: ConfigHelper.AbleOrgId, // TODO: New user OrgId for Leaders & Clients should be a "placeholder" Org.
          firstName: firstName,
          lastName: lastName,
          roleTitle: "",
          emailAddress: emailAddress,
          mobileNumber: "",
          state: "",
          city: "",
          isCoach: isCoach,
          isClient: isClient,
          isParticipant: isParticipant,
          isTenantOrgAdmin: false,
          registeredUtc: DateTime.UtcNow,
          selfRegisteredAsRoleId: selfRegisteredAsRoleId);

        if (!RegisterExistingUser(trans, newUserStub, password)) return false;
        if (!UserRegistration.UpdateRegistrationUserId(trans, emailAddress, newUserStub.UserId)) return false;

        if (isCoach) {

          // For Partners, create new TenantOrg. TenantOrg names are globally unique.
          // A new Partner user can only create a new TenantOrg, not attach to an existing one.
          // Only 1 user is the TenantOrg "owner", determined by TenantOrgOwnerUserId.
          // However multiple users can be a tenant admin, determined by user.IsOrgAdmin.

          int tenantOrgId = TenantOrg.CreateTenantOrg(trans, new TenantOrg.TenantOrgInfo(newCompanyName, newUserStub.UserId));

          // Link user to company.
          UpdateTenantOrgId(trans, newUserStub, tenantOrgId);

          // Set user to be admin for company.
          UpdateIsTenantOrgAdmin(trans, newUserStub, true);

        } else {

          // For Leaders and Clients, create new ClientCompany. Names are unique for each OrgId.
          // A new Partner user can only create a new ClientCompany, not attach to an existing one.

          var companyInfo = ClientCompanies.CreateCompany(trans, ConfigHelper.AbleOrgId, newCompanyName);

          if (isClient) {
            // For clients, make new user the new company "owner".
            UpdateClientCompanyId(trans, newUserStub, companyInfo.CompanyId);
          } else if (isParticipant) {
            // For leaders,
            CreateSelfRegisteredCoachee(trans, newUserStub, companyInfo);
          }
        }

        return true;
      }

      private static void CreateSelfRegisteredCoachee(SqlTransaction trans, NewUserStub newUserStub, DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {

        // Create Project
        Projects.CreateProjectAndProgram(
          trans: trans,
          tenantOrgId: newUserStub.OrgId,
          companyId: companyInfo.CompanyId,
          projectName: $"{ConfigHelper.SelfCreatedUserDefaults.ProjectData.ProjectName} {newUserStub.GetFullName()}",
          preferredProgramName: $"Program for {newUserStub.GetFullName()}",
          canSelfSelectCoach: true,
          createdByUserId: null,
          newJobNumber: out string newJobNumber,
          newProgramJobId: out int newProgramJobId);

        if (newJobNumber.IsNullOrEmpty()) throw new ApplicationException($"CreateProjectAndProgram({companyInfo.CompanyId}, ...) returned blank JobNumber.");

        // Create Coachee
        var coacheeInfo = new DbHelper.AlbertCoachees.AlbertCoacheeInfo() {
          FirstName = newUserStub.FirstName,
          LastName = newUserStub.LastName,
          EmailAddress = newUserStub.EmailAddress,
          ProgramJobId = newProgramJobId,
          CompanyId = companyInfo.CompanyId,
          TenantOrgId = newUserStub.OrgId,
          ProgramStatusId = CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId,
          CoachUserId = ConfigHelper.UserId.Unassigned,
          SubscriptionUser = true
        };
        coacheeInfo.CoacheeId = AlbertCoachees.CreateCoachee(trans, coacheeInfo);
      }

      public static int CreateUserFromCoachee(SqlTransaction trans, AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

        if (coacheeInfo.EmailAddress.IsNullOrEmpty()) throw new ArgumentException("coacheeInfo.EmailAddress required");

        return GetScalarQueryInt(trans, @"
            INSERT INTO sv_User (OrgId, LoginName, FirstName, LastName, Email, Mobile, IsParticipant, ClientCompanyId)
            OUTPUT INSERTED.UserId
            VALUES (@OrgId, @LoginName, @FirstName, @LastName, @Email, @Mobile, @IsParticipant, @ClientCompanyId)",
            NewSqlParameter("@OrgId", coacheeInfo.TenantOrgId),
            NewSqlParameter("@LoginName", (string)null),
            NewSqlParameter("@FirstName", coacheeInfo.FirstName, 100),
            NewSqlParameter("@LastName", coacheeInfo.LastName, 100),
            NewSqlParameter("@Email", coacheeInfo.EmailAddress, 100),
            NewSqlParameter("@Mobile", coacheeInfo.MobilePhone, 50),
            NewSqlParameter("@IsParticipant", true),
            NewSqlParameter("@ClientCompanyId", coacheeInfo?.CompanyId)
          );
      }

      public static bool UpdateUserFromCoachee(
        SqlTransaction trans,
        AbleUser.UserForCoacheeComparison userForCoacheeComparison,
        AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

        if (coacheeInfo.UserId == null) throw new ArgumentException("coacheeInfo.UserId is null");
        if (coacheeInfo.EmailAddress.IsNullOrEmpty()) throw new ArgumentException("coacheeInfo.EmailAddress required");

        // If a matching user exists, keep their existing mobile number unless coachee's number differs.
        if (userForCoacheeComparison != null && coacheeInfo.MobilePhone.IsNullOrEmptyOrWhitespace()) {
          coacheeInfo.MobilePhone = userForCoacheeComparison.Mobile;
        }

        return GetNonQueryInt(trans, @"
          UPDATE sv_User SET
            OrgId = @OrgId,
            FirstName = @FirstName,
            LastName = @LastName,
            Email = @Email,
            Mobile = @Mobile,
            DateOfBirth = @DateOfBirth,
            RoleTitle = @RoleTitle,
            City = @City,
            Country = @Country,
            OrgRoleId = @OrgRoleId
          WHERE
            UserId = @UserId",
          NewSqlParameter("@UserId", coacheeInfo.UserId),
          NewSqlParameter("@OrgId", coacheeInfo.TenantOrgId),
          NewSqlParameter("@FirstName", coacheeInfo.FirstName, 100),
          NewSqlParameter("@LastName", coacheeInfo.LastName, 100),
          NewSqlParameter("@Email", coacheeInfo.EmailAddress, 100),
          NewSqlParameter("@Mobile", coacheeInfo.MobilePhone, 50),
          NewSqlParameter("DateOfBirth", coacheeInfo.UserActivity?.DateOfBirth),
          NewSqlParameter("RoleTitle", coacheeInfo.UserActivity?.RoleTitle, 100),
          NewSqlParameter("City", coacheeInfo.UserActivity?.City, 100),
          NewSqlParameter("Country", coacheeInfo.UserActivity?.Country, 100),
          NewSqlParameter("OrgRoleId", coacheeInfo.UserActivity?.OrgRoleId)
        ) > 0;
      }

      public static bool UpdateIsParticipant(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, bool isParticipant) {

        if (userBasicInfo == null) return false;

        bool updated = UpdateIsParticipant(trans, userBasicInfo.UserId, isParticipant);

        if (updated) userBasicInfo.IsParticipant = isParticipant;

        return updated;
      }

      public static bool UpdateIsParticipant(SqlTransaction trans, int userId, bool isParticipant) {

        return GetNonQueryInt(trans, @"
          UPDATE sv_User
          SET IsParticipant = @IsParticipant
          WHERE UserId = @UserId",
          NewSqlParameter("@UserId", userId),
          NewSqlParameter("IsParticipant", isParticipant)
        ) == 1;
      }

      public enum UserRoleEnum { Coach, Client, Participant }

      public class InviteeBasicInfo {

        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string EmailAddress { get; private set; }
        public UserRoleEnum UserRole { get; private set; }
        public int? OrgId { get; private set; }
        public string MobileNumber { get; private set; }
        public string RoleTitle { get; private set; }
        public string State { get; private set; }
        public string City { get; private set; }

        public InviteeBasicInfo(
          string firstName, string lastName, string emailAddress,
          UserRoleEnum userRole, int? orgId = null) {

          if (firstName.IsNullOrEmpty()) throw new ArgumentException("firstName required.");
          if (lastName.IsNullOrEmpty()) throw new ArgumentException("lastName required.");
          if (emailAddress.IsNullOrEmpty()) throw new ArgumentException("emailAddress required.");

          FirstName = firstName;
          LastName = lastName;
          EmailAddress = emailAddress;
          UserRole = userRole;
          OrgId = orgId;
        }

        public void AddOptionalExtraDetails(string mobileNumber, string roleTitle, string state, string city) {
          MobileNumber = mobileNumber;
          RoleTitle = roleTitle;
          State = state;
          City = city;
        }
      }

      public static AbleUserBasicInfo CreateInviteeUser(SqlTransaction trans, AbleUserBasicInfo invitedByUser, InviteeBasicInfo inviteeBasicInfo) {

        // If insert fails on InviteCode unique key, keep making a new random code until successful.

        while (true) {
          try {
            string inviteCode = GenerateInviteCode();
            int newUserId = GetScalarQueryInt(trans, @"
              INSERT INTO sv_User
                (OrgId, LoginName, FirstName, LastName, Email, InviteCode, InvitedByUserId, IsAlbertCoach, IsClient, IsParticipant,
                  Mobile, RoleTitle, State, City, RegisteredUtc)
              OUTPUT INSERTED.UserId
              VALUES
              (@OrgId, @LoginName, @FirstName, @LastName, @Email, @InviteCode, @InvitedByUserId, @IsAlbertCoach, @IsClient, @IsParticipant,
                @Mobile, @RoleTitle, @State, @City, @RegisteredUtc)",
              NewSqlParameter("OrgId", invitedByUser.OrgId),
              NewSqlParameter("LoginName", (string)null),
              NewSqlParameter("FirstName", inviteeBasicInfo.FirstName),
              NewSqlParameter("LastName", inviteeBasicInfo.LastName),
              NewSqlParameter("Email", inviteeBasicInfo.EmailAddress),
              NewSqlParameter("InviteCode", inviteCode),
              NewSqlParameter("InvitedByUserId", invitedByUser.UserId),
              NewSqlParameter("IsAlbertCoach", inviteeBasicInfo.UserRole == UserRoleEnum.Coach),
              NewSqlParameter("IsClient", inviteeBasicInfo.UserRole == UserRoleEnum.Client),
              NewSqlParameter("IsParticipant", inviteeBasicInfo.UserRole == UserRoleEnum.Participant),
              NewSqlParameter("Mobile", inviteeBasicInfo.MobileNumber),
              NewSqlParameter("RoleTitle", inviteeBasicInfo.RoleTitle),
              NewSqlParameter("State", inviteeBasicInfo.State),
              NewSqlParameter("City", inviteeBasicInfo.City),
              NewSqlParameter("RegisteredUtc", (DateTime?)null)
            );
            return GetBasicInfoById(trans, newUserId, RegisteredFilter.Any);
          } catch (SqlException ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation(nameof(CreateInviteeUser))
              .WithOperationContext("GenerateUniqueInviteCode")
              .WithProperty(DalApplicationInsightsConstants.InvitedByUserId, invitedByUser?.UserId)
              .WithProperty(DalApplicationInsightsConstants.InviteeEmail, inviteeBasicInfo?.EmailAddress)
              .WithProperty(DalApplicationInsightsConstants.InviteeFirstName, inviteeBasicInfo?.FirstName)
              .WithProperty(DalApplicationInsightsConstants.InviteeLastName, inviteeBasicInfo?.LastName)
              .WithProperty(DalApplicationInsightsConstants.IsDuplicateKey, IsDuplicateKeyError(ex))
              .Track();

            if (!IsDuplicateKeyError(ex)) throw ex; // Anything other than unique key violation.
          }
        }
      }

      // For the invited user, set the invite code and invite date.
      // The invite code should only be set ONCE per user, as it is
      // included in correspondence, so if it is already set then don't change it.
      public static void UpdateInviteDetails(AbleUserBasicInfo inviteeInfo, int invitedByUserId, DateTime? invitedUtc = null) {

        DateTime? setInvitedUtc = null;
        string setInviteCode = null;
        int? setInvitedByUserId = null;
        var assignmentsSql = new List<string>();
        var inviteParameters = new List<SqlParameter>();

        // If invitedUtc is null, ignore (i.e. don't set it to null).
        if (invitedUtc != null && invitedUtc != inviteeInfo.InvitedUtc) {
          setInvitedUtc = DateTime.UtcNow;
          assignmentsSql.Add("InvitedUtc = @InvitedUtc");
          inviteParameters.Add(NewSqlParameter("InvitedUtc", setInvitedUtc));
        }

        if (inviteeInfo.InvitedByUserId == null || inviteeInfo.InvitedByUserId != invitedByUserId) {
          setInvitedByUserId = invitedByUserId;
          assignmentsSql.Add("InvitedByUserId = @InvitedByUserId"); // Add Update invited by user to sql query.
          inviteParameters.Add(NewSqlParameter("InvitedByUserId", setInvitedByUserId));
        }

        if (inviteeInfo.InviteCode.IsNullOrEmpty()) {
          setInviteCode = GenerateInviteCode();
          assignmentsSql.Add("InviteCode = @InviteCode"); // Add Update invite code to sql query.
          inviteParameters.Add(NewSqlParameter("InviteCode", setInviteCode));
        }

        if (assignmentsSql.Count > 0) {
          // Update invite details.
          // Duplicate key error means new invite code already exists (should be extremely rare) so create another and try again.
          var sql = $"UPDATE sv_User SET {assignmentsSql.Join(",")} WHERE UserId = @UserId";
          inviteParameters.Add(NewSqlParameter("UserId", inviteeInfo.UserId));
          while (true) {
            try {
              GetNonQueryInt(sql, inviteParameters.ToArray());
              break; // Successful update, exit loop.
            } catch (SqlException ex) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .WithOperation(nameof(UpdateInviteDetails))
                .WithOperationContext("GenerateUniqueInviteCode")
                .WithProperty(DalApplicationInsightsConstants.InviteeUserId, inviteeInfo?.UserId)
                .WithProperty(DalApplicationInsightsConstants.InvitedByUserId, invitedByUserId)
                .WithProperty(DalApplicationInsightsConstants.HasInviteCode, !inviteeInfo?.InviteCode.IsNullOrEmpty())
                .WithProperty(DalApplicationInsightsConstants.IsDuplicateKey, IsDuplicateKeyError(ex))
                .Track();

              if (!IsDuplicateKeyError(ex)) throw ex; // Throw anything other than dup key error.
              setInviteCode = GenerateInviteCode(); // Try new code.
            }
          }
          // Update DTO with changed info.
          if (setInviteCode != null) inviteeInfo.InviteCode = setInviteCode;
          if (setInvitedUtc != null) inviteeInfo.InvitedUtc = setInvitedUtc;
          if (setInvitedByUserId != null) inviteeInfo.InvitedByUserId = setInvitedByUserId;
        }
      }

      public static bool UpdateLastInviteReminderSent(AbleUserBasicInfo userBasicInfo, DateTime inviteReminderDateUtc) {
        int rowsUpdated = UpdateLastInviteReminderSent(userBasicInfo.UserId, inviteReminderDateUtc, userBasicInfo.InvitedUtc == null);
        if (rowsUpdated > 0) {
          userBasicInfo.LastInviteReminderSentUtc = inviteReminderDateUtc;
          return true;
        }
        return false;
      }

      public static bool UpdateLastInviteReminderSentByUserId(int userId, DateTime inviteReminderDateUtc) {
        return UpdateLastInviteReminderSent(userId, inviteReminderDateUtc, true) > 0;
      }

      private static int UpdateLastInviteReminderSent(int userId, DateTime inviteReminderDateUtc, bool setInitialInvitedUtc) {
        string setInvitedUtc = setInitialInvitedUtc ? ", InvitedUtc = @InvitedUtc" : "";
        return GetNonQueryInt($@"
          UPDATE sv_User
          SET LastInviteReminderSentUtc = @LastInviteReminderSentUtc
          {setInvitedUtc}
          WHERE UserId = @UserId",
          NewSqlParameter("LastInviteReminderSentUtc", inviteReminderDateUtc),
          NewSqlParameter("InvitedUtc", inviteReminderDateUtc),
          NewSqlParameter("UserId", userId)
        );
      }

      private static string GenerateInviteCode() {
        return Nanoid.Generate(ConfigHelper.UserInviteCodeAllowedCharacters, ConfigHelper.UserInviteCodeMaxLength);
      }

      public static bool UpdateInviteCode(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, string inviteCode) {
        int rowsUpdated = GetNonQueryInt(trans, @"
          UPDATE sv_User
          SET InviteCode = @InviteCode
          WHERE UserId = @UserId",
          NewSqlParameter("InviteCode", inviteCode),
          NewSqlParameter("UserId", userBasicInfo.UserId)
        );
        if (rowsUpdated > 0) {
          userBasicInfo.InviteCode = inviteCode;
          return true;
        }
        return false;
      }

      public static AbleUserBasicInfo GetUserByInviteCode(string inviteCode) {
        if (inviteCode.IsNullOrEmpty() || inviteCode.Length > ConfigHelper.UserInviteCodeMaxLength) return null;
        var list = GetBasicInfoList(null, "",
          "u.InviteCode = @InviteCode",
          RegisteredFilter.Any,
          NewSqlParameter("InviteCode", inviteCode));
        return list.IsNullOrEmpty() ? null : list[0];
      }

      public static bool RegisterExistingUser(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, string passwordPlainText) {

        if (trans == null) throw new ArgumentNullException(nameof(trans), "Transaction required.");

        if (!UpdatePassword(trans, userBasicInfo, passwordPlainText)
          || !UpdateRegisteredDate(trans, userBasicInfo, DateTime.UtcNow)
          || !UpdateInviteCode(trans, userBasicInfo, null))
          return false;

        if (userBasicInfo.IsAbleCoach) {
          var coach = AlbertCoaches.GetCoachInfo(trans, userBasicInfo.EmailAddress);
          if (coach != null) AlbertCoaches.UpdateIsPartnerActive(trans, coach);
        }

        return true;
      }

      public static bool UpdateRegisteredDate(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, DateTime registeredDateUtc) {
        int rowsUpdated = GetNonQueryInt(trans, @"
          UPDATE sv_User SET
            RegisteredUtc = @RegisteredUtc
          WHERE UserId = @UserId",
          NewSqlParameter("RegisteredUtc", registeredDateUtc),
          NewSqlParameter("UserId", userBasicInfo.UserId)
        );
        if (rowsUpdated > 0) {
          userBasicInfo.RegisteredUtc = registeredDateUtc;
          return true;
        }
        return false;
      }

      public static bool UpdateClientCompanyId(SqlTransaction trans, UserIdentity userBasicInfo, int? clientCompanyId) {

        if (userBasicInfo == null) return false;

        bool updated = GetNonQueryInt(trans, @"
          UPDATE sv_User SET
            ClientCompanyId = @ClientCompanyId
          WHERE UserId = @UserId",
          NewSqlParameter("ClientCompanyId", clientCompanyId),
          NewSqlParameter("UserId", userBasicInfo.UserId)
        ) == 1;

        if (updated) userBasicInfo.ClientCompanyId = clientCompanyId;

        return updated;
      }

      public static bool DeletePasswordReset(int userId) {
        return GetNonQueryInt(@"
          DELETE FROM id_PasswordReset
          WHERE UserId = @UserId",
          NewSqlParameter("UserId", userId)
        ) > 0;
      }

      public static void DeleteExpiredPasswordResets() {
        GetNonQueryInt($@"
          DELETE FROM id_PasswordReset
          WHERE DATEDIFF(MINUTE, ResetRequestedUtc, GETUTCDATE()) > @PasswordResetExpiryMins",
          NewSqlParameter("PasswordResetExpiryMins", PASSWORD_RESET_EXPIRY_MINS)
        );
      }

      private static PasswordHashInfo GeneratePasswordHash(string password) {

        int iterations = PASSWORD_HASH_ITERATIONS * PASSWORD_HASH_ITERATION_DEFAULT_MULTIPLIER;

        // Generate a random salt.
        var salt = new byte[PASSWORD_SALT_SIZE];
        new RNGCryptoServiceProvider().GetBytes(salt);

        // Hash password given salt and iterations.
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations);
        byte[] hash = pbkdf2.GetBytes(PASSWORD_HASH_SIZE);

        return new PasswordHashInfo(salt, PASSWORD_HASH_ITERATION_DEFAULT_MULTIPLIER, hash);
      }

      public static bool IsPasswordCorrect(string testPassword, string saltEncoded, string combinedHashString) {

        if (saltEncoded.IsNullOrEmpty() || combinedHashString.IsNullOrEmpty()) return false;

        var passwordHashInfo = new PasswordHashInfo(saltEncoded, combinedHashString);

        // Generate hash from test password, salt and iterations.

        var pbkdf2 = new Rfc2898DeriveBytes(testPassword, passwordHashInfo.Salt, PASSWORD_HASH_ITERATIONS * passwordHashInfo.IterationMultiplier);
        byte[] testHash = pbkdf2.GetBytes(PASSWORD_HASH_SIZE);
        return Convert.ToBase64String(testHash) == passwordHashInfo.HashEncoded ? true : false;
      }

      public static bool UpdatePassword(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, string passwordPlainText) {

        if (userBasicInfo == null) throw new ArgumentException("userBasicInfo required.");

        var hashInfo = GeneratePasswordHash(passwordPlainText);

        // Note also ensure plain-text password is removed.
        int rowsUpdated = GetNonQueryInt(trans, $@"
          UPDATE {DbTable.User}
          SET PasswordSalt = @PasswordSalt,
              PasswordHashed = @PasswordHashed,
              Password = NULL
          WHERE UserId = @UserId",
          NewSqlParameter("UserId", userBasicInfo.UserId),
          NewSqlParameter("PasswordSalt", hashInfo.SaltEncoded, 50),
          NewSqlParameter("PasswordHashed", hashInfo.CombinedHashString, 50)
        );

        if (rowsUpdated == 0) return false;

        userBasicInfo.SetPasswordHashed(hashInfo);
        return true;
      }

      public static bool UpdateDeletedUtc(SqlTransaction trans, AbleUserBasicInfo userBasicInfo, DateTime? deletedUtc) {

        if (userBasicInfo == null) return false;

        bool updated = GetNonQueryInt(trans, @"
          UPDATE sv_User
          SET DeletedUtc = @DeletedUtc
          WHERE UserId = @UserId",
          NewSqlParameter("UserId", userBasicInfo.UserId),
          NewSqlParameter("DeletedUtc", deletedUtc)
        ) == 1;

        if (updated) userBasicInfo.DeletedUtc = deletedUtc;

        return updated;
      }

      public static int GetInvitedByUserId(AbleUserBasicInfo inviteeUserInfo) {
        if (inviteeUserInfo.InvitedByUserId != null && inviteeUserInfo.InvitedByUserId != ConfigHelper.UserId.Unassigned) {
          return inviteeUserInfo.InvitedByUserId.Value;
        } else if (inviteeUserInfo.LatestCoachingInfo?.PLCUserId != null && inviteeUserInfo.LatestCoachingInfo.PLCUserId != ConfigHelper.UserId.Unassigned) {
          return inviteeUserInfo.LatestCoachingInfo.PLCUserId.Value;
        }
        return ConfigHelper.UserId.Unassigned;
      }

      public static UserForCoacheeComparison GetUserForCoacheeComparison(string emailAddress, RegisteredFilter registeredFilter = RegisteredFilter.Any) {
        return GetUserForCoacheeComparison(registeredFilter, "u.Email = @Email", NewSqlParameter("Email", emailAddress, 100));
      }

      public static UserForCoacheeComparison GetUserForCoacheeComparison(int userId, RegisteredFilter registeredFilter = RegisteredFilter.Any) {
        return GetUserForCoacheeComparison(registeredFilter, "u.UserId = @UserId", NewSqlParameter("UserId", userId));
      }

      private static UserForCoacheeComparison GetUserForCoacheeComparison(RegisteredFilter registeredFilter, string whereClause, params SqlParameter[] sqlParameters) {

        if (registeredFilter == RegisteredFilter.OnlyRegistered) {
          whereClause += " AND u.RegisteredUtc IS NOT NULL";
        } else if (registeredFilter == RegisteredFilter.OnlyUnregistered) {
          whereClause += " AND u.RegisteredUtc IS NULL";
        }

        AbleUser.UserForCoacheeComparison cu = null;

        Query($@"
          SELECT
            u.OrgId, u.UserId, u.UserGuid, u.FirstName, u.LastName, u.Email,
            u.Mobile, u.DateOfBirth, u.City, u.Country, u.OrgRoleId, u.RoleTitle, u.IsOrgAdmin,
            org.OrgOwnerUserId
          FROM sv_User u
          INNER JOIN sv_Organisation org ON org.OrgId = u.OrgId
          WHERE {whereClause}",
          dr => {
            cu = new UserForCoacheeComparison(dr);
          },
          sqlParameters
        );

        return cu;
      }

      public static List<AbleUserBasicInfo> GetUsersForDeletion() {

        return GetBasicInfoList(null, "",
          "u.DeletionRequestedUtc BETWEEN '2000-01-01' AND DATEADD(DAY, -@UserDeletionRequestDelayDays, GETUTCDATE()) AND u.DeletedUtc IS NULL",
          RegisteredFilter.Any,
          NewSqlParameter("UserDeletionRequestDelayDays", ConfigHelper.UserDeletionRequestDelayDays)
        );
      }

      public static PartnerActionsInfo GetPartnerActionsInfo(int userId) {

        PartnerActionsInfo actionsInfo = null;

        Query(null, $@"
          SELECT
            -- Profile is complete if Name, City, Country and TimeZoneId are filled in
            CASE WHEN
            u.FirstName IS NOT NULL AND LEN(LTRIM(RTRIM(u.FirstName))) > 0
            AND u.LastName IS NOT NULL AND LEN(LTRIM(RTRIM(u.LastName))) > 0
            AND u.AbleWebProfileUrl IS NOT NULL AND LEN(LTRIM(RTRIM(u.LastName))) > 0
            AND u.City IS NOT NULL AND LEN(LTRIM(RTRIM(u.City))) > 0
            AND u.Country IS NOT NULL AND LEN(LTRIM(RTRIM(u.Country))) > 0
            AND u.TimeZoneId IS NOT NULL AND LEN(LTRIM(RTRIM(u.TimeZoneId))) > 0
            THEN 1 ELSE 0 END AS HasCompletedProfile,

            -- User has a short bio and has completed at least one of the bios from the dedicated table.
            CASE
              WHEN NULLIF(LTRIM(RTRIM(u.AbleBioShort)),'') IS NOT NULL
              AND EXISTS (SELECT 1 FROM al_PartnerBio p WHERE p.PartnerUserId = u.UserId)
              THEN 1 ELSE 0
            END AS HasFilledBios,

            -- User Joined able with an invitation
            CASE WHEN u.InvitedUtc IS NOT NULL THEN 1 ELSE 0 END AS HasBeenInvitedToThePlatform,

            -- User has invited others to the company
            CASE WHEN EXISTS (
              SELECT 1 FROM sv_User uu WHERE uu.InvitedByUserId = u.UserId
            ) THEN 1 ELSE 0 END AS HasInvitedUsersToCompany,

            -- User has created microlearnings if they are the author of at least one
            CASE WHEN EXISTS (
              SELECT 1 FROM al_Content c WHERE c.AuthorUserId = u.UserId AND c.DeletedUtc IS NULL
            ) THEN 1 ELSE 0 END AS HasCreatedMicrolearnings,

            -- User has participants if they are the coach of at least one (not deleted)
            CASE WHEN EXISTS (
              SELECT 1 FROM al_Coachees c WHERE c.CoachUserId = u.UserId AND c.DeletedUtc IS NULL
            ) THEN 1 ELSE 0 END AS HasParticipants,

            -- User has created projects if the CreatedByUserId is their UserId
            CASE WHEN EXISTS (
              SELECT 1 FROM al_Project pr WHERE pr.CreatedByUserId = u.UserId
            ) THEN 1 ELSE 0 END AS HasCreatedProjects,

            -- The user has Quotes when they have at least one quote where they are the owner
            CASE WHEN EXISTS (
              SELECT 1 FROM al_Quote q WHERE q.OwnerUserId = u.UserId
            ) THEN 1 ELSE 0 END AS HasQuotes,

            -- User has quotes that need atention in two cases:
            -- 1. The Quote is in ClientSigning status and hasn't been accepted
            -- 2. The Quote is not Accepted and not Lost status and ClientAcceptedAmount is 0
            -- ALWAYS when Userid is the owner or the user contact
            CASE WHEN EXISTS (
              SELECT 1
              FROM al_Quote q
              INNER JOIN al_Project prj ON prj.JobNumber = q.JobNumber
              INNER JOIN al_UserProjectAccess pa ON pa.ProjectId = prj.ProjectId
              WHERE
                ((q.QuoteStatusId = @QuoteStatus_ClientSigning AND q.ClientAcceptedUtc IS NULL) OR (q.QuoteStatusId <> @QuoteStatus_Accepted AND q.QuoteStatusId <> @QuoteStatus_Lost AND q.ClientAcceptedAmount = 0))
                AND (q.OwnerUserId = u.UserId OR pa.UserId = u.UserId)
            ) THEN 1 ELSE 0 END AS HasQuotesNeedingAttention,

          -- User Company profile is complete:
          -- 1. If the user is not a client, the client ClientCompanyId will be null, then mark is as complete as they have nothing to do.
          -- 2. If the user has ClientCompanyId. Check if their corresponding Company has a Number of Staff and has selected a sector
          CASE WHEN u.ClientCompanyId IS NOT NULL THEN (
            SELECT
              CASE WHEN cmp.NumberOfStaff IS NOT NULL AND cmp.SectorId IS NOT NULL THEN 1 ELSE 0 END
            FROM sv_SurveyCompany cmp
            WHERE cmp.SvCompanyId = u.ClientCompanyId
          ) ELSE 1 END AS IsCompanyProfileComplete,

          -- User has Workshops to confirm if meets all the following:
          -- They are the PC or PLC of a program
          -- The ProgramStatusId is Setup or Active
          -- The WorshopStatusId is Estimates, NotPlanned or Postponed
          CASE WHEN EXISTS (
            SELECT 1
            FROM ev_WorkshopEvent w
            INNER JOIN id_Job j ON j.JobId = w.ProgramJobId
            WHERE (w.WorkshopStatusId = @WorkshopStatus_Estimated OR w.WorkshopStatusId = @WorkshopStatus_NotPlanned OR w.WorkshopStatusId = @WorkshopStatus_Postponed)
              AND (j.ProgramStatusId = @ProgramStatus_Setup OR j.ProgramStatusId = @ProgramStatus_Active)
              AND (j.LeadConsultantUserId = u.UserId OR j.ProjectCoordinatorUserId = u.UserId)
          ) THEN 1 ELSE 0 END AS HasWorkshopsToConfirm,

          -- User has participants without coach if meets all the following:
          -- They are the PC or PLC of a program
          -- The Status of the participant is Onboarding
          -- The current CoachUserId is Unassigned
          -- They are the PC or PLC of a program
          CASE WHEN EXISTS (
            SELECT 1
            FROM al_Coachees ac
            INNER JOIN id_Job j ON j.JobId = ac.ProgramJobId
            WHERE ac.ProgramStatusId = @CoacheeStatus_Onboarding
              AND ac.CoachUserId = @CoachId_Unassigned
              AND ac.CoachingTypeId <> @CoachingType_NoCoaching
              AND ac.DeletedUtc IS NULL
              AND (j.LeadConsultantUserId = u.UserId OR j.ProjectCoordinatorUserId = u.UserId)
              AND (j.ProgramStatusId = @ProgramStatus_Setup OR j.ProgramStatusId = @ProgramStatus_Active)
          ) THEN 1 ELSE 0 END AS HasParticipantsWithoutCoach,

          -- User has Stuck participants when they have users with more allocated sessions than they have booked.
          CASE WHEN EXISTS (
            SELECT 1
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
            WHERE ac.CoachUserId = u.UserId
              AND ac.ProgramStatusId = @ProgramStatus_Active  -- Active status
              AND (cs.LatestSessionApptDateUtc IS NULL OR cs.LatestSessionApptDateUtc < GETUTCDATE()) -- No sessions or none booked in future
              AND ac.NextBookingTargetDateUtc < GETUTCDATE()  -- Next booking date is also in the past
              AND ac.DeletedUtc IS NULL AND ac.SessionsAllocated > ac.SessionsBooked
          ) THEN 1 ELSE 0 END AS HasStuckParticipants,

          -- User has Quotes for Program Setup when quote items total is different than components
          CASE WHEN EXISTS (
            SELECT 1
            FROM id_Job j
            INNER JOIN al_Quote q on q.JobNumber = j.JobNumber
            INNER JOIN al_Project prj on prj.JobNumber = j.JobNumber
            CROSS APPLY (
              SELECT qi.QuoteItemId, qi.UnitPrice * qi.Quantity AS QuoteItemTotal
              FROM al_QuoteItem qi
              WHERE qi.QuoteId = q.QuoteId
            ) AS qi
            CROSS APPLY (
              SELECT SUM(cmp.ComponentPrice) AS ComponentTotalPrice
              FROM al_Component cmp
              WHERE cmp.QuoteItemId = qi.QuoteItemId
            ) AS cmp
            WHERE qi.QuoteItemTotal <> cmp.ComponentTotalPrice
              AND j.ProgramStatusId = @ProgramStatus_Setup
              AND (j.ProjectCoordinatorUserId = u.UserId OR j.LeadConsultantUserId = u.UserId)
          ) THEN 1 ELSE 0 END AS HasQuotesProgramSetup

          FROM sv_User u
           WHERE u.UserId = @UserId",
          dr => {
            actionsInfo = new PartnerActionsInfo(dr);
          },
          Common.NewSqlParameter("UserId", userId),
          Common.NewSqlParameter("@CoacheeStatus_Onboarding", CoacheeProgramStatus.GetStatus_Onboarding().ProgramStatusId),
          Common.NewSqlParameter("@CoachId_Unassigned", ConfigHelper.UserId.Unassigned),
          Common.NewSqlParameter("@ProgramStatus_Setup", AlbertProgramStatus.Ids.Setup),
          Common.NewSqlParameter("@ProgramStatus_Active", AlbertProgramStatus.Ids.Active),
          Common.NewSqlParameter("@CoachingType_NoCoaching", AlbertCoachingTypes.ReservedType_NoCoaching.CoachingTypeId),
          Common.NewSqlParameter("@QuoteStatus_ClientSigning", DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.client).QuoteStatusId),
          Common.NewSqlParameter("@QuoteStatus_Lost", DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.lost).QuoteStatusId),
          Common.NewSqlParameter("@QuoteStatus_Accepted", DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId),
          Common.NewSqlParameter("@WorkshopStatus_Estimated", WorkshopStatus.WorkshopStatus_Estimated.WorkshopStatusId),
          Common.NewSqlParameter("@WorkshopStatus_NotPlanned", WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId),
          Common.NewSqlParameter("@WorkshopStatus_Postponed", WorkshopStatus.WorkshopStatus_Postponed.WorkshopStatusId)
        );

        return actionsInfo;
      }

      // Redact user data.
      // Replace email address with dummy address which must be unique.
      public static void RedactUserData(AbleUserBasicInfo user) {

        GetNonQueryInt($@"

          DECLARE @RedactedEmailAddr VARCHAR(50) = CONCAT('redacted-', @UserId, '@integral.global');
          DECLARE @OriginalEmailAddr VARCHAR(50) = (SELECT Email FROM sv_User WHERE UserId = @UserId);

          BEGIN TRANSACTION;

          UPDATE sv_User
          SET LoginName = NULL,
              Password = NULL,
              FirstName = '',
              LastName = '',
              RoleTitle = NULL,
              LastLoginUTC = NULL,
              Email = @RedactedEmailAddr,
              Mobile = NULL,
              TimeZoneId = '',
              CalendlyUrlName = NULL,
              AbleBioShort = NULL,
              AbleWebProfileUrl = NULL,
              PasswordSalt = NULL,
              PasswordHashed = NULL,
              APIToken = NULL,
              UserContractTypeId = NULL,
              ProfilePhotoUrl = NULL,
              SignaturePhotoUrl = NULL,
              State = NULL,
              City = NULL,
              TimeZoneIdIANA = '',
              TimeZoneIdWindows = '',
              InviteCode = NULL,
              InvitedUtc = NULL,
              InvitedByUserId = NULL,
              RegisteredUtc = NULL,
              ClientCompanyId = NULL,
              CalendlyTenantAPIKey = NULL,
              CalendlyAccountEmail = NULL,
              OrgRoleId = NULL,
              PartnerActivatedUtc = NULL,
              CompanyDeptId = NULL,
              LastAIMessageSentUtc = NULL,
              Country = NULL,
              DateOfBirth = NULL,
              DeletedUtc = GETUTCDATE()
          WHERE UserId = @UserId;

          UPDATE al_AIMessage
          SET MessageBody = NULL
          WHERE UserId = @UserId;

          DELETE FROM cal
          FROM al_CalendlyPayloads cal
          INNER JOIN al_Coachees ac ON ac.CoacheeId = cal.CoacheeId OR ac.EmailAddress = cal.CoacheeEmail
          WHERE ac.UserId = @UserId;

          UPDATE comms
          SET AIMessageSummary = NULL
          FROM al_CoacheeAIComms comms
          INNER JOIN al_Coachees ac ON ac.CoacheeId = comms.CoacheeId
          WHERE ac.UserId = @UserId;

          UPDATE al_Coachees
          SET PersonId = NULL,
              FirstName = NULL,
              LastName = NULL,
              EmailAddress = @RedactedEmailAddr,
              CompanyId = NULL,
              MobilePhone = NULL,
              TimeZoneId = NULL,
              IANATimeZone = NULL,
              CoacheeNotes = NULL,
              DeletedUtc = GETUTCDATE(),
              PrivateCoachNote = NULL
          WHERE UserId = @UserId;

          UPDATE eh
          SET eh.AddrFrom = IIF(AddrFrom = @OriginalEmailAddr, @RedactedEmailAddr, eh.AddrFrom),
              eh.AddrTo = IIF(AddrTo = @OriginalEmailAddr, @RedactedEmailAddr, eh.AddrTo),
              eh.Subject = NULL
          FROM al_EmailHistory eh
          INNER JOIN al_Coachees ac ON ac.CoacheeId = eh.RecipientCoacheeId OR ac.CoacheeId = eh.ReceivedFromCoacheeId
          WHERE ac.UserId = @UserId;

          DELETE FROM al_PartnerTagToUser
          WHERE UserId = @UserId;

          UPDATE al_UserAISummary
          SET AISummaryText = NULL,
              AIChatContext = NULL
          WHERE UserId = @UserId;

          UPDATE al_UserContract
          SET PostalAddress1 = NULL,
              PostalAddress2 = NULL,
              PostalPostCode = NULL,
              PostalCountry = NULL,
              IDDateOfBirth = NULL,
              IDLicenseOrPassport = NULL,
              IDCountryOfIssue = NULL,
              BankAccountBSB = NULL,
              BankAccountNumber = NULL,
              NextOfKinFullName = NULL,
              NextOfKinMobileNumber = NULL,
              ABNNumber = NULL,
              BusinessEntityName = NULL
          WHERE UserId = @UserId;

          UPDATE al_UserLoginSession
          SET SettingsJson = NULL
          WHERE UserId = @UserId;

          UPDATE al_UserRegistration
          SET BrowserTimeZone = NULL,
              FirstName = NULL,
              LastName = NULL,
              CompanyName = NULL,
              EmailAddress = NULL
          WHERE AbleUserId = @UserId;

          UPDATE cs
          SET cs.MgrNotes = NULL,
              cs.CoachNotes = NULL,
              cs.ApptNotes = NULL,
              cs.ApptEmailText = NULL,
              cs.ApptCancelReason = NULL,
              cs.ApptRecheduleReason = NULL
          FROM id_CoachingSession cs
          INNER JOIN al_Coachees ac ON ac.CoacheeId = cs.AbleCoacheeId
          WHERE ac.UserId = @UserId;

          UPDATE ta
          SET ta.ta_textanswer = NULL,
              ta.ta_textfollowup = NULL
          FROM sv_360_TextAnswers ta
          INNER JOIN sv_Answers sa ON sa.AnswerId = ta.AnswerId
          INNER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
          INNER JOIN sv_User su ON su.UserId = sp.UserId OR su.Email = sp.Email
          WHERE su.UserId = @UserId;

          UPDATE ps
          SET AICoachSummaryText = NULL,
              AICoachSummaryPrompt = NULL,
              AICoachLongFormText = NULL,
              AICoachLongFormPrompt = NULL,
              AIProfileSummaryText = NULL
          FROM sv_ParticipantAICoachSummary ps
          INNER JOIN sv_360_Participants sp ON sp.PartId = ps.ParticipantId
          INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
          WHERE ac.UserId = @UserId;

          COMMIT TRANSACTION;",

          NewSqlParameter("UserId", user.UserId)
        );
      }

      public class UserForCoacheeComparison {

        public int TenantOrgId { get; set; }
        public int TenantOrgOwnerUserId { get; set; }
        public bool IsTenantAdmin { get; set; }

        public int UserId { get; set; }
        public Guid UserGuid { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string RoleTitle { get; set; }
        public string Mobile { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? OrgRoleId { get; set; }

        public UserForCoacheeComparison(SqlDataReader dr) {

          TenantOrgId = dr.GetInt("OrgId");
          TenantOrgOwnerUserId = dr.GetInt("OrgOwnerUserId");
          IsTenantAdmin = dr.GetBoolFromInt("IsOrgAdmin");
          OrgRoleId = dr.GetIntOrNull("OrgRoleId");

          UserId = dr.GetInt("UserId");
          UserGuid = dr.GetGuid("UserGuid");
          FirstName = dr.GetString("FirstName");
          LastName = dr.GetString("LastName");
          Email = dr.GetString("Email");
          RoleTitle = dr.GetString("RoleTitle");
          Mobile = dr.GetString("Mobile");
          City = dr.GetString("City");
          Country = dr.GetString("Country");
          DateOfBirth = dr.GetDateTimeOrNull("DateOfBirth");
        }
      }

      public class PasswordHashInfo {

        public byte[] Salt { get; private set; }
        public byte[] Hash { get; private set; }
        public string SaltEncoded { get; private set; }
        public string HashEncoded { get; private set; }
        public int IterationMultiplier { get; private set; }
        public string CombinedHashString { get; private set; } // Combined iteration multiplier + delimiter + encoded hash string. This is stored in db.

        public PasswordHashInfo(byte[] salt, int iterationMultiplier, byte[] hash) {

          this.Salt = salt;
          this.Hash = hash;
          this.SaltEncoded = Convert.ToBase64String(salt);
          this.HashEncoded = Convert.ToBase64String(hash);
          this.IterationMultiplier = iterationMultiplier;
          // Combine iterations and multipler to get the actual string to store in the db.
          // This is what is stored in the db in "PasswordHashed".
          this.CombinedHashString = iterationMultiplier.ToString() + PASSWORD_HASH_ITERATION_MULTIPLIER_DELIMITER.ToString() + this.HashEncoded;
        }

        public PasswordHashInfo(string saltEncoded, string combinedHashString) {

          this.SaltEncoded = saltEncoded;
          this.Salt = Convert.FromBase64String(saltEncoded);

          // Separate stored string into separate iteration multiplier and hash.
          // Ensure compatibility with old version where multiplier was not stored with the hash (in which case use default multiplier)
          // New version puts a delimiter between the values, i.e. multiplier + delimiter + hash.

          if (combinedHashString.IndexOf(PASSWORD_HASH_ITERATION_MULTIPLIER_DELIMITER) > 0) {

            string[] splitHashString = combinedHashString.Split(PASSWORD_HASH_ITERATION_MULTIPLIER_DELIMITER);
            int iterationMultiplier;

            if (!int.TryParse(splitHashString[0], out iterationMultiplier)) {
              iterationMultiplier = PASSWORD_HASH_ITERATION_DEFAULT_MULTIPLIER;
            }

            this.IterationMultiplier = iterationMultiplier;
            this.HashEncoded = splitHashString[1];

          } else {

            this.HashEncoded = combinedHashString;
            this.IterationMultiplier = PASSWORD_HASH_ITERATION_DEFAULT_MULTIPLIER;
          }

          this.Hash = Convert.FromBase64String(this.HashEncoded);
        }
      }

      public static UserActivityInfo GetUserActivityInfo(SqlDataReader dr) {
        return new UserActivityInfo(dr);
      }

      public class UserIdentity {

        public int UserId { get; internal set; }
        public Guid UserGuid { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }

        public int OrgId { get; internal set; }
        public Guid OrgGuid { get; set; }
        public int? OrgOwnerUserId { get; internal set; }

        public bool IsAbleUser { get; protected set; }
        public bool IsAbleAdmin { get; protected set; }
        public bool IsTenantOrgAdmin { get; internal set; }
        public bool IsAbleCoach { get; protected set; }
        public bool IsAbleClient { get; internal set; }
        public bool IsParticipant { get; internal set; }
        public bool IsPartnerActive { get; internal set; }
        public bool IsIOSReportViewer { get; protected set; }
        public string ViewOnlyIOSReportUniqueId { get; protected set; }

        public DateTime? RegisteredUtc { get; internal set; }
        public DateTime? ProfileHiddenUtc { get; set; }
        public DateTime? DeletedUtc { get; internal set; }

        public int? ClientCompanyId { get; internal set; }

        public string GetFullName() => FirstName + " " + LastName;
        public bool IsRegistered => RegisteredUtc != null;
        public bool IsProfileHidden => ProfileHiddenUtc != null;
        public bool IsSoftDeleted => DeletedUtc != null;

        private ConfigHelper.UserRole _currentRole = ConfigHelper.UserRole.Unset;
        public ConfigHelper.UserRole CurrentRole => _currentRole;
        public void SetCurrentRole(ConfigHelper.UserRole currentRole) {
          _currentRole = currentRole;
        }

        internal void SetIdentityProps(SqlDataReader dr) {

          UserId = dr.GetInt("UserId");
          UserGuid = dr.GetGuid("UserGuid");
          FirstName = dr.GetString("FirstName");
          LastName = dr.GetString("LastName");
          EmailAddress = dr.GetString("Email");

          OrgId = dr.GetInt("OrgId");
          OrgGuid = dr.GetGuid("OrgGuid");
          OrgOwnerUserId = dr.GetIntOrNull("OrgOwnerUserId");

          IsAbleUser = dr.GetBoolFromInt("IsAbleUser");
          IsAbleAdmin = dr.GetBoolFromInt("IsAlbertAdmin");
          IsTenantOrgAdmin = dr.GetBoolFromInt("IsOrgAdmin");
          IsAbleCoach = dr.GetBoolFromInt("IsAlbertCoach");
          IsAbleClient = dr.GetBoolFromInt("IsClient");
          IsParticipant = dr.GetBoolFromInt("IsParticipant");
          IsPartnerActive = dr.GetBoolFromInt("IsPartnerActive");

          IsIOSReportViewer = dr.GetBoolFromInt("IsReportViewer"); // IOS reports only.
          ViewOnlyIOSReportUniqueId = dr.GetString("ViewOnlyReportUniqueId"); // IOS reports only.

          ProfileHiddenUtc = dr.GetDateTimeOrNull("ProfileHiddenUtc");
          RegisteredUtc = dr.GetDateTimeOrNull("RegisteredUtc");
          DeletedUtc = dr.GetDateTimeOrNull("DeletedUtc");

          ClientCompanyId = dr.GetIntOrNull("ClientCompanyId");
        }
      }

      public class AbleUserBasicInfo : UserIdentity {

        public string RoleTitle { get; set; }
        public string MobileNumber { get; set; }
        public string State { get; set; }
        public string City { get; set; }

        public DateTime CreatedUtc { get; protected set; }
        public string PasswordPlainText { get; protected set; }
        public string PasswordSalt { get; protected set; }
        public string PasswordHashed { get; protected set; }
        public DateTime? LastLoginUtc { get; internal set; }

        public string OrgName { get; set; }
        public decimal? PlatformFeePercent { get; internal set; }
        public string ClientCompanyName { get; internal set; }
        public int? ClientLeadUserId { get; internal set; }
        public string TimeZoneIdIana { get; private set; }
        public string TimeZoneIdWindows { get; private set; }
        public TimeZoneInfo TimeZoneInfo { get; private set; }

        public string InviteCode { get; internal set; }
        public DateTime? InvitedUtc { get; internal set; }
        public int? InvitedByUserId { get; internal set; }
        public DateTime? LastInviteReminderSentUtc { get; internal set; }
        public string ComputedSenderEmailName { get; protected set; }
        public string ComputedSenderEmailAddress { get; protected set; }

        public int? SelfRegisteredAsRoleId { get; protected set; }

        public int? UserContractTypeId { get; internal set; }
        public bool IncludeInPayRuns { get; private set; }

        public Subscriptions.User.UserSubscriptionInfo UserSubscription { get; private set; }
        public bool HasSubscription => UserSubscription != null;

        public RelatedCoacheeInfo LatestCoacheeInfo { get; private set; }  // Latest pax with this user id.
        public RelatedCoacheeInfo LatestCoachingInfo { get; private set; } // Latest coaching info for this user id (may be different to latest pax)

        private bool? _isUserTester = null; // initially not set
        private bool? _isUserEmailTester = null; // initially not set
        private bool? _sendEmailsToOverrideRecipient = null; // initially not set

        public bool IsOrgOwner => OrgOwnerUserId != null && UserId == OrgOwnerUserId;

        public AbleUserBasicInfo() { }

        public AbleUserBasicInfo(SqlDataReader dr) {
          SetBasicFields(dr);
        }

        internal void SetBasicFields(SqlDataReader dr) {

          SetIdentityProps(dr);

          RoleTitle = dr.GetString("RoleTitle");
          MobileNumber = dr.GetString("Mobile");
          State = dr.GetString("State");
          City = dr.GetString("City");

          CreatedUtc = dr.GetDateTime("CreatedUtc");
          OrgName = dr.GetString("OrgName");

          ClientCompanyName = dr.GetString("ClientCompanyName");
          ClientLeadUserId = dr.GetIntOrNull("ClientLeadUserId");

          TimeZoneIdIana = dr.GetString("TimeZoneIdIANA");
          SetTimeZoneIdIana(TimeZoneIdIana);

          PasswordPlainText = dr.GetString("Password");
          PasswordSalt = dr.GetString("PasswordSalt");
          PasswordHashed = dr.GetString("PasswordHashed");
          LastLoginUtc = dr.GetDateTimeOrNull("LastLoginUtc");
          InviteCode = dr.GetString("InviteCode");
          InvitedUtc = dr.GetDateTimeOrNull("InvitedUtc");
          InvitedByUserId = dr.GetIntOrNull("InvitedByUserId");
          LastInviteReminderSentUtc = dr.GetDateTimeOrNull("LastInviteReminderSentUtc");

          ComputedSenderEmailName = dr.GetString("ComputedSenderEmailName");
          ComputedSenderEmailAddress = dr.GetString("ComputedSenderEmailAddress");

          SelfRegisteredAsRoleId = dr.GetIntOrNull("SelfRegisteredAsRoleId");

          UserContractTypeId = dr.GetIntOrNull("UserContractTypeId");
          IncludeInPayRuns = dr.GetBoolFromInt("IncludeInPayRuns");

          PlatformFeePercent = dr.GetDecimalOrNull("PlatformFeePercent");

          UserSubscription = Subscriptions.User.GetUserSubscriptionInfo(dr);

          this.LatestCoacheeInfo = GetRelatedCoacheeInfo(dr, LatestCoacheeColPrefix);
          this.LatestCoachingInfo = GetRelatedCoacheeInfo(dr, LatestCoachingColPrefix);
        }

        internal void SetPasswordHashed(PasswordHashInfo hashInfo) {
          this.PasswordHashed = hashInfo.CombinedHashString;
          this.PasswordSalt = hashInfo.SaltEncoded;
        }

        private RelatedCoacheeInfo GetRelatedCoacheeInfo(SqlDataReader dr, string colPrefix) {

          // "Latest Coachee" = with or without coaching.
          if (dr.IsDBNull($"{colPrefix}CoacheeId")) {
            return null;
          } else {
            return new RelatedCoacheeInfo(
              coacheeId: dr.GetInt($"{colPrefix}CoacheeId"),
              coacheeGuid: dr.GetGuid($"{colPrefix}CoacheeGuid"),
              coacheeProgramStatusId: dr.GetIntOrNull($"{colPrefix}CoacheeProgramStatusId"),
              coachUserId: dr.GetInt($"{colPrefix}CoachUserId"),
              sessionsAllocated: dr.GetIntOrNull($"{colPrefix}SessionsAllocated"),
              sessionsBooked: dr.GetIntOrNull($"{colPrefix}SessionsBooked"),
              programJobId: dr.GetInt($"{colPrefix}ProgramJobId"),
              programStatusId: dr.GetIntOrNull($"{colPrefix}ProgramStatusId"),
              plcUserId: dr.GetIntOrNull($"{colPrefix}PLCUserId"),
              jobNumber: dr.GetString($"{colPrefix}JobNumber"),
              companyId: dr.GetInt($"{colPrefix}CompanyId"),
              companyName: dr.GetString($"{colPrefix}CompanyName"),
              pulseSurveyEnabled: dr.GetBoolFromInt($"{colPrefix}PulseSurveyEnabled", false),
              pulseSurveyLastSentUtc: dr.GetDateTimeOrNull($"{colPrefix}PulseSurveyLastSentUtc"),
              disableNudges: dr.GetBoolFromInt($"{colPrefix}DisableNudges", false),
              canSelfSelectCoach: dr.GetBoolFromInt($"{colPrefix}CanSelfSelectCoach")
            );
          }
        }

        public void SetLatestCoachingInfo(Guid coacheeGuid) {

          Query(null, $@"
            SELECT {LatestCoachingJoinAlias}.*
            FROM sv_User u
            {GetLatestCoacheeOuterApplySQL("u", LatestCoachingColPrefix, LatestCoachingJoinAlias, specificCoacheeGuid: true)}
            WHERE u.UserId = @UserId",
            dr => {
              this.LatestCoachingInfo = GetRelatedCoacheeInfo(dr, LatestCoachingColPrefix);
            },
            NewSqlParameter("UserId", this.UserId),
            NewSqlParameter("CoacheeGuid", coacheeGuid)
          );
        }

        public bool IsUserTester {
          get {
            if (_isUserTester == null) {
              _isUserTester = Array.IndexOf<String>(ConfigHelper.TestUserAccounts, EmailAddress.ToLower()) >= 0 ? true : false;
            }
            return (bool)_isUserTester;
          }
        }

        public bool IsUserEmailTester {
          get {
            if (_isUserEmailTester == null) {
              _isUserEmailTester = Array.IndexOf<String>(ConfigHelper.EmailTesterAccounts, EmailAddress.ToLower()) >= 0 ? true : false;
            }
            return (bool)_isUserEmailTester;
          }
        }

        public bool SendEmailsToOverrideRecipient {
          get {
            if (_sendEmailsToOverrideRecipient == null) {
              _sendEmailsToOverrideRecipient
                = !ConfigHelper.EmailRecipientOverrideAddress.IsNullOrEmpty()
                && (!ConfigHelper.IsLiveServer || IsUserTester) ? true : false;
            }
            return (bool)_sendEmailsToOverrideRecipient;
          }
        }

        public void SetTimeZoneIdIana(string timeZoneIdIana) {
          // Caller should handle errors if timeZoneIdIana is blank or invalid.
          this.TimeZoneIdIana = timeZoneIdIana;
          this.TimeZoneIdWindows = TimeHelper.IANAToWindowsTimeZoneId(timeZoneIdIana);
          this.TimeZoneInfo = TimeHelper.GetTimeZoneInfo(timeZoneIdIana);
        }

        public DateTime? GetUTCFromUserTime(DateTime? userLocalTime) {
          return userLocalTime.ToUniversalTimeOrNull(this.TimeZoneInfo);
        }

        public DateTime? GetUserTimeFromUTC(DateTime? universalTime) {
          return universalTime.UtcToTZOrNull(this.TimeZoneInfo);
        }

        public bool IsUnassignedUser => UserId == ConfigHelper.UserId.Unassigned;

        public bool HasContract => UserContractTypeId != null;

      }

      public class RelatedCoacheeInfo {

        public int CoacheeId { get; private set; }
        public Guid CoacheeGuid { get; private set; }
        public int? CoacheeProgramStatusId { get; private set; }
        public int CoachUserId { get; private set; }
        public int? SessionsAllocated { get; private set; }
        public int? SessionsBooked { get; private set; }
        public int ProgramJobId { get; private set; }
        public int? ProgramStatusId { get; private set; }
        public int? PLCUserId { get; private set; }
        public string JobNumber { get; private set; }
        public int CompanyId { get; private set; }
        public string CompanyName { get; private set; }
        public bool PulseSurveyEnabled { get; set; }
        public DateTime? PulseSurveyLastSentUtc { get; private set; }
        public bool DisableNudges { get; set; }
        public bool CanSelfSelectCoach { get; private set; }

        public RelatedCoacheeInfo(
          int coacheeId, Guid coacheeGuid, int? coacheeProgramStatusId, int coachUserId, int? sessionsAllocated, int? sessionsBooked,
          int programJobId, int? programStatusId, int? plcUserId, string jobNumber, int companyId, string companyName,
          bool pulseSurveyEnabled, DateTime? pulseSurveyLastSentUtc, bool disableNudges, bool canSelfSelectCoach) {

          CoacheeId = coacheeId;
          CoacheeGuid = coacheeGuid;
          CoacheeProgramStatusId = coacheeProgramStatusId;
          CoachUserId = coachUserId;
          SessionsAllocated = sessionsAllocated;
          SessionsBooked = sessionsBooked;
          ProgramJobId = programJobId;
          ProgramStatusId = programStatusId;
          PLCUserId = plcUserId;
          JobNumber = jobNumber;
          CompanyId = companyId;
          CompanyName = companyName;
          PulseSurveyEnabled = pulseSurveyEnabled;
          PulseSurveyLastSentUtc = pulseSurveyLastSentUtc;
          DisableNudges = disableNudges;
          CanSelfSelectCoach = canSelfSelectCoach;
        }

        public bool IsCoachAssigned => CoachUserId != ConfigHelper.UserId.Unassigned;
      }

      public class AbleUserInfo : AbleUserBasicInfo {

        public List<int> SurveyTemplateOrgIds { get; private set; }

        public bool IsIOSClientHR { get; protected set; }

        public int? AccessOnlySurveyId { get; private set; }

        public string AbleBioShort { get; private set; }
        public string AbleWebProfileUrl { get; private set; }

        public string DocTagName { get; private set; }
        public bool PortalSpecificSurveysOnly { get; private set; }

        internal string CoachForCompanyIdsStr { get; private set; }
        public List<int> CoachForCompanyIds { get; private set; }
        internal string PCForCompanyIdsStr { get; private set; }
        public List<int> PCForCompanyIds { get; private set; }
        internal string PLCForCompanyIdsStr { get; private set; }
        public List<int> PLCForCompanyIds { get; private set; }
        internal string SalesPartnerForCompanyIdsStr { get; private set; }
        public List<int> SalesPartnerForCompanyIds { get; private set; }
        internal string FacilitatorForCompanyIdsStr { get; private set; }
        public List<int> FacilitatorForCompanyIds { get; private set; }
        internal string ProjectAccessForCompanyIdsStr { get; private set; } // CompanyIds in which user has project access.
        public List<int> ProjectAccessForCompanyIds { get; private set; }

        public List<string> PCForJobNumbers { get; private set; } // JobNumbers (Projects) in which user is a Program Coordinator (PC) in one or more Programs.
        public List<string> PLCForJobNumbers { get; private set; } // JobNumbers (Projects) in which user is a Lead Consultant (PLC) in one or more Programs.
        public List<string> FacilitatorForJobNumbers { get; private set; } // JobNumbers (Projects) in which user is a Workshop Facilitator in one or more Programs.
        public List<string> ConsultingItemsForJobNumbers { get; private set; } // JobNumbers (Projects) in which user has Consulting Items in one or more Programs.
        public List<string> QuoteOwnerJobNumbers { get; private set; } // JobNumbers (Projects) in which user is an owner of one or more Quotes.
        public List<string> QuoteContactJobNumbers { get; private set; } // JobNumbers (Projects) for which user is Quote Contact
        public List<string> CoachForJobNumbers { get; private set; } // JobNumbers (Projects) in which user is a Coach in one or more Programs.
        public List<string> ProjectAccessForJobNumbers { get; private set; } // JobNumbers (Projects) for which user has access - mainly implemented for Clients.
        public Guid? CompanyGuid { get; private set; }
        public bool? DisplayLogoInNavBar { get; private set; }

        public AbleUserInfo(SqlDataReader dr) {

          SetBasicFields(dr);

          this.SurveyTemplateOrgIds = new List<int>() { OrgId }; // Only own org by default.
          this.DocTagName = dr.GetString("DocTagName");
          this.PortalSpecificSurveysOnly = false;
          this.IsIOSClientHR = dr.GetBoolFromInt("IOSClientHR");
          this.AccessOnlySurveyId = dr.GetIntOrNull("AccessOnlySurveyId");

          this.AbleBioShort = dr.GetString("AbleBioShort");
          this.AbleWebProfileUrl = dr.GetString("AbleWebProfileUrl");

          this.CoachForCompanyIdsStr = dr.GetString("CoachCompanyIds");
          this.CoachForCompanyIds = CoachForCompanyIdsStr.ToIntList();
          this.PCForCompanyIdsStr = dr.GetString("PCCompanyIds");
          this.PCForCompanyIds = PCForCompanyIdsStr.ToIntList();
          this.PLCForCompanyIdsStr = dr.GetString("PLCCompanyIds");
          this.PLCForCompanyIds = PLCForCompanyIdsStr.ToIntList();
          this.SalesPartnerForCompanyIdsStr = dr.GetString("SPCompanyIds");
          this.SalesPartnerForCompanyIds = SalesPartnerForCompanyIdsStr.ToIntList();
          this.FacilitatorForCompanyIdsStr = dr.GetString("WKFCompanyIds");
          this.FacilitatorForCompanyIds = FacilitatorForCompanyIdsStr.ToIntList();
          this.ProjectAccessForCompanyIdsStr = dr.GetString("ProjectAccessCompanyIds");
          this.ProjectAccessForCompanyIds = ProjectAccessForCompanyIdsStr.ToIntList();

          this.PCForJobNumbers = new List<string>(dr.GetString("PCJobNos", "").Split(','));
          this.PLCForJobNumbers = new List<string>(dr.GetString("PLCJobNos", "").Split(','));
          this.FacilitatorForJobNumbers = new List<string>(dr.GetString("WKFJobNos", "").Split(','));
          this.ConsultingItemsForJobNumbers = new List<string>(dr.GetString("PCIJobNos", "").Split(','));
          this.QuoteOwnerJobNumbers = new List<string>(dr.GetString("QuoteOwnerJobNos", "").Split(','));
          this.QuoteContactJobNumbers = new List<string>(dr.GetString("QuoteContactJobNos", "").Split(','));
          this.CoachForJobNumbers = new List<string>(dr.GetString("CoachJobNos", "").Split(','));
          this.ProjectAccessForJobNumbers = new List<string>(dr.GetString("ProjectAccessJobNos", "").Split(','));
          this.CompanyGuid = dr.GetGuidOrNull("CompanyGuid");
          this.DisplayLogoInNavBar = dr.GetBoolFromIntOrNull("DisplayLogoInNavBar");
        }

        public bool IsLinkedToAnyCompany() {

          return !CoachForCompanyIdsStr.IsNullOrEmpty()
            || !PCForCompanyIdsStr.IsNullOrEmpty()
            || !PLCForCompanyIdsStr.IsNullOrEmpty()
            || !SalesPartnerForCompanyIdsStr.IsNullOrEmpty()
            || !FacilitatorForCompanyIdsStr.IsNullOrEmpty()
            || !ProjectAccessForCompanyIdsStr.IsNullOrEmpty();
        }

        public bool IsLinkedToCompany(int companyId) {

          return CoachForCompanyIds.Contains(companyId)
            || PCForCompanyIds.Contains(companyId)
            || PLCForCompanyIds.Contains(companyId)
            || SalesPartnerForCompanyIds.Contains(companyId)
            || FacilitatorForCompanyIds.Contains(companyId)
            || ProjectAccessForCompanyIds.Contains(companyId);
        }

        public void AddSurveyTemplateOrgId(int surveyTemplateOrgId) {
          if (!this.SurveyTemplateOrgIds.Contains(surveyTemplateOrgId)) {
            this.SurveyTemplateOrgIds.Add(surveyTemplateOrgId);
          }
        }

        public bool IsDeliveryInProject(string jobNumber) {
          return IsCoachInProject(jobNumber) || IsFacilitatorInProject(jobNumber);
        }

        public bool IsPCorPLCInProject(string jobNumber) {
          return IsLeadConsultantInProject(jobNumber) || IsCoordinatorInProject(jobNumber);
        }

        public bool IsFacilitatorInProject(string jobNumber) {
          return !jobNumber.IsNullOrEmpty() && FacilitatorForJobNumbers.Find(p => p.Equals(jobNumber, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public bool IsConsultingInProject(string jobNumber) {
          return !jobNumber.IsNullOrEmpty() && ConsultingItemsForJobNumbers.Find(p => p.Equals(jobNumber, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public bool IsQuoteOwnerInProject(string jobNumber) {
          return !jobNumber.IsNullOrEmpty() && QuoteOwnerJobNumbers.Find(p => p.Equals(jobNumber, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public bool IsLeadConsultantInProject(string jobNumber) {
          return !jobNumber.IsNullOrEmpty() && PLCForJobNumbers.Find(p => p.Equals(jobNumber, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public bool IsCoordinatorInProject(string jobNumber) {
          return !jobNumber.IsNullOrEmpty() && PCForJobNumbers.Find(p => p.Equals(jobNumber, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public bool IsCoachInProject(string jobNumber) {
          return !jobNumber.IsNullOrEmpty() && CoachForJobNumbers.Find(p => p.Equals(jobNumber, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public bool IsInProjectAccess(string jobNumber) {
          return !jobNumber.IsNullOrEmpty() && ProjectAccessForJobNumbers.Find(p => p.Equals(jobNumber, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public bool IsPLCForAnyProject => PLCForJobNumbers?.Count > 0;
        public bool IsPCForAnyProject => PCForJobNumbers?.Count > 0;
      }

      public class UserActivityInfo {
        public int? LatestCoacheeId { get; private set; }
        public DateTime? DateOfBirth { get; set; }
        public string RoleTitle { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public int? OrgRoleId { get; set; }
        public DateTime? RegisteredUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
        public DateTime? InvitedUtc { get; set; }
        public DateTime? LastestInviteReminderSentUtc { get; set; }
        public DateTime? LastestAIMessageSentUtc { get; set; }
        public DateTime? LastestCoachingSessionUtc { get; set; }
        public DateTime? LatestWorkshop { get; set; }
        public int WorkshopsAllocated { get; set; }
        public int WorkshopsAttended { get; set; }
        public DateTime? LatestCompletedSurveyUtc { get; set; }
        public DateTime? LatestEvalCompletedUtc { get; set; }
        public DateTime? LatestDevPlanUtc { get; set; }
        public DateTime? LatestIntakeCompletedUtc { get; set; }
        public DateTime? LatestIntakeCreatedOpenUtc { get; set; }
        public DateTime? LatestIntakeClosedUtc { get; set; }
        public int AIMsgsLast30Days { get; set; }
        public int TotalAIMsgs { get; set; }
        public DateTime? NextSessionApptDateUtc { get; set; }
        public int SessionsUpcoming { get; set; }
        public int SessionsAllocated { get; set; }
        public int SessionsCompleted { get; set; }
        public bool HasCoaching { get; set; }
        public int? CoacheeStatusId { get; set; }
        public int DaysSinceBooking { get; set; }
        public int SessionsBooked { get; set; }
        public DateTime? MeetCoachEmailSentUtc { get; set; }
        public int? Latest360IntakeCodeId { get; private set; }
        public Guid? Latest360CoacheeGuid { get; private set; }
        public DateTime? Latest360CompletedUtc { get; private set; }
        public int? Latest360CoacheeId { get; private set; }
        public string Latest360SvUID { get; private set; }
        public int? Latest360PartId { get; private set; }
        public string Latest360PartUID { get; private set; }
        public DateTime? LatestPulseCompletedUtc { get; private set; }
        public string AISummaryText { get; private set; }
        public int CoachUserId { get; set; }
        public Guid? CoachUserGuid { get; set; }
        public string CoachFirstName { get; private set; }
        public string CoachLastName { get; private set; }
        public string CoachFullName { get; private set; }
        public string CoachEmail { get; private set; }
        public string CoachTimeZoneIdIANA { get; private set; }
        public string CoachCalendlyUrlName { get; private set; }
        public string CoachComputedSenderEmailName { get; private set; }
        public string CoachComputedSenderEmailAddress { get; private set; }
        public bool IsSoftDeleted { get; private set; }
        public int? CoachingTypeId { get; private set; }

        public UserActivityInfo() { }

        public UserActivityInfo(SqlDataReader dr) {
          this.LatestCoacheeId = dr.GetIntOrNull("CoacheeId", true);
          this.DateOfBirth = dr.GetDateTimeOrNull("DateOfBirth", true);
          this.RoleTitle = dr.GetString("RoleTitle", true);
          this.City = dr.GetString("City", true);
          this.Country = dr.GetString("Country", true);
          this.OrgRoleId = dr.GetIntOrNull("OrgRoleId", true);
          this.RegisteredUtc = dr.GetDateTimeOrNull("RegisteredUtc", true);
          this.LastLoginUtc = dr.GetDateTimeOrNull("LastLoginUtc", true);
          this.InvitedUtc = dr.GetDateTimeOrNull("InvitedUtc", true);
          this.LastestInviteReminderSentUtc = dr.GetDateTimeOrNull("LastInviteReminderSentUtc", true);
          this.LastestCoachingSessionUtc = dr.GetDateTimeOrNull("LatestCoachingSession", true);
          this.LatestWorkshop = dr.GetDateTimeOrNull("LatestWorkshop", true);
          this.WorkshopsAttended = dr.GetInt("WorkshopsAttended", 0);
          this.WorkshopsAllocated = dr.GetInt("WorkshopsAllocated", 0);
          this.LatestCompletedSurveyUtc = dr.GetDateTimeOrNull("LatestCompletedSurvey", true);
          this.LatestEvalCompletedUtc = dr.GetDateTimeOrNull("LatestEvalCompleted", true);
          this.LatestDevPlanUtc = dr.GetDateTimeOrNull("LatestDevPlan", true);
          this.LatestIntakeCompletedUtc = dr.GetDateTimeOrNull("LatestIntakeCompleted", true);
          this.LatestIntakeCreatedOpenUtc = dr.GetDateTimeOrNull("LatestIntakeCreatedOpen", true);
          this.LatestIntakeClosedUtc = dr.GetDateTimeOrNull("LatestIntakeClosed", true);
          this.AIMsgsLast30Days = dr.GetInt("AIMsgsLast30Days", 0);
          this.TotalAIMsgs = dr.GetInt("TotalAIMsgs", 0);
          this.LastestAIMessageSentUtc = dr.GetDateTimeOrNull("LastAIMessageSentUtc", true);
          this.NextSessionApptDateUtc = dr.GetDateTimeOrNull("NextSessionApptDateUtc", true);
          this.SessionsUpcoming = dr.GetInt("SessionsUpcoming", 0);
          this.SessionsAllocated = dr.GetInt("SessionsAllocated", 0);
          this.SessionsCompleted = dr.GetInt("SessionsCompleted", 0);
          this.HasCoaching = this.SessionsAllocated > 0;
          this.CoacheeStatusId = dr.GetIntOrNull("CoacheeStatusId", true);
          this.DaysSinceBooking = dr.GetInt("DaysSinceBooking", 0);
          this.SessionsBooked = dr.GetInt("SessionsBooked", 0);
          this.MeetCoachEmailSentUtc = dr.GetDateTimeOrNull("MeetCoachEmailSentUtc", true);
          this.Latest360IntakeCodeId = dr.GetIntOrNull("Latest360IntakeCodeId");
          this.Latest360CompletedUtc = dr.GetDateTimeOrNull("Latest360CompletedUtc");
          this.Latest360CoacheeId = dr.GetIntOrNull("Latest360CoacheeId");
          this.Latest360CoacheeGuid = dr.GetGuidOrNull("Latest360CoacheeGuid");
          this.Latest360SvUID = dr.GetString("Latest360SvUID");
          this.Latest360PartId = dr.GetIntOrNull("Latest360PartId");
          this.Latest360PartUID = dr.GetString("Latest360PartUID");
          this.LatestPulseCompletedUtc = dr.GetDateTimeOrNull("LatestPulseCompletedUtc");
          this.AISummaryText = dr.GetString("AISummaryText");
          this.CoachUserId = dr.GetInt("CoachUserId", ConfigHelper.UserId.Unassigned);
          this.CoachUserGuid = dr.GetGuidOrNull("CoachUserGuid");
          this.CoachFirstName = dr.GetString("CoachFirstName");
          this.CoachLastName = dr.GetString("CoachLastName");
          this.CoachFullName = this.CoachFirstName + " " + this.CoachLastName;
          this.CoachEmail = dr.GetString("CoachEmail");
          this.CoachTimeZoneIdIANA = dr.GetString("CoachTimeZoneIdIANA");
          this.CoachCalendlyUrlName = dr.GetString("CoachCalendlyUrlName");
          this.CoachComputedSenderEmailName = dr.GetString("CoachComputedSenderEmailName");
          this.CoachComputedSenderEmailAddress = dr.GetString("CoachComputedSenderEmailAddress");
          this.CoachingTypeId = dr.GetIntOrNull("CoachingTypeId");
          this.IsSoftDeleted = dr.GetDateTimeOrNull("DeletedUtc") != null;
        }

        public bool IsRegistered => this.RegisteredUtc != null;

        public DateTime? GetNextApptDateInCoachTZ() {
          if (this.NextSessionApptDateUtc == null) return null;
          return TimeHelper.UtcToTimeZoneId(this.NextSessionApptDateUtc, this.CoachTimeZoneIdIANA).ToDateTimeOrNull();
        }
      }

      public class PartnerActionsInfo {
        // Used for Partners actions on OverviewUpcoming
        public bool HasCompletedProfile { get; private set; }
        public bool HasFilledBios { get; private set; }
        public bool HasBeenInvitedToThePlatform { get; private set; }
        public bool HasInvitedUsersToCompany { get; private set; }
        public bool HasCreatedMicrolearnings { get; private set; }
        public bool HasParticipants { get; private set; }
        public bool HasCreatedProjects { get; private set; }
        public bool HasQuotes { get; private set; }
        public bool HasQuotesNeedingAttention { get; private set; }
        public bool IsCompanyProfileComplete { get; private set; }
        public bool HasWorkshopsToConfirm { get; private set; }
        public bool HasParticipantsWithoutCoach { get; private set; }
        public bool HasStuckParticipants { get; private set; }
        public bool HasQuotesProgramSetup { get; private set; }
        public PartnerActionsInfo(SqlDataReader dr) {
          this.HasCompletedProfile = dr.GetBoolFromInt("HasCompletedProfile");
          this.HasFilledBios = dr.GetBoolFromInt("HasFilledBios");
          this.HasBeenInvitedToThePlatform = dr.GetBoolFromInt("HasBeenInvitedToThePlatform");
          this.HasInvitedUsersToCompany = dr.GetBoolFromInt("HasInvitedUsersToCompany");
          this.HasCreatedMicrolearnings = dr.GetBoolFromInt("HasCreatedMicrolearnings");
          this.HasParticipants = dr.GetBoolFromInt("HasParticipants");
          this.HasCreatedProjects = dr.GetBoolFromInt("HasCreatedProjects");
          this.HasQuotes = dr.GetBoolFromInt("HasQuotes");
          this.HasQuotesNeedingAttention = dr.GetBoolFromInt("HasQuotesNeedingAttention");
          this.IsCompanyProfileComplete = dr.GetBoolFromInt("IsCompanyProfileComplete");
          this.HasWorkshopsToConfirm = dr.GetBoolFromInt("HasWorkshopsToConfirm");
          this.HasParticipantsWithoutCoach = dr.GetBoolFromInt("HasParticipantsWithoutCoach");
          this.HasStuckParticipants = dr.GetBoolFromInt("HasStuckParticipants");
          this.HasQuotesProgramSetup = dr.GetBoolFromInt("HasQuotesProgramSetup");
        }
      }

      public class LogInAsUser : UserIdentity {

        public string SubscriptionName { get; private set; }

        public LogInAsUser(SqlDataReader dr) {
          SetIdentityProps(dr);
          SubscriptionName = dr.GetString("SubscriptionName");
        }
      }
    }
  }

  /// <summary>
  /// Extension methods for AbleUser types
  /// </summary>
  public static class AbleUserExtensions {

    // User profile type string constants (matches Intercom values)
    private const string ProfileTypeString_Admin = "admin";
    private const string ProfileTypeString_TenantAdmin = "tenantadmin";
    private const string ProfileTypeString_Provider = "provider";
    private const string ProfileTypeString_Client = "client";
    private const string ProfileTypeString_Leader = "leader";
    private const string ProfileTypeString_Unknown = "unknown";

    /// <summary>
    /// Gets the UserProfileType for the given user based on their role flags.
    /// Priority order: IsAbleAdmin (Admin) > IsAbleCoach (Provider) > IsAbleClient (Client) > IsParticipant (Leader) > Unknown
    /// </summary>
    public static DbHelper.AbleUser.UserProfileType GetUserProfileType(this DbHelper.AbleUser.UserIdentity userInfo) {

      if (userInfo == null) {
        return DbHelper.AbleUser.UserProfileType.Unknown;
      }

      if (userInfo.IsAbleAdmin) {
        return DbHelper.AbleUser.UserProfileType.Admin;
      } else if (userInfo.IsTenantOrgAdmin) {
        return DbHelper.AbleUser.UserProfileType.TenantAdmin;
      } else if (userInfo.IsAbleCoach) {
        return DbHelper.AbleUser.UserProfileType.Provider;
      } else if (userInfo.IsAbleClient) {
        return DbHelper.AbleUser.UserProfileType.Client;
      } else if (userInfo.IsParticipant) {
        return DbHelper.AbleUser.UserProfileType.Leader;
      }

      return DbHelper.AbleUser.UserProfileType.Unknown;
    }

    /// <summary>
    /// Converts UserProfileType enum to its string representation for Intercom/external systems.
    /// </summary>
    public static string ToProfileString(this DbHelper.AbleUser.UserProfileType profileType) {
      return profileType switch {
        DbHelper.AbleUser.UserProfileType.Admin => ProfileTypeString_Admin,
        DbHelper.AbleUser.UserProfileType.TenantAdmin => ProfileTypeString_TenantAdmin,
        DbHelper.AbleUser.UserProfileType.Provider => ProfileTypeString_Provider,
        DbHelper.AbleUser.UserProfileType.Client => ProfileTypeString_Client,
        DbHelper.AbleUser.UserProfileType.Leader => ProfileTypeString_Leader,
        _ => ProfileTypeString_Unknown,
      };
    }
  }
}
