namespace TodoistShared.Helpers;

public static class EnvironmentHelpers
{
    /// <summary>
    /// Get nullable bool from environment variable
    /// </summary>
    public static bool GetBoolFromEnvVar(string envVarName)
    {
        string? result = Environment.GetEnvironmentVariable(envVarName);
        if (result is null || !bool.TryParse(result, out bool envBoolValue))
        {
            throw new ArgumentException(envVarName);
        }
        return envBoolValue;
    }
}
