<%@ Control Language="C#" AutoEventWireup="true" CodeFile="OrgRpt_Ovw_Quadrants.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.OrgRpt_Ovw_Quadrants" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<div class="tab-content">
  <div class="ctlOrgRptOvw_Quadrants row">
    <div class="col-xs-12">

      <% foreach (var currentTable in TableList) { %>

        <div class="table-responsive">
          <table class="table tblScores">
            <colgroup>
              <col width="300">
              <col width="150">
              <col width="250">
              <col width="250">
            </colgroup>
            <thead>
              <tr>
                <td class="cTitle"><%= currentTable.TableTitle.HTMLEncode() %></td>
                <td class="cProg">Progress</td>
                <td class="cProg">Comparison to <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %> (self)</td>
                <td class="cFeedb">Your Feedback</td>
              </tr>
            </thead>
            <tbody>
              <% foreach (var currentTableRow in GetTableRows(currentTable)) { %>
                  <tr class="rowSkill rowid_<%= currentTableRow.RowCount %> <%= currentTableRow.GetRowProgressClass() %>">
                    <td class="cTitle"><%= currentTableRow.RowText %></td>
                    <td class="cProg">
                      <span class="previousScore"><%= currentTableRow.GetPreviousScoreText() %> </span>
                      <span class="glyphicon glyphicon-<%= currentTableRow.GetProgressPercentGlyphicon() %>"></span>
                      <span class="progressPercent" title="<%= currentTableRow.GetProgressPercentTitle() %>"><%= currentTableRow.GetProgressPercent() %>%</span>
                    </td>
                    <td class=""><span class="progPtsText"><%= currentTableRow.GetBenchComparisonPointsText() %></span></td>
                    <td class="cFeedb"><%= GetScoreBarHTML(currentTableRow) %></td>
                  </tr>
              <% } %>
            </tbody>
          </table>
        </div>
      <% } %>

    </div>
  </div>
</div>
