<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProjectComponents.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProjectComponents"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (Quotes.IsNullOrEmpty()) { %>

    <%= WebHelper.GetEmptyStatePageHtml("Components", "No components yet.") %>

  <% } else { %>

  <% Action ShowQuoteList = delegate { %>
    <div class="proj-components">
      <div class="components-contour contour-full">

        <% foreach (var quote in Quotes) { %>

          <% if (!quote.IsAccepted) continue; %>

          <% decimal quoteTotal = quote.IsAccepted ? quote.ClientAcceptedAmt.GetValueOrDefault(0) : quote.QuoteItemTotal; %>
          <% var quoteItems = DbHelper.AbleQuotes.GetQuoteItems(ProjectInfo.JobNumber, quote.QuoteId, quote.IsAccepted); %>

          <div class="title title-main">Quote: <a href="<%= IsReadOnly ? "" : PathHelper.Pages.QuoteDetails(quote.QuoteGuid) %>"><%= quote.QuoteName.HTMLEncode() %></a></div>

          <div class="quoteInfo">

            <table class="tblQuoteInfo">
              <tr>
                <td class="infoLeftCol">
                  <p><%= quote.IsAccepted ? "Accepted" : "Quote" %> Amount</p>
                  <p class="amt quoteTotalAmt"><%= quoteTotal.ToString("C") %></p>
                  <% if (quote.IsAccepted) { %>
                    <p class="quoteAccepted"><%= WebHelper.DisplayDate(quote.ClientAcceptedUtc.Value.UtcToTZ(null)) %></p>
                  <% } else { %>
                    <p class="quoteAccepted">Not Yet Accepted</p>
                  <% } %>
                </td>
                <td class="">
                  <p>Total Components</p>
                  <p class="amt"><%= quote.ComponentsAmt.ToString("C") %></p>
                  <div class="balance-bar" data-total="<%= quoteTotal %>" data-amount="<%= quote.ComponentsAmt %>"></div>
                </td>
                <td class="">
                  <p>Total Delivered</p>
                  <p class="amt"><%= quote.DeliveredAmt.ToString("C") %></p>
                  <div class="balance-bar" data-total="<%= quoteTotal %>" data-amount="<%= quote.DeliveredAmt %>"></div>
                </td>
                <td class="">
                  <p>Allocated to Invoice</p>
                  <p class="amt"><%= quote.AssignedToInvoiceItem.ToString("C") %></p>
                  <div class="balance-bar" data-total="<%= quoteTotal %>" data-amount="<%= quote.AssignedToInvoiceItem %>"></div>
                </td>
                <td class="">
                  <p>Invoiced Amount</p>
                  <p class="amt"><%= quote.InvoiceItemsAmt.ToString("C") %></p>
                  <div class="balance-bar" data-total="<%= quoteTotal %>" data-amount="<%= quote.InvoiceItemsAmt %>"></div>
                </td>
                <td class="">
                  <p>Invoiced to Xero</p>
                  <p class="amt"><%= quote.InvoicedAmt.ToString("C") %></p>
                  <div class="balance-bar" data-total="<%= quoteTotal %>" data-amount="<%= quote.InvoicedAmt %>"></div>
                </td>
                <td class="">
                  <p>Paid Invoices</p>
                  <p class="amt"><%= quote.PaidAmountInvoices.ToString("C") %></p>
                  <div class="balance-bar" data-total="<%= quote.InvoiceItemsAmt %>" data-amount="<%= quote.PaidAmountInvoices %>"></div>
                </td>
                <td class="">
                  <% if (CanBulkAddInvoiceComponents && quoteItems != null && quoteItems.Count > 0 && quoteItems.Exists(x => x.HasUnallocatedComponentsToInvoice)) { %>
                    <button class="btn btn-sm btn-primary floatright mt20 btnBulkAddInvoice" data-qguid="<%= quote.QuoteGuid %>">Bulk Add Invoice</button>
                  <% } %>
                </td>
              </tr>
            </table>

            <div class="components-contour contour-full">

              <div class="title title-inside">
                <h4>Quote Items</h4>
                <div class="quoteItemInfo">
                  <table class="tblQuoteItem table compressed mb0">
                    <thead>
                      <tr>
                        <th class="qiDescr">Quote Item Description</th>
                        <th class="type-components-money">Total Amount</th>
                        <th class="type-components-chart"></th>
                      </tr>
                    </thead>
                  </table>
                </div>
              </div>

              <% if (!quoteItems.Exists(i => !i.IsNote)) { %>
                <div class="quoteItemInfo">
                  <p class="noItems">None</p>
                </div>
              <% } else { %>

                <% foreach (var quoteItem in quoteItems) { %>

                  <% if (quoteItem.IsNote) continue; %>

                  <% var components = DbHelper.ProgramComponents.GetForQuoteItem(quoteItem.QuoteItemId); %>

                  <div class="quoteItemInfo" data-id="<%= quoteItem.QuoteItemId %>">
                    <div class="quoteNotes">
                      <table class="tblQuoteItem table compressed">
                        <tr>
                          <td class="qiDescr"><%= quoteItem.ItemDescription %></td>
                          <td class="type-components-money"><%= quoteItem.PriceXQty(null).ToString("C", "-") %></td>
                          <td class="type-components-chart"><div class="components-chart" data-percent="<%= GetQuoteItemPercentage(components, quoteItem) %>"><canvas></canvas></div></td>
                        </tr>
                      </table>
                    </div>

                    <div class="componentsList table-responsive">
                      <div class="title">
                        <h4 class="mb10">Components</h4>
                          <table class="tblComponents table compressed">
                            <thead>
                              <tr>
                                <th class="type-description">Type</th>
                                <th class="type-component-money">Price</th>
                                <th class="type-component-date">Completed</th>
                                <th class="type-component-date">P&L</th>
                                <th class="type-component-date">Payrun</th>
                                <th class="type-component-date">Invoice</th>
                                <% if (!IsReadOnly) { %>
                                  <th class="type-actionbutton"></th>
                                  <th class="type-actionbutton"></th>
                                <% } %>
                              </tr>
                            </thead>
                          </table>
                      </div>
                      <table class="tblComponents table compressed">
                        <tbody>
                          <% if (components == null || components.Count == 0) { %>
                            <tr>
                              <td colspan="6">None</td>
                            </tr>
                          <% } else { %>
                            <% foreach (var cmp in components) { %>
                              <tr>
                                <td class="type-description"><%= GetComponentName(cmp) %></td>
                                <td class="type-component-money"><%= cmp.ComponentPrice.ToString("C", "-") %></td>
                                <td class="type-component-date"><%= WebHelper.DisplayDate(cmp.CompletedDateUtc.UtcToTZOrNull(null), "-") %></td>
                                <td class="type-component-date"><%= WebHelper.DisplayDate(cmp.PLPeriodDate.UtcToTZOrNull(null), "-") %></td>
                                <td class="type-component-date"><%= GetPayRunLinkHtml(cmp.PayrunDate, cmp.PayRunId, cmp.PartnerUserId) %></td>
                                <td class="type-component-date"><%= !cmp.InvoiceNumber.IsNullOrEmpty()
                                                                    ? cmp.InvoiceNumber.HTMLEncode()
                                                                    : (cmp.InvoiceItemId != null ? "Yes" : "No") %></td>
                                <% if (!IsReadOnly) { %>
                                  <td class="type-actionbutton"><%= WebHelper.GetActionButton(WebHelper.ActionButtonTypeEnum.edit, "btnEditCmp", "Edit component",
                                    new WebHelper.DataAttributes(("cmp-id", cmp.ComponentId.ToString()) )) %></td>
                                  <td class="type-actionbutton"><%= cmp.InvoiceItemId != null ? "" :
                                                                    !CanAddInvoiceItem ? "" :
                                                                    WebHelper.GetActionButton(
                                                                      WebHelper.ActionButtonTypeEnum.invoiceItem, "btnInvoiceItem", "Create Invoice Item",
                                                                      new WebHelper.DataAttributes(
                                                                        ("cmp-id", cmp.ComponentId.ToString()),
                                                                        ("cmp-description", GetComponentDescription(cmp)),
                                                                        ("cmp-price", cmp.ComponentPrice.ToString()))) %></td>
                                <% } %>
                              </tr>
                            <% } %>
                          <% } %>
                        </tbody>
                      </table>
                    </div>
                  </div>
                <% } // foreach %>
              <% } // quoteItems exist %>
            </div>
          </div>
        <% } // quotes %>
      </div>
    </div>

    <script>
      +function ($) {
        $(document).ready(function () {

          const originalDoughnutDraw = Chart.controllers.doughnut.prototype.draw;
          Chart.helpers.extend(Chart.controllers.doughnut.prototype, {
            draw: function() {
              const chart = this.chart;
              const { width, height, ctx, config } = chart.chart;
              const { datasets } = config.data;
              const dataset = datasets[0];
              const datasetData = dataset.data;
              const completed = datasetData[0];
              const text = `${completed}%`;
              let x, y, mid;
              originalDoughnutDraw.apply(this, arguments);
              const fontSize = (height / 70).toFixed(2);
              ctx.font = fontSize + "em Lato, sans-serif";
              ctx.textBaseline = "top";
              x = Math.round((width - ctx.measureText(text).width) / 2);
              y = (height / 2.3) - fontSize;
              ctx.fillStyle = "#000000"
              ctx.fillText(text, x, y);
              mid = x + ctx.measureText(text).width / 2;
            }
          });

          $(".balance-bar").each(function (i, e) {
            var $bar = $(e);
            var totalAmount = $bar.data("total");
            var thisAmount = $bar.data("amount");
            var percent = 0;
            if (totalAmount > 0) percent = Math.round(thisAmount / totalAmount * 100);
            if (percent == 0 && thisAmount > 0) percent = 1;
            else if (percent == 100 && thisAmount < totalAmount) percent = 99;
            var $inner = $("<div><span>" + percent  + "%</span></div>").addClass("inner").width(percent + "%");
            if (percent == 100) $inner.addClass("full");
            else if (percent > 100) $inner.addClass("full");
            $inner.appendTo($bar);
          });

          $(".components-chart").each(function (i, e) {
            var $e = $(e);
            var percent = $e.data("percent");
            var ctx = $e.find("canvas"); //.getContext('2d');
            var color = percent == 100 ? "#7bf427" : "#77CCFF";
            var chart = new Chart(ctx, {
              type: 'doughnut',
              data: { datasets: [{ data: [percent, 100 - percent], backgroundColor: [color, "#eee"],  }] },
              options: {
                aspectRatio: 1,
                layout: { padding: { left: 0, right: 0, top: 0, bottom: 0, } },
                responsive: true,
                cutoutPercentage: 55,
                legend: { display: false, },
                title: { display: false, },
                tooltips: { enabled: false },
                hover: {mode: null},
              }
            });
          });

        });
      }(jQuery);
    </script>

    <script>
      // Closure for Component Modal
      (function ($) {

        $(document).ready(function () {

          <% if (!IsReadOnly) { %>
            // On Components table edit button clicked
            $(document).on("click", ".btnEditCmp", function (evt) {
              // Get componentId from row button clicked
              var componentId = $(this).data('cmp-id');
              if (!isNumber(componentId)) return;
              var path = '<%= PathHelper.Partials.ComponentModal(ProjectInfo.JobNumber, null) %>' + componentId;
              ShowInvoicingModal(path, "Edit Component");
            });

            // On Components table create invoice button clicked
            $(".btnInvoiceItem").click(InvoiceItemConfirmationModal);
            $(".btnBulkAddInvoice").click(function (evt) {
              var quoteGuid = $(this).data('qguid');
              if (!isGuid(quoteGuid)) return;
              var path = '<%= PathHelper.Partials.Components_BulkAddInvoiceModal(ProjectInfo.JobNumber, null) %>' + quoteGuid;
              ShowInvoicingModal(path, "Bulk Add Invoice");
            });
          <% } %>
        });

        function ShowInvoicingModal(path, title) {

          BootstrapDialog.show({
            title: title,
            onshow: function (dialogRef) {
              var modalDialog = dialogRef.getModalDialog();
              modalDialog.css("width", "780px");
              modalDialog.data("<%= WebHelper.DataAttrName.DialogRef %>", dialogRef);
              var modalBody = dialogRef.getModalBody();
              modalBody.busyLoad("show");
              modalBody.load(path,
                function (data) {
                  modalBody.html(data);
                  modalBody.busyLoad("hide");
                  common_UpdateUI(modalBody);
                }
              );
            },
            onhide: function (dialogRef) {
              var modalDialog = dialogRef.getModalDialog();
              modalDialog.find("textarea.tinymce").each(function (i, e) {
                var mce = $(e).data("editor");
                if (mce != null) mce.remove();
              });
            }
          });
        }

        function InvoiceItemConfirmationModal(evt) {
          var thisBtn = $(evt.target);
          var cmpId = thisBtn.data('cmp-id');
          if (!isNumber(cmpId)) return;
          var cmpDescription = thisBtn.data('cmp-description');
          var cmpPrice = thisBtn.data('cmp-price');

          BootstrapDialog.show({
            type: BootstrapDialog.TYPE_WARNING,
            title: 'Confirmation - Create Invoice Item',
            message: "Create invoice item for component &ldquo;" + cmpDescription + "&rdquo; with total $" + cmpPrice +"?",
            buttons: [
              {
                label: 'No', cssClass: 'btn-secondary',
                action: function (dialog) { dialog.close(); }
              },
              {
                label: 'Yes', cssClass: 'btn-primary',
                action: function (dialog) { dialog.close(); CreateInvoiceItem(cmpId); }
              }
            ]
          });
        }

        function CreateInvoiceItem(cmpId) {
          AjaxSubmit({
            action: "<%= AjaxAction.AddInvoiceItem %>",
            data: {
              "<%= FormFields.ComponentId %>": cmpId
            },
            onFail: function (jqXHR, data) { },
            onError: function (jqXHR, textStatus, errorThrown) { },
            onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
          });
        }
      })(jQuery);


    </script>
  <% }; %>

  <div class="mb30">

    <% ShowQuoteList(); %>

  </div>

   <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

</asp:Content>
