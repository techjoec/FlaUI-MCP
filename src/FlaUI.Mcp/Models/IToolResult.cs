// src/FlaUI.Mcp/Models/IToolResult.cs
namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Base interface for all structured tool results.
/// Provides both compact text (token-efficient) and structured JSON representations.
/// </summary>
public interface IToolResult
{
    /// <summary>
    /// Generate a token-efficient text representation for LLM consumption
    /// </summary>
    string ToCompactString();

    /// <summary>
    /// Return the full structured data object for JSON serialization
    /// </summary>
    object ToStructuredData();
}
