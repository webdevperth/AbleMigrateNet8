using System;
using System.Collections.Generic;
using Integral.Integrations;

namespace Integral.Web {

  public class StripeHelper {

    // Return stripe customer id for this OrgId.
    // Create customer if it doesn't exist.
    // A subscription for the customer may or may not exist.
    public static void FindOrCreateStripeCustomerAndSubscription(
      DbHelper.TenantOrg.TenantOrgInfo tenantOrgInfo,
      out bool createdNewCustomer,
      out string stripeDefaultPaymentMethodId,
      out bool createdNewSubscription,
      out string stripeCustomerSubscriptionId,
      out List<StripeService.SubscriptionItemDto> stripeSubscriptionItemsDto) {

      createdNewCustomer = false;
      createdNewSubscription = false;
      stripeDefaultPaymentMethodId = null;
      stripeCustomerSubscriptionId = null;
      stripeSubscriptionItemsDto = null;

      if (!tenantOrgInfo.StripeCustomerId.IsNullOrEmpty()) {

        // Tenant Org has a stripe customer ID, so that Customer should exist in Stripe.

        bool stripeCustomerFound = StripeService.TryGetCustomerAndSubscription(
          tenantOrgInfo.StripeCustomerId,
          tenantOrgInfo.OrgId,
          out stripeCustomerFound,
          out stripeDefaultPaymentMethodId,
          out stripeCustomerSubscriptionId,
          out stripeSubscriptionItemsDto);

        if (!stripeCustomerFound) {
          throw new ApplicationException($"Stripe customer is missing!\n"
            + $"OrgId: {tenantOrgInfo.OrgId},\n"
            + $"Org Name: {tenantOrgInfo.OrgName},\n"
            + $"Org StripeCustomerId missing: {tenantOrgInfo.StripeCustomerId}.");
        }

        if (tenantOrgInfo.StripeCustomerSubscriptionId != stripeCustomerSubscriptionId) {
          // Ensure tenant org also stores the subscription id.
          DbHelper.TenantOrg.UpdateStripeSubscriptionId(null, tenantOrgInfo, stripeCustomerSubscriptionId);
        }

        if (stripeDefaultPaymentMethodId.IsNullOrEmpty()) return; // Caller must get payment method first before subscription can be created.

        if (!stripeCustomerSubscriptionId.IsNullOrEmpty()) return; // Got both customer and subscription, all ok.

      } else {
        // Tenant (Org) doesn't have a Stripe customerId reference, so we need to create a new stripe customer for it.

        StripeService.CreateCustomer(
          orgId: tenantOrgInfo.OrgId,
          orgGuid: tenantOrgInfo.OrgGuid,
          orgName: tenantOrgInfo.OrgName,
          stripeCustomerEmail: ConfigHelper.IsLiveServer ? SessionHelper.UserInfo.EmailAddress : EmailHelper.GetRecipientOverrideAddress(),
          newStripeCustomerId: out string newStripeCustomerId);

        // Save new Stripe Customer Id to the tenant.
        // Note tenantOrgInfo.StripeCustomerId is updated as well.
        DbHelper.TenantOrg.UpdateStripeCustomerId(
          trans: null,
          orgInfo: tenantOrgInfo,
          stripeCustomerId: newStripeCustomerId);

        createdNewCustomer = true;
      }

      // Create a new subscription for the customer.

      // Get Org subscription info, which is all available Able subscription types,
      // with the current number of userSubscriptions for each (initially zeroes for a new tenant).
      var orgSubsciptionItems = DbHelper.Subscriptions.Org.GetOrgSubscriptionItems(tenantOrgInfo.OrgId);

      // Create a new Stripe Subscription for the Customer, with the Org's subscription items and quantities.
      // Note in Stripe a subscription can't be created without items in it.
      stripeSubscriptionItemsDto = EnsureOrgSubscriptionItemsToDto(orgSubsciptionItems, stripeSubscriptionItemsDto);
      StripeService.CreateCustomerSubscription(
        tenantOrgInfo.StripeCustomerId,
        tenantOrgInfo.OrgId,
        "Able Subscription",
        stripeSubscriptionItemsDto,
        out stripeCustomerSubscriptionId);

      // Save new Stripe SubscriptionId to the org.
      DbHelper.TenantOrg.UpdateStripeSubscriptionId(
        trans: null,
        orgInfo: tenantOrgInfo,
        stripeCustomerSubscriptionId: stripeCustomerSubscriptionId);

      createdNewSubscription = true;
    }

    public static List<StripeService.SubscriptionItemDto> EnsureOrgSubscriptionItemsToDto(
      List<DbHelper.Subscriptions.Org.OrgSubscriptionItem> orgSubscriptionItems,
      List<StripeService.SubscriptionItemDto> subscriptionItemsDto) {

      if (orgSubscriptionItems == null) throw new NullReferenceException(nameof(orgSubscriptionItems));

      if (subscriptionItemsDto == null) subscriptionItemsDto = new List<StripeService.SubscriptionItemDto>();

      // Update subscription items dto to match org subscription items.
      subscriptionItemsDto.RemoveAll(ssi => !orgSubscriptionItems.Exists(osi => osi.StripeProductPriceId == ssi.StripePriceId));
      foreach (var osi in orgSubscriptionItems) {
        var ssi = subscriptionItemsDto.Find(s => s.StripePriceId == osi.StripeProductPriceId);
        if (ssi != null) {
          ssi.SetQuantity(osi.TotalSeats);
        } else {
          subscriptionItemsDto.Add(new StripeService.SubscriptionItemDto(null, osi.StripeProductPriceId, osi.TotalSeats));
        }
      }

      return subscriptionItemsDto;
    }
  }
}
