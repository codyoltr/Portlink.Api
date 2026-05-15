using System.Globalization;

namespace Portlink.Api.Modules.Chatbot.Settings;

public class ChatbotSettings
{
    public string GeminiApiKey { get; init; } = string.Empty;
    public string GeminiModel { get; init; } = "gemini-2.5-flash";
    public string GeminiBaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";
    public decimal GeminiTemperature { get; init; } = 0.4m;
    public int MaxMessageLength { get; init; } = 2000;
    public int MaxRecentMessages { get; init; } = 8;
    public int MaxRecentMessageLength { get; init; } = 1000;
    public int MaxContextLength { get; init; } = 300;

    public static ChatbotSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Chatbot");

        return new ChatbotSettings
        {
            GeminiApiKey = ReadString(configuration, section, "GeminiApiKey", "GEMINI_API_KEY"),
            GeminiModel = ReadString(configuration, section, "GeminiModel", "GEMINI_MODEL", "gemini-2.5-flash"),
            GeminiBaseUrl = ReadString(configuration, section, "GeminiBaseUrl", "GEMINI_BASE_URL", "https://generativelanguage.googleapis.com/v1beta"),
            GeminiTemperature = ReadDecimal(configuration, section, "GeminiTemperature", "GEMINI_TEMPERATURE", 0.4m),
            MaxMessageLength = ReadInt(configuration, section, "MaxMessageLength", "CHATBOT_MAX_MESSAGE_LENGTH", 2000),
            MaxRecentMessages = ReadInt(configuration, section, "MaxRecentMessages", "CHATBOT_MAX_RECENT_MESSAGES", 8),
            MaxRecentMessageLength = ReadInt(configuration, section, "MaxRecentMessageLength", "CHATBOT_MAX_RECENT_MESSAGE_LENGTH", 1000),
            MaxContextLength = ReadInt(configuration, section, "MaxContextLength", "CHATBOT_MAX_CONTEXT_LENGTH", 300)
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            throw new InvalidOperationException("Chatbot ayarlari eksik. Gemini API key yapilandirilmalidir.");
        }
    }

    private static string ReadString(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey, string defaultValue = "")
    {
        var value = ReadNullableString(configuration, section, sectionKey, envKey);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static string? ReadNullableString(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey)
    {
        var sectionValue = section[sectionKey];
        if (!string.IsNullOrWhiteSpace(sectionValue))
        {
            return sectionValue;
        }

        return configuration[envKey];
    }

    private static int ReadInt(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey, int defaultValue)
    {
        var value = ReadNullableString(configuration, section, sectionKey, envKey);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static decimal ReadDecimal(IConfiguration configuration, IConfigurationSection section, string sectionKey, string envKey, decimal defaultValue)
    {
        var value = ReadNullableString(configuration, section, sectionKey, envKey);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }
}
