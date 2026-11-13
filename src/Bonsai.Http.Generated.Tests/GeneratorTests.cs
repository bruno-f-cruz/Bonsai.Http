#if NET8_0
using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;

namespace Bonsai.Http.Generated.Tests;

public class GeneratorTests
{
    [Fact]
    public async Task ParallelGenerationsCanShareThePinnedTool()
    {
        var root = Path.Combine(Path.GetTempPath(), "bonsai-http-parallel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(AppContext.BaseDirectory, "fixtures", "openapi.json");
            var results = await Task.WhenAll(Generate(input, Path.Combine(root, "one")), Generate(input, Path.Combine(root, "two")));
            Assert.All(results, result => Assert.True(result.Exit == 0, result.Error));
        }
        finally { Directory.Delete(root, true); }
    }
    static async Task<(int Exit, string Error)> Generate(string input, string output)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { GeneratorLocation.Path, input, output, "ExampleApi" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await stdout;
        return (process.ExitCode, await stderr);
    }

    [Fact]
    public async Task RegenerationIsDeterministicAndRemovesOnlyOwnedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "bonsai-http-generator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "api.json");
            var output = Path.Combine(root, "output");
            var document = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "openapi.json")))!;
            await File.WriteAllTextAsync(input, document.ToJsonString());
            var first = await Generate(input, output);
            Assert.True(first.Exit == 0, first.Error);
            var before = await File.ReadAllTextAsync(Path.Combine(output, "GetAnimal.Generated.cs"));
            var second = await Generate(input, output);
            Assert.True(second.Exit == 0, second.Error);
            Assert.Equal(before, await File.ReadAllTextAsync(Path.Combine(output, "GetAnimal.Generated.cs")));
            await File.WriteAllTextAsync(Path.Combine(output, "notes.txt"), "keep");
            document["paths"]!["/animals/{animalId}"]!.AsObject().Remove("post");
            await File.WriteAllTextAsync(input, document.ToJsonString());
            var third = await Generate(input, output);
            Assert.True(third.Exit == 0, third.Error);
            Assert.False(File.Exists(Path.Combine(output, "UpdateAnimal.Generated.cs")));
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(output, "notes.txt")));
            document["paths"]!["/animals/{animalId}"]!["get"]!["parameters"]!.AsArray().Add(new JsonObject
            {
                ["name"] = "session", ["in"] = "cookie", ["schema"] = new JsonObject { ["type"] = "string" }
            });
            await File.WriteAllTextAsync(input, document.ToJsonString());
            var invalid = await Generate(input, output);
            Assert.NotEqual(0, invalid.Exit);
            Assert.Contains("cookie", invalid.Error);
            Assert.Equal(before, await File.ReadAllTextAsync(Path.Combine(output, "GetAnimal.Generated.cs")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task YamlQuotedScalarsKeepTheirTypes()
    {
        var root = Path.Combine(Path.GetTempPath(), "bonsai-http-yaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "api.yaml");
            var output = Path.Combine(root, "output");
            await File.WriteAllTextAsync(input, """
                openapi: 3.0.3
                info:
                  title: Quoted values
                  version: "1.0"
                paths:
                  /value:
                    get:
                      operationId: getValue
                      responses:
                        '200':
                          description: OK
                          content:
                            application/json:
                              schema:
                                $ref: '#/components/schemas/Payload'
                components:
                  schemas:
                    Payload:
                      type: object
                      properties:
                        code:
                          type: string
                          default: "001"
                        weight:
                          type: number
                          minimum: 0.5
                """);
            var result = await Generate(input, output);
            Assert.True(result.Exit == 0, result.Error);
            var schema = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output, "OpenApi.Models.schema.json")))!;
            Assert.Equal("001", schema["definitions"]!["Payload"]!["properties"]!["code"]!["default"]!.GetValue<string>());
            Assert.Equal(0.5, schema["definitions"]!["Payload"]!["properties"]!["weight"]!["minimum"]!.GetValue<double>());
        }
        finally { Directory.Delete(root, true); }
    }
}
#endif
