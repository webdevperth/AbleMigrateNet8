<%@ Control Language="C#" AutoEventWireup="true" CodeFile="OrgRpt_Focus.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.OrgRpt_Focus" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<div class="tab-content">
  <div class="ctlOrgRpt_Focus row">
    <div class="col-xs-12">

      <% foreach (var table in TableList) { %>

        <div class="table-responsive <%= table.TableMode == OrgReports.FocusTableMode.Highest ? "modeHighest" : "modeLowest" %>">
          <table class="table tblScores">
            <colgroup>
              <col width="700">
              <col width="250">
            </colgroup>
            <thead>
              <tr>
                <td class="cTitle"><span class="circle"></span><%= table.TableTitle.HTMLEncode() %></td>
                <td class="cFeedb">&nbsp;</td>
              </tr>
            </thead>
            <tbody>
              <% foreach (var tableRow in GetTableRows(table)) { %>
                <tr class="rowSkill rowid_<%= currentTableRow.RowCount %>">
                  <td class="cTitle">
                    <%= reportData.SurveyInfo.OrgReportDisableDrivers ? "" :
                        (@"<div><div class=""sectionTitle"">" + currentTableRow.SectionTitle.HTMLEncode() + "</div></div>") %>
                    <span class="qnNum"><%= currentTableRow.QuestionNumber %>.</span><%= currentTableRow.RowText %>
                  </td>
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
