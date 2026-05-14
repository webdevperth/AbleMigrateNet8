using System;
using System.Collections.Generic;

namespace Integral.Web {
  /// <summary>
  /// Simple service locator for registering and retrieving services via factory functions.
  /// This pattern is used for services that need to be initialized in the web tier (HTTP modules)
  /// but accessed from lower layers without creating circular dependencies.
  ///
  /// Services registered here should be:
  /// - Application-lifetime singletons (not request-scoped)
  /// - Thread-safe
  /// - Stateless or with thread-safe state management
  ///
  /// Usage:
  /// - Registration (in HTTP modules): ServiceLocator.Instance.Register&lt;ITelemetryService&gt;(() => telemetryService);
  /// - Retrieval (in services): var service = ServiceLocator.Instance.GetService&lt;ITelemetryService&gt;();
  /// </summary>
  public sealed class ServiceLocator {
    private static readonly Lazy<ServiceLocator> _instance = new Lazy<ServiceLocator>(() => new ServiceLocator());
    private readonly Dictionary<Type, Func<object>> _serviceFactories = new Dictionary<Type, Func<object>>();
    private readonly object _lock = new object();

    /// <summary>
    /// Private constructor to enforce singleton pattern
    /// </summary>
    private ServiceLocator() { }

    /// <summary>
    /// Gets the singleton instance of the ServiceLocator
    /// </summary>
    public static ServiceLocator Instance => _instance.Value;

    /// <summary>
    /// Register a service factory with the locator.
    /// Thread-safe. If a service of the same type is already registered, it will be replaced.
    /// </summary>
    /// <typeparam name="T">The type to register the service as (typically an interface or the concrete type)</typeparam>
    /// <param name="factory">The factory function that returns the service instance</param>
    public void Register<T>(Func<T> factory) where T : class {
      if (factory == null) {
        throw new ArgumentNullException(nameof(factory));
      }

      lock (_lock) {
        _serviceFactories[typeof(T)] = () => factory();
      }
    }

    /// <summary>
    /// Retrieve a service instance from the locator by invoking its factory.
    /// Thread-safe.
    /// </summary>
    /// <typeparam name="T">The type of service to retrieve</typeparam>
    /// <returns>The service instance returned by the factory, or null if not registered</returns>
    public T GetService<T>() where T : class {
      Func<object> factory;
      lock (_lock) {
        if (!_serviceFactories.TryGetValue(typeof(T), out factory)) {
          return null;
        }
      }

      // Invoke factory outside the lock to avoid potential deadlocks
      return factory() as T;
    }

    /// <summary>
    /// Retrieve a required service instance from the locator.
    /// Throws an exception if the service is not registered.
    /// Use this for services that are critical to application functionality.
    /// Thread-safe.
    /// </summary>
    /// <typeparam name="T">The type of service to retrieve</typeparam>
    /// <returns>The registered service instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not registered</exception>
    public T GetRequiredService<T>() where T : class {
      var service = GetService<T>();
      if (service == null) {
        throw new InvalidOperationException(
          $"Required service '{typeof(T).Name}' is not registered in the ServiceLocator. " +
          $"Ensure the service is registered in Global.asax.cs Application_Start.");
      }
      return service;
    }

    /// <summary>
    /// Check if a service of the specified type has been registered.
    /// Thread-safe.
    /// </summary>
    /// <typeparam name="T">The type to check</typeparam>
    /// <returns>True if a service of this type is registered, false otherwise</returns>
    public bool IsRegistered<T>() where T : class {
      lock (_lock) {
        return _serviceFactories.ContainsKey(typeof(T));
      }
    }

    /// <summary>
    /// Unregister a service of the specified type.
    /// Thread-safe.
    /// </summary>
    /// <typeparam name="T">The type to unregister</typeparam>
    /// <returns>True if the service was removed, false if it wasn't registered</returns>
    public bool Unregister<T>() where T : class {
      lock (_lock) {
        return _serviceFactories.Remove(typeof(T));
      }
    }

    /// <summary>
    /// Clear all registered services. Use with caution.
    /// Thread-safe.
    /// </summary>
    public void Clear() {
      lock (_lock) {
        _serviceFactories.Clear();
      }
    }

    /// <summary>
    /// Validates that all required services are registered.
    /// Call this at application startup to fail-fast if services are missing.
    /// Thread-safe.
    /// </summary>
    /// <param name="requiredServiceTypes">The types of services that must be registered</param>
    /// <exception cref="InvalidOperationException">Thrown when any required service is not registered</exception>
    public void ValidateRequiredServices(params Type[] requiredServiceTypes) {
      var missingServices = new List<string>();

      lock (_lock) {
        foreach (var serviceType in requiredServiceTypes) {
          if (!_serviceFactories.ContainsKey(serviceType)) {
            missingServices.Add(serviceType.Name);
          }
        }
      }

      if (missingServices.Count > 0) {
        throw new InvalidOperationException(
          $"The following required services are not registered in ServiceLocator: {string.Join(", ", missingServices)}. " +
          $"Register them in HTTP modules during initialization.");
      }
    }
  }
}
