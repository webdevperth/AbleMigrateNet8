<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ChartAlbert360Detailed.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.ChartAlbert360Detailed" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<div class="RptPub360Detailed row">

  <div class="col-xs-12 col-md-9">

    <div class="row mt20 mb20">
      <div class="col-md-6">
        <h4>All questions</h4>
      </div>
      <div class="col-md-6">
        <div class="legend">
          <% if (!reportResults.SurveyInfo.IsRatersOnly) { %>
            <span class="self circle"></span>Self <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %>
          <% } %>
          <span class="rater circle"></span>Rater <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %>
        </div>
      </div>
    </div>

    <%= DetailsHtml %>

  </div>
</div>
