using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class OrgReports {

      public const int Max_Previous_Surveys = 3; // Surveys previous to latest to report on (i.e. max 4 surveys in total)
      private const string CacheKey_Results_CurrentSurvey = "OrgReports.Results.CurrentSurvey";
      private const string CacheKey_Results_CurrentSurveyParams = "OrgReports.Results.CurrentSurveyParams";

      private const int NSA_Archive_ResponseCount = 10772;
      private static List<NSA_Archive_Response> NSA_Archive_Totals; // For Q's 1-25


      static OrgReports() {


        // Pre-existing NSA results for the first 25 IOI questions (GblQuestionId's 1-25) prior to the online system existing.
        // This gets added to *unfiltered* benchmark data (i.e. not when benchmarks are filtered).
        NSA_Archive_Totals = new List<NSA_Archive_Response> {
          new NSA_Archive_Response { GblQuestionId = 1   , ResponseSum = 53 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 2   , ResponseSum = 52 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 3   , ResponseSum = 48 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 4   , ResponseSum = 23 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 5   , ResponseSum = 30 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 6   , ResponseSum = 29 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 7   , ResponseSum = 47 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 8   , ResponseSum = 51 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 9   , ResponseSum = 48 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 10  , ResponseSum = 22 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 11  , ResponseSum = 34 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 12  , ResponseSum = 13 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 13  , ResponseSum = 43 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 14  , ResponseSum = 26 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 15  , ResponseSum = 27 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 16  , ResponseSum = 19 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 17  , ResponseSum = 26 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 18  , ResponseSum = 48 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 19  , ResponseSum = 37 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 20  , ResponseSum = 33 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 21  , ResponseSum = 59 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 22  , ResponseSum = 33 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 23  , ResponseSum = 35 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 24  , ResponseSum = 60 * NSA_Archive_ResponseCount },
          new NSA_Archive_Response { GblQuestionId = 25  , ResponseSum = 70 * NSA_Archive_ResponseCount }
        };

      } // constructor

      // eBenchType value must match benchmarkOptions[] index.
      // Note internally "industry" is the same thing as "sector".
      public enum eBenchType { Global = 0, Sector = 1, Organisation = 2 };
      public static Dictionary<eBenchType, string> BenchTypeAbbrev =
        new Dictionary<eBenchType, string>() {
          { eBenchType.Global, "Global" },
          { eBenchType.Sector, "Ind" }, // "Industry" - same thing as "sector".
          { eBenchType.Organisation, "Org" }
        };

      struct NSA_Archive_Response {
        public int GblQuestionId;
        public int ResponseSum;
      }

      public static List<QuestionInfo> GetStandardResults(GetStandardResultsParams gsrParams) {

        if (gsrParams == null) throw new ArgumentException("gsrParams can't be null.");

        // See if results object and previous parameters are in cache.
        var gsrResults = AppCache.Get<List<QuestionInfo>>(CacheKey_Results_CurrentSurvey);
        var gsrParamsCached = AppCache.Get<GetStandardResultsParams>(CacheKey_Results_CurrentSurveyParams);

        // Ensure cached objects exist.
        if (gsrParamsCached == null || gsrResults == null) {

          gsrParamsCached = new GetStandardResultsParams();
          AppCache.Set(CacheKey_Results_CurrentSurveyParams, gsrParamsCached, new TimeSpan(1, 0, 0));

          gsrResults = new List<QuestionInfo>();
          AppCache.Set(CacheKey_Results_CurrentSurvey, gsrResults, new TimeSpan(1, 0, 0));

        } else {

          // If all params are same, just return cached data.
          if (gsrParams.IsSameAs(gsrParamsCached)) {
            return gsrResults;
          } else {
          }
        }

        // Check which params have changed and load the related data.
        // e.g. changed benchmark params trigger re-loading of benchmark data,
        // changed orgLevels param triggers reloading of current survey data,
        // changed surveyId param triggers reloading of all data.


        if (gsrResults.Count == 0 || gsrParams.SurveyId != gsrParamsCached.SurveyId || gsrParams.DivisionCodeIds != gsrParamsCached.DivisionCodeIds)
          LoadCurrentSurveyData(gsrParams, gsrResults);

        if (gsrParams.SurveyId != gsrParamsCached.SurveyId || gsrParams.OrgCompanyId != gsrParamsCached.OrgCompanyId) {
          LoadPreviousSurveyData(gsrParams, gsrResults);
        }

        if (gsrParams.SurveyId != gsrParamsCached.SurveyId || gsrParams.BenchCompanyId != gsrParamsCached.BenchCompanyId || gsrParams.BenchSectorId != gsrParamsCached.BenchSectorId) {
          if (gsrParams.BenchSectorId == null && gsrParams.BenchCompanyId == null) {
            // No filters, so load standard NSA data from summary tables.
            LoadNSABenmarkData(gsrParams, gsrResults);
          } else {
            LoadFilteredBenchmarkData(gsrParams, gsrResults);
          }
        }

        // Update cached params.
        gsrParamsCached.CopyFrom(gsrParams);

        return gsrResults;
      }

      private static void LoadCurrentSurveyData(GetStandardResultsParams gsrParams, List<QuestionInfo> gsrResults) {

        Common.Query(@"

          WITH q
          AS (
            SELECT s.SvCompanyId, q.QuestionId, q.GblQuestionId, q.Sort, q.AutoNumber, q.SurveyId,
              q.QuestionTextFull1 AS QnText,
              t.GblAnswerTypeId,
              cd1.Code AS Dim1Code, cd1.Value AS Dim1Heading,
              cd3.Code AS Dim3Code, cd3.Value AS Dim3Heading,
              cd4.Code AS Dim4Code, cd4.Value AS Dim4Heading
            FROM sv_Survey s
            INNER JOIN sv_360_Questions q ON s.sv_id = q.SurveyId
            INNER JOIN sv_360_AnswerTypes t ON q.AnswerTypeId = t.AnswerTypeId AND t.GblAnswerTypeId = @GblAnswerTypeId
            INNER JOIN sv_360_AnswerTypes td1 ON s.sv_id = td1.SurveyId AND td1.AnswerTypeDescr = 'report section' -- Main headings
            LEFT OUTER JOIN sv_360_Codes cd1 ON td1.AnswerTypeId = cd1.AnswerTypeId AND cd1.Code = q.Dimension1
            INNER JOIN sv_360_AnswerTypes td3 ON s.sv_id = td3.SurveyId AND td3.AnswerTypeDescr = 'dimension' -- Quadrants
            LEFT OUTER JOIN sv_360_Codes cd3 ON td3.AnswerTypeId = cd3.AnswerTypeId AND cd3.Code = q.Dimension3
            LEFT OUTER JOIN sv_360_AnswerTypes td4 ON s.sv_id = td4.SurveyId AND td4.AnswerTypeDescr = 'drivers' -- Drivers
            LEFT OUTER JOIN sv_360_Codes cd4 ON td4.AnswerTypeId = cd4.AnswerTypeId AND cd4.Code = q.Dimension4
            WHERE s.sv_id = @SurveyId
              AND q.IsHeading = 0
          ),

          p
          AS (
            SELECT p.PartId, p.SurveyId
            FROM sv_360_Participants p
            INNER JOIN sv_Answers a ON p.PartId = a.ParticipantId
            INNER JOIN sv_360_Questions q ON a.QuestionId = q.QuestionId
            INNER JOIN sv_360_AnswerTypes t ON q.AnswerTypeId = t.AnswerTypeId
            WHERE p.SurveyId = @SurveyId
              AND t.SurveyId = @SurveyId
              AND p.Completed IS NOT NULL
              AND t.AnswerTypeDescr = 'division' -- Note we're assuming survey has a Division question, most Org surveys do.
              " + (gsrParams.DivisionCodeIds.IsNullOrEmpty() ? "" // Selected Division codes.
                  : "AND a.CodeId IN (" + gsrParams.DivisionCodeIds + ")") + @"
          )

          SELECT q.*, SrvAns.SrvScore, SrvAns.SrvSum, SrvAns.SrvCount
          FROM q
          OUTER APPLY (
            -- Survey Scores
            SELECT
              ROUND(AVG(ac.ValueScore), 0) AS SrvScore,
              SUM(ac.ValueScore) AS SrvSum,
              COUNT(ac.ValueScore) AS SrvCount
            FROM p
            INNER JOIN sv_Answers a ON a.ParticipantId = p.PartId AND a.QuestionId = q.QuestionId
            INNER JOIN sv_360_Codes ac ON ac.CodeId = a.CodeId
            INNER JOIN sv_360_AnswerTypes at ON at.AnswerTypeId = ac.AnswerTypeId
          ) AS SrvAns

          ORDER BY q.Sort",

          dr => {

            double? ansSum = dr.GetDoubleOrNull("SrvSum");
            int? ansCount = dr.GetIntOrNull("SrvCount");
            int? ansAvg = ansSum == null || ansCount == null || ansCount == 0 ? null : (int?)((double)(ansSum / ansCount)).RoundAwayFromZero();
            bool foundRow = false;
            int questionId = dr.GetInt("QuestionId");

            foreach (var row in gsrResults) {
              if (row.QuestionId == questionId) {
                // Replace existing result.
                foundRow = true;
                row.Scores.ScoreSelf.SetScore(ansSum, ansCount);
                break;
              }
            }

            if (!foundRow) {
              // Add new result item.
              gsrResults.Add(new QuestionInfo(
                dr.GetIntOrDefault("Dim1Code", 0),
                dr.GetString("Dim1Heading", ""),
                dr.GetIntOrDefault("Dim3Code", 0),
                dr.GetString("Dim3Heading", ""),
                dr.GetIntOrDefault("Dim4Code", 0),
                dr.GetString("Dim4Heading", ""),
                dr.GetInt("QuestionId"),
                dr.GetInt("GblQuestionId"),
                dr.GetInt("GblAnswerTypeId"),
                dr.GetInt("Sort"),
                dr.GetInt("AutoNumber"),
                dr.GetString("QnText", ""),
                new Questions.QuestionScores(new Questions.ScoreParam(ansSum, ansCount), null, null)
              ));
            }
          },
          Common.NewSqlParameter("SurveyId", gsrParams.SurveyId),
          Common.NewSqlParameter("GblAnswerTypeId", OrgSurveys.Standard_GblAnswerTypeId)
        );
      }

      private static void LoadPreviousSurveyData(GetStandardResultsParams gsrParams, List<QuestionInfo> gsrResults) {

        // Note LoadCurrentSurveyData() needs to be run first to populate the results list.
        // This function finds matching GblQuestionIds and updates them with the previous surveys' results.

        // First clear results.
        foreach (var row in gsrResults) {
          row.Scores.ScorePreviousSelf.SetScore(0, 0);
        }

        var surveys = OrgSurveys.GetLatestCompletedSurveys(gsrParams.OrgCompanyId, Max_Previous_Surveys);
        if (surveys.Count <= 1) return; // No previous surveys.

        int Prv1SurveyId = surveys.Count < 2 ? 0 : surveys[1].SurveyId;
        int Prv2SurveyId = surveys.Count < 3 ? 0 : surveys[2].SurveyId;
        int Prv3SurveyId = surveys.Count < 4 ? 0 : surveys[3].SurveyId;

        Common.Query(@"
          WITH aPrv AS (
            SELECT
              q.SurveyId,
              q.GblQuestionId,
              SUM(c.ValueScore) AS PrvSum,
              COUNT(c.ValueScore) AS PrvCount
            FROM sv_Answers a
            INNER JOIN sv_360_Participants p ON p.PartId = a.ParticipantId
            INNER JOIN sv_360_Questions q ON q.QuestionId = a.QuestionId
            INNER JOIN sv_360_Codes c ON c.CodeId = a.CodeId
            WHERE p.SurveyId IN(@Prv1SurveyId, @Prv2SurveyId, @Prv3SurveyId)
              AND p.Completed IS NOT NULL
            GROUP BY q.SurveyId, q.GblQuestionId
          )
          SELECT
            qSrv.AutoNumber,
            qSrv.GblQuestionId,
            aPrv1.PrvSum AS Prv1Sum, aPrv1.PrvCount AS Prv1Count,
            aPrv2.PrvSum AS Prv1Sum, aPrv2.PrvCount AS Prv2Count,
            aPrv3.PrvSum AS Prv1Sum, aPrv3.PrvCount AS Prv3Count
          FROM sv_360_Questions qSrv
            LEFT OUTER JOIN aPrv aPrv1 ON aPrv1.SurveyId = @Prv1SurveyId AND qSrv.GblQuestionId = aPrv1.GblQuestionId
            LEFT OUTER JOIN aPrv aPrv2 ON aPrv2.SurveyId = @Prv2SurveyId AND qSrv.GblQuestionId = aPrv2.GblQuestionId
            LEFT OUTER JOIN aPrv aPrv3 ON aPrv3.SurveyId = @Prv3SurveyId AND qSrv.GblQuestionId = aPrv3.GblQuestionId
          WHERE qSrv.SurveyId = @SurveyId
            AND qSrv.IsHeading = 0
          ORDER BY qSrv.Sort;",

          dr => {

            if (dr.IsDBNull("GblQuestionId")) return;

            int questionId = dr.GetInt("GblQuestionId");
            double? ansSum = dr.GetDoubleOrNull("Prv1Sum");
            int? ansCount = dr.GetIntOrNull("Prv1Count");
            int? ansAvg = ansSum == null || ansCount == null || ansCount == 0 ? null : (int?)((double)(ansSum / ansCount)).RoundAwayFromZero();

            // Go thru existing results to find matching GblQuestionId and replace the result.
            foreach (var row in gsrResults) {
              if (row.GblQuestionId == questionId) {
                row.Scores.ScorePreviousSelf.SetScore(ansSum, ansCount);
                // TODO:
                // row.Scores.ScorePrevious2Self.SetScore(dr.GetDoubleOrNull("Prv2Sum"), dr.GetIntOrNull("Prv2Count"));
                // row.Scores.ScorePrevious3Self.SetScore(dr.GetDoubleOrNull("Prv3Sum"), dr.GetIntOrNull("Prv3Count"));
                break;
              }
            }
          },
          Common.NewSqlParameter("SurveyId", gsrParams.SurveyId),
          Common.NewSqlParameter("Prv1SurveyId", Prv1SurveyId),
          Common.NewSqlParameter("Prv2SurveyId", Prv2SurveyId),
          Common.NewSqlParameter("Prv3SurveyId", Prv3SurveyId)
        );
      }

      private static void LoadNSABenmarkData(GetStandardResultsParams gsrParams, List<QuestionInfo> gsrResults) {


        // First clear results.
        foreach (var row in gsrResults) {
          row.Scores.ScoreBenchSelf.SetScore(0, 0);
        }

        Common.Query(@"

          SELECT q.GblQuestionId, nsa.ScoreSum, nsa.ScoreCount
          FROM sv_360_Questions q
          INNER JOIN sv_NSA_LikertGblQnScores nsa ON q.GblQuestionId = nsa.GblQuestionId
          WHERE q.SurveyId = @SurveyId
          ORDER BY q.Sort;",

          dr => {
            int questionId = dr.GetInt("GblQuestionId");
            double? scoreSum = dr.GetDoubleOrNull("ScoreSum");
            int? scoreCount = dr.GetIntOrNull("ScoreCount");
            int? scoreAvg = scoreSum == null || scoreCount == null || scoreCount == 0 ? null : (int?)((double)(scoreSum / scoreCount)).RoundAwayFromZero();

            // Go thru existing results to find matching GblQuestionId and replace the result.
            foreach (var row in gsrResults) {
              if (row.GblQuestionId == questionId) {
                row.Scores.ScoreBenchSelf.SetScore(scoreSum, scoreCount);
                break;
              }
            }
          },
          Common.NewSqlParameter("SurveyId", gsrParams.SurveyId)
        );

        // Add the NSA "archive" results to Qns 1-25.
        // TODO: Decide if table sv_NSA_LikertGblQnScores is going to include ("merge")
        // the 25 NSA archive results or not when it starts being auto-recalculated. Currently it doesn't.
        foreach (var nsaData in NSA_Archive_Totals) {
          foreach (var row in gsrResults) {
            if (row.GblQuestionId == nsaData.GblQuestionId) {
              row.Scores.ScoreBenchSelf.AccumulateScore(nsaData.ResponseSum, NSA_Archive_ResponseCount);
              break;
            }
          }
        }

      }

      private static void LoadFilteredBenchmarkData(GetStandardResultsParams gsrParams, List<QuestionInfo> gsrResults) {

        // Note LoadCurrentSurveyData() needs to be run first to populate the results list.
        // This function finds matching GblQuestionIds and updates them with the benchmark results.


        // First clear results.
        foreach (var row in gsrResults) {
          row.Scores.ScoreBenchSelf.SetScore(0, 0);
        }

        Common.Query($@"

          WITH aPrv AS (
            SELECT  q.GblQuestionId, SUM(c.ValueScore) AS PrvSum, COUNT(c.ValueScore) AS PrvCount
            FROM sv_Answers a
            INNER JOIN sv_360_Participants p ON p.PartId = a.ParticipantId
            INNER JOIN sv_360_Questions q ON q.QuestionId = a.QuestionId
            INNER JOIN sv_360_Codes c ON c.CodeId = a.CodeId
            INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
            WHERE s.sv_ReportType = @ReportType_IOS
              AND (@CompanyId IS NULL OR @CompanyId = s.SvCompanyId)
              AND (@SectorId IS NULL OR @SectorId = s.sv_SectorId)
              AND p.Completed IS NOT NULL
            GROUP BY q.GblQuestionId
          )
          SELECT qSrv.AutoNumber, qSrv.GblQuestionId, aPrv.PrvSum, aPrv.PrvCount
          FROM sv_360_Questions qSrv
            LEFT OUTER JOIN aPrv ON qSrv.GblQuestionId = aPrv.GblQuestionId
          WHERE qSrv.SurveyId = @SurveyId
            AND qSrv.IsHeading = 0
          ORDER BY qSrv.Sort;",

          dr => {
            if (dr.IsDBNull("GblQuestionId")) return;

            int questionId = dr.GetInt("GblQuestionId");
            double? ansSum = dr.GetDoubleOrNull("PrvSum");
            int? ansCount = dr.GetIntOrNull("PrvCount");
            int? ansAvg = ansSum == null || ansCount == null || ansCount == 0 ? null : (int?)((double)(ansSum / ansCount)).RoundAwayFromZero();

            // Go thru existing results to find matching GblQuestionId and replace the result.
            foreach (var row in gsrResults) {
              if (row.GblQuestionId == questionId) {
                row.Scores.ScoreBenchSelf.SetScore(ansSum, ansCount);
                break;
              }
            }
          },
          Common.NewSqlParameter("ReportType_IOS", ConfigHelper.ReportTypes.IOS),
          Common.NewSqlParameter("SurveyId", gsrParams.SurveyId),
          Common.NewSqlParameter("CompanyId", gsrParams.BenchCompanyId),
          Common.NewSqlParameter("SectorId", gsrParams.BenchSectorId)
        );
      }

      // Return list of dimension codes in the preferred sorting order (RptOrder1 column in sv_360_Codes)
      public static List<int> GetDimensionCodeOrder(int surveyId, ValidDimensions dimension) {

        var codeList = new List<int>();

        Common.Query(@"
          SELECT c.Code
          FROM sv_360_Codes c
          INNER JOIN sv_360_AnswerTypes t ON c.AnswerTypeId = t.AnswerTypeId " +
          (dimension == ValidDimensions.ReportSections
            // For "Report Sections" only include codes that have questions in the current survey, and exclude code 1 (IOI).
            ? @"CROSS APPLY (
                SELECT TOP 1 q.QuestionId
                FROM sv_360_Questions q
                WHERE q.SurveyId = t.SurveyId
                AND q.Dimension1 = c.Code
                AND c.Code > 1
              ) AS q "
            : "") + @"
          WHERE t.SurveyId = @SurveyId
            AND t.AnswerTypeDescr = @Dimension
          ORDER BY IIF(c.RptOrder1 > 0, c.RptOrder1, c.Code)",

          dr => {
            codeList.Add(dr.GetInt("Code"));
          },
          Common.NewSqlParameter("SurveyId", surveyId),
          Common.NewSqlParameter("Dimension", GetAnsTypeDescrFromDimension(dimension))
        );

        return codeList;
      }

      public static string GetAnsTypeDescrFromDimension(ValidDimensions dimension) {

        string rtn = "";

        switch (dimension) {
          case ValidDimensions.Culture:
            rtn = "Existing & Preferred Culture";
            break;
          case ValidDimensions.Drivers:
            rtn = "Drivers";
            break;
          case ValidDimensions.Quadrants:
            rtn = "Dimension";
            break;
          case ValidDimensions.ReportSections:
            rtn = "Report Section";
            break;
        }

        return rtn;
      }

      public enum ValidDimensions : int {
        ReportSections = 1,
        Culture = 2,
        Quadrants = 3,
        Drivers = 4
      }

      public class DimensionInfo {
        public ValidDimensions Dimension;
        public int Code;
        public string Title;
      }

      public class GetStandardResultsParams {

        public int SurveyId;
        public int OrgCompanyId;
        public string DivisionCodeIds;
        public int? BenchCompanyId;
        public int? BenchSectorId;

        public GetStandardResultsParams() {
          CopyFrom(null);
        }

        public GetStandardResultsParams(int surveyId, int orgCompanyId, string divisionCodeIds, int? benchCompanyId, int? benchSectorId) {
          SurveyId = surveyId;                // Survey to get results for.
          OrgCompanyId = orgCompanyId;        // CompanyId for which to get latest completed surveys (i.e. survey's own CompanyId).
          DivisionCodeIds = divisionCodeIds;  // Comma-list of division code Ids to use or null/blank for all.
          BenchCompanyId = benchCompanyId;    // Company Id to use for benchmark (usually same company as survey).
          BenchSectorId = benchSectorId;      // Sector Id to use for benchmark (usually same sector as survey).
        }

        public bool IsSameAs(GetStandardResultsParams otherParams) {

          if (otherParams == null) return false;

          return (
            this.SurveyId == otherParams.SurveyId
            && this.DivisionCodeIds == otherParams.DivisionCodeIds
            && this.OrgCompanyId == otherParams.OrgCompanyId
            && this.BenchCompanyId == otherParams.BenchCompanyId
            && this.BenchSectorId == otherParams.BenchSectorId);
        }

        public void CopyFrom(GetStandardResultsParams otherParams) {

          if (otherParams == null) {
            // Note init with values that are "out of bounds" so fresh results will definitely be loaded.
            this.SurveyId = -1;
            this.DivisionCodeIds = "-";
            this.OrgCompanyId = -1;
            this.BenchCompanyId = -1;
            this.BenchSectorId = -1;
          } else {
            this.SurveyId = otherParams.SurveyId;
            this.DivisionCodeIds = otherParams.DivisionCodeIds;
            this.OrgCompanyId = otherParams.OrgCompanyId;
            this.BenchCompanyId = otherParams.BenchCompanyId;
            this.BenchSectorId = otherParams.BenchSectorId;
          }
        }
      }

      public class QuestionInfo {

        public int ReportSectionCode { get; private set; }
        public string ReportSectionTitle { get; private set; }
        public int QuadrantCode { get; private set; }
        public string QuadrantTitle { get; private set; }
        public int DriverCode { get; private set; }
        public string DriverTitle { get; private set; }
        public int QuestionId { get; private set; }
        public int GblQuestionId { get; private set; }
        public int GblAnswerTypeId { get; private set; }
        public int Sort { get; private set; }
        public int AutoNumber { get; private set; }
        public string QuestionText { get; private set; }
        public Questions.QuestionScores Scores { get; set; }

        public QuestionInfo(
          int reportSectionCode,
          string reportSectionTitle,
          int quadrantCode,
          string quadrantTitle,
          int driverCode,
          string driverTitle,
          int questionId,
          int gblQuestionId,
          int gblAnswerTypeId,
          int sort,
          int autoNumber,
          string questionText,
          // providing scores is optional.
          Questions.QuestionScores scores = null
        ) {
          ReportSectionCode = reportSectionCode;
          ReportSectionTitle = reportSectionTitle;
          QuadrantCode = quadrantCode;
          QuadrantTitle = quadrantTitle;
          DriverCode = driverCode;
          DriverTitle = driverTitle;
          QuestionId = questionId;
          GblQuestionId = gblQuestionId;
          GblAnswerTypeId = gblAnswerTypeId;
          Sort = sort;
          AutoNumber = autoNumber;
          QuestionText = questionText;
          Scores = scores;
          if (Scores == null) Scores = new Questions.QuestionScores();
        }

        public void AccumulateScores(QuestionInfo qnInfo) {
          if (qnInfo != null) this.Scores.AccumulateScores(qnInfo.Scores);
        }

      }

    }
  }
}
