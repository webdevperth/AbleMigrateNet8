<%@ Page Language="C#" AutoEventWireup="true" CodeFile="StripePaymentMethod.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.StripePaymentMethod" %>

<%@ Import Namespace="Integral.Web" %>

<div class="container-fluid form-horizontal">

  <form id="stripe-payment-form">
    <div id="stripe-payment-element"></div>
    <div align="right"><button id="stripe-payment-submit" class="btn btn-primary mt20">Submit</button></div>
    <div id="stripe-error-message"></div>
  </form>

  <%--
    Note to minimise delay loading the stripe.js library, the
    host page can call AbleJS.Stripe.GetStripe() (no await, ignore return)
    which serves to pre-load stripe.js before showing this UI.
    It is completely optional, doesn't affect function.
  --%>

  <script>
    (async function ($) {

      const stripeForm = document.getElementById('stripe-payment-form');

      // If stripe redirects back here that's the end of the flow, so close this dialog.
      const searchParams = new URLSearchParams(window.location.search);
      if (searchParams.has('<%= UrlKeys.Complete %>')) {
        common_CloseDialog($(stripeForm));
        return;
      }

      const stripeCustomerDefaultPaymentMethodId = '<%= StripeCustomerDefaultPaymentMethodId %>';

      if (!isStringNullOrEmpty(stripeCustomerDefaultPaymentMethodId)) {
        // Payment method already exists, close the dialog.
        // (host will respond to dialog close and recheck payment method)
        common_CloseDialog($(stripeForm));
        return;
      }

      const stripe = await AbleJS.Stripe.GetStripe('<%= ConfigHelper.Stripe.ClientId %>');

      const elements = stripe.elements({
        clientSecret: '<%= StripeClientSecret %>',
        appearance: {/*...*/ },
      });

      const paymentElement = elements.create('payment', {
        layout: 'tabs'
      });

      paymentElement.mount('#stripe-payment-element');

      stripeForm.addEventListener('submit', async (event) => {

        event.preventDefault();

        const { error, setupIntent } = await stripe.confirmSetup({
          elements,
          confirmParams: {
            return_url: AbleJS.Util.PatchQuery({
              url: location.href,
              params: { "<%= UrlKeys.Complete %>": true }
            })
          },
          redirect: 'if_required'
        });

        if (error) {
          // There was an error confiming the payment method.
          const messageContainer = document.querySelector('#stripe-error-message');
          messageContainer.textContent = "Error: " + error.message;
          return;
        }

        common_CloseDialog($(stripeForm));
      });

    })(jQuery);
  </script>

</div>
