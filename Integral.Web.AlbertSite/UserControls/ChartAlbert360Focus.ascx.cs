using System;
using System.Text;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.UserControls {

  public partial class ChartAlbert360Focus : System.Web.UI.UserControl {

    const int Max_Qns_Per_Section = 4;
    Guid urlCoacheeUID;
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo;
    public DbHelper.Reports.Coachee360.Coachee360Results reportResults;
    public string FocusHtml;

    protected void Page_Load(object sender, EventArgs e) {

      if (!Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeGuid).EmptyIfNull(), out urlCoacheeUID)) return;

      coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(urlCoacheeUID);
      if (coacheeInfo == null) return;

      // Get scores for selected survey.
      string urlSelectedSurveyUId = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId); // selected survey to show.
      reportResults = DbHelper.Reports.Coachee360.GetCoachee360ReportResults(coacheeInfo.CoacheeId, urlSelectedSurveyUId, null);
      if (reportResults == null) return;

      // Create sorted lists for self and rater scores.
      var scoresSelf = new SortedList<string, DbHelper.Questions.ReportQuestionInfo>();
      var scoresRaters = new SortedList<string, DbHelper.Questions.ReportQuestionInfo>();
      foreach (var qnItem in reportResults.ReportQuestions) {
        scoresSelf.Add(GetScoreAsSortableString(qnItem.Scores.ScoreSelf.Avg, qnItem.QuestionId), qnItem);
        scoresRaters.Add(GetScoreAsSortableString(qnItem.Scores.ScoreRater.Avg, qnItem.QuestionId), qnItem);
      }

      var sb = new StringBuilder();
      AddQuestions(sb, "Highest Scoring Skills (Self)", true, scoresSelf.Values);
      AddQuestions(sb, "Highest Scoring Skills (Raters)", true, scoresRaters.Values);

      AddQuestions(sb, "Lowest Scoring Skills (Self)", false, scoresSelf.Values);
      AddQuestions(sb, "Lowest Scoring Skills (Raters)", false, scoresRaters.Values);

      FocusHtml = sb.ToString();
    }

    void AddQuestions(StringBuilder sb, string title, bool isHighestFirst, IList<DbHelper.Questions.ReportQuestionInfo> scoreList) {

      sb.AppendLine("<div class=\"boxBorder " + (isHighestFirst ? "qnHighest" : "qnLowest") + "\">");
      sb.AppendLine("<div class=\"boxTitle\"><h4><span class=\"circle\"></span>" + title.HTMLEncode() + "</h4></div>");

      int index = isHighestFirst ? scoreList.Count - 1 : 0;
      int step = isHighestFirst ? -1 : 1;
      int qnCount = 0;

      while (index >= 0 && index < scoreList.Count && qnCount < Max_Qns_Per_Section) {
        var qnItem = scoreList[index];

        sb.AppendLine("<h5>" + qnItem.RptQnGrpHeading.HTMLEncode() + " </h5>");
        //sb.AppendLine("<p>" + qnItem.QuestionText.HTMLEncode() + " </p>");
        AddQuestionLine(sb, qnItem);

        index += step;
        qnCount += 1;
      }

      sb.AppendLine("</div>");
    }

    void AddQuestionLine(StringBuilder sb, DbHelper.Questions.ReportQuestionInfo qnItem) {

      sb.AppendLine("<div class=\"question row\">");
      sb.AppendLine("<div class=\"qnText col-md-9\">"
        + "<span class=\"qnNum\">" + qnItem.AutoNumber + ".</span>"
        + qnItem.QuestionText.HTMLEncode() + "</div>");
      sb.AppendLine("<div class=\"qnBars col-md-3\">");

      sb.AppendLine($@"
        <div class=""scoreBars"">
          <div class=""scoreBar"">
            <div class=""barTitle"">Self</div>
            <div class=""barBg"">
              <span class=""barLine barSelf"" style=""width:{GetFormattedScore(qnItem.Scores.ScoreSelf.Avg)}%""></span>
              <span class=""barMark""></span>
              <span class=""barMark""></span>
              <span class=""barDot dotSelf"" title=""{GetFormattedScore(qnItem.Scores.ScoreBenchSelf.Avg)}"" style=""left:{GetFormattedScore(qnItem.Scores.ScoreBenchSelf.Avg)}%""></span>
            </div>
            <div class=""barScore"">{GetFormattedScore(qnItem.Scores.ScoreSelf.Avg)}</div>
          </div>
          <div class=""barInterGap""></div>
          <div class=""scoreBar"">
            <div class=""barTitle"">Raters</div>
            <div class=""barBg"">
              <span class=""barLine barRater"" style=""width:{GetFormattedScore(qnItem.Scores.ScoreRater.Avg)}%""></span>
              <span class=""barMark""></span>
              <span class=""barMark""></span>
              <span class=""barDot dotRater"" title=""{GetFormattedScore(qnItem.Scores.ScoreBenchRater.Avg)}"" style=""left:{GetFormattedScore(qnItem.Scores.ScoreBenchRater.Avg)}%"">&nbsp;</span>
            </div>
            <div class=""barScore"">{GetFormattedScore(qnItem.Scores.ScoreRater.Avg)}</div>
          </div>
        </div>");

      sb.AppendLine("</div>");
      sb.AppendLine("</div>");
    }

    string GetFormattedScore(double? score) {
      if (score == null) return "";
      return Math.Round((double)score * 10, 0, MidpointRounding.AwayFromZero).ToString();
    }

    string GetScoreAsSortableString(double? score, int id) {
      return (score == null ? "" : ((double)score).ToString("00.00")) + "_" + id;
    }

  }
}
