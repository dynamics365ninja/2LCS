using LCS.Theming;
using AutoMapper;
using CsvHelper;
using LCS.JsonObjects;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LCS.AssetLibrary;

public partial class AssetLibrarySearch : LCS.Theming.ThemedForm
{
    private HttpClientHelper _httpClientHelper;

    internal AssetLibrarySearch(HttpClientHelper httpClientHelper)
    {
        InitializeComponent();
        _httpClientHelper = httpClientHelper;
    }

    private void AssetLibrarySearch_Load(object sender, EventArgs e)
    {
        assetsCreatedAfter.Value = DateTime.Now.AddMonths(-6);
        assetsCreatedBefore.Value = DateTime.Now;
    }

    private async void search_Click(object sender, EventArgs e)
    {
        // Requests run in the background so the progress is visible.
        search.Enabled = false;
        UseWaitCursor = true;
        try
        {
            await SearchAndExportAsync();
        }
        finally
        {
            UseWaitCursor = false;
            search.Enabled = true;
        }
    }

    private async Task SearchAndExportAsync()
    {
        var assets = await Task.Run(() => _httpClientHelper.GetSharedAssetList(AssetFileType.NuGetPackage));
        // The window can be closed while LCS is queried.
        if (IsDisposed || assets == null) return;

        var assetVersionsForExportList = new List<AssetVersionForExport>();

        var config = new MapperConfiguration(
            cfg => cfg.CreateMap<AssetVersion, AssetVersionForExport>(), NullLoggerFactory.Instance);
        var mapper = config.CreateMapper();

        // linear with progress, only assets in date range
        assetExportProgress.Maximum = assets.Count;
        assetExportProgress.Value = 0;
        foreach (var asset in assets)
        {
            assetExportProgress.Value++;
            assetExportProgressNumberOfMax.Text = $"{assetExportProgress.Value} of {assetExportProgress.Maximum}";
            assetExportProgressAssetName.Text = asset.Name;
            var versionList = await Task.Run(() => _httpClientHelper.GetSharedAssetVersionList(asset.ParentAssetId));
            if (IsDisposed) return;
            var assetVersions = (versionList ?? new List<AssetVersion>())
                .Where(assetVersion => assetVersion.CreatedDate >= assetsCreatedAfter.Value
                                    && assetVersion.CreatedDate <= assetsCreatedBefore.Value);
            if (assetVersions.Any() == false)
            {
                break;
            }
            assetVersionsForExportList.AddRange(assetVersions
                .Select(assetVersion =>
                {
                    var fileDescription = assetVersion.FileDescription.Split('\n').Last();
                    assetVersion.ApplicationOrPlatformVersion = fileDescription;
                    return mapper.Map<AssetVersionForExport>(assetVersion);
                }));
        }

        assetExportProgress.Value = 0;
        assetExportProgressNumberOfMax.Text = string.Empty;
        assetExportProgressAssetName.Text = string.Empty;

        SaveFileDialog saveFile = new()
        {
            FileName = "D365FO NuGet Packages.csv",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };

        if (saveFile.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                using StreamWriter sw = new(saveFile.FileName, false, Encoding.Unicode);
                var csv = new CsvWriter(sw, CultureInfo.CurrentCulture);
                csv.WriteRecords(assetVersionsForExportList);
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show(this, ex.Message);
            }
        }
    }
}
