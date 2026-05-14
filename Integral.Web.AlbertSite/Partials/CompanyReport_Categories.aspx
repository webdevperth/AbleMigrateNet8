<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="CompanyReport_Categories.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.CompanyReport_Categories" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<%-- Note requires reports.css --%>

<%@ Import Namespace="Integral.Web" %>

<div class="ctlCA360FT">

  <div class="table-responsive">

    <table class="table tblScores">
      <thead>
        <tr>
          <td class="cTitle minw250">Overall Leadership Indicators</td>
          <td class="cProg minw250">
            <%= SurveyStats.HasPreSurvey ? $"Pre-Post" : $"Comparison to {OrgReports.Benchmark_Display_Text.UCaseFirstChar()}" %>
          </td>
          <td class="cFeedb minw200">Your Feedback</td>
        </tr>
      </thead>
      <tbody>

        <% foreach (var category in Categories) { %>

          <tr class="rowSkill rowid_<%= category.CategoryHeadingId %> <%= GetBenchComparisonRowClass(category) %>">
            <td class="cTitle"><%= category.CategoryHeading.HTMLEncode() %></td>
            <td class="cProg">
              <div class="progPts">
                <% if (SurveyStats.HasPreSurvey) { %>
                  <p><%= GetPrePostComparisonText(category.SelfPostScore, category.SelfPreScore) %></p>
                  <p class="progPtsPre"><%= GetPrePostComparisonText(category.RaterPostScore, category.RaterPreScore) %></p>
                <% } else { %>
                  <%= GetBenchComparisonText(category) %>
                <% } %>
              </div>
            </td>
            <td class="cFeedb">
              <div class="scoreBars">
                <div class="scoreBar">
                  <div class="barTitle">Self</div>
                  <div class="barBg">
                    <span class="barLine barSelf" <%= BarWidthStyle(category.SelfPostScore ?? category.SelfAllScore) %>></span>
                    <span class="barDot dotSelf" title="<%= BenchmarkDisplayName %> Norm = <%= GetScoreFormatted(category.SelfBenchScore) %>" <%= DotPosStyle(category.SelfBenchScore) %>></span>
                  </div>
                  <div class="barScore"><%= GetScoreFormatted(category.SelfPostScore ?? category.SelfAllScore) %></div>
                </div>
                <% if (SurveyStats.RaterAllCount > 0) { %>
                  <div class="scoreBar mt15">
                    <div class="barTitle">Rater</div>
                    <div class="barBg">
                      <span class="barLine barRater" <%= BarWidthStyle(category.RaterPostScore ?? category.RaterAllScore) %>></span>
                      <span class="barDot dotRater" title="<%= BenchmarkDisplayName %> Norm = <%= GetScoreFormatted(category.RaterBenchScore) %>" <%= DotPosStyle(category.RaterBenchScore) %>></span>
                    </div>
                    <div class="barScore"><%= GetScoreFormatted(category.RaterPostScore ?? category.RaterAllScore) %></div>
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
