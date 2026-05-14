<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ChartAlbert360FunctionTable.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.ChartAlbert360FunctionTable" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<%-- Note requires reports.css --%>

<%@ Import Namespace="Integral.Web" %>

<div class="ctlCA360FT">

  <div class="table-responsive">
    <table class="table tblScores">
      <colgroup>
        <col width="30">
        <col width="370">
        <col width="150">
        <col width="300">
        <col width="250">
        <col width="">
      </colgroup>
      <thead>
        <tr>
          <td>&nbsp;</td>
          <td class="cTitle">Overall Leadership Indicators</td>
          <td class="cProg">Progress</td>
          <td class="cProg">Comparison to <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %></td>
          <td class="cFeedb">Your Feedback</td>
          <td>&nbsp;</td>
        </tr>
      </thead>
      <tbody>
        <% foreach (var tableRowItem in TableRows.Values) { %>
          <% TableRowCount++; %>
          <tr class="rowSkill rowid_<%= tableRowItem.RowCount %> <%= GetRowProgressClass(tableRowItem) %>">
            <td>&nbsp;</td>
            <td class="cTitle"><%= tableRowItem.QuestionInfo.RptQnGrpHeading %></td>
            <td class="cProg"><span class="glyphicon glyphicon-<%= GetProgressPercentGlyphicon(tableRowItem) %>"></span><span class="progressPercent"><%= GetProgressPercent(tableRowItem) %>%</span></td>
            <td class=""><span class="progPts"><%= GetBenchComparisonPoints(tableRowItem) %>pt</span> <span class="progPtsText"><%= GetBenchComparisonPointsText(tableRowItem) %></span></td>
            <td class="cFeedb">
              <div class="scoreBars">
                <div class="scoreBar">
                  <div class="barTitle">Self</div>
                  <div class="barBg">
                    <span class="barLine barSelf" <%= $@"style=""width:{GetCSSPercentFromScore(tableRowItem.QuestionInfo.Scores.ScoreSelf.Avg)}""" %>></span>
                    <span class="barMark"></span>
                    <span class="barMark"></span>
                    <span class="barDot dotSelf" title="<%= GetScoreFormatted(tableRowItem.QuestionInfo.Scores.ScoreBenchSelf.Avg) %>"
                      <%= $@"style=""left:{GetCSSPercentFromScore(tableRowItem.QuestionInfo.Scores.ScoreBenchSelf.Avg)}""" %>></span>
                  </div>
                  <div class="barScore"><%= GetScoreFormatted(tableRowItem.QuestionInfo.Scores.ScoreSelf.Avg) %></div>
                </div>
                <div class="barInterGap"></div>
                <div class="scoreBar">
                  <div class="barTitle">Raters</div>
                  <div class="barBg">
                    <span class="barLine barRater" <%= $@"style=""width:{GetCSSPercentFromScore(tableRowItem.QuestionInfo.Scores.ScoreRater.Avg)}""" %>></span>
                    <span class="barMark"></span>
                    <span class="barMark"></span>
                    <span class="barDot dotRater" title="<%= GetScoreFormatted(tableRowItem.QuestionInfo.Scores.ScoreBenchRater.Avg) %>"
                      <%= $@"style=""left:{GetCSSPercentFromScore(tableRowItem.QuestionInfo.Scores.ScoreBenchRater.Avg)}""" %>></span>
                  </div>
                  <div class="barScore"><%= GetScoreFormatted(tableRowItem.QuestionInfo.Scores.ScoreRater.Avg) %></div>
                </div>
              </div>
            </td>
          </tr>
        <% } %>
      </tbody>
    </table>
  </div>
</div>
