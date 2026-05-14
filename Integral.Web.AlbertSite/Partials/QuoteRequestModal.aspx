<%@ Page Language="C#" AutoEventWireup="true" CodeFile="QuoteRequestModal.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.QuoteRequestModal" %>

<%@ Import Namespace="Integral.Web" %>

  <div class="w100p" id="requestContainer">

    <%= GeneralInfoHtml %>

    <%= WebHelper.GetTextAreaVertical("Your request specification:", true, FormFields.RequestText, 12, "", "reqtext") %>

    <button type="button" class="btn btn-primary float-right" id="btnSend">Send</button>

  </div><!-- /.content -->


  <script type="text/javascript">
    (function ($) {

      var btnSend;

      $(document).ready(function () {

        $("div.reqtext").removeClass("w100p");
        $("div.reqtext p").removeClass("ml5");
        $("div.reqtext textarea").addClass("resize-none");
        $("label.control-label").removeClass("col-md-2");
        $("label.control-label").addClass("col-md-3");
        $(".display-only > div").addClass("type-quotereq-title");

        var requestContainer = $('#requestContainer');
        btnSend = $("#btnSend");

        <% if (!GeneralInfoHtml.IsNullOrEmpty()) { %>
          requestContainer.height(290);
        <% } else { %>
          requestContainer.height(210);
        <% } %>

        btnSend.click(SendQuoteRequest);

      }); // ready.

      function SendQuoteRequest() {

        var requestText = $("textarea[name='RequestText']").val();

        AjaxSubmit({
          action: "<%= AjaxAction.SendQuoteRequest %>",
          data: {
            "<%= FormFields.RequestText %>": requestText
          },
          url: "<%= PathHelper.Partials.QuoteRequestModal((ProjectInfo != null ? ProjectInfo.JobNumber : "")) %>",
          onSuccess: function (jqXHR, data) {
            window.Intercom('trackEvent', 'quote_updated', {
              job_number: <%= ProjectInfo.JobNumber %>
            });
          },
          onFail: function (jqXHR, data) { },
          onError: function (jqXHR, textStatus, errorThrown) { },
          onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }


    })(jQuery);
  </script>
