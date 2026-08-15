//! String encryption compatible with the `1Remote.Security` package that was
//! used before the Rust migration (see docs/adr/0009).
//!
//! Algorithm (faithful re-implementation of the .NET code):
//!
//! * salt        = MD5(salt_input)[..8] (lowercase hex, UTF-8 input)
//! * random key  = 24 chars, each in [48, 122) i.e. ASCII '0'..='y'
//! * keystring   = random_key + salt  (plain concatenation)
//! * AES layer   = AesThenHmac.SimpleEncryptWithPassword:
//!     - two independent PBKDF2(keystring, 8-byte salt, 10_000) derivations
//!       producing a 256-bit crypt key and a 256-bit auth key
//!     - AES-256-CBC (PKCS7) with random 16-byte IV
//!     - HMAC-SHA256 over [payload][salt1][salt2][iv][ciphertext]
//!     - layout: [salt1(8)][salt2(8)][iv(16)][ciphertext][mac(32)]
//!     - base64 output
//! * interleave  = first 48 output chars: even index from ciphertext,
//!                 odd index from the random key; remaining ciphertext appended
//! * plaintext   = plain + "+?:?+-->" + seconds + milliseconds
//! * optional secondary key wraps the whole result again with the flag prefix.

use aes::cipher::{BlockDecryptMut, BlockEncryptMut, KeyIvInit};
use base64::{engine::general_purpose::STANDARD as BASE64, Engine as _};
use cbc::{Decryptor, Encryptor};
use hmac::{Hmac, Mac};
use md5::Md5;
use md5::Digest;
use pbkdf2::pbkdf2_hmac;
use rand::Rng;
use sha1::Sha1;
use sha2::Sha256;

type Aes256CbcEnc = Encryptor<aes::Aes256>;
type Aes256CbcDec = Decryptor<aes::Aes256>;
type HmacSha256 = Hmac<Sha256>;

const BLOCK_BIT_SIZE: usize = 128;
const KEY_BIT_SIZE: usize = 256;
const SALT_BIT_SIZE: usize = 64;
const ITERATIONS: u32 = 10_000;

const SEPARATOR: &str = "+?:?+-->";
const SECONDARY_FLAG: &str = "Secondary+Encryption+Flag-->";

const AES_SALT_LEN: usize = SALT_BIT_SIZE / 8; // 8
const AES_IV_LEN: usize = BLOCK_BIT_SIZE / 8; // 16
const AES_KEY_LEN: usize = KEY_BIT_SIZE / 8; // 32
const HMAC_LEN: usize = 32;

// ---------------------------------------------------------------------------
// AES layer (AesThenHmac)
// ---------------------------------------------------------------------------

fn aes_encrypt_with_password(plain: &[u8], password: &str) -> Result<Vec<u8>, String> {
    // PBKDF2 needs to generate a fresh 8-byte salt internally each derivation.
    // .NET Rfc2898DeriveBytes(password, 8, 10000) -> 32 bytes derived.
    let salt1 = rand::thread_rng().gen::<[u8; AES_SALT_LEN]>();
    let salt2 = rand::thread_rng().gen::<[u8; AES_SALT_LEN]>();

    let mut crypt_key = [0u8; AES_KEY_LEN];
    // .NET's Rfc2898DeriveBytes defaults to HMACSHA1 — must match for
    // compatibility with passwords encrypted by 1Remote.Security.
    pbkdf2_hmac::<Sha1>(password.as_bytes(), &salt1, ITERATIONS, &mut crypt_key);

    let mut auth_key = [0u8; AES_KEY_LEN];
    pbkdf2_hmac::<Sha1>(password.as_bytes(), &salt2, ITERATIONS, &mut auth_key);

    let iv: [u8; AES_IV_LEN] = rand::thread_rng().gen();

    // AES-256-CBC PKCS7 encrypt
    let mut ciphertext = vec![0u8; plain.len() + AES_IV_LEN];
    let encryptor = Aes256CbcEnc::new_from_slices(&crypt_key, &iv)
        .map_err(|e| format!("aes init: {e}"))?;
    let ct_len = encryptor
        .encrypt_padded_b2b_mut::<aes::cipher::block_padding::Pkcs7>(plain, &mut ciphertext)
        .map_err(|e| format!("aes encrypt: {e}"))?
        .len();
    ciphertext.truncate(ct_len);

    // HMAC-SHA256 over [payload(empty)][salt1][salt2][iv][ciphertext]
    let mut mac_input = Vec::with_capacity(AES_SALT_LEN * 2 + AES_IV_LEN + ciphertext.len());
    mac_input.extend_from_slice(&salt1);
    mac_input.extend_from_slice(&salt2);
    mac_input.extend_from_slice(&iv);
    mac_input.extend_from_slice(&ciphertext);
    let mut mac = HmacSha256::new_from_slice(&auth_key).map_err(|e| format!("hmac init: {e}"))?;
    mac.update(&mac_input);
    let mac_bytes = mac.finalize().into_bytes();

    let mut out = Vec::with_capacity(mac_input.len() + HMAC_LEN);
    out.extend_from_slice(&mac_input);
    out.extend_from_slice(&mac_bytes);
    Ok(out)
}

fn aes_decrypt_with_password(encrypted: &[u8], password: &str) -> Option<Vec<u8>> {
    let min_len = AES_SALT_LEN * 2 + AES_IV_LEN + HMAC_LEN;
    if encrypted.len() < min_len {
        return None;
    }
    let payload_len = 0;
    let mut salt1 = [0u8; AES_SALT_LEN];
    let mut salt2 = [0u8; AES_SALT_LEN];
    salt1.copy_from_slice(&encrypted[payload_len..payload_len + AES_SALT_LEN]);
    salt2.copy_from_slice(
        &encrypted[payload_len + AES_SALT_LEN..payload_len + AES_SALT_LEN * 2],
    );

    let mut crypt_key = [0u8; AES_KEY_LEN];
    pbkdf2_hmac::<Sha1>(password.as_bytes(), &salt1, ITERATIONS, &mut crypt_key);
    let mut auth_key = [0u8; AES_KEY_LEN];
    pbkdf2_hmac::<Sha1>(password.as_bytes(), &salt2, ITERATIONS, &mut auth_key);

    let mac_off = encrypted.len() - HMAC_LEN;
    let expected_mac = &encrypted[mac_off..];

    let mut mac = HmacSha256::new_from_slice(&auth_key).ok()?;
    mac.update(&encrypted[..mac_off]);
    let computed = mac.finalize().into_bytes();
    // constant-time comparison
    let mut diff = 0u8;
    for (a, b) in computed.iter().zip(expected_mac.iter()) {
        diff |= a ^ b;
    }
    if diff != 0 {
        return None;
    }

    let iv_off = payload_len + AES_SALT_LEN * 2;
    let mut iv = [0u8; AES_IV_LEN];
    iv.copy_from_slice(&encrypted[iv_off..iv_off + AES_IV_LEN]);
    let ct = &encrypted[iv_off + AES_IV_LEN..mac_off];

    let mut plain = vec![0u8; ct.len()];
    let decryptor = Aes256CbcDec::new_from_slices(&crypt_key, &iv).ok()?;
    let plen = decryptor
        .decrypt_padded_b2b_mut::<aes::cipher::block_padding::Pkcs7>(ct, &mut plain)
        .ok()?
        .len();
    plain.truncate(plen);
    Some(plain)
}

// ---------------------------------------------------------------------------
// KeyGenerator
// ---------------------------------------------------------------------------

fn get_random_key() -> String {
    let mut s = String::with_capacity(24);
    let mut rng = rand::thread_rng();
    for _ in 0..24 {
        s.push(char::from_u32(rng.gen_range(48..122)).unwrap());
    }
    s
}

fn get_ket_with_salt(random_key: &str, salt: &str) -> String {
    format!("{random_key}{salt}")
}

// ---------------------------------------------------------------------------
// Config / salt
// ---------------------------------------------------------------------------

fn compute_salt(salt_input: &str) -> String {
    let digest = Md5::digest(salt_input.as_bytes());
    let hex: String = digest.iter().map(|b| format!("{b:02x}")).collect();
    hex.chars().take(8).collect()
}

// ---------------------------------------------------------------------------
// SimpleStringEncipher
// ---------------------------------------------------------------------------

pub fn encrypt(plain: &str, salt_input: &str, secondary_key: Option<&str>) -> Result<String, String> {
    let salt = compute_salt(salt_input);
    let now = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap_or_default();
    let secs = (now.as_secs() % 60) as u32; // DateTime.Now.Second
    let millis = (now.subsec_millis()) as u32; // DateTime.Now.Millisecond
    let augmented = format!("{plain}{SEPARATOR}{secs}{millis}");

    let random_key = get_random_key();
    let ket_with_salt = get_ket_with_salt(&random_key, &salt);
    let bytes = aes_encrypt_with_password(augmented.as_bytes(), &ket_with_salt)?;
    let text = BASE64.encode(bytes);

    // interleave: first 48 output chars even<-text, odd<-random_key
    let mut result = String::with_capacity(text.len());
    let key_chars: Vec<char> = random_key.chars().collect();
    let text_chars: Vec<char> = text.chars().collect();
    let mut ti = 0usize;
    let mut ki = 0usize;
    for i in 0..random_key.len() * 2 {
        if i % 2 == 0 {
            if ti < text_chars.len() {
                result.push(text_chars[ti]);
                ti += 1;
            }
        } else if ki < key_chars.len() {
            result.push(key_chars[ki]);
            ki += 1;
        }
    }
    for c in text_chars.into_iter().skip(ti) {
        result.push(c);
    }

    if let Some(sk) = secondary_key {
        if !sk.is_empty() {
            let wrapped = aes_encrypt_with_password(result.as_bytes(), sk)?;
            result = format!("{SECONDARY_FLAG}{}", BASE64.encode(wrapped));
        }
    }
    Ok(result)
}

pub fn decrypt(cipher: &str, salt_input: &str, secondary_key: Option<&str>) -> Option<String> {
    if cipher.is_empty() || cipher.len() <= 64 {
        return None;
    }
    let mut cipher_text = cipher.to_string();
    if cipher_text.starts_with(SECONDARY_FLAG) {
        let sk = secondary_key?;
        if sk.is_empty() {
            return None;
        }
        let inner = cipher_text.trim_start_matches(SECONDARY_FLAG);
        let bytes = BASE64.decode(inner.as_bytes()).ok()?;
        let plain = aes_decrypt_with_password(&bytes, sk)?;
        cipher_text = String::from_utf8(plain).ok()?;
        // After secondary decryption the result should itself be an
        // interleaved primary ciphertext; validate its length for the loop below.
        if cipher_text.len() <= 64 {
            return None;
        }
    }

    let salt = compute_salt(salt_input);
    let random_key_len = 24;
    let mut random_key = String::with_capacity(random_key_len);
    let mut encrypted_message = String::with_capacity(cipher_text.len());
    let chars: Vec<char> = cipher_text.chars().collect();
    let mut i = 0usize;
    for idx in 0..random_key_len * 2 {
        if idx < chars.len() {
            if idx % 2 == 0 {
                encrypted_message.push(chars[idx]);
            } else {
                random_key.push(chars[idx]);
            }
        }
        i = idx + 1;
    }
    for c in chars.iter().skip(i) {
        encrypted_message.push(*c);
    }
    let ket_with_salt = get_ket_with_salt(&random_key, &salt);
    let bytes = BASE64.decode(encrypted_message.as_bytes()).ok()?;
    let plain = aes_decrypt_with_password(&bytes, &ket_with_salt)?;
    let text = String::from_utf8(plain).ok()?;
    if let Some(pos) = text.rfind(SEPARATOR) {
        return Some(text[..pos].to_string());
    }
    Some(text)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn salt_is_md5_first8() {
        assert_eq!(compute_salt("testsalt123"), "0b433ec1");
        assert_eq!(compute_salt("abc"), "90015098"); // md5("abc") = 900150983cd24fb0d6963f7d28e17f72
    }

    #[test]
    fn roundtrip_encrypt_decrypt() {
        let salt = "testsalt123";
        for plain in ["HelloWorld", "1", "", "a long password with spaces 你好世界", "🔥emoji"] {
            let cipher = encrypt(plain, salt, None).unwrap();
            assert!(cipher.len() > 64, "cipher too short for {plain:?}");
            let dec = decrypt(&cipher, salt, None);
            assert_eq!(dec.as_deref(), Some(plain), "roundtrip failed for {plain:?}");
        }
    }

    #[test]
    fn decrypt_wrong_salt_fails() {
        let cipher = encrypt("secret", "saltA", None).unwrap();
        assert_eq!(decrypt(&cipher, "saltB", None), None);
        // A non-wrapped ciphertext ignores the secondary key (matches .NET).
        assert_eq!(decrypt(&cipher, "saltA", Some("x")), Some("secret".to_string()));
    }

    #[test]
    fn secondary_key_roundtrip() {
        let cipher = encrypt("hello", "salt", Some("mykey")).unwrap();
        assert!(cipher.starts_with(SECONDARY_FLAG));
        assert_eq!(decrypt(&cipher, "salt", Some("mykey")).as_deref(), Some("hello"));
        // wrong secondary key fails
        assert_eq!(decrypt(&cipher, "salt", Some("wrong")), None);
        assert_eq!(decrypt(&cipher, "salt", None), None);
    }

    #[test]
    fn interleave_is_reversible_and_length_preserving() {
        // The interleave step of Encrypt must recover the exact random key.
        let salt = "s";
        let cipher = encrypt("payload", salt, None).unwrap();
        let chars: Vec<char> = cipher.chars().collect();
        let mut key = String::new();
        let mut ct = String::new();
        for (i, c) in chars.iter().enumerate() {
            if i < 48 {
                if i % 2 == 1 {
                    key.push(*c);
                } else {
                    ct.push(*c);
                }
            } else {
                ct.push(*c);
            }
        }
        assert_eq!(key.len(), 24, "recovered random key must be 24 chars");
        // all key chars are in the randomKey alphabet [48,122)
        for c in key.chars() {
            let v = c as u32;
            assert!((48..122).contains(&v), "key char {c:?} outside alphabet");
        }
        // the ciphertext part must be valid base64 that decrypts back
        let ket = get_ket_with_salt(&key, &compute_salt(salt));
        let bytes = BASE64.decode(ct.as_bytes()).unwrap();
        let plain = aes_decrypt_with_password(&bytes, &ket).unwrap();
        let text = String::from_utf8(plain).unwrap();
        assert!(text.starts_with("payload"));
    }

    #[test]
    fn dotnet_compatibility_vectors() {
        // Vectors produced by 1Remote.Security 1.1.0 with salt "testsalt123".
        // Rust must decrypt them (this is THE compatibility guarantee for
        // existing stored passwords).
        let salt = "testsalt123";
        let vectors = [
            "3TsnXyPrXxFs7E1cpEj4BfPxA`DmxWQKfyySjPe4GB4v3;9;TqYPb/raLR8FurKFBt5gYP1mbWavnCWgWBslNzKRsLMh3VlBioJsUygUfi7BpwtnbTQuACgmlRZvC9hifaThn7B6wiqaM1L7L95ao8X0M0KaUDLcI2FfgtAscZyDZA==",
            "C5I?FQcJ1SaOoRxlPZ6NCDYvw7zlgbXDacYLo?cDhshHtmitkwuaYSQffYlZJwBO+tycurN3fZrRii56WqvAnX9trKbqAAZ7FdsEyrSyfoivvT4h9bJsP2B//Qr/9AxPPA5QgzS9v+Qz3T/CYGcUZcYxpb9bQCjCyUPWI+gfHlC+aQ==",
            "EZpuFX+_Vb0\\uImQNikorHlvRcz<I7t<kwluhNQxr7aDXIeyRIej8JWTAWDCPuln2oRZt25IHWOVQK6HhKo0kBrR6PdCRmUS2gCxaSnn8N1yn/6E3X9ODetKvgWxwR/pM1tqz9Hc9KT953b3ukXumIuev+7ZPON2/nH3oMBC6GzJmw==",
        ];
        for (i, v) in vectors.iter().enumerate() {
            let dec = decrypt(v, salt, None);
            assert_eq!(dec.as_deref(), Some("CompatibilityVector42"), "vector {i} failed");
        }
        // Secondary-key vector
        let sec = "Secondary+Encryption+Flag-->epjng1Zqc4sXJLhCZnAYe1yPkhc/lzxJe2gxM2k5m9oQiGWLrUGQx6oTAVHu725b3GdXy1uapRiuTW8R0CLVzH6mOLxSTW8zNJV0AQWK+aAmcHk9mICp0um3uZxMf0/zhmazfDpdmw7wVxBXvdRbfw1rX7CIVVMiM3li8vsveVPvp7GOwqRjwCDG5b62n0QnBv2pCuiG4Cl8lov8DHVhZIhLA7Rcdk4Brixt1Gt0R0JMvwDOb90EXS4h7Wf93ZIQvi9AhpgwBe25Z4Yb4wbnXiiob8hVRW06lXd4gxHof1k=";
        let dec = decrypt(sec, salt, Some("secondary"));
        assert_eq!(dec.as_deref(), Some("SecKeyPayload"));
    }

    #[test]
    fn empty_and_short_inputs_handle_gracefully() {
        // Encrypt of "" still yields a valid decryptable string.
        let cipher = encrypt("", "salt", None).unwrap();
        assert_eq!(decrypt(&cipher, "salt", None).as_deref(), Some(""));
        // Decrypt of empty / too-short cipher returns None, never panics.
        assert_eq!(decrypt("", "salt", None), None);
        assert_eq!(decrypt("short", "salt", None), None);
    }
}
