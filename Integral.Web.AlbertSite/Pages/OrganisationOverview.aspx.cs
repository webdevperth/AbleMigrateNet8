using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class OrganisationOverview : AppCode.PageBaseClasses.CompanyInfoBase {

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Organisation Overview";
    }

    public string GetMonthlyProgressChartJson() {

      const int chartMonths = 6;
      DateTime firstDayOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
      DateTime endMonth = firstDayOfThisMonth;
      DateTime startMonth = firstDayOfThisMonth.AddMonths(-(chartMonths - 1));

      var progressData = DbHelper.Reports.Company.GetMonthlyProgress(CompanyInfo.CompanyId, startMonth, endMonth);
      var chartDto = new MonthlyProgressChartDto();

      // Add month labels.
      for (int i = 0; i < chartMonths; i++) {
        chartDto.labels.Add((startMonth.AddMonths(i)).ToString("MMM yyyy"));
      }

      // Get unique SurveyTypeCodes from the query.
      var distinctSurveyTypeCodes = progressData.Select(p => (p.SurveyTypeCode, p.ProgressChartLineColor)).Distinct().ToList();

      foreach (var surveyTypeCode in distinctSurveyTypeCodes) {

        var dataSet = new MonthlyProgressChartDto.dataset() {
          borderColor = $"#{surveyTypeCode.ProgressChartLineColor}",
          backgroundColor = $"#{surveyTypeCode.ProgressChartLineColor}",
          pointBorderColor = $"#{surveyTypeCode.ProgressChartLineColor}",
          pointBackgroundColor = $"#{surveyTypeCode.ProgressChartLineColor}",
          pointHoverBorderColor = $"#{surveyTypeCode.ProgressChartLineColor}",
          pointHoverBackgroundColor = $"#{surveyTypeCode.ProgressChartLineColor}",
          label = surveyTypeCode.SurveyTypeCode,
          data = new List<decimal?>(Enumerable.Repeat((decimal?)null, chartMonths)) // Init months with nulls (no score for month)
        };

        // Assign scores from db to month for this survey type using MonthIndex (0-based difference from the start month)
        var monthData = progressData.FindAll(p => p.SurveyTypeCode == surveyTypeCode.SurveyTypeCode);

        foreach (var month in monthData) {
          dataSet.data[month.MonthIndex] = month.ScoreAvg.RoundAwayFromZero(1);
        }

        chartDto.datasets.Add(dataSet);
      }

      return JsonConvert.SerializeObject(chartDto, Formatting.Indented);
    }

    public class MonthlyProgressChartDto {
      public List<string> labels = new List<string>();
      public List<dataset> datasets = new List<dataset>();
      public class dataset {
        public string label;
        public string borderColor;
        public string backgroundColor;
        public string pointBorderColor;
        public string pointBackgroundColor;
        public string pointHoverBorderColor;
        public string pointHoverBackgroundColor;
        public int pointRadius = 4;
        public int pointBorderWidth = 0;
        public decimal lineTension = 0.1M;
        public bool fill = false;
        public bool spanGaps = true;
        public List<decimal?> data;
      }
    }
  }
}
