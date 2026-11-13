using System.ComponentModel;
using System.Xml.Serialization;

namespace Bonsai.Http;

/// <summary>An editable, XML-serializable HTTP header entry.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class HttpHeader
{
    [Description("The HTTP header name, such as Accept or Authorization.")]
    [XmlAttribute] public string Name { get; set; } = string.Empty;
    [Description("The HTTP header value.")]
    [XmlAttribute] public string Value { get; set; } = string.Empty;
    public override string ToString() => Name;
}
