<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="SurveyViewer_Overview.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.SurveyViewer_Overview" %>

<%@ Import Namespace="Integral.Web" %>

<div class="ctlCA360SoT h100pc">

  <div class="flex gap15 flex-column-sm h100pc">

    <div class="boxBorder">
      <div class="boxTitle"><h4><%= GetOverallBoxTitle() %></h4></div>
      <div class="boxBody boxScores boxBgBottom">
        <% if (SelfCount > 0) { %>
          <div class="scoreRow">
            <div class="scoreAcross scoreLeft">
              <div class="score"><%= GetScoreFormatted(ScoreSelf, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
              <div class="barOuter"><div class="bar"></div></div>
              <div class="subtitle">Self<%= SelfCount == 1 ? "" : $" ({SelfCount})" %></div>
            </div>
            <div class="scoreAcross scoreRight">
              <div class="score"><%= GetScoreFormatted(NormSelf, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
              <div class="barOuter"><div class="bar"><span>..........</span></div></div>
              <div class="subtitle">Self Norm<div class="benchTypeName">(<%= NormDisplayName %>)</div></div>
            </div>
          </div>
        <% } %>
        <% if (RaterCount > 0) { %>
          <div class="scoreRow raterRow">
            <div class="scoreAcross scoreLeft">
              <div class="score"><%= GetScoreFormatted(ScoreRater, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
              <div class="barOuter"><div class="bar"></div></div>
              <div class="subtitle">Rater (<%= RaterCount %>)</div>
            </div>
            <div class="scoreAcross scoreRight">
              <div class="score"><%= GetScoreFormatted(NormRater, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
              <div class="barOuter"><div class="bar"><span>..........</span></div></div>
              <div class="subtitle">Rater Norm<div class="benchTypeName">(<%= NormDisplayName %>)</div></div>
            </div>
          </div>
        <% } %>
      </div>
    </div>

    <% if (SinglePreSurveyPartId > 0) { %>
      <div class="boxBorder">
        <div class="boxTitle"><h4>Pre-Post Scores</h4></div>
        <div class="boxBody boxScores boxBgBottom">
          <% if (SelfCount > 0) { %>
            <div class="scoreRow">
              <% if (SinglePreSurveyPartId > 0) { %>
                <div class="scoreAcross scoreLeft">
                  <div class="score"><%= GetScoreFormatted(PreSurveyScoreSelf, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
                  <div class="barOuter"><div class="bar"></div></div>
                  <div class="subtitle">Pre-Survey<br/>Self</div>
                </div>
              <% } %>
              <div class="scoreAcross scoreLeft">
                <div class="score"><%= GetScoreFormatted(ScoreSelf, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
                <div class="barOuter"><div class="bar"></div></div>
                <div class="subtitle">Post-Survey<br/>Self<%= SelfCount == 1 ? "" : $" ({SelfCount})" %></div>
              </div>
              <div class="scoreAcross scoreRight">
                <div class="score"><%= GetScoreFormatted(ScoreSelf - PreSurveyScoreSelf, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
                <div class="barOuter"><div class="bar"><span>..........</span></div></div>
                <div class="subtitle">Pre-Post<br/>Self Change</div>
              </div>
            </div>
          <% } %>
          <% if (RaterCount > 0) { %>
            <div class="scoreRow raterRow">
              <% if (SinglePreSurveyPartId > 0) { %>
                <div class="scoreAcross scoreLeft">
                  <div class="score"><%= GetScoreFormatted(PreSurveyScoreRater, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
                  <div class="barOuter"><div class="bar"></div></div>
                  <div class="subtitle">Pre-Survey<br/>Rater (<%= PreSurveyRaterCount %>)</div>
                </div>
              <% } %>
              <div class="scoreAcross scoreLeft">
                <div class="score"><%= GetScoreFormatted(ScoreRater, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
                <div class="barOuter"><div class="bar"></div></div>
                <div class="subtitle">Post-Survey<br/>Rater (<%= RaterCount %>)</div>
              </div>
              <div class="scoreAcross scoreRight">
                <div class="score"><%= GetScoreFormatted(ScoreRater - PreSurveyScoreRater, "NA") %><span class="sub"> / <%= ScoreMaxValue %></span></div>
                <div class="barOuter"><div class="bar"><span>..........</span></div></div>
                <div class="subtitle">Pre-Post<br/>Rater Change</div>
              </div>
            </div>
          <% } %>
        </div>
      </div>
    <% } %>

    <% if (CanShowAISummaryText && !AISummaryText.IsNullOrEmpty()) { %>
      <div class="boxBorder ai-summary-box">
        <div class="boxTitle"><h4>360&deg; Feedback Summary</h4></div>
        <div class="boxBody nicer-scrollbar"><%= WebHelper.MarkdownToHtml(AISummaryText) %></div>
      </div>
    <% } %>

  </div>
</div>

<script>

  (function ($) {

    $(document).ready(function () {


    }); // Doc ready.

  })(jQuery);

</script>
