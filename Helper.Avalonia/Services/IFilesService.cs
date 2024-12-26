using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Mobsub.Helper.Avalonia.Services;

public interface IFilesService
{
    public Task<IStorageFile?> OpenFileAsync();
    public Task<IStorageFile?> SaveFileAsync();
    public Task<IStorageFolder?> SelectFolderAsync();
}