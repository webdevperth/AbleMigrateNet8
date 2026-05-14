using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web {

  public partial class WebHelper {

    public partial class Charts {

      public partial class Coachee {

        public static string GetMonthlyActivityChart(int companyId, DateTime startDate, DateTime endDate, int pixelHeight, string extraClasses = null) {

          // Get data.
          var interactionData = DbHelper.Reports.Coachee.GetInteractionsByMonth(companyId, startDate, endDate);

          // Get all interaction types.
          var interactionTypes = DbHelper.Interactions.GetInteractionTypesByChartOrder();
          // Get interaction type IDs present in the data.
          var usedTypeIds = interactionData.SelectMany(id => id.Interactions.Select(i => i.InteractionTypeId)).ToList();
          // Remove unused interaction types.
          interactionTypes.RemoveAll(t => !usedTypeIds.Contains(t.InteractionTypeId));

          // Info for stacked bar chart.
          var stackLegend = new List<StackLegendItem>(interactionTypes.OrderBy(i => i.ChartOrder).Reverse().Select(i => new StackLegendItem(i.InteractionTypeId, i.InteractionName, i.ChartCSSColour)));
          var bars = new List<StackedBar>(
            interactionData.Select(
              mth => new StackedBar(
                mth.MonthStart.Ticks, // used as a reference to this item
                mth.MonthStart.ToString("MMM yyyy"),
                mth.Interactions.Select(i => new StackedBar.StackItem(i.InteractionTypeId, i.TotalMinutes)).ToList()
              )
            )
          );

          return GetStackedBarChart(pixelHeight, stackLegend, bars, extraClasses);
        }

        public static string GetMonthlyProgressChart(int coacheeId, DateTime startMonth, DateTime endMonth, int pixelHeight, string extraClasses = null) {

          var progressData = DbHelper.Reports.Coachee.GetMonthlyProgress(coacheeId, startMonth, endMonth);
          var chartDto = new MonthlyProgressChartDto();

          // Ensure dates are 1st day of start month and last day of end month.
          startMonth = new DateTime(startMonth.Year, startMonth.Month, 1);
          endMonth = new DateTime(endMonth.Year, endMonth.Month, 1).AddMonths(1).AddDays(-1);

          int chartMonths = TimeHelper.GetCalendarMonthsSpanning(startMonth, endMonth);

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

          string canvasID = $"{canvasIDprefix}{GetNextRequestSequentialNumber()}"; // Unique ID to reference this canvas element in the javascript.

          return
            $@"<div style=""height: {pixelHeight}px"" class=""pos-relative {extraClasses.HTMLEncode()}""><canvas id=""{canvasID}""></canvas></div>"
            + GetJQueryScriptBlock($@"
              var ctx = document.getElementById(""{canvasID}"").getContext(""2d"");
              var myChart = new Chart(ctx, {{
                type: 'line',
                options: {{
                  responsive: true, maintainAspectRatio: false, layout: {{ padding: {{ left: -8 }} }},
                  legend: {{ position: 'left', reverse: true, labels: {{ boxWidth: 15, boxHeight: 13, fontSize: 14, fontColor: '#333', padding: 10, usePointStyle: false }} }},
                  scales: {{ yAxes: [{{ ticks: {{ beginAtZero: true, max: {DbHelper.AlbertSurveys.Maximum360Score} }} }}] }}
                }},
                data: {JsonConvert.SerializeObject(chartDto, Formatting.Indented)}
              }});");
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
  }
}
