<%@ Page Language="C#" AutoEventWireup="true" CodeFile="QuotePublicPDF.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.QuotePublicPDF" MasterPageFile="~/MasterPages/Public.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Open+Sans:ital,wght@0,400;0,600;0,700;1,400&display=swap" rel="stylesheet">

  <style>
    .content { padding: 25px 50px; }

    @media only screen and (max-width: 480px) {
      .content { padding: 25px 20px; }
    }
  </style>

  <div class="quoteView">
    <div class="heading heading-pdf w100p">
      <div class="logo"><img src="<%= PathHelper.Images.TenantOrgLogo(QuoteInfo, true) %>" /></div>
      <div class="title">
        <h2><%= QuoteInfo.QuoteTitle.HTMLEncode() %></h2>
        <p>Job Number: <%= QuoteInfo.JobNumber %></p>
      </div>
    </div>
  </div>

  <section class="signform content quoteView">

    <% if (Panel_Overview_Visible) { %> %>
      <div class="tab-panel" id="Panel_Overview">

        <div class="quote-overview-box">
          <%= QuoteInfo.CoverLetterHtml %>
        </div>

      </div>
    <% } %>

    <% if (Panel_Team_Visible) { %>
      <div class="tab-panel" id="Panel_Team">

        <h3>Your Project Team</h3>
        <p>Your project team includes a Program Lead Consultant, a Program Coodinator and the program delivery team.</p>
        <%= GetTeamHtml() %>

      </div>
    <% } %>

    <% if (Panel_Costing_Visible) { %>
      <div class="tab-panel" id="Panel_Costing">

        <h4 class="mt20">Quote Components</h4>

        <table class="tblItems w100p">
          <thead>
            <tr>
              <th class="w125">Type</th>
              <th>Description</th>
              <th class="w75 align-center">Option</th>
              <th class="w125 pl0 pr0 align-center">Qty</th>
              <th class="w100 align-right pl0 pr0">$ / Unit</th>
              <th class="w125 align-right">Total Price</th>
            </tr>
          </thead>
          <tbody>
            <% foreach (var QuoteItem in QuoteInfo.QuoteItems) { %>
              <tr class="item-row" data-item="<%= QuoteItem.QuoteItemId %>">
                <td class="item-td-cat"><%= QuoteItem.CategoryName %></td>
                <td class="item-td-des"><%= QuoteItem.ItemDescription %></td>
                <td class="item-td-opt align-center pl0 pr0"><%= GetOptional(QuoteItem) %></td>
                <td class="item-td-qty align-center pl0 pr0"><%= (QuoteItem.Quantity == null ? "" : (((decimal)QuoteItem.Quantity).ToString("0.##")) + QuoteItem.QuantityDescr.HTMLEncode().EnsureStartsWith(" ", true)) %></td>
                <td class="item-td-amt align-right pl0 pr0"><%= (QuoteItem.UnitPrice == null ? "" : ((decimal)QuoteItem.UnitPrice).ToString("C")) %></td>
                <td class="item-td-tot align-right item-linetotal" data-amount="<%= QuoteItem.UnitPrice == null || QuoteItem.Quantity == null ? "" : (QuoteItem.UnitPrice.GetValueOrDefault(0) * QuoteItem.Quantity.GetValueOrDefault(0)).ToString() %>"><%= QuoteItem.UnitPrice == null || QuoteItem.Quantity == null ? "" : (QuoteItem.UnitPrice.GetValueOrDefault(0) * QuoteItem.Quantity.GetValueOrDefault(0)).ToString("C") %></td>
              </tr>
            <% } %>
          </tbody>
          <tfoot>
            <tr>
              <td colspan="5" class="total-title">Sub-Total</td>
              <td class="total-amount total-amount-no-gst"><%= QuoteTotalExGST.ToString("C") %></td>
            </tr>
            <tr>
              <td colspan="5" class="total-title">GST</td>
              <td class="total-amount total-amount-gst"><%= QuoteTotalGST.ToString("C") %></td>
            </tr>
            <tr>
              <td colspan="5" class="total-title">Total</td>
              <td class="total-amount total-amount-inc-gst"><%= QuoteTotalIncGST.ToString("C") %></td>
            </tr>
          </tfoot>
        </table>

      </div>
    <% } %>

    <% if (Panel_Contact_Visible) { %>
      <div class="tab-panel" id="Panel_Contact">

        <div class="signformbox">

          <h4>Main Contact</h4>

          <div class="container w100p mb20">
            <div class="row-info">
              <div class="input-text-dual">
                <input type="text" readonly autocomplete="off" class="form-control" name="ClientFirstName" value="<%= ClientFirstName %>" placeholder="Client First Name" />
                <input type="text" readonly autocomplete="off" class="form-control" name="ClientLastName" value="<%= ClientLastName %>" placeholder="Client Last Name" />
              </div>
            </div>
            <div class="row-info">
                <input readonly autocomplete="off" type="text" class="form-control" name="ClientEmail" value="<%= ClientEmailAddress %>" placeholder="Client Email" />
            </div>
          </div>

          <h4>Accounts Payable Contact</h4>

          <div class="container" width="100%">
            <div class="row-info">
              <div class="input-text-dual">
                <input type="text" autocomplete="off" class="form-control" name="AccFirstName" value="<%= QuoteInfo.AccPayFirstName %>" />
                <input type="text" autocomplete="off" class="form-control" name="AccLastName" value="<%= QuoteInfo.AccPayLastName %>" />
              </div>
            </div>
            <div class="row-info">
                <input autocomplete="off" type="text" class="form-control" name="AccEmail" value="<%= QuoteInfo.AccPayEmailAddress %>" />
            </div>
          </div>

          <div class="total-amount mt20">
            Total Amount: <span class="total-amount-inc-gst amount"><%= (QuoteGSTApplicable ? QuoteTotalIncGST : QuoteTotalExGST).ToString("C") %></span> <small><%= QuoteGSTApplicable ? "inc" : "ex" %> GST</small>
          </div>

          <% if (!QuoteInfo.IsAccepted) { %>
            <h4 class="mt20">Please see our <%= WebHelper.GetSimpleLink(ConfigHelper.ExternalUrls.AbleTermsAndConditionsUrl, "Terms and Conditions", true) %>.</h4>
          <% } %>

          <% if (QuoteInfo.IsAccepted) { %>
            <h4 class="mt20">Proposal accepted on: <%= WebHelper.DisplayDate(QuoteInfo.ClientAcceptedUtc.UtcToTZOrNull()) %>.</h4>
          <% } %>

        </div>

      </div>
    <% } %>

  </section>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      $(document).ready(function () {

      });

    })(jQuery);
  </script>

</asp:Content>

