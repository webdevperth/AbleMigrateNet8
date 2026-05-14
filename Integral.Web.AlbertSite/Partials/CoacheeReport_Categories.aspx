<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="CoacheeReport_Categories.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.CoacheeReport_Categories" %>

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
            <%= HasPreSurvey ? $"Pre-Post" : $"Comparison to {OrgReports.Benchmark_Display_Text.UCaseFirstChar()}" %>
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
                <% if (HasPreSurvey) { %>
                  <p><%= GetPrePostComparisonText(category.SurveySelfScore, category.PreSurveySelfScore) %></p>
                  <p class="progPtsPre"><%= GetPrePostComparisonText(category.SurveyRaterScore, category.PreSurveyRaterScore) %></p>
                <% } else { %>
                  <%= GetBenchComparisonText(category) %>
                <% } %>
              </div>
            </td>
            <td class="cFeedb">
              <div class="scoreBars" data-tooltip="<b>Rater Result</b><br>Compared to Self: <%= category.SurveyRaterScore - category.SurveySelfScore %>">

                <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Self, ScoreMinValue, ScoreMaxValue,
                                                      "Self", category.SurveySelfScore, NormDisplayName, Hide360ReportNorms ? null : category.NormSelfScore) %>

                <% if (RaterCount > 0) { %>
                  <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Rater, ScoreMinValue, ScoreMaxValue,
                                                      "Rater", category.SurveyRaterScore, NormDisplayName, Hide360ReportNorms ? null : category.NormRaterScore, "mt15") %>
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
