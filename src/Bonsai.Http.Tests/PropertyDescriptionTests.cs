using System.ComponentModel;
using System.Reflection;
using Bonsai.Http.Server;
using Xunit;

namespace Bonsai.Http.Tests;

public class PropertyDescriptionTests
{
    [Fact]
    public void PublicPropertiesExposeDescriptionsToThePropertyGrid()
    {
        var assemblies = new List<Assembly> { typeof(HttpRequest).Assembly, typeof(HttpServerResponse).Assembly };
#if NETFRAMEWORK
        assemblies.Add(typeof(Design.HttpRequestEditor).Assembly);
#endif
        foreach (var assembly in assemblies)
        foreach (var type in assembly.GetExportedTypes())
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(type))
        {
            // Framework base-class properties have their own metadata contracts.
            if (property.ComponentType.Assembly != assembly) continue;
            Assert.True(!string.IsNullOrWhiteSpace(property.Description), $"{type.FullName}.{property.Name} has no property-grid description.");
        }
    }
}
