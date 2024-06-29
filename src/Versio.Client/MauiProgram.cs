using Microsoft.Extensions.Logging;
using Versio.Shared;

namespace Versio.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddScoped<ISearchService, BM25Search>();
        var modelPath = "model.onnx";
        //builder.Services.AddSingleton<IEmbedderService, EmbedderService>((e) => new EmbedderService(modelPath));

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
