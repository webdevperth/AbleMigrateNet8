using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProjectQuotes : AppCode.PageBaseClasses.ProjectPageBase {

    public List<DbHelper.AbleQuotes.QuoteInfo> QuoteList;
    public bool CanRoleViewQuotes = false, CanRequestQuote, CanCreateQuoteInProject;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Quotes";

      CanRoleViewQuotes = SessionHelper.AppAccess.Quotes.CanCurrentRoleViewQuoteInfo();
      CanCreateQuoteInProject = SessionHelper.AppAccess.Quotes.CanCreateQuoteInProject(ProjectInfo);
      CanRequestQuote = SessionHelper.AppAccess.Quotes.CanRequestQuote();

      QuoteList = DbHelper.AbleQuotes.GetQuotesForLists(SessionHelper.GetUserInfoOrNull(), ProjectInfo).InfoList;

      return Page();
    }

    public string GetQuoteOrProjectName(DbHelper.AbleQuotes.QuoteInfo qi) {
      return qi.QuoteTitle.ValueIfNullOrEmpty(qi.ProjectName);
    }

    public string GetCompanyName(DbHelper.AbleQuotes.QuoteInfo qi) {
      return qi.CompanyInfo?.CompanyName ?? "";
    }

    public string GetQuoteViewIcon(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
      var icon = WebHelper.ActionButtonTypeEnum.view;

      if (SessionHelper.AppAccess.Quotes.CanViewQuoteInfo(quoteInfo)) {
        icon = WebHelper.ActionButtonTypeEnum.edit;
      }

      return WebHelper.GetActionButton(icon, "", "");
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

    public string GetRequestQuoteButtonHtml() {
      return "<button class=\"btn btn-primary\" id=\"btnRequestQuote\">Request Quote</button>";
    }

    public string GetEmptyStateHtml() {
      string title = "Quotes";
      string noQuoteText = "No quotes yet. ";

      if (CanCreateQuoteInProject) {

        return WebHelper.GetEmptyStatePageHtml(
          title: title,
          description: noQuoteText + "Add the first quote to get started!",
          addActionHtml: CanCreateQuoteInProject,
          actionButtonText: "Add Quote",
          actionButtonPath: PathHelper.Pages.ProjectQuoteCreate(ProjectInfo.JobNumber));

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

  }
}
