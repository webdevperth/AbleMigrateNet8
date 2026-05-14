using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Extensions.Primitives;

public static class NameValueCollectionExtensions {

  public static Dictionary<string, StringValues> ToStringValuesDictionary(
      this NameValueCollection collection,
      StringComparer comparer = null) {

    if (collection == null) {
      throw new ArgumentNullException(nameof(collection));
    }

    comparer = comparer ?? StringComparer.OrdinalIgnoreCase;

    var dictionary = new Dictionary<string, StringValues>(comparer);

    foreach (string key in collection.AllKeys) {
      if (key == null) {
        continue;
      }

      string[] values = collection.GetValues(key);

      dictionary[key] = values == null
          ? StringValues.Empty
          : new StringValues(values);
    }

    return dictionary;
  }
}
