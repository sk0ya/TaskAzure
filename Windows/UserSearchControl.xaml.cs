using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaskAzure.Models;
using UserControl = System.Windows.Controls.UserControl;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TaskAzure.Windows;

public partial class UserSearchControl : UserControl
{
    private readonly ObservableCollection<AdoUser> _filteredUsers = new();

    public static readonly DependencyProperty UsersProperty =
        DependencyProperty.Register(nameof(Users), typeof(IList<AdoUser>), typeof(UserSearchControl),
            new PropertyMetadata(null, OnUsersChanged));

    public static readonly DependencyProperty SelectedUserProperty =
        DependencyProperty.Register(nameof(SelectedUser), typeof(AdoUser), typeof(UserSearchControl),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedUserChanged));

    public IList<AdoUser>? Users
    {
        get => (IList<AdoUser>?)GetValue(UsersProperty);
        set => SetValue(UsersProperty, value);
    }

    public AdoUser? SelectedUser
    {
        get => (AdoUser?)GetValue(SelectedUserProperty);
        set => SetValue(SelectedUserProperty, value);
    }

    public UserSearchControl()
    {
        InitializeComponent();
        UserList.ItemsSource = _filteredUsers;
    }

    private static void OnUsersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserSearchControl ctrl)
            ctrl.RefreshFilter();
    }

    private static void OnSelectedUserChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserSearchControl ctrl)
            ctrl.UpdateSelectedDisplay();
    }

    private void UpdateSelectedDisplay()
    {
        if (SelectedUser is { } user)
        {
            SelectedLabel.Text = user.DisplayName;
            SelectedLabel.Visibility = Visibility.Visible;
            PlaceholderLabel.Visibility = Visibility.Collapsed;
        }
        else
        {
            SelectedLabel.Text = "";
            SelectedLabel.Visibility = Visibility.Collapsed;
            PlaceholderLabel.Visibility = Visibility.Visible;
        }
    }

    private void RefreshFilter()
    {
        var filter = SearchBox.Text?.Trim() ?? "";
        _filteredUsers.Clear();
        foreach (var u in Users ?? [])
        {
            if (string.IsNullOrEmpty(filter)
                || u.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || u.UniqueName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _filteredUsers.Add(u);
            }
        }

        NoResultsText.Visibility = _filteredUsers.Count == 0 && !string.IsNullOrEmpty(filter)
            ? Visibility.Visible
            : Visibility.Collapsed;
        UserList.Visibility = _filteredUsers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MainBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DropdownPopup.IsOpen)
        {
            DropdownPopup.IsOpen = false;
        }
        else
        {
            SearchBox.Text = "";
            RefreshFilter();
            DropdownPopup.IsOpen = true;
            SearchBox.Focus();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshFilter();
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (_filteredUsers.Count > 0)
                {
                    UserList.SelectedIndex = 0;
                    if (UserList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem item)
                        item.Focus();
                    e.Handled = true;
                }
                break;
            case Key.Escape:
                DropdownPopup.IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private void UserList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UserList.SelectedItem is not AdoUser user) return;

        SelectedUser = user;
        DropdownPopup.IsOpen = false;
    }

    private void UserList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DropdownPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void DropdownPopup_Closed(object sender, EventArgs e)
    {
        SearchBox.Text = "";
        UserList.SelectedItem = null;
    }
}
