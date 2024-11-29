using System;
using Avalonia.Controls;
using ndsSharp.FileExplorer.Services;

namespace ndsSharp.FileExplorer.Framework;

public abstract class WindowBase<T> : Window where T : ViewModelBase, new()
{
    protected readonly T WindowModel;

    public WindowBase(bool initializeWindowModel = true)
    {
        WindowModel = ViewModelRegistry.New<T>();

        if (initializeWindowModel)
        {
            TaskService.Run(WindowModel.Initialize);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        ViewModelRegistry.Unregister<T>();
    }

    public void BringToTop()
    {
       Topmost = true;
       Topmost = false;
    }
}