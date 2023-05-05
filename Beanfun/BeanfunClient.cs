using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AngleSharp.Html.Parser;
using Flurl.Http;

namespace Beanfun.Beanfun;

public partial class BeanfunClient : IBeanfunClient
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36";

    private const string MainHost = "https://tw.beanfun.com";
    private const string LoginHost = "https://tw.newlogin.beanfun.com";
    private readonly CancellationTokenSource _loginCts = new();
    private readonly CancellationTokenSource _pingCts = new();

    private readonly CookieSession _session = new(new FlurlClient().WithHeader("User-Agent", UserAgent));
    private Task _loginTask = null!;
    private Task _pingTask = null!;
    private string _qrcodeData = null!;
    private string _sessionKey = null!;
    private string? _webToken;

    ~BeanfunClient()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (!_loginCts.IsCancellationRequested) _loginCts.Cancel();
        if (!_pingCts.IsCancellationRequested) _pingCts.Cancel();
        if (_loginTask.Status == TaskStatus.Running) _loginTask.Wait();
        if (_pingTask.Status == TaskStatus.Running) _pingTask.Wait();
        _loginTask.Dispose();
        _pingTask.Dispose();
        _session.Dispose();
    }

    public event EventHandler? LoginEvent;

    public async Task InitAsync()
    {
        _sessionKey = await FetchSessionAsync();
        _qrcodeData = await FetchLoginCodeAsync();
        _loginTask = CheckLoginStatusLoopAsync(_loginCts.Token);
    }

    [GeneratedRegex("""skey=([0-9a-zA-Z]+)""")]
    private static partial Regex SessionKeyPattern();

    private async Task<string> FetchSessionAsync()
    {
        Debug.WriteLine("Fetch login session");
        var resp = await _session
            .Request(MainHost, "/beanfun_block/bflogin/default.aspx")
            .SetQueryParam("service", "999999_T0")
            .WithAutoRedirect(false)
            .GetAsync();
        if (resp.StatusCode is < 300 or >= 400)
            throw new HttpRequestException($"Unexpected status code received: {resp.StatusCode}");

        Debug.WriteLine(resp.Headers.FirstOrDefault("Location"));
        var match = SessionKeyPattern().Match(resp.Headers.FirstOrDefault("Location"));
        if (!match.Success) throw new HttpRequestException("Session not found");

        Debug.WriteLine("login session: " + match.Groups[1].Value);
        return match.Groups[1].Value;
    }

    private async Task<string> FetchLoginCodeAsync()
    {
        Debug.WriteLine("Fetch login code");
        var resp = await _session
            .Request(LoginHost, "/generic_handlers/get_qrcodeData.ashx")
            .SetQueryParam("skey", _sessionKey)
            .GetJsonAsync<FetchQrCodeDataResp>();
        Debug.WriteLine("login code: " + resp!.EncryptData);
        return resp.EncryptData;
    }

    public string GetQrCodeImgUrl()
    {
        return
            $"https://tw.newlogin.beanfun.com/qrhandler.ashx?u=https://beanfunstor.blob.core.windows.net/redirect/appCheck.html?url=beanfunapp://Q/gameLogin/gtw/{_qrcodeData}";
    }

    private async Task CheckLoginStatusLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var status = await CheckLoginStatusAsync();
            switch (status)
            {
                case LoginStatus.Expired:
                    _sessionKey = await FetchSessionAsync();
                    _qrcodeData = await FetchLoginCodeAsync();
                    break;
                case LoginStatus.Success:
                {
                    var authKey = await QrcodeLoginAsync();
                    await LoginCompleteAsync(authKey);
                    LoginEvent?.Invoke(this, EventArgs.Empty);
                    _pingTask = PingLoopAsync(_pingCts);
                    return;
                }
                case LoginStatus.Wait:
                default:
                    break;
            }

            await Task.Delay(2000, token);
        }
    }

    private async Task<LoginStatus> CheckLoginStatusAsync()
    {
        Debug.WriteLine("Check login status");
        var resp = await _session
            .Request(LoginHost, "/generic_handlers/CheckLoginStatus.ashx")
            .WithHeader("Referer", $"https://tw.newlogin.beanfun.com/login/qr_form.aspx?skey={_sessionKey}")
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                { "status", _qrcodeData }
            });
        var content = await resp.GetJsonAsync<CheckLoginResp>();
        Debug.WriteLine(content);
        return content!.Message switch
        {
            "Success" => LoginStatus.Success,
            "Token Expired" => LoginStatus.Expired,
            "Failed" => LoginStatus.Wait,
            _ => LoginStatus.Wait
        };
    }

    [GeneratedRegex("""RedirectPage\("","\.(.+)"\)""")]
    private static partial Regex RedirectPattern();

    [GeneratedRegex("""akey=([0-9a-zA-Z]+)""")]
    private static partial Regex AuthKeyPattern();

    private async Task<string> QrcodeLoginAsync()
    {
        Debug.WriteLine("Login");
        var resp = await _session
            .Request(LoginHost, "/login/qr_step2.aspx")
            .SetQueryParam("skey", _sessionKey)
            .WithHeader("Referer", $"https://tw.newlogin.beanfun.com/login/qr_form.aspx?skey={_sessionKey}")
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                { "status", _qrcodeData }
            });
        var content = await resp.GetStringAsync();
        var match = RedirectPattern().Match(content);
        if (!match.Success) throw new Exception("Redirect path not found");

        var path = match.Groups[1].Value;
        Debug.WriteLine($"redirect path: {path}");
        match = AuthKeyPattern().Match(path);
        if (!match.Success) throw new Exception("Auth key not found");

        var authKey = match.Groups[1].Value;
        Debug.WriteLine($"auth key: {authKey}");
        await _session.Request(LoginHost, "/login", path).GetAsync();
        return authKey;
    }

    private async Task LoginCompleteAsync(string authkey)
    {
        await _session
            .Request(MainHost, "/beanfun_block/bflogin/return.aspx")
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                { "SessionKey", _sessionKey },
                { "AuthKey", authkey },
                { "ServiceCode", "" },
                { "ServiceRegion", "" },
                { "ServiceAccountSN", "0" }
            });
        _webToken = _session.Cookies.First(x => x.Name == "bfWebToken").Value;
        Debug.WriteLine($"token: {_webToken}");
        await _session.Request(MainHost, "/").GetAsync();
        LoginEvent?.Invoke(this, EventArgs.Empty);
    }

    private async Task PingLoopAsync(CancellationTokenSource cts)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            await PingAsync();
            await Task.Delay(60000, cts.Token);
        }
    }

    private async Task PingAsync()
    {
        var resp = await _session
            .Request(MainHost, "/beanfun_block/generic_handlers/echo_token.ashx")
            .SetQueryParam("webtoken", 1)
            .GetAsync();
        var content = await resp.GetStringAsync();
        Debug.WriteLine(content);
    }

    private static string GetCurrentTime(int method)
    {
        var date = DateTime.Now;
        return method switch
        {
            1 => (date.Year - 1900).ToString() + (date.Month - 1) + date.ToString("ddHHmmssfff"),
            2 => date.Year.ToString() + (date.Month - 1) + date.ToString("ddHHmmssfff"),
            _ => date.ToString("yyyyMMddHHmmss.fff")
        };
    }

    public async Task<List<GameAccount>> FetchGameAccountsAsync(Game game)
    {
        var content = await _session
            .Request(MainHost, "/beanfun_block/auth.aspx")
            .SetQueryParam("channel", "game_zone")
            .SetQueryParam("page_and_query",
                $"game_start.aspx?service_code_and_region={game.Code}_{game.Region}")
            .SetQueryParam("web_token", _webToken!)
            .GetStringAsync();
        var html = new HtmlParser().ParseDocument(content);
        var list = html.QuerySelectorAll("ul#ulServiceAccountList > li > div")
            .Select(x => x.Attributes)
            .Select(x => new GameAccount(
                x["id"]!.Value,
                x["sn"]!.Value,
                HttpUtility.HtmlDecode(x["name"]!.Value),
                game
            ))
            .ToList();
        Debug.WriteLine($"account list: {string.Join('\n', list.Select(x => x.ToString()))}");
        return list;
    }

    [GeneratedRegex("""key=([0-9a-zA-Z-]+)""")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("""ServiceAccountCreateTime: "([\w :-]+)",""")]
    private static partial Regex CreateTimePattern();

    [GeneratedRegex("""var m_strSecretCode = '(.+)'""")]
    private static partial Regex SecretCodePattern();

    public async Task<string> FetchOtpAsync(GameAccount account)
    {
        var content = await _session
            .Request(MainHost, "/beanfun_block/game_zone/game_start_step2.aspx")
            .SetQueryParam("service_code", account.Game.Code)
            .SetQueryParam("service_region", account.Game.Region)
            .SetQueryParam("sotp", account.SerialNumber)
            .SetQueryParam("dt", GetCurrentTime(2))
            .GetStringAsync();

        var match = KeyPattern().Match(content);
        if (!match.Success) throw new Exception("Key not found");
        var key = match.Groups[1].Value;
        Debug.WriteLine($"key: {key}");

        match = CreateTimePattern().Match(content);
        if (!match.Success) throw new Exception("Account create time not found");
        var createTime = match.Groups[1].Value;
        Debug.WriteLine($"account create time: {createTime}");

        content = await _session
            .Request(LoginHost, "/generic_handlers/get_cookies.ashx")
            .GetStringAsync();
        match = SecretCodePattern().Match(content);
        if (!match.Success) throw new Exception("Secret code not found");
        var secretCode = match.Groups[1].Value;
        Debug.WriteLine($"secret code: {secretCode}");

        content = await _session
            .Request(MainHost, "/beanfun_block/generic_handlers/get_webstart_otp.ashx")
            .SetQueryParam("SN", key)
            .SetQueryParam("WebToken", _webToken!)
            .SetQueryParam("SecretCode", secretCode)
            .SetQueryParam("ppppp", "1F552AEAFF976018F942B13690C990F60ED01510DDF89165F1658CCE7BC21DBA")
            .SetQueryParam("ServiceCode", account.Game.Code)
            .SetQueryParam("ServiceRegion", account.Game.Region)
            .SetQueryParam("ServiceAccount", account.Id)
            .SetQueryParam("CreateTime", createTime)
            .SetQueryParam("d", Environment.TickCount)
            .GetStringAsync();
        var l = content.Split(';');
        if (l.Length != 2) throw new Exception("OTP unknown error");
        if (l[0] != "1") throw new Exception($"OTP error: {l[1]}");
        var decryptKey = l[1][..8];
        var cipherHex = l[1][8..];
        var otp = DesDecryptOtp(cipherHex, decryptKey);
        Debug.WriteLine($"OTP: {otp}");
        return otp;
    }

    private static string DesDecryptOtp(string cipherHex, string keyStr)
    {
        var cipher = new byte[cipherHex.Length / 2];
        for (var i = 0; i < cipherHex.Length; i += 2) cipher[i / 2] = Convert.ToByte(cipherHex.Substring(i, 2), 16);

        var key = Encoding.ASCII.GetBytes(keyStr);
        var des = DES.Create();
        des.Key = key;
        var plain = des.DecryptEcb(cipher, PaddingMode.None);
        return Encoding.ASCII.GetString(plain);
    }

    [GeneratedRegex("""ServiceList = {"Rows":(\[.*\])}""")]
    private static partial Regex GameListJsonPattern();

    public async Task<List<Game>> FetchGamesAsync()
    {
        Debug.WriteLine("fetch game list");
        var content = await _session.Request(MainHost, "/game_zone/").GetStringAsync();
        var match = GameListJsonPattern().Match(content);
        if (!match.Success) throw new Exception("Game list not found");
        var list = JsonSerializer.Deserialize<List<GameResp>>(match.Groups[1].Value)!
            .Select(x => x.ToGame())
            .OrderBy(x => x.Name)
            .ToList();
        return list;
    }
}

public enum LoginStatus
{
    Wait,
    Success,
    Expired
}

public record GameAccount(
    string Id,
    string SerialNumber,
    string Name,
    Game Game
);

public record Game(
    string Code,
    string Region,
    string Name,
    Uri LargeImageUri,
    Uri SmallImageUri
);