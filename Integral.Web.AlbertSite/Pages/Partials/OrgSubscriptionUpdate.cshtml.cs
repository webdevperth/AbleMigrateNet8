using System;
using System.Collections.Generic;
using Integral.Integrations;
using Microsoft.AspNetCore.Mvc;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public class OrgSubscriptionUpdate : AppCode.PageBaseClasses.SettingsPageBase {

    public class ViewModel_ {
      public DbHelper.Subscriptions.Org.OrgSubscriptionItem SelectedSubscription;
      public int SelectedQuantity;
    }

    public ViewModel_ ViewModel = new ViewModel_();

    public class AjaxAction {
      public const string StripeCheckout = "StripeCheckout";
      public const string UpdateQuantity = "UpdateQuantity";
    }

    public class UrlKeys {
      public const string Quantity = "qty";
    }

    public class FormFields {
      public const string Quantity = "qty";
    }

    public class ReturnValues {
      public const string PageMode = "PageMode";
      public const string SelectedSubscriptionGuid = "subguid";
      public const string SelectedQuantity = "qty";
      public const string ClientSecret = "ClientSecret";
    }

    public enum PageModes { None, SelectQuantity, ShowPaymentMethod }

    public PageModes PageMode = PageModes.None;
    public string StripeClientSecret;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      // Page mode can be passed in url.
      // Default mode is to show quantity selection.
      if (!Enum.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PageMode), out PageMode)) {
        PageMode = PageModes.SelectQuantity;
      }

      // In all page modes, get al_subscription guid from url.
      if (!Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SubscriptionGuid), out Guid selectedSubscriptionGuid)) {
        RespondMessageAndEnd($"Invalid Subscription ID, please reload page and try again.");
        return new EmptyResult();
      }
      // Selected quanity is passed in url in some cases, e.g. when coming back from a redirect after adding a payment method.
      var selectedQuantity = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.SubscriptionQty) ?? 0;

      // Validate given subscription guid against those available for selection.
      var orgSubscriptions = DbHelper.Subscriptions.Org.GetOrgSubscriptionItems(SessionHelper.UserInfo.OrgId);
      var selectedSubscription = orgSubscriptions.Find(item => item.SubscriptionGuid == selectedSubscriptionGuid);

      if (selectedSubscription == null) {
        RespondMessageAndEnd($"Incorrect Subscription ID, please reload page and try again.");
        return new EmptyResult();
      }

      ViewModel.SelectedSubscription = selectedSubscription;
      ViewModel.SelectedQuantity = selectedQuantity;

      // Post submits quantity selection.
      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          if (ajax.Action == AjaxAction.UpdateQuantity) {
            UpdateQuantity(ajax, selectedSubscription);
            return;
          }

        });

        return new EmptyResult();
      }

      // After submitting quantity, the return action can be to show another modal with stripe payment method selection.
      if (PageMode == PageModes.ShowPaymentMethod) {

        // Get stripe "client secret" for front end.
        StripeClientSecret = StripeService.GetClientSecretForPaymentMethod(TenantOrgInfo.StripeCustomerId);
      }

      return Page();
    }

    // User has submitted a quantity for the subscription.
    // If the quanity is acceptable, then before proceeding we must:
    // 1. Ensure the Org is attached to a Stripe customer entity,
    // 2. Ensure the Org is also attached to a Stripe subscription entity.
    // 3. If selected sub is not free, remember the sub guid & quantity for next step and show the stripe payment method selector.
    private void UpdateQuantity(AjaxSubmitHelper ajax, DbHelper.Subscriptions.Org.OrgSubscriptionItem selectedSubscriptionItem) {

      var selectedQuantity = ajax.CheckFieldInt(FormFields.Quantity, true);

      if (ajax.HasErrors) return;

      if (selectedQuantity == selectedSubscriptionItem.TotalSeats) {
        ajax.AddBadField(FormFields.Quantity, "Number of seats is unchanged.");
        return;
      }

      if (selectedQuantity < 0) {
        ajax.AddDialogMessage("Quantity must be greater than zero.");
        return;
      }

      if (selectedQuantity < selectedSubscriptionItem.AssignedSeats) {
        ajax.AddDialogMessage("Must be greater than the number of currently assigned seats.");
        return;
      }

      // Ensure stripe customer exists.
      StripeHelper.FindOrCreateStripeCustomerAndSubscription(
        TenantOrgInfo,
        out bool createdNewCustomer,
        out string stripeCustomerDefaultPaymentMethodId,
        out bool createdNewSubscription,
        out string stripeSubscriptionId,
        out List<StripeService.SubscriptionItemDto> subscriptionItemsDto);

      if (stripeCustomerDefaultPaymentMethodId == null) {
        // No payment method found, return with instructions to show payment method modal.
        ajax.AddReturnValue(ReturnValues.PageMode, PageModes.ShowPaymentMethod);
        ajax.AddReturnValue(ReturnValues.SelectedQuantity, selectedQuantity);
        return;
      }

      // Update db before Stripe.
      DbHelper.Subscriptions.Org.UpdateOrgSubscriptionQuantity(
        trans: null,
        tenantOrgInfo: TenantOrgInfo,
        selectedSubscription: selectedSubscriptionItem,
        quantity: selectedQuantity);

      // Send Intercom event for org subscription update
      SendEvent(
        intercom => intercom.SubscriptionUpdated()
          .FromSession()
          .WithOrganisation(TenantOrgInfo.OrgId, TenantOrgInfo.OrgName)
          .WithSubscriptionDetails(
            subscriptionType: selectedSubscriptionItem.SubscriptionName,
            quantity: selectedQuantity,
            unitPrice: selectedSubscriptionItem.PricePerUserPerMonth
          ),
        operationName: "OrgSubscriptionUpdate_SubscriptionUpdated",
        requestRawUrl: SystemWeb.RequestRawUrl,
        telemetryProperties: new Dictionary<string, object> {
          ["OrganisationId"] = TenantOrgInfo.OrgId,
          ["SubscriptionType"] = selectedSubscriptionItem.SubscriptionName,
          ["Quantity"] = selectedQuantity
        }
      );

      // Send subscription item update to Stripe.
      // Best practice is to update the entire list of subscription items together, instead of
      // updating a single item by itself, to ensure the lists in Able and Stripe are always in sync.
      // If existingStripeSubscriptionId is null, we create it along with the items, otherwise just update the items.
      // TODO: Stripe API updates should be in a retry queue.

      // Reload full compliment of subs for org to sync with Stripe.
      var orgSubscriptionItems = DbHelper.Subscriptions.Org.GetOrgSubscriptionItems(TenantOrgInfo.OrgId);

      // Update with existing items synced with org's current quantities.
      subscriptionItemsDto = StripeHelper.EnsureOrgSubscriptionItemsToDto(orgSubscriptionItems, subscriptionItemsDto);
      StripeService.UpdateSubscriptionItems(TenantOrgInfo.StripeCustomerSubscriptionId, subscriptionItemsDto);

      ajax.SetRedirectUrl(PathHelper.Pages.Settings.Billings(),
        "Subscriptions Updated.",
        AjaxSubmitHelper.PageMessageType.SuccessDialog,
        replace: true);
    }
  }
}
