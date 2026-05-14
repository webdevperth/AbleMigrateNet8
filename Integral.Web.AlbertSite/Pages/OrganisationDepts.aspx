<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="OrganisationDepts.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.OrganisationDepts"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (DeptList.IsNullOrEmpty()) { %>

    <%= WebHelper.GetEmptyStatePageHtml(
          title: "Departments",
          description: "No departments yet, add your first one!",
          addActionHtml: true,
          customActionHtml: GetAddDepartnerButton()) %>

  <% } else { %>

    <div class="content-action-bar">
      <div class="right">
        <%= GetAddDepartnerButton() %>
      </div>
    </div>

    <div class="table-responsive">
      <table class="table table-bordered table-hover">
        <thead>
          <tr>
            <th>Department Name</th>
            <th class="w75"></th>
          </tr>
        </thead>
        <tbody>
          <% foreach (var dept in DeptList) { %>
            <tr tabindex="0" class="deptRow" data-id="<%= dept.CompanyDeptId %>">
              <td><%= dept.CompanyDeptName.HTMLEncode() %></td>
              <td class="w75"><%= WebHelper.GetActionButton(WebHelper.ActionButtonTypeEnum.edit, "", "Edit Department") %></td>
            </tr>
          <% } %>
        </tbody>
      </table>
    </div>

    <div id="dlgDeptInfo" class="displaynone" data-editrow="">
      <table class="mt0 w100p"><tr>
        <td class="w100 bordernone">Dept Name:</td>
        <td class=""><input type="text" class="form-control" id="inpDeptName" /></td>
      </tr></table>
    </div>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var $inpDeptName;

      $(document).ready(function() {

        $inpDeptName = $("#inpDeptName");

        $(".deptRow").click(function (ev) {
          DeptClick($(this).data("id"));
        });

        $("#btnAddDept").click(function (ev) {
          ShowDeptModal(0); // add dept.
        });


      }); // ready.

      function DeptClick(companyDeptId) {
        companyDeptId = toDecimalInt(companyDeptId, 0);
        if (companyDeptId == 0) {
          ShowDeptModal(0);
          return;
        }
        // Get dept info.
        AjaxSubmit({
          busyLoadElement: null,
          action: "<%= AjaxAction.GetDeptInfo %>",
          data: {
            "<%= FormFields.DeptId %>": companyDeptId
          },
          onSuccess: function (jqXHR, data) {
            if (data["<%= ReturnValues.DeptName %>"]) $inpDeptName.val(data["<%= ReturnValues.DeptName %>"]);
            ShowDeptModal(companyDeptId);
          },
        });
      }

      function ShowDeptModal(companyDeptId) {
        if (companyDeptId == 0) $inpDeptName.val("");
        var dlg = common_InfoDialog("#dlgDeptInfo", {
          title: companyDeptId == 0 ? "Add Department" : "Edit Department",
          width: 550,
          focus: $inpDeptName,
          buttons: [
            { text: "Delete", class: "btnDelete btn-warning mr20 float-left" + (companyDeptId == 0 ? " display-none" : ""), isDefault: false, isPrimary: false, close: false, click: function (ev) { SubmitDeptModal(companyDeptId, ev, dlg); } },
            { text: "Cancel", class: "btn-secondary mr20 left", isDefault: false, isPrimary: false, close: true },
            { text: companyDeptId > 0 ? "Update" : "Add Department", isDefault: true, isPrimary: true, close: false, click: function (ev) { SubmitDeptModal(companyDeptId, ev, dlg); } }
          ],
          shown: function ($modal, e) { }
        });
      }

      function SubmitDeptModal(companyDeptId, ev, dlg) {

        $btn = $(ev.target);
        var isDelete = $btn.hasClass("btnDelete");

        AjaxSubmit({
          action: (isDelete ? "<%= AjaxAction.DeleteDept %>" : "<%= AjaxAction.UpdateDept %>"),
          data: {
            "<%= FormFields.DeptId %>": companyDeptId,
            "<%= FormFields.DeptName %>": $inpDeptName.val()
          },
          onSuccess: function (jqXHR, data) { },
        });

      }

    })(jQuery);
  </script>

</asp:Content>
