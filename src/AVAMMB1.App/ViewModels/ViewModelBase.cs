using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AVAMMB1.App.ViewModels;

/// <summary>Base class for all view models.</summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Handles a key press. Returns true when handled.</summary>
    /// <param name="key">Key.</param>
    public virtual bool HandleKey(Key key) => false;
}
