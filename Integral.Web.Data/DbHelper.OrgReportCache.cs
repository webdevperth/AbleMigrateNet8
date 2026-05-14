using System;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public class OrgReportsCached {

      private const int Cache_Lifetime_Minutes = 60; // Time after which cache items expire.
      private const string CacheKey_Results_Prefix = "OrgReports.ResultsCached.SurveyId_";
      private const string CacheKey_Separator = "-";

      // NOTE: Initially used HttpRuntime.Cache as the memory cache, but on the live server it was expiring immediately,
      //       due to pressure on server memory. So for now, just making the cache a static application object,
      //       which will keep the data until app pool recycles. Data is fairly small so it's fine for now.

      // ReportData cache key is the survey UID.
      private static Dictionary<string, ReportData> _ReportDataCache;

      // Constructor, creates static cache object.
      static OrgReportsCached() {

        _ReportDataCache = new Dictionary<string, ReportData>(); // Indexed by "<SurveyUId>_<SessionId>", each ReportData is the memory cache for that survey.

      }

      public class GetReportDataParams {
        public string SurveyUId { get; private set; }
        public string ParticipantUId { get; private set; }
        public string SessionId { get; private set; }
        public ReportFilters RequestedFilters { get; private set; }
        public GetReportDataParams(string surveyUId, string participantUId, string sessionId, ReportFilters requestedFilters) {
          this.SurveyUId = surveyUId;
          this.ParticipantUId = participantUId;
          this.SessionId = sessionId;
          this.RequestedFilters = requestedFilters;
        }
      }

      public class ReportParticipantInfo {
        public string PartUniqueId { get; private set; }
        public int PartId { get; private set; }
        public bool CanAccessTextResponses { get; private set; }
        public ReportParticipantInfo(int participantId, string participantUniqueId, bool canAccessTextResponses) {
          this.PartId = participantId;
          this.PartUniqueId = participantUniqueId;
          this.CanAccessTextResponses = canAccessTextResponses;
        }
      }

      public static ReportParticipantInfo GetReportParticipantInfo(string surveyUID, string participantUID) {

        if (surveyUID.IsNullOrEmpty()) return null;

        // Note the column IsSelfMgr is used to indicate that the participant can view the
        // open text responses. Most participants only see coded stats, not the actual text.
        // IsSelfMgr = 0: Only view coded info.
        // IsSelfMgr = 1: Also view individual text responses.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

          SELECT TOP 1 p.PartId, p.IsSelfMgr
          FROM sv_360_Participants p
          INNER JOIN sv_Survey s ON s.sv_id = p.SurveyId
          WHERE s.sv_uniqueid = @SurveyUId
            AND (@PartUId = '' OR @PartUId <> '' AND p.UniqueId = @PartUId)

          ", conn)) {
            cmd.Parameters.AddVarChar("@SurveyUId", 50, surveyUID);
            cmd.Parameters.AddVarChar("@PartUId", 50, participantUID.EmptyIfNull());
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              if (dr.Read()) {
                // Allow view open text only if specific participant given has IsSelfMgr = 1
                bool canViewOpenText = true; // <-- HACK ALERT (for demos) (!participantUID.IsNullOrEmpty() && dr.GetBoolFromInt("IsSelfMgr"));
                return new ReportParticipantInfo(dr.GetInt("PartId"), participantUID, canViewOpenText);
              }
            }
          }
        }
        return null;

      }

      public static ReportData GetReportData(GetReportDataParams rdParams, bool clearCachedResults = false) {

        // Get existing or new reportdata object for this survey from the memory cache.
        // This will also load the "static" survey data which doesn't change with filters.
        // That's done in a thread-locked fashion so it's only retrieved once per survey, not per request.

        if (rdParams.SurveyUId.IsNullOrEmpty() || rdParams.SessionId.IsNullOrEmpty()) return null;

        if (clearCachedResults) ReportData_ClearCache(rdParams.SurveyUId); // Force re-read of report data into the cache.

        ReportData reportData = ReportData_LoadFromCache(rdParams.SurveyUId, rdParams.SessionId);

        if (reportData.SurveyInfo == null) return null; // Survey wasn't found.

        // Check if reportData needs to be updated based on changes to the filters.
        //
        // requestedFilters are the NEW filters from the UI (whether changed or unchanged from last time),
        // reportData.reportFilters are the CURRENT applied filters that correspond to the data in the cache.

        var lockObj = new Object();
        lock (lockObj) {
          // lock() ensures the filters aren't checked at the same time by different processes.
          // TODO: Use a TABLELOCK instead of lock() for complete safety.

          // Check if division selection changed, to see if we need to re-read previous survey scores.
          // If divs are selected, scorea are read from the db.
          // If no divs are selected, data is copied over from the "static" scores.

          bool divFilterChanged = reportData.HasDivisionFilterChanged(rdParams.RequestedFilters); // Division ("Org Level") filter changed.
          bool demFilterChanged = reportData.HasDemographicFilterChanged(rdParams.RequestedFilters); // Demographic filter changed.
          bool benchFilterChanged = reportData.HasBenchmarkFilterChanged(rdParams.RequestedFilters);

          if (divFilterChanged) {
            // Reload previous survey data that are affected by division filter.
            // Note that previous survey data is only affected by changes to division filter, not demographics.
            ReportData_LoadPreviousSurveyData(reportData, rdParams.RequestedFilters);
            // Update current filter.
            reportData.ReportFilters.SetDivisionCodeIdsFromString(rdParams.RequestedFilters.GetDivisionCodeIdString());
          }

          if (divFilterChanged || demFilterChanged) {
            // Reload current survey data, which is affected by changes to both division & demographic filters.
            ReportData_LoadCurrentSurveyData(reportData, rdParams.RequestedFilters);
            // Update current filter.
            reportData.ReportFilters.SetDemographicsList(rdParams.RequestedFilters.Demographics);
          }

          // Update benchmark filter. Nothing else to do has they're all already loaded.
          if (benchFilterChanged) {
            reportData.ReportFilters.SetBenchmarkType(rdParams.RequestedFilters.BenchType);
          }

        }

        return reportData;

      }

      public static void ReportData_ClearCache(string surveyUId) {
        bool found;
        do {
          found = false;
          foreach (var cacheItem in _ReportDataCache) {
            if (cacheItem.Key.StartsWith(surveyUId + CacheKey_Separator)) {
              found = true;
              _ReportDataCache.Remove(cacheItem.Key);
              break;
            }
          }
        } while (found == true); // until there are no more for this survey.
      }

      private static string GetCacheKey(string surveyUId, string sessionId) {
        return surveyUId + CacheKey_Separator + sessionId;
      }

      private static ReportData ReportData_LoadFromCache(string surveyUId, string sessionId) {

        ReportData reportData;
        string cacheKey = GetCacheKey(surveyUId, sessionId);

        // Return cached report results if found.
        if (_ReportDataCache.ContainsKey(cacheKey)) return _ReportDataCache[cacheKey];

        // Get report results.

        // Lock so only one thread updates _ReportDataCache here.

        var lockObj1 = new Object();
        lock (lockObj1) {

          // Check again in case another process updated it while we waited for the lock.
          if (_ReportDataCache.ContainsKey(cacheKey)) return _ReportDataCache[cacheKey];

          // While we're here, remove any "expired" cache objects.
          var removeKeys = new List<string>();
          foreach (var rc in _ReportDataCache) {
            if ((DateTime.UtcNow - rc.Value.CreatedUTC).TotalMinutes > Cache_Lifetime_Minutes) removeKeys.Add(rc.Key);
          }
          foreach (string key in removeKeys) _ReportDataCache.Remove(key);

          // Create new ReportData object in cache, and perform initial data gathering.
          reportData = new ReportData(surveyUId, sessionId);
          _ReportDataCache.Add(cacheKey, reportData);
        }

        // From here on we only lock on the current reportData object, freeing other processes to query other surveys.

        // Create/recreate table entries which trace Division back links from the current survey.
        // TEST: This shouldn't be needed here anymore, as the back-link summary tables for IOS surveys
        //       are now updated when updating division info (see survey_qntypes.asp).
        // CreateDivCodeLinks(reportData);

        // Get survey questions along with all the "static" results (that don't change with filters).
        if (reportData.IsStaticSurveyDataLoaded) return reportData;

        var lockObj2 = new Object();
        lock (lockObj2) {

          // Check again.
          if (reportData.IsStaticSurveyDataLoaded) return reportData; // Check again.

          // Load static data including YearlyIOIByLinkLevel benchmarks.
          ReportData_LoadStaticData(reportData);
          reportData.SetStaticSurveyDataLoaded();

          // If survey currently does not have any Division links, create the first one for current survey.
          if (reportData.YearlyIOIByLinkLevel.Count == 0) {
            reportData.YearlyIOIByLinkLevel.Add(
              new YearlyIOIScores(
                reportData.SurveyInfo.SurveyId, reportData.SurveyInfo.CloseDateLocal.Year, null, null, null, null));
          }

          // Initial current data read.
          var dummyFilters = new ReportFilters();
          ReportData_LoadPreviousSurveyData(reportData, dummyFilters);
          ReportData_LoadCurrentSurveyData(reportData, dummyFilters);
        }
        return reportData;

      }

      // -- Load static data.

      private static void ReportData_LoadStaticData(ReportData reportData) {

        if (reportData.ReportFilters == null) throw new ArgumentException("reportData.ReportFilters must be set.");

        // First load current survey info.
        // TODO: Restrict access to surveys owned by logged in user (no login as yet)
        reportData.SurveyInfo_LoadInfo();
        if (reportData.SurveyInfo == null) return; // Survey not found, can't continue.

        // Set list of previous survey ids in order of LinkLevel (0 = current, 1 = previous, 2 = previous to that, etc.).
        SetSurveyIdsByLinkLevel(reportData);

        LikertQuestions_LoadStaticData(reportData); // Must be first.
        YearlyIOIChart_LoadStaticData(reportData);
        DirectorateChart_LoadStaticData(reportData);
        DivisionChart_LoadStaticData(reportData);
        OpenTextThemes_LoadStaticData(reportData);
        OpenText_LoadStaticData(reportData);

      }

      private static void LikertQuestions_LoadStaticData(ReportData reportData) {

        // Get all the Likert question details, plus results from entire survey (no filters applied).
        // This takes a second to run, but only occurs once per survey.
        // The benchmark scores are mostly for the tables, where the selected benchmark indicator is shown for each qn.

        reportData.LikertQuestions.Clear();
        reportData.LikertQuestionByGblQnId.Clear();
        reportData.LikertQuestionBySort.Clear();

        Common.Query(@"

          SELECT
            s.SvCompanyId, s.sv_SectorId,
            q.QuestionId, q.GblQuestionId, q.Sort, q.AutoNumber, q.SurveyId,
            q.QuestionTextFull1 AS QnText,
            q.GblAnswerTypeId,
            q.InputType,
            cd1.Code AS Dim1Code, cd1.Value AS Dim1Heading,
            cd3.Code AS Dim3Code, cd3.Value AS Dim3Heading,
            cd4.Code AS Dim4Code, cd4.Value AS Dim4Heading,
            aSrv.ScoreSum_Srv, aSrv.ScoreCount_Srv,
            aPrev.ScoreSum_Prev, aPrev.ScoreCount_Prev,
            aNSA.ScoreSum_NSA + ISNULL(aIOI.ScoreSum, 0) AS ScoreSum_NSA,
            aNSA.ScoreCount_NSA + ISNULL(aIOI.ScoreCount, 0) AS ScoreCount_NSA,
            aSect.ScoreSum_Sect, aSect.ScoreCount_Sect,
            aOrg.ScoreSum_Org, aOrg.ScoreCount_Org
          FROM sv_Survey s
          INNER JOIN sv_360_Questions q ON s.sv_id = q.SurveyId
          INNER JOIN sv_360_AnswerTypes td1 ON s.sv_id = td1.SurveyId AND td1.AnswerTypeDescr = 'report section' -- Main headings
          LEFT OUTER JOIN sv_360_Codes cd1 ON td1.AnswerTypeId = cd1.AnswerTypeId AND cd1.Code = q.Dimension1
          INNER JOIN sv_360_AnswerTypes td3 ON s.sv_id = td3.SurveyId AND td3.AnswerTypeDescr = 'dimension' -- Quadrants
          LEFT OUTER JOIN sv_360_Codes cd3 ON td3.AnswerTypeId = cd3.AnswerTypeId AND cd3.Code = q.Dimension3
          LEFT OUTER JOIN sv_360_AnswerTypes td4 ON s.sv_id = td4.SurveyId AND td4.AnswerTypeDescr = 'drivers' -- Drivers
          LEFT OUTER JOIN sv_360_Codes cd4 ON td4.AnswerTypeId = cd4.AnswerTypeId AND cd4.Code = q.Dimension4
          LEFT OUTER JOIN sv_NSA_IOIScores_Archive aIOI ON aIOI.GblQuestionId = q.GblQuestionId

          -- overall survey scores
          CROSS APPLY (
            SELECT
              SUM(c.ValueScore) AS ScoreSum_Srv,
              COUNT(c.ValueScore) AS ScoreCount_Srv
            FROM sv_360_Codes c
            INNER JOIN sv_Answers a ON c.CodeId = a.CodeId
            INNER JOIN sv_360_Participants p ON p.PartId = a.ParticipantId AND p.Completed IS NOT NULL
            WHERE a.QuestionId = q.QuestionId
          ) aSrv

          -- overall previous scores
          CROSS APPLY (
            SELECT
              SUM(ab.ScoreSum) AS ScoreSum_Prev,
              SUM(ab.ScoreCount) AS ScoreCount_Prev
            FROM sv_SurveyGblQnScoresByDiv ab
            WHERE ab.SurveyId = s.PreviousSurveyId
            AND ab.GblQuestionId = q.GblQuestionId
          ) aPrev

          -- NSA benchmark (all surveys)
          CROSS APPLY (
            SELECT SUM(ao.ScoreSum) AS ScoreSum_NSA,
              SUM(ao.ScoreCount) AS ScoreCount_NSA
            FROM sv_SurveyGblQnScores ao
            INNER JOIN sv_Survey so ON so.sv_id = ao.SurveyId
            WHERE ao.GblQuestionId = q.GblQuestionId
          ) aNSA

          -- org benchmark
          CROSS APPLY (
            SELECT SUM(ao.ScoreSum) AS ScoreSum_Org,
              SUM(ao.ScoreCount) AS ScoreCount_Org
            FROM sv_SurveyGblQnScores ao
            INNER JOIN sv_Survey so ON so.sv_id = ao.SurveyId
            WHERE so.SvCompanyId = s.SvCompanyId -- surveys in same organisation
              AND ao.GblQuestionId = q.GblQuestionId
          ) aOrg

          -- sector benchmark
          CROSS APPLY (
            SELECT SUM(ab.ScoreSum) AS ScoreSum_Sect,
              SUM(ab.ScoreCount) AS ScoreCount_Sect
            FROM sv_SurveyGblQnScores ab
            INNER JOIN sv_Survey sb ON sb.sv_id = ab.SurveyId
            WHERE sb.sv_SectorId = s.sv_SectorId
              AND ab.GblQuestionId = q.GblQuestionId
          ) aSect

          WHERE s.sv_id = @SurveyId
            AND q.GblAnswerTypeId = @GblAnswerTypeId
            AND q.IsHeading = 0
          ORDER BY q.Sort",

          dr => {

            int? scoreSum_Srv = dr.GetIntOrNull("ScoreSum_Srv");
            int? scoreCount_Srv = dr.GetIntOrNull("ScoreCount_Srv");
            double? scoreAvg_Srv = scoreSum_Srv == null || scoreCount_Srv == null || scoreCount_Srv == 0 ? null : (double)scoreSum_Srv / scoreCount_Srv;
            int questionId = dr.GetInt("QuestionId");

            // Add new result item.
            var newQI = new QuestionInfo(
              dr.GetIntOrDefault("Dim1Code", 0),
              dr.GetString("Dim1Heading", ""),
              dr.GetIntOrDefault("Dim3Code", 0),
              dr.GetString("Dim3Heading", ""),
              dr.GetIntOrDefault("Dim4Code", 0),
              dr.GetString("Dim4Heading", ""),
              dr.GetInt("QuestionId"),
              dr.GetInt("GblQuestionId"),
              dr.GetInt("GblAnswerTypeId"),
              dr.GetString("InputType"),
              dr.GetInt("Sort"),
              dr.GetInt("AutoNumber"),
              dr.GetString("QnText", ""),
              new Questions.ScoreParam(scoreSum_Srv, scoreCount_Srv),
              new Questions.ScoreParam(dr.GetIntOrNull("ScoreSum_Prev"), dr.GetIntOrNull("ScoreCount_Prev")),
              new Questions.ScoreParam(dr.GetIntOrNull("ScoreSum_NSA"), dr.GetIntOrNull("ScoreCount_NSA")),
              new Questions.ScoreParam(dr.GetIntOrNull("ScoreSum_Org"), dr.GetIntOrNull("ScoreCount_Org")),
              new Questions.ScoreParam(dr.GetIntOrNull("ScoreSum_Sect"), dr.GetIntOrNull("ScoreCount_Sect"))
            );

            // Add data row and lookups.
            reportData.LikertQuestions.Add(newQI);
            reportData.LikertQuestionBySort.Add(newQI.Sort, newQI);
            if (!reportData.LikertQuestionByGblQnId.ContainsKey(newQI.GblQuestionId)) reportData.LikertQuestionByGblQnId.Add(newQI.GblQuestionId, newQI);
          },
          Common.NewSqlParameter("SurveyId", reportData.SurveyInfo.SurveyId),
          Common.NewSqlParameter("GblAnswerTypeId", OrgSurveys.Standard_GblAnswerTypeId)
        );
      }

      private static void YearlyIOIChart_LoadStaticData(ReportData reportData) {

        // Get overall IOI scores for ALL participants in the following:
        // a) Scores for Current and previous ("linked") surveys,
        // b) Benchmark scores NSA, Org and Sector.
        // These results are mainly used for the top overview line chart.

        // Note 1: The benchmark results for each "link level" (current, prev, next prev, etc)
        //         should be as they were at the year of that survey.
        // Note 2: As this is for the "static data" part of the cache, the survey results are for *all* participants in each survey.
        //         If user selects specific Divisions ("Org Levels"), all survey ("srv") results are re-queried.
        //         If the user select specific demographics, that only affects the *current* survey, so previous ones don't need to be re-queried.

        Query($@"

          WITH svSource AS (

            SELECT DISTINCT TOP 4 dl.LinkLevel, sv.sv_id, sv.SvCompanyId, sv.sv_SectorId, YEAR(sv.sv_createdUTC) AS SurveyYear
            FROM sv_ReportOrgDivLinks dl
            INNER JOIN sv_Survey sv ON dl.SurveyId = sv.sv_id
            WHERE dl.OriginSurveyId = @SurveyId

            UNION

            SELECT 0 AS LinkLevel, sv.sv_id, sv.SvCompanyId, sv.sv_SectorId, YEAR(sv.sv_createdUTC) AS SurveyYear
            FROM sv_Survey sv
            WHERE sv.sv_id = @SurveyId
          )

          SELECT svSource.*,
            srv_div.AvgScore AS SrvScore_div,
            srv_nodiv.AvgScore AS SrvScore_nodiv,
            nsa.AvgScore AS NSAScore,
            sec.AvgScore AS SectorScore,
            org.AvgScore AS OrgScore
          FROM svSource

          -- Overall IOI score for this survey.
          CROSS APPLY (
            SELECT SUM(gqs.ScoreSum) / SUM(gqs.ScoreCount * 1.0) AS AvgScore
            FROM sv_SurveyGblQnScoresByDiv gqs
            WHERE gqs.SurveyId = svSource.sv_id
              AND gqs.GblQuestionId {OrgSurveys.SQL_BETWEEN_IOIGblQns}
              AND gqs.OrgDivisionCodeId IS NOT NULL -- only participants assigned a division
          ) AS srv_div

          -- Overall IOI score for this survey - if no divisions.
          CROSS APPLY (
            SELECT SUM(gqs.ScoreSum) / SUM(gqs.ScoreCount * 1.0) AS AvgScore
            FROM sv_SurveyGblQnScores gqs
            WHERE gqs.SurveyId = svSource.sv_id
              AND gqs.GblQuestionId {OrgSurveys.SQL_BETWEEN_IOIGblQns}
          ) AS srv_nodiv

          -- Get NSA IOI average *as at year of survey*.
          OUTER APPLY (
            SELECT TOP 1 nsa.AvgScore
            FROM sv_NSA_IOIYearlyAverages nsa
            WHERE nsa.Year <= svSource.SurveyYear
            ORDER BY nsa.Year DESC
          ) AS nsa

          -- Get Sector IOI average as at year of survey.
          CROSS APPLY (
            SELECT SUM(gqs.ScoreSum) / SUM(CAST(gqs.ScoreCount AS FLOAT)) AS AvgScore
            FROM sv_SurveyGblQnScores gqs
            INNER JOIN sv_Survey sv ON sv.sv_id = gqs.SurveyId
            WHERE sv.SurveyTypeCode = 'ios'
              AND sv.sv_createdUTC < DATEFROMPARTS(svSource.SurveyYear + 1, 1, 1)
              AND sv.sv_SectorId = svSource.sv_SectorId
              AND gqs.GblQuestionId {OrgSurveys.SQL_BETWEEN_IOIGblQns}
          ) AS sec

          -- Get Org IOI average as at year of survey.
          CROSS APPLY (
            SELECT SUM(gqs.ScoreSum) / SUM(CAST(gqs.ScoreCount AS FLOAT)) AS AvgScore
            FROM sv_SurveyGblQnScores gqs
            INNER JOIN sv_Survey sv ON sv.sv_id = gqs.SurveyId
            WHERE sv.SurveyTypeCode = 'ios'
              AND sv.SvCompanyId = svSource.SvCompanyId
              AND gqs.GblQuestionId {OrgSurveys.SQL_BETWEEN_IOIGblQns}
          ) AS org

          ORDER BY svSource.LinkLevel;",
          dr => {
            reportData.YearlyIOIChart_SetBenchmarks(
              linkLevel: dr.GetInt("LinkLevel"),
              surveyId: dr.GetInt("sv_id"),
              surveyYear: dr.GetInt("SurveyYear"),
              surveyScore_All: dr.GetDoubleOrNull("SrvScore_div") ?? dr.GetDoubleOrNull("SrvScore_nodiv"),
              benchNSAScore: dr.GetDoubleOrNull("NSAScore"),
              benchSectorScore: dr.GetDoubleOrNull("SectorScore"),
              benchOrgScore: dr.GetDoubleOrNull("OrgScore")
            );
          },
          NewSqlParameter("SurveyId", reportData.SurveyInfo.SurveyId)
        );
      }

      private static void DirectorateChart_LoadStaticData(ReportData reportData) {

        // Get the Directorate-level scores for current and previous surveys.
        // This is mainly used for the Overview directorate bar chart, when "all" divisions are selected.
        // Note that the current survey results will have to be re-queried if demographic filters are selected.

        reportData.DirectorateChartScores.Clear();
        int lastRptOrder = -1;
        DivChartScores divScores = null;

        Query($@"
          SELECT
            IIF(dc.RptOrder1 > 0, dc.RptOrder1, dc.Code) AS RptOrder1,
            IIF(dc.Memo <> '', dc.Memo, dc.Value) AS DirectorateName,
            dlo.LinkLevel,
            SUM(sc.ScoreSum) AS ScoreSum,
            SUM(sc.ScoreCount) AS ScoreCount,
            CAST(SUM(sc.ScoreSum) / CAST(SUM(sc.ScoreCount) AS FLOAT) AS DECIMAL(5,0)) AS ScoreAvg
          FROM sv_360_Codes dc
          INNER JOIN sv_DivisionCodeLinksOrdered dlo ON dlo.CodeId = dc.CodeId
          INNER JOIN sv_SurveyGblQnScoresByDiv sc ON sc.OrgDivisionCodeId = dlo.LinkedCodeId
          WHERE dlo.OriginSurveyId = @SurveyId
            AND sc.GblQuestionId " + OrgSurveys.SQL_BETWEEN_IOIGblQns + @"
          GROUP BY dc.RptOrder1, dc.Code, dc.Value, dc.Memo, dlo.LinkLevel
          ORDER BY IIF(dc.RptOrder1 > 0, dc.RptOrder1, dc.Code), dlo.LinkLevel",
          dr => {
            int rptOrder = dr.GetInt("RptOrder1");
            int linkLevel = dr.GetInt("LinkLevel");
            if (rptOrder != lastRptOrder) {
              // New item for this Directorate.
              divScores = new DivChartScores(dr.GetString("DirectorateName"));
              reportData.DirectorateChart_AddScores(rptOrder, divScores);
              lastRptOrder = rptOrder;
            }
            divScores.SetScore_All(linkLevel, dr.GetInt("ScoreSum"), dr.GetInt("ScoreCount"));
          },
          NewSqlParameter("SurveyId", reportData.SurveyInfo.SurveyId)
        );

      }

      private static void DivisionChart_LoadStaticData(ReportData reportData) {

        // Get the Division-level ("Org Level") scores for current and previous surveys.
        // This is mainly used for the Overview Org Level bar chart, when specific divisions are selected.
        // Note that the current survey results will have to be re-queried if demographic filters are selected.

        string sql = @"
          SELECT
            dc.CodeId, dc.Code,
            dc.Value AS OrgLevelName,
            dlo.LinkLevel,
            SUM(sc.ScoreSum) AS ScoreSum,
            SUM(sc.ScoreCount) AS ScoreCount,
            ROUND(SUM(sc.ScoreSum) / SUM(CAST(sc.ScoreCount AS FLOAT)), 0) AS ScoreAvg
          FROM sv_360_Codes dc
          INNER JOIN sv_DivisionCodeLinksOrdered dlo ON dlo.CodeId = dc.CodeId
          INNER JOIN sv_SurveyGblQnScoresByDiv sc on sc.OrgDivisionCodeId = dlo.LinkedCodeId
          WHERE dlo.OriginSurveyId = @SurveyId
            AND sc.GblQuestionId " + OrgSurveys.SQL_BETWEEN_IOIGblQns + @"
          GROUP BY dc.CodeId, dc.Code, dc.Value, dlo.LinkLevel
          ORDER BY dc.Code, dlo.LinkLevel";

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              int lastCode = -1;
              DivChartScores divScores = null;
              while (dr.Read()) {
                int thisCodeId = dr.GetInt("CodeId");
                int thisCode = dr.GetInt("Code");
                int linkLevel = dr.GetInt("LinkLevel");
                if (thisCode != lastCode) {
                  // New item for this division.
                  divScores = new DivChartScores(thisCodeId, dr.GetString("OrgLevelName"));
                  reportData.DivisionChart_AddScores(thisCodeId, divScores);
                  lastCode = thisCode;
                }
                divScores.SetScore_All(linkLevel, dr.GetInt("ScoreSum"), dr.GetInt("ScoreCount"));
              }
            }
          }
        }

      }

      private static void OpenTextThemes_LoadStaticData(ReportData reportData) {

        // Do any themes exist?

        reportData.OpenTextThemesExist = false;

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          using (var cmd = new SqlCommand(@"

          SELECT COUNT(c.CodeId) AS ThemeCount
          FROM sv_360_AnswerTypes t
          INNER JOIN sv_360_Codes c ON t.AnswerTypeId = c.AnswerTypeId
          WHERE t.SurveyId = @SurveyId
            AND t.InputType = 'a' -- Open text questions.
            AND c.Code > 1 -- More than one code indicates themes exist for this open text question.

            ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo?.SurveyId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                if (dr.GetInt("ThemeCount") > 0) reportData.OpenTextThemesExist = true;
              }
            }
          }
        }

      }

      private static void OpenText_LoadStaticData(ReportData reportData) {

        // Loads the Open Text question info.
        // Not the text responses, just the questions.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

          SELECT q.QuestionId, q.AutoNumber, q.AnswerTypeId, q.QuestionTextFull1
          FROM sv_360_Questions q
          WHERE q.SurveyId = @SurveyId
          AND q.InputType = 'a'
          ORDER BY q.Sort

          ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              reportData.OpenTextQuestions.Clear();
              if (dr.Read()) {
                bool eof = false;
                while (!eof) {
                  var qnInfo = new OpenTextQuestion();
                  qnInfo.QuestionId = dr.GetInt("QuestionId");
                  qnInfo.QuestionIds = qnInfo.QuestionId.ToString();
                  qnInfo.AnswerTypeId = dr.GetInt("AnswerTypeId");
                  qnInfo.QuestionNum = dr.GetInt("AutoNumber");
                  qnInfo.QuestionText = dr.GetString("QuestionTextFull1");
                  reportData.OpenTextQuestions.Add(qnInfo);
                  eof = !dr.Read();
                  while (!eof) {
                    if (dr.GetInt("AnswerTypeId") != qnInfo.AnswerTypeId) break;
                    qnInfo.QuestionIds += "," + dr.GetInt("QuestionId");
                    eof = !dr.Read();
                  }
                }
              }
            }
          }
        }




      }

      // -- Previous & Current data - calls individual component routines below.

      private static void ReportData_LoadPreviousSurveyData(ReportData reportData, ReportFilters requestedFilters) {

        LikertQuestions_LoadPreviousScores(reportData, requestedFilters);
        YearlyIOIChart_LoadPreviousScores(reportData, requestedFilters);

        if (requestedFilters.HasDivisions)
          DivisionChart_LoadPreviousScores(reportData, requestedFilters);
        else
          DirectorateChart_LoadPreviousScores(reportData, requestedFilters);

      }

      private static void ReportData_LoadCurrentSurveyData(ReportData reportData, ReportFilters requestedFilters) {

        // Loads current survey data from UI selections (filters).

        var reportFilterSQL = GetReportFilterSQL(requestedFilters);
        reportData.SetReportFilterSQL(reportFilterSQL);

        // First get participant count to ensure it's within the rules.
        reportData.SetParticipantCount(GetParticipantCount(reportData));
        if (reportData.ParticipantCount < reportData.SurveyInfo.OrgReportMinResponsesToShow) return; // Don't bother carrying on, no enough participants.

        LikertQuestions_LoadCurrentScores(reportData, reportFilterSQL);

        YearlyIOIChart_LoadCurrentScores(reportData, reportFilterSQL);

        if (requestedFilters.HasDivisions)
          DivisionChart_LoadCurrentScores(reportData, reportFilterSQL);
        else
          DirectorateChart_LoadCurrentScores(reportData, reportFilterSQL);

      }

      public static int GetParticipantCount(ReportData reportData) {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

          SELECT COUNT(p.PartId) AS PartCount
          FROM sv_360_Participants p
          " + reportData.ReportFilterSQL.demogJoinSQL + @"
          WHERE p.SurveyId = @SurveyId
            AND p.Completed IS NOT NULL
            " + reportData.ReportFilterSQL.divisionConditionSQL + @"

            ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              if (dr.Read()) {
                return dr.GetInt("PartCount");
              }
            }
          }
        }
        return 0;

      }

      // -- Individual report components.

      private static void LikertQuestions_LoadPreviousScores(ReportData reportData, ReportFilters requestedFilters) {

        // Get scores for questions from the previous survey, filtered by selected divisions.

        if (!requestedFilters.HasDivisions) {
          // No divisions selected, so make filtered values same as "all".
          reportData.LikertQuestions_PreviousScores_CopyAllToFiltered();
          return;
        }

        // Clear the previous result for each question.
        reportData.LikertQuestions_PreviousScores_ClearFiltered();

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

        SELECT
          ga.GblQuestionId,
          SUM(ga.ScoreSum) AS ScoreSum,
          SUM(ga.ScoreCount) AS ScoreCount
        FROM sv_SurveyGblQnScoresByDiv ga
        INNER JOIN sv_DivisionCodeLinksOrdered dlo ON dlo.LinkedCodeId = ga.OrgDivisionCodeId
        WHERE dlo.OriginSurveyId = @SurveyId
          AND dlo.LinkLevel = @LinkLevel
          AND dlo.CodeId IN (" + requestedFilters.GetDivisionCodeIdStringOrZero() + @")
        GROUP BY ga.GblQuestionId
        ORDER BY ga.GblQuestionId

        ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            cmd.Parameters.AddInt("@LinkLevel", OrgSurveys.LinkLevel_Previous);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                int gblQnId = dr.GetInt("GblQuestionId");
                if (reportData.LikertQuestionByGblQnId.ContainsKey(gblQnId)) {

                  reportData.LikertQuestionByGblQnId[gblQnId].SetPreviousScore_Filtered(dr.GetInt("ScoreSum"), dr.GetInt("ScoreCount"));
                }
              }
            }
          }
        }

      }

      private static void LikertQuestions_LoadCurrentScores(ReportData reportData, ReportFilterSQL reportFilterSQL) {

        // Get scores for questions from the current survey, filtered by selected divisions.

        if (reportFilterSQL.divisionConditionSQL.IsNullOrEmpty() && reportFilterSQL.demogJoinSQL.IsNullOrEmpty()) {
          // No filters selected, so make scores same as "all".
          reportData.LikertQuestions_CurrentScores_CopyAllToFiltered();
          return;
        }

        // Clear the previous result for each question.
        reportData.LikertQuestions_CurrentScores_ClearFiltered();

        string sql = $@"
          SELECT q.GblQuestionId,
            SUM(c.ValueScore) AS ScoreSum,
            COUNT(c.ValueScore) AS ScoreCount
          FROM sv_360_Codes c
          INNER JOIN sv_Answers a ON c.CodeId = a.CodeId
          INNER JOIN sv_360_Questions q ON q.QuestionId = a.QuestionId
          INNER JOIN sv_360_Participants p ON p.PartId = a.ParticipantId
          {reportFilterSQL.demogJoinSQL}
          WHERE q.GblAnswerTypeId = @GblAnswerTypeId
            AND p.SurveyId = @SurveyId
            AND p.Completed IS NOT NULL
            {reportFilterSQL.divisionConditionSQL}
          GROUP BY q.GblQuestionId
          ORDER BY q.GblQuestionId";

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            cmd.Parameters.AddInt("@GblAnswerTypeId", OrgSurveys.Standard_GblAnswerTypeId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                int gblQnId = dr.GetInt("GblQuestionId");
                if (reportData.LikertQuestionByGblQnId.ContainsKey(gblQnId)) {

                  reportData.LikertQuestionByGblQnId[gblQnId].SetCurrentScore_Filtered(dr.GetInt("ScoreSum"), dr.GetInt("ScoreCount"));

                }
              }
            }
          }
        }

      }

      private static void YearlyIOIChart_LoadPreviousScores(ReportData reportData, ReportFilters requestedFilters) {
        // Use divison back-link table to load overall IOI scores for previous surveys.
        // Note previous survey scores are affected by division filter but not demographic filter.
        // Current survey scores are also affected by the demographic filter so it's done separately to this.

        if (!requestedFilters.HasDivisions) {
          // No divisions selected, so make filtered values same as "all".
          reportData.YearlyIOIChart_CopyAllToFiltered();
          return;
        }

        // Load previous surveys IOI scores based on selected divisions.

        reportData.YearlyIOIChart_ClearFilteredScores();

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

          SELECT
            dlo.LinkLevel,
            IIF(SUM(sc.ScoreCount) = 0,
              NULL,
              CAST(SUM(sc.ScoreSum) / (SUM(sc.ScoreCount) + 0.0) AS DECIMAL(5, 0))
            ) AS ScoreAvg
          FROM sv_SurveyGblQnScoresByDiv sc
          INNER JOIN sv_DivisionCodeLinksOrdered dlo ON dlo.LinkedCodeId = sc.OrgDivisionCodeId
          WHERE dlo.OriginSurveyId = @SurveyId
            AND sc.GblQuestionId " + OrgSurveys.SQL_BETWEEN_IOIGblQns + @"
            AND dlo.CodeId IN(" + requestedFilters.GetDivisionCodeIdStringOrZero() + @")
            AND dlo.LinkLevel > 0
          GROUP BY dlo.LinkLevel
          ORDER BY dlo.LinkLevel

          ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                reportData.YearlyIOIChart_SetFilteredScore(dr.GetInt("LinkLevel"), dr.GetDoubleOrNull("ScoreAvg"));
              }
            }
          }
        }

      }

      private static void YearlyIOIChart_LoadCurrentScores(ReportData reportData, ReportFilterSQL reportFilterSQL) {

        // For current survey get yearly IOI scores for selected divisions AND demographics.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

          SELECT AVG(c.ValueScore) AS AvgScore
          FROM sv_360_Codes c
          INNER JOIN sv_Answers a ON c.CodeId = a.CodeId
          INNER JOIN sv_360_Participants p ON a.ParticipantId = p.PartId AND p.Completed IS NOT NULL
          INNER JOIN sv_360_Questions q ON a.QuestionId = q.QuestionId
          " + reportFilterSQL.demogJoinSQL + @"
          WHERE q.SurveyId = @SurveyId
            AND q.GblQuestionId " + OrgSurveys.SQL_BETWEEN_IOIGblQns + @"
            " + reportFilterSQL.divisionConditionSQL + @"

          ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                reportData.YearlyIOIChart_SetFilteredScore(OrgSurveys.LinkLevel_Current, dr.GetDoubleOrNull("AvgScore"));
              }
            }
          }
        }

      }

      private static void DirectorateChart_LoadPreviousScores(ReportData reportData, ReportFilters requestedFilters) {

        // As the directorate chart is always the result of NOT choosing any divisions,
        // and the demographic filter only affects the current survey, there is no
        // need to read data from the db here. Just copy it from the unfiltered cache.

        reportData.DirectorateChart_CopyAllToFiltered_PreviousOnly();

      }

      private static void DirectorateChart_LoadCurrentScores(ReportData reportData, ReportFilterSQL reportFilterSQL) {

        // Get the Directorate results for the current survey, filtered by Demographics.
        // This is needed when ALL divisions are included, as then they are grouped by Directorate.

        // If there are no demographics selected, then just copy from all results.
        if (reportFilterSQL.demogJoinSQL.IsNullOrEmpty()) {
          reportData.DirectorateChart_CopyAllToFiltered_CurrentOnly();
          return;
        }

        // First clear current scores.
        reportData.DirectorateChartScores_ClearCurrentFiltered();

        string sql = @"
          SELECT
            IIF(dc.RptOrder1 > 0, dc.RptOrder1, dc.Code) AS RptOrder1,
            SUM(c.ValueScore) AS ScoreSum,
            COUNT(c.ValueScore) AS ScoreCount
          FROM sv_360_Codes c
          INNER JOIN sv_Answers a ON c.CodeId = a.CodeId
          INNER JOIN sv_360_Questions q ON q.QuestionId = a.QuestionId
          INNER JOIN sv_360_Participants p ON p.PartId = a.ParticipantId
          INNER JOIN sv_360_Codes dc ON dc.CodeId = p.OrgDivisionCodeId
          " + reportFilterSQL.demogJoinSQL + @"
          WHERE q.SurveyId = @SurveyId
            AND p.SurveyId = @SurveyId
            AND p.Completed IS NOT NULL
            AND q.GblQuestionId " + OrgSurveys.SQL_BETWEEN_IOIGblQns + @"
          GROUP BY dc.RptOrder1, dc.Code
          ORDER BY IIF(dc.RptOrder1 > 0, dc.RptOrder1, dc.Code)
";
        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                int rptOrder = dr.GetInt("RptOrder1");
                reportData.DirectorateChart_SetScore_CurrentFiltered(rptOrder, dr.GetInt("ScoreSum"), dr.GetInt("ScoreCount"));
              }
            }
          }
        }

      }

      private static void DivisionChart_LoadPreviousScores(ReportData reportData, ReportFilters requestedFilters) {

        // First clear previous survey scores.
        reportData.DivisionChart_ClearScores_PreviousFiltered();

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

          SELECT
            dlo.CodeId,
            dlo.LinkLevel,
            SUM(sc.ScoreSum) AS ScoreSum,
            SUM(sc.ScoreCount) AS ScoreCount
          FROM sv_DivisionCodeLinksOrdered dlo
          INNER JOIN sv_SurveyGblQnScoresByDiv sc ON sc.OrgDivisionCodeId = dlo.LinkedCodeId
          WHERE dlo.OriginSurveyId = @SurveyId
            AND dlo.LinkLevel > @LinkLevelCurrent
            AND sc.GblQuestionId " + OrgSurveys.SQL_BETWEEN_IOIGblQns + @"
          GROUP BY dlo.CodeId, dlo.LinkLevel
          ORDER BY dlo.CodeId, dlo.LinkLevel

          ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            cmd.Parameters.AddInt("@LinkLevelCurrent", OrgSurveys.LinkLevel_Current);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                int divCodeId = dr.GetInt("CodeId");
                int linkLevel = dr.GetInt("LinkLevel");
                if (!reportData.DivisionChart_DivisionIdExists(divCodeId))
                  throw new KeyNotFoundException("Division CodeId[" + divCodeId + "] not found.");
                reportData.DivisionChart_SetScores_Filtered(divCodeId, linkLevel, dr.GetInt("ScoreSum"), dr.GetInt("ScoreCount"));
              }
            }
          }
        }

      }

      private static void DivisionChart_LoadCurrentScores(ReportData reportData, ReportFilterSQL reportFilterSQL) {

        // First clear previous survey scores.
        reportData.DivisionChart_ClearScores_CurrentFiltered();

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

          SELECT dc.CodeId,
            SUM(c.ValueScore) AS ScoreSum,
            COUNT(c.ValueScore) AS ScoreCount
          FROM sv_360_Codes c
          INNER JOIN sv_Answers a ON c.CodeId = a.CodeId
          INNER JOIN sv_360_Questions q ON q.QuestionId = a.QuestionId
          INNER JOIN sv_360_Participants p ON p.PartId = a.ParticipantId
          INNER JOIN sv_360_Codes dc ON dc.CodeId = p.OrgDivisionCodeId
          " + reportFilterSQL.demogJoinSQL + @"
          WHERE q.SurveyId = @SurveyId
            AND p.SurveyId = @SurveyId
            AND p.Completed IS NOT NULL
            AND q.GblQuestionId " + OrgSurveys.SQL_BETWEEN_IOIGblQns + @"
            " + reportFilterSQL.divisionConditionSQL + @"
          GROUP BY dc.CodeId
          ORDER BY dc.CodeId

          ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                int divCodeId = dr.GetInt("CodeId");
                reportData.DivisionChart_SetScores_Filtered(
                  divCodeId, OrgSurveys.LinkLevel_Current, dr.GetInt("ScoreSum"), dr.GetInt("ScoreCount"));
              }
            }
          }
        }

      }

      // -- Getting fundamental data before getting the scores.

      private static void SetSurveyIdsByLinkLevel(ReportData reportData) {

        // Set list of previous survey ids in order of LinkLevel (0 = current, 1 = previous, 2 = previous to that, etc.).
        // Requires sv_ReportOrgDivLinks to be up to date (previously done here by CreateDivCodeLinks(), now done in survey_qntypes.asp when links are changed.)

        if (reportData.ReportFilters == null || reportData.SurveyInfo.SurveyId == 0)
          throw new ArgumentException("reportData.SurveyInfo.SurveyId must be set.");

        Common.Query(@"
          SELECT DISTINCT dl.LinkLevel, dl.SurveyId, YEAR(s.sv_createdUTC) AS SurveyYear
          FROM sv_ReportOrgDivLinks dl
          INNER JOIN sv_Survey s ON dl.SurveyId = s.sv_id
          WHERE dl.OriginSurveyId = @OriginSurveyId
          ORDER BY dl.LinkLevel",
          dr => {
            // List index 0 will be the current survey id. Index 1 will be the previous survey id, etc.
            reportData.SurveyInfo_AddSurvey(dr.GetInt("SurveyId"), dr.GetInt("SurveyYear"));
          },
          Common.NewSqlParameter("OriginSurveyId", reportData.SurveyInfo.SurveyId)
        );

        if (reportData.GetMaxUsedLinkLevel() < 0) {
          // No division links found, just add the current survey.
          reportData.SurveyInfo_AddSurvey(reportData.SurveyInfo.SurveyId, reportData.SurveyInfo.CloseDateLocal.Year);
        }
      }

      private static void CreateDivCodeLinks(ReportData reportData) {

        // Create entries for this survey in sv_DivisionCodeLinks which is basically
        // a proper CodeId-linking representation of the division "link codes".
        // Note if @SurveyId is null, then it will create entries for ALL surveys (slow but could occasionally be useful).

        // TEST: This shouldn't be needed here anymore, as the back-link summary tables for IOS surveys
        //       are now updated when updating division info (see survey_qntypes.asp).

        if (reportData.ReportFilters == null || reportData.SurveyInfo.SurveyId == 0)
          throw new ArgumentException("reportData.SurveyInfo.SurveyId must be set.");

        Common.GetNonQueryInt(@"

          -- This creates the many-to-many links from the soon-to-be-deprecated 'code links' in the codes table.

          DELETE dl
            FROM sv_DivisionCodeLinks dl
            INNER JOIN sv_360_Codes c ON c.CodeId = dl.DivCodeId
            INNER JOIN sv_360_AnswerTypes t ON c.AnswerTypeId = t.AnswerTypeId
          WHERE AutoPopulated = 1
          AND (@SurveyId = NULL OR t.SurveyId = @SurveyId);

          INSERT INTO sv_DivisionCodeLinks (DivCodeId, PreviousDivCodeId, AutoPopulated)
          SELECT dc.CodeId, prv_dc.CodeId, 1
          FROM sv_Survey s
          INNER JOIN sv_360_AnswerTypes dt ON dt.SurveyId = s.sv_id AND dt.AnswerTypeDescr = 'division'
          INNER JOIN sv_360_Codes dc ON dc.AnswerTypeId = dt.AnswerTypeId
          INNER JOIN sv_Survey prv_s ON prv_s.sv_id = s.PreviousSurveyId
          INNER JOIN sv_360_AnswerTypes prv_dt ON prv_dt.SurveyId = prv_s.sv_id AND prv_dt.AnswerTypeDescr = 'division'
          INNER JOIN sv_360_Codes prv_dc ON prv_dc.AnswerTypeId = prv_dt.AnswerTypeId
          CROSS APPLY (
            SELECT CAST(Split.A.value('.', 'NVARCHAR(MAX)') AS INT) AS LinkCode
            FROM (
              SELECT CAST('<X>' + REPLACE(dc.ValueTextOther, ',', '</X><X>') + '</X>' AS XML) AS String
            ) AS A
            CROSS APPLY String.nodes('/X') AS Split (A)
          ) AS l
          CROSS APPLY (
            -- Check if sv_DivisionCodeLinks row exists.
            SELECT COUNT(*) AS DivLinkCheck
            FROM sv_DivisionCodeLinks cl
            WHERE cl.DivCodeId = dc.CodeId
              AND cl.PreviousDivCodeId = prv_dc.CodeId
          ) AS lchk
          WHERE s.sv_typecode = 'eds'
            AND (@SurveyId = NULL OR s.sv_id = @SurveyId)
            AND dc.ValueTextOther <> ''
            AND l.LinkCode = prv_dc.Code
            AND lchk.DivLinkCheck = 0;

          -- This uses sv_DivisionCodeLinks to create a dataset more suited to report queries.

          DELETE FROM sv_DivisionCodeLinksOrdered
          WHERE OriginSurveyId = @SurveyId;

          WITH src
          AS (
            SELECT t0.SurveyId AS SurveyId0, c0.AnswerTypeId AS AnswerTypeId0, c0.CodeId AS CodeId0, c0.Code AS Code0, c0.Value AS Value0,
              t1.SurveyId AS SurveyId1, c1.AnswerTypeId AS AnswerTypeId1, c1.CodeId AS CodeId1, c1.Code AS Code1, c1.Value AS Value1,
              t2.SurveyId AS SurveyId2, c2.AnswerTypeId AS AnswerTypeId2, c2.CodeId AS CodeId2, c2.Code AS Code2, c2.Value AS Value2,
              t3.SurveyId AS SurveyId3, c3.AnswerTypeId AS AnswerTypeId3, c3.CodeId AS CodeId3, c3.Code AS Code3, c3.Value AS Value3
            FROM sv_360_Codes c0
            INNER JOIN sv_360_AnswerTypes t0 ON c0.AnswerTypeId = t0.AnswerTypeId
            LEFT OUTER JOIN sv_DivisionCodeLinks dl ON dl.DivCodeId = c0.CodeId
            LEFT OUTER JOIN sv_360_Codes c1 ON c1.CodeId = dl.PreviousDivCodeId
            LEFT OUTER JOIN sv_360_AnswerTypes t1 ON c1.AnswerTypeId = t1.AnswerTypeId
            LEFT OUTER JOIN sv_DivisionCodeLinks dl1 ON dl1.DivCodeId = c1.CodeId
            LEFT OUTER JOIN sv_360_Codes c2 ON c2.CodeId = dl1.PreviousDivCodeId
            LEFT OUTER JOIN sv_360_AnswerTypes t2 ON c2.AnswerTypeId = t2.AnswerTypeId
            LEFT OUTER JOIN sv_DivisionCodeLinks dl2 ON dl2.DivCodeId = c2.CodeId
            LEFT OUTER JOIN sv_360_Codes c3 ON c3.CodeId = dl2.PreviousDivCodeId
            LEFT OUTER JOIN sv_360_AnswerTypes t3 ON c3.AnswerTypeId = t3.AnswerTypeId
            WHERE t0.SurveyId = @SurveyId
              AND t0.AnswerTypeDescr = 'division'
          ),
          result
          AS (
            SELECT DISTINCT src.SurveyId0 AS SurveyId, src.AnswerTypeId0 AS AnswerTypeId, src.CodeId0 AS CodeId, src.Code0 AS Code,
              slinks.LinkLevel, slinks.LinkedSurveyId, slinks.LinkedCodeId, slinks.LinkedCode, slinks.LinkedValue
            FROM src
            CROSS APPLY (
              SELECT 0 AS LinkLevel, s0.SurveyId0 AS LinkedSurveyId, s0.CodeId0 AS LinkedCodeId, s0.Code0 AS LinkedCode, s0.Value0 AS LinkedValue
              FROM src s0
              WHERE s0.CodeId0 = src.CodeId0
              UNION
              SELECT 1 AS LinkLevel, s1.SurveyId1 AS LinkedSurveyId, s1.CodeId1 AS LinkedCodeId, s1.Code1 AS LinkedCode, s1.Value1 AS LinkedValue
              FROM src s1
              WHERE s1.CodeId0 = src.CodeId0
              UNION
              SELECT 2 AS LinkLevel, s2.SurveyId2 AS LinkedSurveyId, s2.CodeId2 AS LinkedCodeId, s2.Code2 AS LinkedCode, s2.Value2 AS LinkedValue
              FROM src s2
              WHERE s2.CodeId0 = src.CodeId0
              UNION
              SELECT 3 AS LinkLevel, s3.SurveyId3 AS LinkedSurveyId, s3.CodeId3 AS LinkedCodeId, s3.Code3 AS LinkedCode, s3.Value3 AS LinkedValue
              FROM src s3
              WHERE s3.CodeId0 = src.CodeId0
            ) AS slinks
          )
          INSERT INTO sv_DivisionCodeLinksOrdered (OriginSurveyId, OriginAnswerTypeId, CodeId, Code, LinkLevel,
          LinkedSurveyId, LinkedCodeId, LinkedCode, LinkedValue)
          SELECT SurveyId, AnswerTypeId, CodeId, Code, LinkLevel,
            LinkedSurveyId, LinkedCodeId, LinkedCode, LinkedValue
          FROM result
          WHERE LinkedSurveyId IS NOT NULL;

          -- Populate sv_ReportOrgDivLinks based on sv_DivisionCodeLinks.

          DELETE FROM sv_ReportOrgDivLinks
          WHERE OriginSurveyId = @SurveyId;

          WITH dc0
          AS (
            SELECT 0 AS LinkLevel, s.sv_id, dc.CodeId, dc.Code, dc.Value, dc.ValueTextOther
            FROM sv_360_Codes dc
            INNER JOIN sv_360_AnswerTypes dt ON dc.AnswerTypeId = dt.AnswerTypeId
            INNER JOIN sv_Survey s ON dt.SurveyId = s.sv_id
            WHERE dt.SurveyId = @SurveyId
              AND dt.AnswerTypeDescr = 'division'
          ),
          dc1
          AS (
            SELECT 1 AS LinkLevel, s.sv_id, dc.CodeId, dc.Code, dc.Value, dc.ValueTextOther
            FROM dc0
            INNER JOIN sv_DivisionCodeLinks dl ON dl.DivCodeId = dc0.CodeId
            INNER JOIN sv_360_Codes dc ON dc.CodeId = dl.PreviousDivCodeId
            INNER JOIN sv_360_AnswerTypes dt ON dc.AnswerTypeId = dt.AnswerTypeId
            INNER JOIN sv_Survey s ON dt.SurveyId = s.sv_id
          ),
          dc2
          AS (
            SELECT 2 AS LinkLevel, s.sv_id, dc.CodeId, dc.Code, dc.Value, dc.ValueTextOther
            FROM dc1
            INNER JOIN sv_DivisionCodeLinks dl ON dl.DivCodeId = dc1.CodeId
            INNER JOIN sv_360_Codes dc ON dc.CodeId = dl.PreviousDivCodeId
            INNER JOIN sv_360_AnswerTypes dt ON dc.AnswerTypeId = dt.AnswerTypeId
            INNER JOIN sv_Survey s ON dt.SurveyId = s.sv_id
          ),
          dc3
          AS (
            SELECT 3 AS LinkLevel, s.sv_id, dc.CodeId, dc.Code, dc.Value, dc.ValueTextOther
            FROM dc2
            INNER JOIN sv_DivisionCodeLinks dl ON dl.DivCodeId = dc2.CodeId
            INNER JOIN sv_360_Codes dc ON dc.CodeId = dl.PreviousDivCodeId
            INNER JOIN sv_360_AnswerTypes dt ON dc.AnswerTypeId = dt.AnswerTypeId
            INNER JOIN sv_Survey s ON dt.SurveyId = s.sv_id
          ),
          AllDivCodes
          AS (
            SELECT * FROM dc0
            UNION
            SELECT * FROM dc1
            UNION
            SELECT * FROM dc2
            UNION
            SELECT * FROM dc3
          )
          INSERT INTO sv_ReportOrgDivLinks
            (OriginSurveyId, LinkLevel, SurveyId, DivisionCodeId, DivisionCode, DivisionTitle, DivLinkCodes)
          SELECT
            @SurveyId, LinkLevel, sv_id, CodeId, Code, Value, ValueTextOther
          FROM AllDivCodes;",

          Common.NewSqlParameter("SurveyId", reportData.SurveyInfo.SurveyId)
        );

      }

      private static ReportFilterSQL GetReportFilterSQL(ReportFilters requestedFilters) {

        ReportFilterSQL reportFilterSQL = new ReportFilterSQL();

        // SQL string used to refine based on demographic filter.
        reportFilterSQL.demogJoinSQL = "";
        int demogJoinCount = 0;
        if (requestedFilters.Demographics != null && requestedFilters.Demographics.Count > 0) {
          foreach (var demogFilter in requestedFilters.Demographics) {
            demogJoinCount++;
            string ansAlias = "adem" + demogJoinCount;
            reportFilterSQL.demogJoinSQL
              += "INNER JOIN sv_Answers " + ansAlias
              + " ON " + ansAlias + ".ParticipantId = p.PartId "
              + "AND " + ansAlias + ".QuestionId = " + demogFilter.QuestionId + " and " + ansAlias + ".CodeId = " + demogFilter.CodeId + " ";
          }
        }

        // SQL string to refine based on "org level" (division) filter.
        reportFilterSQL.divisionConditionSQL = "";
        if (requestedFilters.GetDivisionCodeIdString().IsNullOrEmpty()) {
          reportFilterSQL.divisionConditionSQL = "";
        } else {
          reportFilterSQL.divisionConditionSQL = "AND p.OrgDivisionCodeId IN(" + requestedFilters.GetDivisionCodeIdStringOrZero() + ")";
        }

        return reportFilterSQL;
      }

      // -- Other

      public static List<string> GetOpenTextResponses(ReportData reportData, int answerTypeId) {

        var textList = new List<string>();

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

        SELECT ta.ta_textanswer
        FROM sv_Answers a
          INNER JOIN sv_360_Questions q ON a.QuestionId = q.QuestionId
          INNER JOIN sv_360_Participants p ON p.PartId = a.ParticipantId
          INNER JOIN sv_360_TextAnswers ta ON ta.AnswerId = a.AnswerId
          " + reportData.ReportFilterSQL.demogJoinSQL + @"
        WHERE p.SurveyId = @SurveyId
          AND q.AnswerTypeId = @AnswerTypeId
          AND p.Completed IS NOT NULL
          " + reportData.ReportFilterSQL.divisionConditionSQL + @"
        ORDER BY q.Sort, ta.TextAnswerId

        ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            cmd.Parameters.AddInt("@AnswerTypeId", answerTypeId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                string txt = dr.GetString("ta_textanswer").Trim();
                if (!txt.IsNullOrEmpty()) textList.Add(txt);
              }
            }
          }
        }

        return textList;

      }

      public static List<OpenTextTheme> GetOpenTextThemes(ReportData reportData, int answerTypeId) {

        var themeList = new List<OpenTextTheme>();

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(@"

          SELECT 1 AS ForceOrder, 'All Responses' AS ThemeText, COUNT(p.PartId) AS AnsCount
          FROM sv_360_Participants p
          " + reportData.ReportFilterSQL.demogJoinSQL + @"
          WHERE p.SurveyId = @SurveyId
            AND p.Completed IS NOT NULL
            " + reportData.ReportFilterSQL.divisionConditionSQL + @"

          UNION

          SELECT 2 AS ForceOrder, c.Value AS ThemeText, COUNT(a.AnswerId) AS AnsCount
          FROM sv_360_Codes c
          INNER JOIN sv_Answers a ON c.CodeId = a.CodeId
          INNER JOIN sv_360_Participants p ON a.ParticipantId = p.PartId
          " + reportData.ReportFilterSQL.demogJoinSQL + @"
          WHERE c.AnswerTypeId = @AnswerTypeId
          " + reportData.ReportFilterSQL.divisionConditionSQL + @"
          GROUP BY c.Value

          ORDER BY ForceOrder, AnsCount DESC

          ", conn)) {
            cmd.Parameters.AddInt("@SurveyId", reportData.SurveyInfo.SurveyId);
            cmd.Parameters.AddInt("@AnswerTypeId", answerTypeId);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                themeList.Add(new OpenTextTheme() {
                  ThemeText = dr.GetString("ThemeText"),
                  ResponseCount = dr.GetInt("AnsCount")
                });
              }
            }
          }
        }

        return themeList;
      }

      // -- Classes

      public struct OpenTextTheme {
        public string ThemeText;
        public int ResponseCount;
      }

      public struct ReportFilterSQL {
        public string demogJoinSQL;
        public string divisionConditionSQL;
      }

      public class ReportData {

        public readonly DateTime CreatedUTC = DateTime.UtcNow;

        public string SurveyUId { get; private set; }
        public string SessionId { get; private set; }

        public bool IsStaticSurveyDataLoaded { get; private set; }

        public OrgSurveys.SurveyInfo SurveyInfo { get; private set; }

        // Filters - these are to remember the current filters, which correspond to current data.
        // As filters change, relevant parts of the data are re-queried.
        public ReportFilters ReportFilters;
        public ReportFilterSQL ReportFilterSQL { get; private set; }

        public int ParticipantCount { get; private set; } // No. participants in this resultset.

        // List of survey IDs. Index is the same as "sequence number" used elsewhere.
        // i.e. index 0 = current survey, 1 = previous, 2 = next previous, etc.
        private List<SurveyInfo> SurveyInfoByLinkLevel;

        // List of individual question details and scores.
        public List<QuestionInfo> LikertQuestions { get; private set; } // Main list of question data. Includes current, previous and bench scores for all questions in current survey.
        public Dictionary<int, QuestionInfo> LikertQuestionByGblQnId { get; private set; } // Lookup above by global qn id.
        public Dictionary<int, QuestionInfo> LikertQuestionBySort { get; private set; } // Lookup by question sort.

        // Overall scores for individual surveys, current and historic, filtered and benchmarks.
        // This is used for e.g. the overall IOI line chart.
        // Index of list = LinkLevel, i.e. 0=current, 1=previous, etc.
        public List<YearlyIOIScores> YearlyIOIByLinkLevel;

        // Directorate overview data. Each list item is a directorate, in order of the sort column (RptOrder1).
        // Within each is also a list, each item >0 being a previous survey score for that directorate.
        public List<DivChartScores> DirectorateChartScores { get; private set; }
        public Dictionary<int, DivChartScores> DirectorateChartScoresByRptOrder { get; private set; }

        // Division ("org level") overview data.
        // Each item is a division score.
        public List<DivChartScores> DivisionChartScores;
        private Dictionary<int, DivChartScores> DivisionChartScoresByCodeId;

        public bool OpenTextThemesExist;
        public List<OpenTextQuestion> OpenTextQuestions;
        //public List<OpenTextAnswer> OpenTextAnswers;

        // Constructor.

        public ReportData(string surveyUId, string sessionId) {

          IsStaticSurveyDataLoaded = false;

          this.SurveyUId = surveyUId;
          this.SessionId = sessionId;
          this.SurveyInfo = null;
          this.ReportFilters = new ReportFilters();
          this.SurveyInfoByLinkLevel = new List<SurveyInfo>();
          this.LikertQuestions = new List<QuestionInfo>();
          this.LikertQuestionByGblQnId = new Dictionary<int, QuestionInfo>();
          this.LikertQuestionBySort = new Dictionary<int, QuestionInfo>();
          this.YearlyIOIByLinkLevel = new List<YearlyIOIScores>();
          this.DirectorateChartScores = new List<DivChartScores>();
          this.DirectorateChartScoresByRptOrder = new Dictionary<int, DivChartScores>();
          this.DivisionChartScores = new List<DivChartScores>();
          this.DivisionChartScoresByCodeId = new Dictionary<int, DivChartScores>();
          this.OpenTextQuestions = new List<OpenTextQuestion>();
          //this.OpenTextAnswers = new List<OpenTextAnswer>();
        }

        public void SurveyInfo_LoadInfo() {
          if (this.SurveyUId.IsNullOrEmpty()) throw new Exception("reportData.SurveyUId must be set.");
          SurveyInfo = OrgSurveys.GetSurveyInfo(this.SurveyUId); // Returns null if survey not found.
        }

        public void SetStaticSurveyDataLoaded() {
          IsStaticSurveyDataLoaded = true;
        }

        public int GetMaxUsedLinkLevel() { return SurveyInfoByLinkLevel.Count - 1; } // LinkLevel 0 = current survey

        public SurveyInfo SurveyInfo_GetSurveyAtLinkLevel(int linkLevel) {

          if (linkLevel < 0 || linkLevel > OrgReports.Max_Previous_Surveys) throw new ArgumentOutOfRangeException("linkLevel out of range (" + linkLevel + ")");
          if (linkLevel >= SurveyInfoByLinkLevel.Count) return null;
          return SurveyInfoByLinkLevel[linkLevel];
        }

        public void SurveyInfo_AddSurvey(int surveyId, int surveyYear) {
          SurveyInfoByLinkLevel.Add(new SurveyInfo(surveyId, surveyYear));
        }

        public YearlyIOIScores YearlyIOIChart_GetScores(int linkLevel) {

          if (linkLevel < 0 || linkLevel > OrgReports.Max_Previous_Surveys) throw new ArgumentOutOfRangeException("linkLevel out of range (" + linkLevel + ")");
          if (linkLevel >= YearlyIOIByLinkLevel.Count || YearlyIOIByLinkLevel[linkLevel] == null) throw new ArgumentException("linkLevel " + linkLevel + " not found.");

          return YearlyIOIByLinkLevel[linkLevel];
        }

        public void YearlyIOIChart_SetBenchmarks(
          int linkLevel,
          int surveyId,
          int surveyYear,
          double? surveyScore_All,
          double? benchNSAScore,
          double? benchSectorScore,
          double? benchOrgScore) {

          // Sets the benchmark scores for this link level.
          // These scores are used in the overall page line graph, benchmark line.

          if (linkLevel < 0 || linkLevel > OrgReports.Max_Previous_Surveys) throw new ArgumentOutOfRangeException("linkLevel out of range (" + linkLevel + ")");

          // Pad list if item to add is greater than existing items.
          while (YearlyIOIByLinkLevel.Count <= linkLevel) YearlyIOIByLinkLevel.Add(null);
          // Add new item.
          YearlyIOIByLinkLevel[linkLevel] = new YearlyIOIScores(surveyId, surveyYear, surveyScore_All, benchNSAScore, benchSectorScore, benchOrgScore);
        }

        public void SetParticipantCount(int participantCount) {
          ParticipantCount = participantCount;
        }

        public void YearlyIOIChart_ClearFilteredScores() {
          for (var linkLevel = 0; linkLevel < YearlyIOIByLinkLevel.Count; linkLevel++) {
            YearlyIOIByLinkLevel[linkLevel].SetFilteredSurveyScore(null);
          }
        }

        public void YearlyIOIChart_SetFilteredScore(int linkLevel, double? surveyScore_Filtered) {

          // Sets just the filtered survey score for this Link Level without disturbing the bench scores.

          if (linkLevel < OrgSurveys.LinkLevel_Current || linkLevel > OrgReports.Max_Previous_Surveys) throw new ArgumentOutOfRangeException("linkLevel out of range (" + linkLevel + ")");
          if (linkLevel >= YearlyIOIByLinkLevel.Count || YearlyIOIByLinkLevel[linkLevel] == null) throw new ArgumentException("linkLevel " + linkLevel + " not found.");

          YearlyIOIByLinkLevel[linkLevel].SetFilteredSurveyScore(surveyScore_Filtered);
        }

        public void YearlyIOIChart_CopyAllToFiltered() {
          for (int linkLevel = OrgSurveys.LinkLevel_Current; linkLevel < YearlyIOIByLinkLevel.Count; linkLevel++) {
            YearlyIOIByLinkLevel[linkLevel].SetFilteredSurveyScore(YearlyIOIByLinkLevel[linkLevel].SurveyScore_All);
          }
        }

        public void DirectorateChartScores_ClearCurrentFiltered() {
          foreach (var score in DirectorateChartScores) {
            score.ClearScore_CurrentFiltered();
          }
        }

        public void DirectorateChart_AddScores(int rptOrder, DivChartScores divScores) {
          if (DirectorateChartScoresByRptOrder.ContainsKey(rptOrder))
            throw new ApplicationException("DirectorateChartScoresByRptOrder already contains key " + rptOrder);
          DirectorateChartScores.Add(divScores);
          DirectorateChartScoresByRptOrder.Add(rptOrder, divScores);
        }

        public void DirectorateChart_SetScore_CurrentFiltered(int rptOrder, int scoreSum, int scoreCount) {
          if (!DirectorateChartScoresByRptOrder.ContainsKey(rptOrder))
            throw new KeyNotFoundException("DirectorateChart RptOrder not found: " + rptOrder);
          DirectorateChartScoresByRptOrder[rptOrder].SetScore_Filtered(OrgSurveys.LinkLevel_Current, scoreSum, scoreCount);
        }

        public void DirectorateChart_CopyAllToFiltered_PreviousOnly() {
          foreach (var score in DirectorateChartScores) {
            score.CopyScore_AllToFiltered_PreviousOnly();
          }
        }

        public void DirectorateChart_CopyAllToFiltered_CurrentOnly() {
          foreach (var score in DirectorateChartScores) {
            score.CopyScore_AllToFiltered_CurrentOnly();
          }
        }

        public bool DivisionChart_DivisionIdExists(int divisionCodeId) {
          return DivisionChartScoresByCodeId.ContainsKey(divisionCodeId);
        }

        public void DivisionChart_ClearScores_PreviousFiltered() {
          foreach (var score in DivisionChartScores) score.ClearScore_PreviousFiltered();
        }

        public void DivisionChart_ClearScores_CurrentFiltered() {
          foreach (var score in DivisionChartScores) score.ClearScore_CurrentFiltered();
        }

        public void DivisionChart_AddScores(int thisCodeId, DivChartScores divScores) {
          DivisionChartScores.Add(divScores);
          DivisionChartScoresByCodeId.Add(thisCodeId, divScores);
        }

        public DivChartScores DivisionChart_GetScores(int divisionCodeId) {
          if (!DivisionChart_DivisionIdExists(divisionCodeId)) return null;
          return DivisionChartScoresByCodeId[divisionCodeId];
        }

        public void DivisionChart_SetScores_Filtered(int divisionCodeId, int linkLevel, int scoreSum, int scoreCount) {
          if (!DivisionChart_DivisionIdExists(divisionCodeId))
            throw new ApplicationException("Division CodeId not found: " + divisionCodeId);
          DivisionChartScoresByCodeId[divisionCodeId].SetScore_Filtered(linkLevel, scoreSum, scoreCount);
        }

        public void LikertQuestions_PreviousScores_CopyAllToFiltered() {
          foreach (var qn in LikertQuestions) {
            qn.PreviousScore_CopyAllToFiltered();
          }
        }

        public void LikertQuestions_PreviousScores_ClearFiltered() {
          foreach (var qn in LikertQuestions) {
            qn.PreviousScore_ClearFiltered();
          }
        }

        public void LikertQuestions_CurrentScores_ClearFiltered() {
          foreach (var qn in LikertQuestions) {
            qn.CurrentScore_ClearFiltered();
          }
        }

        public void LikertQuestions_CurrentScores_CopyAllToFiltered() {
          foreach (var qn in LikertQuestions) {
            qn.CurrentScore_CopyAllToFiltered();
          }
        }

        public bool HasDivisionFilterChanged(ReportFilters requestedFilters) {
          return !this.ReportFilters.IsDivisionListSameAs(requestedFilters);
        }

        public bool HasDemographicFilterChanged(ReportFilters requestedFilters) {
          return !this.ReportFilters.IsDemographicListSameAs(requestedFilters);
        }

        public bool HasBenchmarkFilterChanged(ReportFilters requestedFilters) {
          return !this.ReportFilters.IsBenchmarkTypeSameAs(requestedFilters);
        }

        public void SetReportFilterSQL(ReportFilterSQL reportFilterSQL) {
          this.ReportFilterSQL = reportFilterSQL;
        }

        public void GroupAndSortLikertQuestionsByQuadrant() {

          // Split IOI questions into groups based on Quadrant dimension.

          int rowNumber = 0;
          var dimensionInfo = OrgSurveys.GetDimensionInfo(OrgSurveys.DimensionEnum.Quadrants);

          Query($@"
            SELECT sq.QuestionId, sc.Code, sc.Value
            FROM sv_360_Questions sq
            INNER JOIN sv_360_AnswerTypes sat ON sat.SurveyId = sq.SurveyId
            INNER JOIN sv_360_Codes sc ON sc.AnswerTypeId = sat.AnswerTypeId
            WHERE sq.SurveyId = @SurveyId
              AND sq.GblQuestionId {OrgSurveys.SQL_BETWEEN_IOIGblQns}
            AND sat.AnswerTypeDescr = @AnswerTypeDescr_QuadrantDimension
              AND sc.Code = sq.{dimensionInfo.QuestionColumnName}
            ORDER BY sc.RptOrder1 DESC, sq.Sort",
            dr => {
              int questionId = dr.GetInt("QuestionId");
              int sectionCode = dr.GetInt("Code");
              string sectionTitle = dr.GetString("Value");
              var qn = this.LikertQuestions.Find(q => q.QuestionId == questionId);
              qn.ReportSortOrder = 1000; // Any questions not found will go at the end.
              if (qn != null) {
                rowNumber++;
                qn.ReportSortOrder = rowNumber;
                qn.ReportSectionCode = sectionCode;
                qn.ReportSectionTitle = sectionTitle;
              }
            },
            NewSqlParameter("SurveyId", this.SurveyInfo.SurveyId),
            NewSqlParameter("AnswerTypeDescr_QuadrantDimension", dimensionInfo.AnswerTypeDescr)
          );

          // Resort questions in preferred report order.
          this.LikertQuestions.Sort((r1, r2) => r1.ReportSortOrder.CompareTo(r2.ReportSortOrder));

        }


      }

      public class OpenTextQuestion {
        public int QuestionId;
        public int QuestionNum;
        public int AnswerTypeId;
        public string QuestionText = "";
        public string QuestionIds = "";
      }

      public class OpenTextAnswer {
        public int QuestionId;
        public string TextAnswer;
      }

      public class ReportFilters {

        // Current survey this report data is for.
        // public int SurveyId { get; private set; }
        // public string SurveyUId { get; private set; }

        public List<int> DivisionCodeIds { get; private set; } // Division codes selected from the UI.
        private string DivisionCodeIdString; // Above as comma delimitered string.
        public List<DemographicFilter> Demographics { get; private set; } // Demog. QuestionIds and CodeIds selected from the UI.
        public string DemographicsAsString { get; private set; }
        public int CategoryCode { get; private set; }
        public OrgReports.eBenchType BenchType { get; private set; }

        // public ReportFilters(string surveyUId) {
        //   Init(surveyUId);
        // }

        public ReportFilters() {
          Init();
        }

        public ReportFilters(string divCodeIdList, string demCodeIdList, int categoryCode, OrgReports.eBenchType benchType) {
          Init();
          SetDivisionCodeIdsFromString(divCodeIdList);
          SetDemographicsFromString(demCodeIdList);
          this.CategoryCode = categoryCode;
          this.BenchType = benchType;
        }

        private void Init() {
          DivisionCodeIds = new List<int>();
          DivisionCodeIdString = "";
          Demographics = new List<DemographicFilter>();
          DemographicsAsString = "";
          this.BenchType = OrgReports.eBenchType.Global;
        }

        public string GetDivisionCodeIdString() {
          return DivisionCodeIdString.EmptyIfNull();
        }

        public string GetDivisionCodeIdStringOrZero() {
          // Ensure the division code id list is returned as a proper list of numbers or return "0".
          if (DivisionCodeIdString.IsNullOrEmpty()) return "0";
          return DivisionCodeIdString.NumericCSVOrDefault("0");
        }

        public void CopyFrom(ReportFilters reportFilters) {
          this.DivisionCodeIds = new List<int>(reportFilters.DivisionCodeIds);
          this.DivisionCodeIdString = reportFilters.DivisionCodeIdString;
          this.Demographics = new List<DemographicFilter>(reportFilters.Demographics);
          this.DemographicsAsString = reportFilters.DemographicsAsString;
          this.CategoryCode = reportFilters.CategoryCode;
          this.BenchType = reportFilters.BenchType;
        }

        public void SetDivisionCodeIdsFromString(string divCodeIdList) {
          // This is usually set from the IDs coming from the UI.
          // The SQL queries restrict to the current survey, so it doesn't matter
          // if are tampered with in the UI, it just means no results will come back.

          DivisionCodeIdString = "";
          DivisionCodeIds.Clear();

          // Ensure string is correct format. "n,n,n,..."
          if (!Regex.IsMatch(divCodeIdList, "^[0-9]+(?:, *[0-9]+)*$")) return;
          DivisionCodeIdString = divCodeIdList.Replace(" ", "");
          DivisionCodeIds = new List<int>(Array.ConvertAll(DivisionCodeIdString.Split(','), int.Parse));
        }

        public void SetDemographicsFromString(string demCodeIdList) {
          // String is from query, pairs of IDs separated by commas.
          // First of the pair is QuestionId, second is CodeId.
          // e.g. "105632-171261,105633-171306"

          Demographics = new List<DemographicFilter>();

          foreach (var idPair in demCodeIdList.Split(',')) {
            var match = Regex.Match(idPair, "^([0-9]+)[^0-9]([0-9]+)$");
            if (match.Success) {
              int qnId, codeId;
              if (int.TryParse(match.Groups[1].Value, out qnId) && int.TryParse(match.Groups[2].Value, out codeId)) {
                Demographics.Add(new DemographicFilter(qnId, codeId));
              }
            }
          }
          UpdateDemographicsString();

        }

        public void SetDemographicsList(List<DemographicFilter> demogFilter) {
          if (demogFilter == null) Demographics.Clear();
          else Demographics = new List<DemographicFilter>(demogFilter); // copy it
          UpdateDemographicsString();
        }

        private void UpdateDemographicsString() {
          DemographicsAsString = "";
          foreach (var idPair in Demographics) {
            AddToDemographicsString(idPair.QuestionId, idPair.CodeId);
          }
        }

        private void AddToDemographicsString(int questionId, int codeId) {
          if (DemographicsAsString.Length > 0) DemographicsAsString += ",";
          DemographicsAsString += questionId + "-" + codeId;
        }

        public void ClearDemographicsList() {
          Demographics.Clear();
          DemographicsAsString = "";
        }

        public void unused_AddToDemographicsList(int questionId, int codeId) {
          Demographics.Add(new DemographicFilter(questionId, codeId));
          AddToDemographicsString(questionId, codeId);
        }

        public bool HasDivisions {
          get { return !this.DivisionCodeIdString.IsNullOrEmpty(); }
        }

        public bool IsDivisionListSameAs(ReportFilters otherFilters) {
          return this.DivisionCodeIdString == otherFilters.DivisionCodeIdString ? true : false;
        }

        public bool IsDemographicListSameAs(ReportFilters otherFilters) {
          return this.DemographicsAsString == otherFilters.DemographicsAsString ? true : false;
        }

        public bool IsBenchmarkTypeSameAs(ReportFilters otherFilters) {
          return this.BenchType == otherFilters.BenchType ? true : false;
        }

        public void SetBenchmarkType(OrgReports.eBenchType benchType) {
          this.BenchType = benchType;
        }

        public bool IsSameAs(ReportFilters otherFilters) {
          // Return false if any of the filters differ.
          if (this.DivisionCodeIdString != otherFilters.DivisionCodeIdString
            || this.DemographicsAsString != otherFilters.DemographicsAsString
            || this.CategoryCode != otherFilters.CategoryCode
            || this.BenchType != otherFilters.BenchType) return false;
          else return true;
        }

      }

      // Scores for the division bar graphs on the overview page.
      // This class is used for both the Directorate ("all levels") graphs
      // as well as the individual org level graphs.
      public class DivChartScores {

        public int DivCodeId { get; private set; }
        public string DivTitle { get; private set; }

        // Scores per survey. Each list item is a score in the sequence of surveys.
        // DivScores[0]=current survey, DivScores[1]=previous, DivScores[2]=next previous, etc.
        // Note only the current survey's scores are filtered by demographics.
        private List<double?> LinkLevelScores_All; // With no filters applied.
        private List<double?> LinkLevelScores_Filtered; // With division filters applied (all link levels) plus demog filter applied to link level 0 (current).

        // Constructor
        public DivChartScores(string directorateTitle) {
          Init(0, directorateTitle); // Directorates don't have IDs.
        }

        public DivChartScores(int divCodeId, string divTitle) {
          Init(divCodeId, divTitle);
        }

        private void Init(int divCodeId, string divTitle) {
          this.DivCodeId = divCodeId;
          this.DivTitle = divTitle;
          LinkLevelScores_All = new List<double?>(); // Contains scores for curent + previous surveys with no filters.
          LinkLevelScores_Filtered = new List<double?>(); // Contains scores for curent + previous surveys with filters.
          for (int linkLevel = 0; linkLevel <= OrgReports.Max_Previous_Surveys; linkLevel++) {
            LinkLevelScores_All.Add(null);
            LinkLevelScores_Filtered.Add(null);
          }
        }

        // Get score from the specified Link Level (0 = current survey, 1 = previous, 2 = next previous, etc)
        public double? GetScore(int linkLevel) {
          if (linkLevel < OrgSurveys.LinkLevel_Current || linkLevel > OrgReports.Max_Previous_Surveys) throw new ArgumentOutOfRangeException("linkLevel out of range (" + linkLevel + ")");
          if (LinkLevelScores_Filtered.Count <= linkLevel) return null;
          return LinkLevelScores_Filtered[linkLevel];
        }

        // Set score for the specified Link Level (0 = current survey, 1 = previous, 2 = next previous, etc)
        public void SetScore_All(int linkLevel, int scoreSum, int scoreCount) {
          if (linkLevel < OrgSurveys.LinkLevel_Current || linkLevel > OrgReports.Max_Previous_Surveys) throw new ArgumentOutOfRangeException("linkLevel out of range (" + linkLevel + ")");
          while (LinkLevelScores_All.Count <= linkLevel) LinkLevelScores_All.Add(null);
          while (LinkLevelScores_Filtered.Count <= linkLevel) LinkLevelScores_Filtered.Add(null);
          LinkLevelScores_All[linkLevel] = scoreCount > 0 ? (double?)scoreSum / scoreCount : null;
        }

        public void ClearAllScores_Filtered() {
          for (int linkLevel = OrgSurveys.LinkLevel_Current; linkLevel <= OrgReports.Max_Previous_Surveys; linkLevel++) {
            LinkLevelScores_Filtered[linkLevel] = null;
          }
        }

        public void ClearScore_PreviousFiltered() {
          for (int linkLevel = OrgSurveys.LinkLevel_Previous; linkLevel <= OrgReports.Max_Previous_Surveys; linkLevel++) {
            LinkLevelScores_Filtered[linkLevel] = null;
          }
        }

        public void ClearScore_CurrentFiltered() {
          LinkLevelScores_Filtered[OrgSurveys.LinkLevel_Current] = null;
        }

        public void SetScore_Filtered(int linkLevel, int scoreSum, int scoreCount) {
          if (linkLevel < OrgSurveys.LinkLevel_Current || linkLevel > OrgReports.Max_Previous_Surveys) throw new ArgumentOutOfRangeException("linkLevel out of range (" + linkLevel + ")");
          while (LinkLevelScores_Filtered.Count <= linkLevel) LinkLevelScores_Filtered.Add(null);
          LinkLevelScores_Filtered[linkLevel] = scoreCount > 0 ? (double?)scoreSum / scoreCount : null;
        }

        // Copy current survey unfiltered score to the current survey score.
        public void CopyScore_AllToFiltered_PreviousOnly() {
          for (int linkLevel = OrgSurveys.LinkLevel_Previous; linkLevel <= OrgReports.Max_Previous_Surveys; linkLevel++)
            LinkLevelScores_Filtered[linkLevel] = LinkLevelScores_All[linkLevel];
        }

        public void CopyScore_AllToFiltered_CurrentOnly() {
          LinkLevelScores_Filtered[OrgSurveys.LinkLevel_Current] = LinkLevelScores_All[OrgSurveys.LinkLevel_Current];
        }

      }

      public class SurveyInfo {
        public int SurveyId { get; private set; }
        public int SurveyYear { get; private set; }
        public SurveyInfo(int surveyId, int surveyYear) {
          this.SurveyId = surveyId;
          this.SurveyYear = surveyYear;
        }
      }

      public class YearlyIOIScores {

        public int SurveyId { get; private set; }
        public int SurveyYear { get; private set; }

        public double? SurveyScore_All { get; private set; } // Overall survey score without filters
        public double? SurveyScore_Filtered { get; private set; } // With filters (i.e by Div if historic, or by Div & Dem if current.
        public double? BenchmarkScore_NSA { get; private set; }
        public double? BenchmarkScore_Sector { get; private set; }
        public double? BenchmarkScore_Org { get; private set; }

        public YearlyIOIScores(
          int surveyId,
          int surveyYear,
          double? surveyScore_All,
          double? benchNSAScore,
          double? benchSectorScore,
          double? benchOrgScore
        ) {
          this.SurveyId = surveyId;
          this.SurveyYear = surveyYear;

          this.SurveyScore_All = surveyScore_All;
          this.SurveyScore_Filtered = surveyScore_All; // Initially the same as _All
          this.BenchmarkScore_NSA = benchNSAScore;
          this.BenchmarkScore_Sector = benchSectorScore;
          this.BenchmarkScore_Org = benchOrgScore;
        }

        public double? GetBenchScoreByType(OrgReports.eBenchType benchType) {
          if (benchType == OrgReports.eBenchType.Sector) {
            return BenchmarkScore_Sector;
          } else if (benchType == OrgReports.eBenchType.Organisation) {
            return BenchmarkScore_Org;
          } else {
            return BenchmarkScore_NSA;
          }
        }

        public void SetBenchScores(
          double surveyScore_All,
          double benchNSAScore,
          double benchSectorScore,
          double benchOrgScore
        ) {
          this.SurveyScore_All = surveyScore_All;
          this.BenchmarkScore_NSA = benchNSAScore;
          this.BenchmarkScore_Sector = benchSectorScore;
          this.BenchmarkScore_Org = benchOrgScore;
        }

        public void SetFilteredSurveyScore(double? score) {
          SurveyScore_Filtered = score;
        }
      }

      public class DemographicFilter {
        public int QuestionId { get; private set; }
        public int CodeId { get; private set; }
        public DemographicFilter(int questionId, int codeId) {
          QuestionId = questionId;
          CodeId = codeId;
        }
      }

      public class QuestionInfo {

        public int ReportSectionCode { get; set; }
        public string ReportSectionTitle { get; set; }
        public int ReportSortOrder { get; set; }

        public int QuadrantCode { get; private set; }
        public string QuadrantTitle { get; private set; }
        public int DriverCode { get; private set; }
        public string DriverTitle { get; private set; }
        public int QuestionId { get; private set; }
        public int GblQuestionId { get; private set; }
        public int GblAnswerTypeId { get; private set; }
        public string InputType { get; private set; }
        public int Sort { get; private set; }
        public int AutoNumber { get; private set; }
        public string QuestionText { get; private set; }
        public Questions.ScoreParam Score_Survey_All { get; private set; } // Results from all participants.
        public Questions.ScoreParam Score_Survey_Filtered { get; private set; } // Results from filtered participants (e.g. org level, demog)
        public Questions.ScoreParam Score_Benchmark_NSA { get; private set; }
        public Questions.ScoreParam Score_Benchmark_Sector { get; private set; }
        public Questions.ScoreParam Score_Benchmark_Organisation { get; private set; }
        public Questions.ScoreParam Score_PreviousSurvey_All { get; private set; } // Prev survey score for this question - not filtered (all participants)
        public Questions.ScoreParam Score_PreviousSurvey_Filtered { get; private set; } // As above, filtered by the linked divisions.
        //public List<Questions.ScoreParam> Score_PreviousSurveys_All { get; private set; } // Item 0 = previous, item 1 = previous to that, etc.
        //public List<Questions.ScoreParam> Score_PreviousSurveys_Filtered { get; private set; } // Item 0 = previous, item 1 = previous to that, etc.

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
          string inputType,
          int sort,
          int autoNumber,
          string questionText,
          Questions.ScoreParam score_Survey_All,
          Questions.ScoreParam score_PrevSurvey_All,
          Questions.ScoreParam score_Benchmark_NSA,
          Questions.ScoreParam score_Benchmark_Organisation,
          Questions.ScoreParam score_Benchmark_Sector
        ) {
          ReportSectionCode = reportSectionCode;
          ReportSectionTitle = reportSectionTitle;
          ReportSortOrder = 1;

          QuadrantCode = quadrantCode;
          QuadrantTitle = quadrantTitle;
          DriverCode = driverCode;
          DriverTitle = driverTitle;
          QuestionId = questionId;
          GblQuestionId = gblQuestionId;
          GblAnswerTypeId = gblAnswerTypeId;
          InputType = inputType;
          Sort = sort;
          AutoNumber = autoNumber;
          QuestionText = questionText;

          Score_Survey_All = new Questions.ScoreParam(score_Survey_All);
          Score_Survey_Filtered = new Questions.ScoreParam(score_Survey_All); // Initially the same.
          Score_PreviousSurvey_All = new Questions.ScoreParam(score_PrevSurvey_All);
          Score_PreviousSurvey_Filtered = new Questions.ScoreParam(score_PrevSurvey_All); // Initially the same.
          Score_Benchmark_NSA = new Questions.ScoreParam(score_Benchmark_NSA);
          Score_Benchmark_Organisation = new Questions.ScoreParam(score_Benchmark_Organisation);
          Score_Benchmark_Sector = new Questions.ScoreParam(score_Benchmark_Sector);

          // Reset other scores, which are calculated later.
          // Score_PreviousSurveys_All = new List<Questions.ScoreParam>();
          // Score_PreviousSurveys_Filtered = new List<Questions.ScoreParam>();
          // for (int i = 0; i < OrgReports.Max_Previous_Surveys; i++) {
          //   Score_PreviousSurveys_All.Add(new Questions.ScoreParam(null, null));
          //   Score_PreviousSurveys_Filtered.Add(new Questions.ScoreParam(null, null));
          // }
        }

        // Convert score to the QuestionScores type.
        public Questions.QuestionScores ToQuestionScores(OrgReports.eBenchType benchType) {
          return new Questions.QuestionScores(Score_Survey_Filtered, Score_PreviousSurvey_Filtered, GetBenchmarkScore(benchType));
        }

        public void PreviousScore_ClearFiltered() {
          Score_PreviousSurvey_Filtered.ClearScore();
        }
        public void SetPreviousScore_Filtered(int scoreSum, int scoreCount) {
          Score_PreviousSurvey_Filtered.SetScore(scoreSum, scoreCount);
        }
        public void PreviousScore_CopyAllToFiltered() {
          Score_PreviousSurvey_Filtered.SetScore(Score_PreviousSurvey_All);
        }

        public void CurrentScore_ClearFiltered() {
          Score_Survey_Filtered.ClearScore();
        }
        public void CurrentScore_CopyAllToFiltered() {
          Score_Survey_Filtered.SetScore(Score_Survey_All);
        }
        public void SetCurrentScore_Filtered(int scoreSum, int scoreCount) {
          Score_Survey_Filtered.SetScore(scoreSum, scoreCount);
        }

        public Questions.ScoreParam GetBenchmarkScore(OrgReports.eBenchType benchType) {
          if (benchType == OrgReports.eBenchType.Organisation)
            return Score_Benchmark_Organisation;
          else if (benchType == OrgReports.eBenchType.Sector)
            return Score_Benchmark_Sector;
          else
            return Score_Benchmark_NSA;
        }

      }

    } // OrgReports
  } // DbHelper
}
