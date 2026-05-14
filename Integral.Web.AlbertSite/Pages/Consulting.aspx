<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="Consulting.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.Consulting"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (ConsultingListVisible) { %>

    <% if (ConsultingItemList.IsNullOrEmpty()) { %>

      <%= WebHelper.GetEmptyStatePageHtml(
          title: "Consulting Items",
          description: $"No consulting items yet. {(CanAddConsulting ? " Add your first one!" : "")}",
          addActionHtml: CanAddConsulting,
          actionButtonText: "Add Consulting Item",
          actionButtonPath: PathHelper.Pages.Consulting_Add(ProgramInfo.ProgramJobId)) %>

    <% } else { %>

      <% if (CanAddConsulting) { %>
        <div class="content-action-bar">
          <div class="right">
            <a href="<%= PathHelper.Pages.Consulting_Add(ProgramInfo.ProgramJobId) %>" class="btn btn-primary">Add Consulting Item</a>
          </div>
        </div>
      <% } %>

      <div class="table-responsive">
        <table class="tblConsulting table table-bordered table-hover table-rowlink" data-rowlink-url="<%= PathHelper.Pages.Consulting_Edit(ProgramInfo.ProgramJobId, null) %>">
          <thead>
            <tr>
              <th class="type-date">Completion</th>
              <th class="type-description">Title</th>
              <th class="type-user-nameWithAvatar">Consultant</th>
              <% if (CanViewTotalRevenue) { %>
                <th class="type-money"><%= RevenueTextDisplay %></th>
              <% } %>
              <% if (CanViewPartnerRevenue) { %>
                <th class="type-money">Partner</th>
              <% } %>
            </tr>
          </thead>
          <tbody>
            <% if (ConsultingItemList != null) { %>
              <% foreach (var item in ConsultingItemList) { %>
                <tr tabindex="0" class="rowData" data-rowlink-id="<%= item.ConsultingItemId %>">
                  <td class="type-date"><%= item.CompletionDateUtc.UtcToTZOrNull(ConfigHelper.DefaultTimeZoneInfo).ToString("d MMM yyyy") %></td>
                  <td class="type-description"><%= item.ItemTitle.HTMLEncode() %></td>
                  <td class="type-user-nameWithAvatar">
                    <%= WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(item, PathHelper.Images.UserPhotoSize.Thumbnail, true), item.ConsultantFirstName + " " + item.ConsultantLastName, item.ConsultantUserId) %>
                  </td>
                  <% if (CanViewTotalRevenue) { %>
                    <td class="type-money"><%= item.ItemAmount.ToString("C") %></td>
                  <% } %>
                  <% if (CanViewPartnerRevenue) { %>
                    <td class="type-money"><%= WebHelper.GetPartnerRevenueValue(item.ItemAmount, ProgramInfo.Partner_DeliveryPercentage, item.ConsultantUserId == userInfo.UserId, CanViewAllDeliveryTeamRevenue) %></td>
                  <% } %>
                </tr>
              <% } %>
            <% } %>
          </tbody>
        </table>
      </div>

    <% } %>

  <% } %>

  <% if (ConsultingFormVisible) { %>

    <div class="container-fluid">

      <form id="formItem" method="post" action="#" onsubmit="return false" class="form-horizontal">

        <input type="hidden" name="<%= FormFields.ProgramJobId %>" value="<%= ProgramInfo.ProgramJobId %>" />
        <input type="hidden" name="<%= FormFields.ConsultingItemId %>" value="<%= IsNewItem ? "" : ConsultingItemInfo.ConsultingItemId.ToString() %>" />

        <%= WebHelper.GetTextInput("Title:", FormFields.ItemTitle, ConsultingItemInfo.ItemTitle, 7, "", IsReadOnly || IsLimitedEdit) %>

        <%= WebHelper.GetTextArea("Description:", FormFields.Description, 2, 7, ConsultingItemInfo.Description, "", IsReadOnly) %>

        <%= WebHelper.GetSelectRow("Consulting Type:", FormFields.ConsultingTypeId, 3, GetConsultingTypeOptions(), "", IsReadOnly || IsLimitedEdit) %>

        <% if (CanViewTotalRevenue) { %>
          <%= WebHelper.GetCurrencyInput("Amount:", FormFields.RevenueAmount, ConsultingItemInfo.ItemAmount, 2, 2, "", IsReadOnly || IsLimitedEdit) %>
        <% } %>

        <% if (IsReadOnly || IsLimitedEdit) { %>
          <%= WebHelper.GetTextDisplayRow("Consultant:", 3, GetConsultantName()) %>
        <% } else { %>
          <%= GetConsultantDropdownHtml() %>
        <% } %>

        <%= WebHelper.GetInputDateRow("Completion:", FormFields.CompletionDateLocal, ConsultingItemInfo.CompletionDateUtc.UtcToTZOrNull(), "", IsReadOnly) %>

        <% if (CanSetQuoteItem) { %>
          <%= WebHelper.GetQuoteItemSelectRow("Quote Item:", 7,
                FormFields.QuoteItemId,
                IsNewItem, ConsultingItemInfo?.ComponentQuoteInfo,
                GetQuoteItemOptionsHtml(),
                IsLimitedEdit || IsReadOnly) %>
        <% } %>

        <% if (CanMoveToProgram && !IsReadOnly) { %>
          <%= WebHelper.GetSelectRow("Move to Program:", FormFields.MoveToProgramJobId, 7, GetMoveToProgramOptions(), "", IsLimitedEdit || IsReadOnly) %>
        <% } %>

        <div class="btnholder">
          <% if (!IsReadOnly) { %>
            <button type="button" class="btn btn-primary btnUpdate floatright" id="btnUpdate"><%= IsNewItem ? "Add New" : "Update" %> Item</button>
            <% if (CanDeleteConsulting) { %>
              <button type="button" class="btn btn-warning btnDelete floatleft" id="btnDelete">Delete Item</button>
            <% } %>
          <% } %>
          <button type="button" class="btn btn-secondary btnCancel <%= IsReadOnly ? "floatleft" : "floatright mr20" %>" data-mode="cancel"><%= IsReadOnly ? "Back" : "Cancel" %></button>
        </div>

      </form>
    </div>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var btnUpdate, formItem, isNewItem;

      $(document).ready(function() {

        // If user cannot navigate from table, remove the link of navigation.
        <% if (!CanNavigateFromConsultingTable ) { %>
          $('table').removeAttr('data-rowlink-url');
        <% } %>

        btnUpdate = $("#btnUpdate");
        btnDelete = $("#btnDelete");
        formItem = $("#formItem");

        $(".btnCancel").click(function (e) { history.go(-1); });

        isNewItem = <%= IsNewItem ? "true" : "false" %>;

        btnUpdate.click(UpdateItem);
        btnDelete.click(DeleteItem);

        if (isNewItem) formItem.find("input:text:not(:disabled):first").trigger("focus");

      }); // ready.

      function DeleteItem() {

        if (!confirm("Delete this Item?")) return;

        AjaxSubmit({
          form: formItem,
          action: "<%= AjaxAction.DeleteItem %>"
        });
      }

      function UpdateItem() {
        AjaxSubmit({
          form: formItem,
          action: "<%= AjaxAction.UpdateItem %>"
        });
      }

    })(jQuery);
  </script>

</asp:Content>
