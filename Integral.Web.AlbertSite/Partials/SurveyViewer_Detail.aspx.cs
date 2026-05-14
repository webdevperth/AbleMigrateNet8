using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class SurveyViewer_Detail : AppCode.PageBaseClasses.SurveyViewerPartialBase {

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public int TableRowCount = 0;

    protected void Page_Load(object sender, EventArgs e) {

      GetQuestionInfo();

      if (BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org) {
        GetOrgNorms();
      }

      if (SingleSurveyPartId > 0) {
        GetIndividualScores();
      } else {
        GetGroupScores();
      }
    }

    void GetQuestionInfo() {

      // First get question text, categories and max scores.
      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT Value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        SELECT gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf, MAX(sc.ValueScore) AS MaxScore
        FROM IntakeCodeIds ic WITH (NOLOCK)
        INNER JOIN sv_360_Codes sic ON sic.CodeId = ic.IntakeCodeId
        INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId AND sit.AnswerTypeDescr = 'date'
        INNER JOIN sv_360_Questions sq ON sq.SurveyId = sit.SurveyId
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        INNER JOIN al_RptQnGrpHeadings gh ON gh.RptQnGrpHeadingId = gqh.RptQnGrpHeadingId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.AnswerTypeId = sq.AnswerTypeId
        {HeadingConditionSQL.EnsureStartsWith("WHERE ", true)}
        GROUP BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gh.RptQnGrpHeading, gq.GblQuestionId, gq.QuestionTextSelf
        HAVING MAX(sc.ValueScore) > 0
        ORDER BY gh.RptQnGrpHeadingSort, gh.RptQnGrpHeadingId, gq.GblQuestionId",
        dr => {
          int categoryHeadingId = dr.GetInt("RptQnGrpHeadingId");
          var cat = new QuestionInfo() {
            CategoryHeadingId = dr.GetInt("RptQnGrpHeadingId"),
            CategoryHeading = dr.GetString("RptQnGrpHeading"),
            GblQuestionId = dr.GetInt("GblQuestionId"),
            GblQuestionText = dr.GetString("QuestionTextSelf"),
            MaxScore = (int)(dr.GetDoubleOrNull("MaxScore") ?? 0)
          };
          Questions.Add(cat);
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList())
      );
    }

    void GetIndividualScores() {

      DbHelper.Common.Query($@"
        SELECT gq.GblQuestionId,
          gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount,
          gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount,
          CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS SelfScore,
          CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4, 1)) AS RaterScore
        FROM sv_360_Questions sq
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
        LEFT OUTER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        LEFT OUTER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        WHERE sq.SurveyId = @SurveyId
          AND (sp.PartId IS NULL OR (sp.Completed IS NOT NULL AND (sp.PartId = @PartId OR sp.Self_PartId = @PartId)))
          {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}
        GROUP BY gq.GblQuestionId, gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount, gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount
        ORDER BY gq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo != null) {
            gqInfo.SurveySelfScore = dr.GetDecimalOrNull("SelfScore").Round(1, MidpointRounding.AwayFromZero);
            gqInfo.SurveyRaterScore = dr.GetDecimalOrNull("RaterScore").Round(1, MidpointRounding.AwayFromZero);
            int globalSelfTotal = dr.GetIntOrNull("GlobalNormSelfTotal") ?? 0;
            int globalSelfCount = dr.GetIntOrNull("GlobalNormSelfCount") ?? 0;
            if (globalSelfTotal == 0 || globalSelfCount == 0) {
              gqInfo.NormSelfScore = null;
            } else {
              gqInfo.NormSelfScore = ((decimal)globalSelfTotal / globalSelfCount).Round(1, MidpointRounding.AwayFromZero);
            }
            int globalRaterTotal = dr.GetIntOrNull("GlobalNormRaterTotal") ?? 0;
            int globalRaterCount = dr.GetIntOrNull("GlobalNormRaterCount") ?? 0;
            if (globalRaterTotal == 0 || globalRaterCount == 0) {
              gqInfo.NormRaterScore = null;
            } else {
              gqInfo.NormRaterScore = ((decimal)globalRaterTotal / globalRaterCount).Round(1, MidpointRounding.AwayFromZero);
            }
          }
        },
        DbHelper.Common.NewSqlParameter("SurveyId", SingleSurveyId),
        DbHelper.Common.NewSqlParameter("PartId", SingleSurveyPartId)
      );
    }

    void GetGroupScores() {
      DbHelper.Common.Query($@"
        WITH IntakeCodeIds
        AS (
          SELECT Value AS IntakeCodeId
          FROM STRING_SPLIT(@IntakeCodeIds, ',')
        )
        SELECT gq.GblQuestionId,
          gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount,
          gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount,
          CAST(AVG(IIF(sp.IsSelf = 1, sc.ValueScore, NULL)) AS DECIMAL(4,1)) AS AvgSelf,
          CAST(AVG(IIF(sp.IsSelf = 0, sc.ValueScore, NULL)) AS DECIMAL(4,1)) AS AvgRater
        FROM IntakeCodeIds ic WITH (NOLOCK)
        INNER JOIN sv_360_Codes sic ON sic.CodeId = ic.IntakeCodeId
        INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId
        INNER JOIN sv_Survey sv ON sv.sv_id = sit.SurveyId
        INNER JOIN sv_360_Questions sq ON sq.SurveyId = sit.SurveyId
        INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
        INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gq.GblQuestionId
        LEFT OUTER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        LEFT OUTER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
        LEFT OUTER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        WHERE sit.AnswerTypeDescr = 'date'
          AND (sp.PartId IS NULL OR (sp.Completed IS NOT NULL AND sp.DateGroupCode = sic.Code))
          {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}
        GROUP BY gq.GblQuestionId, gq.GlobalNormSelfTotal, gq.GlobalNormSelfCount, gq.GlobalNormRaterTotal, gq.GlobalNormRaterCount
        ORDER BY gq.GblQuestionId",
        dr => {
          int gqId = dr.GetInt("GblQuestionId");
          var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
          if (gqInfo != null) {
            gqInfo.SurveySelfScore = dr.GetDecimalOrNull("AvgSelf").Round(1, MidpointRounding.AwayFromZero);
            gqInfo.SurveyRaterScore = dr.GetDecimalOrNull("AvgRater").Round(1, MidpointRounding.AwayFromZero);
            int globalSelfTotal = dr.GetIntOrNull("GlobalNormSelfTotal") ?? 0;
            int globalSelfCount = dr.GetIntOrNull("GlobalNormSelfCount") ?? 0;
            if (globalSelfTotal == 0 || globalSelfCount == 0) {
              gqInfo.NormSelfScore = null;
            } else {
              gqInfo.NormSelfScore = ((decimal)globalSelfTotal / globalSelfCount).Round(1, MidpointRounding.AwayFromZero);
            }
            int globalRaterTotal = dr.GetIntOrNull("GlobalNormRaterTotal") ?? 0;
            int globalRaterCount = dr.GetIntOrNull("GlobalNormRaterCount") ?? 0;
            if (globalRaterTotal == 0 || globalRaterCount == 0) {
              gqInfo.NormRaterScore = null;
            } else {
              gqInfo.NormRaterScore = ((decimal)globalRaterTotal / globalRaterCount).Round(1, MidpointRounding.AwayFromZero);
            }
          }
        },
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList())
      );
    }

    void GetOrgNorms() {

      // Org scores for questions in selected intakes.
      try {
        DbHelper.Common.Query($@"
          WITH IntakeCodeIds
          AS (
            SELECT Value AS IntakeCodeId
            FROM STRING_SPLIT(@IntakeCodeIds, ',')
          ),
          CompanyIds
          AS (
            SELECT DISTINCT (sv.SvCompanyId) AS SvCompanyId
            FROM sv_360_Codes sic WITH (NOLOCK)
            INNER JOIN IntakeCodeIds ic ON sic.CodeId = ic.IntakeCodeId
            INNER JOIN sv_360_AnswerTypes sit ON sit.AnswerTypeId = sic.AnswerTypeId
            INNER JOIN sv_Survey sv ON sv.sv_id = sit.SurveyId
          )
          SELECT gqs.GblQuestionId,
            SUM(gqs.ScoreSum) AS NormSelfTotal, SUM(gqs.ScoreCount) AS NormSelfCount,
            SUM(gqs.ScoreSumRaters) AS NormRaterTotal, SUM(gqs.ScoreCountRaters) AS NormRaterCount
          FROM CompanyIds
          INNER JOIN sv_Survey sv ON CompanyIds.SvCompanyId = sv.SvCompanyId
          INNER JOIN sv_SurveyGblQnScores gqs ON gqs.SurveyId = sv.sv_id
          INNER JOIN al_RptQnGrpHgGblQns gqh ON gqh.GblQuestionId = gqs.GblQuestionId
          WHERE gqs.GblAnswerTypeId = @GblAnswerTypeId
            {SurveyAndHeadingConditionSQL.EnsureStartsWith("AND ", true)}
          GROUP BY gqs.GblQuestionId",
          dr => {
            int gqId = dr.GetInt("GblQuestionId");
            var gqInfo = Questions.Find(c => c.GblQuestionId == gqId);
            if (gqInfo != null) {
              int orgSelfTotal = dr.GetIntOrNull("NormSelfTotal") ?? 0;
              int orgSelfCount = dr.GetIntOrNull("NormSelfCount") ?? 0;
              if (orgSelfTotal == 0 || orgSelfCount == 0) {
                gqInfo.NormSelfScore = null;
              } else {
                gqInfo.NormSelfScore = ((decimal)orgSelfTotal / orgSelfCount).Round(1, MidpointRounding.AwayFromZero);
              }
              int orgRaterTotal = dr.GetIntOrNull("NormRaterTotal") ?? 0;
              int orgRaterCount = dr.GetIntOrNull("NormRaterCount") ?? 0;
              if (orgRaterTotal == 0 || orgRaterCount == 0) {
                gqInfo.NormRaterScore = null;
              } else {
                gqInfo.NormRaterScore = ((decimal)orgRaterTotal / orgRaterCount).Round(1, MidpointRounding.AwayFromZero);
              }
            }
          },
          DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList.ToStringList()),
          DbHelper.Common.NewSqlParameter("GblAnswerTypeId", ScoringGblAnswerTypeId)
        );
      } catch (Exception) {
        // Ignore for now.
      }
    }

    public void ShowQuestions(Action<string> categoryStart, Action categoryEnd, Action<QuestionInfo> questionDetail) {

      int iQn = 0;
      int? thisHeadingId = null;

      while (iQn < Questions.Count) {

        var qnItem = Questions[iQn];
        thisHeadingId = qnItem.CategoryHeadingId;

        categoryStart(qnItem.CategoryHeading);

        while (iQn < Questions.Count) {
          qnItem = Questions[iQn];
          if (thisHeadingId != qnItem.CategoryHeadingId) break;
          questionDetail(qnItem);
          iQn++;
        }

        categoryEnd();
      }
    }

    public string GetBenchComparisonRowClass(QuestionInfo question) {

      if (question.SurveySelfScore != null && question.NormSelfScore != null && question.SurveySelfScore < question.NormSelfScore) {
        return ""; // was "benchBelow" for red bars, disabled for now (CU-860r0jygu).
      } else {
        return "";
      }
    }

    public class QuestionInfo {
      public int CategoryHeadingId { get; set; }
      public string CategoryHeading { get; set; }
      public int GblQuestionId { get; set; }
      public string GblQuestionText { get; set; }
      public decimal? SurveySelfScore { get; set; }
      public decimal? SurveyRaterScore { get; set; }
      public decimal? NormSelfScore { get; set; }
      public decimal? NormRaterScore { get; set; }
      public int MaxScore { get; set; }
    }

  }
}
