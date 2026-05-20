using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the ChartAlbert360FunctionTable ViewComponent. Mirrors the public API
  // of the legacy UserControls/ChartAlbert360FunctionTable.ascx.cs codebehind so the
  // .cshtml view can render the table rows identically to the original markup.
  public class ChartAlbert360FunctionTableModel {

    public enum eBenchType { Organisation, Global }

    public DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo;
    public DbHelper.Reports.Coachee360.Coachee360Results reportResults;

    public eBenchType benchTypeName;

    public int TableRowCount = 0;

    public Dictionary<int, TableRowInfo> TableRows = new Dictionary<int, TableRowInfo>();

    public void Initialize() {

      foreach (var qnInfo in reportResults.ReportQuestions) {
        if (!TableRows.ContainsKey(qnInfo.RptQnGrpHeadingSort.GetValueOrDefault(0))) {
          TableRows.Add(qnInfo.RptQnGrpHeadingSort.GetValueOrDefault(0), new TableRowInfo(qnInfo));
        } else {
          TableRows[qnInfo.RptQnGrpHeadingSort.GetValueOrDefault(0)].AddQnInfo(qnInfo);
        }
      }
    }

    public string GetRowProgressClass(TableRowInfo tableRowItem) {
      if (tableRowItem.QuestionInfo.Scores.ScoreSelf.Avg == null) return "";
      string rtn = "";
      if (tableRowItem.QuestionInfo.Scores.ScorePreviousSelf.Avg == null) {
        rtn = rtn.AppendWithSeparator(" ", "NoPreviousScore");
      } else {
        if (tableRowItem.QuestionInfo.Scores.ScorePreviousSelf.Avg < tableRowItem.QuestionInfo.Scores.ScoreSelf.Avg)
          rtn = rtn.AppendWithSeparator(" ", "progressUp");
        else if (tableRowItem.QuestionInfo.Scores.ScorePreviousSelf.Avg > tableRowItem.QuestionInfo.Scores.ScoreSelf.Avg)
          rtn = rtn.AppendWithSeparator(" ", "progressDown");
        else
          rtn = rtn.AppendWithSeparator(" ", "progressEqual");
      }
      if (tableRowItem.QuestionInfo.Scores.ScoreBenchSelf.Avg != null) {
        if (tableRowItem.QuestionInfo.Scores.ScoreSelf.Avg < tableRowItem.QuestionInfo.Scores.ScoreBenchSelf.Avg)
          rtn = rtn.AppendWithSeparator(" ", "benchBelow");
      }
      return rtn;
    }

    public string GetProgressPercentGlyphicon(TableRowInfo tableRowItem) {
      if (tableRowItem.ProgressPercent > 0)
        return "arrow-up";
      else if (tableRowItem.ProgressPercent < 0)
        return "arrow-down";
      else
        return "option-horizontal";
    }

    public string GetProgressPercent(TableRowInfo tableRowItem) {
      return tableRowItem.ProgressPercent.ToString("0.0");
    }

    public string GetBenchComparisonPoints(TableRowInfo tableRowItem) {
      return Math.Abs(tableRowItem.BenchComparisonPoints * 10).ToString("0");
    }

    public string GetBenchComparisonPointsText(TableRowInfo tableRowItem) {
      return (tableRowItem.BenchComparisonPoints >= 0 ? "above " : "below ") + benchTypeName + " " + OrgReports.Benchmark_Display_Text;
    }

    public string GetCSSPercentFromScore(double? score) {
      if (score == null) return "-";
      return ((double)score * 10).ToString("0") + "%";
    }

    public string GetScoreFormatted(double? score) {
      if (score == null) return "-";
      return ((double)score * 10).ToString("0");
    }

    public class TableRowInfo {
      // Note all scores are stored here out of 10 (as it comes from the database) not out of 100.

      public DbHelper.Questions.ReportQuestionInfo QuestionInfo { get; private set; }
      public double ProgressPercent { get; private set; } // -ve if lower (down arrow) or +ve if higher (up arrow). e.g. 2.6, 6.6, -4.6, etc.
      public double BenchComparisonPoints { get; private set; } // as above, -ve if below benchmark, +ve if above benchmark.
      public int RowCount { get; set; }

      public TableRowInfo(DbHelper.Questions.ReportQuestionInfo questionInfo) {
        QuestionInfo = questionInfo;
        CalcProgress();
      }

      public void AddQnInfo(DbHelper.Questions.ReportQuestionInfo questionInfo) {
        QuestionInfo.AccumulateScores(questionInfo);
        CalcProgress();
      }

      public void CalcProgress() {
        ProgressPercent = 0;
        BenchComparisonPoints = 0;
        if (QuestionInfo.Scores.ScoreSelf.Avg == null) return;
        if (QuestionInfo.Scores.ScorePreviousSelf.Avg != null) {
          double progressPoints = (double)QuestionInfo.Scores.ScoreSelf.Avg - (double)QuestionInfo.Scores.ScorePreviousSelf.Avg;
          ProgressPercent = progressPoints / (double)QuestionInfo.Scores.ScorePreviousSelf.Avg * 100;
        }
        if (QuestionInfo.Scores.ScoreBenchSelf.Avg != null)
          BenchComparisonPoints = (double)QuestionInfo.Scores.ScoreSelf.Avg - (double)QuestionInfo.Scores.ScoreBenchSelf.Avg;
      }
    }

  }
}
