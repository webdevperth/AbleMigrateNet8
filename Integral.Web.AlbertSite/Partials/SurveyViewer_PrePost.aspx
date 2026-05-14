<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="SurveyViewer_PrePost.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.SurveyViewer_PrePost" %>

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
  <% string preSurveySelfStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.PreSurveySelfScore, qnItem.MaxScore)}\""; %>
  <% string surveySelfStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.SurveySelfScore, qnItem.MaxScore)}\""; %>
  <% string preSurveyRaterStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.PreSurveyRaterScore, qnItem.MaxScore)}\""; %>
  <% string surveyRaterStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.SurveyRaterScore, qnItem.MaxScore)}\""; %>
  <% string preNormSelfStyle = $"style=\"left:{WebHelper.GetCSSPercentFromRatio(qnItem.PreNormSelfScore, qnItem.MaxScore)}\""; %>
  <% string preNormRaterStyle = $"style=\"left:{WebHelper.GetCSSPercentFromRatio(qnItem.PreNormRaterScore, qnItem.MaxScore)}\""; %>
  <% string normSelfStyle = $"style=\"left:{WebHelper.GetCSSPercentFromRatio(qnItem.NormSelfScore, qnItem.MaxScore)}\""; %>
  <% string normRaterStyle = $"style=\"left:{WebHelper.GetCSSPercentFromRatio(qnItem.NormRaterScore, qnItem.MaxScore)}\""; %>
  <div class="question <%= GetBenchComparisonRowClass(qnItem) %>">
    <div class="qnText">
      <%= qnItem.GblQuestionText.HTMLEncode() %>
    </div>
    <div class="qnBars col-md-3">
      <div class="scoreBars">
        <div class="scoreBar preScoreBar">
          <div class="barTitle">Self-Pre</div>
          <div class="barBg">
            <span class="barLine barSelf" <%= preSurveySelfStyle %>></span>
            <span class="barDot dotSelf" title="<%= NormDisplayName %> Norm = <%= GetScoreFormatted(qnItem.PreNormSelfScore) %>" <%= preNormSelfStyle %>></span>
          </div>
          <div class="barScore"><%= qnItem.PreSurveySelfScore == null ? "NA" : qnItem.PreSurveySelfScore.ToString("0.0", "") %></div>
        </div>
        <div class="scoreBar">
          <div class="barTitle">Self-Post</div>
          <div class="barBg">
            <span class="barLine barSelf" <%= surveySelfStyle %>></span>
            <span class="barDot dotSelf" title="<%= NormDisplayName %> Norm = <%= GetScoreFormatted(qnItem.NormSelfScore) %>" <%= normSelfStyle %>></span>
          </div>
          <div class="barScore"><%= qnItem.SurveySelfScore == null ? "NA" : qnItem.SurveySelfScore.ToString("0.0", "") %></div>
        </div>
        <% if (RaterCount > 0) { %>
          <div class="scoreBar preScoreBar">
            <div class="barTitle">Rater-Pre</div>
            <div class="barBg">
              <span class="barLine barRater" <%= preSurveyRaterStyle %>></span>
              <span class="barDot dotRater" title="<%= NormDisplayName %> Norm = <%= GetScoreFormatted(qnItem.PreNormRaterScore) %>" <%= preNormRaterStyle %>></span>
            </div>
            <div class="barScore"><%= qnItem.PreSurveyRaterScore == null ? "NA" : qnItem.PreSurveyRaterScore.ToString("0.0", "") %></div>
          </div>
          <div class="scoreBar">
            <div class="barTitle">Rater-Post</div>
            <div class="barBg">
              <span class="barLine barRater" <%= surveyRaterStyle %>></span>
              <span class="barDot dotRater" title="<%= NormDisplayName %> Norm = <%= GetScoreFormatted(qnItem.NormRaterScore) %>" <%= normRaterStyle %>></span>
            </div>
            <div class="barScore"><%= qnItem.SurveyRaterScore == null ? "NA" : qnItem.SurveyRaterScore.ToString("0.0", "") %></div>
          </div>
        <% } %>
      </div>
    </div>
  </div>
<% }; %>

<div class="RptPub360PrePost">

  <% ShowQuestions(CategoryStart, CategoryEnd, QuestionDetail); %>

</div>


<script type="text/javascript">

  (function ($) {

    $(document).ready(function () {
      new jBox('Tooltip', {
        attach: '.RptPub360PrePost .barDot[title]', position: { y: 'top' }
      });
    });


  })(jQuery);

</script>
