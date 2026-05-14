<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="CompanyReport_Detail.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.CompanyReport_Detail" %>

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
  <div class="question <%= GetBenchComparisonRowClass(qnItem) %>">
    <div class="qnText">
      <%= qnItem.GblQuestionText.HTMLEncode() %>
    </div>
    <div class="qnBars">
      <div class="scoreBars">
        <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Self, SurveyStats.ScaleMinScore, SurveyStats.ScaleMaxScore, "Self", qnItem.SelfAllScore, BenchmarkDisplayName, qnItem.SelfBenchScore) %>
        <% if (SurveyStats.RaterAllCount > 0) { %>
          <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Rater, SurveyStats.ScaleMinScore, SurveyStats.ScaleMaxScore, "Rater", qnItem.RaterAllScore, BenchmarkDisplayName, qnItem.RaterBenchScore) %>
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
