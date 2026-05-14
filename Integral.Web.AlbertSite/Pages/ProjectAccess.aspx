<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProjectAccess.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProjectAccess"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <div class="table-responsive">
    <table class="tbl-project-access table table-bordered">
      <thead>
        <tr>
          <th class="minw200 nowrap">Partner</th>
          <th class="minw300 nowrap">Access Programs</th>
          <% if (!isReadOnly) { %>
            <th class="w125">&nbsp;</th>
          <% } %>
        </tr>
      </thead>
      <tbody>
        <% foreach (var user in ProgramAccessUsers) { %>
          <% bool isCurrentClientUser = SessionHelper.IsUserRoleClient && user.UserId == userInfo.UserId; %>
          <tr class="row-user" data-userid="<%= user.UserId %>">
            <td class="nowrap valign-top">
              <h4><%= (user.FirstName + " " + user.LastName).HTMLEncode() %></h4>
              <p><%= user.Email.HTMLEncode() %></p>
            </td>
            <td>
              <form action="#" method="post" onsubmit="return false;" class="form-user-access">
                <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.UpdateUser %>" />
                <input type="hidden" name="<%= FormFields.AccessUserId %>" value="<%= user.UserId %>" />
                <table class="table noborder width-auto">
                <% foreach (var program in user.Programs) { %>
                  <tr class="tr-user-program"><td><input type="checkbox" class="chk-program" name="<%= GetProgramChkName(user.UserId) %>" id="acc_u<%= user.UserId %>_p<%= program.ProgramJobId %>" value="<%= program.ProgramJobId %>" <%= program.HasAccess ? "checked" : "" %> <%= isCurrentClientUser ? "disabled" : "" %> /></td>
                    <td><label for="acc_u<%= user.UserId %>_p<%= program.ProgramJobId %>"><%= program.ProgramJobName.HTMLEncode() %></label></td></tr>
                <% } %>
                </table>
              </form>
            </td>
            <% if (!isReadOnly) { %>
              <td class="align-center">
                <% if (!isCurrentClientUser) { %>
                  <button class="btn btn-primary btn-update-user" disabled>Update</button>
                <% } %>
              </td>
            <% } %>
          </tr>
        <% } %>
      </tbody>
    </table>
  </div>

  <% if (!isReadOnly) { %>

    <div class="table-title">Add Access</div>

    <div class="table-responsive">
      <form id="formAddUser" method="post" action="#" onsubmit="return false;" class="mb20">

        <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.AddUser %>" />

        <table class="tbl-project-access table borderless align-top">
          <thead>
            <tr>
              <th class="mw400 w400 pl20 pr20">Select or Create Contact:</th>
              <th class="pl20 pr20">Select Programs:</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td class="pt15">
                <%= WebHelper.GetSelect(new WebHelper.SelectInfo() {
                    ID = "selFindUser",
                    InputName = FormFields.AccessUserId,
                    Placeholder = "Find or add new Contact",
                    SearchPlaceholder = "Search for Name or Email",
                    TopOptionsHtml = "",
                    Select2WordWrap = true
                  }) %>
                <div class="displaynone w400 add-new-fields" id="newContactInfo">
                  <h5 class="mt20 mb10">New Contact:</h5>
                  <table class="table borderless">
                    <tr><td class="w100 align-right">First Name:</td><td class=""><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.FirstName, NoRow = true }) %></td></tr>
                    <tr><td class="align-right">Last Name:</td><td><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.LastName, NoRow = true }) %></td></tr>
                    <tr><td class="align-right">Email:</td><td><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.EmailAddress, NoRow = true }) %></td></tr>
                    <tr><td class="align-right">Mobile:</td><td><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.MobileNumber, NoRow = true }) %></td></tr>
                    <tr><td class="align-right">Role:</td><td><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.RoleTitle, NoRow = true }) %></td></tr>
                    <tr><td></td><td><button class="btn btn-primary" id="btnAddAndGrant">Add User and Grant Access</button></td></tr>
                  </table>
                </div>

              </td><td>

                <table class="table noborder width-auto">
                <% foreach (var program in ProjectPrograms) { %>
                  <tr class="tr-user-program"><td><input type="checkbox" class="chk-new-program" name="<%= FormFields.GrantAccessJobId %>" id="grant_p<%= program.ProgramJobId %>" value="<%= program.ProgramJobId %>" /></td>
                    <td><label for="grant_p<%= program.ProgramJobId %>"><%= program.JobName.HTMLEncode() %></label></td></tr>
                <% } %>
                </table>

              </td>
            </tr><tr>
              <td><button disabled class="btn btn-primary" id="btnGrantAccess">Grant Access</button></td>
            </tr>
          </tbody>
        </table>
      </form>
    </div>
  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var tlTimeout = null;
      var $formAddUser = $("#formAddUser");
      var $selFindUser = $("#selFindUser");
      var $btnGrantAccess = $("#btnGrantAccess");
      var $newContactInfo = $("#newContactInfo");
      var $btnAddAndGrant = $("#btnAddAndGrant");

      $(document).ready(function() {

        $btnGrantAccess.click(GrantAccess);
        $btnAddAndGrant.click(GrantAccess);
        $(".btn-update-user").click(UpdateUser);
        $(".chk-program").click(UserDirty);
        $(".chk-new-program").click(function (evt) { ProgramCheckHighlight(evt.target); });

        $selFindUser
          .select2({
            placeholder: "Name / Email",
            minimumInputLength: 3,
            dropdownAutoWidth: false,
            width: "100%",
            ajax: {
              url: document.location.href,
              dataType: "json",
              data: function (params) { return { usersearch: params.term }; },
              delay: 750,
              processResults: function (data) { return data; }
            },
            templateResult: formatResult
          })
          .change(ChangeUser);

        $(".chk-program").each(function (i, e) {
          ProgramCheckHighlight(e);
        });

      }); // ready.

      function ProgramCheckHighlight(e) {
        $(e).closest(".tr-user-program").toggleClass("checked", e.checked);
      }

      function formatResult(result) {
        if (result.loading) return $('<div class="pt5 pb5"></div>').text(result.text);
        if (result.id === "<%= PathHelper.AbleUrlValues.IdNew %>") return $('<div class="pt5 pb5"></div>').html(result.text);
        return result.text;
      }

      function formatSelection (selection) {
        return selection.text;
      }

      function ChangeUser() {
        if ($selFindUser.val() == "<%= PathHelper.AbleUrlValues.IdNew %>") {
          $btnGrantAccess.hide();
          $newContactInfo.slideDown(300, SetNewContactFocus);
        } else {
          $btnGrantAccess.show().prop("disabled", false);
          $newContactInfo.hide();
        }
      }

      function SetNewContactFocus() {
        $newContactInfo.find("input:text").eq(0).focus();
      }

      function UserDirty(evt) {
        ProgramCheckHighlight(evt.target);
        var $row = $(evt.target).closest(".row-user");
        var $btnUpdate = $row.find(".btn-update-user");
        var programsChecked = $row.find(".chk-program:checked").length;
        $btnUpdate.prop("disabled", false).text(programsChecked == 0 ? "Remove" : "Update");
      }

      function UserClean(evt) {
        evt.preventDefault();
        $(evt.target).closest(".row-user").find(".btn-update-user").prop("disabled", false);
      }

      function UpdateUser(evt) {
        evt.preventDefault();
        var $btn = $(evt.target);
        AjaxSubmit({
          form: $btn.closest(".row-user").find("form"),
          onSuccess: function (jqXHR, data) {
            $btn.prop("disabled", true);
            var removeUserId = parseInt("" + data["<%= ReturnValues.RemoveUserId %>"], 10);
            if (!isNaN(removeUserId)) $('.row-user[data-userid="' + removeUserId + '"]').fadeOut();
          },
          onFail: function (jqXHR, data) { },
          onError: function (jqXHR, textStatus, errorThrown) {
            if (app_isDev) common_InfoDialog(jqXHR.responseText);
            else common_InfoDialog("Update failed, please try again later.");
          },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function GrantAccess(evt) {
        var $btn = $selFindUser.val() == "<%= PathHelper.AbleUrlValues.IdNew %>" ? $btnAddAndGrant : $btnGrantAccess;
        AjaxSubmit({
          form: $formAddUser,
          onSuccess: function (jqXHR, data) { },
          onFail: function (jqXHR, data) { },
          onError: function(jqXHR, textStatus, errorThrown) {
            if (app_isDev) common_InfoDialog(jqXHR.responseText);
            else common_InfoDialog("Update failed, please try again later.");
          },
          onAlways: function(data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

    })(jQuery);
  </script>

</asp:Content>
