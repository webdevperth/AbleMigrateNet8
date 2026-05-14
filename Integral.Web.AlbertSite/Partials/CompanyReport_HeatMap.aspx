<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="CompanyReport_HeatMap.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.CompanyReport_HeatMap" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<%-- Note requires reports.css --%>

<%@ Import Namespace="Integral.Web" %>

<div class="Rpt360HeatMap">

  <div class="flex flex-align-center mb10">
    <div class="flex1"></div>
    <div class="flex flex-align-center gap10">
      <div>Sort By: </div>
      <select class="CompanyReport_HeatMap_SortBy w125 noselect2">
        <% foreach(var option in ReportHelper.HeatMap.RowSortOptions) { %>
          <option value="<%= option.Key.HTMLEncode() %>" <%= (QueryRowSortBy == option.Value.OptionEnum).ToValue("selected") %>><%= option.Value.DisplayText.HTMLEncode() %></option>
        <% } %>
      </select>
    </div>
  </div>

  <div class="table-responsive">
    <table class="table tblScores <%= HeatMapRows.Count < 5 ? "fewRows" : "" %>">
      <thead>
        <tr>
          <td>
            Overall Leadership Indicators
            <%= WebHelper.GetIconTooltipByElementId(WebHelper.ActionButtonTypeEnum.info, "Legend", "tooltip-legend") %>
          </td>
          <% foreach (var col in HeatMapColumns) { %>
            <td class="">
              <%= col.ColumnTitle.HTMLEncode().RegexReplace("([^ ]{4,}) ((?=[^ ]{4,}))", "$1<br/>$2", RegexOptions.Compiled) %>
              <% if (ConfigHelper.IsDevServer) Response.Write(col.BenchScore.ToString("(0.0)", "-")); %>
            </td>
          <% } %>
        </tr>
      </thead>
      <tbody>
        <% for(int iRow = 0; iRow < HeatMapRows.Count; iRow++) { %>
          <% var row = HeatMapRows[iRow]; %>
          <tr>
            <td>
              <%= row.RowTitle.HTMLEncode() %>
              <% if (ConfigHelper.IsDevServer) Response.Write(row.RowScore.Score.ToString("(0.0)", "-")); %>
            </td>
            <% for(int iCol = 0; iCol < row.ColumnScores.Count; iCol++) { %>
              <% var colScoreInfo = row.ColumnScores[iCol].ScoreInfo; %>
              <td class="<%= ReportHelper.HeatMap.GetTemperature(colScoreInfo).ClassName ?? "" %>"><%= colScoreInfo.Score.ToString("0.0", "-") %></td>
            <% } %>
          </tr>
        <% } %>
      </tbody>
    </table>
  </div>
</div>

<div id="tooltip-legend" class="Rpt360HeatMap legend display-none">
  <table>
    <% foreach (var temp in Enumerable.Reverse(ReportHelper.HeatMap.ScoreTemperatures)) { %>
      <tr>
        <td><div class="color-block <%= temp.ClassName %>"></div></td>
        <td><%= temp.LegendText.HTMLEncode() %></td>
      </tr>
    <% } %>
  </table>
</div>

<script type="text/javascript">

  (function ($) {

    var selSortBy, partialInfo;

    $(document).ready(function () {

      selSortBy = $(".CompanyReport_HeatMap_SortBy");
      selSortBy.change(ChangeSortBy);
      partialInfo = common_GetPartialInfo(selSortBy);

    });

    function ChangeSortBy() {
      partialInfo.LoadUrl(null, { "<%= ReportHelper.HeatMap.QueryKeys.RowSortBy %>": selSortBy.val() });
    }

  })(jQuery);

</script>
