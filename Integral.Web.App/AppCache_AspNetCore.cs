#if NET10_0_OR_GREATER

/*
Register it in .NET 10:
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAppCache, MemoryAppCache>();
*/

using System;
using Microsoft.Extensions.Caching.Memory;

namespace Integral.Web.AppCache {

  public sealed class AppCache_AspNetCore : IAppCache
  {
    private readonly IMemoryCache _cache;

    public MemoryAppCache(IMemoryCache cache)
    {
      _cache = cache;
    }

    public T? Get<T>(string key)
    {
      return _cache.TryGetValue(key, out T? value) ? value : default;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
      _cache.Set(key, value, new MemoryCacheEntryOptions
      {
        AbsoluteExpirationRelativeToNow = ttl
      });
    }

    public void Remove(string key)
    {
      _cache.Remove(key);
    }
  }
}

#endif
