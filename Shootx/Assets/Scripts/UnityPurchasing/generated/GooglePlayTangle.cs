// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("l7O6l9uFdmC3VU3Ste6qsy1pPHnU841A31lDmi9UWYPym6yQ/p8fcgfs+318oltS5ZXNlSdpJGO1ZrzSa7e89tfPbsFFykQg9WljXM/f9+MBj5jCLjEyisXFSyJy9awYCoCr4h0OvmTnKu0tt1sPnKgazF/6BFW3WNvV2upY29DYWNvb2nQgy5QwrBjfIYnxMXyqn88RgqMc4Hb9r3ijTc3uoQKEHzZnxzn5QRqO9BP3aIp+SiTc3PRRYz7Jlh433s6AV4N5XC45Is8G6QperHSl84xZBi8ggCaM4YDnLluOWQ8BIP4px810SllYVPfpma30Y4VQWYV4rwqyzLVcn9/7UirqWNv46tfc0/Bcklwt19vb29/a2WVy4fNXyY49B9jZ29rb");
        private static int[] order = new int[] { 5,11,12,8,4,9,11,12,11,11,13,13,12,13,14 };
        private static int key = 218;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
