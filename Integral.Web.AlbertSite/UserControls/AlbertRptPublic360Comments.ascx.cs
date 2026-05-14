using System;
using System.Text;
using static Integral.Web.DbHelper.Common;

namespace Integral.Web.PortalSite.UserControls {

  public partial class AlbertRptPublic360Comments : System.Web.UI.UserControl {

    Guid urlCoacheeUID;
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo;
    public DbHelper.Reports.Coachee360.Coachee360Results reportResults;

    public string CommentsText;

    protected void Page_Load(object sender, EventArgs e) {

      if (!Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeGuid).EmptyIfNull(), out urlCoacheeUID)) return;

      coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(urlCoacheeUID);
      if (coacheeInfo == null) return;

      var sb = new StringBuilder();
      int lastQnNumber = 0;
      int lastIsSelf = 0;

      string urlSelectedSurveyUId = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId); // selected survey to show.
      reportResults = DbHelper.Reports.Coachee360.GetCoachee360ReportResults(coacheeInfo.CoacheeId, urlSelectedSurveyUId, null);
      if (reportResults == null) return;

      Query(@"

        SELECT p.PartId, p.Self_PartId, p.IsSelf, q.Sort, q.AutoNumber, q.QuestionTextFull1, q.QuestionTextFullOther1, ta.ta_textanswer
        FROM sv_Answers a
        INNER JOIN sv_360_Participants p ON a.ParticipantId = p.PartId AND p.Completed IS NOT NULL
        INNER JOIN sv_360_Questions q ON q.QuestionId = a.QuestionId
        INNER JOIN sv_360_AnswerTypes t ON q.AnswerTypeId = t.AnswerTypeId
        INNER JOIN sv_360_TextAnswers ta ON a.AnswerId = ta.AnswerId
        WHERE (p.PartId = @PartId OR p.Self_PartId = @PartId)
          AND t.InputType = 'a'
        ORDER BY q.AutoNumber, p.IsSelf DESC, q.Sort",

        dr => {

          int qnNumber = dr.GetInt("AutoNumber");
          int isSelf = dr.GetInt("IsSelf");
          string qnText = reportResults.SurveyInfo.IsRatersOnly ? dr.GetString("QuestionTextFullOther1") : dr.GetString("QuestionTextFull1");
          qnText = qnText.ReplaceTags(new System.Collections.Generic.Dictionary<string, string> {
            { "SelfName", coacheeInfo.FirstName }
          });
          string commentText = dr.GetString("ta_textanswer");

          if (qnNumber != lastQnNumber) {
            // new question
            sb.AppendLine("<h4>" + qnText.HTMLEncode() + "</h4>");
            lastQnNumber = qnNumber;
            lastIsSelf = -1; // reset valud. will be 1 or 0 for each row.
          }

          if (lastIsSelf != isSelf) {
            if (isSelf == 1) sb.AppendLine("<h5>Your Comments</h5>");
            else sb.AppendLine("<h5>Rater Comments</h5>");
            lastIsSelf = isSelf;
          }

          sb.AppendLine($@"<p><span class=""glyphicon glyphicon-play""></span>{commentText.HTMLEncode()}</p>");
        },
        NewSqlParameter("PartId", reportResults.FoundParticipantBrief.PartId)
      );

      CommentsText = sb.ToString();
    }

    public class CommentItem {
      public string CommentText { get; private set; }
      public CommentItem(string commentText) {
        CommentText = commentText;
      }
    }

  }
}
