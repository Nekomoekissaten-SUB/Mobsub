using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Mobsub.RainCurtain.Services;

public interface IFilesService
{
    public Task<IStorageFile?> OpenFileAsync();
    public Task<IStorageFile?> SaveFileAsync();
}