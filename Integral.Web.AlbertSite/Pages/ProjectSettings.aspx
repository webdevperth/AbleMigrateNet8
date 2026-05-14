<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProjectSettings.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProjectSettings"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <div class="container-fluid form-horizontal">

    <form id="formSettings" method="post" action="#" onsubmit="return false" class="form-horizontal">

      <% if (CanChangeTenantOrg) { %>
        <%= WebHelper.GetSelectRow("Organisation:", FormFields.TenantOrgId, 5, GetTenantOrgOptions(), "", isReadOnly) %>
      <% } %>

      <% if (CanChangeProjectCompany) { %>
        <%= WebHelper.GetSelectRow("Customer Company:", FormFields.CompanyId, 5,
          @"<option value="""">[Select or add Company]</option>"
        + @"<option value=""new"">[Add New Company]</option>"
        + GetCompanyOptions(), "", isReadOnly) %>

        <div id="newCompanyInfo" class="add-new-fields displaynone">
          <%= WebHelper.GetTextInput("New Company Name:", FormFields.NewCompanyName, "", 5) %>
        </div>
      <% } %>

      <% if (!IsNewProject) { %>
        <%= WebHelper.GetTextInput("Project/Job Number:", FormFields.JobNumber, ProjectInfo.JobNumber, 3, "", true) %>
      <% } %>

      <%= WebHelper.GetTextInput("Project Title:", FormFields.ProjectName, ProjectInfo.ProjectName, 5, "", isReadOnly) %>

      <% if (!IsNewProject && CanEditInvoiceTypeId) { %>

        <%= WebHelper.GetSelectRow("Invoice Type:", FormFields.InvoiceType, 5, GetInvoiceTypeOptions(), "", isReadOnly) %>

        <div class="invoiceSettings">

          <%= WebHelper.GetPercentInput("Cost Item Markup Percentage:", FormFields.DefaultCostItemMarkupPercent, GetMarkupCostPercentage(), 2, 2, 3, "", !CanUpdateDefaultCostItemMarkupPercent) %>

          <%= WebHelper.CustomCheckBoxRow("Allow Cost Item Manual Overwrite:", FormFields.AllowCostItemUnitPriceManualOverwrite, "1", ProjectInfo.AllowCostItemUnitPriceManualOverwrite, !CanAllowCostItemPriceOverwrite, "") %>

          <%= WebHelper.GetSelectRow("Xero Client:", FormFields.XeroContactId, 5, GetXeroContactOptions(), "", isReadOnly) %>

          <%= WebHelper.CustomCheckBoxRow("Purchase Order Required:", FormFields.PurchaseOrderRequired, "1", ProjectInfo.PurchaseOrderRequired, isReadOnly, "") %>

          <%= WebHelper.GetTextInput("Purchase Order Number:", FormFields.InvoiceNumber, ProjectInfo.PurchaseOrderNumber, 5, "", isReadOnly) %>

          <% if (CanEditXeroAccountCode) { %>
            <%= WebHelper.GetSelectRow("Account:", FormFields.XeroAccountCode, 5, GetXeroAccountOptions(), "", isReadOnly) %>
          <% }%>

          <%= WebHelper.GetRichTextArea("Invoice Notes:", FormFields.InvoicingNotes, 2, 7, ProjectInfo.InvoicingNotes, "", isReadOnly) %>

        </div>

      <% } %>

      <%= WebHelper.GetRichTextArea("Intent of Project:", FormFields.ProjectIntent, 2, 7, ProjectInfo.ProjectIntent, "", isReadOnly) %>

      <%= WebHelper.GetRichTextArea("Program Context:", FormFields.ProgramContext, 2, 7, ProjectInfo.ProgramContext, "", isReadOnly) %>

      <% if (!isReadOnly) { %>
        <div class="btnholder">
          <button type="button" class="btn btn-primary btnUpdate floatright" id="btnUpdate"><%= IsNewProject ? "Create" : "Update" %> Project</button>
        </div>
      <% } %>

    </form>

  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var btnUpdate, formSettings, isNewProject
      var selCompanyId, newCompanyInfo, selInvoiceType;

      $(document).ready(function() {

        selCompanyId = $('select[name="<%= FormFields.CompanyId %>"]');
        selInvoiceType = $('select[name="<%= FormFields.InvoiceType %>"]');
        newCompanyInfo = $("#newCompanyInfo");
        btnUpdate = $("#btnUpdate");
        formSettings = $("#formSettings");
        isNewProject = <%= IsNewProject ? "true" : "false" %>;

        // Company dropdown.
        selCompanyId.change(function(e) { ChangeCompany(); });
        ChangeCompany();

        selInvoiceType.change(UpdateInvoiceLayout);
        UpdateInvoiceLayout();

        btnUpdate.click(UpdateProject);

        if (isNewProject) formSettings.find("input:text:not(:disabled):first").trigger("focus");

      }); // ready.

      function UpdateInvoiceLayout() {
        var invoiceTypeId = selInvoiceType.val();

        if (invoiceTypeId == <%= ConfigHelper.InvoiceInstructionTypeId_NoTransaction %>) {

          $('.invoiceSettings').hide();

        } else {

          $('.invoiceSettings').show();
        }
      }

      function ChangeCompany() {
        var cid = selCompanyId.val();
        if (cid == "new") newCompanyInfo.slideDown(300, function() { formSettings[0]["<%= FormFields.NewCompanyName %>"].focus(); });
        else newCompanyInfo.slideUp(300);
        cid = parseInt(cid, 10) || 0;
      }

      function UpdateProject(options) {

        AjaxSubmit({
          form: formSettings,
          action: "<%= AjaxAction.Update %>"
        });
      }

    })(jQuery);
  </script>

</asp:Content>
