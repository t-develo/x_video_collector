using Microsoft.Extensions.DependencyInjection;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Application.UseCases;

namespace XVideoCollector.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRegisterVideoUseCase, RegisterVideoUseCase>();
        services.AddScoped<IGetVideoUseCase, GetVideoUseCase>();
        services.AddScoped<IListVideosUseCase, ListVideosUseCase>();
        services.AddScoped<IUpdateVideoUseCase, UpdateVideoUseCase>();
        services.AddScoped<IDeleteVideoUseCase, DeleteVideoUseCase>();
        services.AddScoped<IDownloadVideoUseCase, DownloadVideoUseCase>();
        services.AddScoped<ISearchVideosUseCase, SearchVideosUseCase>();
        services.AddScoped<IManageTagsUseCase, ManageTagsUseCase>();
        services.AddScoped<IManageCategoriesUseCase, ManageCategoriesUseCase>();
        services.AddScoped<IRetryVideoDownloadUseCase, RetryVideoDownloadUseCase>();

        return services;
    }
}
