using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class CoacheeReport_Comments : AppCode.PageBaseClasses.CoacheeReportPartialBase {

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() {

      int lastQnNum = 0;

      DbHelper.Common.Query($@"
        WITH tsq
        AS
        (
          SELECT sq.AutoNumber, sq.GblQuestionId, sq.QuestionTextFull1,
            ROW_NUMBER() OVER (PARTITION BY sq.AutoNumber ORDER BY sq.Sort) AS QuestionSeq
          FROM sv_360_Questions sq
          INNER JOIN sv_Survey sv ON sq.SurveyId = sv.sv_id
          INNER JOIN al_SurveyType st ON st.SurveyTypeCode = sv.SurveyTypeCode
          WHERE sq.SurveyId = @SurveyId
            AND (sq.InputType = '{ConfigHelper.InputTypes.OpenText}' OR st.ReportCanViewSingleLineText = 1 AND sq.InputType = '{ConfigHelper.InputTypes.Text}')
        )
        SELECT tsq.AutoNumber, tsq.QuestionTextFull1, sp.IsSelf, ta.ta_textanswer
        FROM tsq
        INNER JOIN sv_360_Questions sq ON sq.AutoNumber = tsq.AutoNumber
        INNER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        INNER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        INNER JOIN sv_360_TextAnswers ta ON sa.AnswerId = ta.AnswerId
        WHERE tsq.QuestionSeq = 1
          AND (sp.PartId = @PartId OR sp.Self_PartId = @PartId)
          AND sp.Completed IS NOT NULL
        ORDER BY tsq.AutoNumber, sp.IsSelf DESC;",
        dr => {
          var q = new QuestionInfo() {
            QuestionNumber = dr.GetInt("AutoNumber"),
            QuestionText = dr.GetString("QuestionTextFull1"),
            IsSelf = dr.GetBoolFromInt("IsSelf"),
            ResponseText = dr.GetString("ta_textanswer").TrimWhitespace()
          };
          if (lastQnNum != q.QuestionNumber || !q.ResponseText.IsNullOrEmpty()) {
            Questions.Add(q);
          }
          lastQnNum = q.QuestionNumber;
        },
        DbHelper.Common.NewSqlParameter("SurveyId", SurveyId),
        DbHelper.Common.NewSqlParameter("PartId", ParticipantId)
      );

      return Page();
    }

    // A render entry within a comment-group: either a heading row or a question response.
    public class CommentEntry {
      public bool IsHeading { get; set; }
      public string HeadingText { get; set; }
      public QuestionInfo Question { get; set; }
    }

    public class CommentGroup {
      public int QuestionNumber { get; set; }
      public string QuestionText { get; set; }
      public List<CommentEntry> Entries { get; set; } = new List<CommentEntry>();
    }

    // Returns Questions grouped by QuestionNumber, with heading rows inserted at IsSelf transitions.
    public List<CommentGroup> GetCommentGroups() {

      var groups = new List<CommentGroup>();

      int iQn = 0;
      int thisQuestionNumber = 0;

      while (iQn < Questions.Count) {

        var qnItem = Questions[iQn];
        thisQuestionNumber = qnItem.QuestionNumber;
        bool isSelf = qnItem.IsSelf;

        var group = new CommentGroup() { QuestionNumber = qnItem.QuestionNumber, QuestionText = qnItem.QuestionText };
        groups.Add(group);

        if (qnItem.IsSelf == true) group.Entries.Add(new CommentEntry() { IsHeading = true, HeadingText = "Participant Responses:" });

        while (iQn < Questions.Count) {
          qnItem = Questions[iQn];
          if (qnItem.IsSelf != isSelf) {
            if (qnItem.IsSelf == false) group.Entries.Add(new CommentEntry() { IsHeading = true, HeadingText = "Rater Responses:" });
            isSelf = qnItem.IsSelf;
          }
          if (thisQuestionNumber != qnItem.QuestionNumber) break;
          group.Entries.Add(new CommentEntry() { IsHeading = false, Question = qnItem });
          iQn++;
        }
      }

      return groups;
    }

    public class QuestionInfo {
      public int QuestionNumber { get; set; }
      public string QuestionText { get; set; }
      public string ResponseText { get; set; }
      public bool IsSelf { get; set; }
    }

  }
}
