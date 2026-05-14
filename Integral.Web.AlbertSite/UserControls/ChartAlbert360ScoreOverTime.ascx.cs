using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using System.Globalization;


namespace Integral.Web.PortalSite.UserControls {

  public partial class ChartAlbert360ScoreOverTime : System.Web.UI.UserControl {

    public double? selfScore;
    public double? selfBenchMark;
    public double? raterScore;
    public double? raterBenchMark;
    public string xAxis;
    public string selfScoreDS;
    public string selfBenchMarkDS;
    public string raterScoreDS;
    public string raterBenchMarkDS;
    public enum eBenchType { Organisation, Global };
    public eBenchType benchTypeName;

    Guid urlCoacheeUID;
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo;
    public DbHelper.Questions.QuestionScores overallScores;
    public DbHelper.Reports.Coachee360.Coachee360Results reportResults;

    protected void Page_Load(object sender, EventArgs e) {

      if (!Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeGuid).EmptyIfNull(), out urlCoacheeUID)) return;

      coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(urlCoacheeUID);
      if (coacheeInfo == null) return;

      string benchType = WebHelper.GetQueryStringValue("benchType");
      if (benchType.IsNullOrEmpty() || benchType != "o" && benchType != "g") benchType = "o"; // default to Organisation
      benchTypeName = benchType == "o" ? eBenchType.Organisation : eBenchType.Global;

      // Get results by question.
      // var results = DbHelper.Reports.Coachee360.GetResults_CoacheeLatestSurvey(coacheeInfo.CoacheeId, benchType == "o" ? coacheeInfo.CompanyId : null);
      string urlSelectedSurveyUId = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId); // selected survey to show.
      reportResults = DbHelper.Reports.Coachee360.GetCoachee360ReportResults(coacheeInfo.CoacheeId, urlSelectedSurveyUId, benchType == "o" ? coacheeInfo.CompanyId : null);
      if (reportResults == null) return;

      // Go thru results (individual questions) and accumulate overall averages.
      var overallScores = new DbHelper.Questions.QuestionScores();
      foreach (var qnInfo in reportResults.ReportQuestions) {
        overallScores.AccumulateScores(qnInfo.Scores);
      }

      selfScore = overallScores.ScoreSelf.Avg;
      selfBenchMark = overallScores.ScoreBenchSelf.Avg;
      raterScore = overallScores.ScoreRater.Avg;
      raterBenchMark = overallScores.ScoreBenchRater.Avg;

      var scoreList = new List<ScoreListItem>();

      using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
        using (var cmd = new SqlCommand(@"

          WITH gbl
          AS (
            SELECT DATEDIFF(MONTH, @OriginDate, p.Completed) AS MonthDiff,
              YEAR(p.Completed) AS SvYear,
              MONTH(p.Completed) AS SvMonth,
              AVG(IIF(p.IsSelf = 1, c.ValueScore, NULL)) AS ScoreGlobalSelf,
              AVG(IIF(p.IsSelf = 0, c.ValueScore, NULL)) AS ScoreGlobalRater
            FROM sv_Survey s WITH (NOLOCK)
            INNER JOIN sv_360_Participants p ON p.SurveyId = s.sv_id
            INNER JOIN sv_Answers a ON a.ParticipantId = p.PartId
            INNER JOIN sv_360_Codes c ON a.CodeId = c.CodeId
            INNER JOIN sv_360_Questions q ON q.QuestionId = a.QuestionId
            INNER JOIN sv_GblQuestions gq ON q.GblQuestionId = gq.GblQuestionId
            WHERE s.IsAlbertSurvey = 1
              --AND s.sv_ReportType = @ReportType
              AND s.AlbertRatersOnly = 0
              AND q.GblAnswerTypeId = 2801 -- LMP Global Ans Type
              AND gq.GblQnGroupId = 22 -- LMP std qns used in Able
              AND p.Completed IS NOT NULL
            GROUP BY DATEDIFF(MONTH, @OriginDate, p.Completed),
            YEAR(p.Completed),
            MONTH(p.Completed) --, p.IsSelf
          ),
          pax AS (
            SELECT DATEDIFF(MONTH, @OriginDate, pc.Completed) AS MonthDiff,
              YEAR(pc.Completed) AS SvYear,
              MONTH(pc.Completed) AS SvMonth,
              AVG(IIF(ac.CompanyId = @CompanyId AND p.IsSelf = 1, c.ValueScore, NULL)) AS ScoreOrgSelf,
              AVG(IIF(ac.CompanyId = @CompanyId AND p.IsSelf = 0, c.ValueScore, NULL)) AS ScoreOrgRater,
              AVG(IIF(ac.CoacheeId = @CoacheeId AND p.IsSelf = 1, c.ValueScore, NULL)) AS ScoreCoacheeSelf,
              AVG(IIF(ac.CoacheeId = @CoacheeId AND p.IsSelf = 0, c.ValueScore, NULL)) AS ScoreCoacheeRater
            FROM al_Coachees ac WITH (NOLOCK)
            INNER JOIN sv_360_Participants pc ON ac.CoacheeId = pc.AbleCoacheeId
            INNER JOIN sv_Survey s ON s.sv_id = pc.SurveyId
            INNER JOIN sv_360_Participants p ON p.SurveyId = pc.SurveyId
            INNER JOIN sv_360_Questions q ON q.SurveyId = pc.SurveyId
            INNER JOIN sv_GblQuestions gq ON q.GblQuestionId = gq.GblQuestionId
            INNER JOIN sv_Answers a ON a.ParticipantId = p.PartId AND a.QuestionId = q.QuestionId
            INNER JOIN sv_360_Codes c ON a.CodeId = c.CodeId
            WHERE ac.CompanyId = @CompanyId
              AND s.IsAlbertSurvey = 1
              AND s.AlbertRatersOnly = 0
              AND q.GblAnswerTypeId = 2801 -- LMP Global Ans Type
              AND gq.GblQnGroupId = 22 -- LMP std qns used in Able
              AND pc.Completed IS NOT NULL
              AND p.Completed IS NOT NULL
              AND (p.PartId = pc.PartId
                OR p.Self_PartId = pc.PartId)
            GROUP BY DATEDIFF(MONTH, @OriginDate, pc.Completed),
            YEAR(pc.Completed),
            MONTH(pc.Completed)
          )
          SELECT
            gbl.MonthDiff, gbl.SvYear, gbl.SvMonth,
            CAST(gbl.ScoreGlobalSelf AS DECIMAL(5, 2)) AS ScoreGlobalSelf,
            CAST(gbl.ScoreGlobalRater AS DECIMAL(5, 2)) AS ScoreGlobalRater,
            CAST(pax.ScoreOrgSelf AS DECIMAL(5, 2)) AS ScoreOrgSelf,
            CAST(pax.ScoreOrgRater AS DECIMAL(5, 2)) AS ScoreOrgRater,
            CAST(pax.ScoreCoacheeSelf AS DECIMAL(5, 2)) AS ScoreCoacheeSelf,
            CAST(pax.ScoreCoacheeRater AS DECIMAL(5, 2)) AS ScoreCoacheeRater
          FROM gbl WITH (NOLOCK)
          LEFT OUTER JOIN pax on gbl.MonthDiff = pax.MonthDiff
          ORDER BY gbl.MonthDiff;

        ", conn)) {

          // cmd.Parameters.AddVarChar("@ReportType", 20, "able360");
          cmd.CommandTimeout = 120;
          cmd.Parameters.AddInt("@CompanyId", coacheeInfo.CompanyId);
          cmd.Parameters.AddInt("@CoacheeId", coacheeInfo.CoacheeId);
          cmd.Parameters.AddDateTime("@OriginDate", new DateTime(2018, 1, 1));
          LogHelper.LogLatestSQL(cmd);
          conn.Open();
          using (SqlDataReader dr = cmd.ExecuteReader()) {
            while (dr.Read()) {
              scoreList.Add(new ScoreListItem(
                dr.GetInt("SvMonth"),
                dr.GetDoubleOrNull("ScoreCoacheeSelf"),
                benchTypeName == eBenchType.Global ? dr.GetDoubleOrNull("ScoreGlobalSelf") : dr.GetDoubleOrNull("ScoreOrgSelf"),
                dr.GetDoubleOrNull("ScoreCoacheeRater"),
                benchTypeName == eBenchType.Global ? dr.GetDoubleOrNull("ScoreGlobalRater") : dr.GetDoubleOrNull("ScoreOrgRater")
              ));
            }
          }
        }
      }

      CreateChartData(scoreList);
    }

    private void CreateChartData(List<ScoreListItem> scoreList) {
      if (scoreList.Count > 0) {

        // Create chart data
        xAxis = "[";
        selfScoreDS = "[";
        selfBenchMarkDS = "[";
        raterScoreDS = "[";
        raterBenchMarkDS = "[";
        int cntMnth = scoreList.Last().ScoreMonth;
        for (int i = 0; i <= 8; i++) {
          if (i < scoreList.Count) {
            xAxis += "'" + DateTimeFormatInfo.CurrentInfo.GetAbbreviatedMonthName(scoreList[i].ScoreMonth) + "'";
            selfScoreDS += GetScoreFormatted(scoreList[i].SelfScore, false);
            selfBenchMarkDS += GetScoreFormatted(scoreList[i].SelfBenchMark, false);
            raterScoreDS += GetScoreFormatted(scoreList[i].RaterScore, false);
            raterBenchMarkDS += GetScoreFormatted(scoreList[i].RaterBenchMark, false);
          } else {
            cntMnth++;
            if (cntMnth > 12)
              cntMnth = 1;

            xAxis += "'" + DateTimeFormatInfo.CurrentInfo.GetAbbreviatedMonthName(cntMnth) + "'";
            selfScoreDS += "";
            selfBenchMarkDS += "";
            raterScoreDS += "";
            raterBenchMarkDS += "";
          }

          xAxis += (i < 8) ? ", " : "";
          selfScoreDS += (i < 8) ? ", " : "";
          selfBenchMarkDS += (i < 8) ? ", " : "";
          raterScoreDS += (i < 8) ? ", " : "";
          raterBenchMarkDS += (i < 8) ? ", " : "";
        }
        xAxis += "]";
        selfScoreDS += "]";
        selfBenchMarkDS += "]";
        raterScoreDS += "]";
        raterBenchMarkDS += "]";
      } else {
        //no result to show
      }

    }

    public class ScoreListItem {
      public int ScoreMonth;
      public double? SelfScore;
      public double? SelfBenchMark;
      public double? RaterScore;
      public double? RaterBenchMark;
      public ScoreListItem(
        int scoreMonth,
        double? selfScore,
        double? selfBenchMark,
        double? raterScore,
        double? raterBenchMark
      ) {
        this.ScoreMonth = scoreMonth;
        this.SelfScore = selfScore;
        this.SelfBenchMark = selfBenchMark;
        this.RaterScore = raterScore;
        this.RaterBenchMark = raterBenchMark;
      }
    }

    public string GetScoreFormatted(double? scoreValue, bool replaceNA) {
      if (replaceNA)
        return scoreValue.HasValue ? Math.Round(scoreValue.Value * 10, 0, MidpointRounding.AwayFromZero).ToString() : "NA";
      else
        return scoreValue.HasValue ? Math.Round(scoreValue.Value * 10, 0, MidpointRounding.AwayFromZero).ToString() : "";
    }

  }
}
