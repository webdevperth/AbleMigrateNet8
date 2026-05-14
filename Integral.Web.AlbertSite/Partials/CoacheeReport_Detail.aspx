<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="CoacheeReport_Detail.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.CoacheeReport_Detail" %>

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
  <% string surveySelfPercent = WebHelper.GetCSSPercentFromRatio(qnItem.SurveySelfScore, ScoreMaxValue); %>
  <% string surveyRaterPercent = WebHelper.GetCSSPercentFromRatio(qnItem.SurveyRaterScore, ScoreMaxValue); %>
  <% string normSelfPercent = WebHelper.GetCSSPercentFromRatio(qnItem.NormSelfScore, ScoreMaxValue); %>
  <% string normRaterPercent = WebHelper.GetCSSPercentFromRatio(qnItem.NormRaterScore, ScoreMaxValue); %>
  <div class="question <%= GetBenchComparisonRowClass(qnItem) %>" data-gid="<%= qnItem.GblQuestionId %>">
    <div class="qnText">
      <%= qnItem.GblQuestionText.HTMLEncode() %>
    </div>
    <div class="qnBars">
      <div class="scoreBars" data-tooltip="<b>Rater Result</b><br>Min: <%= qnItem.SurveyRaterMinScore %>, Max: <%= qnItem.SurveyRaterMaxScore %><br/>Compared to Self: <%= qnItem.SurveyRaterScore - qnItem.SurveySelfScore %>">

        <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Self, ScoreMinValue, ScoreMaxValue,
                                              "Self", qnItem.SurveySelfScore, NormDisplayName, Hide360ReportNorms ? null : qnItem.NormSelfScore) %>

        <% if (RaterCount > 0) { %>
          <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Rater, ScoreMinValue, ScoreMaxValue,
                                                "Rater", qnItem.SurveyRaterScore, NormDisplayName, Hide360ReportNorms ? null : qnItem.NormRaterScore) %>
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
