using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskAzure.Models;

namespace TaskAzure.ViewModels;

public enum VariableKind { User, Text }

/// <summary>テンプレート変数の入力モデル</summary>
public class VariableInput : INotifyPropertyChanged
{
    /// <summary>変数キー (例: "0:user", "1:text")</summary>
    public string Key { get; set; } = "";
    /// <summary>UI表示ラベル</summary>
    public string Label { get; set; } = "";
    public VariableKind Kind { get; set; }

    private string _textValue = "";
    public string TextValue
    {
        get => _textValue;
        set { _textValue = value; OnPropertyChanged(); ValueChanged?.Invoke(); }
    }

    private AdoUser? _selectedUser;
    public AdoUser? SelectedUser
    {
        get => _selectedUser;
        set { _selectedUser = value; OnPropertyChanged(); ValueChanged?.Invoke(); }
    }

    private IList<AdoUser> _users = [];
    public IList<AdoUser> Users
    {
        get => _users;
        set { _users = value; OnPropertyChanged(); }
    }

    /// <summary>テンプレート解決時に使われる実値</summary>
    public string ResolvedValue => Kind switch
    {
        VariableKind.User => SelectedUser?.UniqueName ?? "",
        _ => TextValue,
    };

    public event Action? ValueChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
