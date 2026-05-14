using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;

namespace Integral.Web {
  public partial class DbHelper : HelperBase<DbHelper> {
    public partial class SurveyShare {
      private const string TblPfx = "ss";
      public enum SharedSurveyTypeEnum { All, SharedBy, SharedWith }

      public static bool UnshareSurvey(int surveyShareId) {

        return Common.GetNonQueryInt($@"
          DELETE FROM sv_SurveyShare WHERE SurveyShareId = @SurveyShareId",
          Common.NewSqlParameter("SurveyShareId", surveyShareId)
        ) > 0;
      }

      public static bool AddSurveyShare(int? shareWithUserId, int partId) {

        if (shareWithUserId == null) {
          return false;
        }

        return Common.GetScalarQueryInt(null, @"
          INSERT INTO sv_SurveyShare (UserId, ParticipantId, SharedUtc)
          OUTPUT INSERTED.SurveyShareId
          VALUES (@UserId, @PartId, @SharedUtc)",
          Common.NewSqlParameter("@UserId", shareWithUserId),
          Common.NewSqlParameter("@PartId", partId),
          Common.NewSqlParameter("@SharedUtc", DateTime.UtcNow)
        ) > 0;
      }

      public static bool IsSurveyAlreadySharedWithUserId(int ParticipantId, int userId) {
        int? surveyShareId = null;
        Common.Query(
          $"SELECT {TblPfx}.SurveyShareId FROM sv_SurveyShare {TblPfx} WHERE {TblPfx}.UserId = @UserId AND {TblPfx}.ParticipantId = @ParticipantId",
          dr => {
            surveyShareId = dr.GetIntOrNull("SurveyShareId");
          },
          Common.NewSqlParameter("UserId", userId),
          Common.NewSqlParameter("ParticipantId", ParticipantId)
        );
        return surveyShareId != null;
      }

      public static List<SharedSurveysInfo> GetSharedSurveysForUser(int userId, SharedSurveyTypeEnum sharedSurveyType) {
        string sqlWhere = "";
        if (sharedSurveyType == SharedSurveyTypeEnum.All) {
          sqlWhere = "(usb.UserId = @UserId OR usw.UserId = @UserId)";
        } else if (sharedSurveyType == SharedSurveyTypeEnum.SharedBy) {
          sqlWhere = "usb.UserId = @UserId";
        } else if (sharedSurveyType == SharedSurveyTypeEnum.SharedWith) {
          sqlWhere = "usw.UserId = @UserId";
        }

        return GetSharedSurveysInfo(null, "", sqlWhere, "", Common.NewSqlParameter("@UserId", userId));
      }

      public static SharedSurveysInfo GetSharedSurveyInfo(int surveyShareId) {
        return GetSharedSurveysInfo(1, "", $"{TblPfx}.SurveyShareId = @SurveyShareId", "", Common.NewSqlParameter("@SurveyShareId", surveyShareId)).FirstOrDefault();
      }

      private static List<SharedSurveysInfo> GetSharedSurveysInfo(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams) {

        var sharedSurveyInfo = new List<SharedSurveysInfo>();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);
        string sql = $@"
          WITH SurveyInfo AS (
            SELECT
              s.sv_id, s.sv_uniqueid, s.sv_ReportType, s.SurveyTypeCode, s.ShowSurveyLoggedIn, s.IsAlbertSurvey,
              s.sv_title, s.sv_360GroupName, s.sv_createdUTC, s.ClonedFromSvId, s.GroupReportAvailable, s.sv_FeedbackOption,
              prj.ProgramJobId, prj.JobNumber, prj.FriendlyProjectTitle, prj.ProjectName,
              dc.CodeId AS IntakeCodeId, dc.Code AS IntakeNumber, dc.Value AS IntakeName,
              p.PartId, p.UniqueId AS PartUniqueId, p.UserId AS PartUserId, p.Completed, p.PersonalSurveyClosedUtc, p.AbleCoacheeId,
              ta.GoalText, dc.IntakeCloseDateSelf
            FROM sv_survey s WITH(NOLOCK)
            LEFT OUTER JOIN al_SurveyType st ON st.SurveyTypeCode = s.SurveyTypeCode
            LEFT OUTER JOIN sv_Organisation org ON org.OrgId = s.sv_OrgId
            INNER JOIN sv_360_AnswerTypes dt ON s.sv_id = dt.SurveyId
            INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId
            LEFT OUTER JOIN sv_360_Participants p ON p.IntakeCodeId = dc.CodeId
            OUTER APPLY (
              SELECT TOP 1
                j.JobId AS ProgramJobId,
                prj.JobNumber, prj.FriendlyProjectTitle, prj.ProjectName
              FROM al_Project prj WITH(NOLOCK)
              INNER JOIN id_Job j ON prj.JobNumber = j.JobNumber
              INNER JOIN sv_JoinJobsAndIntakes ji ON j.JobId = ji.JobId
              WHERE ji.DateCodeId = dc.CodeId
            ) AS prj
            OUTER APPLY (
            -- First non-blank Goal question response text.
            SELECT TOP 1 ta.ta_textanswer AS GoalText
            FROM sv_360_TextAnswers ta
            INNER JOIN sv_Answers sa ON ta.AnswerId = sa.AnswerId
            INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
            WHERE sa.ParticipantId = p.PartId
            AND sq.GblQuestionId = 4183
            AND REPLACE(ISNULL(CAST(ta.ta_textanswer AS VARCHAR(10)), ''), ' ', '') <> ''
          ) AS ta

            WHERE dt.AnswerTypeDescr = 'date'
          )
          SELECT {sqlTop}
            {TblPfx}.SurveyShareId, {TblPfx}.SharedUtc,
            si.IntakeCodeId,
            usw.UserId AS UserIdSharedWith, usw.Email AS EmailUserSharedWith, usw.FirstName AS FirstNameSharedWith, usw.LastName as LastNameSharedWith,
            usb.UserId AS UserIdSharedBy, usb.Email AS EmailUserSharedBy, usb.FirstName AS FirstNameSharedBy, usb.LastName as LastNameSharedBy,
            si.sv_id, si.sv_uniqueid, si.sv_ReportType, si.SurveyTypeCode, si.ShowSurveyLoggedIn, si.IsAlbertSurvey, si.sv_title, si.sv_360GroupName,
            si.sv_createdUTC, si.ClonedFromSvId, si.GroupReportAvailable, si.sv_FeedbackOption, si.ProgramJobId, si.JobNumber, si.FriendlyProjectTitle,
            si.ProjectName, si.IntakeCodeId, si.IntakeNumber, si.IntakeName, si.PartId, si.PartUniqueId, si.PartUserId, si.Completed, si.GoalText,
            si.PersonalSurveyClosedUtc, si.AbleCoacheeId, si.IntakeCloseDateSelf
          FROM sv_SurveyShare {TblPfx}
          INNER JOIN SurveyInfo si ON si.PartId = {TblPfx}.ParticipantId
          LEFT JOIN sv_User usw ON usw.UserId = {TblPfx}.UserId
          LEFT JOIN sv_User usb ON usb.UserId = si.PartUserId
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        Common.Query(sql,
          dr => {
            sharedSurveyInfo.Add(new SharedSurveysInfo(dr));
          },
          sqlWhereParams
         );

        return sharedSurveyInfo;
      }

      public class SharedSurveysInfo {
        public int SurveyShareId { get; private set; }
        public DateTime? SharedUtc { get; private set; }
        public int? UserIdSharedWith { get; private set; }
        public string EmailSharedWith { get; private set; }
        public string FirstNameSharedWith { get; private set; }
        public string LastNameSharedWith { get; private set; }
        public int? UserIdSharedBy { get; private set; }
        public string EmailSharedBy { get; private set; }
        public string FirstNameSharedBy { get; private set; }
        public string LastNameSharedBy { get; private set; }
        public SurveyInfo SurveyInfo { get; private set; }
        public SharedSurveysInfo(SqlDataReader dr) {
          this.SurveyShareId = dr.GetInt("SurveyShareId");
          this.SharedUtc = dr.GetDateTimeOrNull("SharedUtc");
          this.UserIdSharedWith = dr.GetIntOrNull("UserIdSharedWith");
          this.EmailSharedWith = dr.GetString("EmailUserSharedWith");
          this.FirstNameSharedWith = dr.GetString("FirstNameSharedWith");
          this.LastNameSharedWith = dr.GetString("LastNameSharedWith");
          this.UserIdSharedBy = dr.GetIntOrNull("UserIdSharedBy");
          this.EmailSharedBy = dr.GetString("EmailUserSharedBy");
          this.FirstNameSharedBy = dr.GetString("FirstNameSharedBy");
          this.LastNameSharedBy = dr.GetString("LastNameSharedBy");
          this.SurveyInfo = new SurveyInfo(dr);
        }

        public string GetSharedWithFullName() => $"{FirstNameSharedWith} {LastNameSharedWith}";
        public string GetSharedByFullName() => $"{FirstNameSharedBy} {LastNameSharedBy}";
      }

      public class SurveyInfo {
        public int SurveyId { get; private set; }
        public string SurveyUID { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public string FriendlyProjectTitle { get; private set; }
        public int? ProgramJobId { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public int? ClonedFromSurveyId { get; private set; }
        public string InternalTitle { get; private set; }
        public string SurveyName { get; private set; }
        public string SurveyTypeCode { get; private set; }
        public int IntakeCodeId { get; private set; }
        public int IntakeNumber { get; private set; }
        public ReportTypes.ReportTypeInfo ReportType { get; private set; }
        public AlbertSurveys.FeedbackOptionEnum FeedbackOption { get; private set; }
        public bool IsAbleSurvey { get; private set; }
        public bool ShowSurveyLoggedIn { get; private set; }
        public bool GroupReportAvailable { get; private set; }
        public int PartId { get; private set; }
        public string PartUniqueId { get; private set; }
        public DateTime? CompletedUtc { get; private set; }
        public string GoalText { get; private set; }
        public DateTime? PersonalSurveyClosedUtc { get; private set; }
        public int? CoacheeId { get; private set; }
        public DateTime? IntakeCloseDateSelf { get; private set; }

        public SurveyInfo(SqlDataReader dr) {
          this.SurveyId = dr.GetInt("sv_id");
          this.SurveyUID = dr.GetString("sv_uniqueid");
          this.ProgramJobNumber = dr.GetString("JobNumber");
          this.FriendlyProjectTitle = dr.GetString("FriendlyProjectTitle").ValueIfNullOrEmpty(dr.GetString("ProjectName"));
          this.ProgramJobId = dr.GetIntOrNull("ProgramJobId");
          this.CreatedUtc = dr.GetDateTime("sv_createdUTC");
          this.ClonedFromSurveyId = dr.GetIntOrNull("ClonedFromSvId");
          this.InternalTitle = dr.GetString("sv_title");
          this.SurveyName = !dr.GetString("sv_360GroupName").IsNullOrEmpty() ? dr.GetString("sv_360GroupName") : dr.GetString("sv_title");
          this.IntakeCodeId = dr.GetInt("IntakeCodeId");
          this.IntakeNumber = dr.GetInt("IntakeNumber");
          this.ReportType = ReportTypes.GetReportTypeByDbValue(dr.GetString("sv_ReportType"));
          this.FeedbackOption = (AlbertSurveys.FeedbackOptionEnum)dr.GetInt("sv_FeedbackOption");
          this.GroupReportAvailable = dr.GetBoolFromInt("GroupReportAvailable");
          this.SurveyTypeCode = dr.GetString("SurveyTypeCode");
          this.IsAbleSurvey = dr.GetBoolFromInt("IsAlbertSurvey");
          this.ShowSurveyLoggedIn = dr.GetBoolFromInt("ShowSurveyLoggedIn");
          this.PartId = dr.GetInt("PartId");
          this.PartUniqueId = dr.GetString("PartUniqueId");
          this.CompletedUtc = dr.GetDateTimeOrNull("Completed");
          this.GoalText = dr.GetString("GoalText");
          this.PersonalSurveyClosedUtc = dr.GetDateTimeOrNull("PersonalSurveyClosedUtc");
          this.CoacheeId = dr.GetIntOrNull("AbleCoacheeId");
          this.IntakeCloseDateSelf = dr.GetDateTimeOrNull("IntakeCloseDateSelf");
        }

        public DbHelper.AlbertSurveys.SurveyTypeEnum SurveyType => DbHelper.AlbertSurveys.GetSurveyType(SurveyTypeCode);

      }
    }
  }
}
