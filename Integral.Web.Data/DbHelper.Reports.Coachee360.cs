using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public class Coachee360 {

        public const string Reports_360_QnGroupName = "LMP_Main_Skills";

        static Coachee360() { }

        public static Coachee360Results GetCoachee360ReportResults(int coacheeId, string surveyUId, int? benchCompanyId) {

          var results = new Coachee360Results();

          var thisSurveyInfo = AlbertSurveys.GetSurveyInfoForCoacheeId(surveyUId, coacheeId);
          if (thisSurveyInfo == null || thisSurveyInfo.IsSelfOnly) return null;

          var previousCompleted = Participants.GetPreviousCompleted(coacheeId, thisSurveyInfo.FoundParticipantBrief.PartId, ConfigHelper.MinimumRatersFor360Report);
          if (previousCompleted == null) return null;

          results.SurveyInfo = thisSurveyInfo;
          results.FoundParticipantBrief = thisSurveyInfo.FoundParticipantBrief;
          results.ReportQuestions = GetCoachee360ReportQuestions(thisSurveyInfo.FoundParticipantBrief.PartId, benchCompanyId, thisSurveyInfo.SurveyId, previousCompleted.PreviousSurveyId);
          return results;

        }

        private static List<Questions.ReportQuestionInfo> GetCoachee360ReportQuestions(int participantId, int? benchCompanyId, int surveyId, int? previousSurveyId) {

          // TODO: A better way of identifying "scoring" questions other than
          //       referencing specific question groups (in this case @RptQnGroupName and @PulseGblATId)

          var results = new List<Questions.ReportQuestionInfo>();

          using (var conn = DbHelper.Common.GetNewIntegralConnection()) {
            using (var cmd = new SqlCommand(@"

          WITH qnlat
          AS (
            -- Questions in latest survey.
            SELECT q.SurveyId AS LatestSurveyId,
              qgh.RptQnGrpHeadingSort, qgh.RptQnGrpHeading,
              q.QuestionId, q.GblQuestionId, q.AutoNumber, q.Sort,
              q.QuestionTextFull1, q.QuestionTextFullOther1
            FROM sv_360_Questions q WITH (NOLOCK)
            INNER JOIN sv_360_AnswerTypes t ON q.AnswerTypeId = t.AnswerTypeId
            LEFT OUTER JOIN al_RptQnGrpHgGblQns qghq
              -- Question headings.
              INNER JOIN al_RptQnGrpHeadings qgh ON qghq.RptQnGrpHeadingId = qgh.RptQnGrpHeadingId
              INNER JOIN al_RptQnGroups qg ON qgh.RptQnGroupId = qg.RptQnGroupId
              ON q.GblQuestionId = qghq.GblQuestionId
            WHERE q.SurveyId = @LatestSurveyId -- questions as they are in the latest survey
              AND (qg.RptQnGroupName = @RptQnGroupName
                OR q.GblAnswerTypeId = @PulseGblATId)
          )

          -- Get survey scores.
          SELECT qnlat.*,
            0 AS IsBench,
            0 AS IsPrevious,
            ans_sv.IsSelf, ans_sv.ScoreSum, ans_sv.ScoreCount, ans_sv.ScoreDiv, ans_sv.ScoreAvg
          FROM qnlat WITH (NOLOCK) -- questions from latest survey for coachee
          OUTER APPLY (
            SELECT p.IsSelf,
              SUM(c.ValueScore) AS ScoreSum,
              COUNT(c.ValueScore) AS ScoreCount,
              SUM(c.ValueScore) / COUNT(c.ValueScore) * 10 AS ScoreDiv,
              Avg(c.ValueScore) * 10 AS ScoreAvg
            FROM sv_360_Participants p WITH (NOLOCK)
            INNER JOIN sv_Answers a ON p.PartId = a.ParticipantId AND a.QuestionId = qnlat.QuestionId
            INNER JOIN sv_360_Codes c ON a.CodeId = c.CodeId
            WHERE p.SurveyId = qnlat.LatestSurveyId
              AND (p.PartId = @PartId OR p.Self_PartId = @PartId)
              AND p.Completed IS NOT NULL
              AND a.QuestionId = qnlat.QuestionId
            GROUP BY p.IsSelf
          ) AS ans_sv

          UNION ALL

          -- Get benchmark scores - org (@CompanyId not null) or global (@CompanyId is null).
          -- Also separate out results from 2nd-latest survey id
          SELECT qnlat.*,
            1 AS IsBench,
            ans_sv.IsPrevious,
            ans_sv.IsSelf, ans_sv.ScoreSum, ans_sv.ScoreCount, ans_sv.ScoreDiv, ans_sv.ScoreAvg
          FROM qnlat WITH (NOLOCK)
          OUTER APPLY (
            SELECT gp.IsSelf,
              CASE gp.SurveyId
                WHEN @PreviousSurveyId THEN 1
                ELSE 0
              END AS IsPrevious,
              SUM(gc.ValueScore) AS ScoreSum,
              COUNT(gc.ValueScore) AS ScoreCount,
              SUM(gc.ValueScore) / COUNT(gc.ValueScore) * 10 AS ScoreDiv,
              Avg(gc.ValueScore) * 10 AS ScoreAvg

            FROM al_Coachees gac WITH (NOLOCK)
            INNER JOIN sv_360_Participants gpc ON gac.CoacheeId = gpc.AbleCoacheeId
            INNER JOIN sv_360_Participants gp ON gp.PartId = gpc.PartId OR gp.Self_PartId = gpc.PartId
            INNER JOIN sv_360_Questions gq ON gq.SurveyId = gpc.SurveyId -- a.QuestionId = q.QuestionId --AND q.GblQuestionId = qnlat.GblQuestionId
            INNER JOIN sv_Answers ga ON ga.ParticipantId = gp.PartId and ga.QuestionId = gq.QuestionId
            INNER JOIN sv_Survey gs ON gs.sv_id = gpc.SurveyId
            INNER JOIN sv_360_Codes gc ON ga.CodeId = gc.CodeId

            WHERE gq.GblQuestionId = qnlat.GblQuestionId
              AND (gp.PartId = gpc.PartId OR gp.Self_PartId = gpc.PartId)
              AND gp.Completed IS NOT NULL
              AND (@CompanyId IS NULL OR gac.CompanyId = @CompanyId)
            GROUP BY CASE gp.SurveyId
              WHEN @PreviousSurveyId THEN 1
              ELSE 0
            END,
            gp.IsSelf
          ) AS ans_sv

          ORDER BY qnlat.RptQnGrpHeadingSort, qnlat.Sort
          , IsBench, IsPrevious, ans_sv.IsSelf DESC --, ans_bench.IsSelf DESC

          ", conn)) {
              cmd.Parameters.AddInt("@LatestSurveyId", surveyId);
              cmd.Parameters.AddInt("@PreviousSurveyId", previousSurveyId);
              cmd.Parameters.AddInt("@CompanyId", benchCompanyId);
              cmd.Parameters.AddInt("@PartId", participantId);
              // TODO: Better way of identifying specific question groups than the below hardcoded id's.
              cmd.Parameters.AddVarChar("@RptQnGroupName", 50, Reports_360_QnGroupName);
              cmd.Parameters.AddInt("@PulseGblATId", ConfigHelper.GblAnsTypeId_360Pulse);

              LogHelper.LogLatestSQL(cmd);

              conn.Open();
              using (SqlDataReader dr = cmd.ExecuteReader()) {

                bool eof = !dr.Read();
                while (!eof) {

                  var row = new Questions.ReportQuestionInfo(
                    dr.GetInt("LatestSurveyId"),
                    dr.GetIntOrNull("RptQnGrpHeadingSort"),
                    dr.GetString("RptQnGrpHeading"),
                    dr.GetInt("QuestionId"),
                    dr.GetInt("GblQuestionId"),
                    dr.GetInt("Sort"),
                    dr.GetInt("AutoNumber"),
                    dr.GetString("QuestionTextFull1"),
                    dr.GetString("QuestionTextFullOther1")
                  );

                  var thisQnId = dr.GetInt("QuestionId");
                  // loop thru rows for this question to get scores for each level - self, rater, benchself, benchrater
                  while (!eof) {
                    if (thisQnId != dr.GetInt("QuestionId")) break;

                    double? scoreSum = dr.GetDoubleOrNull("ScoreSum");
                    int? scoreCount = dr.GetIntOrNull("ScoreCount");

                    // Flags IsPrevious, IsBench and IsSelf indicate the type of score in this row.
                    // Combinations are:
                    // IsBench IsPrevious IsSelf
                    //     1        1       1     Score is Self from Previous survey.
                    //     1        1       0     Score is Raters from Previous survey.
                    //     1        0       1     Score is Self from benchmark surveys EXCLUDING Latest and Previous surveys.
                    //     1        0       0     Score is Raters from benchmark surveys EXCLUDING Latest and Previous surveys.
                    //     0        0       1     Score is Self from Latest survey.
                    //     0        0       0     Score is Raters from Latest survey.

                    if (!dr.IsDBNull("IsSelf")) { // null if no scores exist at all for this question.
                      if (dr.GetInt("IsBench") == 1) {
                        if (dr.GetInt("IsPrevious") == 1) {
                          // Previous survey score.
                          if (dr.GetInt("IsSelf") == 1) row.Scores.ScorePreviousSelf.AccumulateScore(scoreSum, scoreCount);
                          else row.Scores.ScorePreviousRater.AccumulateScore(scoreSum, scoreCount);
                        }
                        // Note previous is also added to bench here, as its result was separated in the query.
                        if (dr.GetInt("IsSelf") == 1) {
                          row.Scores.ScoreBenchSelf.AccumulateScore(scoreSum, scoreCount);
                        } else {
                          row.Scores.ScoreBenchRater.AccumulateScore(scoreSum, scoreCount);
                        }
                      } else {
                        // Latest survey score.
                        if (dr.GetInt("IsSelf") == 1) {
                          row.Scores.ScoreSelf.AccumulateScore(scoreSum, scoreCount);
                        } else {
                          row.Scores.ScoreRater.AccumulateScore(scoreSum, scoreCount);
                        }
                      }
                    }
                    eof = !dr.Read();
                  }
                  results.Add(row);
                }

              }
            }
          }
          return results;
        } // GetResults

        public class Coachee360Results {
          public List<Questions.ReportQuestionInfo> ReportQuestions;
          public AlbertSurveys.SurveyInfo SurveyInfo;
          public Participants.FoundParticipantBrief FoundParticipantBrief;
        }

      }
    }
  }
}
