using Newtonsoft.Json;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using Integral.Web;

namespace Integral.Integrations {

  public class StripeService {

    // Note in here, "customer" refers to the Stripe Customer entity,
    // and "org" refers to the Able Organisation (Tenant) entity.

    // Values internal to Stripe API but the SDK doesn't provide consts for, for some reason.
    public class SDK {
      public class Session {
        public const string Mode_Subsscription = "subscription";
        public const string UIMode_Embedded = "embedded";
        public const string PaymentMethodTypes_Card = "card";
        public const string RedirectOnCompletion_IfRequired = "if_required";
        public const string ReturnUrlMergeKey_SessionId = "CHECKOUT_SESSION_ID";
      }
      public class Subscription {
        public const string BillingMode_Flexible = "flexible";
        public const string CollectionMethod_Charge_Automatically = "charge_automatically";
        public const string ProrationBehavior_Create_Prorations = "create_prorations";
        public const string SaveDefaultPaymentMethod_OnSubscription = "on_subscription";
      }
      public class PaymentMethodTypes {
        public const string Card = "card";
      }
      public class Customer {
        public class ExpandGet {
          public const string InvoiceSettings_DefaultPaymentMethod = "invoice_settings.default_payment_method";
          public const string Subscriptions = "subscriptions";
        }
        public class ExpandSearch {
          public const string InvoiceSettings_DefaultPaymentMethod = "data.invoice_settings.default_payment_method";
          public const string Subscriptions = "data.subscriptions";
        }
      }
    }

    // Custom metadata keys names. DO NOT CHANGE or it will break access to previously saved data.
    public const string MetadataKey_AbleOrgId = "AbleOrgId";
    public const string MetadataKey_AbleSubscriptionId = "AbleSubscriptionId";

    static StripeService() {

      StripeConfiguration.ApiKey = ConfigHelper.Stripe.ApiKey;
      StripeConfiguration.ClientId = ConfigHelper.Stripe.ClientId;
    }

    // Note that When a customer is created in Stripe, the "cus_xyz" stripe customer ID is saved in the sv_Organisation table
    // (aka "TenantOrg"). The reverse link is also saved - the OrgId is saved as metadata in the stripe customer object.

    // Note there is only 1 relevant Stripe Subscription object for a Customer.
    // That subscription will contain metadata linking to the Able OrgId (Tenant) corresponding to the Stripe Customer.
    // Any other subscriptions in the customer will be ignored.
    // The relevant subscription will contain "subscription items" - one item per Able Subscription type (Free, Pro, etc).
    // Each subscription item has a Quantity, which represents the "seats" available to the tenant for that subscription type.

    // Find the StripeCustomerId saved in the Org table, and return the OrgId saved in the stripe customer metadata.
    public static bool TryGetCustomerAndSubscription(
      string stripeCustomerId,
      int ableOrgId,
      out bool stripeCustomerFound,
      out string stripeCustomerDefaultPaymentMethodId,
      out string stripeCustomerSubscriptionId,
      out List<SubscriptionItemDto> subscriptionItemsDto) {

      stripeCustomerFound = false;
      stripeCustomerDefaultPaymentMethodId = null;
      stripeCustomerSubscriptionId = null;
      subscriptionItemsDto = null;

      var customerGetOptions = new CustomerGetOptions() {
        Expand = new List<string>() {
          SDK.Customer.ExpandGet.Subscriptions,
          SDK.Customer.ExpandGet.InvoiceSettings_DefaultPaymentMethod
        }
      };
      var customerService = new CustomerService();
      var customerAndSubscription = customerService.Get(stripeCustomerId, customerGetOptions);

      if (customerAndSubscription == null || customerAndSubscription.Deleted == true) return false;

      stripeCustomerFound = true;

      if (customerAndSubscription.Metadata == null || !customerAndSubscription.Metadata.ContainsKey(MetadataKey_AbleOrgId)) {
        throw new ApplicationException(
          $"Stripe customer OrgId Metadata is Missing.\n"
          + $"StripeCustomerId: {stripeCustomerId}.\n"
          + $"Expected OrgId: {ableOrgId}."
        );
      }

      string customerOrgIdStr = customerAndSubscription.Metadata[MetadataKey_AbleOrgId];
      if (customerOrgIdStr.IsNullOrEmpty()) {
        throw new ApplicationException(
          $"Stripe customer OrgId Metadata is blank.\n"
          + $"StripeCustomerId: {stripeCustomerId}.\n"
          + $"Expected OrgId: {ableOrgId}."
        );
      }

      if (!int.TryParse(customerOrgIdStr, out int customerOrgId)) {
        throw new ApplicationException(
          $"Stripe customer OrgId Metadata is not a number.\n"
          + $"StripeCustomerId: {stripeCustomerId}.\n"
          + $"Expected OrgId: {ableOrgId},\n"
          + $"Metadata OrgId: '{customerOrgIdStr}' - Should be a number.");
      }

      if (customerOrgId != ableOrgId) {
        throw new ApplicationException(
          $"Stripe customer OrgId mismatch.\n"
          + $"StripeCustomerId: {stripeCustomerId}\n"
          + $"Expected OrgId: {ableOrgId}\n"
          + $"Metadata OrgId: {customerOrgId}. Both OrgIds should be the same.");
      }

      // Output value for payment method.
      stripeCustomerDefaultPaymentMethodId = GetOrSetCustomerDefaultPaymentMethodId(customerAndSubscription);

      if (customerAndSubscription.Subscriptions?.Data?.Count > 0) {

        // Remove all subscriptions which do not have correct AbleOrgId metadata.
        customerAndSubscription.Subscriptions.Data.RemoveAll(s => !s.Metadata.ContainsKey(MetadataKey_AbleOrgId) || s.Metadata[MetadataKey_AbleOrgId].ToIntOrNull() != ableOrgId);
        // There can't be more than one.
        if (customerAndSubscription.Subscriptions.Data.Count > 1) {
          throw new ApplicationException($"Stripe customer {stripeCustomerId} has > 1 subscriptions for OrgId = {ableOrgId}.");
        }

        if (customerAndSubscription.Subscriptions.Data.Count == 1) {

          var subscription = customerAndSubscription.Subscriptions.Data[0];

          // Output values for subscription. Convert items from stripe sdk to dto.
          stripeCustomerSubscriptionId = subscription.Id;
          subscriptionItemsDto = subscription.Items.Data.ConvertAll(
            item => new SubscriptionItemDto(
              existingStripeSubscriptionItemId: item.Id,
              stripePriceId: item.Price.Id,
              quantity: (int)item.Quantity
            )
          );
        }
      }

      return true;
    }

    // Check if subscriptionId exists and belongs to the expected customerId.
    public static bool SubscriptionExists(string stripeCustomerId, string stripeCustomerSubscriptionId) {

      if (stripeCustomerId.IsNullOrEmpty()) throw new NullReferenceException(nameof(stripeCustomerId));

      if (stripeCustomerSubscriptionId.IsNullOrEmpty()) return false;

      var subscriptionService = new SubscriptionService();
      var subscription = subscriptionService.Get(stripeCustomerSubscriptionId);

      return subscription?.CustomerId == stripeCustomerId;
    }

    // Return a metadata search term. See https://docs.stripe.com/search#metadata
    private static string GetMetadataSearchTerm(string key, string value) {
      return $"metadata[{JsonConvert.SerializeObject(key)}]:{JsonConvert.SerializeObject(value)}";
    }

    public static void CreateCustomer(
      int orgId, Guid orgGuid, string orgName,
      string stripeCustomerEmail,
      out string newStripeCustomerId) {

      // Note the Able OrgId & Guid is saved as metadata so we can find the customer by them.
      var customerCreateOptions = new CustomerCreateOptions {
        Name = orgName,
        Email = stripeCustomerEmail,
        Metadata = new Dictionary<string, string>() {
          { MetadataKey_AbleOrgId, orgId.ToString() }
        }
      };

      var customerCreateService = new CustomerService();
      Customer newCustomer = customerCreateService.Create(customerCreateOptions);

      newStripeCustomerId = newCustomer.Id;
    }

    // Create the customer's subscription.
    // NOTE A default payment method must be attached to the Customer before this happens.

    // Here we will call Able "subscriptions" (al_Subscription) "plans".
    // Each stripe customer will contain a *single* subscription object.
    // Each subscription contains "subscription items", each of which is an Able plan (al_Subscription).
    // This allows Able plans to be added, removed, quantities changed,
    // and retain one billing cycle with proration in stripe (i.e. one stripe subscription object).
    // Each subscription item is a PriceId of a stripe subscription product.
    // Each subscription product is mapped to the Able plan (al_Subscription) ID.
    // So:
    // Stripe customer ID ("cus_xyz")     <=> Able sv_Organisation ID ("PartnerCompny" or "Provider")
    // Stripe subscription ID ("sub_xyz") <=> Able sv_Organisation ID
    // Stripe sub-item ID ("price_xyz")   <=> Able al_Subscription ID ("Plan")

    public static void CreateCustomerSubscription(
      string stripeCustomerId,
      int orgId,
      string subscriptionDescription,
      List<SubscriptionItemDto> subscriptionItemsDto,
      out string newStripeSubscriptionId) {

      // Create Stripe subscription and pass the Able org id as metadata to identify it
      // as the "primary" subscription for an Able organisation (tenant).

      var subscriptionCreateOptions = new SubscriptionCreateOptions {
        Customer = stripeCustomerId,
        BillingMode = new SubscriptionBillingModeOptions() { Type = SDK.Subscription.BillingMode_Flexible },
        CollectionMethod = SDK.Subscription.CollectionMethod_Charge_Automatically,
        Description = subscriptionDescription,
        PaymentSettings = new SubscriptionPaymentSettingsOptions() {
          SaveDefaultPaymentMethod = SDK.Subscription.SaveDefaultPaymentMethod_OnSubscription
        },
        ProrationBehavior = SDK.Subscription.ProrationBehavior_Create_Prorations,
        Metadata = new Dictionary<string, string>() {
          { MetadataKey_AbleOrgId, orgId.ToString() } // Relates this org subscription back to the Able org id.
        },
        Items = subscriptionItemsDto.ConvertAll(item => new SubscriptionItemOptions() {
          Price = item.StripePriceId,
          Quantity = item.Quantity
        })
      };

      var subscriptionService = new SubscriptionService();
      Subscription newSubscription = subscriptionService.Create(subscriptionCreateOptions);

      // Ensure source object SubscriptionItemIds are updated to the newly added ones (match items by their price ids).
      foreach (var newSubItem in newSubscription.Items) {
        var matchingSubItem = subscriptionItemsDto.Find(i => i.StripePriceId == newSubItem.Price.Id);
        if (matchingSubItem != null) {
          matchingSubItem.SetExistingStripeSubscriptionItemId(newSubItem.Id);
        }
      }

      newStripeSubscriptionId = newSubscription.Id;
    }

    public static void UpdateSubscriptionItems(
      string stripeSubscriptionId,
      List<SubscriptionItemDto> subscriptionItems) {

      var subscriptionUpdateOptions = new SubscriptionUpdateOptions {
        Items = subscriptionItems.ConvertAll(item => new SubscriptionItemOptions() {
          Id = item.ExistingStripeSubscriptionItemId, // null for a new item, existing id when updating an existing item.
          Price = item.StripePriceId,
          Quantity = item.Quantity
        })
      };

      var subscriptionService = new SubscriptionService();
      var subscription = subscriptionService.Update(stripeSubscriptionId, subscriptionUpdateOptions);
    }

    // Returns whether successfully set a default payment method.
    private static string GetOrSetCustomerDefaultPaymentMethodId(Customer customer) {

      if (customer == null) throw new NullReferenceException($"customer is null.");

      if (IsValidPaymentMethod(customer.InvoiceSettings?.DefaultPaymentMethod)) {
        return customer.InvoiceSettings.DefaultPaymentMethod.Id;
      }

      // If other payment methods exist, get the first valid one and set it to default.
      var paymentMethodListOptions = new PaymentMethodListOptions {
        Customer = customer.Id
      };
      var paymentMethodService = new PaymentMethodService();
      var paymentMethods = paymentMethodService.List(paymentMethodListOptions);

      if (paymentMethods?.Data == null || paymentMethods.Data.Count == 0) {
        return null; // No other method found.
      }

      PaymentMethod newDefaultPaymentMethod = null;
      foreach (PaymentMethod method in paymentMethods.Data) {
        if (IsValidPaymentMethod(method)) {
          newDefaultPaymentMethod = method;
          break;
        }
      }

      if (newDefaultPaymentMethod == null) {
        return null; // No valid method found.
      }

      // Set new method as default.
      var options = new CustomerUpdateOptions {
        InvoiceSettings = new CustomerInvoiceSettingsOptions {
          DefaultPaymentMethod = newDefaultPaymentMethod.Id
        }
      };
      var customerService = new CustomerService();
      customerService.Update(customer.Id, options);

      // Refresh the customer object to load in new default payment method.
      // Try this without need to reload customer info.
      customer.InvoiceSettings.DefaultPaymentMethod = newDefaultPaymentMethod;
      customer.InvoiceSettings.DefaultPaymentMethodId = newDefaultPaymentMethod.Id;

      return customer.InvoiceSettings.DefaultPaymentMethodId;
    }

    // If method is card, check expiry date, assume other methods are ok.
    private static bool IsValidPaymentMethod(PaymentMethod paymentMethod) {
      if (paymentMethod == null) return false;
      if (paymentMethod.Card == null) return true; // For non-card types, assume valid.
      return !IsCardExpired(paymentMethod.Card.ExpMonth, paymentMethod.Card.ExpYear);
    }

    // https://docs.stripe.com/payments/save-and-reuse?locale=en-GB
    public static string GetClientSecretForPaymentMethod(string stripeCustomerId) {

      var options = new SetupIntentCreateOptions {
        Customer = stripeCustomerId,
        PaymentMethodTypes = new List<string>
          { // https://docs.stripe.com/api/payment_methods/object?api-version=2025-07-30.preview&rds=1#payment_method_object-type
            SDK.PaymentMethodTypes.Card,
          },
        AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions {
          Enabled = false, // Forcing "card" type, so set to false.
        },
      };

      var service = new SetupIntentService();
      SetupIntent setupIntent = service.Create(options);

      return setupIntent.ClientSecret;
    }

    public static bool IsCardExpired(long expMonth, long expYear) {
      var nowUtc = DateTime.UtcNow;
      return expYear < nowUtc.Year
          || (expYear == nowUtc.Year && expMonth < nowUtc.Month);
    }

    public static string GetClientSecretForSubscriptionCheckout(
      string stripeProductPriceId,
      int? presetQuantity,
      Guid orgGuid,
      string stripeCustomerId,
      string returnUrlIncludingDomain,
      string returnUrlSessionIdKey) {

      // Add session_id merge field to the given return url.
      // See: https://docs.stripe.com/payments/checkout/custom-success-page?payment-ui=embedded-form#return-url
      if (!returnUrlIncludingDomain.IsNullOrEmpty()) {
        returnUrlIncludingDomain +=
          $"{(returnUrlIncludingDomain.Contains("?") ? "&" : "?")}" +
          $"{returnUrlSessionIdKey}={{{SDK.Session.ReturnUrlMergeKey_SessionId}}}";
      }

      var sessionCreateOptions = new SessionCreateOptions {

        Mode = SDK.Session.Mode_Subsscription,
        UiMode = SDK.Session.UIMode_Embedded,
        PaymentMethodTypes = new List<string> { SDK.Session.PaymentMethodTypes_Card },

        ClientReferenceId = orgGuid.ToStringNoBraces(),
        Customer = stripeCustomerId,
        SubscriptionData = new SessionSubscriptionDataOptions() {
          BillingMode = new SessionSubscriptionDataBillingModeOptions() {
            Type = SDK.Subscription.BillingMode_Flexible
          }
        },
        LineItems = new List<SessionLineItemOptions> {
          new SessionLineItemOptions {
            AdjustableQuantity = new SessionLineItemAdjustableQuantityOptions() {
              Enabled = presetQuantity == null,
              Minimum = presetQuantity == null ? 1: (long?)null // not allowed to specify min when presetQuantity is set
            },
            Price = stripeProductPriceId,
            Quantity = presetQuantity ?? 1
          },
        },
        RedirectOnCompletion = SDK.Session.RedirectOnCompletion_IfRequired,
        ReturnUrl = returnUrlIncludingDomain
      };

      var service = new SessionService();
      Session session = service.Create(sessionCreateOptions);

      return session.ClientSecret;
    }

    public class SubscriptionItemDto {

      public string ExistingStripeSubscriptionItemId { get; private set; }
      public string StripePriceId { get; private set; }
      public int Quantity { get; private set; }

      public SubscriptionItemDto(string existingStripeSubscriptionItemId, string stripePriceId, int quantity) {
        ExistingStripeSubscriptionItemId = existingStripeSubscriptionItemId;
        StripePriceId = stripePriceId;
        Quantity = quantity;
      }

      public void SetQuantity(int quantity) {
        if (quantity < 0) throw new ArgumentException($"SetQuantity({quantity}) quantity must be >= 0.");
        Quantity = quantity;
      }

      public void SetExistingStripeSubscriptionItemId(string id) {
        ExistingStripeSubscriptionItemId = id;
      }
    }
  }
}
