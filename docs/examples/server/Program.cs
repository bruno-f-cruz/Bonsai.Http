using Bonsai;
using Bonsai.Expressions;
using Bonsai.Http.Server;
using System.Reactive.Linq;
using System.Xml;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--export-image")
            {
#if WINDOWS
                Bonsai.Editor.WorkflowExporter.ExportImage(args[2], args[1]);
                return 0;
#else
                throw new PlatformNotSupportedException("Workflow image export requires Windows.");
#endif
            }

            if (args.Length != 1 && !(args.Length == 3 && args[1] == "--url"))
                throw new ArgumentException("Pass a server workflow path with optional --url <origin>, or --export-image <svg> <workflow>.");
            using var reader = XmlReader.Create(args[0]);
            var workflow = (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
            var create = workflow.Workflow.Select(node => (node.Value as CombinatorBuilder)?.Combinator).OfType<CreateHttpServer>().Single();
            if (args.Length == 3) create.Url = args[2];
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ConsoleCancelEventHandler cancel = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                completion.TrySetResult(true);
            };
            Console.CancelKeyPress += cancel;
            try
            {
                using var subscription = workflow.Workflow.BuildObservable<HttpServerRequestContext>().Subscribe(
                    request => Console.WriteLine($"Replied to {request.Method} {request.Uri}"),
                    error => completion.TrySetException(error),
                    () => completion.TrySetResult(true));
                Console.WriteLine($"Starting server workflow at {create.Url}. Press Ctrl+C to stop.");
                completion.Task.GetAwaiter().GetResult();
            }
            finally { Console.CancelKeyPress -= cancel; }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
