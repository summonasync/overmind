namespace Overmind.Infrastructure.Agent;

public static class RouterTokenBudget
{
    public const int MaxPromptTokens = 1500;
    public const int MaxHotHeadChars = 1600;
    public const int CharsPerToken = 4;

    public static int EstimateTokens(string text) => (text.Length + CharsPerToken - 1) / CharsPerToken;
}
