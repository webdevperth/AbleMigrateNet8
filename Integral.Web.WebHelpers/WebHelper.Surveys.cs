using Integral.Web.WebHelpers;
using System.Collections.Generic;
using System.Text;

namespace Integral.Web {

  public partial class WebHelper {

    public class Surveys {

      private const int ButtonTextMaxLength = 20;
      private const string ButtonTextForceBlank = "_";
      public const string _QnIdTag = "[qnid]";
      public const string _AnswerTextTagEncode = "[anstext]";
      private const string _OptionColClass = "QnOptionCol";
      private const string _OptionBtnClass = "QnOptionBtn";

      public static readonly string CheckIconHtml = Icon.Check.AddClass("doneIcon").AddAttribute("aria-hidden", "true").ToString();

      public static string GetSurveyQuestionHtml(
        DbHelper.Questions.SurveyQuestionInfo qn,
        DbHelper.Participants.ParticipantInfo partInfo,
        Dictionary<int, string> cachedOptionHtml) {

        string typeClasses, typeAttrs;
        if (qn.IsHeading) {
          typeClasses = "heading";
          typeAttrs = "";
        } else {
          typeClasses = $"question inptype_{qn.InputType} inptypegid_{qn.GblAnswerTypeId}";
          typeAttrs = $@"data-qnid=""{qn.QuestionId}"" data-inptype=""{qn.InputType}""";
        }

        return $@"
          <div class=""row QnRow {typeClasses}"" {typeAttrs}>
            <div class=""col-xs-1 mw50 QnNum"">{GetQnNumDisplay(qn)}</div>
            <div class=""col-xs-11 QnBody"">
              <div class=""QnText"">{GetQnTextDisplay(qn, partInfo)}</div>
              <div class=""QnOptions {GetQnTypeClasses(qn)}"">{CheckIconHtml}{GetQnOptions(qn, cachedOptionHtml)}</div>
              {GetRankedExtraTextHtml(qn)}
              {GetHiddenInput(qn)}
            </div>
          </div>
        ";
      }

      public static string GetOptionHtmlFromQuestionInfo(DbHelper.Questions.SurveyQuestionInfo qnInfo) {

        if (qnInfo.Codes == null || qnInfo.Codes.Count == 0) {
          return "";
        }
        // Note that the html returned must contain tags for any dynamic data, eg. "[qnid]" - see tag constants above.\

        switch (DbHelper.AnswerTypes.InputTypes.GetId(qnInfo.InputType)) {
          case DbHelper.AnswerTypes.InputTypes.Ids.Scale:
            return GetHtmlForScale(qnInfo.Codes);
          case DbHelper.AnswerTypes.InputTypes.Ids.OptionsSingle:
          case DbHelper.AnswerTypes.InputTypes.Ids.Dropdown: // TODO: dropdowns
            return GetHtmlForScale(qnInfo.Codes);
          case DbHelper.AnswerTypes.InputTypes.Ids.Ranked:
            return GetHtmlForRanked(qnInfo.Codes);
          case DbHelper.AnswerTypes.InputTypes.Ids.TextMultiline:
          case DbHelper.AnswerTypes.InputTypes.Ids.TextSingleLine: // TODO: single line text
            return GetHtmlForMultilineText();
          case DbHelper.AnswerTypes.InputTypes.Ids.OptionsMultiple:
            return GetHtmlForMultipleChoiceOptionList(qnInfo);
        }
        return "";
      }

      private static string GetHtmlForScale(List<DbHelper.Questions.CodeInfo> codes) {
        var sbHTML = new StringBuilder();
        GetScaleButtonHTML(sbHTML, "NA", "Not Applicable", "na"); // NA button.
        for (int index = 0; index < codes.Count; index++) {
          var codeInfo = codes[index];
          GetScaleButtonHTML(sbHTML, codeInfo.Code, codeInfo.CodeText, codeInfo.CodeTextAbbrev);
        }
        return sbHTML.ToString();
      }

      private static string GetHtmlForRanked(List<DbHelper.Questions.CodeInfo> codes) {
        if (codes.IsNullOrEmpty()) return string.Empty;
        var sbHTML = new StringBuilder();
        int firstCodeIndex = 0;
        if (codes[0].CodeText.IsNullOrEmpty()) firstCodeIndex = 1;
        for (int index = firstCodeIndex; index < codes.Count; index++) {
          var codeInfo = codes[index];
          GetScaleButtonHTML(sbHTML, codeInfo.Code, codeInfo.CodeText, codeInfo.CodeTextAbbrev);
        }
        return sbHTML.ToString();
      }

      public static string GetQnNumDisplay(DbHelper.Questions.SurveyQuestionInfo qn) {
        string num = (qn.ShowAutoNumber ? qn.AutoNumber.ToString() : "") + qn.CustomNumber;
        if (!num.IsNullOrEmpty()) num += ".";
        return num;
      }

      public static string GetQnTypeClasses(DbHelper.Questions.SurveyQuestionInfo qn) {
        string classes = "qntype_" + qn.InputType;
        if (qn.AddTextBox) classes += " withText";
        return classes;
      }

      public static string GetRankedExtraTextHtml(DbHelper.Questions.SurveyQuestionInfo qn) {
        // Additional text box if needed.
        if (!qn.AddTextBox) return "";
        // Add custom text if needed.
        return
          $@"<div class=""qn_ExtraText"">
            <input class=""form-control"" type=""text"" size=""60"" value=""{qn.AnswerText.HTMLEncode()}"" name=""ans_text_{qn.QuestionId}"" />
          </div>";
      }

      public static string GetHiddenInput(DbHelper.Questions.SurveyQuestionInfo qn) {
        if (qn.IsHeading) return "";
        return $@"<input type=""hidden"" name=""ans_{qn.QuestionId}"" value=""{qn.AnswerCode}"" />";
      }

      public static string GetQnTextDisplay(DbHelper.Questions.SurveyQuestionInfo qn, DbHelper.Participants.ParticipantInfo partInfo) {
        string qnText;
        if (!qn.IsHeading && !qn.ShowQuestionText) return "";
        if (partInfo.IsSelf)
          qnText = qn.QuestionTextSelf;
        else {
          qnText = qn.QuestionTextRater;
          qnText = qnText.ReplaceTags(
            new Dictionary<string, string>() {
              { SurveyHelper.InstructionsTags.SelfName, partInfo.SelfName }
            }
          );
        }
        return qnText;
      }

      public static string GetQnOptions(DbHelper.Questions.SurveyQuestionInfo qn, Dictionary<int, string> cachedOptionHtml) {

        if (qn.AnswerTypeId == null) return "";

        string htmlOut;
        if (cachedOptionHtml == null) cachedOptionHtml = new Dictionary<int, string>();

        // Create and/or return cached option HTML. Replace tags with current values.
        if (!cachedOptionHtml.ContainsKey((int)qn.AnswerTypeId)) {
          htmlOut = GetOptionHtmlFromQuestionInfo(qn);
          cachedOptionHtml.Add((int)qn.AnswerTypeId, htmlOut);
        } else {
          htmlOut = cachedOptionHtml[(int)qn.AnswerTypeId];
        }
        return htmlOut
          .Replace(_QnIdTag, qn.QuestionId.ToString())
          .Replace(_AnswerTextTagEncode, qn.AnswerText.HTMLEncode());
      }

      public static string GetPagination(DbHelper.Questions.QuestionList questionsForPage, DbHelper.Participants.ParticipantInfo partInfo) {

        if (questionsForPage == null) return "";

        var sbHtml = new StringBuilder();

        for (var iPage = 1; iPage <= questionsForPage.TotalPages; iPage++) {
          PaginationLink(sbHtml, questionsForPage, iPage == questionsForPage.ThisPageNum ? "current" : "", iPage.ToString(), iPage);
        }

        return sbHtml.ToString();
      }

      private static void PaginationLink(StringBuilder sbHtml, DbHelper.Questions.QuestionList questionsForPage, string className, string character, int? pageNumber) {

        bool canNavigateToPage = pageNumber != null && pageNumber < questionsForPage.ThisPageNum;
        string navClasses = "", navDataAttr = "";

        if (canNavigateToPage) {
          navClasses = "surveyForm-btn-update";
          navDataAttr = $"data-nextpage=\"{pageNumber.Value}\"";
        }

        sbHtml.Append($"<li class=\"{className}\">");
        sbHtml.Append($"<button type=\"button\" class=\"btn btn-xsm mr5 btn-secondary {navClasses}\" {navDataAttr}>");
        sbHtml.Append($"<span>{character.HTMLEncode()}</span>");
        sbHtml.Append("</button>");
        sbHtml.Append("</li>");
      }

      private static void GetScaleButtonHTML(StringBuilder sbHtml, string buttonText, string buttonTitle, string buttonValue) {
        sbHtml.Append($"<div class=\"col-xs-1 {_OptionColClass}\">");
        sbHtml.Append($"<button type=\"button\" class=\"{_OptionBtnClass}\"");
        if (!buttonTitle.IsNullOrEmpty() && buttonTitle != ButtonTextForceBlank && buttonTitle != buttonText) {
          sbHtml.Append($" title=\"{buttonTitle.HTMLEncode()}\"");
        }
        if (buttonValue != null) sbHtml.Append($" data-value=\"{buttonValue.HTMLEncode()}\"");
        sbHtml.Append($">");
        sbHtml.Append(buttonText.HTMLEncode());
        sbHtml.Append($"</button>");
        sbHtml.Append($"</div>");
      }

      private static void GetScaleButtonHTML(StringBuilder sbHtml, int code, string codeText, string codeTextAbbrev) {
        // Determine button text etc. from codeinfo.
        string buttonText = codeTextAbbrev.HTMLEncode();
        if (buttonText == "") buttonText = codeText.HTMLEncode();
        if (buttonText == ButtonTextForceBlank) buttonText = "&nbsp;";
        if (buttonText == "" || buttonText.Length > ButtonTextMaxLength) buttonText = code.ToString(); // safety - text in button can't be very long.
        GetScaleButtonHTML(sbHtml, buttonText, codeText.HTMLEncode(), code.ToString());
      }

      private static string GetHtmlForMultipleChoiceOptionList(DbHelper.Questions.SurveyQuestionInfo qnInfo) {
        var sbHTML = new StringBuilder();
        for (int index = 0; index < qnInfo.Codes.Count; index++) {
          var codeInfo = qnInfo.Codes[index];
          bool isChecked = qnInfo.AnswersMulti.Exists(c => c.AnswerCodeId == codeInfo.CodeId);
          sbHTML.Append($@"
            <div class=""RadioRow flex gap15"">
              <div class=""RadioCol flex0""><input type=""checkbox"" {(isChecked ? "checked" : "")} class=""icheck"" name=""ans_{_QnIdTag}"" value=""{codeInfo.Code}"" id=""chk_{_QnIdTag}_{codeInfo.Code}"" /></div>
              <label class=""flex1"" for=""chk_{_QnIdTag}_{codeInfo.Code}"">{codeInfo.CodeText.HTMLEncode()}</label>
            </div>");
        }
        return sbHTML.ToString();
      }

      private static string GetHtmlForMultilineText() {
        return $"<div class=\"surveyOpenTextArea\"><textarea cols=\"60\" rows=\"5\" name=\"ans_text_{_QnIdTag}\">{_AnswerTextTagEncode}</textarea></div>";
      }
    }
  }
}
