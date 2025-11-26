using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using WebAPI.Models.Invoice;

namespace WebAPI.Helpers
{
    public static class InvoiceDataHelper
    {
        public static InvoiceDto? LoadInvoiceData(OracleConnection conn, int invoiceId)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "APP.GET_INVOICE_BY_ID";
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId;
            var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
            cmd.Parameters.Add(outputCursor);

            InvoiceDto? invoice = null;
            var items = new List<InvoiceItemDto>();
            var services = new List<InvoiceServiceDto>();

            using var reader = cmd.ExecuteReader();

            if (!reader.HasRows)
                return null;

            while (reader.Read())
            {
                if (invoice == null)
                {
                    invoice = new InvoiceDto
                    {
                        InvoiceId = Convert.ToInt32(reader["InvoiceId"]),
                        StockOutId = reader["StockOutId"] != DBNull.Value ? Convert.ToInt32(reader["StockOutId"]) : 0,
                        CustomerPhone = reader["CustomerPhone"]?.ToString() ?? string.Empty,
                        EmpUsername = reader["EmpUsername"]?.ToString() ?? string.Empty,
                        InvoiceDate = Convert.ToDateTime(reader["InvoiceDate"]),
                        TotalAmount = reader["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAmount"]) : 0,
                        Status = reader["Status"]?.ToString() ?? string.Empty,
                        Items = new List<InvoiceItemDto>(),
                        Services = new List<InvoiceServiceDto>()
                    };
                }

                var itemType = reader["ItemType"]?.ToString();
                if (itemType == "ITEM" && reader["PartId"] != DBNull.Value)
                {
                    items.Add(new InvoiceItemDto
                    {
                        PartId = Convert.ToInt32(reader["PartId"]),
                        PartName = reader["PartName"]?.ToString() ?? string.Empty,
                        Manufacturer = reader["Manufacturer"]?.ToString() ?? string.Empty,
                        Serial = reader["Serial"]?.ToString() ?? string.Empty,
                        Price = reader["ItemPrice"] != DBNull.Value ? Convert.ToDecimal(reader["ItemPrice"]) : 0
                    });
                }
                else if (itemType == "SERVICE" && reader["ServiceId"] != DBNull.Value)
                {
                    services.Add(new InvoiceServiceDto
                    {
                        ServiceId = Convert.ToInt32(reader["ServiceId"]),
                        ServiceName = reader["ServiceName"]?.ToString() ?? string.Empty,
                        Quantity = reader["Quantity"] != DBNull.Value ? Convert.ToInt32(reader["Quantity"]) : 1,
                        Price = reader["ServicePrice"] != DBNull.Value ? Convert.ToDecimal(reader["ServicePrice"]) : 0
                    });
                }
            }

            if (invoice != null)
            {
                invoice.Items = items;
                invoice.Services = services;
            }

            return invoice;
        }
    }
}

