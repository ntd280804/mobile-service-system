using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Services;

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


    }
}