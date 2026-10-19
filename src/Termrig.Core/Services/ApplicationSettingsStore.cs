namespace Termrig.Core.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Termrig.Core.Models;

    /// <summary>Persists application preferences separately from terminal profiles.</summary>
    public sealed class ApplicationSettingsStore
    {
        private readonly string _filePath;

        public ApplicationSettingsStore(string? directoryPath = null)
        {
            _filePath = Path.Combine(directoryPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Constants.ApplicationDirectoryName), "settings.json");
        }

        public async Task<ApplicationSettings> LoadAsync()
        {
            if (!File.Exists(_filePath)) return new ApplicationSettings();
            return JsonSerializer.Deserialize<ApplicationSettings>(await File.ReadAllTextAsync(_filePath))
                ?? new ApplicationSettings();
        }

        public async Task SaveAsync(ApplicationSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            string temporaryPath = _filePath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(settings,
                    new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, _filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
