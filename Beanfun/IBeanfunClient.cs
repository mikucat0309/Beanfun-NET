using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Beanfun.Beanfun;

public interface IBeanfunClient : IDisposable
{
    event EventHandler? LoginEvent;
    Task InitAsync();
    string GetQrCodeImgUrl();
    Task<List<GameAccount>> FetchGameAccountsAsync(Game game);
    Task<string> FetchOtpAsync(GameAccount account);
    Task<List<Game>> FetchGamesAsync();
}