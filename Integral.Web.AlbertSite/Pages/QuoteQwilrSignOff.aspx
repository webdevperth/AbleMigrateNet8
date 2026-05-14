<%@ Page Language="C#" AutoEventWireup="true" CodeFile="QuoteQwilrSignOff.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.QuoteQwilrSignOff" MasterPageFile="~/MasterPages/Public.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="crossorigin">
  <link href="https://fonts.googleapis.com/css2?family=Open+Sans:ital,wght@0,400;0,600;0,700;1,400&display=swap" rel="stylesheet">

  <div class="heading">
    <h2><%= QuoteInfo.QuoteTitle.HTMLEncode() %></h2>
    <p><%= WebHelper.GetLink(new WebHelper.LinkInfo(PathHelper.Pages.QuotePublicPDF(QuoteInfo.PublicGuid), "Download PDF", true) { ClickDisableSeconds = 10, ClickSpinnerSeconds = 10 }) %></p>
  </div>

  <% if (FormVisible) { %>

    <form id="signform" class="form-horizontal" method="post" action="<%= PathHelper.Pages.QuoteQwilrSignOff(QuoteInfo.PublicGuid) %>" onsubmit="return false">

      <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="complete" />
      <input type="hidden" name="quoteitemid" value="<%= QuoteItemIds %>" />

      <h3>Contacts</h3>

      <div class="container">
        <div class="row">
          <div class="col"><input type="text" autocomplete="off" class="form-control" name="ClientFirstName" value="<%= ClientFirstName %>" placeholder="Client First Name" /></div>
          <div class="col">&nbsp;</div>
          <div class="col"><input type="text" autocomplete="off" class="form-control" name="ClientLastName" value="<%= ClientLastName %>" placeholder="Client Last Name" /></div>
        </div>
        <div class="row"><div class="col"></div></div>
        <div class="row">
          <div class="col-3"><input autocomplete="off" type="text" class="form-control" name="ClientEmail" value="<%= ClientEmailAddress %>" placeholder="Client Email" /></div>
        </div>
        <div class="row"><div class="col"></div></div>
        <div class="row">
          <div class="col"><input type="text" autocomplete="off" class="form-control" name="AccFirstName" value="" placeholder="Accounts Payable First Name" /></div>
          <div class="col">&nbsp;</div>
          <div><input type="text" autocomplete="off" class="form-control" name="AccLastName" value="" placeholder="Accounts Payable Last Name" /></div>
        </div>
        <div class="row"><div class="col"></div></div>
        <div class="row">
          <div class="col-3"><input autocomplete="off" type="text" class="form-control" name="AccEmail" value="" placeholder="Accounts Payable Email" /></div>
        </div>
      </div>

      <div class="totalbox">
        Total Amount: <%= TotalAmount.ToString("C") %>
      </div>

      <%= WebHelper.CustomCheckBox("agree", "1", false, "", "I have read and agreed on the " + WebHelper.GetSimpleLink(ConfigHelper.ExternalUrls.AbleTermsAndConditionsUrl, "Terms and Conditions", true) + ".") %>

      <button type="button" id="btnSign" class="btn btn-primary">Sign Proposal</button>

    </form>

    <div id="thankyou" style="display:none">
      <h1>Proposal Accepted, Thank You!</h1>
    </div>

  <% } %>

  <% if (AcceptedVisible) { %>

    <center>
      <h1>Proposal Accepted</h1>
      <p>This proposal was accepted on <%= WebHelper.DisplayDate(QuoteInfo.ClientAcceptedUtc.UtcToTZOrNull()) %>.</p>
    </center>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var $btnSign = $("#btnSign");
      $(document).ready(function () {

        $("input[name=ClientFirstName]").focus();
        $("#btnSign").click(clickSign);

      });

      function clickSign(ev) {

        AjaxSubmit({
          form: $("#signform"),
          onSuccess: function (jqXHR, returnData) {
            ShowThankYou();
          },
          onFail: function (jqXHR, returnData) {
          },
          onAlways: function () {
          }
        });
      }

      function ShowThankYou() {

        if (!app_isDev) $("form").hide();
        $("#thankyou").show();
      }

    })(jQuery);
  </script>

</asp:Content>

