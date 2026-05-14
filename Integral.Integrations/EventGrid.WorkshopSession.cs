using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Integral.Web;
using Integral.Web.Services;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace Integral.Integrations {

  public partial class EventGrid {

    public class WorkshopSession {

      public enum WorkshopUpdateEventType {
        added = 1,
        updated = 2,
        deleted = 3,
        added_test = 4,
        updated_test = 5,
        deleted_test = 6
      }

      public static string PostWorkshopUpdate(
        WorkshopUpdateEventType eventType,
        int workshopEventId, string workshopName, string friendlyId,
        string clientName, string programJobNumber, string programJobName,
        string workshopStatus, DateTime? startDateTime, DateTime? endDateTime,
        string keyFacilitatorName, string venue) {

        var payload = new UpdatePayload();

        payload.id = workshopEventId.ToString();
        payload.subject = eventType.ToString();
        payload.eventType = eventType.ToString();
        payload.eventTime = DateTime.UtcNow;

        payload.data.eventType = eventType.ToString();
        payload.data.workshopEventId = workshopEventId;
        payload.data.Client = clientName;
        payload.data.Programname = programJobName;
        payload.data.workshopStatus = workshopStatus;
        payload.data.startdatetime = startDateTime;
        payload.data.enddatetime = endDateTime;
        payload.data.jobno = programJobNumber;
        payload.data.keyfacilitator = keyFacilitatorName;
        payload.data.venue = venue;
        payload.data.workshopid = friendlyId;
        payload.data.workshopname = workshopName;

        // Note the endpoint expects an array of objects.
        var postPayload = new List<UpdatePayload>();
        postPayload.Add(payload);
        string postResult = PostObject(postPayload);
        return postResult;
      }

      private static string PostObject(object payload) {

        string json = null;
        try {
          json = JsonConvert.SerializeObject(payload);
          var content = new StringContent(json, Encoding.UTF8, "application/json");
          using (var httpClient = new HttpClient()) {
            httpClient.DefaultRequestHeaders.Add("aeg-sas-key", ConfigHelper.EventGridAccessKey);
            var result = httpClient.PostAsync(ConfigHelper.EventGridEndpoint_WorkshopSession, content).Result;
            if (!result.IsSuccessStatusCode) return "Unsuccessful";
          }
          return null;
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("EventGrid_PostWorkshopUpdate")
            .WithProperty("PayloadJson", json)
            .Track();
          return ex.Message;
        }

      }

      public class UpdatePayload {
        public string id { get; set; }
        public string subject { get; set; }
        public string eventType { get; set; }
        public DateTime eventTime { get; set; }
        public PayloadData data { get; set; }
        public UpdatePayload() {
          data = new PayloadData();
        }
        public class PayloadData {
          public int workshopEventId { get; set; }
          public string eventType { get; set; }
          public string Client { get; set; }
          public string Programname { get; set; }
          public string jobno { get; set; }
          public string keyfacilitator { get; set; }
          public string workshopStatus { get; set; }
          public DateTime? startdatetime { get; set; }
          public DateTime? enddatetime { get; set; }
          public string venue { get; set; }
          public string workshopid { get; set; }
          public string workshopname { get; set; }
        }
      }

    }

  }
}
