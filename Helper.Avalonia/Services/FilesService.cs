using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Mobsub.Helper.Avalonia.Services;

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
    
    public async Task<IStorageFolder?> SelectFolderAsync()
    {
        var dirs = await target.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            AllowMultiple = false
        });
        
        return dirs.Count >= 1 ? dirs[0] : null;
    }
}