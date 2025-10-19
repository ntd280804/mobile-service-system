using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace WebAPI.Services
{
    public class QrGeneratorSingleton
    {
        private readonly QRCodeGenerator _qrGenerator;

        public QrGeneratorSingleton()
        {
            _qrGenerator = new QRCodeGenerator();
        }

        // Generate QR code as byte[] PNG
        public byte[] GenerateQRImage(string data)
        {
            using var qrData = _qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrData);
            using var bitmap = qrCode.GetGraphic(20);
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}
