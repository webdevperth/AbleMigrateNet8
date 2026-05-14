<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="PartnerReferrals.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.PartnerReferrals"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <table class="tbl-referrals table table-bordered">
    <thead>
      <tr>
        <th class="minw300 nowrap">Referral</th>
        <th class="type-date">Invited</th>
        <th class="minw100 align-center">Accepted</th>
      </tr>
    </thead>
    <tbody>
      <% if (ReferralList.Count == 0) { %>
        <tr><td colspan="3">No referrals yet.</td>
      <% } %>
      <% foreach (var referral in ReferralList) { %>
        <tr class="">
          <td class="nowrap">
            <div class="referral-name"><%= (referral.InviteeFirstName + " " + referral.InviteeLastName).HTMLEncode() %></div>
            <div class="referral-email"><%= referral.InviteeEmail.HTMLEncode() %></div>
          </td>
          <td class="type-date"><%= WebHelper.DisplayDate(referral.CreatedUtc.UtcToTZ(null)) %></td>
          <td class="align-center"><%= referral.InviteAcceptedUtc == null ? "" : "Yes" %></td>
        </tr>
      <% } %>
    </tbody>
  </table>

  <div class="table-title">Add Referral</div>

  <form id="formAdd" method="post" action="<%= Request.RawUrl %>" onsubmit="return false;">

    <table class="tbl-form table borderless width-auto align-top">
      <tr class="ajaxSubmit-field">
        <td class="w125 align-right pt15">First Name:</td>
        <td class="w400"><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.FirstName, NoRow = true }) %>
          <div class="<%= WebHelper.CSSClasses.AjaxFieldErrorMsg %>"></div>
        </td>
      </tr>
      <tr class="ajaxSubmit-field">
        <td class="align-right pt15">Last Name:</td>
        <td><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.LastName, NoRow = true }) %>
          <div class="<%= WebHelper.CSSClasses.AjaxFieldErrorMsg %>"></div>
        </td>
      </tr>
      <tr class="ajaxSubmit-field">
        <td class="align-right pt15">Email:</td>
        <td><%= WebHelper.GetTextInput(new WebHelper.TextInputSettings() { InputName = FormFields.EmailAddress, NoRow = true }) %>
          <div class="<%= WebHelper.CSSClasses.AjaxFieldErrorMsg %>"></div>
        </td>
      </tr>
      <tr>
        <td></td>
        <td><button class="btn btn-primary floatright" id="btnAdd">Send Referral</button></td>
      </tr>
    </table>

  </form>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var $formAdd = $("#formAdd");
      var $btnAdd = $("#btnAdd");

      $(document).ready(function() {

        $btnAdd.click(AddReferral);

      }); // ready.

      function AddReferral(evt) {
        AjaxSubmit({
          form: $formAdd,
          action: "<%= AjaxAction.AddReferral %>"
        });
      }

    })(jQuery);
  </script>

</asp:Content>
