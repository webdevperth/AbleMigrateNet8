<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Settings_Billings.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.Settings_Billings" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.css">
  <script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.5.12/cropper.min.js"></script>

  <style>
    table.tblBillings > thead > tr > th { font-size: 1.1em; }
    .td-plan { font-size: 1.1em; }
  </style>

  <div class="table-title type-coachees">Usage</div>

  <style>
    .tblBillings > tbody > tr:last-child > td {
      border-bottom: 1px solid #cdcdcd;
    }
  </style>

  <div class="table-responsive">
    <table class="tblBillings table table-bordered">
      <thead>
        <tr>
          <th class="">Plan</th>
          <th class="">Seats</th>
          <th class="">Type</th>
          <th class="">Subscription</th>
          <th class="">Total</th>
          <th class="">Edit</th>
        </tr>
      </thead>
      <tbody>
        <% int totalAssigned = 0, totalOrgSubs = 0; %>
        <% decimal totalPricePerMonth = 0; %>
        <% foreach (var sub in OrgSubscriptions) { %>
          <tr height="70">
            <% totalAssigned += sub.AssignedSeats; %>
            <% totalOrgSubs += sub.PricePerUserPerMonth == 0 ? sub.AssignedSeats : sub.TotalSeats; %>
            <% if (sub.PricePerUserPerMonth > 0) totalPricePerMonth += sub.PricePerUserPerMonth * sub.TotalSeats; %>
            <td class="td-plan font-weight-bold"><%= sub.SubscriptionName.HTMLEncode() %></td>
            <td class=""><%= sub.AssignedSeats %> / <%= sub.PricePerUserPerMonth == 0 ? "unlimited" : sub.TotalSeats.ToString() %></td>
            <td class="">Learner</td>
            <td class=""><%= sub.PricePerUserPerMonth.ToString("C") %> / user / month</td>
            <td class="font-weight-bold"><%= (sub.PricePerUserPerMonth * sub.TotalSeats).ToString("C") %></td>
            <td class="">
              <%
                if (sub.PricePerUserPerMonth > 0) { // Don't show the edit button for free subscriptions as there is nothing to purchase.
                  Response.Write(WebHelper.GetActionButton(
                                   WebHelper.ActionButtonTypeEnum.edit,
                                   "btnEditSubscription",
                                   "Edit Subscription",
                                   new WebHelper.DataAttributes(
                                     (DataAttr.SubscriptionGuid, sub.SubscriptionGuid.ToStringNoBraces()),
                                     (DataAttr.SubscriptionQuantity, sub.TotalSeats.ToString())
                                   )
                                ));
                }
              %>
            </td>
          </tr>
        <% } %>
      </tbody>
      <tfoot>
        <tr>
          <td>&nbsp;</td>
          <td>&nbsp;</td>
          <td>&nbsp;</td>
          <td align="right" class="font-weight-bold">Total Monthly:</td>
          <td class="font-weight-bold"><%= totalPricePerMonth.ToString("C") %></td>
          <td></td>
        </tr>
      </tfoot>
    </table>
  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      AbleJS.Stripe.GetStripe(); // Begin loading stripe if not already.

      var selectedSubscriptionGuid = "<%= SelectedSubscription?.SubscriptionGuid.ToStringNoBraces() %>";
      var selectedQuantity = <%= SelectedQuantity ?? 0 %>;

      $(document).ready(function () {

        $(".btnEditSubscription").click(function (evt) {
          let selectedSubscriptionGuid = $(evt.currentTarget).data("<%= DataAttr.SubscriptionGuid %>");
          let selectedQuantity = $(evt.currentTarget).data("<%= DataAttr.SubscriptionQuantity %>");
          EditSubscription(selectedSubscriptionGuid, selectedQuantity);
        });

        <% if (SelectedSubscription != null) { %>
          let selectedSubscriptionGuid = "<%= SelectedSubscription.SubscriptionGuid.ToStringNoBraces() %>";
          let selectedQuantity = <%= SelectedQuantity ?? 0 %>;
          EditSubscription(selectedSubscriptionGuid, selectedQuantity);
        <% } %>

      });

      function EditSubscription(selectedSubscriptionGuid, selectedQuantity) {

        const partialUrl = AbleJS.Util.PatchQuery({
          url: "<%= PathHelper.Partials.OrgSubscriptionUpdate(Guid.Empty) %>",
          params: {
            "<%= PathHelper.AbleUrlKeys.SubscriptionGuid %>": selectedSubscriptionGuid,
            "<%= PathHelper.AbleUrlKeys.SubscriptionQty %>": selectedQuantity
          }
        });

        common_ShowPartialModal({
          modalTitle: "Edit Subscription",
          widthFitContent: true,
          buttons: {},
          partialUrl: partialUrl
        });
      }

    })(jQuery);
  </script>

</asp:Content>
