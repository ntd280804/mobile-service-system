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
	public sealed class PdfSignatureService
	{
		private readonly OracleSessionHelper _oracleSessionHelper;
		private readonly OracleConnectionManager _connectionManager;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private const int DefaultBlockSizeBytes = 20 * 1024; // 20KB as required

		public PdfSignatureService(OracleSessionHelper oracleSessionHelper, OracleConnectionManager connectionManager, IHttpContextAccessor httpContextAccessor)
		{
			_oracleSessionHelper = oracleSessionHelper ?? throw new ArgumentNullException(nameof(oracleSessionHelper));
			_connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
		}

		/// <summary>
		/// Splits input PDF bytes into 20KB blocks, signs each via SIGN_PDF, and concatenates signatures.
		/// Returns the concatenated signature bytes.
		/// </summary>
		public byte[] SignPdf(byte[] pdfBytes, string privateKey, CancellationToken cancellationToken = default)
		{
			if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));
			if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));

			var concatenatedSignatureStream = new MemoryStream(capacity: Math.Max(64 * 1024, pdfBytes.Length / 2));

			var connection = GetCurrentUserConnection();
			if (connection == null)
			{
				throw new InvalidOperationException("Oracle session not available for current user.");
			}

			foreach (var block in EnumerateBlocks(pdfBytes, DefaultBlockSizeBytes))
			{
				cancellationToken.ThrowIfCancellationRequested();

				var blockSignature = SignBlock(connection, block, privateKey);
				concatenatedSignatureStream.Write(blockSignature, 0, blockSignature.Length);
			}

			return concatenatedSignatureStream.ToArray();
		}

		/// <summary>
		/// Calls a custom procedure (e.g., to persist the final signature) with user-bound parameters plus :p_signature BLOB.
		/// You supply additional parameter bindings through the configureParameters action.
		/// The procedure must accept a BLOB parameter named p_signature (adjust via configureParameters if different).
		/// </summary>
		public void UpdateFinalSignature(string procedureName, byte[] finalSignature, Action<OracleCommand> configureParameters, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name is required.", nameof(procedureName));
			if (finalSignature == null) throw new ArgumentNullException(nameof(finalSignature));
			if (configureParameters == null) throw new ArgumentNullException(nameof(configureParameters));

			var connection = GetCurrentUserConnection();
			if (connection == null)
			{
				throw new InvalidOperationException("Oracle session not available for current user.");
			}

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
				signatureParam.Value = finalSignature;

				// Execute non-query
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Signs the input PDF bytes using a PFX certificate via GroupDocs.Signature and returns the signed PDF bytes.
		/// The visual appearance can be customized via the optional configureAppearance action.
		/// </summary>
		public byte[] SignPdfWithDigitalCertificate(byte[] pdfBytes, byte[] certificatePfxBytes, string certificatePassword, Action<DigitalSignOptions> configureAppearance = null)
		{
			if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));
			if (certificatePfxBytes == null) throw new ArgumentNullException(nameof(certificatePfxBytes));
			if (string.IsNullOrWhiteSpace(certificatePassword)) throw new ArgumentException("Password is required.", nameof(certificatePassword));

			using (var pdfStream = new MemoryStream(pdfBytes, writable: false))
			using (var certStream = new MemoryStream(certificatePfxBytes, writable: false))
			using (var signature = new Signature(pdfStream))
			{
				var options = new DigitalSignOptions(certStream)
				{
					Password = certificatePassword,
					// Reason/Location are optional; can be overridden from configureAppearance
					Reason = "Approved",
					Location = "Việt Nam",
					AllPages = false,
					// Prefer signing on the last page to keep signature near totals/footer
					PagesSetup = new PagesSetup { LastPage = true },
					// Fixed size of the visual box
					Width = 170,
					Height = 90,
					// Use relative alignment so it adapts to any page size or content length
					HorizontalAlignment = HorizontalAlignment.Right,
					VerticalAlignment = VerticalAlignment.Bottom,
					Margin = new Padding(20),
					Border = new Border()
					{
						Visible = true,
						Color = Color.DarkGray,
						Weight = 1
					},
					Appearance = new PdfDigitalSignatureAppearance()
					{
						// Keep labels minimal; content will be readable within the signature box
						ReasonLabel = "Lý do",
						LocationLabel = "Địa điểm",
						DigitalSignedLabel = "Ký bởi",
						DateSignedAtLabel = "Ngày",
					}
				};

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

		/// <summary>
		/// Creates a temporary self-signed certificate (PFX) from a PEM private key so GroupDocs can embed a certificate.
		/// Intended as a fallback when a "real" PFX is not available.
		/// </summary>
		public static byte[] CreateSelfSignedPfxFromPrivateKey(string privateKeyPem, string subjectCommonName, string pfxPassword)
		{
			if (string.IsNullOrWhiteSpace(privateKeyPem)) throw new ArgumentException("Private key PEM is required.", nameof(privateKeyPem));
			if (string.IsNullOrWhiteSpace(subjectCommonName)) subjectCommonName = "MobileServiceSystem";
			if (string.IsNullOrWhiteSpace(pfxPassword)) pfxPassword = Guid.NewGuid().ToString("N");

			// Normalize PEM (ensure header/footer exist)
			string pem = privateKeyPem;
			if (!pem.Contains("BEGIN"))
			{
				// Assume raw base64 without headers
				pem = "-----BEGIN PRIVATE KEY-----\n" + privateKeyPem + "\n-----END PRIVATE KEY-----";
			}

			using (var rsa = RSA.Create())
			{
				try
				{
					rsa.ImportFromPem(pem);
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException("Failed to import private key PEM.", ex);
				}

				var dn = new X500DistinguishedName($"CN={subjectCommonName}");
				var req = new CertificateRequest(dn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

				// Basic constraints and key usage for digital signature
				req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
				req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

				var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
				var notAfter = notBefore.AddYears(2);
				using var cert = req.CreateSelfSigned(notBefore, notAfter);

				// Export as PFX with private key
				return cert.Export(X509ContentType.Pkcs12, pfxPassword);
			}
		}

		private static IEnumerable<byte[]> EnumerateBlocks(byte[] data, int blockSizeBytes)
		{
			if (blockSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(blockSizeBytes));

			var offset = 0;
			while (offset < data.Length)
			{
				var count = Math.Min(blockSizeBytes, data.Length - offset);
				var buffer = new byte[count];
				Buffer.BlockCopy(data, offset, buffer, 0, count);
				yield return buffer;
				offset += count;
			}
		}

		/// <summary>
		/// Executes SIGN_PDF(p_block RAW, p_private_key VARCHAR2, p_signature OUT BLOB) and returns signature bytes for a block.
		/// </summary>
		private static byte[] SignBlock(OracleConnection connection, byte[] blockBytes, string privateKey)
		{
			using (var cmd = new OracleCommand("APP.SIGN_PDF", connection))
			{
				cmd.CommandType = CommandType.StoredProcedure;

				// p_block IN RAW (<= 32KB)
				var pBlock = cmd.Parameters.Add("p_block", OracleDbType.Raw, ParameterDirection.Input);
				pBlock.Value = blockBytes;

				// p_private_key IN VARCHAR2
				var pPrivateKey = cmd.Parameters.Add("p_private_key", OracleDbType.Varchar2, ParameterDirection.Input);
				pPrivateKey.Value = privateKey;

				// p_signature OUT BLOB
				var pSignature = cmd.Parameters.Add("p_signature", OracleDbType.Blob, ParameterDirection.Output);

				cmd.ExecuteNonQuery();

				// Read BLOB value
				if (pSignature.Value == null || pSignature.Value is DBNull)
				{
					throw new InvalidOperationException("SIGN_PDF returned null signature.");
				}

				using (var signatureBlob = (OracleBlob)pSignature.Value)
				{
					if (signatureBlob == null || !signatureBlob.IsNull)
					{
						return signatureBlob.Value;
					}
				}

				throw new InvalidOperationException("SIGN_PDF returned empty signature BLOB.");
			}
		}

		private OracleConnection GetCurrentUserConnection()
		{
			var httpContext = _httpContextAccessor.HttpContext;
			if (httpContext == null) return null;

			var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(httpContext, _connectionManager, out var unauthorized);
			return conn;
		}
	}
}


