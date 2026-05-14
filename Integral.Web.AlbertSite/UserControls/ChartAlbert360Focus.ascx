<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ChartAlbert360Focus.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.ChartAlbert360Focus" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<%-- Note requires reports.css --%>

<div class="RptPub360Focus row">

  <div class="comments col-xs-12 col-md-9">
    <div class="legend">
      <span class="self circle"></span>Self <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %>
      <span class="rater circle"></span>Rater <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %>
    </div>
    <%= FocusHtml %>
  </div>
</div>

