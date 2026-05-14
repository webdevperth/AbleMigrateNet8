<%@ Page Language="C#" AutoEventWireup="true" CodeFile="AddParticipant_QuickAdd_Existing.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.AddParticipant_QuickAdd_Existing" %>

<%@ Import Namespace="Integral.Web" %>

<div class="container-fluid">

  <form id="participantForm" class="form-horizontal" >

    <div class="minh25 mb20">
      <%= GetAssignationInfo() %>
    </div>

    <%= WebHelper.GetSelectRow("Organisation People:", FormFields.OrgPeople, 8, GetOrgPeopleOptions()) %>

    <% if (CanAddParticipant) { %>

      <div class="table-responsive mt10">
        <table class="tblpart table table-bordered table-hover table-rowlink">
          <thead>
            <tr>
              <th class="type-fullname">Participant Name</th>
              <th class="w50"></th>
            </tr>
          </thead>
          <tbody id="tblOrgPeople">
          </tbody>
        </table>
      </div>

      <div class="btnholder">
        <button type="button" class="btn btn-primary floatright btnAddParticipants" data-waitmsg="Updating...">Add Participant Selection</button>
      </div>

    <% } %>

  </form>
</div>

<% var scriptId = new Random((int)DateTime.Now.Ticks).Next(100000); %>

<script id="<%= scriptId %>" type="text/javascript">
  (function ($) {

    var btnAddParticipants, tblOrgPeople, selOrgPeople;
    var modalBody = null;
    var modalForm = null;

    $(document).ready(function () {

      // Find modal objects based on the random ID given to this script tag.
      var scripts = document.getElementsByTagName("script");
      for (var i = scripts.length - 1; i >= 0; i--) {
        if (scripts[i].id == <%= scriptId %>) {
          var thisScript = $(scripts[i]);
          var modalDialog = thisScript.closest(".modal-dialog");
          modalBody = modalDialog.find(".modal-body");
          modalForm = modalBody.find("form");
          break;
        }
      }

      btnAddParticipants = modalForm.find(".btnAddParticipants");
      tblOrgPeople = modalForm.find("#tblOrgPeople");
      selOrgPeople = modalForm.find('[name="<%= FormFields.OrgPeople %>"]');

      btnAddParticipants.click(AddSelectedParticipants);
      selOrgPeople.change(AppendSelectedUserToTable);

      $(document).on('click', '.btnRemoveUser', function () {
        $(this).closest('tr').remove();
      });
    });

    function AppendSelectedUserToTable() {
      var selectedUser = selOrgPeople.val();
      var selectedUserText = selOrgPeople.find('option:selected').text();

      if (selectedUser !== "") {
        // Check if a row with the same data-userid already exists
        if (!tblOrgPeople.find('tr[data-<%= FormFields.UserId %>="' + selectedUser + '"]').length) {
          var row = '<tr data-<%= FormFields.UserId %>="' + selectedUser + '"><td class="type-fullname">' + selectedUserText + '</td><td class="w50"><button class="btnRemoveUser"><%= WebHelper.Icon.Trash %></button></td></tr>';
          tblOrgPeople.append(row);
        } else {
          // User already exists in the table, handle accordingly
          common_InfoDialog("User already exists in the table.");
        }
      }
    }

    function AddSelectedParticipants(evt) {
      var participantForm = modalForm.find("#participantForm");

      // Gather selected users
      var userIdArray = [];
      tblOrgPeople.find("tr").each(function () {
        if ($(this).data("<%= FormFields.UserId %>") !== undefined) {
          userIdArray.push($(this).data("<%= FormFields.UserId %>"));
        }
      });

      if (userIdArray.length === 0) {
        common_InfoDialog("No participants were selected.");
        return;
      }

      AjaxSubmit({
        form: participantForm,
        url: "<%= PathHelper.CurrentUrl %>",
        action: "<%= AjaxAction.AddExistingParticipants %>",
        data: {
          "<%= FormFields.UserId %>": userIdArray
        },
        onSuccess: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) {
          common_InfoDialog("Failed to add participants, please try again later.");
        },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }

  })(jQuery);

</script>
