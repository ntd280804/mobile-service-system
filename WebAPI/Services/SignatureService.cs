using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Microsoft.AspNetCore.Http;
using WebAPI.Helpers;
using WebAPI.Services;
using GroupDocs.Signature;
using GroupDocs.Signature.Domain;
using GroupDocs.Signature.Options;
using GroupDocs.Signature.Options.Appearances;
using System.Drawing;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace MobileServiceSystem.Signing
{
	public sealed class SignatureService
	{
		private readonly OracleSessionHelper _oracleSessionHelper;
		private readonly OracleConnectionManager _connectionManager;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private const int DefaultBlockSizeBytes = 20 * 1024; // 20KB as required

		public SignatureService(OracleSessionHelper oracleSessionHelper, OracleConnectionManager connectionManager, IHttpContextAccessor httpContextAccessor)
		{
			_oracleSessionHelper = oracleSessionHelper ?? throw new ArgumentNullException(nameof(oracleSessionHelper));
			_connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
		}
		
		public void UpdateFinalPDF(string procedureName, byte[] PDF, Action<OracleCommand> configureParameters, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name is required.", nameof(procedureName));
			if (PDF == null) throw new ArgumentNullException(nameof(PDF));
			if (configureParameters == null) throw new ArgumentNullException(nameof(configureParameters));

			var connection = EnsureCurrentUserConnection();

			using (var cmd = new OracleCommand(procedureName, connection))
			{
				cmd.CommandType = CommandType.StoredProcedure;

				// Let caller bind their needed params (e.g., document id). They can also rename p_signature if desired.
				configureParameters(cmd);

				// If caller didn't add p_signature, add a default one.
				if (!cmd.Parameters.Contains("p_signature"))
				{
					cmd.Parameters.Add("p_signature", OracleDbType.Blob, ParameterDirection.Input);
				}

				// Bind signature as BLOB
				var signatureParam = cmd.Parameters["p_signature"];
				signatureParam.Value = PDF;

				// Execute non-query
				cmd.ExecuteNonQuery();
			}
		}

		
		/// Validates that the supplied PFX bytes and password can be opened and contain a private key.
		
		public void ValidateCertificate(byte[] certificatePfxBytes, string certificatePassword)
		{
			if (certificatePfxBytes == null || certificatePfxBytes.Length == 0)
				throw new ArgumentException("Certificate PFX bytes are required.", nameof(certificatePfxBytes));
			if (string.IsNullOrWhiteSpace(certificatePassword))
				throw new ArgumentException("Certificate password is required.", nameof(certificatePassword));

			try
			{
				using var cert = new X509Certificate2(
					certificatePfxBytes,
					certificatePassword,
					X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

				if (!cert.HasPrivateKey)
				{
					throw new InvalidOperationException("Certificate PFX does not contain a private key suitable for signing.");
				}
			}
			catch (CryptographicException ex)
			{
				throw new InvalidOperationException("Không thể mở PFX certificate. Vui lòng kiểm tra lại mật khẩu hoặc định dạng tập tin.", ex);
			}
		}

		
		/// Signs the input PDF bytes using a PFX certificate via GroupDocs.Signature and returns the signed PDF bytes.
		/// The visual appearance can be customized via the optional configureAppearance action.
		
		public byte[] SignPdfWithDigitalCertificate(byte[] pdfBytes, byte[] certificatePfxBytes, string certificatePassword, Action<DigitalSignOptions> configureAppearance = null)
		{
			if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));
			if (certificatePfxBytes == null) throw new ArgumentNullException(nameof(certificatePfxBytes));
			if (string.IsNullOrWhiteSpace(certificatePassword)) throw new ArgumentException("Password is required.", nameof(certificatePassword));

			using (var pdfStream = new MemoryStream(pdfBytes, writable: false))
			using (var certStream = new MemoryStream(certificatePfxBytes, writable: false))
			using (var signature = new Signature(pdfStream))
			{
					var options = CreateDefaultSignOptions(certStream, certificatePassword);

				// Allow caller to customize appearance/placement if needed
				configureAppearance?.Invoke(options);

				using (var output = new MemoryStream())
				{
					// Sign to stream (preferred). If not supported in your GroupDocs version, use a temp file fall-back.
					signature.Sign(output, options);
					return output.ToArray();
				}
			}
		}
		private OracleConnection GetCurrentUserConnection()
		{
			var httpContext = _httpContextAccessor.HttpContext;
			if (httpContext == null) return null;

			var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(httpContext, _connectionManager, out var unauthorized);
			return conn;
		}

		private OracleConnection EnsureCurrentUserConnection()
		{
			return GetCurrentUserConnection()
				?? throw new InvalidOperationException("Oracle session not available for current user.");
		}

		private static DigitalSignOptions CreateDefaultSignOptions(Stream certificateStream, string certificatePassword)
		{
			return new DigitalSignOptions(certificateStream)
			{
				Password = certificatePassword,
				Reason = "Approved",
				Location = "Việt Nam",
				AllPages = false,
				PagesSetup = new PagesSetup { FirstPage = true },
				Width = 170,
				Height = 90,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Padding(30),
				Border = new Border()
				{
					Visible = true,
					Color = Color.DarkGray,
					Weight = 1
				},
				Appearance = new PdfDigitalSignatureAppearance()
				{
					ReasonLabel = "Lý do",
					LocationLabel = "Địa điểm",
					DigitalSignedLabel = "Ký bởi",
					DateSignedAtLabel = "Ngày",
				},
			};
		}
	}
}


