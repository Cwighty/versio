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
        builder.Services.AddScoped<ISearchService, VectorSearch>();
        builder.Services.AddKeyedScoped<ISearchService, BM25Search>(SearchAlgorithmOptions.BM25);
        builder.Services.AddKeyedScoped<ISearchService, VectorSearch>(SearchAlgorithmOptions.KNNVectorSimiliarity);
        var modelPath = "model.onnx";
        var vocabPath = "vocab.txt";
        builder.Services.AddSingleton<IEmbedderService, AllMiniLmEmbedder>((e) => new AllMiniLmEmbedder(modelPath, vocabPath));

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
