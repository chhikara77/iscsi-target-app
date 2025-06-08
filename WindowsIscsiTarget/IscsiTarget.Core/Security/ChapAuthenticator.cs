using System;
using System.Security.Cryptography;
using System.Text;

namespace IscsiTarget.Core.Security
{
    /// <summary>
    /// Handles Challenge-Handshake Authentication Protocol (CHAP) logic.
    /// </summary>
    public static class ChapAuthenticator
    {
        // Based on RFC 1994: PPP Challenge Handshake Authentication Protocol (CHAP)

        /// <summary>
        /// Generates a CHAP challenge.
        /// </summary>
        /// <param name="identifier">The identifier for this challenge.</param>
        /// <param name="challengeValue">The generated challenge value (output).</param>
        /// <param name="name">The name of the authenticator (e.g., target name).</param>
        /// <returns>A byte array representing the challenge PDU part.</returns>
        public static byte[] GenerateChallenge(byte identifier, out byte[] challengeValue, string name)
        {
            // For simplicity, using a random challenge value. Real implementations might have more structure.
            challengeValue = new byte[16]; // Common challenge length
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(challengeValue);
            }

            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            
            // This is a simplified representation. The actual PDU construction will be more complex
            // and handled by PDU classes. This method focuses on CHAP-specific data.
            // Format: Value-Size (1 byte) | Value (variable) | Name (variable)
            // Here, we are just returning the core components that would go into a CHAP Challenge PDU.
            
            // Placeholder: Actual PDU construction would involve more fields and structure.
            // This method is intended to provide the CHAP-specific parts.
            // For now, let's assume the caller will assemble the PDU.
            // The PDU would contain: Identifier, Challenge Value, Name
            return challengeValue; // Returning only challenge value for now, to be integrated into PDU.
        }

        /// <summary>
        /// Verifies a CHAP response.
        /// </summary>
        /// <param name="identifier">The identifier from the original challenge.</param>
        /// <param name="challengeValue">The original challenge value sent to the peer.</param>
        /// <param name="responseValue">The response value received from the peer.</param>
        /// <param name="secret">The shared secret for the peer.</param>
        /// <returns>True if the response is valid, false otherwise.</returns>
        public static bool VerifyResponse(byte identifier, byte[] challengeValue, byte[] responseValue, string secret)
        {
            if (responseValue == null || challengeValue == null || secret == null)
            {
                return false;
            }

            byte[] secretBytes = Encoding.ASCII.GetBytes(secret);
            
            // MD5 hash (as per RFC 1994 for CHAP)
            // Hash = MD5(ID || Secret || Challenge)
            using (MD5 md5 = MD5.Create())
            {
                byte[] idByte = { identifier };
                byte[] concat = new byte[idByte.Length + secretBytes.Length + challengeValue.Length];
                Buffer.BlockCopy(idByte, 0, concat, 0, idByte.Length);
                Buffer.BlockCopy(secretBytes, 0, concat, idByte.Length, secretBytes.Length);
                Buffer.BlockCopy(challengeValue, 0, concat, idByte.Length + secretBytes.Length, challengeValue.Length);

                byte[] expectedResponse = md5.ComputeHash(concat);

                if (responseValue.Length != expectedResponse.Length)
                {
                    return false;
                }

                for (int i = 0; i < expectedResponse.Length; i++)
                {
                    if (responseValue[i] != expectedResponse[i])
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Generates a CHAP response (used by an initiator, or for mutual CHAP by target).
        /// </summary>
        /// <param name="identifier">The identifier from the challenge.</param>
        /// <param name="challengeValue">The challenge value received.</param>
        /// <param name="secret">The shared secret.</param>
        /// <param name="name">The name of the responder (e.g., initiator name).</param>
        /// <returns>A byte array representing the response value (MD5 hash).</returns>
        public static byte[] GenerateResponse(byte identifier, byte[] challengeValue, string secret, out string nameUsed)
        {
            // Name used in response is typically the entity's name being authenticated
            nameUsed = ""; // Placeholder, will be set by caller (e.g. initiator IQN)
            byte[] secretBytes = Encoding.ASCII.GetBytes(secret);
            
            using (MD5 md5 = MD5.Create())
            {
                byte[] idByte = { identifier };
                byte[] concat = new byte[idByte.Length + secretBytes.Length + challengeValue.Length];
                Buffer.BlockCopy(idByte, 0, concat, 0, idByte.Length);
                Buffer.BlockCopy(secretBytes, 0, concat, idByte.Length, secretBytes.Length);
                Buffer.BlockCopy(challengeValue, 0, concat, idByte.Length + secretBytes.Length, challengeValue.Length);

                return md5.ComputeHash(concat);
            }
        }
    }
}