using System.Collections.ObjectModel;
using System.Windows;

namespace Launcher;

public partial class AutoLoginWindow : Window
{
    private readonly string _accountsPath;
    private readonly ObservableCollection<AutoLoginAccount> _accounts = [];

    public AutoLoginAccount? SelectedAccount { get; private set; }

    public AutoLoginWindow(string accountsPath)
    {
        _accountsPath = accountsPath;
        InitializeComponent();
        AccountsList.ItemsSource = _accounts;
        _ = LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        var accounts = await AutoLoginAccountStore.LoadAsync(_accountsPath);
        foreach (var account in accounts)
        {
            _accounts.Add(account);
        }

        if (_accounts.Count > 0)
        {
            AccountsList.SelectedIndex = 0;
        }
    }

    private void AccountsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AccountsList.SelectedItem is not AutoLoginAccount account)
        {
            return;
        }

        LoginBox.Text = account.Login;
        PasswordBox.Password = account.Password;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var login = LoginBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        var existing = _accounts.FirstOrDefault(account =>
            string.Equals(account.Login, login, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = new AutoLoginAccount { Login = login };
            _accounts.Add(existing);
        }

        existing.Password = PasswordBox.Password;
        AccountsList.SelectedItem = existing;
        await AutoLoginAccountStore.SaveAsync(_accountsPath, _accounts);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (AccountsList.SelectedItem is not AutoLoginAccount account)
        {
            return;
        }

        _accounts.Remove(account);
        LoginBox.Clear();
        PasswordBox.Clear();
        await AutoLoginAccountStore.SaveAsync(_accountsPath, _accounts);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (AccountsList.SelectedItem is AutoLoginAccount selected)
        {
            SelectedAccount = selected;
        }
        else if (!string.IsNullOrWhiteSpace(LoginBox.Text))
        {
            SelectedAccount = new AutoLoginAccount
            {
                Login = LoginBox.Text.Trim(),
                Password = PasswordBox.Password
            };
            await AutoLoginAccountStore.SaveAsync(_accountsPath, _accounts.Append(SelectedAccount));
        }

        DialogResult = SelectedAccount is not null;
    }
}
