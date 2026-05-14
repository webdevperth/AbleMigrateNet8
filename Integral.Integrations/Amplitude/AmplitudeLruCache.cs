using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Integral.Integrations.Amplitude {

  /// <summary>
  /// Thread-safe LRU cache for tracking Amplitude user property changes per session.
  /// Allows us to check what session information has been sent to Amplitude already and only
  /// send through updated user properties if they are different to what we have sent before, which should
  /// drastically reduce the number of API calls we are making.
  /// Supports both size-based eviction (LRU), TTL (time since last update), and max age (time since creation).
  /// </summary>
  public class AmplitudeLruCache {

    // the desired size of the cache
    private readonly int _capacity;

    // how long between accesses before an entry in the cache is considered expired
    private readonly int _ttlSeconds;

    // how long since creation before an entry in the cache is considered expired
    private readonly int _maxAgeSeconds;

    private readonly object _lock = new object();


    private readonly LinkedList<string> _accessOrder;
    private readonly Dictionary<string, CacheEntry> _cache;

    public class CacheEntry {
      public string SessionId { get; set; }
      public string UserPropertiesHash { get; set; }
      public DateTimeOffset CreatedAt { get; set; }
      public DateTimeOffset LastUpdated { get; set; }
    }

    public AmplitudeLruCache(int capacity = 1024, int ttlSeconds = 3600, int maxAgeSeconds = 3600) {
      _capacity = capacity;
      _ttlSeconds = ttlSeconds;
      _maxAgeSeconds = maxAgeSeconds;
      _accessOrder = new LinkedList<string>();
      _cache = new Dictionary<string, CacheEntry>();
    }

    /// <summary>
    /// Check if user properties have changed for this session.
    /// Returns true if properties are different or not in cache (should send to API).
    /// Checks both TTL (time since last update) and max age (time since creation).
    /// </summary>
    public bool HasChanged(string sessionId, List<AmplitudeUserProperty> userProperties) {
      if (string.IsNullOrEmpty(sessionId)) return true;

      var hash = ComputeHash(userProperties);
      var now = DateTimeOffset.UtcNow;

      lock (_lock) {
        if (_cache.TryGetValue(sessionId, out CacheEntry entry)) {
          // Check if entry has exceeded max age (time since creation)
          var age = (now - entry.CreatedAt).TotalSeconds;
          if (age > _maxAgeSeconds) {
            // Entry too old, remove it and treat as new
            _accessOrder.Remove(sessionId);
            _cache.Remove(sessionId);
            Add(sessionId, hash);
            return true;
          }

          // Check if entry has exceeded TTL (time since last update)
          var timeSinceUpdate = (now - entry.LastUpdated).TotalSeconds;
          if (timeSinceUpdate > _ttlSeconds) {
            // Entry stale, remove it and treat as new
            _accessOrder.Remove(sessionId);
            _cache.Remove(sessionId);
            Add(sessionId, hash);
            return true;
          }

          // Move to end (most recently used)
          _accessOrder.Remove(sessionId);
          _accessOrder.AddLast(sessionId);

          // Check if hash has changed
          if (entry.UserPropertiesHash == hash) {
            return false; // No change, don't send
          }

          // Properties changed, update cache
          entry.UserPropertiesHash = hash;
          entry.LastUpdated = now;
          return true;
        }

        // Not in cache, add it
        Add(sessionId, hash);
        return true;
      }
    }

    private void Add(string sessionId, string hash) {
      // Must be called within lock

      var now = DateTimeOffset.UtcNow;

      // Remove all TTL-expired entries
      var expiredKeys = new List<string>();
      foreach (var kvp in _cache) {
        var timeSinceUpdate = (now - kvp.Value.LastUpdated).TotalSeconds;
        if (timeSinceUpdate > _ttlSeconds) {
          expiredKeys.Add(kvp.Key);
        }
      }

      foreach (var key in expiredKeys) {
        _cache.Remove(key);
        _accessOrder.Remove(key);
      }

      // Check capacity and evict LRU if needed
      if (_cache.Count >= _capacity) {
        var lruKey = _accessOrder.First.Value;
        _accessOrder.RemoveFirst();
        _cache.Remove(lruKey);
      }

      // Add new entry
      _cache[sessionId] = new CacheEntry {
        SessionId = sessionId,
        UserPropertiesHash = hash,
        CreatedAt = now,
        LastUpdated = now
      };
      _accessOrder.AddLast(sessionId);
    }

    /// <summary>
    /// Compute a hash of user properties for change detection.
    /// Includes property name, value, AND operation in hash since operation matters for Amplitude API.
    ///
    /// Lets be real, this isn't really a hash, but it's good enough for what we need
    /// </summary>
    private string ComputeHash(List<AmplitudeUserProperty> userProperties) {
      if (userProperties == null || userProperties.Count == 0) {
        return string.Empty;
      }

      // Build hash string - order by Name for consistent hashing
      var hashBuilder = new StringBuilder();
      foreach (var prop in userProperties.OrderBy(p => p.Name)) {
        hashBuilder.Append(prop.Name);
        hashBuilder.Append("=");
        hashBuilder.Append(prop.Value?.ToString() ?? "null");
        hashBuilder.Append(":");
        hashBuilder.Append(prop.Operation.ToString());
        hashBuilder.Append(";");
      }

      return hashBuilder.ToString();
    }

    /// <summary>
    /// Get cache statistics for monitoring/debugging
    /// </summary>
    public (int Count, int Capacity) GetStats() {
      lock (_lock) {
        return (_cache.Count, _capacity);
      }
    }

    /// <summary>
    /// Clear the cache (useful for testing or maintenance)
    /// </summary>
    public void Clear() {
      lock (_lock) {
        _cache.Clear();
        _accessOrder.Clear();
      }
    }
  }
}
