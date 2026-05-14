using System;
using System.Collections.Generic;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public class Company {

        // Return info & stats for surveys matching the given criteria.
        // Note that for reports, usually only 1 result is desired (i.e. survey selection criteria is sufficiently precise).

        public enum IncludePulseSurveys { Yes, No }

        public static List<SurveyStats> GetSurveyStatsByType(
          int? companyIdOrNullForAll,
          string projectJobNumbersOrNullForAll,
          List<int> programJobIdsOrNullForAll,
          int? gblAnswerTypeIdOrNullForAll,
          string surveyTypeCode,
          int? onlySurveysWithRptQnGroupId = null) {

          if (!programJobIdsOrNullForAll.IsNullOrEmpty() && projectJobNumbersOrNullForAll.IsNullOrEmpty()) throw new ArgumentException("Must provide projectJobNumber along with job ids.");

          var statsList = new List<SurveyStats>();

          Query($@"
            WITH
            JobNumbers AS (
              SELECT Value AS JobNumber
              FROM STRING_SPLIT(@ProjectJobNumbers, ',')),
            ProgramJobIds AS (
              SELECT Value AS ProgramJobId
              FROM STRING_SPLIT(@ProgramJobIds, ',')),
            Stats AS (
              SELECT
                {(companyIdOrNullForAll == null ? "" : "sc.SvCompanyId, sc.CompanyName,")}
                gat.GblAnswerTypeId, gat.GblAnswerTypeDescr,
                COUNT(DISTINCT ij.JobId) AS ProgramCount,
                COUNT(DISTINCT sv.sv_id) AS SurveyCount,
                MAX(sv.sv_id) AS SampleSurveyId,
                MAX(IIF(sp.IsSelf = 1, sp.Completed, NULL)) AS SelfLatestCompleted,
                COUNT(DISTINCT sp.AbleCoacheeId) AS CoacheeCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 1, sp.PartId, NULL)) AS SelfAllCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 1 AND sp.IsSelfPreSurvey = 1, sp.PartId, NULL)) AS SelfPreCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 1 AND sp.IsSelfPostSurvey = 1, sp.PartId, NULL)) AS SelfPostCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 0, sp.PartId, NULL)) AS RaterAllCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 0 AND sp.IsSelfPreSurvey = 1, sp.PartId, NULL)) AS RaterPreCount,
                COUNT(DISTINCT IIF(sp.IsSelf = 0 AND sp.IsSelfPostSurvey = 1, sp.PartId, NULL)) AS RaterPostCount
              FROM sv_360_Participants sp WITH (NOLOCK)
              INNER JOIN id_Job ij ON ij.JobId = sp.AbleProgramJobId
              {(!projectJobNumbersOrNullForAll.IsNullOrEmpty() // Restrict to selected JobNumbers.
                ? "INNER JOIN JobNumbers jns ON jns.JobNumber = ij.JobNumber"
                : "")}
              {(!programJobIdsOrNullForAll.IsNullOrEmpty() // Restrict to selected JobIds.
                ? "INNER JOIN ProgramJobIds pjs ON pjs.ProgramJobId = ij.JobId"
                : "")}
              INNER JOIN al_Project ap ON ap.JobNumber = ij.JobNumber
              INNER JOIN sv_SurveyCompany sc ON sc.SvCompanyId = ap.SvCompanyId
              INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
              INNER JOIN sv_GblAnswerTypes gat ON sv.PrimaryGblAnswerTypeId = gat.GblAnswerTypeId
              WHERE sp.AbleSvCompanyId = @SvCompanyId
                AND sv.SurveyTypeCode = @SurveyTypeCode
                AND sp.Completed IS NOT NULL
                {(onlySurveysWithRptQnGroupId != null // If RptQnGroupId specified, only include surveys with that qn group.
                  ? "AND EXISTS (SELECT NULL FROM sv_360_Questions sq WHERE sv.sv_id = sq.SurveyId AND sq.RptQnGroupId = @RptQnGroupId)"
                  : "")}
                {(gblAnswerTypeIdOrNullForAll != null // If GblAnswerTypeId specified, ...
                  ? "AND gat.GblAnswerTypeId = @GblAnswerTypeId"
                  : "")}
              GROUP BY
                {(companyIdOrNullForAll == null ? "" : "sc.SvCompanyId, sc.CompanyName,")}
                gat.GblAnswerTypeId, gat.GblAnswerTypeDescr
            )
            SELECT
              {(companyIdOrNullForAll == null ? "NULL AS SvCompanyId, NULL AS CompanyName" : "stats.SvCompanyId, stats.CompanyName")},
              stats.GblAnswerTypeId, stats.GblAnswerTypeDescr,
              stats.ProgramCount, stats.SurveyCount, stats.SampleSurveyId,
              stats.SelfLatestCompleted, stats.CoacheeCount,
              stats.SelfAllCount, stats.SelfPreCount, stats.SelfPostCount,
              stats.RaterAllCount, stats.RaterPreCount, stats.RaterPostCount,
              sc.MinScore, sc.MaxScore
            FROM stats
            CROSS APPLY (
              SELECT MIN(sc.ValueScore) AS MinScore,
                     MAX(sc.ValueScore) AS MaxScore
              FROM sv_360_AnswerTypes sat
              INNER JOIN sv_360_Codes sc ON sat.AnswerTypeId = sc.AnswerTypeId
              WHERE sat.SurveyId = stats.SampleSurveyId
                AND sat.GblAnswerTypeId = stats.GblAnswerTypeId
            ) AS sc
            ORDER BY stats.GblAnswerTypeId;",
            dr => {
              statsList.Add(new SurveyStats(
                companyId: dr.GetInt("SvCompanyId"),
                companyName: dr.GetString("CompanyName"),
                projectId: 0, //dr.GetIntOrNull("ProjectId"),
                projectJobNumber: projectJobNumbersOrNullForAll,
                programJobIds: programJobIdsOrNullForAll,
                programCount: dr.GetInt("ProgramCount"),
                surveyTypeCode: surveyTypeCode,
                gblAnswerTypeId: dr.GetInt("GblAnswerTypeId"),
                gblAnswerTypeDescr: dr.GetString("GblAnswerTypeDescr"),
                rptQnGroupId: onlySurveysWithRptQnGroupId,
                surveyCount: dr.GetInt("SurveyCount"),
                sampleSurveyId: dr.GetInt("SampleSurveyId"),
                selfLatestCompleted: dr.GetDateTimeOrNull("SelfLatestCompleted"),
                selfAllCount: dr.GetInt("SelfAllCount"),
                selfPreCount: dr.GetInt("SelfPreCount"),
                selfPostCount: dr.GetInt("SelfPostCount"),
                raterAllCount: dr.GetInt("RaterAllCount"),
                raterPreCount: dr.GetInt("RaterPreCount"),
                raterPostCount: dr.GetInt("RaterPostCount"),
                coacheeCount: dr.GetInt("CoacheeCount"),
                scaleMinScore: dr.GetInt("MinScore"),
                scaleMaxScore: dr.GetInt("MaxScore")
              ));
            },
            NewSqlParameter("SvCompanyId", companyIdOrNullForAll),
            NewSqlParameter("ProjectJobNumbers", projectJobNumbersOrNullForAll),
            NewSqlParameter("ProgramJobIds", programJobIdsOrNullForAll.ToStringList("")),
            NewSqlParameter("SurveyTypeCode", surveyTypeCode),
            NewSqlParameter("RptQnGroupId", onlySurveysWithRptQnGroupId),
            NewSqlParameter("GblAnswerTypeId", gblAnswerTypeIdOrNullForAll)
          );

          return statsList;
        }

        public static bool GetHasPrePost(int companyId) {

          return GetScalarQueryInt($@"
            SELECT IIF(EXISTS (
              SELECT 1
              FROM sv_SurveyGblQnScores gqs
              INNER JOIN sv_Survey sv ON sv.sv_id = gqs.SurveyId
              WHERE sv.SvCompanyId = @CompanyId
                AND gqs.ScoreCountSelfPre > 0
            ), 1, 0)",
            NewSqlParameter("CompanyId", companyId)
          ) == 1;
        }

        public class SurveyStats {

          public int? SvCompanyId { get; private set; }
          public string CompanyName { get; private set; }
          public int? ProjectId { get; private set; }
          public string ProjectJobNumber { get; private set; }
          public List<int> ProgramJobIds { get; private set; }
          public int ProgramCount { get; private set; }
          public string SurveyTypeCode { get; private set; }
          public int GblAnswerTypeId { get; private set; }
          public string GblAnswerTypeDescr { get; private set; }
          public int? RptQnGroupId { get; private set; }

          public int SurveyCount { get; private set; }
          public int SampleSurveyId { get; private set; }
          public int ScaleMinScore { get; private set; }
          public int ScaleMaxScore { get; private set; }
          public DateTime? SelfLatestCompleted { get; private set; }
          public int CoacheeCount { get; private set; }
          public int SelfAllCount { get; private set; }
          public int SelfPreCount { get; private set; }
          public int SelfPostCount { get; private set; }
          public int RaterAllCount { get; private set; }
          public int RaterPreCount { get; private set; }
          public int RaterPostCount { get; private set; }

          public bool HasSelfs => SelfAllCount > 0;
          public bool HasRaters => RaterAllCount > 0;
          public bool HasPreSurvey => SelfPreCount > 0 || RaterPreCount > 0;

          internal SurveyStats(

            int? companyId, string companyName, int? projectId, string projectJobNumber, List<int> programJobIds, int programCount,
            string surveyTypeCode, int gblAnswerTypeId, string gblAnswerTypeDescr, int? rptQnGroupId,

            int surveyCount, int sampleSurveyId,
            int scaleMinScore, int scaleMaxScore,
            int coacheeCount, DateTime? selfLatestCompleted,
            int selfAllCount, int selfPreCount, int selfPostCount,
            int raterAllCount, int raterPreCount, int raterPostCount) {

            SvCompanyId = companyId;
            CompanyName = companyName;
            ProjectId = projectId;
            ProjectJobNumber = projectJobNumber;
            ProgramJobIds = programJobIds.NullIfEmpty();
            ProgramCount = programCount;
            SurveyTypeCode = surveyTypeCode;
            GblAnswerTypeId = gblAnswerTypeId;
            GblAnswerTypeDescr = gblAnswerTypeDescr;
            SurveyCount = surveyCount;
            SampleSurveyId = sampleSurveyId;
            RptQnGroupId = rptQnGroupId;
            ScaleMinScore = scaleMinScore;
            ScaleMaxScore = scaleMaxScore;
            CoacheeCount = coacheeCount;
            SelfLatestCompleted = selfLatestCompleted;
            SelfAllCount = selfAllCount;
            SelfPreCount = selfPreCount;
            SelfPostCount = selfPostCount;
            RaterAllCount = raterAllCount;
            RaterPreCount = raterPreCount;
            RaterPostCount = raterPostCount;
          }

          internal void SetMinMaxScore(int minScore, int maxScore) {
            ScaleMinScore = minScore;
            ScaleMaxScore = maxScore;
          }
        }

        public class MonthlyProgress {

          public string SurveyTypeCode { get; private set; }
          public string ProgressChartLineColor { get; private set; }
          public int Year { get; private set; }
          public int Month { get; private set; }
          public int MonthIndex { get; private set; }
          public decimal ScoreAvg { get; private set; }

          public MonthlyProgress(string surveyTypeCode, string progressChartLineColor, int year, int month, int monthIndex, decimal scoreAvg) {
            SurveyTypeCode = surveyTypeCode;
            ProgressChartLineColor = progressChartLineColor;
            Year = year;
            Month = month;
            MonthIndex = monthIndex;
            ScoreAvg = scoreAvg;
          }
        }

        public static List<MonthlyProgress> GetMonthlyProgress(int surveyCompanyId, DateTime startDate, DateTime endDate) {

          var result = new List<MonthlyProgress>();

          Query($@"

            SELECT
              sv.SurveyTypeCode, st.ProgressChartLineColor,
              YEAR(sp.Completed) AS Year, MONTH(sp.Completed) AS Month,
              DATEDIFF(month, @StartDate, sp.Completed) AS MonthIndex,
              AVG(sc.ValueScore) AS ScoreAvg
            FROM sv_Survey sv
            INNER JOIN sv_360_Participants sp ON sp.SurveyId = sv.sv_id
            INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
            INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
            INNER JOIN al_SurveyType st ON st.SurveyTypeCode = sv.SurveyTypeCode
            INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
            WHERE sp.AbleSvCompanyId = @SurveyCompanyId
              AND st.ProgressChartLineColor IS NOT NULL
              AND sp.Completed BETWEEN @StartDate AND @EndDate
              AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
            GROUP BY
              sv.SurveyTypeCode, st.ProgressChartLineColor,
              YEAR(sp.Completed), MONTH(sp.Completed),
              DATEDIFF(month, @StartDate, sp.Completed)
            ORDER BY sv.SurveyTypeCode, YEAR(sp.Completed), MONTH(sp.Completed);",

            dr => {
              result.Add(new MonthlyProgress(
                surveyTypeCode: dr.GetString("surveyTypeCode"),
                progressChartLineColor: dr.GetString("ProgressChartLineColor"),
                year: dr.GetInt("Year"),
                month: dr.GetInt("Month"),
                monthIndex: dr.GetInt("MonthIndex"),
                scoreAvg: dr.GetDecimal("ScoreAvg")
              ));
            },

            NewSqlParameter("StartDate", startDate),
            NewSqlParameter("EndDate", endDate),
            NewSqlParameter("SurveyCompanyId", surveyCompanyId)
          );

          return result;
        }

      }
    }
  }
}
