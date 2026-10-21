using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RITSU_Desk.ViewModels;

namespace RITSU_Desk.Views;

public partial class LoginView : UserControl
{
    private LoginViewModel? _vm;

    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as LoginViewModel);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Attach(DataContext as LoginViewModel);
        Keyboard.Focus(string.IsNullOrEmpty(EmailBox.Text) ? EmailBox : PasswordBox);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PasswordBox.Clear();
        Attach(null);
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.Password = PasswordBox.Password;
    }

    private void Attach(LoginViewModel? vm)
    {
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm is not null)
        {
            _vm.PasswordCleared -= OnPasswordCleared;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }
        _vm = vm;
        if (_vm is not null)
        {
            _vm.PasswordCleared += OnPasswordCleared;
            _vm.PropertyChanged += OnVmPropertyChanged;
            UpdateStatusDot();
        }
    }

    private void OnPasswordCleared(object? sender, EventArgs e) => PasswordBox.Clear();

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.ServerStatus)) UpdateStatusDot();
    }

    private void UpdateStatusDot()
    {
        var key = _vm?.ServerStatus switch
        {
            ServerStatus.Online => "Badge.Success.Bg",
            ServerStatus.Offline => "Badge.Danger.Bg",
            ServerStatus.Incompatible => "Badge.Warning.Bg",
            _ => "Text.Muted",
        };
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, key);
    }
}
