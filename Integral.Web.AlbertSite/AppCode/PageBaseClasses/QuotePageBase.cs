using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class QuotePageBase : LoggedInPageModel {

    public bool IsNewQuote { get; private set; }
    public bool CanCreateQuote { get; private set; } = false;
    public bool CanViewQuoteInfo { get; private set; } = false;
    public bool CanEditQuote { get; private set; } = false;
    public bool CanEditQuoteDealSource { get; private set; } = false;
    public bool CanCopyQuote { get; private set; } = false;
    public bool CanDeleteQuote { get; private set; } = false;
    public bool CanChangeSplitRoles { get; private set; } = false;
    public bool IsAccepted { get; private set; } = false;
    public bool IsQuoteFromProjectArea { get; private set; } = false;

    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo { get; protected set; } = null;

    protected override void InitializePage() {

      base.InitializePage();

      if (WebHelper.IsRequestExiting()) return;

      PageTitle = "";
      PageSubtitle = "";
      PageSubSubtitleHTML = "";
      PageSubtitleIsHtml = false;
      IsNewQuote = false;
      QuoteInfo = null;

      // Mirror onto LayoutModel for future ViewComponent consumers. See LayoutModel.cs.
      var layout = LayoutModel.GetCurrent();
      layout.QuoteInfo = QuoteInfo;

      FallbackUrl = PathHelper.Pages.QuoteList();

      Guid urlQuoteGuid;
      if (WebHelper.TryGetQueryStringGuidOrNew(PathHelper.AbleUrlKeys.QuoteGuid, out urlQuoteGuid, out bool isNewQuote)) {
        IsNewQuote = isNewQuote;
      } else {
        SetFallbackRedirect();
        return;
      }
      layout.IsNewQuote = IsNewQuote;

      // ProjectJobNumber is provided if creating quote from the project area.
      string urlProjectJobNumber = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber);

      if (urlProjectJobNumber.IsNullOrEmpty()) {

        ProjectInfo = null;
        layout.ProjectInfo = null;
        CanCreateQuote = SessionHelper.AppAccess.Quotes.CanCreateQuote();

      } else {

        ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(urlProjectJobNumber, SessionHelper.UserInfo);
        layout.ProjectInfo = ProjectInfo;
        if (ProjectInfo == null) {
          SetRedirect(PathHelper.Pages.Projects_List());
          return;
        }

        if (!SessionHelper.AppAccess.Projects.CanViewProject(ProjectInfo)) {
          SetRedirect(PathHelper.Pages.Projects_List());
          return;
        }

        CanCreateQuote = SessionHelper.AppAccess.Quotes.CanCreateQuoteInProject(ProjectInfo);
        IsQuoteFromProjectArea = true;
      }

      if (IsNewQuote) {

        if (!CanCreateQuote) {
          SetFallbackRedirect();
          return;
        }

        CanEditQuote = true;
        CanEditQuoteDealSource = true;
        CanCopyQuote = false;
        CanDeleteQuote = false;
        QuoteInfo = DbHelper.AbleQuotes.GetEmptyQuoteInfo();
        layout.QuoteInfo = QuoteInfo;
        QuoteInfo.OwnerUserId = userInfo.UserId; // Default to current user for new quotes.
        PageSubtitle = "New Quote";
        CanChangeSplitRoles = true;

        if (ProjectInfo != null) {
          // Set project & company related to new quote.
          QuoteInfo.SetProject(ProjectInfo);
          if (ProjectInfo.CompanyId != null) {
            // Default new quote to the project's client company.
            var projectCompany = DbHelper.ClientCompanies.GetBriefCompanyInfoOrNull(ProjectInfo.CompanyId.Value);
            QuoteInfo.SetCompanyInfo(projectCompany);
          }
        }

      } else { // Editing existing quote.

        // If not admin, only give access to quotes "owned" by the current user.
        QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(urlQuoteGuid, userInfo);
        layout.QuoteInfo = QuoteInfo;

        if (QuoteInfo == null) {
          if (ProjectInfo != null) {
            SetRedirect(PathHelper.Pages.ProjectQuotes(ProjectInfo.JobNumber));
          } else {
            SetRedirect(PathHelper.Pages.QuoteList());
          }
          return;
        }

        if (ProjectInfo == null || ProjectInfo.JobNumber != QuoteInfo.JobNumber) {
          ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(QuoteInfo.JobNumber);
          layout.ProjectInfo = ProjectInfo;
        }

        if (QuoteInfo.QuoteTitle.IsNullOrEmpty() && ProjectInfo != null) {
          QuoteInfo.QuoteTitle = ProjectInfo.ProjectName;
        }

        IsNewQuote = false;
        layout.IsNewQuote = IsNewQuote;
        IsAccepted = QuoteInfo.IsAccepted;
        CanViewQuoteInfo = SessionHelper.AppAccess.Quotes.CanViewQuoteInfo(QuoteInfo);
        layout.CanViewQuoteInfo = CanViewQuoteInfo;
        CanCopyQuote = SessionHelper.AppAccess.Quotes.CanCopyQuote(QuoteInfo);
        CanEditQuote = SessionHelper.AppAccess.Quotes.CanEditQuote(QuoteInfo);
        CanEditQuoteDealSource = SessionHelper.AppAccess.Quotes.CanEditQuoteDealSource(QuoteInfo);
        CanDeleteQuote = SessionHelper.AppAccess.Quotes.CanDeleteQuote(QuoteInfo);
        CanChangeSplitRoles = SessionHelper.AppAccess.Quotes.CanChangeSplitRoles(QuoteInfo);

        if (!CanViewQuoteInfo) {
          // User can't view quote, but if user is team member then redirect them to the public quote view page.
          if (QuoteInfo.IsUserTeamMember(userInfo.UserId)) {
            SetRedirect(PathHelper.Pages.QuotePublicView(QuoteInfo.PublicGuid, userInfo.UserGuid, true));
          } else {
            SetFallbackRedirect(); // If user doesn't have access to QuoteInfo and is not a Client or a Team member, go to Fallback page.
          }
          return;
        }

        PageSubtitle = "<a href=\"" + PathHelper.Pages.ProjectInvoicing(QuoteInfo.JobNumber) + "\">" + QuoteInfo.JobNumber.HTMLEncode() + "</a>: " + QuoteInfo.ProjectName.HTMLEncode();
        PageSubtitleIsHtml = true;
      }

      // Check user access to Quote pages.
      if (!CheckUserPageAccess()) {
        SetFallbackRedirect();
        return;
      }
    }

    internal bool CheckUserPageAccess() {
      if (PathHelper.IsCurrentPage(PathHelper.Pages.QuoteDetails(null))) {
        if (!SessionHelper.AppAccess.Quotes.CanCurrentRoleViewQuoteInfo()) return false;
      }
      return true;
    }
  }
}
