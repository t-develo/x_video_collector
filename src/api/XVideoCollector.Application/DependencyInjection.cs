using Microsoft.Extensions.DependencyInjection;
using XVideoCollector.Application.UseCases;

namespace XVideoCollector.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterVideoUseCase>();
        services.AddScoped<GetVideoUseCase>();
        services.AddScoped<ListVideosUseCase>();
        services.AddScoped<UpdateVideoUseCase>();
        services.AddScoped<DeleteVideoUseCase>();
        services.AddScoped<DownloadVideoUseCase>();
        services.AddScoped<SearchVideosUseCase>();
        services.AddScoped<ManageTagsUseCase>();
        services.AddScoped<ManageCategoriesUseCase>();

        return services;
    }
}
