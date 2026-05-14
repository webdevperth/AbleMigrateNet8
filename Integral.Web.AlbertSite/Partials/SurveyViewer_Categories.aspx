<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="SurveyViewer_Categories.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.SurveyViewer_Categories" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<%-- Note requires reports.css --%>

<%@ Import Namespace="Integral.Web" %>

<div class="ctlCA360FT">

  <div class="table-responsive">

    <table class="table tblScores">
      <%--
      <colgroup>
        <col width="400">
        <col width="350">
        <col width="">
      </colgroup>
      --%>
      <thead>
        <tr>
          <td class="cTitle minw250">Overall Leadership Indicators</td>
          <% if (!HideComparisonColumn) { %>
            <td class="cProg minw250">
              <%= HasPreSurvey ? $"Pre-Post" : $"Comparison to {OrgReports.Benchmark_Display_Text.UCaseFirstChar()}" %>
            </td>
          <% } %>
          <td class="cFeedb minw200"><%= HideComparisonColumn ? "" : "Your Feedback" %></td>
        </tr>
      </thead>
      <tbody>

        <% foreach (var category in Categories) { %>

          <% string surveySelfStyle = "style=\"width:" + WebHelper.GetCSSPercentFromRatio(category.SurveySelfScore, category.CategoryMaxScore) + "\""; %>
          <% string surveyRaterStyle = "style=\"width:" + WebHelper.GetCSSPercentFromRatio(category.SurveyRaterScore, category.CategoryMaxScore) + "\""; %>
          <% string normSelfStyle = "style=\"left:" + WebHelper.GetCSSPercentFromRatio(category.NormSelfScore, category.CategoryMaxScore) + "\""; %>
          <% string normRaterStyle = "style=\"left:" + WebHelper.GetCSSPercentFromRatio(category.NormRaterScore, category.CategoryMaxScore) + "\""; %>

          <tr class="rowSkill rowid_<%= category.CategoryHeadingId %> <%= GetBenchComparisonRowClass(category) %>">
            <td class="cTitle"><%= category.CategoryHeading.HTMLEncode() %></td>
            <% if (!HideComparisonColumn) { %>
              <td class="cProg">
                <div class="progPts">
                  <% if (HasPreSurvey) { %>
                    <p><%= GetPrePostComparisonText(category.SurveySelfScore, category.PreSurveySelfScore) %></p>
                    <p class="progPtsPre"><%= GetPrePostComparisonText(category.SurveyRaterScore, category.PreSurveyRaterScore) %></p>
                  <% } else { %>
                    <%= GetBenchComparisonText(category) %>
                  <% } %>
                </div>
              </td>
            <% } %>
            <td class="cFeedb">
              <div class="scoreBars">
                <div class="scoreBar">
                  <div class="barTitle">Self</div>
                  <div class="barBg">
                    <span class="barLine barSelf" <%= surveySelfStyle %>></span>
                    <span class="barDot dotSelf" title="<%= NormDisplayName %> Norm = <%= GetScoreFormatted(category.NormSelfScore) %>" <%= normSelfStyle %>></span>
                  </div>
                  <div class="barScore"><%= GetScoreFormatted(category.SurveySelfScore) %></div>
                </div>
                <% if (RaterCount > 0) { %>
                  <div class="scoreBar mt15">
                    <div class="barTitle">Rater</div>
                    <div class="barBg">
                      <span class="barLine barRater" <%= surveyRaterStyle %>></span>
                      <span class="barDot dotRater" title="<%= NormDisplayName %> Norm = <%= GetScoreFormatted(category.NormRaterScore) %>" <%= normRaterStyle %>></span>
                    </div>
                    <div class="barScore"><%= GetScoreFormatted(category.SurveyRaterScore) %></div>
                  </div>
                <% } %>
              </div>
            </td>
          </tr>
        <% } %>

      </tbody>
    </table>
  </div>
</div><%-- ctlCA360FT --%>


<script type="text/javascript">

  (function ($) {

    $(document).ready(function () {
      new jBox('Tooltip', {
        attach: '.ctlCA360FT .barDot[title]', position: { y: 'top' }
      });
    });

  })(jQuery);

</script>
