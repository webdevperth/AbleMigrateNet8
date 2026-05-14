<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProgramSettings.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProgramSettings"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <div class="container-fluid form-horizontal">

    <form id="formSettings" method="post" action="#" onsubmit="return false" class="form-horizontal">

      <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="update" />
      <input type="hidden" name="<%= FormFields.ProgramJobId %>" value="<%= ProgramInfo.ProgramJobId %>" />
      <input type="hidden" name="<%= FormFields.ProgramJobGUID %>" value="<%= ProgramInfo.ProgramJobGUID.ToString() %>" />

      <% if (AddToProjectInfo == null) { %>
        <%= WebHelper.GetTextInput("Program/Job Number:", FormFields.ProgramJobNumber, ProgramInfo.ProgramJobNumber, 2, "", !CanUpdateJobNumber) %>
        <%= WebHelper.GetTextDisplayRow("Company:", 8, ProgramInfo.CompanyName) %>
      <% } else { %>
        <input type="hidden" name="<%= FormFields.AddToProjectId %>" value="<%= AddToProjectInfo.ProjectId %>" />
        <%= WebHelper.GetTextDisplayRow("Program/Job Number:", 8, AddToProjectInfo.JobNumber) %>
        <%= WebHelper.GetTextDisplayRow("Company:", 8, AddToProjectInfo.ClientCompanyName) %>
      <% } %>
      <%= WebHelper.GetTextInput("Program Name:", FormFields.ProgramJobName, ProgramInfo.ProgramJobName, 6, "", !CanEditSettings) %>

      <% if (CanViewSettingsDates) { %>
        <%= WebHelper.GetInputDateRow("Program Start:", "", ProgramInfo.ProgramStartDateUtc.UtcToTZOrNull(ConfigHelper.DefaultTimeZoneInfo), 2, 3, "(" + ConfigHelper.DefaultTimeZoneAbbrev + ")", !CanEditSettings) %>
        <%= WebHelper.GetInputDateRow("Program End:", "", ProgramInfo.ProgramEndDateUtc.UtcToTZOrNull(ConfigHelper.DefaultTimeZoneInfo), 2, 3, "(" + ConfigHelper.DefaultTimeZoneAbbrev + ")", !CanEditSettings) %>
      <% } %>

      <hr>
      <%= GetProgramStatusOptions("Program Status:") %>
      <hr>

      <%= GetPartnerDropdownHtml("Project Coordinator:", FormFields.ProjectCoordinatorUserId, 3, ProgramInfo.ProjectCoordinatorUserId, !CanEditSettings) %>
      <%= GetPartnerDropdownHtml("Lead Consultant:", FormFields.LeadConsultantUserId, 3, ProgramInfo.LeadConsultantUserId, !CanEditSettings) %>
      <%= GetPartnerDropdownHtml("Sales Partner:", FormFields.SalesPartnerUserId, 3, ProgramInfo.SalesPartnerUserId, !CanEditSettings) %>

      <% if (CanViewPercentages) { %>
        <%= WebHelper.GetPercentInput("Delivery Percentage:", FormFields.Partner_DeliveryPercentage, ProgramInfo.Partner_DeliveryPercentage, 2, 1, "", !CanEditPercentages) %>
        <%= WebHelper.GetPercentInput("Sales Delivery Percentage:", FormFields.Partner_SalesDeliveryPercentage, ProgramInfo.Partner_SalesDeliveryPercentage, 2, 1, "", !CanEditPercentages) %>
        <%= WebHelper.GetPercentInput("PLC Percentage:", FormFields.Partner_PLCPercentage, ProgramInfo.Partner_PLCPercentage, 2, 1, "", !CanEditPercentages) %>
      <% } %>

      <%= WebHelper.GetRichTextArea("Program Notes:", FormFields.ProgramNotes, 2, 8, ProgramInfo.ProgramNotes, "", !CanEditSettings) %>

      <%= WebHelper.GetRichTextArea("Booking Page Instructions:", FormFields.BookingPageInstructions, 2, 8, ProgramInfo.BookingPageInstructions, "", !CanEditSettings) %>

      <% if (CanEditSettings) { %>
        <div class="btnholder">
          <button type="button" class="btn btn-primary btnUpdate floatright" id="btnUpdate"><%= IsNewProgram ? "Create" : "Update" %> Program</button>
          <% if (!IsNewProgram) { %>
            <button type="button" class="btn btn-warning btnDelete floatleft" <%= DisableDelete ? (" title=\"" + DisableDeleteMsg + "\"") : "" %> id="btnDelete">Delete Program</button>
          <% } %>
        </div>
      <% } %>

    </form>

  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var btnUpdate, formSettings, isNewProgram, disableDelete

      $(document).ready(function() {

        btnUpdate = $("#btnUpdate");
        btnDelete = $("#btnDelete");
        formSettings = $("#formSettings");
        isNewProgram = <%= IsNewProgram ? "true" : "false" %>;
        disableDelete = <%= DisableDelete ? "true" : "false" %>;

        btnUpdate.click(UpdateProgram);
        btnDelete.click(DeleteProgram);

        if (isNewProgram) formSettings.find("input:text:not(:disabled):first").trigger("focus");

      }); // ready.

      function DeleteProgram() {

        if (disableDelete) {
          common_InfoDialog("<%= DisableDeleteMsg %>");
          return;
        }

        if (!confirm("Delete this Program?")) return;

        AjaxSubmit({
          form: formSettings,
          action: "delete"
        });
      }

      function UpdateProgram() {

        AjaxSubmit({
          form: formSettings,
          action: "update"
        });
      }

    })(jQuery);
  </script>

</asp:Content>
