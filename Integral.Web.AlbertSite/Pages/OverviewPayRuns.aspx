<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="OverviewPayRuns.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.OverviewPayRuns"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <div class="mb30 payrunsContainer">
    <select id="selPayRuns" class="w275">
      <%= WebHelper.Payruns.GetPayRunSelectOptions(OverviewCoachInfo, UrlPayRunId) %>
    </select>
  </div>

  <div id="divPayRunInfo"></div>

  <div id="emptyStateHtml" class="displaynone">
    <%= WebHelper.GetEmptyStatePageHtml("Payruns", "There's no payruns yet.") %>
  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      var selPayRuns, divPayrunResults;

      $(document).ready(function () {

        selPayRuns = $("#selPayRuns");
        divPayRunInfo = $("#divPayRunInfo");

        selPayRuns.change(PayRunChanged);
        PayRunChanged(true);

      }); // ready.

      function PayRunChanged(initialState) {

        var payRunId = toInt(selPayRuns.val(), null);

        if (payRunId == null) {
          $('.payrunsContainer').hide();
          $('#divPayRunInfo').hide();
          $('#emptyStateHtml').removeClass('displaynone');
        }

        if (initialState !== true) UpdateUrlAddress(payRunId);

        divPayRunInfo.empty();

        AjaxSubmit({
          action: "<%= AjaxAction.GetPayRun %>",
          data: {
            "<%= FormFields.PayRunId %>": payRunId
          },
          onSuccess: function (jqXHR, data) {
            LoadContent(data, initialState, payRunId)
          }
        });
      }

      function LoadContent(data, initialState, payRunId) {

        divPayRunInfo.html(data["<%= AjaxReturnData.PayRunInfoHtml %>"]);

        var hasItems = data["<%= AjaxReturnData.HasItems %>"] === true; // False means content is either blank or just a message.

        // If initial state is on upcoming page but no results, change to "select pay run".
        if (initialState === true && payRunId === 0 && hasItems === false) {
          selPayRuns.val("");
          selPayRuns.change();
        }
      }

      function UpdateUrlAddress(payRunId) {
        if (payRunId > 0) {
          HistoryReplaceUrlParams({ "<%= PathHelper.AbleUrlKeys.PayRunId %>": payRunId });
        } else {
          HistoryReplaceUrlParams({}, true);
        }
      }

    })(jQuery);
  </script>

</asp:Content>
