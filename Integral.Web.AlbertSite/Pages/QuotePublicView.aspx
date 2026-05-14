<%@ Page Language="C#" AutoEventWireup="true" CodeFile="QuotePublicView.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.QuotePublicView" MasterPageFile="~/MasterPages/Public.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <style>
    /* Override (proposal tab has no padding) */
    .tab-content {
      padding: 0px
    }
    .tabMargin{
      margin-top: 20px
    }
  </style>

  <div class="container quoteView">

    <div class="heading flex-wrap-sm">
      <div class="logo"><img src="<%= PathHelper.Images.TenantOrgLogo(QuoteInfo, true) %>" /></div>
      <div class="title">
        <h2><%= QuoteInfo.QuoteTitle.HTMLEncode() %></h2>
      </div>
    </div>

    <% if (!QuoteInfo.IsAccepted) { %>

      <div class="signform">

        <div class="main-tabs-outer">
          <ul class="nav nav-tabs nav-tabs-underlined" id="">
            <% if (QuoteHasOverview) { %>
            <li role="presentation" data-tabname="<%= TabName.Overview %>">
              <a class="nav-link" id="tab-<%= TabName.Overview %>" data-toggle="tab" href="#panel-<%= TabName.Overview %>" role="tab" aria-controls="panel-<%= TabName.Overview %>" aria-selected="true">Overview</a>
            </li>
          <% } %>
          <% if (ShowProposalTab) { %>
            <li role="presentation" data-tabname="<%= TabName.Proposal %>">
              <a class="nav-link" id="tab-<%= TabName.Proposal %>" data-toggle="tab" href="#panel-<%= TabName.Proposal %>" role="tab" aria-controls="panel-<%= TabName.Proposal %>" aria-selected="true">Proposal</a>
            </li>
          <% } %>
          <% if (QuoteHasTeam) { %>
          <li role="presentation" data-tabname="<%= TabName.Team %>">
            <a class="nav-link" id="tab-<%= TabName.Team %>" data-toggle="tab" href="#panel-<%= TabName.Team %>" role="tab" aria-controls="panel-<%= TabName.Team %>">Team</a>
          </li>
          <% } %>
          <li role="presentation" data-tabname="<%= TabName.Costing %>">
            <a class="nav-link" id="tab-<%= TabName.Costing %>" data-toggle="tab" href="#panel-<%= TabName.Costing %>" role="tab" aria-controls="panel-<%= TabName.Costing %>">Costing</a>
          </li>
          <% if (CanViewSigningTab) { %>
            <li role="presentation" data-tabname="<%= TabName.Accept %>">
              <a class="nav-link" id="tab-<%= TabName.Accept %>" data-toggle="tab" href="#panel-<%= TabName.Accept %>" role="tab" aria-controls="panel-<%= TabName.Accept %>">Accept</a>
            </li>
          <% } %>
          <li role="presentation" class="tab-pdflink dropdown " title = "Download PDF">

            <a href="#" class="dropdown-toggle pdflink" data-toggle="dropdown" tabindex="-1">
              <%= WebHelper.Icon.PDF_outline.ToString() %>
            </a>
            <div class="dropdown-menu dropdown-menu-right shadow animated--grow-in" aria-labelledby="userDropdown">
              <a class="dropdown-item" href="<%= PathHelper.Pages.QuotePublicPDF(QuoteInfo.PublicGuid) %>" target="_blank" >Download Quote PDF</a>

              <% if (!QuoteInfo.QwilrPDFUrl.IsNullOrEmptyOrWhitespace()) { %>
                <div class="dropdown-divider"></div>
                <a class="dropdown-item" href="<%= QuoteInfo.QwilrPDFUrl %>" target="_blank" >Download Proposal PDF</a>
              <% } %>
            </div>

          </li>
          </ul>
        </div>

        <form id="signform" class="form-horizontal" method="post" action="<%= PathHelper.Pages.QuotePublicView(QuoteInfo.PublicGuid) %>" onsubmit="return false">
          <div class="tab-content">
            <% if (QuoteHasOverview) { %>
              <div class="tab-pane tab-quote tab-<%= TabName.Overview %> fade in tabMargin" id="panel-<%= TabName.Overview %>" role="tabpanel" aria-labelledby="tab-<%= TabName.Overview %>">
              </div>
            <% } %>
            <% if (ShowProposalTab) { %>
              <div class="tab-pane tab-quote tab-<%= TabName.Proposal %> fade in" id="panel-<%= TabName.Proposal %>" role="tabpanel" aria-labelledby="tab-<%= TabName.Proposal %>"></div>
            <% } %>
            <% if (QuoteHasTeam) { %>
              <div class="tab-pane tab-quote tab-<%= TabName.Team %> fade in tabMargin" id="panel-<%= TabName.Team %>" role="tabpanel" aria-labelledby="tab-<%= TabName.Team %>"></div>
            <% } %>
            <div class="tab-pane tab-quote tab-<%= TabName.Costing %> fade in tabMargin" id="panel-<%= TabName.Costing %>" role="tabpanel" aria-labelledby="tab-<%= TabName.Costing %>"></div>
            <div class="tab-pane tab-quote tab-<%= TabName.Accept %> fade in tabMargin" id="panel-<%= TabName.Accept %>" role="tabpanel" aria-labelledby="tab-<%= TabName.Accept %>"></div>
          </div>
        </form>

        <% if (QuoteHasOverview) { %>
          <div class="" data-appendTo="panel-<%= TabName.Overview %>">
            <div class="quote-overview-box">
              <%= QuoteInfo.CoverLetterHtml %>
            </div>
          </div>
        <% } %>

        <% if (QuoteHasUrlId) { %>
          <div class=" iframe-parent" data-appendTo="panel-<%= TabName.Proposal %>">
            <iframe src="<%= SalesContentUrl.HTMLEncode() %>" class="proposal-frame"></iframe>
          </div>
        <% } %>

        <% if (QuoteHasWebPageUrl) { %>
          <div class=" iframe-parent" data-appendTo="panel-<%= TabName.Proposal %>">
            <iframe src="<%= QuoteInfo.QuoteSalesContentWebPageUrl.HTMLEncode() %>" class="proposal-frame"></iframe>
          </div>
        <% } %>

        <% if (QuoteHasQwilrEmbedded) { %>
          <div class=" iframe-parent" data-appendTo="panel-<%= TabName.Proposal %>">
            <iframe src="<%= QuoteInfo.QwilrUrl %>" class="proposal-frame"></iframe>
          </div>
        <% } %>

        <% if (QuoteHasPDF) { %>
          <div class="iframe-parent" data-appendTo="panel-<%= TabName.Proposal %>">
            <iframe
                src="<%= PathHelper.UrlPath.JS %>/pdfjs/web/viewer.html?file=<%= PathHelper.PDF.QuoteSalesPDFUrl(QuoteInfo)%>#page=1&zoom=75%"
              class="proposal-frame quote-pdf"
              title="Proposal PDF"></iframe>
              <!-- Fallback -->
                <a href="<%= PathHelper.PDF.QuoteSalesPDFUrl(QuoteInfo) %>">Download Quote PDF Here</a>
          </div>
        <% } %>

        <% if (QuoteHasTeam) { %>
          <div class="mb20" data-appendTo="panel-<%= TabName.Team %>">
            <h3 class="mt0">Your Project Team</h3>
            <p class="mb20">Your project team includes a Program Lead Consultant, a Program Coodinator and the program delivery team.</p>
            <div class="team-list">
              <%= GetTeamHtml() %>
            </div>
            <div class="mt30 mb20"><button type="button" class="btn btn-primary btn-next">Costing &gt;</button></div>
          </div>
        <% } %>

        <div class="" data-appendTo="panel-<%= TabName.Costing %>">

          <div class="table-responsive">
            <table class="table w100pc">
              <thead>
                <tr>
                  <th class="w125 hidden-xs hidden-sm">Type</th>
                  <th class="w50 pl0 pr0">&nbsp;</th>
                  <th>Description</th>
                  <th class="w125 pl0 pr0 align-center">Qty</th>
                  <th class="w100 align-right pl0 pr0">$ / Unit</th>
                  <th class="w125 align-right">Total Price</th>
                </tr>
              </thead>
              <tbody>
                <% foreach (var quoteItem in QuoteInfo.QuoteItems) { %>
                  <tr class="item-row" data-item="<%= quoteItem.QuoteItemId %>">
                    <td class="item-td-cat pr0 hidden-xs hidden-sm"><%= quoteItem.CategoryName %></td>
                    <td class="item-td-opt pl0 pr0"><%= GetOptional(quoteItem) %></td>
                    <td class="item-td-des">
                      <div class="item-des-body">
                        <div class="visible-xs visible-sm"><div class="cat-title"><%= quoteItem.CategoryName %></div></div>
                        <%= quoteItem.ItemDescription %>
                      </div>
                    </td>
                    <td class="item-td-qty align-center pl0 pr0"><%= quoteItem.Quantity.ToString("0.##", "") %></td>
                    <td class="item-td-amt align-right pl0 pr0"><%= quoteItem.UnitPrice.ToString("C", "") %></td>
                    <td class="item-td-tot align-right item-linetotal" data-amount="<%= quoteItem.UnitPrice == null || quoteItem.Quantity == null ? "" : (quoteItem.UnitPrice.GetValueOrDefault(0) * quoteItem.Quantity.GetValueOrDefault(0)).ToString() %>"><%= quoteItem.UnitPrice == null || quoteItem.Quantity == null ? "" : (quoteItem.UnitPrice.GetValueOrDefault(0) * quoteItem.Quantity.GetValueOrDefault(0)).ToString("C") %></td>
                  </tr>
                <% } %>
              </tbody>
              <tfoot>
                <tr>
                  <td colspan="6" class="pl0 pr0">
                    <table class="w100pc">
                      <tr>
                        <td class="pl20">
                          <p class="mt10 mb5"><b>Client Approval</b></p>
                          <p>Please click below to indicate your acceptance of the quote.</p>
                          <button type="button" class="btn btn-primary btn-next mt15 mb0">Accept</button>
                        </td>
                        <td align="right" class="pt10">
                          <table class="table noborder" width="100%">
                            <tr>
                              <td class="nowrap total-title pt0 pr0">Sub-Total</td>
                              <td class="nowrap w125 total-amount pt0 total-amount-no-gst"></td>
                            </tr>
                            <tr>
                              <td class="nowrap total-title pt0 pr0">GST</td>
                              <td class="nowrap total-amount pt0 total-amount-gst"></td>
                            </tr>
                            <tr>
                              <td class="nowrap total-title pt0 pr0">Total</td>
                              <td class="nowrap total-amount pt0 total-amount-inc-gst"></td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        <div class="" data-appendTo="panel-<%= TabName.Accept %>">
          <div class="">
              <h3 class="mb10">Client Contact</h3>
              <div class="row-info">
                <div class="input-text-dual">
                  <input type="text" readonly autocomplete="off" class="form-control" name="ClientFirstName" value="<%= ClientFirstName %>" placeholder="Client First Name" />
                  <input type="text" readonly autocomplete="off" class="form-control" name="ClientLastName" value="<%= ClientLastName %>" placeholder="Client Last Name" />
                </div>
              </div>
              <div class="row-info">
                  <input readonly autocomplete="off" type="text" class="form-control" name="ClientEmail" value="<%= ClientEmailAddress %>" placeholder="Client Email" />
              </div>
              <h3 class="mb10 mt15">
                Accounts Payable
                <span class="accountsPayableInfo"><ion-icon name="information-circle-outline"></ion-icon></span>
              </h3>
              <div class="row-info">
                <div class="input-text-dual">
                  <input type="text" autocomplete="off" class="form-control" name="AccFirstName" value="" placeholder="Accounts Payable First Name" />
                  <input type="text" autocomplete="off" class="form-control" name="AccLastName" value="" placeholder="Accounts Payable Last Name" />
                </div>
              </div>
              <div class="row-info">
                  <input autocomplete="off" type="text" class="form-control" name="AccEmail" value="" placeholder="Accounts Payable Email" />
              </div>

            <div class="total-amount mt20">
              Total Amount: <span class="total-amount-inc-gst amount"></span> <small><%= QuoteGSTApplicable ? "inc" : "ex" %> GST</small>
            </div>
            <%= WebHelper.GetAbleTermsAndConditionsCheckBoxHtml("agree") %>
            <button type="button" id="btnSign" class="btn btn-primary">Sign Proposal</button>
          </div>
        </div>

      </div>

    <% } %>

    <div class="thankyou displaynone">
      <center>
        <h3>Proposal Accepted, Thank you!</h3>
        <p>This proposal was accepted on <%= WebHelper.DisplayDate(QuoteInfo.ClientAcceptedUtc.UtcToTZOrNull(), WebHelper.DisplayDate(DateTime.UtcNow.UtcToTZ())) %>.</p>
        <%= GetClientLinkToAbleHtml() %>
      </center>
    </div>

  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var $signForm = $("#signform");
      var $btnSign = $("#btnSign");
      var heading = $(".heading");
      var tabsOuter = $(".main-tabs-outer");
      var menuList = $("ul.nav-tabs");

      $(document).ready(function () {

        ShowThankYou(<%= QuoteInfo.IsAccepted.ToJSTrueFalse() %>);

        new jBox('Tooltip', {
          attach: '.accountsPayableInfo', position: { y: 'top', x: 'right' },
          title: 'Account Payable Info', content: 'The person who administers this invoice.'
        });

        $(".item-optional").change(function (ev) {
          var $chk = $(ev.target);
          var checked = $chk.is(":checked");
          var $row = $chk.closest("tr");
          var itemid = $row.data("item");
          if (!checked) {
            $row.addClass("unchecked");
          } else {
            $row.removeClass("unchecked");
          }
          RecalcTotal();
        });

        RecalcTotal();

        $(".btn-next").click(NextButtonClicked);
        $("#btnSign").click(SignClicked);

        $(".nav-tabs > li:first-child a").trigger("click");

        if ($(".quoteView .proposal-frame").length == 1) {
          $(window).on("resize", ResizeIFrame);
          ResizeIFrame();
        }
      });

      function ResizeIFrame() {
        setTimeout(function () {
          var iframeTop = $(".quoteView .proposal-frame").position().top;
          var winHeight = $(window).height();
          $(".quoteView .proposal-frame").height(winHeight - iframeTop);
        }, 200);
      }

      function RecalcTotal() {

        var totalWithoutGST = 0;
        var gstApplicable = <%= QuoteGSTApplicable.ToJSTrueFalse() %>;
        var $rows = $(".item-row");

        for(var i = 0; i < $rows.length; i++) {
          var $row = $rows.eq(i);
          var $chk = $row.find(".item-optional");
          if ($chk.length == 0 || $chk.is(":checked")) {
            var $lineTotal = $row.find(".item-linetotal");
            if ($lineTotal.length == 1) {
              var amountStr = $lineTotal.data("amount");
              if (isNumber(amountStr)) {
                var lineTotal = parseFloat(amountStr) || 0;
                totalWithoutGST += lineTotal;
              }
            }
          }
        }

        $(".total-amount-no-gst").text(CurrencyFormatter.format(totalWithoutGST));
        $(".total-amount-gst").text(!gstApplicable ? "n/a" : CurrencyFormatter.format(totalWithoutGST / 10));
        $(".total-amount-inc-gst").text(CurrencyFormatter.format(!gstApplicable ? totalWithoutGST : (totalWithoutGST * 1.1)));
      }

      function NextButtonClicked(ev) {
        $btn = $(ev.target);
        $pane = $btn.closest(".tab-pane").next();
        if ($pane.length != 1) return;
        var tabName = $pane.prop("id").replace("panel-", "");
        window.scrollTo(0, 0);
        setTimeout(function () { $("#tab-" + tabName).trigger("click"); }, 200);
      }

      function SignClicked(ev) {

        AjaxSubmit({
          form: $signForm,
          onSuccess: function (jqXHR, data) {
            ShowThankYou(true);
          }
        });
      }

      function ShowThankYou(isAccepted) {

        if (!isAccepted) return;

        $(".signform").hide();
        $(".thankyou").show();
      }

    })(jQuery);
  </script>

</asp:Content>

