using System.IO;
using System.Windows.Media.Imaging;
using Elecctron.App.Services;

namespace Elecctron.App;

internal static class CandidatePhotoLoader
{
    public static async Task<BitmapImage?> LoadAsync(AppsScriptApiClient api, string? photoUrl)
    {
        var bytes = await api.GetCandidatePhotoAsync(photoUrl);
        if (bytes is null || bytes.Length == 0) return null;
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
