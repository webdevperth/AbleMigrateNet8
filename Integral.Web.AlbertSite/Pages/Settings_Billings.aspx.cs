using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class Settings_Billings : AppCode.PageBaseClasses.SettingsPageBase {

    public List<DbHelper.Subscriptions.Org.OrgSubscriptionItem> OrgSubscriptions;
    public DbHelper.Subscriptions.Org.OrgSubscriptionItem SelectedSubscription = null;
    public int? SelectedQuantity;

    public class DataAttr {
      public const string SubscriptionGuid = "subid";
      public const string SubscriptionQuantity = "subqty";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Subscriptions";

      OrgSubscriptions = DbHelper.Subscriptions.Org.GetOrgSubscriptionItems(SessionHelper.UserInfo.OrgId);

      // When arriving back here from a Stripe redirection, the previously-selected subscription
      // Guid and Quantity is passed on the url. If so, then automatically show the quanity picker
      // popup for it, so user can continue from where they left off after having added their c/card info.
      // If not redirected, then user picks which subscription to update as normal.

      Guid? selectedSubscriptionGuid = WebHelper.GetQueryStringGuid(PathHelper.AbleUrlKeys.SubscriptionGuid);

      if (selectedSubscriptionGuid != null) {

        // Validate given subscription guid against the ones available.
        var orgSubscriptions = DbHelper.Subscriptions.Org.GetOrgSubscriptionItems(SessionHelper.UserInfo.OrgId);
        SelectedSubscription = orgSubscriptions.Find(item => item.SubscriptionGuid == selectedSubscriptionGuid);

        if (SelectedSubscription == null) {
          WebHelper.Redirect(PathHelper.Pages.Settings.Billings());
          return;
        }

        SelectedQuantity = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.SubscriptionQty);
      }
    }

  }
}
