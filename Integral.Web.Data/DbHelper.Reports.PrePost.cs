using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using static Integral.Web.DbHelper.Common;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public class PrePost {

        public static List<QuestionInfo> GetQuestions(SqlParameter[] sqlParams) {

          var questions = new List<QuestionInfo>();

          Query(@"
            SELECT gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, sq.GblQuestionId, sq.QuestionTextFull1
            FROM sv_360_Questions sq WITH (NOLOCK)
            INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
            INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = sq.GblQuestionId
            INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
            WHERE sq.SurveyId = @SampleSurveyId
              AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
              AND sq.RptQnGroupId = @RptQnGroupId
            GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, sq.GblQuestionId, sq.QuestionTextFull1
            ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, sq.GblQuestionId;",
            dr => {
              questions.Add(new QuestionInfo(
                categoryHeadingId: dr.GetInt("RptQnGrpHeadingId"),
                categoryHeading: dr.GetString("RptQnGrpHeading"),
                gblQuestionId: dr.GetInt("GblQuestionId"),
                gblQuestionText: dr.GetString("QuestionTextFull1")
              ));
            },
            sqlParams
          );

          return questions;
        }

        public class QuestionInfo {
          public int CategoryHeadingId { get; private set; }
          public string CategoryHeading { get; private set; }
          public int GblQuestionId { get; private set; }
          public string GblQuestionText { get; private set; }
          public decimal? ScoreSelfAll { get; private set; }
          public decimal? ScoreRaterAll { get; private set; }
          public decimal? ScoreSelfPre { get; set; }
          public decimal? ScoreRaterPre { get; set; }
          public decimal? ScoreSelfPost { get; set; }
          public decimal? ScoreRaterPost { get; set; }
          public decimal? BenchScoreSelfPre { get; private set; }
          public decimal? BenchScoreRaterPre { get; private set; }
          public decimal? BenchScoreSelfPost { get; private set; }
          public decimal? BenchScoreRaterPost { get; private set; }
          public QuestionInfo(
            int categoryHeadingId,
            string categoryHeading,
            int gblQuestionId,
            string gblQuestionText
          ) {
            CategoryHeadingId = categoryHeadingId;
            CategoryHeading = categoryHeading;
            GblQuestionId = gblQuestionId;
            GblQuestionText = gblQuestionText;
          }
          public void SetScores(
            decimal? scoreSelfAll,
            decimal? scoreRaterAll,
            decimal? scoreSelfPre,
            decimal? scoreRaterPre,
            decimal? scoreSelfPost,
            decimal? scoreRaterPost
          ) {
            ScoreSelfAll = scoreSelfAll;
            ScoreRaterAll = scoreRaterAll;
            ScoreSelfPre = scoreSelfPre;
            ScoreRaterPre = scoreRaterPre;
            ScoreSelfPost = scoreSelfPost;
            ScoreRaterPost = scoreRaterPost;
          }
          public void SetBenchScores(
            decimal? benchScoreSelfPre,
            decimal? benchScoreRaterPre,
            decimal? benchScoreSelfPost,
            decimal? benchScoreRaterPost
          ) {
            BenchScoreSelfPre = benchScoreSelfPre;
            BenchScoreRaterPre = benchScoreRaterPre;
            BenchScoreSelfPost = benchScoreSelfPost;
            BenchScoreRaterPost = benchScoreRaterPost;
          }
        }



      }

    }
  }
}
