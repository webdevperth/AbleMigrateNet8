using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web {

  public partial class WebHelper {

    public partial class Charts {

      private const string canvasIDprefix = "canvas-chart-";

      private static string GetChartHtmlAndScript(int pixelHeight, ChartJSJson chartJSJson, string extraClasses = null) {

        string canvasID = $"{canvasIDprefix}{GetNextRequestSequentialNumber()}"; // Unique ID to reference this canvas element in the javascript.

        return
          $@"<div style=""height: {pixelHeight}px"" class=""pos-relative {extraClasses.HTMLEncode()}""><canvas id=""{canvasID}""></canvas></div>"
          + GetJQueryScriptBlock($@"
              var ctx = document.getElementById(""{canvasID}"").getContext(""2d"");
              var myChart = new Chart(ctx, {JsonConvert.SerializeObject(chartJSJson, Formatting.Indented)} );
            ");
      }

      public static string GetStackedBarChart(int pixelHeight, List<StackLegendItem> stackLegend, List<StackedBar> bars, string extraClasses = null) {

        if (stackLegend == null || bars == null) return string.Empty;

        // Note in ChartJS each "dataset" is actually a stack, with the dataSet's data[] array being the value for each bar. Strange but true.

        var dataSets = new List<ChartJSJson.Data.dataset>();

        foreach (StackLegendItem legendItem in stackLegend) {

          var barDataForDataset = new List<decimal?>(Enumerable.Repeat((decimal?)null, bars.Count)); // Each dataset (stack item) contains a list of values for each bar.

          // Go through bars in index order. If bar contains a value for this "stackId", assign it to the same barData index value.
          for (int iBar = 0; iBar < bars.Count; iBar++) {
            var bar = bars[iBar];
            var barStackItem = bar.StackItems.Find(si => si.StackItemId == legendItem.StackItemId);
            if (barStackItem != null) barDataForDataset[iBar] = barStackItem.Value;
          }

          dataSets.Add(new ChartJSJson.Data.dataset(legendItem.Label, legendItem.CSSColor, barDataForDataset));
        }

        var chartJSData = new ChartJSJson.Data(bars.Select(b => b.Label).ToList(), dataSets);

        var chartJSJson = new ChartJSJson(ChartJSJson.ChartType.StackedBar, chartJSData);

        return GetChartHtmlAndScript(pixelHeight, chartJSJson, extraClasses);
      }

      public class StackLegendItem {
        public long StackItemId { get; private set; }
        public string Label { get; private set; }
        public string CSSColor { get; private set; }
        public StackLegendItem(long stackItemId, string label, string cssColor) {
          if (cssColor == null) cssColor = "#000";
          if (!cssColor.StartsWith("#")) cssColor = "#" + cssColor;
          StackItemId = stackItemId;
          Label = label;
          CSSColor = cssColor;
        }
      }

      public class StackedBar {
        internal long BarId { get; private set; }
        internal string Label { get; private set; }
        internal List<StackItem> StackItems { get; private set; }
        public StackedBar(long barId, string barLabel, List<StackItem> barStackItems) {
          BarId = barId;
          Label = barLabel;
          StackItems = barStackItems;
        }
        public class StackItem {
          public long StackItemId { get; private set; }
          public decimal? Value { get; private set; }
          public StackItem(long stackItemId, decimal? value) {
            StackItemId = stackItemId;
            Value = value;
          }
        }
      }

      private class ChartJSJson {

        public enum ChartType { Bar, StackedBar, Line }
        public string type { get; private set; }
        public Options options { get; private set; }
        public Data data { get; private set; }

        public ChartJSJson(ChartType chartType, Data chartJSData) {
          this.type = chartType == ChartType.Line ? "line" : "bar";
          this.options = new Options(chartType);
          this.data = chartJSData;
        }

        public class Options {

          public bool responsive { get; private set; } = true;
          public bool maintainAspectRatio { get; private set; } = false;
          public Layout layout { get; private set; } = new Layout();
          public Legend legend { get; private set; } = new Legend();
          public Scales scales { get; private set; }

          public Options(ChartType chartType) {
            scales = new Scales(chartType);
          }

          public class Layout {
            public Padding padding { get; private set; } = new Padding();
            public class Padding {
              public int left { get; private set; } = -8;
            }
          }

          public class Legend {
            public string position { get; private set; } = "left";
            public bool reverse { get; private set; } = true;
            public Labels labels { get; private set; } = new Labels();
            public class Labels {
              public int boxWidth { get; private set; } = 15;
              public int fontSize { get; private set; } = 14;
              public string fontColor { get; private set; } = "#333";
              public int padding { get; private set; } = 10;
              public bool usePointStyle { get; private set; } = false;
            }
          }

          public class Scales {
            public List<Axes> xAxes { get; private set; }
            public List<Axes> yAxes { get; private set; }
            public Scales(ChartType chartType) {
              xAxes = new List<Axes>() { new Axes(chartType) };
              yAxes = new List<Axes>() { new Axes(chartType) };
            }
            public class Axes {
              public bool stacked { get; private set; }
              public Axes(ChartType chartType) {
                stacked = chartType == ChartType.StackedBar;
              }
            }

          }
        }

        public class Data {
          public Data(List<string> labels, List<dataset> datasets) {
            this.labels = labels;
            this.datasets = datasets;
          }
          public List<string> labels { get; private set; }
          public List<dataset> datasets { get; private set; }
          public class dataset {
            public dataset(string label, string cssColor, List<decimal?> data) {
              this.label = label;
              this.data = data;
              // Colours intentionally all the same.
              if (cssColor == null) cssColor = "#000";
              if (!cssColor.StartsWith("#")) cssColor = "#" + cssColor;
              this.borderColor = cssColor;
              this.hoverBorderColor = cssColor;
              this.backgroundColor = cssColor;
              this.hoverBackgroundColor = cssColor;
              this.pointBorderColor = cssColor;
              this.pointHoverBorderColor = cssColor;
              this.pointBackgroundColor = cssColor;
              this.pointHoverBackgroundColor = cssColor;
            }
            public List<decimal?> data { get; private set; }
            public string label;
            public string borderColor;
            public string hoverBorderColor;
            public string backgroundColor;
            public string hoverBackgroundColor;
            public string pointBorderColor;
            public string pointBackgroundColor;
            public string pointHoverBorderColor;
            public string pointHoverBackgroundColor;
            public int pointRadius = 4;
            public int pointBorderWidth = 0;
            public decimal lineTension = 0.1M;
            public bool fill = false;
            public bool spanGaps = true;
          }
        }
      }
    }

  }
}
