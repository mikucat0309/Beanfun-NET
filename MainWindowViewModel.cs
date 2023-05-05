using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using Beanfun.Beanfun;
using Beanfun.Launcher;
using CommunityToolkit.Mvvm.Input;

namespace Beanfun;

public partial class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IBeanfunClient _client = new BeanfunClient();

    public MainWindowViewModel()
    {
        Task.Run(async () =>
        {
            // Currently only MapleStory is supported
            // Games = await _client.FetchGamesAsync();
            Games = (await _client.FetchGamesAsync()).FindAll(x => x.Code == "610074");

            await _client.InitAsync();
            QrCodeImgUrl = _client.GetQrCodeImgUrl();
        });
        _client.LoginEvent += async delegate
        {
            QrCodeVisibility = Visibility.Collapsed;
            GameAccountsVisibility = Visibility.Visible;
            GameAccounts = await _client.FetchGameAccountsAsync(SelectedGame!);
        };
        StartGameCommand = new RelayCommand(StartGame);
    }

    public string? QrCodeImgUrl { get; private set; }
    public Visibility GameAccountsVisibility { get; private set; } = Visibility.Collapsed;
    public Visibility QrCodeVisibility { get; private set; } = Visibility.Visible;
    public List<Game> Games { get; private set; } = new();
    public List<GameAccount> GameAccounts { get; private set; } = new();
    public Game? SelectedGame { get; set; }
    public GameAccount? SelectedGameAccount { get; set; }

    public string SelectedGameId { get; set; } = "";

    public string SelectedGamePassword { get; set; } = "";

    public RelayCommand StartGameCommand { get; private set; }

    public void GameAccounts_OnSelectionChanged()
    {
        if (SelectedGameAccount == null) return;
        SelectedGameId = SelectedGameAccount.Id;
        Task.Run(async () => { SelectedGamePassword = await _client.FetchOtpAsync(SelectedGameAccount); });
    }

    private void StartGame()
    {
        if (SelectedGameAccount == null || string.IsNullOrWhiteSpace(SelectedGamePassword)) return;
        BeanfunLauncher.StartGame(SelectedGameAccount.Game, SelectedGameAccount, SelectedGamePassword);
    }
}