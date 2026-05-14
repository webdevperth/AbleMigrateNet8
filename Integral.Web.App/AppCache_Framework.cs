#if NETFRAMEWORK

using System;
using System.Web;
using System.Web.Caching;

namespace Integral.Web {

  public sealed class AppCache_Framework : IAppCache {

    public T Get<T>(string key) {
      return HttpRuntime.Cache[key] is T value ? value : default;
    }

    public void Set<T>(string key, T value, TimeSpan ttl) {
      HttpRuntime.Cache.Insert(
          key,
          value,
          null,
          DateTime.UtcNow.Add(ttl),
          Cache.NoSlidingExpiration
      );
    }

    public void Remove(string key) {
      HttpRuntime.Cache.Remove(key);
    }
  }
}

#endif
