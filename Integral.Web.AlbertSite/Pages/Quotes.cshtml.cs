using Newtonsoft.Json;
using System;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Quotes : AppCode.PageBaseClasses.LoggedInPageModel {

    public const int Min_Search_Length = 4;
    public const int Default_Rows_Per_Page = 10;
    public DbHelper.AbleQuotes.QuoteInfo thisQuoteInfo = null;
    public SearchInfo searchInfo = new SearchInfo();
    public bool CanRoleViewQuotes = false, CanRequestQuote, CanViewQuoteListActiveToggle, UserHasAnyQuotes, CanCreateQuote;

    public class SearchInfo {
      public string SearchTerm { get; set; }
      public int RowsPerPage { get; set; }
      public int GetPage { get; set; }
      public bool HasSearchTerm { get; set; }
      public bool StatusClosed { get; set; }
    }

    enum PageModeEnum { DisplayPage, GetQuoteList }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Quotes";

      PageModeEnum pagemode = SystemWeb.RequestQueryStringContains(PathHelper.AbleUrlKeys.QuotesSearchTerm)
        ? PageModeEnum.GetQuoteList
        : PageModeEnum.DisplayPage;

      CanRoleViewQuotes = SessionHelper.AppAccess.Quotes.CanCurrentRoleViewQuoteInfo();
      CanRequestQuote = SessionHelper.AppAccess.Quotes.CanRequestQuote();
      CanViewQuoteListActiveToggle = SessionHelper.AppAccess.Quotes.CanViewQuoteListActiveToggle();
      CanCreateQuote = SessionHelper.AppAccess.Quotes.CanCreateQuote();

      if (pagemode == PageModeEnum.DisplayPage) {
        var quotes = DbHelper.AbleQuotes.GetQuotesForLists(
          SessionHelper.GetUserInfoOrNull(),
          DbHelper.AbleQuotes.QuoteListMode.Any,
          null,
          0, 1 // Get 1 row only - we just want to know if the user can see any quotes.
        );
        UserHasAnyQuotes = quotes?.InfoList?.Count > 0;
      }

      if (pagemode == PageModeEnum.GetQuoteList) {

        searchInfo.SearchTerm = (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuotesSearchTerm) ?? SessionHelper.AppState.Quotes.Search_SearchFor ?? "").URLDecode().TrimWhitespace();
        searchInfo.HasSearchTerm = !searchInfo.SearchTerm.IsNullOrEmpty();
        if (!WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuoteStatusToggle).IsNullOrEmpty()) {
          searchInfo.StatusClosed = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuoteStatusToggle) == PathHelper.AbleUrlValues.QuoteStatusToggleValue_Closed;
        } else {
          searchInfo.StatusClosed = SessionHelper.AppState.Programs.Search_StatusClosed;
        }
        searchInfo.GetPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.GetPage).ToIntOrDefault(SessionHelper.AppState.Quotes.Search_CurrentPage.GetValueOrDefault(1));
        searchInfo.RowsPerPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PerPage).ToIntOrDefault(Default_Rows_Per_Page);

        var quotes = GetQuoteList();
        WebHelper.WriteAndEnd(JsonConvert.SerializeObject(quotes), WebHelper.HttpContentType.json);
        return new EmptyResult();
      }

      return Page();
    }

    public string GetRequestQuoteButtonHtml() {
      return "<button class=\"btn btn-primary\" id=\"btnRequestQuote\">Request Quote</button>";
    }

    public string GetEmptyStateHtml() {

      string title = "Quotes";
      string noQuoteText = "No quotes yet. ";

      if (CanCreateQuote) {

        return WebHelper.GetEmptyStatePageHtml(
          title: title,
          description: noQuoteText + "Add the first quote to get started!",
          addActionHtml: CanCreateQuote,
          actionButtonText: "Add Quote",
          actionButtonPath: PathHelper.Pages.QuoteCreate());

      } else if (CanRequestQuote) {

        return WebHelper.GetEmptyStatePageHtml(
          title: title,
          description: noQuoteText + "Request the first quote to get started!",
          addActionHtml: CanRequestQuote,
          customActionHtml: GetRequestQuoteButtonHtml());

      } else {

        return WebHelper.GetEmptyStatePageHtml(title, noQuoteText);
      }
    }

    public string GetRowLinkUrl(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
      if (SessionHelper.AppAccess.Quotes.CanViewQuoteInfo(quoteInfo)) {
        return PathHelper.Pages.QuoteDetails(quoteInfo.PublicGuid);
      } else {
        return PathHelper.Pages.QuotePublicView(quoteInfo.PublicGuid, true);
      }
    }

    public string GetStatusBadge(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
      if (quoteInfo.IsAccepted) {
        return WebHelper.GetStatusBadge(DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusText);
      }
      return WebHelper.GetStatusBadge(quoteInfo.QuoteStatusText);
    }

    public string GetQuoteViewIcon(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
      var icon = WebHelper.ActionButtonTypeEnum.view;
      if (SessionHelper.AppAccess.Quotes.CanViewQuoteInfo(quoteInfo)) {
        icon = WebHelper.ActionButtonTypeEnum.edit;
      }
      return WebHelper.GetActionButton(icon, "", "");
    }

    DbHelper.InfoListPaged<ListResults> GetQuoteList() {
      // If user is NOT admin, a blank search means show all programs with coachees assigned to that user.
      // So an admin user always requires a search term. Non-admin search term can be blank.
      // For non-admins, only programs found and those containing coachees assigned to the coach(user).

      SessionHelper.AppState.Quotes.Search_SearchFor = searchInfo.SearchTerm;
      SessionHelper.AppState.Quotes.Search_CurrentPage = searchInfo.GetPage;
      SessionHelper.AppState.Quotes.Search_StatusClosed = searchInfo.StatusClosed;

      var quoteStatus = searchInfo.StatusClosed ? DbHelper.AbleQuotes.QuoteListMode.Completed : DbHelper.AbleQuotes.QuoteListMode.Active;
      if (!CanViewQuoteListActiveToggle) quoteStatus = DbHelper.AbleQuotes.QuoteListMode.Any; // If user cannot toggle status, then show all quotes regardless of status.

      var quotes = DbHelper.AbleQuotes.GetQuotesForLists(
        SessionHelper.GetUserInfoOrNull(),
        quoteStatus,
        searchInfo.SearchTerm,
        (searchInfo.GetPage - 1) * searchInfo.RowsPerPage,
        searchInfo.RowsPerPage
      );

      // Convert quote data from db to a new InfoListPaged object
      // which has a list of <ListResults> instead of <QuoteInfo>
      // because we don't want to send all data columns to the UI,
      // just a subset of them specifically for the list.
      var listResults = quotes.CustomCopy(quoteInfo => {
        return new ListResults(
          GetRowLinkUrl(quoteInfo),
          quoteInfo.JobNumber + (quoteInfo.DeletedUtc != null ? " D" : ""),
          WebHelper.GetListViewLocatorColumnHtml(quoteInfo.QuoteTitle.ValueIfNullOrEmpty(quoteInfo.ProjectName), quoteInfo.JobNumber, quoteInfo.CompanyInfo.CompanyName),
          quoteInfo.CompanyInfo.CompanyName,
          quoteInfo.CreatedUtc.SpecifyKind(DateTimeKind.Utc),
          GetStatusBadge(quoteInfo),
          quoteInfo.IsAccepted,
          quoteInfo.ClientAcceptedUtc.SpecifyKindOrNull(DateTimeKind.Utc),
          quoteInfo.QuoteItemCount,
          quoteInfo.IsAccepted ? quoteInfo.ClientAcceptedAmount.GetValueOrDefault(0) : quoteInfo.QuoteItemTotalAmount,
          WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(quoteInfo, PathHelper.Images.UserPhotoSize.Thumbnail, true), quoteInfo.OwnerFullName, quoteInfo.OwnerUserId),
          GetQuoteViewIcon(quoteInfo)
        );
      });

      return listResults;
    }

    class ListResults {
      public string QuoteUrl { get; private set; }
      public string JobNumber { get; private set; }
      public string ProjectName { get; private set; }
      public string CompanyName { get; private set; }
      public DateTime CreatedUtc { get; private set; }
      public string StatusText { get; private set; }
      public bool IsAccepted { get; private set; }
      public DateTime? ClientAcceptedUtc { get; private set; }
      public int QuoteItemCount { get; private set; }
      public decimal QuoteItemTotalAmount { get; private set; }
      public string DealOwner { get; private set; }
      public string QuoteViewIcon { get; private set; }
      public ListResults(
        string quoteUrl,
        string jobNumber,
        string projectName,
        string companyName,
        DateTime createdUtc,
        string statusText,
        bool isAccepted,
        DateTime? clientAcceptedUtc,
        int quoteItemCount,
        decimal quoteItemTotalAmount,
        string dealOwner,
        string quoteViewIcon

      ) {
        QuoteUrl = quoteUrl;
        JobNumber = jobNumber;
        ProjectName = projectName;
        CompanyName = companyName;
        CreatedUtc = createdUtc;
        StatusText = statusText;
        IsAccepted = isAccepted;
        ClientAcceptedUtc = clientAcceptedUtc;
        QuoteItemCount = quoteItemCount;
        QuoteItemTotalAmount = quoteItemTotalAmount;
        DealOwner = dealOwner;
        QuoteViewIcon = quoteViewIcon;
      }
    }

  }
}
