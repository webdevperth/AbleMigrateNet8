<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="SurveyViewer_Comments.aspx.cs"
    Inherits="Integral.Web.PortalSite.Page_Partials.SurveyViewer_Comments" %>

<%@ Import Namespace="Integral.Web" %>

<%-- Note requires reports.css --%>

<% Action<int, string> CategoryStart = delegate(int questionNumber, string questionText) { %>
  <div class="boxBorder">
    <div class="boxTitle"><h4><%= questionText.HTMLEncode() %></h4></div>
<% }; %>

<% Action CategoryEnd = delegate { %>
  </div>
<% }; %>

<% Action<QuestionInfo> QuestionDetail = delegate(QuestionInfo questionInfo) { %>
  <div class="question row <%= questionInfo.IsSelf ? "qnText-self" : "qnText-rater" %>">
    <div class="qnText col-md-12">
      <%= questionInfo.ResponseText.HTMLEncode().Replace("\n", "<br>") %>
    </div>
  </div>
<% }; %>

<% Action<string> QuestionHeading = delegate(string text) { %>
  <div class="question row qnHeading">
    <div class="qnText col-md-12">
      <%= text %>
    </div>
  </div>
<% }; %>

<div class="RptPub360Cmnt">

  <% ShowQuestions(CategoryStart, CategoryEnd, QuestionHeading, QuestionDetail); %>

</div>


<script type="text/javascript">

  (function ($) {

  })(jQuery);

</script>
