using System;
using System.Collections.Generic;
using System.Text;
using Integral.Web;
using Newtonsoft.Json;
using System.Net.Http;

namespace Integral.Integrations {

  public partial class EventGrid {

    public class PurchaseOrder {

      private const string DefaultXeroType = "ACCPAY";
      private const string DefaultLineItemAccountCode = "50033";

      public enum EventType {
        PurchaseOrderRequested = 1,
        PurchaseOrderRequested_test = 2
      }

      public static PostResult PostNewPurchaseOrder(PurchaseOrderInfo purchaseOrderInfo) {

        var postResult = new PostResult();
        postResult.Success = false;

        var payload = new UpdatePayload(
          ConfigHelper.IsLiveServer ? "update" : "test",
          ConfigHelper.IsLiveServer ? EventType.PurchaseOrderRequested : EventType.PurchaseOrderRequested_test,
          DateTime.UtcNow, 
          purchaseOrderInfo);

        // Note the endpoint expects an array of objects.
        var postPayload = new List<UpdatePayload>();
        postPayload.Add(payload);

        // Serialise the object and post to eventgrid.
        // Note that null values will be ignore (not serialised) to enable sending a "blank" line item.
        var json = JsonConvert.SerializeObject(postPayload, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        postResult.Json = json;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        using (var httpClient = new HttpClient()) {
          httpClient.DefaultRequestHeaders.Add("aeg-sas-key", ConfigHelper.EventGridAccessKey_Accounting);
          var result = httpClient.PostAsync(ConfigHelper.EventGridEndpoint_Accounting, content).Result;
          postResult.Success = result.IsSuccessStatusCode;
        }

        return postResult;
      }

      public class PostResult {
        public bool Success { get; internal set; }
        public string Json { get; internal set; }
      }

      public class UpdatePayload {
        public string id { get; set; }
        public string subject { get; set; }
        public string eventType { get; set; }
        public DateTime eventTime { get; set; }
        public PurchaseOrderInfo data { get; set; }
        public UpdatePayload(string id, EventType eventType, DateTime eventTime, PurchaseOrderInfo data) {
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

      public class PurchaseOrderInfo {
        public DateTime Date { get; private set; }
        public string Type { get; private set; }
        public string Reference { get; private set; }
        public XeroContact Contact { get; private set; }
        public List<LineItem> LineItems { get; private set; }
        public PurchaseOrderInfo(DateTime date, int purchaseOrderId, string purchaseOrderNumber, Guid contactUID, string jobNumber) {
          this.Date = date;
          this.Type = DefaultXeroType;
          this.Reference = purchaseOrderId.ToString();
          this.Contact = new XeroContact(contactUID);
          this.LineItems = new List<LineItem>() {
            // Default "blank" line item which just shows PO number and job number in the description.
            new LineItem("Job Number: " + jobNumber + "\nPurchase Order No: " + purchaseOrderNumber, null, null, null)
          };
        }
        public void AddLineItem(LineItem lineItem) {
          LineItems.Add(lineItem);
        }
      }
      public class LineItem {
        // Note the nullable members are just to allow a "blank" line item for
        // descriptive purposes - normal line items should have all values.
        public string Description { get; private set; }
        public decimal? Quantity { get; private set; }
        public string AccountCode { get; private set; }
        public decimal? UnitAmount { get; private set; }
        public string TaxType { get; private set; }
        public LineItem(string description, decimal? quantity, decimal? unitAmount, string xeroTaxType) {
          this.Description = description;
          this.Quantity = quantity;
          this.AccountCode = quantity == null ? null : DefaultLineItemAccountCode; // null if sending a "blank" line item.
          this.UnitAmount = unitAmount;
          this.TaxType = xeroTaxType;
        }
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
