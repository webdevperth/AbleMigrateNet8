using System;
using System.Collections.Generic;
using static Integral.Web.DbHelper.OrgReports;

// General helpers for HTML Org reporting.

namespace Integral.Web.PortalSite.Reports {

  public class OrgReports {

    public const int Bars_Min_Negative_Score = -20; // The left-most (minimum) value of the bars.
    public const int Bars_Max_Score = 100; // The right-most (maximum) value of the bars.

    public const int Max_IOI_GblQuestionId = 32;
    public const int Min_Questions_Per_Category = 2; // Don't show sections with less questions than this.
    public const char CombinedUIDs_SplitChar = '-';
    public const string Benchmark_Display_Text = "norm"; // What to call it on the screen - used to be "benchmark", now is "norm".

    public readonly static string[] LinkLevelColours = { "#4BA7FE", "#9894E3", "#6FCAA8", "#FBC050" }; // bar colours for each survey. [0]=current, [1]=previous, etc.

    private const string SessionVar_BenchType = "OrgReports_BenchType";
    private const string QueryString_OrgLevels = "orgLevels";
    private const string QueryString_Demographics = "demog";
    private const string QueryString_BenchType = "benchType";
    private const string QueryString_ExternalIOS_CombinedSurveyPartUIDs = "id";
    private const string QueryString_Dimension = "category";
    private const string QueryString_Reload = "reload";

    public static bool IsReloadRequested() {
      return !WebHelper.GetQueryStringValue(QueryString_Reload).IsNullOrEmpty();
    }

    // "External" IOS reports have a different querystring structure to internal Client IOS reports.
    // SurveyUID and ParticipantUID are combined in one value, separated by a dash.
    public static void GetExternalIOSUIDsFromQuery(out string surveyUID, out string partUID) {
      surveyUID = null;
      partUID = null;
      string uid = WebHelper.GetQueryStringValue(QueryString_ExternalIOS_CombinedSurveyPartUIDs).ToStringOrEmptyIfNull();
      if (!uid.Contains(CombinedUIDs_SplitChar.ToString())) return;
      var uids = uid.Split(CombinedUIDs_SplitChar);
      if (DbHelper.AlbertSurveys.IsValidUniqueId(uids[0]) && DbHelper.Participants.IsValidUniqueId(uids[1])) {
        surveyUID = uids[0];
        partUID = uids[1];
      }
      return;
    }

    public static DbHelper.OrgReportsCached.ReportData GetReportData(string surveyUID, string partUID, bool clearCachedResults = false) {
      string sessionId = SystemWeb.SessionID;
      var requestedFilters = GetReportFiltersFromQuery();
      return DbHelper.OrgReportsCached.GetReportData(new DbHelper.OrgReportsCached.GetReportDataParams(surveyUID, partUID, sessionId, requestedFilters), clearCachedResults);
    }

    public static DbHelper.OrgReportsCached.ReportParticipantInfo GetReportParticipantInfo(string surveyUID, string partUID) {
      return DbHelper.OrgReportsCached.GetReportParticipantInfo(surveyUID, partUID);
    }

    public static DbHelper.OrgReportsCached.ReportFilters GetReportFiltersFromQuery() {
      string divCodeIdList = GetOrgLevelsFromQuery();
      string demCodeIdList = GetDemographicsFromQuery();
      int categoryCode = GetCategoryFromQuery();
      var benchType = GetBenchTypeFromQuery();
      return new DbHelper.OrgReportsCached.ReportFilters(divCodeIdList, demCodeIdList, categoryCode, benchType);
    }

    public static List<BenchmarkOption> BenchmarkOptions =
      new List<BenchmarkOption>() {
        new BenchmarkOption("Global", "g"),       // index 0
        new BenchmarkOption("Industry", "i"),     // index 1
        new BenchmarkOption("Organisation", "o")  // index 2
      };

    public static eBenchType GetBenchTypeFromQuery() {
      string optValue = WebHelper.GetQueryStringValue(QueryString_BenchType).ToStringOrEmptyIfNull();
      switch (optValue) {
        case "i":
          return eBenchType.Sector;
        case "o":
          return eBenchType.Organisation;
        default:
          return eBenchType.Global;
      }
    }

    public static string GetOptValueFromBenchType(eBenchType benchType) {
      switch (benchType) {
        case eBenchType.Sector:
          return "i";
        case eBenchType.Organisation:
          return "o";
        default:
          return "g";
      }
    }

    public static string GetOrgLevelsFromQuery() {
      // Returns comma list of division code IDs or blank for all.
      return WebHelper.GetQueryStringValue(QueryString_OrgLevels).ToStringOrEmptyIfNull();
    }

    public static string GetDemographicsFromQuery() {
      // Returns comma list of division code IDs or blank for all.
      return WebHelper.GetQueryStringValue(QueryString_Demographics).ToStringOrEmptyIfNull();
    }

    public static int GetCategoryFromQuery() {
      int dimension = (int)ValidDimensions.Quadrants; // default
      int.TryParse(WebHelper.GetQueryStringValue(QueryString_Dimension).ToStringOrEmptyIfNull(), out dimension);
      return dimension;
    }

    public static void RememberBenchType(eBenchType benchType) {
      SystemWeb.SetSessionValue(SessionVar_BenchType, GetOptValueFromBenchType(benchType));
    }

    public static string GetBenchTypeNameAbbrev(eBenchType benchType) {
      if (benchType == eBenchType.Organisation) return "Org";
      if (benchType == eBenchType.Sector) return "Industry";
      else return benchType.ToString();
    }

    public class BenchmarkOption {
      public string OptionTitle { get; private set; }
      public string OptionValue { get; private set; }
      internal BenchmarkOption(string optionTitle, string optionValue) {
        this.OptionTitle = optionTitle;
        this.OptionValue = optionValue;
      }
    }

    public static List<QuestionInfo> GetStandardResults(DbHelper.OrgSurveys.SurveyInfo surveyInfo, eBenchType benchType, string divisionCodeIds) {
      return DbHelper.OrgReports.GetStandardResults(
        new GetStandardResultsParams(surveyInfo.SurveyId, surveyInfo.CompanyId,
        divisionCodeIds,
        benchType == eBenchType.Organisation ? (int?)surveyInfo.CompanyId : null,
        benchType == eBenchType.Sector ? surveyInfo.SectorId : null));
    }

    public class HTML {

      private static string scoreBarHTMLTemplate = @"
        <div class=""scoreBars"">
          <div class=""scoreBar"">
            <div class=""barTitle"">[BenchTypeName]</div>
            <div class=""barBg"">
              <span class=""barLine barSelf"" style=""width:[BarWidthPercent]%""></span>
              <span class=""barMark"" style=""left: [ZeroMarkPosPercent]%;""></span>
              <span class=""barDot dotSelf"" title=""[BenchScore]"" style=""left:[BenchMarkPosPercent]%""></span>
            </div>
            <div class=""barScore"">[SurveyScore]</div>
          </div>
        </div>" + "\n";

      static int totalBarScoreRange;

      static HTML() {
        totalBarScoreRange = Bars_Max_Score + Math.Abs(Bars_Min_Negative_Score); // e.g. if bars start at -20 then total range is 120
        scoreBarHTMLTemplate = scoreBarHTMLTemplate.ReplaceTags(new Dictionary<string, string>() {
          { "ZeroMarkPosPercent", GetBarPosPercent(0).ToString() }
        });
      }

      public static string GetScoreBarHTML(eBenchType benchType, double? surveyScore, double? benchScore) {
        string html = scoreBarHTMLTemplate.ReplaceTags("[", "]", new Dictionary<string, string>() {
          { "BenchTypeName", BenchTypeAbbrev[benchType] },
          { "SurveyScore", OrgReports.HTML.GetFormattedScore(surveyScore) },
          { "BarWidthPercent", OrgReports.HTML.GetBarPosPercent(surveyScore).ToString() },
          { "BenchScore", OrgReports.HTML.GetFormattedScore(benchScore, "null") },
          { "BenchMarkPosPercent", Math.Max(0, OrgReports.HTML.GetBarPosPercent(benchScore)).ToString() }
        });
        return html;
      }

      public static int GetBarPosPercent(double? score) {
        if (score == null) score = Bars_Min_Negative_Score; // for now, null will show the min value (i.e. bar length will be zero).
        int posPercent = (int)((score - Bars_Min_Negative_Score) / totalBarScoreRange * 100 + 0.5);
        return posPercent;
      }

      public static string GetFormattedScore(double? score, string valueIfNull = "") {
        if (score == null) return valueIfNull;
        return Math.Round((double)score, 0, MidpointRounding.AwayFromZero).ToString();
      }


    } // HTML

    public class SimpleTable {
      // Note also serves as base class for varied table types.
      public string TableTitle { get; private set; }
      public SimpleTable(string tableTitle) {
        this.TableTitle = tableTitle;
      }
    }

    public class SimpleTableRow {
      // Note also serves as base class for varied table types.

      public string RowText { get; protected set; }
      public DbHelper.Questions.QuestionScores RowScores { get; protected set; }
      // Item counter:
      public int RowCount { get; set; }
      public int ItemCount { get; private set; } // No. items represented in this row.

      public SimpleTableRow(string rowText, DbHelper.Questions.QuestionScores initialScores) {
        RowText = rowText;
        RowScores = new DbHelper.Questions.QuestionScores(initialScores);
        ItemCount = 1; // Initial question added.
      }

      public void AccumulateScores(DbHelper.Questions.QuestionScores scores) {
        RowScores.AccumulateScores(scores);
        ItemCount++; // Subsequent questions added to this row.
      }

      public string GetRowProgressClass() {
        if (RowScores.ScoreSelf.Avg == null) return "";
        string rtn = "";
        if (RowScores.ScorePreviousSelf.Avg == null) {
          rtn = rtn.AppendWithSeparator(" ", "NoPreviousScore");
        } else {
          if (RowScores.ScorePreviousSelf.Avg < RowScores.ScoreSelf.Avg)
            rtn = rtn.AppendWithSeparator(" ", "progressUp");
          else if (RowScores.ScorePreviousSelf.Avg > RowScores.ScoreSelf.Avg)
            rtn = rtn.AppendWithSeparator(" ", "progressDown");
          else
            rtn = rtn.AppendWithSeparator(" ", "progressEqual");
        }
        if (RowScores.ScoreBenchSelf.Avg != null) {
          if (RowScores.ScoreSelf.Avg < RowScores.ScoreBenchSelf.Avg)
            rtn = rtn.AppendWithSeparator(" ", "benchBelow");
        }
        return rtn;
      }

      public string GetProgressPercent() {
        var prevScore = RowScores.ScorePreviousSelf.Avg;
        var thisScore = RowScores.ScoreSelf.Avg;
        if (prevScore == null || thisScore == null) return " - ";
        return (thisScore / prevScore * 100 - 100).RoundAwayFromZeroOrNull(1).ToString();
      }

      public string GetPreviousScoreText() {
        var prevScore = RowScores.ScorePreviousSelf.Avg;
        if (prevScore == null) return " - ";
        return ((double)prevScore).RoundAwayFromZero().ToString();
      }

      public string GetProgressPercentTitle() {
        return RowScores.ScorePreviousSelf.Avg.RoundAwayFromZeroOrNull().ToStringOrEmptyIfNull();
      }

      public string GetScoreBarHTML(eBenchType benchType) {
        return HTML.GetScoreBarHTML(benchType, RowScores.ScoreSelf.Avg, RowScores.ScoreBenchSelf.Avg);
      }

    } // SimpleTableRow

    public enum FocusTableMode { Highest = 1, Lowest = 2 }

    public class FocusTable : SimpleTable { // Simple table + highest/lowest mode.
      public FocusTableMode TableMode { get; private set; }
      public FocusTable(string tableTitle, FocusTableMode tableMode) : base(tableTitle) {
        this.TableMode = tableMode;
      }
    }

    public class FocusTableRow : SimpleTableRow { // Just adds a section title.
      public string QuestionNumber { get; private set; }
      public string SectionTitle { get; private set; }
      public FocusTableRow(string questionNumber, string rowText, DbHelper.Questions.QuestionScores initialScores, string sectionTitle = "") : base(rowText, initialScores) {
        QuestionNumber = questionNumber;
        SectionTitle = sectionTitle;
      }
    }

    public class CategoryTable : SimpleTable { // Simple table + Dimension
      public ValidDimensions Dimension { get; private set; }
      public CategoryTable(string tableTitle, ValidDimensions dimension) : base(tableTitle) {
        this.Dimension = dimension;
      }
    }

    public class CategoryTableRow : SimpleTableRow {

      public double ProgressPercent { get; private set; } // -ve if lower (down arrow) or +ve if higher (up arrow). e.g. 2.6, 6.6, -4.6, etc.
      public double BenchComparisonPoints { get; private set; } // as above, -ve if below benchmark, +ve if above benchmark.

      public CategoryTableRow(string rowText, DbHelper.Questions.QuestionScores initialScores) : base(rowText, initialScores) {
        // Same as base class + recalulate the "progress" values.
        CalcProgress();
      }

      public new void AccumulateScores(DbHelper.Questions.QuestionScores scores) {
        // Same as base class + recalulate the "progress" values.
        base.AccumulateScores(scores);
        CalcProgress();
      }

      public void CalcProgress() {
        ProgressPercent = 0;
        BenchComparisonPoints = 0;
        if (RowScores.ScoreSelf.Avg == null) return;
        if (RowScores.ScoreSelf.Avg != null && RowScores.ScorePreviousSelf.Avg != null && RowScores.ScorePreviousSelf.Avg > 0) {
          ProgressPercent = (double)RowScores.ScoreSelf.Avg.RoundAwayFromZeroOrNull().Value / RowScores.ScorePreviousSelf.Avg.RoundAwayFromZeroOrNull().Value * 100 - 100;
        }
        if (RowScores.ScoreSelf.Avg != null && RowScores.ScoreBenchSelf.Avg != null)
          BenchComparisonPoints = RowScores.ScoreSelf.Avg.RoundAwayFromZeroOrNull().Value - RowScores.ScoreBenchSelf.Avg.RoundAwayFromZeroOrNull().Value;
      }

      public string GetProgressPercentGlyphicon() {
        if (ProgressPercent > 0)
          return "arrow-up";
        else if (ProgressPercent < 0)
          return "arrow-down";
        else
          return "option-horizontal";
      }

      public string GetBenchComparisonPointsText() {
        string comparison;
        if (BenchComparisonPoints == 0) {
          comparison = "Same as";
        } else {
          comparison = "<span class=\"progPts\">" + Math.Abs(BenchComparisonPoints) + "pt</span> ";
          if (BenchComparisonPoints > 0) comparison += "above";
          else comparison += "below";
        }
        comparison += " " + Benchmark_Display_Text;
        return comparison;
      }

    } // CategoryTableRow

  } // OrgReports
}
