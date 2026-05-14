using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class ActivityChart : AppCode.PageBaseClasses.LoggedInPageBase {

    public const int ChartMonths = 6;
    public const int PixelHeight = 320;
    public const string UnitsButtonsName = "chartUnits";

    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo;
    public PathHelper.ActivityChartUnits SelectedChartUnits;
    public List<WebHelper.Charts.StackLegendItem> StackLegend;
    public List<WebHelper.Charts.StackedBar> Bars;
    public string UnitsButtonsHtml;
    public Guid PartialGuid = Guid.NewGuid();
    public bool ShowEmptyStatePageHtml, CanSendSurvey;

    protected void Page_Load(object sender, EventArgs e) {

      CanSendSurvey = SessionHelper.AppAccess.Programs.CanSendSurveys(ProgramInfo);

      int urlCompanyId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.CompanyId) ?? 0;
      int urlProgramId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ProgramJobId) ?? 0;

      List<DbHelper.Reports.Activity.InteractionsForMonth> interactionData = null;

      TimeHelper.GetFirstAndLastDaysOfMonth(
        DateTime.UtcNow.AddMonths(1 - ChartMonths),
        DateTime.UtcNow,
        out DateTime firstDayOfStartMonth, out _);

      if (urlCompanyId > 0) {

        CompanyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(urlCompanyId, SessionHelper.GetUserInfoOrNull());

        if (CompanyInfo == null || !SessionHelper.AppAccess.Companies.CanViewOrganisationOverview(CompanyInfo)) {
          ShowEmptyStatePageHtml = true;
          return;
        }

        // Get data.
        interactionData = DbHelper.Reports.Activity.GetInteractionsByMonthForCompany(CompanyInfo.CompanyId, firstDayOfStartMonth);

      } else if (urlProgramId > 0) {

        ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(urlProgramId);

        if (ProgramInfo == null || !SessionHelper.AppAccess.Programs.CanViewProgramInsights(ProgramInfo)) {
          ShowEmptyStatePageHtml = true;
          return;
        }

        // Get data.
        interactionData = DbHelper.Reports.Activity.GetInteractionsByMonthForProgram(ProgramInfo.ProgramJobId, firstDayOfStartMonth);

      }

      // If list is empty, return
      if (interactionData.IsNullOrEmpty()) {
        ShowEmptyStatePageHtml = true;
        return;
      }

      SelectedChartUnits = WebHelper.GetQueryStringEnum(PathHelper.AbleUrlKeys.ChartUnits, PathHelper.ActivityChartUnits.Minutes);

      // Get all interaction types.
      var interactionTypes = DbHelper.Interactions.GetInteractionTypesByChartOrder();

      // Get interaction type IDs present in the data.
      var usedTypeIds = interactionData.SelectMany(id => id.Interactions.Select(i => i.InteractionTypeId)).ToList();

      // Remove unused interaction types.
      interactionTypes.RemoveAll(t => !usedTypeIds.Contains(t.InteractionTypeId));

      // Info for stacked bar chart.

      StackLegend = new List<WebHelper.Charts.StackLegendItem>(
        interactionTypes
        .OrderBy(i => i.ChartOrder)
        .Reverse()
        .Select(i => new WebHelper.Charts.StackLegendItem(i.InteractionTypeId, i.InteractionName, i.ChartCSSColour))
        );

      Bars = new List<WebHelper.Charts.StackedBar>(
        interactionData
        .Select(
          mth => new WebHelper.Charts.StackedBar(
            mth.MonthStart.Ticks, // used as a reference to this item
            mth.MonthStart.ToString("MMM yyyy"),
            mth.Interactions.Select(i => new WebHelper.Charts.StackedBar.StackItem(
              i.InteractionTypeId, SelectedChartUnits == PathHelper.ActivityChartUnits.Minutes ? i.TotalMinutes : i.TotalInteractions)).ToList()
          )
        )
      );
    }

    public string GetUnitsButtonsHtml() {

      return WebHelper.GetButtonGroup(
        UnitsButtonsName,
        new List<WebHelper.ButtonGroupButton>() {
                new WebHelper.ButtonGroupButton("Minutes", PathHelper.ActivityChartUnits.Minutes.ToString()),
                new WebHelper.ButtonGroupButton("Interactions", PathHelper.ActivityChartUnits.Interactions.ToString())},
        SelectedChartUnits.ToString());
    }
  }
}
