#if NETFRAMEWORK

using Integral.Web.Services;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace Integral.Web {

  // ISystemWeb implementation for .NET Framework.

  public sealed class SystemWeb_Framework : ISystemWeb {

    // Lifecycle / context
    private HttpContext RequestContext => HttpContext.Current;
    private HttpRequest Request => RequestContext?.Request;
    private HttpResponse Response => RequestContext?.Response;
    public bool HasRequest => Request != null; // replaces ad-hoc null checks at call sites

    // Method & content shape
    public bool IsHttpGet => Request?.HttpMethod?.Equals("GET", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsHttpPost => Request?.HttpMethod?.Equals("POST", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsAjaxPost => IsHttpPost && RequestHeaderHasValue(AppHelper.HttpHeaders.IsAjax);
    public bool IsTabulator => RequestHeaderHasValue(AppHelper.HttpHeaders.IsTabulator);
    public bool HasFormFields => IsHttpPost && Request?.Form?.HasKeys() == true && Request?.Form?.Count > 0;
    public bool IsFormUrlEncoded => IsHttpPost && (Request?.ContentType ?? string.Empty).ContainsIgnoreCase("urlencoded");
    public bool IsResponseContentTypeJson => Response?.ContentType?.ToLowerInvariant()?.Contains("json") ?? false;

    // URL
    public string RequestRawUrl => Request?.RawUrl;
    public string RequestRawUrlNoQuery => (RequestRawUrl ?? string.Empty).IndexOf("?") == -1 ? RequestRawUrl : RequestRawUrl.Split('?')[0];
    public string RequestUrlLeftPart(UriPartial urlPartial) => Request?.Url.GetLeftPart(urlPartial);
    public string RequestUserAgent => Request?.UserAgent;
    public string RequestMethod => Request?.HttpMethod;
    public string RequestPhysicalPath => Request?.PhysicalPath;
    public string RequestUrlHost => Request.Url.Host;
    public string JavaScriptStringEncode(string content) => HttpUtility.JavaScriptStringEncode(content);
    public Dictionary<string, StringValues> ParseQueryString(string query) => HttpUtility.ParseQueryString(query).ToStringValuesDictionary();
    public string HtmlEncode(string content) => HttpUtility.HtmlEncode(content);
    public string UrlEncode(string content) => HttpUtility.UrlEncode(content);
    public string UrlDecode(string content) => HttpUtility.UrlDecode(content);

    // Querystring
    public string RequestQueryString => Request?.QueryString.ToString();
    public bool RequestQueryStringContains(string s) => Request?.QueryString?.AllKeys?.Contains(s, StringComparer.OrdinalIgnoreCase) ?? false;
    public string RequestQueryStringValue(string key) => Request?.QueryString?[key];

    // Headers
    public string GetRequestHeader(string name) => Request?.Headers?[name];
    public bool RequestHeaderHasValue(string name) => !string.IsNullOrEmpty(GetRequestHeader(name));
    public string RequestHeadersAsString => Request?.Headers?.ToString();
    public void AddResponseHeader(string name, string value) => Response.Headers.Add(name, value);

    public Uri GetReferrerUri() {

      if (AppHelper.GetRequestItemOrNull(AppHelper.RequestItemKey.ReferrerUri) is Uri uri) return uri;

      // This custom header is sent with every jquery ajax (get or post) call.
      string referrerUrl = GetRequestHeader(AppHelper.HttpHeaders.Referrer);

      // If custom header not present, fallback to browser referrer header (not reliable hence the custom header).
      if (referrerUrl.IsNullOrEmpty()) {
        referrerUrl = Request?.UrlReferrer?.AbsolutePath;
      }
      // If referer(sic) not present, fallback to standard Origin header (note may not provide full url in some cases).
      if (referrerUrl.IsNullOrEmpty()) {
        referrerUrl = GetRequestHeader("Origin");
      }

      Uri.TryCreate(referrerUrl, UriKind.Absolute, out uri);

      AppHelper.SetRequestItem(AppHelper.RequestItemKey.ReferrerUri, uri);

      return uri;
    }
    public string ReferrerAbsolutePath => GetReferrerUri()?.AbsolutePath;

    // Form
    public string GetFormValue(string name) => Request?.Form?[name];
    public IReadOnlyCollection<string> GetFormKeys() => Request?.Form?.AllKeys?.ToList() ?? (IReadOnlyCollection<string>)Array.Empty<string>();
    public string RequestFormAsString => Request?.Form?.ToString();

    // Files
    public bool RequestFilesContains(string key) => Request?.Files?.AllKeys?.Contains(key, StringComparer.OrdinalIgnoreCase) ?? false;
    public IUploadedFile GetRequestFile(string key) {
      var f = Request?.Files?[key];
      return f == null ? null : new FrameworkUploadedFile(f);
    }

    // Cookies
    public string GetRequestCookieValue(string name) => Request?.Cookies?[name]?.Value;
    public void AddResponseCookie(SessionCookieDescriptor cookieDescr) {
      var existing = Request?.Cookies?[cookieDescr.Name];
      var cookie = existing ?? new HttpCookie(cookieDescr.Name);
      cookie.Value = cookieDescr.Value;
      cookie.HttpOnly = cookieDescr.HttpOnly;
      cookie.Secure = cookieDescr.Secure;
      cookie.SameSite = cookieDescr.SameSite;     // SameSiteMode enum already exists in System.Web
      cookie.Path = cookieDescr.Path;
      cookie.Shareable = cookieDescr.Shareable;
      if (cookieDescr.Expires.HasValue) cookie.Expires = cookieDescr.Expires.Value;
      if (existing == null) Response?.Cookies.Add(cookie);
    }

    // Session
    public bool HasSession => RequestContext?.Session != null;
    public string SessionID => RequestContext?.Session?.SessionID;
    public object GetSessionValue(string key) => RequestContext?.Session?[key];
    public void SetSessionValue<T>(string key, T value) => RequestContext.Session[key] = value;

    // Per-request items (HttpContext.Items)
    public bool RequestItemExists(string key) => RequestContext?.Items?.Contains(key) ?? false;
    public object GetRequestItemValue(string key) => RequestContext?.Items?[key];
    public void SetRequestItemValue<T>(string key, T value) => RequestContext.Items[key] = value;
    public void RequestItemRemove(string key) => RequestContext?.Items?.Remove(key);

    // Misc
    public string ApplicationVirtualPath => HttpRuntime.AppDomainAppVirtualPath.EnsureEndsWith("/", StringExt.Ensure.IfNotBlank);
    public string ServerMapPath(string virtualPath) => RequestContext?.Server?.MapPath(virtualPath);
    public void ResponseWrite(string s) => Response?.Write(s);
    public void ResponseWriteLine(string s) => Response?.Write(s + "\n");
    public void SetContentType(string contentType) => Response.ContentType = contentType;
    public void SetStatusCode(int statusCode) => Response.StatusCode = statusCode;
    public void ClearResponseContent() => Response.ClearContent();
    public Stream RequestInputStream => Request.InputStream;

    public string GetRequestBody() {
      try {
        using (
          var reader = new StreamReader(
            Request?.InputStream, encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024, leaveOpen: true
          )
        ) {
          return reader.ReadToEnd();
        }
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(GetRequestBody))
          .Track();
        return null; // ignore errors
      }
    }

    private sealed class FrameworkUploadedFile : IUploadedFile {
      private readonly HttpPostedFile _f;
      public FrameworkUploadedFile(HttpPostedFile f) => _f = f;
      public string FileName => _f.FileName;
      public string ContentType => _f.ContentType;
      public long Length => _f.ContentLength;
      public Stream OpenReadStream() => _f.InputStream;
    }
  }

  public sealed class SessionCookieDescriptor {
    public string Name { get; set; } // init; }
    public string Value { get; set; }
    public bool HttpOnly { get; set; } // init; }
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;
    public bool Secure { get; set; } // init; }
    public DateTime? Expires { get; set; }
    public string Path { get; set; } // init; }
    public bool Shareable { get; set; } // init; }
  }
}

#endif
