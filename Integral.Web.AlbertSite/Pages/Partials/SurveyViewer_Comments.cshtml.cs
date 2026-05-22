using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class SurveyViewer_Comments : AppCode.PageBaseClasses.SurveyViewerPartialBase {

    public List<QuestionInfo> Questions = new List<QuestionInfo>();
    public int TableRowCount = 0;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      DbHelper.Common.Query($@"
        WITH
        {(!IntakeCodeIdList.IsNullOrEmpty()
          ? "IntakeCodeIds AS ( SELECT Value AS IntakeCodeId FROM STRING_SPLIT(@IntakeCodeIds, ',')),"
          : ""
        )}
        tsq
        AS (
          SELECT sq.GblQuestionId, MAX(sq.QuestionTextFull1) AS QuestionTextFull1, MIN(sq.AutoNumber) AS AutoNumber
          FROM sv_360_Questions sq
          INNER JOIN sv_GblQuestions gq ON gq.GblQuestionId = sq.GblQuestionId
          INNER JOIN sv_Survey sv ON sq.SurveyId = sv.sv_id
          INNER JOIN al_SurveyType st ON st.SurveyTypeCode = sv.SurveyTypeCode
          {(!IntakeCodeIdList.IsNullOrEmpty()
            ? @"INNER JOIN sv_360_AnswerTypes sat ON sat.SurveyId = sv.sv_id
                INNER JOIN sv_360_Codes sc ON sc.AnswerTypeId = sat.AnswerTypeId
                INNER JOIN IntakeCodeIds ic ON ic.IntakeCodeId = sc.CodeId"
            : ""
          )}
          WHERE (sq.InputType = '{ConfigHelper.InputTypes.OpenText}' OR st.ReportCanViewSingleLineText = 1 AND sq.InputType = '{ConfigHelper.InputTypes.Text}')
            {(!IntakeCodeIdList.IsNullOrEmpty()
              ? "AND sat.AnswerTypeDescr = 'date'"
              : "AND sq.SurveyId = @SurveyId"
            )}
          GROUP BY sq.GblQuestionId
        )
        SELECT tsq.AutoNumber, tsq.QuestionTextFull1, sp.IsSelf, ta.ta_textanswer
        FROM tsq
        INNER JOIN sv_360_Questions sq ON sq.GblQuestionId = tsq.GblQuestionId
        INNER JOIN sv_Answers sa ON sa.QuestionId = sq.QuestionId
        INNER JOIN sv_360_Participants sp ON sp.PartId = sa.ParticipantId
        {(!IntakeCodeIdList.IsNullOrEmpty()
          ? "INNER JOIN IntakeCodeIds ic ON ic.IntakeCodeId = sp.IntakeCodeId"
          : ""
        )}
        INNER JOIN sv_360_TextAnswers ta ON sa.AnswerId = ta.AnswerId
        {(IsIndividualReport
          ? "WHERE (sp.PartId = @PartId OR sp.Self_PartId = @PartId)"
          : ""
        )}
        ORDER BY tsq.AutoNumber, sp.IsSelf DESC;",
        dr => {
          var q = new QuestionInfo() {
            QuestionNumber = dr.GetInt("AutoNumber"),
            QuestionText = dr.GetString("QuestionTextFull1"),
            IsSelf = dr.GetBoolFromInt("IsSelf"),
            ResponseText = dr.GetString("ta_textanswer")
          };
          Questions.Add(q);
        },
        DbHelper.Common.NewSqlParameter("SurveyId", SingleSurveyId),
        DbHelper.Common.NewSqlParameter("PartId", SingleSurveyPartId),
        DbHelper.Common.NewSqlParameter("IntakeCodeIds", IntakeCodeIdList?.ToStringList())
      );

      return Page();
    }

    public string RenderQuestions() {

      if (Questions.IsNullOrEmpty()) {
        return "No text responses found.";
      }

      var sb = new StringBuilder();

      Action<int, string> categoryStart = (questionNumber, questionText) => {
        sb.Append("<div class=\"boxBorder\">");
        sb.Append("<div class=\"boxTitle\"><h4>").Append(questionText.HTMLEncode()).Append("</h4></div>");
      };

      Action categoryEnd = () => {
        sb.Append("</div>");
      };

      Action<string> questionHeading = (text) => {
        sb.Append("<div class=\"question row qnHeading\">");
        sb.Append("<div class=\"qnText col-md-12\">").Append(text).Append("</div>");
        sb.Append("</div>");
      };

      Action<QuestionInfo> questionDetail = (questionInfo) => {
        sb.Append("<div class=\"question row ").Append(questionInfo.IsSelf ? "qnText-self" : "qnText-rater").Append("\">");
        sb.Append("<div class=\"qnText col-md-12\">");
        sb.Append(questionInfo.ResponseText.HTMLEncode().Replace("\n", "<br>"));
        sb.Append("</div>");
        sb.Append("</div>");
      };

      ShowQuestions(categoryStart, categoryEnd, questionHeading, questionDetail);

      return sb.ToString();
    }

    public void ShowQuestions(Action<int, string> categoryStart, Action categoryEnd, Action<string> questionHeading, Action<QuestionInfo> questionDetail) {

      int iQn = 0;
      int thisQuestionNumber = 0;

      while (iQn < Questions.Count) {

        var qnItem = Questions[iQn];
        thisQuestionNumber = qnItem.QuestionNumber;
        bool isSelf = qnItem.IsSelf;

        categoryStart(qnItem.QuestionNumber, qnItem.QuestionText);

        if (qnItem.IsSelf == true) questionHeading(IsIndividualReport.ToValue("Your", "Self") + " Responses:");

        while (iQn < Questions.Count) {
          qnItem = Questions[iQn];
          if (qnItem.IsSelf != isSelf) {
            if (qnItem.IsSelf == false) questionHeading("Rater Responses:");
            isSelf = qnItem.IsSelf;
          }
          if (thisQuestionNumber != qnItem.QuestionNumber) break;
          questionDetail(qnItem);
          iQn++;
        }

        categoryEnd();
      }
    }

    public class QuestionInfo {
      public int QuestionNumber { get; set; }
      public string QuestionText { get; set; }
      public string ResponseText { get; set; }
      public bool IsSelf { get; set; }
    }

  }
}
