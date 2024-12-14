using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Mobsub.RainCurtain.Services;

public class FilesService(Window target) : IFilesService
{
    public async Task<IStorageFile?> OpenFileAsync()
    {
        var files = await target.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            // Title = "Open Text File",
            AllowMultiple = false
        });

        return files.Count >= 1 ? files[0] : null;
    }

    public Task<IStorageFile?> SaveFileAsync()
    {
        throw new System.NotImplementedException();
    }
}