using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Threading.Tasks;
using WebAPI.Services;

namespace WebAPI.Helpers
{
    
    /// Helper for common controller patterns (Oracle exception handling, session management, etc.)
    
    public class ControllerHelper
    {
        private readonly OracleConnectionManager _connManager;
        private readonly OracleSessionHelper _sessionHelper;

        public ControllerHelper(OracleConnectionManager connManager, OracleSessionHelper sessionHelper)
        {
            _connManager = connManager;
            _sessionHelper = sessionHelper;
        }

        
        /// Wraps an Oracle action with standard connection retrieval and exception handling.
        /// Returns Unauthorized if session is invalid, handles OracleException 28 (session killed),
        /// and returns StatusCode 500 for other exceptions.
        
        /// <param name="httpContext">The current HttpContext.</param>
        /// <param name="action">The action to execute with the Oracle connection.</param>
        /// <param name="errorMessage">Custom error message for general exceptions.</param>
        /// <returns>The IActionResult from the action or an error result.</returns>
        public async Task<IActionResult> ExecuteWithConnection(
            HttpContext httpContext,
            Func<OracleConnection, IActionResult> action,
            string errorMessage = "Lỗi xử lý yêu cầu")
        {
            var conn = _sessionHelper.GetConnectionOrUnauthorized(httpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                return action(conn);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                return await HandleSessionKilled(httpContext);
            }
            catch (OracleException ex)
            {
                return new ObjectResult(new { message = "Oracle Error", errorCode = ex.Number, error = ex.Message })
                {
                    StatusCode = 500
                };
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { message = errorMessage, detail = ex.Message })
                {
                    StatusCode = 500
                };
            }
        }

        
        /// Async version of ExecuteWithConnection.
        
        public async Task<IActionResult> ExecuteWithConnectionAsync(
            HttpContext httpContext,
            Func<OracleConnection, Task<IActionResult>> action,
            string errorMessage = "Lỗi xử lý yêu cầu")
        {
            var conn = _sessionHelper.GetConnectionOrUnauthorized(httpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                return await action(conn);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                return await HandleSessionKilled(httpContext);
            }
            catch (OracleException ex)
            {
                return new ObjectResult(new { message = "Oracle Error", errorCode = ex.Number, error = ex.Message })
                {
                    StatusCode = 500
                };
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { message = errorMessage, detail = ex.Message })
                {
                    StatusCode = 500
                };
            }
        }

        
        /// Handle session killed error (OracleException 28).
        
        private async Task<IActionResult> HandleSessionKilled(HttpContext httpContext)
        {
            _sessionHelper.TryGetSession(httpContext, out var username, out var platform, out var sessionId);
            await _sessionHelper.HandleSessionKilled(httpContext, _connManager, username, platform, sessionId);
            return new UnauthorizedObjectResult(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
        }

        
        /// Create a standard Ok response with data.
        
        public static IActionResult Ok(object data) => new OkObjectResult(data);

        
        /// Create a standard BadRequest response.
        
        public static IActionResult BadRequest(string message) => new BadRequestObjectResult(new { message });

        
        /// Create a standard NotFound response.
        
        public static IActionResult NotFound(string message) => new NotFoundObjectResult(new { message });

        
        /// Create a standard error response with status code 500.
        
        public static IActionResult ServerError(string message, string detail) =>
            new ObjectResult(new { message, detail }) { StatusCode = 500 };

        
        /// Execute an action within a transaction. Auto-rollback on error, auto-commit on success.
        /// Handles OracleException 28 (session killed) and business errors (20001-20999).
        
        public async Task<IActionResult> ExecuteWithTransaction(
            HttpContext httpContext,
            Func<OracleConnection, OracleTransaction, IActionResult> action,
            string errorMessage = "Lỗi xử lý yêu cầu")
        {
            var conn = _sessionHelper.GetConnectionOrUnauthorized(httpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            using var transaction = conn.BeginTransaction();
            try
            {
                var result = action(conn, transaction);
                transaction.Commit();
                return result;
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                transaction.Rollback();
                return await HandleSessionKilled(httpContext);
            }
            catch (OracleException ex) when (ex.Number >= 20001 && ex.Number <= 20999)
            {
                // Business logic errors from stored procedures
                transaction.Rollback();
                return new BadRequestObjectResult(new { message = ex.Message });
            }
            catch (OracleException ex)
            {
                transaction.Rollback();
                return new ObjectResult(new { message = "Oracle Error", errorCode = ex.Number, error = ex.Message })
                {
                    StatusCode = 500
                };
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return new ObjectResult(new { message = errorMessage, detail = ex.Message })
                {
                    StatusCode = 500
                };
            }
        }
    }
}

