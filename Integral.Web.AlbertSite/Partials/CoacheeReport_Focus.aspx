<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="CoacheeReport_Focus.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.CoacheeReport_Focus" %>

<%@ Import Namespace="Integral.Web" %>

<div class="flex flex-align-center mb10">
  <div class="flex1"></div>
  <div class="flex flex-align-center gap10">
    <div>Sort By: </div>
    <div id="SortSelect" class="btn-group btn-group-toggle" data-toggle="buttons">
      <%= GetSortButtons() %>
    </div>
  </div>
</div>

<script type="text/javascript">
  (function ($) {

    var partialInfo, SortSelect;

    $(document).ready(function () {
      SortSelect = $("#SortSelect");
      partialInfo = common_GetPartialInfo(SortSelect);
      SortSelect.on("change", "input", SortSelectChange);
    });

    function SortSelectChange(evt) {
      partialInfo.LoadUrl(null, { "<%= PathHelper.AbleUrlKeys.SortBy %>": SortSelect.find(".active input").val() });
    }

  })(jQuery);
</script>

<% Action<string, string> CategoryStart = delegate(string categoryName, string className) { %>
  <div class="boxBorder <%= className %>">
    <div class="boxTitle"><div class="catCircle"></div><h4><%= categoryName.HTMLEncode() %></h4></div>
<% }; %>

<% Action CategoryEnd = delegate { %>
  </div>
<% }; %>

<% Action<QuestionInfo> QuestionDetail = delegate(QuestionInfo qnItem) { %>
  <div class="question <%= GetBenchComparisonRowClass(qnItem) %>">
    <div class="qnText">
      <div class="sectionTitle"><%= qnItem.CategoryHeading.HTMLEncode() %></div>
      <div><%= qnItem.GblQuestionText.HTMLEncode() %></div>
    </div>
    <div class="qnBars">
      <div class="scoreBars" data-tooltip="<b>Rater Result</b><br>Compared to Self: <%= qnItem.SurveyRaterScore - qnItem.SurveySelfScore %>">

        <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Self, ScoreMinValue, ScoreMaxValue,
                                              "Self", qnItem.SurveySelfScore, NormDisplayName, Hide360ReportNorms ? null : qnItem.NormSelfScore) %>

        <% if (RaterCount > 0) { %>
          <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Rater, ScoreMinValue, ScoreMaxValue,
                                                "Rater", qnItem.SurveyRaterScore, NormDisplayName, Hide360ReportNorms ? null : qnItem.NormRaterScore) %>
        <% } %>
      </div>
    </div>
  </div>
<% }; %>

<div class="RptPub360Focus">

  <% ShowQuestions(CategoryStart, CategoryEnd, QuestionDetail); %>

</div>


<script type="text/javascript">

  (function ($) {

    $(document).ready(function () {
      new jBox('Tooltip', {
        attach: '.RptPub360Focus .barDot[title]', position: { y: 'top' }
      });
    });


  })(jQuery);

</script>
