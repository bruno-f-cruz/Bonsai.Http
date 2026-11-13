using System.ComponentModel;

namespace Bonsai.Http.Server;

internal sealed class ResponseEncodingConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
        new(new[] { string.Empty, "utf-8", "utf-16", "utf-32", "us-ascii", "iso-8859-1" });
}
