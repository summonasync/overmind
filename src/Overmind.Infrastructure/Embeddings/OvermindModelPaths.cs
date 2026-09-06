namespace Overmind.Infrastructure.Embeddings;

public static class OvermindModelPaths
{
    public const string ModelsDirectoryVariable = "OVERMIND_MODELS_DIR";

    public static string RootDirectory =>
        Environment.GetEnvironmentVariable(ModelsDirectoryVariable)
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".overmind",
            "models");

    public static string ModelDirectory(string modelName) => Path.Combine(RootDirectory, modelName);
}
