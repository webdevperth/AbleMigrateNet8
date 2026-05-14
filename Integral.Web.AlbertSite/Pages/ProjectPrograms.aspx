<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProjectPrograms.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProjectPrograms"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (ProgramsInProject == null || ProgramsInProject.TotalRows == 0) { %>

    <%= WebHelper.GetEmptyStatePageHtml(
      title: "Programs",
      description: $"No programs in project yet. {(CanCreateProgram ? "Add the first one to get started!" : "")}",
      addActionHtml: CanCreateProgram,
      actionButtonText: "Add Program",
      actionButtonPath: PathHelper.Pages.Programs_Add(ProjectInfo.JobNumber, Request.RawUrl))
    %>

  <% } else { %>

    <% if (CanCreateProgram) { %>
      <div class="content-action-bar">
        <div class="right">
          <a class="btn btn-primary" href="<%= PathHelper.Pages.Programs_Add(ProjectInfo.JobNumber, Request.RawUrl) %>">Add New Program</a>
        </div>
      </div>
    <% } %>

    <div class="table-responsive">
      <table id="tblPrograms" class="table table-bordered table-hover table-rowlink limit-width" data-rowlink-url="<%= GetRowLinkUrl() %>">
        <thead>
          <tr>
            <th class="type-description">Program Name</th>
            <th class="type-date-range">Date</th>
            <th class="type-status">Status</th>
            <th class="type-date">Req Sent</th>
            <th class="type-qty">Participants</th>
            <th class="type-qty">Workshops</th>
            <th class="type-revenueprogress"><%= WebHelper.GetRevenueCompletionColTitle() %></th>
          </tr>
        </thead>
        <tbody>
          <% foreach (var program in ProgramsInProject.ProgramInfoList) { %>
            <tr class="rowData" id="rowTemplate" data-rowlink-id="<%= program.ProgramJobId %>" tabindex="0">
              <td class="type-description"><%= program.ProgramJobName.HTMLEncode() %></td>
              <td class="type-date-range"><%= WebHelper.DisplayDate(program.ProgramStartDateUtc.UtcToTZOrNull(null), "-") %> -
                                        <%= WebHelper.DisplayDate(program.ProgramEndDateUtc.UtcToTZOrNull(null), "-") %></td>
              <td class="type-status"><%= WebHelper.GetStatusBadge(DbHelper.AlbertProgramStatus.GetProgramStatusById(program.ProgramStatusId).DisplayTitle.HTMLEncode()) %></td>
              <td class="type-date"><%= WebHelper.DisplayDate(program.ParticipantFormEmailSentUtc.UtcToTZOrNull(null)) %></td>
              <td class="type-qty"><%= program.ParticipantCount %></td>
              <td class="type-qty"><%= program.WorkshopCount %></td>
              <td class="type-revenueprogress"><%= GetRevenueProgressHtml(program) %></td>
            </tr>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      $(document).ready(function() {

      }); // ready.

    })(jQuery);
  </script>

</asp:Content>
