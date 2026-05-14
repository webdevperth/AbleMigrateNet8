using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web {

  public partial class AppHelper {

    // Typesafe enum pattern, e.g. https://www.meziantou.net/smart-enums-type-safe-enums-in-dotnet.htm
    public class TypeSafeEnumInt<T> where T : TypeSafeEnumInt<T> {

      private readonly int _value;
      private readonly string _name;
      private static readonly Dictionary<int, T> _itemsById = new Dictionary<int, T>();
      private static readonly List<T> _items = new List<T>();

      public static T[] Values => _itemsById.Values.ToArray();

      protected TypeSafeEnumInt(int value, string name) {
        this._value = value;
        this._name = name;
        _items.Add(this as T);
        _itemsById[value] = this as T;
      }

      public int Value => _value;
      public string Name => _name;

      public static T GetValue(int value) {
        if (_itemsById.TryGetValue(value, out T instance)) return instance;
        throw new InvalidCastException($"Invalid value '{value}' for {typeof(T).Name}");
      }
      public static T GetValueOrDefault(int id, T defaultValue) {
        var instance = _items.Find(i => i.Value == id);
        return instance ?? defaultValue;
      }
      public static T GetValueOrDefault(string name, T defaultValue) {
        var instance = _items.Find(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return instance ?? defaultValue;
      }

      public static IReadOnlyList<T> GetValues() => _items;

      public override string ToString() => this._name;

      // Allows using (T)int to get the type from an int value, same as (enum.member)int.
      public static implicit operator TypeSafeEnumInt<T>(int value) => GetValue(value);

      // Allows using (T)int to get the type from an int value, same as (enum.member)name.
      public static implicit operator TypeSafeEnumInt<T>(string name) {
        var instance = _items.Find(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (instance == null) throw new InvalidCastException($"Invalid name '{name}' for {typeof(T).Name}");
        return instance;
      }

      // Allows using (int)class.member to get the default int value, same as (int)enum.member.
      public static explicit operator int(TypeSafeEnumInt<T> enumValue) => enumValue.Value;

    }
  }
}
