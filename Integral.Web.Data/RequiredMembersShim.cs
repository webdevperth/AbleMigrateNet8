// RequiredMembersShim.cs
// Shim attributes required for C# 11+ `required` members on .NET Framework 4.8

namespace System.Runtime.CompilerServices {
  /// <summary>
  /// Indicates that a type has one or more required members.
  /// The compiler uses this for the `required` keyword.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
  internal sealed class RequiredMemberAttribute : Attribute {
    public RequiredMemberAttribute() { }
  }

  /// <summary>
  /// Indicates that this member is required.
  /// Emitted by the compiler for members declared with `required`.
  /// </summary>
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
  internal sealed class RequiredAttribute : Attribute {
    public RequiredAttribute() { }
  }

  /// <summary>
  /// Indicates that a constructor sets all required members.
  /// Used to suppress "required members not set" analysis for that ctor.
  /// </summary>
  [AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
  internal sealed class SetsRequiredMembersAttribute : Attribute {
    public SetsRequiredMembersAttribute() { }
  }

  // Needed for init-only setters (and sometimes records) on net48
  internal static class IsExternalInit { }

  // Needed by the compiler to mark use of certain features (including required members)
  [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
  internal sealed class CompilerFeatureRequiredAttribute : Attribute {
    public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;

    public string FeatureName { get; }

    // Used by the compiler for required members metadata
    public const string RequiredMembers = "RequiredMembers";
  }
}
