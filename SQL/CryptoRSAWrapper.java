import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.security.PrivateKey;
import java.security.PublicKey;
import java.security.KeyFactory;
import java.security.spec.X509EncodedKeySpec;
import java.security.spec.PKCS8EncodedKeySpec;

import javax.crypto.Cipher;
import java.util.Base64;

public class CryptoRSAWrapper {

    private static KeyPair lastKeyPair;

    // Sinh cặp key và lưu vào lastKeyPair
    public static void generateKeyPair(int keySize) throws Exception {
        KeyPairGenerator keyGen = KeyPairGenerator.getInstance("RSA");
        keyGen.initialize(keySize);
        lastKeyPair = keyGen.generateKeyPair();
    }
 // Lấy public key dưới dạng chuỗi Base64
    public static String getPublicKey() throws Exception {
        if (lastKeyPair == null) throw new Exception("KeyPair chưa được tạo");
        PublicKey pub = lastKeyPair.getPublic();
        return Base64.getEncoder().encodeToString(pub.getEncoded());
    }

    // Lấy private key dưới dạng chuỗi Base64
    public static String getPrivateKey() throws Exception {
        if (lastKeyPair == null) throw new Exception("KeyPair chưa được tạo");
        PrivateKey priv = lastKeyPair.getPrivate();
        return Base64.getEncoder().encodeToString(priv.getEncoded());
    }
// Mã hóa bằng public key
    public static String encrypt(String plainText, String pubKeyStr) throws Exception {
        byte[] keyBytes = Base64.getDecoder().decode(pubKeyStr);
        X509EncodedKeySpec spec = new X509EncodedKeySpec(keyBytes);
        KeyFactory keyFactory = KeyFactory.getInstance("RSA");
        PublicKey pubKey = keyFactory.generatePublic(spec);

        Cipher cipher = Cipher.getInstance("RSA/ECB/PKCS1Padding");
        cipher.init(Cipher.ENCRYPT_MODE, pubKey);
        byte[] encrypted = cipher.doFinal(plainText.getBytes("UTF-8"));

        return Base64.getEncoder().encodeToString(encrypted);
    }
 // Giải mã bằng private key
    public static String decrypt(String cipherText, String privKeyStr) throws Exception {
        byte[] keyBytes = Base64.getDecoder().decode(privKeyStr);
        PKCS8EncodedKeySpec spec = new PKCS8EncodedKeySpec(keyBytes);
        KeyFactory keyFactory = KeyFactory.getInstance("RSA");
        PrivateKey privKey = keyFactory.generatePrivate(spec);

        Cipher cipher = Cipher.getInstance("RSA/ECB/PKCS1Padding");
        cipher.init(Cipher.DECRYPT_MODE, privKey);
        byte[] decrypted = cipher.doFinal(Base64.getDecoder().decode(cipherText));

        return new String(decrypted, "UTF-8");
    }
}
             