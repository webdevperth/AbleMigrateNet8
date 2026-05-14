using Integral.Web.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static Integral.Web.WebHelper.PurchaseServices;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class PurchaseServices : AppCode.PageBaseClasses.LoggedInPageBase {

    private bool CanCreateSubscription;
    private List<WebHelper.PurchaseServices.StepPanelType> UserStepPanelTabs;
    private List<DbHelper.Products.ProductInfo> ProductList;

    public class AjaxAction {
      public const string CompletePurchase = "CompletePurchase";
    }

    public class FormFields {
      public const string PurchaseData = "PurchaseData";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Purchase Services";

      CanCreateSubscription = SessionHelper.AppAccess.Users.CanCreateSubscription();

      UserStepPanelTabs = GetPanelTabsForUser(StepWizardPurposeEnum.ParticipantSubscription);
      ProductList = DbHelper.Products.GetAllProducts();

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.CompletePurchase:
              if (!CanCreateSubscription) {
                ajax.RespondNoAccessToFunction();
                return;
              }
              SubmitPurchase(ajax);
              break;

          }
        });
        return;
      }
    }

    public string GetEventPanelInfo() {

      return GetPurchaseServicesForUser(ProductList);
    }

    public void SubmitPurchase(AjaxSubmitHelper ajax) {

      if (UserStepPanelTabs.IsNullOrEmpty()) {
        ajax.AddErrorToast("Operation not allowed");
        return;
      }

      // Validate Terms and Conditions Agreement
      if (!ajax.CheckFieldBool(WebHelper.PurchaseServices.FormFields.TermsAndConditions, "1")) {
        ajax.AddDialogMessage("Please agree to the Terms and Conditions.");
        return;
      }

      var formValues = new WebHelper.QuoteSigning_FormValues {
        ClientFirstName = userInfo.FirstName,
        ClientLastName = userInfo.LastName,
        ClientEmail = userInfo.EmailAddress,
        AccFirstName = ajax.CheckFieldRegex(WebHelper.PurchaseServices.FormFields.FirstName, "Account First Name", AppHelper.Regex.GeneralText, true, "Required"),
        AccLastName = ajax.CheckFieldRegex(WebHelper.PurchaseServices.FormFields.LastName, "Accounts Last Name", AppHelper.Regex.GeneralText, true, "Required"),
        AccEmail = ajax.CheckFieldRegex(WebHelper.PurchaseServices.FormFields.Email, "Accounts Email", AppHelper.Regex.Email, true, "Required")
      };

      var purchaseData = WebHelper.GetFormValue(FormFields.PurchaseData);
      var selectedItems = new Dictionary<WebHelper.PurchaseServices.StepPanelType, string>();
      if (string.IsNullOrEmpty(purchaseData)) {

        ajax.AddBadField(FormFields.PurchaseData, "No products selected.");
        return;

      } else {
        try {
          // Deserialize selected items
          selectedItems = JsonConvert.DeserializeObject<Dictionary<WebHelper.PurchaseServices.StepPanelType, string>>(purchaseData);

        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("PurchaseServices_DeserializePurchaseData")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .WithPageUrl(Request.RawUrl)
            .WithProperty(ApplicationInsightsConstants.PurchaseDataLength, purchaseData?.Length)
            .Track();

          ajax.AddDialogMessage("Invalid purchase data format.", ex);
          return;
        }
      }

      // Ensure all required steps (except Summary) have selected items
      bool allStepsCompleted = UserStepPanelTabs.Where(step => step != StepPanelType.Summary).All(selectedItems.ContainsKey);

      if (!allStepsCompleted) {
        ajax.AddBadField(FormFields.PurchaseData, "You must select an item in each panel.");
        return;
      }

      if (ajax.BadFieldCount > 0) return;

      // Get selected items from Panels containing Products and it's corresponding ProductInfo
      // The result of this dictionaty would be <PanelType, ProductInfo>
      var selectedProducts = selectedItems
      .Where(kvp => PanelsContainingProducts.Contains(kvp.Key))
      .Select(kvp => new { Key = kvp.Key, ProductId = int.TryParse(kvp.Value, out int result) ? (int?)result : null })
      .Where(x => x.ProductId.HasValue)
      .ToDictionary(
          x => x.Key,
          x => ProductList.FirstOrDefault(prod => prod.ProductId == x.ProductId.Value)
      );

      // Get the Payment Type selected and send the info through the CompletePurchase function
      if (selectedItems.TryGetValue(StepPanelType.Payment, out string paymentString) && Enum.TryParse<PaymentType>(paymentString, out PaymentType paymentType)) {

        bool completed = CompletePurchase(ajax, paymentType, selectedProducts, formValues);
        if (completed) {

          ajax.SetRedirectUrl(PathHelper.Pages.ParticipantUpcoming(), "You are all set! Welcome to Able!");
          return;
        }
      }
      ajax.AddErrorToast("Couldn't complete purchase.");
      return;
    }

    // Use to keep track of products processed and made quote items for, to be able to use them after the loop/transaction.
    class ProcessedProducts {
      public int QuoteItemId { get; set; }
      public StepPanelType ProductType { get; set; }
      public DbHelper.Products.ProductInfo ProductInfo { get; set; }

      public ProcessedProducts(int quoteItemId, StepPanelType productType, DbHelper.Products.ProductInfo productInfo) {
        QuoteItemId = quoteItemId;
        ProductType = productType;
        ProductInfo = productInfo;
      }
    }

    private bool CompletePurchase(AjaxSubmitHelper ajax, PaymentType paymentType, Dictionary<WebHelper.PurchaseServices.StepPanelType, DbHelper.Products.ProductInfo> selectedProducts, WebHelper.QuoteSigning_FormValues formValues) {

      bool completed = false; // All items were processed and added quoteItems to the quote
      List<ProcessedProducts> processedProducts = new List<ProcessedProducts>(); // Track each product added to use after loop/transaction for separate operations

      if (paymentType == PaymentType.Quote) {
        // Get Coachee Info (Automatically created when user registered)
        var coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(userInfo.LatestCoacheeInfo.CoacheeId);
        if (coacheeInfo == null) {
          // It's necessary to set up user
          ajax.AddDialogMessage($"Not enough information to proceed, please get in contact with {ConfigHelper.Email_Coordinator_Address}.");
          return false;
        }

        // Create Quote Info Object
        var newQuoteInfo = new DbHelper.AbleQuotes.NewQuoteInfo(
          jobNumber: userInfo.LatestCoacheeInfo.JobNumber,
          ownerUserId: ConfigHelper.SelfCreatedUserDefaults.QuoteData.DealOwnerUserId,
          leadConsultantUserId: ConfigHelper.SelfCreatedUserDefaults.ProgramData.PLC,
          proposalDesignerUserId: null,
          contactUserId: userInfo.UserId,
          quoteTitle: $"Able Main {WebHelper.DisplayDate(DateTime.UtcNow)} - {userInfo.GetFullName()}",
          brandingOrgId: userInfo.OrgId,
          quoteStatusId: DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.client).QuoteStatusId,
          estimatedStartDateUtc: null,
          xeroTaxType: ConfigHelper.SelfCreatedUserDefaults.QuoteData.XeroTaxType,
          customInvoicing: ConfigHelper.SelfCreatedUserDefaults.QuoteData.CustomInvoicing,
          addToFreshSales: ConfigHelper.SelfCreatedUserDefaults.QuoteData.AddToFreshSales,
          excludeFromSalesIncentive: ConfigHelper.SelfCreatedUserDefaults.QuoteData.ExcludeFromSalesIncentive,
          quoteDealSourceId: null,
          oppPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.OPPPercentage,
          plcPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.PLCPercentage,
          deliveryPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.DeliveryPercentage,
          platformPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.DeliveryPercentage,
          proposalDesignerPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.ProposalDesignerPercentage,
          coverLetterHtml: ""
        );

        int quoteId = 0;

        // Encapsultate all in one transactrion
        completed = DbHelper.Common.UsingTransaction(trans => {
          // Create quote and get the QuoteId
          quoteId = DbHelper.AbleQuotes.CreateQuote(trans, newQuoteInfo);

          decimal quoteTotal = 0;
          // If Quote was created and there's products in the list, add quote items.
          if (quoteId > 0 && !ProductList.IsNullOrEmpty()) {
            // Add quote items.
            // Iterate though the products and add them to the quote items
            foreach (var product in selectedProducts) {
              // If the product is a Subscription or Coaching, update the corresponding value of CoacheeInfo to update later
              if (product.Key == StepPanelType.Subscription) {
                coacheeInfo.UserSubscription = new DbHelper.Subscriptions.User.UserSubscriptionInfo();
                coacheeInfo.UserSubscription.SubscriptionId = product.Value.SubscriptionId;

              } else if (product.Key == StepPanelType.Coaching) {
                coacheeInfo.UserActivity.SessionsAllocated = product.Value.CoachingSessionQuantity.GetValueOrDefault(0);
                coacheeInfo.CoachingTypeId = product.Value.CoachingTypeId;
              }
              // In each iteration get the total this item price * default quantity
              quoteTotal += (product.Value.DefaultUnitPrice.GetValueOrDefault(0) * ConfigHelper.SelfCreatedUserDefaults.QuoteData.QuoteItems_Quantity);
              try {
                var newItemId = DbHelper.AbleQuotes.CreateQuoteItem(
                  trans: trans,
                  quoteId: quoteId,
                  productId: product.Value.ProductId,
                  itemDescription: product.Value.ProductDescription,
                  isOptionalId: DbHelper.AbleQuotes.OptionalEnum.No.Id,
                  unitPrice: product.Value.DefaultUnitPrice,
                  quantity: ConfigHelper.SelfCreatedUserDefaults.QuoteData.QuoteItems_Quantity,
                  quantityDescr: ConfigHelper.SelfCreatedUserDefaults.QuoteData.QuoteItems_QuantityDescription,
                  isAccepted: true// Automatically accept items
                  );

                // Add this processed product to the list
                processedProducts.Add(new ProcessedProducts(newItemId, product.Key, product.Value));

              } catch (Exception ex) {
                var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
                telemetry?.Exception(ex)
                  .WithOperation("PurchaseServices_CreateQuoteItem")
                  .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
                  .WithPageUrl(Request.RawUrl)
                  .WithProperty(ApplicationInsightsConstants.ProductId, product.Value?.ProductId)
                  .WithProperty(ApplicationInsightsConstants.ProductDescription, product.Value?.ProductDescription)
                  .WithProperty(ApplicationInsightsConstants.UnitPrice, product.Value?.DefaultUnitPrice)
                  .WithProperty(ApplicationInsightsConstants.PanelType, product.Key.ToString())
                  .Track();

                ajax.AddDialogMessage("Error creating quote item.");
                if (ConfigHelper.IsDevServer) ajax.AppendToCurrentMessage("<br/>Purchase Services<br/>" + ex.ToString());
                if (!ConfigHelper.IsDevServer) EmailHelper.SendInternalSupportEmail(ex, "QuoteInfo Trying to add new QuoteItem on PurchaseServices.");
                return false;
              }
            }
            // Automatically Sign Quote
            DbHelper.AbleQuotes.UpdateQuoteAccepted(trans, quoteId, quoteTotal, formValues);

            // Update Coachee
            DbHelper.AlbertCoachees.UpdateCoachee(trans, coacheeInfo);

            return true;
          }
          return false;
        });

        if (completed) {

          // Send Intercom event for quote creation (wrapped in try-catch to prevent workflow interruption)
          if (quoteId > 0) {
            var quoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(quoteId);
            if (quoteInfo != null) {
              SendEvent(
                intercom => intercom.QuoteCreated()
                  .FromSession()
                  .WithQuote(quoteId, quoteInfo.QuoteTitle)
                  .WithClientCompany(quoteInfo.CompanyId, quoteInfo.CompanyName ?? ""),
                operationName: "PurchaseServices_QuoteCreated",
                requestRawUrl: SystemWeb.RequestRawUrl,
                telemetryProperties: new Dictionary<string, object> {
                  ["QuoteId"] = quoteId
                }
              );
            }
          }

          // Send Welcome Email
          if (coacheeInfo.CoachingTypeId.HasValue) {

            ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(coacheeInfo.ProgramJobNumber);
            if (ProjectInfo != null) {

              DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo = null;
              if (coacheeInfo.CoachUserId != ConfigHelper.UserId.Unassigned) {
                coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(coacheeInfo.CoachUserId);
              }

              bool welcomeEmailSent = AlbertEmails.ParticipantWelcome.Send(ProjectInfo, coacheeInfo, coachInfo, ProjectInfo, out EmailHelper.MandrillSentResult sendResult, AlbertEmails.ParticipantWelcome.SetSendDates.Yes);

              // Send Intercom event for coachee invitation (purchase flow welcome email)
              if (welcomeEmailSent) {
                var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo.UserGuid);
                if (participantExternalId.HasValue) {
                  SendEvent(
                    intercom => intercom.CoacheeInvited()
                      .FromSession()
                      .WithCoacheeEmailAddress(coacheeInfo.EmailAddress)
                      .WithOrganisation(coacheeInfo.TenantOrgId, coacheeInfo.OrgName),
                    operationName: "PurchaseServices_CoacheeInvited",
                    requestRawUrl: SystemWeb.RequestRawUrl,
                    telemetryProperties: new Dictionary<string, object> {
                      ["ParticipantEmail"] = coacheeInfo?.EmailAddress
                    }
                  );
                }
              }
            }
          }

          if (processedProducts.Count > 0) {
            foreach (var prod in processedProducts) {

              if (prod == null || prod.QuoteItemId == 0) continue;

              if (prod.ProductType == StepPanelType.Subscription) {
                // Value object to capture subscription details for tracking
                string subscriptionType = null;
                int subscriptionQuantity = 0;
                decimal subscriptionUnitPrice = 0;

                // Get the corresponding Quote Item
                DbHelper.Common.UsingTransaction(trans => {
                  var quoteInfo = DbHelper.AbleQuotes.GetQuoteItemsForSubscriptions(trans, prod.QuoteItemId);
                  // Update Subscription
                  if (quoteInfo != null && quoteInfo.AvailableSubscriptions > 0) {
                    DbHelper.Subscriptions.User.UpdateCoacheeSubscription(trans, coacheeInfo, quoteInfo);

                    // Capture subscription details for tracking
                    subscriptionType = quoteInfo.ProductTitle;
                    subscriptionQuantity = quoteInfo.AvailableSubscriptions;
                    subscriptionUnitPrice = quoteInfo.UnitPrice;
                  }
                  return true;
                });

                // Send Intercom event for subscription purchase
                if (!subscriptionType.IsNullOrEmpty()) {
                  var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo.UserGuid);
                  if (participantExternalId.HasValue) {
                    SendEvent(
                      intercom => intercom.SubscriptionUpdated()
                        .FromSession()
                        .WithParticipant(participantExternalId.Value, coacheeInfo.EmailAddress)
                        .WithOrganisation(coacheeInfo.TenantOrgId, coacheeInfo.OrgName)
                        .WithProject(coacheeInfo.ProgramJobId ?? 0, coacheeInfo.ProgramJobNumber ?? "")
                        .WithSubscriptionDetails(
                          subscriptionType: subscriptionType,
                          quantity: subscriptionQuantity,
                          unitPrice: subscriptionUnitPrice
                        ),
                      operationName: "PurchaseServices_SubscriptionUpdated",
                      requestRawUrl: SystemWeb.RequestRawUrl,
                      telemetryProperties: new Dictionary<string, object> {
                        ["ParticipantEmail"] = coacheeInfo?.EmailAddress,
                        ["SubscriptionType"] = subscriptionType
                      }
                    );
                  }
                }

              } else if (prod.ProductType == StepPanelType.Coaching) {
                // Create Session Components
                CreateSessionComponents(coacheeInfo, prod.ProductInfo, prod.QuoteItemId);
              }
            }
          }
        }
      }

      return completed;
    }

    public void CreateSessionComponents(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo, DbHelper.Products.ProductInfo productInfo, int quoteItemId) {

      if (productInfo == null || coacheeInfo == null || !productInfo.CoachingSessionQuantity.HasValue) return;

      // Update coaching sessions.
      try {
        // Create Components object
        var updateComponentsInfo = new DbHelper.ProgramComponents.UpdateSessionComponentsInfo(coacheeInfo);
        var totalCoachingSessions = productInfo.CoachingSessionQuantity.Value;
        var sessionRevenue = productInfo.DefaultUnitPrice.Value / totalCoachingSessions;

        // Add each session to components object
        for (int i = 1; i <= totalCoachingSessions; i++) {
          updateComponentsInfo.AddSessionToUpdate(i, sessionRevenue, quoteItemId);
        }

        // Create session components
        DbHelper.Common.UsingTransaction(trans => {
          DbHelper.ProgramComponents.UpdateSessionComponents(trans, updateComponentsInfo);
          return true;
        });
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("PurchaseServices_CreateSessionComponents")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo?.UserGuid))
          .WithPageUrl(Request.RawUrl)
          .WithProperty(ApplicationInsightsConstants.ProductId, productInfo?.ProductId)
          .WithProperty(ApplicationInsightsConstants.SessionQuantity, productInfo?.CoachingSessionQuantity)
          .WithProperty(ApplicationInsightsConstants.QuoteItemId, quoteItemId)
          .Track();

        throw new ApplicationException("Error updating session components. Please try again later.", ex);
      }
    }
  }
}
