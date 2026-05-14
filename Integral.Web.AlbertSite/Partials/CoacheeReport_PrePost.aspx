<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="CoacheeReport_PrePost.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.CoacheeReport_PrePost" %>

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
  <% string preSurveySelfStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.PreSelfScore, ScoreMaxValue)}\""; %>
  <% string surveySelfStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.PostSelfScore, ScoreMaxValue)}\""; %>
  <% string preSurveyRaterStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.PreRaterScore, ScoreMaxValue)}\""; %>
  <% string surveyRaterStyle = $"style=\"width:{WebHelper.GetCSSPercentFromRatio(qnItem.PostRaterScore, ScoreMaxValue)}\""; %>
  <div class="question <%= GetBenchComparisonRowClass(qnItem) %>" data-gblid="<%= qnItem.GblQuestionId %>">
    <div class="qnText">
      <%= qnItem.GblQuestionText.HTMLEncode() %>
    </div>
    <div class="qnBars col-md-3">
      <div class="scoreBars">
        <div class="scoreBar preScoreBar">
          <div class="barTitle">Self-Pre</div>
          <div class="barBg">
            <span class="barLine barSelf" <%= preSurveySelfStyle %>></span>
          </div>
          <div class="barScore"><%= qnItem.PreSelfScore == null ? "NA" : qnItem.PreSelfScore.ToString("0.0", "") %></div>
        </div>
        <div class="scoreBar">
          <div class="barTitle">Self-Post</div>
          <div class="barBg">
            <span class="barLine barSelf" <%= surveySelfStyle %>></span>
          </div>
          <div class="barScore"><%= qnItem.PostSelfScore == null ? "NA" : qnItem.PostSelfScore.ToString("0.0", "") %></div>
        </div>
        <% if (RaterCount > 0) { %>
          <div class="scoreBar preScoreBar">
            <div class="barTitle">Rater-Pre</div>
            <div class="barBg">
              <span class="barLine barRater" <%= preSurveyRaterStyle %>></span>
            </div>
            <div class="barScore"><%= qnItem.PreRaterScore == null ? "NA" : qnItem.PreRaterScore.ToString("0.0", "") %></div>
          </div>
          <div class="scoreBar">
            <div class="barTitle">Rater-Post</div>
            <div class="barBg">
              <span class="barLine barRater" <%= surveyRaterStyle %>></span>
            </div>
            <div class="barScore"><%= qnItem.PostRaterScore == null ? "NA" : qnItem.PostRaterScore.ToString("0.0", "") %></div>
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
