using System;
using System.Windows.Threading;

namespace StoryboardGrid;
internal sealed class ExportProgress(Dispatcher dispatcher, Action<double> update) : IProgress<double>
{
    public void Report(double value) { _ = dispatcher.BeginInvoke(new Action(()=>update(value))); }
}
