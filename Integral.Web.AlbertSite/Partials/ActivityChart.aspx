<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="ActivityChart.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.ActivityChart" %>

<%@ Import Namespace="Integral.Web" %>

  <% if (ShowEmptyStatePageHtml) { %>

    <%= WebHelper.GetEmptyStatePageHtml(
      title: "Program Insights",
      description: $"No insights yet. {(CanSendSurvey ? "Add your first survey to get program insights!" : "")}",
      addActionHtml: CanSendSurvey,
      actionButtonText: "Add Survey",
      actionButtonPath: PathHelper.Pages.ProgramSendSurvey()) %>

  <% } else { %>

    <div class="flex flex-space-between">
      <div class="table-title">Monthly <%= SelectedChartUnits == PathHelper.ActivityChartUnits.Minutes ? "Activity Minutes" : "Interactions" %></div>
      <%= GetUnitsButtonsHtml() %>
    </div>

    <%= WebHelper.Charts.GetStackedBarChart(PixelHeight, StackLegend, Bars) %>

  <% } %>

<script type="text/javascript">

  (function ($) {

    $(document).ready(function () {

      var partialInfo = <%= WebHelper.GetJQueryPartialInfo() %>;

      var $unitsButtons = partialInfo.contentElement.find('[name="<%= UnitsButtonsName %>"]');

      $unitsButtons.on("change", function (evt) {
        partialInfo.LoadUrl(null, { "<%= PathHelper.AbleUrlKeys.ChartUnits %>": $(evt.target).val() });
      });

    });

  })(jQuery);

</script>
