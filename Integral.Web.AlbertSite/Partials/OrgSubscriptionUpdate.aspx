<%@ Page Language="C#" AutoEventWireup="true" CodeFile="OrgSubscriptionUpdate.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.OrgSubscriptionUpdate" %>

<%@ Import Namespace="Integral.Web" %>

<div class="container-fluid form-horizontal" id="OrgSubscriptionUpdate_Content">

  <% if (PageMode == PageModes.SelectQuantity) { %>

    <style>
      .integer-input {
        display: inline-flex; align-items: center; border: 1px solid #ccc; border-radius: 5px; overflow: hidden; width: 120px;
      }
      .integer-input button {
        width: 30px; height: 30px; border: none; background-color: #f0f0f0; cursor: pointer; font-size: 18px;
      }
      .integer-input input {
        width: 60px; text-align: center; border: none; font-size: 16px;
      }
      .integer-input button:active {
        background-color: #ddd;
      }
    </style>

    <div class="uiSubscriptionCount flex-column flex-align-center minw400">

      <h4 class="mt0">Select Subscription Quanity</h4>
      <h4 class="mt0 mb0"><%= ViewModel.SelectedSubscription.SubscriptionName.HTMLEncode() %></h4>
      <h5 class="mt0 mb0 font-bold"><%= ViewModel.SelectedSubscription.PricePerUserPerMonth.ToString("C") %> / user / month</h5>
      <p class="">Currently Assigned Seats: <%= ViewModel.SelectedSubscription.AssignedSeats %></p>
      <p class="mb20">Purchased Seats: <%= ViewModel.SelectedSubscription.TotalSeats %></p>
      <div class="integer-input">
        <button class="decrement">−</button>
        <input type="text" value="<%= ViewModel.SelectedQuantity %>" />
        <button class="increment">+</button>
      </div>

      <div class="mt20 flex flex-space-between w100p">
        <button class="btn btn-secondary btnCancel">Cancel</button>
        <button class="btn btn-primary btnUpdate">Update</button>
      </div>

    </div>

    <script>
      (function ($) {

        $(document).ready(function () {

          var assignedSubscriptions = <%= ViewModel.SelectedSubscription.AssignedSeats %>;
          var $input = $('.uiSubscriptionCount input');
          var $btnIncrement = $('.uiSubscriptionCount .increment');
          var $btnDecrement = $('.uiSubscriptionCount .decrement');

          $btnDecrement.click(function () { AdjustValue(-1); });
          $btnIncrement.click(function () { AdjustValue(1); });

          $input.on('input', function () {
            $input.val($input.val().replace(/[^0-9]/g, ''));
          });

          $(".uiSubscriptionCount .btnCancel").click(function (evt) {
            common_CloseDialog(evt.target);
          });

          $(".uiSubscriptionCount .btnUpdate").click(function (evt) {
            PostUpdate(evt);
          });

          function AdjustValue(adjustBy) {
            let value = toInt($input.val(), 0);
            value = value + adjustBy;
            if (value < assignedSubscriptions) value = assignedSubscriptions;
            $input.val(value);
          }

          function PostUpdate(evt) {

            var selectedQuantity = toInt($input.val(), 0);

            AjaxSubmit({
              url: "<%= Request.RawUrl.JSONEncode() %>",
              action: "<%= AjaxAction.UpdateQuantity %>",
              data: {
                "<%= FormFields.Quantity %>": selectedQuantity
              },
              onSuccess: function (jqXHR, data) {
                if (data["<%= ReturnValues.PageMode %>"] == "<%= PageModes.ShowPaymentMethod %>") {
                  ShowPaymentMethod(selectedQuantity);
                }
              },
              onFail: function (jqXHR, data) { },
              onError: function (jqXHR, textStatus, errorThrown) { },
              onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
            });

          }

          function ShowPaymentMethod(selectedQuantity) {

            if (selectedQuantity < 1) { // Shouldn't occur.
              alert("Invalid quantity, please reload page and try again.");
              return;
            }

            const partialUrl = AbleJS.Util.PatchQuery({
              url: "<%= Request.RawUrl.HTMLEncode() %>",
              params: {
                "<%= PathHelper.AbleUrlKeys.PageMode %>": "<%= PageModes.ShowPaymentMethod.ToString() %>",
                "<%= PathHelper.AbleUrlKeys.SubscriptionGuid %>": "<%= ViewModel.SelectedSubscription.SubscriptionGuid.ToStringNoBraces() %>",
                "<%= PathHelper.AbleUrlKeys.SubscriptionQty %>": selectedQuantity
              }
            });

            // Show payment method dialog over top of qty dialog.
            common_ShowPartialModal({
              modalTitle: "Enter Payment Details",
              //customClass: "minw500",
              buttons: {},
              partialUrl: partialUrl
            });
          }

        });
      })(jQuery);
    </script>

  <% } else if (PageMode == PageModes.ShowPaymentMethod) { %>

    <form id="payment-form">
      <div id="payment-element"></div>
      <div align="right"><button id="submit" class="btn btn-primary mt20">Submit</button></div>
      <div id="error-message"></div>
    </form>

    <script>
      (async function ($) {

        const form = document.getElementById('payment-form');

        const stripe = await AbleJS.Stripe.GetStripe('<%= ConfigHelper.Stripe.ClientId %>');

        const options = {
          clientSecret: '<%= StripeClientSecret %>',
          appearance: {/*...*/ },
        };
        const elements = stripe.elements(options);
        const paymentElementOptions = { layout: 'accordion' };
        const paymentElement = elements.create('payment', paymentElementOptions);

        paymentElement.mount('#payment-element');

        form.addEventListener('submit', async (event) => {

          event.preventDefault();

          const { error } = await stripe.confirmSetup({
            elements,
            confirmParams: {
              // After payment method confirmed, redirect back to showing quantity picker.
              return_url: "<%= PathHelper.Pages.Settings.BillingsReturnUrl(ViewModel.SelectedSubscription.SubscriptionGuid, ViewModel.SelectedQuantity) %>",
            }
          });

          if (error) {
            // There was an error confiming the payment method.
            const messageContainer = document.querySelector('#error-message');
            messageContainer.textContent = error.message;
          }
        });

      })(jQuery);
    </script>

  <% } %>

</div>
