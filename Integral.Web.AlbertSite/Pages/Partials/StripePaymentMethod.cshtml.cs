using System;
using Integral.Integrations;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class StripePaymentMethod : AppCode.PageBaseClasses.LoggedInPageModel {

    public string StripeClientSecret;
    public string StripeCustomerDefaultPaymentMethodId;

    public class UrlKeys {
      public const string Complete = "complete";
    }

    public IActionResult OnGet() {

      // Get organisation ID for payment method.
      if (!Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrganisationGuid), out Guid organisationGuid)) {
        RespondMessageAndEnd($"Invalid Provider ID, please reload page and try again.");
        return new EmptyResult();
      }

      var providerOrg = DbHelper.TenantOrg.GetTenantOrgByGuid(organisationGuid);
      if (providerOrg == null) {
        RespondMessageAndEnd($"Provider not found, please reload page and try again.");
        return new EmptyResult();
      }

      // Ensure stripe customer exists.
      StripeHelper.FindOrCreateStripeCustomerAndSubscription(
        providerOrg,
        out bool createdNewCustomer,
        out StripeCustomerDefaultPaymentMethodId,
        out bool createdNewSubscription,
        out _, out _);

      // Get stripe "client secret" for front end.
      StripeClientSecret = StripeService.GetClientSecretForPaymentMethod(providerOrg.StripeCustomerId);

      return Page();
    }
  }
}
