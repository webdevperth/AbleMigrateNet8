using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Organisations : AppCode.PageBaseClasses.LoggedInPageModel {

    public List<OrgListInfo> orgResults = new List<OrgListInfo>();
    public SearchInfo searchInfo = new SearchInfo();
    public const int Min_Search_Length = 4;
    public bool CanViewOrganisationListView, CanCreateCompany;
    public class SearchInfo {
      public string SearchTerm { get; set; }
      public bool HasSearchTerm { get; set; }
      public bool StatusInactive { get; set; }
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Organisations";

      CanCreateCompany = SessionHelper.AppAccess.Companies.CanCreate();
      CanViewOrganisationListView = SessionHelper.AppAccess.Companies.CanViewOrganisationListView();

      searchInfo.SearchTerm = (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrganisationSearchTerm) ?? SessionHelper.AppState.Organisations.Search_SearchFor ?? "").URLDecode().TrimWhitespace();
      searchInfo.HasSearchTerm = !searchInfo.SearchTerm.IsNullOrEmpty();
      if (!WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrganisationStatusToggle).IsNullOrEmpty()) {
        searchInfo.StatusInactive = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrganisationStatusToggle) == PathHelper.AbleUrlValues.Organisation_Inactive;
      } else {
        searchInfo.StatusInactive = SessionHelper.AppState.Organisations.Search_StatusInactive;
      }

      if (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrganisationSearchTerm) != null) {
        DoOrganisationSearch();
        return new EmptyResult();
      }

      return Page();
    }

    private void DoOrganisationSearch() {

      SessionHelper.AppState.Organisations.Search_SearchFor = searchInfo.SearchTerm;
      SessionHelper.AppState.Organisations.Search_StatusInactive = searchInfo.StatusInactive;

      var companyList = DbHelper.ClientCompanies.GetCompanyList_BySearch(searchInfo.SearchTerm, searchInfo.StatusInactive, SessionHelper.UserInfo);

      orgResults = new List<OrgListInfo>();

      foreach (var cmp in companyList) {

        orgResults.Add(new OrgListInfo() {
          CompanyId = cmp.CompanyId,
          CompanyName = cmp.CompanyName,
          ClientLeadHtml = WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(cmp, PathHelper.Images.UserPhotoSize.Thumbnail, true), cmp.ClientLeadFullName(), cmp.ClientLeadUserId),
          ProgramsActiveAndTotal = cmp.TotalActivePrograms + " / " + cmp.TotalPrograms,
          ParticipantsActiveAndTotal = cmp.TotalActiveCoachees + " / " + cmp.TotalCoachees,
          TotalDealsOutstanding = cmp.TotalDealOutstandingRevenue.ToString("C0"),
          TotalDealsWon = cmp.TotalDealWonRevenue.ToString("C0"),
          TotalDeliveryRevenueHtml = WebHelper.GetProgressBarHtml(cmp.TotalCompletedDeliveryRevenue, cmp.TotalDeliveryRevenue, "", WebHelper.ProgressBarType.Currency),
          TotalSubscriptions = cmp.TotalActiveSubscriptions + " / " + cmp.TotalSubscriptions,
          OnboardingScore = GetOnboardingScore(cmp),
          RowLinkUrl = GetRowLinkUrl(cmp)
        });
      }

      WebHelper.WriteAndEnd(JsonConvert.SerializeObject(orgResults), WebHelper.HttpContentType.json);
    }

    public string GetOnboardingScore(DbHelper.ClientCompanies.AlbertCompanyInfo cmp) {
      if (cmp.NumberOfStaff <= 0 || cmp.TotalCoachees <= 0) return "0%";
      if (cmp.TotalCoachees >= cmp.NumberOfStaff) return "100%";
      return (cmp.TotalCoachees * 100 / cmp.NumberOfStaff).ToString() + "%";
    }

    private string GetRowLinkUrl(DbHelper.ClientCompanies.AlbertCompanyInfo company) {

      if (SessionHelper.AppAccess.Companies.CanViewOrganisationOverview(company)) {
        return PathHelper.Pages.OrganisationOverview(company.CompanyId);
      } else if (SessionHelper.AppAccess.Companies.CanViewOrganisationSettings(company)) {
        return PathHelper.Pages.OrganisationSettings(company.CompanyId);
      } else if (SessionHelper.AppAccess.Companies.CanViewOrganisationIOSReports(company)) {
        return PathHelper.Reports.OrganisationIOSReports(company.CompanyId);
      }
      return "";
    }

    public class OrgListInfo {

      public int CompanyId { get; set; }
      public string CompanyName { get; set; }
      public string ClientLeadHtml { get; set; }
      public string ProgramsActiveAndTotal { get; set; }
      public string ParticipantsActiveAndTotal { get; set; }
      public string TotalDealsOutstanding { get; set; }
      public string TotalDealsWon { get; set; }
      public string TotalDeliveryRevenueHtml { get; set; }
      public string TotalSubscriptions { get; set; }
      public string OnboardingScore { get; set; }
      public string RowLinkUrl { get; set; }
    }
  }
}
