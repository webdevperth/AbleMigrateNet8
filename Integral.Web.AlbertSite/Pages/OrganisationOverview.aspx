<%@ Page Language="C#" AutoEventWireup="true" CodeFile="OrganisationOverview.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.OrganisationOverview" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <div class="table-title">Metrics</div>
  <div class="flex flex-wrap gap20 mb25">
    <%= WebHelper.GetPeopleMetrics(CompanyInfo) %>
    <%= WebHelper.GetCompanyLeadBox(CompanyInfo) %>
  </div>

  <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
    Url = PathHelper.Partials.ActivityChartForCompany(CompanyInfo.CompanyId, PathHelper.ActivityChartUnits.Minutes),
    InitialWidth = "100%",
    InitialHeight = "320px",
    DeferInitialLoad = false,
    InitialStyle = WebHelper.PartialLoaderStyle.Blank,
    LoaderStyle = WebHelper.PartialLoaderStyle.Chart
  }) %>

  <div class="table-title">Monthly Progress</div>
  <div class="pos-relative h250 mb25"><canvas id="chart-monthly-progress"></canvas></div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script>

    (function ($) {

      $(document).ready(function () {

        CreateMonthlyProgressChart();

        function CreateMonthlyProgressChart() {

          var ctx = document.getElementById("chart-monthly-progress").getContext("2d");
          var dataJson = <%= GetMonthlyProgressChartJson() %>;

          var myChart = new Chart(ctx, {
            type: 'line',
            options: {
              responsive: true,
              maintainAspectRatio: false,
              layout: {
                padding: {
                  left: -8
                }
              },
              legend: {
                position: 'left',
                reverse: true,
                labels: {
                  boxWidth: 15,
                  boxHeight: 13,
                  fontSize: 14,
                  fontColor: '#333',
                  padding: 10,
                  usePointStyle: false
                }
              },
              scales: {
                yAxes: [{
                  ticks: {
                    beginAtZero: false,
                    max: 10 // Max score
                  }
                }]
              }
            },
            data: dataJson
          });
        }

      });

    })(jQuery);
  </script>

</asp:Content>
