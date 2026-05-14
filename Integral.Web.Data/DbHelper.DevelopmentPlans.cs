using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class DevelopmentPlans {

      public enum GetPlansStatus { OnlyClosed, OnlyOpen, Any }

      public static PlanInfo GetPlanSurvey(int surveyId) {

        var planList = GetPlanList(
          "sp.SurveyId = @SurveyId",
          Common.NewSqlParameter("SurveyId", surveyId)
        );

        if (planList.IsNullOrEmpty()) return null;
        return planList[0];
      }

      public static bool DeleteDevPlan(SqlTransaction trans, AlbertSurveys.SurveyInfo surveyInfo) {

        if (surveyInfo?.FoundParticipantBrief == null) return false;

        return Common.GetScalarQueryInt($@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          -- Step 1: Delete from sv_360_TextAnswers for matching AnswerIds in sv_Answers
          DELETE ta
          FROM sv_360_TextAnswers ta
          INNER JOIN sv_Answers a ON ta.AnswerId = a.AnswerId
          INNER JOIN sv_360_Questions q ON a.QuestionId = q.QuestionId
          WHERE q.SurveyId = @SurveyId;

          -- Step 2: Delete from sv_AnswersMulti for matching AnswerIds in sv_Answers
          DELETE m
          FROM sv_AnswersMulti m
          INNER JOIN sv_Answers a ON m.AnswerId = a.AnswerId
          INNER JOIN sv_360_Questions q ON a.QuestionId = q.QuestionId
          WHERE q.SurveyId = @SurveyId;

          -- Step 3: Delete from sv_Answers for matching ParticipantId and QuestionId
          DELETE a
          FROM sv_Answers a
          INNER JOIN sv_360_Questions q ON a.QuestionId = q.QuestionId
          WHERE q.SurveyId = @SurveyId;

          -- Step 4: Delete sv_360_Dimensions
          DELETE sv_360_Dimensions
          WHERE SurveyId = @SurveyId;

          -- Step 5: Delete sv_ReportOrgDivLinks
          DELETE FROM sv_ReportOrgDivLinks
          WHERE OriginSurveyId = @SurveyId;

          -- Step 6: Delete from sv_SurveyShare to avoid empty consults on shared surveys
          DELETE FROM sv_SurveyShare
          WHERE ParticipantId = @PartId

          -- Step 7: Delete from sv_360_Participants if there are no further dependencies
          DELETE sv_360_Participants
          WHERE SurveyId = @SurveyId;

          -- Step 8: Delete from sv_360_Codes to remove dependencies on sv_360_AnswerTypes
          DELETE c
          FROM sv_360_Codes c
          INNER JOIN sv_360_AnswerTypes t ON c.AnswerTypeId = t.AnswerTypeId
          WHERE t.SurveyId = @SurveyId;

          --Step 9: Delete from sv_360_Questions if they are survey-specific
          DELETE sv_360_Questions
          WHERE SurveyId = @SurveyId;

          -- Step 10: Delete from sv_360_AnswerTypes now that dependencies in sv_360_Codes are removed
          DELETE FROM sv_360_AnswerTypes
          WHERE SurveyId = @SurveyId;

          -- Step 11: Delete Scores
          DELETE FROM sv_SurveyGblQnScores
          WHERE SurveyId = @SurveyId;

          DELETE FROM sv_SurveyGblQnScoresByDiv
          WHERE SurveyId = @SurveyId;

          -- Step 12: Delete pages from sv_360_Pages
          DELETE FROM sv_360_Pages
          WHERE SurveyId = @SurveyId

          -- Step 13: Delete from sv_Survey now that there are no dependencies
          DELETE FROM sv_Survey
          OUTPUT DELETED.sv_id
          WHERE sv_id = @SurveyId;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("PartId", surveyInfo.FoundParticipantBrief.PartId),
          Common.NewSqlParameter("SurveyId", surveyInfo.SurveyId)
        ) > 0;
      }

      public static PlanInfo GetPlanSurvey(string surveyUID, string partUID) {

        var planList = GetPlanList(
          "sv.sv_uniqueid = @SurveyUID AND sp.UniqueId = @PartUID",
          Common.NewSqlParameter("SurveyUID", surveyUID),
          Common.NewSqlParameter("PartUID", partUID)
        );

        if (planList.IsNullOrEmpty()) return null;
        return planList[0];
      }

      public static List<PlanInfo> GetPlansForUser(int userId, GetPlansStatus getPlansStatus) {

        return GetPlanList(
          "sp.UserId = @UserId"
          + (getPlansStatus == GetPlansStatus.Any ? "" : $" AND sp.PersonalSurveyClosedUtc IS {(getPlansStatus == GetPlansStatus.OnlyClosed ? "NOT" : "")} NULL"),
          Common.NewSqlParameter("UserId", userId)
        );
      }

      private static List<PlanInfo> GetPlanList(string whereConditionsSQL, params SqlParameter[] whereParams) {

        var sqlParamList = new List<SqlParameter>(whereParams);
        sqlParamList.Add(Common.NewSqlParameter("SurveyTypeCode", ConfigHelper.SurveyTypeCodes.DevPlan));

        var plans = new List<PlanInfo>();

        Common.Query($@"
          SELECT
            sv.sv_id, sv.sv_uniqueid, sv.sv_createdUTC, sv.CreatedByUserId,
            sp.PartId, sp.UniqueId, sp.Completed, sp.UserId, sp.PercentCompleted, sp.PersonalSurveyClosedUtc,
            cu.CoachUserId, cu.CoachFirstName, cu.CoachLastName,
            ta.GoalText,
            pu.UserGuid
          FROM sv_Survey sv
          INNER JOIN sv_360_Participants sp ON sp.SurveyId = sv.sv_id
          LEFT JOIN sv_User pu ON sp.UserId = pu.UserId
          OUTER APPLY (
            -- First non-blank Goal question response text.
            SELECT TOP 1 ta.ta_textanswer AS GoalText
            FROM sv_360_TextAnswers ta
            INNER JOIN sv_Answers sa ON ta.AnswerId = sa.AnswerId
            INNER JOIN sv_360_Questions sq ON sa.QuestionId = sq.QuestionId
            WHERE sa.ParticipantId = sp.PartId
            AND sq.GblQuestionId = 4183
            AND REPLACE(ISNULL(CAST(ta.ta_textanswer AS VARCHAR(10)), ''), ' ', '') <> ''
          ) AS ta
          OUTER APPLY (
            -- Get latest coach if any.
            SELECT TOP 1 cu.UserId AS CoachUserId, cu.FirstName AS CoachFirstName, cu.LastName AS CoachLastName
            FROM al_Coachees ac
            INNER JOIN sv_User cu ON cu.UserId = ac.CoachUserId
            WHERE ac.UserId = sp.UserId
            ORDER BY ac.RowCreatedUtc DESC
          ) AS cu
          WHERE sv.SurveyTypeCode = @SurveyTypeCode
            {whereConditionsSQL.EnsureStartsWith("AND ", true)}
          ORDER BY sv.sv_createdUTC DESC;",
          sqlParamList,
          dr => {
            plans.Add(new PlanInfo(
              userId: dr.GetInt("UserId"),
              surveyId: dr.GetInt("sv_id"),
              surveyUniqueId: dr.GetString("sv_uniqueid"),
              createdUtc: dr.GetDateTime("sv_createdUTC"),
              closedUtc: dr.GetDateTimeOrNull("PersonalSurveyClosedUtc"),
              surveyPartId: dr.GetInt("PartId"),
              surveyPartUniqueId: dr.GetString("UniqueId"),
              surveyPartPercentCompleted: dr.GetIntOrNull("PercentCompleted"),
              surveyPartCompletedUtc: dr.GetDateTimeOrNull("Completed"),
              coachUserId: dr.GetIntOrNull("CoachUserId"),
              coachFirstName: dr.GetString("CoachFirstName"),
              coachLastName: dr.GetString("CoachLastName"),
              goalText: dr.GetString("GoalText"),
              userGuid: dr.GetGuid("UserGuid"),
              createdByUserId: dr.GetInt("CreatedByUserId")
            ));
          }
        );

        return plans;
      }

      public static List<PlanQuestion> GetPlanQuestionsAndAnswers(PlanInfo planInfo) {

        var result = new List<PlanQuestion>();

        // Note this assumes dev plan surveys *always* have a participant attached.
        Common.Query($@"
          SELECT
            sq.QuestionId, sq.GblQuestionId, sq.AutoNumber, sq.IsHeading, sq.QuestionTextFull1,
            sat.InputType,
            ta.ta_textanswer
          FROM sv_360_Questions sq
          INNER JOIN sv_Survey sv ON sq.SurveyId = sv.sv_id
          INNER JOIN sv_360_Participants sp ON sv.sv_id = sp.SurveyId
          LEFT OUTER JOIN sv_360_AnswerTypes sat ON sq.AnswerTypeId = sat.AnswerTypeId
          LEFT OUTER JOIN sv_Answers sa ON sp.PartId = sa.ParticipantId and sq.QuestionId = sa.QuestionId
          LEFT OUTER JOIN sv_360_TextAnswers ta ON ta.AnswerId = sa.AnswerId
          WHERE sv.sv_id = @SurveyId
            AND sp.PartId = @PartId
          ORDER BY sq.SurveyId, sp.PartId, sq.Sort;",
          dr => {
            result.Add(new PlanQuestion(
              questionId: dr.GetInt("QuestionId"),
              gblQuestionId: dr.GetIntOrNull("GblQuestionId"),
              autoNumber: dr.GetInt("AutoNumber"),
              inputType: dr.GetString("InputType"),
              isHeading: dr.GetBoolFromInt("IsHeading"),
              questionText: dr.GetString("QuestionTextFull1"),
              textAnswer: dr.GetString("ta_textanswer")
            ));
          },
          Common.NewSqlParameter("SurveyId", planInfo.SurveyId),
          Common.NewSqlParameter("PartId", planInfo.SurveyPartId)
        );

        return result;
      }

      public static int CreateDevPlanSurvey(AbleUser.AbleUserInfo userInfo) {

        string surveyTitle = $"Development Plan for {userInfo.GetFullName()} {DateTime.UtcNow.ToString("MMM yyyy")}";
        string intakeTitle = $"Development Plan {DateTime.UtcNow.ToString("MMM yyyy")}";

        int devPlanTemplateId = ConfigHelper.TemplateSurveyIds.DevelopmentPlan; // Default template.

        // Check if project has a custom template to use.
        if (userInfo.LatestCoacheeInfo?.JobNumber != null) {
          var projectInfo = Projects.GetProjectInfoOrNull(userInfo.LatestCoacheeInfo.JobNumber);
          if (projectInfo?.DevelopmentPlanTemplateId != null) {
            devPlanTemplateId = projectInfo.DevelopmentPlanTemplateId.Value;
          }
        }

        var devPlanTemplate = AlbertSurveys.GetTemplateInfo(devPlanTemplateId);
        DateTime closeDateUtc = DateTime.UtcNow.AddDays(ConfigHelper.DevelopmentPlanDefaultOpenDays_Self);
        AlbertSurveys.NewSurveyIdInfo newSurveyIdInfo = null;

        Common.UsingTransaction(trans => {
          newSurveyIdInfo = AlbertSurveys.AddSurveyStub(
            trans: trans,
            createdByUserId: userInfo.UserId,
            templateSurvey: devPlanTemplate,
            companyId: null,
            programJobId: null,
            isProgramSurvey: false,
            firstIntakeName: intakeTitle,
            closeDateInCoacheeLocalTime_Self: closeDateUtc,
            closeDateInCoacheeLocalTime_Rater: closeDateUtc,
            scheduledStartDateUTC: null,
            surveyTitleOrNullForDefault: surveyTitle);
          var partInfo = new DbHelper.Participants.AddParticipantToSurveyInfo(newSurveyIdInfo, userInfo);
          Participants.AddParticipantToSurvey(trans.Connection, trans, partInfo);
          return true;
        });

        return newSurveyIdInfo.SurveyId;
      }

      public static int SaveTextAnswer(SqlTransaction trans, int participantId, int questionId, string textAnswer) {

        // Returns the new or updated TextAnswerId.
        return Common.GetScalarQueryInt(trans, $@"

          DECLARE @AnswerId INT = 0;

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          INSERT sv_Answers (ParticipantId, QuestionId)
          SELECT @ParticipantId, @QuestionId
          WHERE NOT EXISTS (SELECT 1 FROM sv_Answers WITH (UPDLOCK, SERIALIZABLE) WHERE ParticipantId = @ParticipantId AND QuestionId = @QuestionId);

          SET @AnswerId = (SELECT AnswerId FROM sv_Answers WHERE ParticipantId = @ParticipantId AND QuestionId = @QuestionId);

          INSERT sv_360_TextAnswers (AnswerId, ta_questionid, ta_textanswer)
          SELECT @AnswerId, @QuestionId, @TextAnswer
          WHERE NOT EXISTS (SELECT 1 FROM sv_360_TextAnswers WHERE AnswerId = @AnswerId);
          IF @@ROWCOUNT = 0 BEGIN
            UPDATE sv_360_TextAnswers
              SET ta_textanswer = @TextAnswer
            OUTPUT INSERTED.TextAnswerId
            WHERE AnswerId = @AnswerId
          END ELSE BEGIN
            SELECT @@identity
          END

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("ParticipantId", participantId),
          Common.NewSqlParameter("QuestionId", questionId),
          Common.NewSqlParameter("TextAnswer", textAnswer)
        );
      }

      public class PlanQuestion {

        public int QuestionId { get; private set; }
        public int? GblQuestionId { get; private set; }
        public int AutoNumber { get; private set; }
        public string InputType { get; private set; }
        public bool IsHeading { get; private set; }
        public string QuestionText { get; private set; }
        public string TextAnswer { get; private set; }

        public PlanQuestion(int questionId, int? gblQuestionId, int autoNumber, string inputType, bool isHeading, string questionText, string textAnswer) {
          QuestionId = questionId;
          GblQuestionId = gblQuestionId;
          AutoNumber = autoNumber;
          InputType = inputType;
          IsHeading = isHeading;
          QuestionText = questionText;
          TextAnswer = textAnswer;
        }
      }

      public class PlanInfo {

        public int UserId { get; private set; }
        public int SurveyId { get; private set; }
        public string SurveyUniqueId { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public DateTime? ClosedUtc { get; private set; }
        public int SurveyPartId { get; private set; }
        public string SurveyPartUniqueId { get; private set; }
        public int? SurveyPartPercentCompleted { get; private set; }
        public DateTime? SurveyPartCompletedUtc { get; private set; }
        public int? CoachUserId { get; private set; }
        public string CoachFirstName { get; private set; }
        public string CoachLastName { get; private set; }
        public string GoalText { get; private set; }
        public Guid UserGuid { get; private set; }
        public int CreatedByUserId { get; private set; }

        public PlanInfo(
          int userId, int surveyId, string surveyUniqueId, DateTime createdUtc, DateTime? closedUtc,
          int surveyPartId, string surveyPartUniqueId, int? surveyPartPercentCompleted, DateTime? surveyPartCompletedUtc,
          int? coachUserId, string coachFirstName, string coachLastName,
          string goalText, Guid userGuid, int createdByUserId) {

          UserId = userId;
          SurveyId = surveyId;
          SurveyUniqueId = surveyUniqueId;
          CreatedUtc = createdUtc;
          ClosedUtc = closedUtc;
          SurveyPartId = surveyPartId;
          SurveyPartUniqueId = surveyPartUniqueId;
          SurveyPartPercentCompleted = surveyPartPercentCompleted;
          SurveyPartCompletedUtc = surveyPartCompletedUtc;
          CoachUserId = coachUserId;
          CoachFirstName = coachFirstName;
          CoachLastName = coachLastName;
          GoalText = goalText ?? "";
          UserGuid = userGuid;
          CreatedByUserId = createdByUserId;
        }
      }

    }
  }
}
