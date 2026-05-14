using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class Products {
      public enum ProductCategory : int {
        CoachingIndividual = 1,
        CoachingOnline = 2,
        GroupSessions = 3,
        Consulting = 4,
        RTO = 5,
        Diagnostic = 6,
        Other = 7,
        Subscription = 8
      }

      public static List<ProductInfo> GetAllProducts() {
        var products = new List<ProductInfo>();

        Common.Query($@"
          SELECT p.ProductId, p.ProductCategoryId, pc.CategoryName, p.SubCategory, p.DisplayTitle, p.DefaultUnitPrice,
            p.ProductDescription, p.IsHidden, p.IsFixedDescription, p.MinAllowedQuotePrice, p.QuoteComponentWarningMessage,
            p.SubscriptionId, p.CoachingTypeId, p.SessionQty as CoachingSessionQuantity,
            p.UserPurchaseTitle, p.UserPurchaseDescription, p.UserCanPurchase, p.RequiresSubscription, p.IsQuantityPerPerson
            FROM al_ProductCategory pc
            INNER JOIN al_Product p ON pc.ProductCategoryId = p.ProductCategoryId
            ORDER BY pc.CategoryName, p.SubCategory, p.DisplayTitle",
          null,
          dr => {
            products.Add(new ProductInfo(
              dr.GetInt("ProductId"),
              dr.GetInt("ProductCategoryId"),
              dr.GetString("CategoryName"),
              dr.GetString("SubCategory"),
              dr.GetString("DisplayTitle"),
              dr.GetString("ProductDescription"),
              dr.GetDecimalOrDefault("DefaultUnitPrice", 0),
              dr.GetBoolFromInt("IsHidden"),
              dr.GetBoolFromInt("IsFixedDescription"),
              dr.GetDecimalOrDefault("MinAllowedQuotePrice", 0),
              dr.GetString("QuoteComponentWarningMessage"),
              dr.GetIntOrNull("SubscriptionId"),
              dr.GetIntOrNull("CoachingTypeId"),
              dr.GetIntOrNull("CoachingSessionQuantity"),
              dr.GetString("UserPurchaseTitle"),
              dr.GetString("UserPurchaseDescription"),
              dr.GetBoolFromInt("UserCanPurchase"),
              dr.GetBoolFromInt("RequiresSubscription"),
              dr.GetBoolFromInt("IsQuantityPerPerson")
            ));
          }
        );
        return products;
      }

      public class ProductInfo {
        public int ProductId { get; private set; }
        public int ProductCategoryId { get; private set; }
        public string CategoryName { get; set; }
        public string SubCategory { get; set; }
        public string DisplayTitle { get; set; }
        public string ProductDescription { get; set; }
        public decimal? DefaultUnitPrice { get; set; }
        public bool IsHidden { get; private set; }
        public bool IsFixedDescription { get; private set; }
        public decimal MinAllowedQuotePrice { get; private set; }
        public string QuoteComponentWarningMessage { get; private set; }
        public int? SubscriptionId { get; private set; }
        public int? CoachingTypeId { get; private set; }
        public int? CoachingSessionQuantity { get; private set; }
        public string UserPurchaseTitle { get; private set; }
        public string UserPurchaseDescription { get; private set; }
        public bool UserCanPurchase { get; private set; }
        public bool RequiresSubscription { get; private set; }
        public bool IsQuantityPerPerson { get; private set; }
        public ProductInfo(int productId, int productCategoryId, string categoryName, string subCategory, string displayTitle, string productDescription,
          decimal? defaultUnitPrice, bool isHidden, bool isFixedDescription, decimal minAllowedQuotePrice, string quoteComponentWarningMessage,
          int? subscriptionId, int? coachingTypeId, int? coachingSessionQuantity, string userPurchaseTitle, string userPurchaseDescription, bool userCanPurchase, bool requiresSubscription, bool isQuantityPerPerson) {
          this.ProductId = productId;
          this.ProductCategoryId = productCategoryId;
          this.CategoryName = categoryName;
          this.SubCategory = subCategory;
          this.DisplayTitle = displayTitle;
          this.ProductDescription = productDescription;
          this.DefaultUnitPrice = defaultUnitPrice;
          this.IsHidden = isHidden;
          this.IsFixedDescription = isFixedDescription;
          this.MinAllowedQuotePrice = minAllowedQuotePrice;
          this.QuoteComponentWarningMessage = quoteComponentWarningMessage;
          this.SubscriptionId = subscriptionId;
          this.CoachingTypeId = coachingTypeId;
          this.CoachingSessionQuantity = coachingSessionQuantity;
          this.UserPurchaseTitle = userPurchaseTitle;
          this.UserPurchaseDescription = userPurchaseDescription;
          this.UserCanPurchase = userCanPurchase;
          this.RequiresSubscription = requiresSubscription;
          this.IsQuantityPerPerson = isQuantityPerPerson;
        }

        public bool IsSubscription => this.ProductCategoryId == (int)ProductCategory.Subscription;
      }
    }
  }
}
