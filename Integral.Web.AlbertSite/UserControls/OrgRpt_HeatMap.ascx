<%@ Control Language="C#" AutoEventWireup="true" CodeFile="OrgRpt_HeatMap.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.OrgRpt_HeatMap" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<div class="container-fluid">
  <div class="ctlOrgRptHeatMap row">
    <div class="col-md-11">

      <%
        if (HeatMapData.IsNullOrEmpty()) {
          ShowNoData();
        } else {
          ShowHeatMap();
        }
      %>

      <% void ShowNoData() { %>

        <div align="center">
          <p><img height="140" src="<%= PathHelper.Images.OrgReportHeatMapEmptyState() %>" /></p>
          <br />
          <h4>No Departments.</h4>
          <p>Add departments to your staff to see your results by department.</p>
          <br />
          <p>Contact support for help setting this up.</p>
          <br />
          <p><a class="btn btn-primary btn-sm" target="_blank" href="<%= ConfigHelper.ExternalUrls.CalendlySupportBooking.HTMLEncode() %>">Contact Support</a></p>
        </div>

      <% } %>

      <% void ShowHeatMap() { %>

        <div class="legend">
          <div class="title">Legend:</div>
          <div class="item col1">Under 20</div>
          <div class="item col2">20 to 39</div>
          <div class="item col3">40 to 59</div>
          <div class="item col4">60 and above</div>
        </div>

        <div class="table-responsive">
          <table class="table tblMain">
            <thead>
              <tr>
                <td class="topLeft">Business Unit</td>
                <%= GetSectionTitleCells() %>
              </tr>
            </thead>
            <tbody>
              <% foreach (var heatMapRow in HeatMapData) { %>
                <tr>
                  <td class="rowTitle"><%= GetRowTitle(heatMapRow).HTMLEncode() %></td>
                  <%= GetRowResultCells(heatMapRow) %>
                </tr>
              <% } %>
            </tbody>
          </table>
        </div>

      <% } %>
    </div>
  </div>
</div>

<script>

  $(document).ready(function () {

    $(".ctlOrgRptHeatMap .legend").css("left", $(".tblMain thead .topLeft").outerWidth() + 20);

  });

</script>
