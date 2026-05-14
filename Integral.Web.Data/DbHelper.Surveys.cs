using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using static Integral.Web.DbHelper.Common;
using System.Diagnostics;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class AlbertSurveys {

      public const int DefaultIntakeDateCode = 1;
      public const int Maximum360Score = 10;
      public const bool ForceLMPSingleQuestionSet = true; // Change new LMPs to a single question set.
      public const int PatchDbCatchupDays = 90;
      public const int OrgReportMinCompletedRequired = 3; // Minimum completed required for org (IOS) report.

      public static readonly string PatchDbNoScopeCondition = $@"
          ( sv.LastUpdatedQuestionInfoUtc IS NULL
            OR sv.MaxPartCompletedUtc > sv.LastUpdatedQuestionInfoUtc
            OR sv.sv_createdUTC > DATEADD(DAY, -{PatchDbCatchupDays}, GETUTCDATE()) )";

      // Since everything admin is in Perth time, including Intercom, all surveys default to perth timezone.
      public static readonly TimeZoneInfo DefaultSurveyTimeZoneInfo = ConfigHelper.DefaultTimeZoneInfo;

      private const string UniqueIdValidChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
      private const string UniqueIdValidRegex = "[0-9a-zA-Z]+";
      private const int UniqueIdMinLength = 10;
      private const int UniqueIdMaxLength = 20;

      public enum SurveyTypeEnum { Standard360, Pulse360, Eval, Intake, IOS, DevelopmentPlan, Unknown }
      public enum EvalTypeEnum { NotEval, Coaching, Workshop }
      public enum OrgReportState { Ok, SurveyNotClosed, NotEnoughResponses }

      public enum FeedbackOptionEnum {
        NoRaters = 0,
        Raters = 1,
        ManagerAndRaters = 2,
        ManagerPeersAndDirectReports = 3,
        ManagerOnly = 4
      }

      public static string GetValidUniqueId(string InputString) {
        // Return input if it is a valid UID otherwise return empty string.
        InputString = InputString.EmptyIfNull().TrimWhitespace();
        if (IsValidUniqueId(InputString)) return InputString;
        else return string.Empty;
      }

      public static bool IsValidUniqueId(string InputString) {
        var isMatch = Regex.IsMatch(InputString, "^" + UniqueIdValidRegex + "$");
        return isMatch;
      }

      private static string GetRandomUniqueId() {
        string uniqueId = string.Empty;
        var rnd = new Random();
        int length = rnd.Next(UniqueIdMinLength, UniqueIdMaxLength);
        for (int i = 0; i < length; i++) {
          int charPos = rnd.Next(0, UniqueIdValidChars.Length - 1);
          uniqueId += UniqueIdValidChars.Substring(charPos, 1);
        }
        return uniqueId;
      }

      public class FirstUpcomingNonEvalSurvey {
        public string ReportType;
        public string ReportSubType;
        public string SurveyTitle;
        public FirstUpcomingNonEvalSurvey(string reportType, string reportSubType, string surveyTitle) {
          ReportType = reportType;
          ReportSubType = reportSubType;
          SurveyTitle = surveyTitle;
        }
      }

      public static FirstUpcomingNonEvalSurvey GetFirstUpcomingNonEvalSurveyOrNull(int coacheeId) {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = $@"
            SELECT TOP 1 s.sv_ReportType, s.ReportSubType, s.sv_360GroupName
            FROM sv_360_Participants p
            INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
            INNER JOIN sv_360_AnswerTypes dt ON p.SurveyId = dt.SurveyId AND dt.AnswerTypeDescr = 'date'
            INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId AND dc.Code = p.DateGroupCode
            WHERE p.AbleCoacheeId = @CoacheeId
              AND p.IsSelf = 1
              AND dc.ScheduledStartDateUtc > GETUTCDATE()
              AND {SQLWhereConditions.NotEvalSurveys("s")}
            ORDER BY p.AbleCoacheeId, dc.ScheduledStartDateUtc";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@CoacheeId", coacheeId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                return new FirstUpcomingNonEvalSurvey(
                  reportType: dr.GetString("sv_ReportType"),
                  reportSubType: dr.GetString("ReportSubType"),
                  surveyTitle: dr.GetString("sv_360GroupName")
                );
              }
            }
          }
        }
        return null;
      }

      public class SQLWhereConditions {
        // Note conditions here need to logically match those in GetSurveyType() for determining survey types.
        public static string Standard360Surveys(string surveyTableAlias) => $"{surveyTableAlias}.SurveyTypeCode = '{ConfigHelper.SurveyTypeCodes.Able360}' AND {surveyTableAlias}.HasLeadershipQuestions = 1";
        public static string Pulse360Surveys(string surveyTableAlias) => $"{surveyTableAlias}.SurveyTypeCode = '{ConfigHelper.SurveyTypeCodes.Pulse360}'";
        public static string NotPulse360Surveys(string surveyTableAlias) => $"{surveyTableAlias}.SurveyTypeCode <> '{ConfigHelper.SurveyTypeCodes.Pulse360}'";
        public static string EvalSurveys(string surveyTableAlias) => $"{surveyTableAlias}.SurveyTypeCode = '{ConfigHelper.SurveyTypeCodes.Eval}'";
        public static string NotEvalSurveys(string surveyTableAlias) => $"{surveyTableAlias}.SurveyTypeCode <> '{ConfigHelper.SurveyTypeCodes.Eval}'";
        public static string WorkshopEvalSurveys(string surveyTableAlias) => $"{EvalSurveys(surveyTableAlias)} AND {surveyTableAlias}.WorkshopEventId IS NOT NULL";
        public static string IntakeSurveys(string surveyTableAlias) => $"{surveyTableAlias}.SurveyTypeCode = '{ConfigHelper.SurveyTypeCodes.Intake}'";
        public static string IOSSurveys(string surveyTableAlias) => $"{surveyTableAlias}.SurveyTypeCode = '{ConfigHelper.SurveyTypeCodes.IOS}'";
        public static string DevelopmentPlanSurveys(string surveyTableAlias) => $"{surveyTableAlias}.SurveyTypeCode = '{ConfigHelper.SurveyTypeCodes.DevPlan}'";
      }

      public class SurveySelectItem {
        public int SurveyId { get; private set; }
        public string SurveyUId { get; private set; }
        public string SurveyTitle { get; private set; }
        public string ReportType { get; private set; }
        public int IntakeCodeId { get; private set; }
        public int IntakeNumber { get; private set; }
        public DateTime? ScheduledStartDateUtc { get; private set; }
        public DateTime IntakeCloseDateLocal { get; private set; }
        public SurveySelectItem(
          int surveyId, string surveyUId, string surveyTitle, string reportType,
          int intakeCodeId, int intakeNumber,
          DateTime? scheduledStartDateUtc, DateTime intakeCloseDateLocal
        ) {
          SurveyId = surveyId; SurveyUId = surveyUId; SurveyTitle = surveyTitle; ReportType = reportType;
          IntakeCodeId = intakeCodeId; IntakeNumber = intakeNumber;
          ScheduledStartDateUtc = scheduledStartDateUtc; IntakeCloseDateLocal = intakeCloseDateLocal;
        }
      }

      // Get all surveys related to a program with a minimum number of self participants.
      // Note that sending "program surveys" creates a record in sv_JoinJobsAndIntakes to relate it to the program.
      public static List<SurveySelectItem> GetSurveySelectForProgram(
        int programJobId, string findSurveyUId, int findSurveyIntakeNumber,
        out SurveyInfo foundSurveyInfo, int minParticipants = 1) {

        var surveyItems = new List<SurveySelectItem>();
        int foundSurveyId = 0, foundIntakeNumber = 0;
        foundSurveyInfo = null;

        string sql = @"
          SELECT
            s.sv_id, s.sv_uniqueid, s.sv_title, s.sv_ReportType,
            s.AlbertRatersOnly, s.sv_FeedbackOption, s.RatersSuggestedMin, s.RatersSuggestedMax, s.IsStrictRaterLimits,
            s.IntakeCodeId, s.IntakeNumber, s.IntakeName,
            s.IntakeCloseDate, s.IntakeCloseDateSelf, s.ScheduledStartDateUtc,
            s.WorkshopEventId, s.PartCount, s.PartsCompleted
          FROM id_Job j
          CROSS APPLY (
            SELECT
              s.sv_id, s.sv_uniqueid, s.sv_title, s.sv_ReportType,
              s.AlbertRatersOnly, s.sv_FeedbackOption, s.RatersSuggestedMin, s.RatersSuggestedMax, s.IsStrictRaterLimits,
              dc.CodeId AS IntakeCodeId, dc.Code AS IntakeNumber, dc.Value AS IntakeName,
              dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc,
              s.WorkshopEventId,
              COUNT(sp.PartId) AS PartCount, COUNT(sp.Completed) AS PartsCompleted,
              MIN(sp.CreatedUtc) AS FirstCreated
            FROM sv_360_Participants sp
            INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
            INNER JOIN sv_Survey s ON s.sv_id = sp.SurveyId
            INNER JOIN sv_360_AnswerTypes dt ON dt.SurveyId = s.sv_id AND dt.AnswerTypeDescr = 'date'
            INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId AND dc.Code = sp.DateGroupCode
            WHERE ac.ProgramJobId = j.JobId
            GROUP BY
              s.sv_id, s.sv_uniqueid, s.sv_title, s.sv_ReportType,
              s.AlbertRatersOnly, s.sv_FeedbackOption, s.RatersSuggestedMin, s.RatersSuggestedMax, s.IsStrictRaterLimits,
              dc.CodeId, dc.Code, dc.Value,
              dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc,
              s.WorkshopEventId
          ) AS s
          WHERE j.JobId = @ProgramJobId
            AND s.PartCount >= @MinParticipants
          ORDER BY s.FirstCreated DESC;
";
        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@ProgramJobId", programJobId);
            cmd.Parameters.AddInt("@MinParticipants", minParticipants);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                var surveyItem = new SurveySelectItem(
                  dr.GetInt("sv_id"), dr.GetString("sv_uniqueid"), dr.GetString("sv_title"), dr.GetString("sv_ReportType"),
                  dr.GetInt("IntakeCodeId"), dr.GetInt("IntakeNumber"),
                  dr.GetDateTimeOrNull("ScheduledStartDateUtc"),
                  dr.GetDateTime("IntakeCloseDate")
                );
                surveyItems.Add(surveyItem);
                // Pick first in list by default, or the one we are looking for.
                if ((findSurveyUId.IsNullOrEmpty() && foundSurveyId == 0)
                  || (!findSurveyUId.IsNullOrEmpty() && findSurveyUId == surveyItem.SurveyUId && findSurveyIntakeNumber == surveyItem.IntakeNumber)) {
                  foundSurveyId = surveyItem.SurveyId;
                  foundIntakeNumber = surveyItem.IntakeNumber;
                }
              }
            }
          }
        }
        if (foundSurveyId != 0) foundSurveyInfo = GetSurveyInfo(foundSurveyId, foundIntakeNumber);
        return surveyItems;
      }

      public enum OnlyViewerCompatible { No, Yes }
      public enum OnlyWhereLeaderCanView360Report { No, Yes }
      private enum FindParticipantsBy { CoacheeId, UserId }

      public static List<SurveyInfo> GetSurveyInfoListForUser(int userId,
        OnlyViewerCompatible onlyViewerCompatible,
        OnlyWhereLeaderCanView360Report onlyWhereLeaderCanView360Report = OnlyWhereLeaderCanView360Report.No) {

        return GetSurveyInfoListForCoacheeOrUser(FindParticipantsBy.UserId, userId, null, null,
          onlyViewerCompatible,
          onlyWhereLeaderCanView360Report,
          out _);
      }

      public static List<SurveyInfo> GetSurveyInfoListForCoachee(int coacheeId,
        OnlyViewerCompatible onlyViewerCompatible,
        OnlyWhereLeaderCanView360Report onlyWhereLeaderCanView360Report = OnlyWhereLeaderCanView360Report.No) {

        return GetSurveyInfoListForCoacheeOrUser(FindParticipantsBy.CoacheeId, coacheeId, null, null,
          onlyViewerCompatible,
          onlyWhereLeaderCanView360Report,
          out _);
      }

      public static List<SurveyInfo> GetSurveyInfoListForCoachee(int coacheeId, string surveyUIdToFind, string partUIdToFind,
        OnlyViewerCompatible onlyViewerCompatible,
        OnlyWhereLeaderCanView360Report onlyWhereLeaderCanView360Report,
        out SurveyInfo surveyInfoFound) {

        return GetSurveyInfoListForCoacheeOrUser(FindParticipantsBy.CoacheeId, coacheeId, surveyUIdToFind, partUIdToFind,
          onlyViewerCompatible,
          onlyWhereLeaderCanView360Report,
          out surveyInfoFound);
      }

      public static List<SurveyInfo> GetSurveyInfoListForUser(int userId, string surveyUIdToFind, string partUIdToFind,
        OnlyViewerCompatible onlyViewerCompatible,
        OnlyWhereLeaderCanView360Report onlyWhereLeaderCanView360Report,
        out SurveyInfo surveyInfoFound) {

        return GetSurveyInfoListForCoacheeOrUser(FindParticipantsBy.UserId, userId, surveyUIdToFind, partUIdToFind,
          onlyViewerCompatible,
          onlyWhereLeaderCanView360Report,
          out surveyInfoFound);
      }

      public static List<SurveyInfo> GetOpenSurveysForUserId(int userId, OnlyViewerCompatible onlyViewerCompatible) {

        var surveysList = GetSurveyInfoListForCoacheeOrUser(FindParticipantsBy.UserId, userId, null, null,
          onlyViewerCompatible,
          OnlyWhereLeaderCanView360Report.No,
          out _);

        if (surveysList != null) surveysList.RemoveAll(s => (s.FoundParticipantBrief?.IsSurveyAvailable ?? false) == false);

        return surveysList;
      }

      public static List<SurveyUserReminderInfo> GetSurveysForUserReminders() {

        var surveyList = new List<SurveyUserReminderInfo>();

        Query($@"
          SELECT
            s.sv_id, s.sv_OrgId, s.sv_uniqueid, s.CreatedByUserId, s.sv_ReportType,
            s.SurveyTypeCode, s.ShowSurveyLoggedIn,
            s.sv_title, s.sv_360GroupName, s.sv_createdUTC, s.ClonedFromSvId, s.AlbertRatersOnly,
            s.sv_FeedbackOption, s.RatersSuggestedMin, s.RatersSuggestedMax, s.IsStrictRaterLimits,
            s.WorkshopEventId, we.WorkshopTitle, s.PrimaryGblAnswerTypeId, s.sv_UseReminders, s.AllowReportWithoutRaters,
            dbo.fnGetOrgSenderEmailName(s.sv_OrgId) AS ComputedSenderEmailName,
            dbo.fnGetOrgSenderEmailAddress(s.sv_OrgId) AS ComputedSenderEmailAddress,
            dc.CodeId AS IntakeCodeId, dc.Code AS IntakeNumber, dc.Value AS IntakeName,
            dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc,
            p.PartId, p.UniqueId AS PartUniqueId, p.FirstInvitationSent, p.LastInvitationSent, p.Completed,
            p.AbleCoacheeId, p.ReportSentUtc,  p.IsSelf,
            p.UserId AS PartUserId, p.Name AS PartName, p.FirstName AS PartFirstName, p.LastName AS PartLastName, p.Email AS PartEmail,
            pr.RaterCountInvited, pr.RaterCountCompleted,
            ac.ProgramJobId, ac.JobNumber, ac.FriendlyProjectTitle, ac.CoachUserId,
            ac.SurveyReminderCadenceId, ac.SurveyReminderCadenceId_Raters,
            ac.CompanyId, ac.CompanyName, ac.BrandingOrgGuid, ac.ParentOrgGuid,
            pself.Name AS SelfName, pself.FirstName AS SelfFirstName, pself.LastName AS SelfLastName, pself.Email AS SelfEmail
          FROM sv_360_Participants p
          INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
          LEFT OUTER JOIN al_SurveyType st ON st.SurveyTypeCode = s.SurveyTypeCode
          INNER JOIN sv_360_AnswerTypes dt ON p.SurveyId = dt.SurveyId AND dt.AnswerTypeDescr = 'date'
          INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId AND dc.Code = p.DateGroupCode
          LEFT OUTER JOIN sv_360_Participants pself ON pself.PartId = p.Self_PartId
          LEFT OUTER JOIN ev_WorkshopEvent we ON we.WorkshopEventId = s.WorkshopEventId
          OUTER APPLY (
            SELECT COUNT(pr.PartId) AS RaterCountInvited,
              COUNT(pr.Completed) AS RaterCountCompleted
            FROM sv_360_Participants pr
            WHERE pr.Self_PartId = p.PartId
          ) AS pr
          OUTER APPLY (
            SELECT TOP 1
              ac.ProgramJobId, ac.CoachUserId, ac.DeletedUtc,
              prj.JobNumber, prj.SvCompanyId, prj.SurveyReminderCadenceId, prj.SurveyReminderCadenceId_Raters,
              CASE WHEN COALESCE(prj.FriendlyProjectTitle, '') <> ''
                THEN prj.FriendlyProjectTitle
                ELSE prj.ProjectName
              END AS FriendlyProjectTitle,
              o_prj.OrgGuid AS ParentOrgGuid,
              ac.CompanyId, cmp.CompanyName, org.OrgGuid as BrandingOrgGuid
            FROM al_Coachees ac
            INNER JOIN id_Job j ON j.JobId = ac.ProgramJobId
            INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
            INNER JOIN sv_Organisation o_prj ON o_prj.OrgId = prj.ParentOrgId
            LEFT OUTER JOIN sv_SurveyCompany cmp ON ac.CompanyId = cmp.SvCompanyId
            LEFT OUTER JOIN sv_Organisation org ON org.OrgId = prj.BrandingOrgId
            WHERE ac.CoacheeId = p.AbleCoacheeId
          ) as ac
          WHERE
            ac.DeletedUtc IS NULL
            AND s.IsAlbertSurvey = 1
            AND s.sv_UseReminders = 1
            AND p.FirstInvitationSent IS NOT NULL
            AND dc.IntakeCloseDate > DATEADD(HOUR, 12, GETUTCDATE()) -- not too near the close date
            AND ( dc.ScheduledStartDateUtc IS NULL OR dc.ScheduledStartDateUtc < GETUTCDATE() )
            AND ( p.Completed IS NULL OR ( s.AlbertRatersOnly = 0 AND p.IsSelf = 1 AND pr.RaterCountInvited < ISNULL(s.RatersSuggestedMin, @MinRaters) ) )
            AND ( ISNULL(p.LastInvitationSent, p.FirstInvitationSent) IS NULL
              OR DATEDIFF(HOUR, ISNULL(p.LastInvitationSent, p.FirstInvitationSent), GETUTCDATE()) > @MinHoursBetweenReminders )
          ORDER BY p.Email",

          dr => {
            var surveyInfo = new SurveyUserReminderInfo(
              partId: dr.GetInt("PartId"),
              partUniqueId: dr.GetString("PartUniqueId"),
              coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
              coachUserId: dr.GetIntOrNull("CoachUserId"),
              programJobId: dr.GetIntOrNull("ProgramJobId"),
              completedUtc: dr.GetDateTimeOrNull("Completed"),
              ratersInvited: dr.GetInt("RaterCountInvited"),
              ratersCompleted: dr.GetInt("RaterCountCompleted"),
              reportSentUtc: dr.GetDateTimeOrNull("ReportSentUtc"),
              surveyId: dr.GetInt(DbTable.Surveys.IdentityColumn),
              surveyUID: dr.GetString("sv_uniqueid"),
              orgId: dr.GetInt("sv_OrgId"),
              programJobNumber: dr.GetString("JobNumber"),
              friendlyProjectTitle: dr.GetString("FriendlyProjectTitle"),
              createdUtc: dr.GetDateTime("sv_createdUTC"),
              clonedFromSurveyId: dr.GetIntOrNull("ClonedFromSvId"),
              internalTitle: dr.GetString("sv_title"),
              surveyName: !dr.GetString("sv_360GroupName").IsNullOrEmpty() ? dr.GetString("sv_360GroupName") : dr.GetString("sv_title"),
              createdByUserId: dr.GetIntOrNull("CreatedByUserId"),
              surveyTimeZone: DefaultSurveyTimeZoneInfo,
              intakeCodeId: dr.GetInt("IntakeCodeId"),
              intakeNumber: dr.GetInt("IntakeNumber"),
              scheduledStartDateUtc: dr.GetDateTimeOrNull("ScheduledStartDateUtc"),
              firstInvitationSentUtc: dr.GetDateTimeOrNull("FirstInvitationSent"),
              latestReminderSentUtc: dr.GetDateTimeOrNull("LastInvitationSent"),
              closeDateSelfLocal: dr.GetDateTime("IntakeCloseDateSelf"),
              closeDateRatersLocal: dr.GetDateTime("IntakeCloseDate"),
              reportType: ReportTypes.GetReportTypeByDbValue(dr.GetString("sv_ReportType")),
              feedbackOption: (FeedbackOptionEnum)dr.GetInt("sv_FeedbackOption"),
              ratersSuggestedMin: dr.GetIntOrNull("RatersSuggestedMin"),
              ratersSuggestedMax: dr.GetIntOrNull("RatersSuggestedMax"),
              isStrictRaterLimits: dr.GetBoolFromInt("IsStrictRaterLimits"),
              isRatersOnly: dr.GetBoolFromInt("AlbertRatersOnly"),
              isSelf: dr.GetBoolFromInt("IsSelf"),
              partUserId: dr.GetIntOrNull("PartUserId"),
              partName: dr.GetString("PartName"),
              partFirstName: dr.GetString("PartFirstName"),
              partLastName: dr.GetString("PartLastName"),
              partEmail: dr.GetString("PartEmail"),
              workshopEventId: dr.GetIntOrNull("WorkshopEventId"),
              workshopTitle: dr.GetString("WorkshopTitle"),
              computedSenderEmailName: dr.GetString("ComputedSenderEmailName").ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_FullName),
              computedSenderEmailAddress: dr.GetString("ComputedSenderEmailAddress").ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_Address),
              primaryGblAnswerTypeId: dr.GetIntOrNull("PrimaryGblAnswerTypeId"),
              surveyTypeCode: dr.GetString("SurveyTypeCode"),
              showSurveyLoggedIn: dr.GetBoolFromInt("ShowSurveyLoggedIn"),
              surveyReminderCadenceId: dr.GetIntOrNull("SurveyReminderCadenceId"),
              surveyReminderCadenceId_Raters: dr.GetIntOrNull("SurveyReminderCadenceId_Raters"),
              useReminder: dr.GetBoolFromInt("sv_UseReminders"),
              allowReportWithoutRaters: dr.GetBoolFromInt("AllowReportWithoutRaters"),
              selfName: dr.GetString("SelfName"),
              selfFirstName: dr.GetString("SelfFirstName"),
              selfLastName: dr.GetString("SelfLastName"),
              selfEmail: dr.GetString("SelfEmail"),
              companyId: dr.GetIntOrNull("CompanyId"),
              companyName: dr.GetString("CompanyName"),
              brandingOrgGuid: dr.GetGuidOrNull("BrandingOrgGuid")
            );
            surveyList.Add(surveyInfo);
          },
          NewSqlParameter("@MinRaters", ConfigHelper.InvitedRatersMinimumRequired),
          NewSqlParameter("@MinHoursBetweenReminders", ConfigHelper.MinimumHoursBetweenReminders)
        );

        return surveyList;
      }

      private static string GetHasOpenTextSQL(string surveyAlias = "s", string surveyTypeAlias = "st", string outputVarName = "HasOpenText") =>
        $@" IIF(EXISTS (
              SELECT 1
              FROM sv_360_Questions q
              WHERE q.SurveyId = {surveyAlias}.sv_id
                AND (q.InputType = '{ConfigHelper.InputTypes.OpenText}' OR {surveyTypeAlias}.ReportCanViewSingleLineText = 1 AND q.InputType = '{ConfigHelper.InputTypes.Text}')
            ), 1, 0) AS {outputVarName}";

      private static List<SurveyInfo> GetSurveyInfoListForCoacheeOrUser(
        FindParticipantsBy findSurveysBy,
        int coacheeOrUserId,
        string surveyUIdToFind, string partUIdToFind,
        OnlyViewerCompatible onlyViewerCompatible,
        OnlyWhereLeaderCanView360Report onlyWhereLeaderCanView360Report,
        out SurveyInfo surveyInfoFound) {

        var surveyList = new List<SurveyInfo>();
        SurveyInfo _surveyInfoFound = null;

        Query($@"

          SELECT
            s.sv_id, s.sv_OrgId, s.sv_uniqueid, s.CreatedByUserId, s.sv_ReportType, s.SvCompanyId,
            s.SurveyTypeCode, s.ShowSurveyLoggedIn, s.IsAlbertSurvey,
            s.sv_title, s.sv_360GroupName, s.sv_createdUTC, s.ClonedFromSvId,
            s.GroupReportAvailable, s.AlbertRatersOnly, s.ReportInformationHtml, s.DisablePDF,
            s.sv_FeedbackOption, s.RatersSuggestedMin, s.RatersSuggestedMax, s.IsStrictRaterLimits,
            s.WorkshopEventId, s.PrimaryGblAnswerTypeId, s.AllowReportWithoutRaters, s.Hide360ReportNorms,
            {GetHasOpenTextSQL()},
            dbo.fnGetOrgSenderEmailName(s.sv_OrgId) AS ComputedSenderEmailName,
            dbo.fnGetOrgSenderEmailAddress(s.sv_OrgId) AS ComputedSenderEmailAddress,
            dc.CodeId AS IntakeCodeId, dc.Code AS IntakeNumber, dc.Value AS IntakeName,
            dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc,
            p.PartId, p.UniqueId AS PartUniqueId, p.FirstInvitationSent, p.Completed,
            p.AbleCoacheeId, p.UserId AS PartUserId, p.ReportSentUtc, p.AI_ViewResultsCheckUtc,
            p.CanLeaderView360Report, p.PersonalSurveyClosedUtc,
            ps.SelfCountAll, ps.SelfCountCompleted,
            pr.RaterCountInvited, pr.RaterCountCompleted,
            su.UserGuid,
            ac.ProgramJobId, ac.JobNumber, ac.FriendlyProjectTitle, ac.CoachUserId
          FROM sv_360_Participants p
          LEFT OUTER JOIN sv_User su ON su.UserId = p.UserId
          INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
          LEFT OUTER JOIN al_SurveyType st ON st.SurveyTypeCode = s.SurveyTypeCode
          INNER JOIN sv_360_AnswerTypes dt ON p.SurveyId = dt.SurveyId AND dt.AnswerTypeDescr = 'date'
          INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId AND dc.Code = p.DateGroupCode
          OUTER APPLY (
            SELECT COUNT(pr.PartId) AS RaterCountInvited,
              COUNT(pr.Completed) AS RaterCountCompleted
            FROM sv_360_Participants pr
            WHERE pr.Self_PartId = p.PartId
          ) AS pr
          OUTER APPLY (
            SELECT COUNT(ps.PartId) AS SelfCountAll,
              COUNT(ps.Completed) AS SelfCountCompleted
            FROM sv_360_Participants ps
            WHERE ps.SurveyId = p.SurveyId
              AND ps.DateGroupCode = p.DateGroupCode
          ) AS ps
          OUTER APPLY (
            SELECT TOP 1
              ac.ProgramJobId, ac.CoachUserId, ac.DeletedUtc,
              prj.JobNumber,
              CASE WHEN COALESCE(prj.FriendlyProjectTitle, '') <> ''
                THEN prj.FriendlyProjectTitle
                ELSE prj.ProjectName
              END AS FriendlyProjectTitle
            FROM al_Coachees ac
            INNER JOIN id_Job j ON j.JobId = ac.ProgramJobId
            INNER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
            WHERE ac.CoacheeId = p.AbleCoacheeId
          ) as ac
          WHERE
            {(findSurveysBy == FindParticipantsBy.CoacheeId ? "p.AbleCoacheeId = @CoacheeOrUserId" : "p.UserId = @CoacheeOrUserId")}
            {(onlyViewerCompatible == OnlyViewerCompatible.Yes ? "AND s.PrimaryGblAnswerTypeId > 0" : "")}
            {(onlyWhereLeaderCanView360Report == OnlyWhereLeaderCanView360Report.Yes ? "AND (p.CanLeaderView360Report = 1 OR s.AllowReportWithoutRaters = 1)" : "")}
            AND p.IsSelf = 1
            AND ac.DeletedUtc IS NULL
          ORDER BY p.CreatedUTC DESC, s.sv_id",

          dr => {
            var surveyInfo = new SurveyInfo(
              surveyId: dr.GetInt(DbTable.Surveys.IdentityColumn),
              surveyUID: dr.GetString("sv_uniqueid"),
              coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
              tenantOrgId: dr.GetInt("sv_OrgId"),
              companyId: dr.GetIntOrNull("SvCompanyId"),
              companyName: null,
              programJobNumber: dr.GetString("JobNumber"),
              friendlyProjectTitle: dr.GetString("FriendlyProjectTitle"),
              programJobId: dr.GetIntOrNull("ProgramJobId"),
              tenantOrgGuid: Guid.Empty, // TODO get orgGuid once all participants are updated with ProjectId.
              brandingOrgGuid: null,
              createdUtc: dr.GetDateTime("sv_createdUTC"),
              clonedFromSurveyId: dr.GetIntOrNull("ClonedFromSvId"),
              internalTitle: dr.GetString("sv_title"),
              surveyName: !dr.GetString("sv_360GroupName").IsNullOrEmpty() ? dr.GetString("sv_360GroupName") : dr.GetString("sv_title"),
              createdByUserId: dr.GetIntOrNull("CreatedByUserId"),
              surveyTimeZone: DefaultSurveyTimeZoneInfo,
              intakeCodeId: dr.GetInt("IntakeCodeId"),
              intakeNumber: dr.GetInt("IntakeNumber"),
              scheduledStartDateUtc: dr.GetDateTimeOrNull("ScheduledStartDateUtc"),
              sentDate: dr.GetDateTimeOrNull("FirstInvitationSent"),
              closeDateSelfLocal: dr.GetDateTime("IntakeCloseDateSelf"),
              closeDateRatersLocal: dr.GetDateTime("IntakeCloseDate"),
              reportType: ReportTypes.GetReportTypeByDbValue(dr.GetString("sv_ReportType")),
              feedbackOption: (FeedbackOptionEnum)dr.GetInt("sv_FeedbackOption"),
              ratersSuggestedMin: dr.GetIntOrNull("RatersSuggestedMin"),
              ratersSuggestedMax: dr.GetIntOrNull("RatersSuggestedMax"),
              isStrictRaterLimits: dr.GetBoolFromInt("IsStrictRaterLimits"),
              selfsInvited: dr.GetInt("SelfCountAll"),
              selfsCompleted: dr.GetInt("SelfCountCompleted"),
              ratersInvited: dr.GetInt("RaterCountInvited"),
              ratersCompleted: dr.GetInt("RaterCountCompleted"),
              isRatersOnly: dr.GetBoolFromInt("AlbertRatersOnly"),
              hasOpenText: dr.GetBoolFromInt("HasOpenText"),
              groupReportAvailable: dr.GetBoolFromInt("GroupReportAvailable"),
              invitationEmailSubject_Self: "",
              invitationEmailBodyTemplate_Self: "",
              invitationEmailSubject_Rater: "",
              invitationEmailBodyTemplate_Rater: "",
              surveyInstructions_Self: "",
              surveyInstructions_Rater: "",
              workshopEventId: dr.GetIntOrNull("WorkshopEventId"),
              computedSenderEmailName: dr.GetString("ComputedSenderEmailName").ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_FullName),
              computedSenderEmailAddress: dr.GetString("ComputedSenderEmailAddress").ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_Address),
              primaryGblAnswerTypeId: dr.GetIntOrNull("PrimaryGblAnswerTypeId"),
              allowReportWithoutRaters: dr.GetBoolFromInt("AllowReportWithoutRaters"),
              surveyTypeCode: dr.GetString("SurveyTypeCode"),
              isAbleSurvey: dr.GetBoolFromInt("IsAlbertSurvey"),
              showSurveyLoggedIn: dr.GetBoolFromInt("ShowSurveyLoggedIn"),
              hide360ReportNorms: dr.GetBoolFromInt("Hide360ReportNorms"),
              reportInformationHtml: dr.GetString("ReportInformationHtml"),
              disablePDF: dr.GetBoolFromInt("DisablePDF"),
              orgReportMinCompletedRequired: null,
              isSharedWithUser: false
            );
            surveyInfo.SetFoundParticipantBrief(
              surveyInfo: surveyInfo,
              partId: dr.GetInt("PartId"),
              partUniqueId: dr.GetString("PartUniqueId"),
              coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
              coachUserId: dr.GetIntOrNull("CoachUserId"),
              userId: dr.GetIntOrNull("PartUserId"),
              userGuid: dr.GetGuidOrNull("UserGuid"),
              programJobId: dr.GetIntOrNull("ProgramJobId"),
              completedUtc: dr.GetDateTimeOrNull("Completed"),
              ratersInvited: dr.GetInt("RaterCountInvited"),
              ratersCompleted: dr.GetInt("RaterCountCompleted"),
              reportSentUtc: dr.GetDateTimeOrNull("ReportSentUtc"),
              aiViewResultsCheckUtc: dr.GetDateTimeOrNull("AI_ViewResultsCheckUtc"),
              canLeaderView360Report: dr.GetBoolFromInt("CanLeaderView360Report"),
              personalSurveyClosedUtc: dr.GetDateTimeOrNull("PersonalSurveyClosedUtc")
            );
            surveyList.Add(surveyInfo);
            if (!surveyUIdToFind.IsNullOrEmpty() && surveyUIdToFind == surveyInfo.SurveyUID && partUIdToFind == surveyInfo.FoundParticipantBrief.PartUniqueId) {
              _surveyInfoFound = GetSurveyInfo(surveyUIdToFind, partUIdToFind);
            }
          },
          NewSqlParameter("CoacheeOrUserId", coacheeOrUserId)
        );

        surveyInfoFound = _surveyInfoFound;

        return surveyList;
      }

      public static List<SurveyInfo> GetOrgSurveysForCompany(int companyId) {
        return GetOrgSurveyList("s.SvCompanyId = @CompanyId",
          new List<SqlParameter>() { NewSqlParameter("CompanyId", companyId) });
      }

      public static List<SurveyInfo> GetOrgSurveysForClient(AbleUser.AbleUserInfo clientUser) {
        return GetOrgSurveyList($@"
          s.ClonedFromSvId = {ConfigHelper.TemplateSurveyIds.IOS_IOIOnly} AND
          (
            {(clientUser.ClientCompanyId == null ? "" : "s.SvCompanyId = @ClientCompanyId OR")}
            EXISTS (
              SELECT 1
              FROM al_UserProjectAccess upa
              INNER JOIN al_Project ap ON ap.ProjectId = upa.ProjectId
              WHERE ap.SvCompanyId = s.SvCompanyId
                AND upa.UserId = @UserId
                {SQL_IN(SQL_ANDOR.AND, "ap.JobNumber", clientUser.ProjectAccessForJobNumbers)}
            )
          )",
          new List<SqlParameter>() {
            NewSqlParameter("UserId", clientUser.UserId),
            NewSqlParameter("ClientCompanyId", clientUser.ClientCompanyId)
          }
        );
      }

      // TODO this is a v.slow query.
      public static List<SurveyInfo> GetOrgSurveyList(string sqlWhereConditions, List<SqlParameter> sqlParams) {

        sqlParams.Add(NewSqlParameter("IOSSurveyTypeCode", ConfigHelper.SurveyTypeCodes.IOS));

        var surveyList = new List<SurveyInfo>();

        Query($@"

          SELECT
            s.sv_id, s.sv_OrgId, s.sv_uniqueid, s.CreatedByUserId, s.sv_ReportType, s.SvCompanyId, s.ProgramJobId,
            s.SurveyTypeCode, s.ShowSurveyLoggedIn, s.IsAlbertSurvey, s.PrimaryGblAnswerTypeId,
            s.sv_title, s.sv_360GroupName, s.sv_createdUTC, s.ClonedFromSvId,
            s.GroupReportAvailable, s.AlbertRatersOnly, s.ReportInformationHtml, s.DisablePDF, s.OrgReportMinResponsesToShow,
            ij.JobNumber, ap.FriendlyProjectTitle,
            cmp.CompanyName,
            dbo.fnGetOrgSenderEmailName(s.sv_OrgId) AS ComputedSenderEmailName,
            dbo.fnGetOrgSenderEmailAddress(s.sv_OrgId) AS ComputedSenderEmailAddress,
            dc.IntakeCodeId, dc.IntakeNumber, dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc,
            p.PartId, p.PartUniqueId, p.FirstInvitationSent, p.Completed,
            p.AbleCoacheeId, p.PartUserId, p.ReportSentUtc, p.AI_ViewResultsCheckUtc,
            p.CanLeaderView360Report, p.PersonalSurveyClosedUtc,
            ps.SelfCountAll, ps.SelfCountCompleted, ps.FirstInvitationSent,
            org.OrgGuid
          FROM sv_Survey s
          INNER JOIN al_SurveyType st ON st.SurveyTypeCode = s.SurveyTypeCode
          INNER JOIN id_Job ij ON ij.JobId = s.ProgramJobId
          INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
          INNER JOIN sv_SurveyCompany cmp ON cmp.SvCompanyId = ap.SvCompanyId
          INNER JOIN sv_Organisation org ON org.OrgId = ap.ParentOrgId
          CROSS APPLY (
            SELECT TOP 1
              p.PartId, p.UniqueId AS PartUniqueId, p.FirstInvitationSent, p.Completed,
              p.AbleCoacheeId, p.UserId AS PartUserId, p.ReportSentUtc, p.AI_ViewResultsCheckUtc,
              p.CanLeaderView360Report, p.PersonalSurveyClosedUtc
            FROM sv_360_Participants p
            WHERE p.SurveyId = s.sv_id
          ) AS p
          CROSS APPLY (
            SELECT TOP 1
              dc.CodeId AS IntakeCodeId, dc.Code AS IntakeNumber,
              dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc
            FROM sv_360_AnswerTypes dt
            INNER JOIN sv_360_Codes dc ON dc.AnswerTypeId = dt.AnswerTypeId
            WHERE dt.SurveyId = s.sv_id
              AND dt.AnswerTypeDescr = 'date'
            ORDER BY dc.Code
          ) AS dc
          OUTER APPLY (
            SELECT
              COUNT(ps.PartId) AS SelfCountAll,
              COUNT(ps.Completed) AS SelfCountCompleted,
              MIN(ps.FirstInvitationSent) AS FirstInvitationSent
            FROM sv_360_Participants ps
            WHERE ps.SurveyId = s.sv_id
          ) AS ps
          WHERE s.SurveyTypeCode = @IOSSurveyTypeCode
            {sqlWhereConditions.EnsureStartsWith("AND ", true)}
          ORDER BY s.sv_createdUTC DESC, s.sv_id",

          dr => {
            var surveyInfo = new SurveyInfo(
              surveyId: dr.GetInt(DbTable.Surveys.IdentityColumn),
              surveyUID: dr.GetString("sv_uniqueid"),
              coacheeId: null,
              tenantOrgId: dr.GetInt("sv_OrgId"),
              tenantOrgGuid: dr.GetGuid("OrgGuid"),
              companyId: dr.GetIntOrNull("SvCompanyId"),
              companyName: dr.GetString("CompanyName"),
              programJobNumber: dr.GetString("JobNumber"),
              friendlyProjectTitle: dr.GetString("FriendlyProjectTitle"),
              programJobId: dr.GetIntOrNull("ProgramJobId"),
              brandingOrgGuid: null,
              createdUtc: dr.GetDateTime("sv_createdUTC"),
              clonedFromSurveyId: dr.GetIntOrNull("ClonedFromSvId"),
              internalTitle: dr.GetString("sv_title"),
              surveyName: !dr.GetString("sv_360GroupName").IsNullOrEmpty() ? dr.GetString("sv_360GroupName") : dr.GetString("sv_title"),
              createdByUserId: dr.GetIntOrNull("CreatedByUserId"),
              surveyTimeZone: DefaultSurveyTimeZoneInfo,
              intakeCodeId: dr.GetInt("IntakeCodeId"),
              intakeNumber: dr.GetInt("IntakeNumber"),
              scheduledStartDateUtc: dr.GetDateTimeOrNull("ScheduledStartDateUtc"),
              sentDate: dr.GetDateTimeOrNull("FirstInvitationSent"),
              closeDateSelfLocal: dr.GetDateTime("IntakeCloseDateSelf"),
              closeDateRatersLocal: dr.GetDateTime("IntakeCloseDate"),
              reportType: ReportTypes.GetReportTypeByDbValue(dr.GetString("sv_ReportType")),
              feedbackOption: FeedbackOptionEnum.NoRaters,
              ratersSuggestedMin: null,
              ratersSuggestedMax: null,
              isStrictRaterLimits: false,
              selfsInvited: dr.GetInt("SelfCountAll"),
              selfsCompleted: dr.GetInt("SelfCountCompleted"),
              ratersInvited: null,
              ratersCompleted: null,
              isRatersOnly: false,
              hasOpenText: false,
              groupReportAvailable: false,
              invitationEmailSubject_Self: "",
              invitationEmailBodyTemplate_Self: "",
              invitationEmailSubject_Rater: "",
              invitationEmailBodyTemplate_Rater: "",
              surveyInstructions_Self: "",
              surveyInstructions_Rater: "",
              workshopEventId: null,
              computedSenderEmailName: dr.GetString("ComputedSenderEmailName").ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_FullName),
              computedSenderEmailAddress: dr.GetString("ComputedSenderEmailAddress").ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_Address),
              primaryGblAnswerTypeId: dr.GetIntOrNull("PrimaryGblAnswerTypeId"),
              allowReportWithoutRaters: true,
              surveyTypeCode: dr.GetString("SurveyTypeCode"),
              isAbleSurvey: dr.GetBoolFromInt("IsAlbertSurvey"),
              showSurveyLoggedIn: dr.GetBoolFromInt("ShowSurveyLoggedIn"),
              hide360ReportNorms: false,
              reportInformationHtml: dr.GetString("ReportInformationHtml"),
              disablePDF: dr.GetBoolFromInt("DisablePDF"),
              orgReportMinCompletedRequired: dr.GetIntOrNull("OrgReportMinResponsesToShow"),
              isSharedWithUser: false
            );
            surveyInfo.SetFoundParticipantBrief(
              surveyInfo: surveyInfo,
              partId: dr.GetInt("PartId"),
              partUniqueId: dr.GetString("PartUniqueId"),
              coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
              coachUserId: null,
              userId: dr.GetIntOrNull("PartUserId"),
              userGuid: null,
              programJobId: dr.GetIntOrNull("ProgramJobId"),
              completedUtc: dr.GetDateTimeOrNull("Completed"),
              ratersInvited: 0,
              ratersCompleted: 0,
              reportSentUtc: null,
              aiViewResultsCheckUtc: dr.GetDateTimeOrNull("AI_ViewResultsCheckUtc"),
              canLeaderView360Report: dr.GetBoolFromInt("CanLeaderView360Report"),
              personalSurveyClosedUtc: dr.GetDateTimeOrNull("PersonalSurveyClosedUtc")
            );
            surveyList.Add(surveyInfo);
          },
          sqlParams.ToArray()
        );

        return surveyList;
      }

      public static TemplateList GetTemplateList() {

        var templateList = new TemplateList();

        string sql = @"
          SELECT
            s.sv_id, sv_uniqueid, s.sv_title, s.sv_360GroupName, s.IsAlbertSurvey,
            s.AlbertRatersOnly, s.sv_FeedbackOption, s.SurveyTypeCode, s.sv_ReportType, s.ShowSurveyLoggedIn,
            q.QnCount
          FROM sv_Survey s
          CROSS APPLY (
            SELECT COUNT(*) AS QnCount
            FROM sv_360_Questions q
            WHERE q.SurveyId = s.sv_id
          ) AS q
          WHERE s.IsTemplate = 1
          AND s.SurveyTypeCode IN(@SurveyTypeCode_360, @SurveyTypeCode_DevPlan, @SurveyTypeCode_Eval, @SurveyTypeCode_Intake, @SurveyTypeCode_IOS, @SurveyTypeCode_Pulse)
          ORDER BY s.IsAlbertSurvey DESC, s.sv_ReportType, s.sv_title";

        Query(sql,
          dr => {
            templateList.Add(new TemplateListItem(
              surveyId: dr.GetInt("sv_id"),
              uniqueId: dr.GetString("sv_uniqueid"),
              isAlbertSurvey: dr.GetBoolFromInt("IsAlbertSurvey"),
              surveyTypeCode: dr.GetString("SurveyTypeCode"),
              reportType: dr.GetString("sv_ReportType"),
              internalTitle: dr.GetString("sv_title"),
              surveyName: !dr.GetString("sv_360GroupName").IsNullOrEmpty() ? dr.GetString("sv_360GroupName") : dr.GetString("sv_title"),
              qnCount: dr.GetInt("QnCount"),
              albertRatersOnly: dr.GetBoolFromInt("AlbertRatersOnly"),
              showSurveyLoggedIn: dr.GetBoolFromInt("ShowSurveyLoggedIn"),
              feedbackOption: (FeedbackOptionEnum)dr.GetInt("sv_FeedbackOption")
            ));
          },
          NewSqlParameter("SurveyTypeCode_360", ConfigHelper.SurveyTypeCodes.Able360),
          NewSqlParameter("SurveyTypeCode_DevPlan", ConfigHelper.SurveyTypeCodes.DevPlan),
          NewSqlParameter("SurveyTypeCode_Eval", ConfigHelper.SurveyTypeCodes.Eval),
          NewSqlParameter("SurveyTypeCode_Intake", ConfigHelper.SurveyTypeCodes.Intake),
          NewSqlParameter("SurveyTypeCode_IOS", ConfigHelper.SurveyTypeCodes.IOS),
          NewSqlParameter("SurveyTypeCode_Pulse", ConfigHelper.SurveyTypeCodes.Pulse360)
        );

        return templateList;
      }

      public static TemplateInfo GetTemplateInfo(int SurveyId, string SurveyUniqueId = "") {
        // Get all data from template survey used to copy across to a new survey.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"
          SELECT " + DbTable.Surveys.IdentityColumn + @",
            sv_OrgId, sv_title, sv_360GroupName,
            sv_typecode, sv_ReportType, ReportSubType, sv_ReportTitle,
            SurveyTypeCode, ShowSurveyLoggedIn,
            sv_intro_directions, sv_intro_directionsOther, ReportInformationHtml, DisablePDF, OrgReportMinResponsesToShow,
            sv_SelfEmailSubject, sv_SelfEmailText, sv_OtherEmailSubject, sv_OtherEmailText,
            sv_FeedbackOption, sv_QuestionSets, sv_ManagerQnSet, sv_PresetQnSet, sv_AltCopyrightText,
            RatersSuggestedMin, RatersSuggestedMax, IsStrictRaterLimits, IsAlbertSurvey,
            RaterInheritsLevel, GroupReportAvailable, AlbertRatersOnly, PrimaryGblAnswerTypeId, AllowReportWithoutRaters, Hide360ReportNorms
          FROM " + DbTable.Surveys + @"
          WHERE (" + (!SurveyUniqueId.IsNullOrEmpty() ? "sv_uniqueid" : DbTable.Surveys.IdentityColumn) + @" = @SurveyId)
            AND (IsTemplate = 1)", conn)) {
            if (!SurveyUniqueId.IsNullOrEmpty()) {
              cmd.Parameters.Add("@SurveyId", SqlDbType.VarChar, 50).Value = SurveyUniqueId;
            } else {
              cmd.Parameters.Add("@SurveyId", SqlDbType.Int).Value = SurveyId;
            }
            conn.Open();
            try {
              using (SqlDataReader dr = cmd.ExecuteReader()) {
                if (dr.Read()) {
                  return new TemplateInfo(
                    surveyId: dr.GetInt(DbTable.Surveys.IdentityColumn),
                    orgId: dr.GetInt("sv_OrgId"),
                    internalTitle: dr.GetString("sv_title"),
                    surveyName: !dr.GetString("sv_360GroupName").IsNullOrEmpty() ? dr.GetString("sv_360GroupName") : dr.GetString("sv_title"),
                    svTypeCode: dr.GetString("sv_typecode"),
                    surveyTypeCode: dr.GetString("SurveyTypeCode"),
                    reportType: ReportTypes.GetReportTypeByDbValue(dr.GetString("sv_ReportType")),
                    reportSubType: dr.GetString("ReportSubType"),
                    reportTitle: dr.GetString("sv_ReportTitle"),
                    introDirections: dr.GetString("sv_intro_directions"),
                    introDirectionsRater: dr.GetString("sv_intro_directionsOther"),
                    invitationEmailSubject_Self: dr.GetString("sv_SelfEmailSubject"),
                    invitationEmailBodyTemplate_Self: dr.GetString("sv_SelfEmailText"),
                    invitationEmailSubject_Rater: dr.GetString("sv_OtherEmailSubject"),
                    invitationEmailBodyTemplate_Rater: dr.GetString("sv_OtherEmailText"),
                    feedbackOption: (FeedbackOptionEnum)dr.GetInt("sv_FeedbackOption"),
                    groupReportAvailable: dr.GetBoolFromInt("GroupReportAvailable"),
                    questionSets: dr.GetInt("sv_QuestionSets"),
                    managerQuestionSet: dr.GetInt("sv_ManagerQnSet"),
                    presetQuestionSet: dr.GetInt("sv_PresetQnSet"),
                    customCopyrightText: dr.GetString("sv_AltCopyrightText"),
                    ratersSuggestedMin: dr.GetIntOrNull("RatersSuggestedMin"),
                    ratersSuggestedMax: dr.GetIntOrNull("RatersSuggestedMax"),
                    isStrictRaterLimits: dr.GetBoolFromInt("IsStrictRaterLimits"),
                    raterInheritsLevel: dr.GetBoolFromInt("RaterInheritsLevel"),
                    isRatersOnly: dr.GetBoolFromInt("AlbertRatersOnly"),
                    showSurveyLoggedIn: dr.GetBoolFromInt("ShowSurveyLoggedIn"),
                    isAlbertSurvey: dr.GetBoolFromInt("IsAlbertSurvey"),
                    primaryGblAnswerTypeId: dr.GetIntOrNull("PrimaryGblAnswerTypeId"),
                    allowReportWithoutRaters: dr.GetBoolFromInt("AllowReportWithoutRaters"),
                    hide360ReportNorms: dr.GetBoolFromInt("Hide360ReportNorms"),
                    reportInformationHtml: dr.GetString("ReportInformationHtml"),
                    disablePDF: dr.GetBoolFromInt("DisablePDF"),
                    orgReportMinCompletedRequired: dr.GetIntOrNull("OrgReportMinResponsesToShow")
                  );
                } else {
                  return null; // Survey id not found or isn't a template.
                }
              }
            } catch (Exception ex) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .WithOperation(nameof(GetTemplateInfo))
                .WithProperty(DalApplicationInsightsConstants.SurveyId, SurveyId)
                .WithProperty(DalApplicationInsightsConstants.SurveyUniqueId, SurveyUniqueId)
                .WithProperty(DalApplicationInsightsConstants.CommandText, cmd.CommandText)
                .Track();

              throw new Exception("<code>sql: " + cmd.CommandText + "\n SurveyId = " + SurveyId + "\n SurveyUniqueId = " + SurveyUniqueId + "</code>", ex);
            }
          }
        }
      }

      public static SurveyInfo GetSurveyInfo(int surveyId, int intakeCode) {
        return GetSurveyInfo(null, surveyId, intakeCode);
      }

      public static SurveyInfo GetSurveyInfo(SqlTransaction trans, int surveyId, int intakeCode) {

        SurveyInfo surveyInfo = null;

        Query(trans, GetSurveySQL("s.sv_id = @SurveyId AND dc.Code = @IntakeCode", null),
          dr => {
            surveyInfo = GetSurveyInfo(dr, false);
          },
          NewSqlParameter("@SurveyId", surveyId),
          NewSqlParameter("@IntakeCode", intakeCode)
        );

        return surveyInfo;
      }

      public static SurveyInfo GetSurveyInfoForUser(string surveyUId, AbleUser.UserIdentity forUser, int intakeCode = 1) {

        SurveyInfo surveyInfo = null;

        Query(GetSurveySQL("s.sv_uniqueid = @SurveyUId AND dc.Code = @IntakeCode", null, forUser),
          dr => {
            surveyInfo = GetSurveyInfo(dr, true, forUser);
          },
          NewSqlParameter("@SurveyUId", surveyUId),
          NewSqlParameter("@ForUserId", forUser?.UserId),
          NewSqlParameter("@IntakeCode", intakeCode)
        );

        return surveyInfo;
      }

      public static SurveyInfo GetSurveyInfo(string surveyUId, int intakeCode = 1) {

        SurveyInfo surveyInfo = null;

        Query(GetSurveySQL("s.sv_uniqueid = @SurveyUId AND dc.Code = @IntakeCode", null),
          dr => {
            surveyInfo = GetSurveyInfo(dr, true);
          },
          NewSqlParameter("@SurveyUId", surveyUId),
          NewSqlParameter("@IntakeCode", intakeCode)
        );

        return surveyInfo;
      }

      public static SurveyInfo GetSurveyInfo(string surveyUId, string partUID) {

        SurveyInfo surveyInfo = null;

        Query(GetSurveySQL("s.sv_uniqueid = @SurveyUId AND p.UniqueId = @PartUID", null),
          dr => {
            surveyInfo = GetSurveyInfo(dr, true);
          },
          NewSqlParameter("@SurveyUId", surveyUId),
          NewSqlParameter("@PartUID", partUID, 50)
        );

        return surveyInfo;
      }

      public static SurveyInfo GetSurveyInfoByPartId(int partId) {

        SurveyInfo surveyInfo = null;

        Query(GetSurveySQL("p.PartId = @PartId", null),
          dr => {
            surveyInfo = GetSurveyInfo(dr, true);
          },
          NewSqlParameter("@PartId", partId)
        );
        return surveyInfo;
      }

      public static SurveyInfo GetSurveyInfoForCoacheeId(string surveyUId, int coacheeId) {

        SurveyInfo surveyInfo = null;

        Query(GetSurveySQL("s.sv_uniqueid = @SurveyUId AND p.AbleCoacheeId = @AbleCoacheeId", null),
          dr => {
            surveyInfo = GetSurveyInfo(dr, true);
          },
          NewSqlParameter("@SurveyUId", surveyUId, 50),
          NewSqlParameter("@AbleCoacheeId", coacheeId)
        );

        return surveyInfo;
      }

      public static SurveyInfo GetLatest360SurveyForCoacheeId(int coacheeId) {

        SurveyInfo surveyInfo = null;

        Query(GetSurveySQL($"p.AbleCoacheeId = @CoacheeId AND {SQLWhereConditions.Standard360Surveys("s")}", "p.CreatedUTC DESC"),
          dr => {
            surveyInfo = GetSurveyInfo(dr, true);
          },
          NewSqlParameter("@CoacheeId", coacheeId)
        );

        return surveyInfo;
      }

      public static SurveyInfo GetLatestIntakeSurveyForUserId(int? userId) {

        SurveyInfo surveyInfo = null;

        Query(GetSurveySQL($"p.UserId = @UserId AND {SQLWhereConditions.IntakeSurveys("s")} AND p.CreatedUTC >= DATEADD(month, -@MonthsToCreateNewSurveyIntake, GETDATE())", "p.CreatedUTC DESC"),
          dr => {
            surveyInfo = GetSurveyInfo(dr, true);
          },
          NewSqlParameter("@UserId", userId),
          NewSqlParameter("@MonthsToCreateNewSurveyIntake", ConfigHelper.MonthsToCreateNewSurveyIntake)
        );

        return surveyInfo;
      }

      private static string GetSurveySQL(string whereClause, string orderByClause, AbleUser.UserIdentity forUser = null) {

        string sql = $@"
          SELECT TOP 1
            s.sv_id, s.sv_OrgId, s.sv_uniqueid, s.CreatedByUserId, s.sv_ReportType, s.SvCompanyId,
            s.SurveyTypeCode, s.ShowSurveyLoggedIn, s.IsAlbertSurvey,
            s.sv_title, s.sv_360GroupName, s.sv_createdUTC, s.ClonedFromSvId,
            s.GroupReportAvailable, s.AlbertRatersOnly,
            s.sv_FeedbackOption, s.RatersSuggestedMin, s.RatersSuggestedMax, s.IsStrictRaterLimits,
            s.sv_SelfEmailSubject, s.sv_SelfEmailText, s.sv_OtherEmailSubject, s.sv_OtherEmailText,
            s.sv_intro_directions, s.sv_intro_directionsOther, s.ReportInformationHtml, s.DisablePDF, s.OrgReportMinResponsesToShow,
            s.WorkshopEventId, s.PrimaryGblAnswerTypeId, s.AllowReportWithoutRaters, s.Hide360ReportNorms,
            {GetHasOpenTextSQL()},
            dbo.fnGetOrgSenderEmailName(s.sv_OrgId) AS ComputedSenderEmailName,
            dbo.fnGetOrgSenderEmailAddress(s.sv_OrgId) AS ComputedSenderEmailAddress,
            prj.ProgramJobId, prj.JobNumber, prj.FriendlyProjectTitle, prj.ProjectName,
            prj.ParentOrgGuid, prj.BrandingOrgGuid,
            dc.CodeId AS IntakeCodeId, dc.Code AS IntakeNumber, dc.Value AS IntakeName,
            dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc,
            p.PartId, p.UniqueId AS PartUniqueId, p.AbleCoacheeId,
            p.CoachUserId, p.UserId AS PartUserId, p.FirstInvitationSent, p.Completed,
            p.ReportSentUtc, p.AI_ViewResultsCheckUtc,
            p.CanLeaderView360Report, p.PersonalSurveyClosedUtc,
            ps.SelfCountAll, ps.SelfCountCompleted,
            pr.RaterCountInvited, pr.RaterCountCompleted,
            su.UserGuid,
            {(forUser == null ? "0" : "IIF(ss.UserId IS NOT NULL, 1, 0)")} AS IsSharedWithUser
          FROM sv_survey s
          LEFT OUTER JOIN al_SurveyType st ON st.SurveyTypeCode = s.SurveyTypeCode
          LEFT OUTER JOIN sv_Organisation org ON org.OrgId = s.sv_OrgId
          INNER JOIN sv_360_AnswerTypes dt ON s.sv_id = dt.SurveyId
          INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId
          LEFT OUTER JOIN sv_360_Participants p ON p.IntakeCodeId = dc.CodeId
          LEFT OUTER JOIN sv_User su ON su.UserId = p.UserId
          {(forUser == null ? string.Empty : @"
            OUTER APPLY (
              SELECT TOP 1 ss.UserId
              FROM sv_SurveyShare ss
              WHERE ss.ParticipantId = p.PartId
              AND ss.UserId = @ForUserId
            ) AS ss"
          )}
          CROSS APPLY (
            SELECT
              COUNT(ps.PartId) AS SelfCountAll,
              COUNT(ps.Completed) AS SelfCountCompleted
            FROM sv_360_Participants ps
            WHERE ps.SurveyId = p.SurveyId
              AND ps.DateGroupCode = p.DateGroupCode
          ) AS ps
          CROSS APPLY (
            SELECT COUNT(pr.PartId) AS RaterCountInvited, COUNT(pr.Completed) AS RaterCountCompleted
            FROM sv_360_Participants pr
            WHERE pr.Self_PartId = p.PartId
          ) AS pr
          OUTER APPLY (
            SELECT TOP 1
              j.JobId AS ProgramJobId,
              prj.JobNumber, prj.FriendlyProjectTitle, prj.ProjectName,
              org_p.OrgGuid AS ParentOrgGuid, org_b.OrgGuid AS BrandingOrgGuid
            FROM al_Project prj
            INNER JOIN id_Job j ON prj.JobNumber = j.JobNumber
            INNER JOIN sv_JoinJobsAndIntakes ji ON j.JobId = ji.JobId
            INNER JOIN sv_Organisation org_p ON org_p.OrgId = prj.ParentOrgId
            LEFT OUTER JOIN sv_Organisation org_b ON org_b.OrgId = prj.BrandingOrgId
            WHERE ji.DateCodeId = dc.CodeId
          ) AS prj
          WHERE dt.AnswerTypeDescr = 'date'";

        if (!whereClause.IsNullOrEmptyOrWhitespace()) {
          sql += " AND " + whereClause;
        }

        if (!orderByClause.IsNullOrEmptyOrWhitespace()) {
          sql += " ORDER BY " + orderByClause;
        }

        return sql;
      }

      private static SurveyInfo GetSurveyInfo(SqlDataReader dr, bool getPartIds, AbleUser.UserIdentity forUser = null) {

        var surveyInfo = new SurveyInfo(
          surveyId: dr.GetInt(DbTable.Surveys.IdentityColumn),
          surveyUID: dr.GetString("sv_uniqueid"),
          coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
          tenantOrgId: dr.GetInt("sv_OrgId"),
          tenantOrgGuid: dr.GetGuidOrNull("ParentOrgGuid") ?? Guid.Empty,
          companyId: dr.GetIntOrNull("SvCompanyId"),
          companyName: null,
          programJobNumber: dr.GetString("JobNumber"),
          friendlyProjectTitle: dr.GetString("FriendlyProjectTitle").ValueIfNullOrEmpty(dr.GetString("ProjectName")),
          programJobId: dr.GetIntOrNull("ProgramJobId"),
          brandingOrgGuid: dr.GetGuidOrNull("BrandingOrgGuid"),
          createdUtc: dr.GetDateTime("sv_createdUTC"),
          clonedFromSurveyId: dr.GetIntOrNull("ClonedFromSvId"),
          internalTitle: dr.GetString("sv_title"),
          surveyName: !dr.GetString("sv_360GroupName").IsNullOrEmpty() ? dr.GetString("sv_360GroupName") : dr.GetString("sv_title"),
          createdByUserId: dr.GetIntOrNull("CreatedByUserId"),
          surveyTimeZone: DefaultSurveyTimeZoneInfo,
          intakeCodeId: dr.GetInt("IntakeCodeId"),
          intakeNumber: dr.GetInt("IntakeNumber"),
          scheduledStartDateUtc: dr.GetDateTimeOrNull("ScheduledStartDateUtc"),
          sentDate: dr.GetDateTimeOrNull("FirstInvitationSent"),
          closeDateSelfLocal: dr.GetDateTime("IntakeCloseDateSelf"),
          closeDateRatersLocal: dr.GetDateTime("IntakeCloseDate"),
          reportType: ReportTypes.GetReportTypeByDbValue(dr.GetString("sv_ReportType")),
          feedbackOption: (FeedbackOptionEnum)dr.GetInt("sv_FeedbackOption"),
          ratersSuggestedMin: dr.GetIntOrNull("RatersSuggestedMin"),
          ratersSuggestedMax: dr.GetIntOrNull("RatersSuggestedMax"),
          isStrictRaterLimits: dr.GetBoolFromInt("IsStrictRaterLimits"),
          selfsInvited: dr.GetInt("SelfCountAll"),
          selfsCompleted: dr.GetInt("SelfCountCompleted"),
          ratersInvited: null,
          ratersCompleted: null,
          isRatersOnly: dr.GetBoolFromInt("AlbertRatersOnly"),
          hasOpenText: dr.GetBoolFromInt("HasOpenText"),
          groupReportAvailable: dr.GetBoolFromInt("GroupReportAvailable"),
          invitationEmailSubject_Self: dr.GetString("sv_SelfEmailSubject"),
          invitationEmailBodyTemplate_Self: dr.GetString("sv_SelfEmailText"),
          invitationEmailSubject_Rater: dr.GetString("sv_OtherEmailSubject"),
          invitationEmailBodyTemplate_Rater: dr.GetString("sv_OtherEmailText"),
          surveyInstructions_Self: dr.GetString("sv_intro_directions"),
          surveyInstructions_Rater: dr.GetString("sv_intro_directionsOther"),
          workshopEventId: dr.GetIntOrNull("WorkshopEventId"),
          computedSenderEmailName: dr.GetString("ComputedSenderEmailName").ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_FullName),
          computedSenderEmailAddress: dr.GetString("ComputedSenderEmailAddress").ValueIfNullOrEmpty(ConfigHelper.Email_Coordinator_Address),
          primaryGblAnswerTypeId: dr.GetIntOrNull("PrimaryGblAnswerTypeId"),
          allowReportWithoutRaters: dr.GetBoolFromInt("AllowReportWithoutRaters"),
          surveyTypeCode: dr.GetString("SurveyTypeCode"),
          isAbleSurvey: dr.GetBoolFromInt("IsAlbertSurvey"),
          showSurveyLoggedIn: dr.GetBoolFromInt("ShowSurveyLoggedIn"),
          hide360ReportNorms: dr.GetBoolFromInt("Hide360ReportNorms"),
          reportInformationHtml: dr.GetString("ReportInformationHtml"),
          disablePDF: dr.GetBoolFromInt("DisablePDF"),
          orgReportMinCompletedRequired: dr.GetIntOrNull("OrgReportMinResponsesToShow"),
          isSharedWithUser: forUser == null ? false : (dr.GetBoolFromIntOrNull("IsSharedWithUser") ?? false)
        );
        if (getPartIds && !dr.IsDBNull("PartId")) {
          surveyInfo.SetFoundParticipantBrief(
            surveyInfo: surveyInfo,
            partId: dr.GetInt("PartId"),
            partUniqueId: dr.GetString("PartUniqueId"),
            coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
            coachUserId: dr.GetIntOrNull("CoachUserId"),
            userId: dr.GetIntOrNull("PartUserId"),
            userGuid: dr.GetGuidOrNull("UserGuid"),
            programJobId: dr.GetIntOrNull("ProgramJobId"),
            completedUtc: dr.GetDateTimeOrNull("Completed"),
            ratersInvited: dr.GetInt("RaterCountInvited"),
            ratersCompleted: dr.GetInt("RaterCountCompleted"),
            reportSentUtc: dr.GetDateTimeOrNull("ReportSentUtc"),
            aiViewResultsCheckUtc: dr.GetDateTimeOrNull("AI_ViewResultsCheckUtc"),
            canLeaderView360Report: dr.GetBoolFromInt("CanLeaderView360Report"),
            personalSurveyClosedUtc: dr.GetDateTimeOrNull("PersonalSurveyClosedUtc")
          );
        }
        return surveyInfo;
      }

      public enum GetPreSurveyBy { CoacheeId, UserId }

      public static void GetFirstCompletedPreSurvey(
        GetPreSurveyBy getPreSurveyBy,
        SurveyInfo currentSurvey,
        AlbertCoachees.AlbertCoacheeInfo currentSurveyCoachee,
        out bool hasPreSurvey, out string preSurveyUid, out int preSurveyIntakeId,
        out string preSurveyPartUId, out int preSurveyPartId, out DateTime preSurveyCompletedUtc) {

        bool _hasPreSurvey = false;
        int _intakeId = 0;
        int _partId = 0;
        string _surveyUId = "", _partUid = "";
        DateTime _completedUtc = DateTime.MinValue;

        string getByCondition;
        if (getPreSurveyBy == GetPreSurveyBy.CoacheeId) {
          getByCondition = "sp.AbleCoacheeId = @CoacheeId AND sp.PrePost360ForCoachee = @PrePostFlag";
        } else {
          getByCondition = "sp.UserId = @UserId AND sp.PrePost360ForUser = @PrePostFlag";
        }

        Query($@"
          SELECT TOP 1 sv.sv_UniqueId, sp.PartId, sp.UniqueId AS PartUId, sp.Completed, sp.IntakeCodeId
          FROM sv_360_Participants sp
          INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
          WHERE sv.SurveyTypeCode = @SurveyTypeCode
            AND {getByCondition}
            AND sp.Completed IS NOT NULL
            AND sp.Completed < @CurrentCompletedUtc
            AND sp.PartId <> @CurrentPartId
            AND sp.IsSelf = 1
          ORDER BY sp.Completed;",
          dr => {
            _hasPreSurvey = true;
            _surveyUId = dr.GetString("sv_UniqueId");
            _partId = dr.GetInt("PartId");
            _partUid = dr.GetString("PartUId");
            _intakeId = dr.GetInt("IntakeCodeId");
            _completedUtc = dr.GetDateTime("Completed");
          },
          NewSqlParameter("SurveyTypeCode", currentSurvey.SurveyTypeCode),
          NewSqlParameter("CoacheeId", currentSurveyCoachee.CoacheeId),
          NewSqlParameter("UserId", currentSurveyCoachee.UserId),
          NewSqlParameter("PrePostFlag", Participants.PrePostFlags.IsPreSurvey),
          NewSqlParameter("CurrentPartId", currentSurvey.FoundParticipantBrief.PartId),
          NewSqlParameter("CurrentCompletedUtc", currentSurvey.FoundParticipantBrief.CompletedUtc)
        );

        hasPreSurvey = _hasPreSurvey;
        preSurveyUid = _surveyUId;
        preSurveyIntakeId = _intakeId;
        preSurveyPartId = _partId;
        preSurveyPartUId = _partUid;
        preSurveyCompletedUtc = _completedUtc;
      }

      public static SurveyInfo CreateCoacheeIntakeSurvey(AlbertCoachees.AlbertCoacheeInfo coacheeInfo, DbHelper.Projects.ProjectInfo projectInfo) {

        int templateId = projectInfo.IntakeSurveyTemplateId ?? ConfigHelper.TemplateSurveyIds.Intake;

        // If coachee does not have Coaching, but has Workshops or AI Coaching, use the Coaching Intake template.
        if (!coacheeInfo.HasCoaching && (coacheeInfo.HasWorkshops || coacheeInfo.UserHasAICoach)) {
          templateId = ConfigHelper.TemplateSurveyIds.CoachingIntake;
        }

        // If intakeSurveyTemplateId has a value, use that. Otherwise, use the default.
        var templateInfo = GetTemplateInfo(templateId);
        templateInfo.SetSurveyName($"Intake Form for {coacheeInfo.GetFullName()}");
        DateTime closeDateUtc = DateTime.UtcNow.AddDays(ConfigHelper.Email_Welcome_SurveyOpenDays);

        SurveyInfo newSurveyInfo = null;
        NewSurveyIdInfo newSurveyIdInfo = null;
        Participants.AddParticipantToSurveyInfo newPartInfo = null;

        UsingTransaction(trans => {

          newSurveyIdInfo = AddSurveyStub(
            trans: trans,
            createdByUserId: AbleUser.GetAutomationUser().UserId,
            templateSurvey: templateInfo,
            companyId: projectInfo?.CompanyId,
            programJobId: coacheeInfo.ProgramJobId,
            isProgramSurvey: false,
            firstIntakeName: "Intake 1 - " + coacheeInfo.GetFullName(),
            closeDateInCoacheeLocalTime_Self: closeDateUtc,
            closeDateInCoacheeLocalTime_Rater: closeDateUtc,
            scheduledStartDateUTC: null);

          newSurveyInfo = GetSurveyInfo(trans, newSurveyIdInfo.SurveyId, newSurveyIdInfo.IntakeCode);

          newPartInfo = new Participants.AddParticipantToSurveyInfo(newSurveyIdInfo, coacheeInfo);
          Participants.AddParticipantToSurvey(trans.Connection, trans, newPartInfo);

          return true;
        });

        newSurveyInfo.SetFoundParticipantBrief(
          surveyInfo: newSurveyInfo,
          partId: newPartInfo.NewPartId,
          partUniqueId: newPartInfo.NewPartUID,
          coacheeId: coacheeInfo.CoacheeId,
          coachUserId: coacheeInfo.CoachUserId,
          userId: coacheeInfo.UserId,
          userGuid: coacheeInfo.UserGuid,
          programJobId: coacheeInfo.ProgramJobId,
          completedUtc: null,
          ratersInvited: 0,
          ratersCompleted: 0,
          reportSentUtc: null,
          aiViewResultsCheckUtc: null,
          canLeaderView360Report: false,
          personalSurveyClosedUtc: null
        );

        return newSurveyInfo;
      }

      public class NewSurveyIdInfo {
        public int SurveyId { get; internal set; }
        public string SurveyUID { get; internal set; }
        public int IntakeCode { get; internal set; }
        public int IntakeCodeId { get; internal set; }
        public NewSurveyIdInfo() { }
        public NewSurveyIdInfo(int surveyId, string surveyUID, int intakeNumber, int intakeCodeId) {
          SurveyId = surveyId;
          SurveyUID = surveyUID;
          IntakeCode = intakeNumber;
          IntakeCodeId = intakeCodeId;
        }
      }

      // TODO: Specify UTC for date arguments, then convert to "AppDefaultTimeZone" (WST) when saving to db.
      public static NewSurveyIdInfo AddSurveyStub(
        SqlTransaction trans,
        int createdByUserId,
        TemplateInfo templateSurvey,
        int? companyId,
        int? programJobId,
        bool isProgramSurvey,
        string firstIntakeName,
        DateTime closeDateInCoacheeLocalTime_Self,
        DateTime closeDateInCoacheeLocalTime_Rater,
        DateTime? scheduledStartDateUTC,
        string surveyTitleOrNullForDefault = null) {

        if (trans == null) throw new ArgumentNullException(nameof(trans), "Transaction required.");

        var newSurveyIdInfo = new NewSurveyIdInfo();
        int tries = 0;

        string sql = @"
          INSERT INTO " + DbTable.Surveys + @" (
            sv_OrgId, sv_uniqueid, sv_typecode, sv_ReportType, ReportSubType,
            SvCompanyId, ProgramJobId, IsProgramSurvey,
            SurveyTypeCode, ShowSurveyLoggedIn,
            sv_title, sv_360GroupName, ClonedFromSvId,
            CreatedByUserId, AlbertRatersOnly,
            sv_offlineid, sv_participants, sv_responses,
            sv_UseManagers, sv_AdminInvites, sv_UseReminders,
            sv_QuestionSets, sv_ManagerQnSet, sv_PresetQnSet,
            sv_ReportTitle, sv_FeedbackOption, sv_AltCopyrightText,
            sv_intro_directions, sv_intro_directionsOther, ReportInformationHtml, DisablePDF, OrgReportMinResponsesToShow,
            sv_SelfEmailSubject, sv_SelfEmailText, sv_OtherEmailSubject, sv_OtherEmailText,
            RatersSuggestedMin, RatersSuggestedMax, IsStrictRaterLimits, RaterInheritsLevel, GroupReportAvailable,
            IsAlbertSurvey, PrimaryGblAnswerTypeId, AllowReportWithoutRaters, Hide360ReportNorms)
          OUTPUT INSERTED." + DbTable.Surveys.IdentityColumn + @"
          VALUES (
            @sv_OrgId, @sv_uniqueid, @sv_typecode, @sv_ReportType, @ReportSubType,
            @SvCompanyId, @ProgramJobId, @IsProgramSurvey,
            @SurveyTypeCode, @ShowSurveyLoggedIn,
            @sv_title, @sv_360GroupName, @ClonedFromSvId,
            @CreatedByUserId, @AlbertRatersOnly,
            0, 0, 0,
            0, 0, 1,
            @sv_QuestionSets, @sv_ManagerQnSet, @sv_PresetQnSet,
            @sv_ReportTitle, @sv_FeedbackOption, @sv_AltCopyrightText,
            @sv_intro_directions, @sv_intro_directionsOther, @ReportInformationHtml, @DisablePDF, @OrgReportMinResponsesToShow,
            @sv_SelfEmailSubject, @sv_SelfEmailText, @sv_OtherEmailSubject, @sv_OtherEmailText,
            @RatersSuggestedMin, @RatersSuggestedMax, @IsStrictRaterLimits, @RaterInheritsLevel, @GroupReportAvailable,
            @IsAlbertSurvey, @PrimaryGblAnswerTypeId, @AllowReportWithoutRaters, @Hide360ReportNorms)";

        // Create new survey record based on the template record.
        using (var cmd = new SqlCommand(sql, trans.Connection, trans)) {

          cmd.Parameters.AddInt("@CreatedByUserId", createdByUserId);
          cmd.Parameters.AddVarChar("@sv_uniqueid", 50, "");
          cmd.Parameters.AddInt("@SvCompanyId", companyId);
          cmd.Parameters.AddInt("@ProgramJobId", programJobId);
          cmd.Parameters.AddTinyIntFromBool("@IsProgramSurvey", isProgramSurvey);

          cmd.Parameters.AddInt("@sv_OrgId", templateSurvey.OrgId);
          cmd.Parameters.AddInt("@ClonedFromSvId", templateSurvey.SurveyId);
          cmd.Parameters.AddVarChar("@sv_title", 200, surveyTitleOrNullForDefault.ValueIfNullOrEmpty(templateSurvey.SurveyName));
          cmd.Parameters.AddVarChar("@sv_360GroupName", 100, templateSurvey.SurveyName);
          cmd.Parameters.AddTinyInt("@sv_FeedbackOption", (int)templateSurvey.FeedbackOption);
          cmd.Parameters.AddVarChar("@sv_typecode", 20, templateSurvey.SvTypeCode);
          cmd.Parameters.AddVarChar("@sv_ReportType", 20, templateSurvey.ReportType.ReportTypeCode);
          cmd.Parameters.AddVarChar("@ReportSubType", 10, templateSurvey.ReportSubType);
          cmd.Parameters.AddVarChar("@SurveyTypeCode", 10, templateSurvey.SurveyTypeCode);
          cmd.Parameters.AddInt("@sv_QuestionSets", 1);
          cmd.Parameters.AddInt("@sv_ManagerQnSet", templateSurvey.ManagerQuestionSet);
          cmd.Parameters.AddInt("@sv_PresetQnSet", templateSurvey.PresetQuestionSet);
          cmd.Parameters.AddVarChar("@sv_ReportTitle", 200, templateSurvey.ReportTitle);
          cmd.Parameters.AddText("@sv_AltCopyrightText", templateSurvey.CustomCopyrightText);
          cmd.Parameters.AddText("@sv_intro_directions", templateSurvey.IntroDirections);
          cmd.Parameters.AddText("@sv_intro_directionsOther", templateSurvey.IntroDirectionsRater);
          cmd.Parameters.AddVarChar("@sv_SelfEmailSubject", 200, templateSurvey.InvitationEmailSubject_Self);
          cmd.Parameters.AddText("@sv_SelfEmailText", templateSurvey.InvitationEmailBodyTemplate_Self);
          cmd.Parameters.AddVarChar("@sv_OtherEmailSubject", 100, templateSurvey.InvitationEmailSubject_Rater);
          cmd.Parameters.AddText("@sv_OtherEmailText", templateSurvey.InvitationEmailBodyTemplate_Rater);
          cmd.Parameters.AddInt("@RatersSuggestedMin", templateSurvey.RatersSuggestedMin);
          cmd.Parameters.AddInt("@RatersSuggestedMax", templateSurvey.RatersSuggestedMax);
          cmd.Parameters.AddTinyIntFromBool("@IsStrictRaterLimits", templateSurvey.IsStrictRaterLimits);
          cmd.Parameters.AddTinyIntFromBool("@RaterInheritsLevel", templateSurvey.RaterInheritsLevel);
          cmd.Parameters.AddTinyIntFromBool("@GroupReportAvailable", templateSurvey.GroupReportAvailable);
          cmd.Parameters.AddTinyIntFromBool("@AlbertRatersOnly", templateSurvey.IsRatersOnly);
          cmd.Parameters.AddTinyIntFromBool("@ShowSurveyLoggedIn", templateSurvey.ShowSurveyLoggedIn);
          cmd.Parameters.AddTinyIntFromBool("@IsAlbertSurvey", templateSurvey.IsAlbertSurvey);
          cmd.Parameters.AddInt("@PrimaryGblAnswerTypeId", templateSurvey.PrimaryGblAnswerTypeId);
          cmd.Parameters.AddTinyIntFromBool("@AllowReportWithoutRaters", templateSurvey.AllowReportWithoutRaters);
          cmd.Parameters.AddTinyIntFromBool("@Hide360ReportNorms", templateSurvey.Hide360ReportNorms);
          cmd.Parameters.AddVarCharMax("@ReportInformationHtml", templateSurvey.ReportInformationHtml);
          cmd.Parameters.AddTinyIntFromBool("@DisablePDF", templateSurvey.DisablePDF);
          cmd.Parameters.AddInt("@OrgReportMinResponsesToShow", templateSurvey.OrgReportMinCompletedRequired);

          while (newSurveyIdInfo.SurveyUID.IsNullOrEmpty() && tries < 20) {
            tries++;
            newSurveyIdInfo.SurveyUID = GetRandomUniqueId();
            cmd.Parameters["@sv_uniqueid"].Value = newSurveyIdInfo.SurveyUID;
            try {
              newSurveyIdInfo.SurveyId = (int)cmd.ExecuteScalar();
            } catch (Exception e) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(e)
                .WithOperation(nameof(AddSurveyStub))
                .WithOperationContext("GenerateUniqueId")
                .WithProperty(DalApplicationInsightsConstants.AttemptNumber, tries)
                .WithProperty(DalApplicationInsightsConstants.SurveyUID, newSurveyIdInfo.SurveyUID)
                .WithProperty(DalApplicationInsightsConstants.IsDuplicateKey, e.Message.IndexOf("unique", StringComparison.InvariantCultureIgnoreCase) >= 0)
                .Track();

              // Possible duplicate key so try again.
              newSurveyIdInfo.SurveyUID = null;
              if (e.Message.IndexOf("unique", StringComparison.InvariantCultureIgnoreCase) < 0) {
                // Not key violation, fatal error.
                throw new ApplicationException("Error finding unique ID: " + e.Message, e);
              }
            }
          }
        }

        if (newSurveyIdInfo.SurveyId == 0 || newSurveyIdInfo.SurveyUID.IsNullOrEmpty()) {
          throw new ApplicationException("Failed to find unique survey ID");
        }

        // Add essential AnswerTypes - Date
        int dateCodeATId = AnswerTypes.AddAnswerTypeBasic(trans.Connection, trans,
          newSurveyIdInfo.SurveyId, AnswerTypes.ReservedGblAnsTypeIds.DateCodes,
          AnswerTypes.ReservedDescriptors.Date, AnswerTypes.InputTypes.OPTIONS_SINGLE, false, false);

        // Add first intake.
        var newIntakeInfo = AddIntake(trans.Connection, trans,
          newSurveyIdInfo.SurveyId, programJobId, firstIntakeName,
          closeDateInCoacheeLocalTime_Self, closeDateInCoacheeLocalTime_Rater, scheduledStartDateUTC);

        newSurveyIdInfo.IntakeCode = newIntakeInfo.Code;
        newSurveyIdInfo.IntakeCodeId = newIntakeInfo.CodeId;

        CopySurveyContent(templateSurvey.SurveyId, newSurveyIdInfo.SurveyId, trans.Connection, trans);

        return newSurveyIdInfo;
      }

      public static void CopySurveyContent(int fromSurveyId, int toSurveyId, SqlConnection conn, SqlTransaction trans) {

        // Note the "replacement tags" in [] brackets, they much match the replace functions.
        // This is done basically so the SQL is more readable here.
        string sqlCopySurveyContent = @"
          -- Copy AnswerTypes and get ID map.
          DECLARE @AnsTypeMap TABLE (NewSurveyID INT, OldAnsTypeId INT, NewAnsTypeId INT);
          MERGE [AnswerTypes] USING ( SELECT [AllColumns] FROM [AnswerTypes] WHERE SurveyId = @CopyFromSurveyId [ExcludeCondition] ) t ON 0 = 1
          WHEN NOT MATCHED THEN
            INSERT ([ColumnsWithoutId])
            VALUES ([ColumnsWithNewId])
            OUTPUT @CopyToSurveyId, t.[IdentityColumn], INSERTED.[IdentityColumn] INTO @AnsTypeMap;"
          .Replace("[AnswerTypes]", DbTable.AnswerTypes.TableName)
          .Replace("[AllColumns]", DbTable.AnswerTypes.GetListOfColumnNames(IncludeIdentityColumn: true))
          .Replace("[ExcludeCondition]", "AND AnswerTypeDescr NOT IN(" + AnswerTypes.NonCopyDescriptorsForSQL + ")")
          .Replace("[ColumnsWithoutId]", DbTable.AnswerTypes.GetListOfColumnNames())
          .Replace("[ColumnsWithNewId]", DbTable.AnswerTypes.GetListOfColumnNames(Prefix: "t").Replace("t.SurveyId", "@CopyToSurveyId").Replace("t.ClonedFromSvId", "@CopyFromSurveyId"))
          .Replace("[IdentityColumn]", DbTable.AnswerTypes.IdentityColumn)
          + @"
          -- Copy codes and reassign AnswerTypeIds via map.
          MERGE [Codes] USING ( SELECT [ColumnsWithMappedId] FROM [Codes] c INNER JOIN @AnsTypeMap m ON c.[AnswerTypeId] = m.OldAnsTypeId ) c ON 0 = 1
          WHEN NOT MATCHED THEN
            INSERT ([ColumnsWithoutId])
            VALUES ([ColumnsWithNewId]);"
          .Replace("[Codes]", DbTable.Codes.TableName)
          .Replace("[ColumnsWithMappedId]", DbTable.Codes.GetListOfColumnNames(Prefix: "c").Replace("c." + DbTable.AnswerTypes.IdentityColumn, "m.NewAnsTypeId"))
          .Replace("[AnswerTypeId]", DbTable.AnswerTypes.IdentityColumn)
          .Replace("[ColumnsWithoutId]", DbTable.Codes.GetListOfColumnNames())
          .Replace("[ColumnsWithNewId]", DbTable.Codes.GetListOfColumnNames(Prefix: "c").Replace(DbTable.AnswerTypes.IdentityColumn, "NewAnsTypeId"))
          + @"
          -- Copy Dimensions
          INSERT INTO sv_360_Dimensions (SurveyId, AnswerTypeId, DimensionNo)
          SELECT @CopyToSurveyId, m.NewAnsTypeId, d.DimensionNo
          FROM sv_360_Dimensions d
          INNER JOIN @AnsTypeMap m ON m.OldAnsTypeId = d.AnswerTypeId;"
          + @"
          -- Copy Pages and create map
          DECLARE @PageMap TABLE ( SurveyId INT, OldPageId INT, NewPageId INT );
          MERGE [Pages] USING ( SELECT [AllColumns] FROM [Pages] WHERE SurveyId = @CopyFromSurveyId ) p ON 0 = 1
          WHEN NOT MATCHED THEN
            INSERT ([ColumnsWithoutId])
            VALUES ([ColumnsWithNewId])
            OUTPUT @CopyToSurveyId, p.[IdentityColumn], INSERTED.[IdentityColumn] INTO @PageMap;"
          .Replace("[Pages]", DbTable.Pages.TableName)
          .Replace("[AllColumns]", DbTable.Pages.GetListOfColumnNames(IncludeIdentityColumn: true))
          .Replace("[ColumnsWithoutId]", DbTable.Pages.GetListOfColumnNames())
          .Replace("[ColumnsWithNewId]", DbTable.Pages.GetListOfColumnNames(Prefix: "p").Replace("p.SurveyId", "@CopyToSurveyId"))
          .Replace("[IdentityColumn]", DbTable.Pages.IdentityColumn)
          + @"
          -- Copy questions and reassign AnswerTypeIds & PageIds via maps.
          INSERT INTO [Questions] ([ColumnsWithoutId])
          SELECT [ColumnsWithNewIds]
          FROM [Questions] q
          LEFT OUTER JOIN @AnsTypeMap t ON t.OldAnsTypeId = q.[AnswerTypeId]
          LEFT OUTER JOIN @AnsTypeMap t2 ON t.OldAnsTypeId = q.[AnswerTypeId]2
          INNER JOIN @PageMap p ON p.OldPageId = q.PageId
          WHERE t.OldAnsTypeId IS NOT NULL
          OR q.IsHeading = 1;"
          .Replace("[Questions]", DbTable.Questions.TableName)
          .Replace("[ColumnsWithoutId]", DbTable.Questions.GetListOfColumnNames())
          .Replace("[ColumnsWithNewIds]", DbTable.Questions.GetListOfColumnNames(Prefix: "q")
            .Replace("q.SurveyId", "@CopyToSurveyId")
            .Replace("q.PageId", "p.NewPageId")
            .Replace("q." + DbTable.AnswerTypes.IdentityColumn + "2", "t2.NewAnsTypeId")
            .Replace("q." + DbTable.AnswerTypes.IdentityColumn, "t.NewAnsTypeId"))
          .Replace("[AnswerTypeId]", DbTable.AnswerTypes.IdentityColumn);

        using (var cmd = new SqlCommand(sqlCopySurveyContent, conn, trans)) {
          cmd.Parameters.Add("@CopyFromSurveyId", SqlDbType.Int).Value = fromSurveyId;
          cmd.Parameters.Add("@CopyToSurveyId", SqlDbType.Int).Value = toSurveyId;
          cmd.ExecuteNonQuery();
        }
      }

      public static AnswerTypes.IntakeInfo AddIntake(
        SqlConnection conn, SqlTransaction trans,
        int surveyId, int? programJobId, string intakeName,
        DateTime CloseDateInCoacheeLocalTime_Self,
        DateTime CloseDateInCoacheeLocalTime_Rater,
        DateTime? scheduledStartDateUTC) {

        var intake = AnswerTypes.AddDateCode(conn, trans, surveyId, intakeName, CloseDateInCoacheeLocalTime_Self, CloseDateInCoacheeLocalTime_Rater, scheduledStartDateUTC);
        if (programJobId != null) AblePrograms.JoinProgramToIntake(conn, trans, (int)programJobId, intake.CodeId);
        return intake;
      }

      public class CreateSurveyInfo {
        public bool Success { get; internal set; }
        public string Message { get; internal set; }
        public SurveyInfo SurveyInfo { get; internal set; }
        public AnswerTypes.IntakeInfo IntakeInfo { get; internal set; }
        public List<NewSurveyParticipant> Participants { get; internal set; }
        public CreateSurveyInfo() {
          Success = false;
          Message = "";
          Participants = new List<NewSurveyParticipant>();
        }
      }

      public class NewSurveyParticipant {
        public AlbertCoachees.AlbertCoacheeInfo CoacheeInfo { get; private set; }
        public Participants.AddParticipantToSurveyInfo ParticipantInfo { get; private set; }
        public NewSurveyParticipant(
          AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
          Participants.AddParticipantToSurveyInfo participantInfo
        ) {
          this.CoacheeInfo = coacheeInfo;
          this.ParticipantInfo = participantInfo;
        }
      }

      public static CreateSurveyInfo CreateSurvey(
        int userId,
        int? companyId,
        int? programJobId,
        bool isProgramSurvey,
        int templateSurveyId,
        string intakeTitle,
        DateTime closeDateSelfLocal,
        DateTime closeDateRaterLocal,
        List<int> coacheeIdsToAdd,
        DateTime? scheduledSendDateUtc = null) {

        var newSurveyIdInfo = new NewSurveyIdInfo();
        var createSurveyInfo = new CreateSurveyInfo();

        // Ensure survey is an Able template.
        var templateInfo = GetTemplateInfo(templateSurveyId);
        if (templateInfo == null) {
          createSurveyInfo.Message = "Invalid Survey Template";
          return createSurveyInfo;
        }

        // Adjust close dates to be the same if required.
        if (templateInfo.IsRatersOnly) closeDateSelfLocal = closeDateRaterLocal;
        else if (templateInfo.FeedbackOption == FeedbackOptionEnum.NoRaters) closeDateRaterLocal = closeDateSelfLocal;

        createSurveyInfo.Success = UsingTransaction(trans => {

          // Create a new survey.
          newSurveyIdInfo = AddSurveyStub(
            trans: trans,
            createdByUserId: userId,
            templateSurvey: templateInfo,
            companyId: companyId,
            programJobId: programJobId,
            isProgramSurvey: isProgramSurvey,
            firstIntakeName: intakeTitle,
            closeDateInCoacheeLocalTime_Self: closeDateSelfLocal,
            closeDateInCoacheeLocalTime_Rater: closeDateRaterLocal,
            scheduledStartDateUTC: scheduledSendDateUtc);

          createSurveyInfo.IntakeInfo = AnswerTypes.GetIntake(trans, newSurveyIdInfo.SurveyId, 1); // Get the default intake.

          if (createSurveyInfo.IntakeInfo == null) {
            createSurveyInfo.Message = "Unable to get intake info.";
            return false; // Rollback transaction;
          }

          createSurveyInfo.SurveyInfo = GetSurveyInfo(trans, newSurveyIdInfo.SurveyId, createSurveyInfo.IntakeInfo.Code);

          if (createSurveyInfo.SurveyInfo == null) {
            createSurveyInfo.Message = "Unable to get survey info.";
            return false; // Rollback transaction;
          }

          // Add participants to the intake.
          foreach (int coacheeId in coacheeIdsToAdd) {
            var coachee = AlbertCoachees.GetCoacheeInfo(coacheeId);
            Participants.AddParticipantToSurveyInfo addParticipantInfo;
            // Get participant object.
            addParticipantInfo = new Participants.AddParticipantToSurveyInfo(
              createSurveyInfo.SurveyInfo, createSurveyInfo.IntakeInfo, coachee);
            // Add to the db.
            Participants.AddParticipantToSurvey(trans.Connection, trans, addParticipantInfo);
            // Add to the result list.
            createSurveyInfo.Participants.Add(new NewSurveyParticipant(coachee, addParticipantInfo));
          }

          return true;
        });

        return createSurveyInfo;
      }

      public static void UpdateIntakeCloseDates(int surveyId, int intakeCode, DateTime closeDateSelfLocal, DateTime closeDateRatersLocal) {
        // Note close date columns in db are in local time not UTC.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

            UPDATE c SET
              c.IntakeCloseDateSelf = @CloseDateSelf,
              c.IntakeCloseDate = @CloseDateRaters
            FROM sv_360_Codes c
            INNER JOIN sv_360_AnswerTypes t ON c.AnswerTypeId = t.AnswerTypeId
            WHERE t.SurveyId = @SurveyId
              AND t.AnswerTypeDescr = 'date'
              AND c.Code = @IntakeCode;

          ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", surveyId);
            cmd.Parameters.AddInt("@IntakeCode", intakeCode);
            cmd.Parameters.AddDateTime("@CloseDateSelf", closeDateSelfLocal);
            cmd.Parameters.AddDateTime("@CloseDateRaters", closeDateRatersLocal);
            conn.Open();
            cmd.ExecuteNonQuery();
          }
        }
      }

      public static void UpdateWorkshopEventId(SqlTransaction trans, SurveyInfo survey, int? workshopEventId) {

        GetNonQueryInt(trans, @"
          UPDATE sv_Survey
          SET WorkshopEventId = @WorkshopEventId
          WHERE sv_id = @SurveyId",
          NewSqlParameter("SurveyId", survey.SurveyId),
          NewSqlParameter("WorkshopEventId", workshopEventId)
        );
        survey.WorkshopEventId = workshopEventId;
      }

      public static bool UpdateParticipantEmailFromCoachee(SqlTransaction trans, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
        return GetNonQueryInt(trans, @"

          UPDATE sp
            SET sp.Email = @NewEmailAddress
          FROM sv_360_Participants sp
          INNER JOIN sv_Survey s ON s.sv_id = sp.SurveyId
          INNER JOIN sv_360_AnswerTypes dt ON dt.SurveyId = s.sv_id AND dt.AnswerTypeDescr = 'date'
          INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId AND dc.Code = sp.DateGroupCode
          WHERE
            sp.Completed IS NULL AND
            ((dc.ScheduledStartDateUtc IS NOT NULL AND dc.ScheduledStartDateUtc > GETUTCDATE())
            OR (dc.IntakeCloseDate > GETUTCDATE() OR dc.IntakeCloseDateSelf > GETUTCDATE()))
            AND (sp.AbleCoacheeId = @CoacheeId OR sp.UserId = @UserId OR sp.Email = @CurrentEmailAddress)",

          NewSqlParameter("CoacheeId", coacheeInfo.CoacheeId),
          NewSqlParameter("CurrentEmailAddress", coacheeInfo.CurrentEmailAddress, 100),
          NewSqlParameter("NewEmailAddress", coacheeInfo.EmailAddress, 100),
          NewSqlParameter("UserId", coacheeInfo.UserId)
        ) == 1;
      }

      public enum PatchDbScope { All, Company, Program, Survey }

      public static void PatchAllSurveys(List<string> patchLog = null) {
        PatchSurveys(PatchDbScope.All, 0, patchLog);
      }

      public static void PatchSurveysInCompany(int companyId, List<string> patchLog = null) {
        PatchSurveys(PatchDbScope.Company, companyId, patchLog);
      }

      public static void PatchSurveysInProgram(int programJobId, List<string> patchLog = null) {
        PatchSurveys(PatchDbScope.Program, programJobId, patchLog);
      }

      public static void PatchSingleSurvey(int surveyId, List<string> patchLog = null) {
        PatchSurveys(PatchDbScope.Survey, surveyId, patchLog);
      }

      private static void PatchSurveys(PatchDbScope patchDbScope, int entityId, List<string> patchLog = null) {

        // Patches are split into 3 parts, which must be performed in order.
        // 1. "structural" - mainly question & type FKs
        // 2. "responses" - intake & participant FKs
        // 3. "scores" - scores and benchmarks calculated from the above.

        // Note that there are 3 mutually exclusive modes:
        // 1. A single survey (patchSurveyId not null)
        // 2. All surveys in a program (patchProgramJobId not null)
        // 3. All surveys in the db (both are null)

        PatchStructuralFKs(patchDbScope, entityId, patchLog);
        PatchResponseFKs(patchDbScope, entityId, patchLog);
        PatchScores(patchDbScope, entityId, patchLog);

        // Other patches.
        PatchCanLeaderView360Report(patchDbScope, entityId, patchLog);
      }

      private static void PatchStructuralFKs(PatchDbScope patchScope, int entityId, List<string> patchLog = null) {

        var stopwatch = new Stopwatch();
        int rowCount;

        if (patchScope != PatchDbScope.Survey) {
          // Update coachee links to CompanyId (not related to surveys)
          stopwatch.Restart();
          rowCount = GetNonQueryInt($@"
            UPDATE ac
            SET ac.CompanyId = ap.SvCompanyId
            FROM al_Coachees ac
            INNER JOIN id_Job ij ON ij.JobId = ac.ProgramJobId
            INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
            WHERE (ac.CompanyId IS NULL OR ac.CompanyId <> ap.SvCompanyId)
              {(patchScope == PatchDbScope.Program ? "AND ac.ProgramJobId = @EntityId" : "")}
              {(patchScope == PatchDbScope.Company ? "AND ap.SvCompanyId = @EntityId" : "")}",
            NewSqlParameter("EntityId", entityId)
          );
          patchLog?.Add($" - {rowCount} Coachee Company Pointers Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

          // Update progam links to CompanyId (not related to surveys)
          stopwatch.Restart();
          rowCount = GetNonQueryInt($@"
            UPDATE ij
            SET ij.CompanyId = ap.SvCompanyId
            FROM id_Job ij
            INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
            WHERE (ij.CompanyId IS NULL OR ij.CompanyId <> ap.SvCompanyId)
              {(patchScope == PatchDbScope.Company ? "AND ap.SvCompanyId = @EntityId" : "")}
              {(patchScope == PatchDbScope.Program ? "AND ij.JobId = @EntityId" : "")}",
            NewSqlParameter("EntityId", entityId)
          );
          patchLog?.Add($" - {rowCount} Program Company Pointers Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");
        }

        // Update survey MaxPartCompletedUtc.
        // Quick, no need for targeting entityId.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sv
          SET sv.MaxPartCompletedUtc = sp.MaxCompleted
          FROM sv_Survey sv
          CROSS APPLY (
            SELECT MAX(sp.Completed) AS MaxCompleted
            FROM sv_360_Participants sp
            WHERE sp.SurveyId = sv.sv_id
          ) AS sp
          WHERE ISNULL(sv.MaxPartCompletedUtc, GETUTCDATE()) <> ISNULL(sp.MaxCompleted, GETUTCDATE());
          SELECT @@rowcount;",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Survey.MaxPartCompletedUtc Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // For Coachees - Update survey participant links to JobId, ProjectId and CompanyId
        // Quick, no need for targeting entityId.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sp
          SET sp.AbleProgramJobId = ij.JobId,
              sp.AbleProjectId = ap.ProjectId,
              sp.AbleSvCompanyId = ap.SvCompanyId
          FROM sv_360_Participants sp
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          INNER JOIN al_Coachees ac ON sp.AbleCoacheeId = ac.CoacheeId
          INNER JOIN id_Job ij ON ac.ProgramJobId = ij.JobId
          INNER JOIN al_Project ap ON ij.JobNumber = ap.JobNumber
          WHERE sp.IsSelf = 1
            AND sv.WorkshopEventId IS NULL -- Exclude workshop evals, they're done separately.
            AND (ISNULL(sp.AbleProgramJobId, 0) <> ij.JobId
                OR ISNULL(sp.AbleProjectId, 0) <> ap.ProjectId
                OR ISNULL(sp.AbleSvCompanyId, 0) <> ap.SvCompanyId);"
        );
        patchLog?.Add($" - {rowCount} Participant Project Pointers Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // For Workshops - same as above - Update survey participant links to JobId, ProjectId and CompanyId
        // Quick, no need for targeting entityId.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sp
          SET sp.AbleProgramJobId = ij.JobId,
              sp.AbleProjectId = ap.ProjectId,
              sp.AbleSvCompanyId = ap.SvCompanyId
          FROM sv_360_Participants sp
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          INNER JOIN ev_WorkshopEvent we ON sv.WorkshopEventId = we.WorkshopEventId
          INNER JOIN id_Job ij ON ij.JobId = we.ProgramJobId
          INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
          WHERE sp.IsSelf = 1
            AND (ISNULL(sp.AbleProgramJobId, 0) <> ij.JobId
                OR ISNULL(sp.AbleProjectId, 0) <> ap.ProjectId
                OR ISNULL(sp.AbleSvCompanyId, 0) <> ap.SvCompanyId);"
        );
        patchLog?.Add($" - {rowCount} Participant Project Pointers Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Update survey ProgramJobId, ProjectId and SvCompanyId where surveys have pax from multiple programs.
        // The survey's ProgramJobId etc. will be set to the most common one from the participants.
        // Quick, no need to scope.
        stopwatch.Restart();
        rowCount = GetScalarQueryInt($@"
          -- Get participants grouped by survey and program, with rank ordered by count of each within a survey.
          WITH svs AS
          (
            SELECT sp.SurveyId, sp.AbleProgramJobId AS SpProgramJobId,
              COUNT(*) AS SpCount,
              COUNT(*) OVER (PARTITION BY sp.SurveyId) AS Diffs,
              ROW_NUMBER() OVER (PARTITION BY sp.SurveyId ORDER BY COUNT(*) DESC) AS Rank
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            WHERE sp.IsSelf = 1
              AND sv.SurveyTypeCode IN ('{ConfigHelper.SurveyTypeCodes.Able360}', '{ConfigHelper.SurveyTypeCodes.IOS}', '{ConfigHelper.SurveyTypeCodes.Eval}')
              AND sv.sv_OrgId = {ConfigHelper.AbleSurveyOrgId}
              AND sp.AbleProgramJobId IS NOT NULL
            GROUP BY sp.SurveyId, sp.AbleProgramJobId
          ),
          -- Add to above a column showing the next row's count within the survey (null if no more in the survey).
          svs2 AS
          (
            SELECT svs.*,
              LEAD(svs.SpCount, 1) OVER (PARTITION BY svs.SurveyId ORDER BY svs.Rank) AS NextSpCount
            FROM svs
          )
          -- Update surveys using only the 1st rank row, as long as its count is > the next row's count.
          UPDATE sv
          SET sv.ProgramJobId = svs2.SpProgramJobId,
              sv.ProjectId = ap.ProjectId,
              sv.SvCompanyId = ap.SvCompanyId
          FROM svs2
          INNER JOIN sv_Survey sv ON sv.sv_id = svs2.SurveyId
          INNER JOIN id_Job pj ON pj.JobId = svs2.SpProgramJobId
          INNER JOIN al_Project ap ON ap.JobNumber = pj.JobNumber
          WHERE svs2.rank = 1
            AND svs2.SpCount > ISNULL(svs2.NextSpCount, 0)
            AND (ISNULL(sv.ProgramJobId, 0) <> svs2.SpProgramJobId
              OR ISNULL(sv.ProjectId, 0) <> ap.ProjectId
              OR ISNULL(sv.SvCompanyId, 0) <> ap.SvCompanyId);
          SELECT @@rowcount;"
        );
        patchLog?.Add($" - {rowCount} Survey SvCompanyIds Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Update participant IntakeCodeIds (both self and rater)
        // Makes linking participants to intakes much easier than going through the "date" answer type.
        // Quick, no need to scope.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sp
          SET sp.IntakeCodeId = sc.CodeId
          FROM sv_360_Participants sp
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          INNER JOIN sv_360_AnswerTypes st ON sp.SurveyId = st.SurveyId
          INNER JOIN sv_360_Codes sc ON st.AnswerTypeId = sc.AnswerTypeId
          WHERE st.AnswerTypeDescr = 'date'
            AND sc.Code = sp.DateGroupCode
            AND ISNULL(sp.IntakeCodeId, 0) <> sc.CodeId"
        );
        patchLog?.Add($" - {rowCount} Participant IntakeCodeIds Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Following updates can be scoped with survey columns.
        string surveyScopeCondition;
        if (patchScope == PatchDbScope.Survey) {
          surveyScopeCondition = "sv.sv_id = @EntityId";
        } else if (patchScope == PatchDbScope.Program) {
          surveyScopeCondition = "sv.ProgramJobId = @EntityId";
        } else if (patchScope == PatchDbScope.Company) {
          surveyScopeCondition = "sv.SvCompanyId = @EntityId";
        } else {
          surveyScopeCondition = PatchDbNoScopeCondition;
        }

        // Sync question GblAnswerTypeIds
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sq
          SET sq.GblAnswerTypeId = sat.GblAnswerTypeId
          FROM sv_360_Questions sq
          INNER JOIN sv_360_AnswerTypes sat ON sq.AnswerTypeId = sat.AnswerTypeId
          INNER JOIN sv_Survey sv ON sv.sv_id = sat.SurveyId
          WHERE ISNULL(sq.GblAnswerTypeId, 0) <> ISNULL(sat.GblAnswerTypeId, 0)
            AND {surveyScopeCondition}",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Question.GblAnswerTypeIds Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Sync question RptQnGroupIds
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sq
          SET sq.RptQnGroupId = gh.RptQnGroupId
          FROM sv_360_Questions sq
          INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
          INNER JOIN al_RptQnGrpHeadings gh ON gqh.RptQnGrpHeadingId = gh.RptQnGrpHeadingId
          WHERE ISNULL(sq.RptQnGroupId, 0) <> ISNULL(gh.RptQnGroupId, 0)
            AND {surveyScopeCondition}",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Question.RptQnGroupIds Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Set sv.PrimaryGblAnswerTypeId = The global question type that occurs most in the survey.
        //     sv.SurveyViewerQuestionCount = Number of questions of that type which are included in global question groups and hence can be used in the report viewers.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          WITH svs
          AS (
            SELECT sq.SurveyId, sat.GblAnswerTypeId,
              ROW_NUMBER() OVER (PARTITION BY sq.SurveyId ORDER BY COUNT(sq.QuestionId) DESC, sat.GblAnswerTypeId) AS RowNum
            FROM sv_360_Questions sq
            INNER JOIN sv_360_AnswerTypes sat ON sq.AnswerTypeId = sat.AnswerTypeId
            INNER JOIN sv_Survey sv ON sv.sv_id = sat.SurveyId
            WHERE sat.GblAnswerTypeId IS NOT NULL
              AND {surveyScopeCondition}
              AND sat.InputType IN('o', 'scale')
            GROUP BY sq.SurveyId, sat.GblAnswerTypeId
          )
          UPDATE sv
          SET sv.SurveyViewerQuestionCount = sq.QnCount,
              sv.PrimaryGblAnswerTypeId = svs.GblAnswerTypeId,
              sv.LastUpdatedQuestionInfoUtc = GETUTCDATE()
          FROM svs
          INNER JOIN sv_Survey sv ON svs.SurveyId = sv.sv_id
          CROSS APPLY (
            SELECT COUNT(*) AS QnCount -- Questions of the primary type that are included in global groups.
            FROM sv_360_Questions sq
            INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
            WHERE sq.SurveyId = sv.sv_id
              AND sq.GblAnswerTypeId = svs.GblAnswerTypeId
          ) AS sq
          WHERE svs.RowNum = 1
            AND (ISNULL(sv.PrimaryGblAnswerTypeId, 0) <> svs.GblAnswerTypeId
              OR ISNULL(sv.SurveyViewerQuestionCount, 0) <> sq.QnCount)
          SELECT @@rowcount;",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Survey.PrimaryGblAnswerTypeIds Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Sync question GblAnswerTypeIds
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sq
          SET sq.GblAnswerTypeId = sat.GblAnswerTypeId
          FROM sv_360_Questions sq
          INNER JOIN sv_360_AnswerTypes sat ON sq.AnswerTypeId = sat.AnswerTypeId
          INNER JOIN sv_Survey sv ON sv.sv_id = sat.SurveyId
          WHERE ISNULL(sq.GblAnswerTypeId, 0) <> ISNULL(sat.GblAnswerTypeId, 0)
            AND {surveyScopeCondition}",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Question.GblAnswerTypeIds Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Sync question RptQnGroupIds
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sq
          SET sq.RptQnGroupId = gh.RptQnGroupId
          FROM sv_360_Questions sq
          INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
          INNER JOIN al_RptQnGrpHeadings gh ON gqh.RptQnGrpHeadingId = gh.RptQnGrpHeadingId
          WHERE ISNULL(sq.RptQnGroupId, 0) <> ISNULL(gh.RptQnGroupId, 0)
            AND {surveyScopeCondition}",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Question.RptQnGroupIds Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Set survey flag HasLeadershipQuestions
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sv
          SET sv.HasLeadershipQuestions = 0
          FROM sv_Survey sv
          WHERE {surveyScopeCondition};

          UPDATE sv
          SET sv.HasLeadershipQuestions = 1
          FROM sv_Survey sv
          WHERE sv.SurveyTypeCode = '{ConfigHelper.SurveyTypeCodes.Able360}'
            AND EXISTS (SELECT 1 FROM sv_360_Questions sq WHERE sq.SurveyId = sv.sv_id AND sq.RptQnGroupId = @RptQnGroupId)
            AND {surveyScopeCondition}",
          NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer),
          NewSqlParameter("EntityId", entityId)
        );

        // Set sv.PrimaryGblAnswerTypeId = The global question type that occurs most in the survey.
        //     sv.SurveyViewerQuestionCount = Number of questions of that type which are included in global question groups and hence can be used in the report viewers.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          WITH svs
          AS (
            SELECT sq.SurveyId, sat.GblAnswerTypeId,
              ROW_NUMBER() OVER (PARTITION BY sq.SurveyId ORDER BY COUNT(sq.QuestionId) DESC, sat.GblAnswerTypeId) AS RowNum
            FROM sv_360_Questions sq
            INNER JOIN sv_360_AnswerTypes sat ON sat.AnswerTypeId = sq.AnswerTypeId
            INNER JOIN sv_Survey sv ON sv.sv_id = sat.SurveyId
            WHERE sat.GblAnswerTypeId IS NOT NULL
              AND {surveyScopeCondition}
              AND sat.InputType IN('o', 'scale')
            GROUP BY sq.SurveyId, sat.GblAnswerTypeId
          )
          UPDATE sv
          SET sv.SurveyViewerQuestionCount = sq.QnCount,
              sv.PrimaryGblAnswerTypeId = svs.GblAnswerTypeId,
              sv.LastUpdatedQuestionInfoUtc = GETUTCDATE()
          FROM svs
          INNER JOIN sv_Survey sv ON svs.SurveyId = sv.sv_id
          CROSS APPLY (
            SELECT COUNT(*) AS QnCount -- Questions of the primary type that are included in global groups.
            FROM sv_360_Questions sq
            INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
            WHERE sq.SurveyId = sv.sv_id
              AND sq.GblAnswerTypeId = svs.GblAnswerTypeId
          ) AS sq
          WHERE svs.RowNum = 1
            AND (ISNULL(sv.PrimaryGblAnswerTypeId, 0) <> svs.GblAnswerTypeId
              OR ISNULL(sv.SurveyViewerQuestionCount, 0) <> sq.QnCount)
          SELECT @@rowcount;",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Survey.PrimaryGblAnswerTypeIds Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

      }

      private static void PatchResponseFKs(PatchDbScope patchScope, int entityId, List<string> patchLog = null) {

        var stopwatch = new Stopwatch();
        int rowCount;

        // Now can use this condition SQL for all the following updates.
        string participantScopeCondition;
        if (patchScope == PatchDbScope.Survey) {
          participantScopeCondition = "sp.SurveyId = @EntityId";
        } else if (patchScope == PatchDbScope.Program) {
          participantScopeCondition = "sp.AbleProgramJobId = @EntityId";
        } else if (patchScope == PatchDbScope.Company) {
          participantScopeCondition = "sp.AbleSvCompanyId = @EntityId";
        } else {
          participantScopeCondition = PatchDbNoScopeCondition;
        }

        // Update User level pre-post survey flags, only if scope is "all".
        // TODO: This keeps doing all of them, instead of only ones that need changing.
        if (patchScope == PatchDbScope.All) {

          stopwatch.Restart();
          rowCount = GetScalarQueryInt($@"

            UPDATE sp
            SET PrePost360ForUser = 0,
                IsSelfPreSurvey = 0, -- legacy
                IsSelfPostSurvey = 0 -- legacy
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            INNER JOIN id_Job ij ON ij.JobId = sp.AbleProgramJobId
            WHERE {SQLWhereConditions.Standard360Surveys("sv")}
              AND sp.IsSelf = 1
              AND sp.Completed IS NOT NULL;

            WITH sps
            AS
            (
              SELECT
                sp.PartId,
                ROW_NUMBER() OVER (PARTITION BY sp.UserId ORDER BY sp.Completed) AS RowAsc,
                ROW_NUMBER() OVER (PARTITION BY sp.UserId ORDER BY sp.Completed DESC) AS RowDesc,
                COUNT(*)     OVER (PARTITION BY sp.UserId) AS Responses
              FROM sv_360_Participants sp
              INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
              INNER JOIN id_Job ij ON ij.JobId = sp.AbleProgramJobId
              WHERE {SQLWhereConditions.Standard360Surveys("sv")}
                AND sp.IsSelf = 1
                AND sp.Completed IS NOT NULL
            )

            UPDATE sp
            SET sp.PrePost360ForUser = PrePostFlag.Value,
                sp.IsSelfPreSurvey = PrePostFlags.IsSelfPre,  -- legacy
                sp.IsSelfPostSurvey = PrePostFlags.IsSelfPost -- legacy

            FROM sv_360_Participants sp
            INNER JOIN sps ON sp.PartId = sps.PartId

            CROSS APPLY (
              SELECT
                IIF(sps.Responses > 0 AND sps.RowAsc = 1, 1, 0) AS IsSelfPre,
                IIF(sps.Responses > 1 AND sps.RowDesc = 1, 1, 0) AS IsSelfPost
              ) AS PrePostFlags

            CROSS APPLY (
              SELECT CASE WHEN PrePostFlags.IsSelfPre = 1 THEN @PrePostFlag_IsPreSurvey
                          WHEN PrePostFlags.IsSelfPost = 1 THEN @PrePostFlag_IsPostSurvey
                          ELSE 0
                     END AS Value
              ) AS PrePostFlag

            WHERE
              (
                sp.PrePost360ForUser <> PrePostFlag.Value
                OR sp.IsSelfPreSurvey <> PrePostFlags.IsSelfPre
                OR sp.IsSelfPostSurvey <> PrePostFlags.IsSelfPost
              )

            ;SELECT @@rowcount;",

            NewSqlParameter("UpdateFromUtc", DateTime.UtcNow.AddYears(-30)),
            NewSqlParameter("RptQnGroupId", ConfigHelper.RptQnGroupId_SkillsViewer),
            NewSqlParameter("PrePostFlag_IsPreSurvey", Participants.PrePostFlags.IsPreSurvey),
            NewSqlParameter("PrePostFlag_IsPostSurvey", Participants.PrePostFlags.IsPostSurvey)
          );

          patchLog?.Add($" - {rowCount} Participant User-Level Pre-Post Flags Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");
        }

        // Update Program level pre-post survey flags, scope must be at least program.
        // TODO: This keeps doing all of them, instead of only ones that need changing.
        if (patchScope != PatchDbScope.Survey) {

          stopwatch.Restart();
          rowCount = GetScalarQueryInt($@"

            UPDATE sp
            SET sp.PrePost360ForCoachee = 0
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            INNER JOIN id_Job ij ON ij.JobId = sp.AbleProgramJobId
            WHERE {SQLWhereConditions.Standard360Surveys("sv")}
              AND {participantScopeCondition}
              AND sp.IsSelf = 1;

            WITH sps
            AS
            (
              SELECT sp.AbleCoacheeId, sp.PartId,
                ROW_NUMBER() OVER (PARTITION BY sp.AbleCoacheeId ORDER BY sp.CreatedUTC) AS RowAsc,
                ROW_NUMBER() OVER (PARTITION BY sp.AbleCoacheeId ORDER BY sp.CreatedUTC DESC) AS RowDesc,
                COUNT(*)     OVER (PARTITION BY sp.AbleCoacheeId) AS Responses
              FROM sv_360_Participants sp
              INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
              INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
              INNER JOIN id_Job ij ON ij.JobId = sp.AbleProgramJobId
              WHERE {SQLWhereConditions.Standard360Surveys("sv")}
                AND {participantScopeCondition}
                AND sp.IsSelf = 1
            )

            UPDATE sp
            SET sp.PrePost360ForCoachee = PrePostFlag.value
            FROM sv_360_Participants sp
            INNER JOIN sps ON sps.PartId = sp.PartId

            CROSS APPLY (
              SELECT CASE
                  WHEN sps.Responses > 0 AND sps.RowAsc = 1 THEN @PrePostFlag_IsPreSurvey
                  WHEN sps.Responses > 1 AND sps.RowDesc = 1 THEN @PrePostFlag_IsPostSurvey
                END AS Value
              ) AS PrePostFlag

            WHERE (sps.RowAsc = 1 OR RowDesc = 1)
              AND sp.PrePost360ForCoachee <> PrePostFlag.value;

            SELECT @@ROWCOUNT;",

            NewSqlParameter("EntityId", entityId),
            NewSqlParameter("PrePostFlag_IsPreSurvey", Participants.PrePostFlags.IsPreSurvey),
            NewSqlParameter("PrePostFlag_IsPostSurvey", Participants.PrePostFlags.IsPostSurvey)
          );

          patchLog?.Add($" - {rowCount} Participant Program-Level Pre-Post Flags Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");
        }

        // Copy these self FKs & flags to raters.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE spr
          SET spr.AbleProgramJobId = sp.AbleProgramJobId,
              spr.AbleProjectId = sp.AbleProjectId,
              spr.AbleSvCompanyId = sp.AbleSvCompanyId,
              spr.IsSelfPreSurvey = sp.IsSelfPreSurvey,
              spr.IsSelfPostSurvey = sp.IsSelfPostSurvey,
              spr.PrePost360ForUser = sp.PrePost360ForUser,
              spr.PrePost360ForCoachee = sp.PrePost360ForCoachee
          FROM sv_360_Participants sp
          INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
          INNER JOIN sv_360_Participants spr ON spr.Self_PartId = sp.PartId
          WHERE sp.IsSelf = 1
            AND {participantScopeCondition}
            AND (ISNULL(spr.AbleProgramJobId, 0) <> ISNULL(sp.AbleProgramJobId, 0)
                OR ISNULL(spr.AbleProjectId, 0) <> ISNULL(sp.AbleProjectId, 0)
                OR ISNULL(spr.AbleSvCompanyId, 0) <> ISNULL(sp.AbleSvCompanyId, 0)
                OR ISNULL(spr.IsSelfPreSurvey, 0) <> ISNULL(sp.IsSelfPreSurvey, 0)
                OR ISNULL(spr.IsSelfPostSurvey, 0) <> ISNULL(sp.IsSelfPostSurvey, 0)
                OR ISNULL(spr.PrePost360ForUser, 0) <> ISNULL(sp.PrePost360ForUser, 0)
                OR ISNULL(spr.PrePost360ForCoachee, 0) <> ISNULL(sp.PrePost360ForCoachee, 0));",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Copied Participant info to Raters in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Update Workshop Eval Survey First/Last flags & CompanyId.
        // Also update survey company based on workshop company not pax company.
        // Note LEFT JOIN to the CTE because we are updating all surveys not just valid ones.
        // Note only update this if the scope is not a single survey as this is only relevant across programs.
        if (patchScope != PatchDbScope.Survey) {

          stopwatch.Restart();
          rowCount = GetScalarQueryInt($@"
            WITH svs
            AS (
              SELECT sv.sv_id,
                COUNT(*) OVER (PARTITION BY we.ProgramJobId) AS SvCount,
                ROW_NUMBER() OVER (PARTITION BY we.ProgramJobId ORDER BY we.StartDateUtc, sv.sv_id) AS SvSeq
              FROM sv_Survey sv
              INNER JOIN ev_WorkshopEvent we ON sv.WorkshopEventId = we.WorkshopEventId
              INNER JOIN id_Job ij ON ij.JobId = we.ProgramJobId
              INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
              WHERE {SQLWhereConditions.EvalSurveys("sv")}
                {(patchScope == PatchDbScope.Program ? "AND we.ProgramJobId = @EntityId" : "")}
                {(patchScope == PatchDbScope.Company ? "AND ap.SvCompanyId = @EntityId" : "")}
                AND sv.MaxPartCompletedUtc IS NOT NULL
            )
            UPDATE sv
            SET sv.WorkshopEvalFirstOrLast =
              CASE
                WHEN svs.SvCount > 1 AND svs.SvSeq = 1 THEN @IsFirstEvalValue
                WHEN svs.SvCount > 1 AND svs.SvSeq = SvCount THEN @IsLastEvalValue
                ELSE NULL
              END
            FROM sv_Survey sv
            INNER JOIN ev_WorkshopEvent we ON sv.WorkshopEventId = we.WorkshopEventId
            INNER JOIN id_Job ij ON we.ProgramJobId = ij.JobId
            INNER JOIN al_Project ap ON ij.JobNumber = ap.JobNumber
            LEFT OUTER JOIN svs ON svs.sv_id = sv.sv_id
            WHERE ISNULL(sv.WorkshopEvalFirstOrLast, 0) <>
              CASE
                WHEN svs.SvCount > 1 AND svs.SvSeq = 1 THEN @IsFirstEvalValue
                WHEN svs.SvCount > 1 AND svs.SvSeq = SvCount THEN @IsLastEvalValue
                ELSE 0
              END
              {(patchScope == PatchDbScope.Program ? "AND we.ProgramJobId = @EntityId" : "")}
              {(patchScope == PatchDbScope.Company ? "AND ap.SvCompanyId = @EntityId" : "")}
            ;SELECT @@rowcount;",
            NewSqlParameter("RptQnGroupId_EvalViewer", ConfigHelper.RptQnGroupId_EvalViewer),
            NewSqlParameter("IsFirstEvalValue", (int)ConfigHelper.EvalFirstOrLastEnum.IsFirstEval),
            NewSqlParameter("IsLastEvalValue", (int)ConfigHelper.EvalFirstOrLastEnum.IsLastEval),
            NewSqlParameter("EntityId", entityId)
          );
          patchLog?.Add($" - {rowCount} Workshop Eval First/Last flags & CompanyId Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");
        }

        // Update intake counters.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          WITH sps
          AS (
            SELECT sp.IntakeCodeId, COUNT(sp.PartId) PartCount, COUNT(sp.Completed) AS CompletedCount
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            WHERE {participantScopeCondition}
            GROUP BY sp.IntakeCodeId
          )
          UPDATE ic
          SET ic.IntakeTotalParticipants = sps.PartCount,
              ic.IntakeTotalCompleted = sps.CompletedCount
          FROM sv_360_Codes ic
          INNER JOIN sps ON sps.IntakeCodeId = ic.CodeId
          WHERE ic.IntakeTotalParticipants IS NULL OR ic.IntakeTotalParticipants <> sps.PartCount
             OR ic.IntakeTotalCompleted IS NULL OR ic.IntakeTotalCompleted <> sps.CompletedCount;
          SELECT @@rowcount;",
          NewSqlParameter("EntityId", entityId)
        );
        patchLog?.Add($" - {rowCount} Intake Counts Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

      }

      private static void PatchScores(PatchDbScope patchScope, int patchEntityId, List<string> patchLog = null) {

        var stopwatch = new Stopwatch();
        int rowCount;

        string surveyScopeCondition;
        if (patchScope == PatchDbScope.Survey) {
          surveyScopeCondition = "sv.sv_id = @EntityId";
        } else if (patchScope == PatchDbScope.Program) {
          surveyScopeCondition = "sv.ProgramJobId = @EntityId";
        } else if (patchScope == PatchDbScope.Company) {
          surveyScopeCondition = "sv.SvCompanyId = @EntityId";
        } else {
          surveyScopeCondition = PatchDbNoScopeCondition;
        }

        // Ensure sv_SurveyGblQnScores rows exist for all surveys.
        // Simply insert any missing ones.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          INSERT INTO sv_SurveyGblQnScores (SurveyId, GblQuestionId, GblAnswerTypeId, ScoreSum, ScoreCount, ScoreSumRaters, ScoreCountRaters)
          SELECT sq.SurveyId, sq.GblQuestionId, sq.GblAnswerTypeId, 0, 0, 0, 0
          FROM sv_360_Questions sq
          INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
          LEFT OUTER JOIN sv_SurveyGblQnScores sgs ON sgs.SurveyId = sv.sv_id AND sgs.GblQuestionId = sq.GblQuestionId
          WHERE sq.GblQuestionId IS NOT NULL
            AND sq.GblAnswerTypeId IS NOT NULL
            AND sgs.GblQnScoreId IS NULL
            AND {surveyScopeCondition}
          GROUP BY sq.SurveyId, sq.GblQuestionId, sq.GblAnswerTypeId;",
          NewSqlParameter("EntityId", patchEntityId)
        );
        patchLog?.Add($" - {rowCount} New sv_SurveyGblQnScores rows added in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        // Calculate totals for sv_SurveyGblQnScores.
        // Requires sv_Survey.MaxPartCompletedUtc updated before this,
        // and sv_SurveyGblQnScores rows exist for all surveys.
        // TODO: Takes 7+ seconds, always does all of them. try to reduce time, update only as needed.
        stopwatch.Restart();

        rowCount = GetNonQueryInt($@"

          WITH scores AS (
            SELECT
              sgs.GblQnScoreId,
              SUM(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS ScoreSumSelf,
              COUNT(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS ScoreCountSelf,
              SUM(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS ScoreSumRaters,
              COUNT(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS ScoreCountRaters,
              SUM(IIF(sp.IsSelf = 1 AND sp.IsSelfPreSurvey = 1, sc.ValueScore, NULL)) AS ScoreSumSelfPre,
              COUNT(IIF(sp.IsSelf = 1 AND sp.IsSelfPreSurvey = 1, sc.ValueScore, NULL)) AS ScoreCountSelfPre,
              SUM(IIF(sp.IsSelf = 1 AND sp.IsSelfPostSurvey = 1, sc.ValueScore, NULL)) AS ScoreSumSelfPost,
              COUNT(IIF(sp.IsSelf = 1 AND sp.IsSelfPostSurvey = 1, sc.ValueScore, NULL)) AS ScoreCountSelfPost,
              SUM(IIF(sp.IsSelf = 0 AND sp.IsSelfPreSurvey = 1, sc.ValueScore, NULL)) AS ScoreSumRaterPre,
              COUNT(IIF(sp.IsSelf = 0 AND sp.IsSelfPreSurvey = 1, sc.ValueScore, NULL)) AS ScoreCountRaterPre,
              SUM(IIF(sp.IsSelf = 0 AND sp.IsSelfPostSurvey = 1, sc.ValueScore, NULL)) AS ScoreSumRaterPost,
              COUNT(IIF(sp.IsSelf = 0 AND sp.IsSelfPostSurvey = 1, sc.ValueScore, NULL)) AS ScoreCountRaterPost
            FROM sv_SurveyGblQnScores sgs
            INNER JOIN sv_Survey sv ON sv.sv_id = sgs.SurveyId
            INNER JOIN sv_360_Questions sq ON sq.SurveyId = sv.sv_id AND sgs.GblQuestionId = sq.GblQuestionId
            LEFT OUTER JOIN (
              sv_360_Participants sp
              INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
              INNER JOIN sv_360_Codes sc ON sa.CodeId = sc.CodeId
            ) ON sp.SurveyId = sq.SurveyId AND sa.QuestionId = sq.QuestionId AND sp.Completed IS NOT NULL
            WHERE {surveyScopeCondition}
            GROUP BY sgs.GblQnScoreId
          )
          UPDATE sgs SET
            sgs.ScoreSum =                scores.ScoreSumSelf,
            sgs.ScoreCount =              scores.ScoreCountSelf,
            sgs.ScoreSumRaters =          scores.ScoreSumRaters,
            sgs.ScoreCountRaters =        scores.ScoreCountRaters,
            sgs.ScoreSumSelfPre =         scores.ScoreSumSelfPre,
            sgs.ScoreCountSelfPre =       scores.ScoreCountSelfPre,
            sgs.ScoreSumSelfPost =        scores.ScoreSumSelfPost,
            sgs.ScoreCountSelfPost =      scores.ScoreCountSelfPost,
            sgs.ScoreSumRaterPre =        scores.ScoreSumRaterPre,
            sgs.ScoreCountRaterPre =      scores.ScoreCountRaterPre,
            sgs.ScoreSumRaterPost =       scores.ScoreSumRaterPost,
            sgs.ScoreCountRaterPost =     scores.ScoreCountRaterPost
          FROM scores
          INNER JOIN sv_SurveyGblQnScores sgs ON sgs.GblQnScoreId = scores.GblQnScoreId;",

          NewSqlParameter("EntityId", patchEntityId)
        );
        patchLog?.Add($" - {rowCount} sv_SurveyGblQnScores Rows Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");
        // Update survey LastUpdatedGblQnScoresUtc accordingly.
        // Requires sv_Survey.MaxPartCompletedUtc updated before this.
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE sv
          SET sv.LastUpdatedGblQnScoresUtc = sv.MaxPartCompletedUtc
          FROM sv_Survey sv
          WHERE sv.MaxPartCompletedUtc IS NOT NULL
            AND (sv.LastUpdatedGblQnScoresUtc IS NULL OR sv.MaxPartCompletedUtc > sv.LastUpdatedGblQnScoresUtc)
            AND {surveyScopeCondition};",
          NewSqlParameter("EntityId", patchEntityId)
        );
        patchLog?.Add($" - {rowCount} LastUpdatedGblQnScoresUtc Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");

        if (patchScope == PatchDbScope.All) {
          // Update sv_GblQuestions global scores.
          // Note only runs on global level, not for individual program or survey.
          // TODO: Takes 7+ seconds, try to reduce time.
          stopwatch.Restart();
          rowCount = GetNonQueryInt($@"
            WITH sq
            AS
            (
              SELECT sq.GblQuestionId,
                SUM(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS GlobalNormSelfTotal,
                COUNT(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS GlobalNormSelfCount,
                SUM(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS GlobalNormRaterTotal,
                COUNT(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS GlobalNormRaterCount
              FROM sv_360_Questions sq
              INNER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
              INNER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
              INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
              WHERE sp.Completed IS NOT NULL
              GROUP BY sq.GblQuestionId
            )
            UPDATE gq
            SET gq.GlobalNormSelfTotal = sq.GlobalNormSelfTotal,
                gq.GlobalNormSelfCount = sq.GlobalNormSelfCount,
                gq.GlobalNormRaterTotal = sq.GlobalNormRaterTotal,
                gq.GlobalNormRaterCount = sq.GlobalNormRaterCount
            FROM sv_GblQuestions gq
            INNER JOIN sq ON sq.GblQuestionId = gq.GblQuestionId"
          );
          patchLog?.Add($" - {rowCount} sv_GblQuestions Global Totals Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");
        }

        // Update WorkshopEvent scores.
        // Requires sv_SurveyGblQnScores updated before this.
        // Note LEFT JOIN because we want to update all WorkshopEvents not just ones with scores.
        // Note only update this if the scope is not a single survey as this is only relevant across programs or everything.
        // TODO: Always does all of them, instead of updating only as needed.
        if (patchScope != PatchDbScope.Survey) {

          var evalSQLConditions = Reports.EvalViewer.GetSQLConditionsForReport(ConfigHelper.EvalType.Workshop);

          stopwatch.Restart();
          rowCount = GetScalarQueryInt($@"
            UPDATE we
            SET we.EvalScoreSum = sv.ScoreSum,
                we.EvalScoreCount = sv.ScoreCount,
                we.EvalFirstOrLast = sv.EvalFirstOrLast,
                we.EvalIntakeCodeId = sv.IntakeCodeId
            FROM ev_WorkshopEvent we
            INNER JOIN id_Job pj ON pj.JobId = we.ProgramJobId
            INNER JOIN al_Project ap ON ap.JobNumber = pj.JobNumber
            LEFT OUTER JOIN (
              SELECT
                sv.WorkshopEventId, sv.sv_id, sv.ProgramJobId, sv.SvCompanyId,
                sv.sv_createdUTC, sv.MaxPartCompletedUtc, sv.LastUpdatedQuestionInfoUtc,
                MAX(sc.CodeId) AS IntakeCodeId,
                MAX(sv.WorkshopEvalFirstOrLast) AS EvalFirstOrLast,
                SUM(gqs.ScoreSum) AS ScoreSum,
                SUM(gqs.ScoreCount) AS ScoreCount
              FROM sv_Survey sv
              INNER JOIN sv_360_AnswerTypes sat ON sat.SurveyId = sv.sv_id AND sat.AnswerTypeDescr = 'date'
              INNER JOIN sv_360_Codes sc ON sat.AnswerTypeId = sc.AnswerTypeId
              INNER JOIN sv_SurveyGblQnScores gqs ON gqs.SurveyId = sv.sv_id
              INNER JOIN al_RptQnGrpHgGblQns gqh ON gqs.GblQuestionId = gqh.GblQuestionId
              WHERE gqs.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
                AND {evalSQLConditions.SurveyAndHeadingConditionSQL}
              GROUP BY
                sv.WorkshopEventId, sv.sv_id, sv.ProgramJobId, sv.SvCompanyId,
                sv.sv_createdUTC, sv.MaxPartCompletedUtc, sv.LastUpdatedQuestionInfoUtc
            ) sv ON sv.WorkshopEventId = we.WorkshopEventId
            WHERE we.StartDate >= @StartFrom
              AND {surveyScopeCondition}
            ;SELECT @@rowcount;",
            NewSqlParameter("StartFrom", DateTime.Now.AddDays(-PatchDbCatchupDays)),
            NewSqlParameter("EntityId", patchEntityId)
          );
          patchLog?.Add($" - {rowCount} Workshop Eval Scores Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");
        }
      }

      // Set flag indicating Leader user is allowed to view their own 360 report if any of:
      //  a) survey has intentionally been sent to pax, or
      //  b) if external script has set AI_ViewResultsCheckUtc, or
      //  c) if survey has been closed some time ago.
      // TODO: (?) Remove this and CanLeaderView360Report column if not needed externally, instead check conditions in code.
      public static void PatchCanLeaderView360Report(PatchDbScope patchDbScope, int entityId, List<string> patchLog = null) {

        var stopwatch = new Stopwatch();
        int rowCount;

        // Sync question GblAnswerTypeIds
        stopwatch.Restart();
        rowCount = GetNonQueryInt($@"
          UPDATE p
          SET p.CanLeaderView360Report = 1
          FROM sv_360_Participants p
          INNER JOIN sv_360_Codes ic ON ic.CodeId = p.IntakeCodeId
          WHERE p.Completed IS NOT NULL
            AND p.IsSelf = 1
            {(patchDbScope == PatchDbScope.Survey ? $"AND p.SurveyId = @EntityId" : "")}
            {(patchDbScope == PatchDbScope.Program ? $"AND p.AbleProgramJobId = @EntityId" : "")}
            {(patchDbScope == PatchDbScope.Company ? $"AND p.AbleSvCompanyId = @EntityId" : "")}
            AND (p.ReportSentUtc IS NOT NULL
                OR p.AI_ViewResultsCheckUtc IS NOT NULL
                OR ic.IntakeCloseDate < DATEADD(DAY, -@PaxAllowViewFeedbackAfterDays, GETUTCDATE()))
            AND p.CanLeaderView360Report = 0;",
          NewSqlParameter("EntityId", entityId),
          NewSqlParameter("PaxAllowViewFeedbackAfterDays", ConfigHelper.Reports_PaxAllowViewFeedbackAfterDays)
        );

        patchLog?.Add($" - {rowCount} Participant.CanLeaderView360Report Flags Updated in {stopwatch.Elapsed.TotalSeconds.ToString("0.00")}s.");
      }

      public static SurveyTypeEnum GetSurveyType(string surveyTypeCode) {
        // Note specificity: ClonedFromSurveyId then SurveyTypeCode then PrimaryGblAnswerTypeId.
        if (surveyTypeCode.EqualsIgnoreCase(ConfigHelper.SurveyTypeCodes.DevPlan)) return SurveyTypeEnum.DevelopmentPlan;
        if (surveyTypeCode.EqualsIgnoreCase(ConfigHelper.SurveyTypeCodes.Intake)) return SurveyTypeEnum.Intake;
        if (surveyTypeCode.EqualsIgnoreCase(ConfigHelper.SurveyTypeCodes.Pulse360)) return SurveyTypeEnum.Pulse360;
        if (surveyTypeCode.EqualsIgnoreCase(ConfigHelper.SurveyTypeCodes.Able360)) return SurveyTypeEnum.Standard360;
        if (surveyTypeCode.EqualsIgnoreCase(ConfigHelper.SurveyTypeCodes.Eval)) return SurveyTypeEnum.Eval;
        if (surveyTypeCode.EqualsIgnoreCase(ConfigHelper.SurveyTypeCodes.IOS)) return SurveyTypeEnum.IOS;
        return SurveyTypeEnum.Unknown;
      }

      public class NewSurveyInfo {

        public SurveyInfo SurveyInfo { get; private set; }
        public Participants.ParticipantInfo PartInfo { get; private set; }
        public int IntakeCodeId { get; private set; } // the new intake that was added to the new survey
        public int IntakeCode { get; private set; } // the new intake code (usually = 1)

        public NewSurveyInfo(int surveyId, int newIntakeCodeId, int newIntakeCode, Participants.ParticipantInfo partInfo) {
          this.SurveyInfo = GetSurveyInfo(surveyId, newIntakeCode);
          this.IntakeCodeId = newIntakeCodeId;
          this.IntakeCode = newIntakeCode;
          this.PartInfo = partInfo;
        }
        public NewSurveyInfo(AlbertSurveys.SurveyInfo surveyInfo, int newIntakeCodeId, int newIntakeCode, Participants.ParticipantInfo partInfo) {
          this.SurveyInfo = surveyInfo;
          this.IntakeCodeId = newIntakeCodeId;
          this.IntakeCode = newIntakeCode;
          this.PartInfo = partInfo;
        }
      }

      public class TemplateList : List<TemplateListItem> { }

      public class TemplateListItem {

        public int SurveyId { get; private set; }
        public string UniqueId { get; private set; }
        public bool IsAlbertSurvey { get; private set; }
        public string SurveyTypeCode { get; private set; }
        public string ReportType { get; private set; }
        public string InternalTitle { get; private set; }
        public string SurveyName { get; private set; }
        public int QnCount { get; private set; }
        public bool AlbertRatersOnly { get; private set; }
        public bool ShowSurveyLoggedIn { get; private set; }
        public FeedbackOptionEnum FeedbackOption { get; private set; }

        public TemplateListItem(
          int surveyId,
          string uniqueId,
          bool isAlbertSurvey,
          string surveyTypeCode,
          string reportType,
          string internalTitle,
          string surveyName,
          int qnCount,
          bool albertRatersOnly,
          bool showSurveyLoggedIn,
          FeedbackOptionEnum feedbackOption
        ) {
          this.SurveyId = surveyId;
          this.UniqueId = uniqueId;
          this.IsAlbertSurvey = isAlbertSurvey;
          this.SurveyTypeCode = surveyTypeCode;
          this.ReportType = reportType;
          this.InternalTitle = internalTitle;
          this.SurveyName = !surveyName.IsNullOrEmpty() ? surveyName : internalTitle;
          this.QnCount = qnCount;
          this.AlbertRatersOnly = albertRatersOnly;
          this.ShowSurveyLoggedIn = showSurveyLoggedIn;
          this.FeedbackOption = feedbackOption;
        }
      }

      public class SurveyInfo {

        // Note that CloseDate and LaunchDate are in survey-local time, not UTC.
        public int SurveyId { get; private set; }
        public string SurveyUID { get; private set; }
        public int? CoacheeId { get; private set; }
        public int OrgId { get; private set; }
        public Guid OrgGuid { get; private set; }
        public int? CompanyId { get; private set; }
        public string CompanyName { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public string FriendlyProjectTitle { get; private set; }
        public int? ProgramJobId { get; private set; }
        public Guid? BrandingOrgGuid { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public int? ClonedFromSurveyId { get; private set; }
        public string InternalTitle { get; private set; }
        public string SurveyName { get; private set; }
        public string SurveyTypeCode { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public int IntakeCodeId { get; private set; }
        public int IntakeNumber { get; private set; }
        public DateTime? ScheduledStartDateUtc { get; private set; }
        public DateTime? SentDateUtc { get; private set; }
        public DateTime CloseDateSelfLocal { get; private set; }
        public DateTime CloseDateRatersLocal { get; private set; }
        public TimeZoneInfo SurveyTimeZone { get; private set; }
        public ReportTypes.ReportTypeInfo ReportType { get; private set; }
        public FeedbackOptionEnum FeedbackOption { get; private set; }
        public bool IsRatersOnly { get; private set; }
        public bool HasOpenText { get; private set; }
        public bool IsAbleSurvey { get; private set; }
        public bool ShowSurveyLoggedIn { get; private set; }
        public string InvitationEmailSubject_Self { get; private set; }
        public string InvitationEmailBodyTemplate_Self { get; private set; }
        public string InvitationEmailSubject_Rater { get; private set; }
        public string InvitationEmailBodyTemplate_Rater { get; private set; }
        public bool GroupReportAvailable { get; private set; }
        public int? RatersSuggestedMin { get; private set; }
        public int? RatersSuggestedMax { get; private set; }
        public bool IsStrictRaterLimits { get; private set; }
        public int? SelfsInvited { get; private set; }
        public int? SelfsCompleted { get; private set; }
        public int? RatersInvited { get; private set; }
        public int? RatersCompleted { get; private set; }
        public string Instructions_Self { get; private set; }
        public string Instructions_Rater { get; private set; }
        public Participants.FoundParticipantBrief FoundParticipantBrief { get; private set; }
        public int? WorkshopEventId { get; internal set; }
        public string ComputedSenderEmailName { get; private set; }
        public string ComputedSenderEmailAddress { get; private set; }
        public int? PrimaryGblAnswerTypeId { get; private set; }
        public bool AllowReportWithoutRaters { get; private set; }
        public bool Hide360ReportNorms { get; private set; }
        public string ReportInformationHtml { get; private set; }
        public bool DisablePDF { get; private set; }
        public int? OrgReportMinCompletedRequired { get; private set; }
        public bool IsSharedWithUser { get; private set; }

        private List<OrgReportState> _orgReportState = new List<OrgReportState>();

        public SurveyInfo(
          int surveyId,
          string surveyUID,
          int? coacheeId,
          int tenantOrgId,
          Guid tenantOrgGuid,
          int? companyId,
          string companyName,
          string programJobNumber,
          string friendlyProjectTitle,
          int? programJobId,
          Guid? brandingOrgGuid,
          DateTime createdUtc,
          int? clonedFromSurveyId,
          string internalTitle,
          string surveyName,
          int? createdByUserId,
          TimeZoneInfo surveyTimeZone,
          int intakeCodeId,
          int intakeNumber,
          DateTime? scheduledStartDateUtc,
          DateTime? sentDate,
          DateTime closeDateSelfLocal,
          DateTime closeDateRatersLocal,
          ReportTypes.ReportTypeInfo reportType,
          FeedbackOptionEnum feedbackOption,
          int? ratersSuggestedMin,
          int? ratersSuggestedMax,
          bool isStrictRaterLimits,
          int? selfsInvited,
          int? selfsCompleted,
          int? ratersInvited,
          int? ratersCompleted,
          bool isRatersOnly,
          bool hasOpenText,
          bool groupReportAvailable,
          string invitationEmailSubject_Self, string invitationEmailBodyTemplate_Self,
          string invitationEmailSubject_Rater, string invitationEmailBodyTemplate_Rater,
          string surveyInstructions_Self, string surveyInstructions_Rater,
          int? workshopEventId,
          string computedSenderEmailName,
          string computedSenderEmailAddress,
          int? primaryGblAnswerTypeId,
          bool allowReportWithoutRaters,
          string surveyTypeCode,
          bool isAbleSurvey,
          bool showSurveyLoggedIn,
          bool hide360ReportNorms,
          string reportInformationHtml,
          bool disablePDF,
          int? orgReportMinCompletedRequired,
          bool isSharedWithUser = false
        ) {
          this.SurveyId = surveyId;
          this.SurveyUID = surveyUID;
          this.CoacheeId = coacheeId;
          this.OrgId = tenantOrgId;
          this.OrgGuid = tenantOrgGuid;
          this.CompanyId = companyId;
          this.CompanyName = companyName;
          this.ProgramJobNumber = programJobNumber;
          this.FriendlyProjectTitle = friendlyProjectTitle;
          this.ProgramJobId = programJobId;
          this.BrandingOrgGuid = brandingOrgGuid;
          this.CreatedUtc = createdUtc;
          this.ClonedFromSurveyId = clonedFromSurveyId;
          this.InternalTitle = internalTitle;
          this.SurveyName = !surveyName.IsNullOrEmpty() ? surveyName : internalTitle;
          this.CreatedByUserId = createdByUserId;
          this.IntakeCodeId = intakeCodeId;
          this.IntakeNumber = intakeNumber;
          this.ScheduledStartDateUtc = scheduledStartDateUtc;
          this.SentDateUtc = sentDate;
          this.CloseDateSelfLocal = closeDateSelfLocal;
          this.CloseDateRatersLocal = closeDateRatersLocal;
          this.SurveyTimeZone = surveyTimeZone;
          this.ReportType = reportType;
          this.FeedbackOption = feedbackOption;
          this.RatersSuggestedMin = ratersSuggestedMin;
          this.RatersSuggestedMax = ratersSuggestedMax;
          this.IsStrictRaterLimits = isStrictRaterLimits;
          this.RatersInvited = ratersInvited;
          this.RatersCompleted = ratersCompleted;
          this.SelfsInvited = selfsInvited;
          this.SelfsCompleted = selfsCompleted;
          this.IsRatersOnly = isRatersOnly;
          this.HasOpenText = hasOpenText;
          this.GroupReportAvailable = groupReportAvailable;
          this.InvitationEmailSubject_Self = invitationEmailSubject_Self;
          this.InvitationEmailBodyTemplate_Self = invitationEmailBodyTemplate_Self;
          this.InvitationEmailSubject_Rater = invitationEmailSubject_Rater;
          this.InvitationEmailBodyTemplate_Rater = invitationEmailBodyTemplate_Rater;
          this.Instructions_Self = surveyInstructions_Self;
          this.Instructions_Rater = surveyInstructions_Rater;
          this.WorkshopEventId = workshopEventId;
          this.ComputedSenderEmailName = computedSenderEmailName;
          this.ComputedSenderEmailAddress = computedSenderEmailAddress;
          this.PrimaryGblAnswerTypeId = primaryGblAnswerTypeId;
          this.AllowReportWithoutRaters = allowReportWithoutRaters;
          this.SurveyTypeCode = surveyTypeCode;
          this.IsAbleSurvey = isAbleSurvey;
          this.ShowSurveyLoggedIn = showSurveyLoggedIn;
          this.Hide360ReportNorms = hide360ReportNorms;
          this.ReportInformationHtml = reportInformationHtml;
          this.DisablePDF = disablePDF;
          this.OrgReportMinCompletedRequired = orgReportMinCompletedRequired;
          this.IsSharedWithUser = isSharedWithUser;

          _orgReportState = GetOrgReportState(IsClosedSelf, OrgReportMinCompletedRequired, SelfsCompleted);
        }

        internal void SetFoundParticipantBrief(SurveyInfo surveyInfo,
          int partId, string partUniqueId, int? coacheeId, int? coachUserId,
          int? userId, Guid? userGuid, int? programJobId, DateTime? completedUtc, int ratersInvited, int ratersCompleted,
          DateTime? reportSentUtc, DateTime? aiViewResultsCheckUtc, bool canLeaderView360Report, DateTime? personalSurveyClosedUtc) {

          this.FoundParticipantBrief = new Participants.FoundParticipantBrief(
            surveyInfo: surveyInfo,
            partId: partId,
            partUniqueId: partUniqueId,
            coacheeId: coacheeId,
            coachUserId: coachUserId,
            userId: userId,
            userGuid: userGuid,
            programJobId: programJobId,
            completedUtc: completedUtc,
            ratersInvited: ratersInvited,
            ratersCompleted: ratersCompleted,
            reportSentUtc: reportSentUtc,
            aiViewResultsCheckUtc: aiViewResultsCheckUtc,
            canLeaderView360Report: canLeaderView360Report,
            personalSurveyClosedUtc: personalSurveyClosedUtc
          );
        }

        // Note, surveys accept responses during the day of the "close date", but not after.
        // Assumed that close dates are stored in LOCAL time, NOT UTC. SurveyTimeZone stores the local time zone.
        public bool IsClosedSelf => DateTime.UtcNow.UtcToTZ(SurveyTimeZone).Date > this.CloseDateSelfLocal.Date;
        public bool IsClosedRaters => DateTime.UtcNow.UtcToTZ(SurveyTimeZone).Date > this.CloseDateRatersLocal.Date;

        public bool IsClosed {
          get {
            if (IsSelfOnly) return IsClosedSelf;
            if (IsRatersOnly) return IsClosedRaters;
            return IsClosedSelf && IsClosedRaters;
          }
        }

        public bool IsScheduledInFuture => ScheduledStartDateUtc.HasValue && ScheduledStartDateUtc.Value > DateTime.UtcNow;

        public SurveyTypeEnum SurveyType => GetSurveyType(SurveyTypeCode);

        public EvalTypeEnum EvalType => SurveyType != SurveyTypeEnum.Eval ? EvalTypeEnum.NotEval : (WorkshopEventId != null ? EvalTypeEnum.Workshop : EvalTypeEnum.Coaching);

        public List<OrgReportState> OrgReportState => _orgReportState;

        public DateTime GetSendDateUtc() {
          return GetSendDateUtc(CreatedUtc, SentDateUtc, ScheduledStartDateUtc);
        }

        public static DateTime GetSendDateUtc(DateTime createdUtc, DateTime? sentUtc, DateTime? scheduledUtc) {
          if (sentUtc == null && scheduledUtc != null) {
            return (DateTime)scheduledUtc;
          } else if (sentUtc != null) {
            return (DateTime)sentUtc;
          } else {
            return createdUtc; // Assumes survey sent at time of creation (default for Able).
          }
        }

        public int MinimumRatersRequiredForReport {
          get {
            if (IsSelfOnly || AllowReportWithoutRaters) return 0;
            if (IsStrictRaterLimits && RatersSuggestedMin != null) return RatersSuggestedMin.Value;
            return ConfigHelper.MinimumRatersFor360Report;
          }
        }

        public bool IsSelfOnly => IsSelfOnly(FeedbackOption, SurveyType);

        public bool IsOrgReportAvailable_Online => SurveyType == SurveyTypeEnum.IOS && OrgReportState.Contains(AlbertSurveys.OrgReportState.Ok);

      }

      public class TemplateInfo {

        public int SurveyId { get; private set; }
        public int OrgId { get; private set; }
        public string InternalTitle { get; private set; }
        public string SurveyName { get; private set; }
        public string SvTypeCode { get; private set; }
        public string SurveyTypeCode { get; private set; } // This is the new SurveyType which will eventually supercede svTypeCode (sv_typecode in the db)
        public ReportTypes.ReportTypeInfo ReportType { get; private set; }
        public string ReportSubType { get; private set; }
        public string ReportTitle { get; private set; }
        public string IntroDirections { get; private set; }
        public string IntroDirectionsRater { get; private set; }
        public string InvitationEmailSubject_Self { get; private set; }
        public string InvitationEmailBodyTemplate_Self { get; private set; }
        public string InvitationEmailSubject_Rater { get; private set; }
        public string InvitationEmailBodyTemplate_Rater { get; private set; }
        public FeedbackOptionEnum FeedbackOption { get; private set; }
        public bool GroupReportAvailable { get; private set; }
        public int QuestionSets { get; private set; }
        public int ManagerQuestionSet { get; private set; }
        public int PresetQuestionSet { get; private set; }
        public string CustomCopyrightText { get; private set; }
        public int RatersSuggestedMin { get; private set; }
        public int RatersSuggestedMax { get; private set; }
        public bool IsStrictRaterLimits { get; private set; }
        public bool RaterInheritsLevel { get; private set; }
        public bool IsRatersOnly { get; private set; }
        public bool ShowSurveyLoggedIn { get; private set; }
        public bool IsAlbertSurvey { get; private set; }
        public int? PrimaryGblAnswerTypeId { get; private set; }
        public bool AllowReportWithoutRaters { get; private set; }
        public bool Hide360ReportNorms { get; private set; }
        public string ReportInformationHtml { get; private set; }
        public bool DisablePDF { get; private set; }
        public int? OrgReportMinCompletedRequired { get; private set; }

        public TemplateInfo(
          int surveyId,
          int orgId,
          string internalTitle,
          string surveyName,
          string svTypeCode,
          string surveyTypeCode,
          ReportTypes.ReportTypeInfo reportType,
          string reportSubType,
          string reportTitle,
          string introDirections,
          string introDirectionsRater,
          string invitationEmailSubject_Self,
          string invitationEmailBodyTemplate_Self,
          string invitationEmailSubject_Rater,
          string invitationEmailBodyTemplate_Rater,
          FeedbackOptionEnum feedbackOption,
          bool groupReportAvailable,
          int questionSets,
          int managerQuestionSet,
          int presetQuestionSet,
          string customCopyrightText,
          int? ratersSuggestedMin,
          int? ratersSuggestedMax,
          bool isStrictRaterLimits,
          bool raterInheritsLevel,
          bool isRatersOnly,
          bool showSurveyLoggedIn,
          bool isAlbertSurvey,
          int? primaryGblAnswerTypeId,
          bool allowReportWithoutRaters,
          bool hide360ReportNorms,
          string reportInformationHtml,
          bool disablePDF,
          int? orgReportMinCompletedRequired
        ) {
          this.SurveyId = surveyId;
          this.OrgId = orgId;
          this.InternalTitle = internalTitle;
          this.SurveyName = !surveyName.IsNullOrEmpty() ? surveyName : internalTitle;
          this.SvTypeCode = svTypeCode;
          this.SurveyTypeCode = surveyTypeCode;
          this.ReportType = reportType;
          this.ReportSubType = reportSubType;
          this.ReportTitle = reportTitle;
          this.IntroDirections = introDirections;
          this.IntroDirectionsRater = introDirectionsRater;
          this.InvitationEmailSubject_Self = invitationEmailSubject_Self;
          this.InvitationEmailBodyTemplate_Self = invitationEmailBodyTemplate_Self;
          this.InvitationEmailSubject_Rater = invitationEmailSubject_Rater;
          this.InvitationEmailBodyTemplate_Rater = invitationEmailBodyTemplate_Rater;
          this.FeedbackOption = feedbackOption;
          this.GroupReportAvailable = groupReportAvailable;
          this.QuestionSets = questionSets;
          this.ManagerQuestionSet = managerQuestionSet;
          this.PresetQuestionSet = presetQuestionSet;
          this.CustomCopyrightText = customCopyrightText;
          this.RatersSuggestedMin = ratersSuggestedMin == null ? 0 : (int)ratersSuggestedMin;
          this.RatersSuggestedMax = ratersSuggestedMax == null ? 0 : (int)ratersSuggestedMax;
          this.IsStrictRaterLimits = isStrictRaterLimits;
          this.RaterInheritsLevel = raterInheritsLevel;
          this.IsRatersOnly = isRatersOnly;
          this.ShowSurveyLoggedIn = showSurveyLoggedIn;
          this.IsAlbertSurvey = isAlbertSurvey;
          this.PrimaryGblAnswerTypeId = primaryGblAnswerTypeId;
          this.AllowReportWithoutRaters = allowReportWithoutRaters;
          this.Hide360ReportNorms = hide360ReportNorms;
          this.ReportInformationHtml = reportInformationHtml;
          this.DisablePDF = disablePDF;
          this.OrgReportMinCompletedRequired = orgReportMinCompletedRequired;
        }

        public void SetInternalTitle(string title) {
          this.InternalTitle = title;
        }
        public void SetSurveyName(string newSurveyName) {
          this.SurveyName = newSurveyName;
        }
        public void SetReportTitle(string reportTitle) {
          this.ReportTitle = reportTitle;
        }
      }

      public class SurveyUserReminderInfo {
        // Note that CloseDate and LaunchDate are in survey-local time, not UTC.
        public int PartId { get; private set; }
        public string PartUniqueId { get; private set; }
        public int? CoacheeId { get; private set; }
        public int? CoachUserId { get; private set; }
        public int? ProgramJobId { get; private set; }
        public DateTime? CompletedUtc { get; private set; }
        public int RatersInvited { get; private set; }
        public int RatersCompleted { get; private set; }
        public DateTime? ReportSentUtc { get; private set; }
        public int SurveyId { get; private set; }
        public string SurveyUID { get; private set; }
        public int OrgId { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public string FriendlyProjectTitle { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public int? ClonedFromSurveyId { get; private set; }
        public string InternalTitle { get; private set; }
        public string SurveyName { get; private set; }
        public string SurveyTypeCode { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public int IntakeCodeId { get; private set; }
        public int IntakeNumber { get; private set; }
        public DateTime? ScheduledStartDateUtc { get; private set; }
        public DateTime? FirstInvitationSentUtc { get; private set; }
        public DateTime? LatestReminderSentUtc { get; private set; }
        public DateTime CloseDateSelfLocal { get; private set; }
        public DateTime CloseDateRatersLocal { get; private set; }
        public TimeZoneInfo SurveyTimeZone { get; private set; }
        public ReportTypes.ReportTypeInfo ReportType { get; private set; }
        public FeedbackOptionEnum FeedbackOption { get; private set; }
        public bool IsRatersOnly { get; private set; }
        public bool IsSelf { get; private set; }
        public int? PartUserId { get; private set; }
        public string PartName { get; private set; }
        public string PartFirstName { get; private set; }
        public string PartLastName { get; private set; }
        public string PartEmail { get; private set; }
        public string SelfName { get; private set; }
        public string SelfFirstName { get; private set; }
        public string SelfLastName { get; private set; }
        public string SelfEmail { get; private set; }
        public bool ShowSurveyLoggedIn { get; private set; }
        public int? RatersSuggestedMin { get; private set; }
        public int? RatersSuggestedMax { get; private set; }
        public bool IsStrictRaterLimits { get; private set; }
        public int? WorkshopEventId { get; private set; }
        public string WorkshopTitle { get; private set; }
        public string ComputedSenderEmailName { get; private set; }
        public string ComputedSenderEmailAddress { get; private set; }
        public int? PrimaryGblAnswerTypeId { get; private set; }
        public int? SurveyReminderCadenceId { get; private set; }
        public int? SurveyReminderCadenceId_Raters { get; private set; }
        public bool UseReminder { get; private set; }
        public int? CompanyId { get; private set; }
        public string CompanyName { get; private set; }
        public Guid? BrandingOrgGuid { get; private set; }
        public bool AllowReportWithoutRaters { get; private set; }

        public SurveyUserReminderInfo(
          int partId,
          string partUniqueId,
          int? coacheeId,
          int? coachUserId,
          int? programJobId,
          DateTime? completedUtc,
          int ratersInvited,
          int ratersCompleted,
          DateTime? reportSentUtc,
          int surveyId,
          string surveyUID,
          int orgId,
          string programJobNumber,
          string friendlyProjectTitle,
          DateTime createdUtc,
          int? clonedFromSurveyId,
          string internalTitle,
          string surveyName,
          int? createdByUserId,
          TimeZoneInfo surveyTimeZone,
          int intakeCodeId,
          int intakeNumber,
          DateTime? scheduledStartDateUtc,
          DateTime? firstInvitationSentUtc,
          DateTime? latestReminderSentUtc,
          DateTime closeDateSelfLocal,
          DateTime closeDateRatersLocal,
          ReportTypes.ReportTypeInfo reportType,
          FeedbackOptionEnum feedbackOption,
          int? ratersSuggestedMin,
          int? ratersSuggestedMax,
          bool isStrictRaterLimits,
          bool isRatersOnly,
          bool isSelf,
          int? partUserId,
          string partName,
          string partFirstName,
          string partLastName,
          string partEmail,
          int? workshopEventId,
          string workshopTitle,
          string computedSenderEmailName,
          string computedSenderEmailAddress,
          int? primaryGblAnswerTypeId,
          string surveyTypeCode,
          bool showSurveyLoggedIn,
          int? surveyReminderCadenceId,
          int? surveyReminderCadenceId_Raters,
          bool useReminder,
          bool allowReportWithoutRaters,
          string selfName,
          string selfFirstName,
          string selfLastName,
          string selfEmail,
          int? companyId,
          string companyName,
          Guid? brandingOrgGuid
        ) {
          this.PartId = partId;
          this.PartUniqueId = partUniqueId;
          this.CoacheeId = coacheeId;
          this.CoachUserId = coachUserId;
          this.ProgramJobId = programJobId;
          this.CompletedUtc = completedUtc;
          this.RatersInvited = ratersInvited;
          this.RatersCompleted = ratersCompleted;
          this.ReportSentUtc = reportSentUtc;
          this.SurveyId = surveyId;
          this.SurveyUID = surveyUID;
          this.OrgId = orgId;
          this.ProgramJobNumber = programJobNumber;
          this.FriendlyProjectTitle = friendlyProjectTitle;
          this.CreatedUtc = createdUtc;
          this.ClonedFromSurveyId = clonedFromSurveyId;
          this.InternalTitle = internalTitle;
          this.SurveyName = !surveyName.IsNullOrEmpty() ? surveyName : internalTitle;
          this.CreatedByUserId = createdByUserId;
          this.IntakeCodeId = intakeCodeId;
          this.IntakeNumber = intakeNumber;
          this.ScheduledStartDateUtc = scheduledStartDateUtc;
          this.FirstInvitationSentUtc = firstInvitationSentUtc;
          this.LatestReminderSentUtc = latestReminderSentUtc;
          this.CloseDateSelfLocal = closeDateSelfLocal;
          this.CloseDateRatersLocal = closeDateRatersLocal;
          this.SurveyTimeZone = surveyTimeZone;
          this.ReportType = reportType;
          this.FeedbackOption = feedbackOption;
          this.RatersSuggestedMin = ratersSuggestedMin;
          this.RatersSuggestedMax = ratersSuggestedMax;
          this.IsStrictRaterLimits = isStrictRaterLimits;
          this.IsRatersOnly = isRatersOnly;
          this.IsSelf = isSelf;
          this.PartUserId = partUserId;
          this.PartName = partName;
          this.PartFirstName = partFirstName;
          this.PartLastName = partLastName;
          this.PartEmail = partEmail;
          this.WorkshopEventId = workshopEventId;
          this.WorkshopTitle = workshopTitle;
          this.ComputedSenderEmailName = computedSenderEmailName;
          this.ComputedSenderEmailAddress = computedSenderEmailAddress;
          this.PrimaryGblAnswerTypeId = primaryGblAnswerTypeId;
          this.SurveyTypeCode = surveyTypeCode;
          this.ShowSurveyLoggedIn = showSurveyLoggedIn;
          this.SurveyReminderCadenceId = surveyReminderCadenceId;
          this.SurveyReminderCadenceId_Raters = surveyReminderCadenceId_Raters ?? surveyReminderCadenceId; // Default to self setting if not set.
          this.UseReminder = useReminder;
          this.AllowReportWithoutRaters = allowReportWithoutRaters;
          this.SelfName = selfName;
          this.SelfFirstName = selfFirstName;
          this.SelfLastName = selfLastName;
          this.SelfEmail = selfEmail;
          this.CompanyId = companyId;
          this.CompanyName = companyName;
          this.BrandingOrgGuid = brandingOrgGuid;
        }

        public bool IsAbleSurvey => this.OrgId == ConfigHelper.AbleSurveyOrgId;

        // Note, surveys accept responses during the day of the "close date", but not after.
        // Assumed that close dates are stored in LOCAL time, NOT UTC. SurveyTimeZone stores the local time zone.
        public bool IsClosedSelf => DateTime.UtcNow.UtcToTZ(SurveyTimeZone).Date > this.CloseDateSelfLocal.Date;
        public bool IsClosedRaters => DateTime.UtcNow.UtcToTZ(SurveyTimeZone).Date > this.CloseDateRatersLocal.Date;

        public bool IsClosed {
          get {
            if (IsSelfOnly) return IsClosedSelf;
            if (IsRatersOnly) return IsClosedRaters;
            return IsClosedSelf && IsClosedRaters;
          }
        }

        public bool IsScheduled => ScheduledStartDateUtc >= DateTime.UtcNow;

        public SurveyTypeEnum SurveyType => GetSurveyType(SurveyTypeCode);

        public EvalTypeEnum EvalType => SurveyType != SurveyTypeEnum.Eval ? EvalTypeEnum.NotEval : (WorkshopEventId != null ? EvalTypeEnum.Workshop : EvalTypeEnum.Coaching);
        public bool IsCompleted => this.CompletedUtc != null;
        public DateTime GetSendDateUtc() {
          return GetSendDateUtc(CreatedUtc, FirstInvitationSentUtc, ScheduledStartDateUtc);
        }

        public static DateTime GetSendDateUtc(DateTime createdUtc, DateTime? sentUtc, DateTime? scheduledUtc) {
          if (sentUtc == null && scheduledUtc != null) {
            return (DateTime)scheduledUtc;
          } else if (sentUtc != null) {
            return (DateTime)sentUtc;
          } else {
            return createdUtc; // Assumes survey sent at time of creation (default for Able).
          }
        }

        public int MinimumRatersRequiredForReport {
          get {
            if (IsSelfOnly || AllowReportWithoutRaters) return 0;
            if (IsStrictRaterLimits && RatersSuggestedMin != null) return RatersSuggestedMin.Value;
            return ConfigHelper.MinimumRatersFor360Report;
          }
        }

        public bool IsSelfOnly => IsSelfOnly(FeedbackOption, SurveyType);

        // Always return a cadence object, use default cadence if CadenceId is null.
        public SurveyReminderCadence SurveyReminderCadence => SurveyReminderCadence.GetByIdOrDefault(SurveyReminderCadenceId);
        // Default to above if CadenceId_Raters is null.
        public SurveyReminderCadence SurveyReminderCadence_Raters => SurveyReminderCadence.GetByIdOrDefault(SurveyReminderCadenceId_Raters);
      }

      public static bool IsSelfOnly(FeedbackOptionEnum feedbackOption, SurveyTypeEnum surveyType) {
        return
          feedbackOption == FeedbackOptionEnum.NoRaters
          || surveyType == SurveyTypeEnum.DevelopmentPlan
          || surveyType == SurveyTypeEnum.Intake
          || surveyType == SurveyTypeEnum.Eval
          || surveyType == SurveyTypeEnum.IOS;
      }

      private static List<OrgReportState> GetOrgReportState(bool isSurveyClosed, int? minCompletedRequired, int? completedCount) {
        // Whether survey itself is in a valid state to produce a report.
        // Note this is separate from whether a user is *allowed* to view a report - see AppAccess for that.
        // Returns either "ok" in one item, or a list of reasons why survey isn't in a state to report.
        var result = new List<OrgReportState>();
        if (!isSurveyClosed) result.Add(OrgReportState.SurveyNotClosed);
        if ((completedCount ?? 0) < (minCompletedRequired ?? OrgReportMinCompletedRequired)) result.Add(OrgReportState.NotEnoughResponses);
        if (result.Count == 0) result.Add(OrgReportState.Ok); // No problems found.
        return result;
      }

      // Used to maintain a cache of SurveyInfo to save db calls in loops.
      public class SurveyInfoCache {

        List<SurveyInfo> _surveyInfoCache;

        public SurveyInfoCache() {

          _surveyInfoCache = new List<SurveyInfo>();
        }

        // Return Survey in cache if found, or from db then add to cache, or null if not found.
        public SurveyInfo GetSurveyInfoOrNull(int surveyId, int intakeNumber) {

          var surveyInfo = _surveyInfoCache.Find(s => s.SurveyId == surveyId && s.IntakeNumber == intakeNumber);

          if (surveyInfo == null) {
            surveyInfo = AlbertSurveys.GetSurveyInfo(surveyId, intakeNumber); // Get from db if not in cache.
            if (surveyInfo != null) _surveyInfoCache.Add(surveyInfo); // If found, add to cache.
          }

          return surveyInfo;
        }
      }

    }
  }
}
