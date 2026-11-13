#if NETFRAMEWORK
using Bonsai.Design;
using System.Reactive.Disposables;
using System.Reactive.Linq;

[assembly: Bonsai.TypeVisualizer(typeof(Bonsai.Http.Design.HttpResponseVisualizer), Target = typeof(Bonsai.Http.HttpResponse))]
[assembly: Bonsai.TypeVisualizer(typeof(Bonsai.Http.Design.HttpResponseVisualizer), Target = typeof(Bonsai.Http.IHttpResponseResult))]

namespace Bonsai.Http.Design;

/// <summary>Registered Bonsai response visualizer using a native Dear ImGui window.</summary>
public sealed class HttpResponseVisualizer : DialogTypeVisualizer
{
    HttpImGuiWindow? window;
    HttpResponse? latest;
    SynchronizationContext? ui;
    readonly HttpResponsePanel panel = new();

    public override void Load(IServiceProvider provider)
    {
        ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Load the visualizer on the Bonsai UI thread.");
        window = new HttpImGuiWindow("HTTP Response");
    }

    public override void Show(object value) => Volatile.Write(ref latest, value is IHttpResponseResult result ? result.Response : (HttpResponse)value);

    public override IObservable<object> Visualize(IObservable<IObservable<object>> source, IServiceProvider provider) =>
        Observable.Create<object>(observer =>
        {
            var data = source.Merge().Subscribe(value => { Show(value); observer.OnNext(value); }, observer.OnError, observer.OnCompleted);
            var render = Observable.ObserveOn(Observable.Interval(TimeSpan.FromMilliseconds(20)), ui!).Subscribe(_ =>
            {
                panel.Response = Volatile.Read(ref latest);
                if (window?.Pump(panel.Draw) == false) observer.OnCompleted();
            }, observer.OnError);
            return new CompositeDisposable(data, render);
        });

    public override void Unload() { window?.Dispose(); window = null; latest = null; }
}
#endif
