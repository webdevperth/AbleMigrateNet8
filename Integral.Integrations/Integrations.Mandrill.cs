using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Integral.Web;

namespace Integral.Integrations {

  public class Mandrill {

    internal class EndPoints {
      internal const string Search = "https://mandrillapp.com/api/1.0/messages/search";
      internal const string Content = "https://mandrillapp.com/api/1.0/messages/content";
    }

    static Mandrill() {
      System.Net.ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
    }

    public static string SendHttpPost(string endpointAddr, Dictionary<string, string> httpHeaders, string payload, string contentType = "application/json") {
      using (var httpClient = new HttpClient()) {
        if (httpHeaders != null) foreach (var h in httpHeaders) httpClient.DefaultRequestHeaders.Add(h.Key, h.Value);
        var encodedPayload = new StringContent(payload, Encoding.UTF8, contentType);
        var postResult = httpClient.PostAsync(endpointAddr, encodedPayload).Result;
        if (!postResult.IsSuccessStatusCode) return null;
        var postContent = postResult.Content;
        if (postContent == null) return null;
        return postContent.ReadAsStringAsync().Result;
      }
    }

    private static string AddToQuery(string query, string newKey, string newValue) {
      if (!newKey.IsNullOrEmpty() && !newValue.IsNullOrEmpty()) {
        if (query.Length > 0) query += " AND ";
        query += newKey + ": \"" + newValue + "\"";
      }
      return query;
    }

    public static MessageContent GetMessageContent(string recipientAddress, string subject, DateTime searchFrom, DateTime searchTo) {
      // Return first message content found in the search criteria.

      string query = "";
      if (!recipientAddress.IsNullOrEmpty()) query = AddToQuery(query, "full_email", recipientAddress);
      if (!subject.IsNullOrEmpty()) query = AddToQuery(query, "subject", subject);

      if (query.IsNullOrEmpty()) throw new ArgumentException("Either subject or recipientAddress required.");

      string jsonQuery = @"{
	        ""key"": """ + ConfigHelper.MandrillAPIKey + @""",
	        ""query"": " + JsonConvert.ToString(query) + @",
	        ""date_from"": " + JsonConvert.SerializeObject(searchFrom) + @",
	        ""date_to"": " + JsonConvert.SerializeObject(searchTo) + @",
	        ""tags"": [],
	        ""senders"": [],
	        ""api_keys"": [],
	        ""limit"": 1
        }";

      string responseJson = SendHttpPost(EndPoints.Search, null, jsonQuery);

      string messageID = responseJson.RegexFirstGroupOrNull(@"""_id"":\s*""([^""]+)""");

      return GetMessageContent(messageID);
    }

    public static MessageContent GetMessageContent(string messageID) {

      string jsonQuery = @"{
	        ""key"": """ + ConfigHelper.MandrillAPIKey + @""",
	        ""id"": """ + messageID + @"""
        }";

      string responseJson = SendHttpPost(EndPoints.Content, null, jsonQuery);

      if (responseJson.IsNullOrEmpty()) return null;

      var messageContent = JsonConvert.DeserializeObject<MessageContent>(responseJson); //, new JsonSerializerSettings() { });

      return messageContent;
    }

    public class MessageContent {
      public string subject { get; set; }
      public string from_email { get; set; }
      public string from_name { get; set; }
      public Recipient to { get; set; }
      public string html { get; set; }
      public string text { get; set; }
      public class Recipient {
        public string email { get; set; }
        public string name { get; set; }
      }
    }

  }
}
