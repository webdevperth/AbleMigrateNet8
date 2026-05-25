using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using static Integral.Web.DbHelper.Common;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Participants {

      public const int Default_DateGroup_Code = 1; // The first date group ("intake") of a survey.
      public const int UniqueId_Assign_MaxTries = 20; // Number of tries to assign a uniqueid before failing.

      // Flag values used for columns PrePost360ForUser and PrePost360ForCoachee.
      public class PrePostFlags {
        public const int None = 0;
        public const int IsPreSurvey = 1;
        public const int IsPostSurvey = 2;
      }
      public enum PrePostScopeEnum { User, Coachee }
      public static string GetPrePostFlagCol(PrePostScopeEnum scope) => scope == PrePostScopeEnum.Coachee ? "PrePost360ForCoachee" : "PrePost360ForUser";

      private const string UniqueIdValidChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
      public const string UniqueIdValidRegex = "[0-9a-zA-Z]+";
      private const int UniqueIdMinLength = 10;
      private const int UniqueIdMaxLength = 20;

      static Participants() {
      }

      public static string GetValidUniqueId(string UIDtoCheck) {
        // Return input if it is a valid UID otherwise return empty string.
        UIDtoCheck = UIDtoCheck.EmptyIfNull().TrimWhitespace();
        if (IsValidUniqueId(UIDtoCheck)) return UIDtoCheck;
        else return "";
      }

      public static bool IsValidUniqueId(string UIDToCheck) {
        var isMatch = Regex.IsMatch(UIDToCheck, "^" + UniqueIdValidRegex + "$");
        return isMatch;
      }

      private static string GetRandomUniqueId() {
        // Note always use on use of Random() - as of Apr 2019, multiple raters can be created at once,
        // in a loop, sometimes leading to new Random() obtainnig the same seed more than once in a row.
        // So from now always use AppHelper.GetRandomInstance() to ensure different seeds each time.
        var rnd = AppHelper.GetRandomInstance();
        string uniqueId = "";
        int length = rnd.Next(UniqueIdMinLength, UniqueIdMaxLength);
        for (int i = 0; i < length; i++) {
          int charPos = rnd.Next(0, UniqueIdValidChars.Length - 1);
          uniqueId += UniqueIdValidChars.Substring(charPos, 1);
        }
        return uniqueId;
      }

      public static ParticipantList GetSelfList(int surveyId, int intakeCode) {
        // Return list of "self" participants. For Able surveys, there is usually just one, which is the coachee.
        return GetParticipantList(surveyId, intakeCode, null);
      }

      public static ParticipantList GetRaterList(int surveyId, int intakeCode, int selfPartId) {
        return GetParticipantList(surveyId, intakeCode, selfPartId);
      }

      // Get all self participants in a survey (ratersForParticipantId = null)
      // or get all raters for a specific participant (ratersForParticipantId > 0)
      //
      // Note that if we're getting a self, then table 'p' will be the self and 'pself' will not be found (hence outer join to pself),
      // Therefore "ISNULL(pself.column, p.column)" ensures the value always comes from the self participant.
      // Raters usually don't have certain columns populated (JobId, CoacheeId, etc), so we must inherit those values from the self.
      private static ParticipantList GetParticipantList(int surveyId, int intakeCode, int? selfPartId = null) {

        var partList = new ParticipantList(surveyId, intakeCode);

        using (var conn = GetNewOpenConnection()) {
          using (var cmd = new SqlCommand(@"
          SELECT
            s.sv_uniqueid AS SurveyUID,
            ij.JobNumber,
            p.PartId, p.UniqueId, p.SurveyId, p.PersonId, p.Self_PartId,
            p.FirstName, p.LastName, p.Name, p.Email, p.Phone,
            p.IsSelf, p.IsSelfMgr, p.IntakeCodeId, p.DateGroupCode, p.QuestionSet, p.LastInvitationSent, p.ReportSentUtc, p.AI_ViewResultsCheckUtc,
            p.CreatedUTC, p.SurveyLastUpdatedUtc, p.Completed, p.Declined, p.InvitationBouncedUTC, p.IsBounceAutoReply, p.PiSaved,
            pr.RatersNotDeclined, pr.RatersCompleted,
            pself.UniqueId AS Self_UniqueId, pself.Name AS Self_Name,
            -- Inherit below values from the self participant if this is a rater.
            ISNULL(pself.AbleCoacheeId, p.AbleCoacheeId) AS AbleCoacheeId,
            ISNULL(pself.UserId, p.UserId) AS UserId,
            ISNULL(pself.AbleProgramJobId, p.AbleProgramJobId) AS AbleProgramJobId
          FROM sv_360_Participants p
          LEFT OUTER JOIN sv_360_Participants pself ON pself.PartId = p.Self_PartId
          LEFT OUTER JOIN id_Job ij ON ij.JobId = ISNULL(pself.AbleProgramJobId, p.AbleProgramJobId) -- Ensure join from self value of AbleProgramJobId.
          INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
          CROSS APPLY (
            SELECT COUNT(PartId) AS RatersNotDeclined, COUNT(Completed) AS RatersCompleted
            FROM sv_360_Participants pr
            WHERE pr.Self_PartId = p.PartId
              AND pr.Declined IS NULL
          ) AS pr
          WHERE p.SurveyId = @SurveyId
            AND p.DateGroupCode = @DateGroupCode
            AND (@Self_PartId IS NULL AND p.Self_PartId IS NULL OR p.Self_PartId = @Self_PartId)
          ORDER BY p.Name
          ", conn)) {
            cmd.Parameters.Add("@SurveyId", SqlDbType.Int).Value = surveyId;
            cmd.Parameters.Add("@DateGroupCode", SqlDbType.Int).Value = intakeCode;
            cmd.Parameters.AddInt("@Self_PartId", selfPartId);
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                partList.Participants.Add(new ParticipantInfo(
                  partId: dr.GetInt("PartId"),
                  partUID: dr.GetString("UniqueId"),
                  surveyId: dr.GetInt("SurveyId"),
                  surveyUID: dr.GetString("SurveyUID"),
                  coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
                  userId: dr.GetIntOrNull("UserId"),
                  programJobId: dr.GetIntOrNull("AbleProgramJobId"),
                  jobNumber: dr.GetString("JobNumber"),
                  personId: dr.GetIntOrNull("PersonId"),
                  selfPartId: dr.GetIntOrNull("Self_PartId"),
                  selfPartUID: dr.GetString("Self_UniqueId"),
                  selfName: dr.GetString("Self_Name"),
                  firstName: dr.GetString("FirstName"),
                  lastName: dr.GetString("LastName"),
                  fullName: dr.GetString("Name"),
                  email: dr.GetString("Email"),
                  phone: dr.GetString("Phone"),
                  isSelf: dr.GetBoolFromInt("IsSelf"),
                  isSelfManager: dr.GetBoolFromInt("IsSelfMgr"),
                  intakeCodeId: dr.GetInt("IntakeCodeId"),
                  intakeCode: dr.GetInt("DateGroupCode"),
                  questionSet: dr.GetIntOrDefault("QuestionSet", 1),
                  createdUTC: dr.GetDateTime("CreatedUTC"),
                  lastReminder: dr.GetDateTimeOrNull("LastInvitationSent"),
                  surveyLastUpdatedUtc: dr.GetDateTimeOrNull("SurveyLastUpdatedUtc"),
                  completedUTC: dr.GetDateTimeOrNull("Completed"),
                  declinedUTC: dr.GetDateTimeOrNull("Declined"),
                  invitationBouncedUTC: dr.GetDateTimeOrNull("InvitationBouncedUTC"),
                  isBounceAutoReply: dr.GetBoolFromInt("IsBounceAutoReply"),
                  reportSentUtc: dr.GetDateTimeOrNull("ReportSentUtc"),
                  ratersNotDeclined: dr.GetInt("RatersNotDeclined"),
                  ratersCompleted: dr.GetInt("RatersCompleted"),
                  aiViewResultsCheckUtc: dr.GetDateTimeOrNull("AI_ViewResultsCheckUtc"),
                  surveyValidatedUpToPageNumber: dr.GetIntOrNull("PiSaved")
                ));
              }
            }
          }
        }
        return partList;
      }

      public static void UpdateReportSent(int participantId) {
        GetNonQueryInt(@"
          UPDATE sv_360_Participants
          SET ReportSentUtc = GETUTCDATE()
          WHERE PartId = @PartId",
          NewSqlParameter("PartId", participantId)
        );
      }

      // Get all participants in an Org (IOS) survey.
      // All Org participants are "selfs", there are no raters.
      public static ParticipantList GetOrgParticipantList(int surveyId, int? divCodeIdOrNullForAll) {

        var partList = new ParticipantList(surveyId, Default_DateGroup_Code); // Org surveys always have just 1 intake.

        using (var conn = GetNewOpenConnection()) {
          using (var cmd = new SqlCommand(@"
          SELECT
            s.sv_uniqueid AS SurveyUID,
            ij.JobNumber,
            p.PartId, p.UniqueId, p.SurveyId, p.PersonId, p.Self_PartId,
            p.FirstName, p.LastName, p.Name, p.Email, p.Phone,
            p.IsSelf, p.IsSelfMgr, p.IntakeCodeId, p.DateGroupCode, p.QuestionSet, p.LastInvitationSent, p.ReportSentUtc, p.AI_ViewResultsCheckUtc,
            p.CreatedUTC, p.SurveyLastUpdatedUtc, p.Completed, p.Declined, p.InvitationBouncedUTC, p.IsBounceAutoReply,
            p.AbleCoacheeId, p.UserId, p.AbleProgramJobId
          FROM sv_360_Participants p
          INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
          LEFT OUTER JOIN id_Job ij ON ij.JobId = p.AbleProgramJobId -- Org survey participants don't always have a program.
          WHERE p.SurveyId = @SurveyId
            AND (@OrgDivisionCodeId IS NULL OR p.OrgDivisionCodeId = @OrgDivisionCodeId)
          ORDER BY p.Name
          ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", surveyId);
            cmd.Parameters.AddInt("@OrgDivisionCodeId", divCodeIdOrNullForAll);
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                partList.Participants.Add(new ParticipantInfo(
                  partId: dr.GetInt("PartId"),
                  partUID: dr.GetString("UniqueId"),
                  surveyId: dr.GetInt("SurveyId"),
                  surveyUID: dr.GetString("SurveyUID"),
                  coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
                  userId: dr.GetIntOrNull("UserId"),
                  programJobId: dr.GetIntOrNull("AbleProgramJobId"),
                  jobNumber: dr.GetString("JobNumber"),
                  personId: dr.GetIntOrNull("PersonId"),
                  selfPartId: null,
                  selfPartUID: null,
                  selfName: "",
                  firstName: dr.GetString("FirstName"),
                  lastName: dr.GetString("LastName"),
                  fullName: dr.GetString("Name"),
                  email: dr.GetString("Email"),
                  phone: dr.GetString("Phone"),
                  isSelf: dr.GetBoolFromInt("IsSelf"),
                  isSelfManager: dr.GetBoolFromInt("IsSelfMgr"),
                  intakeCodeId: dr.GetInt("IntakeCodeId"),
                  intakeCode: dr.GetInt("DateGroupCode"),
                  questionSet: dr.GetIntOrDefault("QuestionSet", 1),
                  createdUTC: dr.GetDateTime("CreatedUTC"),
                  lastReminder: dr.GetDateTimeOrNull("LastInvitationSent"),
                  surveyLastUpdatedUtc: dr.GetDateTimeOrNull("SurveyLastUpdatedUtc"),
                  completedUTC: dr.GetDateTimeOrNull("Completed"),
                  declinedUTC: dr.GetDateTimeOrNull("Declined"),
                  invitationBouncedUTC: dr.GetDateTimeOrNull("InvitationBouncedUTC"),
                  isBounceAutoReply: dr.GetBoolFromInt("IsBounceAutoReply"),
                  reportSentUtc: dr.GetDateTimeOrNull("ReportSentUtc"),
                  ratersNotDeclined: 0,
                  ratersCompleted: 0,
                  aiViewResultsCheckUtc: dr.GetDateTimeOrNull("AI_ViewResultsCheckUtc"),
                  surveyValidatedUpToPageNumber: null
                ));
              }
            }
          }
        }
        return partList;
      }

      public static ParticipantInfo GetParticipantInfo(int? userOrgId, int surveyId, int partId) {
        return GetParticipantInfo(userOrgId, surveyId, null, partId, null);
      }
      public static ParticipantInfo GetParticipantInfo(int? userOrgId, int surveyId, string partUID) {
        return GetParticipantInfo(userOrgId, surveyId, partUID, 0, null);
      }
      public static ParticipantInfo GetParticipantBySurveyAndCoachee(int? userOrgId, int surveyId, int coacheeId) {
        return GetParticipantInfo(userOrgId, surveyId, null, null, coacheeId);
      }

      // Get a single participant record, self or rater. If coacheeId is passed, then it's definitely a self.
      private static ParticipantInfo GetParticipantInfo(int? orgId, int? surveyId, string partUID, int? partId, int? coacheeId) {

        if (partId <= 0 && !IsValidUniqueId(partUID)) return null; // Must provide one or the other.

        ParticipantInfo result = null;

        Query(@"
          SELECT
            s.sv_uniqueid AS SurveyUID,
            p.PartId, p.UniqueId AS PartUID, p.SurveyId, p.Self_PartId, p.IsSelf, p.IsSelfMgr,
            p.PersonId, p.FirstName, p.LastName, p.Name, p.Email, p.Phone,
            p.IntakeCodeId, p.DateGroupCode, p.QuestionSet, p.LastInvitationSent, p.ReportSentUtc, p.AI_ViewResultsCheckUtc,
            p.CreatedUTC, p.SurveyLastUpdatedUtc, p.Completed, p.Declined, p.InvitationBouncedUTC, p.IsBounceAutoReply, p.PiSaved,
            ij.JobNumber,
            pself.UniqueId AS Self_UniqueId, pself.Name AS Self_Name,
            pr.RatersNotDeclined, pr.RatersCompleted,
            -- Inherit below values from the self participant if this is a rater.
            ISNULL(pself.AbleCoacheeId, p.AbleCoacheeId) AS AbleCoacheeId,
            ISNULL(pself.UserId, p.UserId) AS UserId,
            ISNULL(pself.AbleProgramJobId, p.AbleProgramJobId) AS AbleProgramJobId
          FROM sv_360_Participants p
          INNER JOIN sv_Survey s ON p.SurveyId = s.sv_id
          LEFT OUTER JOIN sv_360_Participants pself ON pself.PartId = p.Self_PartId
          LEFT OUTER JOIN id_Job ij ON ij.JobId = ISNULL(pself.AbleProgramJobId, p.AbleProgramJobId) -- Ensure join from self value of AbleProgramJobId.
          CROSS APPLY (
            SELECT COUNT(PartId) AS RatersNotDeclined, COUNT(Completed) AS RatersCompleted
            FROM sv_360_Participants pr
            WHERE pr.Self_PartId = p.PartId
              AND pr.Declined IS NULL
              AND pr.InvitationBouncedUTC IS NULL
          ) AS pr
          WHERE (@PartId IS NULL OR p.PartId = @PartId)
            AND (@SurveyId IS NULL OR s.sv_id = @SurveyId)
            AND (@CoacheeId IS NULL OR p.AbleCoacheeId = @CoacheeId) -- always self if passed coacheeId
            AND (@PartUID IS NULL OR p.UniqueId = @PartUID)
            AND (@OrgId IS NULL OR s.sv_OrgId = @OrgId)",
          dr => {
            result = new ParticipantInfo(
              partId: dr.GetInt("PartId"),
              partUID: dr.GetString("PartUID"),
              surveyId: dr.GetInt("SurveyId"),
              surveyUID: dr.GetString("SurveyUID"),
              coacheeId: dr.GetIntOrNull("AbleCoacheeId"),
              userId: dr.GetIntOrNull("UserId"),
              programJobId: dr.GetIntOrNull("AbleProgramJobId"),
              jobNumber: dr.GetString("JobNumber"),
              personId: dr.GetIntOrNull("PersonId"),
              selfPartId: dr.GetIntOrNull("Self_PartId"),
              selfPartUID: dr.GetString("Self_UniqueId"),
              selfName: dr.GetString("Self_Name"),
              firstName: dr.GetString("FirstName"),
              lastName: dr.GetString("LastName"),
              fullName: dr.GetString("Name"),
              email: dr.GetString("Email"),
              phone: dr.GetString("Phone"),
              isSelf: dr.GetBoolFromInt("IsSelf"),
              isSelfManager: dr.GetBoolFromInt("IsSelfMgr"),
              intakeCodeId: dr.GetInt("IntakeCodeId"),
              intakeCode: dr.GetInt("DateGroupCode"),
              questionSet: dr.GetIntOrDefault("QuestionSet", 1),
              createdUTC: dr.GetDateTime("CreatedUTC"),
              lastReminder: dr.GetDateTimeOrNull("LastInvitationSent"),
              surveyLastUpdatedUtc: dr.GetDateTimeOrNull("SurveyLastUpdatedUtc"),
              completedUTC: dr.GetDateTimeOrNull("Completed"),
              declinedUTC: dr.GetDateTimeOrNull("Declined"),
              invitationBouncedUTC: dr.GetDateTimeOrNull("InvitationBouncedUTC"),
              isBounceAutoReply: dr.GetBoolFromInt("IsBounceAutoReply"),
              reportSentUtc: dr.GetDateTimeOrNull("ReportSentUtc"),
              ratersNotDeclined: dr.GetInt("RatersNotDeclined"),
              ratersCompleted: dr.GetInt("RatersCompleted"),
              aiViewResultsCheckUtc: dr.GetDateTimeOrNull("AI_ViewResultsCheckUtc"),
              surveyValidatedUpToPageNumber: dr.GetIntOrNull("PiSaved")
            );
          },
          NewSqlParameter("OrgId", orgId == 0 ? null : orgId),
          NewSqlParameter("SurveyId", surveyId == 0 ? null : surveyId),
          NewSqlParameter("PartId", partId == 0 ? null : partId),
          NewSqlParameter("PartUID", partUID.IsNullOrEmpty() ? null : partUID),
          NewSqlParameter("CoacheeId", coacheeId == 0 ? null : coacheeId)
        );
        return result;
      }

      public static List<PastRaterInfo> GetPastRatersForParticipant(string selfEmail, int? selfUserId) {

        var pastRaters = new List<PastRaterInfo>();

        if (selfEmail.IsNullOrEmptyOrWhitespace() && selfUserId == null) return null;

        string whereClause = "";
        if (!selfEmail.IsNullOrEmptyOrWhitespace()) {
          whereClause = "pself.Email = @SelfEmail";
        }
        if (selfUserId == null) {
          whereClause = whereClause.EnsureEndsWith(" OR ", StringExt.Ensure.IfNotBlank) + "pself.UserId = @SelfUserId";
        }

        Query($@"
          SELECT
            p.Name AS RaterName, p.Email AS RaterEmail,
            CASE
              WHEN CHARINDEX(' ', p.Name) > 0 THEN LEFT(p.Name, CHARINDEX(' ', p.Name) - 1)
              ELSE p.Name
            END AS RaterFirstName,
            CASE
              WHEN CHARINDEX(' ', p.Name) > 0 THEN RIGHT(p.Name, LEN(p.Name) - CHARINDEX(' ', p.Name))
              ELSE ''
            END AS RaterLastName
            FROM sv_360_Participants p
            INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
            LEFT OUTER JOIN sv_360_Participants pself ON pself.PartId = p.Self_PartId
            WHERE
            s.IsAlbertSurvey = 1
            AND ({whereClause})
            ORDER BY p.Email",
            dr => {
              pastRaters.Add(new PastRaterInfo(
                name: dr.GetString("RaterName"),
                firstName: dr.GetString("RaterFirstName"),
                lastName: dr.GetString("RaterLastName"),
                email: dr.GetString("RaterEmail")
              ));
            },
            NewSqlParameter("SelfEmail", selfEmail),
            NewSqlParameter("SelfUserId", selfUserId)
         );

        return pastRaters;
      }

      // Get the most recent previous completed participant to the given one, where survey report types are the same.
      public static PreviousCompletedInfo GetLatestCompleted(int coacheeId, int minRaters) {
        return GetPreviousCompleted(coacheeId, 0, minRaters);
      }

      // Note if currentPartId = 0, we get the latest two completed survey participants of the coachee.
      // If currentPartId > 0 then get the completed participant of the coachee most recent to the given participant.
      public static PreviousCompletedInfo GetPreviousCompleted(int coacheeId, int currentPartId, int minRaters) {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = @"
            SELECT TOP 1
              pcur.PartId AS CurPartId, pcur.SurveyId AS CurSurveyId, pcur.DateGroupCode AS CurDC, pcur.Completed AS CurCompleted,
              ppre.PrePartId, ppre.PreSurveyId, ppre.PreDC, ppre.PreCompleted, ppre.PreRaters
            FROM sv_360_Participants pcur WITH (NOLOCK)
            OUTER APPLY (
              SELECT TOP 1
                ppre.PartId AS PrePartId, ppre.SurveyId AS PreSurveyId, ppre.DateGroupCode AS PreDC, ppre.Completed AS PreCompleted,
                COUNT(prpre.Completed) AS PreRaters
              FROM sv_360_Participants ppre WITH (NOLOCK)
              INNER JOIN sv_360_Participants prpre ON prpre.Self_PartId = ppre.PartId
              WHERE ppre.AbleCoacheeId = pcur.AbleCoacheeId
              AND ppre.Completed IS NOT NULL
                AND ppre.Completed < pcur.Completed
              GROUP BY ppre.PartId, ppre.SurveyId, ppre.DateGroupCode, ppre.Completed
              HAVING COUNT(prpre.Completed) >= @MinCompleteRaters
              ORDER BY ppre.Completed DESC
              ) AS ppre
            WHERE pcur.AbleCoacheeId = @CoacheeId
              AND pcur.Completed IS NOT NULL
              AND (@CurrentPartId IS NULL
                OR pcur.PartId = @CurrentPartId)
            ORDER BY pcur.Completed DESC";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@CoacheeId", coacheeId);
            cmd.Parameters.AddInt("@CurrentPartId", currentPartId > 0 ? (int?)currentPartId : null);
            cmd.Parameters.AddInt("@MinCompleteRaters", minRaters);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              if (dr.Read())
                return new PreviousCompletedInfo(
                  latestPartId: dr.GetInt("CurPartId"),
                  latestSurveyId: dr.GetInt("CurSurveyId"),
                  latestIntakeCode: dr.GetInt("CurDC"),
                  latestCompleted: dr.GetDateTime("CurCompleted"),
                  previousPartId: dr.GetIntOrNull("PrePartId"),
                  previousSurveyId: dr.GetIntOrNull("PreSurveyId"),
                  previousIntakeCode: dr.GetIntOrNull("PreDC"),
                  previousCompleted: dr.GetDateTimeOrNull("PreCompleted")
                );
            }
          }
        }
        return null;
      }

      public static bool UpdateParticipantInfo(int partId, string partName, string partEmail, string partPhone) {

        bool success = false;
        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"
          UPDATE " + DbTable.Participants + @" SET
            Name = @PartName,
            Email = @PartEmail,
            Phone = @PartPhone
          WHERE " + DbTable.Participants.IdentityColumn + @" = @PartId
          ", conn)) {
            cmd.Parameters.AddInt("@PartId", partId);
            cmd.Parameters.AddVarChar("@PartName", 50, partName);
            cmd.Parameters.AddVarChar("@PartEmail", 50, partEmail);
            cmd.Parameters.AddVarChar("@PartPhone", 50, partPhone);
            conn.Open();
            try {
              int recs = cmd.ExecuteNonQuery();
              if (recs == 1) success = true;
            } catch (Exception e) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(e)
                .WithOperation(nameof(UpdateParticipantInfo))
                .WithProperty(DalApplicationInsightsConstants.PartId, partId)
                .WithProperty(DalApplicationInsightsConstants.PartName, partName)
                .WithProperty(DalApplicationInsightsConstants.PartEmail, partEmail)
                .Track();

            }
          }
        }
        return success;
      }

      public static bool SetBouncedStatus(int partId, bool isBounceAutoReply) {
        return SetBounced(ConfigHelper.IntegralDbConnectionString, partId, DateTime.UtcNow, isBounceAutoReply);
      }
      public static bool SetBouncedStatus_Integral(int partId, bool isBounceAutoReply) {
        return SetBounced(ConfigHelper.IntegralDbConnectionString, partId, DateTime.UtcNow, isBounceAutoReply);
      }
      public static bool ClearBouncedStatus(int partId) {
        return SetBounced(ConfigHelper.IntegralDbConnectionString, partId, null, false);
      }
      public static bool ClearBouncedStatus_Integral(int partId) {
        return SetBounced(ConfigHelper.IntegralDbConnectionString, partId, null, false);
      }
      private static bool SetBounced(string connectionString, int PartId, DateTime? invitationBouncedUTC, bool isBounceAutoReply) {
        // Note setting InvitationBouncedUTC to null clears the bounce status.
        // IsBounceAutoReply indicates either an "undelivered" bounce (false) or auto-reply ("out of office") bounce (true).
        bool success = false;
        using (var conn = new SqlConnection(connectionString)) {
          using (var cmd = new SqlCommand(@"
          UPDATE " + DbTable.Participants + @" SET
            InvitationBouncedUTC = @InvitationBouncedUTC,
            IsBounceAutoReply = @IsBounceAutoReply
          WHERE " + DbTable.Participants.IdentityColumn + @" = @PartId
          ", conn)) {
            cmd.Parameters.AddInt("@PartId", PartId);
            cmd.Parameters.AddDateTime("@InvitationBouncedUTC", invitationBouncedUTC);
            cmd.Parameters.AddTinyIntFromBool("@IsBounceAutoReply", isBounceAutoReply);
            conn.Open();
            try {
              int recs = cmd.ExecuteNonQuery();
              if (recs == 1) success = true;
            } catch (Exception e) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(e)
                .WithOperation(nameof(SetBounced))
                .WithProperty(DalApplicationInsightsConstants.PartId, PartId)
                .WithProperty(DalApplicationInsightsConstants.InvitationBouncedUTC, invitationBouncedUTC)
                .WithProperty(DalApplicationInsightsConstants.IsBounceAutoReply, isBounceAutoReply)
                .Track();

            }
          }
        }
        return success;
      }

      public static void UpdateSurveyResponse(
        DbHelper.Participants.ParticipantInfo partInfo,
        List<DbHelper.Questions.SurveyQuestionInfo> surveyQuestionsToUpdate) {

        // Requires the answer code, codeid, etc. to be in the visibleQuestions data.

        // List of qnids for below SQL.
        string qnIdsForSQL = "";
        foreach (var qnInfo in surveyQuestionsToUpdate) {
          if (qnIdsForSQL.Length > 0) qnIdsForSQL += ",";
          qnIdsForSQL += "(" + qnInfo.QuestionId + ")";
        }

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          conn.Open();
          using (var trans = conn.BeginTransaction()) {
            using (var cmd = new SqlCommand()) {
              cmd.Connection = conn;
              cmd.Transaction = trans;

              // Delete answers on this page.
              cmd.CommandText = @"
              DECLARE @tblQnIds TABLE ( QuestionId INT );
              INSERT @tblQnIds VALUES " + qnIdsForSQL + @";
              DELETE ma
                FROM sv_AnswersMulti ma
                INNER JOIN sv_Answers a ON ma.AnswerId = a.AnswerId
                INNER JOIN @tblQnIds t ON a.QuestionId = t.QuestionId
                WHERE a.ParticipantId = @ParticipantId;
              DELETE ta
                FROM sv_360_TextAnswers ta
                INNER JOIN sv_Answers a ON ta.AnswerId = a.AnswerId
                INNER JOIN @tblQnIds t ON a.QuestionId = t.QuestionId
                WHERE a.ParticipantId = @ParticipantId;
              DELETE a
                FROM sv_Answers a
                INNER JOIN @tblQnIds t ON a.QuestionId = t.QuestionId
                WHERE a.ParticipantId = @ParticipantId;";
              cmd.Parameters.Clear();
              cmd.Parameters.AddInt("@ParticipantId", partInfo.PartId);
              int rowsDeleted = cmd.ExecuteNonQuery();

              // Add answers from form.
              foreach (var qnInfo in surveyQuestionsToUpdate) {
                // Save answer record.
                cmd.CommandText = "INSERT INTO sv_Answers (ParticipantId, QuestionId, Answer, CodeId) "
                  + "OUTPUT INSERTED.AnswerId "
                  + "VALUES (@ParticipantId, @QuestionId, @Answer, @CodeId)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddInt("@ParticipantId", partInfo.PartId);
                cmd.Parameters.AddInt("@QuestionId", qnInfo.QuestionId);
                cmd.Parameters.AddInt("@Answer", qnInfo.AnswerCode); // null if skipped or invalid code
                cmd.Parameters.AddInt("@CodeId", qnInfo.AnswerCodeId); // ditto

                // Add answer.
                var newAnswerId = (int)cmd.ExecuteScalar();

                // Save textanswer record if required.
                if (!qnInfo.AnswerText.IsNullOrEmpty()) {
                  cmd.CommandText = "INSERT INTO sv_360_TextAnswers (ta_questionid, ta_replyid, AnswerId, ta_textanswer) "
                    + "VALUES (@QuestionId, 0, @AnswerId, @TextAnswer)";
                  cmd.Parameters.Clear();
                  cmd.Parameters.AddInt("@QuestionId", qnInfo.QuestionId);
                  cmd.Parameters.AddInt("@AnswerId", newAnswerId);
                  cmd.Parameters.AddText("@TextAnswer", qnInfo.AnswerText);
                  cmd.ExecuteNonQuery();
                }
                // Save multiple answers if any.
                if (qnInfo.AnswersMulti != null && qnInfo.AnswersMulti.Count > 0) {
                  foreach (var multiAnswer in qnInfo.AnswersMulti) {
                    multiAnswer.AnswerId = newAnswerId;
                    cmd.CommandText
                      = "INSERT INTO sv_AnswersMulti (AnswerId, ParticipantId, CodeId, AnsMultiValue) "
                      + "VALUES (@AnswerId, @ParticipantId, @CodeId, @AnsMultiValue)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddInt("@AnswerId", newAnswerId);
                    cmd.Parameters.AddInt("@ParticipantId", partInfo.PartId);
                    cmd.Parameters.AddInt("@CodeId", multiAnswer.AnswerCodeId); // ditto
                    cmd.Parameters.AddInt("@AnsMultiValue", multiAnswer.AnswerCode); // null if skipped or invalid code
                    cmd.ExecuteNonQuery();
                  }
                }
              }
            }
            trans.Commit();
          }
        }
      }

      public static string ParticipantAICoachSummary(int latest360PartId) {

        return GetScalarQuery($@"
          SELECT ais.AICoachSummaryText
          FROM sv_ParticipantAICoachSummary ais
          WHERE ais.ParticipantId = @ParticipantId",
          NewSqlParameter("ParticipantId", latest360PartId)
        ) as string;
      }

      public class PreviousCompletedInfo {
        public int LatestPartId { get; private set; }
        public int LatestSurveyId { get; private set; }
        public int LatestIntakeCode { get; private set; }
        public DateTime LatestCompleted { get; private set; }
        public int? PreviousPartId { get; private set; }
        public int? PreviousSurveyId { get; private set; }
        public int? PreviousIntakeCode { get; private set; }
        public DateTime? PreviousCompleted { get; private set; }
        public PreviousCompletedInfo(
          int latestPartId, int latestSurveyId, int latestIntakeCode, DateTime latestCompleted,
          int? previousPartId, int? previousSurveyId, int? previousIntakeCode, DateTime? previousCompleted
        ) {
          this.LatestPartId = latestPartId;
          this.LatestSurveyId = latestSurveyId;
          this.LatestIntakeCode = latestIntakeCode;
          this.LatestCompleted = latestCompleted;
          this.PreviousPartId = previousPartId;
          this.PreviousSurveyId = previousSurveyId;
          this.PreviousIntakeCode = previousIntakeCode;
          this.PreviousCompleted = previousCompleted;
        }
      }

      public class AddParticipantToSurveyInfo {
        public int NewPartId { get; private set; }
        public string NewPartUID { get; private set; }
        public int SurveyId { get; private set; }
        public string SurveyUID { get; private set; }
        public int? CoacheeId { get; private set; }
        public int? UserId { get; private set; }
        public int? ProgramJobId { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string FullName { get; private set; }
        public string EmailAddr { get; private set; }
        public string MobilePhone { get; private set; }
        public int IntakeCodeId { get; private set; }
        public int IntakeCode { get; private set; }
        public int? RaterForParticipantId { get; private set; }

        protected AddParticipantToSurveyInfo() { } // unused, needed for inheritance.

        public AddParticipantToSurveyInfo(
          int surveyId, string surveyUID, DbHelper.AnswerTypes.IntakeInfo intake, AlbertCoachees.AlbertCoacheeInfo coachee
        ) {
          SetProps(null, intake, coachee);
          this.SurveyId = surveyId;
          this.SurveyUID = surveyUID;
        }

        public AddParticipantToSurveyInfo(
          AlbertSurveys.SurveyInfo survey, DbHelper.AnswerTypes.IntakeInfo intake, AlbertCoachees.AlbertCoacheeInfo coachee
        ) {
          SetProps(survey, intake, coachee);
        }

        public AddParticipantToSurveyInfo(
          AlbertSurveys.NewSurveyIdInfo surveyIdInfo, AlbertCoachees.AlbertCoacheeInfo coachee
        ) {
          SetProps(
            surveyId: surveyIdInfo.SurveyId,
            surveyUID: surveyIdInfo.SurveyUID,
            coacheeId: coachee.CoacheeId,
            firstName: coachee.FirstName,
            lastName: coachee.LastName,
            fullName: coachee.GetFullName(),
            emailAddress: coachee.EmailAddress,
            mobilePhone: coachee.MobilePhone,
            userId: coachee.UserId,
            programJobId: coachee.ProgramJobId,
            intakeCodeId: surveyIdInfo.IntakeCodeId,
            intakeCode: surveyIdInfo.IntakeCode,
            raterForParticipantId: null
          );
        }

        public AddParticipantToSurveyInfo(
          AlbertSurveys.NewSurveyIdInfo surveyIdInfo, AbleUser.AbleUserInfo userInfo
        ) {
          SetProps(
            surveyId: surveyIdInfo.SurveyId,
            surveyUID: surveyIdInfo.SurveyUID,
            coacheeId: userInfo.LatestCoacheeInfo.CoacheeId,
            firstName: userInfo.FirstName,
            lastName: userInfo.LastName,
            fullName: userInfo.GetFullName(),
            emailAddress: userInfo.EmailAddress,
            mobilePhone: userInfo.MobileNumber,
            userId: userInfo.UserId,
            programJobId: null,
            intakeCodeId: surveyIdInfo.IntakeCodeId,
            intakeCode: surveyIdInfo.IntakeCode,
            raterForParticipantId: null
          );
        }

        public AddParticipantToSurveyInfo(
          AlbertSurveys.SurveyInfo survey, DbHelper.AnswerTypes.IntakeInfo intake, DbHelper.ProjectUserAccess.ProjectAccessInfo projectAccess, int? programJobId
        ) {
          SetProps(
            surveyId: survey.SurveyId,
            surveyUID: survey.SurveyUID,
            coacheeId: null,
            firstName: projectAccess.FirstName,
            lastName: projectAccess.LastName,
            fullName: projectAccess.FirstName + " " + projectAccess.LastName,
            emailAddress: projectAccess.Email,
            mobilePhone: projectAccess.Mobile,
            userId: projectAccess.UserId,
            programJobId: programJobId,
            intakeCodeId: intake.CodeId,
            intakeCode: intake.Code,
            raterForParticipantId: null
          );
        }

        protected void SetProps(AlbertSurveys.SurveyInfo survey, DbHelper.AnswerTypes.IntakeInfo intake, AlbertCoachees.AlbertCoacheeInfo coachee) {
          SetProps(
            surveyId: survey?.SurveyId ?? 0,
            surveyUID: survey?.SurveyUID,
            coacheeId: coachee.CoacheeId,
            firstName: coachee.FirstName,
            lastName: coachee.LastName,
            fullName: coachee.GetFullName(),
            emailAddress: coachee.EmailAddress,
            mobilePhone: coachee.MobilePhone,
            userId: coachee.UserId,
            programJobId: coachee.ProgramJobId,
            intakeCodeId: intake.CodeId,
            intakeCode: intake.Code,
            raterForParticipantId: null
          );
        }

        protected void SetProps(
          int surveyId, string surveyUID,
          int? coacheeId, string firstName, string lastName, string fullName,
          string emailAddress, string mobilePhone,
          int? userId, int? programJobId,
          int intakeCodeId, int intakeCode,
          int? raterForParticipantId
        ) {
          this.SurveyId = surveyId;
          this.SurveyUID = surveyUID;
          this.CoacheeId = coacheeId;
          this.FirstName = firstName;
          this.LastName = lastName;
          this.FullName = fullName;
          this.EmailAddr = emailAddress;
          this.MobilePhone = mobilePhone;
          this.UserId = userId;
          this.ProgramJobId = programJobId;
          this.IntakeCodeId = intakeCodeId;
          this.IntakeCode = intakeCode;
          this.RaterForParticipantId = raterForParticipantId;
        }
        internal void SetNewPartIds(int newPartId, string newPartUID) {
          this.NewPartId = newPartId;
          this.NewPartUID = newPartUID;
        }
      }

      public class AddRaterToSurveyInfo : AddParticipantToSurveyInfo {

        public AddRaterToSurveyInfo(
          AlbertSurveys.SurveyInfo survey,
          Participants.ParticipantInfo selfParticipant,
          DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee,
          string firstName, string lastName, string emailAddress
        ) {
          SetProps(survey.SurveyId, survey.SurveyUID, selfParticipant.CoacheeId,
            firstName, lastName, (firstName + " " + lastName).Trim(), emailAddress, "",
            coachee?.UserId ?? selfParticipant.UserId,
            coachee?.ProgramJobId ?? selfParticipant.ProgramJobId,
            selfParticipant.IntakeCodeId, selfParticipant.IntakeCode, selfParticipant.PartId);
        }
      }

      public static void AddRaterToSurvey(AddRaterToSurveyInfo addRaterInfo) {
        if (addRaterInfo.RaterForParticipantId == null) throw new ArgumentException("addRaterInfo.RaterForParticipantId cannot be null.");
        AddParticipantToSurvey(addRaterInfo);
      }

      public static void AddParticipantToSurvey(SqlTransaction trans, AddParticipantToSurveyInfo addPartInfo) {
        if (trans == null) throw new ArgumentNullException(nameof(trans), "Transaction required.");
        AddParticipantToSurvey(trans.Connection, trans, addPartInfo);
      }

      public static void AddParticipantToSurvey(AddParticipantToSurveyInfo addPartInfo) {
        using (var conn = GetNewOpenConnection()) {
          try {
            AddParticipantToSurvey(conn, null, addPartInfo);
          } catch (Exception e) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(e)
              .WithOperation(nameof(AddParticipantToSurvey))
              .WithProperty(DalApplicationInsightsConstants.SurveyId, addPartInfo?.SurveyId)
              .WithProperty(DalApplicationInsightsConstants.CoacheeId, addPartInfo?.CoacheeId)
              .WithProperty(DalApplicationInsightsConstants.UserId, addPartInfo?.UserId)
              .WithProperty(DalApplicationInsightsConstants.ProgramJobId, addPartInfo?.ProgramJobId)
              .WithProperty(DalApplicationInsightsConstants.EmailAddress, addPartInfo?.EmailAddr)
              .WithProperty(DalApplicationInsightsConstants.IntakeCode, addPartInfo?.IntakeCode)
              .WithProperty(DalApplicationInsightsConstants.RaterForParticipantId, addPartInfo?.RaterForParticipantId)
              .Track();
            throw;
          }
        }
      }

      public static void AddParticipantToSurvey(SqlConnection conn, SqlTransaction trans, AddParticipantToSurveyInfo addPartInfo) {

        // Adding a participant to a survey.
        // If raterForParticipantId not null then we're adding a rater for that participant.

        string newPartUID = "";
        int newPartId = 0, tries;
        bool success = false;

        using (var cmd = new SqlCommand(@"
          INSERT INTO " + DbTable.Participants + @"
            (SurveyId, UniqueId, IsSelf, Self_PartId, FirstName, LastName, Name, Email, Phone, IntakeCodeId, DateGroupCode, FeedbackCode, QuestionSet, AbleCoacheeId, UserId, AbleProgramJobId)
          OUTPUT INSERTED." + DbTable.Participants.IdentityColumn + @"
          VALUES (@SurveyId, @UniqueId, @IsSelf, @Self_PartId, @FirstName, @LastName, @FullName, @Email, @Phone, @IntakeCodeId, @DateGroupCode, @FeedbackCode, @QuestionSet, @AbleCoacheeId, @UserId, @ProgramJobId)
          ", conn, trans)) {
          cmd.Parameters.AddInt("@SurveyId", addPartInfo.SurveyId);
          cmd.Parameters.AddInt("@Self_PartId", addPartInfo.RaterForParticipantId);
          cmd.Parameters.AddInt("@IsSelf", addPartInfo.RaterForParticipantId == null ? 1 : 0);
          cmd.Parameters.AddVarChar("@FirstName", 50, addPartInfo.FirstName.EmptyIfNull());
          cmd.Parameters.AddVarChar("@LastName", 50, addPartInfo.LastName.EmptyIfNull());
          cmd.Parameters.AddVarChar("@FullName", 50, addPartInfo.FullName);
          cmd.Parameters.AddVarChar("@Email", 50, addPartInfo.EmailAddr);
          cmd.Parameters.AddVarChar("@Phone", 50, addPartInfo.MobilePhone.EmptyIfNull()); // optional
          cmd.Parameters.AddInt("@IntakeCodeId", addPartInfo.IntakeCodeId);
          cmd.Parameters.AddInt("@DateGroupCode", addPartInfo.IntakeCode);
          cmd.Parameters.AddInt("@FeedbackCode", FeedbackCodes.Self);
          cmd.Parameters.AddInt("@QuestionSet", 1);
          cmd.Parameters.AddInt("@AbleCoacheeId", addPartInfo.CoacheeId);
          cmd.Parameters.AddInt("@UserId", addPartInfo.UserId);
          cmd.Parameters.AddInt("@ProgramJobId", addPartInfo.ProgramJobId);
          // Loop until we find a unique id string, or run out of attempts.
          var paramUniqueId = cmd.Parameters.AddVarChar("@UniqueId", UniqueIdMaxLength, newPartUID);
          for (tries = 0; tries < UniqueId_Assign_MaxTries; tries++) {
            newPartUID = GetRandomUniqueId();
            paramUniqueId.Value = newPartUID; // Set new value of sql parameter to try inserting.
            try {
              newPartId = (int)cmd.ExecuteScalar();
              success = true;
              break;
            } catch (Exception e) {
              if (!e.Message.ToLower().Contains("unique")) { // Not a "violation of unique key" error, must be something serious.
                var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
                telemetry?.Exception(e)
                  .WithOperation(nameof(AddParticipantToSurvey))
                  .WithOperationContext("AssignUniqueId")
                  .WithProperty(DalApplicationInsightsConstants.SurveyId, addPartInfo?.SurveyId)
                  .WithProperty(DalApplicationInsightsConstants.CoacheeId, addPartInfo?.CoacheeId)
                  .WithProperty(DalApplicationInsightsConstants.Tries, tries)
                  .WithProperty(DalApplicationInsightsConstants.UniqueId, newPartUID)
                  .Track();
                throw new ApplicationException("AddParticipant() Error.", e);
              }
            }
          }
        } // cmd
        if (!success) {
          // Shouldn't happen. If it does, we may need to increase attempts or use a different method of creating id strings.
          throw new ApplicationException("Maxed out UniqueId attemps.");
        }
        // Return new ids in the object.
        addPartInfo.SetNewPartIds(newPartId, newPartUID);
      }

      public static void UpdateFirstInvitationSent(int partId, DateTime firstInvitationSent) {
        GetNonQueryInt(null, @"
          UPDATE sv_360_Participants SET
            FirstInvitationSent = @FirstInvitationSent
          WHERE PartId = @PartId",
          NewSqlParameter("PartId", partId),
          NewSqlParameter("FirstInvitationSent", firstInvitationSent)
        );
      }

      // Update LastInvitationSent, and FirstInvitationSent if it hasn't already been set.
      public static void UpdateLastInvitationSent(int partId, DateTime lastInvitationSent) {
        GetNonQueryInt(null, @"
          UPDATE sv_360_Participants SET
            LastInvitationSent = @LastInvitationSent,
            FirstInvitationSent = IIF(FirstInvitationSent IS NULL, @LastInvitationSent, FirstInvitationSent)
          WHERE PartId = @PartId",
          NewSqlParameter("PartId", partId),
          NewSqlParameter("LastInvitationSent", lastInvitationSent)
        );
      }

      public static void UpdateCompletedUtc(int participantId, DateTime? completedUtc) {
        GetNonQueryInt(@"
          UPDATE sv_360_Participants
          SET Completed = @CompletedUtc
          WHERE PartId = @PartId",
          NewSqlParameter("PartId", participantId),
          NewSqlParameter("CompletedUtc", completedUtc)
        );
      }

      public static void UpdateSurveyValidatedUpToPageNumber(ParticipantInfo partInfo, int surveyValidatedUpToPageNumber) {
        GetNonQueryInt(@"
          UPDATE sv_360_Participants
          SET PiSaved = @SurveyValidatedUpToPageNumber
          WHERE PartId = @PartId",
          NewSqlParameter("PartId", partInfo.PartId),
          NewSqlParameter("SurveyValidatedUpToPageNumber", surveyValidatedUpToPageNumber)
        );
        partInfo.SurveyValidatedUpToPageNumber = surveyValidatedUpToPageNumber;
      }

      public static void UpdatePercentCompleted(int participantId, int percent0To100) {
        if (percent0To100 < 0 || percent0To100 > 100) throw new ArgumentException($"Percent out of range ({percent0To100})");
        GetNonQueryInt(@"
          UPDATE sv_360_Participants
          SET PercentCompleted = @PercentCompleted
          WHERE PartId = @PartId",
          NewSqlParameter("PartId", participantId),
          NewSqlParameter("PercentCompleted", percent0To100)
        );
      }

      public static void UpdateSurveyLastUpdatedUtc(ParticipantInfo partInfo, DateTime? surveyLastUpdatedUtc) {
        GetNonQueryInt(@"
          UPDATE sv_360_Participants
          SET SurveyLastUpdatedUtc = @SurveyLastUpdatedUtc
          WHERE PartId = @PartId",
          NewSqlParameter("PartId", partInfo.PartId),
          NewSqlParameter("SurveyLastUpdatedUtc", surveyLastUpdatedUtc)
        );
        partInfo.SurveyLastUpdatedUtc = surveyLastUpdatedUtc;
      }

      public class PastRaterInfo {
        public string Name { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public PastRaterInfo(string name, string firstName, string lastName, string email) {
          this.Name = name;
          this.FirstName = firstName;
          this.LastName = lastName;
          this.Email = email;
        }
      }

      // Structures.

      public static class FeedbackCodes {
        public const int Self = 0;
        public const int Rater = 1;
      }

      public class ParticipantList {
        public int SurveyId { get; private set; }
        public int IntakeCode { get; private set; }
        public List<ParticipantInfo> Participants { get; private set; }
        public ParticipantList(int surveyId, int intakeCode) {
          this.SurveyId = surveyId;
          this.IntakeCode = intakeCode;
          Participants = new List<ParticipantInfo>();
        }
      }

      public class FoundParticipantBrief {

        public AlbertSurveys.SurveyInfo SurveyInfo { get; private set; }
        public int PartId { get; private set; }
        public string PartUniqueId { get; private set; }
        public int? CoacheeId { get; private set; }
        public int? CoachUserId { get; private set; }
        public int? UserId { get; private set; }
        public Guid? UserGuid { get; private set; }
        public int? ProgramJobId { get; private set; }
        public DateTime? CompletedUtc { get; private set; }
        public int RatersInvited { get; private set; }
        public int RatersCompleted { get; private set; }
        public DateTime? ReportSentUtc { get; private set; }
        public DateTime? AI_ViewResultsCheckUtc { get; private set; }
        public bool CanLeaderView360Report { get; private set; }
        public DateTime? PersonalSurveyClosedUtc { get; private set; }

        internal FoundParticipantBrief(
          AlbertSurveys.SurveyInfo surveyInfo,
          int partId,
          string partUniqueId,
          int? coacheeId,
          int? coachUserId,
          int? userId,
          Guid? userGuid,
          int? programJobId,
          DateTime? completedUtc,
          int ratersInvited,
          int ratersCompleted,
          DateTime? reportSentUtc,
          DateTime? aiViewResultsCheckUtc,
          bool canLeaderView360Report,
          DateTime? personalSurveyClosedUtc) {

          this.SurveyInfo = surveyInfo;
          this.PartId = partId;
          this.PartUniqueId = partUniqueId;
          this.CoacheeId = coacheeId;
          this.CoachUserId = coachUserId;
          this.UserId = userId;
          this.UserGuid = userGuid;
          this.ProgramJobId = programJobId;
          this.CompletedUtc = completedUtc;
          this.RatersInvited = ratersInvited;
          this.RatersCompleted = ratersCompleted;
          this.ReportSentUtc = reportSentUtc;
          this.AI_ViewResultsCheckUtc = aiViewResultsCheckUtc;
          this.CanLeaderView360Report = canLeaderView360Report;
          this.PersonalSurveyClosedUtc = personalSurveyClosedUtc;
        }

        public bool IsSurveyAvailable => !SurveyInfo.IsScheduledInFuture && !SurveyInfo.IsClosed && !IsValidStateForReport;

        public bool IsSelfCompleted => CompletedUtc != null;

        public bool IsEnoughRatersCompletedForReport => SurveyInfo.IsSelfOnly || RatersCompleted >= SurveyInfo.MinimumRatersRequiredForReport;

        public bool IsValidStateForReport {
          // Whether survey itself is in a valid state to produce a report.
          // Note this is separate from whether a user is *allowed* to view a report - see AppAccess for that.
          get {
            if (SurveyInfo.ReportType == null) return false;
            if (SurveyInfo.IsSelfOnly || (IsSelfCompleted && SurveyInfo.AllowReportWithoutRaters)) return IsSelfCompleted; // Self-only can run report as soon as self is completed.
            if (SurveyInfo.IsRatersOnly) return SurveyInfo.IsClosed && IsEnoughRatersCompletedForReport; // Only raters needed and survey must be closed.
            return SurveyInfo.IsClosed && IsSelfCompleted && IsEnoughRatersCompletedForReport; // Otherwise needs both self and raters complete and survey must be closed.
          }
        }

        public bool IsReportAvailable_Online =>
          IsValidStateForReport
          && SurveyInfo.ReportType.HasCoacheeOnlineReport
          && SurveyInfo.SurveyType != AlbertSurveys.SurveyTypeEnum.Intake
          && SurveyInfo.SurveyType != AlbertSurveys.SurveyTypeEnum.DevelopmentPlan;

        public bool IsReportAvailable_PDF =>
          !SurveyInfo.DisablePDF
          && IsValidStateForReport
          && !SurveyInfo.ReportType.ReportPathJarvisFolder.IsNullOrEmpty()
          && SurveyInfo.SurveyType != AlbertSurveys.SurveyTypeEnum.Pulse360
          && SurveyInfo.FeedbackOption != AlbertSurveys.FeedbackOptionEnum.NoRaters;
      }

      public class ParticipantInfo {

        public int PartId;
        public string PartUID;
        public int SurveyId;
        public int? PersonId;
        public string SurveyUID;
        public int? CoacheeId;
        public int? UserId;
        public int? ProgramJobId;
        public string JobNumber;
        public int? SelfPartId;
        public string SelfPartUID;
        public string SelfName;
        public string FirstName;
        public string LastName;
        public string FullName;
        public string Email;
        public string Phone;
        public bool IsSelf;
        public bool IsSelfManager;
        public int IntakeCodeId;
        public int IntakeCode;
        public int QuestionSet;
        public DateTime CreatedUTC;
        public DateTime? LastReminderUTC;
        public DateTime? SurveyLastUpdatedUtc;
        public DateTime? CompletedUTC;
        public DateTime? DeclinedUTC;
        public DateTime? InvitationBouncedUTC;
        public bool IsBounceAutoReply;
        public bool IsSelfCompleted;
        public int RatersNotDeclined;
        public int RatersCompleted;
        public DateTime? ReportSentUtc { get; private set; }
        public DateTime? AI_ViewResultsCheckUtc { get; private set; }
        public int? SurveyValidatedUpToPageNumber { get; internal set; }

        public ParticipantInfo(
          int partId, string partUID, int surveyId, string surveyUID,
          int? coacheeId, int? userId, int? programJobId, string jobNumber,
          int? personId, int? selfPartId, string selfPartUID,
          string firstName, string lastName, string fullName, string email, string phone,
          bool isSelf, bool isSelfManager, int intakeCodeId, int intakeCode, int questionSet,
          DateTime createdUTC, DateTime? lastReminder, DateTime? surveyLastUpdatedUtc, DateTime? completedUTC, DateTime? declinedUTC,
          DateTime? invitationBouncedUTC, bool isBounceAutoReply, DateTime? reportSentUtc,
          string selfName, int ratersNotDeclined, int ratersCompleted, DateTime? aiViewResultsCheckUtc,
          int? surveyValidatedUpToPageNumber
        ) {
          this.PartId = partId;
          this.PartUID = partUID;
          this.SurveyId = surveyId;
          this.SurveyUID = surveyUID;
          this.CoacheeId = coacheeId;
          this.UserId = userId;
          this.ProgramJobId = programJobId;
          this.JobNumber = jobNumber;
          this.PersonId = personId;
          this.SelfPartId = selfPartId;
          this.SelfPartUID = selfPartUID;
          this.SelfName = selfName.EmptyIfNull();
          this.FirstName = firstName;
          this.LastName = lastName;
          this.FullName = fullName;
          this.Email = email;
          this.Phone = phone;
          this.IsSelf = isSelf;
          this.IsSelfManager = isSelfManager;
          this.IntakeCodeId = intakeCodeId;
          this.IntakeCode = intakeCode;
          this.QuestionSet = questionSet;
          this.CreatedUTC = createdUTC;
          this.LastReminderUTC = lastReminder;
          this.SurveyLastUpdatedUtc = surveyLastUpdatedUtc;
          this.CompletedUTC = completedUTC;
          this.DeclinedUTC = declinedUTC;
          this.InvitationBouncedUTC = invitationBouncedUTC;
          this.IsBounceAutoReply = isBounceAutoReply;
          this.RatersNotDeclined = ratersNotDeclined;
          this.RatersCompleted = ratersCompleted;
          this.IsSelfCompleted = CompletedUTC == null ? false : true;
          this.ReportSentUtc = reportSentUtc;
          this.AI_ViewResultsCheckUtc = aiViewResultsCheckUtc;
          this.SurveyValidatedUpToPageNumber = surveyValidatedUpToPageNumber;
        }

      }
    }
  }
}
