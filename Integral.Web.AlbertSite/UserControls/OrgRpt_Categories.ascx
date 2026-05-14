<%@ Control Language="C#" AutoEventWireup="true" CodeFile="OrgRpt_Categories.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.OrgRpt_Categories" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>


<div class="tab-content">
  <div class="OrgRptCats row">
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
              <% foreach (var tableRowItem in GetTableRows(currentTable)) { %>
                  <% var tableRow = tableRowItem.Value; %>
                  <tr class="rowSkill rowid_<%= tableRow.RowCount %> <%= tableRow.GetRowProgressClass() %>">
                    <td class="cTitle"><%= tableRow.RowText %></td>
                    <td class="cProg">
                      <span class="previousScore"><%= tableRow.GetPreviousScoreText() %> </span>
                      <span class="glyphicon glyphicon-<%= tableRow.GetProgressPercentGlyphicon() %>"></span>
                      <span class="progressPercent" title="<%= tableRow.GetProgressPercentTitle() %>"><%= tableRow.GetProgressPercent() %>%</span>
                    </td>
                    <td class=""><span class="progPtsText"><%= tableRow.GetBenchComparisonPointsText() %></span></td>
                    <td class="cFeedb"><%= GetScoreBarHTML(tableRow) %></td>
                  </tr>
              <% } %>
            </tbody>
          </table>
        </div>
      <% } %>

    </div>
  </div>
</div>
