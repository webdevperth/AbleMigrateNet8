<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ChartAlbert360ScoreOverTime.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.ChartAlbert360ScoreOverTime" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<div class="ctlCA360SoT">

  <div class="row">
    <div class="col-xs-12 col-md-4"><!-- box scores -->

      <div class="boxBorder boxLeft">

        <div class="boxTitle"><h4>Your <%= reportResults.SurveyInfo.IsRatersOnly ? "Pulse" : "360" %> Score</h4></div>

        <div class="boxScores boxBgTop container-fluid">
          <div class="row">
            <div class="scoreAcross scoreLeft col-xs-6">
              <div class="score"><%= GetScoreFormatted(raterScore, true) %><span class="sub"> / 100</span></div>
              <div class="barOuter"><div class="bar"></div></div>
              <div class="subtitle">Rater</div>
            </div>
            <div class="scoreAcross scoreRight col-xs-6">
              <div class="score"><%= GetScoreFormatted(raterBenchMark, true) %><span class="sub"> / 100</span></div>
              <div class="barOuter"><div class="bar"><span>..........</span></div></div>
              <div class="subtitle">Rater <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %><div class="benchTypeName">(<%= benchTypeName %>)</div></div>
            </div>
          </div>
        </div>

        <% if (!reportResults.SurveyInfo.IsRatersOnly) { %>
          <div class="boxScores boxBgBottom container-fluid">
            <div class="row">
              <div class="scoreAcross scoreLeft col-xs-6">
                <div class="score"><%= GetScoreFormatted(selfScore, true) %><span class="sub"> / 100</span></div>
                <div class="barOuter"><div class="bar"></div></div>
                <div class="subtitle">Self</div>
              </div>
              <div class="scoreAcross scoreRight col-xs-6">
                <div class="score"><%= GetScoreFormatted(selfBenchMark, true) %><span class="sub"> / 100</span></div>
                <div class="barOuter"><div class="bar"><span>..........</span></div></div>
                <div class="subtitle">Self <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %><div class="benchTypeName">(<%= benchTypeName %>)</div></div>
              </div>
            </div>
          </div>
        <% } %>

      </div>
    </div>
    <div class="col-xs-12 col-md-8"><!-- chart -->

      <div class="boxBorder boxRight">

        <div class="boxTitle"><h4><%= reportResults.SurveyInfo.IsRatersOnly ? "Pulse" : "360" %> Scores Over Time</h4></div>

        <div class="canvas-container"><canvas id="ctlCA360SoT_canvas" height="190" width="700" style="height:190px; max-height:190px"></canvas></div>

      </div>
    </div>
  </div><%-- row --%>

</div><%-- ctlCA360SoT --%>


<script type="text/javascript">

  (function ($) {

    var isRatersOnly = <%= reportResults.SurveyInfo.IsRatersOnly ? "true" : "false" %>;

    $(document).ready(function() {

      var canvas = $("#ctlCA360SoT_canvas");
      canvas.width(canvas.parent().width());
      DrawChart(canvas);

    });

    function DrawChart(canvas) {

      var ctx = canvas[0].getContext("2d");

      var chartData = {
        labels: <% =this.xAxis %>,
        datasets: [
          <% if (!reportResults.SurveyInfo.IsRatersOnly) { %>
          {
            label: 'Self',
            data: <% =this.selfScoreDS %>,
            backgroundColor: [ 'rgba(0, 0, 0, 0)' ],
            borderColor: ['#44B189'],
            borderWidth: 2
          },{
            label: 'Self <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %>',
            data: <% =this.selfBenchMarkDS %>,
            borderDash: [3,3],
            backgroundColor: [ 'rgba(0, 0, 0, 0)' ],
            borderColor: ['#44B189'],
            borderWidth: 1
          },
          <% } %>
          {
            label: 'Rater',
            data: <% =this.raterScoreDS %>,
            backgroundColor: [ 'rgba(0, 0, 0, 0)' ],
            borderColor: ['#6C66DA'],
            borderWidth: 2
          },{
            label: 'Rater <%= OrgReports.Benchmark_Display_Text.UCaseFirstChar() %>',
            data: <% =this.raterBenchMarkDS %>,
            borderDash: [3,3],
            backgroundColor: [ 'rgba(0, 0, 0, 0)' ],
            borderColor: ['#6C66DA'],
            borderWidth: 1
          }
        ]
      };

      var chartOptions = {
        responsive: true,
        legend: { display: false }, // remove legend
        elements: {
          line: { tension: 0 } // remove curves
        },
        scales: {
          xAxes: [{ gridLines: { color: "rgba(0, 0, 0, 0.1)" } }],
          yAxes: [{ gridLines: { color: "rgba(0, 0, 0, 0)" }, ticks: { beginAtZero: true, min: 0, max: 100, precision: 0, stepSize: 50 } }]
        }
      };

      var myChart = new Chart(ctx, {
        type: 'line',
        data: chartData,
        options: chartOptions
      }); // new Chart()

    } // DrawChart()


  })(jQuery);


</script>
