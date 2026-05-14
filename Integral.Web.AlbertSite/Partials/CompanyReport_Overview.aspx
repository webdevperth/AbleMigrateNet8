<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="CompanyReport_Overview.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.CompanyReport_Overview" %>

<%@ Import Namespace="Integral.Web" %>

<div class="ctlCA360SoT">
  <div class="boxTable">

    <div class="boxBorder">
      <div class="boxTitle"><h4>Overall Score</h4></div>
      <div class="boxBody boxScores boxBgBottom">
        <% if (SurveyStats.SelfAllCount > 0) { %>
          <div class="scoreRow">
            <div class="scoreAcross scoreLeft">
              <div class="score"><%= GetScoreFormatted(SelfAllScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
              <div class="barOuter"><div class="bar"></div></div>
              <div class="subtitle">Self (<%= SurveyStats.SelfAllCount %>)</div>
            </div>
            <div class="scoreAcross scoreRight">
              <div class="score"><%= GetScoreFormatted(SelfBenchScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
              <div class="barOuter"><div class="bar"><span>..........</span></div></div>
              <div class="subtitle">Self Norm<div class="benchTypeName">(<%= BenchmarkDisplayName %>)</div></div>
            </div>
          </div>
        <% } %>
        <% if (SurveyStats.RaterAllCount > 0) { %>
          <div class="scoreRow raterRow">
            <div class="scoreAcross scoreLeft">
              <div class="score"><%= GetScoreFormatted(RaterAllScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
              <div class="barOuter"><div class="bar"></div></div>
              <div class="subtitle">Rater (<%= SurveyStats.RaterAllCount %>)</div>
            </div>
            <div class="scoreAcross scoreRight">
              <div class="score"><%= GetScoreFormatted(RaterBenchScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
              <div class="barOuter"><div class="bar"><span>..........</span></div></div>
              <div class="subtitle">Rater Norm<div class="benchTypeName">(<%= BenchmarkDisplayName %>)</div></div>
            </div>
          </div>
        <% } %>
      </div>
    </div>

    <% if (SurveyStats.HasPreSurvey) { %>
      <div class="boxBorder mt0">
        <div class="boxTitle"><h4>Pre-Post Scores</h4></div>
        <div class="boxBody boxScores boxBgBottom">
          <% if (SurveyStats.SelfAllCount > 0) { %>
            <div class="scoreRow">
              <div class="scoreAcross scoreLeft">
                <div class="score"><%= GetScoreFormatted(SelfPreScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
                <div class="barOuter"><div class="bar"></div></div>
                <div class="subtitle">Pre-Survey Self (<%= SurveyStats.SelfPreCount %>)</div>
              </div>
              <div class="scoreAcross scoreLeft">
                <div class="score"><%= GetScoreFormatted(SelfPostScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
                <div class="barOuter"><div class="bar"></div></div>
                <div class="subtitle">Post-Survey Self (<%= SurveyStats.SelfPostCount %>)</div>
              </div>
              <div class="scoreAcross scoreRight">
                <div class="score"><%= GetScoreFormatted(SelfPostScore - SelfPreScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
                <div class="barOuter"><div class="bar"><span>..........</span></div></div>
                <div class="subtitle">Pre-Post Change</div>
              </div>
            </div>
          <% } %>
          <% if (SurveyStats.RaterAllCount > 0) { %>
            <div class="scoreRow raterRow">
              <div class="scoreAcross scoreLeft">
                <div class="score"><%= GetScoreFormatted(RaterPreScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
                <div class="barOuter"><div class="bar"></div></div>
                <div class="subtitle">Pre-Survey Rater (<%= SurveyStats.RaterPreCount %>)</div>
              </div>
              <div class="scoreAcross scoreLeft">
                <div class="score"><%= GetScoreFormatted(RaterPostScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
                <div class="barOuter"><div class="bar"></div></div>
                <div class="subtitle">Post-Survey Rater (<%= SurveyStats.RaterPostCount %>)</div>
              </div>
              <div class="scoreAcross scoreRight">
                <div class="score"><%= GetScoreFormatted(RaterPostScore - RaterPreScore, "NA") %><span class="sub"> / <%= SurveyStats.ScaleMaxScore %></span></div>
                <div class="barOuter"><div class="bar"><span>..........</span></div></div>
                <div class="subtitle">Pre-Post Rater Change</div>
              </div>
            </div>
          <% } %>
        </div>
      </div>
    <% } %>

  </div>
</div>
