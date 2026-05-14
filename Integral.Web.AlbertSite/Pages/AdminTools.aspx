<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="AdminTools.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.AdminTools"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <form id="PageForm" method="post" action="#" class="form-horizontal">

    <%= WebHelper.GetSelectRow("Log Back In As:", FormFields.LogInAsUserId, 5, GetLogInAsUserOptionsHtml(), WebHelper.GetButton("Log In As", "btnLogInAs")) %>

  </form>

  <form id="XeroContactForm" method="post" class="form-horizontal mt40">
    <h4 class="mt30">Edit Xero Contact Name</h4>
    <%= WebHelper.GetSelectRow("Xero Contact:", FormFields.XeroContact, 6, WebHelper.GetXeroContactOptions(false, XeroContactsList)) %>
    <%= WebHelper.GetTextInput("Xero Contact Name:", FormFields.XeroContactNewName, "", "", 2, 6, WebHelper.GetButton("Update", "btnUpdateXeroName")) %>
  </form>

  <form id="CompanyForm" method="post" action="#" class="form-horizontal">
    <h4 class="mt30">Client Company</h4>
    <%= WebHelper.GetSelectRow("User:", FormFields.UserIdForCompany, 3, GetUserOptionsForCompanyHtml(), WebHelper.GetTextDisplayRow("Current:", 3, "")) %>
    <%= WebHelper.GetSelectRow("Company:", FormFields.CompanyId, 3, GetCompanyOptionsHtml(), WebHelper.GetButton("Update Company", "btnUpdateCompany")) %>

  </form>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var $PageForm, $XeroContactForm;
      var $selXeroContact = $('select[name="<%= FormFields.XeroContact %>"]');
      var $inpXeroContactNewName = $('input[name="<%= FormFields.XeroContactNewName %>"]');
      var $selUser = $('select[name="<%= FormFields.UserIdForCompany %>"]');
      var $selCompany = $('select[name="<%= FormFields.CompanyId %>"]');

      $(document).ready(function() {

        $PageForm = $("#PageForm");
        $XeroContactForm = $("#XeroContactForm");

        $("#btnLogInAs").click(DoLogInAs);
        $selXeroContact.change(GetXeroContactName);
        $("#btnUpdateXeroName").click(CheckFields_XeroContactName);
        $("#btnUpdateCompany").click(UpdateClientCompany);

        var currenCompanyLabel = $("div.display-only");
        currenCompanyLabel.hide();

        $selUser.on('change', function () {
          var companyIdAttribute = $(this).find('option:selected').data('companyid');
          var $displayDiv = $('div.display-only').find('div');

          // Clear the current selection in $selCompany
          $selCompany.find('option:selected').prop('selected', false);

          if (companyIdAttribute === "<%= FormAttr.NotAssigned %>") {
            // If it's in the 'NotAssigned' option, hide 'Current' label
            currenCompanyLabel.hide();

          } else if (companyIdAttribute !== null && companyIdAttribute !== '') {
            // Set the new selected option in $selCompany and display the current company name
            $selCompany.find('option[value="' + companyIdAttribute + '"]').prop('selected', true).change();
            $displayDiv.text($selCompany.find('option:selected').text());
            currenCompanyLabel.show();
          } else {
            // Set to option with value 'NotAssigned'
            $selCompany.find('option[value="<%= FormAttr.NotAssigned %>"]').prop('selected', true).change();
            $displayDiv.text('Not assigned');
            currenCompanyLabel.show();
          }
        });

      }); // ready.

      function GetXeroContactName() {
        var xeroContactName = "";
        if ($selXeroContact.find("option:selected").val() != "") xeroContactName = $selXeroContact.find("option:selected").text();
        $inpXeroContactNewName.val(xeroContactName).change();
      }

      function CheckFields_XeroContactName() {
        var xeroContactNewName = $inpXeroContactNewName.val();
        var xeroContactCurrentName = $selXeroContact.find("option:selected").text();
        var xeroContactId = $selXeroContact.val();

        if (xeroContactId == "") {
          common_InfoDialog("Please select a Xero Contact.");
          return;
        } else if (xeroContactNewName == "") {
          common_InfoDialog("Please enter a new name for the Xero Contact.");
          return;
        } else if (xeroContactNewName == xeroContactCurrentName) {
          common_InfoDialog("The new name for the Xero Contact is the same as the current name.");
          return;
        }

        common_ConfirmDialog("Are you sure you want to update the Xero Contact Name from: '<b>" + xeroContactCurrentName + "</b>' to: '<b>" + xeroContactNewName + "'</b>",
          function (confirmed) {
            if (confirmed) UpdateXeroContactName();
          }
        );
      }

      function UpdateXeroContactName() {
        AjaxSubmit({
          form: $XeroContactForm,
          action: "<%= AjaxAction.UpdateXeroContact %>"
        });
      }

      function UpdateClientCompany() {
        var selCompanyValue = $selCompany.val();
        var selCompanySelectedId = $selUser.find("option:selected").data("companyid");

        if (selCompanyValue !== null && selCompanyValue !== "" && selCompanySelectedId !== "<%= FormAttr.NotAssigned %>") {
          AjaxSubmit({
            form: $('#CompanyForm'),
            action: "<%= AjaxAction.UpdateClientCompany %>"
          });
        }
      }

      function DoLogInAs() {

        var $selUser = $('select[name="<%= FormFields.LogInAsUserId %>"]');
        var userId = toDecimalInt($selUser.val(), 0);

        if (userId == 0) {
          common_InfoDialog("Select a user to log in as.");
          return;
        }
        AjaxSubmit({
          form: $PageForm,
          action: "<%= AjaxAction.LogInAsUser %>"
        });
      }

    })(jQuery);
  </script>

</asp:Content>
