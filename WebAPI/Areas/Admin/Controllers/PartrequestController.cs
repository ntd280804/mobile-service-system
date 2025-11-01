﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Services;
using WebAPI.Models.Part;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class PartrequestController : ControllerBase
    {

        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;
        private readonly QrGeneratorSingleton _qrGenerator;

        public PartrequestController(OracleConnectionManager connManager, JwtHelper jwtHelper, OracleSessionHelper oracleSessionHelper, QrGeneratorSingleton _QR)
        {

            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
            _qrGenerator = _QR;
        }
        // GET: api/admin/partrequest/getallpartrequests
        [HttpGet("getallpartrequest")]
        [Authorize]
        public IActionResult GetAllImports()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_ALL_PART_REQUESTS";
                cmd.CommandType = CommandType.StoredProcedure;

                var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var result = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        REQUEST_ID = reader["REQUEST_ID"],
                        ORDER_ID = reader["ORDER_ID"],
                        EmpUsername = reader["EmpUsername"],
                        REQUEST_DATE = reader["REQUEST_DATE"],
                        STATUS = reader["STATUS"]
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }

        [HttpPost("accept/{requestId}")]
        [Authorize]
        public IActionResult AcceptPartRequest(int requestId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.ACCEPT_PART_REQUEST";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_request_id", OracleDbType.Int32, ParameterDirection.Input).Value = requestId;
                cmd.ExecuteNonQuery();

                return Ok(new { message = "Accepted" });
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }

        [HttpPost("deny/{requestId}")]
        [Authorize]
        public IActionResult DenyPartRequest(int requestId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.DENY_PART_REQUEST";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_request_id", OracleDbType.Int32, ParameterDirection.Input).Value = requestId;
                cmd.ExecuteNonQuery();

                return Ok(new { message = "Denied" });
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }

        [HttpPost("post")]
        [Authorize]
        public IActionResult CreatePartRequest([FromBody] CreatePartRequestDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest("No items to request");
            if (string.IsNullOrEmpty(dto.EmpUsername))
                return BadRequest("Missing employee username");

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                // Begin transaction
                using var transaction = conn.BeginTransaction();

                // 1. Get EMP_ID from username
                int empId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_EMPLOYEE_ID_BY_USERNAME";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_username", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.EmpUsername;
                    var pEmpId = new OracleParameter("p_emp_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pEmpId);

                    cmd.ExecuteNonQuery();
                    empId = Convert.ToInt32(pEmpId.Value.ToString());
                }

                // 2. Create PART_REQUEST
                int requestId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "APP.CREATE_PART_REQUEST";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_order_id", OracleDbType.Int32, ParameterDirection.Input).Value = (int)dto.OrderId;
                    cmd.Parameters.Add("p_emp_id", OracleDbType.Int32, ParameterDirection.Input).Value = empId;
                    cmd.Parameters.Add("p_status", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.Status;
                    cmd.Parameters.Add("p_request_date", OracleDbType.Date, ParameterDirection.Input).Value = dto.RequestDate;

                    var outRequestId = new OracleParameter("p_request_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(outRequestId);

                    cmd.ExecuteNonQuery();
                    requestId = Convert.ToInt32(outRequestId.Value.ToString());
                }

                // 3. Create PART_REQUEST_ITEM for each item
                foreach (var item in dto.Items)
                {
                    using (var cmdItem = conn.CreateCommand())
                    {
                        cmdItem.Transaction = transaction;
                        cmdItem.CommandText = "APP.CREATE_PART_REQUEST_ITEM";
                        cmdItem.CommandType = CommandType.StoredProcedure;

                        cmdItem.Parameters.Add("p_request_id", OracleDbType.Int32, ParameterDirection.Input).Value = requestId;
                        cmdItem.Parameters.Add("p_part_id", OracleDbType.Int32, ParameterDirection.Input).Value = item.PartId;

                        var outRequestItemId = new OracleParameter("p_request_item_id", OracleDbType.Int32, ParameterDirection.Output);
                        cmdItem.Parameters.Add(outRequestItemId);

                        cmdItem.ExecuteNonQuery();
                    }
                }

                // Commit transaction
                transaction.Commit();

                return Ok(new { message = "Part request created successfully", requestId = requestId });
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { message = "Oracle Error", ErrorCode = ex.Number, Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }


    }
}