using CommunityToolkit.Mvvm.ComponentModel;

namespace client.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private string _password;
    }
}
