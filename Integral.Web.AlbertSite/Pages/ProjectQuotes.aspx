<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProjectQuotes.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProjectQuotes"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (QuoteList.IsNullOrEmpty()) { %>

    <%= GetEmptyStateHtml() %>

  <% } else { %>

    <div class="content-action-bar">
      <div class="right">
        <% if (CanCreateQuoteInProject) { %>
          <a class="btn btn-primary" href="<%= PathHelper.Pages.ProjectQuoteCreate(ProjectInfo.JobNumber) %>">New Quote</a>
        <% } %>
        <% if (CanRequestQuote) { %>
          <%= GetRequestQuoteButtonHtml() %>
        <% } %>
      </div>
    </div>

    <div class="table-responsive">
      <table class="tblQuotes table table-bordered table-hover table-rowlink" data-rowlink-url="">
        <thead>
          <tr>
            <th class="w125 align-center">Created</th>
            <th>Title</th>
            <th class="type-user-nameWithAvatar">Deal Owner</th>
            <th class="w125 align-center">Status</th>
            <th class="w75 align-center">Items</th>
            <th class="w125 align-right pr20">Total ex-GST</th>
            <th class="w75"></th>
          </tr>
        </thead>
        <tbody>
          <% if (!QuoteList.IsNullOrEmpty()) { %>
            <% foreach (var qi in QuoteList) { %>
              <tr tabindex="0" class="rowData" data-rowlink-url="<%= GetRowLinkUrl(qi) %>">
                <td class="align-center"><%= qi.CreatedUtc.UtcToTZ(null).ToString("d MMM yyyy") %></td>
                <td><p class="project-name"><%= GetQuoteOrProjectName(qi).HTMLEncode() %></p><p class="company-name"><%= GetCompanyName(qi).HTMLEncode() %></p></td>
                <td class="type-user-nameWithAvatar"><%= WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(qi, PathHelper.Images.UserPhotoSize.Thumbnail, true), qi.OwnerFullName, qi.OwnerUserId) %></td>
                <td class="align-center"><%= WebHelper.GetStatusBadge(qi.QuoteStatusText.HTMLEncode()) %></td>
                <td class="align-center"><%= qi.QuoteItemCount %></td>
                <td class="align-right pr20"><%= (qi.IsAccepted ? qi.ClientAcceptedAmount.GetValueOrDefault(0) : qi.QuoteItemTotalAmount).ToString("C") %></td>
                <td class="w75"><%= GetQuoteViewIcon(qi) %></td>
              </tr>
            <% } %>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>
</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      $(document).ready(function () {

        <% if (!CanRoleViewQuotes) { %>
          $('.table').data('<%= WebHelper.DataAttrName.RowLinkNewTab %>', true);
        <% } %>

        <% if (CanRequestQuote) { %>
          $("#btnRequestQuote").click(ShowQuoteRequestModal)
        <% } %>

      });

      function ShowQuoteRequestModal() {

        BootstrapDialog.show({
          title: "Request Quote",
          onshow: function (dialogRef) {
            var modalDialog = dialogRef.getModalDialog();
            modalDialog.css("width", "780px");
            modalDialog.data("<%= WebHelper.DataAttrName.DialogRef %>", dialogRef);
            var modalBody = dialogRef.getModalBody();
            modalBody.busyLoad("show");
            modalBody.load('<%= PathHelper.Partials.QuoteRequestModal(ProjectInfo.JobNumber)%>',
              function (data) {
                modalBody.html(data);
                modalBody.busyLoad("hide");
                common_UpdateUI(modalBody);
              }
            );
          },
          onhide: function (dialogRef) {
            var modalDialog = dialogRef.getModalDialog();
            modalDialog.find("textarea.tinymce").each(function (i, e) {
              var mce = $(e).data("editor");
              if (mce != null) mce.remove();
            });
          }
        });
      }

    })(jQuery);
  </script>

</asp:Content>
