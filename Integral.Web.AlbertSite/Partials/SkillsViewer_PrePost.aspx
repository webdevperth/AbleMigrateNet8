<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="SkillsViewer_PrePost.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.SkillsViewer_PrePost" %>

<%@ Import Namespace="Integral.Web" %>

<%-- Note requires reports.css --%>

<% Action<string> CategoryStart = delegate(string categoryName) { %>
  <div class="boxBorder">
    <div class="boxTitle"><h4><%= categoryName.HTMLEncode() %></h4></div>
<% }; %>

<% Action CategoryEnd = delegate { %>
  </div>
<% }; %>

<% Action<DbHelper.Reports.PrePost.QuestionInfo> QuestionDetail = delegate(DbHelper.Reports.PrePost.QuestionInfo qnItem) { %>
  <div class="question">
    <div class="qnText">
      <%= qnItem.GblQuestionText.HTMLEncode() %>
    </div>
    <div class="qnBars col-md-3">
      <div class="scoreBars">
        <div class="scoreBar preScoreBar">
          <div class="barTitle">Self-Pre</div>
          <div class="barBg">
            <span class="barLine barSelf" <%= BarWidthStyle(qnItem.ScoreSelfPre) %>></span>
          </div>
          <div class="barScore"><%= qnItem.ScoreSelfPre == null ? "NA" : qnItem.ScoreSelfPre.ToString("0.0", "") %></div>
        </div>
        <div class="scoreBar">
          <div class="barTitle">Self-Post</div>
          <div class="barBg">
            <span class="barLine barSelf" <%= BarWidthStyle(qnItem.ScoreSelfPost) %>></span>
          </div>
          <div class="barScore"><%= qnItem.ScoreSelfPost == null ? "NA" : qnItem.ScoreSelfPost.ToString("0.0", "") %></div>
        </div>
        <% if (SurveyStats.RaterAllCount > 0) { %>
          <div class="scoreBar preScoreBar">
            <div class="barTitle">Rater-Pre</div>
            <div class="barBg">
              <span class="barLine barRater" <%= BarWidthStyle(qnItem.ScoreRaterPre) %>></span>
            </div>
            <div class="barScore"><%= qnItem.ScoreRaterPre == null ? "NA" : qnItem.ScoreRaterPre.ToString("0.0", "") %></div>
          </div>
          <div class="scoreBar">
            <div class="barTitle">Rater-Post</div>
            <div class="barBg">
              <span class="barLine barRater" <%= BarWidthStyle(qnItem.ScoreRaterPost) %>></span>
            </div>
            <div class="barScore"><%= qnItem.ScoreRaterPost == null ? "NA" : qnItem.ScoreRaterPost.ToString("0.0", "") %></div>
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
