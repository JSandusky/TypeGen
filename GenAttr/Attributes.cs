using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// This stuff is so repetitive that it screams for C-style macros. So much stupid.
/// </summary>

[Flags]
public enum Traits
{
    File = 1,               // Value needs to written to disck
    State = 1 << 1,         // Value needs to be saved into state-saves
    Net = 1 << 2,           // Value needs to be sent across the network
    ReadOnly = 1 << 3,      // Value can only be viewed
    Computed = 1 << 4,      // Means we need to call whatever generator's "PostSerialize" functions
    Interpolate = 1 << 5,   // Value needs to be interpolated (network)
    Default = File | State | Net,
}

namespace GenAttr
{
    [AttributeUsage(AttributeTargets.All)]
    public class TraitsAttribute : Attribute
    {
        public Traits traits;
        public TraitsAttribute(Traits traits) { this.traits = traits; }
    }

    /// <summary>
    /// Use to mark extra guiding information.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class TagAttribute : Attribute
    {
        public string tag;
        public TagAttribute(string tag) {  this.tag = tag; }
    }

    /// <summary>
    /// Use to mark a class as being force to be a struct
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AsStructAttribute : Attribute
    {
    }

    /// <summary>
    /// Use to indicate a name replacement
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Property)]
    public class NativeTypeAttribute : Attribute
    {
        public string name;
        public NativeTypeAttribute(string name) { this.name = name; }
    }

    /// <summary>
    /// Use to mark an array type as fixed length.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct | AttributeTargets.Class)]
    public class FixedLengthAttribute : Attribute
    {
        public int length;
        public FixedLengthAttribute(int length) { this.length = length; }
    }

    /// <summary>
    /// Use to specify the template-container to use
    /// ie. `FixedArray<MyType, 32>`
    /// If also FixedLength marked then fixed-length will be used for 2nd template parameter
    /// Container is expected to have a size() member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ContainerAttribute : Attribute
    {
        public string language = "";
        public string name;
        public ContainerAttribute(string name, string language = "") { this.name = name; this.language = language; }
    }

    /// <summary>
    /// Specifies the kind of pointer to use (default is std::unique_ptr)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class PointerAttribute : Attribute
    {
        public string name;
        public PointerAttribute(string name) { this.name = name; }
    }

    /// <summary>
    /// For GUI generation tasks, clusters things into groups
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class GroupAttribute : Attribute
    {
        public string name;
        public GroupAttribute(string name) { this.name = name; }
    }

    /// <summary>
    /// Give a field an integral ID, for hypothtical future-proof serializers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class IDAttribute : Attribute
    {
        public int id;
        public IDAttribute(int id) { this.id = id; }
    }

    /// <summary>
    /// Don't remember what this was supposed to do?
    /// 
    /// Did I mean expando-groups or something in GUI generation?
    ///     Rect (0 5 10 50)
    ///         x: 0
    ///         y: 5
    ///         width: 10
    ///         height: 50
    ///     
    /// Or the opposite:
    ///     Rect x: 0
    ///     Rect y: 5
    ///     Rect width: 10
    ///     Rect height: 50
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DecomposeUI : Attribute
    {
        public DecomposeUI() {  }
    }

    /// <summary>
    /// Tag a #include
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class HeaderAttribute : Attribute
    {
        public string name;
        public HeaderAttribute(string name) { this.name = name; }
    }

    /// <summary>
    /// Tag a #include
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct | AttributeTargets.Property)]
    public class NoteAttribute : Attribute
    {
        public string comment;
        public NoteAttribute(string comment) { this.comment = comment; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BitflagsAttribute : Attribute
    {
        public string enumSource;
        public BitflagsAttribute(string enumSource) { this.enumSource = enumSource; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class GetterSetterAttribute : Attribute
    { 
        public string getter;
        public string setter;

        public GetterSetterAttribute(string getter, string setter) {  this.getter = getter; this.setter = setter; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ComputedValueAttribute : Attribute
    {
        public ComputedValueAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
    public class NoProcessAttribute : Attribute {  }
}
