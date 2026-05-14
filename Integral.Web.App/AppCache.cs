using System;

namespace Integral.Web {

  public static class AppCache {

    private static IAppCache Impl => ServiceLocator.Instance.GetRequiredService<IAppCache>();

    public static T Get<T>(string key) => Impl.Get<T>(key);

    public static void Set<T>(string key, T value, TimeSpan ttl) => Impl.Set(key, value, ttl);

    public static void Remove(string key) => Impl.Remove(key);
  }
}
