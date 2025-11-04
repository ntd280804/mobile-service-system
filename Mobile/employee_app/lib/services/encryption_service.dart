import 'dart:convert';
import 'dart:typed_data';
import 'package:pointycastle/export.dart';
import 'package:asn1lib/asn1lib.dart';

class EncryptionService {
  /// Hybrid encryption: RSA + AES
  /// Returns {encryptedKeyBlock, cipherData} both as base64 strings
  static Map<String, String> hybridEncrypt(
    String plaintext,
    String publicKeyBase64,
  ) {
    // 1. Generate AES key (256-bit) and IV (128-bit)
    final secureRandom = _getSecureRandom();
    final aesKey = secureRandom.nextBytes(32); // 256 bits
    final iv = secureRandom.nextBytes(16); // 128 bits

    // 2. Encrypt data with AES
    final cipher = PaddedBlockCipher('AES/CBC/PKCS7');
    final params = PaddedBlockCipherParameters(
      ParametersWithIV(KeyParameter(aesKey), iv),
      null,
    );
    cipher.init(true, params);

    final plainBytes = utf8.encode(plaintext);
    final cipherBytes = cipher.process(Uint8List.fromList(plainBytes));

    // 3. Prepare key block: [aesKey + iv]
    final keyBlock = Uint8List(aesKey.length + iv.length);
    keyBlock.setRange(0, aesKey.length, aesKey);
    keyBlock.setRange(aesKey.length, keyBlock.length, iv);

    // 4. Encrypt key block with RSA public key
    final rsaPublicKey = _parsePublicKeyFromBase64(publicKeyBase64);
    final rsaCipher = OAEPEncoding(RSAEngine())
      ..init(
        true,
        PublicKeyParameter<RSAPublicKey>(rsaPublicKey),
      );

    final encryptedKeyBlock = rsaCipher.process(keyBlock);

    // 5. Return both as base64
    return {
      'encryptedKeyBlock': base64.encode(encryptedKeyBlock),
      'cipherData': base64.encode(cipherBytes),
    };
  }

  /// Parse RSA public key from base64-encoded SubjectPublicKeyInfo (SPKI)
  static RSAPublicKey _parsePublicKeyFromBase64(String publicKeyBase64) {
    final bytes = base64.decode(publicKeyBase64);
    final asn1Parser = ASN1Parser(bytes);
    final topLevelSeq = asn1Parser.nextObject() as ASN1Sequence;

    // SPKI structure: SEQUENCE { algorithm, subjectPublicKey }
    final publicKeyBitString = topLevelSeq.elements[1] as ASN1BitString;
    final publicKeyBytes = publicKeyBitString.contentBytes();

    // Parse the actual RSA public key
    final asn1PublicKey = ASN1Parser(publicKeyBytes!);
    final publicKeySeq = asn1PublicKey.nextObject() as ASN1Sequence;

    final modulus = (publicKeySeq.elements[0] as ASN1Integer).valueAsBigInteger;
    final exponent = (publicKeySeq.elements[1] as ASN1Integer).valueAsBigInteger;

    return RSAPublicKey(modulus!, exponent!);
  }

  /// Get a secure random generator
  static SecureRandom _getSecureRandom() {
    final secureRandom = FortunaRandom();
    final random = SecureRandom('Fortuna');
    final seed = List<int>.generate(32, (i) => DateTime.now().millisecondsSinceEpoch + i);
    random.seed(KeyParameter(Uint8List.fromList(seed)));
    return random;
  }
}
