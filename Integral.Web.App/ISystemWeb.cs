using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;

namespace Integral.Web {

  public interface ISystemWeb {

    // Lifecycle / context
    // HttpContext RequestContext { get; }
    // HttpRequest Request { get; }
    // HttpResponse Response { get; }
    bool HasRequest { get; }      // replaces ad-hoc null checks at call sites

    // Method & content shape
    bool IsHttpGet { get; }
    bool IsHttpPost { get; }
    bool IsAjaxPost { get; }
    bool IsTabulator { get; }
    bool HasFormFields { get; }
    bool IsFormUrlEncoded { get; }
    bool IsResponseContentTypeJson { get; }

    // URL
    string RequestRawUrl { get; }
    string RequestRawUrlNoQuery { get; }
    string RequestUrlLeftPart(UriPartial partial);
    string RequestUserAgent { get; }
    string RequestMethod { get; }
    string RequestRealRelativePath { get; }
    string RequestUrlHost { get; }
    string HtmlEncode(string content);
    string JavaScriptStringEncode(string content);
    Dictionary<string, StringValues> ParseQueryString(string query);
    string UrlEncode(string content);
    string UrlDecode(string content);

    // Querystring
    string RequestQueryString { get; }
    bool RequestQueryStringContains(string key);
    string RequestQueryStringValue(string key);
    string ReferrerAbsolutePath { get; }
    Uri GetReferrerUri();

    // Headers
    string GetRequestHeader(string name);
    bool RequestHeaderHasValue(string name);
    string RequestHeadersAsString { get; }
    void AddResponseHeader(string name, string value);

    // Form
    string GetFormValue(string name);                 // replaces RequestForm[name]
    IReadOnlyCollection<string> GetFormKeys();        // replaces RequestForm.AllKeys
    string RequestFormAsString { get; }

    // Files
    bool RequestFilesContains(string key);
    IUploadedFile GetRequestFile(string key);         // POCO, see below

    // Cookies
    string GetRequestCookieValue(string name);
    void AddResponseCookie(SessionCookieDescriptor cookie);    // POCO, see below

    // Session
    bool HasSession { get; }
    string SessionID { get; }
    object GetSessionValue(string key);
    void SetSessionValue<T>(string key, T value);

    // Per-request items (HttpContext.Items)
    bool RequestItemExists(string key);
    object GetRequestItemValue(string key);
    void SetRequestItemValue<T>(string key, T value);
    void RequestItemRemove(string key);

    // Misc
    string ApplicationVirtualPath { get; }
    string WebRootPhysicalPath { get; }
    string GetRequestBody();
    void ResponseWrite(string s);
    void ResponseWriteLine(string s);
    void SetContentType(string contentType);
    void SetStatusCode(int statusCode);
    void ClearResponseContent();
    void ResponseComplete();
    Stream RequestInputStream { get; }
  }

  public interface IUploadedFile {
    string FileName { get; }
    string ContentType { get; }
    long Length { get; }
    Stream OpenReadStream();
  }
}
