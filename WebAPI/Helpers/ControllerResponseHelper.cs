using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebAPI.Models;

namespace WebAPI.Helpers
{
    
    /// Utilities for converting controller results to standardized ApiResponse payloads.
    
    public static class ControllerResponseHelper
    {
        public static ApiResponse<T> ExtractApiResponse<T>(ActionResult<ApiResponse<T>> actionResult, string defaultMessage)
        {
            if (actionResult.Value != null)
            {
                return actionResult.Value;
            }

            return ExtractApiResponseFromResult<T>(actionResult.Result, defaultMessage);
        }

        public static ApiResponse<T> ExtractApiResponseFromResult<T>(IActionResult? result, string defaultMessage)
        {
            if (result == null)
            {
                return ApiResponse<T>.Fail(defaultMessage);
            }

            if (result is ObjectResult obj)
            {
                if (obj.Value is ApiResponse<T> resp)
                    return resp;

                var message = ExtractMessage(obj.Value) ?? defaultMessage;
                return ApiResponse<T>.Fail(message);
            }

            if (result is StatusCodeResult statusCodeResult)
            {
                return ApiResponse<T>.Fail(GetMessageForStatus(statusCodeResult.StatusCode, defaultMessage));
            }

            return ApiResponse<T>.Fail(defaultMessage);
        }

        public static string? ExtractMessage(object? value)
        {
            if (value == null) return null;

            try
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    return msgProp.GetString();
                if (doc.RootElement.TryGetProperty("Message", out var msgProp2))
                    return msgProp2.GetString();
            }
            catch
            {
                // ignore malformed payloads
            }

            return null;
        }

        private static string GetMessageForStatus(int statusCode, string defaultMessage)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => "Bad request",
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status500InternalServerError => "Internal server error",
                _ => defaultMessage
            };
        }
    }
}

