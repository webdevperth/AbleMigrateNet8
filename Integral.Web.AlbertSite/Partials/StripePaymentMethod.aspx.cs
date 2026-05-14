using System;
using Integral.Integrations;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class StripePaymentMethod : AppCode.PageBaseClasses.LoggedInPageBase {

    public string StripeClientSecret;
    public string StripeCustomerDefaultPaymentMethodId;

    public class UrlKeys {
      public const string Complete = "complete";
    }

    protected void Page_Load(object sender, EventArgs e) {

      // Get organisation ID for payment method.
      if (!Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrganisationGuid), out Guid organisationGuid)) {
        RespondMessageAndEnd($"Invalid Provider ID, please reload page and try again.");
        return;
      }

      var providerOrg = DbHelper.TenantOrg.GetTenantOrgByGuid(organisationGuid);
      if (providerOrg == null) {
        RespondMessageAndEnd($"Provider not found, please reload page and try again.");
        return;
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
      return;
    }
  }
}
