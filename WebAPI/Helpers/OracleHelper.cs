using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;

namespace WebAPI.Helpers
{
    
    /// Helper methods for common Oracle operations (stored procedures, cursors, etc.)
    
    public static class OracleHelper
    {
        
        /// Execute a stored procedure that returns a RefCursor and map results to a list.
        
        /// <typeparam name="T">The type to map each row to.</typeparam>
        /// <param name="conn">The Oracle connection.</param>
        /// <param name="procedureName">The stored procedure name (e.g., "APP.GET_ALL_EMPLOYEES").</param>
        /// <param name="cursorParamName">The name of the RefCursor output parameter.</param>
        /// <param name="mapper">A function to map each OracleDataReader row to T.</param>
        /// <param name="inputParams">Optional input parameters as (name, dbType, value) tuples.</param>
        /// <returns>A list of mapped objects.</returns>
        public static List<T> ExecuteRefCursor<T>(
            OracleConnection conn,
            string procedureName,
            string cursorParamName,
            Func<OracleDataReader, T> mapper,
            params (string name, OracleDbType dbType, object value)[] inputParams)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;

            // Add input parameters
            foreach (var (name, dbType, value) in inputParams)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            // Add RefCursor output
            cmd.Parameters.Add(new OracleParameter(cursorParamName, OracleDbType.RefCursor, ParameterDirection.Output));

            var result = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(mapper(reader));
            }
            return result;
        }

        
        /// Execute a stored procedure with optional input parameters and output parameters.
        /// Returns a dictionary of output parameter values.
        
        public static Dictionary<string, object> ExecuteNonQueryWithOutputs(
            OracleConnection conn,
            string procedureName,
            (string name, OracleDbType dbType, object value)[] inputParams,
            (string name, OracleDbType dbType)[] outputParams,
            OracleTransaction? transaction = null)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;
            if (transaction != null) cmd.Transaction = transaction;

            // Add input parameters
            foreach (var (name, dbType, value) in inputParams)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            // Add output parameters
            var outputParamDict = new Dictionary<string, OracleParameter>();
            foreach (var (name, dbType) in outputParams)
            {
                OracleParameter param;
                // Set size for VARCHAR2 and NVarchar2 to avoid "buffer too small" error
                if (dbType == OracleDbType.Varchar2 || dbType == OracleDbType.NVarchar2)
                {
                    param = new OracleParameter(name, dbType, 4000, null, ParameterDirection.Output);
                }
                else
                {
                    param = new OracleParameter(name, dbType, ParameterDirection.Output);
                }
                cmd.Parameters.Add(param);
                outputParamDict[name] = param;
            }

            cmd.ExecuteNonQuery();

            // Collect output values
            var result = new Dictionary<string, object>();
            foreach (var kvp in outputParamDict)
            {
                result[kvp.Key] = kvp.Value.Value;
            }
            return result;
        }

        
        /// Execute a simple stored procedure with no outputs.
        
        public static void ExecuteNonQuery(
            OracleConnection conn,
            string procedureName,
            params (string name, OracleDbType dbType, object value)[] inputParams)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;

            foreach (var (name, dbType, value) in inputParams)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            cmd.ExecuteNonQuery();
        }

        
        /// Execute a simple stored procedure with no outputs, within a transaction.
        
        public static void ExecuteNonQueryWithTransaction(
            OracleConnection conn,
            string procedureName,
            OracleTransaction transaction,
            params (string name, OracleDbType dbType, object value)[] inputParams)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Transaction = transaction;

            foreach (var (name, dbType, value) in inputParams)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            cmd.ExecuteNonQuery();
        }

        
        /// Execute a stored procedure and return a single output parameter value.
        /// Supports optional transaction for use within ExecuteWithTransaction.
        
        public static T ExecuteScalar<T>(
            OracleConnection conn,
            string procedureName,
            string outputParamName,
            OracleTransaction? transaction = null,
            params (string name, OracleDbType dbType, object value)[] inputParams)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;
            if (transaction != null) cmd.Transaction = transaction;

            foreach (var (name, dbType, value) in inputParams)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            // Determine output param type based on T
            var outputDbType = typeof(T) == typeof(string) ? OracleDbType.Varchar2 :
                               typeof(T) == typeof(int) ? OracleDbType.Int32 :
                               typeof(T) == typeof(decimal) ? OracleDbType.Decimal :
                               OracleDbType.Varchar2;

            var outputParam = new OracleParameter(outputParamName, outputDbType, 4000, null, ParameterDirection.Output);
            cmd.Parameters.Add(outputParam);

            cmd.ExecuteNonQuery();

            if (outputParam.Value == null || outputParam.Value == DBNull.Value)
                return default!;

            return (T)Convert.ChangeType(outputParam.Value.ToString(), typeof(T));
        }

        
        /// Execute a PL/SQL function (exposed as StoredProcedure) that returns a VARCHAR2 via ReturnValue.
        /// Commonly used for utility functions like HASH_PASSWORD_20CHARS or GET_EMPLOYEE_ROLES_BY_USERNAME.
        
        public static string? ExecuteFunctionString(
            OracleConnection conn,
            string functionName,
            params (string name, OracleDbType dbType, object value)[] inputParams)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = functionName;
            cmd.CommandType = CommandType.StoredProcedure;

            var returnParam = new OracleParameter("returnVal", OracleDbType.Varchar2, 4000, null, ParameterDirection.ReturnValue);
            cmd.Parameters.Add(returnParam);

            foreach (var (name, dbType, value) in inputParams)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            cmd.ExecuteNonQuery();

            if (returnParam.Value == null || returnParam.Value == DBNull.Value)
                return null;

            return returnParam.Value.ToString();
        }

        
        /// Safely get a string value from reader, returning empty string if null.
        
        public static string GetStringSafe(this OracleDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        
        /// Safely get a string value from reader by column name, returning empty string if null.
        
        public static string GetStringSafe(this OracleDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        
        /// Safely get a nullable decimal from reader.
        
        public static decimal? GetDecimalOrNull(this OracleDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
        }

        
        /// Safely get a nullable int from reader.
        
        public static int? GetInt32OrNull(this OracleDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetDecimal(ordinal));
        }

        
        /// Safely get a nullable DateTime from reader.
        
        public static DateTime? GetDateTimeOrNull(this OracleDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        
        /// Execute a stored procedure that returns a CLOB output parameter.
        
        public static string? ExecuteClobOutput(
            OracleConnection conn,
            string procedureName,
            string outputParamName,
            params (string name, OracleDbType dbType, object value)[] inputParams)
        {
            return ExecuteClobOutput(conn, procedureName, outputParamName, null, inputParams);
        }

        
        /// Execute a stored procedure that returns a CLOB output parameter with transaction support.
        
        public static string? ExecuteClobOutput(
            OracleConnection conn,
            string procedureName,
            string outputParamName,
            OracleTransaction? transaction,
            params (string name, OracleDbType dbType, object value)[] inputParams)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;
            if (transaction != null) cmd.Transaction = transaction;

            foreach (var (name, dbType, value) in inputParams)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            var outputParam = new OracleParameter(outputParamName, OracleDbType.Clob, ParameterDirection.Output);
            cmd.Parameters.Add(outputParam);

            cmd.ExecuteNonQuery();

            if (outputParam.Value == null || outputParam.Value == DBNull.Value)
                return null;

            var clob = (Oracle.ManagedDataAccess.Types.OracleClob)outputParam.Value;
            return clob.Value;
        }

        
        /// Execute a stored procedure that returns a BLOB output parameter.
        
        public static byte[]? ExecuteBlobOutput(
            OracleConnection conn,
            string procedureName,
            string outputParamName,
            params (string name, OracleDbType dbType, object value)[] inputParams)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;

            foreach (var (name, dbType, value) in inputParams)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            var outputParam = new OracleParameter(outputParamName, OracleDbType.Blob, ParameterDirection.Output);
            cmd.Parameters.Add(outputParam);

            cmd.ExecuteNonQuery();

            if (outputParam.Value == null || outputParam.Value == DBNull.Value)
                return null;

            using var blob = (Oracle.ManagedDataAccess.Types.OracleBlob)outputParam.Value;
            return blob?.Value;
        }

        
        /// Execute a SELECT query that returns a single BLOB value from the first row, first column.
        /// Useful for simple queries like "SELECT PDF FROM APP.INVOICE WHERE INVOICE_ID = :p_id".
        
        public static byte[]? ExecuteBlobQuery(
            OracleConnection conn,
            string query,
            params (string name, OracleDbType dbType, object value)[] parameters)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;

            foreach (var (name, dbType, value) in parameters)
            {
                cmd.Parameters.Add(name, dbType, ParameterDirection.Input).Value = value ?? DBNull.Value;
            }

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            if (reader.IsDBNull(0))
                return null;

            using var blob = reader.GetOracleBlob(0);
            return blob?.Value;
        }
    }
}

