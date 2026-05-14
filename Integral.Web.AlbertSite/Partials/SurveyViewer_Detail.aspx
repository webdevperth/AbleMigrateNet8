<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="SurveyViewer_Detail.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.SurveyViewer_Detail" %>

<%@ Import Namespace="Integral.Web" %>

<%-- Note requires reports.css --%>

<% Action<string> CategoryStart = delegate(string categoryName) { %>
  <div class="boxBorder">
    <div class="boxTitle"><h4><%= categoryName.HTMLEncode() %></h4></div>
<% }; %>

<% Action CategoryEnd = delegate { %>
  </div>
<% }; %>

<% Action<QuestionInfo> QuestionDetail = delegate(QuestionInfo qnItem) { %>
  <% string surveySelfPercent = WebHelper.GetCSSPercentFromRatio(qnItem.SurveySelfScore, qnItem.MaxScore); %>
  <% string surveyRaterPercent = WebHelper.GetCSSPercentFromRatio(qnItem.SurveyRaterScore, qnItem.MaxScore); %>
  <% string normSelfPercent = WebHelper.GetCSSPercentFromRatio(qnItem.NormSelfScore, qnItem.MaxScore); %>
  <% string normRaterPercent = WebHelper.GetCSSPercentFromRatio(qnItem.NormRaterScore, qnItem.MaxScore); %>
  <div class="question <%= GetBenchComparisonRowClass(qnItem) %>">
    <div class="qnText">
      <%= qnItem.GblQuestionText.HTMLEncode() %>
    </div>
    <div class="qnBars">
      <div class="scoreBars">
        <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Self, ScoreMinValue, ScoreMaxValue, "Self", qnItem.SurveySelfScore, NormDisplayName, qnItem.NormSelfScore) %>
        <% if (RaterCount > 0) { %>
          <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Rater, ScoreMinValue, ScoreMaxValue, "Rater", qnItem.SurveyRaterScore, NormDisplayName, qnItem.NormRaterScore) %>
        <% } %>
      </div>
    </div>
  </div>
<% }; %>

<div class="RptPub360Detailed">

  <% ShowQuestions(CategoryStart, CategoryEnd, QuestionDetail); %>

</div>


<script type="text/javascript">

  (function ($) {

    $(document).ready(function () {
      new jBox('Tooltip', {
        attach: '.RptPub360Detailed .barDot[title]', position: { y: 'top' }
      });
    });


  })(jQuery);

</script>
