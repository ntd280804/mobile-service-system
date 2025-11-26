using Microsoft.AspNetCore.Http;

namespace WebAPI.Models.Public
{
    public class VerifyInvoiceRequest
    {
        public IFormFile? File { get; set; }
        public decimal InvoiceId { get; set; }
    }
}
