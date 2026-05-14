using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;

namespace Integral.Web {

  // Static facade for calls to SystemWeb.
  // For .NET Framework, implementation is SystemWeb_Framework.
  // For .NET Core, implementation is SystemWeb_AspNetCore.
  public static class SystemWeb {

    private static ISystemWeb Impl => ServiceLocator.Instance.GetRequiredService<ISystemWeb>();

    // public static HttpRequest Request => Impl.Request;
    // public static HttpResponse Response => Impl.Response;
    public static bool HasRequest => Impl.HasRequest;

    public static bool IsHttpGet => Impl.IsHttpGet;
    public static bool IsHttpPost => Impl.IsHttpPost;
    public static bool IsAjaxPost => Impl.IsAjaxPost;
    public static bool IsTabulator => Impl.IsTabulator;
    public static bool HasFormFields => Impl.HasFormFields;
    public static bool IsFormUrlEncoded => Impl.IsFormUrlEncoded;
    public static bool IsResponseContentTypeJson => Impl.IsResponseContentTypeJson;

    // URL
    public static string RequestRawUrl => Impl.RequestRawUrl;
    public static string RequestRawUrlNoQuery => Impl.RequestRawUrlNoQuery;
    public static string RequestUrlLeftPart(UriPartial partial) => Impl.RequestUrlLeftPart(partial);
    public static string RequestUserAgent => Impl.RequestUserAgent;
    public static string RequestMethod => Impl.RequestMethod;
    public static string RequestPhysicalPath => Impl.RequestPhysicalPath;
    public static string RequestUrlHost => Impl.RequestUrlHost;
    public static string JavaScriptStringEncode(string content) => Impl.JavaScriptStringEncode(content);
    public static string HtmlEncode(string content) => Impl.HtmlEncode(content);
    public static Dictionary<string, StringValues> ParseQueryString(string query) => Impl.ParseQueryString(query);
    public static string UrlEncode(string content) => Impl.UrlEncode(content);
    public static string UrlDecode(string content) => Impl.UrlDecode(content);

    // Querystring
    public static string RequestQueryString => Impl.RequestQueryString;
    public static bool RequestQueryStringContains(string key) => Impl.RequestQueryStringContains(key);
    public static string RequestQueryStringValue(string key) => Impl.RequestQueryStringValue(key);
    public static string ReferrerAbsolutePath => Impl.ReferrerAbsolutePath;
    public static Uri GetReferrerUri() => Impl.GetReferrerUri();

    // Headers
    public static string GetRequestHeader(string key) => Impl.GetRequestHeader(key);
    public static bool RequestHeaderHasValue(string key) => Impl.RequestHeaderHasValue(key);
    public static string RequestHeadersAsString => Impl.RequestHeadersAsString;
    public static void AddResponseHeader(string name, string value) => Impl.AddResponseHeader(name, value);

    // Form
    public static string GetFormValue(string name) => Impl.GetFormValue(name);
    public static IReadOnlyCollection<string> GetFormKeys() => Impl.GetFormKeys();        // replaces RequestForm.AllKeys
    public static string RequestFormAsString => Impl.RequestFormAsString;

    // Files
    public static bool RequestFilesContains(string key) => Impl.RequestFilesContains(key);
    public static IUploadedFile GetRequestFile(string key) => Impl.GetRequestFile(key);

    // Cookies
    public static string GetRequestCookieValue(string name) => Impl.GetRequestCookieValue(name);
    public static void AddResponseCookie(SessionCookieDescriptor cookie) => Impl.AddResponseCookie(cookie);

    // Session
    public static bool HasSession => Impl.HasSession;
    public static string SessionID => Impl.SessionID;
    public static object GetSessionValue(string key) => Impl.GetSessionValue(key);
    public static void SetSessionValue(string key, string value) => Impl.SetSessionValue(key, value);
    public static void SetSessionValue<T>(string key, T value) => Impl.SetSessionValue(key, value);

    // Per-request items (HttpContext.Items)
    public static bool RequestItemExists(string key) => Impl.RequestItemExists(key);
    public static object GetRequestItemValue(string key) => Impl.GetRequestItemValue(key);
    public static void SetRequestItemValue<T>(string key, T value) => Impl.SetRequestItemValue(key, value);
    public static void RequestItemRemove(string key) => Impl.RequestItemRemove(key);

    // Misc
    public static string ApplicationVirtualPath => Impl.ApplicationVirtualPath;
    public static string GetRequestBody() => Impl.GetRequestBody();
    public static string ServerMapPath(string virtualPath) => Impl.ServerMapPath(virtualPath);
    public static void ResponseWrite(string s) => Impl.ResponseWrite(s);
    public static void ResponseWriteLine(string s) => Impl.ResponseWriteLine(s);
    public static void SetContentType(string contentType) => Impl.SetContentType(contentType);
    public static void SetStatusCode(int statusCode) => Impl.SetStatusCode(statusCode);
    public static void ClearResponseContent() => Impl.ClearResponseContent();
    public static Stream RequestInputStream => Impl.RequestInputStream;
  }
}
