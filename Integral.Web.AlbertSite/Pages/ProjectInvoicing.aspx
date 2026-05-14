<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProjectInvoicing.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProjectInvoicing"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% Action<string> ShowQuoteSelect = delegate(string selName) { %>
    <div class="quote-select">
      <select name="<%= selName %>" size="1" class="w100p">
        <option value="">[select quote]</option>
        <% foreach (var quote in InvoiceItemsByQuote.Quotes) { %>
          <option value="<%= quote.QuoteId %>"><%=
            quote.QuoteName.LimitLengthTo(50).HTMLEncode()
            + " (" + (quote.IsAccepted ? quote.ClientAcceptedAmt : quote.InvoiceItemsAmt).ToString("C", "??") + ")"
            %></option>
        <% } %>
      </select>
    </div>
  <% }; %>

  <div class="proj-components invoiceInfo">
    <div class="invoiceGenData">
      <div class="row-info-text">
        <label>Company: </label>
        <div class="textHolder"> <%= ProjectInfo.ClientCompanyName.HTMLEncode() %></div>
      </div>
      <div class="row-info-text">
        <label>Duration: </label>
        <div class="textHolder"> <%= WebHelper.DisplayDate(ProjectInfo.MinProgramStartDateUtc.UtcToTZOrNull(), "-") %> -
        <%= WebHelper.DisplayDate(ProjectInfo.MaxProgramEndDateUtc.UtcToTZOrNull(), "-") %></div>
      </div>
      <div class="row-info-text">
        <label>Inv/Order No: </label>
        <div class="textHolder"><%= ProjectInfo.PurchaseOrderNumber.ValueIfNullOrEmpty("[not set]").HTMLEncode() %></div>
      </div>
    </div>

    <div class="invoiceGenData">
      <div class="row-info-text">
        <label>Xero Contact: </label>
        <div class="textHolder"><%= ProjectInfo.XeroContactName.ValueIfNullOrEmpty("[not set]").HTMLEncode() %></div>
      </div>
      <div class="row-info-text">
        <label>Invoice Type: </label>
        <div class="textHolder"><%= ProjectInfo.InvoiceInstructionTypeName.HTMLEncode() %></div>
      </div>
    </div>

    <% Action ShowQuoteList = delegate { %>
      <div class="components-contour contour-full mb30">

        <% foreach (var quote in InvoiceItemsByQuote.Quotes) { %>

          <div class="title title-main">Quote: <a href="<%= PathHelper.Pages.QuoteDetails(quote.QuoteGuid) %>"><%= quote.QuoteName.HTMLEncode() %></a></div>

          <div class="quoteInfo">

            <table class="tblQuoteInfo">
              <tr>
                <td class="infoLeftCol">
                  <p><%= quote.IsAccepted ? "Accepted" : "Quote" %> Amount</p>
                  <p class="amt quoteAmt"><%= (quote.IsAccepted ? quote.ClientAcceptedAmt : quote.QuoteItemTotal).ToString("C", "--") %></p>
                  <% if (quote.IsAccepted) { %>
                    <p class="quoteAccepted"><%= WebHelper.DisplayDate(quote.ClientAcceptedUtc.Value.UtcToTZ(null), "-") %></p>
                  <% } else { %>
                    <p class="quoteAccepted">Not Yet Accepted</p>
                  <% } %>
                </td>
                <td>
                  <p>Total Components</p>
                  <p class="amt"><%= quote.ComponentsAmt.ToString("C") %></p>
                  <p>Balance</p>
                  <% var balance = quote.ClientAcceptedAmt - quote.ComponentsAmt; %>
                  <p class="amt <%= balance == 0 ? "zeroAmt" : "" %>"><%= balance.ToString("C", "--") %></p>
                </td>
                <td>
                  <p>Total Delivered</p>
                  <p class="amt"><%= quote.DeliveredAmt.ToString("C") %></p>
                  <p>Balance</p>
                  <% balance = quote.ClientAcceptedAmt - quote.DeliveredAmt; %>
                  <p class="amt <%= balance == 0 ? "zeroAmt" : "" %>"><%= balance.ToString("C", "--") %></p>
                </td>
                <td>
                  <p>Invoiced Amount</p>
                  <p class="amt"><%= quote.InvoiceItemsAmt.ToString("C") %></p>
                  <p>Balance</p>
                  <% balance = quote.ClientAcceptedAmt - quote.InvoiceItemsAmt; %>
                  <p class="amt <%= balance == 0 ? "zeroAmt" : "" %>"><%= balance.ToString("C", "--") %></p>
                </td>
                <td>
                  <p>Invoiced to Xero</p>
                  <p class="amt"><%= quote.InvoicedAmt.ToString("C") %></p>
                  <p>Balance</p>
                  <% balance = quote.ClientAcceptedAmt - quote.InvoicedAmt; %>
                  <p class="amt <%= balance == 0 ? "zeroAmt" : "" %>"><%= balance.ToString("C", "--") %></p>
                </td>
              </tr>
            </table>
            <div class="components-contour contour-full">
              <div class="title title-main">Invoices</div>
              <div class="invoiceList">
                <% if (!quote.Invoices.Exists(i => i.InvoiceId != null)) { %>
                  <div class="invoiceInfo">
                    <p class="noInvoices">No Invoices</p>
                  </div>
                <% } else { %>
                  <% foreach (var invoice in quote.Invoices) { %>
                    <% if (invoice.InvoiceId == null) continue; %>
                    <div class="invoiceInfo">

                      <% if (CanDeleteInvoice) { %>
                        <button class="btnDeleteInvoice btn btn-sm btn-secondary floatright"
                          data-<%= FormFields.InvoiceId.ToLower() %>="<%= invoice.InvoiceId %>"
                          data-<%= FormFields.InvoiceOrderNumber.ToLower() %>="<%= invoice.InvoiceNumber.HTMLEncode() %>"><%= WebHelper.Icon.Delete %></button>
                      <% } %>

                      <div class="invHeading">
                        <span class="invNumber"><%= invoice.InvoiceNumber %></span>
                        - <span><%= WebHelper.DisplayDate(invoice.CreatedUtc.UtcToTZ(null), "-") %></span>
                        <button class="btnInvDate btn btn-xsm" title="Change Invoice Date"
                          data-date="<%= TimeHelper.UtcToAppDefaultTimeZone(invoice.CreatedUtc).ToString("dd/MM/yyyy") %>"
                          data-<%= FormFields.InvoiceId.ToLower() %>="<%= invoice.InvoiceId %>"><%= WebHelper.Icon.Edit %></button>
                        <span class="invDescr ml10"><%= invoice.InvoiceDescription.HTMLEncode() %></span>
                        <% if (invoice.PaidUtc != null) { %>
                          <p class="mt5"><b>Paid: <span><%= WebHelper.DisplayDate(invoice.PaidUtc.Value.UtcToTZ(null), "-") %></span></b></p>
                        <% } %>
                      </div>

                      <div class="table-responsive">
                        <table class="tblIvoiceItems table compressed">
                          <thead>
                            <tr>
                              <th class="w400">Item</th>
                              <th class="type-money">Unit Price</th>
                              <th class="type-qty">Qty</th>
                              <th class="type-money">Total</th>
                              <th class="type-money-xlg">Component Total</th>
                              <th class="type-date">Payment Date</th>
                              <th class="type-qty">GST</th>
                              <th class="type-icon"></th>
                            </tr>
                          </thead>
                          <% foreach (var invoiceItem in invoice.InvoiceItems) { %>
                            <tr data-<%= FormFields.RemoveInvItemId.ToLower() %>="<%= invoiceItem.InvoiceItemId %>">
                              <td><%= invoiceItem.Description.HTMLEncode() %></td>
                              <td class="type-money"><%= invoiceItem.UnitPrice.ToString("C") %></td>
                              <td class="type-qty"><%= invoiceItem.Quantity %></td>
                              <td class="type-money"><%= (invoiceItem.UnitPrice * invoiceItem.Quantity).ToString("C") %></td>
                              <td class="type-money-xlg"><%= invoiceItem.InvoiceItemTotalComponentAllocated %></td>
                              <td class="type-date"><%= WebHelper.DisplayDate(invoice.PaidUtc.UtcToTZOrNull(), "-") %></td>
                              <td class="align-center"><%= invoiceItem.GSTApplicable ? "Yes" : "No" %></td>
                              <td class="type-icon">
                                <% if (CanAddDeleteItems) { %>
                                  <button class="btnRemoveFromQuote btn btn-sm" title='Remove from Quote'
                                    data-jid="<%= invoiceItem.ProjectId %>"
                                    data-inviid="<%= invoiceItem.InvoiceItemId %>"><%= WebHelper.Icon.MinusCircle %></button>
                                <% } %>
                              </td>
                            </tr>
                          <% } %>
                          <tfoot>
                            <tr>
                              <td colspan="3" class="align-right">Invoice Total:</td>
                              <td class="type-money"><%= invoice.InvoiceTotal.ToString("C") %></td>
                              <td colspan="2"></td>
                            </tr>
                          </tfoot>
                        </table>
                      </div>
                    </div>
                  <% } // invoices %>
                <% } // invoices exist %>
              </div>
            </div>
          </div>
        <% } // quotes %>
      </div>
    </div>
    <script>
      +function ($) {
        $(document).ready(function () {

          $(".btnInvDate")
            .datepicker({ format: "dd/mm/yyyy", title: "Change Invoice Date", autoclose: true })
            .on("changeDate", ChangeInvDate)
            .click(function () { $(this).datepicker("show"); });

          $(".btnDeleteInvoice").click(DeleteInvoice);
          $(".btnRemoveFromQuote").click(RemoveFromQuote);

        });

        function ChangeInvDate(evt) {
          var $btn = $(this);
          AjaxSubmit({
            action: "<%= AjaxAction.ChangeInvoiceDate %>",
            data: {
              "<%= FormFields.InvoiceId %>": $btn.data("<%= FormFields.InvoiceId.ToLower() %>"),
              "<%= FormFields.InvoiceDate %>": moment(evt.date).format()
            }
          });
        }

        function DeleteInvoice(evt) {

          var $btn = $(this);
          var invoiceId = $btn.data("<%= FormFields.InvoiceId.ToLower() %>");
          var invoiceNumber = $btn.data("<%= FormFields.InvoiceOrderNumber.ToLower() %>");

          common_ConfirmDialog("Delete Invoice", "Confirm deletion of invoice number " + invoiceNumber + "?<br/><br/>"
            + "<small>Note - as a safety measure, this will delete the invoice but not the invoice items. "
            + "The invoice items can be deleted separately afterwards, or assigned to a different invoice.</small>", null, function (confirmed) {
            if (confirmed) {
              AjaxSubmit({
                action: "<%= AjaxAction.DeleteInvoice %>",
                data: {
                  "<%= FormFields.InvoiceId %>": invoiceId
                }
              });
            }
          });
        }

        function RemoveFromQuote(evt) {
          var btn = $(evt.target);
          var tr = btn.closest("tr");
          var invItemId = toDecimalInt(tr.data("<%= FormFields.RemoveInvItemId.ToLower() %>"), 0);
          if (invItemId <= 0) {
            alert("Something went wrong. Please reload page and try again.");
            return;
          }
          AjaxSubmit({
            action: "<%= AjaxAction.RemoveItemFromQuote %>",
            data: {
              "<%= FormFields.RemoveInvItemId %>": invItemId
            }
          });
        }

      }(jQuery);
    </script>
  <% }; %>

  <% Action ShowAssignToQuote = delegate { %>
    <div class="assign-to-quote">
      <h4>Assign Invoice Items to Quotes</h4>
      <div class="table-responsive width-auto">
        <table class="table compressed tblAssignToQuotes">
          <thead>
            <tr>
              <th class="w150">Inv No.</th>
              <th class="type-description">Description</th>
              <th class="type-money">Amount</th>
              <th class="w400">Assign to Quote</th>
              <th class="w100 mr10"></th>
            </tr>
          </thead>
          <tbody>
            <% foreach (var item in ItemsNoInvoiceOrQuote) { %>
              <% if (item.QuoteId != null) continue; %>
              <tr data-<%= FormFields.AssignInvItemId %>="<%= item.InvoiceItemId %>">
                <td class="w150"><%= item.InvoiceNumber.HTMLEncode() %></td>
                <td class="type-description"><%= item.Description.HTMLEncode() %></td>
                <td class="type-money"><%= (item.UnitPrice * item.Quantity).ToString("C") %></td>
                <td class="w400"><% ShowQuoteSelect(FormFields.AssignQuoteId); %></td>
                <td class="w100 mr10"><button class="btn btn-sm btn-primary btnAssignToQuote">Assign</button></td>
              </tr>
            <% } %>
          </tbody>
        </table>
      </div>
    </div>
    <script>
      +function ($) {
        $(document).ready(function () {
          var btnAssignToQuote = $(".btnAssignToQuote");
          btnAssignToQuote.click(function (evt) {
            var btn = $(evt.target);
            var tr = btn.closest("tr");
            var invItemId = toDecimalInt(tr.data("<%= FormFields.AssignInvItemId.ToLower() %>"), 0);
            if (invItemId <= 0) {
              alert("Something went wrong. Please reload page and try again.");
              return;
            }
            var quoteId = toDecimalInt(tr.find("select").val(), 0);
            if (quoteId <= 0) {
              alert("Please select a quote to assign this item.");
              return;
            }
            AjaxSubmit({
              action: "<%= AjaxAction.AssignItemToQuote %>",
              data: {
                "<%= FormFields.AssignInvItemId %>": invItemId,
                "<%= FormFields.AssignQuoteId %>": quoteId
              }
            });
          });
        });
      }(jQuery);
    </script>
  <% }; %>

  <% Action ShowAddInvoiceItem = delegate { %>
    <div class="add-invoice-item">
      <form class="form-horizontal mt30" id="formAddInvoiceItem" action="#" method="post" onsubmit="return false;">
        <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.AddInvoiceItem %>" />
        <h4>New Invoice Item</h4>
        <div class="table-responsive width-auto">
          <table class="table compressed tblNewInvoiceItem">
            <tr>
              <td class="w125 align-right">Description:</td>
              <td><input class="form-control" type="text" name="<%= FormFields.InvItemDescription %>" size="30" value="" /></td>
            </tr><tr>
              <td class="w100 align-right">Unit Price:</td>
              <td>
                <div class="flex flex-align-center">
                  <div class="w150"><input class="form-control inp-currency" data-decimalPlaces="2" type="text" name="<%= FormFields.InvItemUnitPrice %>" /></div>
                  <div class="pl20 pr10">Qty:</div>
                  <div class="w100"><input class="form-control" type="text" name="<%= FormFields.InvItemQuantity %>" size="4" /></div>
                  <div class="pl20 pr10 nowrap">GST Applicable:</div>
                  <div class="w75"><%= WebHelper.CustomCheckBox(FormFields.InvItemGSTApplies, "1", false, null) %></div>
                </div>
              </td>
            </tr><tr>
              <td class="align-right">Quote:</td>
              <td><% ShowQuoteSelect(FormFields.InvItemQuoteId); %></td>
            </tr><tr>
              <td></td>
              <td><button class="btn btn-primary floatright" id="btnAddInvoiceItem">Add Invoice Item</button></td>
            </tr>
          </table>
        </div>
      </form>
    </div>
    <script>
      +function ($) {
        $(document).ready(function () {
          var formAddInvoiceItem = $("#formAddInvoiceItem");
          var btnAddInvoiceItem = $("#btnAddInvoiceItem");
          btnAddInvoiceItem.click(function () {
            AjaxSubmit({
              form: formAddInvoiceItem,
            });
          });
        });
      }(jQuery);
    </script>
  <% }; %>

  <% Action ShowCreateInvoice = delegate { %>
    <div class="create-invoice mb30">
      <form class="form-horizontal" id="formCreateInvoice" action="#" method="post" onsubmit="return false;">
        <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.SubmitInvoice %>" />

        <div class="items-to-invoice mt30">
          <h4>Items Available to Invoice:</h4>
          <div class="table-responsive">
            <table class="table compressed tblItemsToInvoice">
              <thead>
                <tr>
                  <% if (CanSubmitInvoice) { %>
                    <th class="w50 align-center">Select</th>
                  <% } %>
                  <th class="type-date">Date</th>
                  <th>Description</th>
                  <th class="type-money">Unit Price</th>
                  <th class="type-qty">Qty</th>
                  <th class="type-money">Amount</th>
                  <th class="type-money-xlg">Component Total</th>
                  <th class="w75 align-center">GST</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <% if (ItemsToInvoiceCount == 0) { %>
                  <tr><td colspan="7">None.</td></tr>
                <% } else { %>
                  <% InvoiceItemsByQuote.IterateInvoiceItems((quote, invoice, invItem) => { %>
                    <% if (invoice.InvoiceId != null) return; %>
                    <tr data-amount="<%= invItem.UnitPrice.ToString("0") %>" data-qty="<%= invItem.Quantity.ToString("0") %>"
                        data-<%= FormFields.DeleteInvItemId %>="<%= invItem.InvoiceItemId %>">
                      <% if (CanSubmitInvoice) { %>
                        <td class="align-center"><%= WebHelper.CustomCheckBox(GetItemCheckboxName(invItem.InvoiceItemId), invItem.InvoiceItemId.ToString(), false, null) %></td>
                      <% } %>
                      <td class="type-date"><span class="date-value"><%= TimeHelper.UtcToAppDefaultTimeZone(invItem.CreatedUtc).ToString("d MMM yyyy") %></span></td>
                      <td class="">
                        <%= invItem.Description.HTMLEncode() %>
                      </td>
                      <td class="type-money"><%= invItem.UnitPrice.ToString("C") %></td>
                      <td class="type-qty"><%= invItem.Quantity.ToString() %></td>
                      <td class="type-money"><%= (invItem.UnitPrice * invItem.Quantity).ToString("C") %></td>
                      <td class="type-money-xlg"><%= invItem.InvoiceItemTotalComponentAllocated.ToString() %></td>
                      <td class="align-center"><%= invItem.GSTApplicable ? "Yes" : "No" %></td>
                      <td>
                        <% if (CanAddDeleteItems) { %>
                          <button class="btnDeleteItem btn btn-xsm" title='Delete Invoice Item'><%= WebHelper.Icon.Delete %></button>
                        <% } %>
                      </td>
                    </tr>
                  <% }); %>
                <% } %>
              </tbody>
            </table>
          </div>
        </div>

        <% if (ItemsToInvoiceCount > 0 && CanSubmitInvoice) { %>
          <div class="w100p mt30">
            <div class="flex flex-align-center">
              <div class="w200 align-right pr10">Client Inv/Order Number:</div>
              <div class="w500"><input class="form-control w100p" type="text" name="<%= FormFields.InvoiceOrderNumber %>" size="20" value="<%= ProjectInfo.PurchaseOrderNumber.HTMLEncode() %>" /></div>
            </div>
            <div class="flex flex-align-center flex-fill mt10">
              <div class="w200 align-right pr10">Description:</div>
              <div class=""><input class="form-control w100p" type="text" name="<%= FormFields.InvoiceDescription %>" size="20" /></div>
            </div>
            <div class="flex flex-align-center flex-fill mt10">
              <div class="w200 align-right pr10">Xero Client:</div>
              <div>
                <select name="<%= FormFields.InvoiceXeroContactId %>" class="form-control w100p" size="1">
                  <%= WebHelper.GetXeroContactOptions(true, XeroContactsList, ProjectInfo.XeroContactId) %>
                </select>
              </div>
            </div>
            <div class="flex flex-align-center flex-fill mt20">
              <div class="w200 align-right pr10 strong">Invoice Total:</div>
              <div class="nowrap strong"><span id="invoiceTotal" class="invoiceTotal"></span></div>
              <div class="align-right">
                <button class="btn btn-primary <%= CanSendInvoiceToXero ? "" : "disabled" %>" id="btnSubmitInvoice">Submit Invoice To Xero</button>
                <% if (!CanSendInvoiceToXero) { %>
                  <p class="fa-sm mt10">Please set the Xero Account in <a href="<%= PathHelper.Pages.ProjectSettings(ProjectInfo.JobNumber) %>">Project Settings</a> to enable this function.</p>
                <% } %>
              </div>
            </div>
          </div>
        <% } %>

      </form>
    </div>

    <% if (ItemsToInvoiceCount > 0) { %>
      <script type="text/javascript">
        (function($) {

          var formCreateInvoice, btnSubmitInvoice, allSameSign;

          $(document).ready(function() {

            formCreateInvoice = $("#formCreateInvoice");
            btnSubmitInvoice = $("#btnSubmitInvoice");
            btnSubmitInvoice.click(SubmitInvoice);

            $(".tblItemsToInvoice input:checkbox").change(ShowInvoiceTotal);
            ShowInvoiceTotal();

            $(".btnDeleteItem").click(function (evt) {
              var btn = $(evt.target);
              var tr = btn.closest("tr");
              var invItemId = toDecimalInt(tr.data("<%= FormFields.DeleteInvItemId.ToLower() %>"), 0);
              if (invItemId <= 0) {
                alert("Something went wrong. Please reload page and try again.");
                return;
              }
              if (!confirm("Do you wish to permanently delete this invoice item?")) return;
              AjaxSubmit({
                action: "<%= AjaxAction.DeleteInvItem %>",
                data: {
                  "<%= FormFields.DeleteInvItemId %>": invItemId
                }
              });
            });
          });

          function ShowInvoiceTotal() {
            var total = 0;
            var firstSign = null;
            allSameSign = true;
            $(".tblItemsToInvoice tr[data-amount]").each(function (i, e) {
              $row = $(e);
              if ($row.find("input:checkbox:checked").length == 1) {
                var rowAmount = parseFloat($row.data("amount"));
                var rowQty = parseFloat($row.data("qty"));
                if (firstSign == null) firstSign = Math.sign(rowAmount);
                else if (firstSign != Math.sign(rowAmount)) allSameSign = false; // Not all the same sign.
                total += rowAmount * rowQty;
              }
            });
            if (total < 0) $("#invoiceTotal").text("($" + Math.abs(total).toFixed(2) + ")");
            else $("#invoiceTotal").text("$" + total.toFixed(2));
          }

          function SubmitInvoice() {
            if (!allSameSign) {
              alert("All items in an invoice must be either positive or negative, not mixed.");
              return;
            }
            AjaxSubmit({
              form: formCreateInvoice,
            });
          }

        })(jQuery);
      </script>
    <% } %>
  <% }; %>

  <div class="mb30">

    <% ShowQuoteList(); %>

    <% if (ItemsWithoutQuoteCount > 0) { %>
      <% ShowAssignToQuote(); %>
    <% } %>

    <% if (CanAddDeleteItems) ShowAddInvoiceItem(); %>

    <% if (CanSubmitInvoice) ShowCreateInvoice(); %>

  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

</asp:Content>
