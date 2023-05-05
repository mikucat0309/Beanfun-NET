using System.Text.Json.Serialization;
using Flurl;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable NotAccessedPositionalProperty.Global

namespace Beanfun.Beanfun;

public record GameResp(
    [property: JsonPropertyName("ServiceCode")]
    string Code,
    [property: JsonPropertyName("ServiceRegion")]
    string Region,
    [property: JsonPropertyName("ServiceSubtypeName")]
    string SubtypeName,
    [property: JsonPropertyName("ServiceFamilyName")]
    string FamilyName,
    [property: JsonPropertyName("ServiceWebsiteURL")]
    string WebsiteUrl,
    [property: JsonPropertyName("ServiceForumPageURL")]
    string? ForumPageUrl,
    [property: JsonPropertyName("ServiceRank")]
    int Rank,
    [property: JsonPropertyName("ServiceXLargeImageName")]
    string XLargeImageName,
    [property: JsonPropertyName("ServiceLargeImageName")]
    string LargeImageName,
    [property: JsonPropertyName("ServiceSmallImageName")]
    string SmallImageName,
    [property: JsonPropertyName("ServiceDownloadURL")]
    string DownloadUrl,
    [property: JsonPropertyName("IsHotGame")]
    bool IsHotGame,
    [property: JsonPropertyName("IsNewGame")]
    bool IsNewGame,
    [property: JsonPropertyName("ServiceStartMode")]
    int StartMode,
    [property: JsonPropertyName("ServiceName")]
    string Name
)
{
    private const string ImageUrlPrefix = "https://tw.images.beanfun.com/uploaded_images/beanfun_tw/game_zone/";

    public Game ToGame()
    {
        return new Game(
            Code,
            Region,
            Name,
            ImageUrlPrefix.AppendPathSegment(LargeImageName).ToUri(),
            ImageUrlPrefix.AppendPathSegment(SmallImageName).ToUri()
        );
    }
}

public record CheckLoginResp(
    [property: JsonPropertyName("ResultData")]
    string? Data,
    [property: JsonPropertyName("Result")] LoginStatus Code,
    [property: JsonPropertyName("ResultMessage")]
    string Message
);

public record FetchQrCodeDataResp(
    [property: JsonPropertyName("intResult")]
    int IntResult,
    [property: JsonPropertyName("strResult")]
    string StrResult,
    [property: JsonPropertyName("strEncryptData")]
    string EncryptData,
    [property: JsonPropertyName("strEncryptBCDOData")]
    string EncryptBcdoData
);