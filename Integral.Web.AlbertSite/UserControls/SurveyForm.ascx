<%@ Control Language="C#" AutoEventWireup="true"
    CodeFile="SurveyForm.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.SurveyForm" %>

<%@ Import Namespace="Integral.Web" %>

<link rel="stylesheet" type="text/css" href="<%= PathHelper.UrlPath.CSS %>survey-viewer-common.css" />
<link rel="stylesheet" href="<%= PathHelper.UrlPath.CSS %>survey.css" />

<%
  if (!IsSurveyLinkValid) {
    ShowSurveyNotFound();
  } else if (IsSurveyClosed) {
    ShowSurveyClosed();
  } else {
    ShowSurveyContent();
  }
%>

<% void ShowSurveyContent() { %>

  <div class="survey_page stcode_<%= SurveyInfo.ReportType.StCode.ToLower() %> page_<%= CurrentPage %>">

    <% if (SurveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) { %>

      <style>
        .devplan-title-header { padding: 20px 15px; border-bottom: 1px solid #ddd; }
        .slideout-body:has(.devplan-title-header) { padding-top: 0; }
        .slideout-body .devplan-title-header { position: sticky; top: 0; z-index: 99; background: #fff; padding: 15px 25px;
                                               box-shadow: 0 5px 10px #eee; margin: 0 calc(0px - var(--slideout-body-padding-side)); }
      </style>

      <div class="flex flex-fill flex-align-center devplan-title-header">

        <div><h3 class="text-muted"><%= GetPlanDateDisplay() %></h3></div>

        <% if (!IsViewingSharedSurvey) { %>
          <div class="align-right">
            <% if (CanShareSurvey) { %>
              <%= WebHelper.GetShareSurveyButtonHtml(SurveyInfo, WebHelper.ShareSurveyButtonTypeEnum.RegularButton, "mr5") %>
            <% } %>
            <% if (CanShowDevPlanReportSlideout) { %>
              <button type="button" id="surveyForm-btn-slideout" class="btn btn-primary btn-sparkles mr5"></button>
            <% } %>
            <button type="button" class="btn btn-primary surveyForm-btn-update">
              <span class="visible-xs"><%= WebHelper.GetIconHtml(WebHelper.ActionButtonTypeEnum.save) %></span>
              <span class="hidden-xs"><%= SubmitButtonText %></span>
            </button>
          </div>
        <% } %>
      </div>

    <% } %>

    <% if (ShowSurveyTitle) { %>
      <h3 class="surveyTitle"><%= SurveyInfo == null ? "" : SurveyInfo.SurveyName %></h3>
    <% } %>

    <% if (ShowParticipantInfo) { %>
      <% if (PartInfo != null) { %>
          <h4 class="surveyFor">Survey for: <span class="partName"><%= PartInfo == null ? "" : PartInfo.FullName %></span></h4>
        <% if (PartInfo.IsSelf) { %>
        <% } else { %>
          <h4 class="surveyFor">Person seeking feedback: <span class="partName"><%= PartInfo == null ? "" : PartInfo.SelfName %></span></h4>
        <% } %>
      <% } %>
    <% } %>

    <div class="survey-instructions">

      <% if (!SurveyInstructionsHtml.IsNullOrEmpty()) { %>

        <%= SurveyInstructionsHtml %>

      <% } else if (!SurveyInfo.ReportType.IsIOS && SurveyInfo.SurveyType != DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) { %>

        <h4>Welcome, <%= PartInfo.FullName %></h4>
        <% if (PartInfo.IsSelf) { %>
          <p>Please rate your effectiveness on the following scale.</p>
          <p>If you do not perform any of these activities due to your role complete that specific question as "NA"</p>
        <% } else { %>
          <p>Please rate <b><%= PartInfo.SelfName %></b> on their effectiveness using the following scale.</p>
          <p>Please select "NA" for any questions you do not feel you can accurately provide feedback on.</p>
        <% } %>

      <% } %>

    </div>

    <% if (QuestionsForPage?.TotalPages > 1) { %>
      <div class="navTop">
        <table cellspacing="0" cellpadding="0" border="0" width="">
          <tr>
            <td class="pr10">Page:</td>
            <td><ul class="pagination pagination-sm no-margin"><%= WebHelper.Surveys.GetPagination(QuestionsForPage, PartInfo) %></ul></td>
          </tr>
        </table>
      </div>
    <% } %>

    <% if (!HasVisibleQuestions) { %>
      <div class="MsgNoQuestions">
        <div class="survey-no-questions-icon"><img src="<%= PathHelper.UrlPath.Images %>survey-no-questions-icon.png" width="80" alt="No Questions" /></div>
        <h4>No questions apply on this page.<br />Please continue on to the next page.</h4>
      </div>
    <% } %>

    <form action="#" method="post" id="formSurvey">

      <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.Update %>" />
      <input type="hidden" name="<%= FormFields.NextPageNumber %>" value="<%= CurrentPage + 1 %>" />

      <div class="QnList">
        <% if (HasVisibleQuestions) { %>
          <% foreach (var qn in QuestionsForPage.Questions) { %>
            <%= WebHelper.Surveys.GetSurveyQuestionHtml(qn, PartInfo, cachedOptionHtml) %>
          <% } %>
        <% } %>
      </div>
    </form>

    <% if (SurveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) { %>

      <hr/>

      <% if (!IsViewingSharedSurvey) { %>
        <div class="flex flex-fill">
          <div>
            <% if (CanDeleteDevPlan) { %>
              <button type="button" id="btnDeleteDevPlan" class="btn btn-warning ml10" title="Delete Development Plan"><%= WebHelper.Icon.Delete %></button>
            <% } %>
          </div>
          <div><button type="button" class="btn btn-primary surveyForm-btn-update float-right"><%= SubmitButtonText %></button></div>
        </div>
      <% } %>

      <div id="surveyForm-slideout-content" class="slideout-content hidden pl30 pr30 pt20 pb20">

        <% if (!LatestSurveyAISummary.IsNullOrEmpty()) { %>
          <div class="boxBorder ai-summary-box">
            <div class="boxTitle"><h5>Capability Summary</h5></div>
            <div><%= WebHelper.MarkdownToHtml(LatestSurveyAISummary) %></div>
          </div>
        <% } %>

        <% if (HasLatest360) { %>

          <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
            ID = "partial_Focus",
            Url = PathHelper.Partials.SurveyViewer_Focus(null, null),
            InitialWidth = "100%",
            InitialHeight = "400px",
            DeferInitialLoad = true,
            InitialStyle = WebHelper.PartialLoaderStyle.Blank,
            LoaderStyle = WebHelper.PartialLoaderStyle.Chart
          }) %>

        <% } %>

      </div>

    <% } else { %>

      <div class="survey-form-footer">
        <% if (CanViewPrevButton) { %>
          <button type="button" class="btn btn-secondary surveyForm-btn-update" data-nextpage="<%= CurrentPage - 1 %>">
            <span class="visible-xs"><%= WebHelper.GetIconHtml(WebHelper.ActionButtonTypeEnum.back) %></span>
            <span class="hidden-xs">Previous Page</span>
          </button>
        <% } %>
        <% if (!SubmitButtonText.IsNullOrEmpty()) { %>
          <button type="button" class="btn btn-primary surveyForm-btn-update" data-nextpage="<%= CurrentPage + 1 %>">
            <span class="visible-xs"><%= WebHelper.GetIconHtml(WebHelper.ActionButtonTypeEnum.save) %></span>
            <span class="hidden-xs"><%= SubmitButtonText %></span>
          </button>
        <% } %>
        <% if (ShowSurveyRandomAnswersButton) { %>
          <button type="button" class="btn-random btn btn-warning btn-large">Random [dev]</button>
        <% } %>
        <% if (ShowSurveyDataEntryButton) { %>
          <a href="<%= PathHelper.JarvisPages.DataEntryUrl(PartInfo.SurveyId, PartInfo.PartId) %>" class="btn btn-secondary btn-large" target="_blank">Data Entry in Jarvis [dev]</a>
        <% } %>
      </div>

    <% } %>

  </div>

<% } %>

<% void ShowSurveyNotFound() { %>
  <div class="container-fluid">
    <div class="row">
      <div class="error-template">
        <h3>Sorry, we can't find that survey.</h3>
        <br/>
        <% if (SessionHelper.IsUserLoggedIn) { %>
          Find the survey you're looking for in your survey list:<br />
          <br />
          <a href="<%= PathHelper.Pages.ParticipantSurveys() %>" class="btn btn-primary btn-sm">Your Survey List</a>
        <% } else { %>
          <p>Please check the link that led you here is correct.</p>
          <p>If you think this may be an error, please <a target="_blank" href="<%= ConfigHelper.HelpUrls.ContactUs %>">let us know</a>.</p>
        <% } %>
        <br/><br/>
      </div>
    </div>
  </div>
<% } %>

<% void ShowSurveyClosed() { %>
  <div class="container-fluid">
    <div class="row">
      <div class="error-template">
        <h1>Hm, looks like that survey is closed.</h1>
        <br/>
        <p>If you think this may be an error, or need more time to respond, please let us know!</p>
        <br/>
        <p>Carl-Johan Malmsten</p>
        <p>Coordinator, Able Digital Coaching</p>
        <p><a href="mailto:cj@integral.global?subject=Survey closed.">cj@integral.global</a></p>
        <br/><br/>
      </div>
    </div>
  </div>
<% } %>

<script>
  (function ($) {

    var $surveyForm;

    $(document).ready(function () {

      $surveyForm = $(".survey_page #formSurvey");

      $(".survey_page .QnList").click(function (ev) {
        QuestionListClicked($(ev.target)); // Clicked in the question list.
      });

      $(".survey_page .surveyForm-btn-update").click(UpdateButtonClicked);

      $(".survey_page .btn-random").click(RandomButtonClicked);

      SelectExistingResponses();
    });

    function SelectExistingResponses() {

      $(".survey_page .QnList").children(".QnRow[data-qnid]").each(function (i, e) {

        var $qnRow = $(e);
        var qnId = parseInt($qnRow.data("qnid"));
        var $formField = $qnRow.find('input:hidden[name="ans_' + qnId + '"]');

        if ($formField.length == 1) {

          var inputType = $qnRow.data("inptype");
          var answerValue = $formField.val();
          var $optButton = $qnRow.find('.QnOptionBtn[data-value="' + answerValue + '"]'); // for scales
          var $extraTextControl = $qnRow.find(".qn_ExtraText input:text");

          $extraTextControl.prop("disabled", true);

          if ($optButton.length == 1) {
            QuestionListClicked($optButton, true);
          } else {
            $optButton = $qnRow.find('input:radio[value="' + answerValue + '"]'); // for option lists (radios)
            if ($optButton.length == 1) {
              $optButton.prop("checked", true);
              $optButton.trigger("change");
              QuestionListClicked($optButton, true);
            }
          }
        }
      });

      UpdatePage();
    }

    function QuestionListClicked($target, noFocusChange) {

      // Show question "answered" tick when clicking on an option.
      if ($target.is(".QnOptionBtn")) {

        var $optRow = $target.closest(".QnOptions");
        var $qnRow = $target.closest(".QnRow");
        var $qnBody = $target.closest(".QnBody");
        var $doneIcon = $optRow.find(".doneIcon");
        var $extraText = $qnBody.children(".qn_ExtraText");
        var $extraTextControl = $extraText.find(".form-control");
        var inputType = $qnRow.data("inptype");
        var qnId = $qnRow.data("qnid");
        var $formField = $qnRow.find("input:hidden[name='ans_" + qnId + "']");
        var optionValue = $target.data("value");

        if (inputType == "o" || inputType == "scale" || inputType == "ranked") {
          var makeSelected = !$target.hasClass("selected");
          // Clear row.
          $optRow.removeClass("selected");
          $doneIcon.hide();
          $optRow.find(".QnOptionBtn").removeClass("selected");
          $extraText.removeClass("required");
          $extraTextControl.removeAttr("placeholder");
          if (makeSelected) {
            $optRow.addClass("selected");
            $doneIcon.show();
            $target.addClass("selected");
            $formField.val(optionValue);
            $extraText.addClass("required");
            $extraTextControl.prop("disabled", false);
            if (!noFocusChange) $extraTextControl.focus();
            $extraTextControl.attr("placeholder", "Type your priority here.");
            if ($extraTextControl.data("old-value")) $extraTextControl.val($extraTextControl.data("old-value"));
          } else {
            $target.blur();
            $formField.val("");
            $extraTextControl.data("old-value", $extraTextControl.val());
            $extraTextControl.val("").prop("disabled", true).change();
          }
        }
      }
      UpdatePage();
    }

    function UpdatePage() {

      // Disable / enable unused ranked buttons on the page.
      var $rankedQns = $(".survey_page .QnRow.inptype_ranked");
      if ($rankedQns.length > 0) {
        var values = [];
        $rankedQns.eq(0).find("button[data-value]").map(function () { values.push($(this).data("value")); });
        for (var i = 1; i < 6; i++) {
          if ($(".survey_page .QnOptions.qntype_ranked button.selected[data-value='" + i + "']").length > 0) {
            $(".survey_page .QnOptions.qntype_ranked button[data-value='" + i + "']:not(.selected)").prop("disabled", true);
          } else {
            $(".survey_page .QnOptions.qntype_ranked button[data-value='" + i + "']").prop("disabled", false);
          }
        }
      }
    }

    function UpdateButtonClicked(evt) {

      $(".survey_page .QnList").children(".QnRow[data-qnid]").each(function (i, e) {
        var $qnRow = $(e);
        var qnId = parseInt($qnRow.data("qnid"));
        var $optButton = $qnRow.find(".QnOptionBtn.selected");
        if ($optButton.length == 1) {
          var answerValue = $optButton.data("value");
          var $formField = $qnRow.find('input:hidden[name="ans_' + qnId + '"]');
          if ($formField.length == 1) $formField.val(answerValue);
        }
      });

      var $pageButton = $(evt.target).closest(".surveyForm-btn-update");
      var nextPageNumber = $pageButton.data("nextpage");
      $surveyForm.find('input:hidden[name="NextPageNumber"]').val(nextPageNumber);

      AjaxSubmit({
        url: "<%= Request.RawUrl %>",
        form: $surveyForm
      });
    };

    function RandomButtonClicked() {

      $(".survey_page .QnOptions.qntype_scale").each(function (i, e) {
        var $opts = $(e).find(".QnOptionBtn[data-value!='na']");
        if ($opts.length > 0) {
          // For scales, select only among the top half of the options.
          var halfOpts = Math.floor($opts.length / 2);
          var randomOpt = Math.floor(Math.random() * halfOpts);
          var $optButton = $opts.eq($opts.length - randomOpt - 1);
          if (!$optButton.hasClass("selected")) $optButton.trigger("click"); // Only if not already selected.
        }
      });

      $(".survey_page .QnOptions.qntype_o").each(function (i, e) {
        var opts = $(e).find("input:radio[value!='na']");
        if (opts.length > 0) {
          opts.removeAttr("checked").trigger("change");
          var opt = opts.eq(Math.floor(Math.random() * opts.length));
          opt.attr("checked", true).trigger("change");
        }
      });

      // Distribute values among ranked questions, no repeats.
      var $rankedQns = $(".survey_page .QnRow.inptype_ranked");
      if ($rankedQns.length > 0) {
        // Clear all options.
        $rankedQns.find(".QnOptionBtn[data-value].selected").each(function (i, e) { QuestionListClicked($(e)); }); // Deselect all selected.
        $rankedQns.find(".qn_ExtraText input:text").val("").change();
        $rankedQns.data("random-assigned", false);
        // Get list of valid options.
        $validOptions = $rankedQns.first().find('.QnOptionBtn[data-value!=""]');
        // Loop thru valid options and assign each to a random unassigned question.
        for (var iSelectOpt = 0; iSelectOpt < $validOptions.length; iSelectOpt++) {
          var selectOptionValue = $validOptions.eq(iSelectOpt).attr("data-value");
          var $unassignedQns = $rankedQns.filter(function () { // Get remaining unassigned qns.
            return $(this).data("random-assigned") === false;
          });
          if ($unassignedQns.length > 0) {
            var randomQnIndex = Math.floor(Math.random() * $unassignedQns.length); // Pick random unassigned qn.
            $qnRow = $unassignedQns.eq(randomQnIndex);
            if ($qnRow.length == 1) {
              var $optButton = $qnRow.find('.QnOptionBtn[data-value="' + selectOptionValue + '"]');
              if ($optButton.length == 1) {
                QuestionListClicked($optButton);
                $qnRow.data("random-assigned", true);
                var $extraTextControl = $qnRow.find(".qn_ExtraText input:text");
                if ($extraTextControl.length == 1) {
                  var qnNumber = $qnRow.children(".QnNum").text();
                  $extraTextControl.val("random text Q" + qnNumber);
                }
              } else {
                console.log("Err: Failed to find option value \"" + selectOptionValue + "\"");
              }
            } else {
              console.log("err: Failed to find random qn index " + randomQnIndex + " out of " + $unassignedQns.length + " unassigned qns.");
            }
          } else {
            console.log("err: No unassigned qns left.");
          }
        }
      }

      UpdatePage();
    }

  })(jQuery);
</script>

<% if (IsDevelopmentPlan) { %>

  <script type="text/javascript">
    (function($) {

      $(document).ready(function () {

        <% if (CanDeleteDevPlan) { %>
          $("#btnDeleteDevPlan").click(function () {
            common_ConfirmDialog("Confirm", "Are you sure you want to delete this Development Plan, this action cannot be undone.", function (confirmed) {
              if (confirmed) {
                AjaxSubmit({
                  form: $("#formSurvey"),
                  action: "<%= AjaxAction.Delete %>"
                });
              }
            });
          });
        <% } %>

        <% if (CanShowDevPlanReportSlideout) { %>

          $("#<%= WebHelper.ElementID.SlideoutPanelTitle %>").html('<h4 class="sparkles">Capability Insights</h4>');
          $("#<%= WebHelper.ElementID.SlideoutPanelBody %>").empty().append($("#surveyForm-slideout-content"));

          $("#surveyForm-btn-slideout").click(function (ev) {
            ev.preventDefault();
            $("#surveyForm-slideout-content").show();
            $("body").removeClass("slideout-show");
            window.setTimeout(function () {
              $("body").addClass("slideout-show");
            }, 100);
          });

          $('.btn-sparkles').jBox('Tooltip', {
            position: { y: 'top', x: 'right' },
            title: 'Helpful hints',
            content: 'Checkout some examples to fill your development plan.'
          });

          var intakeId = <%= ProfileInfo.UserActivity?.Latest360IntakeCodeId.ToStringOrDefaultIfNull("null") %>;
          if (intakeId != null) {
            setTimeout(function () {
              LoadPartReport(intakeId);
            }, 500);
          }

          function LoadPartReport(intakeId) {

            var delay = 300;

            $.EachPartial(function ($partial, partialInfo) {
              if (partialInfo == null) return;
              partialInfo.Clear();
              if (isNumber(intakeId)) {
                setTimeout(function (thisPartialInfo) {
                  var extraValues = {
                    "<%= PathHelper.AbleUrlKeys.SurveyIntakeCodeId %>": intakeId,
                    "<%= PathHelper.AbleUrlKeys.CoacheeGuid %>": "<%= ProfileInfo.UserActivity?.Latest360CoacheeGuid.ToStringNoBracesOrNull() %>",
                    "<%= PathHelper.AbleUrlKeys.SurveyViewerBenchmark %>": "<%= PathHelper.SurveyViewerBenchmarkEnum.Global %>"
                  };
                  thisPartialInfo.LoadUrl(thisPartialInfo.initialUrl, extraValues);
                }, delay, partialInfo);
                delay += 300;
              }
            });
          }
        <% } %>

      }); // ready.

    })(jQuery);
  </script>

<% } %>
