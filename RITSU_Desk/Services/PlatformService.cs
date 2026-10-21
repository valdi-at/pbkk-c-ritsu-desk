using System.Diagnostics;
using Microsoft.Win32;
using RITSU_Desk.Infrastructure;

namespace RITSU_Desk.Services;

public interface IPlatformService
{
    string? AskSaveCsvPath(string suggestedFileName);

    void OpenInBrowser(Uri uri);
}

public sealed class PlatformService : IPlatformService
{
    public string? AskSaveCsvPath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".csv",
            Filter = "File CSV (*.csv)|*.csv",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void OpenInBrowser(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not open browser", ex);
        }
    }
}
