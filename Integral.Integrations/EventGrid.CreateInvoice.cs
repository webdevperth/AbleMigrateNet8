using System;
using System.Collections.Generic;
using System.Text;
using Integral.Web;
using Newtonsoft.Json;
using System.Net.Http;

namespace Integral.Integrations {

  public partial class EventGrid {

    public class Invoice {

      internal enum XeroInvoiceType {
        ACCREC,       // For positive (0+) invoices.
        ACCRECCREDIT  // For negative invoices.
      }
      private enum EventType {
        InvoiceRequested = 1,
        InvoiceRequested_test = 2
      }

      public static PostResult CreateInvoice(InvoiceInfo invoiceInfo) {

        // Set invoice type depending on whether total is positive or negative.
        decimal invoiceTotal = 0;
        foreach (var item in invoiceInfo.LineItems) {
          if (item.UnitAmount != null && item.Quantity != null) invoiceTotal += (item.UnitAmount.Value * item.Quantity.Value);
        }
        if (invoiceTotal < 0) {
          invoiceInfo.SetInvoiceType(XeroInvoiceType.ACCRECCREDIT);
        } else {
          invoiceInfo.SetInvoiceType(XeroInvoiceType.ACCREC);
        }
        // Now ensure all amounts are positive before submitting to Xero.
        foreach (var item in invoiceInfo.LineItems) item.SetAmountPositive();

        var postResult = new PostResult();
        postResult.Success = false;

        var payload = new UpdatePayload(
          ConfigHelper.IsLiveServer ? "update" : "test",
          ConfigHelper.IsLiveServer ? EventType.InvoiceRequested : EventType.InvoiceRequested_test,
          DateTime.UtcNow,
          invoiceInfo);

        // Note the endpoint expects an array of objects.
        var postPayload = new List<UpdatePayload>();
        postPayload.Add(payload);

        if (!ConfigHelper.IsLiveServer) { // Only send on prod.
          postResult.Success = true;
          return postResult;
        }

        postResult = PostPayload(ConfigHelper.EventGridEndpoint_Accounting, postPayload, ConfigHelper.EventGridAccessKey_Accounting);

        return postResult;
      }

      public static PostResult PostPayload(string endpoint, object postPayload, string accessKey) {

        var postResult = new PostResult();
        postResult.Success = false;

        // Serialise the object and post to eventgrid.
        // Note that null values will be ignore (not serialised) to enable sending a "blank" line item.
        var json = JsonConvert.SerializeObject(postPayload, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        postResult.Json = json;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        using (var httpClient = new HttpClient()) {
          httpClient.DefaultRequestHeaders.Add("aeg-sas-key", accessKey);
          var result = httpClient.PostAsync(endpoint, content).Result;
          postResult.Success = result.IsSuccessStatusCode;
        }

        // TODO : there is a potential for losing this event if the system crashes between the two method
        // TODO : calls, in the future we should consider using an outbox pattern for some level of reliability
        // TODO : of publishing the events correctly.

        return postResult;
      }

      public class PostResult {
        public bool Success { get; internal set; }
        public string Json { get; internal set; }
      }

      private class UpdatePayload {
        public string id { get; set; }
        public string subject { get; set; }
        public string eventType { get; set; }
        public DateTime eventTime { get; set; }
        public InvoiceInfo data { get; set; }
        public UpdatePayload(string id, EventType eventType, DateTime eventTime, InvoiceInfo data) {
          this.id = id;
          this.subject = eventType.ToString();
          this.eventType = eventType.ToString();
          this.eventTime = eventTime;
          this.data = data;
        }
        public string ToJson() {
          return JsonConvert.SerializeObject(this);
        }
      }

      public class InvoiceInfo {
        public DateTime Date { get; private set; }
        public string Type { get; private set; }
        public string Reference { get; private set; }
        public XeroContact Contact { get; private set; }
        public List<LineItem> LineItems { get; private set; }
        public InvoiceInfo(string jobNumber, DateTime date, string description, int invoiceId, string clientOrderNumber, Guid contactUID, string projectName) {
          this.Date = date;
          this.Type = XeroInvoiceType.ACCREC.ToString(); // The default.
          this.Reference = invoiceId.ToString();
          this.Contact = new XeroContact(contactUID);
          this.LineItems = new List<LineItem>() {
            // Default "blank" line item which just shows PO number and job number in the description.
            new LineItem(
              (clientOrderNumber.IsNullOrEmpty() ? "" : $"Client Order No: {clientOrderNumber}\n") +
              $"Job No: {jobNumber}\n" +
              $"Name: {projectName}\n" +
              (description.IsNullOrEmpty() ? "" : $"Description: {description}"),
              null, null, null, null)
          };
        }
        public void AddLineItem(LineItem lineItem) => LineItems.Add(lineItem);
        internal void SetInvoiceType(XeroInvoiceType invoiceType) => this.Type = invoiceType.ToString();
      }
      public class LineItem {
        // Note the nullable members are just to allow a "blank" line item for
        // descriptive purposes - normal line items should have all values.
        public string Description { get; private set; }
        public decimal? UnitAmount { get; private set; }
        public decimal? Quantity { get; private set; }
        public string TaxType { get; private set; }
        public string AccountCode { get; private set; }
        public LineItem(string description, decimal? unitAmount, decimal? quantity, string xeroTaxType, string accountCode) {
          this.Description = description;
          this.UnitAmount = unitAmount;
          this.Quantity = quantity;
          this.TaxType = xeroTaxType;
          this.AccountCode = accountCode;
        }
        public void SetAmountPositive() => UnitAmount = UnitAmount == null ? null : (decimal?)Math.Abs(UnitAmount.Value);
      }
      public class XeroContact {
        public Guid ContactID { get; private set; }
        public XeroContact(Guid contactUID) {
          this.ContactID = contactUID;
        }
      }

    }

  }
}
