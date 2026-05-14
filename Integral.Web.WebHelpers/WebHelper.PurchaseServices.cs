using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web {
  public partial class WebHelper {

    public class PurchaseServices {

      #region Enums and Constants
      // Used to describe the purpose of using this "Tab panels" and based on that get the appropiate tabs, etc.
      public enum StepWizardPurposeEnum { ParticipantSubscription, QuickAddingParticipants };

      public const string SubscriptionTerm = "&#47; year"; // &#47; is the entity encoding for a slash
      // Describes the kind of content that the panel is going to display and later will be used for validations
      public enum StepPanelType { None, Subscription, Coaching, Payment, Summary, ParticipantInformation, ParticipantSettings };
      // Describes the type of button, it will define it's functionality and styles.
      public enum StepButtonType { Next, Previous, SeeMore, Complete, AddParticipant };
      public enum StepPanelBoxLayout { None, Grid, Row }; // How the boxes are going to display in the panel. None: No boxes
      // Describes the type of payment method, it will define it's functionality in the backend.
      public enum PaymentType { None, Quote };

      // Validation of panels that contain products or payments
      public static List<StepPanelType> PanelsContainingProducts = new List<StepPanelType>() { StepPanelType.Subscription, StepPanelType.Coaching };
      public static List<StepPanelType> PanelsContainingPayments = new List<StepPanelType>() { StepPanelType.Payment };

      public class CustomStepPanel {
        public StepPanelType StepPanelType;
        public string StepContentHtml;
        public CustomStepPanel(StepPanelType stepPanelType, string stepContentHtml) {
          StepPanelType = stepPanelType;
          StepContentHtml = stepContentHtml;
        }
      }

      // Attributes consulted from the front-end for different functionalities
      // Keep as constants to avoid typos
      public class DataAttributes {
        public const string ProductId = "product-id";
        public const string NextStep = "next-step";
        public const string PreviousStep = "prev-step";
        public const string ProductName = "prod-name";
        public const string ProductPriceDisplay = "prod-price-disp";
        public const string ProductPriceValue = "prod-price-val";
        public const string PriceDisplaySummary = "prod-price-disp-sum";
      }

      public class Public_CSS {
        public const string BoxDescription = "box-description";
        public const string SummaryContainer = "summary-container";
        public const string SummaryTotal = "summary-total";
        public const string BillingInformation = "billing-info";
        public const string ProgramTitle = "program-title";
        public const string PurchaseForm = "purchase-form";
        public const string ParticipantForm = "participantForm";
        public const string PanelInfo = "panel-info";
        public const string SubscriptionOptions = "sub-options";
      }

      private class Private_CSS {
        public const string SummaryFooter = "summary-footer";
        public const string MostPopular = "most-popular";
      }

      public class Button_CSS {
        public const string Next = "btn-next";
        public const string Previous = "btn-previous";
        public const string SeeMore = "btn-seemore";
        public const string Complete = "btn-complete";
        public const string Primary = "btn-primary";
        public const string Secondary = "btn-secondary";
        public const string AddParticipant = "btnPurchaseServices_AddParticipant";
      }

      public class CustomProductId {
        public const string NoCoaching = "no-coaching";
      }

      public class FormFields {
        public const string FirstName = "FirstName";
        public const string LastName = "LastName";
        public const string Email = "Email";
        public const string TermsAndConditions = "TermsAndConditions";
      }

      #endregion Enums and Constants

      public static string GetLearnerPlanSelectionCard(
        DbHelper.Subscriptions.SubscriptionInfo thisSub,
        DbHelper.Subscriptions.SubscriptionInfo prevSub,
        List<DbHelper.Subscriptions.Org.OrgSubscriptionItem> orgSubItems,
        string inputName,
        bool isSelected,
        string buttonText = "Choose Plan") {

        string subGuid = thisSub.SubscriptionGuid.ToStringNoBraces();
        decimal price = thisSub.PricePerUserPerMonth;
        string priceFormatted = price % 1 == 0 ? price.ToString("0") : price.ToString("0.00");

        string prevSubHtml = prevSub == null ? string.Empty : $@"<span class=""plan-feature-item"">Everything in {prevSub.SubscriptionName.HTMLEncode()}</span>";

        // "Feature text" is a text column containing simple lines of text separated by \n.
        // They're displayed in point form as a list.
        string featureHtml = string.Empty;
        if (!thisSub.FeatureListText.IsNullOrEmptyOrWhitespace()) {
          foreach (var featureText in thisSub.FeatureListText.ToList("\n", StringSplitOptions.RemoveEmptyEntries)) {
            if (featureText.Trim().IsNullOrEmptyOrWhitespace()) continue;
            featureHtml += $@"<span class=""plan-feature-item"">{featureText.HTMLEncode()}</span>";
          }
        }

        string checkedAttr = isSelected ? "checked" : string.Empty;
        string fieldNameHtml = inputName.HTMLEncode();

        // If there are seats available for paid plans, show how many.
        string seatsHtml = string.Empty;
        if (thisSub.PricePerUserPerMonth > 0 && orgSubItems != null) {
          var orgSubItem = orgSubItems.Find(i => i.StripeProductPriceId == thisSub.StripeProductPriceId);
          if (orgSubItem != null && orgSubItem.AvailableSeats > 0) {
            seatsHtml = $"{orgSubItem.AvailableSeats} {"seat".ToPlural(orgSubItem.AvailableSeats)} available.";
          }
        }

        return $@"
          <label class=""plan-card active"" for=""plan-card-{inputName}-{subGuid}"">
            <input name=""{fieldNameHtml}"" {checkedAttr} type=""radio"" id=""plan-card-{fieldNameHtml}-{subGuid}"" class=""pos-absolute opacity0"" value=""{subGuid}"" />
            <span class=""card-top"">
              <span class=""plan-name"">{thisSub.SubscriptionName.HTMLEncode()}</span>
              <span class=""plan-price"">US${priceFormatted} <span class=""plan-price-per"">/user/month</span></span>
              <span class=""plan-includes"">Includes:</span>
              {prevSubHtml}
              {featureHtml}
            </span>
            <span class=""card-bottom"">
              <div class=""seats-available"">{seatsHtml}</div>
              <span class=""plan-button"">{buttonText.HTMLEncode()}</span>
            </span>
          </label>";
      }

      #region Models
      // This class is used to store special cases for products, like default selected product, most popular product, etc.
      // To add more just set the panel type in the dictionaty 'ProductSpecialCase' and add the new case in the class.
      // It's use is in GetProductStepCardItems() method.
      public class ProductSpecialCaseInfo {
        public string DefaultSelectedProduct { get; set; }
        public string MostPopularProduct { get; set; }
        public int? LinkProduct { get; set; }
        public string LinkProductText { get; set; }
      }

      public static readonly Dictionary<StepPanelType, ProductSpecialCaseInfo> ProductSpecialCase = new Dictionary<StepPanelType, ProductSpecialCaseInfo>() {
        { StepPanelType.Subscription, new ProductSpecialCaseInfo {
            DefaultSelectedProduct = ConfigHelper.SubscriptionId.AI_Coaching.ToString(),
            MostPopularProduct = ConfigHelper.SubscriptionId.AI_Coaching.ToString(),
            LinkProduct = ConfigHelper.SubscriptionId.FoundationFree,
            LinkProductText = "Continue with free subscription"
        }},
        { StepPanelType.Coaching, new ProductSpecialCaseInfo {
            DefaultSelectedProduct = CustomProductId.NoCoaching
        }}
      };

      // Used to define the type of the tab and it's title, it's the top bar of the purchase services modal.
      public class StepTab {
        public StepPanelType StepType { get; private set; }
        public string Title { get; private set; }

        // Static dictionary for fast lookup
        private static readonly Dictionary<StepPanelType, string> StepTabTitles = new Dictionary<StepPanelType, string>() {
          { StepPanelType.Subscription, "Subscription" },
          { StepPanelType.Coaching, "Coaching" },
          { StepPanelType.Payment, "Payment" },
          { StepPanelType.Summary, "Summary" },
          { StepPanelType.ParticipantInformation, "Info" },
          { StepPanelType.ParticipantSettings, "Settings" }
        };

        public StepTab(StepPanelType stepType) {
          this.StepType = stepType;
          this.Title = StepTabTitles.ContainsKey(stepType) ? StepTabTitles[stepType] : string.Empty;
        }
      }

      // This class is used to store the configuration of the panel, like the layout, the products, etc.
      // One configuration per panel. It's use is in GetStepPanelInfo() method, but it's defined in method GetStepConfiguration().
      public class StepConfiguration {
        public Func<int, bool> CategoryFilter { get; set; } = null;
        public StepPanelBoxLayout Layout { get; set; } = StepPanelBoxLayout.Grid;
        public bool FoldDescription { get; set; } = false; // Show See More Button
        public List<ProductInfo> Products { get; set; } = new List<ProductInfo>();
        public bool PanelContainsProducts { get; set; } = false;
        public string StepPanelBoxLayoutCSS { get; set; }
        public string CustomContentHtml { get; set; }
      }

      public class StepPanelInfo {
        private StepWizardPurposeEnum StepWizardPurpose { get; set; }
        private StepPanelType StepPanelType { get; set; }
        private string Title { get; set; }
        private bool IsVisible { get; set; } = false;
        private List<StepCardItem> StepCardItems { get; set; } = new List<StepCardItem>();
        private StepPanelType NextStepPanelType { get; set; }
        private StepPanelType PreviousStepPanelType { get; set; }
        private StepConfiguration Configuration { get; set; }
        public string StepPanelHtml { get; set; }

        private static readonly Dictionary<StepPanelType, string> StepPanelTitles = new Dictionary<StepPanelType, string>(){
          { StepPanelType.Subscription, "Choose Subscription" },
          { StepPanelType.Coaching, "Select Coaching" },
          { StepPanelType.Payment, "Select Payment Method" },
          { StepPanelType.Summary, "Confirm your order" }
        };

        public StepPanelInfo(StepWizardPurposeEnum stepWizardPurpose, StepPanelType stepPanelType, bool isVisible, StepPanelType nextStepPanelType, StepPanelType previousStepPanelType, StepConfiguration configuration) {
          this.StepWizardPurpose = stepWizardPurpose;
          this.StepPanelType = stepPanelType;
          this.Title = StepPanelTitles.TryGetValue(stepPanelType, out string title) ? title : string.Empty;
          this.IsVisible = isVisible;
          this.NextStepPanelType = nextStepPanelType;
          this.PreviousStepPanelType = previousStepPanelType;
          this.Configuration = configuration;
          this.StepCardItems = GetStepCardItems();

          this.StepPanelHtml = GetStepPanelHtml();
        }

        public string GetStepPanelDataAttributes() {
          string attributes = "";

          if (this.PreviousStepPanelType != StepPanelType.None)
            attributes += $@" data-{DataAttributes.PreviousStep}=""{this.PreviousStepPanelType}""";

          if (this.NextStepPanelType != StepPanelType.None)
            attributes += $@" data-{DataAttributes.NextStep}=""{this.NextStepPanelType}""";

          return attributes.Trim();
        }

        public List<StepButtonInfo> GetStepPanelButtonList() {
          var panelButtons = new List<StepButtonInfo>();

          if (this.PreviousStepPanelType != StepPanelType.None) {
            panelButtons.Add(new StepButtonInfo(StepButtonType.Previous, "Previous"));
          }
          if (this.NextStepPanelType != StepPanelType.None) {
            panelButtons.Add(new StepButtonInfo(StepButtonType.Next, "Next"));
          }
          if (this.NextStepPanelType == StepPanelType.None) {
            if (this.StepWizardPurpose == StepWizardPurposeEnum.QuickAddingParticipants) {
              panelButtons.Add(new StepButtonInfo(StepButtonType.AddParticipant, "Add Participant"));
            } else if (this.StepWizardPurpose == StepWizardPurposeEnum.ParticipantSubscription) {
              panelButtons.Add(new StepButtonInfo(StepButtonType.Complete, "Complete"));
            }
          }
          return panelButtons;
        }

        private List<StepCardItem> GetStepCardItems() {
          if (this.Configuration.PanelContainsProducts) {
            return GetProductStepCardItems();
          } else if (this.StepPanelType == StepPanelType.Payment) {
            return GetPaymentCardItems();
          }
          return null;
        }

        private List<StepCardItem> GetProductStepCardItems() {

          var cardItems = new List<StepCardItem>();

          // Fetch productSpecialCaseInfo once from the dictionary before the loop
          var productSpecialCaseInfo = ProductSpecialCase.TryGetValue(this.StepPanelType, out var productInfo) ? productInfo : null;

          // Process Products from Database
          if (this.Configuration != null && !this.Configuration.Products.IsNullOrEmpty()) {
            cardItems.AddRange(this.Configuration.Products.Select((prod, index) => new StepCardItem(
              title: prod.Name,
              description: prod.Description,
              cardItemId: prod.ProductId.ToString(),
              priceDisplay: GetPriceFormatDisplay(prod.Price, false),
              priceDisplaySummary: GetPriceFormatDisplay(prod.Price, true),
              priceValue: prod.Price.GetValueOrDefault(0),
              isSelected: IsProductDefaultSelected(productSpecialCaseInfo, prod),
              boxButtonInfo: this.Configuration.FoldDescription ? GetProductSeeMoreButtonInfo(prod.Description) : null,
              showAsLinkInsteadOfCardDesign: ShowAsLinkInsteadOfCardDesign(productSpecialCaseInfo, prod, out string linkText),
              linkText: linkText.IsNullOrEmpty() ? prod.Name : linkText,
              isMostPopularProduct: IsMostPopularProduct(productSpecialCaseInfo, prod)
            )));
          }
          return cardItems;
        }
        private bool IsProductDefaultSelected(ProductSpecialCaseInfo productSpecialCaseInfo, ProductInfo prod) {
          return productSpecialCaseInfo?.DefaultSelectedProduct == prod.ProductId.ToString() || (this.StepPanelType == StepPanelType.Subscription && productSpecialCaseInfo?.DefaultSelectedProduct == prod.SubscriptionId.ToString());
        }
        private bool IsMostPopularProduct(ProductSpecialCaseInfo productSpecialCaseInfo, ProductInfo prod) {
          return productSpecialCaseInfo?.MostPopularProduct == prod.ProductId.ToString() || (this.StepPanelType == StepPanelType.Subscription && productSpecialCaseInfo?.MostPopularProduct == prod.SubscriptionId.ToString());
        }
        private bool ShowAsLinkInsteadOfCardDesign(ProductSpecialCaseInfo productSpecialCaseInfo, ProductInfo prod, out string linkText) {
          linkText = string.Empty;
          if (productSpecialCaseInfo?.LinkProduct.HasValue == true && ((this.StepPanelType == StepPanelType.Subscription && productSpecialCaseInfo.LinkProduct == prod.SubscriptionId) || productSpecialCaseInfo.LinkProduct.ToString() == prod.ProductId.ToString())) {
            linkText = productSpecialCaseInfo.LinkProductText;
            return true;
          }
          return false;
        }

        private List<StepCardItem> GetPaymentCardItems() {

          if (this.StepPanelType != StepPanelType.Payment) return null;

          var cardItems = new List<StepCardItem>();
          // Iterate through all Payment methods except for 'None'
          foreach (var paymentType in Enum.GetValues(typeof(PaymentType)).Cast<PaymentType>().Where(p => p != PaymentType.None)) {
            cardItems.Add(new StepCardItem(GetPaymentCardText(paymentType), "", paymentType.ToString(), "", "", 0, paymentType == PaymentType.Quote, null));
          }
          return cardItems;
        }

        private string GetPaymentCardText(PaymentType paymentType) {
          if (paymentType == PaymentType.Quote) {
            return "Pay by Invoice";
          }
          return "";
        }

        private StepButtonInfo GetProductSeeMoreButtonInfo(string productDescription) {
          StepButtonInfo stepButtonInfo = null;

          if (!productDescription.IsNullOrEmptyOrWhitespace()) {
            stepButtonInfo = new StepButtonInfo(StepButtonType.SeeMore, "See More");
          }
          return stepButtonInfo;
        }

        private string GetPriceFormatDisplay(decimal? productPrice, bool isSummaryDisplay) {
          if (StepWizardPurpose == StepWizardPurposeEnum.QuickAddingParticipants) return ""; // Do not display price for Adding participants
          if (productPrice == null || productPrice == 0) return "Free";
          string priceTermHtml = !isSummaryDisplay && this.StepPanelType == StepPanelType.Subscription ? $@"<span class=""price-term"">{SubscriptionTerm}</span>" : "";
          return $"${productPrice} AUD{priceTermHtml}";
        }

        private string GetStepPanelHtml() {

          if (this == null) return "";

          var cardsHtml = GetCardItemsHtml();
          var buttonsHtml = GetButtonGroupHtml(this?.GetStepPanelButtonList());
          var summaryHtml = GetSummaryHtml();
          var billingInfoHtml = GetBillingInformationHtml();

          var subscriptionClass_QuickAddPax = this.StepWizardPurpose == StepWizardPurposeEnum.QuickAddingParticipants && this.StepPanelType == StepPanelType.Subscription ? $"{Public_CSS.SubscriptionOptions}" : "";
          var subscriptionInfoDiv_QuickAddPax = this.StepWizardPurpose == StepWizardPurposeEnum.QuickAddingParticipants && this.StepPanelType == StepPanelType.Subscription ? $@"<div class=""{Public_CSS.PanelInfo}""></div>" : "";

          return $@"
          <div name=""{this.StepPanelType}"" class=""step-panel {(this.IsVisible ? "" : "displaynone")}"" {this.GetStepPanelDataAttributes()}>
            {subscriptionInfoDiv_QuickAddPax}
            <div class=""step-content {subscriptionClass_QuickAddPax}"">
              <h2>{this.Title}</h2>
              {GetPanelBodyHtml()}
            </div>
            {GetButtonGroupHtml(this?.GetStepPanelButtonList())}
          </div>";
        }

        private string GetPanelBodyHtml() {
          if (!this.Configuration.CustomContentHtml.IsNullOrEmpty()) {
            return this.Configuration.CustomContentHtml;
          } else if (this.Configuration.PanelContainsProducts) {
            return GetCardItemsHtml();
          } else if (this.StepPanelType == StepPanelType.Summary) {
            return GetSummaryHtml();
          } else if (this.StepPanelType == StepPanelType.Payment) {
            return GetCardItemsHtml() + GetBillingInformationHtml();
          } else return "";
        }

        private string GetBillingInformationHtml() {
          // Only show billing information for Payment panel
          if (this.StepPanelType != StepPanelType.Payment) return "";
          // By default, the billing information is filled with the user's information
          var userInfo = SessionHelper.GetUserInfoOrNull();
          return $@"
          <hr/>
          <div class=""step-panel {Public_CSS.BillingInformation}"">
            <h2>Billing Information</h2>
            <div class=""mt10 mb10"" id=""newContactInfo"">
              {GetTextInputDual("Name:", FormFields.FirstName, userInfo.FirstName, "First Name", FormFields.LastName, userInfo.LastName, "Last Name", false, InputMaxLength.NoLimit, 10)}
              {GetTextInput("Email:", FormFields.Email, userInfo.EmailAddress, "", 2, 10)}
            </div>
          </div>";
        }

        private string GetSummaryHtml() {
          // The summary panel is the last panel, and we only need to return set the base HTML structure, as the content will be filled by jQuery based on the user selection.
          if (this.StepPanelType != StepPanelType.Summary) return "";

          return $@"
          <div class=""{Public_CSS.SummaryContainer} w100p""></div>
          <div class=""{Private_CSS.SummaryFooter} w100p"">
            {GetAbleTermsAndConditionsCheckBoxHtml(FormFields.TermsAndConditions)}
            <div class=""{Public_CSS.SummaryTotal}""></div>
          </div>";
        }

        private string GetCardItemsHtml() {
          if (this.StepCardItems.IsNullOrEmpty()) return string.Empty;

          this.StepCardItems.Sort((x, y) => {
            // If it's link leave last
            int isContinueComparison = x.ShowAsLinkInsteadOfCardDesign.CompareTo(y.ShowAsLinkInsteadOfCardDesign);
            if (isContinueComparison != 0)
              return isContinueComparison;

            // If neither or both are the specific ProductId, sort by Price
            return x.PriceValue.CompareTo(y.PriceValue);
          }
          );

          var cardItemList = new List<string>();
          var linkItemList = new List<string>();

          foreach (var card in this.StepCardItems) {
            if (card.ShowAsLinkInsteadOfCardDesign) {
              linkItemList.Add(GetProductLinkItemHtml(card));
            } else {
              cardItemList.Add(GetCardItemHtml(card));
            }
          }

          return
            string.Join("", cardItemList).SurroundWith($@"<div class=""{this.Configuration.StepPanelBoxLayoutCSS}"">", "</div>") +
            string.Join("", linkItemList).SurroundWith($@"<div class=""step-link-row"">", "</div>");
        }

        private string GetCardItemHtml(StepCardItem card) {
          if (card == null) return string.Empty;

          string detailsHtml = "";
          if (!card.Description.IsNullOrEmptyOrWhitespace()) {
            detailsHtml = $@"
              <div class=""{Public_CSS.BoxDescription} {(this.Configuration.FoldDescription ? "displaynone" : "")}"">
                {card.Description.Replace("<br>", "")}
              </div>";
          }

          return $@"
          <div class=""step-card {(card.IsSelected ? "selected" : "")} {(card.IsMostPopularProduct ? Private_CSS.MostPopular : "")}"" data-{DataAttributes.ProductId}=""{card.CardItemId}"">
            <h3 class=""{Public_CSS.ProgramTitle}"" data-{DataAttributes.ProductName}=""{card.Title}"">{card.Title}</h3>
            {GetPriceDisplayHtml(card)}
            {detailsHtml}
            {card.BoxButtonInfo?.GetButtonHtml}
          </div>";
        }

        private string GetPriceDisplayHtml(StepCardItem card) {
          if (string.IsNullOrEmpty(card.PriceDisplay)) return "";

          return $@"
          <div class=""price"" data-{DataAttributes.ProductPriceValue}=""{card.PriceValue}"" data-{DataAttributes.PriceDisplaySummary}=""{card.PriceDisplaySummary}"">
            {card.PriceDisplay}
          </div>";
        }

        private string GetProductLinkItemHtml(StepCardItem card) {
          // Used for items that won't be displayed as cards, but as links (i.e. Free subscription)
          if (card == null) return string.Empty;
          return $@"
          <div class=""link-item {(card.IsSelected ? "selected" : "")} {Button_CSS.Next}"" data-{DataAttributes.ProductId}=""{card.CardItemId}"">
            <p class=""{Public_CSS.ProgramTitle}"" data-{DataAttributes.ProductName}=""{card.LinkText}"">{card.LinkText}</p>
            <div class=""price displaynone"" data-{DataAttributes.ProductPriceValue}=""{card.PriceValue}"" data-{DataAttributes.ProductPriceDisplay}=""{card.PriceDisplay}""></div>
          </div>";
        }

        private string GetButtonGroupHtml(List<StepButtonInfo> buttons) {
          if (buttons.IsNullOrEmpty()) return string.Empty;
          return $@"<div class=""button-group"">{string.Join("", buttons.Select(button => button?.GetButtonHtml))}</div>";
        }

      }

      public class StepCardItem {
        public string Title { get; set; }
        public string Description { get; set; }
        public string CardItemId { get; set; }
        public string PriceDisplay { get; set; } // The amount to display in the HTML
        public string PriceDisplaySummary { get; set; } // The amount to display in the summary list
        public decimal PriceValue { get; set; } // The actual value to make calculations in jQuery
        public bool IsSelected { get; set; }
        public bool ShowAsLinkInsteadOfCardDesign { get; set; } // i.e.: 'Continue without Subscription' link that triggers next functionality
        public string LinkText { get; set; } // i.e.: 'Continue without Subscription' link that triggers next functionality
        public StepButtonInfo BoxButtonInfo { get; set; } = null; // Not mandatory
        public bool IsMostPopularProduct { get; set; }

        public StepCardItem(string title, string description, string cardItemId, string priceDisplay, string priceDisplaySummary, decimal priceValue, bool isSelected, StepButtonInfo boxButtonInfo,
          bool showAsLinkInsteadOfCardDesign = false, string linkText = null, bool isMostPopularProduct = false) {
          this.Title = title;
          this.Description = description;
          this.CardItemId = cardItemId;
          this.PriceDisplay = priceDisplay;
          this.PriceDisplaySummary = priceDisplaySummary;
          this.PriceValue = priceValue;
          this.IsSelected = isSelected;
          this.BoxButtonInfo = boxButtonInfo;
          this.ShowAsLinkInsteadOfCardDesign = showAsLinkInsteadOfCardDesign;
          this.LinkText = linkText;
          this.IsMostPopularProduct = isMostPopularProduct;
        }
      }

      public class StepButtonInfo {
        private StepButtonType StepButtonType { get; set; }
        private string Text { get; set; }
        public StepButtonInfo(StepButtonType stepButtonType, string text) {
          this.StepButtonType = stepButtonType;
          this.Text = text;
        }

        private string ButtonClass() {
          string buttonFunctionClass = "", buttonDesignClass = "";

          switch (this.StepButtonType) {
            case StepButtonType.Next:
              buttonFunctionClass = Button_CSS.Next;
              break;
            case StepButtonType.Previous:
              buttonFunctionClass = Button_CSS.Previous;
              buttonDesignClass = Button_CSS.Secondary;
              break;
            case StepButtonType.SeeMore:
              buttonFunctionClass = Button_CSS.SeeMore;
              break;
            case StepButtonType.Complete:
              buttonFunctionClass = Button_CSS.Complete;
              break;
            case StepButtonType.AddParticipant:
              buttonFunctionClass = Button_CSS.AddParticipant;
              break;
            default:
              throw new ArgumentOutOfRangeException();
          }

          if (this.StepButtonType != StepButtonType.SeeMore && this.StepButtonType != StepButtonType.Previous) {
            buttonDesignClass = Button_CSS.Primary;
          }

          return buttonFunctionClass + " " + buttonDesignClass;
        }

        private string ButtonAttributes() {
          string buttonAttrs = "";

          if (this.StepButtonType == StepButtonType.SeeMore) {
            buttonAttrs = "aria-expanded=\"false\"";
          }
          return buttonAttrs;
        }


        public string GetButtonHtml => $@"<button class=""btn {ButtonClass()}"" {ButtonAttributes()}>{this.Text}</button>";

      }

      public class ProductInfo {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal? Price { get; set; }
        public int? SubscriptionId { get; set; }

        public ProductInfo(string productId, string name, string description, decimal? price, int? subscriptionId) {
          this.ProductId = productId;
          this.Name = name;
          this.Description = description;
          this.Price = price;
          this.SubscriptionId = subscriptionId;
        }
      }

      #endregion Models


      #region Business Logic
      // Only public function necessary to retrive the whole business logic and HTML
      // To add another panel, add it to the StepPanelType enum and the GetPanelTabsForUser() method
      public static string GetPurchaseServicesForUser(List<DbHelper.Products.ProductInfo> productList) {
        return GetStepsLayoutHtml(StepWizardPurposeEnum.ParticipantSubscription, productList);
      }

      public static string GetQuickParticipantWizard(List<DbHelper.Products.ProductInfo> productList, List<CustomStepPanel> customStepPanels) {
        // Used in Modal to quickly add a Participant
        return GetStepsLayoutHtml(StepWizardPurposeEnum.QuickAddingParticipants, productList, customStepPanels);
      }

      private static string GetStepsLayoutHtml(StepWizardPurposeEnum stepWizardPurpose, List<DbHelper.Products.ProductInfo> productList, List<CustomStepPanel> customStepPanels = null) {

        var stepPanelTabs = GetPanelTabsForUser(stepWizardPurpose);
        if (stepPanelTabs == null) return "";

        var stepPanelInfoList = GetStepPanelInfoList(stepWizardPurpose, stepPanelTabs, productList, customStepPanels);
        var stepTabInfoList = stepPanelTabs.Select(tab => new StepTab(tab)).ToList();

        if (stepPanelInfoList.IsNullOrEmpty() || stepTabInfoList.IsNullOrEmpty()) return "";

        return $@"
          <form class=""{GetFormClassForWizardUse(stepWizardPurpose)}"">
            <div class=""purchase-services-container"">
              <div class=""purchase-step-container"">
                {string.Join("", stepTabInfoList.Select((tab, index) => $"<div class=\"purchase-step {(index == 0 ? "active" : "")}\" id=\"{tab.StepType}\">{tab.Title}</div>"))}
              </div>
              {string.Join("", stepPanelInfoList.Select(step => step?.StepPanelHtml))}
            </div>
          </form>";
      }

      private static string GetFormClassForWizardUse(StepWizardPurposeEnum stepWizardPurpose) {
        if (stepWizardPurpose == StepWizardPurposeEnum.ParticipantSubscription) {
          return Public_CSS.PurchaseForm;

        } else if (stepWizardPurpose == StepWizardPurposeEnum.QuickAddingParticipants) {
          return Public_CSS.ParticipantForm;
        }

        return "";
      }

      public static List<StepPanelType> GetPanelTabsForUser(StepWizardPurposeEnum stepWizardPurpose) {
        if (stepWizardPurpose == StepWizardPurposeEnum.ParticipantSubscription) {
          return new List<StepPanelType> { StepPanelType.Subscription, StepPanelType.Coaching, StepPanelType.Payment, StepPanelType.Summary };

        } else if (stepWizardPurpose == StepWizardPurposeEnum.QuickAddingParticipants) {
          return new List<StepPanelType> { StepPanelType.ParticipantInformation, StepPanelType.Subscription, StepPanelType.Coaching, StepPanelType.ParticipantSettings };
        }

        return null;
      }

      private static List<StepPanelInfo> GetStepPanelInfoList(StepWizardPurposeEnum stepWizardPurpose, List<StepPanelType> stepTabs, List<DbHelper.Products.ProductInfo> products, List<CustomStepPanel> customStepPanels) {
        if (stepTabs.IsNullOrEmpty()) return null;

        if (products == null || products.IsNullOrEmpty()) return null;

        return stepTabs.Select((step, i) => GetStepPanelInfo(stepWizardPurpose, step, i, stepTabs, products, customStepPanels)).Where(info => info != null).ToList();
      }

      private static StepPanelInfo GetStepPanelInfo(StepWizardPurposeEnum stepWizardPurpose, StepPanelType step, int index, List<StepPanelType> stepTabs, List<DbHelper.Products.ProductInfo> products, List<CustomStepPanel> customStepPanels) {

        var config = GetStepConfiguration(step, customStepPanels);

        if (config.PanelContainsProducts && !products.IsNullOrEmpty()) {
          var filteredProducts = products.Where(p => !p.IsHidden && p.UserCanPurchase && config.CategoryFilter(p.ProductCategoryId)).ToList();
          foreach (var prod in filteredProducts) {
            config.Products.Add(new ProductInfo(prod.ProductId.ToString(), prod.UserPurchaseTitle, prod.UserPurchaseDescription, prod.DefaultUnitPrice, prod.SubscriptionId));
          }
        }

        return new StepPanelInfo(
          stepWizardPurpose: stepWizardPurpose,
          stepPanelType: step,
          isVisible: index == 0, // Is visible ONLY if it's the first panel
          nextStepPanelType: index == stepTabs.Count - 1 ? StepPanelType.None : stepTabs[index + 1],
          previousStepPanelType: index == 0 ? StepPanelType.None : stepTabs[index - 1],
          configuration: config
        );
      }

      private static StepConfiguration GetStepConfiguration(StepPanelType step, List<CustomStepPanel> customStepPanels) {
        var stepConfig = new StepConfiguration();

        if (!customStepPanels.IsNullOrEmpty()) {
          var customStep = customStepPanels.Find(x => x.StepPanelType == step);
          if (customStep != null) {
            stepConfig.CustomContentHtml = customStep.StepContentHtml;
            customStepPanels.Remove(customStep);
          }
        }

        // Define configuration for each panel, what's global/applies for all panels set outside conditionals.
        // If a value depends on another, set value last.
        if (step == StepPanelType.Subscription) {

          stepConfig.CategoryFilter = id => id == (int)DbHelper.Products.ProductCategory.Subscription;

        } else if (step == StepPanelType.Coaching) {

          stepConfig.CategoryFilter = id => id == (int)DbHelper.Products.ProductCategory.CoachingIndividual || id == (int)DbHelper.Products.ProductCategory.CoachingOnline;
          stepConfig.Layout = StepPanelBoxLayout.Row;
          stepConfig.FoldDescription = true;
          stepConfig.Products.Add(new ProductInfo(CustomProductId.NoCoaching, "No Coaching", "", null, null));
        }

        // Always get last
        stepConfig.StepPanelBoxLayoutCSS = GetStepPanelBoxLayoutClass(stepConfig.Layout);
        stepConfig.PanelContainsProducts = PanelsContainingProducts.Contains(step);

        return stepConfig;
      }

      private static string GetStepPanelBoxLayoutClass(StepPanelBoxLayout layout) {
        if (layout == StepPanelBoxLayout.Grid) {
          return "step-card-grid";
        } else if (layout == StepPanelBoxLayout.Row) {
          return "step-card-row";
        }
        return "";
      }

      #endregion Business Logic
    }
  }
}
