using Integral.Web.Services;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class QuoteInfo : AppCode.PageBaseClasses.QuotePageBase {

    public const string UserSearchQueryKey = "UserSearch";
    public const string RtnShowTabKey = "ShowTab";
    public const string ProductOptionValueForNote = "note";
    const bool AddToFreshSales_DefaultValue = true;
    public const string WarningMessage_MissingSubscription = "The product you selected comes with a learner subscription. Check if you require a subscription for the component you have selected, and ensure the component description and participant quantities are correct.";
    public const string ProductRequiresSubscriptionMsg = "This Product comes with a learner subscription";

    public bool CanEditFreshSalesOption, CanEditQuoteBranding, CanViewQuoteBranding, IsClientView;
    public DbHelper.AbleUser.AbleUserInfo QuoteContactUserInfo = null;
    public List<DbHelper.PlatformService.ServiceInfo> PlatformServiceList;
    public List<DbHelper.PlatformService.ServiceInfo> PlatformServicesForQuote;
    public List<DbHelper.ClientCompanies.BriefCompanyInfo> CompanyList;
    public List<DbHelper.TenantOrg.TenantOrgInfo> BrandingOrgs;
    public List<DbHelper.AbleQuotes.QuoteDealSource> QuoteDealSources;
    public List<DbHelper.AbleQuotes.QuoteSalesContentType> SalesContentTypes;
    public List<DbHelper.AbleQuotes.QuoteSalesContentUrl> SalesContentUrls;
    public List<DbHelper.Products.ProductInfo> ProductOptionsForQuote;
    public List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerList;

    public bool IsExistingClient = false;
    public string UpdateMessage = "";
    public int pageTabCounter = 0;
    public bool QuoteInfoSaved = false, CanEditQuoteProject, IsUpdatingAcceptedQuote, CanViewHiddenPartners, CanViewInactivePartners, CanUpdateExcludeFromSalesIncentive;
    public bool CanViewQuoteComponentPrice, CanViewQuoteSplits, CanEditQuoteSplits;
    public PathHelper.QuoteTabEnum SelectedQuoteTab;

    private readonly int QuoteStatusId_Accepted = DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId;

    public class AjaxAction {

      public const string Upload = "upload";
      public const string UploadRevert = "uploadrevert";

      public const string Copy = "copy";
      public const string Delete = "delete";
      public const string GetIsExistingClient = "GetIsExistingClient";
      public const string GetProdWarningMgs = "GetProdWarningMgs";

      public const string UpdateAcceptedQuote = "UpdateAcceptedQuote";
      public const string Update_Project_Tab = "Update_Project_Tab";
      public const string Update_Settings_Tab = "Update_Settings_Tab";
      public const string Update_Splits_Tab = "Update_Splits_Tab";
      public const string Update_Components_Tab = "Update_Components_Tab";
      public const string Update_CoverLetter_Tab = "Update_CoverLetter_Tab";
      public const string Update_Info_Tab = "Update_Info_Tab";
    }

    public class FormFields {
      public const string QuoteTitle = "QuoteTitle";
      public const string QuoteGUID = "QuoteGUID";
      public const string ActiveTab = "ActiveTab";
      public const string CompanyId = "CompanyId";
      public const string CompanyName = "CompanyName";
      public const string ClientLeadUserId = "ClientLeadUserId";
      public const string TBAJobNumber = "TBAJobNumber";
      public const string ProjectJobNumber = "ProjectJobNumber";
      public const string ProjectName = "ProjectName";
      public const string QuoteStatusId = "QuoteStatusId";
      public const string OwnerUserId = "OwnerUserId";
      public const string ContactUserId = "ContactUserId";
      public const string ContactFirstName = "ContactFirstName";
      public const string ContactLastName = "ContactLastName";
      public const string ContactEmail = "ContactEmail";
      public const string ContactRole = "ContactRole";
      public const string ContactPhone = "ContactPhone";
      public const string ContactCity = "ContactCity";
      public const string EstimatedStartDateLocal = "EstimatedStartDateLocal";
      public const string GSTApplicable = "GSTApplicable";
      public const string ExcludeFromSalesIncentive = "ExcludeFromSalesIncentive";
      public const string AddToFreshSales = "AddToFreshSales";
      public const string QwilrUrl = "QwilrUrl";
      public const string QwilrPDFUrl = "QwilrPDFUrl";
      public const string OPPPercentage = "OPPPercentage";
      public const string PLCPercentage = "PLCPercentage";
      public const string DeliveryPercentage = "DeliveryPercentage";
      public const string ServicesTotalPercentage = "ServicesTotalPercentage";
      public const string SplitsTotalPercentage = "SplitsTotalPercentage";
      public const string ProposalDesignerPercentage = "ProposalDesignerPercentage";
      public const string CoverLetterHtml = "CoverLetterHtml";
      public const string TeamUserIds = "TeamUserId";
      public const string ProdKey_Prefix = "Prod";
      public const string ProdKey_IsNote = "_IsNote";
      public const string ProdKey_Id = "_Id";
      public const string ProdKey_Name = "_Name";
      public const string ProdKey_Optional = "_Optional";
      public const string ProdKey_Price = "_Amt";
      public const string ProdKey_MinAllowedQuotePrice = "_MinAllowedQuotePrice";
      public const string ProdKey_Qty = "_Qty";
      public const string ProdKey_QtyDescr = "_QtyDescr";
      public const string ProdKey_RequiresSubscription = "_RequiresSubscription";
      public const string ProdKey_DefaultSubscriptionId = "_DefaultSubscriptionId";
      public const string ProdKey_IsQuantityPerPerson = "_IsQuantityPerPerson";
      public const string ProdKey_IsSubscription = "_IsSubscription";
      public const string CopyQuoteTitle = "CopyQuoteTitle";
      public const string PlatformServiceIds = "PlatformServiceIds";
      public const string BrandingOrgId = "BrandingOrgId";
      public const string QuoteNotes = "QuoteNotes";
      public const string ProposalDesignerUserId = "ProposalDesignerUserId";
      public const string LeadConsultantUserId = "LeadConsultantUserId";
      public const string PlatformFee_CoordinationSupport = "PlatformFee_CoordinationSupport";
      public const string PlatformFee_RTO = "PlatformFee_RTO";
      public const string QuoteDealSourceId = "QuoteDealSourceId";
      public const string QuoteItem_SubscriptionId = "QuoteItem_SubscriptionId";
      public const string QuoteItem_Subscription_Description = "QuoteItem_Subscription_Description";
      public const string QuoteItem_Subscription_Quantity = "QuoteItem_Subscription_Quantity";
      public const string QuoteItem_Subscription_UnitPrice = "QuoteItem_Subscription_UnitPrice";
      public const string QuoteSalesContentTypeId = "QuoteSalesContentTypeId";
      public const string QuoteSalesContentUrlId = "QuoteSalesContentUrlId";
      public const string QuoteSalesContentPDFFileName = "QuoteSalesContentPDFFileName";
      public const string QuoteSalesContentWebPageUrl = "QuoteSalesContentWebPageUrl";
    }

    public class FormValues {
      public string QuoteTitle;
      public Guid QuoteGUID;
      public string ActiveTab;
      public bool IsNewCompany;
      public int CompanyId;
      public string CompanyName;
      public int? ClientLeadUserId;
      public bool IsNewProject;
      public string ProjectJobNumber;
      public string ProjectName;
      public int QuoteStatusId;
      public bool IsNewContact;
      public int OwnerUserId;
      public int? LeadConsultantUserId;
      public int? ProposalDesignerUserId;
      public int ContactUserId;
      public string ContactFirstName;
      public string ContactLastName;
      public string ContactEmail;
      public string ContactRole;
      public string ContactPhone;
      public string ContactCity;
      public DateTime EstimatedStartDateLocal;
      public bool GSTApplicable;
      public bool ExcludeFromSalesIncentive;
      public bool AddToFreshSales;
      public string QwilrUrl;
      public string QwilrPDFUrl;
      public decimal OPPPercentage;
      public decimal PLCPercentage;
      public decimal DeliveryPercentage;
      public decimal PlatformPercentage;
      public decimal ProposalDesignerPercentage;
      public string CoverLetterHtml;
      public List<int> TeamUserIds;
      public List<QuoteItem> QuoteItems;
      public bool QuoteItems_MissingSubscription;
      public int? QuoteItems_RequiredSubscription_ProductId;
      public int? QuoteItems_RequiredSubscription_ProductId_Quantity;
      public List<int> PlatformServiceIds;
      public int? BrandingOrgId;
      public string QuoteNotes;
      public int? QuoteDealSourceId;
      public int? QuoteSalesContentTypeId;
      public int? QuoteSalesContentUrlId;
      public string QuoteSalesContentPDFFileName;
      public string QuoteSalesContentWebPageUrl;

      public class QuoteItem {
        public bool IsNote;
        public int ProductId;
        public string ItemDescription;
        public DbHelper.AbleQuotes.OptionalEnum OptionalInfo;
        public decimal UnitPrice;
        public decimal Quantity;
        public string QuantityDescr;
        public bool IsQuantityPerPerson;
        public bool RequiresSubscription;
        public int? DefaultSubscriptionId;
        public bool IsSubscription;
      }
    }

    // Note data- attributes must be all lowercase.
    public class DataAttrs {
      public const string RequiredIds = "requiredids";
      public const string Percent = "percent";
      public const string AlwaysRequired = "alwaysrequired";
      public const string PlatFee = "platfee";
      public const string TotalTableSum = "totaltablesum";
      public const string TargetFormClass = "targetformclass";
      public const string ClientLeadUserInfo_Class = "clientLeadUserInfo";
      public const string IsQuantityPerPerson = "isquantityperperson";
      public const string IsFixedDescription = "isfixeddescription";
      public const string SubscriptionId = "subscriptionid";
      public const string SubscriptionProductId = "subprodid";
      public const string RequiresSubscription = "requiressubscription";
      public const string IsSubscription = "issubscription";
    }

    public class AjaxReturnData {
      public const string NewCompanyId = "CompanyId";
      public const string IsExistingClient = "IsExistingClient";
      public const string ProdWarningMsgs = "ProdWarningMsgs";
      public const string QuoteItems_MissingSubscription = "QuoteItems_MissingSubscription";
      public const string QuoteItems_RequiredSubscription_ProductId = "QuoteItems_RequiredSubscription_ProductId";
      public const string QuoteItems_RequiredSubscription_ProductId_Quantity = "QuoteItems_RequiredSubscription_ProductId_Quantity";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Quote Details";

      CompanyList = DbHelper.ClientCompanies.GetCompanyList(SessionHelper.UserInfo);
      BrandingOrgs = DbHelper.TenantOrg.GetQuoteBrandingOrgs(SessionHelper.UserInfo, QuoteInfo.BrandingOrgId);
      QuoteDealSources = DbHelper.AbleQuotes.GetQuoteDealSources();
      SalesContentTypes = DbHelper.AbleQuotes.GetSalesContentTypes();
      SalesContentUrls = DbHelper.AbleQuotes.GetSalesContentUrls(QuoteInfo.TenantOrgId);
      ProductOptionsForQuote = DbHelper.Products.GetAllProducts();
      PartnerList = DbHelper.AlbertCoaches.GetCoachInfoList(false, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

      CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners();
      CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners();
      CanUpdateExcludeFromSalesIncentive = SessionHelper.AppAccess.Quotes.CanUpdateExcludeFromSalesIncentive();
      CanViewQuoteComponentPrice = IsNewQuote ? true : SessionHelper.AppAccess.Quotes.CanViewQuoteComponentPrice(QuoteInfo);
      CanViewQuoteSplits = IsNewQuote ? true : SessionHelper.AppAccess.Quotes.CanViewQuoteSplits(QuoteInfo);
      CanEditQuoteSplits = IsNewQuote ? true : SessionHelper.AppAccess.Quotes.CanEditQuoteSplits(QuoteInfo);
      CanEditFreshSalesOption = SessionHelper.AppAccess.Quotes.CanEditFreshSalesOption();
      CanEditQuoteBranding = SessionHelper.AppAccess.Quotes.CanEditQuoteBranding(QuoteInfo);
      CanViewQuoteBranding = SessionHelper.AppAccess.Quotes.CanViewQuoteBranding();
      CanEditQuoteProject = SessionHelper.AppAccess.Quotes.CanEditQuoteProject(QuoteInfo, IsQuoteFromProjectArea);

      QuoteInfoSaved = QuoteInfo.PublicGuid != Guid.Empty;
      IsClientView = SessionHelper.IsUserRoleClient;
      pageTabCounter = 0;
      IsUpdatingAcceptedQuote = CanEditQuoteProject && CanChangeSplitRoles && !CanEditQuote;
      SelectedQuoteTab = PathHelper.QuoteTabEnum.project;

      // Get master platform service list, and which ones are assigned to this quote.
      PlatformServiceList = DbHelper.PlatformService.GetAllServices();

      if (IsNewQuote) {

        PlatformServicesForQuote = null; // New quote, nothing assigned.
        IsExistingClient = false;
        QuoteInfo.DeliveryPercentage = ConfigHelper.Financial.DefaultQuoteDeliveryPercentage;

      } else {

        PlatformServicesForQuote = DbHelper.PlatformService.GetServicesForQuote(QuoteInfo.QuoteId);
        IsExistingClient = QuoteInfo.CompanyInfo == null ? false : GetIsExistingClient(QuoteInfo.CompanyInfo.CompanyId);

        if (QuoteInfo.IsAccepted) {
          // Don't display quote items that were not accepted.
          QuoteInfo.QuoteItems.RemoveAll(qi => qi.IsAccepted != true);
        }

        // Reads if URL contains a specific tab to redirect to, this variable is read in jQuery
        string pageTabQuery = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PageTab, "");
        if (!Enum.TryParse(pageTabQuery, true, out SelectedQuoteTab)) SelectedQuoteTab = PathHelper.QuoteTabEnum.project;
      }

      // Remove services with Hidden flag if they are not already included in the Quote.
      PlatformServiceList.RemoveAll(s => s.IsHidden && (PlatformServicesForQuote == null || !PlatformServicesForQuote.Exists(sq => sq.PlatformServiceId == s.PlatformServiceId)));

      if (QuoteInfo.ContactUserId > 0) {
        // Note these can include unregistered users.
        QuoteContactUserInfo = DbHelper.AbleUser.GetUserByIdOrNull(QuoteInfo.ContactUserId, DbHelper.AbleUser.RegisteredFilter.Any);
      }

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          if (Request.Files?.Count == 1) {
            UploadPDF();
            WebHelper.EndRequest();
            return;
          }

          ajax.ClearBadFields();

          switch (PageAjaxAction) {

            case AjaxAction.Update_Project_Tab:
              if (CanEditQuote) {
                Update_Project_Tab(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;

            case AjaxAction.Update_Settings_Tab:
              if (CanEditQuote) {
                Update_Settings_Tab(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;

            case AjaxAction.Update_Splits_Tab:
              if (CanEditQuote) {
                Update_Splits_Tab(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;

            case AjaxAction.Update_Components_Tab:
              if (CanEditQuote) {
                Update_Components_Tab(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;

            case AjaxAction.Update_CoverLetter_Tab:
              if (CanEditQuote) {
                Update_CoverLetter_Tab(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;

            case AjaxAction.Update_Info_Tab:
              if (CanEditQuote) {
                Update_Info_Tab(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;

            case AjaxAction.Copy:
              if (CanCopyQuote) {
                CopyQuote(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;

            case AjaxAction.Delete:
              if (CanDeleteQuote) {
                DeleteQuote(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;

            case AjaxAction.GetIsExistingClient:
              AjaxGetIsExistingClient(ajax);
              break;

            case AjaxAction.GetProdWarningMgs:
              GetProductsWarningMsgs(ajax);
              break;

            case AjaxAction.UpdateAcceptedQuote:
              if (CanEditQuoteProject || CanEditQuoteSplits) {
                UpdateAcceptedQuote(ajax);
              } else {
                ajax.AddDialogMessage("Permission Denied");
              }
              break;
          }
        });
        return;

      } else {

        var userSearchQuery = WebHelper.GetQueryStringValue(UserSearchQueryKey);

        if (!userSearchQuery.IsNullOrEmpty() && CanEditQuote) {
          WebHelper.WriteAndEnd(GetUserSearchResultJson(userSearchQuery), WebHelper.HttpContentType.json);
          return;
        }
      }

      // If creating a new quote for a specific Project, add Company info to new quote.
      if (IsQuoteFromProjectArea && ProjectInfo != null) {
        QuoteInfo.JobNumber = ProjectInfo.JobNumber;
        if (ProjectInfo.CompanyId != null) {
          var projectCompany = DbHelper.ClientCompanies.GetBriefCompanyInfoOrNull(ProjectInfo.CompanyId.Value, SessionHelper.UserInfo);
          QuoteInfo.SetCompanyInfo(projectCompany);
        }
      }
    }

    void UploadPDF() {

      if (Request?.Files.Count != 1) return;
      var file = Request.Files[0];
      if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) {
        throw new Exception("Only PDF files are allowed.");
      }

      PathHelper.PDF.SavePDFToFile(QuoteInfo, file);
    }

    void Update_Project_Tab(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();

      if (!GetFormValues_ProjectInfo(ajax, formValues) || !GetFormValues_Contact(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.project);
        return;
      }

      bool quoteUpdated = false;

      // Update/Create Quote
      if (IsNewQuote) {
        quoteUpdated = CreateQuote(formValues, ajax);
      } else {
        quoteUpdated = UpdateQuote_Project(formValues, ajax);
      }

      if (quoteUpdated && QuoteInfo != null) {
        // Success.
        UpdateMessage = $"{(IsNewQuote ? "Quote Created!" : "Quote Updated.")}{(IsNewQuote ? "" : UpdateMessage.EnsureStartsWith("<br/><br/>", true))}";

        if (IsNewQuote) {

          ajax.SetRedirectUrl(PathHelper.Pages.QuoteDetails(QuoteInfo.PublicGuid, PathHelper.QuoteTabEnum.settings), UpdateMessage, AjaxSubmitHelper.PageMessageType.SuccessToast);
        } else {
          // Reload QuoteInfo to get updated totals
          QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(QuoteInfo.QuoteId);

          // TODO: Temporarily disabled - creating too many events at the moment
          // Send Intercom event for quote update
          //SendEvent(
          //  intercom => intercom.QuoteUpdated()
          //    .FromSession()
          //    .WithQuote(QuoteInfo.QuoteId, QuoteInfo.QuoteTitle)
          //    .WithClientCompany(QuoteInfo.CompanyId, QuoteInfo.CompanyName ?? "")
          //    .WithQuoteValue(QuoteInfo.QuoteItemTotalAmount),
          //  operationName: "QuoteInfo_Project_QuoteUpdated",
          //  requestRawUrl: SystemWeb.RequestRawUrl,
          //  telemetryProperties: new Dictionary<string, object> {
          //    ["QuoteId"] = QuoteInfo.QuoteId,
          //    ["QuoteValue"] = QuoteInfo.QuoteItemTotalAmount
          //  }
          //);

          ajax.AddSuccessToast(UpdateMessage);
        }
      } else {
        ajax.AddErrorToast(IsNewQuote ? "Couldn't create quote." : "Couldn't update quote");
      }
      return;
    }

    void Update_Settings_Tab(AjaxSubmitHelper ajax) {
      var formValues = new FormValues();

      if (!GetFormValues_Settings(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.settings);
        return;
      }

      bool updated = UpdateQuote_Settings(formValues);

      if (updated) {
        // Reload QuoteInfo to get updated totals
        QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(QuoteInfo.QuoteId);

        // TODO: Temporarily disabled - creating too many events at the moment
        // Send Intercom event for quote update
        //SendEvent(
        //  intercom => intercom.QuoteUpdated()
        //    .FromSession()
        //    .WithQuote(QuoteInfo.QuoteId, QuoteInfo.QuoteTitle)
        //    .WithClientCompany(QuoteInfo.CompanyId, QuoteInfo.CompanyName ?? "")
        //    .WithQuoteValue(QuoteInfo.QuoteItemTotalAmount),
        //  operationName: "QuoteInfo_Settings_QuoteUpdated",
        //  requestRawUrl: SystemWeb.RequestRawUrl,
        //  telemetryProperties: new Dictionary<string, object> {
        //    ["QuoteId"] = QuoteInfo.QuoteId,
        //    ["QuoteValue"] = QuoteInfo.QuoteItemTotalAmount
        //  }
        //);

        ajax.AddSuccessToast("Quote Updated.");
      } else {
        ajax.AddErrorToast("Couldn't update quote");
      }
      return;
    }

    void Update_Splits_Tab(AjaxSubmitHelper ajax) {
      var formValues = new FormValues();

      if (!GetFormValues_Splits(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.splits);
        return;
      }

      bool updated = UpdateQuote_Splits(formValues, ajax);

      if (updated) {
        // Reload QuoteInfo to get updated totals
        QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(QuoteInfo.QuoteId);

        // TODO: Temporarily disabled - creating too many events at the moment
        // Send Intercom event for quote update
        //SendEvent(
        //  intercom => intercom.QuoteUpdated()
        //    .FromSession()
        //    .WithQuote(QuoteInfo.QuoteId, QuoteInfo.QuoteTitle)
        //    .WithClientCompany(QuoteInfo.CompanyId, QuoteInfo.CompanyName ?? "")
        //    .WithQuoteValue(QuoteInfo.QuoteItemTotalAmount),
        //  operationName: "QuoteInfo_Splits_QuoteUpdated",
        //  requestRawUrl: SystemWeb.RequestRawUrl,
        //  telemetryProperties: new Dictionary<string, object> {
        //    ["QuoteId"] = QuoteInfo.QuoteId,
        //    ["QuoteValue"] = QuoteInfo.QuoteItemTotalAmount
        //  }
        //);

        ajax.AddSuccessToast("Quote Updated.");
      } else {
        ajax.AddErrorToast("Couldn't update quote");
      }
      return;
    }

    void Update_Components_Tab(AjaxSubmitHelper ajax) {
      var formValues = new FormValues();

      if (!GetFormValues_Components(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.components);
        return;
      }

      bool updated = UpdateQuote_Components(formValues, ajax);

      if (updated) {
        // Reload QuoteInfo to get updated totals
        QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(QuoteInfo.QuoteId);

        // TODO: Temporarily disabled - creating too many events at the moment
        // Send Intercom event for quote update
        //SendEvent(
        //  intercom => intercom.QuoteUpdated()
        //    .FromSession()
        //    .WithQuote(QuoteInfo.QuoteId, QuoteInfo.QuoteTitle)
        //    .WithClientCompany(QuoteInfo.CompanyId, QuoteInfo.CompanyName ?? "")
        //    .WithQuoteValue(QuoteInfo.QuoteItemTotalAmount),
        //  operationName: "QuoteInfo_Components_QuoteUpdated",
        //  requestRawUrl: SystemWeb.RequestRawUrl,
        //  telemetryProperties: new Dictionary<string, object> {
        //    ["QuoteId"] = QuoteInfo.QuoteId,
        //    ["QuoteValue"] = QuoteInfo.QuoteItemTotalAmount
        //  }
        //);

        if (formValues.QuoteItems_MissingSubscription) {
          ajax.AddReturnValue(AjaxReturnData.QuoteItems_MissingSubscription, formValues.QuoteItems_MissingSubscription.ToJSTrueFalse().ToString());
          ajax.AddReturnValue(AjaxReturnData.QuoteItems_RequiredSubscription_ProductId, formValues.QuoteItems_RequiredSubscription_ProductId);
          ajax.AddReturnValue(AjaxReturnData.QuoteItems_RequiredSubscription_ProductId_Quantity, formValues.QuoteItems_RequiredSubscription_ProductId_Quantity.GetValueOrDefault(1));

        } else {
          // If MissingSubscription is true, Subscription will be Automatically added. Avoid showing Toast twice.
          ajax.AddSuccessToast("Quote Updated.");
        }
      } else {
        ajax.AddErrorToast("Couldn't update quote");
      }
      return;
    }

    void Update_CoverLetter_Tab(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();
      formValues.CoverLetterHtml = WebHelper.GetFormValue(FormFields.CoverLetterHtml);
      if (ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.coverLetter);
        return;
      }

      bool updated = UpdateQuote_CoverLetter(formValues);

      if (updated) {
        // Reload QuoteInfo to get updated totals
        QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(QuoteInfo.QuoteId);

        // TODO: Temporarily disabled - creating too many events at the moment
        // Send Intercom event for quote update
        //SendEvent(
        //  intercom => intercom.QuoteUpdated()
        //    .FromSession()
        //    .WithQuote(QuoteInfo.QuoteId, QuoteInfo.QuoteTitle)
        //    .WithClientCompany(QuoteInfo.CompanyId, QuoteInfo.CompanyName ?? "")
        //    .WithQuoteValue(QuoteInfo.QuoteItemTotalAmount),
        //  operationName: "QuoteInfo_CoverLetter_QuoteUpdated",
        //  requestRawUrl: SystemWeb.RequestRawUrl,
        //  telemetryProperties: new Dictionary<string, object> {
        //    ["QuoteId"] = QuoteInfo.QuoteId,
        //    ["QuoteValue"] = QuoteInfo.QuoteItemTotalAmount
        //  }
        //);

        ajax.AddSuccessToast("Quote Updated.");
      } else {
        ajax.AddErrorToast("Couldn't update quote");
      }
      return;
    }

    void Update_Info_Tab(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();
      formValues.QuoteNotes = WebHelper.GetFormValue(FormFields.QuoteNotes);
      if (ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.info);
        return;
      }

      bool updated = UpdateQuote_Info(formValues);

      if (updated) {
        // Reload QuoteInfo to get updated totals
        QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(QuoteInfo.QuoteId);

        // TODO: Temporarily disabled - creating too many events at the moment
        // Send Intercom event for quote update
        //SendEvent(
        //  intercom => intercom.QuoteUpdated()
        //    .FromSession()
        //    .WithQuote(QuoteInfo.QuoteId, QuoteInfo.QuoteTitle)
        //    .WithClientCompany(QuoteInfo.CompanyId, QuoteInfo.CompanyName ?? "")
        //    .WithQuoteValue(QuoteInfo.QuoteItemTotalAmount),
        //  operationName: "QuoteInfo_Info_QuoteUpdated",
        //  requestRawUrl: SystemWeb.RequestRawUrl,
        //  telemetryProperties: new Dictionary<string, object> {
        //    ["QuoteId"] = QuoteInfo.QuoteId,
        //    ["QuoteValue"] = QuoteInfo.QuoteItemTotalAmount
        //  }
        //);

        ajax.AddSuccessToast("Quote Updated.");
      } else {
        ajax.AddErrorToast("Couldn't update quote");
      }
      return;
    }

    public int GetNextTabNumber() => ++pageTabCounter;

    // If any of the accepted quotes for a company has an admin as an owner,
    // then we count this as an "existing client" for the purposes of the PlatformServices list.
    bool GetIsExistingClient(int companyId) {
      bool rtn = DbHelper.AbleQuotes.IsCompanyExistingClient(companyId);
      return rtn;
    }

    void AjaxGetIsExistingClient(AjaxSubmitHelper ajax) {
      int companyId = WebHelper.GetFormValueIntOrDefault(FormFields.CompanyId, 0);
      ajax.AddReturnValue(AjaxReturnData.IsExistingClient, GetIsExistingClient(companyId));
    }

    public List<WebHelper.SelectOption> GetProjectTopOptions() {

      if (IsUpdatingAcceptedQuote) {
        return new List<WebHelper.SelectOption>() {
          new WebHelper.SelectOption("", "[Select Project]")
        };

      } else {
        return new List<WebHelper.SelectOption>() {
          new WebHelper.SelectOption("", "[Select or add Project]"),
          new WebHelper.SelectOption(PathHelper.AbleUrlValues.IdNew, "[Add New Project]")
        };
      }
    }

    public string GetOptionalOptions() {
      string html = "";
      foreach (var opt in DbHelper.AbleQuotes.OptionalEnum.Options) {
        html += "<option ";
        html += " value=\"" + opt.Id + "\">" + opt.Text.HTMLEncode() + "</option>";
      }
      return html;
    }

    public List<WebHelper.SelectOption> GetSelectedUserOption() {
      List<WebHelper.SelectOption> options = new List<WebHelper.SelectOption>();
      if (QuoteContactUserInfo == null) return options;
      options.Add(new WebHelper.SelectOption(QuoteInfo.ContactUserId.ToString(), $@"{QuoteContactUserInfo.GetFullName()} ({QuoteContactUserInfo.EmailAddress})", true));
      return options;
    }

    string GetUserSearchResultJson(string userSearchQuery) {

      var sb = new StringBuilder();
      int rowCount = 0;
      bool contactsFromAllOrgs = SessionHelper.AppAccess.Quotes.CanSelectContactsFromAllOrgs();

      // Note these can include unregistered users.
      DbHelper.Common.Query($@"
        SELECT UserId, FirstName + ' ' + LastName + ' (' + Email + ')' AS ContactInfo
        FROM sv_user
        WHERE IsAbleUser = 1
          {(contactsFromAllOrgs ? "" : "AND OrgId = @OrgId")}
          AND (FirstName + ' ' + LastName LIKE '%' + @search + '%' OR Email LIKE '%' + @search + '%')",
        new List<SqlParameter>() {
          DbHelper.Common.NewSqlParameter("OrgId", SessionHelper.GetUserInfoOrNull().OrgId),
          DbHelper.Common.NewSqlParameter("search", userSearchQuery)
        },
        dr => {
          rowCount++;
          if (rowCount > 1) sb.Append(",");
          sb.Append("{ \"id\": ");
          sb.Append(dr.GetInt("UserId").ToString());
          sb.Append(", \"text\": ");
          sb.Append(dr.GetString("ContactInfo").JSONEncode(true));
          sb.Append("}");
        });

      if (rowCount == 0) {
        sb.Insert(0, "{ \"id\":\"" + PathHelper.AbleUrlValues.IdNew + "\", \"text\":\"[No matches found. Add a new Contact]\" }");
      } else {
        sb.Insert(0, "{ \"id\":\"" + PathHelper.AbleUrlValues.IdNew + "\", \"text\":\"[Add a new Contact]\" },");
      }

      sb.Insert(0, "{ \"results\": [ ");
      sb.Append("],\"pagination\": { \"more\": false } }");
      return sb.ToString();
    }

    public List<WebHelper.SelectOption> GetStatusOptions() {

      string whereCondition = QuoteInfo.QuoteStatusId != QuoteStatusId_Accepted ? " WHERE QuoteStatusId <> @QuoteStatusId_Accepted " : "";
      List<WebHelper.SelectOption> options = new List<WebHelper.SelectOption>();

      using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
        using (var cmd = new SqlCommand(@"
        SELECT QuoteStatusId, QuoteStatusText
        FROM al_QuoteStatus"
        + whereCondition +
        @" ORDER BY DisplaySort
        ", conn)) {
          cmd.Parameters.Add("@QuoteStatusId_Accepted", SqlDbType.Int).Value = QuoteStatusId_Accepted;
          conn.Open();
          using (var dr = cmd.ExecuteReader()) {
            while (dr.Read()) {
              int quoteStatusId = dr.GetInt("QuoteStatusId");
              WebHelper.SelectOption option = new WebHelper.SelectOption(quoteStatusId.ToString(), dr.GetString("QuoteStatusText"), quoteStatusId == QuoteInfo.QuoteStatusId);
              options.Add(option);
            }
          }
        }
      }
      return options;
    }

    public string GetAddProductOptions() {

      var html = new StringBuilder();

      html.AppendLine("<option value=\"\">[Select Product]</option>");
      html.AppendLine("<option value=\"" + ProductOptionValueForNote + "\">[Note/Heading]</option>");

      foreach (var prod in ProductOptionsForQuote) {

        if (QuoteInfo.QuoteItems != null) {
          // If the product is hidden and not already in the quote, skip it.
          if (prod.IsHidden && !QuoteInfo.QuoteItems.Exists(x => x.ProductId != null && x.ProductId.Value == prod.ProductId)) {
            continue;
          }
        }

        html.Append("<option");
        int productId = prod.ProductId;
        html.Append(" data-subcategory=\"" + prod.SubCategory.HTMLEncode() + "\"");
        html.Append(" data-name=\"" + prod.DisplayTitle.HTMLEncode() + "\"");
        html.Append(" data-description=\"" + prod.ProductDescription.HTMLEncode() + "\"");
        html.Append(" data-defaultprice=\"" + prod.DefaultUnitPrice.ToString() + "\"");
        html.Append(" data-minprice=\"" + prod.MinAllowedQuotePrice.ToString() + "\"");
        html.Append(" data-ishidden=\"" + prod.IsHidden.ToJSTrueFalse() + "\"");
        html.Append($" data-{DataAttrs.RequiresSubscription}=\"" + prod.RequiresSubscription.ToJSTrueFalse() + "\"");
        html.Append($" data-{DataAttrs.SubscriptionId}=\"" + prod.SubscriptionId.ToString() + "\"");
        html.Append($" data-{DataAttrs.IsQuantityPerPerson}=\"" + prod.IsQuantityPerPerson.ToJSTrueFalse() + "\"");
        html.Append($" data-{DataAttrs.IsFixedDescription}=\"" + prod.IsFixedDescription.ToJSTrueFalse() + "\"");
        html.Append($" data-{DataAttrs.IsSubscription}=\"" + prod.IsSubscription.ToJSTrueFalse() + "\"");
        html.Append(" value=\"" + productId + "\">");
        html.Append(string.Join(" - ", prod.CategoryName, prod.SubCategory, prod.DisplayTitle).HTMLEncode());
        html.AppendLine("</option>");
      }

      return html.ToString();
    }

    public string GetDealSourceHtml() {

      string html = "";
      foreach (var dealSource in QuoteDealSources) {
        html += "<option";
        int dealSourceId = dealSource.QuoteDealSourceId;
        if (QuoteInfo.CompanyInfo != null && dealSourceId == QuoteInfo.QuoteDealSourceId) html += " selected";
        html += " value=\"" + dealSourceId + "\">" + dealSource.DealSourceName.HTMLEncode() + "</option>";
      }

      return
        WebHelper.GetSelectRow(
          labelHtml: "Deal Source:",
          fieldName: FormFields.QuoteDealSourceId,
          inputCols: 5,
          optionsHtml: "<option value=\"\">[Select Deal Source]</option>" + html,
          rightHtml: "",
          isReadOnly: !CanEditQuoteDealSource
        );
    }

    public List<WebHelper.SelectOption> GetDealSources() {
      List<WebHelper.SelectOption> options = new List<WebHelper.SelectOption>();
      options.Add(new WebHelper.SelectOption("", "[Select Deal Source]"));
      foreach (var dealSource in QuoteDealSources) {
        int dealSourceId = dealSource.QuoteDealSourceId;
        options.Add(new WebHelper.SelectOption(dealSourceId.ToString(), dealSource.DealSourceName, QuoteInfo.CompanyInfo != null && dealSourceId == QuoteInfo.QuoteDealSourceId));
      }
      return options;
    }

    public List<WebHelper.SelectOption> GetSalesContentTypeOptions() {

      var options = new List<WebHelper.SelectOption>();

      options.Add(new WebHelper.SelectOption(
        value: "",
        text: "None",
        selected: QuoteInfo.QuoteSalesContentTypeId == null
      ));

      foreach (var sc in SalesContentTypes) {
        options.Add(new WebHelper.SelectOption(
          value: sc.QuoteSalesContentTypeId.ToString(),
          text: sc.ListItemText,
          selected: sc.QuoteSalesContentTypeId == QuoteInfo.QuoteSalesContentTypeId
        ));
      }
      return options;
    }

    public List<WebHelper.SelectOption> GetSalesContentUrlOptions() {

      var options = new List<WebHelper.SelectOption>();

      foreach (var sc in SalesContentUrls) {
        options.Add(new WebHelper.SelectOption(
          value: sc.QuoteSalesContentUrlId.ToString(),
          text: sc.ListItemText,
          selected: sc.QuoteSalesContentUrlId == QuoteInfo.QuoteSalesContentUrlId
        ));
      }

      return options;
    }

    public List<WebHelper.SelectOption> GetCompanyOptions() {

      List<WebHelper.SelectOption> options = new List<WebHelper.SelectOption>();
      options.Add(new WebHelper.SelectOption("", "[Select or Add Company]"));
      options.Add(new WebHelper.SelectOption(PathHelper.AbleUrlValues.IdNew, "[Add New Company"));

      foreach (var cmp in CompanyList) {
        int companyId = cmp.CompanyId;
        options.Add(new WebHelper.SelectOption(
          companyId.ToString(),
          cmp.CompanyName,
          QuoteInfo.CompanyInfo != null && companyId == QuoteInfo.CompanyInfo.CompanyId
        ));
      }

      return options;
    }

    void MoveOrgToTop(int orgId) {

      var orgIndex = BrandingOrgs.FindIndex(org => org.OrgId == orgId);
      if (orgIndex >= 0) {
        var org = BrandingOrgs[orgIndex];
        BrandingOrgs.RemoveAt(orgIndex);
        BrandingOrgs.Insert(0, org);
      }
    }

    public List<WebHelper.SelectOption> GetBrandingOrgOptions() {

      MoveOrgToTop(SessionHelper.UserInfo.OrgId); // Move user's company top top of list.

      List<WebHelper.SelectOption> options = new List<WebHelper.SelectOption>();

      foreach (var org in BrandingOrgs) {
        bool selected = false;
        if (QuoteInfo.BrandingOrgId != null) {
          if (org.OrgId == QuoteInfo.BrandingOrgId) selected = true;
        } else {
          if (org.OrgId == userInfo.OrgId) selected = true;
        }
        WebHelper.SelectOption option = new WebHelper.SelectOption(org.OrgId.ToString(), org.OrgName, selected);
        options.Add(option);
      }
      return options;
    }

    void UpdateAcceptedQuote(AjaxSubmitHelper ajax) {

      if (QuoteInfo == null || !IsUpdatingAcceptedQuote) return;

      string currentProject = QuoteInfo.JobNumber;
      string newProject = GetProjectJobNumber(ajax);
      bool updatedQuote = false, isDoingUpdate = false;
      var formValues = new FormValues();

      if (CanEditQuoteProject && !newProject.IsNullOrEmpty() && currentProject != newProject) {
        QuoteInfo.JobNumber = newProject;
        isDoingUpdate = true;
      }

      if (CanEditQuoteSplits || CanChangeSplitRoles) {

        if (!GetFormValues_Splits(ajax, formValues) || ajax.BadFieldCount > 0) {

          ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.splits);
          return;

        } else {

          if (CanEditQuoteSplits) {
            QuoteInfo.DeliveryPercentage = formValues.DeliveryPercentage;
            QuoteInfo.OPPPercentage = formValues.OPPPercentage;
            QuoteInfo.PLCPercentage = formValues.PLCPercentage;
            QuoteInfo.PlatformPercentage = formValues.PlatformPercentage;
            QuoteInfo.ProposalDesignerPercentage = formValues.ProposalDesignerPercentage;
          }

          if (CanChangeSplitRoles) {
            QuoteInfo.OwnerUserId = formValues.OwnerUserId;
            QuoteInfo.ProposalDesignerUserId = formValues.ProposalDesignerUserId;
            QuoteInfo.LeadConsultantUserId = formValues.LeadConsultantUserId;
          }

          isDoingUpdate = true;
        }
      }

      if (isDoingUpdate) {
        // Do everything in one transaction.
        updatedQuote = DbHelper.Common.UsingTransaction(trans => {

          try {

            updatedQuote = DbHelper.AbleQuotes.UpdateQuote(trans, QuoteInfo);

            if (CanEditQuoteSplits) {
              // Platform Services
              if (!UpdatePlatformServices(trans, formValues, QuoteInfo.QuoteId, ajax)) return false;
            }

          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("QuoteInfo_UpdateQuote")
              .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
              .WithPageUrl(Request.RawUrl)
              .WithProperty("QuoteId", QuoteInfo?.QuoteId)
              .WithProperty("QuoteGuid", QuoteInfo?.PublicGuid)
              .WithProperty("IsNewQuote", IsNewQuote)
              .Track();

            ajax.AddDialogMessage("Error updating quote.");
            if (ConfigHelper.IsDevServer) ajax.AppendToCurrentMessage("<br/>" + ex.Message);
            EmailHelper.SendInternalSupportEmail(ex, "QuoteInfo Trying to update Project for Quote.");
            return false;

          }

          return true; // Commit transaction.
        });
      }

      if (updatedQuote) {
        // Reload QuoteInfo to get updated totals
        QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(QuoteInfo.QuoteId);

        // TODO: Temporarily disabled - creating too many events at the moment
        // Send Intercom event for quote update
        //SendEvent(
        //  intercom => intercom.QuoteUpdated()
        //    .FromSession()
        //    .WithQuote(QuoteInfo.QuoteId, QuoteInfo.QuoteTitle)
        //    .WithClientCompany(QuoteInfo.CompanyId, QuoteInfo.CompanyName ?? "")
        //    .WithQuoteValue(QuoteInfo.QuoteItemTotalAmount),
        //  operationName: "QuoteInfo_QuoteUpdated",
        //  requestRawUrl: SystemWeb.RequestRawUrl,
        //  telemetryProperties: new Dictionary<string, object> {
        //    ["QuoteId"] = QuoteInfo.QuoteId,
        //    ["QuoteValue"] = QuoteInfo.QuoteItemTotalAmount
        //  }
        //);

        ajax.SetReloadPage(UpdateMessage);
      } else {
        ajax.AddDialogMessage($"There was an error {UpdateMessage} the quote, please try again.");
      }
      return;
    }

    bool GetFormValues_ProjectInfo(AjaxSubmitHelper ajax, FormValues formValues) {

      formValues.IsNewCompany = WebHelper.GetFormValue(FormFields.CompanyId) == PathHelper.AbleUrlValues.IdNew;
      formValues.IsNewProject = WebHelper.GetFormValue(FormFields.ProjectJobNumber) == PathHelper.AbleUrlValues.IdNew;

      if (formValues.IsNewCompany && !formValues.IsNewProject) {
        ajax.AddDialogMessage("New Company requires a new Project");
        return false;
      }
      if (formValues.IsNewCompany) {
        formValues.CompanyName = ajax.CheckFieldRegex(FormFields.CompanyName, "Company Name", AppHelper.Regex.GeneralText, true, "Use plain characters for Company Name.");
        formValues.ClientLeadUserId = ajax.CheckFieldIntOrNull(FormFields.ClientLeadUserId, "Client Lead", null, null, true, "Please enter a valid Client Lead.");
      } else {
        formValues.CompanyId = ajax.CheckFieldID(FormFields.CompanyId, "Company", true, "Please select a Company.");
      }
      if (formValues.IsNewProject) {
        formValues.ProjectName = ajax.CheckFieldRegex(FormFields.ProjectName, "Project Name", AppHelper.Regex.GeneralText, true, "Use plain characters for Project Name.");
      } else {
        formValues.ProjectJobNumber = GetProjectJobNumber(ajax);
      }

      formValues.QuoteDealSourceId = ajax.CheckFieldID(FormFields.QuoteDealSourceId, "Deal Source", true, "Please select a deal source.");

      // Validate deal source.
      if (!QuoteDealSources.Exists(d => d.QuoteDealSourceId == formValues.QuoteDealSourceId)) {
        ajax.AddBadField(FormFields.QuoteDealSourceId, "Please select a Deal Source");
      }

      if (ajax.HasErrors) return false;

      if (!formValues.IsNewCompany) {
        // Ensure company exists.
        var selectedCmp = CompanyList.Find(cmp => cmp.CompanyId == formValues.CompanyId);
        if (selectedCmp == null) {
          ajax.AddDialogMessage("Selected Company not found.");
          return false;
        }
        if (!formValues.IsNewProject) {
          // Ensure project exists and belongs to company.
          bool canSelectAllProjects = SessionHelper.AppAccess.Quotes.CanSelectProjectsFromAllOrgs();
          var prj = DbHelper.Projects.GetProjectInfoOrNull(formValues.ProjectJobNumber);
          if (prj == null || (!canSelectAllProjects && prj.TenantOrgId != SessionHelper.GetUserInfoOrNull().OrgId)) {
            ajax.AddDialogMessage("Selected Project not found.");
            return false;
          }
          if (prj.CompanyId != selectedCmp.CompanyId) {
            ajax.AddDialogMessage("Selected Project does not belong to selected Company.");
            return false;
          }
        }
      }

      if (IsNewQuote) {

        formValues.OwnerUserId = userInfo.UserId;
        formValues.TeamUserIds = new List<int>();
        formValues.TeamUserIds.Add(userInfo.UserId);
        formValues.QuoteStatusId = DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.draft).QuoteStatusId;
        formValues.QuoteTitle = "";
      }

      return true;
    }

    private string GetProjectJobNumber(AjaxSubmitHelper ajax) {

      // If editing an existing quote, the user may change the selected Project.
      // An Accepted quote may also have the project changed, if that permission is allowed.

      var selectedJobNumber = ajax.CheckFieldRegex(FormFields.ProjectJobNumber, "Project", AppHelper.Regex.GeneralText, true, "Please select a Project.");

      if (IsNewQuote || selectedJobNumber == QuoteInfo.JobNumber) {
        return selectedJobNumber; // Nothing to check if new quote or project number unchanged.
      }

      if (!CanEditQuoteProject) {
        UpdateMessage += "Project ID not updated, you don't have access to perform this change.";
        return QuoteInfo.JobNumber;
      }

      // TODO: HasDependents should exist in QuoteInfo, and checked as part of AppAccess.Quotes.CanEditQuoteProject().
      if (DbHelper.AbleQuotes.QuoteHasDependents(QuoteInfo.QuoteId)) {

        string msg =
          @"Project not updated, the quote has dependent items.
          Please remove all components and invoices linked to this quote to be able to switch projects.<br/>";

        if (IsUpdatingAcceptedQuote) {
          // For Accepted quotes, this message is shown as an error.
          ajax.AddDialogMessage(msg);
          return string.Empty;
        } else {
          // Otherwise message will be added to any existing message by the caller.
          UpdateMessage += msg;
          return QuoteInfo.JobNumber;
        }
      }

      // Success
      UpdateMessage = $"Project ID updated from {QuoteInfo.JobNumber} to {selectedJobNumber}.";
      return selectedJobNumber;
    }

    bool GetFormValues_Contact(AjaxSubmitHelper ajax, FormValues formValues) {

      bool contactsFromAllOrgs = SessionHelper.AppAccess.Quotes.CanSelectContactsFromAllOrgs();
      int userOrgId = SessionHelper.GetUserInfoOrNull().OrgId;

      formValues.IsNewContact = WebHelper.GetFormValue(FormFields.ContactUserId) == PathHelper.AbleUrlValues.IdNew;

      if (!formValues.IsNewContact) {

        formValues.ContactUserId = ajax.CheckFieldID(FormFields.ContactUserId, "Contact", true, "Please select a Contact.");
        if (ajax.BadFieldCount > 0) return false;

        // Check userid exists.
        var contactUser = DbHelper.AbleUser.GetQuoteContactUserOrNull(formValues.ContactUserId);
        if (contactUser == null || (!contactsFromAllOrgs && contactUser.OrgId != userOrgId)) {
          ajax.AddBadField(FormFields.ContactUserId, "Selected User not found.");
          return false;
        }
        return true;
      }

      // New user.

      formValues.ContactFirstName = ajax.CheckFieldRegex(FormFields.ContactFirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please use plain text for Name.");
      formValues.ContactLastName = ajax.CheckFieldRegex(FormFields.ContactLastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please use plain text for Name.");
      formValues.ContactEmail = ajax.CheckFieldRegex(FormFields.ContactEmail, "Email", AppHelper.Regex.Email, true, "Please provide valid email address.");
      formValues.ContactPhone = ajax.CheckFieldRegex(FormFields.ContactPhone, "Phone", AppHelper.Regex.GeneralText, false, "Please use plain text for Phone.");
      formValues.ContactRole = ajax.CheckFieldRegex(FormFields.ContactRole, "Role", AppHelper.Regex.GeneralText, false, "Please use plain text for Role.");
      formValues.ContactCity = ajax.CheckFieldRegex(FormFields.ContactCity, "City", AppHelper.Regex.GeneralText, false, "Please use plain text for City.");

      if (ajax.BadFieldCount > 0) return false;

      // Check if email already exists.
      var findUser = DbHelper.AbleUser.GetUserByEmailOrNull(formValues.ContactEmail, DbHelper.AbleUser.RegisteredFilter.Any);
      if (findUser != null) {
        ajax.AddBadField(FormFields.ContactEmail, "Email address already exists.");
        return false;
      }

      return true;
    }

    bool GetFormValues_Settings(AjaxSubmitHelper ajax, FormValues formValues) {

      formValues.QuoteTitle = ajax.CheckFieldRegex(FormFields.QuoteTitle, "Quote Title", AppHelper.Regex.GeneralText, false, "Use plain characters for Quote Title.");
      formValues.QuoteStatusId = ajax.CheckFieldID(FormFields.QuoteStatusId, "Quote Status", true, "Please select a Quote Status.");

      if (CanEditQuoteBranding) {
        formValues.BrandingOrgId = ajax.CheckFieldID(FormFields.BrandingOrgId, "Branding Company", true, "Please select a Branding Company");
      } else {
        if (IsNewQuote) formValues.BrandingOrgId = userInfo.OrgId;
        else formValues.BrandingOrgId = QuoteInfo?.BrandingOrgId;
      }

      formValues.QuoteSalesContentTypeId = ajax.CheckFieldIntOrNull(
        fieldName: FormFields.QuoteSalesContentTypeId,
        fieldTitle: "Sales Material Type",
        iMin: 1,
        iMax: null,
        isRequired: false,
        invalidMsg: "Please select a Sales Material Type");

      if (formValues.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.UrlList) {
        formValues.QuoteSalesContentUrlId = ajax.CheckFieldID(
          fieldName: FormFields.QuoteSalesContentUrlId,
          fieldTitle: "Sales Material Template",
          isRequired: true,
          invalidMsg: "Please select a Sales Material Template");
      }

      if (formValues.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.WebPageUrl) {
        formValues.QuoteSalesContentWebPageUrl = ajax.CheckFieldRegex(
          fieldName: FormFields.QuoteSalesContentWebPageUrl,
          fieldTitle: "Web Page URL",
          sRegex: AppHelper.Regex.GeneralText,
          isRequired: true,
          customInvalidMsg: "Please enter a valid URL.");
      }

      if (formValues.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.PDF) {
        // TODO
      }

      if (formValues.QuoteSalesContentTypeId == ConfigHelper.QuoteSalesContentTypeId.Qwilr) {

        formValues.QwilrUrl = ajax.CheckFieldRegex(
          fieldName: FormFields.QwilrUrl,
          fieldTitle: "Qwilr Embed Url",
          sRegex: AppHelper.Regex.GeneralText,
          isRequired: false,
          customInvalidMsg: "Please enter a valid URL.");

        formValues.QwilrPDFUrl = ajax.CheckFieldRegex(
          fieldName: FormFields.QwilrPDFUrl,
          fieldTitle: "Qwilr PDF Url",
          sRegex: AppHelper.Regex.GeneralText,
          isRequired: false,
          customInvalidMsg: "Please enter a valid URL.");

        // One or the other of above required.
        if (formValues.QwilrUrl.IsNullOrEmpty() && formValues.QwilrPDFUrl.IsNullOrEmpty() && !ajax.DialogMessageExists()) {
          ajax.AddDialogMessage("Please provide either a Qwilr URL or Qwilr PDF URL.");
        }

        if (!formValues.QwilrUrl.IsNullOrEmpty() && formValues.QwilrUrl.ContainsIgnoreCase(ConfigHelper.ExternalUrls.Qwilr_NonPublicDomain)) {
          ajax.AddBadField(FormFields.QwilrUrl, $"Qwilr URL must not be '{ConfigHelper.ExternalUrls.Qwilr_NonPublicDomain}' as it's not public.");
        }
      }

      formValues.EstimatedStartDateLocal = ajax.GetDatePickerDateUnspecified(FormFields.EstimatedStartDateLocal, "Start Date", false, "Please select a Start Date.") ?? SessionHelper.UtcNowToUserTime();
      formValues.GSTApplicable = ajax.CheckFieldBool(FormFields.GSTApplicable, "1");
      if (CanUpdateExcludeFromSalesIncentive) formValues.ExcludeFromSalesIncentive = ajax.CheckFieldBool(FormFields.ExcludeFromSalesIncentive, "1");
      if (CanEditFreshSalesOption) formValues.AddToFreshSales = ajax.CheckFieldBool(FormFields.AddToFreshSales, "1");

      // Validate branding org.
      if (CanEditQuoteBranding && !BrandingOrgs.Exists(c => c.OrgId == formValues.BrandingOrgId)) {
        ajax.AddBadField(FormFields.BrandingOrgId, "Please select a Branding Company");
      }

      if (!IsAccepted && formValues.QuoteStatusId == QuoteStatusId_Accepted) {
        ajax.AddDialogMessage("Can't assign Accepted status to Quote.");
      }

      if (ajax.HasErrors) return false;

      return true;
    }

    bool GetFormValues_Splits(AjaxSubmitHelper ajax, FormValues formValues) {

      formValues.PlatformServiceIds = ajax.CheckFieldIntList(FormFields.PlatformServiceIds);
      formValues.OPPPercentage = ajax.CheckFieldPercent(FormFields.OPPPercentage, "OPP %", false, true, "Please enter a valid percentage.", 0, 100) ?? 0;
      formValues.PLCPercentage = ajax.CheckFieldPercent(FormFields.PLCPercentage, "PLC %", false, true, "Please enter a valid percentage.", 0, 100) ?? 0;
      formValues.ProposalDesignerPercentage = ajax.CheckFieldPercent(FormFields.ProposalDesignerPercentage, "Proposal Designer %", false, true, "Please enter a valid percentage.", 0, 100) ?? 0;

      if (!IsUpdatingAcceptedQuote) {
        // Validate team members
        formValues.TeamUserIds = new List<int>();
        // Get selected users
        var teamUserIdUx = WebHelper.GetFormValue(FormFields.TeamUserIds).ToIntList();
        if (teamUserIdUx.Count > 0) {
          var existingTeamUserIds = QuoteInfo.QuoteTeamUsers;
          foreach (var tui in teamUserIdUx) {
            var thisUserInfo = PartnerList.Find(x => x.UserId == tui);
            // If user was already in team members, although it's inactive it can stay in the list.
            // If user is active it can be added
            // If a user was force selected in the UX but it's inactive, it won't be added.
            if ((!thisUserInfo.IsPartnerActive && existingTeamUserIds != null && existingTeamUserIds.Exists(x => x.UserId == tui)) || thisUserInfo.IsPartnerActive || (IsNewQuote && tui == userInfo.UserId)) {
              formValues.TeamUserIds.Add(tui);
            }
          }
        }
      }

      if (CanChangeSplitRoles) {

        formValues.OwnerUserId = ajax.CheckFieldID(FormFields.OwnerUserId, "Owner", true, "Please select a Quote Owner.");
        formValues.ProposalDesignerUserId = ajax.CheckFieldID(FormFields.ProposalDesignerUserId, "Proposal Designer", false, "Please select a Proposal Designer.");
        formValues.LeadConsultantUserId = ajax.CheckFieldID(FormFields.LeadConsultantUserId, "Lead Consultant", false, "Please select a Lead Consultant.");

        if (formValues.ProposalDesignerUserId == 0) formValues.ProposalDesignerUserId = null;
        if (formValues.LeadConsultantUserId == 0) formValues.LeadConsultantUserId = null;

      } else if (IsNewQuote) {

        formValues.OwnerUserId = userInfo.UserId; // Default to current user.
        formValues.ProposalDesignerUserId = null;
        formValues.LeadConsultantUserId = null;

      } else {

        // Use existing.
        formValues.OwnerUserId = QuoteInfo.OwnerUserId;
        formValues.ProposalDesignerUserId = QuoteInfo.ProposalDesignerUserId;
        formValues.LeadConsultantUserId = QuoteInfo.LeadConsultantUserId;
      }

      // Check if owner userid is valid.
      if (!PartnerList.Exists(x => x.UserId == formValues.OwnerUserId)) {
        ajax.AddBadField(FormFields.OwnerUserId, "Please select a Quote Owner.");
        return false;
      }

      if (formValues.ProposalDesignerUserId.HasValue && !PartnerList.Exists(x => x.UserId == formValues.ProposalDesignerUserId)) {
        ajax.AddBadField(FormFields.ProposalDesignerUserId, "Please select a valid Proposal Designer.");
        return false;
      }

      if (formValues.LeadConsultantUserId.HasValue && !PartnerList.Exists(x => x.UserId == formValues.LeadConsultantUserId)) {
        ajax.AddBadField(FormFields.LeadConsultantUserId, "Please select a valid Proposal Designer.");
        return false;
      }

      if (!IsUpdatingAcceptedQuote) {
        if (formValues.TeamUserIds == null || formValues.TeamUserIds.Count == 0) {
          ajax.AddBadField(FormFields.TeamUserIds, "At least one team member is required.");
        }
      }

      if (ajax.BadFieldCount > 0) return false;

      // Tally up all selected and mandatory platform service percentages.

      decimal serviceTotalPercentage = 0;
      var selectedOwnerUser = PartnerList.Find(u => u.UserId == formValues.OwnerUserId);

      // Ensure that the user selection of services includes the default platform service
      // and all other mandatory services.
      formValues.PlatformServiceIds.AddIfMissing(DbHelper.PlatformService.PlatformServiceFeeIds.IntegralClientOwner);
      formValues.PlatformServiceIds.AddIfMissing(DbHelper.PlatformService.PlatformServiceFeeIds.PlatformBaseFee);

      // Search all "Always required" services from Database and add them to formValues list
      PlatformServiceList.FindAll(s => s.AlwaysRequired).ForEach(s => {
        formValues.PlatformServiceIds.AddIfMissing(s.PlatformServiceId);
      });

      // Go through selected service ids, check if valid and add to running total percentage.
      foreach (var serviceId in formValues.PlatformServiceIds) {
        var service = PlatformServiceList.Find(s => s.PlatformServiceId == serviceId);
        if (service == null) {
          ajax.AddDialogMessage("Invalid Platform Service ID. Please reload the page and try again."); // Shouldn't happen unless form is fiddled.
          return false;
        }
        serviceTotalPercentage += service.ServiceFeePercent;
      }

      // Assign to form value so it is stored in the db.
      formValues.PlatformPercentage = serviceTotalPercentage;
      formValues.DeliveryPercentage = 1 - (formValues.OPPPercentage + formValues.PLCPercentage + formValues.ProposalDesignerPercentage + serviceTotalPercentage);

      var totalSum = formValues.OPPPercentage + formValues.PLCPercentage + formValues.DeliveryPercentage + formValues.ProposalDesignerPercentage + serviceTotalPercentage;

      // Check that services % + splits % = 100%.
      if (totalSum > 1) {
        ajax.AddDialogMessage("Percent total exceeds 100.");
        return false;
      } else if (totalSum < 1) {
        ajax.AddDialogMessage("Percent total is less than 100.");
        return false;
      }

      return true;
    }

    bool GetFormValues_Components(AjaxSubmitHelper ajax, FormValues formValues) {

      formValues.QuoteItems = new List<FormValues.QuoteItem>();

      bool hasSubscriptionId = false, requiresSubscriptionId = false;
      int? defaultSubscriptionId = null, requiredSubscription_Quantity = null;

      var prodsDone = new List<int>();

      foreach (var key in Request.Form.AllKeys) {

        if (!key.StartsWith(FormFields.ProdKey_Prefix)) continue;

        string numstr = key.RegexMatchStringOrNull("[0-9]+");
        if (!int.TryParse(numstr, out int prodNum)) {
          ajax.AddDialogMessage("Error processing products. Please reload the page and try again.");
          return false;
        }

        if (prodsDone.Contains(prodNum)) continue;
        prodsDone.Add(prodNum);

        var product = new FormValues.QuoteItem() {
          IsNote = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_IsNote) == "true",
          ProductId = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_Id).ToIntOrDefault(0),
          ItemDescription = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_Name),
          OptionalInfo = DbHelper.AbleQuotes.OptionalEnum.GetOptionById(WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_Optional).ToIntOrNull(), DbHelper.AbleQuotes.OptionalEnum.No),
          UnitPrice = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_Price).ToDecimalOrDefault(0),
          Quantity = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_Qty).ToDecimalOrDefault(0),
          QuantityDescr = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_QtyDescr),
          IsQuantityPerPerson = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_IsQuantityPerPerson) == "true",
          RequiresSubscription = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_RequiresSubscription) == "true",
          DefaultSubscriptionId = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_DefaultSubscriptionId).ToIntOrNull(),
          IsSubscription = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_IsSubscription) == "true",
        };

        if (!product.IsNote && product.ProductId <= 0) {
          ajax.AddDialogMessage("Problem adding line item " + prodsDone.Count + "."
            + "<br/>Please try removing and adding that item again.");
          return false;
        } else if (!product.IsNote && product.Quantity <= 0) {
          ajax.AddDialogMessage("Item quantity cannot be zero.");
          return false;
        } else if (!product.IsNote && product.Quantity != Math.Truncate(product.Quantity)) {
          ajax.AddDialogMessage("Only whole numbers allowed for Quantity.");
          return false;
        }

        if (product.IsNote) {
          // Noting to check for headings.
          formValues.QuoteItems.Add(product);
          continue;
        }

        var prod = ProductOptionsForQuote.Find(p => p.ProductId == product.ProductId);

        if (prod == null) {

          ajax.AddDialogMessage("Product is not available.");
          return false;

        } else if (prod.IsHidden) {

          if (QuoteInfo == null || !QuoteInfo.QuoteItems.Exists(x => x.ProductId != null && x.ProductId.Value == prod.ProductId)) {
            // If the product is hidden and quote is new or the product doesn't exist in the current quote, do not allow to add the product to quote items.
            ajax.AddDialogMessage("This product is not available.");
            return false;
          }
        } else if (prod.IsFixedDescription) {

          product.ItemDescription = prod.ProductDescription; // If set UsedDescription make sure to use the description from the database.

        } else if (product.UnitPrice < prod.MinAllowedQuotePrice) {

          ajax.AddDialogMessage($"The minimum allowed price for product '{string.Join(" - ", prod.CategoryName, prod.DisplayTitle)}' is <b>{prod.MinAllowedQuotePrice:C}</b>");
          return false;
        }

        if (product.RequiresSubscription) {
          requiresSubscriptionId = true;
          defaultSubscriptionId = product.DefaultSubscriptionId;
          requiredSubscription_Quantity = Math.Max((int)product.Quantity, requiredSubscription_Quantity.GetValueOrDefault(0));
        }

        if (product.IsSubscription) {
          hasSubscriptionId = true;
        }

        formValues.QuoteItems.Add(product);
      }

      // Identify if user if removing the subscription from the quote items.
      // If a subscription exists in the QuoteInfo.QuoteItems but not in formValues.QuoteItems, that means the subscription is being removed.
      bool isRemovingSubscription = false;
      var subInQuote = QuoteInfo.QuoteItems?.Where(x => x.IsSubscription).FirstOrDefault();
      if (subInQuote != null) {
        if (!formValues.QuoteItems.Exists(x => x.ProductId == subInQuote.ProductId)) {
          isRemovingSubscription = true;
        }
      }

      // Automatically add subscription if there's at least one product that requires a subscription, there's no Subscription in the quote and it's not Removing a subscription in this update
      bool automaticallyAddSubscription = requiresSubscriptionId && !hasSubscriptionId && !isRemovingSubscription;
      if (automaticallyAddSubscription) {
        // There's a product that requires subscription and there's no subscription in the quote
        formValues.QuoteItems_MissingSubscription = true;
        formValues.QuoteItems_RequiredSubscription_ProductId = ProductOptionsForQuote.Find(x => x.IsSubscription && x.SubscriptionId == defaultSubscriptionId).ProductId;
        formValues.QuoteItems_RequiredSubscription_ProductId_Quantity = requiredSubscription_Quantity;
      }

      return true;
    }

    bool CreateOrUpdateCompany(SqlTransaction trans, FormValues formValues, AjaxSubmitHelper ajax) {

      if (formValues.IsNewCompany) {
        // Add new company & get id.
        try {
          var newCompanyInfo = DbHelper.ClientCompanies.CreateCompanyBrief(trans, userInfo.OrgId, formValues.CompanyName, formValues.ClientLeadUserId);
          formValues.CompanyId = newCompanyInfo.CompanyId;
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("QuoteInfo_CreateCompany")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .WithPageUrl(Request.RawUrl)
            .WithProperty("CompanyName", formValues.CompanyName)
            .WithProperty("ClientLeadUserId", formValues.ClientLeadUserId)
            .WithProperty("IsDuplicateKey", DbHelper.GetSqlError(ex) == DbHelper.SqlErrorEnum.DuplicateKey)
            .Track();

          if (DbHelper.GetSqlError(ex) == DbHelper.SqlErrorEnum.DuplicateKey) {
            ajax.AddDialogMessage("Company Name already exists.<br/>Please provide a unique company name or select the existing one.");
            ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.project);
          } else {
            ajax.AddDialogMessage("Error encountered trying to add the new Company!");
            if (ConfigHelper.IsDevServer) ajax.AppendToCurrentMessage("<br/>" + ex.Message);
            EmailHelper.SendInternalSupportEmail(ex, "QuoteInfo Trying to add new Company.");
          }
          return false;
        }
      }
      return true;
    }

    bool CreateOrUpdateProject(SqlTransaction trans, FormValues formValues, AjaxSubmitHelper ajax) {

      if (formValues.IsNewProject) {

        // Add new project & get id.
        try {

          DbHelper.Projects.CreateProjectAndProgram(
            trans: trans,
            tenantOrgId: SessionHelper.UserInfo.OrgId,
            companyId: formValues.CompanyId,
            projectName: formValues.ProjectName,
            preferredProgramName: null,
            canSelfSelectCoach: false,
            createdByUserId: SessionHelper.UserInfo.UserId,
            newJobNumber: out formValues.ProjectJobNumber,
            newProgramJobId: out _
          );

        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("QuoteInfo_CreateProject")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .WithPageUrl(Request.RawUrl)
            .WithProperty("ProjectName", formValues.ProjectName)
            .WithProperty("CompanyId", formValues.CompanyId)
            .WithProperty("IsDuplicateKey", DbHelper.IsDuplicateKeyError(ex))
            .Track();

          if (DbHelper.IsDuplicateKeyError(ex)) {
            ajax.AddDialogMessage("Project Name already exists.<br/>Please provide a unique project name or select the existing one.");
            ajax.AddReturnValue(RtnShowTabKey, PathHelper.QuoteTabEnum.project);
          } else {
            ajax.AddDialogMessage("Error encountered trying to add the new Project.", ex);
            EmailHelper.SendInternalSupportEmail(ex, "QuoteInfo Trying to add new Project.");
          }
          return false;

        }
      }
      return true;
    }

    bool CreateOrUpdateContact(SqlTransaction trans, FormValues formValues, AjaxSubmitHelper ajax) {

      if (formValues.IsNewContact) {

        // Create InviteeBasicInfo object
        var inviteeBasicInfo = new DbHelper.AbleUser.InviteeBasicInfo(
          formValues.ContactFirstName,
          formValues.ContactLastName,
          formValues.ContactEmail,
          DbHelper.AbleUser.UserRoleEnum.Client,
          userInfo.OrgId
        );
        inviteeBasicInfo.AddOptionalExtraDetails(
          formValues.ContactPhone,
          formValues.ContactRole,
          null,
          formValues.ContactCity
        );

        try {
          var inviteeUserInfo = DbHelper.AbleUser.CreateInviteeUser(trans, userInfo, inviteeBasicInfo);
          if (inviteeUserInfo != null) {
            formValues.ContactUserId = inviteeUserInfo.UserId;
          }
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("QuoteInfo_CreateInviteeUser")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .WithPageUrl(Request.RawUrl)
            .WithProperty("ContactEmail", formValues.ContactEmail)
            .WithProperty("ContactFirstName", formValues.ContactFirstName)
            .WithProperty("ContactLastName", formValues.ContactLastName)
            .Track();

          ajax.AddDialogMessage("Problem creating invite, please try again later.", ex);
          return false;
        }

      } else {

        var contactUser = DbHelper.AbleUser.GetQuoteContactUserOrNull(formValues.ContactUserId);

        if (contactUser == null) {
          ajax.AddDialogMessage("Selected Contact not found.");
          return false;
        }

        try {
          DbHelper.AbleUser.UpdateIsClient(null, contactUser, true);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("QuoteInfo_UpdateContactIsClient")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .AddExternalUserId(ExternalUserKind.Client, ConfigHelper.UserRole.Client.ToExternalUserId(contactUser?.UserGuid))
            .WithPageUrl(Request.RawUrl)
            .WithProperty("ContactEmail", contactUser?.EmailAddress)
            .Track();

          ajax.AddDialogMessage("Problem updating Contact.", ex);
          return false;
        }
      }

      return true;
    }

    bool UpdateQuoteItems(SqlTransaction trans, FormValues formValues, int quoteId, AjaxSubmitHelper ajax) {

      // Remove existing quote items.
      DbHelper.Common.GetNonQueryInt(trans,
        "DELETE FROM al_QuoteItem WHERE QuoteId = @QuoteId",
        DbHelper.Common.NewSqlParameter("QuoteId", quoteId));

      if (formValues.QuoteItems.Count == 0) return true; // Do not process items and allow to save.

      foreach (var product in formValues.QuoteItems) {
        try {
          var newItemId = DbHelper.AbleQuotes.CreateQuoteItem(
            trans: trans,
            quoteId: quoteId,
            productId: product.IsNote ? null : (int?)product.ProductId,
            itemDescription: product.ItemDescription,
            isOptionalId: product.IsNote ? DbHelper.AbleQuotes.OptionalEnum.No.Id : product.OptionalInfo.Id,
            unitPrice: product.IsNote ? null : (decimal?)product.UnitPrice,
            quantity: product.IsNote ? null : (decimal?)product.Quantity,
            quantityDescr: product.QuantityDescr.EmptyIfNull().LimitLengthTo(50)
          );
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("QuoteInfo_CreateQuoteItem")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .WithPageUrl(Request.RawUrl)
            .WithProperty("QuoteId", quoteId)
            .WithProperty("ProductId", product.ProductId)
            .WithProperty("IsNote", product.IsNote)
            .WithProperty("ItemDescription", product.ItemDescription)
            .WithProperty("UnitPrice", product.UnitPrice)
            .WithProperty("Quantity", product.Quantity)
            .Track();

          ajax.AddDialogMessage("Error creating quote item.");
          if (ConfigHelper.IsDevServer) ajax.AppendToCurrentMessage("<br/>" + ex.ToString());
          if (!ConfigHelper.IsDevServer) EmailHelper.SendInternalSupportEmail(ex, "QuoteInfo Trying to add new QuoteItem.");
          return false;
        }
      }
      return true;
    }

    bool UpdateQuoteTeamUsers(SqlTransaction trans, FormValues formValues, int quoteId, AjaxSubmitHelper ajax) {
      // Remove existing team users.
      DbHelper.Common.GetNonQueryInt(trans,
        "DELETE FROM al_QuoteTeamUser WHERE QuoteId = @QuoteId",
        DbHelper.Common.NewSqlParameter("QuoteId", quoteId));
      // Add quote items.
      if (formValues.TeamUserIds != null) {
        foreach (var userId in formValues.TeamUserIds) {
          try {
            DbHelper.AbleQuotes.AddQuoteTeamUser(trans, quoteId, userId);
          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("QuoteInfo_AddQuoteTeamUser")
              .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
              .WithPageUrl(Request.RawUrl)
              .WithProperty("QuoteId", quoteId)
              .WithProperty("TeamUserId", userId)
              .Track();

            ajax.AddDialogMessage("Error adding team user.");
            if (ConfigHelper.IsDevServer) ajax.AppendToCurrentMessage("<br/>" + ex.ToString());
            if (!ConfigHelper.IsDevServer) EmailHelper.SendInternalSupportEmail(ex, "QuoteInfo Trying to add new Team User.");
            return false;
          }
        }
      }
      return true;
    }

    bool UpdatePlatformServices(SqlTransaction trans, FormValues formValues, int QuoteId, AjaxSubmitHelper ajax) {
      try {
        DbHelper.PlatformService.UpdateServicesForQuote(trans, QuoteId, formValues.PlatformServiceIds);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("QuoteInfo_UpdatePlatformServices")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithPageUrl(Request.RawUrl)
          .WithProperty("QuoteId", QuoteId)
          .WithProperty("ServiceIdsCount", formValues.PlatformServiceIds?.Count)
          .Track();

        ajax.AddDialogMessage("Error updating Platform Services.");
        if (ConfigHelper.IsDevServer) ajax.AppendToCurrentMessage("<br/>" + ex.ToString());
        if (!ConfigHelper.IsDevServer) EmailHelper.SendInternalSupportEmail(ex, "QuoteInfo: Trying to update Platform Services.");
        return false;
      }
      return true;
    }

    bool CreateQuote(FormValues formValues, AjaxSubmitHelper ajax) {

      int newQuoteId = 0;

      // Do everything in one transaction.
      bool committed = DbHelper.Common.UsingTransaction(trans => {

        if (!CreateOrUpdateCompany(trans, formValues, ajax)) return false;
        if (!CreateOrUpdateProject(trans, formValues, ajax)) return false;
        if (!CreateOrUpdateContact(trans, formValues, ajax)) return false;

        // Add quote.

        var newQuoteInfo = new DbHelper.AbleQuotes.NewQuoteInfo(
          jobNumber: formValues.ProjectJobNumber,
          ownerUserId: formValues.OwnerUserId,
          leadConsultantUserId: formValues.LeadConsultantUserId,
          proposalDesignerUserId: formValues.ProposalDesignerUserId,
          contactUserId: formValues.ContactUserId,
          quoteTitle: formValues.QuoteTitle,
          brandingOrgId: formValues.BrandingOrgId,
          quoteStatusId: formValues.QuoteStatusId,
          estimatedStartDateUtc: formValues.EstimatedStartDateLocal.ToUniversalTime(ConfigHelper.DefaultTimeZoneInfo),
          xeroTaxType: DbHelper.XeroTaxType.GetQuoteTaxTypeFromGSTApplicable(formValues.GSTApplicable),
          customInvoicing: false,
          addToFreshSales: CanEditFreshSalesOption ? formValues.AddToFreshSales : AddToFreshSales_DefaultValue,
          excludeFromSalesIncentive: CanUpdateExcludeFromSalesIncentive ? formValues.ExcludeFromSalesIncentive : false,
          quoteDealSourceId: formValues.QuoteDealSourceId,
          oppPercentage: formValues.OPPPercentage,
          plcPercentage: formValues.PLCPercentage,
          deliveryPercentage: formValues.DeliveryPercentage,
          platformPercentage: formValues.PlatformPercentage,
          proposalDesignerPercentage: formValues.ProposalDesignerPercentage,
          coverLetterHtml: formValues.CoverLetterHtml
        );

        try {
          newQuoteId = DbHelper.AbleQuotes.CreateQuote(trans, newQuoteInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("QuoteInfo_CreateNewQuote")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .WithPageUrl(Request.RawUrl)
            .WithProperty("QuoteTitle", formValues.QuoteTitle)
            .WithProperty("ProjectJobNumber", formValues.ProjectJobNumber)
            .WithProperty("CompanyId", formValues.CompanyId)
            .WithProperty("ContactUserId", formValues.ContactUserId)
            .Track();

          ajax.AddDialogMessage("Error creating quote.");
          if (ConfigHelper.IsDevServer) ajax.AppendToCurrentMessage("<br/>" + ex.Message);
          EmailHelper.SendInternalSupportEmail(ex, "QuoteInfo Trying to add new Quote.");
          return false;
        }

        if (!UpdateQuoteTeamUsers(trans, formValues, newQuoteId, ajax)) return false;

        return true; // Commit transaction.
      });

      if (!committed) return false;

      QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(newQuoteId);

      // Send Intercom event for project creation
      if (formValues.IsNewProject) {
        var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(formValues.ProjectJobNumber);
        if (projectInfo != null) {
          var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(formValues.CompanyId, SessionHelper.GetUserInfoOrNull());
          SendEvent(
            intercom => intercom.ProjectCreated()
              .FromSession()
              .WithProject(projectInfo.ProjectId, formValues.ProjectName)
              .WithProjectJobNumber(formValues.ProjectJobNumber)
              .WithCompany(formValues.CompanyId, companyInfo?.CompanyName),
            operationName: "QuoteInfo_ProjectCreated",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["ProjectJobNumber"] = formValues.ProjectJobNumber
            }
          );
        }
      }

      // Send Intercom event for quote creation
      SendEvent(
        intercom => intercom.QuoteCreated()
          .FromSession()
          .WithQuote(newQuoteId, QuoteInfo?.QuoteTitle ?? formValues.QuoteTitle)
          .WithClientCompany(QuoteInfo?.CompanyId, QuoteInfo?.CompanyName ?? "")
          .WithQuoteValue(QuoteInfo?.QuoteItemTotalAmount ?? 0),
        operationName: "QuoteInfo_QuoteCreated",
        requestRawUrl: SystemWeb.RequestRawUrl,
        telemetryProperties: new Dictionary<string, object> {
          ["QuoteId"] = newQuoteId,
          ["QuoteValue"] = QuoteInfo?.QuoteItemTotalAmount ?? 0
        }
      );

      return true;
    }

    bool UpdateQuote_Project(FormValues formValues, AjaxSubmitHelper ajax) {

      bool committed = DbHelper.Common.UsingTransaction(trans => {

        if (!CreateOrUpdateCompany(trans, formValues, ajax)) return false;
        if (!CreateOrUpdateProject(trans, formValues, ajax)) return false;
        if (!CreateOrUpdateContact(trans, formValues, ajax)) return false;

        QuoteInfo.JobNumber = formValues.ProjectJobNumber;
        QuoteInfo.ContactUserId = formValues.ContactUserId;

        if (CanEditQuoteDealSource) {
          QuoteInfo.QuoteDealSourceId = formValues.QuoteDealSourceId;
        }

        return DbHelper.AbleQuotes.UpdateQuote(trans, QuoteInfo);
      });

      if (committed && formValues.IsNewProject) {
        var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(formValues.ProjectJobNumber);
        if (projectInfo != null) {
          var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(formValues.CompanyId, SessionHelper.GetUserInfoOrNull());
          SendEvent(
            intercom => intercom.ProjectCreated()
              .FromSession()
              .WithProject(projectInfo.ProjectId, formValues.ProjectName)
              .WithProjectJobNumber(formValues.ProjectJobNumber)
              .WithCompany(formValues.CompanyId, companyInfo?.CompanyName),
            operationName: "QuoteInfo_UpdateQuote_ProjectCreated",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["ProjectJobNumber"] = formValues.ProjectJobNumber
            }
          );
        }
      }

      return committed;
    }

    bool UpdateQuote_Settings(FormValues formValues) {

      return DbHelper.Common.UsingTransaction(trans => {

        QuoteInfo.QuoteTitle = formValues.QuoteTitle;
        QuoteInfo.QuoteStatusId = formValues.QuoteStatusId;
        QuoteInfo.BrandingOrgId = formValues.BrandingOrgId;
        QuoteInfo.EstimatedStartDateUtc = SessionHelper.UserTimeToUtc(formValues.EstimatedStartDateLocal);
        QuoteInfo.XeroTaxType = DbHelper.XeroTaxType.GetQuoteTaxTypeFromGSTApplicable(formValues.GSTApplicable);

        QuoteInfo.QuoteSalesContentTypeId = formValues.QuoteSalesContentTypeId;
        QuoteInfo.QuoteSalesContentUrlId = formValues.QuoteSalesContentUrlId;
        QuoteInfo.QuoteSalesContentPDFFileName = formValues.QuoteSalesContentPDFFileName;
        QuoteInfo.QuoteSalesContentWebPageUrl = formValues.QuoteSalesContentWebPageUrl;
        QuoteInfo.QwilrUrl = formValues.QwilrUrl;
        QuoteInfo.QwilrPDFUrl = formValues.QwilrPDFUrl;

        if (CanEditFreshSalesOption) QuoteInfo.AddToFreshSales = formValues.AddToFreshSales;
        if (CanUpdateExcludeFromSalesIncentive) QuoteInfo.ExcludeFromSalesIncentive = formValues.ExcludeFromSalesIncentive;

        return DbHelper.AbleQuotes.UpdateQuote(trans, QuoteInfo);
      });
    }

    bool UpdateQuote_Splits(FormValues formValues, AjaxSubmitHelper ajax) {

      if (!CanEditQuoteSplits) return false;

      return DbHelper.Common.UsingTransaction(trans => {

        if (!UpdateQuoteTeamUsers(trans, formValues, QuoteInfo.QuoteId, ajax)) return false;
        if (!UpdatePlatformServices(trans, formValues, QuoteInfo.QuoteId, ajax)) return false;

        QuoteInfo.OwnerUserId = formValues.OwnerUserId;
        QuoteInfo.LeadConsultantUserId = formValues.LeadConsultantUserId;
        QuoteInfo.ProposalDesignerUserId = formValues.ProposalDesignerUserId;

        QuoteInfo.OPPPercentage = formValues.OPPPercentage;
        QuoteInfo.PLCPercentage = formValues.PLCPercentage;
        QuoteInfo.DeliveryPercentage = formValues.DeliveryPercentage;
        QuoteInfo.PlatformPercentage = formValues.PlatformPercentage;
        QuoteInfo.ProposalDesignerPercentage = formValues.ProposalDesignerPercentage;

        return DbHelper.AbleQuotes.UpdateQuote(trans, QuoteInfo);
      });
    }

    bool UpdateQuote_Components(FormValues formValues, AjaxSubmitHelper ajax) {

      return DbHelper.Common.UsingTransaction(trans => {

        return UpdateQuoteItems(trans, formValues, QuoteInfo.QuoteId, ajax);
      });
    }

    bool UpdateQuote_CoverLetter(FormValues formValues) {

      return DbHelper.Common.UsingTransaction(trans => {

        QuoteInfo.CoverLetterHtml = formValues.CoverLetterHtml;

        return DbHelper.AbleQuotes.UpdateQuote(trans, QuoteInfo);
      });
    }

    bool UpdateQuote_Info(FormValues formValues) {

      return DbHelper.Common.UsingTransaction(trans => {

        QuoteInfo.QuoteNotes = formValues.QuoteNotes;

        return DbHelper.AbleQuotes.UpdateQuote(trans, QuoteInfo);
      });
    }

    void CopyQuote(AjaxSubmitHelper ajax) {

      var quoteTitle = ajax.CheckFieldRegex(FormFields.CopyQuoteTitle, "Quote Title", AppHelper.Regex.GeneralText, false, "Use plain characters for Quote Title.");
      if (ajax.BadFieldCount > 0) return;

      if (quoteTitle.IsNullOrEmpty()) {
        ajax.AddDialogMessage("Please provide a title for the new quote.");
        return;
      }

      int newQuoteId;
      try {
        newQuoteId = DbHelper.AbleQuotes.CopyQuoteAndItems(null, QuoteInfo.QuoteId, quoteTitle);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("QuoteInfo_CopyQuote")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithPageUrl(Request.RawUrl)
          .WithProperty("QuoteId", QuoteInfo?.QuoteId)
          .WithProperty("NewQuoteTitle", quoteTitle)
          .Track();
        ajax.AddDialogMessage("Problem copying Quote - please reload page and try again.<br/>");
        return;
      }

      ajax.SetRedirectUrl(PathHelper.Pages.QuoteList(), "Quote Copied.", AjaxSubmitHelper.PageMessageType.SuccessToast, true);
    }

    void DeleteQuote(AjaxSubmitHelper ajax) {
      if (DbHelper.AbleQuotes.QuoteHasDependents(QuoteInfo.QuoteId)) {
        ajax.AddDialogMessage("Cannot delete - quote items are attached to components.");
        return;
      }
      try {
        DbHelper.AbleQuotes.DeleteQuote(QuoteInfo.QuoteId);
      } catch { }
      ajax.SetRedirectUrl(PathHelper.Pages.QuoteList(), "Quote Deleted.", AjaxSubmitHelper.PageMessageType.SuccessToast, true);

    }

    public string GetTeamMembersDropdownHtml() {

      var teamMemberUserIds = new List<int>();

      if (QuoteInfo.QuoteTeamUsers != null) {
        foreach (var user in QuoteInfo.QuoteTeamUsers) teamMemberUserIds.Add(user.UserId);
      }

      // Adding by default the user who's creating the quote
      if (IsNewQuote && (teamMemberUserIds == null || teamMemberUserIds.Count == 0)) {
        teamMemberUserIds.Add(userInfo.UserId);
      }

      return WebHelper.GetPartnerDropdown(new WebHelper.PartnerDropdownInfo() {
        PartnerInfoList = PartnerList,
        FormName = FormFields.TeamUserIds,
        IsReadOnly = !CanEditQuote,
        SelectedPartnerUserId = IsNewQuote ? userInfo.UserId : (int?)null,
        CanViewHiddenPartners = CanViewHiddenPartners,
        CanViewInactivePartners = CanViewInactivePartners,
        TeamMemberIdList = teamMemberUserIds,
        DataAttrs = new WebHelper.DataAttributes(("width", "200px")),
        DropdownSelect = WebHelper.PartnerDropdownSelect.Multiple,
        DropdownPurpose = WebHelper.PartnerDropdownPurpose.TeamUserSelection
      });
    }

    void GetProductsWarningMsgs(AjaxSubmitHelper ajax) {
      string warningMessagesHtml = "<ul>";

      var productsAdded = new List<int>();
      var prodNums = new List<int>();

      foreach (var key in Request.Form.AllKeys) {

        if (key.StartsWith(FormFields.ProdKey_Prefix)) {

          string numstr = key.RegexMatchStringOrNull("[0-9]+");
          if (!int.TryParse(numstr, out int prodNum)) continue;
          if (productsAdded.Contains(prodNum)) continue;

          prodNums.Add(prodNum);

          var product = new FormValues.QuoteItem() {
            IsNote = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_IsNote) == "true",
            ProductId = WebHelper.GetFormValue(FormFields.ProdKey_Prefix + prodNum + FormFields.ProdKey_Id).ToIntOrDefault(0)
          };

          // Skip heading products.
          if (!product.IsNote && !productsAdded.Contains(product.ProductId)) productsAdded.Add(product.ProductId);
        }
      }

      bool hasSubscriptionId = false, requiresSubscriptionId = false;
      var currentProducts = new List<DbHelper.Products.ProductInfo>();
      foreach (var prod in productsAdded) {
        if (true) {
          var dbprod = ProductOptionsForQuote.Find(p => p.ProductId == prod);
          if (dbprod != null) {
            currentProducts.Add(dbprod);
            if (!dbprod.QuoteComponentWarningMessage.IsNullOrEmptyOrWhitespace()) {
              // Check if this message hasn't been added in another iteration.
              if (!warningMessagesHtml.Contains(dbprod.QuoteComponentWarningMessage)) {
                // Add message to the list.
                warningMessagesHtml += "<li>" + dbprod.QuoteComponentWarningMessage.HTMLEncode() + "</li>";
              }
            }

            if (dbprod.RequiresSubscription) {
              requiresSubscriptionId = true;
            } else if (dbprod.SubscriptionId != null) {
              hasSubscriptionId = true;
            }
          }
        }
      }

      if (currentProducts != null) {
        // Check if a product with category of Subscription is not contained when ProductCategoryId is 1, 2 or 3.
        if ((requiresSubscriptionId && !hasSubscriptionId)
        || (currentProducts.Exists(x => x.ProductCategoryId.Equals((int)DbHelper.Products.ProductCategory.CoachingIndividual) == true
        || x.ProductCategoryId.Equals((int)DbHelper.Products.ProductCategory.CoachingOnline) == true
        || x.ProductCategoryId.Equals((int)DbHelper.Products.ProductCategory.GroupSessions) == true)
          && !currentProducts.Exists(x => x.ProductCategoryId.Equals((int)DbHelper.Products.ProductCategory.Subscription) == true))) {
          // Add Warning Message mentioning subscription product is missing.
          warningMessagesHtml += $"<li> {WarningMessage_MissingSubscription} </li>";
        }
      }

      warningMessagesHtml += "</ul>";
      warningMessagesHtml = warningMessagesHtml.Contains("</li>") ? warningMessagesHtml : string.Empty;

      ajax.AddReturnValue(AjaxReturnData.ProdWarningMsgs, warningMessagesHtml);
    }

    public string GetProjectSplits_Roles() {

      if (PlatformServiceList == null) return "";

      string html = "";
      int? clientLeadUserId = QuoteInfo.CompanyInfo?.ClientLeadUserId;
      var clientLeadInfo = DbHelper.AbleUser.GetBasicInfoById(clientLeadUserId ?? ConfigHelper.UserId.Unassigned, DbHelper.AbleUser.RegisteredFilter.Any);

      var clientLeadFeeInfo = PlatformServiceList.Find(x => x.PlatformServiceId == DbHelper.PlatformService.PlatformServiceFeeIds.IntegralClientOwner);
      if (clientLeadFeeInfo != null) {
        html += $@"
          <tr class=""{DataAttrs.ClientLeadUserInfo_Class}"">
            <td>{clientLeadFeeInfo.ServiceDescription}{GetTooltipText(clientLeadFeeInfo)}</td>
            <td>{GetPercentageLabelHtml(clientLeadFeeInfo.ServiceFeePercent)}</td>
            <td colspan=""2"">{WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(clientLeadInfo, PathHelper.Images.UserPhotoSize.Thumbnail, true), clientLeadInfo.GetFullName(), clientLeadUserId)}</td>
          </tr>";
      }

      // Deal owner selection and percentage
      html += $@"
        <tr>
          <td>Deal Owner</td>
          <td>{WebHelper.GetPercentInput("", FormFields.OPPPercentage, QuoteInfo.OPPPercentage, 0, 0, 4, "", !CanEditQuoteSplits, true)}</td>
          <td colspan=""2"">"
            + GetPartnerDropdown(
                formName: FormFields.OwnerUserId,
                selectedUserId: QuoteInfo.OwnerUserId,
                feePercentage: null,
                labelText: null,
                dropdownSelect: WebHelper.PartnerDropdownSelect.Single) + $@"
          </td>
        </tr>";

      var proposalDesignerFeeInfo = PlatformServiceList.Find(x => x.PlatformServiceId == DbHelper.PlatformService.PlatformServiceFeeIds.ProposalDesigner);
      if (proposalDesignerFeeInfo != null) {
        html += $@"
          <tr>
            <td>{proposalDesignerFeeInfo.ServiceDescription}{GetTooltipText(proposalDesignerFeeInfo)}</td>
            <td>{WebHelper.GetPercentInput("", FormFields.ProposalDesignerPercentage, IsNewQuote ? proposalDesignerFeeInfo.ServiceFeePercent : QuoteInfo.ProposalDesignerPercentage, 0, 0, 4, "", !CanEditQuoteSplits, true)}</td>
            <td colspan=""2"">"
              + GetPartnerDropdown(
                  formName: FormFields.ProposalDesignerUserId,
                  selectedUserId: QuoteInfo.ProposalDesignerUserId,
                  feePercentage: proposalDesignerFeeInfo.ServiceFeePercent,
                  labelText: null,
                  dropdownSelect: WebHelper.PartnerDropdownSelect.Single) + $@"
            </td>
          </tr>";
      }

      // Project Lead selection and percentage
      html += $@"
        <tr>
          <td>Project Lead</td>
          <td>{WebHelper.GetPercentInput("", FormFields.PLCPercentage, QuoteInfo.PLCPercentage, 0, 0, 4, "", !CanEditQuoteSplits, true)}</td>
          <td colspan=""2"">"
          + GetPartnerDropdown(
              FormFields.LeadConsultantUserId,
              QuoteInfo.LeadConsultantUserId,
              null,
              null,
              WebHelper.PartnerDropdownSelect.Single) + $@"
          </td>
        </tr>";

      return html.EnsureStartsWith("<tbody>", StringExt.Ensure.Always).EnsureEndsWith("</tbody>", StringExt.Ensure.Always);
    }

    public string GetProjectSplits_Services() {

      if (PlatformServiceList == null) return "";

      string html = "";

      var coordinationSupportFeeInfo = PlatformServiceList.Find(x => x.PlatformServiceId == DbHelper.PlatformService.PlatformServiceFeeIds.CoordinationSupport);
      if (coordinationSupportFeeInfo != null) {
        bool currentlyIncluded = PlatformServicesForQuote != null && PlatformServicesForQuote.Exists(ps => ps.PlatformServiceId == coordinationSupportFeeInfo.PlatformServiceId);
        html += $@"
          <tr>
            <td>{coordinationSupportFeeInfo.ServiceDescription}{GetTooltipText(coordinationSupportFeeInfo)}</td>
            <td>{GetPercentageLabelHtml((currentlyIncluded ? coordinationSupportFeeInfo.ServiceFeePercent : 0), FormFields.PlatformFee_CoordinationSupport)}</td>
            <td>{WebHelper.GetAvatarForTable_User(PathHelper.Images.CoordinationTeamAvatar(), "PC Team", null)}</td>
            <td>
              {WebHelper.CustomCheckBox(
                new WebHelper.CheckboxInfo() {
                  InputName = FormFields.PlatformServiceIds,
                  Value = coordinationSupportFeeInfo.PlatformServiceId.ToString(),
                  Checked = currentlyIncluded,
                  IsReadOnly = !CanEditQuoteSplits,
                  DataAttributes = new WebHelper.DataAttributes(
                    (DataAttrs.Percent, ((int)(coordinationSupportFeeInfo.ServiceFeePercent * 100)).ToString()),
                    (DataAttrs.TargetFormClass, FormFields.PlatformFee_CoordinationSupport)
                  )
                }
              )}
            </td>
          </tr>";
      }

      var RTO_ManagerFeeInfo = PlatformServiceList.Find(x => x.PlatformServiceId == DbHelper.PlatformService.PlatformServiceFeeIds.RTO_Project);
      if (RTO_ManagerFeeInfo != null) {
        bool currentlyIncluded = PlatformServicesForQuote != null && PlatformServicesForQuote.Exists(ps => ps.PlatformServiceId == RTO_ManagerFeeInfo.PlatformServiceId);
        html += $@"
          <tr>
            <td>{RTO_ManagerFeeInfo.ServiceDescription}{GetTooltipText(RTO_ManagerFeeInfo)}</td>
            <td>{GetPercentageLabelHtml((currentlyIncluded ? RTO_ManagerFeeInfo.ServiceFeePercent : 0), FormFields.PlatformFee_RTO)}</td>
            <td>{WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(ConfigHelper.RTO_ManagerInfo.FirstName, ConfigHelper.RTO_ManagerInfo.LastName, PathHelper.Images.UserPhotoSize.Thumbnail, true), ConfigHelper.RTO_ManagerInfo.FirstName + " " + ConfigHelper.RTO_ManagerInfo.LastName, ConfigHelper.RTO_ManagerInfo.UserId)}</td>
            <td>
              {WebHelper.CustomCheckBox(
                new WebHelper.CheckboxInfo() {
                  InputName = FormFields.PlatformServiceIds,
                  Value = RTO_ManagerFeeInfo.PlatformServiceId.ToString(),
                  Checked = currentlyIncluded,
                  IsReadOnly = !CanEditQuoteSplits,
                  DataAttributes = new WebHelper.DataAttributes(
                    (DataAttrs.Percent, ((int)(RTO_ManagerFeeInfo.ServiceFeePercent * 100)).ToString()),
                    (DataAttrs.TargetFormClass, FormFields.PlatformFee_RTO)
                  )
                }
              )}
            </td>
          </tr>";
      }

      var platformFeeInfo = PlatformServiceList.Find(x => x.PlatformServiceId == DbHelper.PlatformService.PlatformServiceFeeIds.PlatformBaseFee);
      if (platformFeeInfo != null) {
        html += $@"
          <tr>
            <td>{platformFeeInfo.ServiceDescription}{GetTooltipText(platformFeeInfo)}</td>
            <td>{GetPercentageLabelHtml(platformFeeInfo.ServiceFeePercent)}</td>
            <td colspan=""2"">{WebHelper.GetAvatarForTable_User(PathHelper.Images.AbleFavicon(), "Able Platform", null)}</td>
          </tr>";
      }

      return html.EnsureStartsWith("<tbody>", StringExt.Ensure.Always).EnsureEndsWith("</tbody>", StringExt.Ensure.Always);
    }

    public string GetProjectSplits_Total() {

      if (PlatformServiceList == null) return "";

      string rowHtml = "";

      // Delivery Team percentage
      rowHtml += $@"
        <tr>
          <td>Delivery Team</td>
          <td>{GetPercentageLabelHtml(QuoteInfo.DeliveryPercentage, FormFields.DeliveryPercentage)}</td>
          <td colspan=""2"">{WebHelper.GetAvatarForTable_User(PathHelper.Images.CoordinationTeamAvatar(), "Delivery Team", null)}</td>
        </tr>";

      return rowHtml.EnsureStartsWith("<tbody>", StringExt.Ensure.Always).EnsureEndsWith("</tbody>", StringExt.Ensure.Always);
    }

    public string GetUserDropdownOptions(int? selectedUserId, decimal? feePercentage) {
      string html = "<option value=\"\">[Unassigned]</option>";
      foreach (var user in PartnerList) {
        //decimal percentageFee = feePercentage.HasValue ? (decimal)feePercentage : user.PlatformFeePercent.GetValueOrDefault(DefaultBaseFeePercent);
        string feeData = feePercentage.HasValue ? " data-" + DataAttrs.PlatFee + "=\"" + ((int)((decimal)feePercentage * 100)).ToString() + "\"" : "";
        html += "<option ";
        if (selectedUserId != null && user.UserId == selectedUserId) html += "selected ";
        html += " value=\"" + user.UserId + "\" " + feeData + ">" + (user.FirstName + " " + user.LastName).HTMLEncode() + "</option>";
      }
      return html;
    }

    public string GetCompanyLeadDropdownHtml() {
      return GetPartnerDropdown(
        formName: FormFields.ClientLeadUserId,
        selectedUserId: QuoteInfo?.CompanyInfo?.ClientLeadUserId,
        feePercentage: null,
        labelText: "Client Lead:",
        dropdownSelect: WebHelper.PartnerDropdownSelect.Single,
        inputCols: 5);
    }

    private string GetPartnerDropdown(string formName, int? selectedUserId, decimal? feePercentage, string labelText, WebHelper.PartnerDropdownSelect dropdownSelect, int? inputCols = null) {

      var dataAttrs = feePercentage.HasValue ? new WebHelper.DataAttributes((DataAttrs.PlatFee, ((int)((decimal)feePercentage * 100)).ToString())) : null;

      bool canEditDropdown = (CanEditQuoteSplits && CanChangeSplitRoles) || IsUpdatingAcceptedQuote;

      var partnerDropdownInfo = new WebHelper.PartnerDropdownInfo() {
        PartnerInfoList = PartnerList,
        FormName = formName,
        IsReadOnly = !canEditDropdown,
        LabelText = labelText,
        InputCols = inputCols,
        SelectedPartnerUserId = selectedUserId,
        CanViewHiddenPartners = CanViewHiddenPartners,
        CanViewInactivePartners = CanViewInactivePartners,
        DataAttrs = dataAttrs,
        DropdownSelect = dropdownSelect
      };

      string userSelectHtml = new WebHelper.Form.Select() {
        IsReadOnly = partnerDropdownInfo.IsReadOnly,
        InputName = partnerDropdownInfo.FormName,
        Class = WebHelper.CSSClasses.PartnerDropdownClass,
        TopOptionsHtml = WebHelper.GetPartnerDropdownOptionsHtml(partnerDropdownInfo)
      }.ToHtml();

      if (partnerDropdownInfo.LabelText == null) {
        return userSelectHtml;
      }

      return new WebHelper.Form.FormRow() {
        LabelText = partnerDropdownInfo.LabelText,
        ContentHtml = userSelectHtml
      }.ToHtml();
    }

    public string GetPercentageLabelHtml(decimal feePercent, string customClass = "") {
      return $@"<span data-{DataAttrs.Percent}=""{(int)(feePercent * 100)}"" class=""display-value lblPercentageValue {customClass}"">{feePercent * 100:0}</span>%";
    }

    public string GetTooltipText(DbHelper.PlatformService.ServiceInfo serviceInfo) {
      if (serviceInfo.TooltipText.IsNullOrEmpty()) {
        return string.Empty; // No tooltip if no text in the Database.
      }
      return WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, serviceInfo.ServiceDescription, serviceInfo.TooltipText);
    }
  }
}
