using System.ComponentModel;
using System.Xml.Serialization;

namespace Bonsai.Http;

/// <summary>An editable, XML-serializable HTTP header entry.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class HttpHeader
{
    [XmlAttribute] public string Name { get; set; } = string.Empty;
    [XmlAttribute] public string Value { get; set; } = string.Empty;
    public override string ToString() => Name;
}
