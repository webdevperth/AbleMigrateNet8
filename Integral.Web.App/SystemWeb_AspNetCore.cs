using Integral.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Integral.Web {

  public sealed class SystemWeb_AspNetCore : ISystemWeb {

    private readonly IHttpContextAccessor _accessor;
    private readonly IWebHostEnvironment _env;

    public SystemWeb_AspNetCore(IHttpContextAccessor accessor, IWebHostEnvironment env) {
      _accessor = accessor;
      _env = env;
    }

    private HttpContext RequestContext => _accessor.HttpContext;
    private HttpRequest Request => RequestContext?.Request;
    private HttpResponse Response => RequestContext?.Response;

    public bool HasRequest => RequestContext != null;

    // Method & content shape

    public bool IsHttpGet => HttpMethods.IsGet(Request?.Method ?? string.Empty);
    public bool IsHttpPost => HttpMethods.IsPost(Request?.Method ?? string.Empty);
    public bool IsAjaxPost => IsHttpPost && RequestHeaderHasValue(AppHelper.HttpHeaders.IsAjax);
    public bool IsTabulator => RequestHeaderHasValue(AppHelper.HttpHeaders.IsTabulator);

    public bool HasFormFields =>
      IsHttpPost && Request != null && Request.HasFormContentType && Request.Form?.Count > 0;

    public bool IsFormUrlEncoded =>
      IsHttpPost && (Request?.ContentType ?? string.Empty)
        .Contains("urlencoded", StringComparison.OrdinalIgnoreCase);

    public bool IsResponseContentTypeJson =>
      (Response?.ContentType ?? string.Empty)
        .Contains("json", StringComparison.OrdinalIgnoreCase);

    // URL

    public string RequestRawUrl =>
      Request == null ? null
        : Request.PathBase + Request.Path + Request.QueryString;

    public string RequestRawUrlNoQuery =>
      Request == null ? null : Request.PathBase + Request.Path;

    public string RequestUrlLeftPart(UriPartial partial) {
      if (Request == null) return null;
      var full = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}{Request.QueryString}";
      return new Uri(full).GetLeftPart(partial);
    }

    public string RequestUserAgent => GetRequestHeader("User-Agent");
    public string RequestMethod => Request?.Method;
    public string RequestPhysicalPath => Request == null ? null : ServerMapPath(Request.Path.Value);
    public string RequestUrlHost => Request?.Host.Host;
    public string JavaScriptStringEncode(string content) => System.Web.HttpUtility.JavaScriptStringEncode(content);
    public Dictionary<string, StringValues> ParseQueryString(string query) => QueryHelpers.ParseQuery(query);
    public string HtmlEncode(string content) => WebUtility.HtmlEncode(content);
    public string UrlEncode(string content) => WebUtility.UrlEncode(content);
    public string UrlDecode(string content) => WebUtility.UrlDecode(content);

    // Querystring

    public string RequestQueryString =>
      Request == null ? null : Request.QueryString.HasValue
        ? Request.QueryString.Value.TrimStart('?')
        : string.Empty;

    public bool RequestQueryStringContains(string key) =>
      Request?.Query?.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) ?? false;

    public string RequestQueryStringValue(string key) =>
      Request?.Query[key];

    public Uri GetReferrerUri() {
      if (AppHelper.GetRequestItemOrNull(AppHelper.RequestItemKey.ReferrerUri) is Uri cached) return cached;

      string referrerUrl = GetRequestHeader(AppHelper.HttpHeaders.Referrer);
      if (string.IsNullOrEmpty(referrerUrl)) referrerUrl = GetRequestHeader("Referer");
      if (string.IsNullOrEmpty(referrerUrl)) referrerUrl = GetRequestHeader("Origin");

      Uri.TryCreate(referrerUrl, UriKind.Absolute, out var uri);
      AppHelper.SetRequestItem(AppHelper.RequestItemKey.ReferrerUri, uri);
      return uri;
    }

    public string ReferrerAbsolutePath => GetReferrerUri()?.AbsolutePath;

    // Headers

    public string GetRequestHeader(string name) {
      if (Request == null) return null;
      var v = Request.Headers[name];
      return v.Count == 0 ? null : v.ToString();
    }

    public bool RequestHeaderHasValue(string name) => !string.IsNullOrEmpty(GetRequestHeader(name));

    public string RequestHeadersAsString =>
      Request == null
        ? null
        : string.Join("\r\n", Request.Headers.Select(h => $"{h.Key}: {h.Value}"));

    public void AddResponseHeader(string name, string value) {
      if (Response == null) return;
      Response.Headers[name] = value;
    }

    // Form

    public string GetFormValue(string name) =>
      Request != null && Request.HasFormContentType ? Request.Form[name].ToString() : null;

    public IReadOnlyCollection<string> GetFormKeys() =>
      Request != null && Request.HasFormContentType
        ? Request.Form.Keys.ToArray()
        : Array.Empty<string>();

    public string RequestFormAsString =>
      Request != null && Request.HasFormContentType
        ? string.Join("&", Request.Form.Select(kv => $"{kv.Key}={kv.Value}"))
        : null;

    // Files

    public bool RequestFilesContains(string key) =>
      Request?.HasFormContentType == true && Request.Form.Files.GetFile(key) != null;

    public IUploadedFile GetRequestFile(string key) {
      if (Request?.HasFormContentType != true) return null;
      var f = Request.Form.Files.GetFile(key);
      return f == null ? null : new AspNetCoreUploadedFile(f);
    }

    // Cookies

    public string GetRequestCookieValue(string name) => Request?.Cookies[name];

    public void AddResponseCookie(SessionCookieDescriptor descr) {
      if (Response == null) return;
      Response.Cookies.Append(descr.Name, descr.Value ?? string.Empty, new CookieOptions {
        HttpOnly = descr.HttpOnly,
        Secure = descr.Secure,
        SameSite = descr.SameSite,
        Expires = descr.Expires,
        Path = descr.Path,
        IsEssential = true,
      });
    }

    // Session — values stored as JSON strings under the hood; existing callers
    // already coerce results via ToStringOrEmptyIfNull / ToIntOrNull etc.

    public bool HasSession => RequestContext?.Session?.IsAvailable == true;
    public string SessionID => RequestContext?.Session?.Id;

    public object GetSessionValue(string key) =>
      RequestContext?.Session?.GetString(key);

    public void SetSessionValue<T>(string key, T value) {
      var session = RequestContext?.Session;
      if (session == null) return;
      if (value is null) { session.Remove(key); return; }
      var s = value is string str ? str
            : value is IFormattable ? value.ToString()
            : JsonConvert.SerializeObject(value);
      session.SetString(key, s);
    }

    // Per-request items — HttpContext.Items uses object keys; standardise on string.

    public bool RequestItemExists(string key) => RequestContext?.Items.ContainsKey(key) ?? false;
    public object GetRequestItemValue(string key) =>
      RequestContext != null && RequestContext.Items.TryGetValue(key, out var v) ? v : null;
    public void SetRequestItemValue<T>(string key, T value) {
      if (RequestContext != null) RequestContext.Items[key] = value;
    }
    public void RequestItemRemove(string key) => RequestContext?.Items.Remove(key);

    // Misc

    public string ApplicationVirtualPath => Request?.PathBase.Value.EnsureEndsWith("/", StringExt.Ensure.IfNotBlank);

    public string GetRequestBody() {
      try {
        if (Request == null) return null;
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using var reader = new StreamReader(
          Request.Body, encoding: Encoding.UTF8,
          detectEncodingFromByteOrderMarks: true,
          bufferSize: 1024, leaveOpen: true);
        var body = reader.ReadToEnd();
        Request.Body.Position = 0;
        return body;
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex).WithOperation(nameof(GetRequestBody)).Track();
        return null;
      }
    }

    public string ServerMapPath(string virtualPath) =>
      Path.Combine(
        _env.ContentRootPath,
        (virtualPath ?? string.Empty).TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));

    public void ResponseWrite(string s) => Response?.WriteAsync(s).GetAwaiter().GetResult();
    public void ResponseWriteLine(string s) => Response?.WriteAsync(s + "\n").GetAwaiter().GetResult();
    public void SetContentType(string ct) { if (Response != null) Response.ContentType = ct; }
    public void SetStatusCode(int sc) { if (Response != null) Response.StatusCode = sc; }
    public void ClearResponseContent() => Response?.Clear();
    public void ResponseComplete() => Response?.CompleteAsync().GetAwaiter().GetResult();

    public Stream RequestInputStream => Request?.Body;

    private sealed class AspNetCoreUploadedFile : IUploadedFile {
      private readonly IFormFile _f;
      public AspNetCoreUploadedFile(IFormFile f) => _f = f;
      public string FileName => _f.FileName;
      public string ContentType => _f.ContentType;
      public long Length => _f.Length;
      public Stream OpenReadStream() => _f.OpenReadStream();
    }
  }

  // SessionCookieDescriptor lives once for the .NET 10 build.
  public sealed class SessionCookieDescriptor {
    public string Name { get; set; }
    public string Value { get; set; }
    public bool HttpOnly { get; set; }
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;
    public bool Secure { get; set; }
    public DateTime? Expires { get; set; }
    public string Path { get; set; }
    public bool Shareable { get; set; }
  }
}
