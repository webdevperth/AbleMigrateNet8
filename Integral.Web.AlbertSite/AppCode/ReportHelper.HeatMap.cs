using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class ReportHelper {

    public class HeatMap {

      public static class Enums {
        public enum RowSortBy { Unsorted, SelfScore, RaterScore }
      }

      public static class QueryKeys {
        public const string RowSortBy = "sort";
      }

      public const string DefaultSortKey = "";

      public static Dictionary<string, (string DisplayText, Enums.RowSortBy OptionEnum)> RowSortOptions =
        new Dictionary<string, (string DisplayText, Enums.RowSortBy Value)>(StringComparer.OrdinalIgnoreCase) {
          { DefaultSortKey, ("Unsorted", Enums.RowSortBy.Unsorted) },
          { "self", ("Self Score", Enums.RowSortBy.SelfScore) },
          { "rater", ("Rater Score", Enums.RowSortBy.RaterScore) }
        };

      public static readonly List<ScoreTemperature> ScoreTemperatures = new List<ScoreTemperature>() {
        new ScoreTemperature(-1000M, -0.2M, "temp-vlow", "< -0.2 below benchmark"),
        new ScoreTemperature(-0.2M, -0.1M, "temp-low", "-0.2 to -0.1 below benchmark"),
        new ScoreTemperature(-0.1M, 0.1M, "temp-neutral", "-0.1 to 0.1 near benchmark"),
        new ScoreTemperature(0.1M, 0.2M, "temp-medium", "0.1 to 0.2 above benchmark"),
        new ScoreTemperature(0.2M, 0.3M, "temp-high", "0.2 to 0.3 above benchmark"),
        new ScoreTemperature(0.2M, 1000M, "temp-vhigh", "0.3+ above benchmark")
      };

      public static ScoreTemperature ScoreTemperature_Neutral = ScoreTemperatures.Find(st => st.ScoreDiffFrom <= 0 && st.ScoreDiffTo >= 0);

      public class ScoreTemperature {
        public decimal ScoreDiffFrom { get; private set; }
        public decimal ScoreDiffTo { get; private set; }
        public string ClassName { get; private set; }
        public string LegendText { get; private set; }
        public ScoreTemperature(decimal scoreDiffFrom, decimal scoreDiffTo, string className, string legendText) {
          ScoreDiffFrom = scoreDiffFrom;
          ScoreDiffTo = scoreDiffTo;
          ClassName = className;
          LegendText = legendText;
        }
      }

      public static ScoreTemperature GetTemperature(ScoreInfo scoreInfo) {

        // Get difference between score and benchmark score.
        decimal scoreDiff = 0;
        if (scoreInfo?.Score != null && scoreInfo?.BenchScore != null) {
          scoreDiff = scoreInfo.Score.Value - scoreInfo.BenchScore.Value;
        }
        // Return the temperature object whose score range covers the score, or neutral if a temp is not found.
        return ScoreTemperatures.Find(t => scoreDiff >= t.ScoreDiffFrom && scoreDiff < t.ScoreDiffTo) ?? ScoreTemperature_Neutral;
      }

      public class HeatMapRow {

        public int RowEntityId { get; private set; }
        public string RowTitle { get; private set; }
        public int ResponseCount { get; private set; }
        public List<ColumnScore> ColumnScores { get; private set; }
        public ScoreInfo RowScore { get; private set; }

        public class ColumnScore {
          public int ColumnEntityId { get; private set; }
          public ScoreInfo ScoreInfo { get; private set; }
          public ColumnScore(int columnEntityId, decimal? benchScore) {
            ColumnEntityId = columnEntityId;
            ScoreInfo = new ScoreInfo(benchScore);
          }
        }

        public HeatMapRow(int rowEntityId, string rowTitle, int responseCount, List<HeatMapColumn> heatMapColumns) {
          RowEntityId = rowEntityId;
          RowTitle = rowTitle;
          ResponseCount = responseCount;
          RowScore = new ScoreInfo();
          ColumnScores = new List<ColumnScore>();
          foreach (var col in heatMapColumns) ColumnScores.Add(new ColumnScore(col.ColumnEntityId, col.BenchScore));
        }

        public void AddResponseCount(int responseCount) {
          ResponseCount += responseCount;
        }
      }

      public class HeatMapColumn {

        public int ColumnEntityId { get; private set; }
        public string ColumnTitle { get; private set; }
        public decimal? BenchScore { get; private set; }

        public HeatMapColumn(int columnEntityId, string columnTitle) {
          ColumnEntityId = columnEntityId;
          ColumnTitle = columnTitle;
        }

        public void SetBenchScore(int scoreCount, long scoreSum) {
          BenchScore = ((decimal)scoreSum / scoreCount).Round(1, MidpointRounding.AwayFromZero);
        }
      }

      public class ScoreInfo {

        public int ScoreCount { get; private set; } = 0;
        public long ScoreSum { get; private set; } = 0;
        public decimal? Score { get; private set; } = null;
        public decimal? BenchScore { get; private set; } = null;

        public ScoreInfo() { }

        public ScoreInfo(decimal? benchScore) {
          BenchScore = benchScore;
        }

        public void SetScore(int scoreCount, long scoreSum, decimal? benchScore) {
          ScoreCount = 0;
          ScoreSum = 0;
          BenchScore = benchScore;
          AccumulateScore(scoreCount, scoreSum);
        }

        public void AccumulateScore(int scoreCount, long scoreSum) {
          ScoreCount += scoreCount;
          ScoreSum += scoreSum;
          if (ScoreCount > 0) Score = ((decimal)ScoreSum / ScoreCount).Round(1, MidpointRounding.AwayFromZero);
        }
      }

    }
  }
}
