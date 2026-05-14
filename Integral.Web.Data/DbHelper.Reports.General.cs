using System;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public enum NormEnum { Org, Global }

      public class General {

        private static string NormSQL(int questionsFromSurveyId, int? rptQnGroupIdOrNullForAll, NormEnum normType, bool groupByGblQuestionId = false) {

          if (normType == NormEnum.Org) {

            // Get norms from sv_SurveyGblQnScores table.
            return $@"
                SUM(sgs.ScoreSum) AS SelfTotal,
                SUM(sgs.ScoreCount) AS SelfCount,
                SUM(sgs.ScoreSumRaters) AS RaterTotal,
                SUM(sgs.ScoreCountRaters) AS RaterCount
              FROM sv_Survey svq
              INNER JOIN sv_360_Questions sq ON sq.SurveyId = svq.sv_id
              INNER JOIN sv_SurveyGblQnScores sgs ON sgs.GblQuestionId = sq.GblQuestionId
              INNER JOIN sv_Survey svg ON svg.sv_id = sgs.SurveyId
              WHERE svq.sv_id = {questionsFromSurveyId}
                AND sgs.GblAnswerTypeId = svg.PrimaryGblAnswerTypeId
                AND svg.SvCompanyId = svq.SvCompanyId
                {(rptQnGroupIdOrNullForAll == null ? "" : $"AND sq.RptQnGroupId = {rptQnGroupIdOrNullForAll}")}";

          } else {

            return $@"
                SUM(CAST(glq.GlobalNormSelfTotal AS BIGINT)) AS SelfTotal,
                SUM(CAST(glq.GlobalNormSelfCount AS BIGINT)) AS SelfCount,
                SUM(CAST(glq.GlobalNormRaterTotal AS BIGINT)) AS RaterTotal,
                SUM(CAST(glq.GlobalNormRaterCount AS BIGINT)) AS RaterCount
              FROM sv_360_Questions sq
              INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
              INNER JOIN sv_GblQuestions glq ON glq.GblQuestionId = sq.GblQuestionId
              WHERE sv.sv_id = {questionsFromSurveyId}
                AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
                {(rptQnGroupIdOrNullForAll == null ? "" : $"AND sq.RptQnGroupId = {rptQnGroupIdOrNullForAll}")}";
          }
        }

        public static NormResult GetNorms(
          int gblAnswerTypeId,
          int? rptQnGroupIdOrNullForAll = null,
          int? orgCompanyId = null) {

          return GetNorms(gblAnswerTypeId, null, rptQnGroupIdOrNullForAll, orgCompanyId);
        }

        public static NormResult GetNorms(
          int gblAnswerTypeId,
          Action<GetNorms_Questions_Result> ProcessGblQuestion,
          int? rptQnGroupIdOrNullForAll = null,
          int? orgCompanyId = null) {

          var normResult = new NormResult(0, 0, 0, 0);

          Query($@"
            SELECT
              {(ProcessGblQuestion == null ? "" : "glq.GblQuestionId,")}
              SUM(glq.ScoreSum) AS SelfTotal,
              SUM(glq.ScoreCount) AS SelfCount,
              SUM(glq.ScoreSumRaters) AS RaterTotal,
              SUM(glq.ScoreCountRaters) AS RaterCount
            FROM sv_SurveyGblQnScores glq
            INNER JOIN sv_360_Questions sq ON sq.GblQuestionId = glq.GblQuestionId
            INNER JOIN sv_Survey sv ON sv.sv_id = sq.SurveyId
            WHERE glq.GblAnswerTypeId = @GblAnswerTypeId
              {(orgCompanyId == null ? "" : "AND sv.SvCompanyId = @SvCompanyId")}
              {(rptQnGroupIdOrNullForAll == null ? "" : "AND sq.RptQnGroupId = @RptQnGroupId")}
            {(ProcessGblQuestion == null ? "" : "GROUP BY glq.GblQuestionId")}",
            dr => {
              normResult = GetNormsFromRow(dr);
              if (ProcessGblQuestion != null) {
                int gblQuestionId = dr.GetInt("GblQuestionId");
                ProcessGblQuestion(new GetNorms_Questions_Result(gblQuestionId, normResult));
              }
            },
            NewSqlParameter("SvCompanyId", orgCompanyId),
            NewSqlParameter("GblAnswerTypeId", gblAnswerTypeId),
            NewSqlParameter("RptQnGroupId", rptQnGroupIdOrNullForAll)
          );
          return ProcessGblQuestion == null ? normResult : null;
        }

        public static void GetNorms_Questions(
          int questionsFromSurveyId,
          int? rptQnGroupIdOrNullForAll,
          NormEnum normType,
          Action<GetNorms_Questions_Result> ProcessQuestion) {

          var normResult = new NormResult(0, 0, 0, 0);
          Common.Query($@"
            SELECT sq.GblQuestionId, {NormSQL(questionsFromSurveyId, rptQnGroupIdOrNullForAll, normType)}
            GROUP BY sq.GblQuestionId;",
            dr => {
              int gblQuestionId = dr.GetInt("GblQuestionId");
              normResult = GetNormsFromRow(dr);
              ProcessQuestion(new GetNorms_Questions_Result(gblQuestionId, normResult));
            }
          );
        }

        private static NormResult GetNormsFromRow(SqlDataReader dr) {

          return new NormResult(
            dr.GetDecimalOrNull("SelfTotal") ?? 0,
            dr.GetDecimalOrNull("SelfCount") ?? 0,
            dr.GetDecimalOrNull("RaterTotal") ?? 0,
            dr.GetDecimalOrNull("RaterCount") ?? 0
          );
        }

        public static void GetResponseCount_Global(string surveyTypeCode, out int selfCount, out int raterCount) {
          GetResponseCount(surveyTypeCode, null, out selfCount, out raterCount);
        }

        public static void GetResponseCount_Org(string surveyTypeCode, int svCompanyId, out int selfCount, out int raterCount) {
          GetResponseCount(surveyTypeCode, svCompanyId, out selfCount, out raterCount);
        }

        private static void GetResponseCount(string surveyTypeCode, int? svCompanyIdOrNullForGlobal, out int selfCount, out int raterCount) {

          int selfCountTemp = 0;
          int raterCountTemp = 0;

          Common.Query($@"
            SELECT sp.IsSelf, COUNT(sp.Completed) AS PartCount
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
            WHERE sv.SurveyTypeCode = @SurveyTypeCode
              {(svCompanyIdOrNullForGlobal != null ? "AND sp.AbleSvCompanyId = @SvCompanyId" : "")}
            GROUP BY sp.IsSelf",
            dr => {
              if (dr.GetInt("IsSelf") == 1) {
                selfCountTemp = dr.GetInt("PartCount");
              } else {
                raterCountTemp = dr.GetInt("PartCount");
              }
            },
            Common.NewSqlParameter("SurveyTypeCode", surveyTypeCode),
            Common.NewSqlParameter("SvCompanyId", svCompanyIdOrNullForGlobal)
          );

          selfCount = selfCountTemp;
          raterCount = raterCountTemp;
        }

        public static void GetResponseCountForCompanyOrGlobal(int gblAnswerTypeId, int? svCompanyIdOrNullForGlobal, out int selfCount, out int raterCount) {

          int selfCountTemp = 0;
          int raterCountTemp = 0;

          Common.Query($@"
            SELECT COUNT(IIF(sp.IsSelf = 1, sp.PartId, NULL)) AS SelfCount,
              COUNT(IIF(sp.IsSelf = 0, sp.PartId, NULL)) AS RaterCount
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            WHERE (@SvCompanyId IS NULL OR sv.SvCompanyId = @SvCompanyId)
              AND sv.PrimaryGblAnswerTypeId = @GblAnswerTypeId
              AND sp.Completed IS NOT NULL;",
            dr => {
              selfCountTemp = dr.GetInt("SelfCount");
              raterCountTemp = dr.GetInt("RaterCount");
            },
            Common.NewSqlParameter("GblAnswerTypeId", gblAnswerTypeId),
            Common.NewSqlParameter("SvCompanyId", svCompanyIdOrNullForGlobal)
          );

          selfCount = selfCountTemp;
          raterCount = raterCountTemp;
        }

        // Classes

        public class GetNorms_Questions_Result {
          public readonly int GblQuestionId;
          public readonly NormResult NormResult;
          public GetNorms_Questions_Result(int gblQuestionId, NormResult normResult) {
            this.GblQuestionId = gblQuestionId;
            this.NormResult = normResult;
          }
        }

        public class NormResult {

          public readonly decimal SelfTotal, SelfCount, RaterTotal, RaterCount;
          private decimal? _selfNorm = null, _raterNorm = null;

          public NormResult(decimal selfTotal, decimal selfCount, decimal raterTotal, decimal raterCount) {
            SelfTotal = selfTotal;
            SelfCount = selfCount;
            RaterTotal = raterTotal;
            RaterCount = raterCount;
          }
          public decimal? SelfNorm {
            get {
              if (_selfNorm == null && SelfCount > 0) _selfNorm = (SelfTotal / SelfCount).Round(1, MidpointRounding.AwayFromZero);
              return _selfNorm;
            }
          }
          public decimal? RaterNorm {
            get {
              if (_raterNorm == null && RaterCount > 0) _raterNorm = (RaterTotal / RaterCount).Round(1, MidpointRounding.AwayFromZero);
              return _raterNorm;
            }
          }
        }

      }
    }
  }
}
