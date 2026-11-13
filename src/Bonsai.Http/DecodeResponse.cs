using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonsai.Expressions;

namespace Bonsai.Http;

/// <summary>Specifies the output representation of a decoded HTTP response body.</summary>
public enum ResponseFormat
{
    Text,
    Json
}

/// <summary>Decodes HTTP response bodies into text or schema-independent JSON values.</summary>
[DefaultProperty(nameof(Format))]
[WorkflowElementCategory(ElementCategory.Transform)]
[Description("Decodes HTTP response bodies into strings or JSON values. The selected format determines the workflow output type.")]
public sealed class DecodeResponse : SingleArgumentExpressionBuilder
{
    [Description("The response body format: Text outputs strings; Json outputs JsonNode values. Selected when the workflow is built; restart after changing it.")]
    [RefreshProperties(RefreshProperties.All)]
    public ResponseFormat Format { get; set; }

    public override Expression Build(IEnumerable<Expression> arguments)
    {
        var methodName = Format switch
        {
            ResponseFormat.Text => nameof(DecodeText),
            ResponseFormat.Json => nameof(DecodeJson),
            _ => throw new InvalidOperationException($"Unsupported response format: {Format}.")
        };
        var method = typeof(DecodeResponse).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
        return Expression.Call(method, arguments.Single());
    }

    static IObservable<string> DecodeText(IObservable<HttpResponse> source) =>
        source.Select(response => response.ReadText());

    static IObservable<JsonNode?> DecodeJson(IObservable<HttpResponse> source) =>
        source.Select(response =>
        {
            try { return JsonNode.Parse(response.ReadText()); }
            catch (JsonException error) { throw new HttpResponseParseException(response, error); }
        });
}
