<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="SurveyViewer_Focus.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.SurveyViewer_Focus" %>

<%@ Import Namespace="Integral.Web" %>

<% if (ShowScores) { %>
  <div class="flex flex-align-center mb10">
    <div class="flex1"></div>
    <div class="flex flex-align-center gap10">
      <div>Sort By: </div>
      <select class="SurveyViewer_Focus_SortBy w125 noselect2">
        <% foreach (var option in RowSortOptions) { %>
          <option value="<%= option.Key.HTMLEncode() %>" <%= (QueryRowSortBy == option.Value.OptionEnum).ToValue("selected") %>><%= option.Value.DisplayText.HTMLEncode() %></option>
        <% } %>
      </select>
    </div>
  </div>
<% } %>

<script type="text/javascript">
  (function ($) {

    var selSortBy, partialInfo;

    $(document).ready(function () {
      selSortBy = $(".SurveyViewer_Focus_SortBy");
      selSortBy.change(ChangeSortBy);
      partialInfo = common_GetPartialInfo(selSortBy);
    });

    function ChangeSortBy() {
      partialInfo.LoadUrl(null, { "<%= QueryKeys.RowSortBy %>": selSortBy.val() });
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
    <% if (ShowScores) { %>
      <div class="qnBars">
        <div class="scoreBars">
          <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Self, ScoreMinValue, ScoreMaxValue, "Self", qnItem.SurveySelfScore, NormDisplayName, qnItem.NormSelfScore) %>
          <% if (RaterCount > 0) { %>
            <%= WebHelper.GetSurveyViewerScoreBar(WebHelper.SurveyViewerScoreBarType.Rater, ScoreMinValue, ScoreMaxValue, "Rater", qnItem.SurveyRaterScore, NormDisplayName, qnItem.NormRaterScore) %>
          <% } %>
        </div>
      </div>
    <% } %>
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
